using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal static class GlobePictureBackground
    {
        static int U(byte[] b, int p)
        {
            if (p < 0 || p > b.Length - 4)
                throw new InvalidDataException("Truncated globe template.");
            return checked((int)BigEndian.ReadUInt32(b, p));
        }

        static void F(byte[] b, int p, float f)
        {
            byte[] v = BitConverter.GetBytes(f);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(v);
            Buffer.BlockCopy(v, 0, b, p, 4);
        }

        static int Find(byte[] b, string group, string name)
        {
            int root = (b[12] << 8) | b[13];
            foreach (var g in BrresModelVisibility.Dictionary(b, root + 8))
                if (g.Name == group)
                    foreach (var e in BrresModelVisibility.Dictionary(b, g.Data))
                        if (e.Name == name)
                            return e.Data;
            throw new InvalidDataException("Missing globe template resource: " + name);
        }

        // Stretch maps every source pixel into the complete output rectangle; crop is optional.
        static void Draw(Graphics g, Image source, Rectangle target, bool stretch)
        {
            float scale = Math.Max((float)target.Width / source.Width, (float)target.Height / source.Height);
            var destination = stretch ? new RectangleF(target.X, target.Y, target.Width, target.Height) : new RectangleF(target.X + (target.Width - source.Width * scale) / 2, target.Y + (target.Height - source.Height * scale) / 2, source.Width * scale, source.Height * scale);
            var save = g.Save();
            g.SetClip(target);
            using (var attributes = new ImageAttributes())
            {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source, Rectangle.Round(destination), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }

            g.Restore(save);
        }

        public static Bitmap Preview(string path, bool stretch)
        {
            using (var image = Image.FromFile(path))
            {
                var b = new Bitmap(512, 288);
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Draw(g, image, new Rectangle(0, 0, b.Width, b.Height), stretch);
                }

                return b;
            }
        }

        static byte[] LoadTemplate(bool animated)
        {
            string name = animated ? "SkyAnimated.brres.gz" : "SkyStill.brres.gz";
            using (var resource = typeof(GlobePictureBackground).Assembly.GetManifestResourceStream("Studio." + name))
            {
                if (resource == null)
                    throw new InvalidDataException(L.T(
                        "Die Bildvorlage fehlt im Programm. Bitte Studio erneut installieren.",
                        "The program's picture template is missing. Please reinstall Studio."));
                using (var compressed = new GZipStream(resource, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    compressed.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        public static byte[] Build(string path, bool firstFrameOnly, bool stretch, out bool animated)
        {
            using (var image = Image.FromFile(path))
            {
                var dimension = new FrameDimension(image.FrameDimensionsList[0]);
                int count = image.GetFrameCount(dimension);
                animated = !firstFrameOnly && image.RawFormat.Guid == ImageFormat.Gif.Guid && count > 1;
                byte[] b = LoadTemplate(animated);
                int tex = Find(b, "Textures(NW4R)", "galaxy3_2s");
                int width = (b[tex + 28] << 8) | b[tex + 29], height = (b[tex + 30] << 8) | b[tex + 31];
                if (width != 1024 || height != (animated ? 1024 : 576) || U(b, tex + 32) != 14 || U(b, tex + 36) != 1)
                    throw new InvalidDataException("Unsupported globe picture template.");
                int total = 0;
                int[] delays = new int[count];
                byte[] timing = null;
                try
                {
                    timing = image.GetPropertyItem(0x5100).Value;
                }
                catch (ArgumentException)
                {
                }

                for (int i = 0; i < count; i++)
                {
                    int delay = timing != null && timing.Length >= i * 4 + 4 ? BitConverter.ToInt32(timing, i * 4) : 10;
                    delays[i] = Math.Max(2, Math.Min(6000, delay));
                    total = checked(total + delays[i]);
                }

                using (var canvas = new Bitmap(width, height))
                {
                    using (var g = Graphics.FromImage(canvas))
                    {
                        g.Clear(Color.Black);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        if (!animated)
                            Draw(g, image, new Rectangle(0, 0, width, height), stretch);
                        else
                            for (int sample = 0; sample < 8; sample++)
                            {
                                int time = (int)((long)total * sample / 8), frame = 0, elapsed = delays[0];
                                while (frame < count - 1 && elapsed <= time)
                                    elapsed += delays[++frame];
                                image.SelectActiveFrame(dimension, frame);
                                Draw(g, image, new Rectangle((sample % 2) * 512, (sample / 2) * 256, 512, 256), stretch);
                            }
                    }

                    byte[] tpl = TplEncoder.Encode(canvas, TplPixelFormat.CMPR);
                    int header = U(tpl, 12), start = U(tpl, header + 8), destination = checked(tex + U(b, tex + 16)), length = tpl.Length - start;
                    if (destination < tex || destination + length > tex + U(b, tex + 4) || destination + length > b.Length)
                        throw new InvalidDataException("Globe image exceeds texture resource.");
                    Buffer.BlockCopy(tpl, start, b, destination, length);
                }

                if (animated)
                {
                    int srt = Find(b, "AnmTexSrt(NW4R)", "ef_galaxy");
                    if (U(b, srt + 8) != 5)
                        throw new InvalidDataException("Unsupported globe animation revision.");
                    int frames = Math.Max(8, Math.Min(65534, (int)Math.Round(total * 0.6)));
                    b[srt + 32] = (byte)(frames >> 8);
                    b[srt + 33] = (byte)frames;
                    var entries = BrresModelVisibility.Dictionary(b, srt + U(b, srt + 16));
                    if (entries.Count != 1)
                        throw new InvalidDataException("Unsupported globe animation entries.");
                    int mat = entries[0].Data, anim = mat + U(b, mat + 12);
                    if (U(b, anim) != 0xe5)
                        throw new InvalidDataException("Unsupported globe animation tracks.");
                    for (int channel = 0; channel < 2; channel++)
                    {
                        int field = anim + 12 + channel * 4, keys = field + U(b, field);
                        if ((b[keys] << 8 | b[keys + 1]) != 16 || keys + 8 + 16 * 12 > b.Length)
                            throw new InvalidDataException("Unsupported globe keyframes.");
                        F(b, keys + 4, 1f / frames);
                        for (int i = 0; i < 16; i++)
                        {
                            float time = (i % 2 == 0) ? (i / 2) * frames / 8f : ((i / 2) + 1) * frames / 8f - 0.001f;
                            F(b, keys + 8 + i * 12, time);
                            F(b, keys + 16 + i * 12, 0);
                        }
                    }
                }

                return b;
            }
        }
    }
}
