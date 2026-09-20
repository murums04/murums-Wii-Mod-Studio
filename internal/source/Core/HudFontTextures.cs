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
            var label = Regex.Match(name, @"^tt_(time|lap|score)_[EU](?:_lap([1-9][0-9]*))?$", RegexOptions.IgnoreCase);
            if (label.Success)
            {
                string word = label.Groups[1].Value.ToUpperInvariant();
                string lap = label.Groups[2].Value;
                return lap.Length == 0 ? word : lap == "1" ? word + " 1" : lap;
            }
            return null;
        }

        internal static bool IsLabel(string key)
        {
            return Regex.IsMatch(Path.GetFileNameWithoutExtension(key), @"^tt_(time|lap|score)_[EU](?:_lap[1-9][0-9]*)?$", RegexOptions.IgnoreCase);
        }

        internal static Bitmap GenerateLabel(byte[] source, string ttf, string text, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            TexturePreviewResult decoded;
            string error;
            if (!TexturePreview.TryDecode("label.tpl", source, 0, out decoded, out error))
                throw new InvalidDataException(error);
            using (decoded)
            {
                int left = decoded.Width, top = decoded.Height, right = -1, bottom = -1;
                for (int y = 0; y < decoded.Height; y++)
                    for (int x = 0; x < decoded.Width; x++)
                        if (decoded.Bitmap.GetPixel(x, y).A > 24)
                        {
                            left = Math.Min(left, x); top = Math.Min(top, y);
                            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                        }
                if (right < left) throw new InvalidDataException("HUD label has no visible source bounds.");
                using (var face = new FontScriptCollection.Face(ttf))
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    if (text.Any(c => c != ' ' && !face.Coverage.Contains(c)))
                        throw new InvalidDataException("The main TTF does not contain every character in " + text + ".");
                    path.AddString(text, face.Family, (int)face.Style, 64, PointF.Empty, format);
                    RectangleF bounds = path.GetBounds();
                    float margin = 1 + stroke / 2;
                    float width = right - left + 1 - 2 * margin, height = bottom - top + 1 - 2 * margin;
                    if (bounds.Width <= 0 || bounds.Height <= 0 || width <= 0 || height <= 0)
                        throw new InvalidDataException("HUD label is too small for this outline.");
                    float scale = Math.Min(width / bounds.Width, height / bounds.Height);
                    // Unsichtbarer Abstand gehört zum HUD-Layout und darf nicht mit Schrift gefüllt werden.
                    var target = new RectangleF((left + right + 1 - bounds.Width * scale) / 2,
                        (top + bottom + 1 - bounds.Height * scale) / 2, bounds.Width * scale, bounds.Height * scale);
                    return GlyphRasterizer.Render(text, face.Family, face.Style, 64, bounds,
                        new Size(decoded.Width, decoded.Height), target, fill, outline, stroke, hint);
                }
            }
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
                    using (var bitmap = IsLabel(entry.Key) ? GenerateLabel(entry.Value.Data, ttf, Text(entry.Key), fill, outline, stroke, hint)
                        : HudNumberFontForm.Generate(entry.Value.Data, ttf, Text(entry.Key), fill, outline, stroke, hint))
                        entry.Value.Data = TplTextureEditor.ReplaceFirstImage(entry.Value.Data, bitmap, true);
                if (result.Keys.Any(p => Path.GetFileName(p).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Two HUD sources share the same output filename.");
                result.Add(path, archive.Build());
            }
            return result;
        }
    }
}