using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class ParticleTextures
    {
        static void Bounds(byte[] data, int offset, int count)
        {
            if (offset < 0 || count < 0 || (long)offset + count > data.Length) throw new InvalidDataException("Truncated effect texture.");
        }
        static int Word(byte[] data, int offset) { Bounds(data, offset, 2); return data[offset] * 256 + data[offset + 1]; }
        static int Number(byte[] data, int offset) { Bounds(data, offset, 4); return checked((int)BigEndian.ReadUInt32(data, offset)); }
        internal static Bitmap Read(byte[] data, string name)
        {
            Bounds(data, 0, 0x28);
            if (Encoding.ASCII.GetString(data, 0, 4) != "REFT" || Word(data, 4) != 0xfeff || Number(data, 8) != data.Length)
                throw new InvalidDataException("Expected a Wii REFT texture resource.");
            int table = checked(0x18 + Number(data, 0x18));
            int size = Number(data, table), count = Word(data, table + 4), cursor = table + 8;
            Bounds(data, table, size);
            for (int i = 0; i < count; i++)
            {
                int length = Word(data, cursor);
                Bounds(data, cursor + 2, length + 8);
                if (length < 1 || (long)cursor + length + 10 > table + size) throw new InvalidDataException("Invalid REFT name table.");
                string current = Encoding.ASCII.GetString(data, cursor + 2, length - 1);
                if (current.Equals(name, StringComparison.Ordinal))
                {
                    int entry = checked(table + Number(data, cursor + 2 + length));
                    int bytes = checked(32 + Number(data, cursor + 6 + length));
                    Bounds(data, entry, bytes); Bounds(data, entry, 32);
                    int width = Word(data, entry + 4), height = Word(data, entry + 6);
                    int imageSize = Number(data, entry + 8), paletteSize = Number(data, entry + 16);
                    if (width < 1 || height < 1 || width > 4096 || height > 4096 || (long)32 + imageSize + paletteSize > bytes)
                        throw new InvalidDataException("Invalid REFT image dimensions: " + width + "x" + height + " image=" + imageSize + " palette=" + paletteSize + " entry=" + bytes);
                    var tpl = new byte[checked(96 + imageSize + paletteSize)];
                    BigEndian.WriteUInt32(tpl, 0, 0x0020af30); BigEndian.WriteUInt32(tpl, 4, 1); BigEndian.WriteUInt32(tpl, 8, 12);
                    BigEndian.WriteUInt32(tpl, 12, 32);
                    tpl[32] = (byte)(height >> 8); tpl[33] = (byte)height; tpl[34] = (byte)(width >> 8); tpl[35] = (byte)width;
                    BigEndian.WriteUInt32(tpl, 36, data[entry + 12]); BigEndian.WriteUInt32(tpl, 40, 96);
                    Buffer.BlockCopy(data, entry + 32, tpl, 96, imageSize + paletteSize);
                    if (paletteSize > 0)
                    {
                        BigEndian.WriteUInt32(tpl, 16, 80);
                        tpl[80] = data[entry + 14]; tpl[81] = data[entry + 15];
                        BigEndian.WriteUInt32(tpl, 84, data[entry + 13]); BigEndian.WriteUInt32(tpl, 88, (uint)(96 + imageSize));
                    }
                    TexturePreviewResult decoded; string error;
                    if (!TexturePreview.TryDecode(name + ".tpl", tpl, 0, out decoded, out error)) throw new InvalidDataException(error);
                    using (decoded) return new Bitmap(decoded.Bitmap);
                }
                cursor += length + 10;
            }
            return null;
        }
        internal static Bitmap Sample(Bitmap texture, Color color)
        {
            var result = new Bitmap(640, 400);
            using (var g = Graphics.FromImage(result))
            using (var attributes = new ImageAttributes())
            {
                g.Clear(Color.FromArgb(38, 39, 46));
                var matrix = new ColorMatrix { Matrix00 = color.R / 255f, Matrix11 = color.G / 255f, Matrix22 = color.B / 255f, Matrix33 = color.A / 255f };
                attributes.SetColorMatrix(matrix);
                float scale = Math.Min(360f / texture.Width, 330f / texture.Height);
                int width = Math.Max(1, (int)(texture.Width * scale)), height = Math.Max(1, (int)(texture.Height * scale));
                g.DrawImage(texture, new Rectangle((640 - width) / 2, (400 - height) / 2, width, height), 0, 0, texture.Width, texture.Height, GraphicsUnit.Pixel, attributes);
            }
            return result;
        }
    }
}
