using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioTrackBarChrome
    {
        static readonly ConditionalWeakTable<TrackBar, Window> windows = new ConditionalWeakTable<TrackBar, Window>();

        internal static void Attach(Control control)
        {
            var track = control as TrackBar;
            Window existing;
            if (track == null || windows.TryGetValue(track, out existing)) return;
            var window = new Window(track);
            windows.Add(track, window);
            track.HandleCreated += delegate { window.Bind(); };
            track.HandleDestroyed += delegate { window.Unbind(); };
            track.Disposed += delegate { window.Dispose(); };
            track.ValueChanged += delegate { track.Invalidate(); };
            track.EnabledChanged += delegate { track.Invalidate(); };
            track.GotFocus += delegate { track.Invalidate(); };
            track.LostFocus += delegate { track.Invalidate(); };
            track.ChangeUICues += delegate { track.Invalidate(); };
            track.MouseEnter += delegate { track.Invalidate(); };
            track.MouseLeave += delegate { track.Invalidate(); };
            track.MouseCaptureChanged += delegate { track.Invalidate(); };
            if (track.IsHandleCreated) window.Bind();
        }

        internal static bool UsesCustomChrome(bool highContrast) { return !highContrast; }

        sealed class Window : NativeWindow, IDisposable
        {
            readonly TrackBar track;
            bool disposed, painting, hovered, pressed;

            internal Window(TrackBar track) { this.track = track; }
            internal void Bind()
            {
                if (disposed || !track.IsHandleCreated || Handle == track.Handle) return;
                Unbind();
                AssignHandle(track.Handle);
            }
            internal void Unbind() { if (Handle != IntPtr.Zero) ReleaseHandle(); }

            protected override void WndProc(ref Message message)
            {
                int id = message.Msg;
                bool custom = !disposed && Handle != IntPtr.Zero && UsesCustomChrome(SystemInformation.HighContrast);
                if (custom && StudioUx.CanPaint(track) && !painting)
                {
                    if (id == 0x14) { message.Result = (IntPtr)1; return; }
                    if (id == 0xF)
                    {
                        painting = true;
                        try { StudioScrollChrome.PaintClientBuffer(Handle, Draw, true, Rectangle.Empty); }
                        finally { painting = false; }
                        message.Result = IntPtr.Zero;
                        return;
                    }
                }
                // Alle Eingaben gehen durch das native TrackBar; nur die Darstellung wird ersetzt.
                base.WndProc(ref message);
                if (!custom || Handle == IntPtr.Zero || painting) return;
                if (id == 0x317 || id == 0x318)
                {
                    if (message.WParam != IntPtr.Zero)
                        using (var graphics = Graphics.FromHdc(message.WParam)) Draw(graphics);
                    return;
                }
                if (!StudioUx.CanPaint(track)) return;
                if (id == 0x200)
                {
                    long coordinates = message.LParam.ToInt64();
                    bool next = track.Enabled && Thumb().Contains(new Point((short)(coordinates & 0xFFFF), (short)((coordinates >> 16) & 0xFFFF)));
                    if (next == hovered && !track.Capture) return;
                    hovered = next;
                }
                if (id == 0x201) pressed = true;
                if (id == 0x202 || id == 0x215 || id == 8 || id == 0xA && !track.Enabled) pressed = false;
                if (id == 0x2A3 || id == 0xA && !track.Enabled) hovered = false;
                if (id == 0x200 || id == 0x201 || id == 0x202 || id == 0x2A3 || id == 0x215 || id == 7 || id == 8 || id == 5
                    || id == 0xA || id == 0x128 || id == 0x404 || id == 0x405 || id == 0x406 || id == 0x407 || id == 0x408 || id == 0x414)
                    track.Invalidate();
            }

            Rectangle Thumb()
            {
                StudioScrollChrome.NativeRect rectangle;
                SendRect(Handle, 0x419, IntPtr.Zero, out rectangle);
                return rectangle.Rectangle;
            }

            void Draw(Graphics graphics)
            {
                Rectangle bounds = track.ClientRectangle;
                if (bounds.Width < 3 || bounds.Height < 3) return;
                var saved = graphics.Save();
                graphics.SetClip(bounds);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(track.BackColor)) graphics.FillRectangle(brush, bounds);
                StudioScrollChrome.NativeRect channelRect;
                SendRect(Handle, 0x41A, IntPtr.Zero, out channelRect);
                Rectangle channel = channelRect.Rectangle, thumb = Thumb();
                bool vertical = track.Orientation == Orientation.Vertical;
                // Manche Common-Control-Versionen liefern den Kanal auch vertikal in horizontalen Koordinaten.
                if (vertical && channel.Width > channel.Height) channel = new Rectangle(channel.Top, channel.Left, channel.Height, channel.Width);
                int thickness = StudioSurface.Scale(track, 4);
                Rectangle rail = vertical
                    ? new Rectangle(channel.Left + (channel.Width - thickness) / 2, channel.Top, thickness, channel.Height)
                    : new Rectangle(channel.Left, channel.Top + (channel.Height - thickness) / 2, channel.Width, thickness);
                Fill(graphics, rail, track.Enabled ? DarkTheme.Border : DarkTheme.Panel3, thickness / 2);
                if (track.Enabled)
                {
                    Rectangle active = rail;
                    if (vertical) active.Height = Math.Max(0, Math.Min(rail.Height, thumb.Top + thumb.Height / 2 - rail.Top));
                    else active.Width = Math.Max(0, Math.Min(rail.Width, thumb.Left + thumb.Width / 2 - rail.Left));
                    Fill(graphics, active, DarkTheme.Accent, thickness / 2);
                }
                DrawTicks(graphics, rail, thumb, vertical);
                bool hover = track.Enabled && hovered;
                bool dragging = track.Enabled && pressed && track.Capture;
                Fill(graphics, Rectangle.Inflate(thumb, -1, -1), !track.Enabled ? DarkTheme.Panel3 : dragging ? DarkTheme.Cyan : hover ? DarkTheme.Accent : DarkTheme.Panel3, StudioSurface.Scale(track, 5));
                using (var shape = StudioSurface.Shape(Rectangle.Inflate(thumb, -1, -1), StudioSurface.Scale(track, 5)))
                using (var pen = new Pen(!track.Enabled ? DarkTheme.Disabled : hover || dragging ? DarkTheme.Cyan : DarkTheme.Accent)) graphics.DrawPath(pen, shape);
                if (StudioSurface.HasKeyboardFocus(track))
                    ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -2, -2), DarkTheme.Focus, track.BackColor);
                graphics.Restore(saved);
            }

            void DrawTicks(Graphics graphics, Rectangle rail, Rectangle thumb, bool vertical)
            {
                if (track.TickStyle == TickStyle.None) return;
                int count = SendMessage(Handle, 0x410, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int extent = StudioSurface.Scale(track, 3), gap = StudioSurface.Scale(track, 3);
                using (var pen = new Pen(track.Enabled ? DarkTheme.Muted : DarkTheme.Disabled))
                {
                    int start = vertical ? rail.Top + thumb.Height / 2 : rail.Left + thumb.Width / 2;
                    int end = vertical ? rail.Bottom - thumb.Height / 2 - 1 : rail.Right - thumb.Width / 2 - 1;
                    DrawTick(graphics, pen, start, thumb, vertical, gap, extent);
                    DrawTick(graphics, pen, end, thumb, vertical, gap, extent);
                    for (int i = 0; i < count - 2; i++)
                    {
                        int position = SendMessage(Handle, 0x40F, (IntPtr)i, IntPtr.Zero).ToInt32();
                        if (position >= 0) DrawTick(graphics, pen, position, thumb, vertical, gap, extent);
                    }
                }
            }

            void DrawTick(Graphics graphics, Pen pen, int position, Rectangle thumb, bool vertical, int gap, int extent)
            {
                if (track.TickStyle == TickStyle.TopLeft || track.TickStyle == TickStyle.Both)
                {
                    if (vertical) graphics.DrawLine(pen, thumb.Left - gap - extent, position, thumb.Left - gap, position);
                    else graphics.DrawLine(pen, position, thumb.Top - gap - extent, position, thumb.Top - gap);
                }
                if (track.TickStyle == TickStyle.BottomRight || track.TickStyle == TickStyle.Both)
                {
                    if (vertical) graphics.DrawLine(pen, thumb.Right + gap, position, thumb.Right + gap + extent, position);
                    else graphics.DrawLine(pen, position, thumb.Bottom + gap, position, thumb.Bottom + gap + extent);
                }
            }

            static void Fill(Graphics graphics, Rectangle bounds, Color color, int radius)
            {
                if (bounds.Width < 1 || bounds.Height < 1) return;
                using (var shape = StudioSurface.Shape(bounds, radius))
                using (var brush = new SolidBrush(color)) graphics.FillPath(brush, shape);
            }
            public void Dispose() { if (disposed) return; disposed = true; Unbind(); }
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendRect(IntPtr window, int message, IntPtr wParam, out StudioScrollChrome.NativeRect rectangle);
    }
}
