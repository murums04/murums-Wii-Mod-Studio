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
        internal void InvalidateAlignment() { alignedPoints = alignedNormals = null; anatomyIssues = null; rigidVertexSet = null; surfaceReport = null; }

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
            // Kleid- und Fahrmodell teilen Koerpergelenke auch bei abweichender Bindepose.
            return primary;
        }

        internal bool GuideBone(int index)
        {
            return BoneEnabled(index) && SharedBodyBone(index) == index && Bones[index].Name != "nw4r_root";
        }

        internal void FitJointGuides()
        {
            InvalidateAlignment();
            var reviewed = ReviewedJoints == null || JointGuides == null ? null : ReviewedJoints
                .Where(p => PoseBone(p.Key) >= 0 && SameJoint(p.Value, JointGuides[PoseBone(p.Key)]))
                .ToDictionary(p => p.Key, p => p.Value);
            ComponentsReviewed = false;
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
            if (SourceJointGuides == null || new[] { "arm_l1", "arm_l2", "wrist_l1", "arm_r1", "arm_r2", "wrist_r1" }
                .Any(name => !SourceJointGuides.ContainsKey(name))) FitLoweredHumanArms(min, max);
            FitSeparatedPoseArms(min, max, reviewed);
            if (SourceJointGuides != null)
                for (int i = 0; i < Bones.Length; i++)
                    if (SourceJointGuides.ContainsKey(Bones[i].Name)) JointGuides[i] = (float[])SourceJointGuides[Bones[i].Name].Clone();
            if (reviewed != null)
                for (int i = 0; i < Bones.Length; i++)
                {
                    float[] point;
                    if (reviewed.TryGetValue(Bones[i].Name, out point) && point != null && point.Length == 3
                        && point.All(v => !Single.IsNaN(v) && !Single.IsInfinity(v))) JointGuides[i] = (float[])point.Clone();
                }
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

        void FitLoweredHumanArms(float[] min, float[] max)
        {
            float height = max[1] - min[1];
            if (height <= .0001f) return;
            var origin = new[] { (min[0] + max[0]) * .5f, min[1], (min[2] + max[2]) * .5f };
            var points = Points.Select(p => RigVector.Scale(RigVector.Sub(p, origin), 1 / height)).ToArray();
            float[][] left, right;
            var mirrored = points.Select(p => new[] { -p[0], p[1], p[2] }).ToArray();
            bool hasLeft = TryFitLoweredArm(points, out left), hasRight = TryFitLoweredArm(mirrored, out right);
            if (!hasLeft || !hasRight || Math.Abs(left[0][1] - right[0][1]) > .06)
            {
                float[][] frontLeft, frontRight;
                bool foundLeft = TryFitLoweredArm(points, out frontLeft, true);
                bool foundRight = TryFitLoweredArm(mirrored, out frontRight, true);
                if (foundLeft && !foundRight) foundRight = TryFitOppositeArm(mirrored, frontLeft, out frontRight);
                if (foundRight && !foundLeft) foundLeft = TryFitOppositeArm(points, frontRight, out frontLeft);
                if (foundLeft && foundRight && Math.Abs(frontLeft[0][1] - frontRight[0][1]) < .04)
                {
                    left = frontLeft; right = frontRight;
                    hasLeft = hasRight = true;
                }
            }
            // Ein klarer Arm grenzt auf der verdeckten Seite nur die Suche ein, nicht das Ergebnis.
            if (hasLeft && !hasRight) hasRight = TryFitOppositeArm(mirrored, left, out right);
            if (hasRight && !hasLeft) hasLeft = TryFitOppositeArm(points, right, out left);
            if (hasLeft && hasRight && Math.Abs(left[0][1] - right[0][1]) > .06)
            {
                float[][] corrected;
                if (left[0][1] < right[0][1] && TryFitOppositeArm(mirrored, left, out corrected)) right = corrected;
                else if (right[0][1] < left[0][1] && TryFitOppositeArm(points, right, out corrected)) left = corrected;
            }
            for (int side = 0; side < 2; side++)
            {
                if (side == 0 ? !hasLeft : !hasRight) continue;
                string suffix = side == 0 ? "l" : "r";
                var joints = side == 0 ? left : right;
                var names = new[] { "arm_" + suffix + "1", "arm_" + suffix + "2", "wrist_" + suffix + "1" };
                for (int joint = 0; joint < names.Length; joint++)
                {
                    int bone = PoseBone(names[joint]);
                    if (bone < 0) continue;
                    var point = (float[])joints[joint].Clone();
                    if (side == 1) point[0] = -point[0];
                    JointGuides[bone] = RigVector.Add(origin, RigVector.Scale(point, height));
                }
            }
        }

        static bool TryFitOppositeArm(float[][] points, float[][] opposite, out float[][] joints)
        {
            foreach (double radius in new[] { .025, .04, .06 })
                if (TryFitLoweredArm(ArmNeighbourhood(points, opposite, radius), out joints)
                    && Math.Abs(joints[0][1] - opposite[0][1]) < .04) return true;
            var nearby = ArmNeighbourhood(points, opposite, .035);
            if (nearby.Length >= 24)
            {
                bool covered = true;
                for (int sample = 1; sample <= 8; sample++)
                {
                    var center = RigVector.Add(opposite[0], RigVector.Scale(RigVector.Sub(opposite[2], opposite[0]), sample / 8.0));
                    if (nearby.Count(p => RigVector.Length(RigVector.Sub(p, center)) < .035) < 3) { covered = false; break; }
                }
                // Symmetrie nur nutzen, wenn die gesamte Gegenseite durch echte Oberflaeche belegt ist.
                if (covered) { joints = opposite.Select(p => (float[])p.Clone()).ToArray(); return true; }
            }
            joints = null;
            return false;
        }

        static float[][] ArmNeighbourhood(float[][] points, float[][] opposite, double radius)
        {
            var direction = RigVector.Unit(RigVector.Sub(opposite[2], opposite[0]));
            return points.Where(point => {
                var offset = RigVector.Sub(point, opposite[0]);
                double along = RigVector.Dot(offset, direction);
                return along > -.04 && along < .5
                    && RigVector.Length(RigVector.Sub(offset, RigVector.Scale(direction, along))) < radius;
            }).ToArray();
        }

        static bool TryFitLoweredArm(float[][] points, out float[][] joints, bool frontSurface = false)
        {
            joints = null;
            var band = points.Where(p => p[0] > .1f && p[1] > .45f && p[1] < .86f).ToArray();
            if (band.Length < 20) return false;
            float extent = Quantile(band.Select(p => p[0]), .995);
            if (extent > .5f) return false;
            var sections = ArmSections(band, extent, true, frontSurface);
            float[] slope, intercept;
            if (!FitArmLine(sections, out slope, out intercept)) return false;
            // Haare hinter dem Arm duerfen dessen Querschnitt nicht nach hinten ziehen.
            var filtered = band.Where(p => Math.Abs(p[2] - p[0] * slope[1] - intercept[1]) < .03f).ToArray();
            var refined = ArmSections(filtered, extent, false);
            float[] refinedSlope, refinedIntercept;
            if (FitArmLine(refined, out refinedSlope, out refinedIntercept)
                && ArmFitResidual(refined, refinedSlope, refinedIntercept) <= ArmFitResidual(sections, slope, intercept))
            {
                sections = refined;
                slope = refinedSlope;
                intercept = refinedIntercept;
            }
            double residual = ArmFitResidual(sections, slope, intercept);
            var shoulder = new[] { .085f, .085f * slope[0] + intercept[0], .085f * slope[1] + intercept[1] };
            // Nur klar erkennbare, abgesenkte Arme; verschraenkte Posen erfuellen diese Grenzen nicht.
            if (slope[0] <= -3 || slope[0] >= -.25f || residual >= .018
                || shoulder[1] <= .72f || shoulder[1] >= .85f) return false;
            var direction = RigVector.Unit(new[] { 1f, slope[0], slope[1] });
            var distances = new List<float>();
            foreach (var point in band)
            {
                var local = RigVector.Sub(point, shoulder);
                double distance = RigVector.Dot(local, direction);
                if (distance > 0 && RigVector.Length(RigVector.Sub(local, RigVector.Scale(direction, distance))) < .045)
                    distances.Add((float)distance);
            }
            if (distances.Count < 8) return false;
            float reach = Quantile(distances, .995) - .075f;
            if (reach <= .23f || reach >= .4f) return false;
            joints = new[] { shoulder, RigVector.Add(shoulder, RigVector.Scale(direction, reach * .52)),
                RigVector.Add(shoulder, RigVector.Scale(direction, reach)) };
            return true;
        }

        static List<float[]> ArmSections(float[][] points, float extent, bool separateBody, bool frontSurface = false)
        {
            var result = new List<float[]>();
            for (float x = .125f; x < extent * .8f; x += .0125f)
            {
                var section = points.Where(p => Math.Abs(p[0] - x) < .0125f).OrderBy(p => p[1]).ToArray();
                if (section.Length < 4) continue;
                if (separateBody && frontSurface)
                {
                    section = FrontArmSection(section);
                    if (section == null) continue;
                }
                else if (separateBody)
                {
                    float[][] upper = null;
                    int first = 0;
                    for (int i = 1; i <= section.Length; i++)
                    {
                        if (i < section.Length && section[i][1] - section[i - 1][1] <= .025f) continue;
                        if (i - first >= 4) upper = section.Skip(first).Take(i - first).ToArray();
                        first = i;
                    }
                    if (upper == null) continue;
                    section = upper;
                }
                result.Add(Enumerable.Range(0, 3).Select(axis => Quantile(section.Select(p => p[axis]), .5)).ToArray());
            }
            return result;
        }

        static float[][] FrontArmSection(float[][] points)
        {
            var remaining = new HashSet<int>(Enumerable.Range(0, points.Length));
            float[][] selected = null;
            double depth = Double.NegativeInfinity;
            while (remaining.Count > 0)
            {
                int first = remaining.First(); remaining.Remove(first);
                var component = new List<int> { first };
                for (int cursor = 0; cursor < component.Count; cursor++)
                {
                    var point = points[component[cursor]];
                    var neighbours = remaining.Where(i => Math.Pow(points[i][1] - point[1], 2)
                        + Math.Pow(points[i][2] - point[2], 2) < .025 * .025).ToArray();
                    foreach (int index in neighbours) { remaining.Remove(index); component.Add(index); }
                }
                if (component.Count < 4) continue;
                var surface = component.Select(i => points[i]).ToArray();
                double candidate = Quantile(surface.Select(p => p[2]), .5);
                if (candidate > depth) { depth = candidate; selected = surface; }
            }
            return selected;
        }

        static bool FitArmLine(List<float[]> sections, out float[] slope, out float[] intercept)
        {
            slope = intercept = null;
            if (sections.Count < 5) return false;
            var slopes = new List<float[]>();
            for (int i = 0; i < sections.Count; i++)
            for (int j = i + 2; j < sections.Count; j++)
            {
                float span = sections[j][0] - sections[i][0];
                if (span <= .025f) continue;
                slopes.Add(new[] { (sections[j][1] - sections[i][1]) / span, (sections[j][2] - sections[i][2]) / span });
            }
            if (slopes.Count == 0) return false;
            slope = Enumerable.Range(0, 2).Select(axis => Quantile(slopes.Select(p => p[axis]), .5)).ToArray();
            var fittedSlope = slope;
            intercept = Enumerable.Range(0, 2).Select(axis => Quantile(sections.Select(p => p[axis + 1] - p[0] * fittedSlope[axis]), .5)).ToArray();
            return true;
        }

        static double ArmFitResidual(List<float[]> sections, float[] slope, float[] intercept)
        {
            return sections.Average(p => Math.Sqrt(Math.Pow(p[1] - p[0] * slope[0] - intercept[0], 2)
                + Math.Pow(p[2] - p[0] * slope[1] - intercept[1], 2)));
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

        bool DrivingHeadPart(int bone, int head)
        {
            for (int parent = SharedBodyBone(bone); parent >= 0; parent = Bones[parent].Parent)
                if (SharedBodyBone(parent) == head) return true;
            return false;
        }

        double[] UprightHeadRotation(float[][] joints)
        {
            int root = PoseBone("skl_root");
            var sourceFrame = BodyFrame(JointGuides, root);
            var sourceUp = sourceFrame[1];
            var targetUp = new[] { 0f, 1f, 0f };
            var sourceAcross = sourceFrame[0];
            var targetAcross = BodyFrame(joints, root)[0];
            targetAcross = RigVector.Unit(RigVector.Sub(targetAcross, RigVector.Scale(targetUp, RigVector.Dot(targetAcross, targetUp))));
            var upright = RigVector.RotationQuaternion(sourceUp, targetUp);
            var across = RigVector.RotateQuaternion(sourceAcross, upright);
            double angle = Math.Atan2(RigVector.Dot(targetUp, RigVector.Cross(across, targetAcross)), RigVector.Dot(across, targetAcross)) * .5;
            var yaw = new[] { Math.Cos(angle), targetUp[0] * Math.Sin(angle), targetUp[1] * Math.Sin(angle), targetUp[2] * Math.Sin(angle) };
            return RigVector.MultiplyQuaternion(yaw, upright);
        }

        void FitDrivingHeadChildren(float[][] joints, double scale)
        {
            int head = PoseBone("face_1");
            var rotation = UprightHeadRotation(joints);
            foreach (int bone in Enumerable.Range(0, Bones.Length).Where(i => i != head && DrivingHeadPart(i, head)))
                joints[bone] = RigVector.Add(joints[head], RigVector.RotateQuaternion(RigVector.Scale(RigVector.Sub(JointGuides[bone], JointGuides[head]), scale), rotation));
        }

        double[] GameJointRotation(int bone, float[] from, float[] to, GamePoseSettings settings, double[] drivingHeadRotation)
        {
            if (drivingHeadRotation != null && DrivingHeadPart(bone, PoseBone("face_1")))
                return drivingHeadRotation;
            return PoseRotation(bone, from, to, settings);
        }

        float[][] GameSegmentTails(float[][] joints, GamePoseSettings settings, bool source)
        {
            var tails = Enumerable.Range(0, Bones.Length).Select(i => SegmentTail(i, joints)).ToArray();
            if (!source && settings != null && settings.NaturalHuman)
            {
                int head = PoseBone("face_1"), torso = PoseBone("spin");
                if (head >= 0 && torso >= 0)
                {
                    var from = RigVector.Sub(JointGuides[head], JointGuides[torso]);
                    var to = RigVector.Sub(joints[head], joints[torso]);
                    if (RigVector.Length(from) > .000001 && RigVector.Length(to) > .000001)
                    {
                        // Kopfrahmen je nach gespeicherter Pose vom Fahrblick oder Oberkörper ableiten.
                        var offset = RigVector.Sub(SegmentTail(head, JointGuides), JointGuides[head]);
                        // Neue Kartposen erhalten den Quellkopf relativ zum aufrechten Brustrahmen.
                        var rotation = settings.UprightDrivingHead ? UprightHeadRotation(joints) : RigVector.RotationQuaternion(from, to);
                        var direction = RigVector.RotateQuaternion(offset, rotation);
                        foreach (int bone in Enumerable.Range(0, Bones.Length).Where(i => SharedBodyBone(i) == head))
                            tails[bone] = RigVector.Add(joints[bone], direction);
                        if (settings.UprightDrivingHead)
                            foreach (int bone in Enumerable.Range(0, Bones.Length).Where(i => i != head && DrivingHeadPart(i, head)))
                                tails[bone] = RigVector.Add(joints[bone], RigVector.RotateQuaternion(RigVector.Sub(SegmentTail(bone, JointGuides), JointGuides[bone]), rotation));
                    }
                }
            }
            if (settings == null || settings.Contacts == null) return tails;
            foreach (var contact in settings.Contacts.Where(c => c.SourcePoint != null && c.Direction != null))
            {
                int bone = PoseBone(contact.Joint);
                if (bone < 0) continue;
                double length = RigVector.Length(RigVector.Sub(contact.SourcePoint, JointGuides[bone]));
                tails[bone] = source ? contact.SourcePoint : RigVector.Add(joints[bone], RigVector.Scale(contact.Direction, length));
            }
            return tails;
        }

        internal float[][] MenuBoneFrames()
        {
            var rest = ReferenceJoints();
            var transforms = GameBoneTransforms(1);
            return Enumerable.Range(0, Bones.Length).Select(bone => {
                var from = RigVector.Sub(SegmentTail(bone, rest), rest[bone]);
                var to = RigVector.Sub(SegmentTail(bone, JointGuides), JointGuides[bone]);
                var rotation = RigVector.RotationQuaternion(from, to);
                var source = (float[])Bones[bone].Matrix.Clone();
                for (int axis = 0; axis < 3; axis++)
                {
                    var column = RigVector.RotateQuaternion(new[] { source[axis], source[axis + 4], source[axis + 8] }, rotation);
                    source[axis] = column[0]; source[axis + 4] = column[1]; source[axis + 8] = column[2];
                }
                source[3] = JointGuides[bone][0]; source[7] = JointGuides[bone][1]; source[11] = JointGuides[bone][2];
                return RigMatrix.Multiply(transforms[bone], source);
            }).ToArray();
        }

        internal float[][] GameBoneTransforms(int context)
        {
            var settings = GameSettings(context);
            var drivingHeadRotation = settings.NaturalHuman && settings.UprightDrivingHead ? UprightHeadRotation(settings.Joints) : null;
            var sourceTails = GameSegmentTails(JointGuides, settings, true);
            var targetTails = GameSegmentTails(settings.Joints, settings, false);
            return Enumerable.Range(0, Bones.Length).Select(bone => {
                var from = RigVector.Sub(sourceTails[bone], JointGuides[bone]);
                var to = RigVector.Sub(targetTails[bone], settings.Joints[bone]);
                double ratio = Math.Max(.1, Math.Min(10, RigVector.Length(to) / Math.Max(.0001, RigVector.Length(from))));
                var direction = RigVector.Unit(from);
                var matrix = new float[16]; matrix[15] = 1;
                for (int axis = 0; axis < 3; axis++)
                {
                    var unit = new float[3]; unit[axis] = 1;
                    var stretched = RigVector.Add(unit, RigVector.Scale(direction, RigVector.Dot(unit, direction) * (ratio - 1)));
                    var rotated = RigVector.RotateQuaternion(stretched, GameJointRotation(bone, from, to, settings, drivingHeadRotation));
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

        float[][] MapVolumeGeometry(bool normals, float[][] reference, GamePoseSettings settings)
        {
            var drivingHeadRotation = settings != null && settings.NaturalHuman && settings.UprightDrivingHead ? UprightHeadRotation(settings.Joints) : null;
            var rotations = new double[Bones.Length][];
            var duals = new double[Bones.Length][];
            var directions = new float[Bones.Length][];
            var ratios = new double[Bones.Length];
            var sourceTails = GameSegmentTails(JointGuides, settings, true);
            var targetTails = GameSegmentTails(reference, settings, false);
            for (int bone = 0; bone < Bones.Length; bone++)
            {
                var from = RigVector.Sub(sourceTails[bone], JointGuides[bone]);
                var to = RigVector.Sub(targetTails[bone], reference[bone]);
                directions[bone] = RigVector.Unit(from);
                ratios[bone] = Math.Max(.1, Math.Min(10, RigVector.Length(to) / Math.Max(.0001, RigVector.Length(from))));
                rotations[bone] = GameJointRotation(bone, from, to, settings, drivingHeadRotation);
                var offset = RigVector.Sub(reference[bone], RigVector.RotateQuaternion(JointGuides[bone], rotations[bone]));
                duals[bone] = RigVector.MultiplyQuaternion(new[] { 0d, (double)offset[0], offset[1], offset[2] }, rotations[bone]);
                for (int axis = 0; axis < 4; axis++) duals[bone][axis] *= .5;
            }
            var source = GripGeometry(normals, settings);
            var result = new float[source.Length][];
            for (int vertex = 0; vertex < source.Length; vertex++)
            {
                var real = new double[4];
                var dual = new double[4];
                var stretched = new float[3];
                int first = BoneIndices[vertex][Array.IndexOf(BoneWeights[vertex], BoneWeights[vertex].Max())];
                for (int influence = 0; influence < BoneIndices[vertex].Length; influence++)
                {
                    int bone = BoneIndices[vertex][influence];
                    double weight = BoneWeights[vertex][influence];
                    double dot = 0;
                    for (int axis = 0; axis < 4; axis++) dot += rotations[first][axis] * rotations[bone][axis];
                    double sign = dot < 0 ? -1 : 1;
                    for (int axis = 0; axis < 4; axis++)
                    {
                        real[axis] += rotations[bone][axis] * weight * sign;
                        dual[axis] += duals[bone][axis] * weight * sign;
                    }
                    var local = normals ? source[vertex] : RigVector.Sub(source[vertex], JointGuides[bone]);
                    double stretch = RigidVertex(vertex) ? 0 : RigVector.Dot(local, directions[bone]) * (normals ? 1 / ratios[bone] - 1 : ratios[bone] - 1);
                    stretched = RigVector.Add(stretched, RigVector.Scale(RigVector.Add(source[vertex], RigVector.Scale(directions[bone], stretch)), weight));
                }
                // Duale Quaternionen verhindern das Einschnueren beim Mischen gebeugter Gelenke.
                double length = Math.Sqrt(real.Sum(v => v * v));
                if (length < .000001) throw new InvalidOperationException("Invalid blended joint rotation.");
                for (int axis = 0; axis < 4; axis++) { real[axis] /= length; dual[axis] /= length; }
                var point = RigVector.RotateQuaternion(stretched, real);
                if (!normals)
                {
                    var translation = RigVector.MultiplyQuaternion(dual, new[] { real[0], -real[1], -real[2], -real[3] });
                    point = RigVector.Add(point, new[] { (float)(2 * translation[1]), (float)(2 * translation[2]), (float)(2 * translation[3]) });
                }
                result[vertex] = normals ? RigVector.Unit(point) : point;
            }
            return result;
        }

        internal float[][] MapGeometry(bool normals, float[][] reference, bool preserveVolume = false, GamePoseSettings settings = null)
        {
            if (preserveVolume) return MapVolumeGeometry(normals, reference, settings);
            var drivingHeadRotation = settings != null && settings.NaturalHuman && settings.UprightDrivingHead ? UprightHeadRotation(settings.Joints) : null;
            var source = GripGeometry(normals, settings);
            var sourceTails = GameSegmentTails(JointGuides, settings, true);
            var targetTails = GameSegmentTails(reference, settings, false);
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
                    if (RigidVertex(v)) ratio = 1;
                    // Nur längs des Segments skalieren; die Körperdicke bleibt erhalten.
                    var adjusted = RigVector.Add(local, RigVector.Scale(direction, RigVector.Dot(local, direction) * (normals ? 1 / ratio - 1 : ratio - 1)));
                    var rotated = RigVector.RotateQuaternion(adjusted, GameJointRotation(bone, from, to, settings, drivingHeadRotation));
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
        internal static double[] MultiplyQuaternion(double[] a, double[] b)
        {
            return new[] {
                a[0]*b[0] - a[1]*b[1] - a[2]*b[2] - a[3]*b[3],
                a[0]*b[1] + a[1]*b[0] + a[2]*b[3] - a[3]*b[2],
                a[0]*b[2] - a[1]*b[3] + a[2]*b[0] + a[3]*b[1],
                a[0]*b[3] + a[1]*b[2] - a[2]*b[1] + a[3]*b[0]
            };
        }
        internal static double[] RotationQuaternion(float[] from, float[] to)
        {
            if (Length(from) < .000001 || Length(to) < .000001) return new[] { 1d, 0d, 0d, 0d };
            var a = Unit(from); var b = Unit(to);
            double cosine = Math.Max(-1, Math.Min(1, Dot(a, b)));
            var axis = Cross(a, b);
            if (cosine < -.999999)
            {
                axis = Unit(Cross(a, Math.Abs(a[0]) < .9 ? new[] { 1f, 0f, 0f } : new[] { 0f, 1f, 0f }));
                return new[] { 0d, (double)axis[0], axis[1], axis[2] };
            }
            var q = new[] { 1 + cosine, (double)axis[0], axis[1], axis[2] };
            double length = Math.Sqrt(q.Sum(v => v * v));
            return q.Select(v => v / length).ToArray();
        }
        internal static float[] RotateQuaternion(float[] point, double[] q)
        {
            var axis = new[] { (float)q[1], (float)q[2], (float)q[3] };
            var cross = Scale(Cross(axis, point), 2);
            return Add(point, Add(Scale(cross, q[0]), Cross(axis, cross)));
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
