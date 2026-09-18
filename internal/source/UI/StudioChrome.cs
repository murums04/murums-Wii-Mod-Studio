using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class AccentStrip : Control
    {
        readonly Timer timer = new Timer();
        float phase;
        public AccentStrip()
        {
            Height = 4;
            Dock = DockStyle.Top;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer.Interval = 50;
            timer.Tick += delegate
            {
                var f = FindForm();
                if (Visible && f != null && f.WindowState != FormWindowState.Minimized && !SystemInformation.HighContrast)
                {
                    phase = (phase + 0.006f) % 1f;
                    Invalidate();
                }
            };
            VisibleChanged += delegate
            {
                timer.Enabled = Visible;
            };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(DarkTheme.Accent2);
            if (Width < 1)
                return;
            float center = phase * (Width + 400) - 200;
            using (var brush = new LinearGradientBrush(new PointF(center - 200, 0), new PointF(center + 200, 0), DarkTheme.Accent2, DarkTheme.Accent2))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        DarkTheme.Accent2,
                        Color.FromArgb(193, 160, 255),
                        DarkTheme.Accent2
                    },
                    Positions = new[]
                    {
                        0f,
                        .5f,
                        1f
                    }
                };
                e.Graphics.FillRectangle(brush, center - 200, 0, 400, Height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class StudioChrome
    {
        public static Panel Header(string title, string subtitle)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Panel,
                Padding = new Padding(1)
            };
            panel.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (var p = new Pen(DarkTheme.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1));
            };
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(16, 12, 16, 8)
            };
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            grid.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Text = "v" + StudioVersion.Current, TextAlign = ContentAlignment.MiddleRight, ForeColor = DarkTheme.Muted, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, 2, 0);
            var logo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 5, 14, 5)
            };
            try
            {
                using (var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                    if (icon != null)
                        logo.Image = icon.ToBitmap();
            }
            catch
            {
            }

            logo.Disposed += delegate
            {
                if (logo.Image != null)
                    logo.Image.Dispose();
            };
            grid.Controls.Add(logo, 0, 0);
            var text = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            text.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Text = "murums Wii Mod Studio", ForeColor = DarkTheme.Muted, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, 0, 0);
            text.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Text = title, ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoEllipsis = true }, 0, 1);
            text.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Text = subtitle, ForeColor = DarkTheme.Muted, AutoEllipsis = true }, 0, 2);
            grid.Controls.Add(text, 1, 0);
            panel.Controls.Add(grid);
            panel.Controls.Add(new AccentStrip());
            return panel;
        }

        public static void EnableLinks(RichTextBox box)
        {
            box.DetectUrls = true;
            box.LinkClicked += delegate (object sender, LinkClickedEventArgs e)
            {
                OpenLink(box.FindForm(), e.LinkText);
            };
        }

        public static void OpenLink(IWin32Window owner, string address)
        {
            Uri url;
            if (!Uri.TryCreate(address, UriKind.Absolute, out url) || (url.Scheme != "https" && url.Scheme != "http"))
                return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StudioMessageBox.Show(owner, ex.Message, L.T("Link öffnen", "Open link"));
            }
        }
    }

    internal sealed class SelectionClearButton : Button
    {
        public SelectionClearButton()
        {
            FlatStyle = FlatStyle.Flat;
            Height = 36;
            Width = 200;
            Padding = new Padding(10, 0, 10, 0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Enabled ? DarkTheme.Panel3 : DarkTheme.Panel);
            using (var p = new Pen(Enabled ? DarkTheme.Accent : DarkTheme.Border))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, "×  " + Text, Font, new Rectangle(6, 0, Width - 12, Height), Enabled ? DarkTheme.Fore : DarkTheme.Disabled, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused)
                ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8));
        }
    }
}
