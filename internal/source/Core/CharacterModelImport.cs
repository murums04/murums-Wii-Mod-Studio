using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace murumsWiiModStudio
{
    internal sealed class CharacterModelImport
    {
        internal readonly List<float[]> Points = new List<float[]>();
        internal readonly List<int[]> Faces = new List<int[]>();
        internal readonly List<string> Warnings = new List<string>();
        internal string Source, PreparedSource;
        internal ModelRig Rig;
        internal readonly List<string> BoneNames = new List<string>();
        internal readonly List<Color> FaceColors = new List<Color>();
        internal int Joints, Skins;
        internal float FitScale = 1, ReferenceHeight;
        internal void FitTo(CharacterModelImport reference)
        {
            float height = Points.Max(p => p[1]) - Points.Min(p => p[1]);
            float target = reference.Points.Max(p => p[1]) - reference.Points.Min(p => p[1]);
            if (height < .000001f || target < .000001f) throw new InvalidDataException("Model has no usable vertical height.");
            float factor = target / height;
            if (Single.IsNaN(factor) || Single.IsInfinity(factor) || factor < .000001f || factor > 1000000)
                throw new InvalidDataException("Model scale is outside the supported range.");
            FitScale = factor;
            ReferenceHeight = target;
        }
        internal static CharacterModelImport LoadVisual(string path, System.Threading.CancellationToken cancellation)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".dae" && extension != ".obj") return Load(path, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("Model exceeds the 128 MB inspection limit.");
            var model = new CharacterModelImport { Source = Path.GetFullPath(path) };
            model.ReadPreview(model.Source, cancellation);
            model.Warnings.Add("Geometry and texture preview. Game lighting and animations may differ.");
            if (model.Joints == 0) model.Warnings.Add("Use Assign / review movement to prepare the selected RR skeleton.");
            return model;
        }
        void ReadPreview(string input, System.Threading.CancellationToken cancellation)
        {
            try { IntegratedModelImport.ReadModel(input, this); }
            catch (IntegratedModelImport.PreviewLimitException)
            {
                Points.Clear(); Faces.Clear(); FaceColors.Clear(); BoneNames.Clear();
                Joints = Skins = 0; Rig = null;
                string preview = ModelRuntime.SimplifyPreview(input, cancellation);
                IntegratedModelImport.ReadModel(preview, this);
                Warnings.Add(L.T("Vorschau automatisch vereinfacht. Die Originaldatei bleibt erhalten und wird für die RR-Konvertierung verwendet.",
                    "Preview simplified automatically. The original file is preserved and used for RR conversion."));
            }
        }
        const int Limit = 200000;
        static float Number(string value)
        {
            float result = Single.Parse(value, CultureInfo.InvariantCulture);
            if (Single.IsNaN(result) || Single.IsInfinity(result)) throw new InvalidDataException("Non-finite model coordinate.");
            return result;
        }
        static string[] Words(string text)
        {
            return text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }
        internal static CharacterModelImport Load(string path)
        {
            return Load(path, System.Threading.CancellationToken.None);
        }
        internal static CharacterModelImport Load(string path, System.Threading.CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            string format = Path.GetExtension(path).ToLowerInvariant();
            bool integrated = new[] { ".glb", ".gltf", ".blend", ".usdz" }.Contains(format);
            if (!integrated && new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Model inspection is limited to 32 MB per source.");
            var model = new CharacterModelImport { Source = Path.GetFullPath(path) };
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (integrated)
            {
                model.PreparedSource = ModelRuntime.Prepare(path, cancellation);
                model.ReadPreview(model.PreparedSource, cancellation);
            }
            else if (extension == ".obj") model.ReadObj(path);
            else if (extension == ".dae") model.ReadDae(path);
            else throw new InvalidDataException("Import GLB, glTF, BLEND, USDZ, DAE or OBJ.");
            if (model.Points.Count == 0 || model.Faces.Count == 0) throw new InvalidDataException("No supported mesh faces found.");
            if (model.Joints == 0 || model.Skins == 0)
                model.Warnings.Add("No supplied character rig. Use Assign / review movement to prepare the selected RR skeleton.");
            model.Warnings.Add(integrated ? "Geometry and texture preview. Game lighting and animations may differ."
                : "Geometry preview only: materials, scene transforms and animations are not simulated.");
            model.Warnings.Add("A source model is not yet a playable Wii character. Check its rig against the selected RR driver before conversion.");
            return model;
        }
        void AddPoint(float x, float y, float z)
        {
            if (Points.Count >= Limit) throw new InvalidDataException("Model exceeds the 200,000-vertex inspection limit.");
            Points.Add(new[] { x, y, z });
        }
        void AddFace(int[] face)
        {
            if (Faces.Count >= Limit || face.Length > 1000) throw new InvalidDataException("Model exceeds the face inspection limit.");
            if (face.Length < 3 || face.Any(i => i < 0 || i >= Points.Count)) throw new InvalidDataException("Invalid face vertex index.");
            Faces.Add(face);
        }
        void ReadObj(string path)
        {
            foreach (string raw in File.ReadLines(path))
            {
                var line = raw.Split('#')[0].Trim();
                var words = Words(line);
                if (words.Length == 0) continue;
                if (words[0] == "v" && words.Length >= 4)
                    AddPoint(Number(words[1]), Number(words[2]), Number(words[3]));
                else if (words[0] == "f")
                    AddFace(words.Skip(1).Select(w => {
                        int index = Int32.Parse(w.Split('/')[0], CultureInfo.InvariantCulture);
                        return index < 0 ? Points.Count + index : index - 1;
                    }).ToArray());
                else if (words[0] == "mtllib" && words.Length > 1 && !File.Exists(Path.Combine(Path.GetDirectoryName(path), String.Join(" ", words.Skip(1)))))
                    Warnings.Add("Material library not found: " + String.Join(" ", words.Skip(1)));
            }
        }
        void ReadDae(string path)
        {
            var document = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 };
            using (var reader = XmlReader.Create(path, settings)) document.Load(reader);
            Joints = document.SelectNodes("//*[local-name()='node' and @type='JOINT']").Count;
            foreach (XmlElement joint in document.SelectNodes("//*[local-name()='node' and @type='JOINT']"))
                BoneNames.Add(joint.HasAttribute("sid") ? joint.GetAttribute("sid") : joint.GetAttribute("name"));
            Skins = document.SelectNodes("//*[local-name()='skin']").Count;
            foreach (XmlElement mesh in document.SelectNodes("//*[local-name()='mesh']"))
            {
                foreach (XmlElement primitive in mesh.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "triangles" || e.LocalName == "polylist"))
                {
                    var inputs = primitive.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "input").ToArray();
                    var vertex = inputs.FirstOrDefault(e => e.GetAttribute("semantic") == "VERTEX");
                    if (vertex == null) continue;
                    var vertices = mesh.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.GetAttribute("id") == vertex.GetAttribute("source").TrimStart('#'));
                    if (vertices == null) throw new InvalidDataException("Missing Collada vertices.");
                    var position = vertices.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.LocalName == "input" && e.GetAttribute("semantic") == "POSITION");
                    if (position == null) throw new InvalidDataException("Missing Collada positions.");
                    var source = mesh.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.GetAttribute("id") == position.GetAttribute("source").TrimStart('#'));
                    if (source == null) throw new InvalidDataException("Missing Collada position source.");
                    var array = source.SelectSingleNode("*[local-name()='float_array']");
                    var accessor = source.SelectSingleNode("*[local-name()='technique_common']/*[local-name()='accessor']") as XmlElement;
                    if (array == null || accessor == null) throw new InvalidDataException("Missing position array/accessor.");
                    int stride = accessor.HasAttribute("stride") ? Int32.Parse(accessor.GetAttribute("stride")) : 1;
                    int offset = accessor.HasAttribute("offset") ? Int32.Parse(accessor.GetAttribute("offset")) : 0;
                    int count = Int32.Parse(accessor.GetAttribute("count"));
                    var values = Words(array.InnerText);
                    if (stride < 3 || offset < 0 || count < 0 || count > Limit || (long)offset + (long)count * stride > values.Length)
                        throw new InvalidDataException("Invalid Collada position accessor.");
                    int first = Points.Count;
                    for (int i = 0; i < count; i++)
                        AddPoint(Number(values[offset + i * stride]), Number(values[offset + i * stride + 1]), Number(values[offset + i * stride + 2]));
                    int inputStride = inputs.Max(e => e.HasAttribute("offset") ? Int32.Parse(e.GetAttribute("offset")) : 0) + 1;
                    int vertexOffset = vertex.HasAttribute("offset") ? Int32.Parse(vertex.GetAttribute("offset")) : 0;
                    if (inputStride <= 0 || inputStride > 64 || vertexOffset < 0) throw new InvalidDataException("Invalid face input stride.");
                    var indexNode = primitive.SelectSingleNode("*[local-name()='p']");
                    if (indexNode == null) continue;
                    int[] indices = Words(indexNode.InnerText).Select(Int32.Parse).ToArray();
                    int faceCount = Int32.Parse(primitive.GetAttribute("count"));
                    if (faceCount < 0 || faceCount > Limit) throw new InvalidDataException("Collada face count is too large.");
                    var counts = primitive.LocalName == "triangles"
                        ? Enumerable.Repeat(3, faceCount).ToArray()
                        : Words(primitive.SelectSingleNode("*[local-name()='vcount']").InnerText).Select(Int32.Parse).ToArray();
                    if (counts.Length != faceCount) throw new InvalidDataException("Collada face count does not match vcount.");
                    int cursor = 0;
                    foreach (int length in counts)
                    {
                        if (length < 3 || length > 1000 || (long)cursor + (long)length * inputStride > indices.Length)
                            throw new InvalidDataException("Invalid Collada face.");
                        var face = new int[length];
                        for (int j = 0; j < length; j++)
                        {
                            int index = indices[cursor + j * inputStride + vertexOffset];
                            if (index < 0 || index >= count) throw new InvalidDataException("Collada position index out of bounds.");
                            face[j] = first + index;
                        }
                        cursor += length * inputStride;
                        AddFace(face);
                    }
                }
            }
            var firstScene = document.SelectSingleNode("//*[local-name()='library_visual_scenes']/*[local-name()='visual_scene']");
            if (firstScene != null)
            {
                var roots = firstScene.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "node").ToArray();
                double factor = 1;
                while (roots.Length == 1 && roots[0].GetAttribute("id").StartsWith(CharacterModelScaler.RootId, StringComparison.Ordinal))
                {
                    var scaleNode = roots[0].ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.LocalName == "scale");
                    if (scaleNode == null) break;
                    var values = Words(scaleNode.InnerText).Select(Number).ToArray();
                    if (values.Length != 3 || values[0] != values[1] || values[0] != values[2]) break;
                    factor *= values[0];
                    roots = roots[0].ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "node").ToArray();
                }
                foreach (var point in Points)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        float scaled = (float)(point[axis] * factor);
                        if (Single.IsNaN(scaled) || Single.IsInfinity(scaled)) throw new InvalidDataException("Scaled model bounds are invalid.");
                        point[axis] = scaled;
                    }
            }
            foreach (XmlNode image in document.SelectNodes("//*[local-name()='library_images']/*[local-name()='image']/*[local-name()='init_from']"))
            {
                string reference = Uri.UnescapeDataString(image.InnerText.Trim());
                Uri uri;
                if (Uri.TryCreate(reference, UriKind.Absolute, out uri))
                {
                    if (!uri.IsFile) { Warnings.Add("External texture requires manual review: " + reference); continue; }
                    reference = uri.LocalPath;
                }
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(path), reference)))
                    Warnings.Add("Texture not found: " + reference);
            }
        }
        internal Bitmap Preview(int view)
        {
            var bitmap = new Bitmap(900, 500);
            var points = Points.Select(p => view == 1 ? new PointF(p[2], -p[1]) : view == 2 ? new PointF(p[0], -p[2]) : new PointF(p[0], -p[1])).ToArray();
            float left = points.Min(p => p.X), top = points.Min(p => p.Y);
            float width = points.Max(p => p.X) - left, height = points.Max(p => p.Y) - top;
            if (Single.IsInfinity(width) || Single.IsInfinity(height)) throw new InvalidDataException("Model bounds too large.");
            float scale = Math.Min(840 / Math.Max(width, .0001f), 440 / Math.Max(height, .0001f));
            using (var g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(180, 148, 255)))
            {
                g.Clear(Color.FromArgb(35, 36, 44));
                foreach (var face in Faces.Take(30000))
                {
                    var polygon = face.Select(i => new PointF(450 + (points[i].X - left - width / 2) * scale, 250 + (points[i].Y - top - height / 2) * scale)).ToArray();
                    g.DrawPolygon(pen, polygon);
                }
            }
            return bitmap;
        }
    }
}
