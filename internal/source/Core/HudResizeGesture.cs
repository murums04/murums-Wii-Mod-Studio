using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    // A gesture is evaluated against its initial transform, never against the last mouse move.
    internal sealed class HudResizeGesture : IDisposable
    {
        readonly BrlytPaneInfo pane;
        readonly RectangleF bounds;
        readonly int handle;
        readonly bool group;
        readonly float x, y, w, h, sx, sy, rotation;
        readonly Matrix inverse;
        public HudResizeGesture(BrlytPaneInfo p, RectangleF localBounds, int index)
        {
            pane = p;
            bounds = localBounds;
            handle = index;
            group = p.Children.Count > 0;
            x = p.X;
            y = p.Y;
            w = p.Width;
            h = p.Height;
            sx = p.ScaleX;
            sy = p.ScaleY;
            rotation = p.RotZ;
            inverse = HudLayoutGeometry.World(p);
            if (!inverse.IsInvertible)
            {
                inverse.Dispose();
                throw new InvalidOperationException("A zero-scale element cannot be resized.");
            }

            inverse.Invert();
        }

        public void Apply(float worldDx, float worldDy, bool keepAspect, bool snap)
        {
            var delta = new[]
            {
                new PointF(worldDx, worldDy)
            };
            inverse.TransformVectors(delta);
            bool left = handle == 0 || handle == 6 || handle == 7, right = handle == 2 || handle == 3 || handle == 4, top = handle <= 2, bottom = handle >= 4 && handle <= 6;
            float nw = Math.Max(1, bounds.Width + (left ? -delta[0].X : right ? delta[0].X : 0)), nh = Math.Max(1, bounds.Height + (top ? delta[0].Y : bottom ? -delta[0].Y : 0));
            if (snap)
            {
                if (left || right)
                    nw = Math.Max(1, (float)Math.Round(nw / 5) * 5);
                if (top || bottom)
                    nh = Math.Max(1, (float)Math.Round(nh / 5) * 5);
            }

            if (keepAspect && (left || right) && (top || bottom))
            {
                float fx = nw / bounds.Width, fy = nh / bounds.Height, f = Math.Abs(fx - 1) > Math.Abs(fy - 1) ? fx : fy;
                f = Math.Max(f, Math.Max(1 / bounds.Width, 1 / bounds.Height));
                nw = bounds.Width * f;
                nh = bounds.Height * f;
            }

            if (nw > 100000 || nh > 100000)
                return;
            float nl = left ? bounds.Right - nw : bounds.Left, nt = top ? bounds.Top + nh : bounds.Bottom;
            float dx, dy, newSx = sx, newSy = sy;
            if (group)
            {
                float fx = nw / bounds.Width, fy = nh / bounds.Height;
                newSx = sx * fx;
                newSy = sy * fy;
                if (Math.Abs(newSx) > 1000 || Math.Abs(newSy) > 1000)
                    return;
                dx = nl - bounds.Left * fx;
                dy = nt - bounds.Bottom * fy;
            }
            else
            {
                dx = nl + (pane.Origin % 3) * nw / 2;
                dy = nt - (pane.Origin / 3) * nh / 2;
            }

            double a = rotation * Math.PI / 180;
            float nx = x + (float)(Math.Cos(a) * sx * dx - Math.Sin(a) * sy * dy), ny = y + (float)(Math.Sin(a) * sx * dx + Math.Cos(a) * sy * dy);
            if (Math.Abs(nx) > 100000 || Math.Abs(ny) > 100000)
                return;
            pane.X = nx;
            pane.Y = ny;
            if (group)
            {
                pane.ScaleX = newSx;
                pane.ScaleY = newSy;
            }
            else
            {
                pane.Width = nw;
                pane.Height = nh;
            }
        }

        public void Restore()
        {
            pane.X = x;
            pane.Y = y;
            pane.Width = w;
            pane.Height = h;
            pane.ScaleX = sx;
            pane.ScaleY = sy;
        }

        public void Dispose()
        {
            inverse.Dispose();
        }
    }
}
