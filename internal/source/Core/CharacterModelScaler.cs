using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace murumsWiiModStudio
{
    internal static class CharacterModelScaler
    {
        internal const string RootId = "murumsStudioScaleRoot";
        internal static void SaveCopy(string source, string destination, float factor)
        {
            if (Single.IsNaN(factor) || Single.IsInfinity(factor) || factor < .000001f || factor > 1000000)
                throw new InvalidDataException("Scale is outside the supported range.");
            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);
            if (String.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination))
                throw new IOException("Choose a new filename; source and existing files are never overwritten.");
            if (!String.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(destination), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Save the scaled copy beside the source so linked textures/materials remain valid.");
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (!Path.GetExtension(destination).Equals(extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Keep the original model file extension.");
            byte[] output;
            if (extension == ".obj")
            {
                var lines = File.ReadAllLines(source);
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = Regex.Match(lines[i], @"^(\s*v\s+)(\S+)(\s+)(\S+)(\s+)(\S+)(.*)$");
                    if (!match.Success) continue;
                    double x = Double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * factor;
                    double y = Double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) * factor;
                    double z = Double.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) * factor;
                    if (new[] { x, y, z }.Any(n => Double.IsNaN(n) || Double.IsInfinity(n) || Math.Abs(n) > Single.MaxValue))
                        throw new InvalidDataException("Invalid OBJ coordinate.");
                    lines[i] = match.Groups[1].Value + x.ToString("R", CultureInfo.InvariantCulture) + " " +
                        y.ToString("R", CultureInfo.InvariantCulture) + " " + z.ToString("R", CultureInfo.InvariantCulture) + match.Groups[7].Value;
                }
                output = Encoding.UTF8.GetBytes(String.Join(Environment.NewLine, lines) + Environment.NewLine);
            }
            else if (extension == ".dae")
            {
                var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 };
                using (var reader = XmlReader.Create(source, settings)) document.Load(reader);
                var scenes = document.SelectNodes("//*[local-name()='library_visual_scenes']/*[local-name()='visual_scene']");
                if (scenes.Count == 0) throw new InvalidDataException("DAE has no visual scene to scale.");
                int number = 0;
                foreach (XmlElement scene in scenes)
                {
                    string ns = scene.NamespaceURI;
                    var nodes = scene.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "node").ToArray();
                    if (nodes.Length == 0) throw new InvalidDataException("DAE visual scene has no root nodes.");
                    string id = RootId + (++number);
                    while (document.SelectNodes("//*[@id]").OfType<XmlElement>().Any(e => e.GetAttribute("id") == id)) id += "_";
                    var wrapper = document.CreateElement("node", ns);
                    wrapper.SetAttribute("id", id);
                    wrapper.SetAttribute("name", "Studio uniform scale");
                    wrapper.SetAttribute("type", "NODE");
                    var scale = document.CreateElement("scale", ns);
                    string value = factor.ToString("R", CultureInfo.InvariantCulture);
                    scale.InnerText = value + " " + value + " " + value;
                    wrapper.AppendChild(scale);
                    scene.InsertBefore(wrapper, nodes[0]);
                    foreach (var node in nodes) wrapper.AppendChild(node);
                }
                using (var memory = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(memory, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
                        document.Save(writer);
                    output = memory.ToArray();
                }
            }
            else throw new InvalidDataException("Scale a DAE or OBJ source model.");
            using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) stream.Write(output, 0, output.Length);
        }
    }
}
