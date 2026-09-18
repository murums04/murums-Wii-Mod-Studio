using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class HudLayoutCanvas : Control
    {
        public float ScreenWidth, ScreenHeight;
        public string ScreenLabel;
        public BrlytDocument Document;
        public BrlytPaneInfo Selected;
        public Func<BrlytPaneInfo, Bitmap> Texture;
        public Action<BrlytPaneInfo> SelectPane;
        public Action BeginTransform;
        public Action CommitMove;
        public Action PositionChanged;
        public bool Grid = true, Outlines = true, Snap, MoveWhole;
        public float Zoom = 1;
        public Bitmap Reference;
        readonly Dictionary<BrlytPaneInfo, PointF[]> polygons = new Dictionary<BrlytPaneInfo, PointF[]>();
        float panX, panY, panStartX, panStartY;
        Point panStart;
        bool panning;
        float scale = 1, cx, cy;
        bool dragging, moved;
        Point start;
        float startX, startY;
        readonly PointF[] handles = new PointF[8];
        bool handlesVisible;
        RectangleF selectionBounds;
        HudResizeGesture resize;
        public HudLayoutCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(37, 38, 46);
            Dock = DockStyle.Fill;
            TabStop = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public void ResetView()
        {
            Zoom = 1;
            panX = panY = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            polygons.Clear();
            handlesVisible = false;
            if (Document == null)
            {
                TextRenderer.DrawText(g, L.T("Archiv öffnen und ein Layout auswählen", "Open an archive and choose a layout"), Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            float viewW = ScreenWidth > 0 ? ScreenWidth : Document.LayoutWidth, viewH = ScreenHeight > 0 ? ScreenHeight : Document.LayoutHeight;
            scale = Math.Max(.001f, Math.Min(Math.Max(1, Width - 40) / viewW, Math.Max(1, Height - 40) / viewH) * Zoom);
            cx = Width / 2f + panX;
            cy = Height / 2f + panY;
            // lyt1 byte 8 selects a centered coordinate system. Non-centered layouts start at the upper left.
            int lyt = Document.HeaderSize;
            bool centered = lyt + 9 < Document.Data.Length && System.Text.Encoding.ASCII.GetString(Document.Data, lyt, 4) == "lyt1" && Document.Data[lyt + 8] != 0;
            float ox = cx - viewW * scale / 2, oy = cy - viewH * scale / 2;
            if (!centered && ScreenWidth <= 0)
            {
                cx = ox;
                cy = oy;
            }

            var frame = new RectangleF(ox, oy, viewW * scale, viewH * scale);
            if (Reference != null)
                g.DrawImage(Reference, frame);
            using (var pen = new Pen(Color.FromArgb(52, 130, 140, 170)))
            {
                if (Grid && scale * 20 >= 5)
                {
                    for (float x = ox; x <= frame.Right; x += 20 * scale)
                        g.DrawLine(pen, x, oy, x, frame.Bottom);
                    for (float y = oy; y <= frame.Bottom; y += 20 * scale)
                        g.DrawLine(pen, ox, y, frame.Right, y);
                }

                g.DrawRectangle(pen, frame.X, frame.Y, frame.Width, frame.Height);
            }

            foreach (var p in Document.Panes)
            {
                var pts = HudLayoutGeometry.Corners(p);
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new PointF(cx + pts[i].X * scale, cy - pts[i].Y * scale);
                if (pts.Any(v => float.IsNaN(v.X) || float.IsInfinity(v.X) || Math.Abs(v.X) > 1e7 || float.IsNaN(v.Y) || float.IsInfinity(v.Y) || Math.Abs(v.Y) > 1e7))
                    continue;
                polygons[p] = pts;
                bool visible = HudLayoutGeometry.Visible(p);
                if (visible && p.Width > 0 && p.Height > 0 && Math.Abs(p.ScaleX * p.ScaleY) > 0.00001)
                {
                    Bitmap b = Texture == null ? null : Texture(p);
                    if (b != null)
                    {
                        using (var attr = new ImageAttributes())
                        {
                            var cm = new ColorMatrix();
                            cm.Matrix33 = HudLayoutGeometry.Alpha(p);
                            attr.SetColorMatrix(cm);
                            g.DrawImage(b, new[] { pts[0], pts[1], pts[3] }, new RectangleF(0, 0, b.Width, b.Height), GraphicsUnit.Pixel, attr);
                        }
                    }
                    else if (p.Magic == "txt1")
                    {
                        var bounds = PolygonBounds(pts);
                        using (var brush = new SolidBrush(Color.FromArgb(40, 170, 150, 255)))
                            g.FillPolygon(brush, pts);
                        TextRenderer.DrawText(g, string.IsNullOrEmpty(p.EmbeddedText) ? "[" + p.Name + "]" : p.EmbeddedText, Font, Rectangle.Round(bounds), Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
                    }
                }

                if (Outlines || p == Selected)
                {
                    using (var pen = new Pen(p == Selected ? Color.FromArgb(192, 155, 255) : Color.FromArgb(visible ? 85 : 35, 180, 190, 220), p == Selected ? 2 : 1))
                    {
                        if (!visible)
                            pen.DashStyle = DashStyle.Dash;
                        g.DrawPolygon(pen, pts);
                    }

                    if (p == Selected)
                    {
                        var b = PolygonBounds(pts);
                        g.DrawLine(Pens.MediumPurple, cx + p.X * scale - 5, cy - p.Y * scale, cx + p.X * scale + 5, cy - p.Y * scale);
                        TextRenderer.DrawText(g, p.Name, Font, new Point((int)b.Left, (int)b.Top - 22), Color.White, Color.FromArgb(60, 42, 90));
                    }
                }
            }

            // Keep the viewport edge visible even when a large texture covers the entire layout.
            DrawOutsideShade(g, ClientRectangle, frame);
            using (var edge = new Pen(Color.FromArgb(215, 220, 235), 2))
                g.DrawRectangle(edge, frame.X, frame.Y, frame.Width, frame.Height);
            TextRenderer.DrawText(g, (ScreenLabel ?? L.T("Bildschirmreferenz (Layout)", "Screen reference (layout)")) + "  " + viewW.ToString("0.#") + " × " + viewH.ToString("0.#"), Font, new Point((int)frame.Left, Math.Max(0, (int)frame.Top - 19)), Color.White, BackColor);
            DrawSelection(g, frame);
        }

        internal static void DrawOutsideShade(Graphics graphics, Rectangle viewport, RectangleF frame)
        {
            // One clipped region prevents overlapping translucent rectangles when panning
            // the screen reference partly or completely beyond the canvas edge.
            using (var outside = new Region(viewport))
            using (var shade = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                outside.Exclude(frame);
                graphics.FillRegion(shade, outside);
            }
        }

        bool LocalSelectionBounds(out RectangleF result)
        {
            result = RectangleF.Empty;
            if (Selected == null)
                return false;
            var panes = Selected.Children.Count == 0 ? new[]
            {
                Selected
            }

            : Document.Panes.Where(p => (p == Selected || IsChild(p, Selected)) && p.Magic != "pan1" && p.Width > 0 && p.Height > 0 && HudLayoutGeometry.Visible(p)).ToArray();
            var points = panes.SelectMany(HudLayoutGeometry.Corners).ToArray();
            if (points.Length == 0)
                return false;
            using (var inv = HudLayoutGeometry.World(Selected))
            {
                if (!inv.IsInvertible)
                    return false;
                inv.Invert();
                inv.TransformPoints(points);
            }

            result = PolygonBounds(points);
            return result.Width > .001f && result.Height > .001f && !float.IsNaN(result.Width) && !float.IsInfinity(result.Width);
        }

        void DrawSelection(Graphics g, RectangleF frame)
        {
            if (!LocalSelectionBounds(out selectionBounds))
                return;
            float l = selectionBounds.Left, r = selectionBounds.Right, t = selectionBounds.Bottom, b = selectionBounds.Top, mx = (l + r) / 2, my = (t + b) / 2;
            PointF[] pts =
            {
                new PointF(l, t),
                new PointF(mx, t),
                new PointF(r, t),
                new PointF(r, my),
                new PointF(r, b),
                new PointF(mx, b),
                new PointF(l, b),
                new PointF(l, my)
            };
            using (var matrix = HudLayoutGeometry.World(Selected))
                matrix.TransformPoints(pts);
            for (int i = 0; i < 8; i++)
            {
                handles[i] = new PointF(cx + pts[i].X * scale, cy - pts[i].Y * scale);
                if (float.IsNaN(handles[i].X) || float.IsNaN(handles[i].Y) || Math.Abs(handles[i].X) > 1e7 || Math.Abs(handles[i].Y) > 1e7)
                    return;
            }

            handlesVisible = true;
            using (var pen = new Pen(Color.FromArgb(195, 150, 255), 2))
                g.DrawPolygon(pen, new[] { handles[0], handles[2], handles[4], handles[6] });
            for (int i = 0; i < 8; i++)
            {
                var box = new RectangleF(handles[i].X - 4, handles[i].Y - 4, 8, 8);
                g.FillRectangle(Brushes.White, box);
                g.DrawRectangle(Pens.MediumPurple, box.X, box.Y, box.Width, box.Height);
            }

            var bound = PolygonBounds(handles);
            string location = L.T("Links ", "Left ") + ((bound.Left - frame.Left) / frame.Width * 100).ToString("0.0") + "%  ·  " + L.T("Oben ", "Top ") + ((bound.Top - frame.Top) / frame.Height * 100).ToString("0.0") + "%";
            TextRenderer.DrawText(g, location + L.T("  |  Ziehpunkte: Größe · Umschalt: Proportionen", "  |  Handles: resize · Shift: proportions"), Font, new Point(8, Math.Max(0, Height - 24)), Color.White, BackColor);
        }

        int HitHandle(Point p)
        {
            if (!handlesVisible)
                return -1;
            for (int i = 0; i < 8; i++)
                if (Math.Abs(p.X - handles[i].X) <= 7 && Math.Abs(p.Y - handles[i].Y) <= 7)
                    return i;
            return -1;
        }

        Cursor HandleCursor(int index)
        {
            if (index < 0)
                return Cursors.Default;
            int opposite = (index + 4) % 8;
            double angle = Math.Atan2(handles[index].Y - handles[opposite].Y, handles[index].X - handles[opposite].X) * 180 / Math.PI;
            int axis = ((int)Math.Round(angle / 45) + 8) % 4;
            return axis == 0 ? Cursors.SizeWE : axis == 1 ? Cursors.SizeNWSE : axis == 2 ? Cursors.SizeNS : Cursors.SizeNESW;
        }

        static bool IsChild(BrlytPaneInfo p, BrlytPaneInfo parent)
        {
            for (var q = p.Parent; q != null; q = q.Parent)
                if (q == parent)
                    return true;
            return false;
        }

        static RectangleF PolygonBounds(PointF[] p)
        {
            float x = p.Min(v => v.X), y = p.Min(v => v.Y);
            return new RectangleF(x, y, p.Max(v => v.X) - x, p.Max(v => v.Y) - y);
        }

        bool Hit(BrlytPaneInfo p, Point pt)
        {
            if (p.Magic == "pan1" && p.Children.Any(c => Hit(c, pt)))
                return true;
            PointF[] poly;
            if (!polygons.TryGetValue(p, out poly))
                return false;
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(poly);
                return path.IsVisible(pt);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Middle && Document != null && !dragging)
            {
                Focus();
                panning = true;
                panStart = e.Location;
                panStartX = panX;
                panStartY = panY;
                Capture = true;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (panning)
                return;
            if (e.Button != MouseButtons.Left || Document == null)
                return;
            Focus();
            int handle = HitHandle(e.Location);
            if (handle >= 0 && Selected != null)
            {
                try
                {
                    resize = new HudResizeGesture(Selected, selectionBounds, handle);
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                start = e.Location;
                dragging = true;
                moved = false;
                Capture = true;
                Cursor = HandleCursor(handle);
                return;
            }

            var hit = Selected != null && Hit(Selected, e.Location) ? Selected : Document.Panes.LastOrDefault(p => HudLayoutGeometry.Visible(p) && p.Magic != "pan1" && Hit(p, e.Location));
            if (hit == null)
                return;
            if (MoveWhole)
                while (hit.Parent != null)
                    hit = hit.Parent;
            if (SelectPane != null)
                SelectPane(hit);
            Selected = hit;
            start = e.Location;
            startX = hit.X;
            startY = hit.Y;
            dragging = true;
            moved = false;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (panning)
            {
                panX = panStartX + e.X - panStart.X;
                panY = panStartY + e.Y - panStart.Y;
                Invalidate();
                return;
            }

            if (!dragging || Selected == null)
            {
                Cursor = HandleCursor(HitHandle(e.Location));
                return;
            }

            try
            {
                if (!moved && e.Location != start)
                {
                    if (BeginTransform != null)
                        BeginTransform();
                    moved = true;
                }

                if (resize != null)
                {
                    resize.Apply((e.X - start.X) / scale, -(e.Y - start.Y) / scale, (ModifierKeys & Keys.Shift) != 0, Snap);
                    if (PositionChanged != null)
                        PositionChanged();
                    Invalidate();
                    return;
                }

                var delta = HudLayoutGeometry.LocalDelta(Selected, (e.X - start.X) / scale, -(e.Y - start.Y) / scale);
                float x = startX + delta.X, y = startY + delta.Y;
                if (Snap)
                {
                    x = (float)Math.Round(x / 5) * 5;
                    y = (float)Math.Round(y / 5) * 5;
                }

                if (Math.Abs(x) > 100000 || Math.Abs(y) > 100000)
                    return;
                Selected.X = x;
                Selected.Y = y;
                moved |= x != startX || y != startY;
                if (PositionChanged != null)
                    PositionChanged();
                Invalidate();
            }
            catch (InvalidOperationException)
            {
                Cursor = Cursors.No;
            }
        }

        void EndDrag()
        {
            if (!dragging)
                return;
            dragging = false;
            Capture = false;
            Cursor = Cursors.Default;
            if (resize != null)
            {
                resize.Dispose();
                resize = null;
            }

            if (moved && CommitMove != null)
                CommitMove();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (panning)
            {
                if (e.Button == MouseButtons.Middle)
                {
                    panning = false;
                    Capture = false;
                    Cursor = Cursors.Default;
                }

                return;
            }

            if (e.Button == MouseButtons.Left)
                EndDrag();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                panning = false;
                Cursor = Cursors.Default;
                EndDrag();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (dragging || panning)
                return;
            Zoom = Math.Max(.25f, Math.Min(4, Zoom * (e.Delta > 0 ? 1.15f : 1 / 1.15f)));
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && resize != null)
            {
                resize.Dispose();
                resize = null;
            }

            if (disposing && Reference != null)
            {
                Reference.Dispose();
                Reference = null;
            }

            base.Dispose(disposing);
        }
    }
}
