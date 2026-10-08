using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public string SourceRigKind;
        public string[] SourceTrustedJoints;
        public bool[] SourceWeightedVertices;
        public string[] SourceComponentNames;
        public int[] VertexComponents;
        public bool ComponentsReviewed;
        public int[] RigidComponentBones;
        public Dictionary<string, float[]> ReviewedJoints;
        Dictionary<int, string> anatomyIssues;

        internal static readonly string[][] AnatomyChains = {
            new[] { "skl_root", "spin", "face_1" },
            new[] { "arm_l1", "arm_l2", "wrist_l1" },
            new[] { "arm_r1", "arm_r2", "wrist_r1" },
            new[] { "leg_l1", "leg_l2", "ankle_l1" },
            new[] { "leg_r1", "leg_r2", "ankle_r1" }
        };

        internal Dictionary<int, string> JointIssues()
        {
            if (anatomyIssues != null) return anatomyIssues;
            var issues = new Dictionary<int, string>();
            if (JointGuides == null) return issues;
            var trusted = new HashSet<string>(SourceTrustedJoints ?? new string[0]);
            double extent = Enumerable.Range(0, 3).Max(a => Points.Max(p => p[a]) - Points.Min(p => p[a]));
            foreach (var chain in AnatomyChains)
            {
                foreach (string name in chain)
                {
                    int index = PoseBone(name);
                    if (index < 0) continue;
                    float[] reviewed, source;
                    bool confirmed = ReviewedJoints != null && ReviewedJoints.TryGetValue(name, out reviewed)
                        && SameJoint(reviewed, JointGuides[index]);
                    bool known = trusted.Contains(name) && SourceJointGuides != null
                        && SourceJointGuides.TryGetValue(name, out source) && SameJoint(source, JointGuides[index]);
                    if (!confirmed && !known)
                        issues[index] = L.T("Position und Körperseite bestätigen", "Confirm position and body side");
                    if (!confirmed && Points.Min(p => RigVector.Length(RigVector.Sub(p, JointGuides[index]))) > extent * .12)
                        issues[index] = L.T("Weit von der Oberfläche entfernt", "Far from the surface");
                }
                int first = PoseBone(chain[0]), middle = PoseBone(chain[1]), last = PoseBone(chain[2]);
                if (first >= 0 && middle >= 0 && last >= 0)
                {
                    double upper = RigVector.Length(RigVector.Sub(JointGuides[first], JointGuides[middle]));
                    double lower = RigVector.Length(RigVector.Sub(JointGuides[middle], JointGuides[last]));
                    if (Math.Max(upper, lower) > Math.Max(.0001, Math.Min(upper, lower)) * 8)
                        issues[middle] = L.T("Stark ungleiche Segmente: Gelenkreihenfolge prüfen", "Very unequal segments: check joint order");
                }
                for (int i = 1; i < chain.Length; i++)
                {
                    int a = PoseBone(chain[i - 1]), b = PoseBone(chain[i]);
                    if (a < 0 || b < 0) continue;
                    double length = RigVector.Length(RigVector.Sub(JointGuides[a], JointGuides[b]));
                    if (length < extent * .002 || length > extent * .8)
                        issues[b] = L.T("Unplausible Knochenlänge: Punkt verschieben", "Implausible bone length: move joint");
                }
            }
            anatomyIssues = issues;
            return issues;
        }

        static bool SameJoint(float[] a, float[] b)
        {
            return a != null && b != null && a.Length == 3 && b.Length == 3
                && RigVector.Length(RigVector.Sub(a, b)) < .0001;
        }

        internal void ConfirmJoint(int index)
        {
            if (index < 0 || index >= Bones.Length || JointGuides == null) return;
            if (ReviewedJoints == null) ReviewedJoints = new Dictionary<string, float[]>();
            ReviewedJoints[Bones[index].Name] = (float[])JointGuides[index].Clone();
            anatomyIssues = null;
        }

        internal void ConfirmChain(int index)
        {
            if (index < 0 || index >= Bones.Length) return;
            var chain = AnatomyChains.FirstOrDefault(c => c.Contains(Bones[index].Name));
            if (chain != null)
                foreach (string name in chain) ConfirmJoint(PoseBone(name));
        }

        internal bool NeedsComponentReview()
        {
            return !SurfaceReviewCurrent;
        }

        internal void AttachComponent(int component, int bone)
        {
            if (VertexComponents == null || component < 0 || SourceComponentNames == null
                || component >= SourceComponentNames.Length || bone < 0 || bone >= Bones.Length) return;
            Assign(Enumerable.Range(0, Points.Length).Where(v => VertexComponents[v] == component), bone, 1);
            if (RigidComponentBones == null) RigidComponentBones = Enumerable.Repeat(-1, SourceComponentNames.Length).ToArray();
            RigidComponentBones[component] = bone;
            InvalidateAlignment();
            ComponentsReviewed = false;
        }

        bool RigidVertex(int vertex)
        {
            if (LocallyRigid(vertex)) return true;
            if (VertexComponents == null || RigidComponentBones == null) return false;
            int bone = RigidComponentBones[VertexComponents[vertex]];
            return bone >= 0 && BoneIndices[vertex].Length == 1 && BoneIndices[vertex][0] == bone;
        }

        internal void RequireAnatomy()
        {
            if (JointGuides == null || JointIssues().Count > 0)
                throw new InvalidDataException(L.T("Unsichere Anatomie: orange Gelenke prüfen und bestätigen.",
                    "Uncertain anatomy: review and confirm the orange joints."));
        }

        internal bool ReadyForCharacterExport
        {
            get {
                if (JointGuides == null || JointIssues().Count > 0 || !AlignToReference) return false;
                if (SurfaceReviewFingerprint == null && BindingMethod == "Source rig weights") CheckSurface();
                return !NeedsComponentReview();
            }
        }

        internal void RequireExportReview()
        {
            RequireAnatomy();
            if (!AlignToReference)
                throw new InvalidDataException(L.T("Körper zuerst neu zuordnen.", "Rebind the body first."));
            CheckSurface();
            if (NeedsComponentReview())
                throw new InvalidDataException(L.T("Kleidung, Haare und Zusatzteile in Bewegung prüfen und bestätigen.",
                    "Review clothes, hair and accessories in motion, then confirm."));
        }

        void ValidateAnatomyMetadata()
        {
            if (SourceWeightedVertices != null && SourceWeightedVertices.Length != Points.Length)
                throw new InvalidDataException("Invalid source weight provenance.");
            if (VertexComponents != null && (SourceComponentNames == null || VertexComponents.Length != Points.Length
                || VertexComponents.Any(c => c < 0 || c >= SourceComponentNames.Length)))
                throw new InvalidDataException("Invalid source mesh components.");
            if (RigidComponentBones != null && (SourceComponentNames == null || VertexComponents == null
                || RigidComponentBones.Length != SourceComponentNames.Length || RigidComponentBones.Any(b => b < -1 || b >= Bones.Length)))
                throw new InvalidDataException("Invalid rigid component assignment.");
            if (RigidVertices != null && (RigidVertices.Length > Points.Length || RigidVertices.Distinct().Count() != RigidVertices.Length
                || RigidVertices.Any(v => v < 0 || v >= Points.Length || BoneIndices[v].Length != 1)))
                throw new InvalidDataException("Invalid rigid surface selection.");
            if (ReviewedJoints != null && ReviewedJoints.Any(p => p.Value == null || p.Value.Length != 3
                || p.Value.Any(v => Single.IsNaN(v) || Single.IsInfinity(v))))
                throw new InvalidDataException("Invalid reviewed joint positions.");
        }
    }
}
