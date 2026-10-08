using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    // Nur die Darstellung ersetzen; native Scrollbereiche und Eingaben bleiben erhalten.
    internal static class StudioScrollChrome
    {
        static readonly ConditionalWeakTable<Control, Window> windows = new ConditionalWeakTable<Control, Window>();

        internal static void Attach(Control control)
        {
            if (!(control is ScrollableControl) || control is Form)
                if (!(control is TextBoxBase || control is ListBox || control is TreeView || control is ListView || control is ScrollBar || control is UpDownBase)) return;
            Window existing;
            if (windows.TryGetValue(control, out existing)) return;
            var window = new Window(control, control is ScrollBar, HasBorder(control));
            windows.Add(control, window);
            control.HandleCreated += delegate { window.Bind(control.Handle); };
            control.HandleDestroyed += delegate { window.Unbind(); };
            control.Disposed += delegate { window.Dispose(); };
            control.GotFocus += delegate { window.PaintChrome(); };
            control.LostFocus += delegate { window.PaintChrome(); };
            control.EnabledChanged += delegate { window.PaintChrome(); };
            var scrollable = control as ScrollableControl;
            if (scrollable != null)
                scrollable.Layout += delegate { window.PaintChrome(); };
            if (control.IsHandleCreated) window.Bind(control.Handle);
        }

        internal static void RefreshAncestors(Control control)
        {
            if (!StudioUx.CanPaint(control) || SystemInformation.HighContrast) return;
            for (Control current = control.Parent; current != null; current = current.Parent)
            {
                Window window;
                if (current is ScrollableControl && windows.TryGetValue(current, out window)) window.PaintChrome();
            }
        }

        static void RefreshFocusedAncestors()
        {
            IntPtr focus = GetFocus();
            if (focus != IntPtr.Zero) RefreshAncestors(Control.FromChildHandle(focus));
        }

        static bool HasBorder(Control control)
        {
            var text = control as TextBoxBase;
            if (text != null) return text.BorderStyle != BorderStyle.None;
            var list = control as ListBox;
            if (list != null) return list.BorderStyle != BorderStyle.None;
            return control is UpDownBase;
        }

        internal static Window Popup(IntPtr handle, Control owner)
        {
            var window = new Window(owner, false, true);
            window.Bind(handle);
            return window;
        }

        internal sealed class Window : NativeWindow, IDisposable
        {
            readonly Control owner;
            readonly bool scrollbarControl, border;
            readonly System.Collections.Generic.List<Control> ancestors = new System.Collections.Generic.List<Control>();
            readonly EventHandler ancestorChanged;
            readonly EventHandler ancestorStyleChanged;
            readonly LayoutEventHandler ancestorLayout;
            Timer tracking;
            BufferedGraphicsContext horizontalBuffer, verticalBuffer;
            ChromeLayer verticalLayer, horizontalLayer, cornerLayer;
            bool painting, disposed, pointerOnBar, refreshPending;
            int pointerPart, nativeThumbObject;

            internal Window(Control owner, bool scrollbarControl, bool border)
            {
                this.owner = owner;
                this.scrollbarControl = scrollbarControl;
                this.border = border;
                ancestorChanged = delegate { UpdateAncestors(); PaintChrome(); };
                ancestorStyleChanged = delegate
                {
                    PaintChrome();
                    if (refreshPending || disposed || !owner.IsHandleCreated || owner.IsDisposed) return;
                    refreshPending = true;
                    owner.BeginInvoke((MethodInvoker)delegate { refreshPending = false; PaintChrome(); });
                };
                ancestorLayout = delegate { PaintChrome(); };
            }

            internal void Bind(IntPtr handle)
            {
                if (disposed || Handle == handle) return;
                Unbind();
                if (handle != IntPtr.Zero) { AssignHandle(handle); UpdateAncestors(); }
            }

            internal void Unbind()
            {
                if (tracking != null) tracking.Stop();
                nativeThumbObject = 0;
                refreshPending = false;
                foreach (Control ancestor in ancestors)
                {
                    ancestor.LocationChanged -= ancestorChanged;
                    ancestor.SizeChanged -= ancestorChanged;
                    ancestor.VisibleChanged -= ancestorChanged;
                    ancestor.ParentChanged -= ancestorChanged;
                    ancestor.Layout -= ancestorLayout;
                    ancestor.StyleChanged -= ancestorStyleChanged;
                }
                ancestors.Clear();
                DisposeLayer(ref verticalLayer);
                DisposeLayer(ref horizontalLayer);
                DisposeLayer(ref cornerLayer);
                pointerOnBar = false;
                pointerPart = 0;
                if (horizontalBuffer != null) { horizontalBuffer.Dispose(); horizontalBuffer = null; }
                if (verticalBuffer != null) { verticalBuffer.Dispose(); verticalBuffer = null; }
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }

            void UpdateAncestors()
            {
                var next = new System.Collections.Generic.List<Control>();
                for (Control current = owner; current != null; current = current.Parent) next.Add(current);
                bool same = next.Count == ancestors.Count;
                if (same)
                    for (int i = 0; i < next.Count; i++) if (next[i] != ancestors[i]) { same = false; break; }
                if (same) return;
                foreach (Control ancestor in ancestors)
                {
                    ancestor.LocationChanged -= ancestorChanged;
                    ancestor.SizeChanged -= ancestorChanged;
                    ancestor.VisibleChanged -= ancestorChanged;
                    ancestor.ParentChanged -= ancestorChanged;
                    ancestor.Layout -= ancestorLayout;
                    ancestor.StyleChanged -= ancestorStyleChanged;
                }
                ancestors.Clear();
                foreach (Control ancestor in next)
                {
                    ancestors.Add(ancestor);
                    ancestor.LocationChanged += ancestorChanged;
                    ancestor.SizeChanged += ancestorChanged;
                    ancestor.VisibleChanged += ancestorChanged;
                    ancestor.ParentChanged += ancestorChanged;
                    ancestor.Layout += ancestorLayout;
                    ancestor.StyleChanged += ancestorStyleChanged;
                }
            }

            void Track()
            {
                if (SystemInformation.HighContrast || !StudioUx.CanPaint(owner)) return;
                nativeThumbObject = 0;
                Point pointer = Cursor.Position;
                foreach (int objectId in scrollbarControl ? new[] { -4 } : new[] { -5, -6 })
                {
                    ScrollbarInfo info;
                    if (!ReadBar(objectId, out info) || !info.Rect.Contains(pointer)) continue;
                    bool vertical = objectId == -5 || objectId == -4 && owner is VScrollBar;
                    int coordinate = vertical ? pointer.Y - info.Rect.Top : pointer.X - info.Rect.Left;
                    if (coordinate >= info.ThumbTop && coordinate < info.ThumbBottom) nativeThumbObject = objectId;
                    break;
                }
                if (tracking == null)
                {
                    tracking = new Timer { Interval = 16 };
                    tracking.Tick += delegate
                    {
                        if (Handle == IntPtr.Zero || !StudioUx.CanPaint(owner) || !IsWindowVisible(Handle)) { tracking.Stop(); return; }
                        PaintChrome();
                        if (Control.MouseButtons == MouseButtons.None) tracking.Stop();
                    };
                }
                tracking.Start();
            }

            int PointerPart()
            {
                if (Handle == IntPtr.Zero) return 0;
                ScrollbarInfo info;
                Point pointer = Cursor.Position;
                if (ReadBar(scrollbarControl ? -4 : -5, out info) && info.Rect.Contains(pointer))
                    return Part(info, pointer, scrollbarControl ? owner is VScrollBar : true, 10);
                if (!scrollbarControl && ReadBar(-6, out info) && info.Rect.Contains(pointer)) return Part(info, pointer, false, 20);
                return 0;
            }

            static int Part(ScrollbarInfo info, Point pointer, bool vertical, int prefix)
            {
                int coordinate = vertical ? pointer.Y - info.Rect.Top : pointer.X - info.Rect.Left;
                int length = vertical ? info.Rect.Height : info.Rect.Width;
                return prefix + (coordinate < info.LineButton ? 1 : coordinate >= length - info.LineButton ? 3 : 2);
            }

            protected override void WndProc(ref Message message)
            {
                int id = message.Msg;
                bool live = !disposed && Handle != IntPtr.Zero && !SystemInformation.HighContrast && StudioUx.CanPaint(owner);
                if (live && id == 0x85 && OwnsNonClient())
                {
                    PaintChrome();
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (live && (scrollbarControl || owner is CheckedListBox))
                {
                    if (id == 0x14) { message.Result = (IntPtr)1; return; }
                    if (id == 0xF)
                    {
                        painting = true;
                        try
                        {
                            PaintClientBuffer(Handle, delegate(Graphics graphics)
                            {
                                if (scrollbarControl) Draw(graphics);
                                else DrawCheckedItems(graphics, (CheckedListBox)owner);
                            }, true, Rectangle.Empty);
                        }
                        finally { painting = false; }
                        PaintChrome();
                        message.Result = IntPtr.Zero;
                        return;
                    }
                }
                bool nativePress = live && (id == 0xA1 || scrollbarControl && id == 0x201);
                if (nativePress) Track();
                try { base.WndProc(ref message); }
                finally
                {
                    if (nativePress)
                    {
                        nativeThumbObject = 0;
                        PaintChrome();
                    }
                }
                if (disposed || Handle == IntPtr.Zero || SystemInformation.HighContrast) { HideLayers(); return; }
                if (!StudioUx.CanPaint(owner) && id != 0x317 && id != 0x318)
                {
                    if (tracking != null) tracking.Stop();
                    HideLayers();
                    return;
                }
                bool pointerChanged = false;
                if (id == 0xA0 || id == 0x2A2 || id == 0x200 && (scrollbarControl || pointerOnBar))
                {
                    int part = PointerPart();
                    pointerChanged = part != pointerPart;
                    pointerPart = part;
                    pointerOnBar = part != 0;
                }
                if (id == 0x85 || id == 0xF || id == 5 || id == 0x47 || id == 0x18 || id == 0xA || id == 0x1A || id == 0x114 || id == 0x115 || id == 0x20A || id == 0x20E || id == 0xA1 || scrollbarControl && id == 0x202 || id == 7 || id == 8 || id == 0xB6 || pointerChanged)
                    PaintChrome();
                if (id == 0x317 && message.WParam != IntPtr.Zero)
                    using (var graphics = Graphics.FromHdc(message.WParam))
                    {
                        Draw(graphics);
                        if (owner is CheckedListBox)
                        {
                            NativeRect bounds;
                            Point clientOrigin = Point.Empty;
                            if (GetWindowRect(Handle, out bounds) && ClientToScreen(Handle, ref clientOrigin))
                            {
                                graphics.TranslateTransform(clientOrigin.X - bounds.Left, clientOrigin.Y - bounds.Top);
                                DrawCheckedItems(graphics, (CheckedListBox)owner);
                            }
                        }
                    }
                if (id == 0xF || id == 0x202 || id == 0x200 && Control.MouseButtons != MouseButtons.None || id == 0x100 || id == 0x101 || id == 0x115 || id == 0x20A || id == 0x20E || id == 7 || id == 8 || id == 0xA || id == 0x1A)
                    PaintCheckedItems();
                if (id == 0x318 && message.WParam != IntPtr.Zero && owner is CheckedListBox)
                    using (var graphics = Graphics.FromHdc(message.WParam)) DrawCheckedItems(graphics, (CheckedListBox)owner);
                if (id == 0x114 || id == 0x115 || id == 0x20A || id == 0x20E)
                    RefreshFocusedAncestors();
            }

            internal void PaintChrome()
            {
                if (disposed || Handle == IntPtr.Zero || SystemInformation.HighContrast || !StudioUx.CanPaint(owner) || !IsWindowVisible(Handle)) { HideLayers(); return; }
                if (painting) return;
                painting = true;
                IntPtr handle = Handle;
                IntPtr dc = GetWindowDC(handle);
                try
                {
                    if (dc != IntPtr.Zero)
                    {
                        NativeRect bounds, client;
                        if (!GetWindowRect(handle, out bounds) || !GetClientRect(handle, out client)) return;
                        Point origin = Point.Empty;
                        if (!ClientToScreen(handle, ref origin)) return;
                        Rectangle full = new Rectangle(0, 0, Math.Max(1, bounds.Right - bounds.Left), Math.Max(1, bounds.Bottom - bounds.Top));
                        Rectangle clientArea = new Rectangle(origin.X - bounds.Left, origin.Y - bounds.Top, client.Right, client.Bottom);
                        Rectangle[] areas = scrollbarControl ? new[] { full } : NonClientAreas(full.Size, clientArea);
                        if (areas.Length == 0) { HideLayers(); return; }
                        Size horizontalSize = Size.Empty, verticalSize = Size.Empty;
                        foreach (Rectangle area in areas)
                        {
                            if (area.Width == full.Width)
                                horizontalSize = new Size(Math.Max(horizontalSize.Width, area.Width), Math.Max(horizontalSize.Height, area.Height));
                            else
                                verticalSize = new Size(Math.Max(verticalSize.Width, area.Width), Math.Max(verticalSize.Height, area.Height));
                        }
                        ScrollbarInfo vertical, horizontal = new ScrollbarInfo();
                        bool hasVertical = ReadBar(scrollbarControl ? -4 : -5, out vertical);
                        bool hasHorizontal = !scrollbarControl && ReadBar(-6, out horizontal);
                        int saved = SaveDC(dc);
                        try
                        {
                            if (!scrollbarControl)
                                ExcludeClipRect(dc, clientArea.Left, clientArea.Top, clientArea.Right, clientArea.Bottom);
                            using (var target = Graphics.FromHdc(dc))
                            {
                                foreach (Rectangle area in areas)
                                    using (var buffer = FrameBuffer(area.Width == full.Width, area.Width == full.Width ? horizontalSize : verticalSize).Allocate(target, area))
                                    {
                                        buffer.Graphics.ResetTransform();
                                        buffer.Graphics.TranslateTransform(-area.Left, -area.Top);
                                        buffer.Graphics.SetClip(area);
                                        buffer.Graphics.Clear(DarkTheme.Panel);
                                        DrawFrame(buffer.Graphics, bounds, client, origin, vertical, horizontal, hasVertical, hasHorizontal);
                                        buffer.Render(target);
                                    }
                            }
                        }
                        finally { if (saved != 0) RestoreDC(dc, saved); }
                        UpdateLayer(ref verticalLayer, hasVertical ? vertical.Rect : Rectangle.Empty);
                        UpdateLayer(ref horizontalLayer, hasHorizontal ? horizontal.Rect : Rectangle.Empty);
                        UpdateLayer(ref cornerLayer, hasVertical && hasHorizontal ? Rectangle.FromLTRB(vertical.Rect.Left, horizontal.Rect.Top, vertical.Rect.Right, horizontal.Rect.Bottom) : Rectangle.Empty);
                    }
                }
                finally
                {
                    if (dc != IntPtr.Zero) ReleaseDC(handle, dc);
                    painting = false;
                }
            }

            void UpdateLayer(ref ChromeLayer layer, Rectangle area)
            {
                if (area.IsEmpty)
                {
                    if (layer != null) layer.Hide();
                    return;
                }
                if (layer == null) layer = new ChromeLayer(this);
                layer.Update(area);
            }

            void HideLayers()
            {
                if (verticalLayer != null) verticalLayer.Hide();
                if (horizontalLayer != null) horizontalLayer.Hide();
                if (cornerLayer != null) cornerLayer.Hide();
            }

            static void DisposeLayer(ref ChromeLayer layer)
            {
                if (layer != null) { layer.Dispose(); layer = null; }
            }

            // Native Scrollvorgaenge zeichnen intern ohne WM_NCPAINT; die Eingaben bleiben im Originalfenster.
            sealed class ChromeLayer : NativeWindow, IDisposable
            {
                static readonly System.Collections.Generic.List<WeakReference> liveLayers = new System.Collections.Generic.List<WeakReference>();
                static Timer opacityRecovery;
                readonly Window chrome;
                readonly WeakReference registration;
                IntPtr parent;
                bool drawing;
                Rectangle[] excludedAreas = new Rectangle[0];
                Size regionSize;
                double rootOpacity = 1;
                bool highContrast;

                internal ChromeLayer(Window chrome)
                {
                    this.chrome = chrome;
                    registration = new WeakReference(this);
                    if (liveLayers.Count == 0) Application.Idle += RefreshOpacity;
                    liveLayers.Add(registration);
                }

                static void RefreshOpacity(object sender, EventArgs args)
                {
                    bool recover = false;
                    foreach (WeakReference reference in liveLayers.ToArray())
                    {
                        var layer = reference.Target as ChromeLayer;
                        if (layer == null) { liveLayers.Remove(reference); continue; }
                        double opacity = layer.AncestorOpacity();
                        bool contrast = SystemInformation.HighContrast;
                        if (opacity < 1 && !contrast && !layer.chrome.disposed && layer.chrome.Handle != IntPtr.Zero && StudioUx.CanPaint(layer.chrome.owner) && IsWindowVisible(layer.chrome.Handle)) recover = true;
                        if (layer.rootOpacity == opacity && layer.highContrast == contrast) continue;
                        layer.rootOpacity = opacity;
                        layer.highContrast = contrast;
                        layer.chrome.PaintChrome();
                    }
                    if (!recover && opacityRecovery != null) opacityRecovery.Stop();
                    if (liveLayers.Count == 0) StopRecovery();
                }

                static void StartRecovery()
                {
                    if (opacityRecovery == null)
                    {
                        opacityRecovery = new Timer { Interval = 50 };
                        opacityRecovery.Tick += RefreshOpacity;
                    }
                    opacityRecovery.Start();
                }

                static void StopRecovery()
                {
                    Application.Idle -= RefreshOpacity;
                    if (opacityRecovery != null) { opacityRecovery.Dispose(); opacityRecovery = null; }
                }

                double AncestorOpacity()
                {
                    double opacity = 1;
                    for (Control current = chrome.owner; current != null; current = current.Parent)
                    {
                        Form form = current as Form;
                        if (form != null) opacity = Math.Min(opacity, form.Opacity);
                    }
                    return opacity;
                }

                internal void Update(Rectangle screenArea)
                {
                    IntPtr source = chrome.Handle;
                    rootOpacity = AncestorOpacity();
                    highContrast = SystemInformation.HighContrast;
                    if (rootOpacity < 1 || highContrast)
                    {
                        if (rootOpacity < 1 && !highContrast) StartRecovery();
                        Hide();
                        return;
                    }
                    bool isChild = (GetWindowLong(source, -16) & 0x40000000) != 0;
                    if (isChild)
                        for (Control ancestor = chrome.owner.Parent; ancestor != null; ancestor = ancestor.Parent)
                            screenArea.Intersect(ancestor.RectangleToScreen(ancestor.ClientRectangle));
                    if (screenArea.Width <= 0 || screenArea.Height <= 0) { Hide(); return; }
                    IntPtr nextParent = GetAncestor(source, 2);
                    if (nextParent == IntPtr.Zero) { Hide(); return; }
                    if (Handle != IntPtr.Zero && parent != nextParent) DestroyHandle();
                    bool created = Handle == IntPtr.Zero;
                    if (Handle == IntPtr.Zero)
                    {
                        parent = nextParent;
                        regionSize = Size.Empty;
                        excludedAreas = new Rectangle[0];
                        CreateHandle(new CreateParams
                        {
                            Parent = parent,
                            Style = unchecked((int)0x80000000),
                            ExStyle = 0x080000A0,
                            X = 0, Y = 0, Width = 1, Height = 1
                        });
                    }
                    if (!UpdateRegion(screenArea, isChild)) { Hide(); return; }
                    IntPtr after = IntPtr.Zero;
                    if (created)
                    {
                        // Neue Flächen direkt über dem Besitzer, unter bereits darüberliegenden Fenstern einfügen.
                        after = GetWindow(parent, 3);
                        if (after == Handle) after = GetWindow(after, 3);
                    }
                    if (!SetWindowPos(Handle, after, screenArea.Left, screenArea.Top, screenArea.Width, screenArea.Height, created ? 0x0050u : 0x0054u))
                    {
                        Hide();
                        if (created) DestroyHandle();
                        return;
                    }
                    Paint(false);
                }

                bool UpdateRegion(Rectangle screenArea, bool isChild)
                {
                    var excluded = new System.Collections.Generic.List<Rectangle>();
                    if (isChild)
                        for (Control current = chrome.owner; current.Parent != null; current = current.Parent)
                        {
                            Control container = current.Parent;
                            int index = container.Controls.GetChildIndex(current, false);
                            for (int i = 0; i < index; i++)
                            {
                                Control sibling = container.Controls[i];
                                if (!sibling.Visible || sibling.IsDisposed) continue;
                                Rectangle overlap = Rectangle.Intersect(screenArea, container.RectangleToScreen(sibling.Bounds));
                                if (overlap.Width <= 0 || overlap.Height <= 0) continue;
                                overlap.Offset(-screenArea.Left, -screenArea.Top);
                                excluded.Add(overlap);
                            }
                        }
                    bool same = regionSize == screenArea.Size && excludedAreas.Length == excluded.Count;
                    if (same)
                        for (int i = 0; i < excluded.Count; i++) if (excludedAreas[i] != excluded[i]) { same = false; break; }
                    if (same) return true;
                    if (excluded.Count == 0)
                    {
                        if (SetWindowRgn(Handle, IntPtr.Zero, true) == 0) return false;
                    }
                    else
                    {
                        IntPtr region = CreateRectRgn(0, 0, screenArea.Width, screenArea.Height);
                        if (region == IntPtr.Zero) return false;
                        try
                        {
                            foreach (Rectangle area in excluded)
                            {
                                IntPtr cut = CreateRectRgn(area.Left, area.Top, area.Right, area.Bottom);
                                if (cut == IntPtr.Zero) return false;
                                try { if (CombineRgn(region, region, cut, 4) == 0) return false; }
                                finally { DeleteObject(cut); }
                            }
                            if (SetWindowRgn(Handle, region, true) == 0) return false;
                            region = IntPtr.Zero;
                        }
                        finally { if (region != IntPtr.Zero) DeleteObject(region); }
                    }
                    regionSize = screenArea.Size;
                    excludedAreas = excluded.ToArray();
                    return true;
                }

                void Paint(bool beginPaint)
                {
                    if (drawing || Handle == IntPtr.Zero || chrome.disposed || chrome.Handle == IntPtr.Zero) return;
                    drawing = true;
                    try
                    {
                        PaintClientBuffer(Handle, delegate(Graphics graphics)
                        {
                            NativeRect source, layer;
                            if (!GetWindowRect(chrome.Handle, out source) || !GetWindowRect(Handle, out layer)) return;
                            graphics.TranslateTransform(source.Left - layer.Left, source.Top - layer.Top);
                            chrome.Draw(graphics);
                        }, beginPaint, Rectangle.Empty);
                    }
                    finally { drawing = false; }
                }

                internal void Hide() { if (Handle != IntPtr.Zero) ShowWindow(Handle, 0); }

                protected override void WndProc(ref Message message)
                {
                    if (message.Msg == 0x84) { message.Result = (IntPtr)(-1); return; }
                    if (message.Msg == 0x21) { message.Result = (IntPtr)3; return; }
                    if (message.Msg == 0x14) { message.Result = (IntPtr)1; return; }
                    if (message.Msg == 0xF) { Paint(true); message.Result = IntPtr.Zero; return; }
                    if ((message.Msg == 0x317 || message.Msg == 0x318) && message.WParam != IntPtr.Zero)
                    {
                        using (var graphics = Graphics.FromHdc(message.WParam))
                        {
                            NativeRect source, layer;
                            if (GetWindowRect(chrome.Handle, out source) && GetWindowRect(Handle, out layer))
                            {
                                graphics.TranslateTransform(source.Left - layer.Left, source.Top - layer.Top);
                                chrome.Draw(graphics);
                            }
                        }
                        message.Result = IntPtr.Zero;
                        return;
                    }
                    base.WndProc(ref message);
                }

                public void Dispose()
                {
                    liveLayers.Remove(registration);
                    if (liveLayers.Count == 0) StopRecovery();
                    if (Handle != IntPtr.Zero) DestroyHandle();
                }
            }

            BufferedGraphicsContext FrameBuffer(bool horizontal, Size size)
            {
                BufferedGraphicsContext context;
                if (horizontal)
                {
                    if (horizontalBuffer == null) horizontalBuffer = new BufferedGraphicsContext();
                    context = horizontalBuffer;
                }
                else
                {
                    if (verticalBuffer == null) verticalBuffer = new BufferedGraphicsContext();
                    context = verticalBuffer;
                }
                Size maximum = new Size(Math.Max(1, size.Width) + 1, Math.Max(1, size.Height) + 1);
                if (context.MaximumBuffer != maximum) context.MaximumBuffer = maximum;
                return context;
            }

            bool OwnsNonClient()
            {
                if (scrollbarControl) return false;
                if (border) return true;
                ScrollbarInfo info;
                return ReadBar(-5, out info) || ReadBar(-6, out info);
            }

            void Draw(Graphics graphics)
            {
                NativeRect bounds;
                if (!GetWindowRect(Handle, out bounds)) return;
                ScrollbarInfo vertical, horizontal = new ScrollbarInfo();
                bool hasVertical = ReadBar(scrollbarControl ? -4 : -5, out vertical);
                bool hasHorizontal = !scrollbarControl && ReadBar(-6, out horizontal);
                NativeRect client = new NativeRect();
                Point clientOrigin = Point.Empty;
                GetClientRect(Handle, out client);
                ClientToScreen(Handle, ref clientOrigin);
                DrawFrame(graphics, bounds, client, clientOrigin, vertical, horizontal, hasVertical, hasHorizontal);
            }

            void DrawFrame(Graphics graphics, NativeRect bounds, NativeRect client, Point clientOrigin, ScrollbarInfo vertical, ScrollbarInfo horizontal, bool hasVertical, bool hasHorizontal)
            {
                Point origin = new Point(bounds.Left, bounds.Top);
                if (border && !scrollbarControl)
                {
                    if (client.Right > 0 && client.Bottom > 0)
                    {
                        var saved = graphics.Save();
                        graphics.ExcludeClip(new Rectangle(clientOrigin.X - origin.X, clientOrigin.Y - origin.Y, client.Right, client.Bottom));
                        using (var brush = new SolidBrush(DarkTheme.Panel)) graphics.FillRectangle(brush, 0, 0, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
                        using (var pen = new Pen(owner.Enabled ? owner.ContainsFocus ? DarkTheme.Focus : DarkTheme.Border : DarkTheme.Panel3))
                            graphics.DrawRectangle(pen, 0, 0, Math.Max(0, bounds.Right - bounds.Left - 1), Math.Max(0, bounds.Bottom - bounds.Top - 1));
                        graphics.Restore(saved);
                    }
                }
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle verticalArea = vertical.Rect;
                verticalArea.Offset(-origin.X, -origin.Y);
                Rectangle horizontalArea = horizontal.Rect;
                horizontalArea.Offset(-origin.X, -origin.Y);
                if (hasVertical && graphics.IsVisible(verticalArea)) DrawBar(graphics, vertical, origin, scrollbarControl ? owner is VScrollBar : true);
                if (hasHorizontal && graphics.IsVisible(horizontalArea)) DrawBar(graphics, horizontal, origin, false);
                if (hasVertical && hasHorizontal)
                {
                    Rectangle corner = Rectangle.FromLTRB(vertical.Rect.Left - origin.X, horizontal.Rect.Top - origin.Y, vertical.Rect.Right - origin.X, horizontal.Rect.Bottom - origin.Y);
                    using (var brush = new SolidBrush(DarkTheme.Panel)) graphics.FillRectangle(brush, corner);
                }
            }

            void PaintCheckedItems()
            {
                var list = owner as CheckedListBox;
                if (list == null || painting || disposed || Handle == IntPtr.Zero || SystemInformation.HighContrast || !StudioUx.CanPaint(list)) return;
                painting = true;
                IntPtr handle = Handle, dc = GetDC(handle);
                try
                {
                    if (dc != IntPtr.Zero)
                    {
                        using (var target = Graphics.FromHdc(dc))
                        using (var buffer = BufferedGraphicsManager.Current.Allocate(target, list.ClientRectangle))
                        {
                            DrawCheckedItems(buffer.Graphics, list);
                            buffer.Render(target);
                        }
                    }
                }
                finally
                {
                    if (dc != IntPtr.Zero) ReleaseDC(handle, dc);
                    painting = false;
                }
            }

            static void DrawCheckedItems(Graphics graphics, CheckedListBox list)
            {
                var saved = graphics.Save();
                graphics.SetClip(list.ClientRectangle);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(list.BackColor)) graphics.FillRectangle(brush, list.ClientRectangle);
                for (int i = list.TopIndex; i < list.Items.Count; i++)
                {
                    Rectangle row = list.GetItemRectangle(i);
                    if (row.Top >= list.ClientSize.Height) break;
                    bool selected = list.SelectedIndex == i;
                    Color background = selected ? DarkTheme.AccentSoft : list.BackColor;
                    using (var brush = new SolidBrush(background)) graphics.FillRectangle(brush, row);
                    int size = Math.Min(StudioSurface.Scale(list, 17), Math.Max(4, row.Height - 4));
                    int inset = StudioSurface.Scale(list, 5);
                    bool right = list.RightToLeft == RightToLeft.Yes;
                    Rectangle glyph = new Rectangle(right ? row.Right - inset - size : row.Left + inset, row.Top + (row.Height - size) / 2, size, size);
                    CheckState state = list.GetItemCheckState(i);
                    using (var shape = StudioSurface.Shape(glyph, StudioSurface.Scale(list, 4)))
                    {
                        using (var brush = new SolidBrush(!list.Enabled ? DarkTheme.Panel : state != CheckState.Unchecked ? DarkTheme.Accent : DarkTheme.Panel2)) graphics.FillPath(brush, shape);
                        using (var pen = new Pen(!list.Enabled ? DarkTheme.Disabled : state != CheckState.Unchecked ? DarkTheme.Accent : DarkTheme.Border)) graphics.DrawPath(pen, shape);
                    }
                    if (state != CheckState.Unchecked)
                        using (var pen = new Pen(list.Enabled ? Color.White : DarkTheme.Disabled, Math.Max(2, size / 7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        {
                            if (state == CheckState.Indeterminate) graphics.DrawLine(pen, glyph.Left + size * .25f, glyph.Top + size * .5f, glyph.Left + size * .75f, glyph.Top + size * .5f);
                            else graphics.DrawLines(pen, new[] { new PointF(glyph.Left + size * .23f, glyph.Top + size * .5f), new PointF(glyph.Left + size * .43f, glyph.Top + size * .7f), new PointF(glyph.Left + size * .77f, glyph.Top + size * .3f) });
                        }
                    int reserve = size + inset * 3;
                    Rectangle text = new Rectangle(right ? row.Left + inset : row.Left + reserve, row.Top, Math.Max(0, row.Width - reserve - inset), row.Height);
                    TextRenderer.DrawText(graphics, list.GetItemText(list.Items[i]), list.Font, text, list.Enabled ? DarkTheme.Fore : DarkTheme.Disabled, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (right ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
                    if (selected && StudioSurface.HasKeyboardFocus(list)) ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(row, -2, -1), DarkTheme.Focus, background);
                }
                graphics.Restore(saved);
            }

            bool ReadBar(int objectId, out ScrollbarInfo info)
            {
                info = new ScrollbarInfo { Size = Marshal.SizeOf(typeof(ScrollbarInfo)), State = new int[6] };
                return GetScrollBarInfo(Handle, objectId, ref info) && (info.State[0] & 0x8000) == 0 && (info.State[0] & 0x10000) == 0 && info.Rect.Width > 0 && info.Rect.Height > 0;
            }

            void DrawBar(Graphics graphics, ScrollbarInfo info, Point origin, bool vertical)
            {
                Rectangle bar = info.Rect;
                int objectId = scrollbarControl ? -4 : vertical ? -5 : -6;
                bool draggingThumb = nativeThumbObject == objectId;
                if (draggingThumb)
                {
                    ScrollInfo scroll = new ScrollInfo { Size = Marshal.SizeOf(typeof(ScrollInfo)), Mask = 0x17 };
                    if (GetScrollInfo(Handle, scrollbarControl ? 2 : vertical ? 1 : 0, ref scroll))
                    {
                        int thumbLength = info.ThumbBottom - info.ThumbTop;
                        int travel = Math.Max(0, (vertical ? bar.Height : bar.Width) - info.LineButton * 2 - thumbLength);
                        long range = Math.Max(0L, (long)scroll.Maximum - scroll.Minimum - Math.Max(0L, (long)scroll.Page - 1));
                        if (range > 0)
                        {
                            long position = Math.Max(0L, Math.Min(range, (long)scroll.TrackPosition - scroll.Minimum));
                            info.ThumbTop = info.LineButton + (int)((position * travel + range / 2) / range);
                            info.ThumbBottom = info.ThumbTop + thumbLength;
                        }
                    }
                }
                Point pointer = Cursor.Position;
                bool enabled = owner.Enabled && (info.State[0] & 1) == 0;
                bool hover = enabled && bar.Contains(pointer);
                bar.Offset(-origin.X, -origin.Y);
                using (var brush = new SolidBrush(DarkTheme.Panel)) graphics.FillRectangle(brush, bar);
                int length = vertical ? bar.Height : bar.Width;
                int arrow = Math.Min(info.LineButton, length / 2);
                Rectangle first = vertical ? new Rectangle(bar.Left, bar.Top, bar.Width, arrow) : new Rectangle(bar.Left, bar.Top, arrow, bar.Height);
                Rectangle last = vertical ? new Rectangle(bar.Left, bar.Bottom - arrow, bar.Width, arrow) : new Rectangle(bar.Right - arrow, bar.Top, arrow, bar.Height);
                Point local = new Point(pointer.X - origin.X, pointer.Y - origin.Y);
                DrawArrow(graphics, first, vertical, false, enabled && (info.State[1] & 1) == 0, local, info.State[1]);
                DrawArrow(graphics, last, vertical, true, enabled && (info.State[5] & 1) == 0, local, info.State[5]);
                if (info.ThumbBottom <= info.ThumbTop || info.ThumbTop < arrow) return;
                int thickness = Math.Max(2, Math.Min(StudioSurface.Scale(owner, 6), (vertical ? bar.Width : bar.Height) - 4));
                Rectangle thumb = vertical ? new Rectangle(bar.Left + (bar.Width - thickness) / 2, bar.Top + info.ThumbTop + 1, thickness, Math.Max(1, info.ThumbBottom - info.ThumbTop - 2))
                    : new Rectangle(bar.Left + info.ThumbTop + 1, bar.Top + (bar.Height - thickness) / 2, Math.Max(1, info.ThumbBottom - info.ThumbTop - 2), thickness);
                bool pressed = draggingThumb || (info.State[3] & 8) != 0;
                Color color = !enabled ? DarkTheme.Panel3 : pressed ? DarkTheme.Cyan : hover ? DarkTheme.Accent : DarkTheme.Border;
                using (var shape = StudioSurface.Shape(thumb, Math.Min(thumb.Width, thumb.Height) / 2))
                using (var brush = new SolidBrush(color)) graphics.FillPath(brush, shape);
            }

            static void DrawArrow(Graphics graphics, Rectangle area, bool vertical, bool forward, bool enabled, Point pointer, int state)
            {
                if (area.Width < 3 || area.Height < 3) return;
                if (enabled && area.Contains(pointer))
                    using (var brush = new SolidBrush((state & 8) != 0 ? DarkTheme.AccentSoft : DarkTheme.Panel3)) graphics.FillRectangle(brush, area);
                float cx = area.Left + area.Width / 2f, cy = area.Top + area.Height / 2f;
                float span = Math.Max(2, Math.Min(area.Width, area.Height) / 5f);
                float direction = forward ? 1 : -1;
                using (var pen = new Pen(enabled ? DarkTheme.Muted : DarkTheme.Border, Math.Max(1, span / 2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    PointF a = vertical ? new PointF(cx - span, cy - direction * span / 2) : new PointF(cx - direction * span / 2, cy - span);
                    PointF b = vertical ? new PointF(cx, cy + direction * span / 2) : new PointF(cx + direction * span / 2, cy);
                    PointF c = vertical ? new PointF(cx + span, cy - direction * span / 2) : new PointF(cx - direction * span / 2, cy + span);
                    graphics.DrawLines(pen, new[] { a, b, c });
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Unbind();
                if (tracking != null) { tracking.Dispose(); tracking = null; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeRect
        {
            internal int Left, Top, Right, Bottom;
            internal Rectangle Rectangle { get { return System.Drawing.Rectangle.FromLTRB(Left, Top, Right, Bottom); } }
        }

        internal static Rectangle[] NonClientAreas(Size window, Rectangle client)
        {
            int left = Math.Max(0, Math.Min(window.Width, client.Left));
            int top = Math.Max(0, Math.Min(window.Height, client.Top));
            int right = Math.Max(left, Math.Min(window.Width, client.Right));
            int bottom = Math.Max(top, Math.Min(window.Height, client.Bottom));
            var areas = new System.Collections.Generic.List<Rectangle>(4);
            if (top > 0) areas.Add(new Rectangle(0, 0, window.Width, top));
            if (left > 0 && bottom > top) areas.Add(new Rectangle(0, top, left, bottom - top));
            if (right < window.Width && bottom > top) areas.Add(new Rectangle(right, top, window.Width - right, bottom - top));
            if (bottom < window.Height) areas.Add(new Rectangle(0, bottom, window.Width, window.Height - bottom));
            return areas.ToArray();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PaintState
        {
            internal IntPtr DC;
            internal int Erase;
            internal NativeRect Paint;
            internal int Restore, Update;
            internal int Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7, Reserved8;
        }

        internal static void PaintClientBuffer(IntPtr handle, Action<Graphics> draw, bool beginPaint, Rectangle excluded)
        {
            PaintState state = new PaintState();
            IntPtr dc = beginPaint ? BeginPaint(handle, out state) : GetDC(handle);
            try
            {
                NativeRect client;
                if (dc == IntPtr.Zero || !GetClientRect(handle, out client) || client.Right <= 0 || client.Bottom <= 0) return;
                int saved = SaveDC(dc);
                try
                {
                    if (!excluded.IsEmpty) ExcludeClipRect(dc, excluded.Left, excluded.Top, excluded.Right, excluded.Bottom);
                    using (var target = Graphics.FromHdc(dc))
                    using (var buffer = BufferedGraphicsManager.Current.Allocate(target, client.Rectangle))
                    {
                        buffer.Graphics.Clear(DarkTheme.Panel2);
                        draw(buffer.Graphics);
                        buffer.Render(target);
                    }
                }
                finally { if (saved != 0) RestoreDC(dc, saved); }
            }
            finally
            {
                if (beginPaint) EndPaint(handle, ref state);
                else if (dc != IntPtr.Zero) ReleaseDC(handle, dc);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ScrollInfo
        {
            internal int Size, Mask, Minimum, Maximum;
            internal uint Page;
            internal int Position, TrackPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ScrollbarInfo
        {
            internal int Size;
            internal NativeRect Bounds;
            internal int LineButton, ThumbTop, ThumbBottom, Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal int[] State;
            internal Rectangle Rect { get { return Bounds.Rectangle; } }
        }

        [DllImport("user32.dll")] static extern bool GetScrollBarInfo(IntPtr window, int objectId, ref ScrollbarInfo info);
        [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr window, int bar, ref ScrollInfo info);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint relation);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr item);
        [DllImport("user32.dll")] static extern IntPtr GetFocus();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr window, out PaintState state);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr window, ref PaintState state);
        [DllImport("gdi32.dll")] static extern int SaveDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool RestoreDC(IntPtr dc, int saved);
        [DllImport("gdi32.dll")] static extern int ExcludeClipRect(IntPtr dc, int left, int top, int right, int bottom);
    }
}
