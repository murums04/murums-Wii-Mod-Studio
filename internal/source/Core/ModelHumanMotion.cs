using System;
using System.Linq;
using System.IO;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        internal static readonly string[] AnimationStyles = { null, "neutral", "feminine", "masculine" };
        internal static string[] AnimationStyleLabels()
        {
            return new[] { L.T("Originalbewegungen", "Original movement"), L.T("Menschlich · neutral", "Human · neutral"),
                L.T("Menschlich · feminin", "Human · feminine"), L.T("Menschlich · maskulin", "Human · masculine") };
        }

        internal void SetAnimationStyle(string style)
        {
            if (!AnimationStyles.Contains(style)) throw new InvalidDataException("Unknown animation style.");
            if (style != null && !HasHumanJoints)
                throw new InvalidOperationException(L.T("Zuerst eine menschliche Gelenkvorlage zuordnen.", "Assign a human joint template first."));
            bool firstHumanStyle = HumanAnimationStyle == null && style != null;
            HumanAnimationStyle = style;
            if (firstHumanStyle && MenuPose != null) MenuPose.MotionStrength = 100;
        }

        internal const double HumanMotionDuration = 20;

        internal static string HumanMotionLabel(double seconds)
        {
            int part = (int)(Math.Max(0, seconds) % HumanMotionDuration / 4);
            return new[] { L.T("Linken Arm beugen", "Bend left arm"), L.T("Rechten Arm beugen", "Bend right arm"),
                L.T("Linkes Bein beugen", "Bend left leg"), L.T("Rechtes Bein beugen", "Bend right leg"),
                L.T("Oberkörper drehen", "Turn upper body") }[part];
        }

        internal float[][] HumanMotionGeometry(double seconds, int[][] indices = null, float[][] weights = null)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException("seconds");
            if (!HasHumanJoints || JointGuides == null) return Points;
            double time = Math.Max(0, seconds) % HumanMotionDuration, progress = time % 4 / 4;
            int part = (int)(time / 4);
            double pulse = Math.Pow(Math.Sin(Math.PI * progress), 2);
            if (pulse < .00000001) return Points;
            var identity = new[] { 1f, 0, 0, 0, 0, 1f, 0, 0, 0, 0, 1f, 0, 0, 0, 0, 1f };
            var local = Bones.Select(b => identity).ToArray();
            var up = RigVector.Unit(RigVector.Sub(JointGuides[PoseBone("face_1")], JointGuides[PoseBone("skl_root")]));
            var across = RigVector.Unit(RigVector.Sub(JointGuides[PoseBone("arm_l1")], JointGuides[PoseBone("arm_r1")]));
            var forward = RigVector.Unit(RigVector.Cross(across, up));
            if (part < 4)
            {
                bool leg = part >= 2; string side = part % 2 == 0 ? "l" : "r";
                string prefix = (leg ? "leg_" : "arm_") + side;
                int start = PoseBone(prefix + "1"), middle = PoseBone(prefix + "2");
                int end = PoseBone((leg ? "ankle_" : "wrist_") + side + "1");
                var upper = RigVector.Unit(RigVector.Sub(JointGuides[middle], JointGuides[start]));
                var lower = RigVector.Unit(RigVector.Sub(JointGuides[end], JointGuides[middle]));
                var axis = RigVector.Cross(upper, lower);
                if (RigVector.Length(axis) < .05) axis = RigVector.Cross(lower, RigVector.Scale(forward, leg ? -1 : 1));
                if (RigVector.Length(axis) < .05) axis = across;
                double bend = Math.Acos(Math.Max(-1, Math.Min(1, RigVector.Dot(upper, lower)))) * 180 / Math.PI;
                double target = bend > 85 ? 45 : 100;
                local[middle] = HumanRotation(JointGuides[middle], axis, Math.Max(-60, Math.Min(60, target - bend)) * pulse);
                if (leg) local[start] = HumanRotation(JointGuides[start], across, -20 * pulse);
            }
            else local[PoseBone("spin")] = HumanRotation(JointGuides[PoseBone("spin")], up, 25 * Math.Sin(progress * 2 * Math.PI) * pulse);
            // Eigene Gelenkbewegungen auf der Quellhaltung; keine RR-Animationsdatei und keine Gewichtsänderung.
            var transforms = new float[Bones.Length][];
            Func<int, float[]> transform = null;
            transform = b => {
                if (transforms[b] != null) return transforms[b];
                int primary = SharedBodyBone(b), parent = Bones[b].Parent;
                return transforms[b] = primary != b ? transform(primary) : parent < 0 ? local[b] : RigMatrix.Multiply(transform(parent), local[b]);
            };
            for (int b = 0; b < Bones.Length; b++) transform(b);
            indices = indices ?? BoneIndices; weights = weights ?? BoneWeights;
            var result = new float[Points.Length][];
            for (int v = 0; v < Points.Length; v++)
            {
                var p = Points[v]; var moved = new float[3];
                for (int j = 0; j < indices[v].Length; j++)
                {
                    var m = transforms[indices[v][j]]; float weight = weights[v][j];
                    moved[0] += (m[0]*p[0]+m[1]*p[1]+m[2]*p[2]+m[3])*weight;
                    moved[1] += (m[4]*p[0]+m[5]*p[1]+m[6]*p[2]+m[7])*weight;
                    moved[2] += (m[8]*p[0]+m[9]*p[1]+m[10]*p[2]+m[11])*weight;
                }
                result[v] = moved;
            }
            return result;
        }

        static float[] HumanRotation(float[] pivot, float[] axis, double degrees)
        {
            axis = RigVector.Unit(axis); double x = axis[0], y = axis[1], z = axis[2];
            double c = Math.Cos(degrees * Math.PI / 180), s = Math.Sin(degrees * Math.PI / 180), t = 1-c;
            var m = new[] { (float)(t*x*x+c), (float)(t*x*y-s*z), (float)(t*x*z+s*y), 0f,
                (float)(t*x*y+s*z), (float)(t*y*y+c), (float)(t*y*z-s*x), 0f,
                (float)(t*x*z-s*y), (float)(t*y*z+s*x), (float)(t*z*z+c), 0f, 0f, 0f, 0f, 1f };
            var shift = RigVector.Sub(pivot, RigMatrix.Point(m, pivot));
            m[3] = shift[0]; m[7] = shift[1]; m[11] = shift[2]; return m;
        }
    }
}
