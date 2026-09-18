using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace murumsWiiModStudio
{
    internal sealed class BrctrPlacement
    {
        public string Control, Layout, Name;
        public StudioArchiveCopy Archive;
        public int Offset, Opacity;
        public float X, Y, ScaleX, ScaleY, WideX, WideY, WideScaleX, WideScaleY;
        public override string ToString()
        {
            return (Archive == null ? "" : Path.GetFileName(Archive.Source) + " / ") + Path.GetFileNameWithoutExtension(Control) + " / " + Name;
        }
    }

    internal static class BrctrPlacements
    {
        static int U16(byte[] b, int o)
        {
            if (o < 0 || o > b.Length - 2)
                throw new InvalidDataException("Truncated BRCTR.");
            return b[o] << 8 | b[o + 1];
        }

        static float F32(byte[] b, int o)
        {
            if (o < 0 || o > b.Length - 4)
                throw new InvalidDataException("Truncated BRCTR transform.");
            var v = new[]
            {
                b[o + 3],
                b[o + 2],
                b[o + 1],
                b[o]
            };
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(v);
            float f = BitConverter.ToSingle(v, 0);
            if (float.IsNaN(f) || float.IsInfinity(f) || Math.Abs(f) > 100000)
                throw new InvalidDataException("Invalid BRCTR transform.");
            return f;
        }

        static string Name(byte[] b, int table, int offset)
        {
            int o = table + offset;
            if (o < 0 || o >= b.Length)
                throw new InvalidDataException("Invalid BRCTR name offset.");
            int end = Array.IndexOf(b, (byte)0, o);
            if (end < 0)
                throw new InvalidDataException("Unterminated BRCTR name.");
            return Encoding.ASCII.GetString(b, o, end - o);
        }

        public static List<BrctrPlacement> Read(string key, byte[] bytes)
        {
            var result = new List<BrctrPlacement>();
            if (bytes.Length < 20 || Encoding.ASCII.GetString(bytes, 0, 4) != "bctr")
                throw new InvalidDataException("Invalid BRCTR header.");
            int names = U16(bytes, 16), layout = U16(bytes, 14);
            string name = Name(bytes, names, U16(bytes, 6));
            int first = layout + U16(bytes, layout), count = U16(bytes, layout + 2);
            if (first < 0 || first > bytes.Length || count > (bytes.Length - first) / 60)
                throw new InvalidDataException("Invalid BRCTR variant table.");
            for (int i = 0; i < count; i++)
            {
                int o = first + i * 60;
                result.Add(new BrctrPlacement { Control = key, Layout = name, Offset = o, Opacity = U16(bytes, o + 2), Name = Name(bytes, names, U16(bytes, o)), X = F32(bytes, o + 12), Y = F32(bytes, o + 16), ScaleX = F32(bytes, o + 24), ScaleY = F32(bytes, o + 28), WideX = F32(bytes, o + 32), WideY = F32(bytes, o + 36), WideScaleX = F32(bytes, o + 44), WideScaleY = F32(bytes, o + 48) });
            }

            return result;
        }

        public static byte[] Write(BrctrPlacement variant, byte[] source, bool wide, float x, float y, float sx, float sy, byte alpha)
        {
            if (!Read(variant.Control, source).Any(v => v.Offset == variant.Offset && v.Name == variant.Name))
                throw new InvalidDataException("BRCTR variant changed.");
            var result = (byte[])source.Clone();
            int at = variant.Offset + (wide ? 32 : 12);
            float[] values =
            {
                x,
                y,
                sx,
                sy
            };
            int[] offsets =
            {
                at,
                at + 4,
                at + 12,
                at + 16
            };
            for (int i = 0; i < 4; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) || Math.Abs(values[i]) > 100000)
                    throw new InvalidDataException("Invalid placement value.");
                var bytes = BitConverter.GetBytes(values[i]);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                Array.Copy(bytes, 0, result, offsets[i], 4);
            }

            result[variant.Offset + 2] = 0;
            result[variant.Offset + 3] = alpha;
            return result;
        }

        public static List<BrctrPlacement> Find(HudLayoutResource resource, IEnumerable<StudioArchiveCopy> archives)
        {
            var result = new List<BrctrPlacement>();
            if (resource == null)
                return result;
            string folder = Path.GetDirectoryName(resource.Key).Replace('\\', '/');
            int slash = folder.LastIndexOf('/');
            folder = slash < 0 ? "" : folder.Substring(0, slash) + "/";
            foreach (var archive in archives.OrderBy(a => a == resource.Archive ? 0 : 1))
                foreach (var f in archive.Files.Where(f => f.Key.EndsWith(".brctr", StringComparison.OrdinalIgnoreCase) && f.Key.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        foreach (var variant in Read(f.Key, f.Value.Data).Where(v => string.Equals(Path.GetFileNameWithoutExtension(v.Layout), Path.GetFileNameWithoutExtension(resource.Key), StringComparison.OrdinalIgnoreCase)))
                        {
                            variant.Archive = archive;
                            result.Add(variant);
                        }
                    }
                    catch (InvalidDataException)
                    {
                    }
                }

            return result;
        }
    }
}
