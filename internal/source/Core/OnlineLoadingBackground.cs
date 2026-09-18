using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal static class OnlineLoadingBackground
    {
        const string Name = "murums_wait_bg";
        static void W(byte[] b, int p, int n)
        {
            BigEndian.WriteUInt32(b, p, (uint)n);
        }

        static void S(byte[] b, int p, int n)
        {
            b[p] = (byte)(n >> 8);
            b[p + 1] = (byte)n;
        }

        static void F(byte[] b, int p, float n)
        {
            byte[] a = BitConverter.GetBytes(n);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(a);
            a.CopyTo(b, p);
        }

        static byte[] Section(string magic, int size)
        {
            byte[] b = new byte[size];
            Encoding.ASCII.GetBytes(magic).CopyTo(b, 0);
            W(b, 4, size);
            return b;
        }

        public static byte[] AddPicture(byte[] original)
        {
            var doc = BrlytDocument.FromBytes(original);
            if (doc.LittleEndian)
                throw new InvalidDataException("Unsupported layout byte order.");
            foreach (var p in doc.Panes)
                if (p.Name == Name)
                    return (byte[])original.Clone();
            if (doc.Panes.Count == 0 || doc.Panes[0].Name != "RootPane")
                throw new InvalidDataException("Expected message RootPane.");
            var textures = new List<string>(doc.Textures);
            textures.Add(Name + ".tpl");
            int len = 12 + textures.Count * 8;
            foreach (var t in textures)
                len += Encoding.ASCII.GetByteCount(t) + 1;
            var tx = Section("txl1", (len + 3) & ~3);
            S(tx, 8, textures.Count);
            int pos = 12 + textures.Count * 8;
            for (int i = 0; i < textures.Count; i++)
            {
                W(tx, 12 + i * 8, pos - 12);
                byte[] t = Encoding.ASCII.GetBytes(textures[i]);
                t.CopyTo(tx, pos);
                pos += t.Length + 1;
            }

            byte[] mat = new byte[0x5c];
            Encoding.ASCII.GetBytes(Name).CopyTo(mat, 0);
            for (int i = 0; i < 8; i++)
                S(mat, 0x1c + 2 * i, 255);
            for (int i = 0x2c; i < 0x3c; i++)
                mat[i] = 255;
            W(mat, 0x3c, 0x111);
            S(mat, 0x40, textures.Count - 1);
            F(mat, 0x50, 1);
            F(mat, 0x54, 1);
            mat[0x58] = 1;
            mat[0x59] = 4;
            mat[0x5a] = 30;
            byte[] pic = Section("pic1", 128);
            pic[8] = 1;
            pic[9] = 4;
            pic[10] = 255;
            Encoding.ASCII.GetBytes(Name).CopyTo(pic, 12);
            F(pic, 0x2c, -1);
            F(pic, 0x3c, 1);
            F(pic, 0x40, 1);
            F(pic, 0x44, 950);
            F(pic, 0x48, 600);
            for (int i = 0x4c; i < 0x5c; i++)
                pic[i] = 255;
            S(pic, 0x5c, doc.Materials.Count);
            pic[0x5e] = 1;
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
            for (int i = 0; i < 8; i++)
                F(pic, 0x60 + i * 4, uv[i]);
            var sections = new List<byte[]>();
            pos = doc.HeaderSize;
            bool inserted = false, hasMat = false, hasTx = false;
            for (int i = 0; i < doc.SectionCount; i++)
            {
                int size = checked((int)BigEndian.ReadUInt32(original, pos + 4));
                byte[] block = new byte[size];
                Buffer.BlockCopy(original, pos, block, 0, size);
                string magic = Encoding.ASCII.GetString(block, 0, 4);
                if (magic == "txl1")
                {
                    block = tx;
                    hasTx = true;
                }

                if (magic == "mat1")
                {
                    hasMat = true;
                    int count = doc.Materials.Count;
                    byte[] next = Section("mat1", size + 4 + mat.Length);
                    S(next, 8, count + 1);
                    for (int j = 0; j < count; j++)
                        W(next, 12 + 4 * j, checked((int)BigEndian.ReadUInt32(block, 12 + 4 * j)) + 4);
                    W(next, 12 + 4 * count, size + 4);
                    Buffer.BlockCopy(block, 12 + 4 * count, next, 16 + 4 * count, size - 12 - 4 * count);
                    mat.CopyTo(next, size + 4);
                    block = next;
                }

                sections.Add(block);
                if (magic == "pas1" && !inserted)
                {
                    sections.Add(pic);
                    inserted = true;
                }

                pos += size;
            }

            if (!inserted || !hasMat || !hasTx)
                throw new InvalidDataException("Unsupported message layout structure.");
            using (var stream = new MemoryStream())
            {
                stream.Write(original, 0, doc.HeaderSize);
                foreach (var b in sections)
                    stream.Write(b, 0, b.Length);
                byte[] result = stream.ToArray();
                W(result, 8, result.Length);
                S(result, 14, sections.Count);
                BrlytDocument.FromBytes(result);
                return result;
            }
        }

        static ArchiveEntry PathEntry(ArchiveEntry root, string path)
        {
            ArchiveEntry cur = root;
            if (cur.Children.Count == 1 && cur.Children[0].Name == ".")
                cur = cur.Children[0];
            foreach (string name in path.Split('/'))
            {
                cur = cur.FindChild(name);
                if (cur == null)
                    throw new InvalidDataException("Missing online resource: " + path);
            }

            return cur;
        }

        public static byte[] Build(byte[] input, string picture)
        {
            var a = U8Archive.Load(input);
            var layout = PathEntry(a.Root, "message_window/blyt/common_w017_message.brlyt");
            layout.Data = AddPicture(layout.Data);
            var images = PathEntry(a.Root, "message_window/timg");
            var target = images.FindChild(Name + ".tpl");
            if (target == null)
            {
                target = new ArchiveEntry(Name + ".tpl", false);
                images.AddChild(target);
            }

            using (var src = Image.FromFile(picture))
            using (var image = new Bitmap(800, 500))
            {
                using (var g = Graphics.FromImage(image))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Max(800f / src.Width, 500f / src.Height);
                    g.DrawImage(src, (800 - src.Width * scale) / 2, (500 - src.Height * scale) / 2, src.Width * scale, src.Height * scale);
                }

                target.Data = TplEncoder.Encode(image, TplPixelFormat.CMPR);
            }

            return Yaz0.Compress(a.BuildU8());
        }
    }
}
