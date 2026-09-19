using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class HudOverviewForm : StudioToolForm
    {
        readonly List<HudTexture> entries;
        readonly FlowLayoutPanel gallery = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };

        public HudOverviewForm(List<HudTexture> textures)
            : base("HUD Preview", "Current pending textures • Pressed/released states together • Illustration of assets, not game placement")
        {
            entries = textures.Where(t => TplTextureEditor.IsTpl(t.Archive.Files[t.Key].Data)).ToList();
            Body.Controls.Add(gallery);
            Finish();
            RenderGallery();
        }

        static Bitmap MakeThumbnail(Bitmap source)
        {
            float scale = Math.Min(230f / source.Width, 148f / source.Height);
            return new Bitmap(source, Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
        }
        void RenderGallery()
        {
            gallery.SuspendLayout();
            while (gallery.Controls.Count > 0)
                gallery.Controls[0].Dispose();
            int shown = 0;
            int failed = 0;
            foreach (var texture in entries)
            {
                byte[] data = texture.Archive.Files[texture.Key].Data;
                byte[] generated;
                string replacement;
                if (texture.Archive.Generated.TryGetValue(texture.Key, out generated))
                    data = generated;
                if (texture.Archive.Pictures.TryGetValue(texture.Key, out replacement))
                    using (var bitmap = TplTextureEditor.LoadSourceBitmap(replacement))
                        data = TplTextureEditor.ReplaceFirstImage(data, bitmap, true);
                TexturePreviewResult decoded;
                string error;
                var panel = new Panel { Width = 230, Height = 210, Margin = new Padding(6) };
                if (TexturePreview.TryDecode(texture.Key, data, 0, out decoded, out error))
                {
                    using (decoded)
                    {
                        var picture = new murumsWiiModStudio.ZoomPanPictureBox
                        {
                            Dock = DockStyle.Fill,
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Image = MakeThumbnail(decoded.Bitmap),
                            BackColor = Color.FromArgb(55, 55, 62)
                        };
                        picture.Disposed += delegate
                        {
                            if (picture.Image != null)
                            {
                                picture.Image.Dispose();
                                picture.Image = null;
                            }
                        };
                        panel.Controls.Add(picture);
                    }
                    shown++;
                }
                else
                {
                    failed++;
                    panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = error });
                }
                panel.Controls.Add(new Label
                {
                    Dock = DockStyle.Bottom, Height = 62, AutoEllipsis = true,
                    Text = Path.GetFileName(texture.Key) + "\n" + Path.GetFileName(texture.Archive.Source)
                });
                gallery.Controls.Add(panel);
            }
            Status.Text = entries.Count + L.T(" Texturen. Alle Einträge durch Scrollen erreichbar.", " textures. Scroll to reach every entry.")
                + (failed > 0 ? "; " + failed + L.T(" nicht lesbar", " unreadable") : "")
                + "\n" + L.T("Texturübersicht; keine Simulation von Spielpositionen oder Animationen.", "Texture gallery; does not simulate game placement or animations.");            gallery.ResumeLayout();
        }
    }
}