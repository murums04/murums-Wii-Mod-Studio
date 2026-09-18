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
        readonly ComboBox pictureMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 350
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
        TableLayoutPanel layout;
        Func<string> sharedOutput;
        public void UseOutputFolder(Func<string> resolve)
        {
            sharedOutput = resolve;
            layout.RowStyles[4].Height = 0;
            foreach (Control c in layout.Controls)
                if (layout.GetRow(c) == 4)
                    c.Visible = false;
        }

        public MenuModelsForm()
        {
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Font;
            Text = L.T("3D-Menühintergründe", "3D menu backgrounds");
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
            grid.Height = 460;
            scrollHost.Controls.Add(grid);
            scrollHost.SizeChanged += delegate
            {
                grid.Height = Math.Max(460, scrollHost.ClientSize.Height);
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
            browse.Click += delegate
            {
                using (var d = new OpenFileDialog
                {
                    InitialDirectory = LocalFolder,
                    Filter = "Model archives|BackModel.szs;Earth.szs|SZS|*.szs"
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                        LoadArchive(d.FileName);
            };
            grid.Controls.Add(browse, 1, 1);
            models.Dock = DockStyle.Fill;
            models.AutoScroll = true;
            models.Padding = new Padding(4, 4, 4, 4);
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
            DarkTheme.StyleTabs(appearance);
            colors.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(940, 0), Margin = new Padding(4, 12, 4, 4), Text = L.T("Sternmuster: kleines helles Muster auf dunklem Grund, das über den 3D-Himmel wiederholt wird. Für ein normales Hintergrundbild den Tab Foto / GIF verwenden.", "Star pattern: a small light pattern on a dark background, repeated across the 3D sky. For a normal background picture, use Photo / GIF.") });
            var originalTab = new TabPage(L.T("Sternenhimmel", "Starry sky"));
            originalTab.Controls.Add(colors);
            appearance.TabPages.Add(originalTab);
            var photoTab = new TabPage(L.T("Foto / GIF", "Photo / GIF"));
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
                Text = L.T("Foto oder GIF wählen…", "Choose photo or GIF…"),
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
            pictureMode.Items.Add(L.T("Standbild · höhere Auflösung (empfohlen)", "Still image · higher resolution (recommended)"));
            pictureMode.Items.Add(L.T("GIF · 8 Bilder (experimentell)", "GIF · 8 frames (experimental)"));
            pictureMode.SelectedIndex = 0;
            pictureMode.SelectedIndexChanged += delegate
            {
                UpdatePictureInfo();
            };
            actions.Controls.Add(pictureMode);
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
            output.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RR_MODELS_BUILD");
            grid.Controls.Add(output, 0, 4);
            var folder = new Button
            {
                Text = L.T("Ausgabeordner…", "Output folder…"),
                Dock = DockStyle.Fill
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
            build.Enabled = false;
            build.Click += delegate
            {
                Build();
            };
            grid.Controls.Add(build, 0, 6);
            grid.SetColumnSpan(build, 2);
            status.Dock = DockStyle.Fill;
            grid.Controls.Add(status, 0, 5);
            grid.SetColumnSpan(status, 2);
            DarkTheme.Apply(this);
            StyleButtons(this);
            build.BackColor = DarkTheme.Accent;
            build.ForeColor = Color.White;
            string local = Path.Combine(LocalFolder, "BackModel.szs");
            if (File.Exists(local))
                LoadArchive(local);
        }

        public Button ExternalBuildButton()
        {
            layout.RowStyles[6].SizeType = SizeType.Absolute;
            layout.RowStyles[6].Height = 0;
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

        static string LocalFolder
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "internal", "local-game-files");
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

        public void LoadArchive(string path)
        {
            try
            {
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
                isEarth = switches.ContainsKey("earth_with_dummy_tex.brres");
                appearance.Visible = isEarth;
                layout.RowStyles[2].SizeType = isEarth ? SizeType.Absolute : SizeType.Percent;
                layout.RowStyles[2].Height = isEarth ? 72 : 100;
                layout.RowStyles[3].SizeType = isEarth ? SizeType.Percent : SizeType.Absolute;
                layout.RowStyles[3].Height = isEarth ? 100 : 0;
                ClearPicture();
                globeColor = skyColor = glowColor = Color.White;
                starPicture = "";
                starButton.Text = L.T("Sternmuster wählen…", "Choose star pattern…");
                RefreshColors();
                build.Enabled = true;
                status.Text = L.T("Nur Kopien werden geschrieben. Kopiere das Ergebnis in dein Custom Pack und starte über WheelWizard neu.", "Only copies are written. Copy the result into your custom pack and restart through WheelWizard.");
            }
            catch (Exception ex)
            {
                build.Enabled = false;
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
            pictureInfo.Text = fullPicture.Length == 0 ? L.T("Kein Foto gewählt. Der Sternenhimmel bleibt erhalten. Beispiel: ein breites Bild im Format 16:9.", "No photo selected. Keeps the starry sky. Example: a wide 16:9 image.") : Path.GetFileName(fullPicture) + "\r\n" + (pictureMode.SelectedIndex == 0 ? L.T("Standbild: 1024 × 576; bei GIFs das erste Bild.", "Still image: 1024 × 576; first frame for GIFs.") : L.T("Experimentell: 8 verteilte GIF-Bilder, je 512 × 256. Weniger scharf und flüssig.", "Experimental: 8 sampled GIF frames, each 512 × 256. Less sharp and smooth.")) + "\r\n" + L.T("Vorschau zeigt das Standbild mit gewähltem Zuschnitt, nicht die Spielansicht. Ersetzt das Sternmuster; der Globus bleibt separat einstellbar.", "Preview shows the still image with selected fitting, not the game view. Replaces the star pattern; the globe is controlled separately.");
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
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 225,
                Height = 44,
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
            build.Enabled = false;
            UseWaitCursor = true;
            try
            {
                string dir = Path.GetFullPath(sharedOutput == null ? output.Text.Trim() : sharedOutput()), path = Path.Combine(dir, Path.GetFileName(source.Text));
                if (String.Equals(Path.GetFullPath(source.Text), path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(L.T("Bitte einen anderen Ausgabeordner wählen; das Quellarchiv bleibt erhalten.", "Choose a different output folder to preserve the source archive."));
                var archive = U8Archive.Load(File.ReadAllBytes(source.Text));
                if (isEarth && fullPicture.Length > 0)
                {
                    bool animated;
                    SceneColorTools.Find(archive.Root, "galaxy.brres").Data = GlobePictureBackground.Build(LocalFolder, fullPicture, pictureMode.SelectedIndex == 0, fitting.SelectedIndex == 0, out animated);
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
                string globeSource = Path.Combine(LocalFolder, "globe.arc");
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
