using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class CharacterVehicleStyle
    {
        internal static string Key(CharacterAsset asset)
        {
            return Path.GetFileNameWithoutExtension(asset.Target).Split('-')[0];
        }

        internal static CharacterImages.VehicleTexture Body(CharacterAsset asset)
        {
            return CharacterImages.Textures(asset).Where(t => !t.Name.ToLowerInvariant().Contains("tire"))
                .OrderByDescending(t => { var info = TplTextureEditor.GetImageInfo(t.Tpl, 0); return info.Width * info.Height; })
                .FirstOrDefault();
        }

        internal static Rectangle[] LogoRegions(string key, CharacterImages.VehicleTexture texture)
        {
            if (texture == null) return new Rectangle[0];
            var info = TplTextureEditor.GetImageInfo(texture.Tpl, 0);
            if (info.Width != 256 || info.Height != 128) return new Rectangle[0];
            // Standardatlanten; abweichende Fahrzeuge lassen sich in der Texturansicht markieren.
            if (key.EndsWith("df_kart")) return new[] { new Rectangle(16, 24, 64, 24), new Rectangle(32, 88, 32, 24) };
            if (key.EndsWith("df_bike")) return new[] { new Rectangle(152, 16, 40, 40), new Rectangle(160, 64, 32, 40) };
            switch (key)
            {
                case "la_kart": return new[] { new Rectangle(16, 16, 32, 32), new Rectangle(96, 88, 32, 32) };
                case "la_bike": return new[] { new Rectangle(24, 8, 32, 32) };
                case "lb_bike": return new[] { new Rectangle(144, 56, 48, 48) };
                case "lc_kart": return new[] { new Rectangle(128, 16, 32, 40) };
                case "lc_bike": return new[] { new Rectangle(0, 0, 32, 32) };
                case "ld_kart": return new[] { new Rectangle(64, 24, 32, 40) };
                case "ld_bike": return new[] { new Rectangle(16, 16, 56, 56), new Rectangle(160, 64, 64, 64) };
                case "le_kart": return new[] { new Rectangle(64, 48, 48, 48) };
                case "le_bike": return new[] { new Rectangle(120, 72, 56, 56) };
                case "ma_kart": return new[] { new Rectangle(0, 24, 32, 24) };
                case "ma_bike": return new[] { new Rectangle(16, 0, 40, 40), new Rectangle(152, 24, 40, 40) };
                case "mc_bike": return new[] { new Rectangle(168, 32, 40, 40) };
                case "me_kart": return new[] { new Rectangle(64, 24, 32, 40) };
                case "me_bike": return new[] { new Rectangle(0, 0, 32, 32) };
                default: return new Rectangle[0];
            }
        }

        internal static Color PaintColor(Color original, Color target)
        {
            float high = Math.Max(original.R, Math.Max(original.G, original.B));
            float low = Math.Min(original.R, Math.Min(original.G, original.B));
            if (original.A < 16 || high < 16 || (high - low) / high < .18f) return original;
            float targetHigh = Math.Max(target.R, Math.Max(target.G, target.B));
            float targetLow = Math.Min(target.R, Math.Min(target.G, target.B));
            double value = high / 255.0 * (.25 + .75 * targetHigh / 255.0);
            double saturation = targetHigh == 0 ? 0 : (targetHigh - targetLow) / targetHigh;
            double hue = target.GetHue() / 60.0, chroma = value * saturation;
            double x = chroma * (1 - Math.Abs(hue % 2 - 1)), m = value - chroma;
            double r = 0, g = 0, b = 0;
            if (hue < 1) { r = chroma; g = x; }
            else if (hue < 2) { r = x; g = chroma; }
            else if (hue < 3) { g = chroma; b = x; }
            else if (hue < 4) { g = x; b = chroma; }
            else if (hue < 5) { r = x; b = chroma; }
            else { r = chroma; b = x; }
            return Color.FromArgb(original.A, (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        internal static CharacterAsset Paint(CharacterAsset source, Color color, Rectangle[] protectedRegions, CharacterAsset baseline = null)
        {
            var texture = Body(source);
            if (texture == null) throw new InvalidDataException(L.T("Keine bearbeitbare Fahrzeugtextur gefunden.", "No editable vehicle texture found."));
            protectedRegions = protectedRegions.Concat((source.LogoRegions ?? new CharacterLogoRegion[0]).Where(r => r.Texture == texture.Name).Select(r => r.Bounds)).ToArray();
            byte[] paintBase = source.PaintBase ?? Body(baseline ?? source).Tpl;
            using (var image = CharacterImages.Decode(texture.Tpl))
            using (var original = CharacterImages.Decode(paintBase))
            {
                if (original.Size != image.Size) throw new InvalidDataException("Vehicle texture sizes differ.");
                for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                        if (!protectedRegions.Any(r => r.Contains(x, y))) image.SetPixel(x, y, PaintColor(original.GetPixel(x, y), color));
                var result = CharacterImages.ReplaceTexture(source, texture, image, new Rectangle(Point.Empty, image.Size), protectedRegions);
                result.PaintBase = paintBase;
                return result;
            }
        }

        internal static CharacterAsset Logo(CharacterAsset source, Bitmap logo, Rectangle[] regions, string textureName = null)
        {
            if (regions.Length == 0) throw new InvalidDataException(L.T("Für dieses Fahrzeug die Logostelle unter Texturen markieren.", "Mark the logo area under Textures for this vehicle."));
            var result = source;
            foreach (var region in regions)
            {
                var texture = textureName == null ? Body(result) : CharacterImages.Textures(result).Single(t => t.Name == textureName);
                result = CharacterImages.ReplaceEmblem(result, texture, region, logo);
            }
            string name = textureName ?? Body(source).Name;
            var retained = (source.LogoRegions ?? new CharacterLogoRegion[0]).Where(r => r.Texture != name || !regions.Any(b => b.IntersectsWith(r.Bounds)));
            result.LogoRegions = retained.Concat(regions.Select(r => new CharacterLogoRegion { Texture = name, X = r.X, Y = r.Y, Width = r.Width, Height = r.Height })).ToArray();
            return result;
        }

        internal static CharacterAsset SyncMenu(CharacterAsset menu, CharacterAsset before, CharacterAsset after)
        {
            string member = CharacterImages.Members(menu).Keys.SingleOrDefault(k => Path.GetFileNameWithoutExtension(k) == Key(after));
            if (member == null) throw new InvalidDataException(L.T("Fahrzeug fehlt im Menüarchiv: ", "Vehicle missing from menu archive: ") + Key(after));
            var changed = CharacterImages.Textures(after);
            var previous = CharacterImages.Textures(before);
            var result = menu;
            foreach (var texture in changed)
            {
                var old = previous.SingleOrDefault(t => t.Name == texture.Name);
                if (old != null && old.Tpl.SequenceEqual(texture.Tpl)) continue;
                var destination = CharacterImages.Textures(result, Path.GetFileName(member)).SingleOrDefault(t => t.Name == texture.Name);
                if (destination == null) throw new InvalidDataException(L.T("Menütextur fehlt: ", "Menu texture missing: ") + texture.Name);
                using (var image = CharacterImages.Decode(texture.Tpl))
                {
                    var info = TplTextureEditor.GetImageInfo(destination.Tpl, 0);
                    if (info.Width != image.Width || info.Height != image.Height) throw new InvalidDataException("Menu/race texture sizes differ: " + texture.Name);
                    result = CharacterImages.ReplaceTexture(result, destination, image, new Rectangle(Point.Empty, image.Size));
                }
            }
            result = CharacterVehicleWindows.SyncMenu(result, before, after, member);
            return CharacterVehicleTires.SyncMenu(result, before, after, member);
        }

        internal static Color[] Palette(CharacterModelImport model)
        {
            if (model == null) return new Color[0];
            var colors = new List<Color>(model.FaceColors);
            if (model.Rig != null)
                foreach (string name in model.Rig.Materials.Select(m => m.Texture).Where(n => n != null).Distinct().Take(64))
                {
                    string path = Path.Combine(model.Rig.Folder, name);
                    if (!File.Exists(path)) continue;
                    using (var source = TplTextureEditor.LoadSourceBitmap(path))
                    using (var sample = new Bitmap(source, 32, 32))
                        for (int y = 0; y < 32; y++)
                            for (int x = 0; x < 32; x++) colors.Add(sample.GetPixel(x, y));
                }
            return colors.Where(c => c.A > 64 && c.GetSaturation() > .25 && c.GetBrightness() > .1 && c.GetBrightness() < .9)
                .GroupBy(c => (c.R / 32 << 8) | (c.G / 32 << 4) | c.B / 32).OrderByDescending(g => g.Count()).Take(6)
                .Select(g => Color.FromArgb((int)g.Average(c => c.R), (int)g.Average(c => c.G), (int)g.Average(c => c.B))).ToArray();
        }
    }
}
