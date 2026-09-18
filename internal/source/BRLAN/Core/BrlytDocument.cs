using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class BrlytMaterialInfo
    {
        public string Name = "";
        public int Index;
        public int Offset;
        public readonly List<BrlytTextureBinding> Bindings = new List<BrlytTextureBinding>();
        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class BrlytTexCoordSet
    {
        public float U0, V0;
        public float U1, V1;
        public float U2, V2;
        public float U3, V3;
        public float MinU
        {
            get
            {
                return Math.Min(Math.Min(U0, U1), Math.Min(U2, U3));
            }
        }

        public float MaxU
        {
            get
            {
                return Math.Max(Math.Max(U0, U1), Math.Max(U2, U3));
            }
        }

        public float MinV
        {
            get
            {
                return Math.Min(Math.Min(V0, V1), Math.Min(V2, V3));
            }
        }

        public float MaxV
        {
            get
            {
                return Math.Max(Math.Max(V0, V1), Math.Max(V2, V3));
            }
        }

        // Nintendo BRLYT pic1 stores the first two vertices on the top edge and
        // the next two on the bottom edge. These helpers are only used by the
        // Retro Rewind texture baker to compensate intentional UV mirroring.
        public bool FlipX
        {
            get
            {
                return ((U0 + U2) * 0.5f) > ((U1 + U3) * 0.5f);
            }
        }

        public bool FlipY
        {
            get
            {
                return ((V0 + V1) * 0.5f) > ((V2 + V3) * 0.5f);
            }
        }
    }

    internal sealed class BrlytPaneInfo
    {
        public string Magic = "";
        public string Name = "";
        public int Offset;
        public int Size;
        public byte Flags;
        public byte Origin;
        public byte Alpha;
        public float X, Y, Z;
        public float RotX, RotY, RotZ;
        public float ScaleX, ScaleY;
        public float Width, Height;
        public int MaterialId = -1;
        // pic1-specific texture-coordinate sets. Kept read-only by the safe
        // editor; parsing them lets callers respect texture atlas/crop mapping.
        public readonly List<BrlytTexCoordSet> TexCoords = new List<BrlytTexCoordSet>();
        // txt1-specific fields. They are ignored for non-text panes.
        public int FontId = -1;
        public byte TextOrigin;
        public byte LineAlignment;
        public float FontWidth, FontHeight, CharSize, LineSize;
        public string EmbeddedText = "";
        public BrlytPaneInfo Parent;
        public readonly List<BrlytPaneInfo> Children = new List<BrlytPaneInfo>();
        public bool Visible
        {
            get
            {
                return (Flags & 1) != 0;
            }

            set
            {
                if (value)
                    Flags |= 1;
                else
                    Flags = (byte)(Flags & ~1);
            }
        }

        public override string ToString()
        {
            return "[" + Magic + "] " + (String.IsNullOrEmpty(Name) ? "(unnamed)" : Name);
        }
    }

    internal sealed class BrlytDocument
    {
        private sealed class SectionRef
        {
            public string Magic;
            public int Offset;
            public int Size;
        }

        public string SourcePath = "";
        public byte[] Data;
        public bool LittleEndian;
        public ushort HeaderSize;
        public ushort SectionCount;
        public float LayoutWidth = 640.0f;
        public float LayoutHeight = 480.0f;
        public readonly List<string> Textures = new List<string>();
        public readonly List<string> Fonts = new List<string>();
        public readonly List<BrlytMaterialInfo> Materials = new List<BrlytMaterialInfo>();
        public readonly List<BrlytPaneInfo> Panes = new List<BrlytPaneInfo>();
        public readonly List<BrlytPaneInfo> RootPanes = new List<BrlytPaneInfo>();
        private readonly List<SectionRef> _sections = new List<SectionRef>();
        public static BrlytDocument Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("BRLYT file was not found.", path);
            BrlytDocument doc = FromBytes(File.ReadAllBytes(path));
            doc.SourcePath = path;
            return doc;
        }

        public static BrlytDocument FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 0x10 || ReadAscii(bytes, 0, 4) != "RLYT")
                throw new InvalidDataException("The file is not a valid BRLYT (RLYT).");
            BrlytDocument doc = new BrlytDocument();
            doc.Data = (byte[])bytes.Clone();
            ushort bom = ReadU16BE(bytes, 4);
            if (bom == 0xFEFF)
                doc.LittleEndian = false;
            else if (bom == 0xFFFE)
                doc.LittleEndian = true;
            else
                throw new InvalidDataException("Unknown BRLYT byte order.");
            doc.HeaderSize = ReadU16(bytes, 0x0C, doc.LittleEndian);
            doc.SectionCount = ReadU16(bytes, 0x0E, doc.LittleEndian);
            if (doc.HeaderSize < 0x10 || doc.HeaderSize > bytes.Length)
                throw new InvalidDataException("Invalid BRLYT header size.");
            int pos = doc.HeaderSize;
            int i;
            for (i = 0; i < doc.SectionCount; i++)
            {
                if (pos + 8 > bytes.Length)
                    throw new InvalidDataException("BRLYT section table is truncated.");
                string magic = ReadAscii(bytes, pos, 4);
                uint rawSize = ReadU32(bytes, pos + 4, doc.LittleEndian);
                if (rawSize < 8 || rawSize > Int32.MaxValue || pos + (int)rawSize > bytes.Length)
                    throw new InvalidDataException("Invalid BRLYT section at 0x" + pos.ToString("X") + ".");
                SectionRef sec = new SectionRef();
                sec.Magic = magic;
                sec.Offset = pos;
                sec.Size = (int)rawSize;
                doc._sections.Add(sec);
                pos += sec.Size;
            }

            doc.Parse();
            return doc;
        }

        private void Parse()
        {
            Textures.Clear();
            Fonts.Clear();
            Materials.Clear();
            Panes.Clear();
            RootPanes.Clear();
            SectionRef lyt = FindSection("lyt1");
            if (lyt != null && lyt.Size >= 0x14)
            {
                LayoutWidth = ReadF32(Data, lyt.Offset + 0x0C, LittleEndian);
                LayoutHeight = ReadF32(Data, lyt.Offset + 0x10, LittleEndian);
                if (Single.IsNaN(LayoutWidth) || Single.IsInfinity(LayoutWidth) || LayoutWidth <= 0)
                    LayoutWidth = 640;
                if (Single.IsNaN(LayoutHeight) || Single.IsInfinity(LayoutHeight) || LayoutHeight <= 0)
                    LayoutHeight = 480;
            }

            ParseTextures();
            ParseFonts();
            ParseMaterials();
            ParsePanes();
        }

        private SectionRef FindSection(string magic)
        {
            int i;
            for (i = 0; i < _sections.Count; i++)
                if (_sections[i].Magic == magic)
                    return _sections[i];
            return null;
        }

        private void ParseTextures()
        {
            SectionRef sec = FindSection("txl1");
            if (sec == null || sec.Size < 0x0C)
                return;
            int count = ReadU16(Data, sec.Offset + 8, LittleEndian);
            int baseOffset = sec.Offset + 0x0C;
            int i;
            for (i = 0; i < count; i++)
            {
                int p = baseOffset + i * 8;
                if (p + 8 > sec.Offset + sec.Size)
                    break;
                uint rel = ReadU32(Data, p, LittleEndian);
                long abs = (long)baseOffset + rel;
                Textures.Add(abs >= sec.Offset && abs < sec.Offset + sec.Size ? ReadCString(Data, (int)abs, sec.Offset + sec.Size) : "");
            }
        }

        private void ParseFonts()
        {
            SectionRef sec = FindSection("fnl1");
            if (sec == null || sec.Size < 0x0C)
                return;
            int count = ReadU16(Data, sec.Offset + 8, LittleEndian);
            int baseOffset = sec.Offset + 0x0C;
            int i;
            for (i = 0; i < count; i++)
            {
                int p = baseOffset + i * 8;
                if (p + 8 > sec.Offset + sec.Size)
                    break;
                uint rel = ReadU32(Data, p, LittleEndian);
                long abs = (long)baseOffset + rel;
                Fonts.Add(abs >= sec.Offset && abs < sec.Offset + sec.Size ? ReadCString(Data, (int)abs, sec.Offset + sec.Size) : "");
            }
        }

        private void ParseMaterials()
        {
            SectionRef sec = FindSection("mat1");
            if (sec == null || sec.Size < 0x0C)
                return;
            int count = ReadU16(Data, sec.Offset + 8, LittleEndian);
            int table = sec.Offset + 0x0C;
            int i;
            for (i = 0; i < count; i++)
            {
                int p = table + i * 4;
                if (p + 4 > sec.Offset + sec.Size)
                    break;
                uint rel = ReadU32(Data, p, LittleEndian);
                if (rel > Int32.MaxValue)
                    continue;
                int off = sec.Offset + (int)rel;
                if (off < sec.Offset || off + 0x40 > sec.Offset + sec.Size)
                    continue;
                BrlytMaterialInfo mat = new BrlytMaterialInfo();
                mat.Index = i;
                mat.Offset = off;
                mat.Name = ReadFixedCString(Data, off, 20);
                uint flags = ReadU32(Data, off + 0x3C, LittleEndian);
                int mapCount = (int)(flags & 0x0F);
                int slot;
                for (slot = 0; slot < mapCount; slot++)
                {
                    int mp = off + 0x40 + slot * 4;
                    if (mp + 4 > sec.Offset + sec.Size)
                        break;
                    ushort id = ReadU16(Data, mp, LittleEndian);
                    BrlytTextureBinding b = new BrlytTextureBinding();
                    b.MaterialName = mat.Name;
                    b.TextureId = id;
                    b.TextureName = id < Textures.Count ? Textures[id] : "";
                    b.Slot = slot;
                    b.Offset = mp;
                    mat.Bindings.Add(b);
                }

                Materials.Add(mat);
            }
        }

        private static bool IsPaneMagic(string magic)
        {
            return magic == "pan1" || magic == "pic1" || magic == "txt1" || magic == "wnd1" || magic == "bnd1" || magic == "prt1";
        }

        private void ParsePanes()
        {
            Stack<BrlytPaneInfo> parents = new Stack<BrlytPaneInfo>();
            BrlytPaneInfo last = null;
            int i;
            for (i = 0; i < _sections.Count; i++)
            {
                SectionRef sec = _sections[i];
                if (IsPaneMagic(sec.Magic))
                {
                    if (sec.Size < 0x4C)
                    {
                        last = null;
                        continue;
                    }

                    int p = sec.Offset + 8;
                    BrlytPaneInfo pane = new BrlytPaneInfo();
                    pane.Magic = sec.Magic;
                    pane.Offset = sec.Offset;
                    pane.Size = sec.Size;
                    pane.Flags = Data[p];
                    pane.Origin = Data[p + 1];
                    pane.Alpha = Data[p + 2];
                    pane.Name = ReadFixedCString(Data, p + 4, 16);
                    pane.X = ReadF32(Data, p + 0x1C, LittleEndian);
                    pane.Y = ReadF32(Data, p + 0x20, LittleEndian);
                    pane.Z = ReadF32(Data, p + 0x24, LittleEndian);
                    pane.RotX = ReadF32(Data, p + 0x28, LittleEndian);
                    pane.RotY = ReadF32(Data, p + 0x2C, LittleEndian);
                    pane.RotZ = ReadF32(Data, p + 0x30, LittleEndian);
                    pane.ScaleX = ReadF32(Data, p + 0x34, LittleEndian);
                    pane.ScaleY = ReadF32(Data, p + 0x38, LittleEndian);
                    pane.Width = ReadF32(Data, p + 0x3C, LittleEndian);
                    pane.Height = ReadF32(Data, p + 0x40, LittleEndian);
                    if (sec.Magic == "pic1" && sec.Size >= 0x60)
                    {
                        pane.MaterialId = ReadU16(Data, sec.Offset + 0x5C, LittleEndian);
                        int texCoordCount = Data[sec.Offset + 0x5E];
                        int tcBase = sec.Offset + 0x60;
                        int tc;
                        for (tc = 0; tc < texCoordCount; tc++)
                        {
                            int q = tcBase + tc * 0x20;
                            if (q + 0x20 > sec.Offset + sec.Size || q + 0x20 > Data.Length)
                                break;
                            BrlytTexCoordSet uv = new BrlytTexCoordSet();
                            uv.U0 = ReadF32(Data, q + 0x00, LittleEndian);
                            uv.V0 = ReadF32(Data, q + 0x04, LittleEndian);
                            uv.U1 = ReadF32(Data, q + 0x08, LittleEndian);
                            uv.V1 = ReadF32(Data, q + 0x0C, LittleEndian);
                            uv.U2 = ReadF32(Data, q + 0x10, LittleEndian);
                            uv.V2 = ReadF32(Data, q + 0x14, LittleEndian);
                            uv.U3 = ReadF32(Data, q + 0x18, LittleEndian);
                            uv.V3 = ReadF32(Data, q + 0x1C, LittleEndian);
                            pane.TexCoords.Add(uv);
                        }
                    }
                    else if (sec.Magic == "txt1" && sec.Size >= 0x74)
                    {
                        pane.MaterialId = ReadU16(Data, sec.Offset + 0x50, LittleEndian);
                        pane.FontId = ReadU16(Data, sec.Offset + 0x52, LittleEndian);
                        pane.TextOrigin = Data[sec.Offset + 0x54];
                        pane.LineAlignment = Data[sec.Offset + 0x55];
                        uint textRel = ReadU32(Data, sec.Offset + 0x58, LittleEndian);
                        pane.FontWidth = ReadF32(Data, sec.Offset + 0x64, LittleEndian);
                        pane.FontHeight = ReadF32(Data, sec.Offset + 0x68, LittleEndian);
                        pane.CharSize = ReadF32(Data, sec.Offset + 0x6C, LittleEndian);
                        pane.LineSize = ReadF32(Data, sec.Offset + 0x70, LittleEndian);
                        long textAbs = (long)sec.Offset + textRel;
                        if (textAbs >= sec.Offset && textAbs + 1 < sec.Offset + sec.Size)
                            pane.EmbeddedText = ReadUtf16CString(Data, (int)textAbs, sec.Offset + sec.Size, LittleEndian);
                    }
                    else if (sec.Magic == "wnd1" && sec.Size >= 0x68)
                    {
                        uint contentRel = ReadU32(Data, sec.Offset + 0x60, LittleEndian);
                        long content = (long)sec.Offset + contentRel;
                        if (content >= sec.Offset && content + 0x12 <= sec.Offset + sec.Size)
                            pane.MaterialId = ReadU16(Data, (int)content + 0x10, LittleEndian);
                    }

                    if (parents.Count > 0)
                    {
                        pane.Parent = parents.Peek();
                        pane.Parent.Children.Add(pane);
                    }
                    else
                        RootPanes.Add(pane);
                    Panes.Add(pane);
                    last = pane;
                }
                else if (sec.Magic == "pas1")
                {
                    if (last != null)
                        parents.Push(last);
                    last = null;
                }
                else if (sec.Magic == "pae1")
                {
                    if (parents.Count > 0)
                        parents.Pop();
                    last = null;
                }
            }
        }

        public BrlytMaterialInfo MaterialForPane(BrlytPaneInfo pane)
        {
            if (pane == null || pane.MaterialId < 0 || pane.MaterialId >= Materials.Count)
                return null;
            return Materials[pane.MaterialId];
        }

        public void ApplyPane(BrlytPaneInfo pane)
        {
            if (pane == null)
                return;
            if (!IsAscii(pane.Name))
                throw new InvalidDataException("Pane names must use ASCII characters.");
            int p = pane.Offset + 8;
            if (p < 8 || p + 0x44 > Data.Length)
                throw new InvalidDataException("Pane offset is outside BRLYT.");
            Data[p] = pane.Flags;
            Data[p + 1] = pane.Origin;
            Data[p + 2] = pane.Alpha;
            WriteFixedAscii(Data, p + 4, 16, pane.Name);
            WriteF32(Data, p + 0x1C, pane.X, LittleEndian);
            WriteF32(Data, p + 0x20, pane.Y, LittleEndian);
            WriteF32(Data, p + 0x24, pane.Z, LittleEndian);
            WriteF32(Data, p + 0x28, pane.RotX, LittleEndian);
            WriteF32(Data, p + 0x2C, pane.RotY, LittleEndian);
            WriteF32(Data, p + 0x30, pane.RotZ, LittleEndian);
            WriteF32(Data, p + 0x34, pane.ScaleX, LittleEndian);
            WriteF32(Data, p + 0x38, pane.ScaleY, LittleEndian);
            WriteF32(Data, p + 0x3C, pane.Width, LittleEndian);
            WriteF32(Data, p + 0x40, pane.Height, LittleEndian);
            if (pane.Magic == "pic1" && pane.Offset + 0x5E <= Data.Length && pane.MaterialId >= 0 && pane.MaterialId <= UInt16.MaxValue)
                WriteU16(Data, pane.Offset + 0x5C, (ushort)pane.MaterialId, LittleEndian);
            else if (pane.Magic == "txt1" && pane.Offset + 0x74 <= Data.Length)
            {
                if (pane.MaterialId >= 0 && pane.MaterialId <= UInt16.MaxValue)
                    WriteU16(Data, pane.Offset + 0x50, (ushort)pane.MaterialId, LittleEndian);
                if (pane.FontId >= 0 && pane.FontId <= UInt16.MaxValue)
                    WriteU16(Data, pane.Offset + 0x52, (ushort)pane.FontId, LittleEndian);
                Data[pane.Offset + 0x54] = pane.TextOrigin;
                Data[pane.Offset + 0x55] = pane.LineAlignment;
                WriteF32(Data, pane.Offset + 0x64, pane.FontWidth, LittleEndian);
                WriteF32(Data, pane.Offset + 0x68, pane.FontHeight, LittleEndian);
                WriteF32(Data, pane.Offset + 0x6C, pane.CharSize, LittleEndian);
                WriteF32(Data, pane.Offset + 0x70, pane.LineSize, LittleEndian);
            }
            else if (pane.Magic == "wnd1" && pane.Offset + 0x68 <= Data.Length && pane.MaterialId >= 0 && pane.MaterialId <= UInt16.MaxValue)
            {
                uint contentRel = ReadU32(Data, pane.Offset + 0x60, LittleEndian);
                long content = (long)pane.Offset + contentRel;
                if (content >= pane.Offset && content + 0x12 <= pane.Offset + pane.Size && content + 0x12 <= Data.Length)
                    WriteU16(Data, (int)content + 0x10, (ushort)pane.MaterialId, LittleEndian);
            }
        }

        public void ApplyMaterialName(BrlytMaterialInfo material)
        {
            if (material == null)
                return;
            if (!IsAscii(material.Name))
                throw new InvalidDataException("Material names must use ASCII characters.");
            if (Encoding.ASCII.GetByteCount(material.Name ?? "") > 19)
                throw new InvalidDataException("Material names are limited to 19 ASCII characters in this safe editor.");
            WriteFixedAscii(Data, material.Offset, 20, material.Name);
            int i;
            for (i = 0; i < material.Bindings.Count; i++)
            {
                BrlytTextureBinding binding = material.Bindings[i];
                binding.MaterialName = material.Name;
                if (binding.Offset >= 0 && binding.Offset + 2 <= Data.Length)
                {
                    if (binding.TextureId >= Textures.Count)
                        throw new InvalidDataException("Texture ID is outside the BRLYT texture list.");
                    WriteU16(Data, binding.Offset, binding.TextureId, LittleEndian);
                    binding.TextureName = Textures[binding.TextureId];
                }
            }
        }

        public void Save(string path)
        {
            int i;
            for (i = 0; i < Panes.Count; i++)
                ApplyPane(Panes[i]);
            for (i = 0; i < Materials.Count; i++)
                ApplyMaterialName(Materials[i]);
            murumsWiiModStudio.BackupManager.WriteAllBytesSafely(path, Data);
            SourcePath = path;
        }

        private static bool IsAscii(string text)
        {
            if (text == null)
                return true;
            int i;
            for (i = 0; i < text.Length; i++)
                if (text[i] > 0x7F)
                    return false;
            return true;
        }

        private static string ReadAscii(byte[] data, int off, int count)
        {
            if (off < 0 || count < 0 || off + count > data.Length)
                return "";
            return Encoding.ASCII.GetString(data, off, count);
        }

        private static string ReadCString(byte[] data, int off, int limit)
        {
            int max = Math.Min(limit, data.Length);
            int e = off;
            while (e < max && data[e] != 0)
                e++;
            return e > off ? Encoding.ASCII.GetString(data, off, e - off) : "";
        }

        private static string ReadUtf16CString(byte[] data, int off, int limit, bool littleEndian)
        {
            if (off < 0 || off >= data.Length)
                return "";
            int max = Math.Min(limit, data.Length);
            int end = off;
            while (end + 1 < max)
            {
                if (data[end] == 0 && data[end + 1] == 0)
                    break;
                end += 2;
            }

            if (end <= off)
                return "";
            try
            {
                Encoding enc = littleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;
                return enc.GetString(data, off, end - off);
            }
            catch
            {
                return "";
            }
        }

        private static string ReadFixedCString(byte[] data, int off, int length)
        {
            if (off < 0 || off >= data.Length)
                return "";
            int end = Math.Min(data.Length, off + length);
            int e = off;
            while (e < end && data[e] != 0)
                e++;
            return Encoding.ASCII.GetString(data, off, e - off);
        }

        private static void WriteFixedAscii(byte[] data, int off, int length, string text)
        {
            if (off < 0 || off + length > data.Length)
                throw new InvalidDataException("String field is outside BRLYT.");
            byte[] bytes = Encoding.ASCII.GetBytes(text ?? "");
            if (bytes.Length >= length)
                throw new InvalidDataException("Text is too long for this BRLYT field.");
            int i;
            for (i = 0; i < length; i++)
                data[off + i] = 0;
            Buffer.BlockCopy(bytes, 0, data, off, bytes.Length);
        }

        private static ushort ReadU16BE(byte[] d, int p)
        {
            return (ushort)((d[p] << 8) | d[p + 1]);
        }

        private static ushort ReadU16(byte[] d, int p, bool le)
        {
            return le ? (ushort)(d[p] | (d[p + 1] << 8)) : ReadU16BE(d, p);
        }

        private static void WriteU16(byte[] d, int p, ushort value, bool le)
        {
            if (le)
            {
                d[p] = (byte)value;
                d[p + 1] = (byte)(value >> 8);
            }
            else
            {
                d[p] = (byte)(value >> 8);
                d[p + 1] = (byte)value;
            }
        }

        private static uint ReadU32(byte[] d, int p, bool le)
        {
            if (le)
                return (uint)(d[p] | (d[p + 1] << 8) | (d[p + 2] << 16) | (d[p + 3] << 24));
            return ((uint)d[p] << 24) | ((uint)d[p + 1] << 16) | ((uint)d[p + 2] << 8) | d[p + 3];
        }

        private static float ReadF32(byte[] d, int p, bool le)
        {
            byte[] b = new byte[4];
            Buffer.BlockCopy(d, p, b, 0, 4);
            if (BitConverter.IsLittleEndian != le)
                Array.Reverse(b);
            return BitConverter.ToSingle(b, 0);
        }

        private static void WriteF32(byte[] d, int p, float value, bool le)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian != le)
                Array.Reverse(b);
            Buffer.BlockCopy(b, 0, d, p, 4);
        }
    }
}
