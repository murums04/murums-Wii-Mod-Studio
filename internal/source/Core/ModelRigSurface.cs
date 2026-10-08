using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public int[] RigidVertices;
        public string SurfaceReviewFingerprint;
        public Dictionary<string, string> SurfaceReviewFingerprints;
        public Dictionary<string, string> MenuSurfaceReviews;
        HashSet<int> rigidVertexSet;
        SurfaceReport surfaceReport;
        int[][] surfaceNeighbours;

        internal void ValidateSurfaceReviewState()
        {
            if (SurfaceReviewFingerprints != null && (SurfaceReviewFingerprints.Count > 37
                || SurfaceReviewFingerprints.Any(p => p.Key != "" && !ValidVehicleKey(p.Key)
                    || p.Value == null || p.Value.Length != 64 || p.Value.Any(c => !Uri.IsHexDigit(c)))))
                throw new InvalidDataException("Invalid vehicle surface reviews.");
            if (MenuSurfaceReviews != null && (MenuSurfaceReviews.Count > 512
                || MenuSurfaceReviews.Any(p => !ValidMenuReviewKey(p.Key)
                    || p.Value == null || p.Value.Length != 64 || p.Value.Any(c => !Uri.IsHexDigit(c)))))
                throw new InvalidDataException("Invalid menu vehicle surface reviews.");
        }

        internal sealed class SurfaceIssue
        {
            internal string Reason, Pose;
            internal int[] Vertices;
            internal bool Urgent;
            public override string ToString() { return Reason + (String.IsNullOrEmpty(Pose) ? "" : " · " + Pose); }
        }
        internal sealed class SurfaceReport
        {
            internal string Fingerprint, Vehicle;
            internal readonly List<SurfaceIssue> Issues = new List<SurfaceIssue>();
            internal int[] MarkedVertices { get { return Issues.SelectMany(i => i.Vertices).Distinct().ToArray(); } }
        }

        internal string AnatomyStatus
        {
            get {
                if (JointIssues().Count == 0) return L.T("Gelenke übernommen / geprüft", "Joints recognised / reviewed");
                return SourceJointGuides != null && SourceJointGuides.Count > 0
                    ? L.T("Einzelne Gelenke prüfen", "Review uncertain joints")
                    : L.T("Gelenke am Modell zeigen", "Place the joints on your model");
            }
        }

        internal static readonly string[] SurfaceTargets = { "head", "torso", "arm_l", "arm_r", "leg_l", "leg_r", "hips", "rigid" };
        internal static string SurfaceTargetLabel(string target)
        {
            switch (target) {
                case "head": return L.T("Kopf / Haare", "Head / hair");
                case "torso": return L.T("Oberkörper / Kleidung", "Torso / clothes");
                case "arm_l": return L.T("Linker Arm", "Left arm");
                case "arm_r": return L.T("Rechter Arm", "Right arm");
                case "leg_l": return L.T("Linkes Bein", "Left leg");
                case "leg_r": return L.T("Rechtes Bein", "Right leg");
                case "hips": return L.T("Becken", "Hips");
                default: return L.T("Starr am gewählten Körperteil", "Rigidly to the selected body part");
            }
        }
        int[] SurfaceBones(string target, int selected)
        {
            string[] names;
            switch (target) {
                case "head": names = new[] { "face_1" }; break;
                case "torso": names = new[] { "skl_root", "spin" }; break;
                case "hips": names = new[] { "skl_root" }; break;
                case "arm_l": case "arm_r": names = new[] { target + "1", target + "2", "wrist_" + target.Substring(4) + "1" }; break;
                case "leg_l": case "leg_r": names = new[] { target + "1", target + "2", "ankle_" + target.Substring(4) + "1" }; break;
                case "rigid": return BoneEnabled(selected) ? new[] { selected } : new int[0];
                default: throw new ArgumentException("Unknown body region.");
            }
            return names.Select(PoseBone).Where(BoneEnabled).ToArray();
        }

        internal void CorrectSurface(IEnumerable<int> vertices, string target, int selected)
        {
            RequireAnatomy();
            var marked = ExpandSurfaceSelection(vertices);
            if (marked.Length == 0 || marked.Any(v => v < 0 || v >= Points.Length)) throw new ArgumentException("Select a surface first.");
            var allowed = SurfaceBones(target, selected);
            if (allowed.Length == 0) throw new InvalidDataException("No matching body part.");
            var rigid = new HashSet<int>(RigidVertices ?? new int[0]);
            if (RigidComponentBones != null && VertexComponents != null) {
                var components = new HashSet<int>(marked.Select(v => VertexComponents[v]));
                for (int v = 0; v < Points.Length; v++)
                    if (components.Contains(VertexComponents[v]) && RigidVertex(v)) rigid.Add(v);
                foreach (int component in components) RigidComponentBones[component] = -1;
            }
            rigid.ExceptWith(marked);
            foreach (int vertex in marked)
            {
                var kept = new Dictionary<int, double>();
                for (int j = 0; j < BoneIndices[vertex].Length; j++)
                    if (allowed.Contains(BoneIndices[vertex][j])) {
                        int bone = BoneIndices[vertex][j];
                        kept[bone] = (kept.ContainsKey(bone) ? kept[bone] : 0) + BoneWeights[vertex][j];
                    }
                if (kept.Values.Sum() <= .000001)
                    foreach (int bone in allowed)
                        kept[bone] = 1 / Math.Max(.000001, RigVector.SegmentDistanceSquared(Points[vertex], JointGuides[bone], SegmentTail(bone, JointGuides)));
                double sum = kept.Values.Sum();
                var pairs = kept.Where(p => p.Value > sum * .00001).OrderByDescending(p => p.Value).ToArray();
                sum = pairs.Sum(p => p.Value);
                BoneIndices[vertex] = pairs.Select(p => p.Key).ToArray();
                BoneWeights[vertex] = pairs.Select(p => (float)(p.Value / sum)).ToArray();
                if (allowed.Length == 1) rigid.Add(vertex);
            }
            RigidVertices = rigid.OrderBy(v => v).ToArray();
            ManualVertices = (ManualVertices ?? new int[0]).Concat(marked).Distinct().ToArray();
            ComponentsReviewed = false;
            SurfaceReviewFingerprint = null;
            InvalidateAlignment();
        }

        bool LocallyRigid(int vertex)
        {
            if (RigidVertices == null) return false;
            if (rigidVertexSet == null) rigidVertexSet = new HashSet<int>(RigidVertices);
            return rigidVertexSet.Contains(vertex) && BoneIndices[vertex].Length == 1;
        }

        internal int[] ExpandSurfaceSelection(IEnumerable<int> selected)
        {
            var marked = new HashSet<int>(selected);
            if (marked.Any(v => v < 0 || v >= Points.Length)) throw new ArgumentException("Invalid surface selection.");
            Func<int, string> key = v => String.Join("/", Points[v].Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))
                + ":" + String.Join("/", BoneIndices[v].Select((b, j) => b + "=" + BoneWeights[v][j].ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            var seams = new HashSet<string>(marked.Select(key));
            for (int v = 0; v < Points.Length; v++) if (seams.Contains(key(v))) marked.Add(v);
            return marked.OrderBy(v => v).ToArray();
        }

        internal int[][] SurfaceNeighbours()
        {
            if (surfaceNeighbours != null) return surfaceNeighbours;
            var edges = Enumerable.Range(0, Points.Length).Select(v => new HashSet<int>()).ToArray();
            foreach (var face in Faces)
                for (int j = 0; j < 3; j++) { int a = face[j], b = face[(j + 1) % 3]; edges[a].Add(b); edges[b].Add(a); }
            var seams = new Dictionary<string, int>();
            for (int v = 0; v < Points.Length; v++)
            {
                string key = String.Join("/", Points[v].Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                int first;
                if (seams.TryGetValue(key, out first)) { edges[v].Add(first); edges[first].Add(v); }
                else seams[key] = v;
            }
            surfaceNeighbours = edges.Select(e => e.ToArray()).ToArray();
            return surfaceNeighbours;
        }

        internal int[] ConnectedSurface(IEnumerable<int> selected)
        {
            var graph = SurfaceNeighbours();
            var found = new HashSet<int>(selected.Where(v => v >= 0 && v < Points.Length));
            var pending = new Queue<int>(found);
            while (pending.Count > 0)
                foreach (int next in graph[pending.Dequeue()]) if (found.Add(next)) pending.Enqueue(next);
            return found.OrderBy(v => v).ToArray();
        }

        internal int[] GrowSurfaceSelection(IEnumerable<int> include, IEnumerable<int> exclude)
        {
            var inside = new HashSet<int>(ExpandSurfaceSelection(include));
            var outside = new HashSet<int>(ExpandSurfaceSelection(exclude));
            if (inside.Count == 0 || outside.Count == 0 || inside.Overlaps(outside))
                throw new InvalidDataException(L.T("Zuerst getrennte Flächen für „gehört dazu“ und „bleibt draußen“ markieren.", "Mark separate surfaces for include and exclude first."));
            var graph = SurfaceNeighbours();
            var distances = Enumerable.Repeat(Double.PositiveInfinity, Points.Length).ToArray();
            var labels = new bool[Points.Length];
            var queue = new SortedSet<Tuple<double, int>>();
            foreach (int v in inside.Concat(outside)) { distances[v] = 0; labels[v] = inside.Contains(v); queue.Add(Tuple.Create(0d, v)); }
            while (queue.Count > 0)
            {
                var current = queue.Min; queue.Remove(current);
                if (current.Item1 != distances[current.Item2]) continue;
                foreach (int next in graph[current.Item2])
                {
                    double candidate = current.Item1 + RigVector.Length(RigVector.Sub(Points[current.Item2], Points[next]));
                    if (candidate >= distances[next]) continue;
                    distances[next] = candidate; labels[next] = labels[current.Item2]; queue.Add(Tuple.Create(candidate, next));
                }
            }
            return Enumerable.Range(0, Points.Length).Where(v => labels[v] && !Double.IsPositiveInfinity(distances[v])).ToArray();
        }

        internal string SurfaceFingerprint()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write("Surface review 3: vehicle context and proper crossings");
                writer.Write(Points.Length); writer.Write(Faces.Length); writer.Write(AlignToReference);
                foreach (var point in Points) foreach (float value in point) writer.Write(value);
                foreach (var face in Faces) foreach (int value in face) writer.Write(value);
                foreach (int material in FaceMaterials) writer.Write(material);
                foreach (var material in Materials) writer.Write(material.Color[3]);
                writer.Write(ReferenceHeight); writer.Write(SizePercent);
                foreach (var bone in Bones) { writer.Write(bone.Name); writer.Write(bone.Parent); foreach (float value in bone.Matrix) writer.Write(value); }
                for (int v = 0; v < Points.Length; v++) {
                    writer.Write(BoneIndices[v].Length);
                    for (int j = 0; j < BoneIndices[v].Length; j++) { writer.Write(BoneIndices[v][j]); writer.Write(BoneWeights[v][j]); }
                    writer.Write(RigidVertex(v));
                }
                if (JointGuides != null) foreach (var point in JointGuides) foreach (float value in point) writer.Write(value);
                writer.Write(ActiveVehicle ?? "");
                writer.Write(Serializer().Serialize(new { MenuPose, DrivingPose = GameSettings(2), NaturalVehicleFitting, GripVertices, SourceHandGrips }));
                writer.Flush(); stream.Position = 0;
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
            }
        }

        internal bool SurfaceReviewCurrent
        {
            get
            {
                if (!ComponentsReviewed) return false;
                string fingerprint = SurfaceFingerprint(), saved;
                return SurfaceReviewFingerprint == fingerprint || SurfaceReviewFingerprints != null
                    && SurfaceReviewFingerprints.TryGetValue(ActiveVehicle ?? "", out saved) && saved == fingerprint;
            }
        }

        void RememberSurfaceReview(SurfaceReport report)
        {
            // Kopien fuer abgeleitete Haltungen duerfen die Pruefungen des Projekts nicht aendern.
            SurfaceReviewFingerprints = SurfaceReviewFingerprints == null ? new Dictionary<string, string>()
                : new Dictionary<string, string>(SurfaceReviewFingerprints);
            SurfaceReviewFingerprints[ActiveVehicle ?? ""] = report.Fingerprint;
            ComponentsReviewed = true;
            SurfaceReviewFingerprint = report.Fingerprint;
        }

        internal void ConfirmSurfaceReview()
        {
            var report = CheckSurface();
            RememberSurfaceReview(report);
        }

        internal SurfaceReport CheckSurface()
        {
            RequireAnatomy();
            if (!AlignToReference) throw new InvalidDataException(L.T("Gelenke zuerst zuordnen.", "Assign the joints first."));
            string fingerprint = SurfaceFingerprint();
            if (surfaceReport != null && surfaceReport.Fingerprint == fingerprint && surfaceReport.Vehicle == ActiveVehicle) return surfaceReport;
            var testRig = (ModelRig)MemberwiseClone();
            testRig.VehiclePoses = VehiclePoses == null ? new Dictionary<string, GamePoseSettings>() : new Dictionary<string, GamePoseSettings>(VehiclePoses);
            testRig.InitializeGamePose(1, null); testRig.InitializeGamePose(2, null);
            var report = new SurfaceReport { Fingerprint = fingerprint, Vehicle = ActiveVehicle };
            var mixed = new Dictionary<string, HashSet<int>>();
            for (int v = 0; v < Points.Length; v++)
                for (int a = 0; a < BoneIndices[v].Length; a++)
                    for (int b = a + 1; b < BoneIndices[v].Length; b++)
                    {
                        if (BoneWeights[v][a] < .1 || BoneWeights[v][b] < .1) continue;
                        int first = BoneIndices[v][a], second = BoneIndices[v][b];
                        string x = Bones[first].Name, y = Bones[second].Name;
                        bool remote = x == "face_1" && (y.StartsWith("arm_") || y.StartsWith("wrist_") || y.StartsWith("leg_") || y.StartsWith("ankle_"))
                            || y == "face_1" && (x.StartsWith("arm_") || x.StartsWith("wrist_") || x.StartsWith("leg_") || x.StartsWith("ankle_"));
                        if (!remote) continue;
                        string label = L.T("Folgt gleichzeitig: ", "Follows both: ") + ModelRigForm.BoneLabel(Bones[Math.Min(first, second)].Name)
                            + " + " + ModelRigForm.BoneLabel(Bones[Math.Max(first, second)].Name);
                        HashSet<int> area;
                        if (!mixed.TryGetValue(label, out area)) mixed[label] = area = new HashSet<int>();
                        area.Add(v);
                    }
            foreach (var pair in mixed) AddSurfaceIssues(report, pair.Value, pair.Key, "");
            var poses = new List<Tuple<string, float[][], double>> {
                Tuple.Create(L.T("Neutral", "Neutral"), Points, 1d),
                Tuple.Create(L.T("Menü", "Menu"), testRig.GameGeometry(1, false), (double)testRig.GameSettings(1).Scale / 100),
                Tuple.Create(L.T("Fahren", "Driving"), testRig.GameGeometry(2, false), (double)testRig.GameSettings(2).Scale / 100)
            };
            foreach (string name in new[] { "arm_l2", "arm_r2", "leg_l2", "leg_r2" }) {
                int bone = PoseBone(name);
                if (bone >= 0) foreach (float angle in new[] { -60f, 60f })
                    poses.Add(Tuple.Create(ModelRigForm.BoneLabel(name) + " " + angle.ToString("0") + "°", Pose(bone, angle), 1d));
            }
            double extent = Enumerable.Range(0, 3).Max(a => Points.Max(p => p[a]) - Points.Min(p => p[a]));
            var collisionPoses = new HashSet<float[][]>(poses.Skip(1).Take(2).Select(p => p.Item2));
            var collisions = new SurfaceCollisionProbe(this);
            foreach (var pose in poses)
            {
                var stretched = new HashSet<int>(); var collapsed = new HashSet<int>(); var jumped = new HashSet<int>(); var rigid = new HashSet<int>();
                var points = pose.Item2; double scale = Math.Max(.000001, pose.Item3);
                for (int v = 0; v < Points.Length; v++)
                    if (points[v].Any(x => Single.IsNaN(x) || Single.IsInfinity(x)) || RigVector.Length(RigVector.Sub(RigVector.Scale(points[v], 1 / scale), Points[v])) > extent * 2) jumped.Add(v);
                foreach (var face in Faces)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int a = face[j], b = face[(j + 1) % 3];
                        double before = RigVector.Length(RigVector.Sub(Points[a], Points[b]));
                        if (before < extent * .000001) continue;
                        double ratio = RigVector.Length(RigVector.Sub(points[a], points[b])) / before / scale;
                        if (ratio > 3) { stretched.Add(a); stretched.Add(b); }
                        if (RigidVertex(a) && RigidVertex(b) && BoneIndices[a][0] == BoneIndices[b][0] && Math.Abs(ratio - 1) > .02) { rigid.Add(a); rigid.Add(b); }
                    }
                    double oldArea = RigVector.Length(RigVector.Cross(RigVector.Sub(Points[face[1]], Points[face[0]]), RigVector.Sub(Points[face[2]], Points[face[0]])));
                    if (oldArea < extent * extent * .0000000001) continue;
                    double area = RigVector.Length(RigVector.Cross(RigVector.Sub(points[face[1]], points[face[0]]), RigVector.Sub(points[face[2]], points[face[0]])));
                    if (area / oldArea / scale / scale < .05) foreach (int v in face) collapsed.Add(v);
                }
                AddSurfaceIssues(report, stretched, L.T("Stark gedehnte Fläche", "Strongly stretched surface"), pose.Item1);
                AddSurfaceIssues(report, collapsed, L.T("Zusammenfallende Fläche", "Collapsing surface"), pose.Item1);
                AddSurfaceIssues(report, jumped, L.T("Sehr große Bewegung", "Very large movement"), pose.Item1, true);
                AddSurfaceIssues(report, rigid, L.T("Starres Teil verändert seine Form", "Rigid part changes shape"), pose.Item1, true);
                if (collisionPoses.Contains(points) && !jumped.Any(v => points[v].Any(x => Single.IsNaN(x) || Single.IsInfinity(x))))
                    AddSurfaceIssues(report, collisions.MarkedVertices(points),
                    L.T("Neue Durchdringung: Fläche und Zuordnung prüfen", "New surface crossing: review the area and assignment"), pose.Item1, true);
            }
            surfaceReport = report;
            if (SurfaceReviewFingerprint == null && !report.Issues.Any(i => i.Urgent) && BindingMethod == "Source rig weights"
                && (ManualVertices == null || ManualVertices.Length == 0) && SourceWeightedVertices != null && SourceWeightedVertices.All(v => v)
                && SourceBoneIndices != null && SourceBoneWeights != null
                && Enumerable.Range(0, Points.Length).All(v => BoneIndices[v].SequenceEqual(SourceBoneIndices[v]) && BoneWeights[v].SequenceEqual(SourceBoneWeights[v]))) {
                RememberSurfaceReview(report);
            }
            return report;
        }

        void AddSurfaceIssues(SurfaceReport report, HashSet<int> marked, string reason, string pose, bool urgent = false)
        {
            if (marked.Count == 0) return;
            var neighbours = SurfaceNeighbours();
            var remaining = new HashSet<int>(marked);
            while (remaining.Count > 0)
            {
                int first = remaining.First(); var area = new List<int>(); var queue = new Queue<int>();
                queue.Enqueue(first); remaining.Remove(first);
                while (queue.Count > 0) {
                    int current = queue.Dequeue(); area.Add(current);
                    foreach (int next in neighbours[current]) if (remaining.Remove(next)) queue.Enqueue(next);
                }
                report.Issues.Add(new SurfaceIssue { Reason = reason, Pose = pose, Vertices = area.ToArray(), Urgent = urgent });
            }
        }
    }
}
