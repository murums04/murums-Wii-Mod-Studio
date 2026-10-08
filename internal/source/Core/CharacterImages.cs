using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class CharacterImages
    {
        internal sealed class VehicleTexture
        {
            internal string Member, Name;
            internal int Offset, Length;
            internal byte[] Tpl;
            public override string ToString() { return Name + " · " + Path.GetFileName(Member); }
        }

        internal static Bitmap Fit(Bitmap source, int width, int height)
        {
            var result = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Min((float)width / source.Width, (float)height / source.Height);
                float w = source.Width * scale, h = source.Height * scale;
                graphics.DrawImage(source, (width - w) / 2, (height - h) / 2, w, h);
            }
            return result;
        }

        internal static CharacterAsset Minimap(Bitmap image, CharacterDefinition character, int slot)
        {
            if (slot < 1 || slot > 50) throw new ArgumentException("Invalid RR variant.");
            using (var fitted = Fit(image, 32, 32))
            {
                string path = "Character/Map/" + character.Code + "-" + slot + ".tpl";
                return new CharacterAsset { Source = path, Target = path, Role = "Minimap icon", Data = Brlan.TplEncoder.Encode(fitted, Brlan.TplPixelFormat.RGB5A3) };
            }
        }

        internal static List<VehicleTexture> Textures(CharacterAsset asset)
        {
            return Textures(asset, "kart_model.brres");
        }

        internal static Dictionary<string, byte[]> Members(CharacterAsset asset)
        {
            if (asset.Target.EndsWith(".brres", StringComparison.OrdinalIgnoreCase))
                return new Dictionary<string, byte[]> { { "", asset.Data } };
            return new StudioArchiveCopy(asset.Source, asset.Data).Files
                .ToDictionary(p => p.Key, p => p.Value.Data, StringComparer.OrdinalIgnoreCase);
        }

        internal static List<VehicleTexture> Textures(CharacterAsset asset, string memberName)
        {
            var members = Members(asset);
            var result = new List<VehicleTexture>();
            foreach (var member in members.Where(f => f.Key == "" || Path.GetFileName(f.Key).Equals(memberName, StringComparison.OrdinalIgnoreCase)))
            {
                var data = member.Value;
                if (data.Length < 16 || Encoding.ASCII.GetString(data, 0, 4) != "bres") throw new InvalidDataException("Invalid vehicle BRRES.");
                int root = (data[12] << 8) | data[13];
                foreach (var group in BrresModelVisibility.Dictionary(data, root + 8).Where(g => g.Name == "Textures(NW4R)"))
                    foreach (var texture in BrresModelVisibility.Dictionary(data, group.Data))
                    {
                        int p = texture.Data;
                        if (p < 0 || p + 48 > data.Length || Encoding.ASCII.GetString(data, p, 4) != "TEX0") throw new InvalidDataException("Invalid vehicle texture.");
                        int format = checked((int)BigEndian.ReadUInt32(data, p + 32));
                        int levels = checked((int)BigEndian.ReadUInt32(data, p + 36));
                        if (format == 8 || format == 9 || format == 10) continue;
                        if (levels < 1 || levels > 16) throw new InvalidDataException("Invalid vehicle mipmaps.");
                        int width = (data[p + 28] << 8) | data[p + 29], height = (data[p + 30] << 8) | data[p + 31];
                        int offset = checked(p + (int)BigEndian.ReadUInt32(data, p + 16));
                        int length = 0;
                        for (int i = 0; i < levels; i++) length = checked(length + TplTextureEditor.GetBaseLevelPayloadLength(Math.Max(1, width >> i), Math.Max(1, height >> i), format));
                        if (offset < p || length <= 0 || (long)offset + length > data.Length || (long)offset + length > p + (long)BigEndian.ReadUInt32(data, p + 4)) throw new InvalidDataException("Vehicle texture exceeds its resource.");
                        byte[] tpl = new byte[64 + length];
                        BigEndian.WriteUInt32(tpl, 0, 0x20af30); BigEndian.WriteUInt32(tpl, 4, 1); BigEndian.WriteUInt32(tpl, 8, 12); BigEndian.WriteUInt32(tpl, 12, 20);
                        tpl[20] = (byte)(height >> 8); tpl[21] = (byte)height; tpl[22] = (byte)(width >> 8); tpl[23] = (byte)width;
                        BigEndian.WriteUInt32(tpl, 24, (uint)format); BigEndian.WriteUInt32(tpl, 28, 64);
                        BigEndian.WriteUInt32(tpl, 40, 1); BigEndian.WriteUInt32(tpl, 44, 1); tpl[54] = (byte)(levels - 1);
                        Buffer.BlockCopy(data, offset, tpl, 64, length);
                        TplTextureEditor.GetImageInfo(tpl, 0);
                        result.Add(new VehicleTexture { Name = texture.Name, Member = member.Key, Offset = offset, Length = length, Tpl = tpl });
                    }
            }
            return result.OrderBy(t => t.Name == "body" ? 0 : 1).ThenBy(t => t.Name).ToList();
        }

        internal static Bitmap Decode(byte[] data)
        {
            TexturePreviewResult result; string error;
            if (!TexturePreview.TryDecode("image.tpl", data, 0, out result, out error)) throw new InvalidDataException(error);
            using (result) return new Bitmap(result.Bitmap);
        }

        internal static CharacterAsset ReplaceEmblem(CharacterAsset source, VehicleTexture target, Rectangle region, Bitmap image)
        {
            using (var original = Decode(target.Tpl))
            using (var fitted = Fit(image, region.Width, region.Height))
            {
                if (region.Width < 2 || region.Height < 2 || !new Rectangle(Point.Empty, original.Size).Contains(region))
                    throw new InvalidDataException(L.T("Das vorhandene Logo im Texturbild markieren.", "Mark the existing logo in the texture."));
                using (var graphics = Graphics.FromImage(original))
                {
                    var border = new List<Color>();
                    for (int x = region.Left; x < region.Right; x++) { border.Add(original.GetPixel(x, region.Top)); border.Add(original.GetPixel(x, region.Bottom - 1)); }
                    for (int y = region.Top; y < region.Bottom; y++) { border.Add(original.GetPixel(region.Left, y)); border.Add(original.GetPixel(region.Right - 1, y)); }
                    var background = border.GroupBy(c => (c.R / 32 << 8) | (c.G / 32 << 4) | c.B / 32).OrderByDescending(g => g.Count()).First();
                    var color = Color.FromArgb(background.Max(c => c.A), (int)background.Average(c => c.R), (int)background.Average(c => c.G), (int)background.Average(c => c.B));
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    using (var brush = new SolidBrush(color)) graphics.FillRectangle(brush, region);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    graphics.DrawImageUnscaled(fitted, region.Location);
                }
                return ReplaceTexture(source, target, original, region);
            }
        }

        internal static CharacterAsset ReplaceTexture(CharacterAsset source, VehicleTexture target, Bitmap image, Rectangle region, Rectangle[] protectedRegions = null)
        {
            var current = Textures(source, Path.GetFileName(target.Member)).SingleOrDefault(t => t.Member == target.Member && t.Name == target.Name);
            if (current == null) throw new InvalidDataException("Texture is no longer present.");
            var info = TplTextureEditor.GetImageInfo(current.Tpl, 0);
            if (image.Width != info.Width || image.Height != info.Height || region.Width < 1 || region.Height < 1
                || !new Rectangle(0, 0, info.Width, info.Height).Contains(region)) throw new InvalidDataException("Invalid texture region.");
            byte[] replaced = TplTextureEditor.ReplaceFirstImage(current.Tpl, image, false);
            var members = Members(source);
            byte[] data = (byte[])members[current.Member].Clone();
            int blockWidth = info.Format == 0 || info.Format == 1 || info.Format == 2 || info.Format == 14 ? 8 : 4;
            int blockHeight = info.Format == 0 || info.Format == 14 ? 8 : 4;
            int blockBytes = info.Format == 6 ? 64 : 32;
            int sourceOffset = 64, targetOffset = current.Offset;
            for (int level = 0; level <= info.MaxLod; level++)
            {
                int width = Math.Max(1, info.Width >> level), height = Math.Max(1, info.Height >> level);
                int blocks = (width + blockWidth - 1) / blockWidth;
                int divisor = 1 << level;
                int left = region.Left / divisor / blockWidth, top = region.Top / divisor / blockHeight;
                int right = ((region.Right + divisor - 1) / divisor + blockWidth - 1) / blockWidth;
                int bottom = ((region.Bottom + divisor - 1) / divisor + blockHeight - 1) / blockHeight;
                for (int y = top; y < bottom; y++)
                    for (int x = left; x < right; x++)
                    {
                        var block = new Rectangle(x * blockWidth * divisor, y * blockHeight * divisor, blockWidth * divisor, blockHeight * divisor);
                        if (protectedRegions != null && protectedRegions.Any(r => r.IntersectsWith(block))) continue;
                        int offset = (y * blocks + x) * blockBytes;
                        Buffer.BlockCopy(replaced, sourceOffset + offset, data, targetOffset + offset, blockBytes);
                    }
                int bytes = TplTextureEditor.GetBaseLevelPayloadLength(width, height, info.Format);
                sourceOffset += bytes; targetOffset += bytes;
            }
            return ReplaceMember(source, current.Member, data);
        }

        internal static CharacterAsset ReplaceMember(CharacterAsset source, string member, byte[] data)
        {
            byte[] changed = data;
            if (member.Length > 0)
            {
                var archive = new StudioArchiveCopy(source.Source, source.Data);
                archive.Files[member].Data = data;
                changed = archive.Build();
            }
            return new CharacterAsset { Source = source.Source, Target = source.Target, Role = source.Role, Data = changed, LogoRegions = source.LogoRegions, PaintBase = source.PaintBase };
        }
    }
}
