using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class MenuModelsForm : Form
    {
        readonly TextBox source = new TextBox(), output = new TextBox();
        readonly FlowLayoutPanel models = new FlowLayoutPanel();
        readonly Button build = new Button();
        readonly Label status = new Label();
        readonly Dictionary<string, CheckBox> switches = new Dictionary<string, CheckBox>();
        readonly FlowLayoutPanel colors = new FlowLayoutPanel();
        Color globeColor = Color.White, skyColor = Color.White, glowColor = Color.White;
        string starPicture = "";
        Button starButton;
        Button globeButton, skyButton, glowButton;
        readonly ComboBox skyMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly Panel appearance = new Panel
        {
            Dock = DockStyle.Fill
        };
        readonly Panel modelReview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28) };
        Bitmap skyPreview;
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = DarkTheme.Panel
        };
        readonly ComboBox fitting = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 350
        };
        readonly Label pictureInfo = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(470, 0)
        };
        readonly Button clearPicture = new SelectionClearButton
        {
            Width = 200,
            Height = 32
        };
        string fullPicture = "";
        bool isEarth;
        readonly bool embedded;
        readonly string archiveName;
        TableLayoutPanel layout;
        Func<string> sharedOutput;
        Action<string> setSharedOutput;
        string selectedGlobe;
        bool customOutput;
        SpecialEditorHistory editHistory;

        object[] CaptureSettings()
        {
            return new object[] { globeColor, skyColor, glowColor, starPicture, fullPicture, skyMode.SelectedIndex,
                fitting.SelectedIndex, switches.ToDictionary(x => x.Key, x => x.Value.Checked) };
        }

        void RestoreSettings(object[] state)
        {
            globeColor = (Color)state[0]; skyColor = (Color)state[1]; glowColor = (Color)state[2];
            foreach (var entry in (Dictionary<string, bool>)state[7])
                if (switches.ContainsKey(entry.Key)) switches[entry.Key].Checked = entry.Value;
            ClearPicture(); fitting.SelectedIndex = (int)state[6]; skyMode.SelectedIndex = (int)state[5];
            starPicture = (string)state[3];
            starButton.Text = starPicture.Length == 0 ? L.T("Sternmuster wählen…", "Choose star pattern…") : Path.GetFileName(starPicture);
            if (((string)state[4]).Length > 0) SelectPicture((string)state[4]);
            RefreshColors(); UpdatePictureInfo();
        }

        public void UseOutputFolder(Func<string> resolve, Action<string> update = null)
        {
            sharedOutput = resolve;
            setSharedOutput = update;
            output.Text = resolve();
            customOutput = false;
        }

        string OutputFolder()
        {
            return customOutput || sharedOutput == null ? output.Text.Trim() : sharedOutput();
        }
        internal void RefreshSharedOutput()
        {
            if (sharedOutput != null && output.Text != sharedOutput())
                output.Text = sharedOutput();
        }

        public MenuModelsForm(string archiveName) : this(archiveName, false)
        {
        }

        internal MenuModelsForm(string archiveName, bool embedded)
        {
            this.archiveName = archiveName;
            this.embedded = embedded;
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "MKWii Menu Models Tool";
            Size = new Size(1050, 830);
            MinimumSize = new Size(950, 680);
            StartPosition = FormStartPosition.CenterParent;
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                ColumnCount = 2,
                RowCount = 7
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            layout = grid;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            Controls.Add(scrollHost);
            grid.Dock = DockStyle.Fill;
            scrollHost.Controls.Add(grid);
            scrollHost.AutoScroll = false;
            var help = new Label
            {
                Dock = DockStyle.Fill,
                Text = L.T("Häkchen = Modell sichtbar. Schalte einzelne Modelle aus und erstelle die Kopie. Zum Wiederanzeigen einschalten und erneut erstellen. Verwendet immer das unveränderte Quellarchiv; betrifft alle Menüs, die dieses Modell teilen.", "Checked = model visible. Switch individual models off and create a copy. To show them again, switch on and rebuild. Always uses the unchanged source archive; affects every menu sharing the model.")
            };
            grid.Controls.Add(help, 0, 0);
            grid.SetColumnSpan(help, 2);
            source.Dock = DockStyle.Fill;
            source.ReadOnly = true;
            grid.Controls.Add(source, 0, 1);
            var browse = new Button
            {
                Text = L.T("Quelle wählen…", "Choose source…"),
                Dock = DockStyle.Fill
            };
            browse.Text = L.T("Archiv hinzufügen…", "Add archive…");
            StudioActions.Icon(browse, StudioIcon.Add);
            browse.Name = "PackSourceAction";
            browse.Click += delegate { ChooseModelArchive(); };
            var clearArchives = new Button { Text = L.T("Auswahl leeren", "Clear selection"), Name = "PackClearAction", AutoSize = true };
            StudioActions.Icon(clearArchives, StudioIcon.Remove);
            clearArchives.Click += delegate {
                if (StudioMessageBox.Show(this, L.T("Geladene Quellen und Modelleinstellungen entfernen?", "Clear loaded sources and model settings?"), Text, MessageBoxButtons.YesNo) == DialogResult.Yes) ClearArchives();
            };
            Controls.Add(clearArchives);
            grid.Controls.Remove(source);
            var sourcePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty
            };
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sourcePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            sourcePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            sourcePanel.Controls.Add(new Label
            {
                Text = L.T("Datei öffnen: ", "Open file: ") + (archiveName == "Earth.szs" ? "Earth.szs + globe.arc" : "BackModel.szs"),
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, 0);
            sourcePanel.Controls.Add(browse, 1, 0);
            sourcePanel.Controls.Add(source, 0, 1);
            sourcePanel.SetColumnSpan(source, 2);
            grid.Controls.Add(sourcePanel, 0, 1);
            grid.SetColumnSpan(sourcePanel, 2);
            grid.RowStyles[1].Height = 70;
            models.Dock = DockStyle.Top;
            models.AutoSize = true;
            models.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            models.AutoScroll = false;
            models.Padding = new Padding(8);
            models.BackColor = DarkTheme.Panel;
            models.Enabled = false;
            grid.Controls.Add(models, 0, 2);
            grid.SetColumnSpan(models, 2);
            colors.Dock = DockStyle.Fill;
            colors.AutoScroll = true;
            colors.FlowDirection = FlowDirection.TopDown;
            colors.WrapContents = false;
            colors.WrapContents = true;
            colors.AutoScroll = true;
            globeButton = ColorButton(L.T("Globus-Farbe…", "Globe colour…"), true);
            skyButton = ColorButton(L.T("Himmel-Farbe…", "Sky colour…"), false);
            colors.Controls.Add(globeButton);
            colors.Controls.Add(skyButton);
            glowButton = new Button
            {
                Text = L.T("Globus-Schein…", "Globe glow colour…"),
                Width = 170,
                Height = 36
            };
            glowButton.Click += delegate
            {
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = glowColor
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        glowColor = d.Color;
                        RefreshColourBorders();
                        status.Text = L.T("Färbt den Leuchtrand separat. Weiß erhält den ursprünglichen blauen Schein.", "Tints the glowing rim separately. White keeps the original blue glow.");
                    }
            };
            colors.Controls.Add(glowButton);
            colors.SetFlowBreak(glowButton, true);
            var reset = new Button
            {
                Text = L.T("Originalfarben", "Original colours"),
                Width = 155,
                Height = 36
            };
            reset.Click += delegate
            {
                globeColor = skyColor = glowColor = Color.White;
                RefreshColors();
            };
            colors.Controls.Add(reset);
            var liveColours = new Button { Text = L.T("Live-Farbvorschau…", "Live colour preview…"), Width = 195, Height = 36 };
            liveColours.Click += delegate { PreviewSetup(); };
            colors.Controls.Add(liveColours);
            starButton = new Button
            {
                Text = L.T("Sternmuster wählen…", "Choose star pattern…"),
                Width = 185,
                Height = 32
            };
            starButton.Click += delegate
            {
                using (var d = new OpenFileDialog
                {
                    Filter = "Pictures|*.png;*.jpg;*.jpeg"
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        starPicture = d.FileName;
                        starButton.Text = Path.GetFileName(starPicture);
                        status.Text = L.T("Helles Muster auf dunklem Grund, z. B. Sterne. Wird als Graustufen-Sterntextur gekachelt, nicht als Vollbildfoto. Die Himmelfarbe färbt das Muster.", "Light pattern on dark background, e.g. stars. Tiled as a grayscale star texture, not a full-screen photo. Sky colour tints the pattern.");
                    }
            };
            colors.Controls.Add(starButton);
            var resetPattern = new Button
            {
                Text = L.T("Originalmuster", "Original pattern"),
                Width = 155,
                Height = 32
            };
            resetPattern.Click += delegate
            {
                starPicture = "";
                starButton.Text = L.T("Sternmuster wählen…", "Choose star pattern…");
            };
            colors.Controls.Add(resetPattern);
            colors.SetFlowBreak(resetPattern, true);
            colors.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(430, 0), Text = L.T("Helles Muster auf dunklem Grund. Wiederholt sich über dem Himmel; kein Vollbildfoto.", "Light pattern on a dark background. Repeated across the sky; not a full-screen photo.") });
            var skyGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            skyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            skyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 282));
            skyGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            appearance.Controls.Add(skyGrid);
            var globeActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            foreach (Control control in new Control[] { globeButton, glowButton, reset, liveColours }) globeActions.Controls.Add(control);
            var propertyGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(6) };
            propertyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            propertyGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            propertyGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            propertyGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            propertyGrid.Controls.Add(globeActions, 0, 0);
            skyGrid.Controls.Add(propertyGrid, 1, 0);
            skyMode.Items.AddRange(new object[] { L.T("Himmel: Sternenmuster", "Sky: star pattern"), L.T("Himmel: eigenes Standbild", "Sky: own still image") });
            skyMode.SelectedIndex = 0;
            propertyGrid.Controls.Add(skyMode, 0, 1);
            var skyOptions = new Panel { Dock = DockStyle.Fill };
            propertyGrid.Controls.Add(skyOptions, 0, 2);
            skyOptions.Controls.Add(colors);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            skyOptions.Controls.Add(actions);
            actions.Visible = false;
            skyMode.SelectedIndexChanged += delegate
            {
                bool photo = skyMode.SelectedIndex == 1;
                colors.Visible = !photo;
                actions.Visible = photo;
                if (!photo) ClearPicture();
            };
            skyGrid.Controls.Add(preview, 0, 0);
            var previewArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            previewArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            previewArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewArea.Controls.Add(preview, 0, 0);
            previewArea.Controls.Add(new Label { Text = L.T("Farbmuster mit Zuschnitt; Spielbeleuchtung und Originalgeometrie können abweichen.", "Colour sample with picture fitting; game lighting and original geometry may differ."), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4, 8, 4, 4) }, 0, 1);
            skyGrid.Controls.Add(previewArea, 0, 0);
            var choosePicture = new Button
            {
                Text = L.T("Bild auswählen…", "Choose picture…"),
                Width = 200,
                Height = 32
            };
            choosePicture.Click += delegate
            {
                using (var d = new OpenFileDialog
                {
                    Filter = "Pictures|*.png;*.jpg;*.jpeg;*.gif"
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                        SelectPicture(d.FileName);
            };
            actions.Controls.Add(choosePicture);
            fitting.Items.Add(L.T("Auf Bildschirm strecken (ohne Ränder)", "Stretch to screen (no borders)"));
            fitting.Items.Add(L.T("Bildschirm füllen (Ränder abschneiden)", "Fill screen (crop edges)"));
            fitting.SelectedIndex = 0;
            fitting.SelectedIndexChanged += delegate
            {
                if (fullPicture.Length > 0)
                    SelectPicture(fullPicture);
            };
            actions.Controls.Add(fitting);
            clearPicture.Text = L.T("Originalhimmel verwenden", "Use original sky");
            clearPicture.Click += delegate
            {
                ClearPicture();
            };
            actions.Controls.Add(clearPicture);
            actions.Controls.Add(pictureInfo);
            foreach (Control property in colors.Controls.Cast<Control>().Concat(actions.Controls.Cast<Control>()))
            {
                property.MaximumSize = new Size(242, 0);
                property.Width = Math.Min(property.Width, 242);
            }
            fitting.DropDownWidth = 400;
            var modelStage = new Panel { Dock = DockStyle.Fill };
            modelStage.Controls.Add(appearance);
            grid.Controls.Add(modelStage, 0, 3);
            grid.SetColumnSpan(modelStage, 2);
            var modelReviewFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            var reviewTitle = new Label { Text = L.T("Menümodelle prüfen", "Review menu models"), AutoSize = true, Font = new Font(Font.FontFamily, 16, FontStyle.Bold), Margin = new Padding(0, 0, 0, 14) };
            var reviewHint = new Label { Text = L.T("Wähle oben, welche Modelle sichtbar bleiben. Prüfe sie anschließend in der 3D-Vorschau und speichere deine Archivkopie.", "Choose above which models stay visible. Review them in the 3D preview, then save your archive copy."), AutoSize = true, MaximumSize = new Size(580, 0), Margin = new Padding(0, 0, 0, 18) };
            var modelPreview = new Button { Text = L.T("3D-Vorschau öffnen", "Open 3D preview"), AutoSize = true, MinimumSize = new Size(220, 42) };
            modelPreview.Click += delegate { if (HasLoadedArchive) PreviewSetup(); else ChooseModelArchive(); };
            modelReviewFlow.Controls.Add(reviewTitle); modelReviewFlow.Controls.Add(reviewHint); modelReviewFlow.Controls.Add(modelPreview);
            modelReview.Controls.Add(modelReviewFlow);
            modelStage.Controls.Add(modelReview);
            modelReview.Visible = false;
            EventHandler refreshModelFlow = delegate
            {
                bool loaded = HasLoadedArchive;
                models.Visible = loaded;
                modelReview.Visible = !loaded || !isEarth;
                appearance.Visible = loaded && isEarth;
                reviewTitle.Text = loaded ? L.T("Menümodelle prüfen", "Review menu models") : L.T("Modelldateien öffnen", "Open model files");
                reviewHint.Text = loaded ? L.T("Wähle oben, welche Modelle sichtbar bleiben. Prüfe sie anschließend in der 3D-Vorschau und speichere deine Archivkopie.", "Choose above which models stay visible. Review them in the 3D preview, then save your archive copy.")
                    : L.T("Wähle ", "Choose ") + (archiveName == "Earth.szs" ? "Earth.szs / globe.arc" : "BackModel.szs") + L.T(" aus deinem Pack. Fehlende Originaldateien ergänzt der Custom Pack Maker.", " from your pack. Use Custom Pack Maker to add missing original files.");
                modelPreview.Text = loaded ? L.T("3D-Vorschau öffnen", "Open 3D preview") : L.T("Modellarchiv wählen…", "Choose model archive…");
            };
            source.TextChanged += refreshModelFlow;
            refreshModelFlow(this, EventArgs.Empty);
            UpdatePictureInfo();
            output.Dock = DockStyle.Fill;
            output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MUR_EDITED");
            var outputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
            outputPanel.Controls.Add(new Label { Text = L.T("Ausgabeordner", "Output location"), AutoSize = true }, 0, 0);
            outputPanel.Controls.Add(output, 0, 1);
            grid.Controls.Add(outputPanel, 0, 4);
            grid.RowStyles[4].Height = 64;
            output.TextChanged += delegate
            {
                customOutput = true;
                if (setSharedOutput != null)
                    setSharedOutput(output.Text);
            };
            var folder = new Button
            {
                Text = L.T("Auswählen", "Browse"),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Size = new Size(140, 30)
            };
            folder.Click += delegate
            {
                using (var d = new FolderPickerDialog
                {
                    SelectedPath = output.Text
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                        output.Text = d.SelectedPath;
            };
            grid.Controls.Add(folder, 1, 4);
            StudioActions.Icon(folder, StudioIcon.Folder);
            build.Text = L.T("Modellkopien speichern", "Save model copies");
            build.Dock = DockStyle.None;
            build.Anchor = AnchorStyles.Right;
            build.Size = new Size(260, 34);
            ToolStatus.Set(this, false); build.Enabled = false;
            build.Click += delegate
            {
                Build();
            };
            grid.RowCount = 5;
            while (grid.RowStyles.Count > 5) grid.RowStyles.RemoveAt(grid.RowStyles.Count - 1);
            var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 78, ColumnCount = 1, RowCount = 2, Padding = new Padding(14, 0, 14, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.Controls.Add(build, 0, 0);
            footer.Controls.Add(ToolStatus.Wrap(this, status), 0, 1);
            Controls.Add(footer);
            footer.SendToBack();
            if (!embedded) PackSelection.Attach(this, delegate(CustomPack pack)
            {
                output.Text = Path.Combine(pack.FilesFolder, "MUR_EDITED");
                customOutput = true;
                if (setSharedOutput != null) setSharedOutput(output.Text);
            });
            footer.SendToBack();
            if (embedded)
            {
                sourcePanel.Controls.Clear();
                sourcePanel.RowCount = 1;
                sourcePanel.RowStyles.Clear();
                sourcePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                sourcePanel.Controls.Add(source, 0, 0);
                sourcePanel.SetColumnSpan(source, 1);
                sourcePanel.Controls.Add(browse, 1, 0);
                grid.RowStyles[1].Height = 36;
                grid.RowStyles[4].Height = 0;
                outputPanel.Visible = folder.Visible = false;
                footer.Visible = false;
            }
            DarkTheme.Apply(this);
            StyleButtons(this);
            RefreshColourBorders();
            ToolStatus.Watch(this);
            build.BackColor = DarkTheme.Accent;
            build.ForeColor = Color.White;
            DarkTheme.StylePrimary(modelPreview);
            VisibleChanged += delegate
            {
                if (!Visible || source.Text.Length != 0)
                    return;
                string available = MenuModelSource.FindCached(archiveName);
                if (available != null)
                    LoadArchive(available);
            };
            appearance.Visible = false;
            help.Text = L.T("Modelldateien aus deinem Pack öffnen. Fehlende Originaldateien im Custom Pack Maker aus einer ISO importieren.", "Open model files from your pack. Import missing original files from an ISO in Custom Pack Maker.");
            help.Text += "\n" + L.T("Dateien: ", "Files: ") + (archiveName == "Earth.szs" ? "Earth.szs / globe.arc" : "BackModel.szs");
            help.Visible = !embedded;
            layout.RowStyles[0].Height = embedded ? 0 : 48;
            string cached = MenuModelSource.FindCached(archiveName);
            if (cached != null)
                LoadArchive(cached);
            else
                status.Text = L.T("Quelldatei wählen: ", "Choose a source file: ") + archiveName + L.T(". Nicht im RR-Download enthalten.", ". Not included in the RR download.");
            var historyBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false };
            Controls.Add(historyBar);
            editHistory = new SpecialEditorHistory(this, historyBar, CaptureSettings, RestoreSettings, () => source.Text + "|" + selectedGlobe);
        }

        void ImportGameModels()
        {
            string selected = GameArchiveImportForm.Select(this, archiveName == "Earth.szs"
                ? "Earth.szs / globe.arc|Earth.szs;globe.arc" : "BackModel.szs|BackModel.szs", archiveName);
            if (selected == null)
                return;
            try
            {
                AddArchives(new[] { selected });
            }
            catch (Exception error)
            {
                StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void ChooseGameFolder()
        {
            using (var dialog = new FolderPickerDialog
            {
                Description = L.T("Spielordner mit Modelldateien wählen", "Choose a game folder containing model files")
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string[] candidates =
                {
                    Path.Combine(dialog.SelectedPath, archiveName),
                    Path.Combine(dialog.SelectedPath, "Scene", "Model", archiveName),
                    Path.Combine(dialog.SelectedPath, "files", "Scene", "Model", archiveName)
                };
                foreach (string candidate in candidates)
                {
                    if (!File.Exists(candidate))
                        continue;
                    LoadArchive(candidate);
                    return;
                }
                StudioMessageBox.Show(this,
                    L.T("Nicht gefunden: ", "Not found: ") + archiveName + "\n\n"
                    + L.T("Wähle einen Ordner mit den entpackten Modelldateien deines Spiels. Ein RR-Download allein enthält diese Dateien nicht.",
                          "Choose a folder containing your extracted game models. An RR download alone does not include these files."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ChooseModelArchive()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = L.T("Modellarchive hinzufügen", "Add model archives"),
                Filter = archiveName == "Earth.szs"
                    ? "Earth.szs / globe.arc|Earth.szs;globe.arc"
                    : "BackModel.szs|BackModel.szs",
                InitialDirectory = PackSelection.Folder(this), Multiselect = true,
                CheckFileExists = true
            })
            {
                if (ToolArchiveFilters.Show(dialog, this) != DialogResult.OK)
                    return;
                try
                {
                    AddArchives(ToolArchiveFilters.SelectedFiles(dialog));
                }
                catch (Exception error)
                {
                    StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        internal void ClearArchives()
        {
            source.Clear(); selectedGlobe = null;
            ClearPicture(); starPicture = ""; globeColor = skyColor = glowColor = Color.White;
            foreach (var item in switches.Values) item.Checked = true;
            modelReview.Visible = false;
            if (isEarth) RefreshColors(); build.Enabled = false; status.Text = L.T("Zum Start ein Archiv hinzufügen.", "Add archive to begin.");
            PackSelection.SourceCleared(this);
            if (editHistory != null) editHistory.Reset();
        }
        internal void AddArchives(string[] paths)
        {
            foreach (string path in paths)
            {
                if (String.Equals(Path.GetFileName(path), "globe.arc", StringComparison.OrdinalIgnoreCase))
                {
                    var archive = U8Archive.Load(File.ReadAllBytes(path));
                    if (SceneColorTools.Find(archive.Root, "earth.brres.LZ") == null)
                        throw new InvalidDataException(L.T("globe.arc enthält kein Globusmodell.", "globe.arc contains no globe model."));
                    if (selectedGlobe != null && !Path.GetFullPath(selectedGlobe).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                        throw new IOException("globe.arc is already loaded. Clear selection before switching sources.");
                    selectedGlobe = path;
                }
            }
            foreach (string path in paths)
                if (String.Equals(Path.GetFileName(path), archiveName, StringComparison.OrdinalIgnoreCase))
                    if (!String.Equals(source.Text, path, StringComparison.OrdinalIgnoreCase)) LoadArchive(path);
            status.Text = L.T("Geladene Archive: ", "Loaded archives: ")
                + (source.Text.Length > 0 ? Path.GetFileName(source.Text) : L.T("Earth.szs fehlt", "Earth.szs missing"))
                + (selectedGlobe != null ? " + globe.arc" : "");
        }
        public Button ExternalBuildButton()
        {
            var footer = build.Parent as TableLayoutPanel;
            if (footer != null)
            {
                footer.RowStyles[0].Height = 0;
                footer.Height = 30;
            }
            return build;
        }

        static void StyleButtons(Control c)
        {
            foreach (Control child in c.Controls)
            {
                var b = child as Button;
                if (b != null)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.BackColor = DarkTheme.Panel2;
                    b.FlatAppearance.BorderColor = DarkTheme.Border;
                }

                StyleButtons(child);
            }
        }

        Button ColorButton(string text, bool globe)
        {
            var b = new Button
            {
                Text = text,
                Width = 170,
                Height = 36
            };
            b.Click += delegate
            {
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = globe ? globeColor : skyColor
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        if (globe)
                            globeColor = d.Color;
                        else
                            skyColor = d.Color;
                        RefreshColors();
                        if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                    }
            };
            return b;
        }

        void RefreshColourBorders()
        {
            ColourButton.SetColor(globeButton, globeColor);
            ColourButton.SetColor(skyButton, skyColor);
            ColourButton.SetColor(glowButton, glowColor);
        }

        void RefreshColors()
        {
            RefreshColourBorders();
            RefreshPreview();
            status.Text = L.T("Farben und Sichtbarkeit prüfen, dann Modellkopien speichern. Weiß bewahrt die Originalfarben.", "Review colours and visibility, then save model copies. White preserves original colours.");
        }

        internal bool HasLoadedArchive
        {
            get { return source.Text.Length > 0 && switches.Count > 0; }
        }

        public void LoadArchive(string path)
        {
            try
            {
                MenuModelSource.Validate(path, archiveName);
                var archive = U8Archive.Load(File.ReadAllBytes(path));
                switches.Clear();
                while (models.Controls.Count > 0)
                    models.Controls[0].Dispose();
                Collect(archive.Root);
                var inspect = new Button
                {
                    Text = L.T("Vorschau der Auswahl…", "Preview setup…"),
                    Width = 195,
                    Height = 34
                };
                inspect.Click += delegate
                {
                    PreviewSetup();
                };
                models.Controls.Add(inspect);
                if (switches.Count == 0)
                    throw new InvalidDataException("No supported menu models in this archive.");
                source.Text = path;
                models.Enabled = true;
                appearance.Enabled = true;
                PackSelection.SourceLoaded(this);
                isEarth = switches.ContainsKey("earth_with_dummy_tex.brres");
                appearance.Visible = isEarth;
                modelReview.Visible = !isEarth;
                layout.RowStyles[2].SizeType = SizeType.AutoSize;
                layout.RowStyles[2].Height = 0;
                layout.RowStyles[3].SizeType = SizeType.Percent;
                layout.RowStyles[3].Height = 100;
                ClearPicture();
                globeColor = skyColor = glowColor = Color.White;
                starPicture = "";
                starButton.Text = L.T("Sternmuster wählen…", "Choose star pattern…");
                RefreshColors();
                build.Enabled = true;
                if (editHistory != null) editHistory.Reset();
                status.Text = L.T("Nur Kopien werden geschrieben. Kopiere das Ergebnis in dein Custom Pack und starte dein Spiel neu.", "Only copies are written. Copy the result into your custom pack and restart your game.");
            }
            catch (Exception ex)
            {
                ToolStatus.Set(this, false); build.Enabled = false;
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text);
            }
        }

        void SelectPicture(string path)
        {
            try
            {
                {
                    var bitmap = GlobePictureBackground.Preview(path, fitting.SelectedIndex == 0);
                    if (skyPreview != null) skyPreview.Dispose();
                    skyPreview = bitmap;
                }

                fullPicture = path;
                skyMode.SelectedIndex = 1;
                UpdatePictureInfo();
                RefreshPreview();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text);
            }
        }

        void ClearPicture()
        {
            fullPicture = "";
            if (skyMode.SelectedIndex != 0) skyMode.SelectedIndex = 0;
            var previous = preview.Image;
            preview.Image = null;
            if (previous != null)
                previous.Dispose();
            if (skyPreview != null) { skyPreview.Dispose(); skyPreview = null; }
            UpdatePictureInfo();
            RefreshPreview();
        }

        void RefreshPreview()
        {
            if (!isEarth) return;
            bool visible = !switches.ContainsKey("earth_with_dummy_tex.brres") || switches["earth_with_dummy_tex.brres"].Checked;
            var previous = preview.Image;
            preview.Image = ColourSamples.Globe(globeColor, skyColor, glowColor, visible, skyPreview);
            if (previous != null) previous.Dispose();
        }

        void UpdatePictureInfo()
        {
            clearPicture.Enabled = fullPicture.Length > 0;
            skyButton.Enabled = starButton.Enabled = fullPicture.Length == 0;
            pictureInfo.Text = fullPicture.Length == 0
                ? L.T(
                    "Kein Bild gewählt. Der Sternenhimmel bleibt erhalten. Beispiel: ein breites Bild im Format 16:9.",
                    "No picture selected. Keeps the starry sky. Example: a wide 16:9 image.")
                : Path.GetFileName(fullPicture) + "\r\n"
                    + L.T(
                        "Standbild: 1024 × 576. GIFs werden nur als erstes Bild verwendet; keine Animation.",
                        "Still image: 1024 × 576. GIFs use only the first frame; no animation.")
                    + "\r\n"
                    + L.T(
                        "Die Vorschau zeigt den gewählten Zuschnitt. Ersetzt das Sternmuster; der Globus bleibt separat einstellbar.",
                        "Preview shows the selected fitting. Replaces the star pattern; the globe is controlled separately.");
            if (fullPicture.Length == 0)
                status.Text = L.T("Originalhimmel beim nächsten Erstellen. Bereits erstellte Dateien bleiben unverändert.", "Original sky on the next build. Existing files remain unchanged.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && skyPreview != null) { skyPreview.Dispose(); skyPreview = null; }
            if (disposing && preview.Image != null)
            {
                preview.Image.Dispose();
                preview.Image = null;
            }

            base.Dispose(disposing);
        }

        void PreviewSetup()
        {
            if (isEarth)
            {
                bool visible = !switches.ContainsKey("earth_with_dummy_tex.brres") || switches["earth_with_dummy_tex.brres"].Checked;
                var dialog = new GlobeColourPreviewForm(globeColor, skyColor, glowColor, visible, skyPreview);
                StudioEditor.Open(this, dialog, delegate(DialogResult result) {
                    if (result == DialogResult.OK)
                    {
                        globeColor = dialog.GlobeColor; skyColor = dialog.SkyColor; glowColor = dialog.GlowColor;
                        RefreshColors();
                        if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                    }
                });
                return;
            }
            StudioEditor.Open(this, new MenuModelPreviewForm(source.Text, switches.ToDictionary(p => p.Key, p => p.Value.Checked)), delegate { });
        }

        void Collect(ArchiveEntry entry)
        {
            if (entry.IsDirectory)
            {
                foreach (var c in entry.Children)
                    Collect(c);
                return;
            }

            string label = null;
            switch (entry.Name)
            {
                case "trophy_kinoko.brres":
                    label = L.T("Trophäe", "Trophy");
                    break;
                case "flag.brres":
                    label = L.T("Flagge / Voting", "Flag / voting");
                    break;
                case "stop_watch.brres":
                    label = L.T("Stoppuhr", "Stopwatch");
                    break;
                case "baloon.brres":
                    label = L.T("Ballons / Münze", "Balloons / coin");
                    break;
                case "earth_with_dummy_tex.brres":
                    label = L.T("Globus", "Globe");
                    break;
                case "galaxy.brres":
                    label = L.T("Hintergrund", "Background");
                    break;
            }

            if (label == null)
                return;
            if (BrresModelVisibility.IsHidden(entry.Data))
                throw new InvalidDataException(L.T("Diese Quelle enthält bereits ausgeblendete Modelle. Bitte das unveränderte Archiv wählen, damit Einschalten zuverlässig funktioniert.", "This source already contains hidden models. Choose the unchanged archive so switching them on works reliably."));
            var check = new CheckBox
            {
                Text = label + L.T(" — sichtbar", " — visible"),
                Checked = true,
                Appearance = Appearance.Normal,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 225,
                Height = 32,
                Margin = new Padding(6)
            };
            check.CheckedChanged += delegate
            {
                check.Text = label + (check.Checked ? L.T(" — sichtbar", " — visible") : L.T(" — ausgeblendet", " — hidden"));
                RefreshPreview();
            };
            switches.Add(entry.Name, check);
            models.Controls.Add(check);
        }

        void Build()
        {
            ToolStatus.Set(this, false); build.Enabled = false;
            UseWaitCursor = true;
            try
            {
                string dir = Path.GetFullPath(OutputFolder()), path = Path.Combine(dir, Path.GetFileName(source.Text));
                if (String.Equals(Path.GetFullPath(source.Text), path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(L.T("Bitte einen anderen Ausgabeordner wählen; das Quellarchiv bleibt erhalten.", "Choose a different output folder to preserve the source archive."));
                var archive = U8Archive.Load(File.ReadAllBytes(source.Text));
                if (isEarth && fullPicture.Length > 0)
                {
                    bool animated;
                    SceneColorTools.Find(archive.Root, "galaxy.brres").Data = GlobePictureBackground.Build(fullPicture, true, fitting.SelectedIndex == 0, out animated);
                }

                if (isEarth && fullPicture.Length == 0 && starPicture.Length > 0)
                {
                    var e = SceneColorTools.Find(archive.Root, "galaxy.brres");
                    using (var image = new Bitmap(starPicture))
                        e.Data = SceneColorTools.ReplaceStarPattern(e.Data, image);
                }

                if (isEarth && fullPicture.Length == 0 && skyColor.ToArgb() != Color.White.ToArgb())
                {
                    var e = SceneColorTools.Find(archive.Root, "galaxy.brres");
                    e.Data = SceneColorTools.TintSky(e.Data, skyColor);
                }

                byte[] globe = null;
                string globeSource = selectedGlobe ?? MenuModelSource.FindGlobeArchive(source.Text);
                if (isEarth && (globeColor.ToArgb() != Color.White.ToArgb() || glowColor.ToArgb() != Color.White.ToArgb()))
                {
                    if (!File.Exists(globeSource))
                        throw new FileNotFoundException(L.T("Für die Globusfarbe wird globe.arc aus deinem Spielabbild im lokalen Quellordner benötigt.", "Globe colour requires globe.arc from your game image in the local source folder."), globeSource);
                    status.Text = L.T("Globus wird eingefärbt…", "Colouring globe…");
                    status.Refresh();
                    globe = SceneColorTools.ColorGlobeArchive(File.ReadAllBytes(globeSource), globeColor, glowColor);
                }
                else if (isEarth && File.Exists(globeSource))
                    globe = File.ReadAllBytes(globeSource);
                foreach (var pair in switches)
                    if (!pair.Value.Checked)
                    {
                        var e = SceneColorTools.Find(archive.Root, pair.Key);
                        e.Data = BrresModelVisibility.Hide(e.Data);
                    }

                byte[] model = Yaz0.Compress(archive.BuildU8());
                Directory.CreateDirectory(dir);
                if (globe != null)
                {
                    if (String.Equals(Path.GetFullPath(globeSource), Path.Combine(dir, "globe.arc"), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Output must not overwrite the globe source.");
                    ArchiveCopyExport.SaveCopy(globeSource, globe, Path.Combine(dir, "globe.arc"));
                }

                ArchiveCopyExport.SaveCopy(source.Text, model, path);
                status.Text = L.T("Erstellt: ", "Created: ") + Path.GetFileName(path) + (globe != null ? " + globe.arc" : "") + "\r\n" + dir;
                ExportHelp.Show(this, dir);
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                build.Enabled = true;
            }
        }
    }
}

