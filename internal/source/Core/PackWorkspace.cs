using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class PackFinding
    {
        public string Level { get; set; }
        public string File { get; set; }
        public string Detail { get; set; }
    }
    internal static class PackWorkspace
    {
        internal static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
        }
        internal static IEnumerable<string> Files(string root)
        {
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            var folders = new Stack<string>();
            folders.Push(Path.GetFullPath(root));
            while (folders.Count > 0)
            {
                string folder = folders.Pop();
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked folders are not supported: " + folder);
                foreach (string file in Directory.GetFiles(folder).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked file: " + file);
                    yield return file;
                }
                foreach (string child in Directory.GetDirectories(folder).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(child);
                    if (name.Equals("MUR_EDITED", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".murums", StringComparison.OrdinalIgnoreCase)) continue;
                    folders.Push(child);
                }
            }
        }
        internal static string Relative(string root, string path)
        {
            root = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path is outside the pack.");
            return path.Substring(root.Length).Replace('\\', '/');
        }
        internal static void Separate(string source, string target)
        {
            source = Path.GetFullPath(source).TrimEnd('\\', '/');
            target = Path.GetFullPath(target).TrimEnd('\\', '/');
            if (target.Equals(source, StringComparison.OrdinalIgnoreCase)
                || target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Einen Ausgabeordner außerhalb der Quelle wählen.", "Choose an output folder outside the source."));
        }
        internal static List<PackFinding> Check(string folder, CancellationToken cancel)
        {
            var findings = new List<PackFinding>();
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int inspected = 0;
            var availableResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var references = new List<KeyValuePair<string, string>>();
            foreach (string file in Files(folder))
            {
                cancel.ThrowIfCancellationRequested();
                string relative = Relative(folder, file), name = Path.GetFileName(file);
                if (names.ContainsKey(name))
                    findings.Add(new PackFinding { Level = "Warning", File = relative, Detail = L.T(
                        "Gleicher Dateiname auch unter ", "Same filename also at ") + names[name] });
                else names.Add(name, relative);
                try
                {
                    long length = new FileInfo(file).Length;
                    if (length == 0) throw new InvalidDataException(L.T("Leere Datei.", "Empty file."));
                    if (!new[] { ".szs", ".arc", ".u8", ".brfnt", ".breff", ".tpl" }.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                    if (length > 128L * 1024 * 1024) throw new NotSupportedException("File exceeds the 128 MiB inspection limit.");
                    byte[] data = File.ReadAllBytes(file);
                    if (new[] { ".szs", ".arc", ".u8" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    {
                        byte[] header = Yaz0.IsYaz0(data) ? Yaz0.Decompress(data) : data;
                        if (header.Length >= 4 && Encoding.ASCII.GetString(header, 0, 4) == "bres")
                            throw new NotSupportedException(L.T(
                                "BRRES-Modellcontainer, kein U8-Archiv. Modell- und Animationsdaten wurden nicht geprüft.",
                                "BRRES model container, not a U8 archive. Model and animation data were not checked."));
                        var archive = new StudioArchiveCopy(file, data);
                        foreach (var entry in archive.Files)
                        {
                            cancel.ThrowIfCancellationRequested();
                            availableResources.Add(Path.GetFileName(entry.Key));
                            try
                            {
                                CheckResource(entry.Key, entry.Value.Data);
                                if (entry.Key.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase))
                                {
                                    var layout = BrlytDocument.FromBytes(entry.Value.Data);
                                    foreach (string resource in layout.Textures.Concat(layout.Fonts).Where(r => !String.IsNullOrWhiteSpace(r)))
                                        references.Add(new KeyValuePair<string, string>(relative + " / " + entry.Key, Path.GetFileName(resource)));
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!(ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException || ex is NotSupportedException)) throw;
                                findings.Add(new PackFinding { Level = ex is NotSupportedException ? "Not checked" : "Error",
                                    File = relative + " / " + entry.Key, Detail = ex.Message });
                            }
                        }
                        foreach (var entry in archive.Files.Where(e => e.Key.EndsWith(".breff", StringComparison.OrdinalIgnoreCase)))
                        {
                            string texture = Path.ChangeExtension(entry.Key, ".breft").Replace('\\', '/');
                            if (!archive.Files.Keys.Any(k => k.Equals(texture, StringComparison.OrdinalIgnoreCase)))
                                findings.Add(new PackFinding { Level = "Warning", File = relative + " / " + entry.Key,
                                    Detail = L.T("Zugehörige BREFT fehlt in diesem Archiv; externe Zuordnung prüfen.", "Matching BREFT is absent in this archive; check external mapping.") });
                        }
                    }
                    else { CheckResource(file, data); availableResources.Add(name); }
                    inspected++;
                }
                catch (NotSupportedException ex) { findings.Add(new PackFinding { Level = "Not checked", File = relative, Detail = ex.Message }); }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException || ex is IndexOutOfRangeException)) throw;
                    findings.Add(new PackFinding { Level = "Error", File = relative, Detail = ex.Message });
                }
            }
            foreach (var reference in references.Where(r => !availableResources.Contains(r.Value)).Distinct())
                findings.Add(new PackFinding { Level = "Warning", File = reference.Key,
                    Detail = L.T("Referenz nicht in den geprüften Archiven gefunden: ", "Reference not found in the checked archives: ")
                        + reference.Value + L.T(". Kann aus gemeinsamen Spiel-/RR-Dateien stammen; Quelle prüfen.", ". May come from shared game/RR files; check the source.") });
            findings.Insert(0, new PackFinding { Level = "Info", File = "", Detail = inspected + L.T(
                " Dateien strukturell geprüft. RR-Laufzeit, gemeinsame Referenzen und Spielbarkeit separat testen.",
                " files checked structurally. Test RR runtime, shared references and playability separately.") });
            return findings;
        }
        static void CheckResource(string name, byte[] data)
        {
            if (name.EndsWith(".brfnt", StringComparison.OrdinalIgnoreCase)) new BrfntFont(data);
            else if (name.EndsWith(".breff", StringComparison.OrdinalIgnoreCase)) new ParticleEffects(data);
            else if (name.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase)) BrlytDocument.FromBytes(data);
            else if (name.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
            {
                TexturePreviewResult image; string error;
                if (!TexturePreview.TryDecode(name, data, 0, out image, out error)) throw new InvalidDataException(error ?? "Invalid TPL.");
                int count = image.ImageCount;
                image.Dispose();
                for (int index = 1; index < count; index++)
                {
                    if (!TexturePreview.TryDecode(name, data, index, out image, out error))
                        throw new InvalidDataException("Image " + (index + 1) + ": " + error);
                    image.Dispose();
                }
            }
        }
        internal static string Restore(string snapshot, string destination)
        {
            snapshot = Path.GetFullPath(snapshot);
            Separate(snapshot, destination);
            string manifestPath = Path.Combine(snapshot, "manifest.json");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            var manifest = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath));
            if (manifest == null || !manifest.ContainsKey("format") || (string)manifest["format"] != "murums-pack-snapshot"
                || !manifest.ContainsKey("version") || Convert.ToInt32(manifest["version"]) != 1 || !manifest.ContainsKey("files"))
                throw new InvalidDataException("Select a Studio snapshot with a valid manifest.");
            string filesRoot = Path.Combine(snapshot, "files");
            var available = new HashSet<string>(Files(filesRoot).Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            var output = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in ((System.Collections.IEnumerable)manifest["files"]).Cast<Dictionary<string, object>>())
            {
                string relative = (string)record["path"];
                if (Path.IsPathRooted(relative)) throw new InvalidDataException("Invalid snapshot path.");
                string file = Path.GetFullPath(Path.Combine(filesRoot, relative));
                Relative(filesRoot, file);
                if (!available.Contains(file)) throw new InvalidDataException("Missing snapshot file: " + relative);
                byte[] data = File.ReadAllBytes(file);
                if (Convert.ToInt64(record["bytes"]) != data.Length || !Hash(data).Equals((string)record["sha256"], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Snapshot checksum mismatch: " + relative);
                if (output.ContainsKey(relative)) throw new InvalidDataException("Duplicate snapshot path.");
                output.Add(relative, data);
            }
            for (var parent = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(destination))); parent != null; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked output folder.");
            CharacterPackage.WriteNew(destination, output);
            return destination;
        }

        internal static string Snapshot(string source, string destination, Action<string, string> prepare = null)
        {
            Separate(source, destination);
            destination = Path.GetFullPath(destination);
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Choose a new snapshot folder.");
            string parent = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(parent);
            for (var p = new DirectoryInfo(parent); p != null; p = p.Parent)
                if ((p.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked output folder.");
            string stage = Path.Combine(parent, ".murums-stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var manifest = new List<object>();
            try
            {
                foreach (string file in Files(source))
                {
                    string relative = Relative(source, file), output = Path.Combine(stage, "files", relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    byte[] data = File.ReadAllBytes(file);
                    using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write)) stream.Write(data, 0, data.Length);
                    manifest.Add(new { path = relative, bytes = data.Length, sha256 = Hash(data) });
                }
                File.WriteAllText(Path.Combine(stage, "manifest.json"), new JavaScriptSerializer().Serialize(new {
                    format = "murums-pack-snapshot", version = 1, utc = DateTime.UtcNow.ToString("o"), files = manifest
                }), new UTF8Encoding(false));
                if (prepare != null) prepare(stage, destination);
                Directory.Move(stage, destination);
                return destination;
            }
            catch
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                throw;
            }
        }
    }
}




