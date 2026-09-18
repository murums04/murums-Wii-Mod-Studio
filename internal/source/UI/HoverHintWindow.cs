using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    // Layered + transparent makes the entire hint ignore mouse hit testing, including
    // its child label. NOACTIVATE preserves the editor's focus and first click.
    internal sealed class HoverHintWindow : Form
    {
        Form host;
        internal Form HostForm
        {
            get
            {
                return host != null && !host.IsDisposed && !host.Disposing ? host : null;
            }
        }

        readonly Label text = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Padding = new Padding(8, 6, 8, 6)
        };
        readonly Timer expiry = new Timer
        {
            Interval = 12000
        };
        public HoverHintWindow()
        {
            TopMost = true;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Opacity = 0.99;
            BackColor = DarkTheme.Panel2;
            text.BackColor = BackColor;
            text.ForeColor = DarkTheme.Fore;
            Controls.Add(text);
            expiry.Tick += delegate
            {
                Hide();
            };
            VisibleChanged += delegate
            {
                if (!Visible)
                {
                    expiry.Stop();
                    DetachHost();
                }
            };
        }

        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ExStyle |= 0x80000 | 0x20 | 0x08000000 | 0x80;
                return p;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {
                m.Result = new IntPtr(-1);
                return;
            } // HTTRANSPARENT

            if (m.Msg == 0x21)
            {
                m.Result = new IntPtr(3);
                return;
            } // MA_NOACTIVATE

            base.WndProc(ref m);
        }

        public void ShowHint(string value, Form owner, Point point)
        {
            if (IsDisposed || owner == null || owner.IsDisposed || owner.Disposing || !owner.Visible)
                return;
            Hide();
            text.Text = value;
            var area = Screen.FromPoint(point).WorkingArea;
            text.MaximumSize = new Size(Math.Min(520, Math.Max(100, area.Width - 24)), 0);
            PerformLayout();
            var size = PreferredSize;
            Location = new Point(Math.Max(area.Left, Math.Min(point.X, area.Right - size.Width)), Math.Max(area.Top, Math.Min(point.Y, area.Bottom - size.Height)));
            // Do not pass the editor to Show(IWin32Window): WinForms retains that
            // native owner after Hide, and can access it after the editor is disposed.
            Show();
            host = owner;
            host.Deactivate += HostUnavailable;
            host.FormClosed += HostClosed;
            host.Disposed += HostUnavailable;
            expiry.Stop();
            expiry.Start();
        }

        void HostUnavailable(object sender, EventArgs e)
        {
            Hide();
        }

        void HostClosed(object sender, FormClosedEventArgs e)
        {
            Hide();
        }

        void DetachHost()
        {
            if (host == null)
                return;
            host.Deactivate -= HostUnavailable;
            host.FormClosed -= HostClosed;
            host.Disposed -= HostUnavailable;
            host = null;
        }

        public new void Hide()
        {
            if (IsDisposed)
                return;
            expiry.Stop();
            DetachHost();
            base.Hide();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachHost();
                expiry.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class HoverInputFilter : IMessageFilter
    {
        readonly Action dismiss;
        readonly Func<Point> position;
        Point lastPosition;
        public HoverInputFilter(Action dismiss) : this(dismiss, delegate
        {
            return Cursor.Position;
        })
        {
        }

        internal HoverInputFilter(Action dismiss, Func<Point> position)
        {
            this.dismiss = dismiss;
            this.position = position;
            lastPosition = position();
        }

        public bool PreFilterMessage(ref Message m)
        {
            // Dismiss before the original input is dispatched. Never consume or replay it.
            // Showing a transparent top-level hint can produce WM_MOUSEMOVE without
            // physical movement. Dismissing on that synthetic event creates a show/hide loop.
            if (m.Msg == 0x200 || m.Msg == 0xA0)
            {
                Point current = position();
                if (current != lastPosition)
                {
                    lastPosition = current;
                    dismiss();
                }

                return false;
            }

            if ((m.Msg >= 0x200 && m.Msg <= 0x20E) || (m.Msg >= 0x100 && m.Msg <= 0x109) || (m.Msg >= 0xA0 && m.Msg <= 0xAD))
            {
                lastPosition = position();
                dismiss();
            }

            return false;
        }
    }
}
