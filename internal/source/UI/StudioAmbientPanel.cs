using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;

namespace murumsWiiModStudio
{
    internal class StudioAmbientPanel : Panel
    {
        readonly Timer animation = new Timer { Interval = 100 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Func<bool> motionPreference;
        Bitmap violet, cyan, violetSized, cyanSized, pointerGlow;
        Form owner;
        bool resizing;
        bool pointerInside;
        Point pointerTarget;
        PointF pointerPosition;
        float pointerStrength;

        internal StudioAmbientPanel() : this(delegate { return StudioWindowLayout.MotionEnabled; }) { }

        internal StudioAmbientPanel(Func<bool> motionPreference)
        {
            if (motionPreference == null) throw new ArgumentNullException("motionPreference");
            this.motionPreference = motionPreference;
            DoubleBuffered = true;
            ResizeRedraw = true;
            animation.Tick += delegate {
                if (!CanAnimate()) { animation.Stop(); clock.Stop(); return; }
                float target = pointerInside ? 1 : 0;
                pointerStrength += (target - pointerStrength) * .24f;
                pointerPosition.X += (pointerTarget.X - pointerPosition.X) * .24f;
                pointerPosition.Y += (pointerTarget.Y - pointerPosition.Y) * .24f;
                bool settling = Math.Abs(target - pointerStrength) > .01f
                    || pointerInside && (Math.Abs(pointerTarget.X - pointerPosition.X) > 1 || Math.Abs(pointerTarget.Y - pointerPosition.Y) > 1);
                animation.Interval = settling ? 33 : 100;
                Invalidate(false);
            };
        }

        bool CanAnimate()
        {
            return !IsDisposed && IsHandleCreated && Visible && !resizing
                && !SystemInformation.HighContrast && !SystemInformation.TerminalServerSession
                && motionPreference() && StudioUx.CanPaint(this);
        }

        void UpdateAnimation(object sender, EventArgs e)
        {
            var next = TopLevelControl as Form;
            if (next != owner)
            {
                UnbindOwner();
                owner = next;
                if (owner != null)
                {
                    owner.Resize += UpdateAnimation;
                    owner.VisibleChanged += UpdateAnimation;
                    owner.ResizeBegin += BeginResize;
                    owner.ResizeEnd += EndResize;
                }
            }
            animation.Enabled = CanAnimate();
            if (animation.Enabled) clock.Start(); else clock.Stop();
            if (!animation.Enabled) { pointerInside = false; pointerStrength = 0; }
        }

        void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate { if (!IsDisposed) { UpdateAnimation(this, EventArgs.Empty); Invalidate(false); } }); }
            catch (InvalidOperationException) { }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!CanAnimate()) return;
            pointerTarget = e.Location;
            if (!pointerInside && pointerStrength < .01f) pointerPosition = e.Location;
            pointerInside = true;
            animation.Interval = 33;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            pointerInside = false;
            if (animation.Enabled) animation.Interval = 33;
        }

        void BeginResize(object sender, EventArgs e) { resizing = true; animation.Stop(); clock.Stop(); }
        void EndResize(object sender, EventArgs e) { resizing = false; UpdateAnimation(sender, e); }
        void UnbindOwner()
        {
            if (owner == null) return;
            owner.Resize -= UpdateAnimation;
            owner.VisibleChanged -= UpdateAnimation;
            owner.ResizeBegin -= BeginResize;
            owner.ResizeEnd -= EndResize;
            owner = null;
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateAnimation(this, e); }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); UpdateAnimation(this, e); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SystemEvents.UserPreferenceChanged += PreferenceChanged; UpdateAnimation(this, e); }
        protected override void OnHandleDestroyed(EventArgs e) { SystemEvents.UserPreferenceChanged -= PreferenceChanged; animation.Stop(); clock.Stop(); UnbindOwner(); base.OnHandleDestroyed(e); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (SystemInformation.HighContrast || Width < 80 || Height < 80) return;
            if (violet == null) violet = Glow(DarkTheme.Accent);
            if (cyan == null) cyan = Glow(DarkTheme.Cyan);
            double time = motionPreference() ? clock.Elapsed.TotalSeconds * Math.PI * 2 / 35 : 0;
            int size = Math.Min(760, Math.Max(320, Width / 2));
            int drift = (int)(Math.Sin(time) * 22);
            int expansion = (int)(Math.Cos(time) * size * .018);
            DrawGlow(e.Graphics, violet, ref violetSized, size + expansion, -size / 4 + drift, Height - size * 3 / 4);
            DrawGlow(e.Graphics, cyan, ref cyanSized, size - expansion, Width - size * 3 / 4 - drift, -size / 3);
            int spacing = StudioSurface.Scale(this, 24);
            using (var dots = new SolidBrush(Color.FromArgb(18, DarkTheme.Muted)))
                for (int y = 8; y < Height; y += spacing)
                    for (int x = 8; x < Width; x += spacing)
                        if (e.ClipRectangle.Contains(x, y)) e.Graphics.FillRectangle(dots, x, y, 1, 1);
            if (motionPreference() && pointerStrength > .01f)
            {
                if (pointerGlow == null) pointerGlow = Glow(DarkTheme.Accent);
                int diameter = Math.Min(520, StudioSurface.Scale(this, 320));
                var area = new Rectangle((int)pointerPosition.X - diameter / 2, (int)pointerPosition.Y - diameter / 2, diameter, diameter);
                using (var attributes = new ImageAttributes())
                {
                    var opacity = new ColorMatrix { Matrix33 = pointerStrength };
                    attributes.SetColorMatrix(opacity);
                    e.Graphics.DrawImage(pointerGlow, area, 0, 0, pointerGlow.Width, pointerGlow.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        static void DrawGlow(Graphics graphics, Bitmap original, ref Bitmap sized, int size, int x, int y)
        {
            // Die langsame Größenänderung braucht kein neues Resampling in jedem Frame.
            if (sized == null || Math.Abs(sized.Width - size) > 1)
            {
                var next = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var canvas = Graphics.FromImage(next)) canvas.DrawImage(original, 0, 0, size, size);
                if (sized != null) sized.Dispose();
                sized = next;
            }
            graphics.DrawImageUnscaled(sized, x, y);
        }

        static Bitmap Glow(Color color)
        {
            var glow = new Bitmap(192, 192, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(glow))
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, 192, 192);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(36, color);
                    brush.SurroundColors = new[] { Color.FromArgb(0, color) };
                    graphics.FillPath(brush, path);
                }
            }
            return glow;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animation.Dispose();
                SystemEvents.UserPreferenceChanged -= PreferenceChanged;
                UnbindOwner();
                if (violet != null) { violet.Dispose(); violet = null; }
                if (cyan != null) { cyan.Dispose(); cyan = null; }
                if (violetSized != null) { violetSized.Dispose(); violetSized = null; }
                if (cyanSized != null) { cyanSized.Dispose(); cyanSized = null; }
                if (pointerGlow != null) { pointerGlow.Dispose(); pointerGlow = null; }
            }
            base.Dispose(disposing);
        }
    }
}
