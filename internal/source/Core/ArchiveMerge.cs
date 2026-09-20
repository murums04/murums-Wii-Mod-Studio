using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed class MergeEntry
    {
        internal string Path;
        internal byte[] Original;
        internal readonly List<byte[]> Candidates = new List<byte[]>();
        internal readonly List<string> Sources = new List<string>();
        internal int Choice;
        internal bool Conflict { get { return Candidates.Count > 1; } }
    }
    internal sealed class ArchiveMerge
    {
        internal readonly StudioArchiveCopy Original;
        internal readonly List<StudioArchiveCopy> Variants = new List<StudioArchiveCopy>();
        internal readonly List<MergeEntry> Entries = new List<MergeEntry>();
        internal ArchiveMerge(string path) { Original = new StudioArchiveCopy(path); }
        static bool Same(byte[] a, byte[] b) { return a == null ? b == null : b != null && a.SequenceEqual(b); }
        internal void Add(IEnumerable<string> paths)
        {
            var added = new List<StudioArchiveCopy>();
            foreach (string path in paths)
            {
                string full = System.IO.Path.GetFullPath(path);
                if (!System.IO.Path.GetFileName(full).Equals(System.IO.Path.GetFileName(Original.Source), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("All variants must have the original archive filename.");
                if (Original.Source.Equals(full, StringComparison.OrdinalIgnoreCase)
                    || Variants.Concat(added).Any(v => v.Source.Equals(full, StringComparison.OrdinalIgnoreCase))) continue;
                added.Add(new StudioArchiveCopy(full));
            }
            Variants.AddRange(added); Recalculate();
        }
        void Recalculate()
        {
            var previous = Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
            Entries.Clear();
            foreach (string path in Original.Files.Keys.Concat(Variants.SelectMany(v => v.Files.Keys)).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
            {
                ArchiveEntry original; Original.Files.TryGetValue(path, out original);
                var entry = new MergeEntry { Path = path, Original = original == null ? null : original.Data };
                foreach (var variant in Variants)
                {
                    ArchiveEntry changed; variant.Files.TryGetValue(path, out changed);
                    byte[] data = changed == null ? null : changed.Data;
                    if (Same(entry.Original, data)) continue;
                    int index = entry.Candidates.FindIndex(c => Same(c, data));
                    if (index < 0) { entry.Candidates.Add(data); entry.Sources.Add(System.IO.Path.GetDirectoryName(variant.Source)); }
                    else entry.Sources[index] += " | " + System.IO.Path.GetDirectoryName(variant.Source);
                }
                if (entry.Candidates.Count == 0) continue;
                entry.Choice = entry.Conflict ? -1 : 1;
                MergeEntry old;
                if (previous.TryGetValue(path, out old) && old.Choice >= 0
                    && old.Candidates.Count == entry.Candidates.Count
                    && old.Candidates.All(c => entry.Candidates.Any(n => Same(c, n))))
                    entry.Choice = old.Choice == 0 ? 0 : 1 + entry.Candidates.FindIndex(c => Same(c, old.Candidates[old.Choice - 1]));
                Entries.Add(entry);
            }
        }
        internal byte[] Build()
        {
            if (Variants.Count == 0) throw new InvalidOperationException("Add edited variants first.");
            if (Entries.Any(e => e.Choice < 0 || e.Choice > e.Candidates.Count)) throw new InvalidOperationException("Resolve every conflict before exporting.");
            var output = new StudioArchiveCopy(Original.Source, Original.Original);
            foreach (var entry in Entries)
            {
                if (entry.Choice == 0) continue;
                byte[] data = entry.Candidates[entry.Choice - 1];
                ArchiveEntry existing;
                if (output.Files.TryGetValue(entry.Path, out existing))
                {
                    if (data == null) { existing.Parent.Children.Remove(existing); output.Files.Remove(entry.Path); }
                    else existing.Data = (byte[])data.Clone();
                }
                else if (data != null)
                {
                    string[] parts = entry.Path.Split('/');
                    var parent = output.Archive.Root;
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        var child = parent.FindChild(parts[i]);
                        if (child == null) { child = new ArchiveEntry(parts[i], true); parent.AddChild(child); }
                        if (!child.IsDirectory) throw new InvalidDataException("File/directory conflict: " + entry.Path);
                        parent = child;
                    }
                    if (parent.FindChild(parts.Last()) != null) throw new InvalidDataException("Path conflict: " + entry.Path);
                    var file = new ArchiveEntry(parts.Last(), false) { Data = (byte[])data.Clone() };
                    parent.AddChild(file); output.Files.Add(entry.Path, file);
                }
            }
            return output.Build();
        }
    }
}
