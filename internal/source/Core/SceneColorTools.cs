using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class SceneColorTools
    {
        static int U(byte[] b, int p)
        {
            Check(b, p, 4);
            return checked((int)BigEndian.ReadUInt32(b, p));
        }

        static void Check(byte[] b, int p, int n)
        {
            if (p < 0 || n < 0 || (long)p + n > b.Length)
                throw new InvalidDataException("Scene resource is truncated.");
        }

        static int Word(byte[] b, int p)
        {
            Check(b, p, 2);
            return b[p] << 8 | b[p + 1];
        }

        static void PutWord(byte[] b, int p, int n)
        {
            b[p] = (byte)(n >> 8);
            b[p + 1] = (byte)n;
        }

        static List<BrresModelVisibility.Entry> Group(byte[] b, string name)
        {
            Check(b, 0, 16);
            if (Encoding.ASCII.GetString(b, 0, 4) != "bres")
                throw new InvalidDataException("Expected BRRES.");
            foreach (var g in BrresModelVisibility.Dictionary(b, Word(b, 12) + 8))
                if (g.Name == name)
                    return BrresModelVisibility.Dictionary(b, g.Data);
            return new List<BrresModelVisibility.Entry>();
        }

        static int Tint565(int value, Color color)
        {
            int r = (value >> 11) * 255 / 31, g = ((value >> 5) & 63) * 255 / 63, bl = (value & 31) * 255 / 31;
            int light = Math.Max(r, Math.Max(g, bl));
            return ((light * color.R / 255 * 31 / 255) << 11) | ((light * color.G / 255 * 63 / 255) << 5) | (light * color.B / 255 * 31 / 255);
        }

        public static byte[] TintGlobeTextures(byte[] original, Color color)
        {
            byte[] b = (byte[])original.Clone();
            if (color.ToArgb() == Color.White.ToArgb())
                return b;
            int changed = 0;
            foreach (var t in Group(b, "Textures(NW4R)"))
            {
                int p = t.Data;
                Check(b, p, 48);
                if (Encoding.ASCII.GetString(b, p, 4) != "TEX0" || (U(b, p + 8) != 3 && U(b, p + 8) != 1))
                    throw new InvalidDataException("Unsupported globe texture header.");
                if (U(b, p + 32) != 14)
                    continue;
                int data = p + U(b, p + 16), w = Word(b, p + 28), h = Word(b, p + 30), levels = U(b, p + 36);
                if (levels < 1 || levels > 16)
                    throw new InvalidDataException("Invalid mipmap count.");
                int bytes = 0;
                for (int i = 0; i < levels; i++)
                {
                    bytes = checked(bytes + ((w + 7) / 8) * ((h + 7) / 8) * 32);
                    w = Math.Max(1, w / 2);
                    h = Math.Max(1, h / 2);
                }

                Check(b, data, bytes);
                if ((long)data + bytes > (long)p + U(b, p + 4))
                    throw new InvalidDataException("Texture exceeds its section.");
                for (int q = data; q < data + bytes; q += 8)
                {
                    int a = Word(b, q), c = Word(b, q + 2), x = Tint565(a, color), y = Tint565(c, color);
                    bool opaque = a > c, swap = opaque ? x < y : x > y;
                    if (swap)
                    {
                        int z = x;
                        x = y;
                        y = z;
                        for (int k = 4; k < 8; k++)
                        {
                            int n = 0;
                            for (int j = 0; j < 4; j++)
                            {
                                int ix = (b[q + k] >> (j * 2)) & 3;
                                ix = ix < 2 ? ix ^ 1 : (opaque ? ix ^ 1 : ix);
                                n |= ix << (j * 2);
                            }

                            b[q + k] = (byte)n;
                        }
                    }

                    if (opaque && x == y)
                    {
                        if (x < 65535)
                            x++;
                        else
                            y--;
                    }

                    PutWord(b, q, x);
                    PutWord(b, q + 2, y);
                }

                changed++;
            }

            if (changed == 0)
                throw new InvalidDataException("No globe colour textures found.");
            return b;
        }

        static void Recolor(byte[] b, int p, Color c)
        {
            Check(b, p, 4);
            int v = Math.Max(b[p], Math.Max(b[p + 1], b[p + 2]));
            b[p] = (byte)(v * c.R / 255);
            b[p + 1] = (byte)(v * c.G / 255);
            b[p + 2] = (byte)(v * c.B / 255);
        }

        public static byte[] TintSky(byte[] original, Color color)
        {
            byte[] b = (byte[])original.Clone();
            if (color.ToArgb() == Color.White.ToArgb())
                return b;
            int edits = 0;
            foreach (var model in BrresModelVisibility.Models(b))
            {
                int version = U(b, model.Data + 8);
                if (version != 11)
                    throw new InvalidDataException("Unsupported sky model revision.");
                int dict = model.Data + U(b, model.Data + 48);
                foreach (var mat in BrresModelVisibility.Dictionary(b, dict))
                {
                    int dl = mat.Data + U(b, mat.Data + 60);
                    Check(b, dl, 160);
                    if (dl + 160 > mat.Data + U(b, mat.Data))
                        throw new InvalidDataException("Sky material display list out of bounds.");
                    for (int i = 0; i < 7; i++)
                    {
                        int lo = dl + (i < 3 ? 32 + i * 20 : 96 + (i - 3) * 10), hi = lo + 5, reg = i < 3 ? 0xe2 + i * 2 : 0xe0 + (i - 3) * 2;
                        if (b[lo] != 0x61 || b[hi] != 0x61 || b[lo + 1] != reg || b[hi + 1] != reg + 1)
                            throw new InvalidDataException("Unsupported sky material colour commands.");
                        uint l = BigEndian.ReadUInt32(b, lo + 1), h = BigEndian.ReadUInt32(b, hi + 1);
                        int r = (int)(l & 0x7ff), g = (int)((h >> 12) & 0x7ff), blue = (int)(h & 0x7ff);
                        if (r > 255 || g > 255 || blue > 255)
                            throw new InvalidDataException("Unsupported signed sky colour.");
                        int light = Math.Max(r, Math.Max(g, blue));
                        uint nl = (l & ~0x7ffu) | (uint)(light * color.R / 255), nh = (h & ~0x7ff7ffu) | (uint)(light * color.B / 255) | ((uint)(light * color.G / 255) << 12);
                        BigEndian.WriteUInt32(b, lo + 1, nl);
                        BigEndian.WriteUInt32(b, hi + 1, nh);
                        if (i < 3)
                        {
                            for (int repeat = 1; repeat <= 2; repeat++)
                            {
                                int at = hi + repeat * 5;
                                if (b[at] != 0x61 || BigEndian.ReadUInt32(b, at + 1) != h)
                                    throw new InvalidDataException("Unsupported repeated sky colour command.");
                                BigEndian.WriteUInt32(b, at + 1, nh);
                            }
                        }

                        edits++;
                    }
                }
            }

            if (edits == 0)
                throw new InvalidDataException("No sky material colours found.");
            return b;
        }

        public static byte[] ReplaceStarPattern(byte[] original, Bitmap image)
        {
            byte[] b = (byte[])original.Clone();
            foreach (var texture in Group(b, "Textures(NW4R)"))
            {
                if (texture.Name != "galaxy3_2s")
                    continue;
                int p = texture.Data, w = Word(b, p + 28), h = Word(b, p + 30), start = p + U(b, p + 16);
                if (U(b, p + 32) != 1 || U(b, p + 36) != 1 || w % 8 != 0 || h % 4 != 0)
                    throw new InvalidDataException("Unsupported star texture.");
                Check(b, start, w * h);
                if (start + w * h > p + U(b, p + 4))
                    throw new InvalidDataException("Star texture exceeds section.");
                using (var scaled = new Bitmap(image, w, h))
                {
                    int q = start;
                    for (int by = 0; by < h; by += 4)
                        for (int bx = 0; bx < w; bx += 8)
                            for (int y = 0; y < 4; y++)
                                for (int x = 0; x < 8; x++)
                                {
                                    Color c = scaled.GetPixel(bx + x, by + y);
                                    b[q++] = (byte)(((c.R * 299 + c.G * 587 + c.B * 114) / 1000) * c.A / 255);
                                }
                }

                return b;
            }

            throw new InvalidDataException("Star pattern texture not found.");
        }

        public static byte[] TintGlobeGlow(byte[] original, Color color)
        {
            byte[] b = (byte[])original.Clone();
            if (color.ToArgb() == Color.White.ToArgb())
                return b;
            foreach (var t in Group(b, "Textures(NW4R)"))
            {
                if (t.Name != "luminous")
                    continue;
                int p = t.Data;
                if (U(b, p + 32) != 6 || U(b, p + 36) != 1)
                    throw new InvalidDataException("Unsupported globe glow texture.");
                int start = p + U(b, p + 16), length = ((Word(b, p + 28) + 3) / 4) * ((Word(b, p + 30) + 3) / 4) * 64;
                Check(b, start, length);
                if ((long)start + length > (long)p + U(b, p + 4))
                    throw new InvalidDataException("Glow texture exceeds section.");
                for (int block = start; block < start + length; block += 64)
                    for (int j = 0; j < 16; j++)
                    {
                        int r = block + j * 2 + 1, g = block + 32 + j * 2, bl = g + 1, light = Math.Max(b[r], Math.Max(b[g], b[bl]));
                        b[r] = (byte)(light * color.R / 255);
                        b[g] = (byte)(light * color.G / 255);
                        b[bl] = (byte)(light * color.B / 255);
                    }

                return b;
            }

            throw new InvalidDataException("Globe glow texture luminous is missing.");
        }

        public static ArchiveEntry Find(ArchiveEntry e, string name)
        {
            if (!e.IsDirectory)
                return e.Name == name ? e : null;
            foreach (var c in e.Children)
            {
                var hit = Find(c, name);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        public static byte[] ColorGlobeArchive(byte[] input, Color color)
        {
            return ColorGlobeArchive(input, color, Color.White);
        }

        public static byte[] ColorGlobeArchive(byte[] input, Color color, Color glow)
        {
            var a = U8Archive.Load(input);
            var e = Find(a.Root, "earth.brres.LZ");
            if (e == null)
                throw new InvalidDataException("earth.brres.LZ is missing from globe.arc.");
            e.Data = NintendoLz.Encode(TintGlobeGlow(TintGlobeTextures(NintendoLz.Decode(e.Data), color), glow));
            return a.BuildU8();
        }
    }
}
