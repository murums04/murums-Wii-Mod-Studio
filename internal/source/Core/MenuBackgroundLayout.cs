using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    // Explicit conversion of the stock MenuBG layout, never an unrelated obi/border.
    internal static class MenuBackgroundLayout
    {
        public const string TextureName = "murums_background.tpl";
        private static void U16(byte[] b, int p, int v)
        {
            b[p] = (byte)(v >> 8);
            b[p + 1] = (byte)v;
        }

        private static void U32(byte[] b, int p, int v)
        {
            BigEndian.WriteUInt32(b, p, (uint)v);
        }

        private static void F32(byte[] b, int p, float v)
        {
            byte[] n = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(n);
            Buffer.BlockCopy(n, 0, b, p, 4);
        }

        private static byte[] Section(string name, int size)
        {
            byte[] b = new byte[size];
            Encoding.ASCII.GetBytes(name).CopyTo(b, 0);
            U32(b, 4, size);
            return b;
        }

        public static byte[] Convert(byte[] input)
        {
            BrlytDocument doc = BrlytDocument.FromBytes(input);
            if (doc.LittleEndian)
                throw new InvalidDataException("Unsupported background byte order.");
            BrlytPaneInfo picture = null;
            foreach (var pane in doc.Panes)
                if (pane.Name == "line0" && pane.Magic == "pic1")
                    picture = pane;
            if (picture == null || picture.TexCoords.Count != 1 || picture.MaterialId < 0)
                throw new InvalidDataException("Expected MenuBG line0 picture was not found.");
            var material = doc.Materials[picture.MaterialId];
            if (material.Name != "line0" || material.Bindings.Count != 1)
                throw new InvalidDataException("Unsupported MenuBG material.");
            int textureId = material.Bindings[0].TextureId;
            // An older export flattened line0 to Z=0 while its bg_null parent stayed at
            // Z=-999. Restore that known broken output; otherwise retain the
            // input's depth so the picture stays in the menu camera's range.
            bool repairFlattenedDepth = doc.Textures[textureId] == TextureName && picture.Z == 0 && picture.Parent != null && picture.Parent.Name == "bg_null" && picture.Parent.Z == -999;
            if (repairFlattenedDepth)
                picture.Z = 998;
            doc.Textures[textureId] = TextureName;
            foreach (var pane in doc.Panes)
            {
                if (pane.Magic == "pic1")
                    pane.Visible = pane == picture;
                if (pane == picture)
                {
                    pane.X = pane.Y = pane.RotX = pane.RotY = pane.RotZ = 0;
                    pane.ScaleX = pane.ScaleY = 1;
                    pane.Origin = 4;
                    pane.Alpha = 255;
                    pane.Width = 800;
                    pane.Height = 500;
                }

                doc.ApplyPane(pane);
            }

            for (int i = 0; i < 16; i++)
                doc.Data[picture.Offset + 0x4c + i] = 255;
            float[] uv =
            {
                0,
                0,
                1,
                0,
                0,
                1,
                1,
                1
            };
            for (int i = 0; i < uv.Length; i++)
                F32(doc.Data, picture.Offset + 0x60 + 4 * i, uv[i]);
            int textSize = 12 + doc.Textures.Count * 8;
            foreach (string name in doc.Textures)
                textSize += Encoding.ASCII.GetByteCount(name) + 1;
            byte[] txl = Section("txl1", (textSize + 3) & ~3);
            U16(txl, 8, doc.Textures.Count);
            int cursor = 12 + doc.Textures.Count * 8;
            for (int i = 0; i < doc.Textures.Count; i++)
            {
                U32(txl, 12 + i * 8, cursor - 12);
                byte[] name = Encoding.ASCII.GetBytes(doc.Textures[i]);
                name.CopyTo(txl, cursor);
                cursor += name.Length + 1;
            }

            // A single texture with identity SRT and the SDK's default texture TEV setup.
            byte[] simple = new byte[0x5c];
            Encoding.ASCII.GetBytes("line0").CopyTo(simple, 0);
            for (int i = 0; i < 8; i++)
                U16(simple, 0x1c + i * 2, 255);
            for (int i = 0x2c; i < 0x3c; i++)
                simple[i] = 255;
            U32(simple, 0x3c, 0x111);
            U16(simple, 0x40, textureId);
            F32(simple, 0x50, 1);
            F32(simple, 0x54, 1);
            simple[0x58] = 1;
            simple[0x59] = 4;
            simple[0x5a] = 30;
            var sections = new List<byte[]>();
            int offset = doc.HeaderSize;
            for (int i = 0; i < doc.SectionCount; i++)
            {
                int size = checked((int)BigEndian.ReadUInt32(doc.Data, offset + 4));
                string magic = Encoding.ASCII.GetString(doc.Data, offset, 4);
                byte[] section = new byte[size];
                Buffer.BlockCopy(doc.Data, offset, section, 0, size);
                if (magic == "txl1")
                    section = txl;
                if (magic == "mat1")
                {
                    var mats = new List<byte[]>();
                    int total = 12 + doc.Materials.Count * 4;
                    for (int m = 0; m < doc.Materials.Count; m++)
                    {
                        int start = (int)BigEndian.ReadUInt32(section, 12 + m * 4);
                        int end = m + 1 < doc.Materials.Count ? (int)BigEndian.ReadUInt32(section, 16 + m * 4) : size;
                        byte[] data = new byte[end - start];
                        Buffer.BlockCopy(section, start, data, 0, data.Length);
                        if (m == picture.MaterialId)
                            data = simple;
                        mats.Add(data);
                        total += data.Length;
                    }

                    section = Section("mat1", total);
                    U16(section, 8, mats.Count);
                    int pos = 12 + mats.Count * 4;
                    for (int m = 0; m < mats.Count; m++)
                    {
                        U32(section, 12 + m * 4, pos);
                        mats[m].CopyTo(section, pos);
                        pos += mats[m].Length;
                    }
                }

                sections.Add(section);
                offset += size;
            }

            using (MemoryStream stream = new MemoryStream())
            {
                stream.Write(doc.Data, 0, doc.HeaderSize);
                foreach (byte[] section in sections)
                    stream.Write(section, 0, section.Length);
                byte[] result = stream.ToArray();
                U32(result, 8, result.Length);
                BrlytDocument.FromBytes(result);
                return result;
            }
        }
    }
}
