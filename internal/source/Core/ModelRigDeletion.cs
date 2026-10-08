using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public int RemovedSurfaceCount;
        internal sealed class MeshState
        {
            internal float[][] Points, Normals, Uvs, Weights, SourceWeights;
            internal int[][] Faces, Indices, SourceIndices;
            internal int[] FaceMaterials, Components, Manual, Rigid;
            internal bool[] SourceWeighted;
            internal GripVertex[] Grips;
            internal int RemovedCount;
        }

        internal MeshState CaptureMesh()
        {
            return new MeshState { Points = Points, Normals = Normals, Uvs = Uvs, Faces = Faces,
                FaceMaterials = FaceMaterials, Indices = BoneIndices, Weights = BoneWeights,
                SourceIndices = SourceBoneIndices, SourceWeights = SourceBoneWeights,
                SourceWeighted = SourceWeightedVertices, Components = VertexComponents,
                Manual = ManualVertices, Rigid = RigidVertices, Grips = GripVertices, RemovedCount = RemovedSurfaceCount };
        }

        internal void RestoreMesh(MeshState state)
        {
            Points = state.Points; Normals = state.Normals; Uvs = state.Uvs;
            Faces = state.Faces; FaceMaterials = state.FaceMaterials;
            BoneIndices = state.Indices; BoneWeights = state.Weights;
            SourceBoneIndices = state.SourceIndices; SourceBoneWeights = state.SourceWeights;
            SourceWeightedVertices = state.SourceWeighted; VertexComponents = state.Components;
            ManualVertices = state.Manual; RigidVertices = state.Rigid;
            GripVertices = state.Grips; RemovedSurfaceCount = state.RemovedCount;
            surfaceNeighbours = null; InvalidateAlignment();
        }

        internal int[] DeletionPart(IEnumerable<int> seeds)
        {
            // Keine Verbindung allein durch gleiche Position: Kleidung darf den Körper berühren.
            var edges = Enumerable.Range(0, Points.Length).Select(v => new List<int>()).ToArray();
            foreach (var face in Faces)
                for (int i = 0; i < 3; i++) { int a = face[i], b = face[(i + 1) % 3]; edges[a].Add(b); edges[b].Add(a); }
            var selected = new HashSet<int>(seeds.Where(v => v >= 0 && v < Points.Length));
            var queue = new Queue<int>(selected);
            while (queue.Count > 0) foreach (int v in edges[queue.Dequeue()]) if (selected.Add(v)) queue.Enqueue(v);
            return selected.OrderBy(v => v).ToArray();
        }

        internal int DeletionFaceCount(IEnumerable<int> vertices)
        {
            var selected = new HashSet<int>(vertices);
            return Faces.Count(f => f.All(selected.Contains));
        }

        internal int DeleteSurface(IEnumerable<int> vertices)
        {
            Validate();
            var selected = new HashSet<int>(vertices);
            if (selected.Any(v => v < 0 || v >= Points.Length)) throw new ArgumentException("Invalid deletion selection.");
            var keptFaces = Enumerable.Range(0, Faces.Length).Where(i => !Faces[i].All(selected.Contains)).ToArray();
            int removed = Faces.Length - keptFaces.Length;
            if (removed == 0) return 0;
            if (keptFaces.Length == 0) throw new InvalidDataException(L.T("Das gesamte Modell kann nicht gelöscht werden. Auswahl verkleinern.", "The entire model cannot be deleted. Reduce the selection."));
            var kept = keptFaces.SelectMany(i => Faces[i]).Distinct().OrderBy(v => v).ToArray();
            var map = Enumerable.Repeat(-1, Points.Length).ToArray();
            for (int i = 0; i < kept.Length; i++) map[kept[i]] = i;
            var old = CaptureMesh();
            try {
                Points = RemapVertices(Points, kept); Normals = RemapVertices(Normals, kept); Uvs = RemapVertices(Uvs, kept);
                BoneIndices = RemapVertices(BoneIndices, kept); BoneWeights = RemapVertices(BoneWeights, kept);
                SourceBoneIndices = RemapVertices(SourceBoneIndices, kept); SourceBoneWeights = RemapVertices(SourceBoneWeights, kept);
                SourceWeightedVertices = RemapVertices(SourceWeightedVertices, kept); VertexComponents = RemapVertices(VertexComponents, kept);
                ManualVertices = RemapSelection(ManualVertices, map); RigidVertices = RemapSelection(RigidVertices, map);
                GripVertices = GripVertices == null ? null : GripVertices.Where(v => map[v.Index] >= 0)
                    .Select(v => new GripVertex { Index = map[v.Index], Point = v.Point, Normal = v.Normal }).ToArray();
                Faces = keptFaces.Select(i => Faces[i].Select(v => map[v]).ToArray()).ToArray();
                FaceMaterials = keptFaces.Select(i => FaceMaterials[i]).ToArray();
                surfaceNeighbours = null; InvalidateAlignment(); Validate();
            }
            catch { RestoreMesh(old); throw; }
            ComponentsReviewed = false; SurfaceReviewFingerprint = null;
            RemovedSurfaceCount += removed;
            return removed;
        }

        static T[] RemapVertices<T>(T[] values, int[] kept) { return values == null ? null : kept.Select(v => values[v]).ToArray(); }
        static int[] RemapSelection(int[] values, int[] map) { return values == null ? null : values.Where(v => map[v] >= 0).Select(v => map[v]).ToArray(); }
    }
}
