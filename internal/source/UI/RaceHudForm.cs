using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class RaceHudForm : Form
    {
        SpecialEditorHistory editHistory;
        TableLayoutPanel workspaceLayout;
        readonly Timer searchDelay = new Timer { Interval = 250 };
        bool refreshingTextures;
        readonly TextBox search = new TextBox { Width = 220 };
        readonly HudFontSettings numberSettings = new HudFontSettings();
        readonly RaceHudSession session = new RaceHudSession();
        readonly ListBox textures = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true
        };
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(55, 55, 62)
        };
        readonly ComboBox category = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 205
        };
        readonly CheckBox shadows = new CheckBox
        {
            Text = L.T("Positionszahl-Schatten ausblenden", "Hide placement-number shadows"),
            AutoSize = true
        };
        readonly TextBox output = new TextBox
        {
            Dock = DockStyle.Fill
        };
        readonly Label status = Label(), detail = Label(), sources = Label();
        readonly Button clear = new Button { Text = L.T("Zurücksetzen", "Reset"), AutoSize = true };
        readonly ToolTip tips = new ToolTip
        {
            AutoPopDelay = 15000
        };
        Button choose, import, save, numberFont, colours, mapColours, moveHud;
        bool dirty;
        static Label Label()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                UseMnemonic = false,
                AutoEllipsis = true
            };
        }

        HudTexture Selected
        {
            get
            {
                return textures.SelectedItem as HudTexture;
            }
        }

        internal void PrepareWorkspace()
        {
            var header = workspaceLayout.GetControlFromPosition(0, 0);
            if (header != null) header.Visible = false;
            workspaceLayout.RowStyles[0].Height = 0;
            workspaceLayout.Padding = new Padding(8, 6, 8, 4);
        }

        public RaceHudForm()
        {
            Text = "RR-MKWii Race HUD Tool — murums Wii Mod Studio";
            Size = new Size(1180, 910);
            MinimumSize = new Size(950, 700);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 8
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (float height in new[]
            {
                StudioChrome.HeaderHeight + 26f,
                82f,
                34f,
                -1f,
                42f,
                38f,
                46f,
                30f
            }

            )
                grid.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent : SizeType.Absolute, height < 0 ? 100 : height));
            Controls.Add(grid);
            workspaceLayout = grid;
            grid.Controls.Add(ToolFileHint.Wrap(StudioChrome.Header("RR-MKWii Race HUD Tool", L.T("Archive laden • Anzeigen bearbeiten • Kopien speichern", "Load archives • Edit HUD elements • Save copies")), "Race.szs + Race_E.szs / Race_U.szs / Race_J.szs · PNG / JPG"), 0, 0);
            grid.RowStyles[1].SizeType = SizeType.AutoSize;
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            var addArchives = Button(L.T("Archiv hinzufügen…", "Add archive…"), Add, L.T("Mehrere Race-/RR-HUD-Archive laden; bestehende Änderungen bleiben erhalten.", "Select several Race / RR HUD archives; existing edits are kept."));
            addArchives.Name = "PackSourceAction";
            StudioActions.Icon(addArchives, StudioIcon.Add);
            bar.Controls.Add(addArchives);
            var clearSelection = Button(L.T("Auswahl leeren", "Clear selection"), delegate {
                if (dirty && StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen verwerfen und geladene Archive entfernen?", "Discard unsaved changes and clear loaded archives?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                session.Archives.Clear(); dirty = false; shadows.Checked = false;
                RefreshList(); UpdateState(); PackSelection.SourceCleared(this);
            }, L.T("Alle geladenen Archive entfernen.", "Clear all loaded archives."));
            StudioActions.Icon(clearSelection, StudioIcon.Remove);
            bar.Controls.Add(clearSelection);
            import = Button(L.T("Passende Bilder importieren…", "Import matching pictures…"), Match, L.T("Bilder und Unterordner nach genauen Dateinamen durchsuchen. Treffer werden vorgemerkt; noch wird kein Archiv gespeichert.", "Scan a picture folder and its subfolders. Exact filename matches become pending replacements; no archive is saved yet."));
            StudioActions.Icon(import, StudioIcon.Import);
            bar.Controls.Add(import);
            category.Items.AddRange(new object[] { L.T("Positionszahlen", "Placement numbers"), L.T("Zeit / Runden / Punkte", "Timer / laps / score"), L.T("Items / Minimap", "Items / minimap"), L.T("Vorgemerkte Änderungen", "Pending replacements"), L.T("Alle Texturen", "All textures"), L.T("Countdown / Start / Ziel", "Countdown / start / finish"), L.T("Spielernamen / Warnungen", "Player names / warnings"), L.T("Ergebnisse", "Results"), L.T("Eingabeanzeige", "Input viewer"), L.T("Minimap / Symbole", "Minimap / icons"), L.T("Tacho", "Speedometer"), L.T("Itembox / Glas", "Item box / glass") });
            category.SelectedIndex = 0;
            category.SelectedIndexChanged += delegate
            {
                RefreshList();
            };
            StudioUx.DisableHover(search);
            search.TextChanged += delegate
            {
                searchDelay.Stop();
                searchDelay.Start();
            };
            searchDelay.Tick += delegate
            {
                searchDelay.Stop();
                RefreshList();
            };
            Disposed += delegate { searchDelay.Dispose(); };
            StudioUx.SetHelp(search, L.T("Sucht einen Namensteil in allen geladenen Texturen, unabhängig von der Kategorie.", "Find part of a name in all loaded textures, regardless of category."));
            mapColours = Button(L.T("Kartenfarben…", "Map colours…"), EditMapColours, L.T("Minimap-Materialfarbe, Deckkraft und Eckverlauf anhand einer Vorschau bearbeiten.", "Edit the minimap material colour, opacity and four-corner gradient with a sample preview."));
            var layoutActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            layoutActions.Controls.Add(mapColours);
            moveHud = Button(L.T("HUD visuell verschieben…", "Move HUD visually…"), EditLayouts, "Drag the whole selected HUD layout or individual elements. Apply here, then save edited archives below.");
            layoutActions.Controls.Add(moveHud);
            layoutActions.Controls.Add(Button(L.T("Kategorie ansehen…", "Preview category…"), delegate
            {
                StudioEditor.Open(this, new HudOverviewForm(FilteredTextures()), delegate { });
            }, L.T("Aktuelle Bilder gemeinsam prüfen, auch vorgemerkte Änderungen und Eingabezustände. Keine Simulation des Spiel-Layouts.", "Review current images together, including pending replacements and separate input states. Not a game layout simulation.")));
            grid.Controls.Add(bar, 0, 1);
            grid.Controls.Add(sources, 0, 2);
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Width = 1100,
                SplitterDistance = 278,
                Panel1MinSize = 200,
                Panel2MinSize = 320
            };
            var browser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            browser.Controls.Add(new Label { Text = L.T("Kategorie", "Category"), AutoSize = true }, 0, 0);
            category.Dock = DockStyle.Fill;
            browser.Controls.Add(category, 0, 1);
            browser.Controls.Add(new Label { Text = L.T("Textur suchen (alle Kategorien)", "Find texture (all categories)"), AutoSize = true }, 0, 2);
            search.Dock = DockStyle.Fill;
            browser.Controls.Add(search, 0, 3);
            browser.Controls.Add(textures, 0, 4);
            browser.Controls.Clear();
            browser.RowCount = 2;
            browser.RowStyles.Clear();
            browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            browser.Controls.Add(new Label { Text = L.T("Texturen", "Textures"), AutoSize = true }, 0, 0);
            browser.Controls.Add(textures, 0, 1);
            split.Panel1.Controls.Add(browser);
            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.Controls.Add(new Label { Text = L.T("Texturvorschau", "Texture preview"), AutoSize = true }, 0, 0);
            layoutActions.Padding = new Padding(0, 2, 0, 2);
            grid.Controls.Add(layoutActions, 0, 4);
            right.Controls.Add(preview, 0, 1);
            right.Controls.Add(detail, 0, 2);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };
            choose = Button(L.T("Ausgewähltes Bild ersetzen…", "Replace selected picture…"), Choose, L.T("Ein PNG/JPG/BMP wählen. Die Vorschau zeigt das konvertierte Ergebnis in der ursprünglichen Texturgrösse.", "Choose one PNG/JPG/BMP. The preview shows the converted result at the original texture dimensions."));
            StudioActions.Icon(choose, StudioIcon.Image);
            actions.Controls.Add(choose);
            clear.Click += delegate
            {
                if (Selected != null)
                {
                    Selected.Archive.Pictures.Remove(Selected.Key);
                    Selected.Archive.Generated.Remove(Selected.Key);
                    dirty = true;
                    RefreshList();
                }
            };
            StudioUx.SetHelp(clear, "Discard this pending replacement. The preview returns to the opened archive's texture; files already exported are not changed.");
            actions.Controls.Add(clear);
            StudioActions.Icon(clear, StudioIcon.Refresh);
            numberFont = Button(L.T("Zahlenschrift…", "Number font…"), GenerateNumber, L.T("Ziffer oder Trennzeichen aus einer TTF erstellen. Einzelne Textur, passende Dateinamen oder gesamten Zahlensatz wählen und prüfen.", "Create a digit or separator from a TTF with fill, outline and hinting. For timer, lap and score textures. Choose one entry, matching filenames or the complete number set, then review."));
            StudioActions.Icon(numberFont, StudioIcon.Font);
            actions.Controls.Add(numberFont);
            colours = Button(L.T("Farben…", "Colours…"), Recolour, L.T("Grund- und Konturfarben ändern. Transparenz, Grösse und Format bleiben erhalten. Gedrückte und freigegebene Zustände sind getrennt bearbeitbar.", "Change dark/base and light/outline colours of this texture. Preview retains alpha, dimensions and format. Pressed and released states can be edited separately."));
            actions.Controls.Add(colours);
            layoutActions.Visible = false;
            var layoutToggle = new CheckBox { Text = L.T("Layout && Übersicht", "Layout && overview"), AutoSize = true };
            layoutToggle.CheckedChanged += delegate { layoutActions.Visible = layoutToggle.Checked; };
            actions.Controls.Add(layoutToggle);
            actions.AutoSize = true;
            right.Controls.Add(actions, 0, 3);

            split.Panel2.Controls.Add(right);
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            filters.Controls.Add(new Label { Text = L.T("Kategorie", "Category"), AutoSize = true }, 0, 0);
            filters.Controls.Add(category, 0, 1);
            filters.Controls.Add(new Label { Text = L.T("Textur suchen (alle Kategorien)", "Find texture (all categories)"), AutoSize = true }, 1, 0);
            filters.Controls.Add(search, 1, 1);
            filters.Controls.Clear();
            filters.ColumnCount = 1; filters.RowCount = 4;
            filters.ColumnStyles.Clear(); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filters.RowStyles.Clear();
            for (int i = 0; i < 4; i++) filters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            filters.AutoSize = true;
            category.Dock = search.Dock = DockStyle.Fill;
            filters.Controls.Add(new Label { Text = L.T("Kategorie", "Category"), AutoSize = true }, 0, 0);
            filters.Controls.Add(category, 0, 1);
            filters.Controls.Add(new Label { Text = L.T("Textur suchen", "Find texture"), AutoSize = true }, 0, 2);
            filters.Controls.Add(search, 0, 3);
            browser.Controls.Remove(browser.GetControlFromPosition(0, 0));
            browser.RowStyles[0].SizeType = SizeType.AutoSize;
            browser.Controls.Add(filters, 0, 0);
            workspace.RowCount = 1; workspace.RowStyles.Clear();
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.Controls.Add(split, 0, 0);
            grid.Controls.Add(workspace, 0, 3);
            textures.SelectedIndexChanged += delegate
            {
                if (!refreshingTextures)
                    ShowPreview();
            };
            var explanation = Label();
            explanation.Text = L.T("Bilder mit passenden Textur-Dateinamen importieren. Unter Vorgemerkte Änderungen prüfen.\nTransparente PNGs für Freisteller verwenden. Die Vorschau zeigt Texturen, keine Spielanimationen.", "Import pictures with matching texture names. Review changes under Pending replacements.\nUse transparent PNGs for cut-outs. Preview shows textures, not game animations.");
            explanation.AutoSize = true;
            explanation.MaximumSize = new Size(320, 0);
            var options = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };
            options.Controls.Add(shadows);
            options.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(300, 0), UseMnemonic = false, Text = L.T("Ohne Haken bleiben die ursprünglichen Schatten erhalten. Ins Bild gemalte Schatten werden nicht entfernt.", "Unchecked = keep source shadows. Does not remove shadows painted into images.") });
            shadows.CheckedChanged += delegate
            {
                dirty = true;
                UpdateState();
            };
            options.AutoSize = true;
            options.FlowDirection = FlowDirection.TopDown;
            options.WrapContents = false;
            options.Controls.Add(explanation);
            options.SizeChanged += delegate {
                int width = Math.Max(120, options.ClientSize.Width - 12);
                foreach (Control child in options.Controls) child.MaximumSize = new Size(width, 0);
            };
            options.Visible = false;
            var more = new CheckBox { Text = L.T("Optionen && Hilfe", "Options && help"), AutoSize = true };
            more.CheckedChanged += delegate { options.Visible = more.Checked; };
            browser.RowCount = 4;
            browser.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            browser.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            browser.Controls.Add(more, 0, 2);
            browser.Controls.Add(options, 0, 3);
            grid.RowStyles[4].SizeType = grid.RowStyles[5].SizeType = SizeType.AutoSize;
            right.RowStyles[2].Height = 42;
            var export = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4
            };
            export.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            export.Controls.Add(new Label { Text = L.T("Ausgabeordner", "Output folder"), AutoSize = true }, 0, 0);
            export.Controls.Add(output, 1, 0);
            var browseOutput = Button(L.T("Ausgabeordner wählen…", "Choose output folder…"), delegate
            {
                using (var d = new FolderPickerDialog
                {
                    Description = L.T("Ordner für bearbeitete Archivkopien wählen", "Choose where edited archive copies will be saved"),
                    SelectedPath = output.Text
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                        output.Text = d.SelectedPath;
            }, "Choose a separate output folder using Explorer navigation.");
            StudioActions.Icon(browseOutput, StudioIcon.Folder);
            export.Controls.Add(browseOutput, 2, 0);
            save = Button(L.T("Archivkopien speichern", "Save archive copies"), Save, L.T("Nur Archive mit gewählten Änderungen unter ursprünglichen Namen speichern. Quellen bleiben unverändert.", "Write only archives with selected changes, using their original filenames. Sources remain unchanged."));
            export.Controls.Add(save, 3, 0);
            grid.Controls.Add(ToolStatus.Wrap(this, status), 0, 7);
            grid.Controls.Add(export, 0, 6);
            foreach (Control control in export.Controls) control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            output.Dock = DockStyle.None;
            PackSelection.Attach(this, delegate(CustomPack pack) { output.Text = Path.Combine(pack.FilesFolder, "MUR_EDITED"); });
            DarkTheme.Apply(this);
            sources.TextChanged += delegate { StudioUx.SetHelp(sources, sources.Text); };
            detail.TextChanged += delegate { StudioUx.SetHelp(detail, detail.Text); };
            StudioUx.Attach(this);
            ToolStatus.Watch(this);
            StudioUx.SetHelp(category, "Filter all loaded archives. Input viewer lists textures referenced by the controller-overlay layouts, including pressed and released states. Pending replacements shows the next export.");
            StudioUx.SetHelp(output, "Edited copies are saved here under their original archive names. The source folder cannot be overwritten.");
            StudioUx.SetHelp(shadows, "Checked: make position_sha transparent in the single and multiplayer layouts. Unchecked: preserve the opened source.");
            save.BackColor = DarkTheme.Accent;
            save.ForeColor = Color.White;
            save.FlatStyle = FlatStyle.Flat;
            save.UseVisualStyleBackColor = false;
            UpdateState();
            RefreshList();
            editHistory = new SpecialEditorHistory(this, bar,
                delegate { return new object[] { session.CaptureEdits(), shadows.Checked, numberSettings.Ttf,
                    numberSettings.Fill, numberSettings.Outline, numberSettings.Stroke, numberSettings.Hint }; },
                delegate(object[] state) {
                    session.RestoreEdits((object[])state[0]); shadows.Checked = (bool)state[1];
                    numberSettings.Ttf = (string)state[2]; numberSettings.Fill = (Color)state[3]; numberSettings.Outline = (Color)state[4];
                    numberSettings.Stroke = (decimal)state[5]; numberSettings.Hint = (int)state[6];
                    dirty = session.SelectedCount > 0 || shadows.Checked; RefreshList(); UpdateState();
                }, delegate { return session.SourceIdentity; });
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Close without saving the current selection changes?", "RR-MKWii Race HUD Tool", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        Button Button(string text, Action action, string help)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(6, 0, 6, 0)
            };
            StudioUx.SetHelp(b, help);
            b.Click += delegate
            {
                ToolStatus.Set(this, false);
                UseWaitCursor = true;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    status.Text = "Action failed; source archives unchanged.";
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, "RR-MKWii Race HUD Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    UseWaitCursor = false;
                }
            };
            return b;
        }

        void Open()
        {
            if (dirty && StudioMessageBox.Show(this, "Discard pending changes and open another pack?", Text,
                MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            string path = GameArchiveImportForm.Select(this, ToolArchiveFilters.Race, "Race.szs");
            if (path == null)
                return;
            session.Open(path);
            PackSelection.SourceLoaded(this);
            output.Text = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(path), "MUR_EDITED"));
            shadows.Checked = false;
            dirty = false;
            RefreshList();
            UpdateState();
        }

        void Add()
        {
            string[] paths = GameArchiveImportForm.SelectMany(this, ToolArchiveFilters.Race);
            if (paths.Length == 0) return;
            var next = new RaceHudSession();
            next.Archives.AddRange(session.Archives);
            foreach (string path in paths) next.Add(path);
            session.Archives.Clear();
            session.Archives.AddRange(next.Archives);
            PackSelection.SourceLoaded(this);
            if (output.Text.Length == 0) output.Text = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(paths[0]), "MUR_EDITED"));
            RefreshList(); UpdateState();
        }
        void Match()
        {
            using (var d = new FolderPickerDialog
            {
                Description = "Choose your HUD picture folder (subfolders are included)"
            }

            )
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    var before = session.Archives.Select(a => new Dictionary<string, string>(a.Pictures)).ToList();
                    var generatedBefore = session.Archives.Select(a => new Dictionary<string, byte[]>(a.Generated)).ToList();
                    int n = 0;
                    try
                    {
                        foreach (var a in session.Archives)
                            n += a.MatchFolder(d.SelectedPath);
                        foreach (var a in session.Archives)
                        {
                            foreach (var p in a.Pictures)
                                using (var b = TplTextureEditor.LoadSourceBitmap(p.Value))
                                    a.Generated[p.Key] = TplTextureEditor.ReplaceFirstImage(a.Files[p.Key].Data, b, true);
                            a.Pictures.Clear();
                        }
                    }
                    catch
                    {
                        for (int i = 0; i < before.Count; i++)
                        {
                            session.Archives[i].Generated.Clear();
                            foreach (var p in generatedBefore[i])
                                session.Archives[i].Generated.Add(p.Key, p.Value);
                            session.Archives[i].Pictures.Clear();
                            foreach (var p in before[i])
                                session.Archives[i].Pictures.Add(p.Key, p.Value);
                        }

                        throw;
                    }

                    dirty |= n > 0;
                    category.SelectedIndex = 3;
                    RefreshList();
                    status.Text = n + " matching pictures selected. Duplicate names are skipped. Nothing saved yet.";
                }
        }

        List<HudTexture> FilteredTextures()
        {
            return session.SearchTextures(category.SelectedIndex, search.Text);
        }

        void RefreshList()
        {
            var selected = Selected;
            refreshingTextures = true;
            textures.BeginUpdate();
            try
            {
                textures.Items.Clear();
                foreach (var t in FilteredTextures())
                    textures.Items.Add(t);
                if (textures.Items.Count > 0)
                {
                    int index = 0;
                    if (selected != null)
                        for (int i = 0; i < textures.Items.Count; i++)
                        {
                            var t = (HudTexture)textures.Items[i];
                            if (t.Archive == selected.Archive && t.Key == selected.Key)
                                index = i;
                        }

                    textures.SelectedIndex = index;
                }
                else
                {
                    SetImage(null);
                    detail.Text = search.Text.Trim().Length > 0 ? L.T("Keine Textur mit diesem Namensteil in den geladenen Archiven.", "No texture matching this name in the loaded archives.") : category.SelectedIndex == 11 ? "No tt_item_box_* textures loaded. Add Race.szs from your pack. These textures also appear under Items / minimap." : category.SelectedIndex == 10 ? "No speedometer layout loaded. Add the mod archive containing the speedometer, such as RaceAssets.szs. Race.szs and its language archive may not contain it." : category.SelectedIndex == 8 ? "No Input Viewer textures loaded. Use Add archive to open Retro Rewind Assets/RaceAssets.szs. Both standard and Nunchuk layouts are supported." : category.SelectedIndex == 2 ? "Items and minimap are normally in Race.szs. Use Add archive to load Race.szs from the same custom pack." : category.SelectedIndex == 3 ? "No pending picture replacements. Choose a texture in another category or import a picture folder." : "No textures in this category. Add your language's Race archive or select All textures.";
                }
            }
            finally
            {
                textures.EndUpdate();
                refreshingTextures = false;
            }

            if (Selected != null)
                ShowPreview();
            else
                UpdateState();
        }

        void UpdateState()
        {
            bool loaded = session.Archives.Count > 0;
            moveHud.Enabled = loaded;
            import.Enabled = loaded;
            shadows.Enabled = session.HasShadows;
            save.Enabled = session.SelectedCount > 0 || (shadows.Enabled && shadows.Checked);
            choose.Enabled = Selected != null && TplTextureEditor.CanReplaceImage(Selected.Archive.Files[Selected.Key].Data, 0);
            clear.Enabled = Selected != null && (Selected.Archive.Pictures.ContainsKey(Selected.Key) || Selected.Archive.Generated.ContainsKey(Selected.Key));
            numberFont.Enabled = choose.Enabled && category.SelectedIndex != 8;
            numberFont.Visible = numberFont.Enabled;
            clear.Visible = clear.Enabled;
            colours.Enabled = choose.Enabled;
            mapColours.Enabled = session.Archives.Any(a => a.Files.Keys.Any(HudMapColours.IsLayout));
            sources.Text = loaded ? string.Join(" + ", session.Archives.Select(a => Path.GetFileName(a.Source)).ToArray()) + "   •   " + session.SelectedCount + L.T(" Änderungen", " changes")
                : L.T("Race.szs für Bilder und Minimap öffnen; Spracharchiv für Zahlen und Timer ergänzen.", "Open Race.szs for pictures and minimap; add a language archive for numbers and timer.");
            if (loaded) StudioUx.SetHelp(sources, string.Join("\n", session.Archives.Select(a => a.Source).ToArray()));
        }

        void SetImage(Image image)
        {
            var old = preview.Image;
            preview.Image = image;
            if (old != null)
                old.Dispose();
        }

        void ShowPreview()
        {
            try
            {
                if (Selected == null)
                    return;
                var t = Selected;
                byte[] data = t.Archive.Files[t.Key].Data;
                if (t.Key.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase) || t.Key.EndsWith(".brctr", StringComparison.OrdinalIgnoreCase))
                {
                    SetImage(null);
                    detail.Text = t.Key + "\nPending layout changes. Open Move HUD / layout to review.";
                    UpdateState();
                    return;
                }

                byte[] generated;
                if (t.Archive.Generated.TryGetValue(t.Key, out generated))
                    data = generated;
                string selected;
                if (t.Archive.Pictures.TryGetValue(t.Key, out selected))
                    using (var b = TplTextureEditor.LoadSourceBitmap(selected))
                        data = TplTextureEditor.ReplaceFirstImage(data, b, true);
                TexturePreviewResult result;
                string error;
                if (!TexturePreview.TryDecode(t.Key, data, 0, out result, out error))
                    throw new InvalidDataException(error);
                using (result)
                {
                    SetImage(new Bitmap(result.Bitmap));
                    detail.Text = Path.GetFileName(t.Key) + " • " + result.Width + " × " + result.Height + " • " + result.FormatName + "\n" + Path.GetFileName(t.Archive.Source) + " / " + t.Key + "\n" + (selected != null ? "Replacement: " + Path.GetFileName(selected) : generated != null ? "Generated / recoloured texture" : "Opened archive's texture");
                }
            }
            catch (Exception ex)
            {
                SetImage(null);
                detail.Text = ex.Message;
            }

            UpdateState();
        }

        void Choose()
        {
            var t = Selected;
            if (t == null)
                return;
            using (var d = new OpenFileDialog
            {
                Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp"
            }

            )
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    using (var b = TplTextureEditor.LoadSourceBitmap(d.FileName))
                        t.Archive.Generated[t.Key] = TplTextureEditor.ReplaceFirstImage(t.Archive.Files[t.Key].Data, b, true);
                    t.Archive.Pictures.Remove(t.Key);
                    dirty = true;
                    RefreshList();
                }
        }

        void EditLayouts()
        {
            if (session.Archives.Count == 0)
                return;
            var snapshots = session.Archives.Select(a => new StudioArchiveCopy(a.Source, a.Build(false))).ToList();
            var d = new GameHudForm(snapshots);
            StudioEditor.Open(this, d, delegate(DialogResult result) {
                if (result == DialogResult.OK)
                {
                    foreach (var change in d.Session.Changes)
                    {
                        var target = session.Archives.Single(a => a.Source == change.Archive.Source);
                        target.Generated[change.Key] = (byte[])change.Bytes.Clone();
                        target.Pictures.Remove(change.Key);
                        dirty = true;
                    }

                    RefreshList();
                    if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                }
            });
        }

        void EditMapColours()
        {
            var layouts = session.Archives.SelectMany(a => a.Files.Keys.Where(HudMapColours.IsLayout).Select(k => new HudTexture { Archive = a, Key = k })).ToList();
            var d = new HudMapColourForm(layouts);
            StudioEditor.Open(this, d, delegate(DialogResult result) {
                if (result == DialogResult.OK)
                {
                    foreach (var change in d.Results)
                        change.Key.Archive.Generated[change.Key.Key] = change.Value;
                    dirty |= d.Results.Count > 0;
                    RefreshList();
                    if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                }
            });
        }

        void Recolour()
        {
            var t = Selected;
            if (t == null)
                return;
            byte[] data = t.Archive.Files[t.Key].Data;
            byte[] generated;
            if (t.Archive.Generated.TryGetValue(t.Key, out generated))
                data = generated;
            string path;
            if (t.Archive.Pictures.TryGetValue(t.Key, out path))
                using (var b = TplTextureEditor.LoadSourceBitmap(path))
                    data = TplTextureEditor.ReplaceFirstImage(data, b, true);
            var dialog = new HudTextureColorForm(t.Key, data);
            StudioEditor.Open(this, dialog, delegate(DialogResult result) {
                if (result == DialogResult.OK)
                {
                    t.Archive.Generated[t.Key] = dialog.Result;
                    t.Archive.Pictures.Remove(t.Key);
                    dirty = true;
                    RefreshList();
                    if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                }
            });
        }

        void GenerateNumber()
        {
            var t = Selected;
            if (t == null)
                return;
            var dialog = new HudNumberFontForm(t.Key, t.Archive.Files[t.Key].Data, numberSettings, session.Textures(4), t);
            StudioEditor.Open(this, dialog, delegate(DialogResult result) {
                if (result == DialogResult.OK)
                {
                    foreach (var change in dialog.Results)
                    {
                        change.Key.Archive.Generated[change.Key.Key] = change.Value;
                        change.Key.Archive.Pictures.Remove(change.Key.Key);
                    }

                    dirty = true;
                    RefreshList();
                    if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                }
            });
        }

        void Save()
        {
            var paths = session.Save(output.Text, shadows.Checked);
            dirty = false;
            status.Text = "Saved " + paths.Count + " edited archive(s) to " + output.Text + ". Back up your pack files before copying these results into it.";
            if (paths.Count > 0)
                ExportHelp.Show(this, output.Text);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SetImage(null);
                tips.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
