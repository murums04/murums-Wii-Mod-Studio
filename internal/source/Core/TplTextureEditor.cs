using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal sealed class TplTextureInfo
    {
        public int ImageHeaderOffset;
        public int Width;
        public int Height;
        public int Format;
        public int DataOffset;
        public int PayloadLength;
        public bool HasMipMaps;
        public int MaxLod;
        public string FormatName
        {
            get
            {
                switch (Format)
                {
                    case 0:
                        return "I4";
                    case 1:
                        return "I8";
                    case 2:
                        return "IA4";
                    case 3:
                        return "IA8";
                    case 4:
                        return "RGB565";
                    case 5:
                        return "RGB5A3";
                    case 6:
                        return "RGBA32";
                    case 8:
                        return "CI4";
                    case 9:
                        return "CI8";
                    case 10:
                        return "CI14X2";
                    case 14:
                        return "CMPR";
                    default:
                        return "Format " + Format.ToString();
                }
            }
        }
    }

    internal static partial class TplTextureEditor
    {
        private const uint TplMagic = 0x0020AF30u;
        public static bool CanReplaceImage(byte[] data, int imageIndex)
        {
            try
            {
                int format = GetImageInfo(data, imageIndex).Format;
                return format >= 0 && format <= 6 || format == 8 || format == 9 || format == 14;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTpl(byte[] data)
        {
            return data != null && data.Length >= 12 && ReadU32(data, 0) == TplMagic;
        }

        public static TplTextureInfo GetFirstImageInfo(byte[] data)
        {
            return GetImageInfo(data, 0);
        }

        public static TplTextureInfo GetImageInfo(byte[] data, int imageIndex)
        {
            if (!IsTpl(data))
                throw new InvalidDataException(L.T("Die ausgewählte Datei ist keine gültige TPL.", "The selected file is not a valid TPL."));
            uint count = ReadU32(data, 4);
            int table = checked((int)ReadU32(data, 8));
            if (count == 0)
                throw new InvalidDataException(L.T("Die TPL enthält keine Bilder.", "The TPL contains no images."));
            if (imageIndex < 0 || imageIndex >= count)
                throw new InvalidDataException("Invalid TPL image index.");
            if (table < 12 || (long)table + (long)count * 8 > data.Length)
                throw new InvalidDataException(L.T("Die TPL-Bildtabelle ist ungültig.", "The TPL image table is invalid."));
            table = checked(table + imageIndex * 8);
            int imageHeader = checked((int)ReadU32(data, table));
            if (imageHeader < 0 || (long)imageHeader + 0x24 > data.Length)
                throw new InvalidDataException(L.T("Der TPL-Bildheader ist ungültig.", "The TPL image header is invalid."));
            int height = ReadU16(data, imageHeader + 0x00);
            int width = ReadU16(data, imageHeader + 0x02);
            int format = checked((int)ReadU32(data, imageHeader + 0x04));
            int dataOffset = checked((int)ReadU32(data, imageHeader + 0x08));
            if (width <= 0 || height <= 0)
                throw new InvalidDataException(L.T("Ungültige TPL-Bildgrösse.", "Invalid TPL image size."));
            int payload = GetBaseLevelPayloadLength(width, height, format);
            if (payload <= 0)
                throw new NotSupportedException(L.T("Dieses TPL-Format wird für 'Bild importieren' noch nicht unterstützt: ", "This TPL format is not yet supported by Import Texture: ") + GetFormatName(format));
            if (format == 8 || format == 9)
                ValidatePalette(data, table, format);
            int maxLod = data[imageHeader + 0x22];
            int maxPossible = 0;
            for (int dimension = Math.Max(width, height); dimension > 1; dimension >>= 1)
                maxPossible++;
            if (maxLod > maxPossible)
                throw new InvalidDataException("Invalid TPL mipmap count.");
            long totalPayload = payload;
            for (int level = 1; level <= maxLod; level++)
                totalPayload += GetBaseLevelPayloadLength(Math.Max(1, width >> level), Math.Max(1, height >> level), format);
            if (dataOffset < 0 || (long)dataOffset + totalPayload > data.Length)
                throw new InvalidDataException(L.T("Die TPL-Bilddaten liegen ausserhalb der Datei.", "The TPL image data is outside the file."));
            TplTextureInfo info = new TplTextureInfo();
            info.ImageHeaderOffset = imageHeader;
            info.Width = width;
            info.Height = height;
            info.Format = format;
            info.DataOffset = dataOffset;
            info.PayloadLength = payload;
            info.HasMipMaps = data[imageHeader + 0x22] != 0;
            info.MaxLod = maxLod;
            return info;
        }

        public static Bitmap LoadSourceBitmap(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            TexturePreviewResult decoded;
            string error;
            if (TexturePreview.TryDecode(Path.GetFileName(path), data, 0, out decoded, out error) && decoded != null && decoded.Bitmap != null)
            {
                try
                {
                    return new Bitmap(decoded.Bitmap);
                }
                finally
                {
                    decoded.Dispose();
                }
            }

            throw new InvalidDataException(error == null ? L.T("Das Quellbild konnte nicht gelesen werden.", "The source image could not be read.") : L.T("Das Quellbild konnte nicht gelesen werden: ", "The source image could not be read: ") + error);
        }

        public static byte[] ReplaceFirstImage(byte[] targetTpl, Bitmap source, bool resizeToTarget)
        {
            return ReplaceImage(targetTpl, source, resizeToTarget, 0);
        }

        public static byte[] ReplaceImage(byte[] targetTpl, Bitmap source, bool resizeToTarget, int imageIndex)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            TplTextureInfo info = GetImageInfo(targetTpl, imageIndex);
            Bitmap prepared = source;
            Bitmap resized = null;
            if (source.Width != info.Width || source.Height != info.Height)
            {
                if (!resizeToTarget)
                    throw new InvalidOperationException(L.F("Das Quellbild ist {0}×{1}, die Ziel-TPL jedoch {2}×{3}.", "The source image is {0}×{1}, but the target TPL is {2}×{3}.", source.Width, source.Height, info.Width, info.Height));
                resized = Resize(source, info.Width, info.Height);
                prepared = resized;
            }

            try
            {
                if (info.Format == 8 || info.Format == 9)
                    return ReplaceIndexed(targetTpl, prepared, info, imageIndex);
                byte[] texels = EncodeBaseLevel(prepared, info.Format);
                if (texels.Length != info.PayloadLength)
                    throw new InvalidDataException(L.T("Die erzeugte TPL-Datenmenge stimmt nicht mit der Zieltextur überein.", "The encoded TPL payload does not match the target texture."));
                byte[] output = (byte[])targetTpl.Clone();
                Buffer.BlockCopy(texels, 0, output, info.DataOffset, texels.Length);
                int offset = info.DataOffset + texels.Length;
                for (int level = 1; level <= info.MaxLod; level++)
                {
                    using (Bitmap mip = Resize(prepared, Math.Max(1, info.Width >> level), Math.Max(1, info.Height >> level)))
                    {
                        byte[] mipTexels = EncodeBaseLevel(mip, info.Format);
                        Buffer.BlockCopy(mipTexels, 0, output, offset, mipTexels.Length);
                        offset += mipTexels.Length;
                    }
                }

                return output;
            }
            finally
            {
                if (resized != null)
                    resized.Dispose();
            }
        }

        private static Bitmap Resize(Bitmap source, int width, int height)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }

            return result;
        }

        internal static int GetBaseLevelPayloadLength(int width, int height, int format)
        {
            checked
            {
                switch (format)
                {
                    case 0:
                        return Blocks(width, 8) * Blocks(height, 8) * 32; // I4
                    case 1:
                        return Blocks(width, 8) * Blocks(height, 4) * 32; // I8
                    case 2:
                        return Blocks(width, 8) * Blocks(height, 4) * 32; // IA4
                    case 3:
                        return Blocks(width, 4) * Blocks(height, 4) * 32; // IA8
                    case 4:
                        return Blocks(width, 4) * Blocks(height, 4) * 32; // RGB565
                    case 5:
                        return Blocks(width, 4) * Blocks(height, 4) * 32; // RGB5A3
                    case 6:
                        return Blocks(width, 4) * Blocks(height, 4) * 64; // RGBA32
                    case 8:
                        return Blocks(width, 8) * Blocks(height, 8) * 32;
                    case 9:
                        return Blocks(width, 8) * Blocks(height, 4) * 32;
                    case 14:
                        return Blocks(width, 8) * Blocks(height, 8) * 32; // CMPR
                    default:
                        return -1; // CI formats require palette rebuilding.
                }
            }
        }

        private static int Blocks(int size, int block)
        {
            return (size + block - 1) / block;
        }

        private static byte[] EncodeBaseLevel(Bitmap bitmap, int format)
        {
            PixelReader px = new PixelReader(bitmap);
            try
            {
                switch (format)
                {
                    case 0:
                        return EncodeI4(px);
                    case 1:
                        return EncodeI8(px);
                    case 2:
                        return EncodeIA4(px);
                    case 3:
                        return EncodeIA8(px);
                    case 4:
                        return EncodeRgb16(px, false);
                    case 5:
                        return EncodeRgb16(px, true);
                    case 6:
                        return EncodeRgba32(px);
                    case 14:
                        return EncodeCmpr(px);
                    default:
                        throw new NotSupportedException(L.T("Dieses TPL-Format kann noch nicht neu kodiert werden: ", "This TPL format cannot yet be re-encoded: ") + GetFormatName(format));
                }
            }
            finally
            {
                px.Dispose();
            }
        }

        private static byte[] EncodeI4(PixelReader px)
        {
            byte[] output = new byte[Blocks(px.Width, 8) * Blocks(px.Height, 8) * 32];
            int p = 0;
            for (int by = 0; by < px.Height; by += 8)
                for (int bx = 0; bx < px.Width; bx += 8)
                    for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x += 2)
                        {
                            byte i0 = (byte)(Intensity(px.Get(bx + x, by + y)) >> 4);
                            byte i1 = (byte)(Intensity(px.Get(bx + x + 1, by + y)) >> 4);
                            output[p++] = (byte)((i0 << 4) | i1);
                        }

            return output;
        }

        private static byte[] EncodeI8(PixelReader px)
        {
            byte[] output = new byte[Blocks(px.Width, 8) * Blocks(px.Height, 4) * 32];
            int p = 0;
            for (int by = 0; by < px.Height; by += 4)
                for (int bx = 0; bx < px.Width; bx += 8)
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 8; x++)
                            output[p++] = Intensity(px.Get(bx + x, by + y));
            return output;
        }

        private static byte[] EncodeIA4(PixelReader px)
        {
            byte[] output = new byte[Blocks(px.Width, 8) * Blocks(px.Height, 4) * 32];
            int p = 0;
            for (int by = 0; by < px.Height; by += 4)
                for (int bx = 0; bx < px.Width; bx += 8)
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            Color c = px.Get(bx + x, by + y);
                            output[p++] = (byte)(((c.A >> 4) << 4) | (Intensity(c) >> 4));
                        }

            return output;
        }

        private static byte[] EncodeIA8(PixelReader px)
        {
            byte[] output = new byte[Blocks(px.Width, 4) * Blocks(px.Height, 4) * 32];
            int p = 0;
            for (int by = 0; by < px.Height; by += 4)
                for (int bx = 0; bx < px.Width; bx += 4)
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            Color c = px.Get(bx + x, by + y);
                            output[p++] = c.A;
                            output[p++] = Intensity(c);
                        }

            return output;
        }

        private static byte[] EncodeRgb16(PixelReader px, bool rgb5a3)
        {
            byte[] output = new byte[Blocks(px.Width, 4) * Blocks(px.Height, 4) * 32];
            int p = 0;
            for (int by = 0; by < px.Height; by += 4)
                for (int bx = 0; bx < px.Width; bx += 4)
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            Color c = px.Get(bx + x, by + y);
                            ushort v = rgb5a3 ? EncodeRgb5A3(c.R, c.G, c.B, c.A) : EncodeRgb565(c.R, c.G, c.B);
                            output[p++] = (byte)(v >> 8);
                            output[p++] = (byte)v;
                        }

            return output;
        }

        private static byte[] EncodeRgba32(PixelReader px)
        {
            byte[] output = new byte[Blocks(px.Width, 4) * Blocks(px.Height, 4) * 64];
            int p = 0;
            for (int by = 0; by < px.Height; by += 4)
                for (int bx = 0; bx < px.Width; bx += 4)
                {
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            Color c = px.Get(bx + x, by + y);
                            output[p++] = c.A;
                            output[p++] = c.R;
                        }

                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            Color c = px.Get(bx + x, by + y);
                            output[p++] = c.G;
                            output[p++] = c.B;
                        }
                }

            return output;
        }

        private static byte[] EncodeCmpr(PixelReader px)
        {
            byte[] output = new byte[Blocks(px.Width, 8) * Blocks(px.Height, 8) * 32];
            int outPos = 0;
            for (int by = 0; by < px.Height; by += 8)
                for (int bx = 0; bx < px.Width; bx += 8)
                    for (int subY = 0; subY < 8; subY += 4)
                        for (int subX = 0; subX < 8; subX += 4)
                        {
                            byte[] rgba = new byte[64];
                            bool hasAlpha = false;
                            int n = 0;
                            for (int y = 0; y < 4; y++)
                                for (int x = 0; x < 4; x++)
                                {
                                    Color c = px.Get(bx + subX + x, by + subY + y);
                                    if (c.A < 128)
                                        hasAlpha = true;
                                    rgba[n * 4 + 0] = c.R;
                                    rgba[n * 4 + 1] = c.G;
                                    rgba[n * 4 + 2] = c.B;
                                    rgba[n * 4 + 3] = c.A;
                                    n++;
                                }

                            ushort c0, c1;
                            ChooseCmprEndpoints(rgba, hasAlpha, out c0, out c1);
                            output[outPos++] = (byte)(c0 >> 8);
                            output[outPos++] = (byte)c0;
                            output[outPos++] = (byte)(c1 >> 8);
                            output[outPos++] = (byte)c1;
                            byte[, ] palette = BuildCmprPalette(c0, c1);
                            for (int row = 0; row < 4; row++)
                            {
                                byte packed = 0;
                                for (int col = 0; col < 4; col++)
                                {
                                    int pixelIndex = row * 4 + col;
                                    int idx = hasAlpha && rgba[pixelIndex * 4 + 3] < 128 ? 3 : FindNearestCmprColor(rgba, pixelIndex, palette, hasAlpha ? 3 : 4);
                                    packed |= (byte)((idx & 3) << (6 - col * 2));
                                }

                                output[outPos++] = packed;
                            }
                        }

            return output;
        }

        private static byte Intensity(Color c)
        {
            int v = (c.R * 299 + c.G * 587 + c.B * 114 + 500) / 1000;
            if (v < 0)
                v = 0;
            if (v > 255)
                v = 255;
            return (byte)v;
        }

        private static void ChooseCmprEndpoints(byte[] rgba, bool hasAlpha, out ushort c0, out ushort c1)
        {
            int bestA = -1, bestB = -1, bestDistance = -1;
            for (int i = 0; i < 16; i++)
            {
                if (rgba[i * 4 + 3] < 128)
                    continue;
                for (int j = i + 1; j < 16; j++)
                {
                    if (rgba[j * 4 + 3] < 128)
                        continue;
                    int dr = rgba[i * 4 + 0] - rgba[j * 4 + 0];
                    int dg = rgba[i * 4 + 1] - rgba[j * 4 + 1];
                    int db = rgba[i * 4 + 2] - rgba[j * 4 + 2];
                    int d = dr * dr + dg * dg + db * db;
                    if (d > bestDistance)
                    {
                        bestDistance = d;
                        bestA = i;
                        bestB = j;
                    }
                }
            }

            if (bestA < 0)
            {
                c0 = 0;
                c1 = 0;
                return;
            }

            if (bestB < 0)
                bestB = bestA;
            ushort a = EncodeRgb565(rgba[bestA * 4], rgba[bestA * 4 + 1], rgba[bestA * 4 + 2]);
            ushort b = EncodeRgb565(rgba[bestB * 4], rgba[bestB * 4 + 1], rgba[bestB * 4 + 2]);
            if (hasAlpha)
            {
                if (a <= b)
                {
                    c0 = a;
                    c1 = b;
                }
                else
                {
                    c0 = b;
                    c1 = a;
                }
            }
            else
            {
                if (a > b)
                {
                    c0 = a;
                    c1 = b;
                }
                else
                {
                    c0 = b;
                    c1 = a;
                }

                if (c0 == c1)
                {
                    if (c0 < 0xFFFF)
                        c0++;
                    else if (c1 > 0)
                        c1--;
                }
            }
        }

        private static byte[, ] BuildCmprPalette(ushort c0, ushort c1)
        {
            byte[, ] p = new byte[4, 4];
            byte r, g, b;
            DecodeRgb565(c0, out r, out g, out b);
            p[0, 0] = r;
            p[0, 1] = g;
            p[0, 2] = b;
            p[0, 3] = 255;
            DecodeRgb565(c1, out r, out g, out b);
            p[1, 0] = r;
            p[1, 1] = g;
            p[1, 2] = b;
            p[1, 3] = 255;
            if (c0 > c1)
            {
                for (int c = 0; c < 3; c++)
                {
                    p[2, c] = (byte)((2 * p[0, c] + p[1, c]) / 3);
                    p[3, c] = (byte)((p[0, c] + 2 * p[1, c]) / 3);
                }

                p[2, 3] = p[3, 3] = 255;
            }
            else
            {
                for (int c = 0; c < 3; c++)
                    p[2, c] = (byte)((p[0, c] + p[1, c]) / 2);
                p[2, 3] = 255;
                p[3, 0] = p[3, 1] = p[3, 2] = p[3, 3] = 0;
            }

            return p;
        }

        private static int FindNearestCmprColor(byte[] rgba, int pixelIndex, byte[, ] palette, int count)
        {
            int r = rgba[pixelIndex * 4], g = rgba[pixelIndex * 4 + 1], b = rgba[pixelIndex * 4 + 2];
            int best = 0, bestDist = Int32.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int dr = r - palette[i, 0], dg = g - palette[i, 1], db = b - palette[i, 2];
                int d = dr * dr + dg * dg + db * db;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        private static ushort EncodeRgb565(byte r, byte g, byte b)
        {
            return (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
        }

        private static ushort EncodeRgb5A3(byte r, byte g, byte b, byte a)
        {
            if (a >= 224)
                return (ushort)(0x8000 | ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));
            return (ushort)(((a >> 5) << 12) | ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4));
        }

        private static void DecodeRgb565(ushort value, out byte r, out byte g, out byte b)
        {
            int r5 = (value >> 11) & 31, g6 = (value >> 5) & 63, b5 = value & 31;
            r = (byte)((r5 << 3) | (r5 >> 2));
            g = (byte)((g6 << 2) | (g6 >> 4));
            b = (byte)((b5 << 3) | (b5 >> 2));
        }

        private static string GetFormatName(int format)
        {
            switch (format)
            {
                case 0:
                    return "I4";
                case 1:
                    return "I8";
                case 2:
                    return "IA4";
                case 3:
                    return "IA8";
                case 4:
                    return "RGB565";
                case 5:
                    return "RGB5A3";
                case 6:
                    return "RGBA32";
                case 8:
                    return "CI4";
                case 9:
                    return "CI8";
                case 10:
                    return "CI14X2";
                case 14:
                    return "CMPR";
                default:
                    return "Format " + format.ToString();
            }
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length)
                throw new EndOfStreamException();
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new EndOfStreamException();
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private sealed class PixelReader : IDisposable
        {
            private Bitmap _source;
            private Bitmap _converted;
            private BitmapData _lockData;
            private byte[] _pixels;
            private int _stride;
            private int _absStride;
            public int Width
            {
                get
                {
                    return _source.Width;
                }
            }

            public int Height
            {
                get
                {
                    return _source.Height;
                }
            }

            public PixelReader(Bitmap bitmap)
            {
                _source = bitmap;
                if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    _converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(_converted))
                        g.DrawImageUnscaled(bitmap, 0, 0);
                    _source = _converted;
                }

                Rectangle rect = new Rectangle(0, 0, _source.Width, _source.Height);
                _lockData = _source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                _stride = _lockData.Stride;
                _absStride = Math.Abs(_stride);
                _pixels = new byte[_absStride * _source.Height];
                Marshal.Copy(_lockData.Scan0, _pixels, 0, _pixels.Length);
            }

            public Color Get(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                    return Color.FromArgb(0, 0, 0, 0);
                int sourceY = _stride >= 0 ? y : (Height - 1 - y);
                int o = sourceY * _absStride + x * 4;
                return Color.FromArgb(_pixels[o + 3], _pixels[o + 2], _pixels[o + 1], _pixels[o]);
            }

            public void Dispose()
            {
                if (_lockData != null)
                {
                    _source.UnlockBits(_lockData);
                    _lockData = null;
                }

                if (_converted != null)
                {
                    _converted.Dispose();
                    _converted = null;
                }
            }
        }
    }
}
