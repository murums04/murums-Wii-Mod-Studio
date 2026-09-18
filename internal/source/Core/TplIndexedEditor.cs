using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;

namespace murumsWiiModStudio
{
    internal static partial class TplTextureEditor
    {
        static int ValidatePalette(byte[] data, int table, int format)
        {
            int header = checked((int)ReadU32(data, table + 4));
            if (header < 12 || (long)header + 12 > data.Length)
                throw new InvalidDataException("Invalid TPL palette header.");
            int count = ReadU16(data, header), kind = checked((int)ReadU32(data, header + 4)), offset = checked((int)ReadU32(data, header + 8));
            if (count < 1 || count > (format == 8 ? 16 : 256) || kind > 2 || offset < 0 || (long)offset + count * 2 > data.Length)
                throw new InvalidDataException("Invalid TPL palette.");
            return header;
        }

        static void Put32(byte[] data, int p, int value)
        {
            data[p] = (byte)(value >> 24);
            data[p + 1] = (byte)(value >> 16);
            data[p + 2] = (byte)(value >> 8);
            data[p + 3] = (byte)value;
        }

        static int Align32(int n)
        {
            return checked((n + 31) & ~31);
        }

        static ushort PaletteValue(Color c, int kind)
        {
            return kind == 0 ? (ushort)((c.A << 8) | Intensity(c)) : kind == 1 ? EncodeRgb565(c.R, c.G, c.B) : EncodeRgb5A3(c.R, c.G, c.B, c.A);
        }

        static Color PaletteColour(ushort v, int kind)
        {
            if (kind == 0)
                return Color.FromArgb(v >> 8, v & 255, v & 255, v & 255);
            if (kind == 1)
            {
                byte r, g, b;
                DecodeRgb565(v, out r, out g, out b);
                return Color.FromArgb(r, g, b);
            }

            if ((v & 32768) != 0)
            {
                int r = (v >> 10) & 31, g = (v >> 5) & 31, b = v & 31;
                return Color.FromArgb((r << 3) | (r >> 2), (g << 3) | (g >> 2), (b << 3) | (b >> 2));
            }

            int a = (v >> 12) & 7;
            return Color.FromArgb((a << 5) | (a << 2) | (a >> 1), ((v >> 8) & 15) * 17, ((v >> 4) & 15) * 17, (v & 15) * 17);
        }

        static long ColourDistance(Color a, Color b)
        {
            long da = a.A - b.A, dr = (a.R * a.A - b.R * b.A) / 255, dg = (a.G * a.A - b.G * b.A) / 255, db = (a.B * a.A - b.B * b.A) / 255;
            return da * da * 3 + dr * dr + dg * dg + db * db;
        }

        static byte[] ReplaceIndexed(byte[] target, Bitmap bitmap, TplTextureInfo info, int index)
        {
            int table = checked((int)ReadU32(target, 8) + index * 8), oldPalette = ValidatePalette(target, table, info.Format), kind = checked((int)ReadU32(target, oldPalette + 4)), capacity = info.Format == 8 ? 16 : 256;
            var counts = new Dictionary<ushort, int>();
            using (var px = new PixelReader(bitmap))
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        ushort v = PaletteValue(px.Get(x, y), kind);
                        int n;
                        counts.TryGetValue(v, out n);
                        counts[v] = n + 1;
                    }

            var candidates = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key).ToList();
            var values = new List<ushort>();
            if (candidates.Count <= capacity)
                values.AddRange(candidates.Select(p => p.Key));
            else
            {
                var transparent = candidates.FirstOrDefault(p => PaletteColour(p.Key, kind).A == 0);
                if (transparent.Value > 0)
                    values.Add(transparent.Key);
                else
                    values.Add(candidates[0].Key);
                while (values.Count < capacity)
                {
                    ushort best = 0;
                    double score = -1;
                    foreach (var p in candidates)
                    {
                        Color c = PaletteColour(p.Key, kind);
                        long distance = values.Min(v => ColourDistance(c, PaletteColour(v, kind)));
                        double weight = distance * Math.Sqrt(p.Value);
                        if (weight > score)
                        {
                            score = weight;
                            best = p.Key;
                        }
                    }

                    values.Add(best);
                }
            }

            Color[] colours = values.Select(v => PaletteColour(v, kind)).ToArray();
            var payload = new List<byte>();
            for (int level = 0; level <= info.MaxLod; level++)
            {
                Bitmap mip = level == 0 ? null : Resize(bitmap, Math.Max(1, info.Width >> level), Math.Max(1, info.Height >> level));
                try
                {
                    using (var px = new PixelReader(mip ?? bitmap))
                    {
                        int bh = info.Format == 8 ? 8 : 4;
                        for (int by = 0; by < px.Height; by += bh)
                            for (int bx = 0; bx < px.Width; bx += 8)
                                for (int y = 0; y < bh; y++)
                                    for (int x = 0; x < 8; x += info.Format == 8 ? 2 : 1)
                                    {
                                        int first = NearestPalette(px.Get(bx + x, by + y), colours);
                                        payload.Add((byte)(info.Format == 8 ? (first << 4) | NearestPalette(px.Get(bx + x + 1, by + y), colours) : first));
                                    }
                    }
                }
                finally
                {
                    if (mip != null)
                        mip.Dispose();
                }
            }

            int imageHeader = Align32(target.Length), paletteHeader = imageHeader + 36, paletteData = Align32(paletteHeader + 12), imageData = Align32(paletteData + values.Count * 2);
            byte[] output = new byte[checked(imageData + payload.Count)];
            Buffer.BlockCopy(target, 0, output, 0, target.Length);
            Buffer.BlockCopy(target, info.ImageHeaderOffset, output, imageHeader, 36);
            Buffer.BlockCopy(target, oldPalette, output, paletteHeader, 12);
            Put32(output, table, imageHeader);
            Put32(output, table + 4, paletteHeader);
            Put32(output, imageHeader + 8, imageData);
            output[paletteHeader] = (byte)(values.Count >> 8);
            output[paletteHeader + 1] = (byte)values.Count;
            Put32(output, paletteHeader + 8, paletteData);
            for (int i = 0; i < values.Count; i++)
            {
                output[paletteData + i * 2] = (byte)(values[i] >> 8);
                output[paletteData + i * 2 + 1] = (byte)values[i];
            }

            Buffer.BlockCopy(payload.ToArray(), 0, output, imageData, payload.Count);
            return output;
        }

        static int NearestPalette(Color c, Color[] palette)
        {
            int best = 0;
            long distance = long.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                long d = ColourDistance(c, palette[i]);
                if (d < distance)
                {
                    best = i;
                    distance = d;
                }
            }

            return best;
        }
    }
}
