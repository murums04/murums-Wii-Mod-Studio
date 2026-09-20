using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal static class ColourGradient
    {
        internal static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        internal static void Apply(Bitmap mask, Color[] colours, Color foreground)
        {
            if (colours == null || colours.Length != 5) throw new ArgumentException("Five map colours required.");
            var area = new Rectangle(0, 0, mask.Width, mask.Height);
            var bits = mask.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[mask.Width * 4];
                Color baseColour = Blend(colours[0], foreground, .35f);
                for (int y = 0; y < mask.Height; y++)
                {
                    IntPtr pointer = IntPtr.Add(bits.Scan0, y * bits.Stride);
                    Marshal.Copy(pointer, row, 0, row.Length);
                    float v = mask.Height == 1 ? 0 : y / (float)(mask.Height - 1);
                    for (int x = 0; x < mask.Width; x++)
                    {
                        int p = x * 4, alpha = row[p + 3];
                        if (alpha == 0) continue;
                        float u = mask.Width == 1 ? 0 : x / (float)(mask.Width - 1);
                        Color top = Blend(colours[1], colours[2], u), bottom = Blend(colours[3], colours[4], u);
                        Color color = Blend(top, bottom, v);
                        row[p] = (byte)(color.B * baseColour.B / 255);
                        row[p + 1] = (byte)(color.G * baseColour.G / 255);
                        row[p + 2] = (byte)(color.R * baseColour.R / 255);
                        row[p + 3] = (byte)(alpha * color.A / 255 * baseColour.A / 255);
                    }
                    Marshal.Copy(row, 0, pointer, row.Length);
                }
            }
            finally { mask.UnlockBits(bits); }
        }
    }
}
