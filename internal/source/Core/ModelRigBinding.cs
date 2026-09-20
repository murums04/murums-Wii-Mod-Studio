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
        public Dictionary<string, float[]> SourceJointGuides;
        public int[][] SourceBoneIndices;
        public float[][] SourceBoneWeights;

        void ValidateSourceBinding()
        {
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
            if (SourceJointGuides == null || SourceBoneIndices == null || SourceBoneWeights == null
                || SourceBoneIndices.Length != Points.Length || SourceBoneWeights.Length != Points.Length) return false;
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
        sealed class BoundVertex { public int[] Indices; public float[] Weights; }
        sealed class BindingResult { public BoundVertex[] Vertices; public int Missing; }

        internal void BindWithBlender(CancellationToken token)
        {
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
            File.WriteAllText(input, Serializer().Serialize(new { Points, Faces, Segments = segments,
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
            BindingMethod = "Blender bone heat";
            AlignToReference = true;
            InvalidateAlignment();
        }
    }
}
