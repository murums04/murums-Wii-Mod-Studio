using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    // All specialised editors work on an in-memory snapshot and save a separate copy.
    internal sealed class StudioArchiveCopy
    {
        public readonly string Source;
        public readonly byte[] Original;
        public readonly U8Archive Archive;
        public readonly Dictionary<string, ArchiveEntry> Files = new Dictionary<string, ArchiveEntry>(StringComparer.Ordinal);
        public StudioArchiveCopy(string path) : this(path, File.ReadAllBytes(path))
        {
        }

        public StudioArchiveCopy(string path, byte[] snapshot)
        {
            Source = Path.GetFullPath(path);
            Original = (byte[])snapshot.Clone();
            Archive = U8Archive.Load(Original);
            Walk(Archive.Root, "");
        }

        void Walk(ArchiveEntry parent, string prefix)
        {
            foreach (var e in parent.Children)
            {
                string key = prefix + e.Name;
                if (e.IsDirectory)
                    Walk(e, key + "/");
                else
                    Files.Add(key, e);
            }
        }

        public byte[] Build()
        {
            byte[] bytes = Archive.BuildU8();
            return Yaz0.IsYaz0(Original) ? Yaz0.Compress(bytes) : bytes;
        }

        public void Save(string path, params string[] otherSources)
        {
            string full = Path.GetFullPath(path);
            if (string.Equals(full, Source, StringComparison.OrdinalIgnoreCase) || otherSources.Any(p => string.Equals(full, Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Choose a separate output file. The opened sources must remain unchanged.");
            BackupManager.WriteAllBytesSafely(full, Build());
        }
    }
}
