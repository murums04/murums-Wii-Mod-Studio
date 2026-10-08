using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal abstract class StudioFrame : Form
    {
        bool minimizedLayout;
        [StructLayout(LayoutKind.Sequential)]
        struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct WindowPlacement
        {
            public int Length, Flags, ShowCommand;
            public Point MinPosition, MaxPosition;
            public NativeRect NormalPosition;
        }
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacement placement);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        internal Control TitleBar(MenuStrip menu)
        {
            var bar = new TableLayoutPanel { Name = "StudioTitleBar", Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                Margin = Padding.Empty, BackColor = DarkTheme.Navigation };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            menu.Dock = DockStyle.Fill;
            menu.AutoSize = true;
            menu.Margin = Padding.Empty;
            bar.Controls.Add(menu, 0, 0);
            var caption = new Label { Dock = DockStyle.Fill, Text = "murums Wii Studio", TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkTheme.Muted, AutoEllipsis = true, Font = new Font("Segoe UI", 9), AccessibleName = Text };
            caption.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            };
            caption.DoubleClick += delegate { ToggleWindow(); };
            TextChanged += delegate { caption.Text = Text; caption.AccessibleName = Text; };
            caption.Text = Text;
            bar.Controls.Add(caption, 1, 0);
            var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            for (int i = 0; i < 3; i++) controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var minimize = CaptionButton(L.T("Minimieren", "Minimize"), StudioIcon.Remove);
            var maximize = CaptionButton(L.T("Wiederherstellen", "Restore window"), StudioIcon.Collapse);
            var close = CaptionButton(L.T("Studio schließen", "Close Studio"), StudioIcon.Close);
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            maximize.Click += delegate { ToggleWindow(); };
            close.Click += delegate { Close(); };
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized) return;
                maximize.Text = WindowState == FormWindowState.Maximized ? L.T("Wiederherstellen", "Restore window") : L.T("Maximieren", "Maximize");
                maximize.AccessibleDescription = maximize.Text;
                StudioActions.Icon(maximize, WindowState == FormWindowState.Maximized ? StudioIcon.Collapse : StudioIcon.Expand);
            };
            controls.Controls.Add(minimize, 0, 0); controls.Controls.Add(maximize, 1, 0); controls.Controls.Add(close, 2, 0);
            bar.Controls.Add(controls, 2, 0);
            return bar;
        }

        Button CaptionButton(string title, StudioIcon icon)
        {
            var button = new Button { Text = title, Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = DarkTheme.Navigation,
                TabStop = false, CausesValidation = false };
            StudioActions.Icon(button, icon);
            return button;
        }

        void ToggleWindow() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }

        protected override void OnShown(EventArgs e)
        {
            // Erst Tastaturnavigation zeigt Fokusrahmen; die Steuerelemente bleiben erreichbar.
            SendMessage(Handle, 0x127, (IntPtr)0x10001, IntPtr.Zero);
            base.OnShown(e);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 5)
            {
                if (message.WParam == (IntPtr)1 && !minimizedLayout) { SuspendLayout(); minimizedLayout = true; }
                bool restoringLayout = message.WParam != (IntPtr)1 && minimizedLayout;
                base.WndProc(ref message);
                if (message.WParam == (IntPtr)1)
                {
                    var placement = new WindowPlacement { Length = Marshal.SizeOf(typeof(WindowPlacement)) };
                    if (GetWindowPlacement(message.HWnd, ref placement))
                    {
                        int width = placement.NormalPosition.Right - placement.NormalPosition.Left;
                        int height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
                        // Der eigene Rahmen hat keine zusaetzlichen Standardrahmen-Masse.
                        if (width > 0 && height > 0) Size = new Size(width, height);
                    }
                }
                if (restoringLayout)
                {
                    minimizedLayout = false;
                    // Erst mit der wiederhergestellten Clientgroesse neu anordnen.
                    ResumeLayout(true);
                }
                return;
            }
            if (message.Msg == 0x83 && message.WParam != IntPtr.Zero)
            {
                if (IsZoomed(message.HWnd))
                {
                    var proposed = (NativeRect)Marshal.PtrToStructure(message.LParam, typeof(NativeRect));
                    Rectangle work = Screen.FromHandle(message.HWnd).WorkingArea;
                    proposed.Left = Math.Max(proposed.Left, work.Left);
                    proposed.Top = Math.Max(proposed.Top, work.Top);
                    proposed.Right = Math.Min(proposed.Right, work.Right);
                    proposed.Bottom = Math.Min(proposed.Bottom, work.Bottom);
                    Marshal.StructureToPtr(proposed, message.LParam, false);
                }
                message.Result = IntPtr.Zero;
                return;
            }
            if (message.Msg == 0x24)
            {
                base.WndProc(ref message);
                var info = (MinMaxInfo)Marshal.PtrToStructure(message.LParam, typeof(MinMaxInfo));
                Screen screen = Screen.FromHandle(Handle);
                Rectangle work = screen.WorkingArea, monitor = screen.Bounds;
                info.MaxPosition = new Point(work.Left - monitor.Left, work.Top - monitor.Top);
                info.MaxSize = new Point(work.Width, work.Height);
                Marshal.StructureToPtr(info, message.LParam, false);
                return;
            }
            if (message.Msg == 0x84 && WindowState == FormWindowState.Normal)
            {
                long packed = message.LParam.ToInt64();
                Point point = PointToClient(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
                int edge = Math.Max(5, Font.Height / 3);
                bool left = point.X < edge, right = point.X >= ClientSize.Width - edge;
                bool top = point.Y < edge, bottom = point.Y >= ClientSize.Height - edge;
                int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 0;
                if (hit != 0) { message.Result = (IntPtr)hit; return; }
            }
            base.WndProc(ref message);
        }
    }
}
