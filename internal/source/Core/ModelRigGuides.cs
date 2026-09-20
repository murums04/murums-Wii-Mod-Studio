using System;
using System.Collections.Generic;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public float[][] JointGuides;
        public bool AlignToReference;
        public int[] ManualVertices;
        float[][] alignedPoints, alignedNormals;
        internal void InvalidateAlignment() { alignedPoints = alignedNormals = null; }

        internal float[][] ReferenceJoints()
        {
            return Bones.Select(b => new[] { b.Matrix[3], b.Matrix[7], b.Matrix[11] }).ToArray();
        }

        internal int SharedBodyBone(int index)
        {
            string name = Bones[index].Name;
            if (!name.StartsWith("pcd_", StringComparison.Ordinal)) return index;
            int primary = Array.FindIndex(Bones, b => b.Name == name.Substring(4));
            if (primary < 0) return index;
            double distance = new[] { 3, 7, 11 }.Sum(axis => Math.Pow(Bones[index].Matrix[axis] - Bones[primary].Matrix[axis], 2));
            return distance < .01 ? primary : index;
        }

        internal bool GuideBone(int index)
        {
            return BoneEnabled(index) && SharedBodyBone(index) == index && Bones[index].Name != "nw4r_root";
        }

        internal void FitJointGuides()
        {
            InvalidateAlignment();
            var min = Enumerable.Range(0, 3).Select(a => Points.Min(p => p[a])).ToArray();
            var max = Enumerable.Range(0, 3).Select(a => Points.Max(p => p[a])).ToArray();
            float height = max[1] - min[1], width = max[0] - min[0];
            float center = (min[0] + max[0]) / 2;
            float depth = Points.Where(p => p[1] > min[1] + height * .5f && p[1] < min[1] + height * .8f).Select(p => p[2]).DefaultIfEmpty((min[2] + max[2]) / 2).OrderBy(v => v).ElementAt(Math.Max(0, Points.Count(p => p[1] > min[1] + height * .5f && p[1] < min[1] + height * .8f) / 2));
            JointGuides = ReferenceJoints();
            for (int i = 0; i < Bones.Length; i++)
            {
                string name = Bones[i].Name;
                if (name != "skl_root" && name != "spin" && name != "face_1" && name != "mouth_1" && name != "tie_1"
                    && !name.StartsWith("arm_") && !name.StartsWith("wrist_") && !name.StartsWith("leg_") && !name.StartsWith("ankle_")) continue;
                float side = name.Contains("_l") ? 1 : name.Contains("_r") ? -1 : 0;
                float x = 0, y = .55f;
                if (name == "spin") y = .65f;
                else if (name == "face_1") y = .86f;
                else if (name == "mouth_1") y = .88f;
                else if (name == "tie_1") y = .7f;
                else if (name.StartsWith("arm_")) { x = name.EndsWith("2") ? .32f : .14f; y = .76f; }
                else if (name.StartsWith("wrist_")) { x = .44f; y = .76f; }
                else if (name.StartsWith("leg_")) { x = .065f; y = name.EndsWith("2") ? .28f : .5f; }
                else if (name.StartsWith("ankle_")) { x = .065f; y = .065f; }
                JointGuides[i] = new[] { center + side * width * x, min[1] + height * y, depth };
            }
            FitHumanShoulders(min, max);
            if (SourceJointGuides != null)
                for (int i = 0; i < Bones.Length; i++)
                    if (SourceJointGuides.ContainsKey(Bones[i].Name)) JointGuides[i] = (float[])SourceJointGuides[Bones[i].Name].Clone();
            for (int i = 0; i < Bones.Length; i++)
                if (SharedBodyBone(i) != i) JointGuides[i] = (float[])JointGuides[SharedBodyBone(i)].Clone();
        }

        void FitHumanShoulders(float[] min, float[] max)
        {
            float height = max[1] - min[1], width = max[0] - min[0], center = (min[0] + max[0]) * .5f;
            if (width < height * .65f) return;
            var arms = Points.Where(p => Math.Abs(p[0] - center) > width * .26f
                && Math.Abs(p[0] - center) < width * .40f && p[1] > min[1] + height * .6f).ToArray();
            if (arms.Length < 12) return;
            float armHeight = Quantile(arms.Select(p => p[1]), .5);
            float armDepth = Quantile(arms.Select(p => p[2]), .5);
            var chest = Points.Where(p => p[1] > armHeight - height * .10f && p[1] < armHeight - height * .055f
                && Math.Abs(p[0] - center) < height * .2f && Math.Abs(p[2] - armDepth) < height * .065f).ToArray();
            if (chest.Length < 12) return;
            // Schulterdrehpunkte aus dem Rumpfquerschnitt, nicht aus der Armspannweite.
            float shoulder = Quantile(chest.Select(p => Math.Abs(p[0] - center)), .85);
            if (shoulder < height * .045f || shoulder > height * .14f) return;
            foreach (string side in new[] { "l", "r" })
            {
                int a = PoseBone("arm_" + side + "1"), b = PoseBone("arm_" + side + "2"), c = PoseBone("wrist_" + side + "1");
                if (a < 0 || b < 0 || c < 0) continue;
                float sign = side == "l" ? 1 : -1;
                JointGuides[a] = new[] { center + sign * shoulder, armHeight, armDepth };
                JointGuides[c][1] = armHeight; JointGuides[c][2] = armDepth;
                JointGuides[b] = RigVector.Add(JointGuides[a], RigVector.Scale(RigVector.Sub(JointGuides[c], JointGuides[a]), .52));
            }
        }

        static float Quantile(IEnumerable<float> values, double fraction)
        {
            var ordered = values.OrderBy(v => v).ToArray();
            return ordered[(int)((ordered.Length - 1) * fraction)];
        }

        internal void SetJointGuide(int index, float[] point)
        {
            if (JointGuides == null || !BoneEnabled(index) || point == null || point.Length != 3
                || point.Any(v => Single.IsNaN(v) || Single.IsInfinity(v) || Math.Abs(v) > 1e8))
                throw new ArgumentException("Invalid joint position.");
            JointGuides[index] = (float[])point.Clone();
            InvalidateAlignment();
        }

        internal int SegmentEnd(int bone)
        {
            string name = Bones[bone].Name;
            string preferred = name == "skl_root" ? "spin" : name == "spin" ? "face_1"
                : name.StartsWith("arm_") ? (name.EndsWith("1") ? name.Substring(0, name.Length - 1) + "2" : "wrist_" + (name.Contains("_l") ? "l1" : "r1"))
                : name.StartsWith("leg_") ? (name.EndsWith("1") ? name.Substring(0, name.Length - 1) + "2" : "ankle_" + (name.Contains("_l") ? "l1" : "r1")) : null;
            return preferred == null ? -1 : Array.FindIndex(Bones, b => b.Name == preferred);
        }

        float[] SegmentTail(int bone, float[][] joints)
        {
            int end = SegmentEnd(bone);
            if (end >= 0) return joints[end];
            var p = joints[bone];
            int parent = Bones[bone].Parent;
            float height = Math.Max(1, ReferenceHeight);
            if (Bones[bone].Name == "face_1") return new[] { p[0], p[1] + height * .12f, p[2] };
            if (Bones[bone].Name.StartsWith("ankle")) return new[] { p[0], p[1], p[2] + height * .075f };
            if (parent >= 0)
                return RigVector.Add(p, RigVector.Scale(RigVector.Sub(p, joints[parent]), .3f));
            return new[] { p[0], p[1] + height * .08f, p[2] };
        }

        internal void ReassignFromGuides()
        {
            if (JointGuides == null) throw new InvalidOperationException("Place the joints first.");
            var candidates = Enumerable.Range(0, Bones.Length).Where(i => BoneEnabled(i) && SharedBodyBone(i) == i && Bones[i].Name != "nw4r_root" && Bones[i].Name != "mouth_1" && Bones[i].Name != "tie_1").ToArray();
            var tails = Enumerable.Range(0, Bones.Length).Select(i => SegmentTail(i, JointGuides)).ToArray();
            var indices = new int[Points.Length][];
            var influences = new float[Points.Length][];
            var fixedVertices = new HashSet<int>(ManualVertices ?? new int[0]);
            double soft = Math.Pow(Math.Max(1, ReferenceHeight) * .018, 2);
            for (int v = 0; v < Points.Length; v++)
            {
                if (fixedVertices.Contains(v))
                {
                    indices[v] = (int[])BoneIndices[v].Clone();
                    influences[v] = (float[])BoneWeights[v].Clone();
                    continue;
                }
                var near = candidates.Select(i => new { Bone = i, Distance = RigVector.SegmentDistanceSquared(Points[v], JointGuides[i], tails[i]) })
                    .OrderBy(p => p.Distance).Take(2).ToArray();
                double[] weights = near.Select(p => 1 / Math.Pow(p.Distance + soft, 2)).ToArray();
                double sum = weights.Sum();
                indices[v] = near.Select(p => p.Bone).ToArray();
                influences[v] = weights.Select(w => (float)(w / sum)).ToArray();
            }
            InvalidateAlignment();
            BoneIndices = indices;
            BoneWeights = influences;
            AlignToReference = true;
        }

        internal float[][] AlignedGeometry(bool normals)
        {
            var source = normals ? Normals : Points;
            if (!AlignToReference || JointGuides == null) return source;
            if (normals && alignedNormals != null) return alignedNormals;
            if (!normals && alignedPoints != null) return alignedPoints;
            var result = MapGeometry(normals, ReferenceJoints());
            if (normals) alignedNormals = result; else alignedPoints = result;
            return result;
        }

        internal float[][] GameBoneTransforms(int context)
        {
            var settings = GameSettings(context);
            return Enumerable.Range(0, Bones.Length).Select(bone => {
                var from = RigVector.Sub(SegmentTail(bone, JointGuides), JointGuides[bone]);
                var to = RigVector.Sub(SegmentTail(bone, settings.Joints), settings.Joints[bone]);
                double ratio = Math.Max(.1, Math.Min(10, RigVector.Length(to) / Math.Max(.0001, RigVector.Length(from))));
                var direction = RigVector.Unit(from);
                var matrix = new float[16]; matrix[15] = 1;
                for (int axis = 0; axis < 3; axis++)
                {
                    var unit = new float[3]; unit[axis] = 1;
                    var stretched = RigVector.Add(unit, RigVector.Scale(direction, RigVector.Dot(unit, direction) * (ratio - 1)));
                    var rotated = RigVector.Rotate(stretched, from, to);
                    var transformed = TransformGamePoint(rotated, settings, false, false);
                    var origin = TransformGamePoint(new float[3], settings, false, false);
                    for (int row = 0; row < 3; row++) matrix[row * 4 + axis] = transformed[row] - origin[row];
                }
                var target = TransformGamePoint(settings.Joints[bone], settings, false, false);
                var offset = RigVector.Sub(target, RigMatrix.Point(matrix, JointGuides[bone]));
                matrix[3] = offset[0]; matrix[7] = offset[1]; matrix[11] = offset[2];
                return matrix;
            }).ToArray();
        }

        internal float[][] MapGeometry(bool normals, float[][] reference)
        {
            var source = normals ? Normals : Points;
            var sourceTails = Enumerable.Range(0, Bones.Length).Select(i => SegmentTail(i, JointGuides)).ToArray();
            var targetTails = Enumerable.Range(0, Bones.Length).Select(i => SegmentTail(i, reference)).ToArray();
            var result = new float[source.Length][];
            for (int v = 0; v < source.Length; v++)
            {
                var point = new float[3];
                for (int j = 0; j < BoneIndices[v].Length; j++)
                {
                    int bone = BoneIndices[v][j];
                    var from = RigVector.Sub(sourceTails[bone], JointGuides[bone]);
                    var to = RigVector.Sub(targetTails[bone], reference[bone]);
                    var local = normals ? source[v] : RigVector.Sub(source[v], JointGuides[bone]);
                    var direction = RigVector.Unit(from);
                    double ratio = Math.Max(.1, Math.Min(10, RigVector.Length(to) / Math.Max(.0001, RigVector.Length(from))));
                    // Nur längs des Segments skalieren; die Körperdicke bleibt erhalten.
                    var adjusted = RigVector.Add(local, RigVector.Scale(direction, RigVector.Dot(local, direction) * (normals ? 1 / ratio - 1 : ratio - 1)));
                    var rotated = RigVector.Rotate(adjusted, from, to);
                    if (!normals) rotated = RigVector.Add(rotated, reference[bone]);
                    point = RigVector.Add(point, RigVector.Scale(rotated, BoneWeights[v][j]));
                }
                result[v] = normals ? RigVector.Unit(point) : point;
            }
            return result;
        }
    }

    internal static class RigVector
    {
        internal static float[] Add(float[] a, float[] b) { return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] }; }
        internal static float[] Sub(float[] a, float[] b) { return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }; }
        internal static float[] Scale(float[] a, double b) { return new[] { (float)(a[0] * b), (float)(a[1] * b), (float)(a[2] * b) }; }
        internal static double Dot(float[] a, float[] b) { return (double)a[0] * b[0] + (double)a[1] * b[1] + (double)a[2] * b[2]; }
        internal static double Length(float[] a) { return Math.Sqrt(Dot(a, a)); }
        internal static float[] Unit(float[] a) { return Scale(a, 1 / Math.Max(.000001, Length(a))); }
        internal static float[] Cross(float[] a, float[] b) { return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }
        internal static double SegmentDistanceSquared(float[] p, float[] a, float[] b)
        {
            var d = Sub(b, a);
            var nearest = Add(a, Scale(d, Math.Max(0, Math.Min(1, Dot(Sub(p, a), d) / Math.Max(.000001, Dot(d, d))))));
            var difference = Sub(p, nearest);
            return Dot(difference, difference);
        }
        internal static float[] Rotate(float[] p, float[] from, float[] to)
        {
            var a = Unit(from); var b = Unit(to);
            double cosine = Math.Max(-1, Math.Min(1, Dot(a, b)));
            var axis = Cross(a, b);
            double sine = Length(axis);
            if (sine < .000001)
            {
                if (cosine >= 0) return p;
                axis = Unit(Cross(a, Math.Abs(a[0]) < .9 ? new[] { 1f, 0f, 0f } : new[] { 0f, 1f, 0f }));
                return Sub(Scale(axis, 2 * Dot(axis, p)), p);
            }
            axis = Unit(axis);
            return Add(Add(Scale(p, cosine), Scale(Cross(axis, p), sine)), Scale(axis, Dot(axis, p) * (1 - cosine)));
        }
    }
}