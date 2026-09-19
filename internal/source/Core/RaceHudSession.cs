using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class HudTexture
    {
        public RaceHudArchive Archive;
        public string Key;
        public override string ToString()
        {
            return ((Archive.Pictures.ContainsKey(Key) || Archive.Generated.ContainsKey(Key)) ? "● " : "") + Path.GetFileName(Key) + "  [" + Path.GetFileName(Archive.Source) + " / " + (Path.GetDirectoryName(Key) ?? "") + "]";
        }
    }

    internal sealed class RaceHudSession
    {
        public readonly List<RaceHudArchive> Archives = new List<RaceHudArchive>();
        public void Open(string path)
        {
            var loaded = new List<RaceHudArchive>();
            loaded.Add(new RaceHudArchive(path));
            string name = Path.GetFileName(path);
            if (!name.Equals("Race.szs", StringComparison.OrdinalIgnoreCase))
            {
                string common = Path.Combine(Path.GetDirectoryName(path), "Race.szs");
                if (File.Exists(common))
                    loaded.Add(new RaceHudArchive(common));
            }

            string assets = HudAssetSource.Find(path);
            if (File.Exists(assets) && !loaded.Any(a => string.Equals(a.Source, Path.GetFullPath(assets), StringComparison.OrdinalIgnoreCase)))
                loaded.Add(new RaceHudArchive(assets));
            string replacements = HudAssetSource.Find(path, "ReplacedAssets.szs");
            if (File.Exists(replacements) && !loaded.Any(a => String.Equals(a.Source, Path.GetFullPath(replacements), StringComparison.OrdinalIgnoreCase)))
                loaded.Add(new RaceHudArchive(replacements));
            Archives.Clear();
            Archives.AddRange(loaded);
        }

        public void Add(string path)
        {
            string full = Path.GetFullPath(path);
            if (Archives.Any(a => string.Equals(a.Source, full, StringComparison.OrdinalIgnoreCase)))
                return;
            if (Archives.Any(a => string.Equals(Path.GetFileName(a.Source), Path.GetFileName(full), StringComparison.OrdinalIgnoreCase)))
                throw new IOException("This archive name is already loaded from another folder. Open a new pack instead.");
            var added = new RaceHudArchive(full);
            string assets = HudAssetSource.Find(full);
            RaceHudArchive supplementary = null;
            if (File.Exists(assets)
                && !Path.GetFullPath(assets).Equals(full, StringComparison.OrdinalIgnoreCase)
                && !Archives.Any(a => Path.GetFileName(a.Source).Equals("RaceAssets.szs", StringComparison.OrdinalIgnoreCase)))
                supplementary = new RaceHudArchive(assets);
            string replacements = HudAssetSource.Find(full, "ReplacedAssets.szs");
            RaceHudArchive replacementArchive = null;
            if (File.Exists(replacements) && !Path.GetFullPath(replacements).Equals(full, StringComparison.OrdinalIgnoreCase)
                && !Archives.Any(a => Path.GetFileName(a.Source).Equals("ReplacedAssets.szs", StringComparison.OrdinalIgnoreCase)))
                replacementArchive = new RaceHudArchive(replacements);
            Archives.Add(added);
            if (replacementArchive != null) Archives.Add(replacementArchive);
            if (supplementary != null)
                Archives.Add(supplementary);
        }

        public List<HudTexture> SearchTextures(int category, string query)
        {
            string term = (query ?? "").Trim();
            if (term.Length == 0)
                return Textures(category);
            return Textures(4).Where(texture =>
                Path.GetFileName(texture.Key).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        public List<HudTexture> Textures(int category)
        {
            var result = new List<HudTexture>();
            foreach (var a in Archives)
            {
                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (category == 2 || (category >= 5 && category <= 10))
                    foreach (var f in a.Files)
                    {
                        string name = Path.GetFileName(f.Key).ToLowerInvariant();
                        if (name.EndsWith(".brlyt") && MatchesLayout(f.Key.ToLowerInvariant(), category))
                            foreach (string texture in BrlytDocument.FromBytes(f.Value.Data).Textures)
                                referenced.Add(Path.GetFileName(texture));
                    }

                foreach (var f in a.Files)
                {
                    if (!TplTextureEditor.IsTpl(f.Value.Data))
                    {
                        if (category == 3 && a.Generated.ContainsKey(f.Key))
                            result.Add(new HudTexture { Archive = a, Key = f.Key });
                        continue;
                    }

                    string n = f.Key.ToLowerInvariant();
                    bool selected = a.Pictures.ContainsKey(f.Key) || a.Generated.ContainsKey(f.Key);
                    if (category == 0 && !n.Contains("position"))
                        continue;
                    if (category == 1 && !n.Contains("number") && !n.Contains("lap") && !n.Contains("time") && !n.Contains("score") && !n.Contains("slash"))
                        continue;
                    if (category == 2 && !n.Contains("item") && !n.Contains("map") && !referenced.Contains(Path.GetFileName(f.Key)))
                        continue;
                    if (category == 3 && !selected)
                        continue;
                    if (category == 11 && !Path.GetFileName(f.Key).StartsWith("tt_item_box_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    bool namedSupplement = category == 10 && Path.GetFileNameWithoutExtension(f.Key).Equals("speed", StringComparison.OrdinalIgnoreCase)
                        || category == 8 && Path.GetFileName(f.Key).StartsWith("basic_", StringComparison.OrdinalIgnoreCase);
                    if (category >= 5 && category <= 10 && !namedSupplement && !referenced.Contains(Path.GetFileName(f.Key)))
                        continue;
                    result.Add(new HudTexture { Archive = a, Key = f.Key });
                }
            }

            return result.OrderBy(t => Path.GetFileName(t.Key)).ThenBy(t => t.Archive.Source).ToList();
        }

        static bool MatchesLayout(string name, int category)
        {
            if (category == 2)
                return name.Contains("item") || name.Contains("map") || name.Contains("chase_icon");
            if (category == 5)
                return name.Contains("count") || name.Contains("start") || name.Contains("finish") || name.Contains("goal") || name.EndsWith("/go.brlyt") || name.Contains("tt_go") || name.Contains("race_message") || name.Contains("congratulations");
            if (category == 6)
                return name.Contains("name") || name.Contains("player") || name.Contains("warning") || name.Contains("alarm") || name.Contains("live_message");
            if (category == 9)
                return name.Contains("map") || name.Contains("chase_icon");
            if (category == 10)
                return name.Contains("speed");
            if (category == 8)
                return name.Contains("inputviewer") || name.Contains("input_viewer");
            if (category == 7)
                return name.Contains("result");
            return false;
        }

        public int SelectedCount
        {
            get
            {
                return Archives.Sum(a => a.Pictures.Count + a.Generated.Count);
            }
        }

        public bool HasShadows
        {
            get
            {
                return Archives.Any(a => a.ShadowCount > 0);
            }
        }

        public List<string> Save(string folder, bool hide)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new IOException("Choose an output folder.");
            var pending = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in Archives)
            {
                if (a.Pictures.Count == 0 && a.Generated.Count == 0 && !(hide && a.ShadowCount > 0))
                    continue;
                string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(a.Source)));
                if (Archives.Any(s => string.Equals(dest, s.Source, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("Choose a separate output folder. Source archives must remain unchanged.");
                pending.Add(dest, a.Build(hide));
            }

            if (pending.Count == 0)
                throw new InvalidOperationException("Select replacement pictures or enable the shadow switch first.");
            Directory.CreateDirectory(folder);
            foreach (var p in pending)
                BackupManager.WriteAllBytesSafely(p.Key, p.Value);
            return pending.Keys.ToList();
        }
    }
}
