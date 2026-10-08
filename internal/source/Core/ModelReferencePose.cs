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
        public float[] SourcePoint;
        public float[] Direction;
        public float[] SourceAxis, TargetAxis;
    }

    internal sealed partial class ModelRig
    {
        sealed class PoseLimb
        {
            internal int Start, Middle, End;
            internal double Upper, Lower, UpperRadius, LowerRadius;
            internal bool Leg;
            internal float Side;
        }

        internal bool HasHumanJoints
        {
            get
            {
                return new[] { "skl_root", "spin", "face_1", "arm_l1", "arm_l2", "wrist_l1",
                    "arm_r1", "arm_r2", "wrist_r1", "leg_l1", "leg_l2", "ankle_l1",
                    "leg_r1", "leg_r2", "ankle_r1" }.All(name => PoseBone(name) >= 0);
            }
        }

        internal void ApplyReferenceGamePose(int context, RigPoseReference reference, bool copyMenuPose = false)
        {
            ApplyReferenceGamePose(context, reference, copyMenuPose, false);
        }

        internal void FitNaturalRacePose(RigPoseReference reference)
        {
            ApplyReferenceGamePose(2, reference, false, true);
        }

        internal void ResizeGamePose(int context, RigPoseReference reference, float percent)
        {
            if (Single.IsNaN(percent) || Single.IsInfinity(percent) || percent < 1 || percent > 500)
                throw new ArgumentOutOfRangeException("percent");
            var previous = GameSettings(context);
            if (previous == null) throw new InvalidOperationException("Initialize the pose before resizing.");
            var resized = Serializer().Deserialize<GamePoseSettings>(Serializer().Serialize(previous));
            resized.Scale = percent;
            SetGameSettings(context, resized);
            try
            {
                if (context == 2 && HasHumanJoints && reference != null)
                    ApplyReferenceGamePose(context, reference, false, false);
            }
            catch { SetGameSettings(context, previous); throw; }
        }

        void ApplyReferenceGamePose(int context, RigPoseReference reference, bool copyMenuPose, bool fitSize)
        {
            if (context != 1 && context != 2) throw new ArgumentOutOfRangeException("context");
            if (reference == null || JointGuides == null)
                throw new InvalidOperationException(L.T("Zuerst Originalmodell und Körperzuordnung laden.", "Load the original model and body assignment first."));
            if (!HasHumanJoints)
                throw new InvalidOperationException(L.T("Diese Vorlage hat kein vollständiges menschliches Skelett. Originalbewegungen bleiben möglich; für eine natürliche Sitzhaltung im Character Builder eine menschliche Bewegungsquelle wählen.",
                    "This template has no complete human skeleton. Original movement remains available; choose a human movement source in Character Builder for natural sitting poses."));
            reference.RequireUsableTransforms(this);
            int root = PoseBone("skl_root");
            if (root < 0) root = 0;
            var target = ReferencePoseJoints(reference, context == 1 && copyMenuPose);
            var previous = GameSettings(context);
            float scale = previous == null ? 100 : previous.Scale;
            var source = JointGuides.Select(p => RigVector.Scale(p, scale / 100)).ToArray();
            var sourceFrame = BodyFrame(source, root);
            var across = RigVector.Unit(new[] { target[PoseBone("arm_l1")][0] - target[PoseBone("arm_r1")][0],
                0f, target[PoseBone("arm_l1")][2] - target[PoseBone("arm_r1")][2] });
            if (RigVector.Length(across) < .9) across = new[] { 1f, 0f, 0f };
            var forward = RigVector.Cross(across, new[] { 0f, 1f, 0f });
            var limbs = PoseLimbs(source);
            var contacts = limbs.Select(l => ReferenceContact(l, reference, target)).ToArray();
            float[][] joints;
            if (context == 1)
                joints = copyMenuPose ? CopiedMenuPose(source, sourceFrame, target, reference, limbs, root)
                    : StandingPose(source, sourceFrame, target, reference, limbs, root, across, forward);
            else if ((previous == null || fitSize) && reference.VehicleGeometry != null && reference.VehicleCode != null)
            {
                joints = NaturalVehiclePose(target, root, across, forward, reference, out scale);
                contacts = PoseLimbs(JointGuides.Select(p => RigVector.Scale(p, scale / 100)).ToArray())
                    .Select(limb => ReferenceContact(limb, reference, target)).ToArray();
            }
            else
                joints = SeatedPose(source, sourceFrame, target, limbs, root, across, forward, reference.VehicleCode, contacts, scale / 100, reference.VehicleGeometry);
            bool uprightHead = context == 2 && reference.VehicleCode != null && reference.VehicleCode.EndsWith("_kart", StringComparison.Ordinal)
                && (previous == null || fitSize || previous.UprightDrivingHead);
            if (uprightHead) FitDrivingHeadChildren(joints, scale / 100);
            var settings = new GamePoseSettings {
                NaturalHuman = true,
                UprightDrivingHead = uprightHead,
                Position = previous != null && !fitSize ? (float[])previous.Position.Clone() : new float[3],
                Rotation = previous != null && !fitSize ? (float[])previous.Rotation.Clone() : new float[3],
                StableMenu = previous == null || previous.StableMenu,
                MenuSourceCode = previous == null ? null : previous.MenuSourceCode,
                MenuSourceSlot = previous == null ? 0 : previous.MenuSourceSlot,
                MenuSourceName = previous == null ? null : previous.MenuSourceName,
                Animations = previous == null ? null : previous.Animations,
                Scale = scale,
                MotionStrength = previous == null ? (context == 1 && HumanAnimationStyle == null ? 25 : 100) : previous.MotionStrength,
                Joints = joints.Select(p => RigVector.Scale(p, 100 / scale)).ToArray(),
                Contacts = context == 1 ? new PoseContact[0] : contacts
            };
            if (settings.Joints.Any(p => !ValidGameVector(p))) throw new InvalidDataException("Invalid original pose.");
            SetGameSettings(context, settings);
            if (context == 2) NaturalVehicleFitting = true;
        }

        float[][] ReferencePoseJoints(RigPoseReference reference, bool menuTransfer = false)
        {
            return Bones.Select((b, bone) => {
                int index = reference.MatchBone(this, bone);
                if (index < 0 && b.Parent < 0 && !BoneIndices.Any(indices => indices.Contains(bone)))
                    return new[] { b.Matrix[3], b.Matrix[7], b.Matrix[11] };
                if (index < 0 && (OptionalPart(bone) != null || menuTransfer))
                {
                    for (int parent = b.Parent; parent >= 0; parent = Bones[parent].Parent)
                    {
                        int ancestor = reference.MatchBone(this, parent);
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
                result.Add(new PoseLimb { Start = start, Middle = middle, End = end, Upper = upper, Lower = lower,
                    UpperRadius = leg ? LimbRadius(start, middle, upper) : 0,
                    LowerRadius = leg ? LimbRadius(middle, end, lower) : 0,
                    Leg = leg, Side = side == "l" ? 1 : -1 });
            }
            return result;
        }

        float[][] CopiedMenuPose(float[][] source, float[][] sourceFrame, float[][] target,
            RigPoseReference reference, List<PoseLimb> limbs, int root)
        {
            foreach (string name in new[] { "skl_root", "spin", "face_1" })
                if (reference.MatchBone(this, PoseBone(name)) < 0)
                    throw new InvalidDataException(L.T("Diese Menüvorlage hat kein passendes menschliches Skelett.", "This menu template has no matching human skeleton."));
            foreach (var limb in limbs)
                foreach (int bone in new[] { limb.Start, limb.Middle, limb.End })
                    if (reference.MatchBone(this, bone) < 0)
                        throw new InvalidDataException(L.T("Diese Menüvorlage hat keine passenden Arme und Beine. Eine menschliche Vorlage wählen.",
                            "This menu template has no matching arms and legs. Choose a human template."));
            var joints = PlaceBody(source, sourceFrame, BodyFrame(target, root), root, source[root]);
            foreach (var limb in limbs)
            {
                var upper = RigVector.Unit(RigVector.Sub(target[limb.Middle], target[limb.Start]));
                var lower = RigVector.Unit(RigVector.Sub(target[limb.End], target[limb.Middle]));
                if (RigVector.Length(upper) < .9 || RigVector.Length(lower) < .9)
                    throw new InvalidDataException(L.T("Die Menüvorlage enthält zusammenfallende Gelenke.", "The menu template contains overlapping joints."));
                joints[limb.Middle] = RigVector.Add(joints[limb.Start], RigVector.Scale(upper, limb.Upper));
                joints[limb.End] = RigVector.Add(joints[limb.Middle], RigVector.Scale(lower, limb.Lower));
            }
            float lift = limbs.Where(l => l.Leg).Max(l => source[l.End][1] - joints[l.End][1]);
            foreach (var point in joints) point[1] += lift;
            return joints;
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
            var standing = PlaceBody(source, sourceFrame, frame, root, hip);

            var joints = PlaceBody(source, sourceFrame, frame, root, hip);
            foreach (var limb in limbs)
            {
                if (limb.Leg)
                {
                    var endpoint = RigVector.Add(standing[limb.Start], new[] { 0f, -(float)LimbReach(limb, 5), 0f });

                    SolveLimb(joints, limb, endpoint, forward, 0, 135);
                }
                else
                {
                    var upper = RigVector.Unit(RigVector.Add(RigVector.Scale(across, limb.Side * .1), new[] { 0f, -1f, 0f }));
                    var lower = RigVector.Unit(RigVector.Add(upper, RigVector.Scale(forward, .18)));
                    joints[limb.Middle] = RigVector.Add(joints[limb.Start], RigVector.Scale(upper, limb.Upper));
                    joints[limb.End] = RigVector.Add(joints[limb.Middle], RigVector.Scale(lower, limb.Lower));
                }
            }
            return joints;
        }

        double LimbRadius(int start, int end, double length)
        {
            var segment = RigVector.Sub(JointGuides[end], JointGuides[start]);
            double originalLength = RigVector.Length(segment);
            var axis = RigVector.Unit(segment);
            var radii = new List<double>();
            for (int vertex = 0; vertex < Points.Length; vertex++)
            {
                int influence = Array.IndexOf(BoneIndices[vertex], start);
                if (influence < 0 || BoneWeights[vertex][influence] < .5f) continue;
                var delta = RigVector.Sub(Points[vertex], JointGuides[start]);
                double along = RigVector.Dot(delta, axis);
                if (along < originalLength * .15 || along > originalLength * .85) continue;
                radii.Add(RigVector.Length(RigVector.Sub(delta, RigVector.Scale(axis, along))));
            }
            if (radii.Count == 0) return length * .1;
            radii.Sort();
            return radii[(int)((radii.Count - 1) * .95)] * length / originalLength;
        }

        float[][] NaturalVehiclePose(float[][] target, int root, float[] across, float[] forward, RigPoseReference reference, out float scale)
        {
            float[][] best = null;
            double bestCost = Double.MaxValue;
            bool bestReachable = false;
            scale = 100;
            for (int percent = 80; percent <= 120; percent += 5)
            {
                var source = JointGuides.Select(p => RigVector.Scale(p, percent / 100.0)).ToArray();
                var limbs = PoseLimbs(source);
                var contacts = limbs.Select(limb => ReferenceContact(limb, reference, target)).ToArray();
                var joints = SeatedPose(source, BodyFrame(source, root), target, limbs, root, across, forward,
                    reference.VehicleCode, contacts, percent / 100.0, reference.VehicleGeometry);
                double cost = Math.Pow((100 - percent) * .12, 2);
                bool kart = reference.VehicleCode != null && reference.VehicleCode.EndsWith("_kart", StringComparison.Ordinal);
                bool reachable = true;
                var bodyUp = kart ? BodyFrame(joints, root)[1] : null;
                double height = PoseHeight(limbs);
                foreach (var limb in limbs)
                {
                    var upper = RigVector.Unit(RigVector.Sub(joints[limb.Middle], joints[limb.Start]));
                    var lower = RigVector.Unit(RigVector.Sub(joints[limb.End], joints[limb.Middle]));
                    double bend = Math.Acos(Math.Max(-1, Math.Min(1, RigVector.Dot(upper, lower)))) * 180 / Math.PI;
                    double armBend = reference.VehicleCode != null && reference.VehicleCode.EndsWith("_kart", StringComparison.Ordinal) ? 65 : 55;
                    cost += Math.Pow((bend - (limb.Leg ? 95 : armBend)) * .3, 2);
                    var contact = contacts.Single(c => c.Joint == Bones[limb.End].Name);
                    var endpoint = contact.SourcePoint == null ? contact.Target
                        : RigVector.Sub(contact.Target, RigVector.Scale(contact.Direction,
                            RigVector.Length(RigVector.Sub(contact.SourcePoint, JointGuides[limb.End])) * percent / 100));
                    double contactError = RigVector.Length(RigVector.Sub(joints[limb.End], endpoint));
                    cost += contactError * 1000;
                    if (contactError > .1) reachable = false;
                    if (kart && !limb.Leg)
                        cost += RelaxedShoulderCost(joints, limb, bodyUp) * Math.Pow(180 * .3 / height, 2) / .12;
                    if (limb.Leg)
                        cost += Math.Pow(Math.Max(0, RigVector.Dot(joints[limb.Middle], across) * limb.Side
                            - Math.Abs(RigVector.Dot(joints[limb.End], across))), 2) * 2;
                }
                if (best == null || (kart && reachable && !bestReachable) || ((!kart || reachable == bestReachable) && cost < bestCost))
                { bestCost = cost; best = joints; scale = percent; bestReachable = reachable; }
            }
            return best;
        }

        float[][] SeatedPose(float[][] source, float[][] sourceFrame, float[][] target,
            List<PoseLimb> limbs, int root, float[] across, float[] forward, string vehicle, PoseContact[] contacts, double scale, VehiclePoseGeometry vehicleGeometry)
        {
            bool kart = vehicle != null && vehicle.EndsWith("_kart", StringComparison.Ordinal);
            double preferredLean = kart ? 12 : 24;
            double height = PoseHeight(limbs);
            var endpoints = limbs.ToDictionary(limb => limb.End, limb => {
                var contact = contacts.Single(c => c.Joint == Bones[limb.End].Name);
                if (contact.SourcePoint == null) return (float[])target[limb.End].Clone();
                double length = RigVector.Length(RigVector.Sub(contact.SourcePoint, JointGuides[limb.End])) * scale;
                // Sitzsuche und Armbeugung muessen denselben Handkontakt verwenden.
                return RigVector.Sub(contact.Target, RigVector.Scale(contact.Direction, length));
            });
            var pelvis = Enumerable.Range(0, Points.Length).Where(v =>
                BoneIndices[v].Select((bone, i) => bone == root ? BoneWeights[v][i] : 0).Sum() >= .5f)
                .Select(v => RigVector.Scale(Points[v], scale)).ToArray();
            double bestCost = Double.MaxValue;
            float[][] best = null;
            bool bestReachable = false;
            // Hüfte und Oberkörper gemeinsam optimieren; Gliedmaßen werden niemals verlängert.
            for (int lean = 0; lean <= (kart ? 35 : 65); lean += 5)
            for (int depth = -8; depth <= 5; depth++)
            {
                double angle = lean * Math.PI / 180;
                var up = RigVector.Add(new[] { 0f, (float)Math.Cos(angle), 0f }, RigVector.Scale(forward, Math.Sin(angle)));
                var frame = new[] { across, up, RigVector.Cross(across, up) };
                var offset = RigVector.Scale(forward, depth * height * .015);
                var hip = RigVector.Add(target[root], offset);
                var seat = vehicleGeometry == null ? null : vehicleGeometry.SeatHeight(hip);
                if (seat.HasValue && pelvis.Length > 0)
                {
                    float underside = pelvis.Min(p => ChangeBodyFrame(RigVector.Sub(p, source[root]), sourceFrame, frame)[1]);
                    hip[1] = seat.Value - underside;
                }
                var joints = PlaceBody(source, sourceFrame, frame, root, hip);
                double cost = RigVector.Dot(offset, offset) * .4 + Math.Pow((lean - preferredLean) * height / 180, 2) * .08;
                bool reachable = true;
                foreach (var limb in limbs)
                {
                    double distance = RigVector.Length(RigVector.Sub(endpoints[limb.End], joints[limb.Start]));
                    double residual = distance - Math.Max(LimbReach(limb, limb.Leg ? 135 : 145), Math.Min(LimbReach(limb, limb.Leg ? 25 : 12), distance));
                    cost += residual * residual * 100;
                    if (Math.Abs(residual) > .1) reachable = false;
                    if (kart && !limb.Leg)
                    {
                        SolveLimb(joints, limb, endpoints[limb.End], SeatedLimbPole(limb, across, forward), 12, 145);
                        cost += RelaxedShoulderCost(joints, limb, up);
                    }
                    if (vehicleGeometry != null && vehicle != null && (kart || vehicle.EndsWith("_bike", StringComparison.Ordinal)))
                    {
                        double cosine = (distance * distance - limb.Upper * limb.Upper - limb.Lower * limb.Lower) / (2 * limb.Upper * limb.Lower);
                        double bend = Math.Acos(Math.Max(-1, Math.Min(1, cosine))) * 180 / Math.PI;
                        cost += Math.Pow((bend - (limb.Leg ? 95 : kart ? 65 : 55)) * height / 180, 2) * (kart ? .12 : .06);
                    }
                }
                // Erreichbare Kontakte gehen vor Sitzkomfort; sonst bleibt die Kontaktwarnung erhalten.
                if (best == null || (kart && reachable && !bestReachable) || ((!kart || reachable == bestReachable) && cost < bestCost))
                { bestCost = cost; best = joints; bestReachable = reachable; }
            }
            foreach (var limb in limbs)
            {
                var pole = SeatedLimbPole(limb, across, forward);
                SolveLimb(best, limb, endpoints[limb.End], pole, limb.Leg ? 25 : 12, limb.Leg ? 135 : 145);
                if (limb.Leg)
                    FitSeatedLeg(best, limb, endpoints[limb.End], across, forward, vehicleGeometry, vehicle != null && vehicle.EndsWith("_bike", StringComparison.Ordinal));
            }
            return best;
        }

        double PoseHeight(List<PoseLimb> limbs)
        {
            return (Points.Max(p => p[1]) - Points.Min(p => p[1])) * limbs[0].Upper
                / RigVector.Length(RigVector.Sub(JointGuides[limbs[0].Middle], JointGuides[limbs[0].Start]));
        }

        static float[] SeatedLimbPole(PoseLimb limb, float[] across, float[] forward)
        {
            return limb.Leg ? RigVector.Add(forward, RigVector.Scale(across, limb.Side * .12))
                : RigVector.Add(new[] { 0f, -1f, 0f }, RigVector.Add(RigVector.Scale(across, limb.Side * .1), RigVector.Scale(forward, -.15)));
        }

        static double RelaxedShoulderCost(float[][] joints, PoseLimb limb, float[] bodyUp)
        {
            // Ellbogenhebung und Hueftversatz werden in denselben Spielunits bewertet.
            var lifted = RigVector.Add(RigVector.Sub(joints[limb.Middle], joints[limb.Start]), RigVector.Scale(bodyUp, limb.Upper));
            return RigVector.Dot(lifted, lifted) * .4;
        }

        void FitSeatedLeg(float[][] joints, PoseLimb limb, float[] endpoint,
            float[] across, float[] forward, VehiclePoseGeometry vehicle, bool bike)
        {
            SolveLimb(joints, limb, endpoint, forward, 15, 140);
            var origin = joints[limb.Start];
            var delta = RigVector.Sub(joints[limb.End], origin);
            double distance = RigVector.Length(delta);
            var direction = RigVector.Unit(delta);
            double along = (limb.Upper * limb.Upper - limb.Lower * limb.Lower + distance * distance) / (2 * distance);
            double radius = Math.Sqrt(Math.Max(0, limb.Upper * limb.Upper - along * along));
            var center = RigVector.Add(origin, RigVector.Scale(direction, along));
            var bend = RigVector.Unit(RigVector.Sub(joints[limb.Middle], center));
            var sideways = RigVector.Cross(direction, bend);
            var guide = RigVector.Add(origin, RigVector.Add(RigVector.Scale(forward, limb.Upper * .85),
                RigVector.Add(new[] { 0f, (float)(limb.Upper * (bike ? -.1 : .35)), 0f },
                    RigVector.Scale(across, RigVector.Dot(delta, across) * .5))));
            double best = Double.MaxValue;
            float[] knee = joints[limb.Middle];
            double thickness = limb.UpperRadius;
            for (int angle = 0; angle < 360; angle += 3)
            {
                double radians = angle * Math.PI / 180;
                var point = RigVector.Add(center, RigVector.Add(RigVector.Scale(bend, radius * Math.Cos(radians)), RigVector.Scale(sideways, radius * Math.Sin(radians))));
                joints[limb.Middle] = point;
                var error = RigVector.Sub(point, guide);
                double lateral = RigVector.Dot(point, across);
                double cost = RigVector.Dot(error, error);
                cost += Math.Pow(Math.Max(0, -lateral * limb.Side + thickness), 2) * 100;
                cost += Math.Pow(Math.Max(0, RigVector.Dot(origin, forward) - RigVector.Dot(point, forward)), 2) * 10;
                if (vehicle != null && bike)
                    foreach (int start in new[] { limb.Start, limb.Middle })
                    {
                        int end = start == limb.Start ? limb.Middle : limb.End;
                        foreach (double t in new[] { .35, .65, .9 })
                        {
                            var sample = RigVector.Add(RigVector.Scale(joints[start], 1 - t), RigVector.Scale(joints[end], t));
                            var surface = vehicle.SideSurface(sample, limb.Side);
                            if (!surface.HasValue) continue;
                            double radiusAtSample = start == limb.Start ? limb.UpperRadius : limb.LowerRadius;
                            double overlap = Math.Max(0, surface.Value + radiusAtSample - sample[0] * limb.Side);
                            cost += overlap * overlap * 100;
                        }
                    }
                if (cost < best) { best = cost; knee = point; }
            }
            joints[limb.Middle] = knee;
        }

        PoseContact ReferenceContact(PoseLimb limb, RigPoseReference reference, float[][] target)
        {
            var contact = new PoseContact { Joint = Bones[limb.End].Name, Target = (float[])target[limb.End].Clone() };
            int referenceBone = reference.MatchBone(this, limb.End);
            bool bike = reference.VehicleGeometry != null && reference.VehicleCode != null && reference.VehicleCode.EndsWith("_bike", StringComparison.Ordinal);
            if (reference.Contacts == null || referenceBone < 0 || reference.Contacts[referenceBone] == null) return contact;
            var surface = Enumerable.Range(0, Points.Length).Where(v => {
                int index = Array.IndexOf(BoneIndices[v], limb.End);
                return index >= 0 && BoneWeights[v][index] >= .75f;
            }).Select(v => Points[v]).ToArray();
            if (surface.Length < 4) return contact;
            contact.SourcePoint = Enumerable.Range(0, 3).Select(axis => (surface.Min(p => p[axis]) + surface.Max(p => p[axis])) * .5f).ToArray();
            if (limb.Leg)
            {
                contact.SourcePoint[1] = surface.Min(p => p[1]);
                contact.Target = (float[])reference.Contacts[referenceBone].Clone();
                if (reference.VehicleGeometry != null)
                {
                    var expected = (float[])contact.Target.Clone();
                    if (bike) expected[0] = limb.Side * Math.Max(Math.Abs(expected[0]) * .65f, (float)(Math.Abs(JointGuides[limb.Start][0]) + limb.Upper * .35));
                    contact.Target = reference.VehicleGeometry.FootSupport(expected, limb.Side,
                        bike ? limb.LowerRadius : 0, bike ? limb.Lower * .5 : 0);
                }
                contact.Direction = RigVector.Unit(RigVector.Sub(contact.SourcePoint, JointGuides[limb.End]));
                if (RigVector.Length(contact.Direction) < .9)
                {
                    contact.SourcePoint = contact.Direction = null;
                    contact.Target = (float[])target[limb.End].Clone();
                }
                return contact;
            }
            HandGrip grip;
            if (SourceHandGrips != null && SourceHandGrips.TryGetValue(Bones[limb.End].Name, out grip))
            {
                contact.SourcePoint = grip.Point;
                contact.SourceAxis = grip.Axis;
                contact.TargetAxis = new[] { limb.Side, 0f, 0f };
            }
            contact.Target = (float[])reference.Contacts[referenceBone].Clone();
            if (reference.VehicleGeometry != null)
            {
                var expected = (float[])contact.Target.Clone();
                float[] axis;
                contact.Target = reference.VehicleGeometry.Grip(expected, out axis);
                if (contact.SourceAxis != null) contact.TargetAxis = axis;
            }
            contact.Direction = reference.VehicleGeometry != null && reference.VehicleCode != null && reference.VehicleCode.EndsWith("_bike", StringComparison.Ordinal)
                ? RigVector.Unit(new[] { 0f, -.35f, 1f })
                : RigVector.Unit(RigVector.Sub(contact.Target, target[limb.End]));
            if (contact.TargetAxis != null)
                contact.Direction = RigVector.Unit(RigVector.Sub(contact.Direction,
                    RigVector.Scale(contact.TargetAxis, RigVector.Dot(contact.Direction, contact.TargetAxis))));
            if (RigVector.Length(contact.Direction) < .9 || RigVector.Length(RigVector.Sub(contact.SourcePoint, JointGuides[limb.End])) < .001)
            {
                contact.SourcePoint = contact.Direction = contact.SourceAxis = contact.TargetAxis = null;
                contact.Target = (float[])target[limb.End].Clone();
            }
            return contact;
        }

        internal float[] ContactPosition(int context, PoseContact contact)
        {
            int index = PoseBone(contact.Joint);
            return contact.SourcePoint == null ? GameJoints(context)[index]
                : RigMatrix.Point(GameBoneTransforms(context)[index], contact.SourcePoint);
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
            double tolerance = settings.UprightDrivingHead ? .1 : Math.Max(.1, ReferenceHeight * .01);
            return settings.Contacts.Where(c => {
                int index = PoseBone(c.Joint);
                return index >= 0 && RigVector.Length(RigVector.Sub(ContactPosition(context, c), c.Target)) > tolerance;
            }).ToArray();
        }
    }
}
