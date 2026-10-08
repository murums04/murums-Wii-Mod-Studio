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

        sealed class PaletteCandidate
        {
            internal ushort Value;
            internal int Count;
            internal Color Colour;
        }

        static List<ushort> BuildImportPalette(Dictionary<ushort, int> counts, int capacity, int kind)
        {
            var candidates = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
                .Select(p => new PaletteCandidate { Value = p.Key, Count = p.Value, Colour = PaletteColour(p.Key, kind) }).ToList();
            if (candidates.Count <= capacity) return candidates.Select(p => p.Value).ToList();
            var transparent = candidates.FirstOrDefault(p => p.Colour.A == 0);
            var values = new List<ushort> { transparent == null ? candidates[0].Value : transparent.Value };
            var distances = new long[candidates.Count];
            var weights = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++) { distances[i] = Int64.MaxValue; weights[i] = Math.Sqrt(candidates[i].Count); }
            while (values.Count < capacity)
            {
                Color newest = PaletteColour(values[values.Count - 1], kind);
                double score = -1;
                int best = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    distances[i] = Math.Min(distances[i], ColourDistance(candidates[i].Colour, newest));
                    double weight = distances[i] * weights[i];
                    if (weight > score) { score = weight; best = i; }
                }
                if (score <= 0) break;
                values.Add(candidates[best].Value);
            }
            return values;
        }
        static void EncodeIndexedImage(Bitmap bitmap, int format, int kind, int maxLod, out byte[] imageBytes, out byte[] paletteBytes, bool includeMipPalette = true)
        {
            if ((format != 8 && format != 9) || kind < 0 || kind > 2) throw new ArgumentException("Invalid indexed TPL format.");
            var counts = new Dictionary<ushort, int>();
            var levels = new List<Bitmap>();
            try
            {
                levels.Add(bitmap);
                for (int level = 1; level <= maxLod; level++)
                    levels.Add(Resize(bitmap, Math.Max(1, bitmap.Width >> level), Math.Max(1, bitmap.Height >> level)));
                foreach (Bitmap mip in includeMipPalette ? levels : new List<Bitmap> { bitmap })
                    using (var px = new PixelReader(mip))
                        for (int y = 0; y < mip.Height; y++)
                            for (int x = 0; x < mip.Width; x++)
                            {
                                ushort value = PaletteValue(px.Get(x, y), kind);
                                int count; counts.TryGetValue(value, out count);
                                counts[value] = count + 1;
                            }
                List<ushort> values = BuildImportPalette(counts, format == 8 ? 16 : 256, kind);
                Color[] colours = values.Select(v => PaletteColour(v, kind)).ToArray();
                var nearest = new Dictionary<int, int>();
                using (var payload = new MemoryStream())
                {
                    foreach (Bitmap mip in levels)
                        using (var px = new PixelReader(mip))
                        {
                            int bh = format == 8 ? 8 : 4;
                            for (int by = 0; by < px.Height; by += bh)
                                for (int bx = 0; bx < px.Width; bx += 8)
                                    for (int y = 0; y < bh; y++)
                                        for (int x = 0; x < 8; x += format == 8 ? 2 : 1)
                                        {
                                            int first = CachedNearestPalette(px.Get(bx + x, by + y), colours, nearest);
                                            payload.WriteByte((byte)(format == 8 ? (first << 4) | CachedNearestPalette(px.Get(bx + x + 1, by + y), colours, nearest) : first));
                                        }
                        }
                    imageBytes = payload.ToArray();
                }
                paletteBytes = new byte[values.Count * 2];
                for (int i = 0; i < values.Count; i++)
                {
                    paletteBytes[i * 2] = (byte)(values[i] >> 8); paletteBytes[i * 2 + 1] = (byte)values[i];
                }
            }
            finally { for (int i = 1; i < levels.Count; i++) levels[i].Dispose(); }
        }

        static int CachedNearestPalette(Color colour, Color[] palette, Dictionary<int, int> nearest)
        {
            int index, key = colour.ToArgb();
            if (!nearest.TryGetValue(key, out index))
            {
                index = NearestPalette(colour, palette);
                if (nearest.Count < 65536) nearest.Add(key, index);
            }
            return index;
        }

        static byte[] ReplaceIndexed(byte[] target, Bitmap bitmap, TplTextureInfo info, int index)
        {
            int table = checked((int)ReadU32(target, 8) + index * 8);
            int oldPalette = ValidatePalette(target, table, info.Format), kind = checked((int)ReadU32(target, oldPalette + 4));
            byte[] payload, palette;
            EncodeIndexedImage(bitmap, info.Format, kind, info.MaxLod, out payload, out palette, false);
            TplLayout layout;
            try { layout = ReadImportLayout(target); }
            catch (InvalidDataException) { return ReplaceIndexedWithAncillaryData(target, info, index, table, oldPalette, payload, palette); }
            return RepackImport(layout, index, info.Format, info.MaxLod, payload, palette, kind, false);
        }

        static byte[] ReplaceIndexedWithAncillaryData(byte[] target, TplTextureInfo info, int index, int table, int oldPalette, byte[] payload, byte[] palette)
        {
            TplLayout layout = ReadImportLayout(target, true);
            TplImageRecord selected = layout.Images[index];
            bool shared = false;
            for (int i = 0; i < layout.Images.Length; i++)
            {
                if (i == index) continue;
                TplImageRecord other = layout.Images[i];
                if (other.Header == selected.Header || other.ImageData == selected.ImageData
                    || other.PaletteHeader == selected.PaletteHeader || other.PaletteData == selected.PaletteData) shared = true;
            }
            int start = selected.PaletteData.Offset;
            bool fits = (long)start + palette.Length <= target.Length;
            foreach (TplBlock block in layout.Blocks)
                if (block != selected.PaletteData && (long)start < (long)block.Offset + block.Bytes.Length && (long)block.Offset < (long)start + palette.Length)
                    fits = false;
            if (fits)
                for (int p = selected.PaletteData.Bytes.Length; p < palette.Length; p++)
                    if (target[start + p] != 0) { fits = false; break; }
            if (shared || !fits) return AppendIndexedImport(target, info, table, oldPalette, payload, palette);
            byte[] output = (byte[])target.Clone();
            Buffer.BlockCopy(payload, 0, output, info.DataOffset, payload.Length);
            Array.Clear(output, start, Math.Max(selected.PaletteData.Bytes.Length, palette.Length));
            Buffer.BlockCopy(palette, 0, output, start, palette.Length);
            output[oldPalette] = (byte)((palette.Length / 2) >> 8); output[oldPalette + 1] = (byte)(palette.Length / 2);
            return output;
        }

        static byte[] AppendIndexedImport(byte[] target, TplTextureInfo info, int table, int oldPalette, byte[] payload, byte[] palette)
        {
            // Unbekannte Zusatzdaten bleiben beim bisherigen Originalprofil unangetastet.
            int imageHeader = Align32(target.Length), paletteHeader = imageHeader + 36;
            int paletteData = Align32(paletteHeader + 12), imageData = Align32(paletteData + (info.Format == 8 ? 16 : 256) * 2);
            byte[] output = new byte[checked(imageData + payload.Length)];
            Buffer.BlockCopy(target, 0, output, 0, target.Length);
            Buffer.BlockCopy(target, info.ImageHeaderOffset, output, imageHeader, 36);
            Buffer.BlockCopy(target, oldPalette, output, paletteHeader, 12);
            Put32(output, table, imageHeader); Put32(output, table + 4, paletteHeader);
            Put32(output, imageHeader + 8, imageData);
            output[paletteHeader] = (byte)((palette.Length / 2) >> 8); output[paletteHeader + 1] = (byte)(palette.Length / 2);
            Put32(output, paletteHeader + 8, paletteData);
            Buffer.BlockCopy(palette, 0, output, paletteData, palette.Length);
            Buffer.BlockCopy(payload, 0, output, imageData, payload.Length);
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
