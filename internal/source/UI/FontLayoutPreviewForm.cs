using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class FontLayoutPreviewForm : StudioToolForm
    {
        sealed class LayoutTarget
        {
            internal string Source, FontName;
            internal BrlytPaneInfo Pane;
            public override string ToString() { return Source + " / " + Pane.Name + " [" + FontName + "]"; }
        }
        readonly ComboBox targets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 750 };
        readonly TextBox input = new TextBox { Text = "PLEASE WAIT A MOMENT.  123456", MaxLength = 80, Width = 460 };
        readonly PictureBox picture = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly Dictionary<string, byte[]> originalFonts, changedFonts;
        internal FontLayoutPreviewForm(IEnumerable<string> sources, Dictionary<string, byte[]> originals, Dictionary<string, byte[]> changes)
            : base(L.T("Schrift im Textfeld prüfen", "Check font in text fields"),
                L.T("Geladene Layoutmaße • Original und Vorschau • Überbreite sichtbar machen",
                    "Loaded layout dimensions • Original and preview • Reveal overflowing text"))
        {
            originalFonts = originals.ToDictionary(p => Path.GetFileName(p.Key), p => p.Value, StringComparer.OrdinalIgnoreCase);
            changedFonts = changes.ToDictionary(p => Path.GetFileName(p.Key), p => p.Value, StringComparer.OrdinalIgnoreCase);
            Actions.Controls.Add(targets); Actions.SetFlowBreak(targets, true);
            Actions.Controls.Add(input);
            var presets = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            presets.Items.Add(L.T("Beispieltext wählen…", "Choose sample text…"));
            presets.SelectedIndex = 0;
            presets.Items.AddRange(new object[] { "PLEASE WAIT A MOMENT.", "WWWWWWWWWW", "PLAYER 99999 VR", "SW2 RAINBOW ROAD", "H (αηκγ)  あいうえお", "00:00.000  123 km/h" });
            presets.SelectedIndexChanged += delegate { if (presets.SelectedIndex > 0) input.Text = (string)presets.SelectedItem; };
            Actions.Controls.Add(presets);
            Body.Controls.Add(picture);
            var warnings = new List<string>();
            foreach (string source in sources.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var archive = new StudioArchiveCopy(source);
                    foreach (var entry in archive.Files.Where(e => e.Key.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase)))
                    {
                        var layout = BrlytDocument.FromBytes(entry.Value.Data);
                        foreach (var pane in layout.Panes.Where(p => p.Magic == "txt1" && p.FontId >= 0 && p.FontId < layout.Fonts.Count))
                        {
                            string font = Path.GetFileName(layout.Fonts[pane.FontId]);
                            if (!originalFonts.ContainsKey(font) || pane.Width <= 0 || pane.Height <= 0) continue;
                            targets.Items.Add(new LayoutTarget { Source = Path.GetFileName(source) + " / " + Path.GetFileName(entry.Key), Pane = pane, FontName = font });
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is InvalidDataException || ex is NotSupportedException)) throw;
                    warnings.Add(Path.GetFileName(source) + ": " + ex.Message);
                }
            }
            targets.SelectedIndexChanged += delegate { Guard(Draw); };
            input.TextChanged += delegate { Guard(Draw); };
            Finish();
            if (targets.Items.Count > 0) targets.SelectedIndex = 0;
            else Status.Text = L.T("Keine passenden Textfelder. Menü-/HUD-Archive und die zugehörigen BRFNTs laden.", "No matching text fields. Load menu/HUD archives and their BRFNTs.");
            if (warnings.Count > 0) StudioUx.SetHelp(targets, String.Join(Environment.NewLine, warnings));
        }
        void Draw()
        {
            var target = targets.SelectedItem as LayoutTarget;
            if (target == null) return;
            byte[] changed;
            if (!changedFonts.TryGetValue(target.FontName, out changed)) changed = originalFonts[target.FontName];
            var original = new BrfntFont(originalFonts[target.FontName]);
            var after = new BrfntFont(changed);
            var pane = target.Pane;
            float beforeWidth, afterWidth;
            using (var left = original.LayoutSample(input.Text, pane.FontWidth, pane.FontHeight, pane.CharSize, out beforeWidth))
            using (var right = after.LayoutSample(input.Text, pane.FontWidth, pane.FontHeight, pane.CharSize, out afterWidth))
            {
                float extent = Math.Max(pane.Width, Math.Max(beforeWidth, afterWidth));
                float fit = Math.Min(3, Math.Min(820 / Math.Max(1, extent + 40), 115 / Math.Max(1, pane.Height + 32)));
                var image = new Bitmap(920, 400);
                using (var g = Graphics.FromImage(image))
                {
                    g.Clear(Color.FromArgb(37, 38, 44));
                    for (int row = 0; row < 2; row++)
                    {
                        float y = 62 + row * 180;
                        float textWidth = row == 0 ? beforeWidth : afterWidth;
                        g.DrawString(row == 0 ? L.T("Original", "Original") : L.T("Aktueller Font", "Current font"), Font, Brushes.White, 24, y - 32);
                        using (var pen = new Pen(textWidth > pane.Width ? Color.OrangeRed : Color.MediumPurple, 2))
                            g.DrawRectangle(pen, 30, y, pane.Width * fit, pane.Height * fit);
                        Bitmap text = row == 0 ? left : right;
                        // Text über den Feldrand hinaus zeigen, damit Überbreite erkennbar bleibt.
                        g.DrawImage(text, new RectangleF(30 - 16 * fit, y - 16 * fit, text.Width * fit, text.Height * fit));
                    }
                }
                Image old = picture.Image; picture.Image = image; if (old != null) old.Dispose();
            }
            int missing = input.Text.Count(c => !after.Characters.ContainsKey(c));
            Status.Text = String.Format(L.T("Feld: {0:0.#} × {1:0.#} • Textbreite: {2:0.#} → {3:0.#} • fehlende Zeichen: {4}\nStatische Einzelzeile vor Animation/Elterntransformationen. Roter Rahmen = Überbreite; kein vollständiger Spielrenderer.",
                "Field: {0:0.#} × {1:0.#} • Text width: {2:0.#} → {3:0.#} • missing characters: {4}\nStatic single line before animations/parent transforms. Red outline = overflow; not a full game renderer."),
                pane.Width, pane.Height, beforeWidth, afterWidth, missing);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && picture.Image != null) { picture.Image.Dispose(); picture.Image = null; }
            base.Dispose(disposing);
        }
    }
}

