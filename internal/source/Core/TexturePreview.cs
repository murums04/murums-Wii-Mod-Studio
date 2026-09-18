using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal sealed class TexturePreviewResult : IDisposable
    {
        public Bitmap Bitmap;
        public string TypeName;
        public string FormatName;
        public int Width;
        public int Height;
        public int ImageCount;
        public int ImageIndex;
        public string Extra;
        public void Dispose()
        {
            if (Bitmap != null)
            {
                Bitmap.Dispose();
                Bitmap = null;
            }
        }
    }

    internal static class TexturePreview
    {
        private const uint TplMagic = 0x0020AF30u;
        public static bool TryDecode(string fileName, byte[] data, int imageIndex, out TexturePreviewResult result, out string error)
        {
            result = null;
            error = null;
            if (data == null || data.Length == 0)
                return false;
            try
            {
                if (data.Length >= 12 && ReadU32(data, 0) == TplMagic)
                {
                    result = DecodeTpl(data, imageIndex);
                    return true;
                }

                string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff")
                {
                    using (MemoryStream ms = new MemoryStream(data, false))
                    using (Image image = Image.FromStream(ms, true, true))
                    {
                        Bitmap bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.DrawImageUnscaled(image, 0, 0);
                        }

                        result = new TexturePreviewResult();
                        result.Bitmap = bitmap;
                        result.TypeName = "Image";
                        result.FormatName = image.RawFormat.Guid == ImageFormat.Gif.Guid ? "GIF" : image.RawFormat.Guid == ImageFormat.Png.Guid ? "PNG" : image.RawFormat.Guid == ImageFormat.Jpeg.Guid ? "JPEG" : image.RawFormat.Guid == ImageFormat.Bmp.Guid ? "BMP" : ext.TrimStart('.').ToUpperInvariant();
                        result.Width = image.Width;
                        result.Height = image.Height;
                        result.ImageCount = 1;
                        result.ImageIndex = 0;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                result = null;
                return false;
            }

            return false;
        }

        private static TexturePreviewResult DecodeTpl(byte[] data, int requestedIndex)
        {
            if (data.Length < 0x14)
                throw new InvalidDataException("TPL header is truncated.");
            uint countU = ReadU32(data, 4);
            uint tableU = ReadU32(data, 8);
            if (countU == 0 || countU > 4096)
                throw new InvalidDataException("Invalid TPL image count.");
            int count = checked((int)countU);
            int table = checked((int)tableU);
            if (table < 0 || table + count * 8 > data.Length)
                throw new InvalidDataException("TPL image table is outside the file.");
            int index = requestedIndex;
            if (index < 0)
                index = 0;
            if (index >= count)
                index = count - 1;
            int entryOff = table + index * 8;
            int imageHeader = checked((int)ReadU32(data, entryOff));
            int paletteHeader = checked((int)ReadU32(data, entryOff + 4));
            if (imageHeader < 0 || imageHeader + 0x24 > data.Length)
                throw new InvalidDataException("TPL image header is outside the file.");
            int height = ReadU16(data, imageHeader + 0x00);
            int width = ReadU16(data, imageHeader + 0x02);
            int format = checked((int)ReadU32(data, imageHeader + 0x04));
            int dataOffset = checked((int)ReadU32(data, imageHeader + 0x08));
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
                throw new InvalidDataException("Invalid TPL dimensions.");
            if (dataOffset < 0 || dataOffset >= data.Length)
                throw new InvalidDataException("TPL image data offset is outside the file.");
            ushort[] palette = null;
            int paletteFormat = -1;
            if (format == 8 || format == 9 || format == 10)
            {
                if (paletteHeader <= 0 || paletteHeader + 12 > data.Length)
                    throw new InvalidDataException("Paletted TPL texture has no valid palette header.");
                int paletteCount = ReadU16(data, paletteHeader + 0x00);
                paletteFormat = checked((int)ReadU32(data, paletteHeader + 0x04));
                int paletteDataOffset = checked((int)ReadU32(data, paletteHeader + 0x08));
                if (paletteCount <= 0 || paletteCount > 16384 || paletteDataOffset < 0 || paletteDataOffset + paletteCount * 2 > data.Length)
                    throw new InvalidDataException("TPL palette data is outside the file.");
                palette = new ushort[paletteCount];
                int p;
                for (p = 0; p < paletteCount; p++)
                    palette[p] = (ushort)ReadU16(data, paletteDataOffset + p * 2);
            }

            PixelSurface surface = new PixelSurface(width, height);
            Bitmap bitmap = null;
            try
            {
                switch (format)
                {
                    case 0:
                        DecodeI4(data, dataOffset, surface);
                        break;
                    case 1:
                        DecodeI8(data, dataOffset, surface);
                        break;
                    case 2:
                        DecodeIA4(data, dataOffset, surface);
                        break;
                    case 3:
                        DecodeIA8(data, dataOffset, surface);
                        break;
                    case 4:
                        DecodeRgb565(data, dataOffset, surface);
                        break;
                    case 5:
                        DecodeRgb5A3(data, dataOffset, surface);
                        break;
                    case 6:
                        DecodeRgba32(data, dataOffset, surface);
                        break;
                    case 8:
                        DecodeCi4(data, dataOffset, surface, palette, paletteFormat);
                        break;
                    case 9:
                        DecodeCi8(data, dataOffset, surface, palette, paletteFormat);
                        break;
                    case 10:
                        DecodeCi14X2(data, dataOffset, surface, palette, paletteFormat);
                        break;
                    case 14:
                        DecodeCmpr(data, dataOffset, surface);
                        break;
                    default:
                        throw new NotSupportedException("TPL texture format " + format + " is not supported by the preview yet.");
                }

                bitmap = surface.ToBitmap();
            }
            catch
            {
                if (bitmap != null)
                    bitmap.Dispose();
                throw;
            }

            TexturePreviewResult result = new TexturePreviewResult();
            result.Bitmap = bitmap;
            result.TypeName = "TPL";
            result.FormatName = GetFormatName(format);
            result.Width = width;
            result.Height = height;
            result.ImageCount = count;
            result.ImageIndex = index;
            result.Extra = palette != null ? "Palette: " + GetPaletteFormatName(paletteFormat) + " • " + palette.Length + " colors" : null;
            return result;
        }

        private static void DecodeI4(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 8)
                for (bx = 0; bx < bmp.Width; bx += 8)
                    for (y = 0; y < 8; y++)
                        for (x = 0; x < 8; x += 2)
                        {
                            byte b = ReadByte(data, p++);
                            SetGray(bmp, bx + x, by + y, Expand4((b >> 4) & 0xF), 255);
                            SetGray(bmp, bx + x + 1, by + y, Expand4(b & 0xF), 255);
                        }
        }

        private static void DecodeI8(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 8)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 8; x++)
                            SetGray(bmp, bx + x, by + y, ReadByte(data, p++), 255);
        }

        private static void DecodeIA4(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 8)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 8; x++)
                        {
                            byte b = ReadByte(data, p++);
                            byte a = Expand4((b >> 4) & 0xF);
                            byte i = Expand4(b & 0xF);
                            SetGray(bmp, bx + x, by + y, i, a);
                        }
        }

        private static void DecodeIA8(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 4)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 4; x++)
                        {
                            byte a = ReadByte(data, p++);
                            byte i = ReadByte(data, p++);
                            SetGray(bmp, bx + x, by + y, i, a);
                        }
        }

        private static void DecodeRgb565(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 4)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 4; x++)
                        {
                            ushort v = (ushort)ReadU16(data, p);
                            p += 2;
                            Color c = ColorFromRgb565(v);
                            SetPixelSafe(bmp, bx + x, by + y, c);
                        }
        }

        private static void DecodeRgb5A3(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 4)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 4; x++)
                        {
                            ushort v = (ushort)ReadU16(data, p);
                            p += 2;
                            SetPixelSafe(bmp, bx + x, by + y, ColorFromRgb5A3(v));
                        }
        }

        private static void DecodeRgba32(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, y, x, i;
            byte[] a = new byte[16];
            byte[] r = new byte[16];
            byte[] g = new byte[16];
            byte[] b = new byte[16];
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 4)
                {
                    for (i = 0; i < 16; i++)
                    {
                        a[i] = ReadByte(data, p++);
                        r[i] = ReadByte(data, p++);
                    }

                    for (i = 0; i < 16; i++)
                    {
                        g[i] = ReadByte(data, p++);
                        b[i] = ReadByte(data, p++);
                    }

                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 4; x++)
                        {
                            i = y * 4 + x;
                            SetPixelSafe(bmp, bx + x, by + y, Color.FromArgb(a[i], r[i], g[i], b[i]));
                        }
                }
        }

        private static void DecodeCi4(byte[] data, int start, PixelSurface bmp, ushort[] palette, int paletteFormat)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 8)
                for (bx = 0; bx < bmp.Width; bx += 8)
                    for (y = 0; y < 8; y++)
                        for (x = 0; x < 8; x += 2)
                        {
                            byte b = ReadByte(data, p++);
                            SetPalettePixel(bmp, bx + x, by + y, (b >> 4) & 0xF, palette, paletteFormat);
                            SetPalettePixel(bmp, bx + x + 1, by + y, b & 0xF, palette, paletteFormat);
                        }
        }

        private static void DecodeCi8(byte[] data, int start, PixelSurface bmp, ushort[] palette, int paletteFormat)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 8)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 8; x++)
                            SetPalettePixel(bmp, bx + x, by + y, ReadByte(data, p++), palette, paletteFormat);
        }

        private static void DecodeCi14X2(byte[] data, int start, PixelSurface bmp, ushort[] palette, int paletteFormat)
        {
            int p = start;
            int by, bx, y, x;
            for (by = 0; by < bmp.Height; by += 4)
                for (bx = 0; bx < bmp.Width; bx += 4)
                    for (y = 0; y < 4; y++)
                        for (x = 0; x < 4; x++)
                        {
                            int idx = ReadU16(data, p) & 0x3FFF;
                            p += 2;
                            SetPalettePixel(bmp, bx + x, by + y, idx, palette, paletteFormat);
                        }
        }

        private static void DecodeCmpr(byte[] data, int start, PixelSurface bmp)
        {
            int p = start;
            int by, bx, sy, sx, row, col;
            for (by = 0; by < bmp.Height; by += 8)
                for (bx = 0; bx < bmp.Width; bx += 8)
                    for (sy = 0; sy < 8; sy += 4)
                        for (sx = 0; sx < 8; sx += 4)
                        {
                            ushort c0 = (ushort)ReadU16(data, p);
                            p += 2;
                            ushort c1 = (ushort)ReadU16(data, p);
                            p += 2;
                            Color[] pal = BuildCmprPalette(c0, c1);
                            for (row = 0; row < 4; row++)
                            {
                                byte bits = ReadByte(data, p++);
                                for (col = 0; col < 4; col++)
                                {
                                    int idx = (bits >> (6 - col * 2)) & 3;
                                    SetPixelSafe(bmp, bx + sx + col, by + sy + row, pal[idx]);
                                }
                            }
                        }
        }

        private static Color[] BuildCmprPalette(ushort c0, ushort c1)
        {
            Color a = ColorFromRgb565(c0);
            Color b = ColorFromRgb565(c1);
            Color[] p = new Color[4];
            p[0] = a;
            p[1] = b;
            if (c0 > c1)
            {
                p[2] = Color.FromArgb(255, (2 * a.R + b.R) / 3, (2 * a.G + b.G) / 3, (2 * a.B + b.B) / 3);
                p[3] = Color.FromArgb(255, (a.R + 2 * b.R) / 3, (a.G + 2 * b.G) / 3, (a.B + 2 * b.B) / 3);
            }
            else
            {
                p[2] = Color.FromArgb(255, (a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2);
                p[3] = Color.FromArgb(0, 0, 0, 0);
            }

            return p;
        }

        private static void SetPalettePixel(PixelSurface bmp, int x, int y, int index, ushort[] palette, int paletteFormat)
        {
            if (palette == null || index < 0 || index >= palette.Length)
            {
                SetPixelSafe(bmp, x, y, Color.Magenta);
                return;
            }

            ushort v = palette[index];
            Color c;
            if (paletteFormat == 0)
            {
                byte a = (byte)(v >> 8);
                byte i = (byte)v;
                c = Color.FromArgb(a, i, i, i);
            }
            else if (paletteFormat == 1)
                c = ColorFromRgb565(v);
            else
                c = ColorFromRgb5A3(v);
            SetPixelSafe(bmp, x, y, c);
        }

        private static Color ColorFromRgb565(ushort v)
        {
            int r5 = (v >> 11) & 31, g6 = (v >> 5) & 63, b5 = v & 31;
            int r = (r5 << 3) | (r5 >> 2), g = (g6 << 2) | (g6 >> 4), b = (b5 << 3) | (b5 >> 2);
            return Color.FromArgb(255, r, g, b);
        }

        private static Color ColorFromRgb5A3(ushort v)
        {
            if ((v & 0x8000) != 0)
            {
                int r5 = (v >> 10) & 31, g5 = (v >> 5) & 31, b5 = v & 31;
                return Color.FromArgb(255, Expand5(r5), Expand5(g5), Expand5(b5));
            }

            int a3 = (v >> 12) & 7, r4 = (v >> 8) & 15, g4 = (v >> 4) & 15, b4 = v & 15;
            int a = (a3 * 255 + 3) / 7;
            return Color.FromArgb(a, Expand4(r4), Expand4(g4), Expand4(b4));
        }

        private static int Expand5(int v)
        {
            return (v << 3) | (v >> 2);
        }

        private static byte Expand4(int v)
        {
            return (byte)((v << 4) | v);
        }

        private static void SetGray(PixelSurface bmp, int x, int y, byte i, byte a)
        {
            SetPixelSafe(bmp, x, y, Color.FromArgb(a, i, i, i));
        }

        private static void SetPixelSafe(PixelSurface bmp, int x, int y, Color color)
        {
            bmp.SetPixel(x, y, color);
        }

        private static byte ReadByte(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length)
                throw new EndOfStreamException("Texture data is truncated.");
            return data[offset];
        }

        private static int ReadU16(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length)
                throw new EndOfStreamException("Unexpected end of file.");
            return (data[offset] << 8) | data[offset + 1];
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new EndOfStreamException("Unexpected end of file.");
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private sealed class PixelSurface
        {
            public readonly int Width;
            public readonly int Height;
            private readonly int[] _pixels;
            public PixelSurface(int width, int height)
            {
                Width = width;
                Height = height;
                _pixels = new int[checked(width * height)];
            }

            public void SetPixel(int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                    return;
                _pixels[y * Width + x] = color.ToArgb();
            }

            public Bitmap ToBitmap()
            {
                Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                Rectangle rect = new Rectangle(0, 0, Width, Height);
                BitmapData lockData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    if (lockData.Stride == Width * 4)
                    {
                        Marshal.Copy(_pixels, 0, lockData.Scan0, _pixels.Length);
                    }
                    else
                    {
                        int y;
                        for (y = 0; y < Height; y++)
                        {
                            IntPtr row = new IntPtr(lockData.Scan0.ToInt64() + (long)y * lockData.Stride);
                            Marshal.Copy(_pixels, y * Width, row, Width);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(lockData);
                }

                return bitmap;
            }
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
                    return "Format " + format;
            }
        }

        private static string GetPaletteFormatName(int format)
        {
            switch (format)
            {
                case 0:
                    return "IA8";
                case 1:
                    return "RGB565";
                case 2:
                    return "RGB5A3";
                default:
                    return "Format " + format;
            }
        }
    }
}
