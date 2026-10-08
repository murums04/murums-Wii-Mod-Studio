using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal sealed class RigRasterizer
    {
        sealed class Texture { internal int Width, Height; internal int[] Pixels; internal bool Translucent; }
        internal int[] SurfaceFaces, Overlay;
        internal System.Collections.Generic.HashSet<int> Selection;
        internal bool SelectionWholeFaces;
        internal int SurfaceWidth;
        internal bool TransparentBackground;
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
                    result.Translucent = result.Pixels.Any(pixel => ((uint)pixel >> 24) > 0 && ((uint)pixel >> 24) < 255);
                    return result;
                }
            }).ToArray();
        }
        sealed class Layer
        {
            internal RigRasterizer Renderer;
            internal PointF[] Points;
            internal double[] Depths;
            internal float[] Influence;
            internal bool Weights, Solid, Selectable;
        }
        sealed class Face
        {
            internal Layer Layer;
            internal int Index;
            internal double Depth;
        }
        internal Bitmap Draw(Size size, PointF[] points, double[] depths, int selectedBone, bool weights, bool solid = false, RigRasterizer background = null, PointF[] backgroundPoints = null, double[] backgroundDepths = null)
        {
            int width = Math.Max(1, size.Width), height = Math.Max(1, size.Height);
            var pixels = TransparentBackground ? new int[width * height]
                : Enumerable.Repeat(unchecked((int)0xff23242c), width * height).ToArray();
            SurfaceWidth = width;
            SurfaceFaces = Enumerable.Repeat(-1, pixels.Length).ToArray();
            var zbuffer = Enumerable.Repeat(Double.NegativeInfinity, pixels.Length).ToArray();
            var pickDepth = Enumerable.Repeat(Double.NegativeInfinity, pixels.Length).ToArray();
            var transparent = new List<Face>();
            if (background != null)
                DrawLayer(new Layer { Renderer = background, Points = backgroundPoints, Depths = backgroundDepths },
                    width, height, pixels, zbuffer, pickDepth, transparent);
            var layer = new Layer { Renderer = this, Points = points, Depths = depths,
                Weights = weights && selectedBone >= 0, Solid = solid, Selectable = true };
            if (layer.Weights)
            {
                layer.Influence = new float[points.Length];
                for (int i = 0; i < points.Length; i++)
                    for (int j = 0; j < rig.BoneIndices[i].Length; j++)
                        if (rig.BoneIndices[i][j] == selectedBone) layer.Influence[i] += rig.BoneWeights[i][j];
            }
            DrawLayer(layer, width, height, pixels, zbuffer, pickDepth, transparent);
            // Fahrzeug und Figur teilen die Tiefe; Glas wird zuletzt von hinten nach vorne gemischt.
            foreach (var face in transparent.OrderBy(face => face.Depth))
                DrawFace(face.Layer, face.Index, width, height, pixels, zbuffer, pickDepth, true);
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var target = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, target.Scan0, pixels.Length); }
            finally { result.UnlockBits(target); }
            return result;
        }
        void DrawLayer(Layer layer, int width, int height, int[] pixels, double[] zbuffer, double[] pickDepth, List<Face> transparent)
        {
            var model = layer.Renderer.rig;
            for (int index = 0; index < model.Faces.Length; index++)
            {
                int material = model.FaceMaterials[index];
                float alpha = model.Materials[material].Color[3];
                if (alpha <= 0) continue;
                DrawFace(layer, index, width, height, pixels, zbuffer, pickDepth, false);
                var texture = layer.Renderer.textures[material];
                if (!layer.Weights && !layer.Solid && (alpha < 1 || texture != null && texture.Translucent))
                {
                    var vertices = model.Faces[index];
                    transparent.Add(new Face { Layer = layer, Index = index,
                        Depth = vertices.Average(vertex => layer.Depths[vertex]) });
                }
            }
        }
        static bool TopLeft(PointF a, PointF b, bool positive)
        {
            if (!positive) { var temporary = a; a = b; b = temporary; }
            return b.Y > a.Y || b.Y == a.Y && b.X < a.X;
        }
        void DrawFace(Layer layer, int f, int width, int height, int[] pixels, double[] zbuffer, double[] pickDepth, bool transparent)
        {
            var renderer = layer.Renderer;
            var model = renderer.rig;
            var points = layer.Points;
            var depths = layer.Depths;
            var face = model.Faces[f]; int ia = face[0], ib = face[1], ic = face[2];
            var a = points[ia]; var b = points[ib]; var c = points[ic];
            float denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(denominator) < .001) return;
            var material = model.Materials[model.FaceMaterials[f]];
            if (material.DoubleSided == false && denominator >= 0) return;
            int left = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
            int right = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
            int top = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
            int bottom = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
            var texture = renderer.textures[model.FaceMaterials[f]];
            int tint = 0;
            if (layer.Selectable)
            {
                if (renderer.Overlay != null && renderer.Overlay.Length == points.Length) tint = Math.Max(renderer.Overlay[ia], Math.Max(renderer.Overlay[ib], renderer.Overlay[ic]));
                if (renderer.Selection != null && (renderer.SelectionWholeFaces
                    ? renderer.Selection.Contains(ia) && renderer.Selection.Contains(ib) && renderer.Selection.Contains(ic)
                    : renderer.Selection.Contains(ia) || renderer.Selection.Contains(ib) || renderer.Selection.Contains(ic))) tint = 4;
            }
            var baseColor = material.Color;
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
                if (wa == 0 && !TopLeft(b, c, denominator > 0) || wb == 0 && !TopLeft(c, a, denominator > 0)
                    || wc == 0 && !TopLeft(a, b, denominator > 0)) continue;
                double depth = depths[ia] * wa + depths[ib] * wb + depths[ic] * wc;
                int pixel = y * width + x;
                if (depth <= zbuffer[pixel]) continue;
                int texel = 0;
                if (texture != null && !layer.Weights)
                {
                    float u = model.Uvs[ia][0] * wa + model.Uvs[ib][0] * wb + model.Uvs[ic][0] * wc;
                    float v = model.Uvs[ia][1] * wa + model.Uvs[ib][1] * wb + model.Uvs[ic][1] * wc;
                    u -= (float)Math.Floor(u); v -= (float)Math.Floor(v);
                    texel = texture.Pixels[Math.Min(texture.Height - 1, (int)((1 - v) * texture.Height)) * texture.Width + Math.Min(texture.Width - 1, (int)(u * texture.Width))];
                    if ((uint)texel >> 24 == 0) continue;
                }
                int color;
                if (layer.Weights)
                {
                    float amount = layer.Influence[ia] * wa + layer.Influence[ib] * wb + layer.Influence[ic] * wc;
                    color = Color.FromArgb(255, (int)(40 + 210 * amount), (int)(75 + 85 * (1 - amount)), (int)(70 + 130 * (1 - amount))).ToArgb();
                }
                else if (layer.Solid) color = Color.FromArgb(255, shade, shade, Math.Min(255, shade + 8)).ToArgb();
                else
                {
                    int alpha = (int)Math.Round((texture == null ? 255 : (uint)texel >> 24) * Math.Max(0, Math.Min(1, baseColor[3])));
                    color = texture != null ? (texel & 0xffffff) | (alpha << 24)
                        : Color.FromArgb(alpha, Channel(baseColor[0]), Channel(baseColor[1]), Channel(baseColor[2])).ToArgb();
                }
                int opacity = (int)((uint)color >> 24);
                if (opacity == 0 || transparent != (opacity < 255)) continue;
                if (tint > 0)
                {
                    Color mark = tint == 4 ? Color.Cyan : tint == 3 ? Color.DodgerBlue : tint == 2 ? Color.Orange : Color.MediumPurple;
                    color = Color.FromArgb(opacity, (((color >> 16) & 255) + mark.R * 2) / 3, (((color >> 8) & 255) + mark.G * 2) / 3, ((color & 255) + mark.B * 2) / 3).ToArgb();
                }
                pixels[pixel] = transparent ? Blend(color, pixels[pixel]) : color;
                if (!transparent) zbuffer[pixel] = depth;
                if (layer.Selectable && depth > pickDepth[pixel]) { SurfaceFaces[pixel] = f; pickDepth[pixel] = depth; }
            }
        }
        static int Channel(float value) { return (int)(255 * Math.Max(0, Math.Min(1, value))); }
        static int Blend(int source, int destination)
        {
            int alpha = (int)((uint)source >> 24), behind = (int)((uint)destination >> 24);
            int remaining = behind * (255 - alpha);
            int total = alpha * 255 + remaining;
            if (total == 0) return 0;
            int red = (((source >> 16) & 255) * alpha * 255 + ((destination >> 16) & 255) * remaining) / total;
            int green = (((source >> 8) & 255) * alpha * 255 + ((destination >> 8) & 255) * remaining) / total;
            int blue = ((source & 255) * alpha * 255 + (destination & 255) * remaining) / total;
            return ((total + 127) / 255 << 24) | (red << 16) | (green << 8) | blue;
        }
    }
}
