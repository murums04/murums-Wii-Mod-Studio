using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class CharacterReplacementExport
    {
        internal const string FolderName = "Character replacement files";
        internal static string Write(string editedFolder, IDictionary<string, byte[]> files)
        {
            if (files == null || files.Count == 0) throw new ArgumentException("No character replacement files.");
            string folder = Path.GetFullPath(Path.Combine(editedFolder, FolderName));
            var flat = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                string name = Path.GetFileName(file.Key.Replace('/', Path.DirectorySeparatorChar));
                if (String.IsNullOrEmpty(name) || !new[] { ".szs", ".brres", ".tpl" }.Contains(Path.GetExtension(name).ToLowerInvariant()))
                    throw new InvalidDataException("Not a character game file: " + name);
                if (flat.ContainsKey(name)) throw new InvalidDataException("Duplicate character filename: " + name);
                flat.Add(name, file.Value);
            }
            Directory.CreateDirectory(folder);
            var before = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var written = new List<string>();
            string backupFolder = Path.Combine(Path.GetFullPath(editedFolder), ".murums_backups");
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N");
            try
            {
                foreach (var file in flat)
                {
                    string path = Path.Combine(folder, file.Key);
                    RrMissingFiles.ValidatePath(folder, path);
                    before[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                }
                foreach (var file in flat)
                {
                    string path = Path.Combine(folder, file.Key);
                    if (before[path] != null && before[path].SequenceEqual(file.Value)) continue;
                    string temporary = Path.Combine(folder, ".mur-character-" + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        File.WriteAllBytes(temporary, file.Value);
                        if (before[path] == null) File.Move(temporary, path);
                        else
                        {
                            Directory.CreateDirectory(backupFolder);
                            File.Replace(temporary, path, Path.Combine(backupFolder, file.Key + "." + stamp + ".bak"));
                        }
                        written.Add(path);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
            }
            catch
            {
                foreach (string path in written.AsEnumerable().Reverse())
                    if (before[path] == null) File.Delete(path); else File.WriteAllBytes(path, before[path]);
                throw;
            }
            return folder;
        }
    }
}
