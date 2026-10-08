using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        internal sealed class SurfaceCollisionProbe
        {
            sealed class Node
            {
                internal int Start, Count;
                internal Node Left, Right;
                internal readonly double[] Min = new double[3], Max = new double[3];
            }
            struct Vector
            {
                internal double X, Y, Z;
                internal Vector(double x, double y, double z) { X = x; Y = y; Z = z; }
                internal static Vector From(float[] p) { return new Vector(p[0], p[1], p[2]); }
                internal static Vector Sub(Vector a, Vector b) { return new Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
                internal static Vector Cross(Vector a, Vector b) { return new Vector(a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X); }
                internal static double Dot(Vector a, Vector b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
                internal static Vector Scale(Vector a, double s) { return new Vector(a.X*s, a.Y*s, a.Z*s); }
            }

            readonly ModelRig rig;
            readonly int[] order;
            readonly int[][] seamIds;
            readonly double epsilon;
            readonly Node root;
            readonly HashSet<long> originalPairs;
            float[][] points;
            double[][] faceMin, faceMax;

            internal SurfaceCollisionProbe(ModelRig model)
            {
                if (model == null || model.Points == null || model.Points.Length == 0 || model.Faces == null
                    || model.FaceMaterials == null || model.FaceMaterials.Length != model.Faces.Length
                    || model.Materials == null || model.Materials.Length == 0
                    || model.Faces.Any(f => f == null || f.Length != 3 || f.Any(v => v < 0 || v >= model.Points.Length))
                    || model.FaceMaterials.Any(m => m < 0 || m >= model.Materials.Length)
                    || model.Materials.Any(m => m == null || m.Color == null || m.Color.Length != 4 || Single.IsNaN(m.Color[3]) || Single.IsInfinity(m.Color[3])))
                    throw new InvalidDataException("Invalid collision surface.");
                CheckGeometry(model.Points, model.Points.Length);
                rig = model;
                double height = (double)model.Points.Max(p => p[1]) - model.Points.Min(p => p[1]);
                if (height < .000001) height = Enumerable.Range(0, 3).Max(a => (double)model.Points.Max(p => p[a]) - model.Points.Min(p => p[a]));
                epsilon = Math.Max(height * .00001, 1e-10);
                var seams = new Dictionary<Tuple<double, double, double>, int>();
                var vertices = new int[model.Points.Length];
                for (int v = 0; v < vertices.Length; v++)
                {
                    var p = model.Points[v];
                    var key = Tuple.Create(Math.Round(p[0] / epsilon), Math.Round(p[1] / epsilon), Math.Round(p[2] / epsilon));
                    int seam;
                    if (!seams.TryGetValue(key, out seam)) { seam = seams.Count; seams.Add(key, seam); }
                    vertices[v] = seam;
                }
                seamIds = model.Faces.Select(f => f.Select(v => vertices[v]).ToArray()).ToArray();
                order = Enumerable.Range(0, model.Faces.Length).Where(f => model.Materials[model.FaceMaterials[f]].Color[3] > 0).ToArray();
                points = model.Points;
                faceMin = new double[model.Faces.Length][]; faceMax = new double[model.Faces.Length][];
                UpdateFaces();
                if (order.Length > 0) root = Build(0, order.Length);
                originalPairs = FindPairs(model.Points, false);
            }

            void UpdateFaces()
            {
                foreach (int f in order)
                {
                    var face = rig.Faces[f];
                    if (faceMin[f] == null) { faceMin[f] = new double[3]; faceMax[f] = new double[3]; }
                    for (int a = 0; a < 3; a++)
                    {
                        faceMin[f][a] = Math.Min(points[face[0]][a], Math.Min(points[face[1]][a], points[face[2]][a]));
                        faceMax[f][a] = Math.Max(points[face[0]][a], Math.Max(points[face[1]][a], points[face[2]][a]));
                    }
                }
            }
            Node Build(int start, int count)
            {
                var node = new Node { Start = start, Count = count };
                Bounds(node);
                if (count <= 8) return node;
                int axis = 0;
                for (int a = 1; a < 3; a++) if (node.Max[a] - node.Min[a] > node.Max[axis] - node.Min[axis]) axis = a;
                Array.Sort(order, start, count, Comparer<int>.Create((a, b) =>
                    (faceMin[a][axis] + faceMax[a][axis]).CompareTo(faceMin[b][axis] + faceMax[b][axis])));
                node.Left = Build(start, count / 2); node.Right = Build(start + count / 2, count - count / 2);
                return node;
            }
            void Bounds(Node node)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    double min = Double.PositiveInfinity, max = Double.NegativeInfinity;
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        min = Math.Min(min, faceMin[order[i]][axis]); max = Math.Max(max, faceMax[order[i]][axis]);
                    }
                    node.Min[axis] = min; node.Max[axis] = max;
                }
            }
            void Refit(Node node)
            {
                if (node.Left == null) { Bounds(node); return; }
                Refit(node.Left); Refit(node.Right);
                for (int a = 0; a < 3; a++) { node.Min[a] = Math.Min(node.Left.Min[a], node.Right.Min[a]); node.Max[a] = Math.Max(node.Left.Max[a], node.Right.Max[a]); }
            }
            static bool Overlap(double[] aMin, double[] aMax, double[] bMin, double[] bMax)
            {
                for (int a = 0; a < 3; a++) if (aMax[a] < bMin[a] || bMax[a] < aMin[a]) return false;
                return true;
            }
            void Traverse(Node a, Node b, HashSet<long> pairs, bool newOnly)
            {
                if (!Overlap(a.Min, a.Max, b.Min, b.Max)) return;
                if (a.Left == null && b.Left == null)
                {
                    for (int i = a.Start; i < a.Start + a.Count; i++)
                        for (int j = b.Start; j < b.Start + b.Count; j++)
                        {
                            if (a == b && j <= i) continue;
                            int first = Math.Min(order[i], order[j]), second = Math.Max(order[i], order[j]);
                            long key = ((long)first << 32) | (uint)second;
                            if (first == second || newOnly && originalPairs.Contains(key)
                                || !Overlap(faceMin[first], faceMax[first], faceMin[second], faceMax[second])) continue;
                            bool shared = false;
                            foreach (int seam in seamIds[first]) if (Array.IndexOf(seamIds[second], seam) >= 0) { shared = true; break; }
                            if (!shared && ProperCross(points, rig.Faces[first], rig.Faces[second], epsilon)) pairs.Add(key);
                        }
                    return;
                }
                if (a == b) { Traverse(a.Left, a.Left, pairs, newOnly); Traverse(a.Left, a.Right, pairs, newOnly); Traverse(a.Right, a.Right, pairs, newOnly); }
                else if (a.Left != null && (b.Left == null || a.Count >= b.Count)) { Traverse(a.Left, b, pairs, newOnly); Traverse(a.Right, b, pairs, newOnly); }
                else { Traverse(a, b.Left, pairs, newOnly); Traverse(a, b.Right, pairs, newOnly); }
            }
            HashSet<long> FindPairs(float[][] geometry, bool newOnly)
            {
                CheckGeometry(geometry, rig.Points.Length);
                points = geometry; UpdateFaces();
                var result = new HashSet<long>();
                if (root != null) { Refit(root); Traverse(root, root, result, newOnly); }
                return result;
            }
            internal HashSet<long> NewPairs(float[][] geometry) { return FindPairs(geometry, true); }
            internal HashSet<int> MarkedVertices(float[][] geometry)
            {
                var marked = new HashSet<int>();
                foreach (long pair in NewPairs(geometry))
                {
                    foreach (int v in rig.Faces[(int)(pair >> 32)]) marked.Add(v);
                    foreach (int v in rig.Faces[(int)(pair & 0xffffffffL)]) marked.Add(v);
                }
                return marked;
            }
            internal int OriginalPairCount { get { return originalPairs.Count; } }
            internal long[] OriginalPairs { get { return originalPairs.OrderBy(p => p).ToArray(); } }

            static void CheckGeometry(float[][] geometry, int count)
            {
                if (geometry == null || geometry.Length != count || geometry.Any(p => p == null || p.Length != 3 || p.Any(v => Single.IsNaN(v) || Single.IsInfinity(v))))
                    throw new InvalidDataException("Invalid collision geometry.");
            }

            static bool Interval(Vector[] triangle, double[] distances, Vector axis, double tolerance, out double min, out double max)
            {
                min = Double.PositiveInfinity; max = Double.NegativeInfinity;
                for (int i = 0; i < 3; i++)
                {
                    int j = (i + 1) % 3;
                    double value;
                    if (distances[i] * distances[j] < 0)
                    {
                        double fraction = distances[i] / (distances[i] - distances[j]);
                        value = Vector.Dot(triangle[i], axis) + Vector.Dot(Vector.Sub(triangle[j], triangle[i]), axis) * fraction;
                    }
                    else if (Math.Abs(distances[i]) <= tolerance) value = Vector.Dot(triangle[i], axis);
                    else continue;
                    min = Math.Min(min, value); max = Math.Max(max, value);
                }
                return min <= max;
            }
            internal static bool ProperCross(float[][] geometry, int[] first, int[] second, double tolerance)
            {
                var a = first.Select(v => Vector.From(geometry[v])).ToArray();
                var b = second.Select(v => Vector.From(geometry[v])).ToArray();
                var na = Vector.Cross(Vector.Sub(a[1], a[0]), Vector.Sub(a[2], a[0]));
                var nb = Vector.Cross(Vector.Sub(b[1], b[0]), Vector.Sub(b[2], b[0]));
                double la = Math.Sqrt(Vector.Dot(na, na)), lb = Math.Sqrt(Vector.Dot(nb, nb));
                if (Math.Min(la, lb) <= tolerance * tolerance) return false;
                na = Vector.Scale(na, 1 / la); nb = Vector.Scale(nb, 1 / lb);
                var da = a.Select(p => Vector.Dot(Vector.Sub(p, b[0]), nb)).ToArray();
                var db = b.Select(p => Vector.Dot(Vector.Sub(p, a[0]), na)).ToArray();
                if (!(da.Min() < -tolerance && da.Max() > tolerance && db.Min() < -tolerance && db.Max() > tolerance)) return false;
                var axis = Vector.Cross(na, nb); double length = Math.Sqrt(Vector.Dot(axis, axis));
                if (length < 1e-8) return false;
                axis = Vector.Scale(axis, 1 / length);
                double aMin, aMax, bMin, bMax;
                return Interval(a, da, axis, tolerance, out aMin, out aMax) && Interval(b, db, axis, tolerance, out bMin, out bMax)
                    && Math.Min(aMax, bMax) - Math.Max(aMin, bMin) > tolerance;
            }
        }
    }
}
