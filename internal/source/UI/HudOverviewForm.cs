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
        public HudOverviewForm(List<HudTexture> textures) : base("HUD Preview", "Current pending textures • Pressed/released states together • Illustration of assets, not game placement")
        {
            var gallery = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            Body.Controls.Add(gallery);
            int shown = 0;
            foreach (var t in textures.Where(t => TplTextureEditor.IsTpl(t.Archive.Files[t.Key].Data)).Take(100))
            {
                byte[] data = t.Archive.Files[t.Key].Data, b;
                string path;
                if (t.Archive.Generated.TryGetValue(t.Key, out b))
                    data = b;
                if (t.Archive.Pictures.TryGetValue(t.Key, out path))
                    using (var bitmap = TplTextureEditor.LoadSourceBitmap(path))
                        data = TplTextureEditor.ReplaceFirstImage(data, bitmap, true);
                TexturePreviewResult d;
                string error;
                if (!TexturePreview.TryDecode(t.Key, data, 0, out d, out error))
                    continue;
                using (d)
                {
                    var panel = new Panel
                    {
                        Width = 230,
                        Height = 210,
                        Margin = new Padding(6)
                    };
                    var picture = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = new Bitmap(d.Bitmap),
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
                    panel.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 62, Text = Path.GetFileName(t.Key) + "\n" + Path.GetFileName(t.Archive.Source), AutoEllipsis = true });
                    gallery.Controls.Add(panel);
                    shown++;
                }
            }

            Finish();
            Status.Text = shown + " textures shown (maximum 100). Uses the pending exported texture bytes.\nLayout position, game tinting and animation are not simulated. Input on/off states have separate labelled tiles.";
        }
    }
}
