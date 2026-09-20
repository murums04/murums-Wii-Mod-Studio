using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class IntegratedModelImport
    {
        [StructLayout(LayoutKind.Sequential)] struct AiString
        {
            internal uint Length;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] internal byte[] Bytes;
            public override string ToString() { return Encoding.UTF8.GetString(Bytes, 0, (int)Math.Min(Length, 1023)); }
        }
        [StructLayout(LayoutKind.Sequential)] struct Scene
        {
            internal uint Flags;
            internal IntPtr Root;
            internal uint MeshCount;
            internal IntPtr Meshes;
            internal uint MaterialCount;
            internal IntPtr Materials;
            internal uint AnimationCount; internal IntPtr Animations;
            internal uint TextureCount; internal IntPtr Textures;
        }
        [StructLayout(LayoutKind.Sequential)] struct Node
        {
            internal AiString Name;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal float[] Transform;
            internal IntPtr Parent;
            internal uint ChildCount;
            internal IntPtr Children;
            internal uint MeshCount;
            internal IntPtr Meshes;
        }
        [StructLayout(LayoutKind.Sequential)] struct Mesh
        {
            internal uint Primitive, VertexCount, FaceCount;
            internal IntPtr Vertices, Normals, Tangents, Bitangents;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] internal IntPtr[] Colors;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] internal IntPtr[] Uvs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] internal uint[] UvComponents;
            internal IntPtr Faces;
            internal uint BoneCount;
            internal IntPtr Bones;
            internal uint Material;
        }
        [StructLayout(LayoutKind.Sequential)] struct EmbeddedTexture
        {
            internal uint Width, Height;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)] internal byte[] Hint;
            internal IntPtr Data;
        }
        sealed class PreviewData
        {
            internal readonly List<float[]> Uvs = new List<float[]>();
            internal readonly List<int> FaceMaterials = new List<int>();
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int StringDelegate(IntPtr material, byte[] key, uint type, uint index, out AiString value);
        [StructLayout(LayoutKind.Sequential)] struct Face { internal uint Count; internal IntPtr Indices; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ImportDelegate(byte[] path, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ReleaseDelegate(IntPtr scene);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ErrorDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ExportDelegate(IntPtr scene, byte[] format, byte[] path, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ColorDelegate(IntPtr material, byte[] key, uint type, uint index, [Out] float[] color);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr library, string name);
        static IntPtr library;
        static ImportDelegate import;
        static ReleaseDelegate release;
        static ErrorDelegate error;
        static ExportDelegate export;
        static ColorDelegate materialColor;
        static StringDelegate materialString;
        static readonly object gate = new object();

        static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }
        static T Read<T>(IntPtr pointer) { return (T)Marshal.PtrToStructure(pointer, typeof(T)); }
        static T Bind<T>(string name) where T : class
        {
            IntPtr pointer = GetProcAddress(library, name);
            if (pointer == IntPtr.Zero) throw new InvalidDataException("Model importer API missing: " + name);
            return Marshal.GetDelegateForFunctionPointer(pointer, typeof(T)) as T;
        }
        static void Initialize()
        {
            if (materialString != null) return;
            if (IntPtr.Size != 8) throw new NotSupportedException(L.T("Der integrierte Modellimport benötigt 64-Bit-Windows.", "Integrated model import requires 64-bit Windows."));
            string path = Path.Combine(ModelRuntime.Root, "assimp-vc143-mt.dll");
            library = LoadLibraryEx(path, IntPtr.Zero, 0x00000008);
            if (library == IntPtr.Zero) throw new IOException(L.T("Die interne Modellkomponente fehlt. Studio mit dem vollständigen Installer aktualisieren.", "The internal model component is missing. Update Studio using the complete installer."));
            import = Bind<ImportDelegate>("aiImportFile"); release = Bind<ReleaseDelegate>("aiReleaseImport");
            error = Bind<ErrorDelegate>("aiGetErrorString"); export = Bind<ExportDelegate>("aiExportScene");
            materialColor = Bind<ColorDelegate>("aiGetMaterialColor");
            materialString = Bind<StringDelegate>("aiGetMaterialString");
        }
        static IntPtr Open(string path)
        {
            Initialize();
            if (new FileInfo(path).Length > 512L * 1024 * 1024) throw new InvalidDataException("Model exceeds the 512 MB import limit.");
            IntPtr scene = import(Utf8(path), 0x2u | 0x8u | 0x400u | 0x10000000u);
            if (scene == IntPtr.Zero) throw new InvalidDataException(L.T("Modell konnte nicht importiert werden: ", "Could not import model: ") + Marshal.PtrToStringAnsi(error()));
            return scene;
        }
        internal sealed class PreviewLimitException : Exception { }

        internal static void ReadModel(string path, CharacterModelImport result)
        {
            lock (gate)
            {
                IntPtr pointer = Open(path);
                try
                {
                    Scene scene = Read<Scene>(pointer);
                    if (scene.MeshCount == 0 || scene.MeshCount > 10000 || scene.Root == IntPtr.Zero) throw new InvalidDataException("No supported mesh found.");
                    var bones = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < scene.MeshCount; i++)
                    {
                        Mesh mesh = Read<Mesh>(Marshal.ReadIntPtr(scene.Meshes, i * IntPtr.Size));
                        if (mesh.BoneCount > 4096) throw new InvalidDataException("Too many bones.");
                        if (mesh.BoneCount > 0) result.Skins++;
                        for (int j = 0; j < mesh.BoneCount; j++) bones.Add(Read<AiString>(Marshal.ReadIntPtr(mesh.Bones, j * IntPtr.Size)).ToString());
                    }
                    result.Joints = bones.Count;
                    result.BoneNames.AddRange(bones);
                    var preview = new PreviewData();
                    Visit(scene, scene.Root, Identity(), result, new HashSet<IntPtr>(), 0, preview);
                    string folder = ModelRuntime.NewWorkFolder();
                    var materials = ReadMaterials(scene, path, folder);
                    result.Rig = new ModelRig {
                        Folder = folder, Points = result.Points.ToArray(), Faces = result.Faces.ToArray(),
                        Uvs = preview.Uvs.ToArray(), Normals = result.Points.Select(p => new[] { 0f, 1f, 0f }).ToArray(),
                        FaceMaterials = preview.FaceMaterials.ToArray(), Materials = materials,
                        Bones = new[] { new ModelRig.Bone { Name = "preview", Parent = -1, Matrix = Identity().Select(v => (float)v).ToArray() } },
                        BoneIndices = result.Points.Select(p => new[] { 0 }).ToArray(), BoneWeights = result.Points.Select(p => new[] { 1f }).ToArray()
                    };
                    result.Rig.Validate();
                }
                finally { release(pointer); }
            }
        }
        static void Visit(Scene scene, IntPtr pointer, double[] parent, CharacterModelImport result, HashSet<IntPtr> seen, int depth, PreviewData preview)
        {
            if (depth > 128 || !seen.Add(pointer) || seen.Count > 50000) throw new InvalidDataException("Invalid or excessively deep scene hierarchy.");
            Node node = Read<Node>(pointer);
            double[] world = Multiply(parent, node.Transform.Select(v => (double)v).ToArray());
            if (node.MeshCount > scene.MeshCount || node.ChildCount > 50000) throw new InvalidDataException("Invalid model node.");
            for (int i = 0; i < node.MeshCount; i++)
            {
                int meshId = Marshal.ReadInt32(node.Meshes, i * 4);
                if (meshId < 0 || meshId >= scene.MeshCount) throw new InvalidDataException("Invalid mesh index.");
                Mesh mesh = Read<Mesh>(Marshal.ReadIntPtr(scene.Meshes, meshId * IntPtr.Size));
                if (mesh.VertexCount > 200000 || mesh.FaceCount > 200000 || result.Points.Count + mesh.VertexCount > 200000)
                    throw new PreviewLimitException();
                int start = result.Points.Count;
                var points = new float[checked((int)mesh.VertexCount * 3)];
                Marshal.Copy(mesh.Vertices, points, 0, points.Length);
                var uvs = new float[points.Length];
                if (mesh.Uvs[0] != IntPtr.Zero) Marshal.Copy(mesh.Uvs[0], uvs, 0, uvs.Length);
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    var transformed = new float[3];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        double value = world[axis * 4 + 3];
                        for (int k = 0; k < 3; k++) value += world[axis * 4 + k] * points[v * 3 + k];
                        if (Double.IsNaN(value) || Double.IsInfinity(value) || Math.Abs(value) > 1e12) throw new InvalidDataException("Invalid model coordinate.");
                        transformed[axis] = (float)value;
                    }
                    result.Points.Add(transformed);
                    preview.Uvs.Add(new[] { uvs[v * 3], uvs[v * 3 + 1] });
                }
                Color color = Color.FromArgb(132, 118, 168);
                if (mesh.Material < scene.MaterialCount)
                {
                    var rgba = new float[4];
                    if (materialColor(Marshal.ReadIntPtr(scene.Materials, (int)mesh.Material * IntPtr.Size), Utf8("$clr.diffuse"), 0, 0, rgba) == 0)
                        color = Color.FromArgb(255, Channel(rgba[0]), Channel(rgba[1]), Channel(rgba[2]));
                }
                int faceSize = Marshal.SizeOf(typeof(Face));
                for (int f = 0; f < mesh.FaceCount; f++)
                {
                    Face face = Read<Face>(IntPtr.Add(mesh.Faces, checked(f * faceSize)));
                    if (face.Count < 3) continue;
                    if (face.Count > 1000) throw new InvalidDataException("Invalid polygon size.");
                    if (result.Faces.Count >= 200000) throw new PreviewLimitException();
                    var indices = new int[face.Count]; Marshal.Copy(face.Indices, indices, 0, indices.Length);
                    if (indices.Any(v => v < 0 || v >= mesh.VertexCount)) throw new InvalidDataException("Invalid vertex index.");
                    result.Faces.Add(indices.Select(v => v + start).ToArray()); result.FaceColors.Add(color);
                    preview.FaceMaterials.Add(mesh.Material < scene.MaterialCount ? (int)mesh.Material : 0);
                }
            }
            for (int i = 0; i < node.ChildCount; i++) Visit(scene, Marshal.ReadIntPtr(node.Children, i * IntPtr.Size), world, result, seen, depth + 1, preview);
        }
        static ModelRig.Material[] ReadMaterials(Scene scene, string source, string folder)
        {
            if (scene.MaterialCount == 0 || scene.MaterialCount > 1024 || scene.TextureCount > 4096) throw new InvalidDataException("Invalid material count.");
            var result = new ModelRig.Material[scene.MaterialCount];
            for (int i = 0; i < result.Length; i++)
            {
                IntPtr material = Marshal.ReadIntPtr(scene.Materials, i * IntPtr.Size);
                var color = new[] { 1f, 1f, 1f, 1f };
                materialColor(material, Utf8("$clr.diffuse"), 0, 0, color);
                AiString texture;
                string texturePath = null;
                if (materialString(material, Utf8("$tex.file"), 12, 0, out texture) == 0 || materialString(material, Utf8("$tex.file"), 1, 0, out texture) == 0)
                    texturePath = texture.ToString();
                string name = "surface-" + i + ".png";
                bool saved = false;
                try
                {
                    int index;
                    if (!String.IsNullOrEmpty(texturePath) && texturePath.StartsWith("*") && Int32.TryParse(texturePath.Substring(1), out index) && index >= 0 && index < scene.TextureCount)
                    {
                        var data = Read<EmbeddedTexture>(Marshal.ReadIntPtr(scene.Textures, index * IntPtr.Size));
                        if (data.Height == 0 && data.Width > 0 && data.Width < 64 * 1024 * 1024)
                        {
                            var bytes = new byte[data.Width]; Marshal.Copy(data.Data, bytes, 0, bytes.Length);
                            using (var stream = new MemoryStream(bytes)) using (var image = Image.FromStream(stream)) SavePreviewTexture(image, Path.Combine(folder, name));
                            saved = true;
                        }
                    }
                    else if (!String.IsNullOrEmpty(texturePath))
                    {
                        string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), texturePath));
                        if (!new Uri(path).IsUnc && File.Exists(path))
                            using (var image = Image.FromFile(path)) { SavePreviewTexture(image, Path.Combine(folder, name)); saved = true; }
                    }
                }
                catch (ArgumentException) { }
                catch (IOException) { }
                result[i] = new ModelRig.Material { Name = "surface-" + i, Texture = saved ? name : null, Color = color };
            }
            return result;
        }
        static void SavePreviewTexture(Image image, string path)
        {
            double factor = Math.Min(1, 1024.0 / Math.Max(image.Width, image.Height));
            using (var bitmap = new Bitmap(Math.Max(1, (int)(image.Width * factor)), Math.Max(1, (int)(image.Height * factor))))
            {
                using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        static int Channel(float value) { return Single.IsNaN(value) ? 128 : (int)Math.Max(0, Math.Min(255, value * 255)); }
        static double[] Identity() { return new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }; }
        static double[] Multiply(double[] a, double[] b)
        {
            var result = new double[16];
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) for (int i = 0; i < 4; i++) result[r * 4 + c] += a[r * 4 + i] * b[i * 4 + c];
            return result;
        }
        internal static void ExportCopy(string source, string destination, float scale, string format)
        {
            if (Single.IsNaN(scale) || Single.IsInfinity(scale) || scale < .000001f || scale > 1000000) throw new InvalidDataException("Invalid model scale.");
            if (File.Exists(destination) || Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a new filename; existing files are kept.");
            lock (gate)
            {
                IntPtr pointer = Open(source);
                try
                {
                    Scene scene = Read<Scene>(pointer);
                    Node node = Read<Node>(scene.Root);
                    for (int i = 0; i < 12; i++) node.Transform[i] *= scale;
                    Marshal.Copy(node.Transform, 0, IntPtr.Add(scene.Root, Marshal.SizeOf(typeof(AiString))), 16);
                    string staging = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination)), ".studio-model-" + Guid.NewGuid().ToString("N") + Path.GetExtension(destination));
                    try
                    {
                        if (export(pointer, Utf8(format), Utf8(staging), 0) != 0)
                            throw new IOException("Model export failed: " + Marshal.PtrToStringAnsi(error()));
                        if (!File.Exists(staging) || new FileInfo(staging).Length < 20) throw new IOException("Empty model export.");
                        File.Move(staging, destination);
                    }
                    finally { if (File.Exists(staging)) File.Delete(staging); }
                }
                finally { release(pointer); }
            }
        }
    }
}
