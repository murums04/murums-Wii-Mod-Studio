using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed class PoseContact
    {
        public string Joint;
        public float[] Target;
    }

    internal sealed partial class ModelRig
    {
        sealed class PoseLimb
        {
            internal int Start, Middle, End;
            internal double Upper, Lower;
            internal bool Leg;
            internal float Side;
        }

        internal void ApplyReferenceGamePose(int context, RigPoseReference reference)
        {
            if (context != 1 && context != 2) throw new ArgumentOutOfRangeException("context");
            if (reference == null || JointGuides == null)
                throw new InvalidOperationException(L.T("Zuerst Originalmodell und Körperzuordnung laden.", "Load the original model and body assignment first."));
            int root = PoseBone("skl_root");
            if (root < 0) root = 0;
            var target = ReferencePoseJoints(reference);
            var previous = GameSettings(context);
            float scale = previous == null ? 100 : previous.Scale;
            var source = JointGuides.Select(p => RigVector.Scale(p, scale / 100)).ToArray();
            var sourceFrame = BodyFrame(source, root);
            var across = RigVector.Unit(new[] { target[PoseBone("arm_l1")][0] - target[PoseBone("arm_r1")][0],
                0f, target[PoseBone("arm_l1")][2] - target[PoseBone("arm_r1")][2] });
            if (RigVector.Length(across) < .9) across = new[] { 1f, 0f, 0f };
            var forward = RigVector.Cross(across, new[] { 0f, 1f, 0f });
            var limbs = PoseLimbs(source);
            float[][] joints;
            if (context == 1)
                joints = StandingPose(source, sourceFrame, target, reference, limbs, root, across, forward);
            else
                joints = SeatedPose(source, sourceFrame, target, limbs, root, across, forward, reference.VehicleCode);
            var settings = new GamePoseSettings {
                NaturalHuman = true,
                Scale = scale,
                MotionStrength = previous == null ? (context == 1 ? 25 : 100) : previous.MotionStrength,
                Joints = joints.Select(p => RigVector.Scale(p, 100 / scale)).ToArray(),
                Contacts = context == 1 ? new PoseContact[0] : limbs.Select(l => new PoseContact {
                    Joint = Bones[l.End].Name, Target = (float[])target[l.End].Clone()
                }).ToArray()
            };
            if (settings.Joints.Any(p => !ValidGameVector(p))) throw new InvalidDataException("Invalid original pose.");
            SetGameSettings(context, settings);
            if (context == 2) NaturalVehicleFitting = true;
        }

        float[][] ReferencePoseJoints(RigPoseReference reference)
        {
            return Bones.Select((b, bone) => {
                int index = reference.MatchBone(this, bone);
                if (index < 0 && b.Parent < 0 && !BoneIndices.Any(indices => indices.Contains(bone)))
                    return new[] { b.Matrix[3], b.Matrix[7], b.Matrix[11] };
                if (index < 0 && OptionalPart(bone) != null)
                {
                    for (int parent = b.Parent; parent >= 0; parent = Bones[parent].Parent)
                    {
                        int ancestor = Array.IndexOf(reference.Names, Bones[parent].Name);
                        if (ancestor >= 0)
                            return RigVector.Add(reference.Joints[ancestor], RigVector.Sub(JointGuides[bone], JointGuides[parent]));
                    }
                }
                if (index < 0) throw new InvalidDataException(L.T("Dieses Originalmodell hat andere Gelenke. Die passende Charakterreferenz wählen.", "This original model has different joints. Choose the matching character reference."));
                return reference.Joints[index];
            }).ToArray();
        }

        List<PoseLimb> PoseLimbs(float[][] source)
        {
            var result = new List<PoseLimb>();
            foreach (bool leg in new[] { false, true })
            foreach (string side in new[] { "l", "r" })
            {
                string prefix = leg ? "leg_" : "arm_";
                int start = PoseBone(prefix + side + "1"), middle = PoseBone(prefix + side + "2");
                int end = PoseBone((leg ? "ankle_" : "wrist_") + side + "1");
                if (start < 0 || middle < 0 || end < 0)
                    throw new InvalidDataException(L.T("Für die natürliche Haltung werden Schulter, Ellbogen, Hand, Hüfte, Knie und Fuß auf beiden Seiten benötigt.", "Natural poses require shoulder, elbow, hand, hip, knee and foot joints on both sides."));
                double upper = RigVector.Length(RigVector.Sub(source[middle], source[start]));
                double lower = RigVector.Length(RigVector.Sub(source[end], source[middle]));
                if (upper < .001 || lower < .001) throw new InvalidDataException(L.T("Zusammenfallende Gelenkpunkte zuerst auseinanderziehen.", "Separate overlapping joint points first."));
                result.Add(new PoseLimb { Start = start, Middle = middle, End = end, Upper = upper, Lower = lower, Leg = leg, Side = side == "l" ? 1 : -1 });
            }
            return result;
        }

        float[][] StandingPose(float[][] source, float[][] sourceFrame, float[][] target,
            RigPoseReference reference, List<PoseLimb> limbs, int root, float[] across, float[] forward)
        {
            var frame = new[] { across, new[] { 0f, 1f, 0f }, forward };
            float floor = reference.Visual != null && reference.Visual.Points.Count > 0
                ? reference.Visual.Points.Min(p => p[1]) : target.Min(p => p[1]);
            float sourceScale = (float)(limbs[0].Upper / RigVector.Length(RigVector.Sub(JointGuides[limbs[0].Middle], JointGuides[limbs[0].Start])));
            float sourceFloor = Points.Min(p => p[1]) * sourceScale;
            var hip = new[] { target[root][0], 0f, target[root][2] };
            foreach (var leg in limbs.Where(l => l.Leg))
            {
                double drop = LimbReach(leg, 5);
                float footHeight = Math.Max(0, source[leg.End][1] - sourceFloor);
                hip[1] = Math.Max(hip[1], (float)(floor + footHeight + drop + source[root][1] - source[leg.Start][1]));
            }
            var joints = PlaceBody(source, sourceFrame, frame, root, hip);
            foreach (var limb in limbs)
            {
                if (limb.Leg)
                {
                    var endpoint = RigVector.Add(joints[limb.Start], new[] { 0f, -(float)LimbReach(limb, 5), 0f });
                    SolveLimb(joints, limb, endpoint, forward, 0, 135);
                }
                else
                {
                    var upper = RigVector.Unit(RigVector.Add(RigVector.Scale(across, limb.Side * .15), new[] { 0f, -1f, 0f }));
                    var lower = RigVector.Unit(RigVector.Add(upper, RigVector.Scale(forward, .18)));
                    joints[limb.Middle] = RigVector.Add(joints[limb.Start], RigVector.Scale(upper, limb.Upper));
                    joints[limb.End] = RigVector.Add(joints[limb.Middle], RigVector.Scale(lower, limb.Lower));
                }
            }
            return joints;
        }

        float[][] SeatedPose(float[][] source, float[][] sourceFrame, float[][] target,
            List<PoseLimb> limbs, int root, float[] across, float[] forward, string vehicle)
        {
            double preferredLean = vehicle != null && vehicle.EndsWith("_kart", StringComparison.Ordinal) ? 8 : 24;
            double height = (Points.Max(p => p[1]) - Points.Min(p => p[1])) * limbs[0].Upper / RigVector.Length(RigVector.Sub(JointGuides[limbs[0].Middle], JointGuides[limbs[0].Start]));
            double bestCost = Double.MaxValue;
            float[][] best = null;
            // Hüfte und Oberkörper gemeinsam optimieren; Gliedmaßen werden niemals verlängert.
            for (int lean = 0; lean <= 50; lean += 5)
            for (int vertical = -2; vertical <= 2; vertical++)
            for (int depth = -3; depth <= 3; depth++)
            {
                double angle = lean * Math.PI / 180;
                var up = RigVector.Add(new[] { 0f, (float)Math.Cos(angle), 0f }, RigVector.Scale(forward, Math.Sin(angle)));
                var frame = new[] { across, up, RigVector.Cross(across, up) };
                var offset = RigVector.Add(new[] { 0f, (float)(vertical * height * .01), 0f }, RigVector.Scale(forward, depth * height * .015));
                var joints = PlaceBody(source, sourceFrame, frame, root, RigVector.Add(target[root], offset));
                double cost = RigVector.Dot(offset, offset) * .4 + Math.Pow((lean - preferredLean) * height / 180, 2) * .08;
                foreach (var limb in limbs)
                {
                    double distance = RigVector.Length(RigVector.Sub(target[limb.End], joints[limb.Start]));
                    double residual = distance - Math.Max(LimbReach(limb, limb.Leg ? 135 : 145), Math.Min(LimbReach(limb, limb.Leg ? 25 : 12), distance));
                    cost += residual * residual * 100;
                }
                if (cost < bestCost) { bestCost = cost; best = joints; }
            }
            foreach (var limb in limbs)
            {
                var pole = limb.Leg
                    ? RigVector.Add(forward, RigVector.Scale(across, limb.Side * .12))
                    : RigVector.Add(RigVector.Scale(across, limb.Side), RigVector.Add(RigVector.Scale(forward, -.25), new[] { 0f, -.25f, 0f }));
                SolveLimb(best, limb, target[limb.End], pole, limb.Leg ? 25 : 12, limb.Leg ? 135 : 145);
            }
            return best;
        }

        static double LimbReach(PoseLimb limb, double flexion)
        {
            return Math.Sqrt(limb.Upper * limb.Upper + limb.Lower * limb.Lower + 2 * limb.Upper * limb.Lower * Math.Cos(flexion * Math.PI / 180));
        }

        static void SolveLimb(float[][] joints, PoseLimb limb, float[] endpoint, float[] pole, double minFlexion, double maxFlexion)
        {
            var origin = joints[limb.Start];
            var delta = RigVector.Sub(endpoint, origin);
            double distance = Math.Max(LimbReach(limb, maxFlexion), Math.Min(LimbReach(limb, minFlexion), RigVector.Length(delta)));
            var direction = RigVector.Length(delta) > .001 ? RigVector.Unit(delta) : new[] { 0f, -1f, 0f };
            double along = (limb.Upper * limb.Upper - limb.Lower * limb.Lower + distance * distance) / (2 * distance);
            double perpendicular = Math.Sqrt(Math.Max(0, limb.Upper * limb.Upper - along * along));
            var bend = RigVector.Sub(pole, RigVector.Scale(direction, RigVector.Dot(pole, direction)));
            if (RigVector.Length(bend) < .001) bend = RigVector.Cross(direction, new[] { 0f, 1f, 0f });
            if (RigVector.Length(bend) < .001) bend = RigVector.Cross(direction, new[] { 1f, 0f, 0f });
            joints[limb.Middle] = RigVector.Add(origin, RigVector.Add(RigVector.Scale(direction, along), RigVector.Scale(RigVector.Unit(bend), perpendicular)));
            joints[limb.End] = RigVector.Add(origin, RigVector.Scale(direction, distance));
        }

        static float[][] PlaceBody(float[][] source, float[][] sourceFrame, float[][] targetFrame, int root, float[] hip)
        {
            return source.Select(point => RigVector.Add(hip, ChangeBodyFrame(RigVector.Sub(point, source[root]), sourceFrame, targetFrame))).ToArray();
        }

        int PoseBone(string name) { return Array.FindIndex(Bones, b => b.Name == name); }

        float[][] BodyFrame(float[][] points, int root)
        {
            int left = PoseBone("arm_l1"), right = PoseBone("arm_r1");
            if (left < 0 || right < 0) throw new InvalidDataException("Missing shoulder joints.");
            var across = RigVector.Unit(RigVector.Sub(points[left], points[right]));
            var up = RigVector.Sub(RigVector.Scale(RigVector.Add(points[left], points[right]), .5), points[root]);
            up = RigVector.Unit(RigVector.Sub(up, RigVector.Scale(across, RigVector.Dot(up, across))));
            if (RigVector.Length(across) < .9 || RigVector.Length(up) < .9)
                throw new InvalidDataException(L.T("Schulter- und Hüftpunkte vor der automatischen Haltung prüfen.", "Check shoulder and hip joints before fitting the pose."));
            return new[] { across, up, RigVector.Unit(RigVector.Cross(across, up)) };
        }

        static float[] ChangeBodyFrame(float[] point, float[][] source, float[][] target)
        {
            var result = new float[3];
            for (int axis = 0; axis < 3; axis++) result = RigVector.Add(result, RigVector.Scale(target[axis], RigVector.Dot(point, source[axis])));
            return result;
        }

        internal PoseContact[] MissedContacts(int context)
        {
            var settings = GameSettings(context);
            if (settings == null || settings.Contacts == null) return new PoseContact[0];
            var joints = GameJoints(context);
            return settings.Contacts.Where(c => {
                int index = PoseBone(c.Joint);
                return index >= 0 && RigVector.Length(RigVector.Sub(joints[index], c.Target)) > Math.Max(.1, ReferenceHeight * .01);
            }).ToArray();
        }
    }
}
