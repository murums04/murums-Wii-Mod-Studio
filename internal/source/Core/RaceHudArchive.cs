using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class RaceHudArchive
    {
        public readonly string Source;
        readonly byte[] original;
        public readonly Dictionary<string, ArchiveEntry> Files = new Dictionary<string, ArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, byte[]> Generated = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Pictures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public RaceHudArchive(string path)
        {
            Source = Path.GetFullPath(path);
            original = File.ReadAllBytes(Source);
            Walk(U8Archive.Load(original).Root, "", Files);
        }

        static void Walk(ArchiveEntry e, string path, Dictionary<string, ArchiveEntry> result)
        {
            string p = path.Length == 0 ? e.Name : path + "/" + e.Name;
            if (e.IsDirectory)
            {
                foreach (var c in e.Children)
                    Walk(c, p, result);
            }
            else
                result[p.TrimStart('/')] = e;
        }

        public static string PictureKey(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path).Trim();
            if (n.EndsWith(".tpl-0", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 6);
            return n;
        }

        public int MatchFolder(string folder)
        {
            var matches = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp")
                    continue;
                string key = PictureKey(p);
                if (!matches.ContainsKey(key))
                    matches[key] = new List<string>();
                matches[key].Add(p);
            }

            int count = 0;
            foreach (var f in Files)
            {
                if (!TplTextureEditor.IsTpl(f.Value.Data) || !TplTextureEditor.CanReplaceImage(f.Value.Data, 0))
                    continue;
                List<string> candidates;
                if (matches.TryGetValue(Path.GetFileNameWithoutExtension(f.Key), out candidates) && candidates.Count == 1)
                {
                    Pictures[f.Key] = candidates[0];
                    Generated.Remove(f.Key);
                    count++;
                }
            }

            return count;
        }

        public int ShadowCount
        {
            get
            {
                int n = 0;
                foreach (var f in Files)
                    if (IsPositionLayout(f.Key))
                        foreach (var p in BrlytDocument.FromBytes(f.Value.Data).Panes)
                            if (p.Name == "position_sha")
                                n++;
                return n;
            }
        }

        static bool IsPositionLayout(string p)
        {
            string n = Path.GetFileName(p);
            return n == "game_image_position.brlyt" || n == "game_image_position_multi.brlyt";
        }

        public byte[] Build(bool hideShadow)
        {
            var archive = U8Archive.Load(original);
            var files = new Dictionary<string, ArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            Walk(archive.Root, "", files);
            foreach (var p in Generated)
                files[p.Key].Data = (byte[])p.Value.Clone();
            foreach (var p in Pictures)
            {
                ArchiveEntry target;
                if (!files.TryGetValue(p.Key, out target))
                    throw new InvalidDataException("Texture not found: " + p.Key);
                using (Bitmap b = TplTextureEditor.LoadSourceBitmap(p.Value))
                    target.Data = TplTextureEditor.ReplaceFirstImage(target.Data, b, true);
            }

            if (hideShadow)
                foreach (var f in files)
                    if (IsPositionLayout(f.Key))
                    {
                        var d = BrlytDocument.FromBytes(f.Value.Data);
                        foreach (var p in d.Panes)
                            if (p.Name == "position_sha")
                            {
                                p.Alpha = 0;
                                d.ApplyPane(p);
                            }

                        f.Value.Data = d.Data;
                    }

            byte[] data = archive.BuildU8();
            return Yaz0.IsYaz0(original) ? Yaz0.Compress(data) : data;
        }

        public string Save(string folder, bool hideShadow)
        {
            string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(Source)));
            if (string.Equals(dest, Source, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a separate output folder. The source archive is preserved.");
            byte[] data = Build(hideShadow);
            Directory.CreateDirectory(folder);
            BackupManager.WriteAllBytesSafely(dest, data);
            return dest;
        }
    }
}
