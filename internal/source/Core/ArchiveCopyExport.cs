using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class ArchiveCopyExport
    {
        static string Key(string key) { return key.StartsWith("./", StringComparison.Ordinal) ? key.Substring(2) : key; }

        internal static byte[] Merge(byte[] original, byte[] edited, byte[] existing)
        {
            var before = new StudioArchiveCopy("before.szs", original);
            var after = new StudioArchiveCopy("after.szs", edited);
            var target = new StudioArchiveCopy("target.szs", existing);
            var oldFiles = before.Files.ToDictionary(p => Key(p.Key), p => p.Value);
            var newFiles = after.Files.ToDictionary(p => Key(p.Key), p => p.Value);
            foreach (var item in newFiles)
            {
                ArchiveEntry old;
                if (oldFiles.TryGetValue(item.Key, out old) && old.Data.SequenceEqual(item.Value.Data)) continue;
                var root = target.Archive.Root;
                if (root.Children.Count == 1 && root.Children[0].Name == ".") root = root.Children[0];
                string[] parts = item.Key.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    bool directory = i < parts.Length - 1;
                    var child = root.FindChild(parts[i]);
                    if (child == null) { child = new ArchiveEntry(parts[i], directory); root.AddChild(child); }
                    if (child.IsDirectory != directory) throw new IOException("Output resource type differs: " + item.Key);
                    if (!directory) child.Data = (byte[])item.Value.Data.Clone();
                    root = child;
                }
            }
            // Nicht mehr verwendete Texturen dürfen bestehen bleiben; fremde Änderungen bleiben erhalten.
            return target.Build();
        }

        internal static byte[] PrepareCopy(string source, byte[] original, byte[] edited, string destination)
        {
            string full = Path.GetFullPath(destination);
            if (full.Equals(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a separate output file.");
            RrMissingFiles.ValidatePath(Path.GetDirectoryName(full), full);
            if (Directory.Exists(full)) throw new IOException("A folder occupies the output file: " + full);
            return File.Exists(full) ? Merge(original, edited, File.ReadAllBytes(full)) : edited;
        }

        internal static void SaveCopy(string source, byte[] edited, string destination)
        {
            SaveCopy(source, File.ReadAllBytes(source), edited, destination);
        }

        internal static void SaveCopy(string source, byte[] original, byte[] edited, string destination)
        {
            string full = Path.GetFullPath(destination);
            byte[] data = PrepareCopy(source, original, edited, full);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            BackupManager.WriteAllBytesSafely(full, data);
        }

        internal static void Save(IEnumerable<StudioArchiveCopy> archives, string folder)
        {
            var sources = archives.ToArray();
            var writes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                string target = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(source.Source)));
                if (sources.Any(a => a.Source.Equals(target, StringComparison.OrdinalIgnoreCase))) throw new IOException("Choose a separate output folder.");
                RrMissingFiles.ValidatePath(Path.GetFullPath(folder), target);
                var original = new StudioArchiveCopy(source.Source, source.Original);
                if (source.Files.All(p => original.Files.ContainsKey(p.Key) && p.Value.Data.SequenceEqual(original.Files[p.Key].Data))) continue;
                byte[] modified = source.Build();
                byte[] pending;
                if (writes.TryGetValue(target, out pending))
                {
                    if (!owners[target].Equals(source.Source, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Two sources share the same output filename: " + Path.GetFileName(target));
                    writes[target] = Merge(source.Original, modified, pending);
                }
                else
                {
                    writes.Add(target, PrepareCopy(source.Source, source.Original, modified, target));
                    owners.Add(target, source.Source);
                }
            }
            Directory.CreateDirectory(folder);
            BackupManager.WriteBatch(writes);
        }
    }
}
