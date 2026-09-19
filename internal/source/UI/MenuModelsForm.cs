using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        readonly TabControl appearance = new TabControl
        {
            Dock = DockStyle.Fill
        };
        readonly PictureBox preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(20, 20, 24)
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
        readonly string archiveName;
        TableLayoutPanel layout;
        Func<string> sharedOutput;
        Action<string> setSharedOutput;
        string selectedGlobe;
        bool customOutput;

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
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "MKWii Menu Models Tool";
            Size = new Size(880, 590);
            MinimumSize = new Size(780, 540);
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
            grid.Dock = DockStyle.Top;
            grid.Height = embedded ? 380 : 450;
            scrollHost.Controls.Add(grid);
            scrollHost.SizeChanged += delegate
            {
                grid.Height = Math.Max(embedded ? 380 : 450, scrollHost.ClientSize.Height);
            };
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
            browse.Text = "Open file…";
            browse.Name = "PackSourceAction";
            browse.Click += delegate { ChooseModelArchive(); };
            grid.Controls.Remove(source);
            var sourcePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty
            };
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
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
            grid.RowStyles[1].Height = 70;            models.Dock = DockStyle.Fill;
            models.AutoScroll = true;
            models.Padding = new Padding(8);
            models.BackColor = DarkTheme.Panel;
            models.Enabled = false;
            grid.Controls.Add(models, 0, 2);
            grid.SetColumnSpan(models, 2);
            colors.Dock = DockStyle.Fill;
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
                        glowButton.FlatAppearance.BorderColor = glowColor;
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
            DarkTheme.StyleTabs(appearance);
            colors.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(940, 0), Margin = new Padding(4, 4, 4, 4), Text = L.T("Sternmuster: kleines helles Muster auf dunklem Grund, das über den 3D-Himmel wiederholt wird. Für ein normales Hintergrundbild den Tab Standbild verwenden.", "Star pattern: a small light pattern on a dark background, repeated across the 3D sky. For a normal background picture, use Still image.") });
            var originalTab = new TabPage(L.T("Sternenhimmel", "Starry sky"));
            originalTab.Controls.Add(colors);
            appearance.TabPages.Add(originalTab);
            var photoTab = new TabPage(L.T("Standbild", "Still image"));
            appearance.TabPages.Add(photoTab);
            var photoGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(8)
            };
            photoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            photoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            photoTab.Controls.Add(photoGrid);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            photoGrid.Controls.Add(actions, 0, 0);
            photoGrid.Controls.Add(preview, 1, 0);
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
            grid.Controls.Add(appearance, 0, 3);
            grid.SetColumnSpan(appearance, 2);
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
            build.Text = L.T("Kopien für diesen Tab erstellen", "Create copies for this tab");
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
            DarkTheme.Apply(this);
            StyleButtons(this);
            ToolStatus.Watch(this);
            build.BackColor = DarkTheme.Accent;
            build.ForeColor = Color.White;
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
        }

        void ImportGameModels()
        {
            string folder = OutputFolder();
            string imported = GameArchiveImportForm.Import(this, archiveName, folder);
            if (imported == null)
                return;
            try
            {
                string model = imported;
                string globeOverride = null;
                if (Path.GetFileName(imported) == "globe.arc")
                {
                    globeOverride = imported;
                    model = Path.Combine(Path.GetDirectoryName(imported), archiveName);
                    if (!File.Exists(model))
                        model = MenuModelSource.FindCached(archiveName);
                    if (model == null)
                    {
                        status.Text = L.T("globe.arc gespeichert. Bitte noch Earth.szs auswählen oder importieren.",
                            "globe.arc saved. Please select or import Earth.szs as well.");
                        return;
                    }
                }
                string sourceCopy = MenuModelSource.RememberArchive(model, archiveName, globeOverride);
                selectedGlobe = null;
                LoadArchive(sourceCopy);
                status.Text = L.T("Import gespeichert in: ", "Import saved to: ") + Path.GetDirectoryName(imported)
                    + L.T(". Bearbeitete Kopien werden im gewählten Ausgabeordner gespeichert.",
                          ". Edited copies will be saved in the selected output folder.");
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
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    AddArchives(dialog.FileNames);
                }
                catch (Exception error)
                {
                    StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
                    selectedGlobe = path;
                }
            }
            foreach (string path in paths)
                if (String.Equals(Path.GetFileName(path), archiveName, StringComparison.OrdinalIgnoreCase))
                    LoadArchive(path);
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
                    b.BackColor = Color.FromArgb(36, 39, 49);
                    b.FlatAppearance.BorderColor = Color.FromArgb(78, 82, 98);
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
                    }
            };
            return b;
        }

        void RefreshColors()
        {
            glowButton.FlatAppearance.BorderColor = glowColor;
            globeButton.BackColor = skyButton.BackColor = Color.FromArgb(36, 39, 49);
            globeButton.ForeColor = skyButton.ForeColor = Color.White;
            globeButton.FlatStyle = skyButton.FlatStyle = FlatStyle.Flat;
            globeButton.FlatAppearance.BorderSize = skyButton.FlatAppearance.BorderSize = 3;
            globeButton.FlatAppearance.BorderColor = globeColor;
            skyButton.FlatAppearance.BorderColor = skyColor;
            status.Text = L.T("Farben tönen die bestehenden Oberflächen. Weiß = unveränderte Originalfarben. Vorschau: Farbfelder, keine Spielansicht.", "Colours tint the existing surfaces. White = unchanged original colours. Preview: colour swatches, not an in-game view.");
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
                layout.RowStyles[2].SizeType = SizeType.Absolute;
                layout.RowStyles[2].Height = isEarth ? 64 : 112;
                layout.RowStyles[3].SizeType = SizeType.Percent;
                layout.RowStyles[3].Height = 100;
                ClearPicture();
                globeColor = skyColor = glowColor = Color.White;
                starPicture = "";
                starButton.Text = L.T("Sternmuster wählen…", "Choose star pattern…");
                RefreshColors();
                build.Enabled = true;
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
                    var previous = preview.Image;
                    preview.Image = bitmap;
                    if (previous != null)
                        previous.Dispose();
                }

                fullPicture = path;
                UpdatePictureInfo();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text);
            }
        }

        void ClearPicture()
        {
            fullPicture = "";
            var previous = preview.Image;
            preview.Image = null;
            if (previous != null)
                previous.Dispose();
            UpdatePictureInfo();
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
            if (disposing && preview.Image != null)
            {
                preview.Image.Dispose();
                preview.Image = null;
            }

            base.Dispose(disposing);
        }

        void PreviewSetup()
        {
            using (var f = new Form
            {
                Text = L.T("Vorschau der Auswahl — schematisch", "Setup preview — schematic"),
                Size = new Size(780, 590),
                StartPosition = FormStartPosition.CenterParent
            }

            )
            {
                var image = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom
                };
                var summary = new TextBox
                {
                    Dock = DockStyle.Bottom,
                    Height = 160,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical
                };
                foreach (var pair in switches)
                    summary.AppendText((pair.Value.Checked ? "ON  " : "OFF ") + pair.Value.Text + Environment.NewLine);
                summary.AppendText("Schematic colour swatches and model visibility; not the exported 3D scene.");
                var b = new Bitmap(720, 330);
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(35, 35, 42));
                    if (isEarth)
                    {
                        using (var sky = new SolidBrush(Color.FromArgb(255, skyColor.R / 3, skyColor.G / 3, skyColor.B / 3)))
                            g.FillRectangle(sky, 0, 0, 720, 330);
                        bool globeVisible = !switches.ContainsKey("earth_with_dummy_tex.brres") || switches["earth_with_dummy_tex.brres"].Checked;
                        if (globeVisible)
                        {
                            using (var glow = new Pen(glowColor, 14))
                                g.DrawEllipse(glow, 260, 60, 200, 200);
                            using (var globe = new SolidBrush(globeColor))
                                g.FillEllipse(globe, 260, 60, 200, 200);
                        }

                        g.DrawString("Colour combination only — no globe geometry / game lighting", Font, Brushes.White, 15, 300);
                    }
                    else
                        g.DrawString("Selected model visibility is listed below.\n3D geometry is not rendered by this preview.", Font, Brushes.White, 30, 90);
                }

                image.Image = b;
                f.Controls.Add(image);
                f.Controls.Add(summary);
                DarkTheme.Apply(f);
                try
                {
                    f.ShowDialog(this);
                }
                finally
                {
                    image.Image = null;
                    b.Dispose();
                }
            }
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
                    BackupManager.WriteAllBytesSafely(Path.Combine(dir, "globe.arc"), globe);
                }

                BackupManager.WriteAllBytesSafely(path, model);
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
