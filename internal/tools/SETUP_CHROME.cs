using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio.Setup
{
    public sealed class AccentStrip : Control
    {
        public AccentStrip() { Height = 1; Dock = DockStyle.Top; BackColor = SetupChrome.Border; }
    }

    public sealed class SetupProgressBar : Control
    {
        int value;
        readonly Func<bool> highContrast;
        public int Value { get { return value; } set { int next = Math.Max(0, Math.Min(100, value)); if (this.value == next) return; this.value = next; Invalidate(); } }
        public SetupProgressBar() : this(delegate { return SystemInformation.HighContrast; }) { }
        internal SetupProgressBar(Func<bool> highContrast)
        {
            if (highContrast == null) throw new ArgumentNullException("highContrast");
            this.highContrast = highContrast;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 4;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            bool contrast = highContrast();
            e.Graphics.Clear(contrast ? SystemColors.Control : Color.FromArgb(39, 30, 59));
            int width = (int)((long)Width * value / 100);
            if (width < 1 || Height < 1) return;
            var fill = new Rectangle(0, 0, width, Height);
            if (contrast || width == 1)
            {
                using (var brush = new SolidBrush(contrast ? SystemColors.Highlight : SetupChrome.Accent)) e.Graphics.FillRectangle(brush, fill);
            }
            else
            {
                using (var brush = new LinearGradientBrush(fill, SetupChrome.Accent, SetupChrome.Cyan, 0F)) e.Graphics.FillRectangle(brush, fill);
            }
        }
        protected override void OnSystemColorsChanged(EventArgs e) { base.OnSystemColorsChanged(e); Invalidate(); }
    }

    public static class SetupChrome
    {
        public static readonly Color Back = SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(10, 9, 17);
        public static readonly Color Panel = SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(15, 13, 25);
        public static readonly Color Panel2 = SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(22, 18, 35);
        public static readonly Color Hover = SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(39, 30, 59);
        public static readonly Color Border = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(82, 65, 111);
        public static readonly Color Fore = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(238, 239, 246);
        public static readonly Color Muted = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(176, 172, 195);
        public static Color Accent { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(139, 92, 246); } }
        public static Color Cyan { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(34, 211, 238); } }
        public static readonly Color Primary = SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(105, 66, 182);
        public static readonly Color Focus = SystemInformation.HighContrast ? SystemColors.Highlight : Cyan;
        sealed class Marker { internal bool Primary, KeyboardPressed; }
        static readonly ConditionalWeakTable<Control, Marker> styled = new ConditionalWeakTable<Control, Marker>();
        static readonly ConditionalWeakTable<Control, ScrollWindow> scrollWindows = new ConditionalWeakTable<Control, ScrollWindow>();
        public static string L(string german, string english)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? german : english;
        }
        public static string ToolRecommendation(string id, string fallback)
        {
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "de") return fallback;
            switch (id)
            {
                case "wszst": return "Empfohlen";
                case "wit": return "Für ISO / WBFS";
                case "ffmpeg": return "Für Audioimport";
                case "LoopingAudioConverter": return "Für BRSTM";
                default: return "Optional";
            }
        }
        public static string ToolPurpose(string id, string fallback)
        {
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "de") return fallback;
            switch (id)
            {
                case "wszst": return "Dateien umwandeln und prüfen";
                case "wit": return "ISO- und WBFS-Abbilder lesen";
                case "RiiStudio": return "Erweiterte 3D- und Modellbearbeitung";
                case "SwitchToolbox": return "Zusätzliche Formate; für HUD und Texturen nicht nötig";
                case "BrawlCrate": return "Spezialformate; für HUD und Layouts nicht nötig";
                case "ffmpeg": return "MP3, FLAC und OGG umwandeln";
                case "LoopingAudioConverter": return "Audiodateien in BRSTM umwandeln";
                case "NintyFont": return "Spezialschriften; für Schriftersatz nicht nötig";
                default: return fallback;
            }
        }
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        static void Frame(Form form)
        {
            if (!form.IsHandleCreated || SystemInformation.HighContrast) return;
            int dark = 1, back = ColorTranslator.ToWin32(Panel), text = ColorTranslator.ToWin32(Fore), border = ColorTranslator.ToWin32(Border);
            DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
            DwmSetWindowAttribute(form.Handle, 35, ref back, 4);
            DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
            DwmSetWindowAttribute(form.Handle, 34, ref border, 4);
        }
        public static void Button(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = primary ? Primary : Panel2;
            button.ForeColor = primary ? SystemInformation.HighContrast ? SystemColors.HighlightText : Color.FromArgb(8, 11, 20) : Fore;
            button.FlatAppearance.MouseOverBackColor = primary ? Accent : Hover;
            button.FlatAppearance.MouseDownBackColor = SystemInformation.HighContrast ? primary ? SystemColors.Highlight : SystemColors.ControlDark : primary ? Color.FromArgb(99, 68, 180) : Border;
            Marker marker;
            if (styled.TryGetValue(button, out marker)) { marker.Primary = primary; button.Invalidate(); return; }
            marker = new Marker { Primary = primary };
            styled.Add(button, marker);
            button.GotFocus += delegate { button.Invalidate(); };
            button.LostFocus += delegate { marker.KeyboardPressed = false; button.Invalidate(); };
            button.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Space) { marker.KeyboardPressed = true; button.Invalidate(); } };
            button.KeyUp += delegate { marker.KeyboardPressed = false; button.Invalidate(); };
            button.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.Clear(button.Parent == null ? Back : button.Parent.BackColor);
                bool hover = button.Enabled && button.ClientRectangle.Contains(button.PointToClient(Control.MousePosition));
                Color fill = button.Enabled ? (hover ? button.FlatAppearance.MouseOverBackColor : button.BackColor) : Panel;
                using (var shape = new GraphicsPath())
                {
                    int x = Math.Max(1, button.Width - 13), y = Math.Max(1, button.Height - 13);
                    shape.AddArc(1, 1, 12, 12, 180, 90); shape.AddArc(x, 1, 12, 12, 270, 90);
                    shape.AddArc(x, y, 12, 12, 0, 90); shape.AddArc(1, y, 12, 12, 90, 90); shape.CloseFigure();
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    if (marker.Primary && button.Enabled && !SystemInformation.HighContrast)
                    {
                        bool pressed = marker.KeyboardPressed || hover && (Control.MouseButtons & MouseButtons.Left) != 0;
                        float lift = pressed ? .16f : hover ? .08f : 0;
                        Color start = Color.FromArgb(Accent.R + (int)((255 - Accent.R) * lift), Accent.G + (int)((255 - Accent.G) * lift), Accent.B + (int)((255 - Accent.B) * lift));
                        Color end = Color.FromArgb(Cyan.R + (int)((255 - Cyan.R) * lift), Cyan.G + (int)((255 - Cyan.G) * lift), Cyan.B + (int)((255 - Cyan.B) * lift));
                        using (var brush = new LinearGradientBrush(button.ClientRectangle, start, end, 15F)) e.Graphics.FillPath(brush, shape);
                    }
                    else using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, shape);
                    e.Graphics.SmoothingMode = SmoothingMode.None;
                }
                TextRenderer.DrawText(e.Graphics, button.Text, button.Font, new Rectangle(5, 3, Math.Max(1, button.Width - 10), Math.Max(1, button.Height - 6)),
                    button.Enabled ? button.ForeColor : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                if (button.Focused && button.Enabled)
                    using (var pen = new Pen(Focus, 2))
                        e.Graphics.DrawRectangle(pen, 2, 2, Math.Max(0, button.Width - 5), Math.Max(0, button.Height - 5));
            };
        }
        public static void Apply(Control control)
        {
            var form = control as Form;
            if (form != null)
            {
                form.BackColor = Back;
                form.ForeColor = Fore;
                Marker marker;
                if (!styled.TryGetValue(form, out marker))
                {
                    styled.Add(form, new Marker());
                    form.HandleCreated += delegate { Frame(form); };
                    form.SystemColorsChanged += delegate { Frame(form); };
                    form.Shown += delegate
                    {
                        Rectangle work = Screen.FromControl(form.Owner ?? form).WorkingArea;
                        form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, work.Width), Math.Min(form.MinimumSize.Height, work.Height));
                        if (form.WindowState != FormWindowState.Normal) return;
                        Size size = new Size(Math.Min(form.Width, work.Width), Math.Min(form.Height, work.Height));
                        form.Bounds = new Rectangle(Math.Max(work.Left, Math.Min(form.Left, work.Right - size.Width)), Math.Max(work.Top, Math.Min(form.Top, work.Bottom - size.Height)), size.Width, size.Height);
                    };
                    if (form.IsHandleCreated) Frame(form);
                }
            }
            var button = control as Button;
            if (button != null) Button(button, button.BackColor == Primary || button.BackColor == Accent);
            var box = control as TextBoxBase;
            if (box != null)
            {
                box.BackColor = Panel; box.ForeColor = Fore; box.BorderStyle = box.ReadOnly ? BorderStyle.None : BorderStyle.FixedSingle;
                if (box.Multiline) AttachScroll(box);
            }
            if (control is ScrollBar) AttachScroll(control);
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = Panel;
                grid.BorderStyle = BorderStyle.None;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Panel2;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                grid.DefaultCellStyle.BackColor = Panel;
                grid.DefaultCellStyle.ForeColor = Fore;
                grid.DefaultCellStyle.SelectionBackColor = SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(53, 45, 73);
                grid.DefaultCellStyle.SelectionForeColor = SystemInformation.HighContrast ? SystemColors.HighlightText : Fore;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Panel;
                grid.GridColor = Border;
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                Marker marker;
                if (!styled.TryGetValue(grid, out marker))
                {
                    styled.Add(grid, new Marker());
                    grid.CellPainting += PaintChoiceCell;
                    grid.ControlAdded += delegate(object sender, ControlEventArgs e) { Apply(e.Control); };
                }
            }
            foreach (Control child in control.Controls) Apply(child);
        }

        static void AttachScroll(Control control)
        {
            ScrollWindow window;
            if (scrollWindows.TryGetValue(control, out window)) return;
            window = new ScrollWindow(control);
            scrollWindows.Add(control, window);
            control.HandleCreated += delegate { window.Bind(); };
            control.HandleDestroyed += delegate { window.Unbind(); };
            control.Disposed += delegate { window.Unbind(); };
            control.EnabledChanged += delegate { window.PaintChrome(); };
            control.GotFocus += delegate { window.PaintChrome(); };
            control.LostFocus += delegate { window.PaintChrome(); };
            if (control.IsHandleCreated) window.Bind();
        }

        sealed class ScrollWindow : NativeWindow
        {
            readonly Control owner;
            readonly bool clientBar;
            bool painting;
            internal ScrollWindow(Control owner) { this.owner = owner; clientBar = owner is ScrollBar; }
            internal void Bind()
            {
                if (owner.IsDisposed || owner.Disposing || Handle == owner.Handle) return;
                Unbind(); AssignHandle(owner.Handle);
            }
            internal void Unbind() { if (Handle != IntPtr.Zero) ReleaseHandle(); }
            bool Live()
            {
                var form = owner.TopLevelControl as Form;
                return !painting && Handle != IntPtr.Zero && !owner.IsDisposed && !owner.Disposing && owner.Visible && !SystemInformation.HighContrast
                    && (form == null || !form.IsDisposed && !form.Disposing && form.Visible && form.WindowState != FormWindowState.Minimized);
            }
            protected override void WndProc(ref Message message)
            {
                int id = message.Msg;
                if (Live() && clientBar && id == 0x14) { message.Result = (IntPtr)1; return; }
                if (Live() && clientBar && id == 0xF)
                {
                    PaintData data;
                    IntPtr handle = Handle, dc = BeginPaint(handle, out data);
                    try { Paint(dc); }
                    finally { EndPaint(handle, ref data); }
                    message.Result = IntPtr.Zero; return;
                }
                if (Live() && !clientBar && id == 0x85)
                {
                    PaintChrome(); message.Result = IntPtr.Zero; return;
                }
                base.WndProc(ref message);
                if (id == 0xF || id == 5 || id == 0x18 || id == 0xA || id == 7 || id == 8 || id == 0x114 || id == 0x115 || id == 0x20A
                    || id == 0x200 || id == 0x2A3 || id == 0x201 || id == 0x202 || id == 0x100 || id == 0x101 || id == 0xE0 || id == 0xE1 || id == 0xE2 || id == 0xE6 || id == 0xE9
                    || id == 0xC || id == 0xB6 || id == 0xB7)
                    PaintChrome();
                if ((id == 0x317 || clientBar && id == 0x318) && message.WParam != IntPtr.Zero && !SystemInformation.HighContrast)
                    Paint(message.WParam);
            }
            internal void PaintChrome()
            {
                if (!Live()) return;
                IntPtr handle = Handle, dc = GetWindowDC(handle);
                try { Paint(dc); }
                finally { if (dc != IntPtr.Zero) ReleaseDC(handle, dc); }
            }
            void Paint(IntPtr dc)
            {
                if (painting || dc == IntPtr.Zero || Handle == IntPtr.Zero) return;
                painting = true;
                int saved = SaveDC(dc);
                try
                {
                    NativeRect bounds;
                    if (!GetWindowRect(Handle, out bounds)) return;
                    Point origin = new Point(bounds.Left, bounds.Top);
                    if (!clientBar)
                    {
                        NativeRect client; Point clientOrigin = Point.Empty;
                        if (!GetClientRect(Handle, out client) || !ClientToScreen(Handle, ref clientOrigin)) return;
                        ExcludeClipRect(dc, clientOrigin.X - origin.X, clientOrigin.Y - origin.Y,
                            clientOrigin.X - origin.X + client.Right, clientOrigin.Y - origin.Y + client.Bottom);
                    }
                    using (var target = Graphics.FromHdc(dc))
                    {
                        DrawBuffered(target, origin, clientBar ? -4 : -5, clientBar ? owner is VScrollBar : true);
                        if (!clientBar) DrawBuffered(target, origin, -6, false);
                    }
                }
                finally { if (saved != 0) RestoreDC(dc, saved); painting = false; }
            }
            void DrawBuffered(Graphics target, Point origin, int objectId, bool vertical)
            {
                var info = new ScrollInfo { Size = Marshal.SizeOf(typeof(ScrollInfo)), State = new int[6] };
                if (!GetScrollBarInfo(Handle, objectId, ref info) || (info.State[0] & 0x18000) != 0) return;
                Rectangle area = Rectangle.FromLTRB(info.Bounds.Left - origin.X, info.Bounds.Top - origin.Y, info.Bounds.Right - origin.X, info.Bounds.Bottom - origin.Y);
                if (area.Width < 1 || area.Height < 1) return;
                using (var buffer = BufferedGraphicsManager.Current.Allocate(target, area))
                {
                    Graphics graphics = buffer.Graphics;
                    graphics.ResetTransform(); graphics.TranslateTransform(-area.Left, -area.Top); graphics.SetClip(area);
                    using (var brush = new SolidBrush(Panel)) graphics.FillRectangle(brush, area);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Point mouse = Control.MousePosition; mouse.Offset(-origin.X, -origin.Y);
                    bool enabled = owner.Enabled && (info.State[0] & 1) == 0;
                    int arrow = Math.Min(info.LineButton, (vertical ? area.Height : area.Width) / 2);
                    Rectangle first = vertical ? new Rectangle(area.Left, area.Top, area.Width, arrow) : new Rectangle(area.Left, area.Top, arrow, area.Height);
                    Rectangle last = vertical ? new Rectangle(area.Left, area.Bottom - arrow, area.Width, arrow) : new Rectangle(area.Right - arrow, area.Top, arrow, area.Height);
                    Arrow(graphics, first, vertical, false, enabled && (info.State[1] & 1) == 0, mouse, info.State[1]);
                    Arrow(graphics, last, vertical, true, enabled && (info.State[5] & 1) == 0, mouse, info.State[5]);
                    if (info.ThumbBottom > info.ThumbTop && info.ThumbTop >= arrow)
                    {
                        int thickness = Math.Max(2, Math.Min((int)Math.Round(6 * owner.DeviceDpi / 96.0), (vertical ? area.Width : area.Height) - 4));
                        Rectangle thumb = vertical
                            ? new Rectangle(area.Left + (area.Width - thickness) / 2, area.Top + info.ThumbTop + 1, thickness, Math.Max(1, info.ThumbBottom - info.ThumbTop - 2))
                            : new Rectangle(area.Left + info.ThumbTop + 1, area.Top + (area.Height - thickness) / 2, Math.Max(1, info.ThumbBottom - info.ThumbTop - 2), thickness);
                        Color colour = !enabled ? Hover : (info.State[3] & 8) != 0 ? Accent : area.Contains(mouse) ? Primary : Border;
                        using (var brush = new SolidBrush(colour)) graphics.FillRectangle(brush, thumb);
                    }
                    if (owner.Focused && enabled)
                        using (var pen = new Pen(Focus)) graphics.DrawRectangle(pen, area.Left, area.Top, Math.Max(0, area.Width - 1), Math.Max(0, area.Height - 1));
                    buffer.Render(target);
                }
            }
            static void Arrow(Graphics graphics, Rectangle area, bool vertical, bool forward, bool enabled, Point mouse, int state)
            {
                if (area.Width < 3 || area.Height < 3) return;
                if (enabled && area.Contains(mouse))
                    using (var brush = new SolidBrush((state & 8) != 0 ? Primary : Hover)) graphics.FillRectangle(brush, area);
                float x = area.Left + area.Width / 2f, y = area.Top + area.Height / 2f;
                float size = Math.Max(2, Math.Min(area.Width, area.Height) / 5f), sign = forward ? 1 : -1;
                using (var pen = new Pen(enabled ? Muted : Border, Math.Max(1, size / 2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    graphics.DrawLines(pen, vertical
                        ? new[] { new PointF(x - size, y - sign * size / 2), new PointF(x, y + sign * size / 2), new PointF(x + size, y - sign * size / 2) }
                        : new[] { new PointF(x - sign * size / 2, y - size), new PointF(x + sign * size / 2, y), new PointF(x - sign * size / 2, y + size) });
            }
        }

        [StructLayout(LayoutKind.Sequential)] struct NativeRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct PaintData
        {
            internal IntPtr DC; internal int Erase; internal NativeRect Bounds; internal int Restore, Update;
            internal int Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7, Reserved8;
        }
        [StructLayout(LayoutKind.Sequential)] struct ScrollInfo
        {
            internal int Size; internal NativeRect Bounds; internal int LineButton, ThumbTop, ThumbBottom, Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal int[] State;
        }
        [DllImport("user32.dll")] static extern bool GetScrollBarInfo(IntPtr window, int objectId, ref ScrollInfo info);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr window, out PaintData data);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr window, ref PaintData data);
        [DllImport("gdi32.dll")] static extern int SaveDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool RestoreDC(IntPtr dc, int saved);
        [DllImport("gdi32.dll")] static extern int ExcludeClipRect(IntPtr dc, int left, int top, int right, int bottom);

        static void PaintChoiceCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (SystemInformation.HighContrast || e.RowIndex < 0 || e.ColumnIndex < 0 || !(grid[e.ColumnIndex, e.RowIndex] is DataGridViewCheckBoxCell)) return;
            e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
            int size = Math.Max(14, Math.Min(e.CellBounds.Height - 10, (int)Math.Round(16 * grid.DeviceDpi / 96.0)));
            var box = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - size) / 2, e.CellBounds.Y + (e.CellBounds.Height - size) / 2, size, size);
            bool selected = Object.Equals(e.FormattedValue, true) || Object.Equals(e.FormattedValue, CheckState.Checked);
            bool mixed = Object.Equals(e.FormattedValue, CheckState.Indeterminate);
            using (var brush = new SolidBrush(selected || mixed ? Accent : Panel2)) e.Graphics.FillRectangle(brush, box);
            using (var pen = new Pen(grid.Enabled ? selected || mixed ? Accent : Border : Muted)) e.Graphics.DrawRectangle(pen, box);
            if (selected || mixed)
            {
                var smooth = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(SystemInformation.HighContrast ? SystemColors.HighlightText : Color.FromArgb(8, 11, 20), Math.Max(2, size / 8f)))
                {
                    if (mixed) e.Graphics.DrawLine(pen, box.X + size / 4, box.Y + size / 2, box.Right - size / 4, box.Y + size / 2);
                    else e.Graphics.DrawLines(pen, new[] { new PointF(box.X + size * .22f, box.Y + size * .51f), new PointF(box.X + size * .43f, box.Y + size * .72f), new PointF(box.X + size * .80f, box.Y + size * .27f) });
                }
                e.Graphics.SmoothingMode = smooth;
            }
            e.Handled = true;
        }
        public static Form Message(string message, string title, MessageBoxButtons buttons)
        {
            var form = new Form { Text = title, Font = new Font("Segoe UI", 10), AutoScaleMode = AutoScaleMode.Font,
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false,
                ClientSize = new Size(560, 240), MinimumSize = new Size(450, 240) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, AutoSize = true, MaximumSize = new Size(520, 0), Font = new Font("Segoe UI", 12, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            var messageText = new TextBox { Text = message, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.None, BorderStyle = BorderStyle.None, TabStop = false };
            bool sizingText = false;
            EventHandler fitText = delegate
            {
                if (sizingText || messageText.IsDisposed || messageText.Disposing || messageText.ClientSize.Width < 1) return;
                sizingText = true;
                try
                {
                    int width = messageText.ClientSize.Width + (messageText.ScrollBars == ScrollBars.Vertical ? SystemInformation.VerticalScrollBarWidth : 0);
                    int height = TextRenderer.MeasureText(messageText.Text, messageText.Font, new Size(Math.Max(1, width - 4), Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
                    ScrollBars next = height > messageText.ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
                    if (messageText.ScrollBars != next) messageText.ScrollBars = next;
                }
                finally { sizingText = false; }
            };
            messageText.SizeChanged += fitText; messageText.FontChanged += fitText; messageText.TextChanged += fitText;
            layout.Controls.Add(messageText, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 16, 0, 0) };
            bool yesNo = buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel;
            var accept = new Button { Text = yesNo ? L("Ja", "Yes") : "OK", DialogResult = yesNo ? DialogResult.Yes : DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 34) };
            Button(accept, true); actions.Controls.Add(accept);
            if (buttons != MessageBoxButtons.OK)
            {
                var cancel = new Button { Text = yesNo ? L("Nein", "No") : L("Abbrechen", "Cancel"), DialogResult = yesNo ? DialogResult.No : DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 34) };
                actions.Controls.Add(cancel); form.CancelButton = cancel;
                form.AcceptButton = yesNo ? cancel : accept;
                if (buttons == MessageBoxButtons.YesNoCancel)
                {
                    var third = new Button { Text = L("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 34) };
                    actions.Controls.Add(third); form.CancelButton = third;
                }
            }
            else { form.AcceptButton = accept; form.CancelButton = accept; }
            layout.Controls.Add(actions, 0, 2); form.Controls.Add(layout); Apply(form);
            return form;
        }
        public static DialogResult ShowMessage(string message, string title) { return ShowMessage(null, message, title, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult ShowMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon) { return ShowMessage(null, message, title, buttons, icon); }
        public static DialogResult ShowMessage(IWin32Window owner, string message, string title) { return ShowMessage(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult ShowMessage(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (var form = Message(message, title, buttons)) return form.ShowDialog(owner);
        }
    }
}
