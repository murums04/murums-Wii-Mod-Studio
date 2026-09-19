using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class RetroRewindGifWizard : Form
    {
        private sealed class TargetEditor
        {
            public string Key;
            public string DisplayName;
            public string ArchiveBaseName;
            public bool IsTitle;
            public TextBox CommonPath;
            public TextBox LanguagePath;
            public Control LanguageFields;
            public Label ArchiveStatus;
            public TextBox SourcePath;
            public CheckBox UseFirstFrameForRelatedScreens;
            public Label Capability;
            public TabPage Tab;
        }

        private readonly List<TargetEditor> _targets = new List<TargetEditor>();
        private readonly Dictionary<string, MenuModelsForm> _modelEditors = new Dictionary<string, MenuModelsForm>(StringComparer.OrdinalIgnoreCase);
        private TextBox _uiFolder;
        private TextBox _outputFolder;
        private string _lastDefaultOutput = "";
        private ComboBox _quality;
        private ComboBox _language;
        private readonly string[] _languageSuffixes = new string[]
        {
            "G",
            "E",
            "U",
            "F",
            "S",
            "I",
            "J",
            "M",
            "Q",
            "K"
        };
        private ComboBox _menuFraming;
        private CheckBox _cleanupOld;
        private StudioProgressBar _progress;
        private Label _status;
        private readonly Dictionary<TabPage, Button> _buildButtons = new Dictionary<TabPage, Button>();
        private readonly Dictionary<TabPage, Button> _exports = new Dictionary<TabPage, Button>();
        private Button _close;
        private bool _busy;
        private TabControl _configuration;
        public string OutputFolder { get; private set; }
        public string Summary { get; private set; }

        public RetroRewindGifWizard(string currentArchivePath)
        {
            Text = L.T("MKWii Backgrounds Tool", "MKWii Backgrounds Tool");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(980, 700);
            Size = new Size(1180, Math.Min(950, Screen.FromControl(this).WorkingArea.Height - 40));
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.25F);
            AutoScaleMode = AutoScaleMode.Dpi;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            AutoDetectPaths(currentArchivePath);
            var openSource = NewButton("Open file…");
            openSource.Name = "PackSourceAction";
            openSource.Click += delegate { BrowseMenuSource(); };
            Controls.Add(openSource);

            PackSelection.Attach(this, delegate(CustomPack pack)
            {
                DetectArchivesInFolder(pack.FilesFolder);
                if (_targets.Any(t => IsReadableArchive(t.CommonPath.Text) || IsReadableArchive(t.LanguagePath.Text)))
                    PackSelection.SourceLoaded(this);
                _outputFolder.Text = Path.Combine(pack.FilesFolder, "MUR_EDITED");
            });
            DarkTheme.Apply(this); ToolStatus.Watch(this);
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (_busy)
                {
                    e.Cancel = true;
                    _status.Text = L.T("Build läuft noch – bitte warten.", "Build is still running – please wait.");
                }
            };
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 4F));
            Controls.Add(root);
            root.Controls.Add(BuildHeaderPanel(), 0, 0);
            TabControl tabs = new TabControl();
            _configuration = tabs;
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 7);
            tabs.Margin = new Padding(0, 0, 0, 8);
            tabs.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            DarkTheme.StyleTabs(tabs);
            root.Controls.Add(tabs, 0, 1);
            TabPage buildTab = NewTab(L.T("Ordner & Qualität", "Folders & quality"));
            BuildSettingsTab(buildTab);
            tabs.TabPages.Add(buildTab);
            TabPage titleTab = NewTab(L.T("Titel / Lizenz", "Title / License"));
            AddTargetGroup(titleTab, "title", L.T("Titelbildschirm + Lizenz / Hauptmenü", "Title screen + license / main menu"), "Title", true, L.T("GIF: Titelbildschirm animiert; Lizenz/Hauptmenü verwenden standardmässig Frame 0. PNG/JPEG: alles statisch.", "GIF: animated title screen; license/main menu use frame 0 by default. PNG/JPEG: all static."));
            tabs.TabPages.Add(titleTab);
            TabPage licenseTab = NewTab(L.T("Lizenz-Einstellungen", "License settings"));
            AddTargetGroup(licenseTab, "license-settings", L.T("Lizenz-Einstellungen: Hintergrund", "License settings: background"), "Title", false,
                L.T("Title.szs: Hintergrund der Menüansicht. Obere und untere Balken separat über Menütexturen bearbeiten.", "Title.szs: menu background. Edit top and bottom bars separately through Menu textures."));
            var menuTextures = NewButton(L.T("Menütexturen / Balken...", "Menu textures / bars..."));
            menuTextures.Dock = DockStyle.Bottom;
            menuTextures.Height = 38;
            menuTextures.Click += delegate
            {
                using (var editor = new MenuTextureForm())
                    editor.ShowDialog(this);
            };
            licenseTab.Controls.Add(menuTextures);
            tabs.TabPages.Add(licenseTab);
            TabPage singleTab = NewTab(L.T("Singleplayer", "Single Player"));
            AddTargetGroup(singleTab, "single", L.T("Singleplayer + Online-Voting", "Single player + online voting"), "MenuSingle", false, L.T("Ändert den Hintergrund der Einzelspieler-Menüs und der Online-Abstimmung. Wähle ein Foto oder ein GIF; passende Animationen werden automatisch eingerichtet.", "Changes the single-player menu and online voting background. Pick a photo or GIF; compatible animations are configured automatically."));
            tabs.TabPages.Add(singleTab);
            TabPage multiTab = NewTab(L.T("Multiplayer", "Multiplayer"));
            AddTargetGroup(multiTab, "multi", L.T("Lokaler Multiplayer", "Local multiplayer"), "MenuMulti", false, L.T("Ändert das tatsächliche Hintergrundlayout, einschließlich Controller-Anmeldung. 3D-Modelle werden separat eingestellt.", "Changes the actual menu background layout. The controller registration screen is included. 3D models are managed separately."));
            tabs.TabPages.Add(multiTab);
            TabPage otherTab = NewTab(L.T("Weitere UI", "Other UI"));
            TabControl other = new TabControl
            {
                Dock = DockStyle.Fill
            };
            DarkTheme.StyleTabs(other);
            AddLoadingTab(other);
            TabPage onlineTab = NewTab(L.T("Globus & Himmel", "Globe & sky"));
            AddModelsPage(onlineTab, "Earth.szs");
            other.TabPages.Add(onlineTab);
            TabPage modelsTab = NewTab(L.T("3D-Modelle", "3D models"));
            AddModelsPage(modelsTab, "BackModel.szs");
            other.TabPages.Add(modelsTab);
            otherTab.Controls.Add(other);
            tabs.TabPages.Add(otherTab);
            AddBuildButton(titleTab);
            AddBuildButton(licenseTab);
            AddBuildButton(singleTab);
            AddBuildButton(multiTab);
            _outputFolder.TextChanged += delegate
            {
                UpdateBuildButtons();
                foreach (var editor in _modelEditors.Values) editor.RefreshSharedOutput();
            };
            Activated += delegate
            {
                UpdateBuildButtons();
            };
            UpdateBuildButtons();
            tabs.SelectedIndex = 0;
            Shown += delegate
            {
                tabs.SelectedIndex = 0;
            };
            _progress = new StudioProgressBar(); ToolStatus.Register(this, _progress);
            _progress.Dock = DockStyle.Fill;
            _progress.Minimum = 0;
            _progress.Maximum = 100;
            _progress.Margin = new Padding(0, 0, 0, 0);
            root.Controls.Add(_progress, 0, 4);
            _status = new Label();
            _status.AutoSize = true;
            _status.Margin = new Padding(0, 8, 0, 6);
            _status.ForeColor = Color.FromArgb(215, 220, 232);
            _status.Text = L.T("Bereit.", "Ready.");
            root.Controls.Add(_status, 0, 3);
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.Padding = new Padding(0, 7, 0, 4);
            buttons.BackColor = Color.Transparent;
            var exportRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            exportRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            exportRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            exportRow.Controls.Add(buttons, 0, 0);
            root.Controls.Add(exportRow, 0, 2);
            _close = NewButton(L.T("Schliessen", "Close"));
            _close.Width = 130;
            _close.Margin = new Padding(3, 10, 3, 4);
            _close.Click += delegate
            {
                Close();
            };
            exportRow.Controls.Add(_close, 1, 0);
            foreach (var pair in _exports)
            {
                var button = pair.Value;
                var previous = button.Parent;
                previous.Controls.Remove(button);
                if (previous is FlowLayoutPanel && previous.Controls.Count == 0)
                {
                    previous.Parent.Controls.Remove(previous);
                    previous.Dispose();
                }

                button.Dock = DockStyle.None;
                button.AutoSize = true;
                button.MinimumSize = new Size(260, 36);
                buttons.Controls.Add(button);
                buttons.Controls.SetChildIndex(button, 0);
            }

            tabs.SelectedIndexChanged += delegate
            {
                ResetTabProgress();
                RefreshExportLocation();
            };
            other.SelectedIndexChanged += delegate
            {
                ResetTabProgress();
                RefreshExportLocation();
            };
            RefreshExportLocation();
        }

        private void ResetTabProgress()
        {
            if (_busy)
                return;
            _progress.Value = 0;
            _status.Text = L.T("Bereit.", "Ready.");
        }

        private void RefreshExportLocation()
        {
            foreach (var pair in _exports)
            {
                bool active = true;
                for (Control node = pair.Key; node != null; node = node.Parent)
                {
                    var page = node as TabPage;
                    if (page != null && page.Parent is TabControl && ((TabControl)page.Parent).SelectedTab != page)
                        active = false;
                }

                pair.Value.Visible = active;
                var editor = pair.Key.Controls.OfType<MenuModelsForm>().FirstOrDefault();
                if (active && editor != null) editor.RefreshSharedOutput();
            }
        }

        private string ResolveOutputFolder()
        {
            if (String.IsNullOrWhiteSpace(_outputFolder.Text))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MUR_EDITED");
            string output = Path.GetFullPath(_outputFolder.Text.Trim());
            string source = String.IsNullOrWhiteSpace(_uiFolder.Text) ? "" : Path.GetFullPath(_uiFolder.Text.Trim());
            if (String.Equals(output.TrimEnd(Path.DirectorySeparatorChar), source.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(source, "MUR_EDITED");
            _outputFolder.Text = output;
            return output;
        }

        private void AddModelsPage(TabPage tab, string archiveName)
        {
            tab.AutoScroll = false;
            tab.Padding = new Padding(0);
            MenuModelsForm editor = new MenuModelsForm(archiveName, true);
            _modelEditors.Add(archiveName, editor);
            editor.TopLevel = false;
            editor.FormBorderStyle = FormBorderStyle.None;
            editor.MinimumSize = Size.Empty;
            editor.Dock = DockStyle.Fill;
            editor.UseOutputFolder(ResolveOutputFolder, delegate(string path) { _outputFolder.Text = path; });
            tab.Controls.Add(editor);
            editor.Show();
            _exports.Add(tab, editor.ExternalBuildButton());
        }

        private void AddLoadingTab(TabControl tabs)
        {
            TabPage page = NewTab(L.T("Wartefenster", "Waiting screens"));
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            int row = 0;
            TextBox archive, picture;
            var purpose = NewLabel(L.T("Archiv des Wartefensters wählen: Title.szs für Titel-/Lizenzmenüs, Globe.szs für Online-Menüs. Gemeinsam verwendete Dialoge werden ebenfalls geändert.", "Choose the archive used by the waiting screen: Title.szs for title/license menus, Globe.szs for online menus. Changes affect dialogs sharing this layout."));
            purpose.AutoSize = true;
            purpose.MaximumSize = new Size(980, 0);
            purpose.Margin = new Padding(8, 8, 8, 18);
            grid.Controls.Add(purpose, 0, row++);
            grid.SetColumnSpan(purpose, 3);
            AddFileRow(grid, ref row, L.T("Datei öffnen", "Open file") + ": Title.szs / Globe.szs", out archive, L.T("Aus deinem Custom Pack. Diese Kopie behält deine bisherigen Online-Menüänderungen.", "From your custom pack. This copy preserves your existing online menu edits."), "Menu archives|Globe.szs;Title.szs;MenuSingle.szs;MenuMulti.szs;Channel.szs");
            AddFileRow(grid, ref row, L.T("Hintergrundbild", "Background picture") + "\n" + L.T("z. B. hintergrund.png", "e.g. background.png"), out picture, L.T("Zum Beispiel wallpaper.png. Statisch; bei GIF wird das erste Bild verwendet. Betrifft auch andere Online-Dialoge mit demselben Nachrichtenfenster.", "For example wallpaper.png. Static; GIF uses its first frame. Also affects other online dialogs sharing this message window."), "Pictures|*.png;*.jpg;*.jpeg;*.gif");
            var preview = new PictureBox
            {
                Height = 125,
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            grid.Controls.Add(preview, 1, row++);
            var create = NewButton(L.T("Kopien für diesen Tab erstellen", "Create copies for this tab"));
            create.AutoSize = true;
            create.MinimumSize = new Size(260, 34);
            create.Enabled = false;
            StylePrimaryButton(create);
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 7, 0, 0)
            };
            footer.Controls.Add(create);
            _exports.Add(page, create);
            var info = NewLabel(L.T("Ausgabe nach Ordner & Qualität; keine Änderung am Quellarchiv.", "Output uses Folders & quality; source archive remains unchanged."));
            info.MaximumSize = new Size(720, 0);
            grid.Controls.Add(info, 1, row++);
            grid.SetColumnSpan(info, 2);
            var clear = new SelectionClearButton
            {
                Text = L.T("Bildauswahl leeren", "Clear picture selection"),
                Enabled = false
            };
            clear.Click += delegate
            {
                picture.Clear();
            };
            grid.Controls.Add(clear, 1, row++);
            EventHandler update = delegate
            {
                create.Enabled = File.Exists(archive.Text) && File.Exists(picture.Text);
                clear.Enabled = picture.Text.Length > 0;
            };
            archive.TextChanged += update;
            picture.TextChanged += update;
            picture.TextChanged += delegate
            {
                if (preview.Image != null)
                    preview.Image.Dispose();
                preview.Image = null;
                try
                {
                    using (var image = Image.FromFile(picture.Text))
                        preview.Image = new Bitmap(image);
                }
                catch
                {
                }
            };
            preview.Disposed += delegate
            {
                if (preview.Image != null)
                    preview.Image.Dispose();
            };
            _uiFolder.TextChanged += delegate
            {
                archive.Text = "";
                foreach (string folder in new[]
                {
                    _uiFolder.Text,
                    Path.Combine(_uiFolder.Text, "Scene", "UI"),
                    Path.Combine(_uiFolder.Text, "UI")
                }

                )
                {
                    string file = Path.Combine(folder, "Globe.szs");
                    if (File.Exists(file))
                    {
                        archive.Text = file;
                        break;
                    }
                }
            };
            create.Click += delegate
            {
                try
                {
                    string output = Path.Combine(ResolveOutputFolder(), Path.GetFileName(archive.Text));
                    if (String.Equals(output, Path.GetFullPath(archive.Text), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(L.T("Wähle einen separaten Ausgabeordner.", "Choose a separate output folder."));
                    create.Enabled = false;
                    UseWaitCursor = true;
                    byte[] data = OnlineLoadingBackground.Build(File.ReadAllBytes(archive.Text), picture.Text);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    BackupManager.WriteAllBytesSafely(output, data);
                    _status.Text = L.T("Kopie erstellt: ", "Copy created: ") + output;
                    ExportHelp.Show(this, Path.GetDirectoryName(output));
                }
                catch (Exception ex)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    UseWaitCursor = false;
                    update(null, EventArgs.Empty);
                }
            };
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            content.Controls.Add(grid);
            page.AutoScroll = false;
            page.Controls.Add(content);
            page.Controls.Add(footer);
            tabs.TabPages.Add(page);
        }

        private static void AddInfoTab(TabControl tabs, string name, string heading, string description)
        {
            TabPage page = NewTab(name);
            Label body = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                Text = heading + "\r\n\r\n" + description,
                Font = new Font("Segoe UI", 12F)
            };
            page.Controls.Add(body);
            tabs.TabPages.Add(page);
        }

        private Control BuildHeaderPanel()
        {
            return StudioChrome.Header("MKWii Backgrounds Tool", L.T("Wähle deinen Custom-Pack-Ordner. Passe Bilder und Modelle an und erstelle eine Kopie.", "Choose your custom mod folder. Edit pictures and models, then create a copy."));
        }

        private static void StylePrimaryButton(Button b)
        {
            if (b == null)
                return;
            b.BackColor = DarkTheme.Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = DarkTheme.Accent2;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(157, 112, 255);
            b.FlatAppearance.MouseDownBackColor = DarkTheme.Accent2;
        }

        private static TabPage NewTab(string text)
        {
            TabPage page = new TabPage(text);
            page.BackColor = DarkTheme.Back;
            page.ForeColor = DarkTheme.Fore;
            page.AutoScroll = true;
            page.Padding = new Padding(12);
            return page;
        }

        private void AddTargetGroup(TabPage tab, string key, string displayName, string archiveBaseName, bool isTitle, string capabilityText)
        {
            GroupBox group = new GroupBox();
            group.Text = displayName;
            group.Dock = DockStyle.Top;
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.Padding = new Padding(12, 8, 12, 12);
            group.Margin = new Padding(0, 0, 0, 12);
            group.BackColor = DarkTheme.Panel;
            group.ForeColor = Color.White;
            group.FlatStyle = FlatStyle.Flat;
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.AutoSize = true;
            grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grid.ColumnCount = 3;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            group.Controls.Add(grid);
            TargetEditor target = new TargetEditor();
            target.Key = key;
            target.DisplayName = displayName;
            target.ArchiveBaseName = archiveBaseName;
            target.IsTitle = isTitle;
            target.Tab = tab;
            int row = 0;
            AddFileRow(grid, ref row, L.T("Basis-Menüarchiv", "Base menu archive") + "\n" + archiveBaseName + ".szs", out target.CommonPath, L.T("Beispiel: ", "Example: ") + archiveBaseName + L.T(".szs aus deinem Custom Pack. Wird über den Mod-Ordner erkannt.", ".szs from your custom pack. Detected from the mod folder."), "SZS (*.szs)|*.szs|All files|*.*");
            TableLayoutPanel languageFields = new TableLayoutPanel();
            languageFields.AutoSize = true;
            languageFields.Dock = DockStyle.Top;
            languageFields.ColumnCount = 3;
            languageFields.Margin = new Padding(0);
            languageFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            languageFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            languageFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            int languageRow = 0;
            AddFileRow(languageFields, ref languageRow, L.T("Zusätzliches Menüarchiv", "Additional menu archive") + "\n" + L.T("z. B. ", "e.g. ") + archiveBaseName + "_E.szs", out target.LanguagePath, isTitle ? L.T("Für den Titelbildschirm erforderlich. Im kopierten Custom Pack: Title_E.szs; in der RR-UI-Referenz: Title_U.szs. Passend zur Spielsprache auswählen.", "Required for the title screen. Copied custom pack: Title_E.szs; RR UI reference: Title_U.szs. Select the file matching your game language.") : L.T("Dieses zusätzliche Archiv wurde im gewählten Ordner für deine Spielsprache gefunden. Es wird zusammen mit dem Basisarchiv verarbeitet.", "This additional archive was found in the selected folder for your game language. It is processed together with the base archive."), "SZS (*.szs)|*.szs|All files|*.*");
            target.LanguageFields = languageFields;
            languageFields.Visible = isTitle;
            grid.SetColumnSpan(languageFields, 3);
            grid.Controls.Add(languageFields, 0, row++);
            target.ArchiveStatus = new Label();
            target.ArchiveStatus.AutoSize = true;
            target.ArchiveStatus.MaximumSize = new Size(700, 0);
            target.ArchiveStatus.ForeColor = DarkTheme.Muted;
            grid.SetColumnSpan(target.ArchiveStatus, 2);
            grid.Controls.Add(target.ArchiveStatus, 1, row++);
            AddFileRow(grid, ref row, L.T("Dein Bild oder GIF", "Your picture or GIF") + "\n" + L.T("z. B. hintergrund.png / .gif", "e.g. background.png / .gif"), out target.SourcePath, L.T("Beispiel: urlaub.jpg oder hintergrund.gif. PNG/JPG bleiben statisch; GIFs werden in unterstützten Menüs animiert. Ohne Bild wird dieser Bereich ausgelassen.", "Example: holiday.jpg or background.gif. PNG/JPG stay static; GIFs animate in supported menus. Areas without a picture are skipped."), "Background images (*.gif;*.png;*.jpg;*.jpeg)|*.gif;*.png;*.jpg;*.jpeg|GIF (*.gif)|*.gif|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|All files|*.*");
            if (isTitle)
            {
                target.UseFirstFrameForRelatedScreens = NewCheck(L.T("Erstes Bild zusätzlich für Lizenzwahl und Hauptmenü verwenden", "Also use the first image for license selection and the main menu"), true);
                grid.SetColumnSpan(target.UseFirstFrameForRelatedScreens, 2);
                grid.Controls.Add(target.UseFirstFrameForRelatedScreens, 1, row++);
            }

            target.Capability = new Label();
            target.Capability.AutoSize = true;
            target.Capability.MaximumSize = new Size(830, 0);
            target.Capability.ForeColor = Color.FromArgb(180, 190, 210);
            target.Capability.Margin = new Padding(0, 6, 0, 8);
            target.Capability.Text = capabilityText;
            if (!isTitle && archiveBaseName != "MenuSingle" && archiveBaseName != "MenuMulti" && archiveBaseName != "Title")
            {
                target.Capability.Text += L.T("\r\nFür diesen Bereich ist kein geprüfter Bildimport verfügbar. Es werden keine Menürahmen als Hintergrund ersetzt.", "\r\nNo verified picture import is available for this area. Menu borders will not be replaced as backgrounds.");
                target.SourcePath.Enabled = false;
                foreach (Control control in grid.Controls)
                    if (control is Button && grid.GetRow(control) == grid.GetRow(target.SourcePath))
                        control.Enabled = false;
            }

            grid.SetColumnSpan(target.Capability, 2);
            grid.Controls.Add(target.Capability, 1, row++);
            _targets.Add(target);
            Button clearSource = new SelectionClearButton
            {
                Text = L.T("Bildauswahl leeren", "Clear picture selection")
            };
            clearSource.AutoSize = false;
            clearSource.Enabled = false;
            clearSource.Click += delegate
            {
                target.SourcePath.Clear();
                _status.Text = L.T("Bildauswahl geleert. Bereits erstellte Dateien bleiben unverändert. Kein RR-Reset.", "Picture selection cleared. Previously created files are unchanged. This does not restore RR defaults.");
            };
            grid.Controls.Add(clearSource, 1, row++);
            target.CommonPath.TextChanged += delegate
            {
                UpdateBuildButtons();
            };
            target.LanguagePath.TextChanged += delegate
            {
                UpdateBuildButtons();
            };
            target.SourcePath.TextChanged += delegate
            {
                clearSource.Enabled = !String.IsNullOrWhiteSpace(target.SourcePath.Text);
                UpdateBuildButtons();
            };
            Label pathWarning = new Label();
            pathWarning.AutoSize = true;
            pathWarning.ForeColor = Color.Orange;
            pathWarning.MaximumSize = new Size(700, 0);
            grid.SetColumnSpan(pathWarning, 2);
            grid.Controls.Add(pathWarning, 1, row++);
            EventHandler validatePaths = delegate
            {
                bool same = false;
                try
                {
                    same = target.LanguagePath.Text.Trim().Length > 0 && String.Equals(Path.GetFullPath(target.CommonPath.Text.Trim()), Path.GetFullPath(target.LanguagePath.Text.Trim()), StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                }

                pathWarning.Text = same ? L.T("Hier ist zweimal dasselbe Archiv gewählt. Unten gehört die Variante mit _E, _G usw. hinein; bei optionalen Archiven darf das Feld leer bleiben.", "The same archive is selected twice. The second field needs the _E, _G etc. variant; optional archives may be left empty.") : "";
            };
            target.CommonPath.TextChanged += validatePaths;
            target.LanguagePath.TextChanged += validatePaths;
            PictureBox preview = new PictureBox();
            preview.Height = 110;
            preview.Dock = DockStyle.Fill;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            grid.SetColumnSpan(preview, 2);
            grid.Controls.Add(preview, 1, row++);
            target.SourcePath.TextChanged += delegate
            {
                Image old = preview.Image;
                preview.Image = null;
                if (old != null)
                    old.Dispose();
                string source = target.SourcePath.Text.Trim();
                if (!File.Exists(source))
                    return;
                try
                {
                    using (Image image = Image.FromFile(source))
                        preview.Image = new Bitmap(image);
                }
                catch
                {
                }
            };
            preview.Disposed += delegate
            {
                if (preview.Image != null)
                {
                    preview.Image.Dispose();
                    preview.Image = null;
                }
            };
            tab.Controls.Add(group);
            group.BringToFront();
        }

        private void BuildSettingsTab(TabPage tab)
        {
            GroupBox card = new GroupBox();
            card.Text = L.T("Build- und Qualitätsoptionen", "Build and quality options");
            card.Dock = DockStyle.Top;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Padding = new Padding(12, 8, 12, 12);
            card.Margin = new Padding(0, 0, 0, 12);
            card.BackColor = DarkTheme.Panel;
            card.ForeColor = Color.White;
            card.FlatStyle = FlatStyle.Flat;
            tab.Controls.Add(card);
            TableLayoutPanel form = new TableLayoutPanel();
            form.Dock = DockStyle.Top;
            form.AutoSize = true;
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            form.ColumnCount = 3;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            card.Controls.Add(form);
            int row = 0;
            form.Controls.Add(NewLabel(L.T("Dein Custom-Mod-Ordner", "Your custom mod folder")), 0, row);
            _uiFolder = new TextBox();
            _uiFolder.Dock = DockStyle.Fill;
            StylePathBox(_uiFolder);
            _uiFolder.Leave += delegate
            {
                if (Directory.Exists(_uiFolder.Text.Trim()))
                    DetectArchivesInFolder(_uiFolder.Text.Trim());
            };
            form.Controls.Add(_uiFolder, 1, row);
            Button detect = NewButton(L.T("Auswählen", "Browse"));
            detect.Click += delegate
            {
                BrowseAndDetectUiFolder();
            };
            form.Controls.Add(detect, 2, row++);
            AddExample(form, ref row, L.T("Ordner mit Title.szs und MenuSingle.szs; auch Scene/UI wird erkannt.", "Folder containing Title.szs and MenuSingle.szs; Scene/UI is also supported."));
            form.Controls.Add(NewLabel(L.T("Ausgabeordner", "Output folder")), 0, row);
            _outputFolder = new TextBox();
            _outputFolder.Dock = DockStyle.Fill;
            StylePathBox(_outputFolder);
            form.Controls.Add(_outputFolder, 1, row);
            Button browseOut = NewButton(L.T("Auswählen", "Browse"));
            browseOut.Click += delegate
            {
                BrowseOutputFolder();
            };
            form.Controls.Add(browseOut, 2, row++);
            AddExample(form, ref row, L.T("Alle Tabs speichern hier mit Originalnamen. Standard: MUR_EDITED in deinem Pack. Danach die Dateien ins Pack kopieren.", "All tabs save to this folder with the original filenames. Default: MUR_EDITED inside your pack. Copy its files into the pack when ready."));
            form.Controls.Add(NewLabel(L.T("Sprache im Spiel", "In-game language")), 0, row);
            _language = new ComboBox();
            _language.Dock = DockStyle.Fill;
            _language.DropDownStyle = ComboBoxStyle.DropDownList;
            _language.Items.AddRange(new object[] { "Deutsch (G)", "English / PAL (E)", "English / USA (U)", "Français (F)", "Español (S)", "Italiano (I)", "Japanese (J)", "Español / USA (M)", "Français / Canada (Q)", "Korean (K)" });
            _language.SelectedIndex = L.Current == UiLanguage.German ? 0 : 1;
            _language.SelectedIndexChanged += delegate
            {
                DetectArchivesInFolder(_uiFolder.Text);
                ResetPathViews();
            };
            form.SetColumnSpan(_language, 2);
            form.Controls.Add(_language, 1, row++);
            AddExample(form, ref row, L.T("Beispiel: Title_E.szs → English / PAL (E). Wähle die Sprache, die du im Spiel verwendest; die App-Sprache ist unabhängig davon.", "Example: Title_E.szs → English / PAL (E). Choose the language used in your game; the app language is independent."));
            form.Controls.Add(NewLabel(L.T("Animationsqualität", "Animation quality")), 0, row);
            _quality = new ComboBox();
            _quality.Dock = DockStyle.Fill;
            _quality.DropDownStyle = ComboBoxStyle.DropDownList;
            _quality.DropDownWidth = 700;
            _quality.Items.Add(L.T("Sicher / Wii-freundlich — CMPR, jedes 2. Frame (empfohlen)", "Safe / Wii-friendly — CMPR, every 2nd frame (recommended)"));
            _quality.Items.Add(L.T("Flüssig — CMPR, jedes Frame", "Smooth — CMPR, every frame"));
            _quality.Items.Add(L.T("Hohe Qualität / schwer — RGB565, jedes 2. Frame", "High quality / heavy — RGB565, every 2nd frame"));
            _quality.Items.Add(L.T("Maximale Qualität / sehr schwer — RGB565, jedes Frame", "Maximum quality / very heavy — RGB565, every frame"));
            _quality.SelectedIndex = 0;
            form.SetColumnSpan(_quality, 2);
            form.Controls.Add(_quality, 1, row++);
            AddExample(form, ref row, L.T("Beispiel: Für ein GIF zuerst die empfohlene Einstellung testen. Jedes Frame macht Animationen flüssiger, benötigt aber mehr Speicher.", "Example: start with the recommended setting for a GIF. Every frame makes animation smoother but uses more memory."));
            form.Controls.Add(NewLabel(L.T("Bild anpassen", "Picture fitting")), 0, row);
            _menuFraming = new ComboBox();
            _menuFraming.Dock = DockStyle.Fill;
            _menuFraming.DropDownStyle = ComboBoxStyle.DropDownList;
            _menuFraming.Items.Add(L.T("Automatisch — füllend zuschneiden (Titel nutzt sein Layout)", "Automatic — crop to fill (title uses its layout mapping)"));
            _menuFraming.Items.Add(L.T("Strecken — Quelle auf Zieltextur strecken", "Stretch — stretch source to target texture"));
            _menuFraming.Items.Add(L.T("Fill / Crop — Seitenverhältnis behalten und zuschneiden", "Fill / Crop — preserve aspect ratio and crop"));
            _menuFraming.SelectedIndex = 0;
            form.SetColumnSpan(_menuFraming, 2);
            form.Controls.Add(_menuFraming, 1, row++);
            AddExample(form, ref row, L.T("Beispiel: Ein Hochkantfoto wird beim Zuschneiden an den Rändern gekürzt. Strecken zeigt das ganze Bild, kann es aber verzerren.", "Example: a portrait photo loses its edges when cropped. Stretch shows the whole picture but may distort it."));
            _cleanupOld = NewCheck(L.T("Alte von murums erzeugte Background-TPL-Serien vor dem Build bereinigen", "Clean old murums-generated background TPL sequences before build"), true);
            form.SetColumnSpan(_cleanupOld, 2);
            form.Controls.Add(_cleanupOld, 1, row++);
            AddExample(form, ref row, L.T("Beispiel: Beim Ersetzen eines früheren GIFs werden alte, von murums erzeugte Animationsbilder bereinigt.", "Example: when replacing an earlier GIF, old animation images generated by murums are cleaned up."));
            Label hint = new Label();
            hint.AutoSize = true;
            hint.MaximumSize = new Size(850, 0);
            hint.ForeColor = Color.FromArgb(180, 190, 210);
            hint.Margin = new Padding(0, 12, 0, 8);
            hint.Text = L.T("Ordner-Erkennung akzeptiert direkt Scene/UI oder einen Mod-Ordner mit Scene/UI und sucht Common-/Sprachpaare wie Title.szs / Title_E.szs ; weitere Archive werden nur angezeigt, wenn sie tatsächlich gefunden wurden. Sprachsuffixe werden nicht auf E festgelegt. Bereiche ohne ausgewähltes Bild werden übersprungen.", "Folder detection accepts Scene/UI directly or a mod folder containing Scene/UI and finds common/language pairs such as Title.szs / Title_E.szs ; other additional archives are shown only when actually found. Language suffixes are not hard-coded to E. Areas without a picture are skipped.");
            form.SetColumnSpan(hint, 2);
            form.Controls.Add(hint, 1, row++);
        }

        private void AddFileRow(TableLayoutPanel form, ref int row, string label, out TextBox box, string cue, string filter)
        {

            var caption = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 1,
                Margin = new Padding(0)
            };
            string[] lines = label.Split('\n');
            caption.Controls.Add(NewLabel(lines[0]));
            Label fileHint = null;
            if (lines.Length > 1)
            {
                fileHint = NewLabel((filter.Contains(".szs") ? L.T("Datei: ", "File: ") : "") + lines[1]);
                fileHint.Font = new Font("Segoe UI", 9F);
                fileHint.Margin = new Padding(0, 0, 8, 6);
                caption.Controls.Add(fileHint);
            }
            form.Controls.Add(caption, 0, row);
            box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.Tag = cue;
            StylePathBox(box);
            form.Controls.Add(box, 1, row);
            TextBox captured = box;
            Label fieldLabel = fileHint;
            if (fieldLabel != null && label.Contains(".szs"))
            {
                string defaultHint = L.T("Datei: ", "File: ") + lines[1];
                captured.TextChanged += delegate
                {
                    string fileName = Path.GetFileName(captured.Text);
                    fieldLabel.Text = String.IsNullOrWhiteSpace(fileName) ? defaultHint : L.T("Datei: ", "File: ") + fileName;
                };
            }
            Button button = NewButton(L.T("Auswählen", "Browse"));
            button.Click += delegate
            {
                BrowseFile(captured, filter);
            };
            form.Controls.Add(button, 2, row++);
            AddExample(form, ref row, cue);
        }

        private static void AddExample(TableLayoutPanel form, ref int row, string text)
        {
            Label hint = new Label();
            hint.AutoSize = true;
            hint.Dock = DockStyle.Fill;
            hint.MaximumSize = new Size(700, 0);
            hint.ForeColor = DarkTheme.Muted;
            hint.Margin = new Padding(0, 0, 0, 10);
            hint.Text = text;
            form.SetColumnSpan(hint, 2);
            form.Controls.Add(hint, 1, row++);
        }

        private void AutoDetectPaths(string currentArchivePath)
        {
            string dir = null;
            try
            {
                if (!String.IsNullOrWhiteSpace(currentArchivePath))
                {
                    string full = Path.GetFullPath(currentArchivePath);
                    dir = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
                }
            }
            catch
            {
            }

            if (String.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _uiFolder.Text = dir;
            DetectArchivesInFolder(dir);
            if (String.IsNullOrWhiteSpace(_outputFolder.Text))
                _outputFolder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MUR_EDITED");
            ResetPathViews();
        }

        private void DetectArchivesInFolder(string folder)
        {
            string previousDefault = _lastDefaultOutput;
            bool useDefault = String.IsNullOrWhiteSpace(_outputFolder.Text) || String.Equals(_outputFolder.Text, previousDefault, StringComparison.OrdinalIgnoreCase);
            folder = ResolveUiFolder(folder);
            if (String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;
            _uiFolder.Text = folder;
            int i;
            for (i = 0; i < _targets.Count; i++)
            {
                TargetEditor target = _targets[i];
                string common = FindCommonArchive(folder, target.ArchiveBaseName);
                string language = FindLanguageArchive(folder, target.ArchiveBaseName);
                target.CommonPath.Text = !String.IsNullOrWhiteSpace(common) ? common : "";
                target.LanguagePath.Text = !String.IsNullOrWhiteSpace(language) ? language : "";
                target.LanguageFields.Visible = target.IsTitle || !String.IsNullOrWhiteSpace(language);
                target.ArchiveStatus.Text = String.IsNullOrWhiteSpace(common) ? L.T("Im gewählten Ordner nicht vorhanden: ", "Not found in the selected folder: ") + target.ArchiveBaseName + ".szs" : L.T("Gefunden: ", "Found: ") + Path.GetFileName(common) + (String.IsNullOrWhiteSpace(language) ? (target.IsTitle ? L.T(". Das zusätzliche Titelarchiv für die gewählte Spielsprache fehlt.", ". The additional title archive for the selected game language is missing.") : L.T(". Kein zusätzliches Archiv für die gewählte Spielsprache gefunden oder nötig.", ". No additional archive for the selected game language was found or is required.")) : " + " + Path.GetFileName(language));
            }

            _lastDefaultOutput = Path.Combine(folder, "MUR_EDITED");
            if (useDefault)
                _outputFolder.Text = _lastDefaultOutput;
            ResetPathViews();
        }

        private static string ResolveUiFolder(string folder)
        {
            if (String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return folder;
            try
            {
                if (LooksLikeUiFolder(folder))
                    return folder;
                string sceneUi = Path.Combine(folder, "Scene", "UI");
                if (Directory.Exists(sceneUi) && LooksLikeUiFolder(sceneUi))
                    return sceneUi;
                string ui = Path.Combine(folder, "UI");
                if (Directory.Exists(ui) && LooksLikeUiFolder(ui))
                    return ui;
            }
            catch
            {
            }

            return folder;
        }

        private static bool LooksLikeUiFolder(string folder)
        {
            string[] known = new string[]
            {
                "Title",
                "MenuSingle",
                "MenuMulti",
                "Globe",
                "MenuOther",
                "Channel",
                "Present",
                "Award",
                "Event",
                "Race"
            };
            int i;
            for (i = 0; i < known.Length; i++)
            {
                if (File.Exists(Path.Combine(folder, known[i] + ".szs")) || File.Exists(Path.Combine(folder, known[i] + "_R.szs")))
                    return true;
            }

            return false;
        }

        private static string FindCommonArchive(string folder, string baseName)
        {
            try
            {
                string common = Path.Combine(folder, baseName + ".szs");
                if (File.Exists(common))
                    return common;
                string koreanDefault = Path.Combine(folder, baseName + "_R.szs");
                if (File.Exists(koreanDefault))
                    return koreanDefault;
            }
            catch
            {
            }

            return "";
        }

        private string FindLanguageArchive(string folder, string baseName)
        {
            try
            {
                string suffix = _languageSuffixes[Math.Max(0, _language.SelectedIndex)];
                string p = Path.Combine(folder, baseName + "_" + suffix + ".szs");
                if (File.Exists(p))
                    return p;
            }
            catch
            {
            }

            return "";
        }

        private void BrowseAndDetectUiFolder()
        {
            using (murumsWiiModStudio.FolderPickerDialog d = new murumsWiiModStudio.FolderPickerDialog())
            {
                d.Description = L.T("Mario Kart Wii / Retro Rewind Scene/UI-Ordner auswählen", "Choose the Mario Kart Wii / Retro Rewind Scene/UI folder");
                if (Directory.Exists(_uiFolder.Text))
                    d.SelectedPath = _uiFolder.Text;
                if (d.ShowDialog(this) == DialogResult.OK)
                    DetectArchivesInFolder(d.SelectedPath);
            }
        }

        private static bool IsReadableArchive(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                U8Archive.Load(File.ReadAllBytes(path));
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        private void BrowseMenuSource()
        {
            string path = GameArchiveImportForm.Select(this, "Menu / model archives|*.szs;*.arc", null, _outputFolder.Text);
            if (path == null)
                return;
            try
            {
                LoadSourceFile(path);
            }
            catch (Exception error)
            {
                StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void LoadSourceFile(string path)
        {
            string fileName = Path.GetFileName(path);
            string modelName = String.Equals(fileName, "globe.arc", StringComparison.OrdinalIgnoreCase) ? "Earth.szs" : fileName;
            MenuModelsForm editor;
            if (_modelEditors.TryGetValue(modelName, out editor))
            {
                if (!String.Equals(fileName, "globe.arc", StringComparison.OrdinalIgnoreCase))
                    MenuModelSource.Validate(path, _modelEditors.Keys.First(key => String.Equals(key, modelName, StringComparison.OrdinalIgnoreCase)));
                editor.AddArchives(new[] { path });
                if (!editor.HasLoadedArchive)
                {
                    _status.Text = L.T("globe.arc ausgewählt. Öffne zusätzlich Earth.szs, um Globus und Himmel zu bearbeiten.",
                        "globe.arc selected. Open Earth.szs as well to edit the globe and sky.");
                    return;
                }
                PackSelection.SourceLoaded(this);
                for (Control control = editor.Parent; control != null; control = control.Parent)
                {
                    var page = control as TabPage;
                    if (page != null && page.Parent is TabControl)
                        ((TabControl)page.Parent).SelectedTab = page;
                }
                return;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            TargetEditor target = _targets.FirstOrDefault(t => String.Equals(name, t.ArchiveBaseName, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(t.ArchiveBaseName + "_", StringComparison.OrdinalIgnoreCase));
            if (target == null)
                throw new InvalidDataException(L.T("Wähle ein unterstütztes Menü- oder Modellarchiv.", "Choose a supported menu or model archive."));
            U8Archive.Load(File.ReadAllBytes(path));
            if (String.Equals(name, target.ArchiveBaseName, StringComparison.OrdinalIgnoreCase))
                target.CommonPath.Text = path;
            else
            {
                target.LanguagePath.Text = path;
                target.LanguageFields.Visible = true;
            }
            PackSelection.SourceLoaded(this);
            ResetPathViews();
        }
        private void BrowseOutputFolder()
        {
            using (murumsWiiModStudio.FolderPickerDialog d = new murumsWiiModStudio.FolderPickerDialog())
            {
                d.Description = L.T("Ausgabeordner für die gepatchten UI-Archive", "Output folder for the patched UI archives");
                if (Directory.Exists(_outputFolder.Text))
                    d.SelectedPath = _outputFolder.Text;
                if (d.ShowDialog(this) == DialogResult.OK)
                    _outputFolder.Text = d.SelectedPath;
            }
        }

        private void BrowseFile(TextBox target, string filter)
        {
            if (filter.Contains(".szs"))
            {
                string preferred = String.IsNullOrWhiteSpace(target.Text) ? null : Path.GetFileName(target.Text);
                string imported = GameArchiveImportForm.Select(this, filter, preferred, _outputFolder.Text);
                if (imported != null)
                {
                    target.Text = imported;
                    ResetPathView(target);
                }
                return;
            }
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = filter;
                if (!String.IsNullOrWhiteSpace(target.Text))
                {
                    try
                    {
                        d.InitialDirectory = Path.GetDirectoryName(target.Text);
                        d.FileName = Path.GetFileName(target.Text);
                    }
                    catch
                    {
                    }
                }

                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = d.FileName;
                    ResetPathView(target);
                }
            }
        }

        private static bool IsSupportedBackgroundSource(string path)
        {
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".gif" || ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private void AddBuildButton(TabPage tab)
        {
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.AutoScroll = true;
            while (tab.Controls.Count > 0)
            {
                Control child = tab.Controls[0];
                tab.Controls.Remove(child);
                content.Controls.Add(child);
            }

            tab.AutoScroll = false;
            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 48;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.Padding = new Padding(0, 7, 0, 0);
            Button build = NewButton(L.T("Kopien für diesen Tab erstellen", "Create copies for this tab"));
            build.AutoSize = true;
            build.MinimumSize = new Size(260, 34);
            StylePrimaryButton(build);
            build.Click += delegate
            {
                BuildBackgrounds(tab);
            };
            footer.Controls.Add(build);
            tab.Controls.Add(content);
            tab.Controls.Add(footer);
            _buildButtons.Add(tab, build);
            _exports.Add(tab, build);
        }

        private bool CanBuildTab(TabPage tab)
        {
            if (_busy || _outputFolder == null || String.IsNullOrWhiteSpace(_outputFolder.Text))
                return false;
            bool any = false;
            foreach (TargetEditor target in _targets)
            {
                if (target.Tab != tab || String.IsNullOrWhiteSpace(target.SourcePath.Text))
                    continue;
                if (!target.IsTitle && target.ArchiveBaseName != "MenuSingle" && target.ArchiveBaseName != "MenuMulti" && target.ArchiveBaseName != "Title")
                    return false;
                any = true;
                string common = target.CommonPath.Text.Trim();
                string language = target.LanguagePath.Text.Trim();
                string source = target.SourcePath.Text.Trim();
                if (!File.Exists(common) || !File.Exists(source) || !IsSupportedBackgroundSource(source))
                    return false;
                if ((target.IsTitle || language.Length > 0) && !File.Exists(language))
                    return false;
                try
                {
                    if (language.Length > 0 && String.Equals(Path.GetFullPath(common), Path.GetFullPath(language), StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            return any;
        }

        private void UpdateBuildButtons()
        {
            foreach (KeyValuePair<TabPage, Button> item in _buildButtons)
                item.Value.Enabled = CanBuildTab(item.Key);
        }

        private void BuildBackgrounds(TabPage tab)
        {
            string output = ResolveOutputFolder();
            if (_busy)
                return;
            if (String.IsNullOrWhiteSpace(output))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle einen Ausgabeordner.", "Choose an output folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<TargetEditor> enabled = new List<TargetEditor>();
            int i;
            for (i = 0; i < _targets.Count; i++)
            {
                TargetEditor t = _targets[i];
                if (t.Tab != tab || String.IsNullOrWhiteSpace(t.SourcePath.Text))
                    continue;
                if (!File.Exists((t.CommonPath.Text ?? "").Trim()))
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Für {0} fehlt das Common-Archiv.", "The common archive for {0} is missing.", t.DisplayName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!File.Exists((t.SourcePath.Text ?? "").Trim()) || !IsSupportedBackgroundSource(t.SourcePath.Text))
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Für {0} fehlt eine gültige GIF/PNG/JPG/JPEG-Quelle.", "A valid GIF/PNG/JPG/JPEG source is missing for {0}.", t.DisplayName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (t.IsTitle && !File.Exists((t.LanguagePath.Text ?? "").Trim()))
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Die Titel-Sprachdatei fehlt. Prüfe unter „Ordner & Qualität“ die Sprache im Spiel und wähle den Mod-Ordner mit den passenden Title_*.szs-Dateien.", "The title language file is missing. Check the in-game language under 'Folders & quality' and select the mod folder containing the matching Title_*.szs files."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string langCheck = (t.LanguagePath.Text ?? "").Trim();
                if (!String.IsNullOrWhiteSpace(langCheck) && !File.Exists(langCheck))
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Sprachdatei für {0} nicht gefunden. Wähle eine vorhandene Datei oder leere das optionale Feld.", "Language file for {0} not found. Choose an existing file or clear the optional field.", t.DisplayName));
                    return;
                }

                if (File.Exists(langCheck))
                {
                    try
                    {
                        if (String.Equals(Path.GetFullPath(t.CommonPath.Text.Trim()), Path.GetFullPath(langCheck), StringComparison.OrdinalIgnoreCase))
                        {
                            murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Bei {0} dürfen Common- und Sprach-Archiv nicht dieselbe Datei sein.", "For {0}, common and language archive must not be the same file.", t.DisplayName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    catch
                    {
                    }
                }

                enabled.Add(t);
            }

            if (enabled.Count == 0)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle ein Bild für einen Bereich in diesem Tab.", "Choose a picture for an area in this tab."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int takeEvery = 2;
            TplPixelFormat format = TplPixelFormat.CMPR;
            if (_quality.SelectedIndex == 1)
            {
                takeEvery = 1;
                format = TplPixelFormat.CMPR;
            }
            else if (_quality.SelectedIndex == 2)
            {
                takeEvery = 2;
                format = TplPixelFormat.RGB565;
            }
            else if (_quality.SelectedIndex == 3)
            {
                takeEvery = 1;
                format = TplPixelFormat.RGB565;
            }

            StringBuilder summary = new StringBuilder();
            int failures = 0;
            try
            {
                Directory.CreateDirectory(output);
                _busy = true;
                _configuration.Enabled = false;
                UpdateBuildButtons();
                _close.Enabled = false;
                UseWaitCursor = true;
                for (i = 0; i < enabled.Count; i++)
                {
                    TargetEditor target = enabled[i];
                    int basePercent = (int)(100.0 * i / enabled.Count);
                    int span = Math.Max(1, (int)(100.0 / enabled.Count));
                    _status.Text = L.F("Baue {0}...", "Building {0}...", target.DisplayName);
                    Application.DoEvents();
                    try
                    {
                        if (target.IsTitle)
                        {
                            RetroRewindGifBuildOptions options = new RetroRewindGifBuildOptions();
                            options.CommonTitlePath = target.CommonPath.Text.Trim();
                            options.LanguageTitlePath = target.LanguagePath.Text.Trim();
                            options.SourcePath = target.SourcePath.Text.Trim();
                            options.OutputFolder = output;
                            options.TakeEvery = takeEvery;
                            options.Format = format;
                            options.ReplaceBokeboke = true;
                            options.ReplaceCommonFallbacks = target.UseFirstFrameForRelatedScreens == null || target.UseFirstFrameForRelatedScreens.Checked;
                            options.AnimateCommonMenus = false;
                            options.MenuFittingMode = Math.Max(0, _menuFraming.SelectedIndex);
                            options.CleanupOldAnimations = _cleanupOld.Checked;
                            RetroRewindGifBuildResult result = RetroRewindGifBuilder.Build(options, delegate (int pct, string text)
                            {
                                _progress.Value = Math.Max(0, Math.Min(100, basePercent + (pct * span / 100)));
                                _status.Text = target.DisplayName + ": " + text;
                                _status.Refresh();
                                _progress.Refresh();
                                Application.DoEvents();
                            });
                            summary.AppendLine("[OK] " + target.DisplayName);
                            summary.AppendLine(result.Summary);
                        }
                        else
                        {
                            RetroRewindMenuBackgroundBuildOptions options = new RetroRewindMenuBackgroundBuildOptions();
                            options.ArchiveBaseName = target.ArchiveBaseName;
                            options.CommonArchivePath = target.CommonPath.Text.Trim();
                            options.LanguageArchivePath = target.ArchiveBaseName == "Title" ? "" : (target.LanguagePath.Text ?? "").Trim();
                            options.SourcePath = target.SourcePath.Text.Trim();
                            options.OutputFolder = output;
                            options.TakeEvery = takeEvery;
                            options.Format = format;
                            options.FittingMode = Math.Max(0, _menuFraming.SelectedIndex);
                            options.CleanupOldAnimations = _cleanupOld.Checked;
                            RetroRewindMenuBackgroundBuildResult result = RetroRewindMenuBackgroundBuilder.Build(options, delegate (int pct, string text)
                            {
                                _progress.Value = Math.Max(0, Math.Min(100, basePercent + (pct * span / 100)));
                                _status.Text = target.DisplayName + ": " + text;
                                _status.Refresh();
                                _progress.Refresh();
                                Application.DoEvents();
                            });
                            summary.AppendLine(result.WasPatched ? "[OK] " + target.DisplayName : "[SKIP] " + target.DisplayName);
                            summary.AppendLine(result.Summary);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        summary.AppendLine("[FEHLER / ERROR] " + target.DisplayName + ": " + ex.Message);
                    }

                    summary.AppendLine();
                }

                OutputFolder = output;
                Summary = summary.ToString().Trim();
                try
                {
                    File.WriteAllText(Path.Combine(output, "RR_BACKGROUNDS_REPORT.txt"), Summary + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                }

                _progress.Value = failures == 0 ? 100 : 0;
                _status.Text = failures == 0 ? L.T("Fertig. Die Bereiche mit ausgewähltem Bild in diesem Tab wurden verarbeitet.", "Done. The areas with a selected picture in this tab were processed.") : L.F("Fertig mit {0} Fehler(n). Siehe Zusammenfassung.", "Done with {0} error(s). See summary.", failures);
                murumsWiiModStudio.StudioMessageBox.Show(this, failures == 0 ? ExportHelp.Message(output) + "\n\n" + Summary : Summary, L.T("MKWii Backgrounds Tool", "MKWii Backgrounds Tool"), MessageBoxButtons.OK, failures == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                ToolStatus.Set(this, false);
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Kopien konnten nicht erstellt werden", "Could not create copies"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                _busy = false;
                _configuration.Enabled = true;
                UpdateBuildButtons();
                _close.Enabled = true;
            }
        }

        private void ResetPathViews()
        {
            ResetPathView(_uiFolder);
            ResetPathView(_outputFolder);
            int i;
            for (i = 0; i < _targets.Count; i++)
            {
                ResetPathView(_targets[i].CommonPath);
                ResetPathView(_targets[i].LanguagePath);
                ResetPathView(_targets[i].SourcePath);
            }
        }

        private static void ResetPathView(TextBox box)
        {
            if (box == null)
                return;
            box.SelectionStart = 0;
            box.SelectionLength = 0;
        }

        private static Label NewLabel(string text)
        {
            Label l = new Label();
            l.UseMnemonic = false;
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(0, 7, 8, 6);
            l.ForeColor = DarkTheme.Muted;
            return l;
        }

        private static Button NewButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.Height = 34;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.FlatAppearance.MouseOverBackColor = DarkTheme.Panel3;
            b.FlatAppearance.MouseDownBackColor = DarkTheme.AccentSoft;
            b.FlatAppearance.BorderSize = 1;
            b.Padding = new Padding(6, 0, 6, 0);
            return b;
        }

        private static void StylePathBox(TextBox box)
        {
            if (box == null)
                return;
            box.BackColor = DarkTheme.Panel;
            box.ForeColor = DarkTheme.Fore;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Margin = new Padding(0, 3, 0, 3);
        }

        private static CheckBox NewCheck(string text, bool value)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = value;
            c.AutoSize = true;
            c.Margin = new Padding(0, 8, 0, 4);
            return c;
        }
    }

    internal sealed class RetroRewindGifBuildOptions
    {
        public string CommonTitlePath;
        public string LanguageTitlePath;
        public string SourcePath;
        public string OutputFolder;
        public int TakeEvery = 2;
        public TplPixelFormat Format = TplPixelFormat.CMPR;
        public bool ReplaceBokeboke = true;
        public bool ReplaceCommonFallbacks = true;
        public bool AnimateCommonMenus = false;
        // 0 = BRLYT UV-aware (recommended), 1 = legacy exact stretch, 2 = legacy fill/crop.
        public int MenuFittingMode = 0;
        public bool CleanupOldAnimations = true;
    }

    internal sealed class RetroRewindGifBuildResult
    {
        public string OutputFolder;
        public string Summary;
    }

    internal static class RetroRewindGifBuilder
    {
        private sealed class SelectedSourceFrame : IDisposable
        {
            public Bitmap Bitmap;
            public int DurationMs;
            public void Dispose()
            {
                if (Bitmap != null)
                    Bitmap.Dispose();
            }
        }

        private sealed class CommonTarget
        {
            public string MaterialName = "";
            public byte Slot;
            public string TextureName = "";
            public int Width;
            public int Height;
        }

        private sealed class CommonFrameGroup
        {
            public int Width;
            public int Height;
            public string Prefix = "";
            public readonly List<string> Names = new List<string>();
            public readonly List<byte[]> TplData = new List<byte[]>();
            public List<ushort> Indices = new List<ushort>();
        }

        private sealed class CommonTextureLayout
        {
            public string TextureName = "";
            public string MaterialName = "";
            public string PaneName = "";
            public int Slot;
            public float PaneWidth;
            public float PaneHeight;
            public float LayoutWidth;
            public float LayoutHeight;
            public float U0, V0;
            public float U1 = 1.0f, V1;
            public float U2, V2 = 1.0f;
            public float U3 = 1.0f, V3 = 1.0f;
            public float MinU = 0.0f;
            public float MaxU = 1.0f;
            public float MinV = 0.0f;
            public float MaxV = 1.0f;
            public bool FlipX;
            public bool FlipY;
            public bool HasExplicitUv;
            public BrlytPaneInfo Pane;
        }

        private sealed class BrctrInfo
        {
            public string MainLayoutName = "";
            public string PictureSourceLayoutName = "";
            public readonly List<string> AnimationGroupNames = new List<string>();
            public readonly List<string> AnimationLayoutGroupNames = new List<string>();
            public readonly List<string> AnimationBrlanNames = new List<string>();
        }

        public static RetroRewindGifBuildResult Build(RetroRewindGifBuildOptions options, Action<int, string> progress)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (progress == null)
                progress = delegate (int pct, string text)
                {
                };
            progress(2, L.T("Archive werden geladen...", "Loading archives..."));
            U8Archive common = U8Archive.Load(File.ReadAllBytes(options.CommonTitlePath));
            U8Archive language = U8Archive.Load(File.ReadAllBytes(options.LanguageTitlePath));
            ArchiveEntry langTitle = FindEntry(language.Root, "title");
            ArchiveEntry langAnim = FindEntry(language.Root, "title", "anim");
            ArchiveEntry langBlyt = FindEntry(language.Root, "title", "blyt");
            ArchiveEntry langTimg = FindEntry(language.Root, "title", "timg");
            ArchiveEntry titleOn = FindEntry(language.Root, "title", "anim", "title_on.brlan");
            ArchiveEntry titleBrlyt = FindEntry(language.Root, "title", "blyt", "title.brlyt");
            if (langTitle == null || langAnim == null || langBlyt == null || langTimg == null || titleOn == null || titleBrlyt == null)
                throw new InvalidDataException(L.T("Das Sprach-Archiv hat nicht die benötigte Title-Struktur (title/anim/title_on.brlan + title/blyt/title.brlyt + title/timg).", "The language archive does not have the required title structure (title/anim/title_on.brlan + title/blyt/title.brlyt + title/timg)."));
            BrlytLayoutMap titleMap = LoadBrlytMap(titleBrlyt.Data, "title.brlyt");
            BrlytTextureBinding topBinding = titleMap.FindFirstByMaterial("title_top");
            BrlytTextureBinding bottomBinding = titleMap.FindFirstByMaterial("title_bottom");
            if (topBinding == null || bottomBinding == null)
                throw new InvalidDataException(L.T("title_top/title_bottom konnten in title.brlyt nicht aufgelöst werden.", "title_top/title_bottom could not be resolved from title.brlyt."));
            if (topBinding.Slot < 0 || topBinding.Slot > 255 || bottomBinding.Slot < 0 || bottomBinding.Slot > 255)
                throw new InvalidDataException(L.T("title_top/title_bottom verwendet einen Texture-Slot ausserhalb 0–255.", "title_top/title_bottom uses a texture slot outside 0–255."));
            if (String.IsNullOrWhiteSpace(topBinding.TextureName) || String.IsNullOrWhiteSpace(bottomBinding.TextureName))
                throw new InvalidDataException(L.T("title_top/title_bottom hat kein gültiges TPL-Binding.", "title_top/title_bottom has no valid TPL binding."));
            ArchiveEntry commonImageBrlyt = FindEntry(common.Root, "title", "blyt", "title_image_common.brlyt");
            ArchiveEntry commonTimg = FindEntry(common.Root, "title", "timg");
            ArchiveEntry titleImageCtrl = FindEntry(common.Root, "title", "ctrl", "TitleImage.brctr");
            ArchiveEntry titleBackCtrl = FindEntry(common.Root, "title", "ctrl", "TitleBack.brctr");
            ArchiveEntry titleBokeBrlyt = FindEntry(language.Root, "title", "blyt", "title_boke.brlyt");
            ArchiveEntry commonLoopTemplate = FindEntry(common.Root, "title", "anim", "common_w002_title_light_loop.brlan");
            byte[] titleImageCtrlOriginal = titleImageCtrl != null && titleImageCtrl.Data != null ? (byte[])titleImageCtrl.Data.Clone() : null;
            byte[] titleBackCtrlOriginal = titleBackCtrl != null && titleBackCtrl.Data != null ? (byte[])titleBackCtrl.Data.Clone() : null;
            if ((options.ReplaceCommonFallbacks || options.AnimateCommonMenus) && (commonImageBrlyt == null || commonTimg == null))
                throw new InvalidDataException(L.T("Title.szs enthält title/blyt/title_image_common.brlyt oder title/timg nicht.", "Title.szs is missing title/blyt/title_image_common.brlyt or title/timg."));
            if (options.AnimateCommonMenus && (titleBackCtrl == null || titleBokeBrlyt == null || commonLoopTemplate == null))
                throw new InvalidDataException(L.T("Für die Menü-GIF-Animation fehlen TitleBack.brctr, title_boke.brlyt oder common_w002_title_light_loop.brlan.", "Menu GIF animation requires TitleBack.brctr, title_boke.brlyt and common_w002_title_light_loop.brlan."));
            BrlytLayoutMap commonMap = null;
            BrlytDocument commonLayout = null;
            string controllerInfo = "";
            if (commonImageBrlyt != null && commonTimg != null)
            {
                commonMap = LoadBrlytMap(commonImageBrlyt.Data, "title_image_common.brlyt");
                commonLayout = BrlytDocument.FromBytes(commonImageBrlyt.Data);
            }

            // Safety: controllers are runtime contracts. Never blank or rewrite their picture-source
            // fields. Alpha21 proved that title_image_common is required by Pages::BlurryTitle::OnActivate,
            // which calls SetPicturePane for names such as mario2_boke.
            if (titleImageCtrl != null)
            {
                BrctrInfo imageCtrl = ReadBrctrInfo(titleImageCtrl.Data);
                if (String.IsNullOrWhiteSpace(imageCtrl.PictureSourceLayoutName))
                    throw new InvalidDataException(L.T("TitleImage.brctr hat keine Picture-Source mehr. Das entspricht einer unsicheren Alpha21-Ausgabe. Bitte zuerst die originale Retro-Rewind Title.szs wiederherstellen und danach neu bauen.", "TitleImage.brctr no longer has a picture source. An older export may have removed it. Restore the original Retro Rewind Title.szs first, then build again."));
                controllerInfo = "TitleImage.brctr preserved: " + imageCtrl.MainLayoutName + " / " + imageCtrl.PictureSourceLayoutName;
                // Retro Rewind's controller maps its Fade state to the BRLYT group
                // title_on_off. In the supplied Title_E.szs that group contains only
                // title_bottom. NW4R only binds BRLAN targets reachable through the
                // selected BRLYT group, so the generated title_top RLTP existed in
                // title_on.brlan but was never applied at runtime. Expand the exact
                // controller-referenced group to include both title panes.
                string bindGroup = "";
                int gi;
                for (gi = 0; gi < imageCtrl.AnimationLayoutGroupNames.Count; gi++)
                {
                    string candidate = imageCtrl.AnimationLayoutGroupNames[gi];
                    if (BrlytHasGroup(titleBrlyt.Data, candidate))
                    {
                        bindGroup = candidate;
                        break;
                    }
                }

                if (String.IsNullOrWhiteSpace(bindGroup) && BrlytHasGroup(titleBrlyt.Data, "title_on_off"))
                    bindGroup = "title_on_off";
                if (String.IsNullOrWhiteSpace(bindGroup))
                    throw new InvalidDataException(L.T("Die von TitleImage.brctr referenzierte BRLYT-Animationsgruppe wurde in title.brlyt nicht gefunden.", "The BRLYT animation group referenced by TitleImage.brctr was not found in title.brlyt."));
                titleBrlyt.Data = EnsureBrlytGroupContainsPanes(titleBrlyt.Data, bindGroup, new string[] { "title_bottom", "title_top" });
                if (!BrlytGroupContainsPane(titleBrlyt.Data, bindGroup, "title_bottom") || !BrlytGroupContainsPane(titleBrlyt.Data, bindGroup, "title_top"))
                    throw new InvalidDataException("Title BRLYT group validation failed after adding title_top.");
                controllerInfo += "\r\nTitle animation BRLYT group: " + bindGroup + " = title_bottom + title_top";
            }

            if (titleBackCtrl != null)
            {
                BrctrInfo backCtrl = ReadBrctrInfo(titleBackCtrl.Data);
                if (String.IsNullOrWhiteSpace(backCtrl.PictureSourceLayoutName))
                    throw new InvalidDataException(L.T("TitleBack.brctr hat keine Picture-Source mehr. Das entspricht einer unsicheren Alpha21-Ausgabe. Bitte zuerst die originale Retro-Rewind Title.szs wiederherstellen.", "TitleBack.brctr no longer has a picture source. An older export may have removed it. Restore the original Retro Rewind Title.szs first."));
                if (!String.IsNullOrWhiteSpace(controllerInfo))
                    controllerInfo += "\r\n";
                controllerInfo += "TitleBack.brctr preserved: " + backCtrl.MainLayoutName + " / " + backCtrl.PictureSourceLayoutName;
            }

            progress(8, L.T("Hintergrundquelle wird gelesen und vorbereitet...", "Reading and preparing background source..."));
            List<SelectedSourceFrame> selected = LoadSelectedFrames(options.SourcePath, Math.Max(1, options.TakeEvery));
            if (selected.Count == 0)
                throw new InvalidDataException(L.T("Die Hintergrundquelle enthält kein verwertbares Bild.", "The background source contains no usable image."));
            Bitmap fullFrame0 = null;
            Bitmap topFrame0 = null;
            Bitmap bottomFrame0 = null;
            List<string> topNames = new List<string>();
            List<string> bottomNames = new List<string>();
            List<float> keyFrames = new List<float>();
            List<byte[]> topTplData = new List<byte[]>();
            List<byte[]> bottomTplData = new List<byte[]>();
            float timeline = 0.0f;
            try
            {
                int i;
                for (i = 0; i < selected.Count; i++)
                {
                    using (Bitmap normalized = ResizeFrame(selected[i].Bitmap, 832, 456, true))
                    using (Bitmap top = Crop(normalized, new Rectangle(0, 0, 832, 140)))
                    using (Bitmap bottom = Crop(normalized, new Rectangle(0, 140, 832, 316)))
                    {
                        if (i == 0)
                        {
                            fullFrame0 = new Bitmap(normalized);
                            topFrame0 = new Bitmap(top);
                            bottomFrame0 = new Bitmap(bottom);
                        }

                        string topName = "mwms_rr_top_" + i.ToString("D3", CultureInfo.InvariantCulture) + ".tpl";
                        string bottomName = "mwms_rr_bottom_" + i.ToString("D3", CultureInfo.InvariantCulture) + ".tpl";
                        topNames.Add(topName);
                        bottomNames.Add(bottomName);
                        keyFrames.Add(timeline);
                        topTplData.Add(TplEncoder.Encode(top, options.Format));
                        bottomTplData.Add(TplEncoder.Encode(bottom, options.Format));
                    }

                    timeline += Math.Max(1.0f, selected[i].DurationMs * 60.0f / 1000.0f);
                    progress(10 + (int)(43.0 * (i + 1) / selected.Count), L.F("Hintergrundbilder werden konvertiert: {0}/{1}", "Converting background images: {0}/{1}", i + 1, selected.Count));
                }

                if (timeline > 65535.0f)
                    throw new InvalidDataException(L.T("Die Animation ist länger als 65535 BRLAN-Frames.", "The animation is longer than 65535 BRLAN frames."));
                ushort totalFrames = (ushort)Math.Max(1, (int)Math.Ceiling(timeline));
                progress(55, L.T("Alte erzeugte Background-Dateien werden bereinigt...", "Cleaning old generated background files..."));
                if (options.CleanupOldAnimations)
                {
                    CleanupOldTitleGifFiles(langTimg, topBinding.TextureName, bottomBinding.TextureName);
                    if (commonTimg != null)
                        CleanupOldCommonGifFiles(commonTimg);
                }

                BrlanDocument doc = BrlanCodec.Parse(titleOn.Data);
                if (doc.Pai == null)
                    throw new InvalidDataException(L.T("title_on.brlan enthält keine pai1-Sektion.", "title_on.brlan has no pai1 section."));
                RemoveGeneratedTextureNames(doc.Pai.Textures, topBinding.TextureName, bottomBinding.TextureName);
                RemoveGeneratedCommonTextureNames(doc.Pai.Textures);
                RemoveObsoleteCommonRltpAnimations(doc, commonMap);
                List<ushort> topIndices = AppendTextureNames(doc.Pai.Textures, topNames);
                List<ushort> bottomIndices = AppendTextureNames(doc.Pai.Textures, bottomNames);
                ApplyRltp(doc, "title_top", (byte)Math.Max(0, topBinding.Slot), keyFrames, topIndices);
                ApplyRltp(doc, "title_bottom", (byte)Math.Max(0, bottomBinding.Slot), keyFrames, bottomIndices);
                doc.Pai.Flags = 1;
                doc.Pai.Frames = totalFrames;
                titleOn.Data = BrlanCodec.Build(doc);
                progress(62, L.T("TPL-Hintergrundserien werden in das Sprach-Archiv geschrieben...", "Writing background TPL sequences into the language archive..."));
                WriteGeneratedTpls(langTimg, topNames, topTplData);
                WriteGeneratedTpls(langTimg, bottomNames, bottomTplData);
                int staticLanguageCount = 0;
                staticLanguageCount += ReplaceTpl(langTimg, topBinding.TextureName, topFrame0) ? 1 : 0;
                staticLanguageCount += ReplaceTpl(langTimg, bottomBinding.TextureName, bottomFrame0) ? 1 : 0;
                if (options.ReplaceBokeboke)
                {
                    staticLanguageCount += ReplaceTplIfExists(langTimg, "tt_title_screen_title_rogo_bokeboke.tpl", topFrame0) ? 1 : 0;
                    staticLanguageCount += ReplaceTplIfExists(langTimg, "tt_title_screen_mario0_bokeboke.tpl", bottomFrame0) ? 1 : 0;
                }

                int commonCount = 0;
                int commonUvCount = 0;
                List<string> mappingDiagnostics = new List<string>();
                if ((options.ReplaceCommonFallbacks || options.AnimateCommonMenus) && commonMap != null)
                {
                    progress(69, L.T("Retro-Rewind-Menühintergründe werden anhand der BRLYT-UVs gebacken...", "Baking Retro Rewind menu backgrounds using BRLYT UV mapping..."));
                    Dictionary<string, CommonTextureLayout> layouts = BuildCommonTextureLayouts(commonLayout);
                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int b;
                    for (b = 0; b < commonMap.Bindings.Count; b++)
                    {
                        string texture = commonMap.Bindings[b].TextureName ?? "";
                        if (!texture.StartsWith("tt_title_screen_", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!options.ReplaceBokeboke && IsBokebokeTexture(texture))
                            continue;
                        if (!seen.Add(texture))
                            continue;
                        CommonTextureLayout layout = FindCommonTextureLayout(layouts, texture);
                        if (ReplaceTplMenuFitIfExists(commonTimg, texture, fullFrame0, topFrame0, bottomFrame0, options.MenuFittingMode, layout, mappingDiagnostics))
                        {
                            commonCount++;
                            if (layout != null && layout.HasExplicitUv && options.MenuFittingMode == 0)
                                commonUvCount++;
                        }
                    }

                    // Some BRLYTs list textures which are not currently bound. Include those as well.
                    for (b = 0; b < commonMap.Textures.Count; b++)
                    {
                        string texture = commonMap.Textures[b] ?? "";
                        if (!texture.StartsWith("tt_title_screen_", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!options.ReplaceBokeboke && IsBokebokeTexture(texture))
                            continue;
                        if (!seen.Add(texture))
                            continue;
                        CommonTextureLayout layout = FindCommonTextureLayout(layouts, texture);
                        if (ReplaceTplMenuFitIfExists(commonTimg, texture, fullFrame0, topFrame0, bottomFrame0, options.MenuFittingMode, layout, mappingDiagnostics))
                        {
                            commonCount++;
                            if (layout != null && layout.HasExplicitUv && options.MenuFittingMode == 0)
                                commonUvCount++;
                        }
                    }
                }

                string menuAnimationInfo = L.T("deaktiviert", "disabled");
                if (options.AnimateCommonMenus)
                {
                    progress(77, L.T("TitleBack-Menüloop wird aufgebaut...", "Building the TitleBack menu loop..."));
                    // The live menu uses title_boke.brlyt. Its static title/chara panes are the
                    // exact same 832x140 + 832x316 split as the Press-A title. Add a proper
                    // BRLYT animation group, bind a dedicated Loop animation in TitleBack.brctr
                    // and keep the required title_image_common picture source intact.
                    titleBokeBrlyt.Data = EnsureTitleBackBrlanGroup(titleBokeBrlyt.Data);
                    byte[] backLoop = BuildTitleBackLoopBrlan(commonLoopTemplate.Data, totalFrames, keyFrames, topNames, bottomNames);
                    WriteOrReplaceFile(langAnim, TitleBackLoopBrlan, backLoop);
                    titleBackCtrl.Data = PatchTitleBackControllerForLoop(titleBackCtrl.Data, TitleBackLoopBrlan);
                    ValidateTitleBackLoop(titleBackCtrl.Data, titleBokeBrlyt.Data, backLoop, langTimg, topNames, bottomNames);
                    menuAnimationInfo = L.T("aktiv — TitleBack eAFLoop + title/chara RLTP", "enabled — TitleBack eAFLoop + title/chara RLTP");
                    mappingDiagnostics.Add("MENU ANIMATION | TitleBack.brctr picture-source preserved | group=eAFLoop -> mwms_rr_loop | animation=Loop | brlan=mwms_rr_titleback_loop.brlan | materials=title,chara");
                }

                // TitleImage.brctr always remains byte-for-byte unchanged. TitleBack.brctr is
                // preserved too unless the user explicitly enables the menu GIF option; in that
                // case it is structurally rebuilt with one Loop group while retaining the exact
                // main layout and picture-source names.
                ValidateGeneratedTitle(doc, langTimg, topNames, bottomNames);
                if (titleImageCtrlOriginal != null && !ByteArraysEqual(titleImageCtrlOriginal, titleImageCtrl.Data))
                    throw new InvalidDataException("Safety validation failed: TitleImage.brctr was modified.");
                if (!options.AnimateCommonMenus && titleBackCtrlOriginal != null && !ByteArraysEqual(titleBackCtrlOriginal, titleBackCtrl.Data))
                    throw new InvalidDataException("Safety validation failed: TitleBack.brctr was modified while menu animation was disabled.");
                if (options.AnimateCommonMenus)
                {
                    BrctrInfo patchedBack = ReadBrctrInfo(titleBackCtrl.Data);
                    if (!ContainsLayoutName(patchedBack.MainLayoutName, "title_boke") || !ContainsLayoutName(patchedBack.PictureSourceLayoutName, "title_image_common") || !patchedBack.AnimationGroupNames.Contains("eAFLoop") || !ContainsBrlanNameInList(patchedBack.AnimationBrlanNames, "mwms_rr_titleback_loop"))
                        throw new InvalidDataException("Safety validation failed: patched TitleBack.brctr lost its expected layout, picture source or loop BRLAN binding.");
                }

                progress(84, L.T("Gepatchte SZS-Dateien werden erstellt...", "Building patched SZS files..."));
                Directory.CreateDirectory(options.OutputFolder);
                string commonOut = SafeOutputPath(options.OutputFolder, options.CommonTitlePath);
                string languageOut = SafeOutputPath(options.OutputFolder, options.LanguageTitlePath);
                SaveSzs(common, commonOut);
                progress(92, L.T("Sprach-Archiv wird komprimiert...", "Compressing language archive..."));
                SaveSzs(language, languageOut);
                string commonMode = options.MenuFittingMode == 0 ? L.F("BRLYT 4-Punkt-UV ({0} Texturen mit expliziten UVs)", "BRLYT 4-point UV ({0} textures with explicit UVs)", commonUvCount) : (options.MenuFittingMode == 2 ? L.T("Legacy Fill/Crop", "Legacy Fill/Crop") : L.T("Legacy Volltextur-Stretch", "Legacy full-texture stretch"));
                if (mappingDiagnostics.Count > 0)
                {
                    try
                    {
                        string diagPath = Path.Combine(options.OutputFolder, "RR_BACKGROUND_MAPPING.txt");
                        File.WriteAllLines(diagPath, mappingDiagnostics.ToArray(), Encoding.UTF8);
                    }
                    catch
                    {
                    }
                }

                string animationLine = L.F("Lizenz-/Hauptmenü: statisches erstes Bild\r\nController-Basis: {0}\r\nStatischer Fallback-Fit: {1}", "License/main menu: static first image\r\nController base: {0}\r\nStatic fallback fitting: {1}", String.IsNullOrWhiteSpace(controllerInfo) ? "—" : controllerInfo, commonMode);
                RetroRewindGifBuildResult result = new RetroRewindGifBuildResult();
                result.OutputFolder = options.OutputFolder;
                result.Summary = L.F("Retro-Rewind-Hintergrund erstellt.\r\n\r\nQuellbilder: {0}\r\nBRLAN-Länge: {1} Frames\r\nFormat: {2}\r\nStatische Sprach-TPLs ersetzt: {3}\r\nStatische title_image_common-TPLs ersetzt: {4}\r\n{5}\r\n\r\nAusgabe:\r\n{6}\r\n{7}", "Retro Rewind background created.\r\n\r\nSource images: {0}\r\nBRLAN length: {1} frames\r\nFormat: {2}\r\nStatic language TPLs replaced: {3}\r\nStatic title_image_common TPLs replaced: {4}\r\n{5}\r\n\r\nOutput:\r\n{6}\r\n{7}", selected.Count, totalFrames, options.Format.ToString(), staticLanguageCount, commonCount, animationLine, commonOut, languageOut);
                progress(100, L.T("Fertig.", "Done."));
                return result;
            }
            finally
            {
                int i;
                for (i = 0; i < selected.Count; i++)
                    selected[i].Dispose();
                if (fullFrame0 != null)
                    fullFrame0.Dispose();
                if (topFrame0 != null)
                    topFrame0.Dispose();
                if (bottomFrame0 != null)
                    bottomFrame0.Dispose();
            }
        }

        private static List<CommonTarget> CollectCommonTargets(BrlytLayoutMap map, ArchiveEntry timg)
        {
            List<CommonTarget> targets = new List<CommonTarget>();
            if (map == null || timg == null)
                return targets;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int i;
            for (i = 0; i < map.Bindings.Count; i++)
            {
                BrlytTextureBinding binding = map.Bindings[i];
                string texture = binding.TextureName ?? "";
                string material = binding.MaterialName ?? "";
                if (!texture.StartsWith("tt_title_screen_", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (String.IsNullOrWhiteSpace(material))
                    continue;
                if (binding.Slot < 0 || binding.Slot > 255)
                    continue;
                string key = material + "\n" + binding.Slot.ToString(CultureInfo.InvariantCulture);
                if (!seen.Add(key))
                    continue;
                ArchiveEntry entry = timg.FindChild(texture);
                if (entry == null || entry.IsDirectory)
                    continue;
                TplTextureInfo info = TplTextureEditor.GetFirstImageInfo(entry.Data);
                CommonTarget target = new CommonTarget();
                target.MaterialName = material;
                target.Slot = (byte)binding.Slot;
                target.TextureName = texture;
                target.Width = info.Width;
                target.Height = info.Height;
                targets.Add(target);
            }

            return targets;
        }

        private static List<CommonFrameGroup> CreateCommonGroups(List<CommonTarget> targets)
        {
            List<CommonFrameGroup> groups = new List<CommonFrameGroup>();
            int i;
            for (i = 0; i < targets.Count; i++)
            {
                CommonTarget target = targets[i];
                CommonFrameGroup existing = FindCommonGroup(groups, target.Width, target.Height);
                if (existing != null)
                    continue;
                CommonFrameGroup group = new CommonFrameGroup();
                group.Width = target.Width;
                group.Height = target.Height;
                group.Prefix = "mwms_rr_common_" + target.Width.ToString(CultureInfo.InvariantCulture) + "x" + target.Height.ToString(CultureInfo.InvariantCulture) + "_";
                groups.Add(group);
            }

            return groups;
        }

        private static CommonFrameGroup FindCommonGroup(List<CommonFrameGroup> groups, int width, int height)
        {
            int i;
            for (i = 0; i < groups.Count; i++)
                if (groups[i].Width == width && groups[i].Height == height)
                    return groups[i];
            return null;
        }

        private static Dictionary<string, CommonTextureLayout> BuildCommonTextureLayouts(BrlytDocument layout)
        {
            Dictionary<string, CommonTextureLayout> result = new Dictionary<string, CommonTextureLayout>(StringComparer.OrdinalIgnoreCase);
            if (layout == null)
                return result;
            int i;
            for (i = 0; i < layout.Panes.Count; i++)
            {
                BrlytPaneInfo pane = layout.Panes[i];
                if (pane == null || !String.Equals(pane.Magic, "pic1", StringComparison.Ordinal))
                    continue;
                BrlytMaterialInfo material = layout.MaterialForPane(pane);
                if (material == null)
                    continue;
                int b;
                for (b = 0; b < material.Bindings.Count; b++)
                {
                    BrlytTextureBinding binding = material.Bindings[b];
                    string texture = binding.TextureName ?? "";
                    if (String.IsNullOrWhiteSpace(texture))
                        continue;
                    CommonTextureLayout item = new CommonTextureLayout();
                    item.TextureName = texture;
                    item.MaterialName = material.Name ?? "";
                    item.PaneName = pane.Name ?? "";
                    item.Slot = binding.Slot;
                    item.PaneWidth = pane.Width;
                    item.PaneHeight = pane.Height;
                    item.LayoutWidth = layout.LayoutWidth;
                    item.LayoutHeight = layout.LayoutHeight;
                    item.Pane = pane;
                    BrlytTexCoordSet uv = null;
                    if (pane.TexCoords.Count > 0)
                    {
                        int uvIndex = binding.Slot >= 0 && binding.Slot < pane.TexCoords.Count ? binding.Slot : 0;
                        uv = pane.TexCoords[uvIndex];
                    }

                    if (uv != null && IsFinite(uv.MinU) && IsFinite(uv.MaxU) && IsFinite(uv.MinV) && IsFinite(uv.MaxV) && uv.MaxU - uv.MinU > 0.0001f && uv.MaxV - uv.MinV > 0.0001f)
                    {
                        item.U0 = uv.U0;
                        item.V0 = uv.V0;
                        item.U1 = uv.U1;
                        item.V1 = uv.V1;
                        item.U2 = uv.U2;
                        item.V2 = uv.V2;
                        item.U3 = uv.U3;
                        item.V3 = uv.V3;
                        item.MinU = uv.MinU;
                        item.MaxU = uv.MaxU;
                        item.MinV = uv.MinV;
                        item.MaxV = uv.MaxV;
                        item.FlipX = uv.FlipX;
                        item.FlipY = uv.FlipY;
                        item.HasExplicitUv = true;
                    }

                    CommonTextureLayout previous;
                    if (!result.TryGetValue(texture, out previous) || (!previous.HasExplicitUv && item.HasExplicitUv))
                        result[texture] = item;
                }
            }

            return result;
        }

        private static CommonTextureLayout FindCommonTextureLayout(Dictionary<string, CommonTextureLayout> layouts, string texture)
        {
            if (layouts == null || String.IsNullOrWhiteSpace(texture))
                return null;
            CommonTextureLayout result;
            return layouts.TryGetValue(texture, out result) ? result : null;
        }

        private static bool IsFinite(float value)
        {
            return !Single.IsNaN(value) && !Single.IsInfinity(value);
        }

        private static bool IsBokebokeTexture(string name)
        {
            return !String.IsNullOrEmpty(name) && name.IndexOf("bokeboke", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Bitmap PrepareMenuFrame(Bitmap source, byte[] originalTpl, TplTextureInfo info, int fittingMode, CommonTextureLayout layout, string textureName, out string diagnostic)
        {
            diagnostic = "";
            if (fittingMode == 2)
            {
                diagnostic = textureName + " | " + info.Width.ToString(CultureInfo.InvariantCulture) + "x" + info.Height.ToString(CultureInfo.InvariantCulture) + " | legacy fill/crop";
                return ResizeFrame(source, info.Width, info.Height, true);
            }

            if (fittingMode == 1)
            {
                diagnostic = textureName + " | " + info.Width.ToString(CultureInfo.InvariantCulture) + "x" + info.Height.ToString(CultureInfo.InvariantCulture) + " | legacy full stretch";
                return ResizeFrameExact(source, info.Width, info.Height);
            }

            return PrepareMenuFrameUvAware(source, originalTpl, info, layout, textureName, out diagnostic);
        }

        private static Bitmap PrepareMenuFrameUvAware(Bitmap source, byte[] originalTpl, TplTextureInfo info, CommonTextureLayout layout, string textureName, out string diagnostic)
        {
            if (layout == null || !layout.HasExplicitUv)
            {
                diagnostic = textureName + " | " + info.Width.ToString(CultureInfo.InvariantCulture) + "x" + info.Height.ToString(CultureInfo.InvariantCulture) + " | no explicit pic1 UV -> full texture fallback";
                return ResizeFrameExact(source, info.Width, info.Height);
            }

            // The four pic1 UV vertices describe where the pane samples the texture.
            // Bake the already-selected source region into that exact texture-space mapping
            // instead of treating the physical TPL rectangle as an independently scaled image.
            // DrawImage(PointF[]) performs the affine mapping TL/TR/BL directly; this
            // also handles standard horizontal/vertical mirroring and rotated UVs.
            PointF p0 = new PointF(layout.U0 * info.Width, layout.V0 * info.Height);
            PointF p1 = new PointF(layout.U1 * info.Width, layout.V1 * info.Height);
            PointF p2 = new PointF(layout.U2 * info.Width, layout.V2 * info.Height);
            PointF p3 = new PointF(layout.U3 * info.Width, layout.V3 * info.Height);
            if (!IsFinite(p0.X) || !IsFinite(p0.Y) || !IsFinite(p1.X) || !IsFinite(p1.Y) || !IsFinite(p2.X) || !IsFinite(p2.Y) || !IsFinite(p3.X) || !IsFinite(p3.Y))
            {
                diagnostic = textureName + " | invalid pic1 UV values -> full texture fallback";
                return ResizeFrameExact(source, info.Width, info.Height);
            }

            float area2 = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
            if (Math.Abs(area2) < 0.01f)
            {
                diagnostic = textureName + " | degenerate pic1 UV mapping -> full texture fallback";
                return ResizeFrameExact(source, info.Width, info.Height);
            }

            // GDI+'s three-point image mapping is affine. NW4R picture UVs are normally
            // rectangular/parallelogram mappings. Keep a diagnostic if the authored BRLYT
            // uses a truly non-affine fourth vertex so that the exact archive can be inspected.
            PointF predictedP3 = new PointF(p1.X + p2.X - p0.X, p1.Y + p2.Y - p0.Y);
            float p3dx = p3.X - predictedP3.X;
            float p3dy = p3.Y - predictedP3.Y;
            float affineError = (float)Math.Sqrt(p3dx * p3dx + p3dy * p3dy);
            Bitmap target = DecodeTplBitmapOrBlank(originalTpl, textureName, info.Width, info.Height);
            using (Graphics g = Graphics.FromImage(target))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                PointF[] dest = new PointF[]
                {
                    p0,
                    p1,
                    p2
                };
                g.DrawImage(source, dest, new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }

            BrlytPaneInfo pane = layout.Pane;
            string paneGeometry = pane == null ? "pane geometry unavailable" : String.Format(CultureInfo.InvariantCulture, "layout={0:0.###}x{1:0.###} panePos=({2:0.###},{3:0.###}) paneScale=({4:0.###},{5:0.###}) paneSize={6:0.###}x{7:0.###} origin=0x{8:X2} flags=0x{9:X2} parent={10}", layout.LayoutWidth, layout.LayoutHeight, pane.X, pane.Y, pane.ScaleX, pane.ScaleY, pane.Width, pane.Height, pane.Origin, pane.Flags, pane.Parent == null || String.IsNullOrWhiteSpace(pane.Parent.Name) ? "—" : pane.Parent.Name);
            diagnostic = String.Format(CultureInfo.InvariantCulture, "{0} | TPL {1}x{2} | pane={3} material={4} slot={5} | {6} | UV TL=({7:0.######},{8:0.######}) TR=({9:0.######},{10:0.######}) BL=({11:0.######},{12:0.######}) BR=({13:0.######},{14:0.######}) | texture px TL=({15:0.###},{16:0.###}) TR=({17:0.###},{18:0.###}) BL=({19:0.###},{20:0.###}) BR=({21:0.###},{22:0.###}) | bounds=({23:0.######},{24:0.######})-({25:0.######},{26:0.######}) | flipX={27} flipY={28} | affineErrorPx={29:0.###}{30}", textureName, info.Width, info.Height, layout.PaneName, layout.MaterialName, layout.Slot, paneGeometry, layout.U0, layout.V0, layout.U1, layout.V1, layout.U2, layout.V2, layout.U3, layout.V3, p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, layout.MinU, layout.MinV, layout.MaxU, layout.MaxV, layout.FlipX, layout.FlipY, affineError, affineError > 1.0f ? " | WARNING: non-affine UV quad; TL/TR/BL affine approximation used" : "");
            return target;
        }

        private static Bitmap DecodeTplBitmapOrBlank(byte[] tpl, string name, int width, int height)
        {
            TexturePreviewResult decoded;
            string error;
            if (TexturePreview.TryDecode(name, tpl, 0, out decoded, out error) && decoded != null && decoded.Bitmap != null)
            {
                try
                {
                    if (decoded.Bitmap.Width == width && decoded.Bitmap.Height == height)
                        return new Bitmap(decoded.Bitmap);
                }
                finally
                {
                    decoded.Dispose();
                }
            }

            Bitmap blank = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(blank))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.Transparent);
            }

            return blank;
        }

        private static Bitmap ResizeFrameExact(Bitmap source, int width, int height)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (source.Width == width && source.Height == height)
                return new Bitmap(source);
            return RenderResizedBitmap(source, new RectangleF(0, 0, source.Width, source.Height), width, height, false);
        }

        private static void ConfigureHighQualityGraphics(Graphics g)
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
        }

        private static RectangleF ComputeResizeSourceRect(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool fillCrop)
        {
            if (!fillCrop || sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
                return new RectangleF(0, 0, sourceWidth, sourceHeight);
            float sourceAspect = sourceWidth / (float)sourceHeight;
            float targetAspect = targetWidth / (float)targetHeight;
            if (Math.Abs(sourceAspect - targetAspect) < 0.0001f)
                return new RectangleF(0, 0, sourceWidth, sourceHeight);
            if (sourceAspect > targetAspect)
            {
                float cropWidth = sourceHeight * targetAspect;
                return new RectangleF((sourceWidth - cropWidth) / 2.0f, 0, cropWidth, sourceHeight);
            }

            float cropHeight = sourceWidth / targetAspect;
            return new RectangleF(0, (sourceHeight - cropHeight) / 2.0f, sourceWidth, cropHeight);
        }

        private static Bitmap ExtractRegion(Bitmap source, RectangleF srcRect)
        {
            int cropWidth = Math.Max(1, (int)Math.Round(srcRect.Width));
            int cropHeight = Math.Max(1, (int)Math.Round(srcRect.Height));
            Bitmap cropped = new Bitmap(cropWidth, cropHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(cropped))
            using (ImageAttributes attrs = new ImageAttributes())
            {
                ConfigureHighQualityGraphics(g);
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.Clear(Color.Transparent);
                g.DrawImage(source, new Rectangle(0, 0, cropWidth, cropHeight), srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, attrs);
            }

            return cropped;
        }

        private static Bitmap RenderResizedBitmap(Bitmap source, RectangleF srcRect, int width, int height, bool letterbox)
        {
            Bitmap working = ExtractRegion(source, srcRect);
            Bitmap current = working;
            try
            {
                while (current.Width / 2 >= width && current.Height / 2 >= height)
                {
                    int nextWidth = Math.Max(width, current.Width / 2);
                    int nextHeight = Math.Max(height, current.Height / 2);
                    Bitmap next = new Bitmap(nextWidth, nextHeight, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(next))
                    using (ImageAttributes attrs = new ImageAttributes())
                    {
                        ConfigureHighQualityGraphics(g);
                        attrs.SetWrapMode(WrapMode.TileFlipXY);
                        g.Clear(Color.Transparent);
                        g.DrawImage(current, new Rectangle(0, 0, nextWidth, nextHeight), 0, 0, current.Width, current.Height, GraphicsUnit.Pixel, attrs);
                    }

                    if (!Object.ReferenceEquals(current, working))
                        current.Dispose();
                    current = next;
                }

                Bitmap target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(target))
                using (ImageAttributes attrs = new ImageAttributes())
                {
                    ConfigureHighQualityGraphics(g);
                    attrs.SetWrapMode(WrapMode.TileFlipXY);
                    g.Clear(Color.Black);
                    Rectangle dest = new Rectangle(0, 0, width, height);
                    if (letterbox)
                    {
                        float sx = width / (float)current.Width;
                        float sy = height / (float)current.Height;
                        float scale = Math.Min(sx, sy);
                        int drawWidth = Math.Max(1, (int)Math.Round(current.Width * scale));
                        int drawHeight = Math.Max(1, (int)Math.Round(current.Height * scale));
                        dest = new Rectangle((width - drawWidth) / 2, (height - drawHeight) / 2, drawWidth, drawHeight);
                    }

                    g.DrawImage(current, dest, 0, 0, current.Width, current.Height, GraphicsUnit.Pixel, attrs);
                }

                return target;
            }
            finally
            {
                if (current != null && !Object.ReferenceEquals(current, working))
                    current.Dispose();
                if (working != null)
                    working.Dispose();
            }
        }

        private static bool ReplaceTplMenuFitIfExists(ArchiveEntry timg, string name, Bitmap fullFrame, Bitmap topFrame, Bitmap bottomFrame, int fittingMode, CommonTextureLayout layout, List<string> diagnostics)
        {
            ArchiveEntry entry = timg.FindChild(name);
            if (entry == null || entry.IsDirectory)
                return false;
            TplTextureInfo info = TplTextureEditor.GetFirstImageInfo(entry.Data);
            // Retro Rewind's title_image_common source images in the supplied archive are
            // physically 832x316 and represent the lower part of the same 832x456 title.
            // Feeding fullFrame here caused a seam bug: it compressed all 456 rows into
            // 316 rows. Select the already-cropped source first, then apply any authored UVs.
            Bitmap source = fullFrame;
            string sourceLabel = "full 832x456";
            if (info.Width == 832 && info.Height == 316 && bottomFrame != null)
            {
                source = bottomFrame;
                sourceLabel = "BOTTOM crop 832x316 (y=140..455)";
            }
            else if (info.Width == 832 && info.Height == 140 && topFrame != null)
            {
                source = topFrame;
                sourceLabel = "TOP crop 832x140 (y=0..139)";
            }

            string diagnostic;
            using (Bitmap resized = PrepareMenuFrame(source, entry.Data, info, fittingMode, layout, name, out diagnostic))
                entry.Data = TplTextureEditor.ReplaceFirstImage(entry.Data, resized, false);
            if (diagnostics != null && !String.IsNullOrWhiteSpace(diagnostic))
                diagnostics.Add(sourceLabel + " | " + diagnostic);
            return true;
        }

        private static void CleanupOldCommonGifFiles(ArchiveEntry timg)
        {
            if (timg == null || !timg.IsDirectory)
                return;
            int i;
            for (i = timg.Children.Count - 1; i >= 0; i--)
            {
                ArchiveEntry child = timg.Children[i];
                if (child == null || child.IsDirectory)
                    continue;
                string n = child.Name ?? "";
                if (n.StartsWith("mwms_rr_common_", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
                    timg.Children.RemoveAt(i);
            }
        }

        private static void RemoveGeneratedCommonTextureNames(List<string> textures)
        {
            int i;
            for (i = textures.Count - 1; i >= 0; i--)
            {
                string n = textures[i] ?? "";
                if (n.StartsWith("mwms_rr_common_", StringComparison.OrdinalIgnoreCase))
                    textures.RemoveAt(i);
            }
        }

        private static void RepeatExistingLoopKeys(BrlanDocument doc, ushort oldFrames, ushort newFrames)
        {
            if (doc == null || doc.Pai == null || oldFrames == 0 || newFrames <= oldFrames)
                return;
            int a;
            for (a = 0; a < doc.Pai.Animations.Count; a++)
            {
                AnimationModel animation = doc.Pai.Animations[a];
                int t;
                for (t = 0; t < animation.Tags.Count; t++)
                {
                    TagModel tag = animation.Tags[t];
                    if (tag.RawOnly)
                        continue;
                    int e;
                    for (e = 0; e < tag.Entries.Count; e++)
                    {
                        EntryModel entry = tag.Entries[e];
                        if (entry.Keys.Count == 0)
                            continue;
                        List<KeyframeModel> original = new List<KeyframeModel>();
                        int k;
                        for (k = 0; k < entry.Keys.Count; k++)
                            original.Add(entry.Keys[k].Clone());
                        entry.Keys.Clear();
                        HashSet<int> usedFrames = new HashSet<int>();
                        int cycle = 0;
                        while (cycle * (int)oldFrames < newFrames)
                        {
                            float offset = cycle * (float)oldFrames;
                            for (k = 0; k < original.Count; k++)
                            {
                                float frame = original[k].Frame + offset;
                                if (frame < -0.001f || frame >= newFrames - 0.0001f)
                                    continue;
                                int quantized = (int)Math.Round(frame * 1000.0f);
                                if (!usedFrames.Add(quantized))
                                    continue;
                                KeyframeModel copy = original[k].Clone();
                                copy.Frame = frame;
                                entry.Keys.Add(copy);
                            }

                            cycle++;
                        }

                        entry.Keys.Sort(delegate (KeyframeModel x, KeyframeModel y)
                        {
                            return x.Frame.CompareTo(y.Frame);
                        });
                    }
                }
            }
        }

        private static void RemoveObsoleteCommonRltpAnimations(BrlanDocument doc, BrlytLayoutMap commonMap)
        {
            if (doc == null || doc.Pai == null || commonMap == null)
                return;
            HashSet<string> commonMaterials = new HashSet<string>(StringComparer.Ordinal);
            int i;
            for (i = 0; i < commonMap.Bindings.Count; i++)
            {
                string material = commonMap.Bindings[i].MaterialName ?? "";
                if (!String.IsNullOrWhiteSpace(material))
                    commonMaterials.Add(material);
            }

            for (i = doc.Pai.Animations.Count - 1; i >= 0; i--)
            {
                AnimationModel animation = doc.Pai.Animations[i];
                if (animation == null || animation.TargetKind != 1 || !commonMaterials.Contains(animation.Name ?? ""))
                    continue;
                int t;
                for (t = animation.Tags.Count - 1; t >= 0; t--)
                {
                    TagModel tag = animation.Tags[t];
                    if (tag != null && String.Equals(tag.Magic, "RLTP", StringComparison.Ordinal))
                        animation.Tags.RemoveAt(t);
                }

                if (animation.Tags.Count == 0)
                    doc.Pai.Animations.RemoveAt(i);
            }
        }

        private static BrctrInfo ReadBrctrInfo(byte[] data)
        {
            if (data == null || data.Length < 0x14 || ReadAscii(data, 0, 4) != "bctr")
                throw new InvalidDataException(L.T("TitleImage.brctr ist keine gültige BRCTR-Datei.", "TitleImage.brctr is not a valid BRCTR file."));
            int nameTable = ReadU16BE(data, 0x10);
            if (nameTable <= 0 || nameTable >= data.Length)
                throw new InvalidDataException(L.T("TitleImage.brctr hat eine ungültige Namenstabelle.", "TitleImage.brctr has an invalid name table."));
            BrctrInfo info = new BrctrInfo();
            info.MainLayoutName = ReadBrctrName(data, nameTable, ReadU16BE(data, 0x06));
            info.PictureSourceLayoutName = ReadBrctrName(data, nameTable, ReadU16BE(data, 0x0A));
            int animHeader = ReadU16BE(data, 0x0C);
            if (animHeader > 0 && animHeader + 8 <= data.Length)
            {
                int firstGroup = ReadU16BE(data, animHeader + 0x00);
                int groupCount = ReadU16BE(data, animHeader + 0x02);
                int groupBase = animHeader + firstGroup;
                int g;
                for (g = 0; g < groupCount; g++)
                {
                    int pos = groupBase + g * 0x08;
                    if (pos < 0 || pos + 0x08 > data.Length)
                        break;
                    string group = ReadBrctrName(data, nameTable, ReadU16BE(data, pos + 0x00));
                    string layoutGroup = ReadBrctrName(data, nameTable, ReadU16BE(data, pos + 0x02));
                    if (!String.IsNullOrWhiteSpace(group) && !info.AnimationGroupNames.Contains(group))
                        info.AnimationGroupNames.Add(group);
                    if (!String.IsNullOrWhiteSpace(layoutGroup) && !info.AnimationLayoutGroupNames.Contains(layoutGroup))
                        info.AnimationLayoutGroupNames.Add(layoutGroup);
                }

                int firstAnimation = ReadU16BE(data, animHeader + 0x04);
                int animationCount = ReadU16BE(data, animHeader + 0x06);
                int baseOffset = animHeader + firstAnimation;
                int i;
                for (i = 0; i < animationCount; i++)
                {
                    int pos = baseOffset + i * 0x0C;
                    if (pos < 0 || pos + 0x0C > data.Length)
                        break;
                    string brlan = ReadBrctrName(data, nameTable, ReadU16BE(data, pos + 0x02));
                    if (!String.IsNullOrWhiteSpace(brlan) && !info.AnimationBrlanNames.Contains(brlan))
                        info.AnimationBrlanNames.Add(brlan);
                }
            }

            return info;
        }

        private static string SelectControllerAnimationHost(List<string> names)
        {
            if (names == null || names.Count == 0)
                return "";
            // TitleImage.brctr in Retro Rewind binds title_off/title_off_to_on/title_on.
            // title_on is the steady visible state and is the correct place for a
            // looping background pattern. Do not stretch transition BRLANs such as
            // title_off_to_on because doing so would also stretch the UI transition.
            int i;
            for (i = 0; i < names.Count; i++)
                if (ContainsBrlanName(names[i], "title_on"))
                    return names[i];
            // Some packs may explicitly bind the existing common light loop. It is
            // safe to use only when the controller itself lists it.
            for (i = 0; i < names.Count; i++)
                if (ContainsBrlanName(names[i], "common_w002_title_light_loop"))
                    return names[i];
            // Last conservative fallback: an explicitly bound BRLAN whose basename
            // ends in _on, but never an *_off_to_on transition.
            for (i = 0; i < names.Count; i++)
            {
                string stem = BrlanStem(names[i]);
                if (stem.EndsWith("_on", StringComparison.OrdinalIgnoreCase) && stem.IndexOf("off_to_on", StringComparison.OrdinalIgnoreCase) < 0)
                    return names[i];
            }

            return "";
        }

        private static ArchiveEntry FindBrlanByControllerName(ArchiveEntry animFolder, string controllerName)
        {
            if (animFolder == null || !animFolder.IsDirectory || String.IsNullOrWhiteSpace(controllerName))
                return null;
            string wanted = NormalizeBrlanFileName(controllerName);
            ArchiveEntry exact = animFolder.FindChild(wanted);
            if (exact != null && !exact.IsDirectory)
                return exact;
            string wantedStem = Path.GetFileNameWithoutExtension(wanted);
            int i;
            for (i = 0; i < animFolder.Children.Count; i++)
            {
                ArchiveEntry child = animFolder.Children[i];
                if (child == null || child.IsDirectory)
                    continue;
                string stem = Path.GetFileNameWithoutExtension(child.Name ?? "");
                if (String.Equals(stem, wantedStem, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }

        private static string NormalizeBrlanFileName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "";
            string file = value.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            if (slash >= 0)
                file = file.Substring(slash + 1);
            if (!file.EndsWith(".brlan", StringComparison.OrdinalIgnoreCase))
                file += ".brlan";
            return file;
        }

        private static string BrlanStem(string value)
        {
            string file = NormalizeBrlanFileName(value);
            return Path.GetFileNameWithoutExtension(file) ?? "";
        }

        private static bool ContainsLayoutName(string value, string stem)
        {
            if (String.IsNullOrWhiteSpace(value) || String.IsNullOrWhiteSpace(stem))
                return false;
            string file = value.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            if (slash >= 0)
                file = file.Substring(slash + 1);
            string noExt = Path.GetFileNameWithoutExtension(file);
            return String.Equals(noExt, stem, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsBrlanName(string value, string stem)
        {
            if (String.IsNullOrWhiteSpace(value) || String.IsNullOrWhiteSpace(stem))
                return false;
            string file = value.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            if (slash >= 0)
                file = file.Substring(slash + 1);
            string noExt = Path.GetFileNameWithoutExtension(file);
            return String.Equals(noExt, stem, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadBrctrName(byte[] data, int nameTable, int relativeOffset)
        {
            if (relativeOffset == 0)
                return "";
            int offset = nameTable + relativeOffset;
            if (offset < nameTable || offset >= data.Length)
                return "";
            int end = offset;
            while (end < data.Length && data[end] != 0)
                end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (Object.ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int i;
            for (i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        private static ushort ReadU16BE(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 2 > data.Length)
                return 0;
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static string ReadAscii(byte[] data, int offset, int length)
        {
            if (data == null || offset < 0 || length < 0 || offset + length > data.Length)
                return "";
            return Encoding.ASCII.GetString(data, offset, length);
        }

        private static void ValidateGeneratedCommon(BrlanDocument doc, ArchiveEntry timg, List<CommonTarget> targets, List<CommonFrameGroup> groups)
        {
            if (doc == null || doc.Pai == null)
                throw new InvalidDataException("Common BRLAN validation failed.");
            if (doc.Pai.Frames == 0)
                throw new InvalidDataException("Common BRLAN frame count is zero.");
            int g;
            for (g = 0; g < groups.Count; g++)
            {
                int n;
                for (n = 0; n < groups[g].Names.Count; n++)
                    if (timg.FindChild(groups[g].Names[n]) == null)
                        throw new InvalidDataException("Missing generated common TPL: " + groups[g].Names[n]);
            }

            int i;
            for (i = 0; i < targets.Count; i++)
            {
                bool found = false;
                int a;
                for (a = 0; a < doc.Pai.Animations.Count; a++)
                {
                    AnimationModel animation = doc.Pai.Animations[a];
                    if (animation.TargetKind == 1 && String.Equals(animation.Name, targets[i].MaterialName, StringComparison.Ordinal) && HasRltp(animation))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new InvalidDataException(L.T("Menü-RLTP-Validierung fehlgeschlagen für Material: ", "Menu RLTP validation failed for material: ") + targets[i].MaterialName);
            }
        }

        private static bool BrlytHasGroup(byte[] data, string groupName)
        {
            if (data == null || data.Length < 0x10 || String.IsNullOrWhiteSpace(groupName) || ReadAscii(data, 0, 4) != "RLYT")
                return false;
            if (ReadU16BE(data, 4) != 0xFEFF)
                return false;
            int headerSize = ReadU16BE(data, 0x0C);
            int sectionCount = ReadU16BE(data, 0x0E);
            int pos = headerSize;
            int i;
            for (i = 0; i < sectionCount; i++)
            {
                if (pos < 0 || pos + 8 > data.Length)
                    return false;
                string magic = ReadAscii(data, pos, 4);
                int size = checked((int)ReadU32BE(data, pos + 4));
                if (size < 8 || pos + size > data.Length)
                    return false;
                if (magic == "grp1" && size >= 0x1C && String.Equals(ReadFixedCString(data, pos + 8, 16), groupName, StringComparison.Ordinal))
                    return true;
                pos += size;
            }

            return false;
        }

        private static bool BrlytGroupContainsPane(byte[] data, string groupName, string paneName)
        {
            if (data == null || data.Length < 0x10 || String.IsNullOrWhiteSpace(groupName) || String.IsNullOrWhiteSpace(paneName) || ReadAscii(data, 0, 4) != "RLYT")
                return false;
            if (ReadU16BE(data, 4) != 0xFEFF)
                return false;
            int headerSize = ReadU16BE(data, 0x0C);
            int sectionCount = ReadU16BE(data, 0x0E);
            int pos = headerSize;
            int i;
            for (i = 0; i < sectionCount; i++)
            {
                if (pos < 0 || pos + 8 > data.Length)
                    return false;
                string magic = ReadAscii(data, pos, 4);
                int size = checked((int)ReadU32BE(data, pos + 4));
                if (size < 8 || pos + size > data.Length)
                    return false;
                if (magic == "grp1" && size >= 0x1C && String.Equals(ReadFixedCString(data, pos + 8, 16), groupName, StringComparison.Ordinal))
                {
                    int count = ReadU16BE(data, pos + 0x18);
                    int required = 0x1C + count * 16;
                    if (required > size)
                        return false;
                    int n;
                    for (n = 0; n < count; n++)
                        if (String.Equals(ReadFixedCString(data, pos + 0x1C + n * 16, 16), paneName, StringComparison.Ordinal))
                            return true;
                    return false;
                }

                pos += size;
            }

            return false;
        }

        private static byte[] EnsureBrlytGroupContainsPanes(byte[] data, string groupName, string[] paneNames)
        {
            if (data == null || data.Length < 0x10 || ReadAscii(data, 0, 4) != "RLYT")
                throw new InvalidDataException("title.brlyt is not a valid BRLYT.");
            if (ReadU16BE(data, 4) != 0xFEFF)
                throw new InvalidDataException("title.brlyt must use the Wii big-endian BRLYT byte order.");
            if (String.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("BRLYT group name is empty.", "groupName");
            int headerSize = ReadU16BE(data, 0x0C);
            int sectionCount = ReadU16BE(data, 0x0E);
            int pos = headerSize;
            int targetPos = -1;
            int targetSize = 0;
            int targetCount = 0;
            int i;
            for (i = 0; i < sectionCount; i++)
            {
                if (pos < 0 || pos + 8 > data.Length)
                    throw new InvalidDataException("title.brlyt section table is truncated.");
                string magic = ReadAscii(data, pos, 4);
                int size = checked((int)ReadU32BE(data, pos + 4));
                if (size < 8 || pos + size > data.Length)
                    throw new InvalidDataException("title.brlyt has an invalid section size.");
                if (magic == "grp1" && size >= 0x1C && String.Equals(ReadFixedCString(data, pos + 8, 16), groupName, StringComparison.Ordinal))
                {
                    targetPos = pos;
                    targetSize = size;
                    targetCount = ReadU16BE(data, pos + 0x18);
                    break;
                }

                pos += size;
            }

            if (targetPos < 0)
                throw new InvalidDataException("BRLYT animation group was not found: " + groupName);
            int memberEnd = 0x1C + targetCount * 16;
            if (memberEnd > targetSize)
                throw new InvalidDataException("BRLYT animation group member table is truncated: " + groupName);
            List<string> missing = new List<string>();
            if (paneNames != null)
            {
                for (i = 0; i < paneNames.Length; i++)
                {
                    string pane = paneNames[i] ?? "";
                    if (String.IsNullOrWhiteSpace(pane))
                        continue;
                    bool exists = false;
                    int n;
                    for (n = 0; n < targetCount; n++)
                    {
                        if (String.Equals(ReadFixedCString(data, targetPos + 0x1C + n * 16, 16), pane, StringComparison.Ordinal))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists && !missing.Contains(pane))
                        missing.Add(pane);
                }
            }

            if (missing.Count == 0)
                return (byte[])data.Clone();
            if (targetCount + missing.Count > 65535)
                throw new InvalidDataException("Too many BRLYT animation group members.");
            int insertAt = targetPos + memberEnd;
            int extra = missing.Count * 16;
            byte[] output = new byte[data.Length + extra];
            Buffer.BlockCopy(data, 0, output, 0, insertAt);
            for (i = 0; i < missing.Count; i++)
                WriteFixedAsciiBytes(output, insertAt + i * 16, 16, missing[i]);
            Buffer.BlockCopy(data, insertAt, output, insertAt + extra, data.Length - insertAt);
            WriteU16BE(output, targetPos + 0x18, (ushort)(targetCount + missing.Count));
            WriteU32BE(output, targetPos + 4, (uint)(targetSize + extra));
            WriteU32BE(output, 8, (uint)output.Length);
            return output;
        }

        private const string TitleBackLoopGroup = "mwms_rr_loop";
        private const string TitleBackLoopBrlan = "mwms_rr_titleback_loop.brlan";
        private static byte[] BuildTitleBackLoopBrlan(byte[] template, ushort totalFrames, List<float> keyFrames, List<string> topNames, List<string> bottomNames)
        {
            BrlanDocument doc = BrlanCodec.Parse(template);
            if (doc.Pai == null)
                throw new InvalidDataException("TitleBack loop template has no pai1 section.");
            doc.Pai.Textures.Clear();
            doc.Pai.Animations.Clear();
            doc.Pai.Frames = totalFrames;
            doc.Pai.Flags = 1;
            List<ushort> topIndices = AppendTextureNames(doc.Pai.Textures, topNames);
            List<ushort> bottomIndices = AppendTextureNames(doc.Pai.Textures, bottomNames);
            ApplyRltp(doc, "title", 0, keyFrames, topIndices);
            ApplyRltp(doc, "chara", 0, keyFrames, bottomIndices);
            return BrlanCodec.Build(doc);
        }

        private static void WriteOrReplaceFile(ArchiveEntry folder, string name, byte[] data)
        {
            if (folder == null || !folder.IsDirectory)
                throw new InvalidDataException("Target archive folder is missing.");
            ArchiveEntry file = folder.FindChild(name);
            if (file == null)
            {
                file = new ArchiveEntry(name, false);
                folder.AddChild(file);
            }

            if (file.IsDirectory)
                throw new InvalidDataException("Generated file name collides with a directory: " + name);
            file.Data = data == null ? new byte[0] : data;
        }

        private static byte[] EnsureTitleBackBrlanGroup(byte[] data)
        {
            if (data == null || data.Length < 0x10 || ReadAscii(data, 0, 4) != "RLYT")
                throw new InvalidDataException("title_boke.brlyt is not a valid BRLYT.");
            if (ReadU16BE(data, 4) != 0xFEFF)
                throw new InvalidDataException("title_boke.brlyt must use the Wii big-endian BRLYT byte order.");
            int headerSize = ReadU16BE(data, 0x0C);
            int sectionCount = ReadU16BE(data, 0x0E);
            int pos = headerSize;
            string lastMagic = "";
            string lastGroupName = "";
            int i;
            for (i = 0; i < sectionCount; i++)
            {
                if (pos < 0 || pos + 8 > data.Length)
                    throw new InvalidDataException("title_boke.brlyt section table is truncated.");
                string magic = ReadAscii(data, pos, 4);
                int size = checked((int)ReadU32BE(data, pos + 4));
                if (size < 8 || pos + size > data.Length)
                    throw new InvalidDataException("title_boke.brlyt has an invalid section size.");
                if (magic == "grp1" && size >= 0x1C)
                {
                    string groupName = ReadFixedCString(data, pos + 8, 16);
                    if (String.Equals(groupName, TitleBackLoopGroup, StringComparison.Ordinal))
                        return (byte[])data.Clone();
                    lastGroupName = groupName;
                }

                lastMagic = magic;
                pos += size;
            }

            if (pos != data.Length)
                throw new InvalidDataException("title_boke.brlyt contains unsupported trailing bytes.");
            if (lastMagic != "grp1" || !String.Equals(lastGroupName, "RootGroup", StringComparison.Ordinal))
                throw new InvalidDataException("title_boke.brlyt does not end in the expected RootGroup; refusing to inject an animation group.");
            string[] panes = new string[]
            {
                "title",
                "chara"
            };
            byte[] grs = new byte[8];
            WriteAsciiBytes(grs, 0, "grs1");
            WriteU32BE(grs, 4, 8);
            int groupSize = 0x1C + panes.Length * 16;
            byte[] grp = new byte[groupSize];
            WriteAsciiBytes(grp, 0, "grp1");
            WriteU32BE(grp, 4, (uint)groupSize);
            WriteFixedAsciiBytes(grp, 8, 16, TitleBackLoopGroup);
            WriteU16BE(grp, 0x18, (ushort)panes.Length);
            WriteU16BE(grp, 0x1A, 0);
            for (i = 0; i < panes.Length; i++)
                WriteFixedAsciiBytes(grp, 0x1C + i * 16, 16, panes[i]);
            byte[] gre = new byte[8];
            WriteAsciiBytes(gre, 0, "gre1");
            WriteU32BE(gre, 4, 8);
            byte[] output = new byte[data.Length + grs.Length + grp.Length + gre.Length];
            Buffer.BlockCopy(data, 0, output, 0, data.Length);
            int dst = data.Length;
            Buffer.BlockCopy(grs, 0, output, dst, grs.Length);
            dst += grs.Length;
            Buffer.BlockCopy(grp, 0, output, dst, grp.Length);
            dst += grp.Length;
            Buffer.BlockCopy(gre, 0, output, dst, gre.Length);
            WriteU32BE(output, 8, (uint)output.Length);
            WriteU16BE(output, 0x0E, (ushort)(sectionCount + 3));
            return output;
        }

        private static byte[] PatchTitleBackControllerForLoop(byte[] data, string brlanName)
        {
            BrctrInfo info = ReadBrctrInfo(data);
            if (!ContainsLayoutName(info.MainLayoutName, "title_boke") || !ContainsLayoutName(info.PictureSourceLayoutName, "title_image_common"))
                throw new InvalidDataException("TitleBack.brctr does not match the expected title_boke + title_image_common contract.");
            if (ContainsBrlanNameInList(info.AnimationBrlanNames, Path.GetFileNameWithoutExtension(brlanName)))
                return (byte[])data.Clone();
            if (info.AnimationBrlanNames.Count != 0)
                throw new InvalidDataException("TitleBack.brctr already contains unknown animation bindings; refusing to overwrite them.");
            int nameTable = ReadU16BE(data, 0x10);
            string main = ReadBrctrName(data, nameTable, ReadU16BE(data, 0x06));
            string bmg = ReadBrctrName(data, nameTable, ReadU16BE(data, 0x08));
            string picture = ReadBrctrName(data, nameTable, ReadU16BE(data, 0x0A));
            ushort unknownHeader = ReadU16BE(data, 0x12);
            int layout = ReadU16BE(data, 0x0E);
            if (layout <= 0 || layout + 12 > data.Length)
                throw new InvalidDataException("TitleBack.brctr has no valid layout sub-header.");
            int firstVariant = ReadU16BE(data, layout + 0x00);
            int variantCount = ReadU16BE(data, layout + 0x02);
            int messageCount = ReadU16BE(data, layout + 0x06);
            int pictureCount = ReadU16BE(data, layout + 0x0A);
            if (variantCount != 1 || messageCount != 0 || pictureCount != 0)
                throw new InvalidDataException("TitleBack.brctr layout structure differs from the supported Retro Rewind controller.");
            int variantPos = layout + firstVariant;
            if (variantPos < 0 || variantPos + 0x3C > data.Length)
                throw new InvalidDataException("TitleBack.brctr variant is truncated.");
            byte[] variant = new byte[0x3C];
            Buffer.BlockCopy(data, variantPos, variant, 0, variant.Length);
            string variantName = ReadBrctrName(data, nameTable, ReadU16BE(variant, 0));
            MemoryStream names = new MemoryStream();
            names.WriteByte(0);
            Dictionary<string, ushort> offsets = new Dictionary<string, ushort>(StringComparer.Ordinal);
            offsets[""] = 0;
            ushort mainOff = AppendBrctrName(names, offsets, main);
            ushort bmgOff = AppendBrctrName(names, offsets, bmg);
            ushort pictureOff = AppendBrctrName(names, offsets, picture);
            ushort variantOff = AppendBrctrName(names, offsets, variantName);
            ushort groupOff = AppendBrctrName(names, offsets, "eAFLoop");
            ushort paneGroupOff = AppendBrctrName(names, offsets, TitleBackLoopGroup);
            ushort animationOff = AppendBrctrName(names, offsets, "Loop");
            ushort brlanOff = AppendBrctrName(names, offsets, brlanName);
            const int animHeader = 0x14;
            const int groupPos = animHeader + 0x08;
            const int animationPos = animHeader + 0x10;
            const int layoutPos = animHeader + 0x1C;
            const int variantOutPos = layoutPos + 0x0C;
            const int nameOutPos = variantOutPos + 0x3C;
            byte[] nameBytes = names.ToArray();
            byte[] output = new byte[nameOutPos + nameBytes.Length];
            WriteAsciiBytes(output, 0, "bctr");
            WriteU16BE(output, 0x04, 2);
            WriteU16BE(output, 0x06, mainOff);
            WriteU16BE(output, 0x08, bmgOff);
            WriteU16BE(output, 0x0A, pictureOff);
            WriteU16BE(output, 0x0C, animHeader);
            WriteU16BE(output, 0x0E, layoutPos);
            WriteU16BE(output, 0x10, nameOutPos);
            WriteU16BE(output, 0x12, unknownHeader);
            WriteU16BE(output, animHeader + 0x00, 0x08);
            WriteU16BE(output, animHeader + 0x02, 1);
            WriteU16BE(output, animHeader + 0x04, 0x10);
            WriteU16BE(output, animHeader + 0x06, 1);
            WriteU16BE(output, groupPos + 0x00, groupOff);
            WriteU16BE(output, groupPos + 0x02, paneGroupOff);
            WriteU16BE(output, groupPos + 0x04, 0);
            WriteU16BE(output, groupPos + 0x06, 1);
            WriteU16BE(output, animationPos + 0x00, animationOff);
            WriteU16BE(output, animationPos + 0x02, brlanOff);
            WriteU16BE(output, animationPos + 0x04, 0);
            WriteU16BE(output, animationPos + 0x06, 0);
            WriteU32BE(output, animationPos + 0x08, 0x3F800000u);
            WriteU16BE(output, layoutPos + 0x00, 0x0C);
            WriteU16BE(output, layoutPos + 0x02, 1);
            WriteU16BE(output, layoutPos + 0x04, 0x48);
            WriteU16BE(output, layoutPos + 0x06, 0);
            WriteU16BE(output, layoutPos + 0x08, 0x48);
            WriteU16BE(output, layoutPos + 0x0A, 0);
            Buffer.BlockCopy(variant, 0, output, variantOutPos, variant.Length);
            WriteU16BE(output, variantOutPos, variantOff);
            Buffer.BlockCopy(nameBytes, 0, output, nameOutPos, nameBytes.Length);
            return output;
        }

        private static ushort AppendBrctrName(MemoryStream names, Dictionary<string, ushort> offsets, string value)
        {
            value = value ?? "";
            ushort existing;
            if (offsets.TryGetValue(value, out existing))
                return existing;
            if (names.Length > 65534)
                throw new InvalidDataException("BRCTR name table is too large.");
            ushort offset = (ushort)names.Length;
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            names.Write(bytes, 0, bytes.Length);
            names.WriteByte(0);
            offsets[value] = offset;
            return offset;
        }

        private static bool ContainsBrlanNameInList(List<string> values, string stem)
        {
            if (values == null)
                return false;
            int i;
            for (i = 0; i < values.Count; i++)
                if (ContainsBrlanName(values[i], stem))
                    return true;
            return false;
        }

        private static void ValidateTitleBackLoop(byte[] brctr, byte[] brlyt, byte[] brlan, ArchiveEntry timg, List<string> topNames, List<string> bottomNames)
        {
            BrctrInfo info = ReadBrctrInfo(brctr);
            if (!ContainsLayoutName(info.MainLayoutName, "title_boke") || !ContainsLayoutName(info.PictureSourceLayoutName, "title_image_common"))
                throw new InvalidDataException("TitleBack loop validation failed: controller layout contract changed.");
            if (!info.AnimationGroupNames.Contains("eAFLoop"))
                throw new InvalidDataException("TitleBack loop validation failed: controller does not expose the standard eAFLoop group.");
            if (!ContainsBrlanNameInList(info.AnimationBrlanNames, "mwms_rr_titleback_loop"))
                throw new InvalidDataException("TitleBack loop validation failed: controller does not bind the generated BRLAN.");
            if (!BrlytHasGroup(brlyt, TitleBackLoopGroup))
                throw new InvalidDataException("TitleBack loop validation failed: BRLYT animation group is missing.");
            BrlanDocument doc = BrlanCodec.Parse(brlan);
            if (doc.Pai == null || doc.Pai.Frames == 0)
                throw new InvalidDataException("TitleBack loop validation failed: BRLAN has no usable pai1 timeline.");
            bool title = false, chara = false;
            int a;
            for (a = 0; a < doc.Pai.Animations.Count; a++)
            {
                AnimationModel animation = doc.Pai.Animations[a];
                if (animation.TargetKind != 1 || !HasRltp(animation))
                    continue;
                if (String.Equals(animation.Name, "title", StringComparison.Ordinal))
                    title = true;
                if (String.Equals(animation.Name, "chara", StringComparison.Ordinal))
                    chara = true;
            }

            if (!title || !chara)
                throw new InvalidDataException("TitleBack loop validation failed: title/chara RLTP targets are missing.");
            int i;
            for (i = 0; i < topNames.Count; i++)
                if (timg.FindChild(topNames[i]) == null)
                    throw new InvalidDataException("Missing generated top TPL for TitleBack: " + topNames[i]);
            for (i = 0; i < bottomNames.Count; i++)
                if (timg.FindChild(bottomNames[i]) == null)
                    throw new InvalidDataException("Missing generated bottom TPL for TitleBack: " + bottomNames[i]);
        }

        private static string ReadFixedCString(byte[] data, int offset, int length)
        {
            if (data == null || offset < 0 || length < 0 || offset + length > data.Length)
                return "";
            int end = offset;
            int max = offset + length;
            while (end < max && data[end] != 0)
                end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static uint ReadU32BE(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 4 > data.Length)
                return 0;
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteU16BE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }

        private static void WriteU32BE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        private static void WriteAsciiBytes(byte[] data, int offset, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value ?? "");
            Buffer.BlockCopy(bytes, 0, data, offset, bytes.Length);
        }

        private static void WriteFixedAsciiBytes(byte[] data, int offset, int length, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value ?? "");
            if (bytes.Length > length)
                throw new InvalidDataException("ASCII field is too long: " + value);
            Buffer.BlockCopy(bytes, 0, data, offset, bytes.Length);
        }

        private static List<SelectedSourceFrame> LoadSelectedFrames(string path, int every)
        {
            List<SelectedSourceFrame> frames = new List<SelectedSourceFrame>();
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            if (ext != ".gif")
            {
                using (Image img = Image.FromFile(path))
                {
                    Bitmap frame = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(img, 0, 0);
                    }

                    SelectedSourceFrame selected = new SelectedSourceFrame();
                    selected.Bitmap = frame;
                    // A static PNG/JPEG only needs a one-frame BRLAN texture binding.
                    selected.DurationMs = 16;
                    frames.Add(selected);
                }

                return frames;
            }

            using (Image img = Image.FromFile(path))
            {
                FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
                int count = img.GetFrameCount(dim);
                int[] durations = ReadGifDurations(img, count);
                int start;
                for (start = 0; start < count; start += every)
                {
                    int end = Math.Min(count, start + every);
                    int groupMs = 0;
                    int q;
                    for (q = start; q < end; q++)
                        groupMs += durations[q];
                    img.SelectActiveFrame(dim, start);
                    Bitmap frame = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(img, 0, 0);
                    }

                    SelectedSourceFrame selected = new SelectedSourceFrame();
                    selected.Bitmap = frame;
                    selected.DurationMs = Math.Max(10, groupMs);
                    frames.Add(selected);
                }
            }

            return frames;
        }

        private static int[] ReadGifDurations(Image img, int count)
        {
            int[] durations = new int[count];
            int i;
            for (i = 0; i < count; i++)
                durations[i] = 100;
            try
            {
                PropertyItem item = img.GetPropertyItem(0x5100);
                byte[] data = item.Value;
                for (i = 0; i < count && i * 4 + 3 < data.Length; i++)
                {
                    int hundredths = BitConverter.ToInt32(data, i * 4);
                    durations[i] = Math.Max(10, hundredths * 10);
                }
            }
            catch
            {
            }

            return durations;
        }

        private static Bitmap ResizeFrame(Bitmap source, int width, int height, bool fillCrop)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            RectangleF srcRect = ComputeResizeSourceRect(source.Width, source.Height, width, height, fillCrop);
            return RenderResizedBitmap(source, srcRect, width, height, !fillCrop);
        }

        private static Bitmap Crop(Bitmap source, Rectangle rect)
        {
            Bitmap result = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
                g.DrawImage(source, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            return result;
        }

        private static BrlytLayoutMap LoadBrlytMap(byte[] data, string fileName)
        {
            string temp = Path.Combine(Path.GetTempPath(), "murums_rr_" + Guid.NewGuid().ToString("N") + "_" + fileName);
            try
            {
                File.WriteAllBytes(temp, data ?? new byte[0]);
                return BrlytInspector.Load(temp);
            }
            finally
            {
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch
                {
                }
            }
        }

        private static ArchiveEntry FindEntry(ArchiveEntry root, params string[] parts)
        {
            ArchiveEntry current = root;
            // Originalarchive können ihre Inhalte unter einem zusätzlichen Punktordner ablegen.
            while (current != null && current.IsDirectory && current.Children.Count == 1
                && current.Children[0].IsDirectory && current.Children[0].Name == ".")
                current = current.Children[0];
            int i;
            for (i = 0; i < parts.Length; i++)
            {
                if (current == null || !current.IsDirectory)
                    return null;
                current = current.FindChild(parts[i]);
            }

            return current;
        }

        private static void CleanupOldTitleGifFiles(ArchiveEntry timg, string topBase, string bottomBase)
        {
            string topPrefix = Path.GetFileNameWithoutExtension(topBase ?? "") + "_anim_";
            string bottomPrefix = Path.GetFileNameWithoutExtension(bottomBase ?? "") + "_anim_";
            int i;
            for (i = timg.Children.Count - 1; i >= 0; i--)
            {
                ArchiveEntry child = timg.Children[i];
                if (child == null || child.IsDirectory)
                    continue;
                string n = child.Name ?? "";
                bool ours = n.StartsWith("mwms_rr_top_", StringComparison.OrdinalIgnoreCase) || n.StartsWith("mwms_rr_bottom_", StringComparison.OrdinalIgnoreCase) || (!String.IsNullOrEmpty(topPrefix) && n.StartsWith(topPrefix, StringComparison.OrdinalIgnoreCase)) || (!String.IsNullOrEmpty(bottomPrefix) && n.StartsWith(bottomPrefix, StringComparison.OrdinalIgnoreCase));
                if (ours && n.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
                    timg.Children.RemoveAt(i);
            }
        }

        private static void RemoveGeneratedTextureNames(List<string> textures, string topBase, string bottomBase)
        {
            string topPrefix = Path.GetFileNameWithoutExtension(topBase ?? "") + "_anim_";
            string bottomPrefix = Path.GetFileNameWithoutExtension(bottomBase ?? "") + "_anim_";
            int i;
            for (i = textures.Count - 1; i >= 0; i--)
            {
                string n = textures[i] ?? "";
                if (n.StartsWith("mwms_rr_top_", StringComparison.OrdinalIgnoreCase) || n.StartsWith("mwms_rr_bottom_", StringComparison.OrdinalIgnoreCase) || (!String.IsNullOrEmpty(topPrefix) && n.StartsWith(topPrefix, StringComparison.OrdinalIgnoreCase)) || (!String.IsNullOrEmpty(bottomPrefix) && n.StartsWith(bottomPrefix, StringComparison.OrdinalIgnoreCase)))
                    textures.RemoveAt(i);
            }
        }

        private static List<ushort> AppendTextureNames(List<string> textures, List<string> names)
        {
            List<ushort> indices = new List<ushort>();
            int i;
            for (i = 0; i < names.Count; i++)
            {
                if (textures.Count >= 65535)
                    throw new InvalidDataException("Too many BRLAN texture entries.");
                indices.Add((ushort)textures.Count);
                textures.Add(names[i]);
            }

            return indices;
        }

        private static void ApplyRltp(BrlanDocument doc, string materialName, byte slot, List<float> keyFrames, List<ushort> indices)
        {
            AnimationModel material = null;
            int i;
            for (i = doc.Pai.Animations.Count - 1; i >= 0; i--)
            {
                AnimationModel a = doc.Pai.Animations[i];
                if (a.TargetKind == 1 && String.Equals(a.Name, materialName, StringComparison.Ordinal))
                {
                    if (material == null)
                        material = a;
                    else
                        doc.Pai.Animations.RemoveAt(i);
                }
            }

            if (material == null)
            {
                material = new AnimationModel();
                material.Name = materialName;
                material.TargetKind = 1;
                material.Unknown16 = 0;
                doc.Pai.Animations.Add(material);
            }

            TagModel rltp = null;
            for (i = material.Tags.Count - 1; i >= 0; i--)
            {
                TagModel tag = material.Tags[i];
                if (String.Equals(tag.Magic, "RLTP", StringComparison.Ordinal))
                {
                    if (rltp == null && !tag.RawOnly)
                        rltp = tag;
                    else
                        material.Tags.RemoveAt(i);
                }
            }

            if (rltp == null)
            {
                rltp = new TagModel();
                rltp.Magic = "RLTP";
                rltp.RawOnly = false;
                material.Tags.Add(rltp);
            }

            rltp.Entries.Clear();
            EntryModel entry = new EntryModel();
            entry.Index = slot;
            entry.Target = 0;
            entry.KeyType = 1;
            entry.UnknownByte = 0;
            entry.Unknown16 = 0;
            for (i = 0; i < keyFrames.Count && i < indices.Count; i++)
            {
                KeyframeModel k = new KeyframeModel();
                k.Frame = keyFrames[i];
                k.UIntValue = indices[i];
                k.Padding = 0;
                entry.Keys.Add(k);
            }

            rltp.Entries.Add(entry);
        }

        private static void WriteGeneratedTpls(ArchiveEntry timg, List<string> names, List<byte[]> data)
        {
            int i;
            for (i = 0; i < names.Count && i < data.Count; i++)
            {
                ArchiveEntry entry = timg.FindChild(names[i]);
                if (entry == null)
                {
                    entry = new ArchiveEntry(names[i], false);
                    timg.AddChild(entry);
                }

                entry.Data = data[i];
            }
        }

        private static bool ReplaceTpl(ArchiveEntry timg, string name, Bitmap frame)
        {
            ArchiveEntry entry = timg.FindChild(name);
            if (entry == null || entry.IsDirectory)
                throw new FileNotFoundException(L.T("Benötigte TPL fehlt: ", "Required TPL is missing: ") + name);
            entry.Data = TplTextureEditor.ReplaceFirstImage(entry.Data, frame, true);
            return true;
        }

        private static bool ReplaceTplIfExists(ArchiveEntry timg, string name, Bitmap frame)
        {
            ArchiveEntry entry = timg.FindChild(name);
            if (entry == null || entry.IsDirectory)
                return false;
            entry.Data = TplTextureEditor.ReplaceFirstImage(entry.Data, frame, true);
            return true;
        }

        private static bool ReplaceTplFillCropIfExists(ArchiveEntry timg, string name, Bitmap frame)
        {
            ArchiveEntry entry = timg.FindChild(name);
            if (entry == null || entry.IsDirectory)
                return false;
            TplTextureInfo info = TplTextureEditor.GetFirstImageInfo(entry.Data);
            using (Bitmap resized = ResizeFrame(frame, info.Width, info.Height, true))
                entry.Data = TplTextureEditor.ReplaceFirstImage(entry.Data, resized, false);
            return true;
        }

        private static void ValidateGeneratedTitle(BrlanDocument doc, ArchiveEntry timg, List<string> top, List<string> bottom)
        {
            if (doc == null || doc.Pai == null)
                throw new InvalidDataException("BRLAN validation failed.");
            if (doc.Pai.Frames == 0)
                throw new InvalidDataException("BRLAN frame count is zero.");
            int i;
            for (i = 0; i < top.Count; i++)
                if (timg.FindChild(top[i]) == null)
                    throw new InvalidDataException("Missing generated TPL: " + top[i]);
            for (i = 0; i < bottom.Count; i++)
                if (timg.FindChild(bottom[i]) == null)
                    throw new InvalidDataException("Missing generated TPL: " + bottom[i]);
            bool topFound = false, bottomFound = false;
            for (i = 0; i < doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = doc.Pai.Animations[i];
                if (a.TargetKind != 1)
                    continue;
                if (String.Equals(a.Name, "title_top", StringComparison.Ordinal))
                    topFound = HasRltp(a);
                if (String.Equals(a.Name, "title_bottom", StringComparison.Ordinal))
                    bottomFound = HasRltp(a);
            }

            if (!topFound || !bottomFound)
                throw new InvalidDataException(L.T("RLTP-Validierung fehlgeschlagen: title_top/title_bottom fehlt.", "RLTP validation failed: title_top/title_bottom is missing."));
        }

        private static bool HasRltp(AnimationModel a)
        {
            int i;
            for (i = 0; i < a.Tags.Count; i++)
                if (String.Equals(a.Tags[i].Magic, "RLTP", StringComparison.Ordinal) && !a.Tags[i].RawOnly && a.Tags[i].Entries.Count > 0)
                    return true;
            return false;
        }

        private static string SafeOutputPath(string outputFolder, string sourcePath)
        {
            string candidate = Path.Combine(outputFolder, Path.GetFileName(sourcePath));
            try
            {
                if (String.Equals(Path.GetFullPath(candidate), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                    candidate = Path.Combine(outputFolder, "MUR_EDITED", Path.GetFileName(sourcePath));
            }
            catch
            {
            }

            return candidate;
        }

        private static void SaveSzs(U8Archive archive, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            byte[] raw = archive.BuildU8();
            byte[] outBytes = Yaz0.Compress(raw);
            BackupManager.WriteAllBytesSafely(path, outBytes);
        }
    }

    internal sealed class RetroRewindMenuBackgroundBuildOptions
    {
        public string ArchiveBaseName;
        public string CommonArchivePath;
        public string LanguageArchivePath;
        public string SourcePath;
        public string OutputFolder;
        public int TakeEvery = 2;
        public TplPixelFormat Format = TplPixelFormat.CMPR;
        // 0 = automatic/fill-crop for generic menu backgrounds, 1 = exact stretch, 2 = fill-crop.
        public int FittingMode = 0;
        public bool CleanupOldAnimations = true;
    }

    internal sealed class RetroRewindMenuBackgroundBuildResult
    {
        public bool WasPatched;
        public bool WasAnimated;
        public string Summary;
    }

    internal static class RetroRewindMenuBackgroundBuilder
    {
        private sealed class SourceFrame : IDisposable
        {
            public Bitmap Bitmap;
            public int DurationMs;
            public void Dispose()
            {
                if (Bitmap != null)
                    Bitmap.Dispose();
            }
        }

        private sealed class FileRef
        {
            public ArchiveEntry Entry;
            public ArchiveEntry Parent;
            public string Path;
        }

        private sealed class BackgroundCandidate
        {
            public FileRef Tpl;
            public FileRef Brlyt;
            public FileRef Brlan;
            public BrlytTextureBinding Binding;
            public TplTextureInfo TplInfo;
        }

        public static RetroRewindMenuBackgroundBuildResult Build(RetroRewindMenuBackgroundBuildOptions options, Action<int, string> progress)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (progress == null)
                progress = delegate
                {
                };
            if (!File.Exists(options.CommonArchivePath))
                throw new FileNotFoundException("Common archive not found.", options.CommonArchivePath);
            if (!File.Exists(options.SourcePath))
                throw new FileNotFoundException("Background source not found.", options.SourcePath);
            if (String.IsNullOrWhiteSpace(options.OutputFolder))
                throw new InvalidDataException("Output folder is empty.");
            string ext = Path.GetExtension(options.SourcePath ?? "").ToLowerInvariant();
            if (ext != ".gif" && ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                throw new InvalidDataException(L.T("Unterstützte Hintergrundformate: GIF, PNG, JPG und JPEG.", "Supported background formats: GIF, PNG, JPG and JPEG."));
            Directory.CreateDirectory(options.OutputFolder);
            progress(2, L.T("Hintergrundquelle wird gelesen...", "Reading background source..."));
            List<SourceFrame> frames = LoadSourceFrames(options.SourcePath, Math.Max(1, options.TakeEvery));
            if (frames.Count == 0)
                throw new InvalidDataException(L.T("Die Quelle enthält kein lesbares Bild.", "The source contains no readable image."));
            StringBuilder summary = new StringBuilder();
            bool anyPatched = false;
            bool anyAnimated = false;
            try
            {
                progress(10, L.T("Common-Archiv wird analysiert...", "Analyzing common archive..."));
                U8Archive common = U8Archive.Load(File.ReadAllBytes(options.CommonArchivePath));
                string commonDiagnostic;
                bool commonAnimated;
                bool commonPatched = PatchArchive(common, options, frames, out commonAnimated, out commonDiagnostic);
                anyPatched |= commonPatched;
                anyAnimated |= commonAnimated;
                string commonOut = SafeOutputPath(options.OutputFolder, options.CommonArchivePath);
                if (commonPatched)
                    SaveSzs(common, commonOut);
                else
                    throw new NotSupportedException(L.T("Kein geprüfter Hintergrund in diesem Archiv. Es wurde keine Ausgabedatei geschrieben.", "No verified background in this archive. No output file was written."));
                summary.AppendLine(Path.GetFileName(options.CommonArchivePath) + ": " + commonDiagnostic);
                progress(68, L.T("Sprach-Archiv wird geprüft...", "Checking language archive..."));
                string languagePath = (options.LanguageArchivePath ?? "").Trim();
                if (File.Exists(languagePath))
                {
                    U8Archive language = U8Archive.Load(File.ReadAllBytes(languagePath));
                    string languageDiagnostic;
                    bool languageAnimated;
                    bool languagePatched = PatchArchive(language, options, frames, out languageAnimated, out languageDiagnostic, true);
                    anyPatched |= languagePatched;
                    anyAnimated |= languageAnimated;
                    string languageOut = SafeOutputPath(options.OutputFolder, languagePath);
                    if (languagePatched)
                        SaveSzs(language, languageOut);
                    else
                        summary.AppendLine(L.T("Zusätzliches Archiv unverändert; keine Ausgabekopie geschrieben.", "Additional archive unchanged; no output copy written."));
                    summary.AppendLine(Path.GetFileName(languagePath) + ": " + languageDiagnostic);
                }
                else
                {
                    summary.AppendLine(L.T("Kein Sprach-Archiv gewählt; nur Common-Archiv verarbeitet.", "No language archive selected; only the common archive was processed."));
                }

                progress(100, L.T("Bereich fertig.", "Area complete."));
                RetroRewindMenuBackgroundBuildResult result = new RetroRewindMenuBackgroundBuildResult();
                result.WasPatched = anyPatched;
                result.WasAnimated = anyAnimated;
                result.Summary = summary.ToString().Trim();
                return result;
            }
            finally
            {
                int i;
                for (i = 0; i < frames.Count; i++)
                    frames[i].Dispose();
            }
        }

        private static bool PatchArchive(U8Archive archive, RetroRewindMenuBackgroundBuildOptions options, List<SourceFrame> frames, out bool animated, out string diagnostic, bool optionalLanguage = false)
        {
            animated = false;
            diagnostic = "";
            List<FileRef> files = new List<FileRef>();
            CollectFiles(archive.Root, "", files);
            if (options.ArchiveBaseName == "MenuSingle" || options.ArchiveBaseName == "MenuMulti" || options.ArchiveBaseName == "Title")
            {
                FileRef layout = FindExactSuffix(files, "/bg/blyt/bg.brlyt");
                FileRef loop = FindExactSuffix(files, "/bg/anim/bg_Loop.brlan");
                FileRef existing = FindExactSuffix(files, "/bg/timg/ht_squareWhite_00.tpl");
                if (optionalLanguage && layout == null && loop == null && existing == null)
                {
                    diagnostic = L.T("Dieses Spracharchiv enthält keinen eigenen Menühintergrund und bleibt unverändert.",
                        "This language archive contains no separate menu background and remains unchanged.");
                    return false;
                }
                if (layout == null || loop == null || existing == null)
                    throw new InvalidDataException("Required MenuBG layout, animation or texture folder is missing.");
                layout.Entry.Data = MenuBackgroundLayout.Convert(layout.Entry.Data);
                ArchiveEntry image = existing.Parent.FindChild(MenuBackgroundLayout.TextureName);
                if (image == null)
                {
                    image = new ArchiveEntry(MenuBackgroundLayout.TextureName, false);
                    existing.Parent.AddChild(image);
                }

                using (Bitmap first = PrepareFrame(frames[0].Bitmap, 800, 500, options.FittingMode))
                    image.Data = TplEncoder.Encode(first, options.Format);
                BrlanDocument animation = BrlanCodec.Parse(loop.Entry.Data);
                // This animation is scoped to bg.brlyt. Remove scrolling and erroneous old border animation.
                animation.Pai.Animations.Clear();
                animation.Pai.Textures.Clear();
                loop.Entry.Data = BrlanCodec.Build(animation);
                files.Clear();
                CollectFiles(archive.Root, "", files);
            }

            BackgroundCandidate candidate = FindBackgroundCandidate(files, options.ArchiveBaseName);
            if (candidate == null || candidate.Tpl == null || candidate.Tpl.Entry == null)
            {
                diagnostic = L.T("Kein geprüftes Hintergrundlayout erkannt.", "No verified background layout detected.");
                return false;
            }

            string sourceKind = frames.Count > 1 ? "GIF" : Path.GetExtension(options.SourcePath).TrimStart('.').ToUpperInvariant();
            using (Bitmap first = PrepareFrame(frames[0].Bitmap, candidate.TplInfo.Width, candidate.TplInfo.Height, options.FittingMode))
            {
                candidate.Tpl.Entry.Data = TplTextureEditor.ReplaceFirstImage(candidate.Tpl.Entry.Data, first, false);
            }

            StringBuilder info = new StringBuilder();
            info.Append("target=").Append(candidate.Tpl.Path);
            info.Append(" ").Append(candidate.TplInfo.Width).Append("x").Append(candidate.TplInfo.Height);
            if (candidate.Binding != null)
                info.Append(" material=").Append(candidate.Binding.MaterialName).Append(" slot=").Append(candidate.Binding.Slot);
            info.Append(" source=").Append(sourceKind);
            bool gif = String.Equals(Path.GetExtension(options.SourcePath), ".gif", StringComparison.OrdinalIgnoreCase) && frames.Count > 1;
            if (gif && candidate.Brlan != null && candidate.Binding != null && !String.IsNullOrWhiteSpace(candidate.Binding.MaterialName))
            {
                animated = TryBuildGifAnimation(candidate, options, frames, out diagnostic);
                if (animated)
                {
                    info.Append(" animated=").Append(candidate.Brlan.Path);
                    diagnostic = info.ToString() + " | " + diagnostic;
                    return true;
                }

                info.Append(" | ").Append(diagnostic);
            }
            else if (gif)
            {
                info.Append(" | ").Append(L.T("GIF statisch: keine sichere Loop-BRLAN/Materialbindung erkannt.", "GIF static fallback: no safe loop BRLAN/material binding detected."));
            }

            diagnostic = info.ToString();
            return true;
        }

        private static BackgroundCandidate FindBackgroundCandidate(List<FileRef> files, string archiveBaseName)
        {
            FileRef exactLayout = FindExactSuffix(files, "/bg/blyt/bg.brlyt");
            FileRef exactTexture = FindExactSuffix(files, "/bg/timg/" + MenuBackgroundLayout.TextureName);
            if (exactLayout == null || exactTexture == null)
                return null;
            BrlytDocument exactDoc = BrlytDocument.FromBytes(exactLayout.Entry.Data);
            foreach (var pane in exactDoc.Panes)
            {
                if (pane.Magic != "pic1" || !pane.Visible)
                    continue;
                var material = exactDoc.MaterialForPane(pane);
                if (material == null)
                    continue;
                foreach (var binding in material.Bindings)
                    if (binding.TextureName == MenuBackgroundLayout.TextureName)
                        return new BackgroundCandidate
                        {
                            Tpl = exactTexture,
                            Brlyt = exactLayout,
                            Binding = binding,
                            TplInfo = TplTextureEditor.GetFirstImageInfo(exactTexture.Entry.Data),
                            Brlan = FindExactSuffix(files, "/bg/anim/bg_Loop.brlan")
                        };
            }

            return null;
        }

        private static bool TryBuildGifAnimation(BackgroundCandidate candidate, RetroRewindMenuBackgroundBuildOptions options, List<SourceFrame> frames, out string diagnostic)
        {
            diagnostic = "";
            if (candidate.Brlan == null || candidate.Binding == null)
                return false;
            BrlanDocument doc;
            try
            {
                doc = BrlanCodec.Parse(candidate.Brlan.Entry.Data);
            }
            catch (Exception ex)
            {
                diagnostic = L.T("BRLAN konnte nicht gelesen werden: ", "BRLAN could not be read: ") + ex.Message;
                return false;
            }

            if (doc == null || doc.Pai == null)
            {
                diagnostic = L.T("Loop-BRLAN enthält keine pai1-Sektion.", "Loop BRLAN has no pai1 section.");
                return false;
            }

            string materialName = candidate.Binding.MaterialName;
            byte slot = (byte)Math.Max(0, Math.Min(255, candidate.Binding.Slot));
            AnimationModel material = FindMaterialAnimation(doc, materialName);
            // Confirmed working compatibility path from the existing BRLAN Studio:
            // stock MenuSingle bg_Loop animates line0, while bg.tpl belongs to P_pict.
            if (material == null && String.Equals(options.ArchiveBaseName, "MenuSingle", StringComparison.OrdinalIgnoreCase) && String.Equals(materialName, "P_pict", StringComparison.Ordinal))
            {
                int a;
                for (a = 0; a < doc.Pai.Animations.Count; a++)
                {
                    AnimationModel legacy = doc.Pai.Animations[a];
                    if (legacy.TargetKind == 1 && String.Equals(legacy.Name, "line0", StringComparison.Ordinal))
                    {
                        legacy.Name = "P_pict";
                        legacy.Tags.Clear();
                        material = legacy;
                        break;
                    }
                }
            }

            if (material == null)
            {
                material = new AnimationModel();
                material.Name = materialName;
                material.TargetKind = 1;
                material.Unknown16 = 0;
                doc.Pai.Animations.Add(material);
            }

            TagModel rltp = null;
            int t;
            for (t = material.Tags.Count - 1; t >= 0; t--)
            {
                TagModel tag = material.Tags[t];
                if (!String.Equals(tag.Magic, "RLTP", StringComparison.Ordinal))
                    continue;
                if (rltp == null && !tag.RawOnly)
                    rltp = tag;
                else
                    material.Tags.RemoveAt(t);
            }

            if (rltp == null)
            {
                rltp = new TagModel();
                rltp.Magic = "RLTP";
                rltp.RawOnly = false;
                material.Tags.Add(rltp);
            }

            EntryModel entry = null;
            for (t = 0; t < rltp.Entries.Count; t++)
                if (rltp.Entries[t].Index == slot)
                {
                    entry = rltp.Entries[t];
                    break;
                }

            if (entry == null)
            {
                entry = new EntryModel();
                entry.Index = slot;
                rltp.Entries.Add(entry);
            }

            string prefix = "mwms_bg_" + MakeSafeStem(options.ArchiveBaseName) + "_";
            List<string> names = new List<string>();
            List<byte[]> tplData = new List<byte[]>();
            List<float> cycleFrames = new List<float>();
            float cycleDuration = 0.0f;
            int i;
            for (i = 0; i < frames.Count; i++)
            {
                string name = prefix + i.ToString("D3", CultureInfo.InvariantCulture) + ".tpl";
                names.Add(name);
                cycleFrames.Add(cycleDuration);
                using (Bitmap prepared = PrepareFrame(frames[i].Bitmap, candidate.TplInfo.Width, candidate.TplInfo.Height, options.FittingMode))
                    tplData.Add(TplEncoder.Encode(prepared, options.Format));
                cycleDuration += Math.Max(1.0f, frames[i].DurationMs * 60.0f / 1000.0f);
            }

            if (cycleDuration <= 0.0f)
                cycleDuration = 1.0f;
            if (options.CleanupOldAnimations)
                CleanupGeneratedTpls(candidate.Tpl.Parent, prefix);
            WriteGeneratedTpls(candidate.Tpl.Parent, names, tplData);
            List<string> oldTextures = new List<string>(doc.Pai.Textures);
            List<string> cleaned = new List<string>();
            for (i = 0; i < oldTextures.Count; i++)
            {
                string name = oldTextures[i] ?? "";
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    cleaned.Add(name);
            }

            doc.Pai.Textures.Clear();
            for (i = 0; i < cleaned.Count; i++)
                doc.Pai.Textures.Add(cleaned[i]);
            List<ushort> indices = new List<ushort>();
            for (i = 0; i < names.Count; i++)
            {
                if (doc.Pai.Textures.Count >= 65535)
                    throw new InvalidDataException("Too many BRLAN texture entries.");
                indices.Add((ushort)doc.Pai.Textures.Count);
                doc.Pai.Textures.Add(names[i]);
            }

            RemapOtherRltpEntries(doc, oldTextures, doc.Pai.Textures, entry);
            entry.Index = slot;
            entry.Target = 0;
            entry.KeyType = 1;
            entry.UnknownByte = 0;
            entry.Unknown16 = 0;
            entry.Keys.Clear();
            int targetFrames = Math.Max(1, (int)Math.Ceiling(cycleDuration));
            if (targetFrames > 65535)
                targetFrames = 65535;
            float offset = 0.0f;
            while (offset < targetFrames - 0.0001f)
            {
                for (i = 0; i < cycleFrames.Count && i < indices.Count; i++)
                {
                    float frame = offset + cycleFrames[i];
                    if (frame >= targetFrames - 0.0001f)
                        break;
                    KeyframeModel key = new KeyframeModel();
                    key.Frame = frame;
                    key.UIntValue = indices[i];
                    key.Padding = 0;
                    entry.Keys.Add(key);
                }

                offset += cycleDuration;
            }

            if (entry.Keys.Count == 0)
            {
                KeyframeModel key = new KeyframeModel();
                key.Frame = 0.0f;
                key.UIntValue = indices[0];
                key.Padding = 0;
                entry.Keys.Add(key);
            }

            doc.Pai.Flags = 1;
            doc.Pai.Frames = (ushort)Math.Max(1, Math.Min(65535, targetFrames));
            candidate.Brlan.Entry.Data = BrlanCodec.Build(doc);
            diagnostic = L.F("GIF animiert über RLTP ({0} Frames, Loop-BRLAN {1}).", "GIF animated through RLTP ({0} frames, loop BRLAN {1}).", frames.Count, candidate.Brlan.Entry.Name);
            return true;
        }

        private static AnimationModel FindMaterialAnimation(BrlanDocument doc, string materialName)
        {
            if (doc == null || doc.Pai == null)
                return null;
            int i;
            for (i = 0; i < doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = doc.Pai.Animations[i];
                if (a.TargetKind == 1 && String.Equals(a.Name, materialName, StringComparison.Ordinal))
                    return a;
            }

            return null;
        }

        private static void RemapOtherRltpEntries(BrlanDocument doc, List<string> oldTextures, List<string> newTextures, EntryModel skipEntry)
        {
            Dictionary<string, ushort> newIndices = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            int i, j, k, q;
            for (i = 0; i < newTextures.Count && i < 65535; i++)
            {
                string n = newTextures[i] ?? "";
                if (!newIndices.ContainsKey(n))
                    newIndices[n] = (ushort)i;
            }

            for (i = 0; i < doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = doc.Pai.Animations[i];
                for (j = 0; j < a.Tags.Count; j++)
                {
                    TagModel tag = a.Tags[j];
                    if (tag.RawOnly || !String.Equals(tag.Magic, "RLTP", StringComparison.Ordinal))
                        continue;
                    for (k = 0; k < tag.Entries.Count; k++)
                    {
                        EntryModel e = tag.Entries[k];
                        if (Object.ReferenceEquals(e, skipEntry) || e.KeyType != 1)
                            continue;
                        for (q = 0; q < e.Keys.Count; q++)
                        {
                            int oldIndex = e.Keys[q].UIntValue;
                            if (oldIndex < 0 || oldIndex >= oldTextures.Count)
                                continue;
                            ushort mapped;
                            if (newIndices.TryGetValue(oldTextures[oldIndex] ?? "", out mapped))
                                e.Keys[q].UIntValue = mapped;
                        }
                    }
                }
            }
        }

        private static List<SourceFrame> LoadSourceFrames(string path, int every)
        {
            List<SourceFrame> frames = new List<SourceFrame>();
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            if (ext != ".gif")
            {
                using (Image img = Image.FromFile(path))
                {
                    Bitmap frame = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(img, 0, 0);
                    }

                    SourceFrame selected = new SourceFrame();
                    selected.Bitmap = frame;
                    selected.DurationMs = 16;
                    frames.Add(selected);
                }

                return frames;
            }

            using (Image img = Image.FromFile(path))
            {
                FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
                int count = img.GetFrameCount(dim);
                int[] durations = ReadGifDurations(img, count);
                int start;
                for (start = 0; start < count; start += every)
                {
                    int end = Math.Min(count, start + every);
                    int groupMs = 0;
                    int q;
                    for (q = start; q < end; q++)
                        groupMs += durations[q];
                    img.SelectActiveFrame(dim, start);
                    Bitmap frame = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(img, 0, 0);
                    }

                    SourceFrame selected = new SourceFrame();
                    selected.Bitmap = frame;
                    selected.DurationMs = Math.Max(10, groupMs);
                    frames.Add(selected);
                }
            }

            return frames;
        }

        private static int[] ReadGifDurations(Image img, int count)
        {
            int[] durations = new int[count];
            int i;
            for (i = 0; i < count; i++)
                durations[i] = 100;
            try
            {
                PropertyItem item = img.GetPropertyItem(0x5100);
                byte[] data = item.Value;
                for (i = 0; i < count && i * 4 + 3 < data.Length; i++)
                {
                    int hundredths = BitConverter.ToInt32(data, i * 4);
                    durations[i] = Math.Max(10, hundredths * 10);
                }
            }
            catch
            {
            }

            return durations;
        }

        private static Bitmap PrepareFrame(Bitmap source, int width, int height, int fittingMode)
        {
            bool fillCrop = fittingMode != 1;
            Bitmap target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(target))
            using (ImageAttributes attrs = new ImageAttributes())
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.Clear(Color.Black);
                if (!fillCrop)
                {
                    g.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
                    return target;
                }

                float sourceAspect = source.Width / (float)source.Height;
                float targetAspect = width / (float)height;
                RectangleF src = new RectangleF(0, 0, source.Width, source.Height);
                if (sourceAspect > targetAspect)
                {
                    float cropWidth = source.Height * targetAspect;
                    src.X = (source.Width - cropWidth) / 2.0f;
                    src.Width = cropWidth;
                }
                else if (sourceAspect < targetAspect)
                {
                    float cropHeight = source.Width / targetAspect;
                    src.Y = (source.Height - cropHeight) / 2.0f;
                    src.Height = cropHeight;
                }

                g.DrawImage(source, new Rectangle(0, 0, width, height), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }

            return target;
        }

        private static void CleanupGeneratedTpls(ArchiveEntry timg, string prefix)
        {
            if (timg == null || !timg.IsDirectory)
                return;
            int i;
            for (i = timg.Children.Count - 1; i >= 0; i--)
            {
                ArchiveEntry child = timg.Children[i];
                if (child == null || child.IsDirectory)
                    continue;
                if ((child.Name ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (child.Name ?? "").EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
                    timg.Children.RemoveAt(i);
            }
        }

        private static void WriteGeneratedTpls(ArchiveEntry timg, List<string> names, List<byte[]> data)
        {
            if (timg == null || !timg.IsDirectory)
                throw new InvalidDataException("Background TPL parent directory is invalid.");
            int i;
            for (i = 0; i < names.Count && i < data.Count; i++)
            {
                ArchiveEntry entry = timg.FindChild(names[i]);
                if (entry == null)
                {
                    entry = new ArchiveEntry(names[i], false);
                    timg.AddChild(entry);
                }

                entry.Data = data[i];
            }
        }

        private static void CollectFiles(ArchiveEntry dir, string path, List<FileRef> files)
        {
            if (dir == null || !dir.IsDirectory)
                return;
            int i;
            for (i = 0; i < dir.Children.Count; i++)
            {
                ArchiveEntry child = dir.Children[i];
                string childPath = path + "/" + (child.Name ?? "");
                if (child.IsDirectory)
                    CollectFiles(child, childPath, files);
                else
                {
                    FileRef item = new FileRef();
                    item.Entry = child;
                    item.Parent = dir;
                    item.Path = childPath;
                    files.Add(item);
                }
            }
        }

        private static List<FileRef> FindByExtension(List<FileRef> files, string extension)
        {
            List<FileRef> result = new List<FileRef>();
            int i;
            for (i = 0; i < files.Count; i++)
                if ((files[i].Entry.Name ?? "").EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    result.Add(files[i]);
            return result;
        }

        private static List<FileRef> FindByName(List<FileRef> files, string name)
        {
            List<FileRef> result = new List<FileRef>();
            int i;
            for (i = 0; i < files.Count; i++)
                if (String.Equals(files[i].Entry.Name, name, StringComparison.OrdinalIgnoreCase))
                    result.Add(files[i]);
            return result;
        }

        private static FileRef FindExactSuffix(List<FileRef> files, string suffix)
        {
            string wanted = (suffix ?? "").Replace('\\', '/');
            int i;
            for (i = 0; i < files.Count; i++)
            {
                string p = (files[i].Path ?? "").Replace('\\', '/');
                if (p.EndsWith(wanted, StringComparison.OrdinalIgnoreCase))
                    return files[i];
            }

            return null;
        }

        private static BrlytLayoutMap LoadBrlytMap(byte[] data, string fileName)
        {
            string temp = Path.Combine(Path.GetTempPath(), "murums_bg_" + Guid.NewGuid().ToString("N") + "_" + fileName);
            try
            {
                File.WriteAllBytes(temp, data ?? new byte[0]);
                return BrlytInspector.Load(temp);
            }
            finally
            {
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch
                {
                }
            }
        }

        private static string MakeSafeStem(string value)
        {
            StringBuilder sb = new StringBuilder();
            string s = (value ?? "bg").ToLowerInvariant();
            int i;
            for (i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
            }

            if (sb.Length == 0)
                sb.Append("bg");
            return sb.ToString();
        }

        private static string SafeOutputPath(string outputFolder, string sourcePath)
        {
            string candidate = Path.Combine(outputFolder, Path.GetFileName(sourcePath));
            try
            {
                if (String.Equals(Path.GetFullPath(candidate), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                    candidate = Path.Combine(outputFolder, "MUR_EDITED", Path.GetFileName(sourcePath));
            }
            catch
            {
            }

            return candidate;
        }

        private static void SaveSzs(U8Archive archive, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            byte[] raw = archive.BuildU8();
            byte[] outBytes = Yaz0.Compress(raw);
            BackupManager.WriteAllBytesSafely(path, outBytes);
        }
    }
}
