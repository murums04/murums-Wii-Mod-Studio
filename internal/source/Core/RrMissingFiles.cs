using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace murumsWiiModStudio
{
    internal sealed class RrMissingFile
    {
        internal string Source, RelativePath, Destination;
        internal long Size;
        internal bool Exists;
        public override string ToString() { return RelativePath; }
    }

    internal static class RrMissingFiles
    {
        internal static bool Matches(string name, string patterns)
        {
            return patterns.Split(';').Any(pattern => Regex.IsMatch(name,
                "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase));
        }

        internal static string InferRegion(string pack)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string stem in new[] { "Race", "Title", "Common" })
                foreach (string region in new[] { "E", "U", "J" })
                    if (File.Exists(Path.Combine(pack, stem + "_" + region + ".szs"))) found.Add(region);
            return found.Count == 1 ? found.First() : null;
        }

        internal static List<RrMissingFile> Plan(string rrRoot, string pack, string patterns, string region,
            CharacterDefinition character = null, int slot = 0)
        {
            rrRoot = Path.GetFullPath(rrRoot);
            pack = Path.GetFullPath(pack);
            if (!Directory.Exists(pack)) throw new DirectoryNotFoundException(pack);
            if (Inside(pack, rrRoot) || Inside(rrRoot, pack))
                throw new IOException(L.T("RR-Installation und Custom Pack müssen getrennte Ordner sein.", "RR installation and custom pack must be separate folders."));
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (character == null)
            {
                foreach (var entry in RetroRewindSource.Catalog(rrRoot))
                {
                    string name = CustomPacks.RegionalFileName(entry.Key, region);
                    bool matches = Matches(name, patterns) || region == null && new[] { "E", "U", "J" }
                        .Any(candidate => Matches(CustomPacks.RegionalFileName(entry.Key, candidate), patterns));
                    if (matches) sources.Add(name, entry.Value);
                }
            }
            else
            {
                if (slot < 1 || slot > 50) throw new InvalidDataException("Choose an existing RR variant.");
                var paths = CharacterPackage.Missing(character, slot, new CharacterAsset[0]);
                string suffix = character.Code + "-" + slot;
                paths.Add("Character/Map/" + suffix + ".tpl");
                paths.Add("Character/AllKart/" + suffix + "-allkart.szs");
                paths.Add("Character/AllKart/" + suffix + "-allkart_BT.szs");
                foreach (string relative in paths)
                {
                    string source = Path.Combine(rrRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(source)) sources.Add(relative, source);
                }
                foreach (string name in new[] { "UIAssets.szs", "RaceAssets.szs" })
                {
                    string source = Path.Combine(rrRoot, "Assets", name);
                    if (File.Exists(source)) sources.Add(name, source);
                }
            }
            return sources.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => {
                string destination = Path.Combine(pack, p.Key.Replace('/', Path.DirectorySeparatorChar));
                ValidatePath(pack, destination);
                return new RrMissingFile { Source = p.Value, RelativePath = p.Key, Destination = destination,
                    Size = new FileInfo(p.Value).Length, Exists = File.Exists(destination) };
            }).ToList();
        }

        internal static string[] CopyMissing(string pack, IEnumerable<RrMissingFile> selection)
        {
            pack = Path.GetFullPath(pack);
            var files = selection.ToArray();
            if (files.Select(f => f.Destination).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
                throw new IOException("Duplicate destination.");
            foreach (var file in files)
            {
                ValidatePath(pack, file.Destination);
                if (!File.Exists(file.Source)) throw new FileNotFoundException(file.Source);
                if (File.Exists(file.Destination)) continue;
                if (Directory.Exists(file.Destination)) throw new IOException("A folder already occupies: " + file.RelativePath);
            }
            string stage = Path.Combine(pack, ".studio-rr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var copied = new List<RrMissingFile>();
            try
            {
                for (int i = 0; i < files.Length; i++)
                    if (!File.Exists(files[i].Destination))
                    {
                        string temporary = Path.Combine(stage, i.ToString());
                        File.Copy(files[i].Source, temporary, false);
                        if (!Hash(temporary).SequenceEqual(Hash(files[i].Source))) throw new IOException("Copy verification failed.");
                    }
                for (int i = 0; i < files.Length; i++)
                {
                    string temporary = Path.Combine(stage, i.ToString());
                    if (!File.Exists(temporary)) continue;
                    ValidatePath(pack, files[i].Destination);
                    if (File.Exists(files[i].Destination)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(files[i].Destination));
                    File.Move(temporary, files[i].Destination);
                    copied.Add(files[i]);
                }
                return files.Where(f => File.Exists(f.Destination)).Select(f => f.Destination).ToArray();
            }
            catch
            {
                foreach (var file in copied)
                    if (File.Exists(file.Destination) && Hash(file.Destination).SequenceEqual(Hash(file.Source)))
                        File.Delete(file.Destination);
                throw;
            }
            finally
            {
                foreach (string file in Directory.GetFiles(stage)) File.Delete(file);
                Directory.Delete(stage);
            }
        }

        static byte[] Hash(string path)
        {
            using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path)) return hash.ComputeHash(stream);
        }
        static bool Inside(string path, string root)
        {
            return path.Equals(root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        internal static void ValidatePath(string pack, string destination)
        {
            if (!Inside(Path.GetFullPath(destination), pack) || Path.GetFullPath(destination).Equals(pack, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Destination is outside the pack.");
            var current = new DirectoryInfo(Path.GetDirectoryName(destination));
            while (current != null)
            {
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked destination folder.");
                current = current.Parent;
            }
            if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked destination file.");
        }
    }
}
