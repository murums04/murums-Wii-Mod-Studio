using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public sealed class Bone { public string Name; public int Parent; public float[] Matrix; }
        public sealed class Material { public string Name, Texture; public float[] Color; }
        public float[][] Points, Normals, Uvs, BoneWeights;
        public int[][] Faces, BoneIndices;
        public int[] FaceMaterials;
        public Bone[] Bones;
        public Material[] Materials;
        public float ReferenceHeight, SizePercent;
        public int OriginalTriangles;
        internal string Folder, Reference;
        internal static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 100 * 1024 * 1024, RecursionLimit = 100 }; }
        internal static ModelRig Prepare(CharacterModelImport source, string template, float sizePercent, int triangles, CancellationToken token)
        {
            string folder = ModelRuntime.NewWorkFolder();
            string reference = Path.Combine(folder, "reference.dae");
            StudioModelLibrary.Call("ExportReference", template, reference);
            string input = Path.GetExtension(source.Source).Equals(".obj", StringComparison.OrdinalIgnoreCase)
                ? source.Source : source.PreparedSource;
            if (input == null)
            {
                input = Path.Combine(folder, "source.glb");
                IntegratedModelImport.ExportCopy(source.Source, input, 1, "glb2");
            }
            RunScript("ModelRigPrepare.py", folder, token, input, reference, folder, triangles.ToString(), sizePercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Path.GetExtension(source.Source).Equals(".dae", StringComparison.OrdinalIgnoreCase) ? "merge" : "keep");
            var rig = Read(Path.Combine(folder, "rig.json"));
            rig.Reference = reference;
            return rig;
        }
        internal static ModelRig Read(string path)
        {
            if (new FileInfo(path).Length > 100 * 1024 * 1024) throw new InvalidDataException("Rig file is too large.");
            var rig = Serializer().Deserialize<ModelRig>(File.ReadAllText(path));
            rig.Folder = Path.GetDirectoryName(Path.GetFullPath(path));
            rig.Reference = Path.Combine(rig.Folder, "reference.dae");
            rig.Validate();
            return rig;
        }
        internal void Validate()
        {
            if (Points == null || Points.Length == 0 || Points.Length > 200000 || Faces == null || Faces.Length == 0 || Faces.Length > 200000
                || Bones == null || Bones.Length == 0 || Bones.Length > 256 || Materials == null || Materials.Length == 0 || Materials.Length > 1024
                || BoneWeights == null || BoneWeights.Length != Points.Length || BoneIndices == null || BoneIndices.Length != Points.Length
                || Normals == null || Normals.Length != Points.Length || Uvs == null || Uvs.Length != Points.Length
                || FaceMaterials == null || FaceMaterials.Length != Faces.Length) throw new InvalidDataException("Invalid rig structure.");
            ValidateGamePoses();
            ValidateSourceBinding();
            if (JointGuides != null && (JointGuides.Length != Bones.Length || JointGuides.Any(p => p == null || p.Length != 3 || p.Any(v => Single.IsNaN(v) || Single.IsInfinity(v) || Math.Abs(v) > 1e8))))
                throw new InvalidDataException("Invalid joint guides.");
            if (ManualVertices != null && (ManualVertices.Length > Points.Length || ManualVertices.Any(v => v < 0 || v >= Points.Length))) throw new InvalidDataException("Invalid manually assigned vertices.");
            if (AlignToReference && JointGuides == null) throw new InvalidDataException("Missing alignment joints.");
            for (int i = 0; i < Bones.Length; i++)
                if (Bones[i] == null || String.IsNullOrWhiteSpace(Bones[i].Name) || Bones[i].Parent >= i || Bones[i].Parent < -1 || Bones[i].Matrix == null
                    || Bones[i].Matrix.Length != 16 || Bones[i].Matrix.Any(v => Single.IsNaN(v) || Single.IsInfinity(v))) throw new InvalidDataException("Invalid rig bone.");
            if (DisabledBones != null && (DisabledBones.Distinct().Count() != DisabledBones.Length || DisabledBones.Any(i => i < 0 || i >= Bones.Length || Bones[i].Parent < 0)))
                throw new InvalidDataException("Invalid disabled model parts.");
            if (Bones.Select(b => b.Name).Distinct(StringComparer.Ordinal).Count() != Bones.Length) throw new InvalidDataException("Duplicate bone names.");
            for (int i = 0; i < Points.Length; i++)
            {
                if (Points[i] == null || Normals[i] == null || Uvs[i] == null || BoneIndices[i] == null || BoneWeights[i] == null || Points[i].Length != 3 || Normals[i].Length != 3 || Uvs[i].Length != 2
                    || Points[i].Concat(Normals[i]).Concat(Uvs[i]).Any(v => Single.IsNaN(v) || Single.IsInfinity(v) || Math.Abs(v) > 1e9)
                    || BoneIndices[i].Length == 0 || BoneIndices[i].Length > 4 || BoneIndices[i].Length != BoneWeights[i].Length
                    || BoneIndices[i].Any(b => !BoneEnabled(b)) || BoneWeights[i].Any(w => Single.IsNaN(w) || Single.IsInfinity(w) || w < 0)
                    || Math.Abs(BoneWeights[i].Sum() - 1) > .001) throw new InvalidDataException("Invalid vertex weights.");
            }
            for (int i = 0; i < Faces.Length; i++)
                if (Faces[i] == null || Faces[i].Length != 3 || Faces[i].Any(v => v < 0 || v >= Points.Length) || FaceMaterials[i] < 0 || FaceMaterials[i] >= Materials.Length)
                    throw new InvalidDataException("Invalid rig face.");
            foreach (var material in Materials)
                if (material == null || String.IsNullOrEmpty(material.Name) || material.Color == null || material.Color.Length != 4
                    || material.Color.Any(v => Single.IsNaN(v) || Single.IsInfinity(v))
                    || material.Texture != null && (Path.GetFileName(material.Texture) != material.Texture || !File.Exists(Path.Combine(Folder, material.Texture))))
                    throw new InvalidDataException("Missing or invalid rig texture.");
        }
        internal void Save(string path) { Validate(); BackupManager.WriteAllBytesSafely(path, System.Text.Encoding.UTF8.GetBytes(Serializer().Serialize(this))); }
        internal CharacterModelImport Preview()
        {
            var model = new CharacterModelImport { Source = Path.Combine(Folder, "rig.json"), ReferenceHeight = ReferenceHeight, Joints = Bones.Length, Skins = 1, Rig = this };
            model.Points.AddRange(Points); model.Faces.AddRange(Faces); model.BoneNames.AddRange(Bones.Select(b => b.Name));
            foreach (int material in FaceMaterials)
            {
                var color = Materials[material].Color;
                model.FaceColors.Add(Color.FromArgb(255, Channel(color[0]), Channel(color[1]), Channel(color[2])));
            }
            return model;
        }
        static int Channel(float value) { return (int)Math.Max(0, Math.Min(255, value * 255)); }
        internal float[][] Pose(int selected, float degrees, bool referencePose = false)
        {
            var geometry = referencePose ? AlignedGeometry(false) : Points;
            if (!BoneEnabled(selected) || degrees == 0) return geometry;
            var affected = new HashSet<int>();
            for (int b = 0; b < Bones.Length; b++)
                for (int p = b; p >= 0; p = Bones[p].Parent) if (p == selected) { affected.Add(b); break; }
            var m = Bones[selected].Matrix;
            var pivot = !referencePose && JointGuides != null ? JointGuides[selected] : new[] { m[3], m[7], m[11] };
            var result = new float[Points.Length][];
            for (int i = 0; i < Points.Length; i++)
            {
                float influence = 0;
                for (int j = 0; j < BoneIndices[i].Length; j++) if (affected.Contains(BoneIndices[i][j])) influence += BoneWeights[i][j];
                var p = geometry[i];
                var moved = MovementPoint(p, pivot, Bones[selected].Name, degrees);
                result[i] = RigVector.Add(p, RigVector.Scale(RigVector.Sub(moved, p), influence));
            }
            return result;
        }
        internal float[][] BonePositions(int selected, float degrees, bool referencePose = false)
        {
            var points = !referencePose && JointGuides != null ? JointGuides.Select(p => (float[])p.Clone()).ToArray() : ReferenceJoints();
            if (!BoneEnabled(selected) || degrees == 0) return points;
            var pivot = points[selected];
            for (int i = 0; i < Bones.Length; i++)
                for (int parent = Bones[i].Parent; parent >= 0; parent = Bones[parent].Parent)
                    if (parent == selected)
                    {
                        points[i] = MovementPoint(points[i], pivot, Bones[selected].Name, degrees);
                        break;
                    }
            return points;
        }
        static float[] MovementPoint(float[] point, float[] pivot, string name, float degrees)
        {
            var axis = name.StartsWith("arm") || name.StartsWith("wrist") ? new[] { 0f, 0f, 1f } : new[] { 1f, 0f, 0f };
            var local = RigVector.Sub(point, pivot);
            double angle = degrees * Math.PI / 180, cosine = Math.Cos(angle), sine = Math.Sin(angle);
            return RigVector.Add(pivot, RigVector.Add(RigVector.Add(RigVector.Scale(local, cosine), RigVector.Scale(RigVector.Cross(axis, local), sine)), RigVector.Scale(axis, RigVector.Dot(axis, local) * (1 - cosine))));
        }
        internal void Assign(IEnumerable<int> vertices, int bone, float strength)
        {
            if (vertices == null || !BoneEnabled(bone) || Single.IsNaN(strength) || Single.IsInfinity(strength) || strength <= 0 || strength > 1) throw new ArgumentException("Invalid bone weight.");
            var selectedVertices = vertices.Distinct().ToArray();
            if (selectedVertices.Any(v => v < 0 || v >= Points.Length)) throw new ArgumentException("Unknown vertex.");
            InvalidateAlignment();
            foreach (int vertex in selectedVertices)
            {
                var weights = new Dictionary<int, float>();
                for (int j = 0; j < BoneIndices[vertex].Length; j++) weights[BoneIndices[vertex][j]] = BoneWeights[vertex][j] * (1 - strength);
                weights[bone] = (weights.ContainsKey(bone) ? weights[bone] : 0) + strength;
                var selected = weights.Where(w => w.Value > .00001f).OrderByDescending(w => w.Value).Take(4).ToArray();
                float total = selected.Sum(w => w.Value);
                BoneIndices[vertex] = selected.Select(w => w.Key).ToArray(); BoneWeights[vertex] = selected.Select(w => w.Value / total).ToArray();
            }
            ManualVertices = (ManualVertices ?? new int[0]).Concat(selectedVertices).Distinct().ToArray();
        }
        internal string ExportDae(CancellationToken token, int triangleLimit = 0, string reference = null, int gameContext = 0, RigPoseReference gameReference = null)
        {
            string path = Path.Combine(Folder, "rig-reviewed.json");
            Validate();
            var output = (ModelRig)MemberwiseClone();
            output.Points = gameContext == 0 ? AlignedGeometry(false) : GameExportGeometry(gameContext, false, gameReference);
            output.Normals = gameContext == 0 ? AlignedGeometry(true) : GameExportGeometry(gameContext, true, gameReference);
            output.MenuPose = output.RacePose = null;
            output.VehiclePoses = null;
            output.ActiveVehicle = null;
            output.NaturalVehicleFitting = false;
            output.JointGuides = null;
            if (gameReference != null)
            {
                var mapping = Enumerable.Range(0, Bones.Length).Select(bone => {
                    int referenceBone = gameReference.MatchBone(this, bone);
                    int mapped = referenceBone < 0 ? -1 : Array.FindIndex(Bones, b => b.Name == gameReference.Names[referenceBone]);
                    return mapped < 0 ? bone : mapped;
                }).ToArray();
                output.BoneIndices = BoneIndices.Select(indices => indices.Select(bone => mapping[bone]).ToArray()).ToArray();
            }
            output.AlignToReference = false;
            output.Save(path);
            string destination = Path.Combine(Folder, triangleLimit == 0 ? "rigged.dae" : "rigged-lod.dae");
            RunScript("ModelRigExport.py", Folder, token, path, reference ?? Reference, destination, triangleLimit.ToString());
            return destination;
        }
        internal static void RunScript(string name, string work, CancellationToken token, params string[] args)
        {
            string script = Path.Combine(work, name);
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Studio." + name))
            using (var output = File.Create(script)) { if (input == null) throw new IOException("Missing internal model resource."); input.CopyTo(output); }
            ModelRuntime.Run(ModelRuntime.Blender(token), "--background --factory-startup --disable-autoexec --offline-mode --python-exit-code 1 --python "
                + ModelRuntime.Quote(script) + " -- " + String.Join(" ", args.Select(ModelRuntime.Quote)), work, token);
        }
    }
}
