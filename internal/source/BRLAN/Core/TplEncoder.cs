using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio.Brlan
{
    internal enum TplPixelFormat
    {
        RGB565 = 4,
        RGB5A3 = 5,
        CMPR = 14
    }

    internal static class TplEncoder
    {
        public static byte[] Encode(Bitmap bitmap, TplPixelFormat format)
        {
            if (bitmap == null)
                throw new ArgumentNullException("bitmap");
            if (bitmap.Width <= 0 || bitmap.Height <= 0 || bitmap.Width > 65535 || bitmap.Height > 65535)
                throw new InvalidDataException(L.T("Ungültige Bildgrösse für TPL.", "Invalid image size for TPL."));
            const int imageTableOffset = 0x0C;
            const int imageHeaderOffset = 0x14;
            const int imageHeaderSize = 0x24;
            int dataOffset = Align(imageHeaderOffset + imageHeaderSize, 0x20);
            byte[] texels = format == TplPixelFormat.CMPR ? EncodeCmpr(bitmap) : Encode16BitTiled(bitmap, format);
            byte[] output = new byte[dataOffset + texels.Length];
            // TPL header
            WriteU32BE(output, 0x00, 0x0020AF30u);
            WriteU32BE(output, 0x04, 1u);
            WriteU32BE(output, 0x08, imageTableOffset);
            // Image table: image header, palette header (none)
            WriteU32BE(output, 0x0C, imageHeaderOffset);
            WriteU32BE(output, 0x10, 0u);
            // Image header
            WriteU16BE(output, imageHeaderOffset + 0x00, (ushort)bitmap.Height);
            WriteU16BE(output, imageHeaderOffset + 0x02, (ushort)bitmap.Width);
            WriteU32BE(output, imageHeaderOffset + 0x04, (uint)format);
            WriteU32BE(output, imageHeaderOffset + 0x08, (uint)dataOffset);
            WriteU32BE(output, imageHeaderOffset + 0x0C, 0u); // WrapS Clamp
            WriteU32BE(output, imageHeaderOffset + 0x10, 0u); // WrapT Clamp
            WriteU32BE(output, imageHeaderOffset + 0x14, 1u); // MinFilter Linear
            WriteU32BE(output, imageHeaderOffset + 0x18, 1u); // MagFilter Linear
            WriteF32BE(output, imageHeaderOffset + 0x1C, 0.0f);
            output[imageHeaderOffset + 0x20] = 0; // EdgeLOD
            output[imageHeaderOffset + 0x21] = 0; // MinLOD
            output[imageHeaderOffset + 0x22] = 0; // MaxLOD
            output[imageHeaderOffset + 0x23] = 0; // Unpacked
            Buffer.BlockCopy(texels, 0, output, dataOffset, texels.Length);
            return output;
        }

        public static void Save(Bitmap bitmap, string path, TplPixelFormat format)
        {
            File.WriteAllBytes(path, Encode(bitmap, format));
        }

        private static byte[] Encode16BitTiled(Bitmap bitmap, TplPixelFormat format)
        {
            int blocksX = (bitmap.Width + 3) / 4;
            int blocksY = (bitmap.Height + 3) / 4;
            byte[] data = new byte[blocksX * blocksY * 32];
            Bitmap source = bitmap;
            Bitmap converted = null;
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(converted))
                    g.DrawImageUnscaled(bitmap, 0, 0);
                source = converted;
            }

            BitmapData lockData = null;
            try
            {
                Rectangle rect = new Rectangle(0, 0, source.Width, source.Height);
                lockData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = lockData.Stride;
                int absStride = Math.Abs(stride);
                byte[] pixels = new byte[absStride * source.Height];
                Marshal.Copy(lockData.Scan0, pixels, 0, pixels.Length);
                int p = 0;
                int by, bx, py, px;
                for (by = 0; by < blocksY; by++)
                {
                    for (bx = 0; bx < blocksX; bx++)
                    {
                        for (py = 0; py < 4; py++)
                        {
                            for (px = 0; px < 4; px++)
                            {
                                int x = bx * 4 + px;
                                int y = by * 4 + py;
                                byte r = 0, g = 0, b = 0, a = 0;
                                if (x < source.Width && y < source.Height)
                                {
                                    int sourceY = stride >= 0 ? y : (source.Height - 1 - y);
                                    int offset = sourceY * absStride + x * 4;
                                    b = pixels[offset + 0];
                                    g = pixels[offset + 1];
                                    r = pixels[offset + 2];
                                    a = pixels[offset + 3];
                                }

                                ushort value = format == TplPixelFormat.RGB565 ? EncodeRgb565(r, g, b) : EncodeRgb5A3(r, g, b, a);
                                data[p++] = (byte)(value >> 8);
                                data[p++] = (byte)value;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (lockData != null)
                    source.UnlockBits(lockData);
                if (converted != null)
                    converted.Dispose();
            }

            return data;
        }

        private static byte[] EncodeCmpr(Bitmap bitmap)
        {
            int blocksX = (bitmap.Width + 7) / 8;
            int blocksY = (bitmap.Height + 7) / 8;
            byte[] output = new byte[blocksX * blocksY * 32];
            Bitmap source = bitmap;
            Bitmap converted = null;
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(converted))
                    g.DrawImageUnscaled(bitmap, 0, 0);
                source = converted;
            }

            BitmapData lockData = null;
            try
            {
                Rectangle rect = new Rectangle(0, 0, source.Width, source.Height);
                lockData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = lockData.Stride;
                int absStride = Math.Abs(stride);
                byte[] pixels = new byte[absStride * source.Height];
                Marshal.Copy(lockData.Scan0, pixels, 0, pixels.Length);
                int outPos = 0;
                int by, bx, subY, subX;
                for (by = 0; by < blocksY; by++)
                {
                    for (bx = 0; bx < blocksX; bx++)
                    {
                        // Each 8x8 CMPR tile contains four 4x4 DXT1-like subblocks:
                        // top-left, top-right, bottom-left, bottom-right.
                        for (subY = 0; subY < 8; subY += 4)
                        {
                            for (subX = 0; subX < 8; subX += 4)
                            {
                                byte[] rgba = new byte[16 * 4];
                                int py, px;
                                bool hasAlpha = false;
                                int n = 0;
                                for (py = 0; py < 4; py++)
                                {
                                    for (px = 0; px < 4; px++)
                                    {
                                        int x = bx * 8 + subX + px;
                                        int y = by * 8 + subY + py;
                                        byte r = 0, g = 0, b = 0, a = 0;
                                        if (x < source.Width && y < source.Height)
                                        {
                                            int sourceY = stride >= 0 ? y : (source.Height - 1 - y);
                                            int o = sourceY * absStride + x * 4;
                                            b = pixels[o + 0];
                                            g = pixels[o + 1];
                                            r = pixels[o + 2];
                                            a = pixels[o + 3];
                                        }

                                        if (a < 128)
                                            hasAlpha = true;
                                        rgba[n * 4 + 0] = r;
                                        rgba[n * 4 + 1] = g;
                                        rgba[n * 4 + 2] = b;
                                        rgba[n * 4 + 3] = a;
                                        n++;
                                    }
                                }

                                ushort c0, c1;
                                ChooseCmprEndpoints(rgba, hasAlpha, out c0, out c1);
                                output[outPos++] = (byte)(c0 >> 8);
                                output[outPos++] = (byte)c0;
                                output[outPos++] = (byte)(c1 >> 8);
                                output[outPos++] = (byte)c1;
                                byte[, ] palette = BuildCmprPalette(c0, c1);
                                int row;
                                for (row = 0; row < 4; row++)
                                {
                                    byte packed = 0;
                                    int col;
                                    for (col = 0; col < 4; col++)
                                    {
                                        int pixelIndex = row * 4 + col;
                                        byte a = rgba[pixelIndex * 4 + 3];
                                        int idx;
                                        if (hasAlpha && a < 128)
                                            idx = 3;
                                        else
                                            idx = FindNearestCmprColor(rgba, pixelIndex, palette, hasAlpha ? 3 : 4);
                                        packed |= (byte)((idx & 3) << (6 - col * 2));
                                    }

                                    output[outPos++] = packed;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (lockData != null)
                    source.UnlockBits(lockData);
                if (converted != null)
                    converted.Dispose();
            }

            return output;
        }

        private static void ChooseCmprEndpoints(byte[] rgba, bool hasAlpha, out ushort c0, out ushort c1)
        {
            int bestA = -1, bestB = -1, bestDistance = -1;
            int i, j;
            for (i = 0; i < 16; i++)
            {
                if (rgba[i * 4 + 3] < 128)
                    continue;
                for (j = i + 1; j < 16; j++)
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
                // Fully transparent block.
                c0 = 0;
                c1 = 0;
                return;
            }

            if (bestB < 0)
                bestB = bestA;
            ushort a565 = EncodeRgb565(rgba[bestA * 4 + 0], rgba[bestA * 4 + 1], rgba[bestA * 4 + 2]);
            ushort b565 = EncodeRgb565(rgba[bestB * 4 + 0], rgba[bestB * 4 + 1], rgba[bestB * 4 + 2]);
            if (hasAlpha)
            {
                // c0 <= c1 enables the 3-color + transparent mode.
                if (a565 <= b565)
                {
                    c0 = a565;
                    c1 = b565;
                }
                else
                {
                    c0 = b565;
                    c1 = a565;
                }
            }
            else
            {
                // c0 > c1 enables the full 4-color interpolation mode.
                if (a565 > b565)
                {
                    c0 = a565;
                    c1 = b565;
                }
                else
                {
                    c0 = b565;
                    c1 = a565;
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
            DecodeRgb565(c1, out r, out g, out b);
            p[1, 0] = r;
            p[1, 1] = g;
            p[1, 2] = b;
            p[0, 3] = 255;
            p[1, 3] = 255;
            int channel;
            if (c0 > c1)
            {
                for (channel = 0; channel < 3; channel++)
                {
                    p[2, channel] = (byte)((2 * p[0, channel] + p[1, channel]) / 3);
                    p[3, channel] = (byte)((p[0, channel] + 2 * p[1, channel]) / 3);
                }

                p[2, 3] = p[3, 3] = 255;
            }
            else
            {
                for (channel = 0; channel < 3; channel++)
                    p[2, channel] = (byte)((p[0, channel] + p[1, channel]) / 2);
                p[2, 3] = 255;
                p[3, 0] = p[3, 1] = p[3, 2] = p[3, 3] = 0;
            }

            return p;
        }

        private static int FindNearestCmprColor(byte[] rgba, int pixelIndex, byte[, ] palette, int count)
        {
            int r = rgba[pixelIndex * 4 + 0];
            int g = rgba[pixelIndex * 4 + 1];
            int b = rgba[pixelIndex * 4 + 2];
            int best = 0;
            int bestDist = Int32.MaxValue;
            int i;
            for (i = 0; i < count; i++)
            {
                int dr = r - palette[i, 0];
                int dg = g - palette[i, 1];
                int db = b - palette[i, 2];
                int d = dr * dr + dg * dg + db * db;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        private static void DecodeRgb565(ushort value, out byte r, out byte g, out byte b)
        {
            int r5 = (value >> 11) & 31;
            int g6 = (value >> 5) & 63;
            int b5 = value & 31;
            r = (byte)((r5 << 3) | (r5 >> 2));
            g = (byte)((g6 << 2) | (g6 >> 4));
            b = (byte)((b5 << 3) | (b5 >> 2));
        }

        private static ushort EncodeRgb565(byte r8, byte g8, byte b8)
        {
            int r = r8 >> 3;
            int g = g8 >> 2;
            int b = b8 >> 3;
            return (ushort)((r << 11) | (g << 5) | b);
        }

        private static ushort EncodeRgb5A3(byte r8, byte g8, byte b8, byte a8)
        {
            // GX RGB5A3: opaque pixels use 1RRRRRGGGGGBBBBB.
            // Translucent pixels use 0AAARRRRGGGGBBBB.
            if (a8 >= 224)
            {
                return (ushort)(0x8000 | ((r8 >> 3) << 10) | ((g8 >> 3) << 5) | (b8 >> 3));
            }

            return (ushort)(((a8 >> 5) << 12) | ((r8 >> 4) << 8) | ((g8 >> 4) << 4) | (b8 >> 4));
        }

        private static int Align(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        private static void WriteU16BE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }

        private static void WriteU32BE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        private static void WriteF32BE(byte[] data, int offset, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                data[offset] = b[3];
                data[offset + 1] = b[2];
                data[offset + 2] = b[1];
                data[offset + 3] = b[0];
            }
            else
            {
                Buffer.BlockCopy(b, 0, data, offset, 4);
            }
        }
    }
}
