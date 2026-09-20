using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class HudMapColourForm : StudioToolForm
    {
        readonly List<HudTexture> entries;
        readonly ComboBox targets = new ComboBox
        {
            Width = 620,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        readonly NumericUpDown alpha = new NumericUpDown
        {
            Maximum = 255,
            Width = 80
        };
        readonly ComboBox channel = new ComboBox
        {
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly Button chooseColour, uniformColour;
        Color[] colours;
        Color foreground;
        bool loading;
        readonly Dictionary<HudTexture, byte[]> pending = new Dictionary<HudTexture, byte[]>();
        public Dictionary<HudTexture, byte[]> Results
        {
            get
            {
                return pending;
            }
        }

        public HudMapColourForm(List<HudTexture> layouts) : base("Map Colours", "Edit minimap colour and four-corner gradient • Review a sample • Apply, then save in Race HUD")
        {
            entries = layouts;
            Actions.Controls.Add(targets);
            foreach (var e in entries)
                targets.Items.Add(e);
            Actions.SetFlowBreak(targets, true);
            channel.Items.AddRange(new object[] { "Map base", "Top left", "Top right", "Bottom left", "Bottom right" });
            Actions.Controls.Add(channel);
            chooseColour = Action("Choose colour…", "Edit the map material or one gradient corner.", delegate
            {
                if (colours == null)
                    return;
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = colours[channel.SelectedIndex]
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        Color c = d.Color;
                        colours[channel.SelectedIndex] = Color.FromArgb((int)alpha.Value, c);
                        Changed();
                    }
            });
            Actions.Controls.Add(new Label { Text = "Opacity (0–255)", AutoSize = true, Margin = new Padding(8, 12, 0, 0) });
            Actions.Controls.Add(alpha);
            uniformColour = Action("Uniform colour…", "Choose one base colour and clear the corner gradient to white.", delegate
            {
                if (colours == null) return;
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = colours[0]
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        colours[0] = d.Color;
                        for (int i = 1; i < 5; i++)
                            colours[i] = Color.White;
                        Changed();
                        LoadChannel();
                    }
            });
            Action("Restore opened map", "Restore this layout to the source opened in Race HUD.", delegate
            {
                var t = (HudTexture)targets.SelectedItem;
                pending[t] = (byte[])t.Archive.Files[t.Key].Data.Clone();
                LoadTarget();
            });
            ExportAction("Apply map changes", "Queue map layout changes. Save edited archives in Race HUD writes separate archive copies.", delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            });
            Body.Controls.Add(preview);
            targets.SelectedIndexChanged += delegate
            {
                Guard(LoadTarget);
            };
            channel.SelectedIndexChanged += delegate
            {
                LoadChannel();
            };
            alpha.ValueChanged += delegate
            {
                if (loading || colours == null)
                    return;
                colours[channel.SelectedIndex] = Color.FromArgb((int)alpha.Value, colours[channel.SelectedIndex]);
                Changed();
            };
            channel.SelectedIndex = 0;
            Finish();
            RefreshColourButtons();
            if (entries.Count > 0)
                targets.SelectedIndex = 0;
        }

        void LoadTarget()
        {
            var t = targets.SelectedItem as HudTexture;
            if (t == null)
                return;
            byte[] b;
            if (!pending.TryGetValue(t, out b) && !t.Archive.Generated.TryGetValue(t.Key, out b))
                b = t.Archive.Files[t.Key].Data;
            colours = HudMapColours.Read(b);
            foreground = HudMapColours.Foreground(b);
            LoadChannel();
            DrawPreview();
        }

        void RefreshColourButtons()
        {
            ColourButton.SetColor(chooseColour, colours != null && channel.SelectedIndex >= 0 ? colours[channel.SelectedIndex] : Color.White);
            ColourButton.SetColor(uniformColour, colours == null ? Color.White : colours[0]);
        }

        void LoadChannel()
        {
            RefreshColourButtons();
            if (colours == null || channel.SelectedIndex < 0)
                return;
            loading = true;
            alpha.Value = colours[channel.SelectedIndex].A;
            loading = false;
        }

        void Changed()
        {
            RefreshColourButtons();
            var t = (HudTexture)targets.SelectedItem;
            byte[] b;
            if (!pending.TryGetValue(t, out b) && !t.Archive.Generated.TryGetValue(t.Key, out b))
                b = t.Archive.Files[t.Key].Data;
            pending[t] = HudMapColours.Apply(b, colours);
            DrawPreview();
        }

        void DrawPreview()
        {
            var image = new Bitmap(720, 380);
            using (var g = Graphics.FromImage(image))
            {
                g.Clear(Color.FromArgb(45, 45, 50));
                using (var path = new GraphicsPath())
                {
                    path.AddClosedCurve(new[] { new PointF(90, 120), new PointF(250, 55), new PointF(530, 70), new PointF(635, 190), new PointF(470, 305), new PointF(300, 210), new PointF(120, 285) });
                    using (var mask = new Bitmap(720, 380))
                    {
                        using (var mg = Graphics.FromImage(mask))
                        using (var pen = new Pen(Color.White, 22))
                        {
                            mg.SmoothingMode = SmoothingMode.AntiAlias;
                            mg.DrawPath(pen, path);
                        }

                        ColourGradient.Apply(mask, colours, foreground);

                        g.DrawImageUnscaled(mask, 0, 0);
                    }
                }
            }

            var old = preview.Image;
            preview.Image = image;
            if (old != null)
                old.Dispose();
            Status.Text = "Illustrative track with 35% shade; not the loaded course. Approximate material/gradient.\nGame rendering and animations may alter the result. Changes apply only to the selected layout.";
        }

        static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && preview.Image != null)
            {
                preview.Image.Dispose();
                preview.Image = null;
            }

            base.Dispose(disposing);
        }
    }
}

