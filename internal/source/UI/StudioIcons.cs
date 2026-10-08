using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal enum StudioIcon
    {
        Open, Save, SaveAs, Import, Export, Undo, Redo, Add, Remove, Delete,
        Refresh, Search, Expand, Collapse, Fit, ZoomIn, ZoomOut, Play, Pause, Stop,
        Previous, Next, Up, Down, Close, Settings, Help, Folder, Archive, Pack,
        Character, Image, Font, Layout, Audio, Effects, Compare, Check, Menu, Home
    }

    internal static class StudioIcons
    {
        // Der Aufrufer besitzt das Bitmap und gibt es nach Gebrauch frei.
        public static Bitmap Create(StudioIcon icon, int size, Color color)
        {
            if (!Enum.IsDefined(typeof(StudioIcon), icon))
                throw new ArgumentOutOfRangeException("icon");
            if (size < 8 || size > 512)
                throw new ArgumentOutOfRangeException("size", "Icongröße muss zwischen 8 und 512 liegen.");

            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                // Kleine Icons benötigen mindestens 1.6 Gerätepixel für klaren Kontrast.
                using (var pen = new Pen(Color.White, Math.Max(1.7f, 1.6f * 24f / size)))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.ScaleTransform(size / 24f, size / 24f);
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    Draw(graphics, pen, icon);
                }
                Tint(bitmap, color);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private static void Draw(Graphics g, Pen p, StudioIcon icon)
        {
            switch (icon)
            {
                case StudioIcon.Open:
                    Lines(g, p, 4, 19, 4, 6, 10, 6, 12, 8, 19, 8);
                    Lines(g, p, 4, 19, 7, 11, 20, 11, 17, 19, 4, 19);
                    break;
                case StudioIcon.Folder:
                    Lines(g, p, 4, 19, 4, 6, 10, 6, 12, 8, 20, 8, 20, 19, 4, 19);
                    break;
                case StudioIcon.Save:
                case StudioIcon.SaveAs:
                    Lines(g, p, 5, 4, 17, 4, 20, 7, 20, 20, 4, 20, 4, 4, 5, 4);
                    Lines(g, p, 8, 4, 8, 10, 16, 10, 16, 4);
                    if (icon == StudioIcon.Save) Lines(g, p, 8, 20, 8, 14, 16, 14, 16, 20);
                    else
                    {
                        Lines(g, p, 9, 18, 10, 15, 15, 10, 18, 13, 13, 18, 9, 18);
                        g.DrawLine(p, 14, 11, 17, 14);
                    }
                    break;
                case StudioIcon.Import:
                case StudioIcon.Export:
                    Lines(g, p, 4, 14, 4, 20, 20, 20, 20, 14);
                    bool import = icon == StudioIcon.Import;
                    g.DrawLine(p, 12, 4, 12, 15);
                    Lines(g, p, 8, import ? 11 : 8, 12, import ? 15 : 4, 16, import ? 11 : 8);
                    break;
                case StudioIcon.Undo:
                case StudioIcon.Redo:
                    if (icon == StudioIcon.Redo)
                    {
                        g.TranslateTransform(24, 0);
                        g.ScaleTransform(-1, 1);
                    }
                    Lines(g, p, 9, 5, 5, 9, 9, 13);
                    using (var curve = new GraphicsPath())
                    {
                        curve.AddBezier(5, 9, 20, 5, 23, 19, 12, 19);
                        g.DrawPath(p, curve);
                    }
                    break;
                case StudioIcon.Add:
                    g.DrawLine(p, 12, 5, 12, 19);
                    g.DrawLine(p, 5, 12, 19, 12);
                    break;
                case StudioIcon.Remove: g.DrawLine(p, 5, 12, 19, 12); break;
                case StudioIcon.Delete:
                    g.DrawLine(p, 4, 7, 20, 7);
                    Lines(g, p, 9, 7, 9, 4, 15, 4, 15, 7);
                    Lines(g, p, 6, 7, 7, 20, 17, 20, 18, 7);
                    g.DrawLine(p, 10, 11, 10, 16);
                    g.DrawLine(p, 14, 11, 14, 16);
                    break;
                case StudioIcon.Refresh:
                    g.DrawArc(p, 5, 5, 14, 14, 35, 290);
                    Lines(g, p, 20, 5, 20, 10, 15, 10);
                    break;
                case StudioIcon.Search:
                case StudioIcon.ZoomIn:
                case StudioIcon.ZoomOut:
                    g.DrawEllipse(p, 4, 4, 12, 12);
                    g.DrawLine(p, 15, 15, 20, 20);
                    if (icon != StudioIcon.Search) g.DrawLine(p, 7, 10, 13, 10);
                    if (icon == StudioIcon.ZoomIn) g.DrawLine(p, 10, 7, 10, 13);
                    break;
                case StudioIcon.Expand:
                case StudioIcon.Collapse:
                    Corners(g, p);
                    if (icon == StudioIcon.Expand)
                    {
                        g.DrawLine(p, 4, 4, 9, 9);
                        g.DrawLine(p, 20, 4, 15, 9);
                        g.DrawLine(p, 4, 20, 9, 15);
                        g.DrawLine(p, 20, 20, 15, 15);
                    }
                    else
                    {
                        Lines(g, p, 5, 9, 9, 9, 9, 5);
                        Lines(g, p, 15, 5, 15, 9, 19, 9);
                        Lines(g, p, 5, 15, 9, 15, 9, 19);
                        Lines(g, p, 15, 19, 15, 15, 19, 15);
                    }
                    break;
                case StudioIcon.Fit:
                    Corners(g, p);
                    g.DrawRectangle(p, 8, 8, 8, 8);
                    break;
                case StudioIcon.Play: Lines(g, p, 8, 4, 20, 12, 8, 20, 8, 4); break;
                case StudioIcon.Pause:
                    g.DrawRectangle(p, 6, 5, 3, 14);
                    g.DrawRectangle(p, 15, 5, 3, 14);
                    break;
                case StudioIcon.Stop: g.DrawRectangle(p, 5, 5, 14, 14); break;
                case StudioIcon.Previous:
                    g.DrawLine(p, 5, 5, 5, 19);
                    Lines(g, p, 18, 5, 8, 12, 18, 19, 18, 5);
                    break;
                case StudioIcon.Next:
                    g.DrawLine(p, 19, 5, 19, 19);
                    Lines(g, p, 6, 5, 16, 12, 6, 19, 6, 5);
                    break;
                case StudioIcon.Up:
                case StudioIcon.Down:
                    Lines(g, p, 6, icon == StudioIcon.Up ? 15 : 9, 12, icon == StudioIcon.Up ? 9 : 15,
                        18, icon == StudioIcon.Up ? 15 : 9);
                    break;
                case StudioIcon.Close:
                    g.DrawLine(p, 6, 6, 18, 18);
                    g.DrawLine(p, 6, 18, 18, 6);
                    break;
                case StudioIcon.Settings:
                    using (var path = new GraphicsPath())
                    {
                        var points = new PointF[32];
                        for (int i = 0; i < points.Length; i++)
                        {
                            double angle = (i + 0.5) * Math.PI / 16;
                            float radius = (i % 4 == 0 || i % 4 == 3) ? 8 : 6.4f;
                            points[i] = new PointF(12 + radius * (float)Math.Cos(angle), 12 + radius * (float)Math.Sin(angle));
                        }
                        path.AddPolygon(points);
                        g.DrawPath(p, path);
                    }
                    g.DrawEllipse(p, 9, 9, 6, 6);
                    break;
                case StudioIcon.Help:
                    g.DrawEllipse(p, 4, 4, 16, 16);
                    using (var curve = new GraphicsPath())
                    {
                        curve.AddBezier(9, 9, 9, 5, 18, 7, 13, 12);
                        curve.AddLine(13, 12, 12, 14);
                        g.DrawPath(p, curve);
                    }
                    g.DrawLine(p, 12, 17, 12.01f, 17);
                    break;
                case StudioIcon.Archive:
                    g.DrawRectangle(p, 5, 8, 14, 12);
                    g.DrawRectangle(p, 4, 4, 16, 4);
                    g.DrawLine(p, 10, 12, 14, 12);
                    break;
                case StudioIcon.Pack:
                    Lines(g, p, 4, 8, 12, 4, 20, 8, 20, 16, 12, 20, 4, 16, 4, 8, 12, 12, 20, 8);
                    g.DrawLine(p, 12, 12, 12, 20);
                    g.DrawLine(p, 8, 6, 16, 10);
                    break;
                case StudioIcon.Character:
                    g.DrawEllipse(p, 8, 4, 8, 8);
                    g.DrawArc(p, 4, 14, 16, 12, 180, 180);
                    g.DrawLine(p, 4, 20, 20, 20);
                    break;
                case StudioIcon.Image:
                    g.DrawRectangle(p, 4, 4, 16, 16);
                    g.DrawEllipse(p, 7, 7, 3, 3);
                    Lines(g, p, 4, 17, 9, 12, 13, 16, 16, 13, 20, 17);
                    break;
                case StudioIcon.Font:
                    Lines(g, p, 5, 20, 12, 4, 19, 20);
                    g.DrawLine(p, 8, 14, 16, 14);
                    break;
                case StudioIcon.Layout:
                    g.DrawRectangle(p, 4, 4, 16, 16);
                    g.DrawLine(p, 4, 9, 20, 9);
                    g.DrawLine(p, 10, 9, 10, 20);
                    break;
                case StudioIcon.Audio:
                    Lines(g, p, 5, 9, 8, 9, 13, 5, 13, 19, 8, 15, 5, 15, 5, 9);
                    g.DrawArc(p, 12, 8, 6, 8, -70, 140);
                    g.DrawArc(p, 12, 4, 8, 16, -60, 120);
                    break;
                case StudioIcon.Effects:
                    Lines(g, p, 5, 17, 16, 6, 19, 9, 8, 20, 5, 17);
                    g.DrawLine(p, 13, 9, 16, 12);
                    g.DrawLine(p, 8, 4, 8, 8);
                    g.DrawLine(p, 6, 6, 10, 6);
                    g.DrawLine(p, 17, 3, 17, 5);
                    break;
                case StudioIcon.Compare:
                    g.DrawRectangle(p, 4, 5, 6, 14);
                    g.DrawRectangle(p, 14, 5, 6, 14);
                    g.DrawLine(p, 7, 9, 7, 15);
                    g.DrawLine(p, 16, 12, 18, 12);
                    break;
                case StudioIcon.Check: Lines(g, p, 5, 12, 10, 17, 20, 6); break;
                case StudioIcon.Menu:
                    g.DrawLine(p, 5, 6, 19, 6);
                    g.DrawLine(p, 5, 12, 19, 12);
                    g.DrawLine(p, 5, 18, 19, 18);
                    break;
                case StudioIcon.Home:
                    Lines(g, p, 4, 11, 12, 4, 20, 11);
                    Lines(g, p, 6, 10, 6, 20, 10, 20, 10, 14, 14, 14, 14, 20, 18, 20, 18, 10);
                    break;
            }
        }

        private static void Corners(Graphics g, Pen p)
        {
            Lines(g, p, 4, 9, 4, 4, 9, 4);
            Lines(g, p, 15, 4, 20, 4, 20, 9);
            Lines(g, p, 4, 15, 4, 20, 9, 20);
            Lines(g, p, 15, 20, 20, 20, 20, 15);
        }

        private static void Tint(Bitmap bitmap, Color color)
        {
            // Erst die Deckung zeichnen: Kreuzungen erhöhen dadurch kein Teilalpha.
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[bitmap.Width * 4];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, row.Length);
                    for (int x = 0; x < row.Length; x += 4)
                    {
                        byte alpha = (byte)((row[x + 3] * color.A + 127) / 255);
                        row[x] = alpha == 0 ? (byte)0 : color.B;
                        row[x + 1] = alpha == 0 ? (byte)0 : color.G;
                        row[x + 2] = alpha == 0 ? (byte)0 : color.R;
                        row[x + 3] = alpha;
                    }
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
        }

        private static void Lines(Graphics g, Pen p, params float[] coordinates)
        {
            var points = new PointF[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = new PointF(coordinates[i * 2], coordinates[i * 2 + 1]);
            g.DrawLines(p, points);
        }
    }
}
