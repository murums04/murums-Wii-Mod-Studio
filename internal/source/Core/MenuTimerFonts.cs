using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class MenuTimerFonts
    {
        internal static Dictionary<string, byte[]> Generate(string folder, string ttf, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            return Generate(Directory.Exists(folder) ? Directory.GetFiles(folder, "*.szs") : new string[0], ttf, fill, outline, stroke, hint);
        }

        internal static Dictionary<string, byte[]> Generate(IEnumerable<string> paths, string ttf, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var stems = new[] { "Title", "MenuSingle", "MenuMulti", "Globe", "Channel", "Award" };
            foreach (string path in paths)
            {
                string stem = Path.GetFileNameWithoutExtension(path).Split('_')[0];
                if (!stems.Contains(stem, StringComparer.OrdinalIgnoreCase)) continue;
                var archive = new StudioArchiveCopy(path);
                var targets = archive.Files.Where(p => p.Key.Replace("./", "").StartsWith("control/timg/", StringComparison.OrdinalIgnoreCase)
                    && HudNumberFontForm.SetCharacter(p.Key) != null).ToArray();
                if (targets.Length == 0) continue;
                foreach (var entry in targets)
                {
                    string text = HudNumberFontForm.SetCharacter(entry.Key);
                    using (var bitmap = HudNumberFontForm.Generate(entry.Value.Data, ttf, text, fill, outline, stroke, hint))
                        entry.Value.Data = TplTextureEditor.ReplaceFirstImage(entry.Value.Data, bitmap, true);
                }
                result.Add(path, archive.Build());
            }
            return result;
        }
    }
}