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
            var writes = flat.ToDictionary(file => Path.Combine(folder, file.Key), file => file.Value,
                StringComparer.OrdinalIgnoreCase);
            foreach (string path in writes.Keys) RrMissingFiles.ValidatePath(folder, path);
            BackupManager.WriteBatch(writes, null, Path.Combine(Path.GetFullPath(editedFolder), ".murums_backups"));
            return folder;
        }
    }
}
