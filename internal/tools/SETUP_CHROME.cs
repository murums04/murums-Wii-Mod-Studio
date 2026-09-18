using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace murumsWiiModStudio.Setup
{
    public sealed class AccentStrip : Control
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
                var form = FindForm();
                if (Visible && form != null && form.WindowState != FormWindowState.Minimized && !SystemInformation.HighContrast)
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
            var purple = Color.FromArgb(103, 72, 190);
            e.Graphics.Clear(purple);
            if (Width < 1)
                return;
            float center = phase * (Width + 400) - 200;
            using (var brush = new LinearGradientBrush(new PointF(center - 200, 0), new PointF(center + 200, 0), purple, purple))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        purple,
                        Color.FromArgb(193, 160, 255),
                        purple
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
}
