using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class PackSelectionStrip : TableLayoutPanel
    {
        readonly Timer pulse = new Timer { Interval = 50 };
        readonly System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
        bool required;
        bool paused;
        internal bool Required
        {
            get { return required; }
            set { required = value; UpdateAnimation(); Invalidate(); }
        }
        internal bool Paused
        {
            get { return paused; }
            set { paused = value; UpdateAnimation(); }
        }
        internal bool Animating { get { return pulse.Enabled; } }

        internal PackSelectionStrip()
        {
            Name = "CustomPackBar";
            DoubleBuffered = true;
            pulse.Tick += delegate { Invalidate(); };
            VisibleChanged += delegate { UpdateAnimation(); };
            EnabledChanged += delegate { UpdateAnimation(); };
        }

        internal void UpdateAnimation()
        {
            var owner = FindForm();
            bool run = required && !paused && Visible && Enabled && !IsDisposed && owner != null && owner.Visible;
            if (run) { elapsed.Start(); pulse.Start(); }
            else { elapsed.Stop(); pulse.Stop(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!required) return;
            double blend = .35 + .65 * (1 - Math.Cos(elapsed.Elapsed.TotalSeconds * Math.PI / 1.5)) / 2;
            Color start = DarkTheme.Border, end = DarkTheme.Accent;
            Color color = Color.FromArgb((int)(start.R + (end.R - start.R) * blend),
                (int)(start.G + (end.G - start.G) * blend), (int)(start.B + (end.B - start.B) * blend));
            using (var pen = new Pen(color, 3))
                e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { pulse.Stop(); pulse.Dispose(); elapsed.Stop(); }
            base.Dispose(disposing);
        }
    }
}
