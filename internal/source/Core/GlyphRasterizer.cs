using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace murumsWiiModStudio
{
    internal enum GlyphHinting
    {
        None,
        GridFit,
        Monochrome
    }

    internal static class GlyphRasterizer
    {
        // Rasterize at the final vertical resolution; horizontal fitting retains legacy advances.
        public static Bitmap Render(string text, FontFamily family, FontStyle style, float em, RectangleF bounds, Size size, RectangleF target, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            var mask = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(mask))
            {
                if (hint == GlyphHinting.None)
                {
                    using (var p = new GraphicsPath())
                    using (var sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                    {
                        p.AddString(text, family, (int)style, em, PointF.Empty, sf);
                        using (var m = new Matrix(target.Width / bounds.Width, 0, 0, target.Height / bounds.Height, target.X - bounds.X * target.Width / bounds.Width, target.Y - bounds.Y * target.Height / bounds.Height))
                            p.Transform(m);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.FillPath(Brushes.White, p);
                    }
                }
                else
                {
                    float scale = target.Height / bounds.Height;
                    int pad = 4;
                    int w = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale) + pad * 2), h = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale) + pad * 2);
                    using (var raw = new Bitmap(w, h))
                    using (var rg = Graphics.FromImage(raw))
                    using (var font = new Font(family, em * scale, style, GraphicsUnit.Pixel))
                    using (var sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                    {
                        rg.TextRenderingHint = hint == GlyphHinting.GridFit ? TextRenderingHint.AntiAliasGridFit : TextRenderingHint.SingleBitPerPixelGridFit;
                        rg.DrawString(text, font, Brushes.White, new PointF(pad - bounds.X * scale, pad - bounds.Y * scale), sf);
                        g.InterpolationMode = hint == GlyphHinting.Monochrome ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
                        g.DrawImage(raw, new RectangleF(target.X - pad * target.Width / (bounds.Width * scale), target.Y - pad, target.Width + 2 * pad * target.Width / (bounds.Width * scale), target.Height + 2 * pad), new RectangleF(0, 0, w, h), GraphicsUnit.Pixel);
                    }
                }
            }

            var result = new Bitmap(size.Width, size.Height);
            int radius = (int)Math.Ceiling(stroke / 2);
            for (int y = 0; y < size.Height; y++)
                for (int x = 0; x < size.Width; x++)
                {
                    float a = mask.GetPixel(x, y).A / 255f, edge = a;
                    for (int yy = -radius; yy <= radius; yy++)
                        for (int xx = -radius; xx <= radius; xx++)
                        {
                            float weight = Math.Max(0, Math.Min(1, stroke / 2 + 1f - (float)Math.Sqrt(xx * xx + yy * yy)));
                            int px = x + xx, py = y + yy;
                            if (weight > 0 && px >= 0 && py >= 0 && px < size.Width && py < size.Height)
                                edge = Math.Max(edge, mask.GetPixel(px, py).A / 255f * weight);
                        }

                    float fa = a * fill.A / 255f, oa = (stroke > 0 ? edge : 0) * outline.A / 255f * (1 - fa), total = fa + oa;
                    if (total <= 0)
                        continue;
                    result.SetPixel(x, y, Color.FromArgb(Math.Min(255, (int)(total * 255 + 0.5f)), (int)((fill.R * fa + outline.R * oa) / total), (int)((fill.G * fa + outline.G * oa) / total), (int)((fill.B * fa + outline.B * oa) / total)));
                }

            mask.Dispose();
            return result;
        }
    }
}
