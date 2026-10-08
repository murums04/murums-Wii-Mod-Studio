using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public string BindingMethod, BindingWarning;
        public float[][] BindingProxyPoints;
        public int[][] BindingProxyFaces;
        public Dictionary<string, float[]> SourceJointGuides;
        public int[][] SourceBoneIndices;
        public float[][] SourceBoneWeights;

        void ValidateSourceBinding()
        {
            if (BindingProxyPoints != null || BindingProxyFaces != null)
            {
                if (BindingProxyPoints == null || BindingProxyFaces == null || BindingProxyPoints.Length == 0
                    || BindingProxyPoints.Length > 200000 || BindingProxyFaces.Length == 0 || BindingProxyFaces.Length > 200000
                    || BindingProxyPoints.Any(p => p == null || p.Length != 3 || p.Any(v => Single.IsNaN(v) || Single.IsInfinity(v) || Math.Abs(v) > 1e9))
                    || BindingProxyFaces.Any(f => f == null || f.Length != 3 || f.Any(i => i < 0 || i >= BindingProxyPoints.Length)))
                    throw new InvalidDataException("Invalid binding surface.");
            }
            if (SourceJointGuides != null && SourceJointGuides.Any(p => p.Value == null || p.Value.Length != 3
                || p.Value.Any(v => Single.IsNaN(v) || Single.IsInfinity(v) || Math.Abs(v) > 1e8)))
                throw new InvalidDataException("Invalid source joint guides.");
            if (SourceBoneIndices == null && SourceBoneWeights == null) return;
            if (SourceBoneIndices == null || SourceBoneWeights == null || SourceBoneIndices.Length != Points.Length || SourceBoneWeights.Length != Points.Length)
                throw new InvalidDataException("Invalid source binding size.");
            for (int v = 0; v < Points.Length; v++)
                if (SourceBoneIndices[v] == null || SourceBoneWeights[v] == null || SourceBoneIndices[v].Length == 0
                    || SourceBoneIndices[v].Length > 4 || SourceBoneIndices[v].Length != SourceBoneWeights[v].Length
                    || SourceBoneIndices[v].Any(i => i < 0 || i >= Bones.Length)
                    || SourceBoneWeights[v].Any(w => Single.IsNaN(w) || Single.IsInfinity(w) || w < 0)
                    || Math.Abs(SourceBoneWeights[v].Sum() - 1) > .001)
                    throw new InvalidDataException("Invalid source binding weights.");
        }

        internal bool RestoreSourceBinding()
        {
            if (SourceWeightedVertices == null || SourceWeightedVertices.Any(v => !v)) return false;
            if (SourceJointGuides == null || SourceBoneIndices == null || SourceBoneWeights == null
                || SourceBoneIndices.Length != Points.Length || SourceBoneWeights.Length != Points.Length) return false;
            if (AnatomyChains.SelectMany(c => c).Any(name => PoseBone(name) >= 0 && !SourceJointGuides.ContainsKey(name))) return false;
            for (int b = 0; b < Bones.Length; b++)
                if (SourceJointGuides.ContainsKey(Bones[b].Name)
                    && RigVector.Length(RigVector.Sub(JointGuides[b], SourceJointGuides[Bones[b].Name])) > .0001) return false;
            var fixedVertices = new HashSet<int>(ManualVertices ?? new int[0]);
            for (int v = 0; v < Points.Length; v++)
            {
                if (fixedVertices.Contains(v)) continue;
                var weights = new Dictionary<int, float>();
                for (int j = 0; j < SourceBoneIndices[v].Length; j++)
                {
                    int b = SourceBoneIndices[v][j];
                    while (!BoneEnabled(b) && b >= 0) b = Bones[b].Parent;
                    if (b < 0) throw new InvalidDataException("Source weights have no enabled parent.");
                    weights[b] = (weights.ContainsKey(b) ? weights[b] : 0) + SourceBoneWeights[v][j];
                }
                BoneIndices[v] = weights.Keys.ToArray(); BoneWeights[v] = weights.Values.ToArray();
            }
            BindingWarning = null; BindingMethod = "Source rig weights"; AlignToReference = true; InvalidateAlignment(); return true;
        }
        bool CanKeepSourceWeights(int vertex)
        {
            if (SourceWeightedVertices == null || !SourceWeightedVertices[vertex]
                || SourceBoneIndices == null || SourceBoneWeights == null || SourceJointGuides == null) return false;
            foreach (int bone in SourceBoneIndices[vertex])
            {
                if (!BoneEnabled(bone)) return false;
                var chain = AnatomyChains.FirstOrDefault(c => c.Contains(Bones[bone].Name));
                if (chain == null) return false;
                foreach (string name in chain)
                {
                    float[] source;
                    int index = PoseBone(name);
                    if (index < 0 || !SourceJointGuides.TryGetValue(name, out source) || !SameJoint(source, JointGuides[index])) return false;
                }
            }
            return true;
        }

        sealed class BoundVertex { public int[] Indices; public float[] Weights; }
        sealed class BindingResult { public BoundVertex[] Vertices; public int Missing; }

        internal void BindWithBlender(CancellationToken token)
        {
            RequireAnatomy();
            if (JointGuides == null) throw new InvalidOperationException("Place the joints first.");
            if (RestoreSourceBinding()) return;
            string work = ModelRuntime.NewWorkFolder();
            var candidates = Enumerable.Range(0, Bones.Length).Where(i => GuideBone(i)
                && (Bones[i].Name == "skl_root" || Bones[i].Name == "spin" || Bones[i].Name == "face_1"
                    || Bones[i].Name.StartsWith("arm_") || Bones[i].Name.StartsWith("wrist_")
                    || Bones[i].Name.StartsWith("leg_") || Bones[i].Name.StartsWith("ankle_"))).ToArray();
            var segments = candidates.Select(i => new { Index = i, Head = JointGuides[i], Tail = SegmentTail(i, JointGuides) })
                .Where(s => RigVector.Length(RigVector.Sub(s.Tail, s.Head)) > .0001).ToArray();
            string input = Path.Combine(work, "input.json"), output = Path.Combine(work, "weights.json");
            File.WriteAllText(input, Serializer().Serialize(new { Points, Faces, ProxyPoints = BindingProxyPoints,
                ProxyFaces = BindingProxyFaces, Segments = segments,
                WeldDistance = Math.Max(.000001, ReferenceHeight * .000001) }));
            RunScript("ModelRigBind.py", work, token, input, output);
            var result = Serializer().Deserialize<BindingResult>(File.ReadAllText(output));
            if (result.Vertices == null || result.Vertices.Length != Points.Length || result.Missing > Points.Length * .05)
                throw new InvalidDataException(L.T("Die automatische Zuordnung konnte diese Oberfläche nicht zuverlässig binden. Gelenkpunkte prüfen; das Modell wurde nicht verändert.",
                    "Automatic binding could not reliably bind this surface. Check the joint positions; the model was not changed."));
            var allowed = new HashSet<int>(candidates);
            foreach (var vertex in result.Vertices)
                if (vertex.Indices == null || vertex.Weights == null || vertex.Indices.Length != vertex.Weights.Length
                    || vertex.Indices.Length > 4 || vertex.Indices.Any(i => !allowed.Contains(i))
                    || vertex.Weights.Any(w => w <= 0 || Single.IsNaN(w) || Single.IsInfinity(w))
                    || (vertex.Weights.Length > 0 && Math.Abs(vertex.Weights.Sum() - 1) > .001))
                    throw new InvalidDataException("Invalid automatic binding result.");
            var fixedVertices = new HashSet<int>(ManualVertices ?? new int[0]);
            // Vereinzelte unverbundene Details erhalten Gewichte vom nächsten gebundenen Punkt.
            var bound = Enumerable.Range(0, Points.Length).Where(i => result.Vertices[i].Indices.Length > 0).ToArray();
            if (bound.Length == 0) throw new InvalidDataException("No surface could be bound.");
            for (int v = 0; v < Points.Length; v++)
            {
                if (fixedVertices.Contains(v)) continue;
                if (CanKeepSourceWeights(v))
                {
                    BoneIndices[v] = (int[])SourceBoneIndices[v].Clone();
                    BoneWeights[v] = (float[])SourceBoneWeights[v].Clone();
                    continue;
                }
                var vertex = result.Vertices[v];
                if (vertex.Indices.Length == 0)
                {
                    int selected = v;
                    int nearest = bound.OrderBy(i => RigVector.Dot(RigVector.Sub(Points[i], Points[selected]), RigVector.Sub(Points[i], Points[selected]))).First();
                    vertex = result.Vertices[nearest];
                }
                BoneIndices[v] = (int[])vertex.Indices.Clone();
                BoneWeights[v] = (float[])vertex.Weights.Clone();
            }
            BindingWarning = null;
            ComponentsReviewed = false;
            ModelRuntime.DeleteWorkFolder(work);
            BindingMethod = "Blender bone heat";
            AlignToReference = true;
            InvalidateAlignment();
        }
    }
}
