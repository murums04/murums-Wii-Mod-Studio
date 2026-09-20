using System;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterModelViewport : Control, IMessageFilter
    {
        CharacterModelImport model;
        RigRasterizer rasterizer;
        internal int SelectedBone = -1;
        internal float PoseDegrees;
        internal int GameContext;
        internal bool EditGameJoints;
        internal CharacterModelImport ReferenceModel;
        internal float[][] AnimationPoints;
        internal bool ShowWeights, Marking, ShowBones, EditJoints, BrushSelection, ReferencePose;
        internal bool ThroughSelection;
        internal int BrushRadius = 20;
        internal event EventHandler JointMoveStarted, JointMoved, BoneSelected;
        internal PointF[] ProjectedJoints { get; private set; }
        int draggedJoint = -1;
        bool brushDrag;
        Point brushPoint;
        float[] originalJoint;
        Point jointStart;
        double projectionScale = 1;
        double projectionYaw, projectionPitch;
        bool subtractSelection;
        bool addSelection;
        internal readonly HashSet<int> SelectedVertices = new HashSet<int>();
        internal event EventHandler SelectionChanged;
        Point markStart;
        Rectangle mark;
        bool markingDrag;
        PointF[] projected;
        float yaw = -.55f, pitch = .3f, zoom = 1, modelScale = 1;
        PointF pan;
        Point last;
        MouseButtons drag;
        readonly ToolTip tip = new ToolTip();
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        internal CharacterModelImport Model { get { return model; } set { model = value; rasterizer = value != null && value.Rig != null ? new RigRasterizer(value.Rig) : null; SelectedVertices.Clear(); ResetView(); } }
        internal float ModelScale { get { return modelScale; } set { modelScale = value; Invalidate(); } }
        internal float Zoom { get { return zoom; } }
        internal PointF Pan { get { return pan; } }

        public CharacterModelViewport()
        {
            Dock = DockStyle.Fill;
            BackColor = DarkTheme.Panel2;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            tip.SetToolTip(this, L.T("Rechts ziehen: drehen • Mausrad: Zoom • Mausrad ziehen: verschieben • Links: gewähltes Werkzeug",
                "Right-drag: orbit • Wheel: zoom • Middle-drag: pan • Left: current tool"));
            Application.AddMessageFilter(this);
        }

        internal bool Wireframe { get; set; }
        internal void ResetView()
        {
            yaw = -.55f; pitch = .3f; zoom = 1; pan = PointF.Empty;
            drag = MouseButtons.None; draggedJoint = -1; brushDrag = markingDrag = false; Capture = false; Cursor = Cursors.Default; Invalidate();
        }
        internal void SetView(int view)
        {
            ResetView();
            if (view == 1) { yaw = 0; pitch = 0; }
            else if (view == 2) { yaw = (float)Math.PI / 2; pitch = 0; }
            else if (view == 3) { yaw = 0; pitch = (float)Math.PI / 2; }
            else if (view == 4) { yaw = (float)Math.PI; pitch = 0; }
            Invalidate();
        }
        internal void ZoomAt(Point point, int delta)
        {
            if (model == null || !Enabled || delta == 0) return;
            float next = (float)Math.Max(.05, Math.Min(50, zoom * Math.Pow(1.2, delta / 120.0)));
            float ratio = next / zoom;
            float x = point.X - Width / 2f, y = point.Y - Height / 2f;
            pan = new PointF(x - (x - pan.X) * ratio, y - (y - pan.Y) * ratio);
            zoom = next; Invalidate();
        }
        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x020A || !Visible || !Enabled || !IsHandleCreated || model == null) return false;
            long packed = message.LParam.ToInt64();
            var point = new Point(unchecked((short)packed), unchecked((short)(packed >> 16)));
            if (WindowFromPoint(point) != Handle) return false;
            ZoomAt(PointToClient(point), unchecked((short)(message.WParam.ToInt64() >> 16)));
            return true;
        }
        protected override void OnMouseWheel(MouseEventArgs e) { ZoomAt(e.Location, e.Delta); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (model != null)
            {
                Focus();
                if (((EditJoints && !ReferencePose) || EditGameJoints) && e.Button == MouseButtons.Left && model.Rig != null)
                {
                    int joint = HitJoint(e.Location);
                    if (joint >= 0)
                    {
                        SelectedBone = joint;
                        if (BoneSelected != null) BoneSelected(this, EventArgs.Empty);
                        if (JointMoveStarted != null) JointMoveStarted(this, EventArgs.Empty);
                        draggedJoint = joint;
                        originalJoint = EditGameJoints ? model.Rig.GameJoints(GameContext)[joint] : (float[])model.Rig.JointGuides[joint].Clone();
                        jointStart = e.Location;
                        Capture = true;
                        Cursor = Cursors.SizeAll;
                    }
                    return;
                }
                if (Marking && e.Button == MouseButtons.Left)
                {
                    subtractSelection = (ModifierKeys & Keys.Control) != 0;
                    addSelection = (ModifierKeys & Keys.Shift) != 0;
                    if (BrushSelection)
                    {
                        brushDrag = true;
                        Capture = true;
                        BrushAt(e.Location);
                    }
                    else
                    {
                        markingDrag = true; markStart = e.Location; mark = Rectangle.Empty; Capture = true;
                    }
                    return;
                }
                if (e.Button == MouseButtons.Middle && e.Clicks == 2) { ResetView(); return; }
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
                {
                    drag = e.Button; last = e.Location; Capture = true;
                    Cursor = e.Button == MouseButtons.Middle ? Cursors.SizeAll : Cursors.Hand;
                }
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            brushPoint = e.Location;
            if (draggedJoint >= 0)
            {
                var position = DragPosition(originalJoint, e.X - jointStart.X, e.Y - jointStart.Y);
                if (EditGameJoints) model.Rig.SetGameJoint(GameContext, draggedJoint, position);
                else model.Rig.SetJointGuide(draggedJoint, position);
                Invalidate();
                return;
            }
            if (brushDrag) { BrushAt(e.Location); return; }
            if (markingDrag)
            {
                mark = Rectangle.FromLTRB(Math.Min(markStart.X, e.X), Math.Min(markStart.Y, e.Y), Math.Max(markStart.X, e.X), Math.Max(markStart.Y, e.Y));
                Invalidate(); return;
            }
            if (drag == MouseButtons.Left || drag == MouseButtons.Right)
            {
                yaw = (yaw + (e.X - last.X) * .012f) % ((float)Math.PI * 2);
                pitch = Math.Max(-1.56f, Math.Min(1.56f, pitch + (e.Y - last.Y) * .012f));
            }
            else if (drag == MouseButtons.Middle)
                pan = new PointF(pan.X + e.X - last.X, pan.Y + e.Y - last.Y);
            if (drag != MouseButtons.None) { last = e.Location; Invalidate(); }
            else if (EditJoints || EditGameJoints) Cursor = HitJoint(e.Location) >= 0 ? Cursors.SizeAll : Cursors.Default;
            else if (Marking && BrushSelection) Invalidate();
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (draggedJoint >= 0 && e.Button == MouseButtons.Left)
            {
                bool moved = e.Location != jointStart;
                draggedJoint = -1;
                Capture = false;
                Cursor = Cursors.Default;
                if (moved && JointMoved != null) JointMoved(this, EventArgs.Empty);
            }
            if (brushDrag && e.Button == MouseButtons.Left) { brushDrag = false; Capture = false; }
            if (markingDrag && e.Button == MouseButtons.Left)
            {
                markingDrag = false; Capture = false;
                if (!addSelection && !subtractSelection) SelectedVertices.Clear();
                SelectRegion(mark, false, Point.Empty);
                mark = Rectangle.Empty; if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); Invalidate();
            }
            if (e.Button == drag) { drag = MouseButtons.None; Capture = false; Cursor = Cursors.Default; }
            base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture)
            {
                if (draggedJoint >= 0 && originalJoint != null)
                {
                    if (EditGameJoints) model.Rig.SetGameJoint(GameContext, draggedJoint, originalJoint);
                    else model.Rig.SetJointGuide(draggedJoint, originalJoint);
                    Invalidate();
                }
                markingDrag = brushDrag = false;
                draggedJoint = -1;
                drag = MouseButtons.None;
                Cursor = Cursors.Default;
            }
            base.OnMouseCaptureChanged(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (model == null)
            {
                TextRenderer.DrawText(g, L.T("3D-Modell importieren, um es anzusehen.", "Import a 3D model to preview it."), Font, ClientRectangle, DarkTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            float[] min = { model.Points.Min(p => p[0]), model.Points.Min(p => p[1]), model.Points.Min(p => p[2]) };
            float[] max = { model.Points.Max(p => p[0]), model.Points.Max(p => p[1]), model.Points.Max(p => p[2]) };
            double radius = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => Math.Pow((double)max[i] - min[i], 2)));
            double fit = Math.Min(Width, Height) * .75 / Math.Max(radius * model.FitScale, .0001);
            if (GameContext > 0)
                fit = Math.Min(Width / Math.Max(.0001, max[0] - min[0] + max[2] - min[2]), Height / Math.Max(.0001, max[1] - min[1])) * .9 / Math.Max(.0001, model.FitScale);
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw), cp = Math.Cos(pitch), sp = Math.Sin(pitch);
            projectionScale = fit * zoom * modelScale;
            projectionYaw = yaw; projectionPitch = pitch;
            var display = AnimationPoints ?? (model.Rig == null ? model.Points.ToArray() : GameContext > 0 ? model.Rig.GameGeometry(GameContext, false) : model.Rig.Pose(SelectedBone, PoseDegrees, ReferencePose));
            var points = new PointF[model.Points.Count];
            var depths = new double[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = display[i];
                double x = (p[0] - ((double)min[0] + max[0]) / 2) * modelScale;
                double y = (p[1] - ((double)min[1] + max[1]) / 2) * modelScale;
                double z = (p[2] - ((double)min[2] + max[2]) / 2) * modelScale;
                double rx = x * cy + z * sy, rz = -x * sy + z * cy;
                double ry = y * cp - rz * sp;
                depths[i] = y * sp + rz * cp;
                points[i] = new PointF((float)(Width / 2.0 + pan.X + rx * fit * zoom), (float)(Height / 2.0 + pan.Y - ry * fit * zoom));
            }
            using (var grid = new Pen(Color.FromArgb(55, 57, 67)))
            {
                g.DrawLine(grid, 0, Height / 2f + pan.Y, Width, Height / 2f + pan.Y);
                g.DrawLine(grid, Width / 2f + pan.X, 0, Width / 2f + pan.X, Height);
            }
            projected = points;
            if (rasterizer != null && !Wireframe)
                using (var bitmap = rasterizer.Draw(ClientSize, points, depths, SelectedBone, ShowWeights)) g.DrawImageUnscaled(bitmap, 0, 0);
            var faceColors = rasterizer == null || Wireframe
                ? model.Faces.Select((face, index) => new { face, index }).ToDictionary(p => p.face, p => p.index) : null;
            using (var edge = new Pen(Color.FromArgb(145, 119, 210)))
            {
                foreach (var face in (rasterizer == null || Wireframe ? model.Faces : new List<int[]>()).OrderBy(f => f.Average(i => depths[i])))
                {
                    var polygon = face.Select(i => points[i]).ToArray();
                    if (!Wireframe)
                    {
                        var a = polygon[0]; var b = polygon[1]; var c = polygon[2];
                        double area = Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
                        int shade = 70 + (int)Math.Min(85, Math.Sqrt(area) * .4);
                        int faceIndex = faceColors[face];
                        Color surface = faceIndex < model.FaceColors.Count ? model.FaceColors[faceIndex] : Color.FromArgb(shade, shade, Math.Min(255, shade + 45));
                        using (var brush = new SolidBrush(surface))
                            g.FillPolygon(brush, polygon);
                    }
                    if (Wireframe) g.DrawPolygon(edge, polygon);
                }
            }
            if (ReferenceModel != null)
            {
                Func<float[], PointF> projectReference = p => {
                    double x = (p[0] - ((double)min[0] + max[0]) / 2) * modelScale;
                    double y = (p[1] - ((double)min[1] + max[1]) / 2) * modelScale;
                    double z = (p[2] - ((double)min[2] + max[2]) / 2) * modelScale;
                    double rx = x * cy + z * sy, rz = -x * sy + z * cy;
                    return new PointF((float)(Width / 2.0 + pan.X + rx * fit * zoom), (float)(Height / 2.0 + pan.Y - (y * cp - rz * sp) * fit * zoom));
                };
                var referencePoints = ReferenceModel.Points.Select(projectReference).ToArray();
                using (var outline = new Pen(Color.FromArgb(90, 90, 210, 255), 1))
                    foreach (var face in ReferenceModel.Faces) g.DrawPolygon(outline, face.Select(i => referencePoints[i]).ToArray());
                TextRenderer.DrawText(g, L.T("Blaues Gitter: Originalmodell (nur Hilfe)", "Blue wireframe: original model (reference only)"), Font, new Point(8, 30), Color.LightSkyBlue);
            }
            if (model.Rig != null && ShowBones && AnimationPoints == null)
            {
                Func<float[], PointF> projectBone = p => {
                    double x = (p[0] - ((double)min[0] + max[0]) / 2) * modelScale;
                    double y = (p[1] - ((double)min[1] + max[1]) / 2) * modelScale;
                    double z = (p[2] - ((double)min[2] + max[2]) / 2) * modelScale;
                    double rx = x * cy + z * sy, rz = -x * sy + z * cy;
                    return new PointF((float)(Width / 2.0 + pan.X + rx * fit * zoom), (float)(Height / 2.0 + pan.Y - (y * cp - rz * sp) * fit * zoom));
                };
                var positions = (GameContext > 0 ? model.Rig.GameJoints(GameContext) : model.Rig.BonePositions(SelectedBone, PoseDegrees, ReferencePose)).Select(projectBone).ToArray();
                ProjectedJoints = positions;
                using (var bonePen = new Pen(Color.FromArgb(230, 185, 80), 2))
                using (var boneBrush = new SolidBrush(Color.White))
                    for (int i = 0; i < positions.Length; i++)
                    {
                        if (!model.Rig.GuideBone(i)) continue;
                        if (model.Rig.Bones[i].Parent >= 0 && model.Rig.GuideBone(model.Rig.Bones[i].Parent)) g.DrawLine(bonePen, positions[i], positions[model.Rig.Bones[i].Parent]);
                        var point = positions[i];
                        float radiusPoint = EditJoints || EditGameJoints ? 6 : 3;
                        g.FillEllipse(i == SelectedBone ? Brushes.Cyan : boneBrush, point.X - radiusPoint, point.Y - radiusPoint, radiusPoint * 2, radiusPoint * 2);
                        if (i == SelectedBone)
                            TextRenderer.DrawText(g, ModelRigForm.BoneLabel(model.Rig.Bones[i].Name), Font, new Point((int)point.X + 10, (int)point.Y + 8), Color.Cyan, Color.FromArgb(35, 36, 44));
                    }
                if (GameContext > 0)
                    using (var warning = new Pen(Color.Orange, 2))
                        foreach (var contact in model.Rig.MissedContacts(GameContext))
                        {
                            int joint = Array.FindIndex(model.Rig.Bones, b => b.Name == contact.Joint);
                            var target = projectBone(contact.Target);
                            var actual = positions[joint];
                            g.DrawLine(warning, actual, target);
                            g.DrawEllipse(warning, actual.X - 8, actual.Y - 8, 16, 16);
                            g.DrawLine(warning, target.X - 5, target.Y - 5, target.X + 5, target.Y + 5);
                            g.DrawLine(warning, target.X - 5, target.Y + 5, target.X + 5, target.Y - 5);
                        }
            }
            using (var highlight = new SolidBrush(Color.Yellow))
                foreach (int vertex in SelectedVertices) if (vertex < points.Length) g.FillRectangle(highlight, points[vertex].X - 1, points[vertex].Y - 1, 3, 3);
            if (!mark.IsEmpty) using (var pen = new Pen(Color.Yellow)) g.DrawRectangle(pen, mark);
            if (Marking && BrushSelection && ClientRectangle.Contains(brushPoint))
                using (var pen = new Pen(Color.Cyan, 2)) g.DrawEllipse(pen, brushPoint.X - BrushRadius, brushPoint.Y - BrushRadius, BrushRadius * 2, BrushRadius * 2);
            string label = "Geometry • " + (modelScale * 100).ToString("0.##") + "% • X " + ((max[0] - min[0]) * modelScale).ToString("0.###") +
                "  Y " + ((max[1] - min[1]) * modelScale).ToString("0.###") + "  Z " + ((max[2] - min[2]) * modelScale).ToString("0.###") + " (source units)";
            if (model.ReferenceHeight > 0) label += " • RR: " + (100 * modelScale / model.FitScale).ToString("0.#") + "%";
            TextRenderer.DrawText(g, label, Font, new Point(8, 8), DarkTheme.Fore);
        }
        internal int HitJoint(Point point)
        {
            if (ProjectedJoints == null) return -1;
            return Enumerable.Range(0, ProjectedJoints.Length)
                .Where(i => model.Rig.GuideBone(i))
                .Select(i => new { Index = i, Distance = Math.Pow(point.X - ProjectedJoints[i].X, 2) + Math.Pow(point.Y - ProjectedJoints[i].Y, 2) })
                .Where(p => p.Distance <= 225).OrderBy(p => p.Distance).Select(p => p.Index).DefaultIfEmpty(-1).First();
        }

        internal float[] DragPosition(float[] point, float dx, float dy)
        {
            double x = dx / Math.Max(.00001, projectionScale), y = -dy / Math.Max(.00001, projectionScale);
            double cy = Math.Cos(projectionYaw), sy = Math.Sin(projectionYaw), cp = Math.Cos(projectionPitch), sp = Math.Sin(projectionPitch);
            return new[] { (float)(point[0] + cy * x + sy * sp * y), (float)(point[1] + cp * y), (float)(point[2] + sy * x - cy * sp * y) };
        }

        void BrushAt(Point point)
        {
            SelectRegion(new Rectangle(point.X - BrushRadius, point.Y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1), true, point);
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
        }

        void SelectRegion(Rectangle area, bool circle, Point center)
        {
            if (projected == null) return;
            var selection = new HashSet<int>();
            if (ThroughSelection || rasterizer == null || rasterizer.SurfaceFaces == null)
            {
                for (int i = 0; i < projected.Length; i++)
                    if (area.Contains(Point.Round(projected[i])) && (!circle || Math.Pow(projected[i].X - center.X, 2) + Math.Pow(projected[i].Y - center.Y, 2) <= BrushRadius * BrushRadius)) selection.Add(i);
            }
            else
            {
                var clipped = Rectangle.Intersect(ClientRectangle, area);
                for (int y = clipped.Top; y < clipped.Bottom; y += 2)
                    for (int x = clipped.Left; x < clipped.Right; x += 2)
                    {
                        if (circle && (x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) > BrushRadius * BrushRadius) continue;
                        int pixel = y * rasterizer.SurfaceWidth + x;
                        if (pixel >= rasterizer.SurfaceFaces.Length) continue;
                        int face = rasterizer.SurfaceFaces[pixel];
                        if (face >= 0) foreach (int vertex in model.Faces[face]) selection.Add(vertex);
                    }
            }
            if (subtractSelection) SelectedVertices.ExceptWith(selection);
            else SelectedVertices.UnionWith(selection);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Application.RemoveMessageFilter(this); tip.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
