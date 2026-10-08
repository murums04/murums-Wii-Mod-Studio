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
        SpecialEditorHistory editHistory;
        int sourceGeneration;
        StudioArchiveCopy archive;
        string archiveDetails = "";
        readonly Dictionary<string, Dictionary<int, string>> characterReports = new Dictionary<string, Dictionary<int, string>>();
        Button characterCheck;
        readonly List<string> additionalArchives = new List<string>();
        readonly Label archiveInfo = new Label { AutoSize = true, MaximumSize = new Size(980, 0), ForeColor = DarkTheme.Muted };
        string source, ttf, japaneseTtf, otherTtf;
        int mainScript;
        FontScriptSources ScriptSources
        {
            get { return new FontScriptSources { Latin = ttf, Japanese = japaneseTtf, Other = otherTtf, MainScript = mainScript }; }
        }
        string MainTtf { get { return ScriptSources.MainPath; } }
        byte[] original, pending;
        readonly Dictionary<string, byte[]> changes = new Dictionary<string, byte[]>();
        readonly Dictionary<string, StudioArchiveCopy> menuCopies = new Dictionary<string, StudioArchiveCopy>(StringComparer.OrdinalIgnoreCase);
        readonly CheckBox includeRaceMessages = new CheckBox { Text = L.T("GO / Ziel (gemeinsame Schrift)", "GO / Finish (shared font)"), AutoSize = true, Checked = true, Anchor = AnchorStyles.Left };

        readonly CheckBox includeHud = new CheckBox { Text = L.T("Renn-HUD: Zeit, Runde, km/h", "Race HUD: time, laps, km/h"), Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
        readonly CheckBox includeMenu = new CheckBox { Text = L.T("Menütext", "Menu text"), Checked = true, AutoSize = true };
        readonly CheckBox includeTimers = new CheckBox { Text = L.T("Menü-Zeitanzeigen", "Menu timers"), Checked = true, AutoSize = true };
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
            BackColor = DarkTheme.Panel2
        };
        readonly PictureBox sample = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = DarkTheme.Panel2
        };
        readonly TextBox sampleText = new TextBox
        {
            Dock = DockStyle.Top,
            Text = "Mario Kart Wii  0123456789  ÄÖÜ äöü ß",
            MaxLength = 80
        };
        readonly Button import, save, fillButton, outlineButton, symbolsButton, chooseFonts;
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
        public FontChangerForm() : base("RR-MKWii Font Changer Tool", L.T("1. Archive laden  •  2. Schriftgruppen und TTF wählen  •  3. Vorschau und Kopien speichern", "1. Load archives  •  2. Choose font groups and TTF  •  3. Preview and save copies"), "Font.szs · MenuSingle.szs / MenuMulti.szs / Globe.szs · Race.szs + Race_E/U/J.szs · RaceAssets.szs / ReplacedAssets.szs")
        {
            var addArchive = Action(L.T("Archiv hinzufügen…", "Add archive…"), L.T("Font.szs zusammen mit Menü-/HUD-Archiven wählen. Weitere Dateien werden zur Auswahl hinzugefügt.", "Select Font.szs together with menu/HUD archives. Later selections append to the loaded files."), AddArchives);
            addArchive.Name = "PackSourceAction";
            StudioActions.Icon(Action(L.T("Auswahl leeren", "Clear selection"), L.T("Geladene Dateien entfernen; vor dem Verwerfen ungespeicherter Änderungen nachfragen.", "Clear loaded files; asks before discarding unsaved changes."), ClearSelection), StudioIcon.Remove);
            chooseFonts = Action(L.T("Schriften wählen…", "Choose fonts…"), L.T("Hauptschrift wählen; Japanisch und weitere Alphabete sind optional.", "Choose your main font; Japanese and more alphabets are optional."), delegate
            {
                var dialog = new FontScriptForm(ScriptSources);
                StudioEditor.Open(this, dialog, delegate(DialogResult result) {
                    if (result == DialogResult.OK)
                    {
                        ttf = dialog.Selection.Latin;
                        japaneseTtf = dialog.Selection.Japanese;
                        otherTtf = dialog.Selection.Other;
                        mainScript = dialog.Selection.MainScript;
                        SettingsChanged();
                        if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                    }
                });
            });
            chooseFonts.Name = "FontStyleAction";
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
            Actions.Controls.Add(new Label { Name = "FontStyleField", Text = L.T("Schrift im Archiv", "Font in archive"), AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            fonts.Name = "FontStyleField";
            Actions.Controls.Add(fonts);
            import = Action(L.T("Änderungen ansehen", "Preview changes"), L.T("Aktuelle Schrift und Einstellungen in der Vorschau ansehen. Speichern kann sie auch direkt anwenden.", "Preview the current TTF and settings. Save can also render them directly."), GenerateReplacement);
            StudioActions.Icon(import, StudioIcon.Refresh);
            var resetChanges = Action(L.T("Änderungen zurücksetzen", "Reset changes"), L.T("Alle noch nicht gespeicherten Schrift-, HUD- und Symboländerungen verwerfen.", "Discard all pending font, HUD and symbol changes."), delegate
            {
                changes.Clear();
                symbolChanges.Clear();
                menuCopies.Clear();
                pending = null;
                needsRender = false;
                dirty = false;
                Update();
            });
            StudioActions.Icon(resetChanges, StudioIcon.Undo);
            fillButton = Action(L.T("Füllung: #000000…", "Fill: #000000…"), L.T("Füllfarbe der Zeichen wählen. IA4-/IA8-Schriften speichern Graustufen. Speichern übernimmt die aktuellen Einstellungen auch ohne Vorschau.", "Choose the letter's interior colour. IA4/IA8 fonts store colours as grayscale. Preview is optional; Save applies the current settings."), delegate
            {
                PickColour(true);
            });
            fillButton.Name = "FontStyleAction";
            outlineButton = Action(L.T("Kontur: #FFFFFF…", "Outline: #FFFFFF…"), L.T("Konturfarbe wählen. IA4-/IA8-Schriften speichern Graustufen. Speichern übernimmt die aktuellen Einstellungen auch ohne Vorschau.", "Choose the outline colour. IA4/IA8 fonts store colours as grayscale. Preview is optional; Save applies the current settings."), delegate
            {
                PickColour(false);
            });
            outlineButton.Name = "FontStyleAction";
            Actions.Controls.Add(new Label { Name = "FontStyleField", Text = L.T("Konturbreite (px)", "Outline width (px)"), AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            outlineSize.Name = "FontStyleField";
            Actions.Controls.Add(outlineSize);
            hinting.Items.AddRange(new object[] { L.T("Keine", "None"), L.T("Geglättet", "Hinted / smooth"), L.T("Scharf", "Hinted / sharp") });
            hinting.SelectedIndex = 0;
            Actions.Controls.Add(new Label { Name = "FontStyleField", Text = "Hinting", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            hinting.Name = "FontStyleField";
            Actions.Controls.Add(hinting);
            StudioUx.SetHelp(hinting, L.T("Keine erhält glatte Konturen. Geglättet passt die Schrift an das Pixelraster an, Scharf rastert ohne Graustufen. Speichern übernimmt die Einstellungen auch ohne Vorschau. Dies sind Windows-Verfahren.", "None preserves smooth outlines. Hinted modes rasterize TTF strokes against the pixel grid. Sharp uses monochrome rasterization. Preview is optional; Save applies the current settings. These are Windows modes, not FreeType's slight/medium/full levels."));
            StudioUx.SetHelp(outlineSize, L.T("Konturbreite in Pixeln der Schrifttextur. Null entfernt die Kontur. Speichern übernimmt die Einstellungen auch ohne Vorschau.", "Outline width in font-atlas pixels. Zero disables the outline. Save applies the current settings to the selected font; Preview is optional."));
            save = ExportAction(L.T("Schriftkopie speichern", "Save font copy"), L.T("Aktuelle Einstellungen anwenden und eine separate Kopie in MUR_EDITED mit dem ursprünglichen Dateinamen speichern.", "Render current settings if needed and save a separate copy in MUR_EDITED, keeping the original filename."), Save);
            characterCheck = ExportAction(L.T("Zeichen prüfen…", "Character check…"), L.T("Ersetzte, fehlende und geschützte Zeichen der ausgewählten Schriftgruppen prüfen.", "Review replaced, missing and protected characters for selected font groups."), delegate {
                if (needsRender || characterReports.Count == 0) GenerateReplacement();
                StudioEditor.Open(this, new FontCharacterReviewForm(characterReports), delegate { });
            }, false);
            symbolsButton = ExportAction(L.T("Symbole…", "Symbols…"), L.T("Spielsymbole ansehen und einzeln ersetzen. Die Schriftumwandlung erhält diese Symbole.", "Browse and individually replace game symbols. TTF conversion preserves them."), EditSymbols, false);
            var scopes = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            scopes.Controls.AddRange(new Control[] { includeMenu, includeTimers, includeRaceMessages, includeHud });
            scopes.SetFlowBreak(includeHud, true);
            scopes.Controls.Add(archiveInfo);
            var archiveButton = new Button { Text = L.T("Archivübersicht…", "Archive overview…"), AutoSize = true };
            archiveButton.Click += delegate { StudioEditor.Open(this, new FontArchiveInfoForm(archiveDetails), delegate { }); };
            scopes.Controls.Add(archiveButton);
            var layoutPreview = new Button { Text = L.T("Textfelder prüfen…", "Check text fields…"), AutoSize = true };
            layoutPreview.Click += delegate { Guard(delegate {
                if (archive == null) throw new InvalidOperationException(L.T("Font.szs und Menü-/HUD-Archive laden.", "Load Font.szs and menu/HUD archives."));
                if (needsRender) GenerateReplacement();
                var originals = archive.Files.Where(e => e.Key.EndsWith(".brfnt", StringComparison.OrdinalIgnoreCase)).ToDictionary(e => e.Key, e => e.Value.Data);
                StudioEditor.Open(this, new FontLayoutPreviewForm(additionalArchives, originals, changes), delegate { });
            }); };
            scopes.Controls.Add(layoutPreview);
            scopes.Controls.Add(symbolsButton);
            scopes.Controls.Add(characterCheck);
            scopes.SizeChanged += delegate { archiveInfo.MaximumSize = new Size(Math.Max(120, Math.Min(228, scopes.ClientSize.Width - 28)), 0); };
            Body.Controls.Add(scopes);
            scopes.BringToFront();
            foreach (var choice in new[] { includeMenu, includeTimers, includeRaceMessages, includeHud })
                choice.CheckedChanged += delegate { SettingsChanged(); };
            StudioUx.SetHelp(includeMenu, L.T("Unterstützte Buchstaben und Zahlen in Font.szs ändern. Symbole bleiben erhalten.", "Change supported letters and number fonts in Font.szs. Symbols are preserved."));
            StudioUx.SetHelp(includeTimers, L.T("Bilder der Zeitanzeigen in den zugehörigen Menüarchiven ändern.", "Change timer pictures in adjacent menu archives."));
            StudioUx.SetHelp(includeRaceMessages, L.T("Gemeinsame Schrift von Countdown, GO und Ziel ändern; andere Ansichten mit dieser Schrift ändern sich ebenfalls.", "Change the shared font used by countdown, GO and Finish; other screens using that font also change."));
            StudioUx.SetHelp(includeHud, L.T("Rennzahlen, km/h und Trennzeichen einschließlich RR-Anpassungen ändern. Positionsnummern bleiben unverändert.", "Change race digits, km/h and separators, including RR overrides. Position numbers remain unchanged."));
            var atlasTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(4)
            };
            atlasTools.Controls.Add(new Label { Text = L.T("Atlasseite", "Atlas page"), AutoSize = true, Margin = new Padding(4, 6, 8, 0) });
            atlasTools.Controls.Add(previousSheet);
            atlasTools.Controls.Add(sheetLabel);
            atlasTools.Controls.Add(nextSheet);
            previousSheet.Text = L.T("Vorherige Atlasseite", "Previous atlas page");
            nextSheet.Text = L.T("Nächste Atlasseite", "Next atlas page");
            StudioActions.Icon(previousSheet, StudioIcon.Previous);
            StudioActions.Icon(nextSheet, StudioIcon.Next);
            previousSheet.Click += delegate { if (sheet.Value > sheet.Minimum) sheet.Value--; };
            nextSheet.Click += delegate { if (sheet.Value < sheet.Maximum) sheet.Value++; };
            Actions.SetFlowBreak(fonts, false);
            sheet.ValueChanged += delegate
            {
                RefreshSheetNavigation();
                Guard(Render);
            };
            var tabs = new DarkTabControl
            {
                Dock = DockStyle.Fill
            };
            var textTab = new TabPage(L.T("Beispieltext", "Sample text"));
            var atlasTab = new TabPage(L.T("Schriftatlas", "Font atlas"));
            textTab.Controls.Add(sample);
            textTab.Controls.Add(sampleText);
            atlasTab.Controls.Add(preview);
            atlasTab.Controls.Add(atlasTools);
            tabs.TabPages.Add(atlasTab);
            tabs.TabPages.Add(textTab);
            tabs.SelectedTab = textTab;
            Body.Controls.Add(tabs);
            scopes.SendToBack();
            DarkTheme.StyleTabs(tabs);
            sampleText.TextChanged += delegate
            {
                Guard(Render);
            };
            StudioUx.SetHelp(sampleText, L.T("Bis zu 80 Zeichen mit den tatsächlichen BRFNT-Glyphen und Abständen anzeigen. Fehlende Zeichen lassen eine Lücke; Spielfarben und Ersatzschriften werden nicht simuliert.", "Preview up to 80 characters using the actual BRFNT glyphs and spacing. Characters absent from this font leave a gap; game colours and fallback fonts are not simulated."));
            StudioUx.SetHelp(fonts, L.T("Eine Schrift für die Vorschau wählen. Die Kontrollkästchen bestimmen die exportierten Schriftgruppen. Positionsnummern und Spielsymbole bleiben beim TTF-Import erhalten.", "Select a font for preview. The checkboxes choose which font groups are exported. Position numbers and game symbols are preserved by TTF import."));
            StudioUx.SetHelp(sheet, L.T("Die tatsächlich kodierten Schriftatlasseiten einschließlich erhaltener Spielsymbole ansehen.", "Browse the actual encoded font atlas pages, including preserved game symbols."));
            Finish();
            RefreshColourButtons();
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
                if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ohne Speichern der Schriftänderungen schliessen?", "Close without saving the font changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                    e.Cancel = true;
            };
            InitializeHistory();
        }

        void InitializeHistory()
        {
            Actions.Visible = true;
            editHistory = new SpecialEditorHistory(this, Actions,
                delegate { return new object[] { ttf, japaneseTtf, otherTtf, mainScript, fillColor, outlineColor,
                    outlineSize.Value, hinting.SelectedIndex, includeMenu.Checked, includeTimers.Checked,
                    includeRaceMessages.Checked, includeHud.Checked, needsRender,
                    SpecialEditorHistory.Copy(changes), SpecialEditorHistory.Copy(symbolChanges), SpecialEditorHistory.Copy(menuCopies),
                    characterReports.ToDictionary(p => p.Key, p => SpecialEditorHistory.Copy(p.Value)) }; },
                delegate(object[] state) {
                    ttf = (string)state[0]; japaneseTtf = (string)state[1]; otherTtf = (string)state[2]; mainScript = (int)state[3];
                    fillColor = (Color)state[4]; outlineColor = (Color)state[5]; outlineSize.Value = (decimal)state[6];
                    hinting.SelectedIndex = (int)state[7]; includeMenu.Checked = (bool)state[8]; includeTimers.Checked = (bool)state[9];
                    includeRaceMessages.Checked = (bool)state[10]; includeHud.Checked = (bool)state[11]; needsRender = (bool)state[12];
                    SpecialEditorHistory.Replace(changes, (Dictionary<string, byte[]>)state[13]);
                    SpecialEditorHistory.Replace(symbolChanges, (Dictionary<string, byte[]>)state[14]);
                    SpecialEditorHistory.Replace(menuCopies, (Dictionary<string, StudioArchiveCopy>)state[15]);
                    characterReports.Clear();
                    foreach (var pair in (Dictionary<string, Dictionary<int, string>>)state[16]) characterReports.Add(pair.Key, SpecialEditorHistory.Copy(pair.Value));
                    pending = null; if (selectedFont != null) changes.TryGetValue(selectedFont, out pending);
                    dirty = changes.Count > 0 || menuCopies.Count > 0 || needsRender;
                    RefreshColourButtons(); Update();
                }, delegate { return sourceGeneration + "|" + (source ?? "") + "|" + String.Join("|", additionalArchives); });
        }

        private void AlignActionRows()
        {
            var controls = Actions.Controls.Cast<Control>().ToArray();
            var settings = new FlowLayoutPanel {
                Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false,
                FlowDirection = FlowDirection.TopDown, Padding = new Padding(4)
            };
            foreach (Control control in controls.Where(c => c.Name == "FontStyleAction" || c.Name == "FontStyleField")) {
                control.Margin = new Padding(3, 4, 3, 4);
                control.Anchor = AnchorStyles.Left;
                if (control is Button) control.MinimumSize = new Size(220, 34);
                control.Width = Math.Min(control.Width, 232);
                settings.Controls.Add(control);
            }
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 276));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var settingsTabs = new DarkTabControl { Dock = DockStyle.Fill };
            var styleTab = new TabPage(L.T("Schrift & Stil", "Font & style"));
            var scopeTab = new TabPage(L.T("Anwenden auf", "Apply to"));
            styleTab.Controls.Add(settings);
            settingsTabs.TabPages.AddRange(new[] { styleTab, scopeTab });
            DarkTheme.StyleTabs(settingsTabs);
            var content = new Panel { Dock = DockStyle.Fill };
            foreach (Control control in Body.Controls.Cast<Control>().ToArray())
            {
                var scopes = control as FlowLayoutPanel;
                if (scopes == null) { content.Controls.Add(control); continue; }
                scopes.AutoSize = false;
                scopes.Dock = DockStyle.Fill;
                scopes.AutoScroll = true;
                scopes.WrapContents = false;
                scopes.FlowDirection = FlowDirection.TopDown;
                foreach (Control item in scopes.Controls)
                {
                    item.MaximumSize = new Size(228, 0);
                    item.MinimumSize = new Size(Math.Min(item.MinimumSize.Width, 228), item.MinimumSize.Height);
                    item.Margin = new Padding(5, 6, 5, 6);
                }
                scopeTab.Controls.Add(scopes);
            }
            Body.Controls.Add(workspace);
            workspace.Controls.Add(content, 0, 0);
            workspace.Controls.Add(settingsTabs, 1, 0);
            DarkTheme.Apply(workspace);
            settingsTabs.ItemSize = new Size(124, 32);
            Actions.Visible = true;
        }

        void RefreshColourButtons()
        {
            fillButton.Text = L.T("Füllung: #", "Fill: #") + (fillColor.ToArgb() & 0xFFFFFF).ToString("X6") + "…";
            outlineButton.Text = L.T("Kontur: #", "Outline: #") + (outlineColor.ToArgb() & 0xFFFFFF).ToString("X6") + "…";
            ColourButton.SetColor(fillButton, fillColor);
            ColourButton.SetColor(outlineButton, outlineColor);
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
                    RefreshColourButtons();
                    SettingsChanged();
                }
        }

        static bool IsCompanion(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path).Split('_')[0];
            return new[] { "Title", "MenuSingle", "MenuMulti", "MenuOther", "Globe", "Channel", "Award", "Race", "RaceAssets", "ReplacedAssets" }
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
            if (archive == null) throw new InvalidOperationException(L.T("Zuerst Font.szs hinzufügen.", "Add Font.szs first."));
            var next = new List<string>(additionalArchives);
            foreach (string item in paths)
            {
                string path = Path.GetFullPath(item);
                if (!IsCompanion(path)) throw new InvalidDataException(L.T("Ein Menü-/HUD-Archiv hinzufügen: MenuSingle, MenuMulti, Globe, Race, RaceAssets oder ReplacedAssets. Zuerst Font.szs hinzufügen.", "Add a menu/HUD archive: MenuSingle, MenuMulti, Globe, Race, RaceAssets or ReplacedAssets. Add Font.szs first."));
                new StudioArchiveCopy(path);
                string existing = next.FirstOrDefault(p => Path.GetFileName(p).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));
                if (existing != null && !existing.Equals(path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.F("Ein Archiv namens {0} ist bereits aus einem anderen Ordner geladen. Zuerst die Font.szs des gewünschten Packs öffnen.", "An archive named {0} is already loaded from another folder. Open the intended pack's Font.szs first.", Path.GetFileName(path)));
                if (existing == null) next.Add(path);
            }
            additionalArchives.Clear();
            additionalArchives.AddRange(next);
            SettingsChanged();
        }

        void ClearSelection()
        {
            if (dirty && StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen verwerfen und alle Dateien schliessen?", "Discard unsaved changes and clear all loaded files?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            sourceGeneration++;
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
            string filter = L.T("Schrift-/Menü-/HUD-Archive|Font.szs;Font_*.szs;homeBtn*.szs;*.brfnt;", "Font / menu / HUD archives|Font.szs;Font_*.szs;homeBtn*.szs;*.brfnt;") + ToolArchiveFilters.FontExtras.Split('|')[1];
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
                    else if (!IsCompanion(path)) throw new InvalidDataException(L.T("Dieses Archiv enthält keine unterstützten Schriften oder HUD-/Menütexturen.", "This archive has no supported fonts or HUD/menu textures."));
                }
            }
            if (fontPaths.Count > 1) throw new InvalidDataException(L.T("Eine Font.szs und die gewünschten Menü-/HUD-Archive wählen. Zum Wechseln des Schriftarchivs die Auswahl leeren.", "Choose one Font.szs plus any menu/HUD archives. Clear selection to switch to another font archive."));
            if (source == null)
            {
                if (fontPaths.Count != 1) throw new InvalidDataException(L.T("Bei der ersten Auswahl Font.szs zusammen mit den gewünschten Menü-/HUD-Archiven hinzufügen.", "Include Font.szs in the first selection, together with the menu/HUD archives you want."));
            }
            else if (fontPaths.Any(p => !Path.GetFullPath(p).Equals(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(L.T("Eine Schriftquelle ist bereits geladen. Vor dem Wechseln die Auswahl leeren.", "A font source is already loaded. Use Clear selection before switching fonts."));
            string fontSource = source ?? fontPaths[0];
            var extras = paths.Where(p => !fontPaths.Contains(p)).ToArray();
            if (Path.GetExtension(fontSource).Equals(".brfnt", StringComparison.OrdinalIgnoreCase) && extras.Length > 0)
                throw new InvalidDataException(L.T("Für zusätzliche Archive Font.szs verwenden; eine einzelne BRFNT wird separat bearbeitet.", "Use Font.szs with additional archives; an individual BRFNT is edited separately."));
            var known = source == null ? FindCompanions(fontSource) : new List<string>(additionalArchives);
            foreach (string extra in extras)
                if (known.Any(p => Path.GetFileName(p).Equals(Path.GetFileName(extra), StringComparison.OrdinalIgnoreCase)
                    && !Path.GetFullPath(p).Equals(Path.GetFullPath(extra), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException(L.T("Ein Archiv mit diesem Namen ist bereits aus einem anderen Ordner geladen: ", "An archive with this name is already loaded from another folder: ") + Path.GetFileName(extra));
            if (source == null) LoadSource(fontSource);
            if (extras.Length > 0) AddArchivePaths(extras);
        }
        void RefreshArchiveInfo()
        {
            string needed = L.T("Menütext / GO / Ziel: Font.szs. Zeitanzeigen: MenuSingle.szs, MenuMulti.szs, Globe.szs.", "Menu text / GO / Finish: Font.szs. Timers: MenuSingle.szs, MenuMulti.szs, Globe.szs.");
            needed += L.T("\nHUD: Race.szs + Race_E.szs / Race_U.szs / Race_J.szs (deine Region); RR: RaceAssets.szs + ReplacedAssets.szs.", "\nHUD: Race.szs + Race_E.szs / Race_U.szs / Race_J.szs (your region); RR: RaceAssets.szs + ReplacedAssets.szs.");
            if (source == null) { archiveDetails = needed; archiveInfo.Text = L.T("1. Font.szs hinzufügen; passende Menü-/HUD-Quellen werden automatisch geladen.", "1. Add Font.szs; matching menu / HUD sources load automatically."); return; }
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
                    missing.Add(L.T("Race_E.szs / Race_U.szs / Race_J.szs (eine Region)", "Race_E.szs / Race_U.szs / Race_J.szs (one region)"));
            }
            archiveDetails = needed + L.T("\nGeladen: ", "\nLoaded: ") + loaded
                + (missing.Count == 0 ? "" : L.T("\nFür gewählte Gruppen nicht geladen: ", "\nNot loaded for selected groups: ") + String.Join(", ", missing) + L.T(". Mit Archiv hinzufügen ergänzen; exportiert werden nur geladene Archive.", ". Use Add archive; export covers loaded archives only."));
            archiveDetails += L.T("\n\nQuellpfade:\n", "\n\nSource paths:\n") + String.Join("\n", new[] { source }.Concat(additionalArchives));
            archiveInfo.Text = L.F("{0} Dateien geladen", "{0} files loaded", additionalArchives.Count + 1)
                + (missing.Count == 0 ? L.T(" • Quellen für die gewählten Gruppen bereit.", " • Sources ready for selected groups.") : L.F(" • {0} benötigte Quellen fehlen – siehe Archivübersicht.", " • {0} source requirements missing — see Archive overview.", missing.Count));
            StudioUx.SetHelp(archiveInfo, archiveDetails);
        }
        void Open()
        {
            if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ungespeicherte Schriftänderungen verwerfen und eine andere Quelle öffnen?", "Discard the pending fonts and open another source?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
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
                p = ToolArchiveFilters.Show(picker, this) == DialogResult.OK ? ToolArchiveFilters.SelectedFile(picker) : null;
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
                throw new InvalidDataException(L.T("Dieses Archiv enthält keine BRFNT-Schriften.", "This archive contains no BRFNT fonts."));
            // Validate before replacing the current document.
            new BrfntFont(next == null ? File.ReadAllBytes(p) : next.Files[names[0]].Data);
            var companions = FindCompanions(p);
            characterReports.Clear();
            source = p;
            sourceGeneration++;
            symbolChanges.Clear();
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
            characterCheck.Enabled = original != null && MainTtf != null;
            symbolsButton.Enabled = original != null;
            includeMenu.Enabled = true; includeTimers.Enabled = includeRaceMessages.Enabled = includeHud.Enabled = archive != null;
            save.Text = (includeRaceMessages.Checked || includeHud.Checked) && archive != null ? L.T("Schrift-/HUD-Kopien speichern", "Save font / HUD copies") : L.T("Schriftkopie speichern", "Save font copy");
            import.Enabled = original != null && MainTtf != null;
            save.Enabled = changes.Count > 0 || menuCopies.Count > 0 || (needsRender && original != null && MainTtf != null);
            if (MainTtf == null) DarkTheme.StylePrimary(chooseFonts);
            else DarkTheme.StyleNeutral(chooseFonts);
            if (original != null)
            {
                var f = new BrfntFont(pending ?? original);
                fillButton.Enabled = outlineButton.Enabled = f.Format != 0 && f.Format != 1;
                sheet.Value = 1;
                sheet.Maximum = f.Sheets;
                RefreshSheetNavigation();
                Render();
            }

            Status.Text = original == null ? L.T("Öffne zum Start die Font.szs deines Packs.", "Choose your pack's Font.szs to begin.")
                : Path.GetFileName(source) + L.F(" • {0} Schriften • ", " • {0} fonts • ", fonts.Items.Count)
                    + (MainTtf == null ? L.T("Nächster Schritt: eigene Schrift wählen.", "Next: choose your replacement font.")
                        : L.T("Hauptschrift: ", "Main font: ") + Path.GetFileName(MainTtf));
            StudioUx.SetHelp(Status, L.T("Unterstützte Zeichen werden ersetzt; fehlende Zeichen und Spielsymbole bleiben original. Abstände bleiben erhalten, breite Schriften können komprimiert werden. I4/I8: Farbmaske des Spiels. IA4/IA8: Graustufen.",
                "Supported letters are replaced; missing characters and game symbols stay original. Existing spacing is retained; wide fonts may be compressed. I4/I8: game-defined colour mask. IA4/IA8: grayscale colours."));
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
            var dialog = new FontSymbolsForm(inputs);
            StudioEditor.Open(this, dialog, delegate(DialogResult result) {
                if (result == DialogResult.OK)
                {
                    foreach (var item in dialog.Changes) { changes[item.Key] = item.Value; symbolChanges[item.Key] = item.Value; }
                    changes.TryGetValue(selectedFont, out pending);
                    dirty = changes.Count > 0;
                    Update();
                    if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                }
            });
        }
        void SettingsChanged()
        {
            if (editHistory != null && editHistory.Restoring) return;
            needsRender = original != null && MainTtf != null;
            if (needsRender) dirty = true;
            Update();
            if (needsRender)
                Status.Text = L.T("Einstellungen geändert → Vorschau aktualisieren oder Schriftkopien speichern.", "Settings changed → refresh the preview or save font copies.");
        }

        void GenerateReplacement()
        {
            if (original == null || MainTtf == null || fonts.SelectedItem == null)
                throw new InvalidOperationException(L.T("Zuerst eine Wii-Schrift öffnen und eine TTF wählen.", "Open a Wii font and choose a TTF first."));
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
                byte[] converted = font.ImportFonts(ScriptSources, fillColor, outlineColor, (float)outlineSize.Value,
                    (GlyphHinting)hinting.SelectedIndex, out count, BrfntFont.HasGameNumbers(key), report);
                reports.Add(key, report);
                if (count > 0) generated.Add(key, converted);
            }

            var menus = includeTimers.Checked && archive != null ? MenuTimerFonts.Generate(additionalArchives, ScriptSources.ForCharacter('0'), fillColor, outlineColor,
                (float)outlineSize.Value, (GlyphHinting)hinting.SelectedIndex) : new Dictionary<string, StudioArchiveCopy>();
            if (includeHud.Checked && archive != null)
                foreach (var entry in HudFontTextures.Generate(additionalArchives, ScriptSources.ForCharacter('0'), fillColor, outlineColor,
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
            Status.Text = L.F("{0} Textschriften + {1} Menü-/HUD-Archive bereit. ALLE exportierten Dateien in dein Pack kopieren, einschließlich RaceAssets und ReplacedAssets, sofern exportiert.",
                "{0} text fonts + {1} menu / HUD archives ready. Copy ALL exported files into your pack, including RaceAssets and ReplacedAssets when exported.", generated.Count, menuCopies.Count);
        }

        void Save()
        {
            if (String.IsNullOrEmpty(source)) throw new InvalidOperationException(L.T("Zuerst eine Wii-Schrift öffnen.", "Open a Wii font first."));
            string folder = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(source), "MUR_EDITED"));
            string dest = SaveCopy(folder);
            Status.Text = L.F("Gespeichert: {0}\nAlle exportierten Dateien in dein Pack kopieren. Zeitanzeigen, HUD-Zahlen, km/h und Schrägstrich benötigen auch die exportierten Menü-/HUD-Archive.",
                "Saved: {0}\nCopy all exported files into your pack. Timers, HUD numbers, km/h and slash need the exported menu/HUD archives too.", dest);
            ExportHelp.Show(this, folder);
        }

        string SaveCopy(string folder)
        {
            if (needsRender) GenerateReplacement();
            if (changes.Count == 0 && menuCopies.Count == 0) throw new InvalidOperationException(L.T("Keine Ersetzungen bereit. Eine TTF und Schriftgruppen wählen; die Zeichenprüfung erklärt erhaltene oder fehlende Zeichen.", "No replacements are ready. Choose a TTF and font groups; Character check explains preserved or missing characters."));
            string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(source)));
            if (string.Equals(dest, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Einen separaten Ausgabeordner wählen.", "Choose a separate output folder."));
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
                var copies = new List<StudioArchiveCopy> { fresh };
                copies.AddRange(menuCopies.Values);
                ArchiveCopyExport.Save(copies, folder);
            }
            dirty = false;
            return archive == null ? dest : folder;
        }
    }
}

