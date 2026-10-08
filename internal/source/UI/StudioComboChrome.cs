using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioComboChrome
    {
        static readonly ConditionalWeakTable<Control, Window> windows = new ConditionalWeakTable<Control, Window>();
        static readonly ConditionalWeakTable<TabControl, Window> overflowWindows = new ConditionalWeakTable<TabControl, Window>();

        internal static void TabOverflow(TabControl tabs)
        {
            if (!tabs.IsHandleCreated || !StudioUx.CanPaint(tabs)) return;
            IntPtr handle = FindWindowEx(tabs.Handle, IntPtr.Zero, "msctls_updown32", null);
            Window window;
            if (!overflowWindows.TryGetValue(tabs, out window))
            {
                if (handle == IntPtr.Zero) return;
                window = new Window(tabs, true, true);
                overflowWindows.Add(tabs, window);
                tabs.HandleDestroyed += delegate { window.Unbind(); };
                tabs.Disposed += delegate { window.Dispose(); };
            }
            window.BindNative(handle);
            window.PaintChrome();
        }

        internal static void Attach(Control control)
        {
            bool spinner = control.Parent is UpDownBase && !(control is TextBoxBase);
            if (!(control is ComboBox) && !spinner) return;
            Window existing;
            if (windows.TryGetValue(control, out existing)) return;
            var window = new Window(control, spinner);
            windows.Add(control, window);
            control.HandleCreated += delegate { window.Bind(); };
            control.HandleDestroyed += delegate { window.Unbind(); };
            control.Disposed += delegate { window.Dispose(); };
            control.MouseEnter += delegate { window.PaintChrome(); };
            control.MouseLeave += delegate { window.PaintChrome(); };
            control.GotFocus += delegate { window.PaintChrome(); };
            control.LostFocus += delegate { window.PaintChrome(); };
            control.ChangeUICues += delegate { window.PaintChrome(); };
            control.EnabledChanged += delegate { window.PaintChrome(); };
            var combo = control as ComboBox;
            if (combo != null)
            {
                combo.DropDown += delegate { window.BindPopup(); window.PaintChrome(); };
                combo.DropDownClosed += delegate { window.PaintChrome(); };
                combo.SelectedIndexChanged += delegate { window.PaintChrome(); };
                combo.TextChanged += delegate { window.PaintChrome(); };
            }
            if (control.IsHandleCreated) window.Bind();
        }

        sealed class Window : NativeWindow, IDisposable
        {
            readonly Control control;
            readonly bool spinner, overflow;
            StudioScrollChrome.Window popup;
            IntPtr popupHandle;
            bool painting, disposed;
            int pointerState;

            internal Window(Control control, bool spinner, bool overflow = false) { this.control = control; this.spinner = spinner; this.overflow = overflow; }

            internal void BindNative(IntPtr handle)
            {
                if (disposed || Handle == handle) return;
                Unbind();
                if (handle != IntPtr.Zero) AssignHandle(handle);
            }

            internal void Bind()
            {
                if (disposed || !control.IsHandleCreated || Handle == control.Handle) return;
                Unbind();
                AssignHandle(control.Handle);
            }

            internal void BindPopup()
            {
                if (Handle == IntPtr.Zero || SystemInformation.HighContrast) return;
                var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
                if (!GetComboBoxInfo(Handle, ref info) || info.List == IntPtr.Zero || popupHandle == info.List && popup != null && popup.Handle == info.List) return;
                if (popup != null) popup.Dispose();
                popupHandle = info.List;
                popup = StudioScrollChrome.Popup(info.List, control);
            }

            internal void Unbind()
            {
                if (popup != null) { popup.Dispose(); popup = null; popupHandle = IntPtr.Zero; }
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }

            protected override void WndProc(ref Message message)
            {
                int id = message.Msg;
                var combo = control as ComboBox;
                bool custom = spinner || combo != null && combo.DropDownStyle != ComboBoxStyle.Simple;
                bool live = !disposed && Handle != IntPtr.Zero && !SystemInformation.HighContrast && StudioUx.CanPaint(control);
                if (live && custom)
                {
                    if (id == 0x14) { message.Result = (IntPtr)1; return; }
                    if (id == 0xF)
                    {
                        painting = true;
                        try { StudioScrollChrome.PaintClientBuffer(Handle, Draw, true, EditableArea()); }
                        finally { painting = false; }
                        message.Result = IntPtr.Zero;
                        return;
                    }
                }
                base.WndProc(ref message);
                if (disposed || Handle == IntPtr.Zero || SystemInformation.HighContrast) return;
                if (!StudioUx.CanPaint(control) && id != 0x317 && id != 0x318) return;
                bool pointerChanged = false;
                if (id == 0x200 || id == 0x2A3)
                {
                    int state = PointerState();
                    pointerChanged = state != pointerState;
                    pointerState = state;
                }
                if (id == 0xF || id == 0x85 || pointerChanged || id == 0x201 || id == 0x202 || id == 7 || id == 8 || id == 0xA || id == 5 || id == 0xC || id == 0x14F || id == 0x203 || id == 0x111)
                    PaintChrome();
                if ((id == 0x317 || id == 0x318) && message.WParam != IntPtr.Zero)
                    using (var graphics = Graphics.FromHdc(message.WParam)) Draw(graphics);
            }

            internal void PaintChrome()
            {
                if (painting || disposed || Handle == IntPtr.Zero || SystemInformation.HighContrast || !StudioUx.CanPaint(control)) return;
                var combo = control as ComboBox;
                if (combo != null && combo.DropDownStyle == ComboBoxStyle.Simple) return;
                painting = true;
                try
                {
                    StudioScrollChrome.PaintClientBuffer(Handle, Draw, false, EditableArea());
                }
                finally
                {
                    painting = false;
                }
            }

            Rectangle EditableArea()
            {
                var combo = control as ComboBox;
                if (combo == null || combo.DropDownStyle != ComboBoxStyle.DropDown) return Rectangle.Empty;
                var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
                return GetComboBoxInfo(Handle, ref info) ? info.Item.Rectangle : Rectangle.Empty;
            }

            void Draw(Graphics graphics)
            {
                StudioScrollChrome.NativeRect client;
                if (!StudioScrollChrome.GetClientRect(Handle, out client)) return;
                Rectangle bounds = client.Rectangle;
                if (bounds.Width < 3 || bounds.Height < 3) return;
                Point pointer = LocalPointer();
                bool hover = control.Enabled && bounds.Contains(pointer);
                bool pressed = hover && (Control.MouseButtons & MouseButtons.Left) != 0;
                Color foreground = control.Enabled ? DarkTheme.Fore : DarkTheme.Disabled;
                var combo = control as ComboBox;
                var saved = graphics.Save();
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (spinner)
                {
                    using (var brush = new SolidBrush(DarkTheme.Panel2)) graphics.FillRectangle(brush, bounds);
                    if (overflow)
                    {
                        DrawSideArrow(graphics, new Rectangle(0, 0, bounds.Width / 2, bounds.Height), false, foreground, pointer, pressed);
                        DrawSideArrow(graphics, new Rectangle(bounds.Width / 2, 0, bounds.Width - bounds.Width / 2, bounds.Height), true, foreground, pointer, pressed);
                        graphics.Restore(saved);
                        return;
                    }
                    Rectangle upper = new Rectangle(0, 0, bounds.Width, bounds.Height / 2);
                    Rectangle lower = new Rectangle(0, bounds.Height / 2, bounds.Width, bounds.Height - bounds.Height / 2);
                    DrawArrow(graphics, upper, true, foreground, hover, pressed);
                    DrawArrow(graphics, lower, false, foreground, hover, pressed);
                    graphics.Restore(saved);
                    return;
                }
                var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
                if (combo == null || !GetComboBoxInfo(Handle, ref info)) { graphics.Restore(saved); return; }
                Rectangle button = info.Button.Rectangle;
                if (combo.DropDownStyle == ComboBoxStyle.Simple)
                {
                    graphics.Restore(saved);
                    return;
                }
                // Editierbare Felder behalten Auswahl, Caret, IME und ihr natives Kindfenster.
                if (combo.DropDownStyle == ComboBoxStyle.DropDown)
                    graphics.ExcludeClip(info.Item.Rectangle);
                using (var brush = new SolidBrush(DarkTheme.Panel2)) graphics.FillRectangle(brush, bounds);
                bool open = combo.DroppedDown;
                Rectangle arrowSurface = Rectangle.Inflate(button, -1, -1);
                using (var shape = StudioSurface.Shape(arrowSurface, StudioSurface.Scale(control, 4)))
                using (var brush = new SolidBrush(!control.Enabled ? DarkTheme.Panel : open || pressed ? DarkTheme.AccentSoft : hover ? DarkTheme.Panel3 : DarkTheme.Panel2)) graphics.FillPath(brush, shape);
                DrawChevron(graphics, button, open, foreground);
                if (combo.DropDownStyle == ComboBoxStyle.DropDownList)
                {
                    Rectangle text = new Rectangle(StudioSurface.Scale(control, 9), 1, Math.Max(0, button.Left - StudioSurface.Scale(control, 14)), Math.Max(0, bounds.Height - 2));
                    if (combo.RightToLeft == RightToLeft.Yes)
                        text = new Rectangle(button.Right + StudioSurface.Scale(control, 5), 1, Math.Max(0, bounds.Width - button.Right - StudioSurface.Scale(control, 14)), Math.Max(0, bounds.Height - 2));
                    TextRenderer.DrawText(graphics, combo.Text, combo.Font, text, foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (combo.RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
                }
                bool focus = StudioSurface.HasKeyboardFocusWithin(combo);
                using (var shape = StudioSurface.Shape(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), StudioSurface.Scale(control, 5)))
                using (var pen = new Pen(!control.Enabled ? DarkTheme.Border : focus ? DarkTheme.Focus : hover || open ? DarkTheme.Accent : DarkTheme.Border, focus ? StudioSurface.Scale(control, 2) : 1)) graphics.DrawPath(pen, shape);
                graphics.Restore(saved);
            }

            void DrawArrow(Graphics graphics, Rectangle bounds, bool up, Color color, bool hover, bool pressed)
            {
                if (hover && bounds.Contains(LocalPointer()))
                    using (var brush = new SolidBrush(pressed ? DarkTheme.AccentSoft : DarkTheme.Panel3)) graphics.FillRectangle(brush, bounds);
                DrawChevron(graphics, bounds, up, color);
            }

            Point LocalPointer()
            {
                Point origin = Point.Empty;
                StudioScrollChrome.ClientToScreen(Handle, ref origin);
                return new Point(Cursor.Position.X - origin.X, Cursor.Position.Y - origin.Y);
            }

            int PointerState()
            {
                if (!control.Enabled || !StudioUx.CanPaint(control)) return 0;
                StudioScrollChrome.NativeRect client;
                if (!StudioScrollChrome.GetClientRect(Handle, out client)) return 0;
                Point pointer = LocalPointer();
                if (!client.Rectangle.Contains(pointer)) return 0;
                int part = !spinner ? 1 : overflow ? pointer.X < client.Right / 2 ? 1 : 2 : pointer.Y < client.Bottom / 2 ? 1 : 2;
                return part + ((Control.MouseButtons & MouseButtons.Left) != 0 ? 4 : 0);
            }

            static void DrawSideArrow(Graphics graphics, Rectangle bounds, bool forward, Color color, Point pointer, bool pressed)
            {
                if (bounds.Contains(pointer)) using (var brush = new SolidBrush(pressed ? DarkTheme.AccentSoft : DarkTheme.Panel3)) graphics.FillRectangle(brush, bounds);
                float cx = bounds.Left + bounds.Width / 2f, cy = bounds.Top + bounds.Height / 2f, direction = forward ? 1 : -1;
                float size = Math.Max(2, Math.Min(bounds.Width, bounds.Height) / 5f);
                using (var pen = new Pen(color, Math.Max(1.5f, size / 2.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    graphics.DrawLines(pen, new[] { new PointF(cx - direction * size / 2, cy - size), new PointF(cx + direction * size / 2, cy), new PointF(cx - direction * size / 2, cy + size) });
            }

            static void DrawChevron(Graphics graphics, Rectangle bounds, bool up, Color color)
            {
                float cx = bounds.Left + bounds.Width / 2f, cy = bounds.Top + bounds.Height / 2f;
                float size = Math.Max(2, Math.Min(bounds.Width, bounds.Height) / 5f), direction = up ? -1 : 1;
                using (var pen = new Pen(color, Math.Max(1.5f, size / 2.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    graphics.DrawLines(pen, new[] { new PointF(cx - size, cy - direction * size / 2), new PointF(cx, cy + direction * size / 2), new PointF(cx + size, cy - direction * size / 2) });
            }

            public void Dispose() { if (disposed) return; disposed = true; Unbind(); }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ComboInfo
        {
            internal int Size;
            internal StudioScrollChrome.NativeRect Item, Button;
            internal int ButtonState;
            internal IntPtr Combo, Edit, List;
        }
        [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr window, ref ComboInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
    }
}
