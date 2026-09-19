using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class HudTextureColorForm : StudioToolForm
    {
        readonly byte[] source;
        readonly string key;
        Color dark = Color.Black, light = Color.White;
        readonly PictureBox picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(55, 55, 62)
        };
        readonly Button apply, darkButton, lightButton;
        public byte[] Result;
        public HudTextureColorForm(string name, byte[] bytes) : base("HUD Texture Colours", "Choose base and outline colours • Apply directly or preview first")
        {
            source = bytes;
            key = name;
            darkButton = Action("Base colour…", "Replace dark pixels. Transparent areas stay transparent.", delegate
            {
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = dark
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        dark = d.Color;
                        InvalidatePreview();
                    }
            });
            lightButton = Action("Outline colour…", "Replace light pixels. Intermediate shades blend between both colours; this does not create a new outline.", delegate
            {
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = light
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        light = d.Color;
                        InvalidatePreview();
                    }
            });
            Action("Preview colours", "Preview the encoded texture in its original format before applying.", Preview);
            apply = ExportAction("Apply colours", "Queue this texture in Race HUD. Save edited archives there to write the copy.", delegate
            {
                ApplyColours();

            });
            Body.Controls.Add(picture);
            Finish();
            InvalidatePreview();
            ShowTexture(source);
        }

        void InvalidatePreview()
        {
            Result = null;
            if (apply != null)
                apply.Enabled = true;
            darkButton.Text = "Base: #" + (dark.ToArgb() & 0xffffff).ToString("X6");
            lightButton.Text = "Outline: #" + (light.ToArgb() & 0xffffff).ToString("X6");
            darkButton.FlatAppearance.BorderSize = lightButton.FlatAppearance.BorderSize = 3;
            darkButton.FlatAppearance.BorderColor = dark;
            lightButton.FlatAppearance.BorderColor = light;
            Status.Text = "Dark pixels = base; light pixels = outline. Transparency is preserved.\nSelect pressed/released textures separately. Grayscale formats and game tinting can limit colours.";
        }

        void ApplyColours()
        {
            if (Result == null) Preview();
            DialogResult = DialogResult.OK;
            Close();
        }

        void Preview()
        {
            TexturePreviewResult decoded;
            string error;
            if (!TexturePreview.TryDecode(key, source, 0, out decoded, out error))
                throw new InvalidDataException(error);
            using (decoded)
            using (var recoloured = Recolour(decoded.Bitmap, dark, light))
                Result = TplTextureEditor.ReplaceFirstImage(source, recoloured, true);
            ShowTexture(Result);
            apply.Enabled = true;
        }

        void ShowTexture(byte[] bytes)
        {
            TexturePreviewResult decoded;
            string error;
            if (!TexturePreview.TryDecode(key, bytes, 0, out decoded, out error))
                throw new InvalidDataException(error);
            using (decoded)
            {
                var old = picture.Image;
                picture.Image = new Bitmap(decoded.Bitmap);
                if (old != null)
                    old.Dispose();
            }
        }

        internal static Bitmap Recolour(Bitmap source, Color dark, Color light)
        {
            var output = new Bitmap(source.Width, source.Height);
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    int t = (c.R * 299 + c.G * 587 + c.B * 114 + 500) / 1000;
                    output.SetPixel(x, y, Color.FromArgb(c.A, (dark.R * (255 - t) + light.R * t + 127) / 255, (dark.G * (255 - t) + light.G * t + 127) / 255, (dark.B * (255 - t) + light.B * t + 127) / 255));
                }

            return output;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && picture.Image != null)
            {
                picture.Image.Dispose();
                picture.Image = null;
            }

            base.Dispose(disposing);
        }
    }
}
