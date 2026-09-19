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
        bool needsRender;
        bool selectingFont;
        string selectedFont;
        readonly ComboBox fonts = new ComboBox
        {
            Width = 260,
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
            Action("Open file…", "Read your pack's font archive or one Wii BRFNT font.", Open);
            Action("Choose TTF…", "Load a TrueType font privately for this conversion. It is not installed in Windows. Preview is optional; Save applies the current settings.", delegate
            {
                string p = OpenPath("TrueType font|*.ttf");
                if (p != null)
                {
                    ttf = p;
                    SettingsChanged();
                }
            });
            fonts.SelectedIndexChanged += delegate
            {
                if (fonts.SelectedItem == null || selectingFont)
                    return;
                Guard(delegate
                {
                    string nextFont = (string)fonts.SelectedItem;
                    try
                    {
                        if (needsRender) GenerateReplacement();
                        byte[] nextOriginal = archive == null ? File.ReadAllBytes(source) : archive.Files[nextFont].Data;
                        new BrfntFont(nextOriginal);
                        selectedFont = nextFont;
                        original = nextOriginal;
                        changes.TryGetValue(nextFont, out pending);
                        needsRender = false;
                        Update();
                    }
                    catch
                    {
                        selectingFont = true;
                        fonts.SelectedItem = selectedFont;
                        selectingFont = false;
                        throw;
                    }
                });
            };
            Actions.Controls.Add(new Label { Text = "Font in archive", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(fonts);
            import = Action("Preview Latin replacement", "Preview the current TTF and settings. Save can also render them directly.", GenerateReplacement);
            StudioHistorySymbols.Button(Action("Undo selected font", "Remove the pending replacement for this font. Other selected fonts are kept.", delegate
            {
                if (fonts.SelectedItem != null && (needsRender || changes.ContainsKey((string)fonts.SelectedItem)))
                {
                    changes.Remove((string)fonts.SelectedItem);
                    pending = null;
                    needsRender = false;
                    dirty = changes.Count > 0;
                    Update();
                }
            }), false, L.T("Ausgewählte Schrift zurücksetzen. Andere Schriften bleiben erhalten.", "Undo selected font. Other selected fonts are kept."));
            Actions.SetFlowBreak(Actions.Controls[Actions.Controls.Count - 1], true);
            fillButton = Action("Fill: #000000…", "Choose the letter's interior colour. IA4/IA8 fonts store colours as grayscale. Preview is optional; Save applies the current settings.", delegate
            {
                PickColour(true);
            });
            outlineButton = Action("Outline: #FFFFFF…", "Choose the outline colour. IA4/IA8 fonts store colours as grayscale. Preview is optional; Save applies the current settings.", delegate
            {
                PickColour(false);
            });
            Actions.Controls.Add(new Label { Text = "Outline width (px)", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(outlineSize);
            hinting.Items.AddRange(new object[] { "None", "Hinted / smooth", "Hinted / sharp" });
            hinting.SelectedIndex = 0;
            Actions.Controls.Add(new Label { Text = "Hinting", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(hinting);
            StudioUx.SetHelp(hinting, "None preserves smooth outlines. Hinted modes rasterize TTF strokes against the pixel grid. Sharp uses monochrome rasterization. Preview is optional; Save applies the current settings. These are Windows modes, not FreeType's slight/medium/full levels.");
            StudioUx.SetHelp(outlineSize, "Outline width in font-atlas pixels. Zero disables the outline. Save applies the current settings to the selected font; Preview is optional.");
            save = ExportAction("Save font copy", "Render current settings if needed and save a separate copy in MUR_EDITED, keeping the original filename.", Save);
            var atlasTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(4)
            };
            atlasTools.Controls.Add(new Label { Text = "Atlas page", AutoSize = true, Margin = new Padding(4, 6, 8, 0) });
            atlasTools.Controls.Add(sheet);
            Actions.SetFlowBreak(fonts, false);
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
            AlignActionRows();
            outlineSize.ValueChanged += delegate { SettingsChanged(); };
            hinting.SelectedIndexChanged += delegate { SettingsChanged(); };
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

        private void AlignActionRows()
        {
            var controls = Actions.Controls.Cast<Control>().ToArray();
            var first = controls.TakeWhile(control => control != fillButton).ToArray();
            var second = controls.SkipWhile(control => control != fillButton).ToArray();
            Actions.SuspendLayout();
            Actions.Controls.Clear();
            Actions.FlowDirection = FlowDirection.TopDown;
            Actions.WrapContents = false;
            foreach (var rowControls in new[] { first, second })
            {
                var row = new TableLayoutPanel
                {
                    Height = 42,
                    ColumnCount = rowControls.Length + 1,
                    RowCount = 1,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                for (int index = 0; index < rowControls.Length; index++)
                {
                    Control control = rowControls[index];
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    control.Margin = new Padding(3, 4, 3, 4);
                    control.Anchor = AnchorStyles.Left;
                    var button = control as Button;
                    if (button != null)
                    {
                        int width = Math.Max(button.Width, button.GetPreferredSize(Size.Empty).Width);
                        button.AutoSize = false;
                        button.Size = new Size(width, 34);
                    }
                    if (control is ComboBox)
                    {
                        var cell = new Panel
                        {
                            Width = control.Width,
                            Height = 34,
                            Margin = control.Margin,
                            Anchor = AnchorStyles.Left
                        };
                        control.Margin = Padding.Empty;
                        cell.Controls.Add(control);
                        cell.Layout += delegate
                        {
                            control.Left = 0;
                            control.Top = Math.Max(0, (cell.ClientSize.Height - control.Height) / 2);
                        };
                        row.Controls.Add(cell, index, 0);
                    }
                    else
                        row.Controls.Add(control, index, 0);
                }
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Actions.Controls.Add(row);
            }
            Actions.SizeChanged += delegate
            {
                foreach (Control row in Actions.Controls)
                    row.Width = Math.Max(1, Actions.ClientSize.Width - Actions.Padding.Horizontal);
            };
            foreach (Control row in Actions.Controls)
                row.Width = Math.Max(1, Actions.ClientSize.Width - Actions.Padding.Horizontal);
            Actions.ResumeLayout(true);
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
                    SettingsChanged();
                }
        }

        void Open()
        {
            if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Discard the pending fonts and open another source?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            string p;
            string folder = PackSelection.Folder(this);
            if (String.IsNullOrEmpty(folder) && !String.IsNullOrEmpty(source))
                folder = Path.GetDirectoryName(source);
            using (var picker = new OpenFileDialog
            {
                Title = L.T("Font.szs oder Schriftdatei öffnen", "Open Font.szs or a font file"),
                Filter = "Wii fonts (Font.szs, BRFNT)|*.szs;*.arc;*.brfnt",
                InitialDirectory = folder ?? "",
                FileName = "Font.szs",
                CheckFileExists = true,
                Multiselect = false,
                RestoreDirectory = true
            })
                p = picker.ShowDialog(this) == DialogResult.OK ? picker.FileName : null;
            if (p == null)
                return;
            LoadSource(p);
        }

        void LoadSource(string p)
        {
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
            PackSelection.SourceLoaded(this);
            archive = next;
            pending = null;
            changes.Clear();
            needsRender = false;
            dirty = false;
            fonts.Items.Clear();
            fonts.Items.AddRange(names);
            fonts.SelectedIndex = Math.Max(0, Array.FindIndex(names, n => n.EndsWith("kart_kanji_font.brfnt", StringComparison.OrdinalIgnoreCase)));
        }

        new void Update()
        {
            import.Enabled = original != null && ttf != null;
            save.Enabled = changes.Count > 0 || (needsRender && original != null && ttf != null);
            if (original != null)
            {
                var f = new BrfntFont(pending ?? original);
                fillButton.Enabled = outlineButton.Enabled = f.Format != 0 && f.Format != 1;
                sheet.Value = 1;
                sheet.Maximum = f.Sheets;
                Render();
            }

            Status.Text = original == null ? "Choose your pack's Font.szs to begin." : Path.GetFileName(source) + " • " + fonts.Items.Count + " fonts • " + (ttf == null ? "Choose a TTF." : Path.GetFileName(ttf)) + "\nLatin characters only; Japanese text and game symbols are preserved. Existing spacing is retained; wide fonts may be compressed. I4/I8: coverage mask, game-defined colour. IA4/IA8: grayscale colours.";
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

        void SettingsChanged()
        {
            needsRender = original != null && ttf != null;
            if (needsRender) dirty = true;
            Update();
            if (needsRender)
                Status.Text = "Settings changed. Save font copy applies them directly; Preview Latin replacement is optional.";
        }

        void GenerateReplacement()
        {
            if (original == null || ttf == null || fonts.SelectedItem == null)
                throw new InvalidOperationException("Open a Wii font and choose a TTF first.");
            int count;
            byte[] rendered = new BrfntFont(original).ImportLatin(ttf, fillColor, outlineColor,
                (float)outlineSize.Value, (GlyphHinting)hinting.SelectedIndex, out count);
            pending = rendered;
            changes[selectedFont] = rendered;
            needsRender = false;
            dirty = true;
            Update();
            Status.Text = count + " Latin characters rendered from " + Path.GetFileName(ttf) + ". Save font copy to export.";
        }
        void Save()
        {
            if (String.IsNullOrEmpty(source)) throw new InvalidOperationException("Open a Wii font first.");
            string folder = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(source), "MUR_EDITED"));
            string dest = SaveCopy(folder);
            Status.Text = "Saved: " + dest + "\nCopy this file into your test pack to check text spacing in-game.";
            ExportHelp.Show(this, folder);
        }

        string SaveCopy(string folder)
        {
            if (needsRender) GenerateReplacement();
            if (changes.Count == 0) throw new InvalidOperationException("Choose a TTF or preview a replacement before saving.");
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
            return dest;
        }
    }
}
