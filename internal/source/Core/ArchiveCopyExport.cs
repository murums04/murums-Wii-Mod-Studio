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

        internal static void SaveCopy(string source, byte[] edited, string destination)
        {
            string full = Path.GetFullPath(destination);
            if (full.Equals(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a separate output file.");
            RrMissingFiles.ValidatePath(Path.GetDirectoryName(full), full);
            byte[] data = File.Exists(full) ? Merge(File.ReadAllBytes(source), edited, File.ReadAllBytes(full)) : edited;
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            BackupManager.WriteAllBytesSafely(full, data);
        }

        internal static void Save(IEnumerable<StudioArchiveCopy> archives, string folder)
        {
            var sources = archives.ToArray();
            var writes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                string target = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(source.Source)));
                if (sources.Any(a => a.Source.Equals(target, StringComparison.OrdinalIgnoreCase))) throw new IOException("Choose a separate output folder.");
                RrMissingFiles.ValidatePath(Path.GetFullPath(folder), target);
                var original = new StudioArchiveCopy(source.Source, source.Original);
                if (source.Files.All(p => original.Files.ContainsKey(p.Key) && p.Value.Data.SequenceEqual(original.Files[p.Key].Data))) continue;
                byte[] modified = source.Build();
                writes.Add(target, File.Exists(target) ? Merge(source.Original, modified, File.ReadAllBytes(target)) : modified);
            }
            Directory.CreateDirectory(folder);
            foreach (var write in writes) BackupManager.WriteAllBytesSafely(write.Key, write.Value);
        }
    }
}
