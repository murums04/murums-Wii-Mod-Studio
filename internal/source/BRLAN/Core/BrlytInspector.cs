using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class BrlytTextureBinding
    {
        public string MaterialName = "";
        public string TextureName = "";
        public ushort TextureId;
        public int Slot;
        public int Offset = -1;
        public override string ToString()
        {
            return MaterialName + "  •  slot " + Slot.ToString() + "  •  " + TextureName;
        }
    }

    internal sealed class BrlytLayoutMap
    {
        public string SourcePath = "";
        public readonly List<string> Textures = new List<string>();
        public readonly List<BrlytTextureBinding> Bindings = new List<BrlytTextureBinding>();
        public List<BrlytTextureBinding> FindByTexture(string textureName)
        {
            List<BrlytTextureBinding> result = new List<BrlytTextureBinding>();
            int i;
            for (i = 0; i < Bindings.Count; i++)
                if (String.Equals(Bindings[i].TextureName, textureName, StringComparison.OrdinalIgnoreCase))
                    result.Add(Bindings[i]);
            return result;
        }

        public BrlytTextureBinding FindFirstByMaterial(string materialName)
        {
            int i;
            for (i = 0; i < Bindings.Count; i++)
                if (String.Equals(Bindings[i].MaterialName, materialName, StringComparison.Ordinal))
                    return Bindings[i];
            return null;
        }
    }

    internal static class BrlytInspector
    {
        private sealed class SectionRef
        {
            public string Magic;
            public int Offset;
            public int Size;
        }

        public static BrlytLayoutMap Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(L.T("BRLYT-Datei wurde nicht gefunden.", "BRLYT file was not found."), path);
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 0x10 || ReadAscii(data, 0, 4) != "RLYT")
                throw new InvalidDataException(L.T("Die Datei ist keine gültige BRLYT (RLYT).", "The file is not a valid BRLYT (RLYT)."));
            bool littleEndian;
            ushort bomBe = ReadU16BE(data, 4);
            if (bomBe == 0xFEFF)
                littleEndian = false;
            else if (bomBe == 0xFFFE)
                littleEndian = true;
            else
                throw new InvalidDataException(L.T("Unbekannte BRLYT-Byteorder.", "Unknown BRLYT byte order."));
            ushort headerSize = ReadU16(data, 0x0C, littleEndian);
            ushort sectionCount = ReadU16(data, 0x0E, littleEndian);
            if (headerSize < 0x10 || headerSize > data.Length)
                throw new InvalidDataException(L.T("Ungültige BRLYT-Headergrösse.", "Invalid BRLYT header size."));
            List<SectionRef> sections = new List<SectionRef>();
            int pos = headerSize;
            int s;
            for (s = 0; s < sectionCount && pos + 8 <= data.Length; s++)
            {
                string magic = ReadAscii(data, pos, 4);
                uint rawSize = ReadU32(data, pos + 4, littleEndian);
                if (rawSize < 8 || rawSize > Int32.MaxValue || pos + (int)rawSize > data.Length)
                    throw new InvalidDataException(L.T("Ungültige BRLYT-Sektion bei Offset 0x", "Invalid BRLYT section at offset 0x") + pos.ToString("X"));
                SectionRef sec = new SectionRef();
                sec.Magic = magic;
                sec.Offset = pos;
                sec.Size = (int)rawSize;
                sections.Add(sec);
                pos += (int)rawSize;
            }

            BrlytLayoutMap map = new BrlytLayoutMap();
            map.SourcePath = path;
            SectionRef txl = FindSection(sections, "txl1");
            if (txl != null)
                ParseTextures(data, txl, littleEndian, map.Textures);
            SectionRef mat = FindSection(sections, "mat1");
            if (mat != null)
                ParseMaterials(data, mat, littleEndian, map);
            return map;
        }

        private static SectionRef FindSection(List<SectionRef> sections, string magic)
        {
            int i;
            for (i = 0; i < sections.Count; i++)
                if (sections[i].Magic == magic)
                    return sections[i];
            return null;
        }

        private static void ParseTextures(byte[] data, SectionRef sec, bool littleEndian, List<string> textures)
        {
            if (sec.Size < 0x0C)
                return;
            int count = ReadU16(data, sec.Offset + 8, littleEndian);
            int offsetsBase = sec.Offset + 0x0C;
            int i;
            for (i = 0; i < count; i++)
            {
                int entry = offsetsBase + i * 8;
                if (entry + 8 > sec.Offset + sec.Size)
                    break;
                uint rel = ReadU32(data, entry, littleEndian);
                long absoluteLong = (long)offsetsBase + rel;
                if (absoluteLong < sec.Offset || absoluteLong >= sec.Offset + sec.Size)
                {
                    textures.Add("");
                    continue;
                }

                textures.Add(ReadCString(data, (int)absoluteLong, sec.Offset + sec.Size));
            }
        }

        private static void ParseMaterials(byte[] data, SectionRef sec, bool littleEndian, BrlytLayoutMap map)
        {
            if (sec.Size < 0x0C)
                return;
            int count = ReadU16(data, sec.Offset + 8, littleEndian);
            int table = sec.Offset + 0x0C;
            int i;
            for (i = 0; i < count; i++)
            {
                int offPos = table + i * 4;
                if (offPos + 4 > sec.Offset + sec.Size)
                    break;
                uint rel = ReadU32(data, offPos, littleEndian);
                if (rel > Int32.MaxValue)
                    continue;
                int material = sec.Offset + (int)rel;
                if (material < sec.Offset || material + 0x40 > sec.Offset + sec.Size)
                    continue;
                string materialName = ReadFixedCString(data, material, 20);
                uint flags = ReadU32(data, material + 0x3C, littleEndian);
                int textureMapCount = (int)(flags & 0x0F);
                int mapPos = material + 0x40;
                int slot;
                for (slot = 0; slot < textureMapCount; slot++)
                {
                    int p = mapPos + slot * 4;
                    if (p + 4 > sec.Offset + sec.Size)
                        break;
                    ushort textureId = ReadU16(data, p, littleEndian);
                    string textureName = textureId < map.Textures.Count ? map.Textures[textureId] : "";
                    BrlytTextureBinding binding = new BrlytTextureBinding();
                    binding.MaterialName = materialName;
                    binding.TextureName = textureName;
                    binding.TextureId = textureId;
                    binding.Slot = slot;
                    map.Bindings.Add(binding);
                }
            }
        }

        public static string FindNearbyLayout(string brlanPath)
        {
            if (String.IsNullOrWhiteSpace(brlanPath))
                return null;
            try
            {
                string full = Path.GetFullPath(brlanPath);
                string dir = Path.GetDirectoryName(full);
                if (String.IsNullOrEmpty(dir))
                    return null;
                List<string> candidates = new List<string>();
                AddBrlytFiles(candidates, dir);
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent != null)
                {
                    string siblingBlyt = Path.Combine(parent.FullName, "blyt");
                    AddBrlytFiles(candidates, siblingBlyt);
                }

                if (candidates.Count == 0)
                    return null;
                if (candidates.Count == 1)
                    return candidates[0];
                string stem = Path.GetFileNameWithoutExtension(full).ToLowerInvariant();
                stem = stem.Replace("_off_to_on", "").Replace("_loop", "").Replace("_off", "").Replace("_on", "");
                int bestScore = Int32.MinValue;
                string best = null;
                int i;
                for (i = 0; i < candidates.Count; i++)
                {
                    string cstem = Path.GetFileNameWithoutExtension(candidates[i]).ToLowerInvariant();
                    int score = 0;
                    if (cstem == stem)
                        score += 100;
                    if (cstem.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                        score += 50;
                    if (stem.StartsWith(cstem, StringComparison.OrdinalIgnoreCase))
                        score += 30;
                    if (cstem.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 20;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidates[i];
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        private static void AddBrlytFiles(List<string> target, string dir)
        {
            try
            {
                if (String.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    return;
                string[] files = Directory.GetFiles(dir, "*.brlyt", SearchOption.TopDirectoryOnly);
                int i;
                for (i = 0; i < files.Length; i++)
                    if (!target.Contains(files[i]))
                        target.Add(files[i]);
            }
            catch
            {
            }
        }

        private static string ReadCString(byte[] data, int offset, int limit)
        {
            if (offset < 0 || offset >= data.Length)
                return "";
            int end = offset;
            int max = Math.Min(limit, data.Length);
            while (end < max && data[end] != 0)
                end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static string ReadFixedCString(byte[] data, int offset, int length)
        {
            int end = offset;
            int max = Math.Min(data.Length, offset + length);
            while (end < max && data[end] != 0)
                end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static string ReadAscii(byte[] data, int offset, int length)
        {
            if (offset < 0 || offset + length > data.Length)
                return "";
            return Encoding.ASCII.GetString(data, offset, length);
        }

        private static ushort ReadU16BE(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static ushort ReadU16(byte[] data, int offset, bool little)
        {
            if (little)
                return (ushort)(data[offset] | (data[offset + 1] << 8));
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadU32(byte[] data, int offset, bool little)
        {
            if (little)
                return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }
    }

    internal sealed class TplMetadata
    {
        public int Width;
        public int Height;
        public uint Format;
    }

    internal static class TplMetadataReader
    {
        public static bool TryRead(string path, out TplMetadata info)
        {
            info = null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 0x20 || ReadU32BE(data, 0) != 0x0020AF30u)
                    return false;
                uint count = ReadU32BE(data, 4);
                uint table = ReadU32BE(data, 8);
                if (count < 1 || table > Int32.MaxValue || (int)table + 8 > data.Length)
                    return false;
                uint imageHeader = ReadU32BE(data, (int)table);
                if (imageHeader > Int32.MaxValue || (int)imageHeader + 0x0C > data.Length)
                    return false;
                TplMetadata m = new TplMetadata();
                m.Height = ReadU16BE(data, (int)imageHeader + 0x00);
                m.Width = ReadU16BE(data, (int)imageHeader + 0x02);
                m.Format = ReadU32BE(data, (int)imageHeader + 0x04);
                info = m;
                return m.Width > 0 && m.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        public static string FindSiblingTpl(string brlytPath, string textureName)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(brlytPath) || String.IsNullOrWhiteSpace(textureName))
                    return null;
                string blytDir = Path.GetDirectoryName(Path.GetFullPath(brlytPath));
                DirectoryInfo root = Directory.GetParent(blytDir);
                if (root == null)
                    return null;
                string path = Path.Combine(root.FullName, "timg", textureName);
                return File.Exists(path) ? path : null;
            }
            catch
            {
                return null;
            }
        }

        private static ushort ReadU16BE(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadU32BE(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
