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
        string archiveDetails = "";
        readonly Dictionary<string, Dictionary<int, string>> characterReports = new Dictionary<string, Dictionary<int, string>>();
        Button characterCheck;
        readonly List<string> additionalArchives = new List<string>();
        readonly Label archiveInfo = new Label { AutoSize = true, MaximumSize = new Size(980, 0), ForeColor = Color.FromArgb(190, 166, 255) };
        string source, ttf;
        byte[] original, pending;
        readonly Dictionary<string, byte[]> changes = new Dictionary<string, byte[]>();
        readonly Dictionary<string, byte[]> menuCopies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        readonly CheckBox includeRaceMessages = new CheckBox { Text = "GO / Finish (shared font)", AutoSize = true, Checked = true, Anchor = AnchorStyles.Left };

        readonly CheckBox includeHud = new CheckBox { Text = "Race HUD: numbers / km/h / slash", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
        readonly CheckBox includeMenu = new CheckBox { Text = "Menu text", Checked = true, AutoSize = true };
        readonly CheckBox includeTimers = new CheckBox { Text = "Menu timers", Checked = true, AutoSize = true };
        readonly Dictionary<string, byte[]> symbolChanges = new Dictionary<string, byte[]>();
        bool dirty;
        bool needsRender;
        bool selectingFont;
        string selectedFont;
        readonly ComboBox fonts = new ComboBox
        {
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly Button previousSheet = new Button { Text = "‹", Width = 34, Height = 28 };
        readonly Button nextSheet = new Button { Text = "›", Width = 34, Height = 28 };
        readonly Label sheetLabel = new Label { Text = "1 / 1", AutoSize = false, Width = 62, Height = 28, TextAlign = ContentAlignment.MiddleCenter };
        readonly NumericUpDown sheet = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1,
            Width = 60
        };
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(50, 50, 55)
        };
        readonly PictureBox sample = new murumsWiiModStudio.ZoomPanPictureBox
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
        readonly Button import, save, fillButton, outlineButton, symbolsButton;
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
        public FontChangerForm() : base("MKWii Font Changer Tool", "1. Load archives  •  2. Choose font groups and TTF  •  3. Preview and save copies", "Font.szs · MenuSingle.szs / MenuMulti.szs / Globe.szs · Race.szs + Race_E/U/J.szs · RaceAssets.szs / ReplacedAssets.szs")
        {
            Action("Add archive…", "Select Font.szs together with menu/HUD archives. Later selections append to the loaded files.", AddArchives).Name = "PackSourceAction";
            Action("Clear selection", "Clear loaded files; asks before discarding unsaved changes.", ClearSelection);
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
            import = Action("Preview changes", "Preview the current TTF and settings. Save can also render them directly.", GenerateReplacement);
            StudioHistorySymbols.Button(Action("Undo changes", "Discard all pending font, HUD and symbol changes.", delegate
            {
                changes.Clear();
                symbolChanges.Clear();
                menuCopies.Clear();
                pending = null;
                needsRender = false;
                dirty = false;
                Update();
            }), false, "Undo all pending changes.");
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
            characterCheck = ExportAction("Character check…", "Review replaced, missing and protected characters for selected font groups.", delegate {
                if (needsRender || characterReports.Count == 0) GenerateReplacement();
                using (var review = new FontCharacterReviewForm(characterReports)) review.ShowDialog(this);
            }, false);
            symbolsButton = ExportAction("Symbols…", "Browse and individually replace game symbols. TTF conversion preserves them.", EditSymbols, false);
            var scopes = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            scopes.Controls.AddRange(new Control[] { includeMenu, includeTimers, includeRaceMessages, includeHud });
            scopes.SetFlowBreak(includeHud, true);
            scopes.Controls.Add(archiveInfo);
            var archiveButton = new Button { Text = "Archive overview…", AutoSize = true };
            archiveButton.Click += delegate { using (var overview = new FontArchiveInfoForm(archiveDetails)) overview.ShowDialog(this); };
            scopes.Controls.Add(archiveButton);
            scopes.SizeChanged += delegate { archiveInfo.MaximumSize = new Size(Math.Max(200, scopes.ClientSize.Width - 12), 0); };
            Body.Controls.Add(scopes);
            scopes.BringToFront();
            foreach (var choice in new[] { includeMenu, includeTimers, includeRaceMessages, includeHud })
                choice.CheckedChanged += delegate { SettingsChanged(); };
            StudioUx.SetHelp(includeMenu, "Change supported letters and number fonts in Font.szs. Symbols are preserved.");
            StudioUx.SetHelp(includeTimers, "Change timer pictures in adjacent menu archives.");
            StudioUx.SetHelp(includeRaceMessages, "Change the shared font used by countdown, GO and Finish; other screens using that font also change.");
            StudioUx.SetHelp(includeHud, "Change race digits, km/h and separators, including RR overrides. Position numbers remain unchanged.");
            var atlasTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(4)
            };
            atlasTools.Controls.Add(new Label { Text = "Atlas page", AutoSize = true, Margin = new Padding(4, 6, 8, 0) });
            atlasTools.Controls.Add(previousSheet);
            atlasTools.Controls.Add(sheetLabel);
            atlasTools.Controls.Add(nextSheet);
            previousSheet.Click += delegate { if (sheet.Value > sheet.Minimum) sheet.Value--; };
            nextSheet.Click += delegate { if (sheet.Value < sheet.Maximum) sheet.Value++; };
            Actions.SetFlowBreak(fonts, false);
            sheet.ValueChanged += delegate
            {
                RefreshSheetNavigation();
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
            tabs.TabPages.Add(atlasTab);
            tabs.TabPages.Add(textTab);
            tabs.SelectedTab = atlasTab;
            Body.Controls.Add(tabs);
            scopes.SendToBack();
            DarkTheme.StyleTabs(tabs);
            sampleText.TextChanged += delegate
            {
                Guard(Render);
            };
            StudioUx.SetHelp(sampleText, "Preview up to 80 characters using the actual BRFNT glyphs and spacing. Characters absent from this font leave a gap; game colours and fallback fonts are not simulated.");
            StudioUx.SetHelp(fonts, "Select a font for preview. The checkboxes choose which font groups are exported. Position numbers and game symbols are preserved by TTF import.");
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

        static bool IsCompanion(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path).Split('_')[0];
            return new[] { "Title", "MenuSingle", "MenuMulti", "Globe", "Channel", "Award", "Race", "RaceAssets", "ReplacedAssets" }
                .Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        static List<string> FindCompanions(string fontPath)
        {
            var result = new List<string>();
            if (Path.GetExtension(fontPath).Equals(".brfnt", StringComparison.OrdinalIgnoreCase)) return result;
            string folder = Path.GetDirectoryName(Path.GetFullPath(fontPath));
            var folders = new List<string> { folder, Path.Combine(folder, "Assets") };
            if (Path.GetFileName(folder).Equals("UI", StringComparison.OrdinalIgnoreCase))
                folders.Add(Path.Combine(Path.GetDirectoryName(folder), "Assets"));
            foreach (string dir in folders.Where(Directory.Exists))
                foreach (string candidate in Directory.GetFiles(dir, "*.szs").Where(IsCompanion))
                {
                    if (result.Any(p => Path.GetFileName(p).Equals(Path.GetFileName(candidate), StringComparison.OrdinalIgnoreCase))) continue;
                    try { new StudioArchiveCopy(candidate); result.Add(candidate); }
                    catch (InvalidDataException) { }
                }
            return result;
        }

        void AddArchivePaths(IEnumerable<string> paths)
        {
            if (archive == null) throw new InvalidOperationException("Add Font.szs first.");
            var next = new List<string>(additionalArchives);
            foreach (string item in paths)
            {
                string path = Path.GetFullPath(item);
                if (!IsCompanion(path)) throw new InvalidDataException("Add a menu/HUD archive: MenuSingle, MenuMulti, Globe, Race, RaceAssets or ReplacedAssets. Add Font.szs first.");
                new StudioArchiveCopy(path);
                string existing = next.FirstOrDefault(p => Path.GetFileName(p).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));
                if (existing != null && !existing.Equals(path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("An archive named " + Path.GetFileName(path) + " is already loaded from another folder. Open the intended pack's Font.szs first.");
                if (existing == null) next.Add(path);
            }
            additionalArchives.Clear();
            additionalArchives.AddRange(next);
            SettingsChanged();
        }

        void ClearSelection()
        {
            if (dirty && StudioMessageBox.Show(this, "Discard unsaved changes and clear all loaded files?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            archive = null; source = null; original = pending = null; selectedFont = null;
            changes.Clear(); symbolChanges.Clear(); menuCopies.Clear(); characterReports.Clear(); additionalArchives.Clear();
            fonts.Items.Clear(); dirty = needsRender = false;
            var old = preview.Image; preview.Image = null; if (old != null) old.Dispose();
            old = sample.Image; sample.Image = null; if (old != null) old.Dispose();
            Update();
            PackSelection.SourceCleared(this);
        }

        void AddArchives()
        {
            string filter = "Font / menu / HUD archives|Font.szs;Font_*.szs;homeBtn*.szs;*.brfnt;" + ToolArchiveFilters.FontExtras.Split('|')[1];
            AddSelectedPaths(GameArchiveImportForm.SelectMany(this, filter));
        }

        void AddSelectedPaths(string[] paths)
        {
            if (paths.Length == 0) return;
            var fontPaths = new List<string>();
            foreach (string path in paths)
            {
                if (Path.GetExtension(path).Equals(".brfnt", StringComparison.OrdinalIgnoreCase))
                { new BrfntFont(File.ReadAllBytes(path)); fontPaths.Add(path); }
                else
                {
                    var candidate = new StudioArchiveCopy(path);
                    if (candidate.Files.Keys.Any(k => k.EndsWith(".brfnt", StringComparison.OrdinalIgnoreCase))) fontPaths.Add(path);
                    else if (!IsCompanion(path)) throw new InvalidDataException("This archive has no supported fonts or HUD/menu textures.");
                }
            }
            if (fontPaths.Count > 1) throw new InvalidDataException("Choose one Font.szs plus any menu/HUD archives. Clear selection to switch to another font archive.");
            if (source == null)
            {
                if (fontPaths.Count != 1) throw new InvalidDataException("Include Font.szs in the first selection, together with the menu/HUD archives you want.");
            }
            else if (fontPaths.Any(p => !Path.GetFullPath(p).Equals(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("A font source is already loaded. Use Clear selection before switching fonts.");
            string fontSource = source ?? fontPaths[0];
            var extras = paths.Where(p => !fontPaths.Contains(p)).ToArray();
            if (Path.GetExtension(fontSource).Equals(".brfnt", StringComparison.OrdinalIgnoreCase) && extras.Length > 0)
                throw new InvalidDataException("Use Font.szs with additional archives; an individual BRFNT is edited separately.");
            var known = source == null ? FindCompanions(fontSource) : new List<string>(additionalArchives);
            foreach (string extra in extras)
                if (known.Any(p => Path.GetFileName(p).Equals(Path.GetFileName(extra), StringComparison.OrdinalIgnoreCase)
                    && !Path.GetFullPath(p).Equals(Path.GetFullPath(extra), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("An archive with this name is already loaded from another folder: " + Path.GetFileName(extra));
            if (source == null) LoadSource(fontSource);
            if (extras.Length > 0) AddArchivePaths(extras);
        }
        void RefreshArchiveInfo()
        {
            string needed = "Menu text / GO / Finish: Font.szs. Timers: MenuSingle.szs, MenuMulti.szs, Globe.szs.";
            needed += "\nHUD: Race.szs + Race_E.szs / Race_U.szs / Race_J.szs (your region); RR: RaceAssets.szs + ReplacedAssets.szs.";
            if (source == null) { archiveDetails = needed; archiveInfo.Text = "1. Add Font.szs; matching menu / HUD sources load automatically."; return; }
            string loaded = String.Join(", ", new[] { Path.GetFileName(source) }.Concat(additionalArchives.Select(Path.GetFileName)));
            var missing = new List<string>();
            if (includeTimers.Checked && archive != null)
                foreach (string name in new[] { "MenuSingle.szs", "MenuMulti.szs", "Globe.szs" })
                    if (!additionalArchives.Any(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase))) missing.Add(name);
            if (includeHud.Checked && archive != null)
            {
                foreach (string name in new[] { "Race.szs", "RaceAssets.szs", "ReplacedAssets.szs" })
                    if (!additionalArchives.Any(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase))) missing.Add(name);
                if (!additionalArchives.Any(p => Path.GetFileName(p).StartsWith("Race_", StringComparison.OrdinalIgnoreCase)))
                    missing.Add("Race_E.szs / Race_U.szs / Race_J.szs (one region)");
            }
            archiveDetails = needed + "\nLoaded: " + loaded
                + (missing.Count == 0 ? "" : "\nNot loaded for selected groups: " + String.Join(", ", missing) + ". Use Add archive; export covers loaded archives only.");
            archiveDetails += "\n\nSource paths:\n" + String.Join("\n", new[] { source }.Concat(additionalArchives));
            archiveInfo.Text = (additionalArchives.Count + 1) + " files loaded"
                + (missing.Count == 0 ? " • Sources ready for selected groups." : " • " + missing.Count + " source requirements missing — see Archive overview.");
            StudioUx.SetHelp(archiveInfo, archiveDetails);
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
                Filter = ToolArchiveFilters.Fonts,
                InitialDirectory = folder ?? "",
                FileName = "Font.szs",
                CheckFileExists = true,
                Multiselect = false,
                RestoreDirectory = true
            })
                p = ToolArchiveFilters.Show(picker, this) == DialogResult.OK ? picker.FileName : null;
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
            var companions = FindCompanions(p);
            characterReports.Clear();
            source = p;
            additionalArchives.Clear();
            additionalArchives.AddRange(companions);
            PackSelection.SourceLoaded(this);
            archive = next;
            pending = null;
            changes.Clear();
            menuCopies.Clear();
            needsRender = false;
            dirty = false;
            fonts.Items.Clear();
            fonts.Items.AddRange(names);
            fonts.SelectedIndex = Math.Max(0, Array.FindIndex(names, n => n.EndsWith("kart_kanji_font.brfnt", StringComparison.OrdinalIgnoreCase)));
        }

        new void Update()
        {
            RefreshArchiveInfo();
            RefreshSheetNavigation();
            characterCheck.Enabled = original != null && ttf != null;
            symbolsButton.Enabled = original != null;
            includeMenu.Enabled = true; includeTimers.Enabled = includeRaceMessages.Enabled = includeHud.Enabled = archive != null;
            save.Text = (includeRaceMessages.Checked || includeHud.Checked) && archive != null ? "Save font / HUD copies" : "Save font copy";
            import.Enabled = original != null && ttf != null;
            save.Enabled = changes.Count > 0 || menuCopies.Count > 0 || (needsRender && original != null && ttf != null);
            if (original != null)
            {
                var f = new BrfntFont(pending ?? original);
                fillButton.Enabled = outlineButton.Enabled = f.Format != 0 && f.Format != 1;
                sheet.Value = 1;
                sheet.Maximum = f.Sheets;
                RefreshSheetNavigation();
                Render();
            }

            Status.Text = original == null ? "Choose your pack's Font.szs to begin." : Path.GetFileName(source) + " • " + fonts.Items.Count + " fonts • " + (ttf == null ? "Choose a TTF." : Path.GetFileName(ttf)) + "\nLetters supported by the TTF are replaced; missing characters and game symbols stay original. Existing spacing is retained; wide fonts may be compressed. I4/I8: coverage mask, game-defined colour. IA4/IA8: grayscale colours.";
        }

        void RefreshSheetNavigation()
        {
            sheetLabel.Text = sheet.Value + " / " + sheet.Maximum;
            previousSheet.Enabled = original != null && sheet.Value > sheet.Minimum;
            nextSheet.Enabled = original != null && sheet.Value < sheet.Maximum;
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

        void EditSymbols()
        {
            var inputs = archive == null ? new Dictionary<string, byte[]> { { selectedFont, symbolChanges.ContainsKey(selectedFont) ? symbolChanges[selectedFont] : original } }
                : archive.Files.Where(p => p.Key.EndsWith(".brfnt", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(p => p.Key, p => symbolChanges.ContainsKey(p.Key) ? symbolChanges[p.Key] : p.Value.Data);
            using (var dialog = new FontSymbolsForm(inputs))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var item in dialog.Changes) { changes[item.Key] = item.Value; symbolChanges[item.Key] = item.Value; }
                    changes.TryGetValue(selectedFont, out pending);
                    dirty = changes.Count > 0;
                    Update();
                }
        }
        void SettingsChanged()
        {
            needsRender = original != null && ttf != null;
            if (needsRender) dirty = true;
            Update();
            if (needsRender)
                Status.Text = "Settings changed. Save font copy applies them directly; Preview changes is optional.";
        }

        void GenerateReplacement()
        {
            if (original == null || ttf == null || fonts.SelectedItem == null)
                throw new InvalidOperationException("Open a Wii font and choose a TTF first.");
            var generated = new Dictionary<string, byte[]>();
            var reports = new Dictionary<string, Dictionary<int, string>>();
            string[] keys = archive == null ? (includeMenu.Checked ? new[] { selectedFont } : new string[0])
                : archive.Files.Keys.Where(k => k.EndsWith(".brfnt", StringComparison.OrdinalIgnoreCase)
                    && (Path.GetFileName(k).Equals("tt_kart_font_rodan_ntlg_pro_b.brfnt", StringComparison.OrdinalIgnoreCase)
                        ? includeRaceMessages.Checked : includeMenu.Checked)).ToArray();
            foreach (string key in keys)
            {
                byte[] bytes = symbolChanges.ContainsKey(key) ? symbolChanges[key] : archive == null ? original : archive.Files[key].Data;
                var font = new BrfntFont(bytes);
                int count;
                var report = new Dictionary<int, string>();
                byte[] converted = font.ImportLatin(ttf, fillColor, outlineColor, (float)outlineSize.Value,
                    (GlyphHinting)hinting.SelectedIndex, out count, BrfntFont.HasGameNumbers(key), report);
                reports.Add(key, report);
                if (count > 0) generated.Add(key, converted);
            }

            var menus = includeTimers.Checked && archive != null ? MenuTimerFonts.Generate(additionalArchives, ttf, fillColor, outlineColor,
                (float)outlineSize.Value, (GlyphHinting)hinting.SelectedIndex) : new Dictionary<string, byte[]>();
            if (includeHud.Checked && archive != null)
                foreach (var entry in HudFontTextures.Generate(additionalArchives, ttf, fillColor, outlineColor,
                    (float)outlineSize.Value, (GlyphHinting)hinting.SelectedIndex))
                    menus.Add(entry.Key, entry.Value);
            changes.Clear();
            foreach (var entry in symbolChanges) changes[entry.Key] = entry.Value;
            foreach (var entry in generated) changes[entry.Key] = entry.Value;
            characterReports.Clear();
            foreach (var entry in reports) characterReports.Add(entry.Key, entry.Value);
            menuCopies.Clear();
            foreach (var entry in menus) menuCopies.Add(entry.Key, entry.Value);
            changes.TryGetValue(selectedFont, out pending);
            needsRender = false;
            dirty = true;
            Update();
            Status.Text = generated.Count + " text fonts + " + menuCopies.Count
                + " menu / HUD archives ready. Copy ALL exported files into your pack, including RaceAssets and ReplacedAssets when exported.";
        }

        void Save()
        {
            if (String.IsNullOrEmpty(source)) throw new InvalidOperationException("Open a Wii font first.");
            string folder = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(source), "MUR_EDITED"));
            string dest = SaveCopy(folder);
            Status.Text = "Saved: " + dest + "\nCopy all exported files into your pack. Timers, HUD numbers, km/h and slash need the exported menu/HUD archives too.";
            ExportHelp.Show(this, folder);
        }

        string SaveCopy(string folder)
        {
            if (needsRender) GenerateReplacement();
            if (changes.Count == 0 && menuCopies.Count == 0) throw new InvalidOperationException("No replacements are ready. Choose a TTF and font groups; Character check explains preserved or missing characters.");
            string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(source)));
            if (string.Equals(dest, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a separate output folder.");
            foreach (var entry in menuCopies)
                if (Path.GetFullPath(Path.Combine(folder, Path.GetFileName(entry.Key))).Equals(Path.GetFullPath(entry.Key), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Choose a separate output folder for menu copies.");
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

            foreach (var entry in menuCopies)
                BackupManager.WriteAllBytesSafely(Path.Combine(folder, Path.GetFileName(entry.Key)), entry.Value);
            dirty = false;
            return dest;
        }
    }
}
