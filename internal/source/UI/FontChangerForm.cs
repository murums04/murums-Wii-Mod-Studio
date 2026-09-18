using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class FontChangerForm : StudioToolForm
    {
        StudioArchiveCopy archive;
        string source, ttf;
        byte[] original, pending;
        readonly Dictionary<string, byte[]> changes = new Dictionary<string, byte[]>();
        bool dirty;
        readonly ComboBox fonts = new ComboBox
        {
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly NumericUpDown sheet = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1,
            Width = 60
        };
        readonly PictureBox preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(50, 50, 55)
        };
        readonly PictureBox sample = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(50, 50, 55)
        };
        readonly TextBox sampleText = new TextBox
        {
            Dock = DockStyle.Top,
            Text = "Mario Kart Wii  0123456789  ÄÖÜ äöü ß",
            MaxLength = 80
        };
        readonly Button import, save, fillButton, outlineButton;
        readonly ComboBox hinting = new ComboBox
        {
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Color fillColor = Color.Black, outlineColor = Color.White;
        readonly NumericUpDown outlineSize = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 4,
            DecimalPlaces = 1,
            Increment = 0.5M,
            Value = 2,
            Width = 65
        };
        public FontChangerForm() : base("MKWii Font Changer Tool", "Open Font.szs • Choose a text font and a TTF • Preview and save a separate copy", "Font.szs · *.brfnt · *.ttf")
        {
            Action("Browse ISO/WBFS…", "Read your pack's font archive or one Wii BRFNT font.", Open);
            Action("Choose TTF…", "Load a TrueType font privately for this conversion. It is not installed in Windows. Click Preview to apply it.", delegate
            {
                string p = OpenPath("TrueType font|*.ttf");
                if (p != null)
                {
                    ttf = p;
                    Update();
                }
            });
            fonts.SelectedIndexChanged += delegate
            {
                if (fonts.SelectedItem == null)
                    return;
                Guard(delegate
                {
                    changes.TryGetValue((string)fonts.SelectedItem, out pending);
                    original = archive == null ? File.ReadAllBytes(source) : archive.Files[(string)fonts.SelectedItem].Data;
                    Update();
                });
            };
            Actions.Controls.Add(new Label { Text = "Font in archive", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(fonts);
            import = Action("Preview Latin replacement", "Render existing Latin letters, punctuation and digits. Preserve special symbols, spacing and the archive's other fonts.", delegate
            {
                int count;
                pending = new BrfntFont(original).ImportLatin(ttf, fillColor, outlineColor, (float)outlineSize.Value, (GlyphHinting)hinting.SelectedIndex, out count);
                changes[(string)fonts.SelectedItem] = pending;
                dirty = true;
                Update();
                Status.Text = count + " Latin characters rendered from " + Path.GetFileName(ttf) + ". Review the atlas; game appearance still needs testing.";
            });
            StudioHistorySymbols.Button(Action("Undo selected font", "Remove the pending replacement for this font. Other selected fonts are kept.", delegate
            {
                if (fonts.SelectedItem != null && changes.Remove((string)fonts.SelectedItem))
                {
                    pending = null;
                    dirty = changes.Count > 0;
                    Update();
                }
            }), false, L.T("Ausgewählte Schrift zurücksetzen. Andere Schriften bleiben erhalten.", "Undo selected font. Other selected fonts are kept."));
            Actions.SetFlowBreak(Actions.Controls[Actions.Controls.Count - 1], true);
            fillButton = Action("Fill: #000000…", "Choose the letter's interior colour. IA4/IA8 fonts store colours as grayscale. Click Preview to apply.", delegate
            {
                PickColour(true);
            });
            outlineButton = Action("Outline: #FFFFFF…", "Choose the outline colour. IA4/IA8 fonts store colours as grayscale. Click Preview to apply.", delegate
            {
                PickColour(false);
            });
            Actions.Controls.Add(new Label { Text = "Outline width (px)", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(outlineSize);
            hinting.Items.AddRange(new object[] { "None", "Hinted / smooth", "Hinted / sharp" });
            hinting.SelectedIndex = 0;
            Actions.Controls.Add(new Label { Text = "Hinting", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(hinting);
            StudioUx.SetHelp(hinting, "None preserves smooth outlines. Hinted modes rasterize TTF strokes against the pixel grid. Sharp uses monochrome rasterization. Click Preview to apply. These are Windows modes, not FreeType's slight/medium/full levels.");
            StudioUx.SetHelp(outlineSize, "Outline width in font-atlas pixels. Zero disables the outline. Click Preview to apply to the selected font.");
            save = ExportAction("Save font copy", "Save the previewed result in MUR_EDITED beside the source file, keeping the original filename.", Save);
            var atlasTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(4)
            };
            atlasTools.Controls.Add(new Label { Text = "Atlas page", AutoSize = true, Margin = new Padding(4, 6, 8, 0) });
            atlasTools.Controls.Add(sheet);
            Actions.SetFlowBreak(fonts, true);
            sheet.ValueChanged += delegate
            {
                Guard(Render);
            };
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            var textTab = new TabPage("Sample text");
            var atlasTab = new TabPage("Font atlas");
            textTab.Controls.Add(sample);
            textTab.Controls.Add(sampleText);
            atlasTab.Controls.Add(preview);
            atlasTab.Controls.Add(atlasTools);
            tabs.TabPages.Add(textTab);
            tabs.TabPages.Add(atlasTab);
            Body.Controls.Add(tabs);
            DarkTheme.StyleTabs(tabs);
            sampleText.TextChanged += delegate
            {
                Guard(Render);
            };
            StudioUx.SetHelp(sampleText, "Preview up to 80 characters using the actual BRFNT glyphs and spacing. Characters absent from this font leave a gap; game colours and fallback fonts are not simulated.");
            StudioUx.SetHelp(fonts, "Each archive can contain several text and symbol fonts. Only this selected font is changed.");
            StudioUx.SetHelp(sheet, "Browse the actual encoded font atlas pages, including preserved game symbols.");
            Finish();
            Update();
            FormClosed += delegate
            {
                if (preview.Image != null)
                    preview.Image.Dispose();
                if (sample.Image != null)
                    sample.Image.Dispose();
            };
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Close without saving the font changes?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        void PickColour(bool interior)
        {
            using (var picker = new ColorDialog
            {
                FullOpen = true,
                Color = interior ? fillColor : outlineColor
            }

            )
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    if (interior)
                        fillColor = picker.Color;
                    else
                        outlineColor = picker.Color;
                    fillButton.Text = "Fill: #" + (fillColor.ToArgb() & 0xFFFFFF).ToString("X6") + "…";
                    outlineButton.Text = "Outline: #" + (outlineColor.ToArgb() & 0xFFFFFF).ToString("X6") + "…";
                    Status.Text = "Click Preview Latin replacement to apply these colours. IA4/IA8 fonts convert colours to grayscale; the preview shows the exported result.";
                }
        }

        void Open()
        {
            if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Discard the pending fonts and open another source?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            string p = OpenPath("Wii fonts|*.szs;*.arc;*.brfnt");
            if (p == null)
                return;
            StudioArchiveCopy next = Path.GetExtension(p).Equals(".brfnt", StringComparison.OrdinalIgnoreCase) ? null : new StudioArchiveCopy(p);
            string[] names = next == null ? new[]
            {
                Path.GetFileName(p)
            }

            : next.Files.Keys.Where(k => k.EndsWith(".brfnt", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (names.Length == 0)
                throw new InvalidDataException("This archive contains no BRFNT fonts.");
            // Validate before replacing the current document.
            new BrfntFont(next == null ? File.ReadAllBytes(p) : next.Files[names[0]].Data);
            source = p;
            archive = next;
            pending = null;
            changes.Clear();
            dirty = false;
            fonts.Items.Clear();
            fonts.Items.AddRange(names);
            fonts.SelectedIndex = Math.Max(0, Array.FindIndex(names, n => n.EndsWith("kart_kanji_font.brfnt", StringComparison.OrdinalIgnoreCase)));
        }

        new void Update()
        {
            import.Enabled = original != null && ttf != null;
            save.Enabled = changes.Count > 0;
            if (original != null)
            {
                var f = new BrfntFont(pending ?? original);
                sheet.Value = 1;
                sheet.Maximum = f.Sheets;
                Render();
            }

            Status.Text = original == null ? "Choose your pack's Font.szs to begin." : Path.GetFileName(source) + " • " + (ttf == null ? "Choose a TTF." : Path.GetFileName(ttf)) + "\nLatin characters only; Japanese text and game symbols are preserved. Existing spacing is retained; wide fonts may be compressed. IA4/IA8: grayscale colours.";
        }

        void Render()
        {
            if (original == null)
                return;
            var f = new BrfntFont(pending ?? original);
            var b = f.Atlas((int)sheet.Value - 1);
            var old = preview.Image;
            preview.Image = b;
            if (old != null)
                old.Dispose();
            var rendered = f.Sample(sampleText.Text);
            old = sample.Image;
            sample.Image = rendered;
            if (old != null)
                old.Dispose();
        }

        void Save()
        {
            string folder = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(source), "MUR_EDITED"));
            string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(source)));
            if (string.Equals(dest, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a separate output folder.");
            if (archive == null)
            {
                Directory.CreateDirectory(folder);
                BackupManager.WriteAllBytesSafely(dest, pending);
            }
            else
            {
                var fresh = new StudioArchiveCopy(source, archive.Original);
                foreach (var change in changes)
                    fresh.Files[change.Key].Data = change.Value;
                Directory.CreateDirectory(folder);
                fresh.Save(dest);
            }

            dirty = false;
            Status.Text = "Saved: " + dest + "\nCopy this file into your test pack to check text spacing in-game.";
            ExportHelp.Show(this, folder);
        }
    }
}
