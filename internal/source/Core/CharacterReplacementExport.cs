using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class CharacterReplacementExport
    {
        internal const string FolderName = "Character replacement files";
        internal static string SourceFolder(string name)
        {
            const string slot = @"(?:[1-9]|[1-4][0-9]|50)";
            foreach (var character in CharacterDefinition.All)
            {
                string id = character.Code + "-" + slot;
                if (System.Text.RegularExpressions.Regex.IsMatch(name, "^" + id + @"\.brres$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Character/Driver";
                if (System.Text.RegularExpressions.Regex.IsMatch(name, "^" + id + @"\.tpl$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Character/Map";
                if (System.Text.RegularExpressions.Regex.IsMatch(name, "^" + id + @"-allkart(?:_BT)?\.szs$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Character/AllKart";
                if (System.Text.RegularExpressions.Regex.IsMatch(name, "^" + character.Weight + @"(?:a|b|c|d|e|df)_(?:kart|bike)-" + id + @"\.szs$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Character";
            }
            return null;
        }

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
            BackupManager.WriteBatch(writes, exportCopy: true);
            return folder;
        }
    }

    internal sealed class CharacterExportSnapshot
    {
        readonly Dictionary<string, string> hashes;
        internal readonly DateTime Created = DateTime.Now;
        internal CharacterExportSnapshot(string folder, IEnumerable<string> names)
        {
            hashes = names.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(name => name,
                name => Hash(Path.Combine(folder, name)), StringComparer.OrdinalIgnoreCase);
        }
        static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream));
        }
        internal string[] Differences(string folder)
        {
            return hashes.Where(item => !File.Exists(Path.Combine(folder, item.Key))
                || Hash(Path.Combine(folder, item.Key)) != item.Value).Select(item => item.Key).ToArray();
        }
    }
}
