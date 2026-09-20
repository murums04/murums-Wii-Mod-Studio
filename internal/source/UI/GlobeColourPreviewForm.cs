using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class ColourSamples
    {
        internal static Bitmap Globe(Color surface, Color sky, Color glow, bool visible, Image background)
        {
            var image = new Bitmap(720, 380);
            using (var g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(13, 16, 27));
                if (background != null) g.DrawImage(background, new Rectangle(0, 0, 720, 380));
                else
                {
                    var random = new Random(47);
                    Color stars = sky.ToArgb() == Color.White.ToArgb() ? Color.FromArgb(155, 174, 209) : sky;
                    for (int i = 0; i < 180; i++)
                        using (var brush = new SolidBrush(Color.FromArgb(random.Next(40, 220), stars)))
                        {
                            int size = random.Next(1, 4);
                            g.FillEllipse(brush, random.Next(720), random.Next(380), size, size);
                        }
                }
                if (visible)
                {
                    Color rim = glow.ToArgb() == Color.White.ToArgb() ? Color.FromArgb(100, 160, 255) : glow;
                    for (int r = 150; r > 137; r--)
                        using (var pen = new Pen(Color.FromArgb((150 - r) * 3, rim), 3))
                            g.DrawEllipse(pen, 360 - r, 190 - r, r * 2, r * 2);
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(225, 55, 270, 270);
                        using (var brush = new PathGradientBrush(path))
                        {
                            Color tint = surface.ToArgb() == Color.White.ToArgb() ? Color.FromArgb(116, 168, 235) : surface;
                            brush.CenterPoint = new PointF(302, 120);
                            brush.CenterColor = tint;
                            brush.SurroundColors = new[] { Color.FromArgb(tint.R / 8, tint.G / 8, tint.B / 8) };
                            g.FillPath(brush, path);
                        }
                        var state = g.Save();
                        g.SetClip(path);
                        using (var pen = new Pen(Color.FromArgb(65, 255, 255, 255), 1))
                        {
                            for (int i = 1; i <= 4; i++) g.DrawEllipse(pen, 360 - i * 27, 55, i * 54, 270);
                            for (int i = -2; i <= 2; i++) g.DrawEllipse(pen, 225, 175 + i * 44, 270, 30);
                        }
                        g.Restore(state);
                    }
                }
            }
            return image;
        }

        internal static Bitmap Particles(Color primary, Color secondary, bool star)
        {
            var image = new Bitmap(720, 330);
            using (var g = Graphics.FromImage(image))
            {
                g.Clear(Color.FromArgb(38, 39, 45));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 2))
                    g.DrawEllipse(pen, 275, 205, 170, 50);
                var random = new Random(93);
                for (int i = 0; i < 45; i++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2);
                    float radius = 35 + random.Next(115);
                    float x = 360 + (float)Math.Cos(angle) * radius * 1.8f;
                    float y = 160 + (float)Math.Sin(angle) * radius;
                    float size = random.Next(4, 13);
                    Color colour = i % 2 == 0 ? primary : secondary;
                    using (var brush = new SolidBrush(colour))
                    {
                        if (star)
                        {
                            var points = new PointF[10];
                            for (int j = 0; j < points.Length; j++)
                            {
                                double a = j * Math.PI / 5 - Math.PI / 2;
                                float r = j % 2 == 0 ? size : size * .4f;
                                points[j] = new PointF(x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r);
                            }
                            g.FillPolygon(brush, points);
                        }
                        else g.FillEllipse(brush, x, y, size * .55f, size * 1.5f);
                    }
                }
            }
            return image;
        }
    }

    internal sealed class GlobeColourPreviewForm : StudioToolForm
    {
        internal Color GlobeColor, SkyColor, GlowColor;
        readonly PictureBox picture = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly bool globeVisible;
        readonly Image background;
        readonly Button globe, sky, glow;
        internal GlobeColourPreviewForm(Color surface, Color stars, Color rim, bool visible, Image backdrop)
            : base(L.T("Globusfarben", "Globe colours"),
                L.T("Farben wählen • Vorschau vergleichen • In die Einstellungen übernehmen",
                    "Choose colours • Review the sample • Apply to settings"))
        {
            GlobeColor = surface; SkyColor = stars; GlowColor = rim;
            globeVisible = visible; background = backdrop;
            globe = Action(L.T("Globus…", "Globe…"), "", delegate { Choose(0); });
            sky = Action(L.T("Himmel…", "Sky…"), "", delegate { Choose(1); });
            glow = Action(L.T("Leuchtrand…", "Glow…"), "", delegate { Choose(2); });
            sky.Enabled = background == null;
            Action(L.T("Originalfarben", "Original colours"), "", delegate {
                GlobeColor = SkyColor = GlowColor = Color.White; Draw();
            });
            ExportAction(L.T("Farben übernehmen", "Apply colours"), "", delegate { DialogResult = DialogResult.OK; Close(); });
            Body.Controls.Add(picture);
            Status.Text = L.T("Schematische Farbkombination mit Beispielbeleuchtung; keine geladenen Globus-Texturen oder Spielsimulation.\nWeiß erhält Originalfarben. Erst „Kopien erstellen“ im Globus-Tool schreibt Dateien.",
                "Schematic colour combination with sample lighting; not loaded globe textures or a game simulation.\nWhite keeps original colours. Create copies in the globe tool to write files.");
            Finish(); Draw();
        }
        void Choose(int which)
        {
            using (var dialog = new ColorDialog { FullOpen = true, Color = which == 0 ? GlobeColor : which == 1 ? SkyColor : GlowColor })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (which == 0) GlobeColor = dialog.Color;
                else if (which == 1) SkyColor = dialog.Color;
                else GlowColor = dialog.Color;
                Draw();
            }
        }
        void Draw()
        {
            ColourButton.SetColor(globe, GlobeColor);
            ColourButton.SetColor(sky, SkyColor);
            ColourButton.SetColor(glow, GlowColor);
            Image old = picture.Image;
            picture.Image = ColourSamples.Globe(GlobeColor, SkyColor, GlowColor, globeVisible, background);
            if (old != null) old.Dispose();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && picture.Image != null) { picture.Image.Dispose(); picture.Image = null; }
            base.Dispose(disposing);
        }
    }
}
