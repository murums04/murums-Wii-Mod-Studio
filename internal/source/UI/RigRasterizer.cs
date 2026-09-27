using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal sealed class RigRasterizer
    {
        sealed class Texture { internal int Width, Height; internal int[] Pixels; }
        internal int[] SurfaceFaces;
        internal int SurfaceWidth;
        readonly ModelRig rig;
        readonly Texture[] textures;
        internal RigRasterizer(ModelRig model)
        {
            rig = model;
            textures = model.Materials.Select(m => {
                if (m.Texture == null) return null;
                using (var source = new Bitmap(Path.Combine(model.Folder, m.Texture)))
                using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // Bild-DPI darf die UV-Zuordnung nicht veraendern.
                        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                            0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                    }
                    var result = new Texture { Width = bitmap.Width, Height = bitmap.Height, Pixels = new int[bitmap.Width * bitmap.Height] };
                    var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try { Marshal.Copy(bits.Scan0, result.Pixels, 0, result.Pixels.Length); }
                    finally { bitmap.UnlockBits(bits); }
                    return result;
                }
            }).ToArray();
        }
        internal Bitmap Draw(Size size, PointF[] points, double[] depths, int selectedBone, bool weights, bool solid = false)
        {
            int width = Math.Max(1, size.Width), height = Math.Max(1, size.Height);
            var pixels = Enumerable.Repeat(unchecked((int)0xff23242c), width * height).ToArray();
            SurfaceWidth = width;
            SurfaceFaces = Enumerable.Repeat(-1, pixels.Length).ToArray();
            var zbuffer = Enumerable.Repeat(Double.NegativeInfinity, pixels.Length).ToArray();
            var influence = new float[points.Length];
            if (weights && selectedBone >= 0)
                for (int i = 0; i < points.Length; i++)
                    for (int j = 0; j < rig.BoneIndices[i].Length; j++) if (rig.BoneIndices[i][j] == selectedBone) influence[i] += rig.BoneWeights[i][j];
            for (int f = 0; f < rig.Faces.Length; f++)
            {
                var face = rig.Faces[f]; int ia = face[0], ib = face[1], ic = face[2];
                var a = points[ia]; var b = points[ib]; var c = points[ic];
                float denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
                if (Math.Abs(denominator) < .001) continue;
                int left = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
                int right = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
                int top = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
                int bottom = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
                var texture = textures[rig.FaceMaterials[f]];
                var baseColor = rig.Materials[rig.FaceMaterials[f]].Color;
                double nx = (b.Y - a.Y) * (depths[ic] - depths[ia]) - (depths[ib] - depths[ia]) * (c.Y - a.Y);
                double ny = (depths[ib] - depths[ia]) * (c.X - a.X) - (b.X - a.X) * (depths[ic] - depths[ia]);
                double nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                double light = length > 0 ? Math.Abs((nx * -.3 + ny * -.5 + nz * .81) / length) : 1;
                int shade = (int)(105 + 115 * light);
                for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
                {
                    float wa = ((b.Y - c.Y) * (x + .5f - c.X) + (c.X - b.X) * (y + .5f - c.Y)) / denominator;
                    float wb = ((c.Y - a.Y) * (x + .5f - c.X) + (a.X - c.X) * (y + .5f - c.Y)) / denominator;
                    float wc = 1 - wa - wb;
                    if (wa < 0 || wb < 0 || wc < 0) continue;
                    double depth = depths[ia] * wa + depths[ib] * wb + depths[ic] * wc;
                    int pixel = y * width + x;
                    if (depth <= zbuffer[pixel]) continue;
                    int texel = 0;
                    if (texture != null && !(weights && selectedBone >= 0))
                    {
                        float u = rig.Uvs[ia][0] * wa + rig.Uvs[ib][0] * wb + rig.Uvs[ic][0] * wc;
                        float v = rig.Uvs[ia][1] * wa + rig.Uvs[ib][1] * wb + rig.Uvs[ic][1] * wc;
                        u -= (float)Math.Floor(u); v -= (float)Math.Floor(v);
                        texel = texture.Pixels[Math.Min(texture.Height - 1, (int)((1 - v) * texture.Height)) * texture.Width + Math.Min(texture.Width - 1, (int)(u * texture.Width))];
                        if ((uint)texel >> 24 < 128) continue;
                    }
                    int color;
                    if (weights && selectedBone >= 0)
                    {
                        float amount = influence[ia] * wa + influence[ib] * wb + influence[ic] * wc;
                        color = Color.FromArgb(255, (int)(40 + 210 * amount), (int)(75 + 85 * (1 - amount)), (int)(70 + 130 * (1 - amount))).ToArgb();
                    }
                    else if (solid) color = Color.FromArgb(255, shade, shade, Math.Min(255, shade + 8)).ToArgb();
                    else if (texture != null) color = texel;
                    else color = Color.FromArgb(255, (int)(255 * Math.Max(0, Math.Min(1, baseColor[0]))), (int)(255 * Math.Max(0, Math.Min(1, baseColor[1]))), (int)(255 * Math.Max(0, Math.Min(1, baseColor[2])))).ToArgb();
                    pixels[pixel] = color; zbuffer[pixel] = depth; SurfaceFaces[pixel] = f;
                }
            }
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var target = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, target.Scan0, pixels.Length); }
            finally { result.UnlockBits(target); }
            return result;
        }
    }
}
