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
            var archive = new StudioArchiveCopy(asset.Source, asset.Data);
            var result = new List<VehicleTexture>();
            foreach (var member in archive.Files.Where(f => Path.GetFileName(f.Key).Equals("kart_model.brres", StringComparison.OrdinalIgnoreCase)))
            {
                var data = member.Value.Data;
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
            var current = Textures(source).SingleOrDefault(t => t.Member == target.Member && t.Name == target.Name);
            if (current == null) throw new InvalidDataException("Vehicle texture is no longer present.");
            var info = TplTextureEditor.GetImageInfo(current.Tpl, 0);
            if (region.Width < 2 || region.Height < 2 || !new Rectangle(0, 0, info.Width, info.Height).Contains(region)) throw new InvalidDataException(L.T("Zuerst das vorhandene Emblem im Texturbild markieren.", "Mark the existing emblem in the texture first."));
            var archive = new StudioArchiveCopy(source.Source, source.Data);
            using (var original = Decode(current.Tpl))
            using (var fitted = Fit(image, region.Width, region.Height))
            {
                using (var graphics = Graphics.FromImage(original))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(fitted, region.Location);
                }
                byte[] replaced = TplTextureEditor.ReplaceFirstImage(current.Tpl, original, false);
                // Nur die ausgewählte Textur ersetzen; Modelle und andere Ressourcen bleiben bytegleich.
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
                            int offset = (y * blocks + x) * blockBytes;
                            Buffer.BlockCopy(replaced, sourceOffset + offset, archive.Files[current.Member].Data, targetOffset + offset, blockBytes);
                        }
                    int bytes = TplTextureEditor.GetBaseLevelPayloadLength(width, height, info.Format);
                    sourceOffset += bytes; targetOffset += bytes;
                }
            }
            return new CharacterAsset { Source = source.Source, Target = source.Target, Role = source.Role, Data = archive.Build() };
        }
    }
}