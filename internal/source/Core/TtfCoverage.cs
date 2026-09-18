using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class TtfCoverage
    {
        static int U16(byte[] b, int p)
        {
            if (p < 0 || p + 2 > b.Length)
                throw new InvalidDataException("Truncated TTF.");
            return b[p] * 256 + b[p + 1];
        }

        static int U32(byte[] b, int p)
        {
            return checked((int)BigEndian.ReadUInt32(b, p));
        }

        // Inspect Unicode cmap entries before rendering, so a missing character never becomes a fallback-font glyph.
        public static HashSet<int> Latin(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            var found = new HashSet<int>();
            int tables = U16(b, 4), cmap = -1, limit = 0;
            for (int i = 0; i < tables; i++)
            {
                int p = checked(12 + i * 16);
                U32(b, p + 12);
                if (Encoding.ASCII.GetString(b, p, 4) == "cmap")
                {
                    cmap = U32(b, p + 8);
                    limit = checked(cmap + U32(b, p + 12));
                }
            }

            if (cmap < 0 || limit > b.Length || limit < cmap + 4)
                throw new InvalidDataException("TTF Unicode character map missing.");
            int count = U16(b, cmap + 2);
            for (int t = 0; t < count; t++)
            {
                int record = checked(cmap + 4 + t * 8);
                if (record + 8 > limit)
                    throw new InvalidDataException("Invalid TTF cmap records.");
                int platform = U16(b, record), encoding = U16(b, record + 2);
                if (platform != 0 && !(platform == 3 && (encoding == 1 || encoding == 10)))
                    continue;
                int p = checked(cmap + U32(b, record + 4));
                if (p < cmap || p + 4 > limit)
                    throw new InvalidDataException("Invalid TTF cmap pointer.");
                int format = U16(b, p);
                if (format == 4)
                {
                    int end = checked(p + U16(b, p + 2)), n = U16(b, p + 6) / 2;
                    if (end > limit || n < 1 || p + 16L + n * 8 > end)
                        throw new InvalidDataException("Invalid TTF cmap segments.");
                    for (int c = 33; c <= 255; c++)
                        for (int s = 0; s < n; s++)
                        {
                            int last = U16(b, p + 14 + s * 2), first = U16(b, p + 16 + n * 2 + s * 2);
                            if (c < first || c > last)
                                continue;
                            int delta = U16(b, p + 16 + n * 4 + s * 2), ro = p + 16 + n * 6 + s * 2, range = U16(b, ro), glyph;
                            if (range == 0)
                                glyph = (c + delta) & 65535;
                            else
                            {
                                int at = checked(ro + range + (c - first) * 2);
                                if (at + 2 > end)
                                    throw new InvalidDataException("Invalid TTF glyph index.");
                                glyph = U16(b, at);
                                if (glyph != 0)
                                    glyph = (glyph + delta) & 65535;
                            }

                            if (glyph != 0)
                                found.Add(c);
                            break;
                        }
                }

                if (format == 12)
                {
                    int end = checked(p + U32(b, p + 4)), groups = U32(b, p + 12);
                    if (end > limit || p + 16L + groups * 12L > end)
                        throw new InvalidDataException("Invalid TTF cmap groups.");
                    for (int i = 0; i < groups; i++)
                    {
                        int q = p + 16 + i * 12, first = U32(b, q), last = U32(b, q + 4), start = U32(b, q + 8);
                        for (int c = Math.Max(33, first); c <= Math.Min(255, last); c++)
                            if ((long)start + c - first > 0)
                                found.Add(c);
                    }
                }
            }

            if (found.Count == 0)
                throw new NotSupportedException("This TTF has no supported Latin Unicode mapping (cmap format 4 or 12).");
            return found;
        }
    }
}
