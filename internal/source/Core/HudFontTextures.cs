using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace murumsWiiModStudio
{
    internal static class HudFontTextures
    {
        internal static string Text(string key)
        {
            string digit = HudNumberFontForm.SetCharacter(key);
            if (digit != null) return digit;
            string name = Path.GetFileNameWithoutExtension(key);
            if (name.Equals("speed", StringComparison.OrdinalIgnoreCase)) return "km/h";
            return null;
        }

        internal static Dictionary<string, byte[]> Generate(string folder, string ttf, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(folder)) return result;
            var paths = Directory.GetFiles(folder, "*.szs").Where(p =>
                Regex.IsMatch(Path.GetFileName(p), @"^Race(?:_[A-Za-z]+)?.szs$", RegexOptions.IgnoreCase)).ToList();
            var assetFolders = new List<string> { folder, Path.Combine(folder, "Assets") };
            if (Path.GetFileName(folder).Equals("UI", StringComparison.OrdinalIgnoreCase))
                assetFolders.Add(Path.Combine(Path.GetDirectoryName(folder), "Assets"));
            foreach (string name in new[] { "RaceAssets.szs", "ReplacedAssets.szs" })
            {
                string path = assetFolders.Select(f => Path.Combine(f, name)).FirstOrDefault(File.Exists);
                if (path != null) paths.Add(path);
            }
            return Generate(paths, ttf, fill, outline, stroke, hint);
        }

        internal static Dictionary<string, byte[]> Generate(IEnumerable<string> paths, string ttf, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!Regex.IsMatch(name, @"^Race(?:_[A-Za-z]+)?$", RegexOptions.IgnoreCase)
                    && !name.Equals("RaceAssets", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("ReplacedAssets", StringComparison.OrdinalIgnoreCase)) continue;
                var archive = new StudioArchiveCopy(path);
                var targets = archive.Files.Where(p => TplTextureEditor.IsTpl(p.Value.Data) && Text(p.Key) != null).ToArray();
                if (targets.Length == 0) continue;
                foreach (var entry in targets)
                    using (var bitmap = HudNumberFontForm.Generate(entry.Value.Data, ttf, Text(entry.Key), fill, outline, stroke, hint))
                        entry.Value.Data = TplTextureEditor.ReplaceFirstImage(entry.Value.Data, bitmap, true);
                if (result.Keys.Any(p => Path.GetFileName(p).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Two HUD sources share the same output filename.");
                result.Add(path, archive.Build());
            }
            return result;
        }
    }
}