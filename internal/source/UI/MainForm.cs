using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class MainForm : Form
    {
        private const string AppName = "murums Wii Mod Studio";
        private const string AppVersion = StudioVersion.Current;
        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private TreeView _tree;
        private TabControl _tabs;
        private TextBox _details;
        private TextBox _raw;
        private RichTextBox _help;
        private TabPage _helpPage;
        private PictureBox _preview;
        private Label _previewInfo;
        private Button _previewPrevious;
        private Button _previewNext;
        private Button _previewExport;
        private Button _previewImportTpl;
        private TexturePreviewResult _previewResult;
        private ArchiveEntry _previewEntry;
        private int _previewImageIndex;
        private string _lastFindText;
        private ToolStripStatusLabel _status;
        private Label _selectionInfo;
        private TreeNode _dragHoverNode;
        private enum TreeDropPlacement
        {
            Before,
            Into,
            After
        }

        private U8Archive _archive;
        private string _currentPath;
        private bool _dirty;
        private readonly ArchiveHistory _history = new ArchiveHistory();
        private ToolStripButton _undoButton;
        private ToolStripButton _redoButton;
        private ToolStripMenuItem _undoMenu;
        private ToolStripMenuItem _redoMenu;
        public MainForm()
        {
            Text = AppName + " v" + AppVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 680);
            Size = new Size(1320, 820);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.5F);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            AllowDrop = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            ApplyTheme();
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;
        }

        private void BuildUi()
        {
            SuspendLayout();
            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(0);
            shell.Padding = new Padding(0);
            shell.ColumnCount = 1;
            shell.RowCount = 5;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            Controls.Add(shell);
            Panel brand = murumsWiiModStudio.StudioChrome.Header(L.T("Dein Wii-Modding-Studio", "Your Wii modding studio"), L.T("Menübilder und Animationen gestalten • Spieldateien öffnen und bearbeiten", "Create menu pictures and animations • Open and edit game files"));
            shell.Controls.Add(brand, 0, 0);
            _menu = new MenuStrip();
            _menu.Dock = DockStyle.Fill;
            _menu.Margin = new Padding(0);
            _menu.Padding = new Padding(8, 3, 0, 3);
            _menu.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            ToolStripMenuItem file = new ToolStripMenuItem(L.T("Datei", "File"));
            file.DropDownItems.Add(MakeMenu(L.T("Öffnen...", "Open..."), Keys.Control | Keys.O, delegate
            {
                OpenDialog();
            }));
            file.DropDownItems.Add(MakeMenu(L.T("Ressource separat öffnen...", "Open standalone resource..."), Keys.Control | Keys.Shift | Keys.O, delegate
            {
                OpenStandaloneResourceDialog();
            }));
            file.DropDownItems.Add(MakeMenu(L.T("Speichern", "Save"), Keys.Control | Keys.S, delegate
            {
                SaveArchive(false);
            }));
            file.DropDownItems.Add(MakeMenu(L.T("Speichern unter...", "Save as..."), Keys.Control | Keys.Shift | Keys.S, delegate
            {
                SaveArchive(true);
            }));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(MakeMenu(L.T("Beenden", "Exit"), Keys.None, delegate
            {
                Close();
            }));
            ToolStripMenuItem edit = new ToolStripMenuItem(L.T("Bearbeiten", "Edit"));
            _undoMenu = MakeMenu(StudioHistorySymbols.Undo + "  " + L.T("Rückgängig", "Undo"), Keys.Control | Keys.Z, delegate
            {
                NavigateHistory(false);
            });
            _redoMenu = MakeMenu(StudioHistorySymbols.Redo + "  " + L.T("Wiederholen", "Redo"), Keys.Control | Keys.Y, delegate
            {
                NavigateHistory(true);
            });
            _undoMenu.Enabled = false;
            _redoMenu.Enabled = false;
            edit.DropDownItems.Add(_undoMenu);
            edit.DropDownItems.Add(_redoMenu);
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeMenu(L.T("Dateien importieren...", "Import files..."), Keys.Control | Keys.I, delegate
            {
                ImportFiles();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Ordner importieren...", "Import folder..."), Keys.None, delegate
            {
                ImportFolder();
            }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeMenu(L.T("Ausgewählte Ressource bearbeiten...", "Edit selected resource..."), Keys.Control | Keys.Enter, delegate
            {
                EditSelectedResource();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Ausgewählte Datei ersetzen...", "Replace selected file..."), Keys.Control | Keys.R, delegate
            {
                ReplaceSelected();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("TPL-Bild importieren...", "Import TPL texture..."), Keys.Control | Keys.T, delegate
            {
                ImportTextureSelected();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Ausgewählten Eintrag exportieren...", "Export selected entry..."), Keys.Control | Keys.E, delegate
            {
                ExportSelected();
            }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeMenu(L.T("Neuer Ordner...", "New folder..."), Keys.Control | Keys.Shift | Keys.N, delegate
            {
                NewFolder();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Duplizieren", "Duplicate"), Keys.Control | Keys.D, delegate
            {
                DuplicateSelected();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Umbenennen", "Rename"), Keys.F2, delegate
            {
                RenameSelected();
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Löschen", "Delete"), Keys.Delete, delegate
            {
                DeleteSelected();
            }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeMenu(L.T("Nach oben verschieben", "Move up"), Keys.Control | Keys.Up, delegate
            {
                MoveSelected(-1);
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Nach unten verschieben", "Move down"), Keys.Control | Keys.Down, delegate
            {
                MoveSelected(1);
            }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeMenu(L.T("Eintrag suchen...", "Find entry..."), Keys.Control | Keys.F, delegate
            {
                FindEntry(false);
            }));
            edit.DropDownItems.Add(MakeMenu(L.T("Weitersuchen", "Find next"), Keys.F3, delegate
            {
                FindEntry(true);
            }));
            ToolStripMenuItem tools = new ToolStripMenuItem(L.T("Tools", "Tools"));
            tools.DropDownItems.Add(MakeMenu("Game HUD Tool...", Keys.None, delegate
            {
                using (var hud = new GameHudForm())
                    hud.ShowDialog(this);
            }));
            tools.DropDownItems.Add(MakeMenu("Race HUD Tool...", Keys.None, delegate
            {
                using (var hud = new RaceHudForm())
                    hud.ShowDialog(this);
            }));
            tools.DropDownItems.Add(MakeMenu(L.T("RR-Backgrounds Tool...", "RR-Backgrounds Tool..."), Keys.None, delegate
            {
                OpenRetroRewindGifWizard();
            }));
            tools.DropDownItems.Add(MakeMenu("Font Changer Tool...", Keys.None, delegate
            {
                using (var f = new FontChangerForm())
                    f.ShowDialog(this);
            }));
            tools.DropDownItems.Add(MakeMenu("Theme Project Tool...", Keys.None, delegate
            {
                using (var f = new ThemeProjectForm())
                    f.ShowDialog(this);
            }));
            tools.DropDownItems.Add(MakeMenu("Archive Compare Tool...", Keys.None, delegate
            {
                using (var f = new ArchiveCompareForm())
                    f.ShowDialog(this);
            }));
            tools.DropDownItems.Add(MakeMenu("Menu Text Tool...", Keys.None, delegate
            {
                using (var f = new MenuTextForm())
                    f.ShowDialog(this);
            }));
            tools.DropDownItems.Add(MakeMenu("Music && Loops Tool...", Keys.None, delegate
            {
                using (var f = new MusicLoopForm())
                    f.ShowDialog(this);
            }));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(MakeMenu(L.T("Ausgewählte BRLAN bearbeiten / GIF", "Edit selected BRLAN / GIF"), Keys.None, delegate
            {
                EditSelectedBrlan();
            }));
            tools.DropDownItems.Add(MakeMenu(L.T("Ausgewählte BRLYT bearbeiten", "Edit selected BRLYT"), Keys.None, delegate
            {
                EditSelectedBrlyt();
            }));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(MakeMenu(L.T("Archiv validieren", "Validate archive"), Keys.None, delegate
            {
                ValidateArchive();
            }));
            tools.DropDownItems.Add(MakeMenu(L.T("Streckendateien prüfen...", "Check race-track files..."), Keys.None, delegate
            {
                CheckCourse();
            }));
            tools.DropDownItems.Add(MakeMenu(L.T("Toolchain-Status...", "Toolchain status..."), Keys.None, delegate
            {
                using (ToolchainForm f = new ToolchainForm())
                    f.ShowDialog(this);
            }));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(MakeMenu(L.T("Alles aufklappen", "Expand all"), Keys.None, delegate
            {
                _tree.ExpandAll();
            }));
            tools.DropDownItems.Add(MakeMenu(L.T("Alles zuklappen", "Collapse all"), Keys.None, delegate
            {
                CollapseTree();
            }));
            ToolStripMenuItem language = new ToolStripMenuItem(L.T("Sprache", "Language"));
            ToolStripMenuItem german = new ToolStripMenuItem("Deutsch");
            ToolStripMenuItem english = new ToolStripMenuItem("English");
            german.Checked = L.Current == UiLanguage.German;
            english.Checked = L.Current == UiLanguage.English;
            german.Click += delegate
            {
                AppSettings.RequestLanguageChange(this, UiLanguage.German);
            };
            english.Click += delegate
            {
                AppSettings.RequestLanguageChange(this, UiLanguage.English);
            };
            language.DropDownItems.Add(german);
            language.DropDownItems.Add(english);
            ToolStripMenuItem helpMenu = new ToolStripMenuItem(L.T("Hilfe", "Help"));
            helpMenu.DropDownItems.Add(MakeMenu(L.T("Allgemeine Hilfe", "General help"), Keys.F1, delegate
            {
                StudioHelpWindow.Show(this, _helpPage, L.T("Hilfe", "Help"));
            }));
            helpMenu.DropDownItems.Add(MakeMenu(L.T("Über ", "About ") + AppName, Keys.None, delegate
            {
                ShowAbout();
            }));
            _menu.Items.Add(file);
            _menu.Items.Add(edit);
            _menu.Items.Add(tools);
            _menu.Items.Add(language);
            _menu.Items.Add(helpMenu);
            MainMenuStrip = _menu;
            shell.Controls.Add(_menu, 0, 1);
            _toolbar = new ToolStrip();
            _toolbar.Dock = DockStyle.Fill;
            _toolbar.Margin = new Padding(0);
            _toolbar.AutoSize = false;
            _toolbar.Height = 42;
            _toolbar.GripStyle = ToolStripGripStyle.Hidden;
            _toolbar.Padding = new Padding(8, 5, 0, 5);
            _toolbar.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _toolbar.Items.Add(MakeToolButton(L.T("Öffnen", "Open"), delegate
            {
                OpenDialog();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Texturen finden", "Find textures"), delegate
            {
                BrowseTextures();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Speichern", "Save"), delegate
            {
                SaveArchive(false);
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Speichern unter", "Save as"), delegate
            {
                SaveArchive(true);
            }));
            _undoButton = MakeToolButton(StudioHistorySymbols.Undo, delegate
            {
                NavigateHistory(false);
            });
            _redoButton = MakeToolButton(StudioHistorySymbols.Redo, delegate
            {
                NavigateHistory(true);
            });
            _undoButton.ToolTipText = L.T("Rückgängig (Strg+Z)", "Undo (Ctrl+Z)");
            _redoButton.ToolTipText = L.T("Wiederholen (Strg+Y)", "Redo (Ctrl+Y)");
            _undoButton.Enabled = false;
            _redoButton.Enabled = false;
            _toolbar.Items.Add(_undoButton);
            _toolbar.Items.Add(_redoButton);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton(L.T("+ Dateien", "+ Files"), delegate
            {
                ImportFiles();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Bearbeiten", "Edit"), delegate
            {
                EditSelectedResource();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Ersetzen", "Replace"), delegate
            {
                ReplaceSelected();
            }));
            ToolStripDropDownButton more = new ToolStripDropDownButton(L.T("Weitere Aktionen", "More actions"));
            more.DropDownItems.Add(MakeMenu(L.T("Texturbild importieren...", "Import texture image..."), Keys.None, delegate
            {
                ImportTextureSelected();
            }));
            more.DropDownItems.Add(MakeMenu(L.T("Umbenennen...", "Rename..."), Keys.None, delegate
            {
                RenameSelected();
            }));
            more.DropDownItems.Add(MakeMenu(L.T("Duplizieren", "Duplicate"), Keys.None, delegate
            {
                DuplicateSelected();
            }));
            more.DropDownItems.Add(MakeMenu(L.T("Löschen", "Delete"), Keys.None, delegate
            {
                DeleteSelected();
            }));
            _toolbar.Items.Add(more);
            _toolbar.Items.Add(MakeToolButton(L.T("Export", "Export"), delegate
            {
                ExportSelected();
            }));
            _toolbar.Items.Add(new ToolStripSeparator());
            ToolStripDropDownButton checks = new ToolStripDropDownButton(L.T("Prüfen", "Check"));
            checks.DropDownItems.Add(MakeMenu(L.T("Archivstruktur", "Archive structure"), Keys.None, delegate
            {
                ValidateArchive();
            }));
            checks.DropDownItems.Add(MakeMenu(L.T("Streckendateien prüfen...", "Check race-track files..."), Keys.None, delegate
            {
                CheckCourse();
            }));
            _toolbar.Items.Add(checks);
            shell.Controls.Add(_toolbar, 0, 2);
            SplitContainer mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Margin = new Padding(0);
            mainSplit.FixedPanel = FixedPanel.Panel1;
            mainSplit.SplitterWidth = 4;
            mainSplit.BackColor = DarkTheme.Border;
            shell.Controls.Add(mainSplit, 0, 3);
            Shown += delegate
            {
                try
                {
                    int desired = 300;
                    int minimumLeft = 200;
                    int minimumRight = 430;
                    int maxAllowed = Math.Max(minimumLeft, mainSplit.Width - minimumRight - mainSplit.SplitterWidth);
                    if (desired > maxAllowed)
                        desired = maxAllowed;
                    if (desired < minimumLeft)
                        desired = minimumLeft;
                    // Move the splitter first while no panel minimums are active.
                    // Applying minimums before this can throw on some DPI/layout setups.
                    mainSplit.Panel1MinSize = 0;
                    mainSplit.Panel2MinSize = 0;
                    if (desired > 0 && desired < mainSplit.Width - mainSplit.SplitterWidth)
                        mainSplit.SplitterDistance = desired;
                    if (mainSplit.Width > minimumLeft + minimumRight + mainSplit.SplitterWidth)
                    {
                        mainSplit.Panel1MinSize = minimumLeft;
                        mainSplit.Panel2MinSize = minimumRight;
                    }
                }
                catch
                {
                }
            };
            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.Margin = new Padding(0);
            left.Padding = new Padding(0);
            left.ColumnCount = 1;
            left.RowCount = 2;
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainSplit.Panel1.Controls.Add(left);
            Label archiveHeader = new Label();
            archiveHeader.Dock = DockStyle.Fill;
            archiveHeader.Text = L.T("  ARCHIV", "  ARCHIVE");
            archiveHeader.TextAlign = ContentAlignment.MiddleLeft;
            archiveHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            archiveHeader.ForeColor = DarkTheme.Muted;
            archiveHeader.BackColor = DarkTheme.Panel2;
            left.Controls.Add(archiveHeader, 0, 0);
            _tree = new TreeView();
            _tree.ImageList = CreateTreeImageList();
            _tree.Dock = DockStyle.Fill;
            _tree.Margin = new Padding(0);
            _tree.HideSelection = false;
            _tree.AllowDrop = true;
            _tree.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _tree.AfterSelect += delegate
            {
                UpdateSelection();
            };
            _tree.NodeMouseClick += OnNodeMouseClick;
            _tree.NodeMouseDoubleClick += OnNodeMouseDoubleClick;
            _tree.KeyDown += OnTreeKeyDown;
            _tree.ItemDrag += OnTreeItemDrag;
            _tree.DragEnter += OnTreeDragEnter;
            _tree.DragOver += OnTreeDragOver;
            _tree.DragLeave += OnTreeDragLeave;
            _tree.DragDrop += OnTreeDragDrop;
            left.Controls.Add(_tree, 0, 1);
            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(0);
            right.Padding = new Padding(12);
            right.ColumnCount = 1;
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            right.RowCount = 2;
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainSplit.Panel2.Controls.Add(right);
            _selectionInfo = new Label();
            _selectionInfo.Dock = DockStyle.Fill;
            _selectionInfo.Text = L.T("Kein Archiv geöffnet", "No archive opened");
            _selectionInfo.TextAlign = ContentAlignment.MiddleLeft;
            _selectionInfo.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            _selectionInfo.ForeColor = DarkTheme.Fore;
            right.Controls.Add(_selectionInfo, 0, 0);
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            TabPage previewTab = new TabPage(L.T("Vorschau", "Preview"));
            previewTab.Padding = new Padding(10);
            TableLayoutPanel previewLayout = new TableLayoutPanel();
            previewLayout.Dock = DockStyle.Fill;
            previewLayout.Margin = new Padding(0);
            previewLayout.Padding = new Padding(0);
            previewLayout.ColumnCount = 1;
            previewLayout.RowCount = 3;
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            FlowLayoutPanel previewTools = new FlowLayoutPanel();
            previewTools.Dock = DockStyle.Fill;
            previewTools.FlowDirection = FlowDirection.LeftToRight;
            previewTools.WrapContents = false;
            previewTools.Margin = new Padding(0);
            previewTools.Padding = new Padding(0, 2, 0, 2);
            previewTools.BackColor = DarkTheme.Panel;
            _previewPrevious = MakePreviewButton("◀", delegate
            {
                ChangePreviewImage(-1);
            });
            _previewNext = MakePreviewButton("▶", delegate
            {
                ChangePreviewImage(1);
            });
            _previewExport = MakePreviewButton(L.T("PNG exportieren", "Export PNG"), delegate
            {
                ExportPreviewPng();
            });
            _previewImportTpl = MakePreviewButton(L.T("Bild ersetzen", "Replace image"), delegate
            {
                ImportTextureSelected();
            });
            previewTools.Controls.Add(_previewPrevious);
            previewTools.Controls.Add(_previewNext);
            previewTools.Controls.Add(_previewExport);
            previewTools.Controls.Add(_previewImportTpl);
            _previewPrevious.Enabled = false;
            _previewNext.Enabled = false;
            _previewExport.Enabled = false;
            _previewImportTpl.Enabled = false;
            Panel imageHost = new Panel();
            imageHost.Dock = DockStyle.Fill;
            imageHost.Margin = new Padding(0);
            imageHost.Padding = new Padding(8);
            imageHost.BackColor = Color.FromArgb(28, 28, 33);
            _preview = new PictureBox();
            _preview.Dock = DockStyle.Fill;
            _preview.SizeMode = PictureBoxSizeMode.Zoom;
            _preview.BackColor = Color.FromArgb(38, 38, 44);
            imageHost.Controls.Add(_preview);
            _previewInfo = new Label();
            _previewInfo.Dock = DockStyle.Fill;
            _previewInfo.TextAlign = ContentAlignment.MiddleLeft;
            _previewInfo.ForeColor = DarkTheme.Muted;
            _previewInfo.Text = L.T("Wähle eine TPL- oder Bilddatei aus.", "Select a TPL or image file.");
            previewLayout.Controls.Add(previewTools, 0, 0);
            previewLayout.Controls.Add(imageHost, 0, 1);
            previewLayout.Controls.Add(_previewInfo, 0, 2);
            previewTab.Controls.Add(previewLayout);
            _tabs.TabPages.Add(previewTab);
            TabPage detailsTab = new TabPage(L.T("Details", "Details"));
            detailsTab.Padding = new Padding(10);
            _details = new TextBox();
            _details.Dock = DockStyle.Fill;
            _details.Multiline = true;
            _details.ReadOnly = true;
            _details.ScrollBars = ScrollBars.Vertical;
            _details.BorderStyle = BorderStyle.None;
            _details.Font = new Font("Consolas", 10.5F);
            detailsTab.Controls.Add(_details);
            _tabs.TabPages.Add(detailsTab);
            TabPage rawTab = new TabPage(L.T("Raw / Hex", "Raw / Hex"));
            rawTab.Padding = new Padding(10);
            _raw = new TextBox();
            _raw.Dock = DockStyle.Fill;
            _raw.Multiline = true;
            _raw.ReadOnly = true;
            _raw.ScrollBars = ScrollBars.Both;
            _raw.WordWrap = false;
            _raw.BorderStyle = BorderStyle.None;
            _raw.Font = new Font("Consolas", 9.5F);
            rawTab.Controls.Add(_raw);
            _tabs.TabPages.Add(rawTab);
            TabPage helpTab = new TabPage(L.T("Hilfe", "Help"));
            helpTab.Padding = new Padding(14);
            _help = new RichTextBox();
            _help.Dock = DockStyle.Fill;
            _help.ReadOnly = true;
            _help.BorderStyle = BorderStyle.None;
            _help.Font = new Font("Segoe UI", 10.5F);
            StudioChrome.EnableLinks(_help);
            _help.Text = BuildHelpText();
            StudioHelp.BuildNavigation(helpTab, _help);
            _helpPage = helpTab;
            Disposed += delegate
            {
                _helpPage.Dispose();
            };
            right.Controls.Add(_tabs, 0, 1);
            StatusStrip statusStrip = new StatusStrip();
            statusStrip.Dock = DockStyle.Fill;
            statusStrip.Margin = new Padding(0);
            statusStrip.SizingGrip = false;
            _status = new ToolStripStatusLabel(L.T("Bereit", "Ready"));
            statusStrip.Items.Add(_status);
            shell.Controls.Add(statusStrip, 0, 4);
            ResumeLayout(true);
        }

        private void ApplyTheme()
        {
            DarkTheme.Apply(this);
            DarkTheme.StyleTree(_tree);
            DarkTheme.StyleTabs(_tabs);
            _details.BackColor = DarkTheme.Panel;
            _details.ForeColor = DarkTheme.Fore;
            _raw.BackColor = DarkTheme.Panel;
            _raw.ForeColor = DarkTheme.Fore;
            _help.BackColor = DarkTheme.Panel;
            _help.ForeColor = DarkTheme.Fore;
            MurumsDarkToolStripRenderer renderer = new MurumsDarkToolStripRenderer();
            ToolStripManager.Renderer = renderer;
            DarkTheme.StyleToolStrip(_menu, renderer);
            DarkTheme.StyleToolStrip(_toolbar, renderer);
            ToolStrip statusStrip = _status != null ? _status.GetCurrentParent() : null;
            if (statusStrip != null)
            {
                statusStrip.BackColor = DarkTheme.Panel2;
                statusStrip.ForeColor = DarkTheme.Fore;
                statusStrip.Renderer = renderer;
            }
        }

        private ToolStripMenuItem MakeMenu(string text, Keys shortcut, EventHandler action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None)
                item.ShortcutKeys = shortcut;
            item.Click += action;
            return item;
        }

        private ToolStripButton MakeToolButton(string text, EventHandler action)
        {
            ToolStripButton button = new ToolStripButton(text);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.AutoSize = true;
            button.Padding = new Padding(8, 0, 8, 0);
            button.Click += action;
            return button;
        }

        private Button MakePreviewButton(string text, EventHandler action)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 29;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = DarkTheme.Border;
            button.FlatAppearance.MouseOverBackColor = DarkTheme.Panel3;
            button.FlatAppearance.MouseDownBackColor = DarkTheme.Accent2;
            button.BackColor = DarkTheme.Panel2;
            button.ForeColor = DarkTheme.Fore;
            button.Margin = new Padding(0, 0, 6, 0);
            button.Padding = new Padding(7, 0, 7, 0);
            button.Click += action;
            return button;
        }

        private string BuildHelpText()
        {
            return StudioHelp.Guide();
        }

        public void OpenFromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            ResourceInfo info = ResourceDetector.Detect(path);
            if (info.Kind == ResourceKind.U8Archive)
            {
                if (!ConfirmDiscardChanges())
                    return;
                LoadArchive(path);
                return;
            }

            if (Visible)
                OpenStandaloneResource(path);
            else
                Shown += delegate
                {
                    OpenStandaloneResource(path);
                };
        }

        private void OpenStandaloneResourceDialog()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = ResourceDetector.OpenFilter;
                dlg.Title = L.T("Nintendo-Wii-Ressource öffnen", "Open Nintendo Wii resource");
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    OpenFromPath(dlg.FileName);
            }
        }

        private void OpenStandaloneResource(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;
            SyncResourceEditorLanguage();
            ResourceInfo info = ResourceDetector.Detect(path);
            if (info.Kind == ResourceKind.Brlan)
            {
                BackupManager.CreateBackup(path);
                using (murumsWiiModStudio.Brlan.MainForm editor = new murumsWiiModStudio.Brlan.MainForm())
                {
                    editor.OpenFromPath(path);
                    editor.ShowDialog(this);
                }
            }
            else if (info.Kind == ResourceKind.Brlyt)
            {
                BackupManager.CreateBackup(path);
                using (murumsWiiModStudio.Brlan.BrlytEditorForm editor = new murumsWiiModStudio.Brlan.BrlytEditorForm(path))
                    editor.ShowDialog(this);
            }
            else if (info.Kind == ResourceKind.Tpl || info.Kind == ResourceKind.Image)
            {
                using (StandaloneTextureForm editor = new StandaloneTextureForm(path))
                    editor.ShowDialog(this);
            }
            else
            {
                using (FormatInspectorForm inspector = new FormatInspectorForm(path))
                    inspector.ShowDialog(this);
            }
        }

        public void ShowStartCenter()
        {
            using (StartCenterForm start = new StartCenterForm(this))
                start.ShowDialog(this);
        }

        public void OpenRetroRewindGifWizard()
        {
            using (RetroRewindGifWizard wizard = new RetroRewindGifWizard(_currentPath))
            {
                wizard.ShowDialog(this);
            }
        }

        private void OpenDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = ResourceDetector.OpenFilter;
                dialog.Title = L.T("Nintendo-Wii-Datei öffnen", "Open Nintendo Wii file");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                OpenFromPath(dialog.FileName);
            }
        }

        private void LoadArchive(string path)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _status.Text = L.T("Archiv wird geöffnet...", "Opening archive...");
                byte[] data = File.ReadAllBytes(path);
                _archive = U8Archive.Load(data);
                _history.Reset(_archive.BuildU8());
                _currentPath = path;
                _dirty = false;
                RebuildTree();
                _status.Text = L.F("Geöffnet: {0} ({1})", "Opened: {0} ({1})", Path.GetFileName(path), _archive.WasCompressed ? "Yaz0 + U8" : "U8");
                UpdateTitle();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Öffnen fehlgeschlagen", "Open failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = L.T("Öffnen fehlgeschlagen", "Open failed");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void SaveArchive(bool saveAs)
        {
            if (_archive == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Öffne zuerst ein Archiv.", "Open an archive first."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = _currentPath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Compressed SZS (*.szs)|*.szs|Raw U8 archive (*.arc)|*.arc|Raw U8 archive (*.u8)|*.u8|All files|*.*";
                    dialog.Title = L.T("Archiv speichern", "Save archive");
                    string originalExt = Path.GetExtension(_currentPath ?? "").ToLowerInvariant();
                    dialog.FilterIndex = originalExt == ".arc" ? 2 : originalExt == ".u8" ? 3 : 1;
                    if (!string.IsNullOrEmpty(_currentPath))
                    {
                        dialog.FileName = Path.GetFileNameWithoutExtension(_currentPath) + "_edited" + Path.GetExtension(_currentPath);
                        dialog.InitialDirectory = Path.GetDirectoryName(_currentPath);
                    }

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    path = dialog.FileName;
                }
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                _status.Text = L.T("U8-Archiv wird erstellt...", "Building U8 archive...");
                byte[] raw = _archive.BuildU8();
                byte[] output;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".szs")
                {
                    _status.Text = L.T("Yaz0 wird komprimiert...", "Compressing Yaz0...");
                    output = Yaz0.Compress(raw);
                }
                else
                {
                    output = raw;
                }

                BackupManager.WriteAllBytesSafely(path, output);
                _history.Record(raw);
                _history.MarkSaved();
                _currentPath = path;
                _dirty = false;
                _status.Text = L.F("Gespeichert: {0} ({1:N0} Bytes)", "Saved: {0} ({1:N0} bytes)", Path.GetFileName(path), output.Length);
                UpdateTitle();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Speichern fehlgeschlagen", "Save failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = L.T("Speichern fehlgeschlagen", "Save failed");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private ArchiveEntry SelectedEntry()
        {
            return _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as ArchiveEntry;
        }

        private ArchiveEntry SelectedDirectory()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null)
                return null;
            return entry.IsDirectory ? entry : entry.Parent;
        }

        private void ImportFiles()
        {
            ArchiveEntry dir = SelectedDirectory();
            if (_archive == null || dir == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle zuerst einen Ordner im Archiv aus.", "Select a folder in the archive first."), L.T("Dateien importieren", "Import files"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = L.T("Dateien importieren nach ", "Import files into ") + GetEntryPath(dir);
                dialog.Filter = "All files|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                ImportFilePaths(dir, dialog.FileNames);
            }
        }

        private void ImportFolder()
        {
            ArchiveEntry dir = SelectedDirectory();
            if (_archive == null || dir == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle zuerst einen Ordner im Archiv aus.", "Select a folder in the archive first."), L.T("Ordner importieren", "Import folder"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (murumsWiiModStudio.FolderPickerDialog dialog = new murumsWiiModStudio.FolderPickerDialog())
            {
                dialog.Description = L.T("Ordner auswählen, der importiert werden soll", "Select the folder to import");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                ImportFilePaths(dir, new string[] { dialog.SelectedPath });
            }
        }

        private void ImportFilePaths(ArchiveEntry dir, string[] paths)
        {
            if (dir == null || paths == null || paths.Length == 0)
                return;
            int imported = 0;
            int i;
            for (i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (File.Exists(path))
                {
                    imported += ImportSingleFile(dir, path);
                }
                else if (Directory.Exists(path))
                {
                    imported += ImportDirectory(dir, path);
                }
            }

            if (imported > 0)
            {
                MarkDirty();
                RebuildTreeAndSelect(dir);
                _status.Text = L.F("{0} Eintrag/Einträge nach {1} importiert", "Imported {0} item(s) into {1}", imported, GetEntryPath(dir));
            }
        }

        private int ImportSingleFile(ArchiveEntry dir, string path)
        {
            if (dir == null || !File.Exists(path))
                return 0;
            string name = Path.GetFileName(path);
            ArchiveEntry existing = dir.FindChild(name);
            if (existing != null)
            {
                if (existing.IsDirectory)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ein Ordner mit diesem Namen existiert bereits: ", "A folder with that name already exists: ") + name, L.T("Import", "Import"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 0;
                }

                DialogResult answer = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ein Eintrag namens '" + name + "' existiert bereits. Ersetzen?", "An entry named '" + name + "' already exists. Replace it?"), L.T("Doppelter Name", "Duplicate name"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                    return 0;
                existing.Data = File.ReadAllBytes(path);
                return 1;
            }

            ArchiveEntry file = new ArchiveEntry(name, false);
            file.Data = File.ReadAllBytes(path);
            dir.AddChild(file);
            return 1;
        }

        private int ImportDirectory(ArchiveEntry parent, string path)
        {
            string name = new DirectoryInfo(path).Name;
            ArchiveEntry target = parent.FindChild(name);
            if (target != null && !target.IsDirectory)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Eine Datei blockiert den Ordnernamen: ", "A file blocks the folder name: ") + name, L.T("Ordnerimport", "Folder import"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            if (target == null)
            {
                target = new ArchiveEntry(name, true);
                parent.AddChild(target);
            }

            int count = 1;
            string[] files = Directory.GetFiles(path);
            int i;
            for (i = 0; i < files.Length; i++)
                count += ImportSingleFile(target, files[i]);
            string[] dirs = Directory.GetDirectories(path);
            for (i = 0; i < dirs.Length; i++)
                count += ImportDirectory(target, dirs[i]);
            return count;
        }

        private void ReplaceSelected()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null || entry.IsDirectory)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle eine Datei zum Ersetzen aus.", "Select a file to replace."), L.T("Ersetzen", "Replace"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = L.T("Ersetzen: ", "Replace: ") + entry.Name;
                dialog.Filter = "All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    entry.Data = File.ReadAllBytes(dialog.FileName);
                    MarkDirty();
                    UpdateSelection();
                    _status.Text = L.T("Ersetzt: ", "Replaced: ") + GetEntryPath(entry);
                }
                catch (Exception ex)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Ersetzen fehlgeschlagen", "Replace failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportTextureSelected()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null || entry.IsDirectory || !TplTextureEditor.IsTpl(entry.Data))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle zuerst eine TPL-Datei aus.", "Select a TPL file first."), L.T("TPL-Bild importieren", "Import TPL texture"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TplTextureInfo info;
            int imageIndex = _previewEntry == entry ? _previewImageIndex : 0;
            try
            {
                info = TplTextureEditor.GetImageInfo(entry.Data, imageIndex);
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("TPL-Bild importieren", "Import TPL texture"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = L.T("Bild in ", "Import image into ") + entry.Name + " — " + info.FormatName + " " + info.Width + "×" + info.Height;
                dialog.Filter = "Supported images/TPL|*.tpl;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|TPL texture|*.tpl|Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    using (Bitmap source = TplTextureEditor.LoadSourceBitmap(dialog.FileName))
                    {
                        bool resize = false;
                        if (source.Width != info.Width || source.Height != info.Height)
                        {
                            DialogResult answer = murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Das Quellbild ist {0}×{1}, die Ziel-TPL {2}×{3}.\r\n\r\nAutomatisch auf die Zielgrösse skalieren?", "The source image is {0}×{1}, the target TPL is {2}×{3}.\r\n\r\nResize automatically to the target size?", source.Width, source.Height, info.Width, info.Height), L.T("Bildgrösse anpassen", "Resize image"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (answer != DialogResult.Yes)
                                return;
                            resize = true;
                        }

                        entry.Data = TplTextureEditor.ReplaceImage(entry.Data, source, resize, imageIndex);
                    }

                    MarkDirty();
                    UpdateSelection();
                    UpdatePreview(entry, imageIndex, true);
                    string mip = info.HasMipMaps ? L.T(" Verkleinerte Texturstufen wurden ebenfalls aktualisiert.", " Smaller texture levels were also updated.") : string.Empty;
                    _status.Text = L.T("TPL-Bild ersetzt; Name/Format/Parameter beibehalten: ", "TPL image replaced; name/format/settings preserved: ") + GetEntryPath(entry) + mip;
                }
                catch (Exception ex)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("TPL-Bild konnte nicht importiert werden", "TPL texture import failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportSelected()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle einen Eintrag zum Exportieren aus.", "Select an entry to export."), L.T("Export", "Export"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (entry.IsDirectory)
                {
                    using (murumsWiiModStudio.FolderPickerDialog dialog = new murumsWiiModStudio.FolderPickerDialog())
                    {
                        dialog.Description = L.T("Zielordner für den Export auswählen", "Select destination folder for export");
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                            return;
                        string folderName = entry == _archive.Root ? Path.GetFileNameWithoutExtension(_currentPath ?? "archive") : entry.Name;
                        string target = Path.Combine(dialog.SelectedPath, folderName);
                        ExportDirectory(entry, target);
                        _status.Text = L.T("Ordner exportiert: ", "Folder exported: ") + target;
                    }
                }
                else
                {
                    using (SaveFileDialog dialog = new SaveFileDialog())
                    {
                        dialog.Title = L.T("Exportieren: ", "Export: ") + entry.Name;
                        dialog.FileName = entry.Name;
                        dialog.Filter = "All files|*.*";
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                            return;
                        File.WriteAllBytes(dialog.FileName, entry.Data ?? new byte[0]);
                        _status.Text = L.T("Exportiert: ", "Exported: ") + entry.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Export fehlgeschlagen", "Export failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportDirectory(ArchiveEntry entry, string path)
        {
            Directory.CreateDirectory(path);
            int i;
            for (i = 0; i < entry.Children.Count; i++)
            {
                ArchiveEntry child = entry.Children[i];
                string childPath = Path.Combine(path, child.Name);
                if (child.IsDirectory)
                    ExportDirectory(child, childPath);
                else
                    File.WriteAllBytes(childPath, child.Data ?? new byte[0]);
            }
        }

        private void NewFolder()
        {
            ArchiveEntry dir = SelectedDirectory();
            if (_archive == null || dir == null)
                return;
            string name = Prompt.Show(this, L.T("Neuer Ordner", "New folder"), L.T("Ordnername:", "Folder name:"), "new_folder");
            if (name == null)
                return;
            name = name.Trim();
            if (!IsValidName(name))
                return;
            if (dir.FindChild(name) != null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ein Eintrag mit diesem Namen existiert bereits.", "An entry with that name already exists."), L.T("Neuer Ordner", "New folder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ArchiveEntry child = new ArchiveEntry(name, true);
            dir.AddChild(child);
            MarkDirty();
            RebuildTreeAndSelect(child);
        }

        private void DuplicateSelected()
        {
            ArchiveEntry entry = SelectedEntry();
            if (_archive == null || entry == null || entry == _archive.Root)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle einen Datei- oder Ordner-Eintrag zum Duplizieren aus.", "Select a file or folder entry to duplicate."), L.T("Duplizieren", "Duplicate"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ArchiveEntry parent = entry.Parent;
            if (parent == null)
                return;
            string baseName = Path.GetFileNameWithoutExtension(entry.Name);
            string ext = entry.IsDirectory ? string.Empty : Path.GetExtension(entry.Name);
            string name = baseName + "_copy" + ext;
            int n = 2;
            while (parent.FindChild(name) != null)
            {
                name = baseName + "_copy" + n + ext;
                n++;
            }

            ArchiveEntry copy = CloneEntry(entry, name);
            int index = parent.Children.IndexOf(entry);
            copy.Parent = parent;
            parent.Children.Insert(index < 0 ? parent.Children.Count : index + 1, copy);
            MarkDirty();
            RebuildTreeAndSelect(copy);
        }

        private ArchiveEntry CloneEntry(ArchiveEntry source, string newName)
        {
            ArchiveEntry copy = new ArchiveEntry(newName, source.IsDirectory);
            if (!source.IsDirectory)
            {
                byte[] data = source.Data ?? new byte[0];
                copy.Data = new byte[data.Length];
                Buffer.BlockCopy(data, 0, copy.Data, 0, data.Length);
            }
            else
            {
                int i;
                for (i = 0; i < source.Children.Count; i++)
                {
                    ArchiveEntry child = CloneEntry(source.Children[i], source.Children[i].Name);
                    copy.AddChild(child);
                }
            }

            return copy;
        }

        private void MoveSelected(int delta)
        {
            ArchiveEntry entry = SelectedEntry();
            if (_archive == null || entry == null || entry == _archive.Root || entry.Parent == null)
                return;
            List<ArchiveEntry> list = entry.Parent.Children;
            int oldIndex = list.IndexOf(entry);
            int newIndex = oldIndex + delta;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= list.Count)
                return;
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, entry);
            MarkDirty();
            RebuildTreeAndSelect(entry);
        }

        private void FindEntry(bool repeat)
        {
            if (_archive == null || _tree.Nodes.Count == 0)
                return;
            string text = _lastFindText;
            if (!repeat || string.IsNullOrEmpty(text))
            {
                text = Prompt.Show(this, L.T("Eintrag suchen", "Find entry"), L.T("Name oder Pfad enthält:", "Name or path contains:"), text ?? string.Empty);
                if (text == null)
                    return;
                text = text.Trim();
                if (text.Length == 0)
                    return;
                _lastFindText = text;
            }

            List<TreeNode> nodes = new List<TreeNode>();
            CollectTreeNodes(_tree.Nodes, nodes);
            int start = 0;
            if (_tree.SelectedNode != null)
            {
                int selected = nodes.IndexOf(_tree.SelectedNode);
                if (selected >= 0)
                    start = selected + 1;
            }

            int pass;
            for (pass = 0; pass < 2; pass++)
            {
                int from = pass == 0 ? start : 0;
                int to = pass == 0 ? nodes.Count : Math.Min(start, nodes.Count);
                int i;
                for (i = from; i < to; i++)
                {
                    TreeNode node = nodes[i];
                    ArchiveEntry entry = node.Tag as ArchiveEntry;
                    string haystack = entry == null ? node.Text : GetEntryPath(entry) + " " + entry.Name;
                    if (haystack.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _tree.SelectedNode = node;
                        node.EnsureVisible();
                        _tree.Focus();
                        _status.Text = L.T("Gefunden: ", "Found: ") + haystack.Trim();
                        return;
                    }
                }
            }

            murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Kein passender Eintrag gefunden.", "No matching entry found."), L.T("Suchen", "Find"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CollectTreeNodes(TreeNodeCollection collection, List<TreeNode> output)
        {
            int i;
            for (i = 0; i < collection.Count; i++)
            {
                TreeNode node = collection[i];
                output.Add(node);
                if (node.Nodes.Count > 0)
                    CollectTreeNodes(node.Nodes, output);
            }
        }

        private void RenameSelected()
        {
            ArchiveEntry entry = SelectedEntry();
            if (_archive == null || entry == null || entry == _archive.Root)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Die Archivwurzel kann nicht umbenannt werden.", "The archive root cannot be renamed."), L.T("Umbenennen", "Rename"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = Prompt.Show(this, L.T("Umbenennen", "Rename"), L.T("Neuer Name des Archiveintrags:", "New archive entry name:"), entry.Name);
            if (name == null)
                return;
            name = name.Trim();
            if (!IsValidName(name))
                return;
            ArchiveEntry sibling = entry.Parent == null ? null : entry.Parent.FindChild(name);
            if (sibling != null && sibling != entry)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ein anderer Eintrag verwendet diesen Namen bereits.", "Another entry already uses that name."), L.T("Umbenennen", "Rename"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            entry.Name = name;
            MarkDirty();
            RebuildTreeAndSelect(entry);
        }

        private bool IsValidName(string name)
        {
            try
            {
                U8Archive.ValidateEntryName(name);
            }
            catch (InvalidDataException)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Bitte einen gültigen Dateinamen ohne Pfadzeichen, abschließenden Punkt oder reservierten Windows-Namen wählen.", "Choose a valid filename without path characters, trailing dots or reserved Windows names."), L.T("Ungültiger Name", "Invalid name"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void DeleteSelected()
        {
            ArchiveEntry entry = SelectedEntry();
            if (_archive == null || entry == null || entry == _archive.Root)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Die Archivwurzel kann nicht gelöscht werden.", "The archive root cannot be deleted."), L.T("Löschen", "Delete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult answer = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("'" + entry.Name + "' aus dem Archiv löschen?", "Delete '" + entry.Name + "' from the archive?"), L.T("Löschen", "Delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
                return;
            ArchiveEntry parent = entry.Parent;
            parent.Children.Remove(entry);
            MarkDirty();
            RebuildTreeAndSelect(parent);
        }

        private void ValidateArchive()
        {
            if (_archive == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Öffne zuerst ein Archiv.", "Open an archive first."), L.T("Validieren", "Validate"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> issues = new List<string>();
            try
            {
                _archive.BuildU8();
                ValidateDirectory(_archive.Root, "/", issues);
            }
            catch (Exception ex)
            {
                issues.Add(ex.Message);
            }

            if (issues.Count == 0)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Keine strukturellen Probleme gefunden.\r\n\r\nHinweis: Das bestätigt nur die U8-Archivstruktur, nicht die Laufzeitkompatibilität einzelner TPL/BRLAN-Dateien.", "No structural problems found.\r\n\r\nNote: this only validates the U8 archive structure, not runtime compatibility of individual TPL/BRLAN files."), L.T("Validierung erfolgreich", "Validation passed"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                _status.Text = L.T("Validierung: keine strukturellen Probleme", "Validation: no structural problems");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(L.F("{0} Problem(e) gefunden:", "Found {0} issue(s):", issues.Count));
            sb.AppendLine();
            for (int i = 0; i < issues.Count && i < 40; i++)
                sb.AppendLine("• " + issues[i]);
            if (issues.Count > 40)
                sb.AppendLine("...");
            murumsWiiModStudio.StudioMessageBox.Show(this, sb.ToString(), L.T("Validierungsprobleme", "Validation issues"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _status.Text = L.F("Validierung: {0} Problem(e)", "Validation: {0} issue(s)", issues.Count);
        }

        public void BrowseTextures()
        {
            if (_archive == null)
            {
                OpenDialog();
                if (_archive == null)
                    return;
            }

            using (TextureBrowserForm browser = new TextureBrowserForm(_archive.Root))
            {
                if (browser.ShowDialog(this) == DialogResult.OK && browser.SelectedTexture != null)
                {
                    RebuildTreeAndSelect(browser.SelectedTexture);
                    _status.Text = L.T("Bild ausgewählt. Mit „Bild ersetzen“ in der Vorschau dein eigenes Bild einsetzen.", "Image selected. Use Replace image in the preview to insert your own picture.");
                }
            }
        }

        public void BrowseTexturesFromFile(string path)
        {
            OpenFromPath(path);
            if (_archive != null && String.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase))
                BrowseTextures();
        }

        private async void CheckCourse()
        {
            if (_archive == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Öffne zuerst ein Streckenarchiv.", "Open a course archive first."));
                return;
            }

            ToolDescriptor tool = ToolchainManager.FindById("wszst");
            if (ToolchainManager.Find(tool) == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wiimms SZS Tool fehlt. Unter Werkzeuge > Toolchain kannst du es zuordnen oder installieren.", "Wiimms SZS Tool is missing. Locate or install it under Tools > Toolchain."));
                return;
            }

            string temp = Path.Combine(Path.GetTempPath(), "murums-check-" + Guid.NewGuid().ToString("N") + ".u8");
            try
            {
                byte[] snapshot = _archive.BuildU8();
                Enabled = false;
                _status.Text = L.T("Strecke wird geprüft...", "Checking course...");
                string report = await System.Threading.Tasks.Task.Run(delegate
                {
                    File.WriteAllBytes(temp, snapshot);
                    string output, error;
                    bool ok = ToolchainManager.RunCapture(tool, "check --no-wildcards --no-colors " + ToolchainManager.QuoteArgument(temp), out output, out error);
                    return (ok ? L.T("Prüfung abgeschlossen.", "Check completed.") : L.T("Prüfung meldet Hinweise oder Fehler.", "Check reported warnings or errors.")) + "\r\n\r\n" + output + "\r\n" + error;
                });
                Enabled = true;
                using (Form result = new Form())
                {
                    result.Text = L.T("MKW-Streckenprüfung — aktueller Bearbeitungsstand", "MKW course check — current edits");
                    result.Size = new Size(900, 650);
                    result.StartPosition = FormStartPosition.CenterParent;
                    TextBox box = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        Multiline = true,
                        ReadOnly = true,
                        ScrollBars = ScrollBars.Both,
                        WordWrap = false,
                        Text = report
                    };
                    result.Controls.Add(box);
                    DarkTheme.Apply(result);
                    result.ShowDialog(this);
                }

                _status.Text = L.T("Streckenprüfung abgeschlossen; Originaldatei unverändert.", "Course check completed; original file unchanged.");
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Prüfung fehlgeschlagen", "Check failed"));
            }
            finally
            {
                Enabled = true;
                if (File.Exists(temp))
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void ValidateDirectory(ArchiveEntry dir, string path, List<string> issues)
        {
            Dictionary<string, ArchiveEntry> names = new Dictionary<string, ArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dir.Children.Count; i++)
            {
                ArchiveEntry child = dir.Children[i];
                string childPath = path == "/" ? "/" + child.Name : path + "/" + child.Name;
                if (string.IsNullOrEmpty(child.Name))
                    issues.Add(childPath + ": empty name");
                if (child.Name.IndexOf('/') >= 0 || child.Name.IndexOf('\\') >= 0)
                    issues.Add(childPath + ": invalid slash in name");
                if (names.ContainsKey(child.Name))
                    issues.Add(path + ": duplicate name " + child.Name);
                else
                    names.Add(child.Name, child);
                if (child.Parent != dir)
                    issues.Add(childPath + ": invalid parent link");
                if (child.IsDirectory)
                    ValidateDirectory(child, childPath, issues);
                else if (child.Data == null)
                    issues.Add(childPath + ": file data is null");
            }
        }

        private void CollapseTree()
        {
            _tree.CollapseAll();
            if (_tree.Nodes.Count > 0)
                _tree.Nodes[0].Expand();
        }

        private ImageList CreateTreeImageList()
        {
            ImageList images = new ImageList();
            images.ColorDepth = ColorDepth.Depth32Bit;
            images.ImageSize = new Size(16, 16);
            images.TransparentColor = Color.Transparent;
            images.Images.Add("folder", CreateTreeGlyph(Color.FromArgb(224, 181, 74), true));
            images.Images.Add("file", CreateTreeGlyph(Color.FromArgb(160, 168, 184), false));
            images.Images.Add("texture", CreateTreeGlyph(Color.FromArgb(89, 196, 255), false));
            images.Images.Add("animation", CreateTreeGlyph(Color.FromArgb(193, 127, 255), false));
            images.Images.Add("layout", CreateTreeGlyph(Color.FromArgb(116, 214, 145), false));
            images.Images.Add("resource", CreateTreeGlyph(Color.FromArgb(255, 142, 108), false));
            return images;
        }

        private Bitmap CreateTreeGlyph(Color color, bool folder)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(color))
                using (Pen pen = new Pen(Color.FromArgb(210, 230, 230, 235)))
                {
                    if (folder)
                    {
                        g.FillRectangle(brush, 1, 5, 14, 9);
                        g.FillRectangle(brush, 2, 3, 6, 4);
                        g.DrawRectangle(pen, 1, 5, 13, 8);
                    }
                    else
                    {
                        g.FillRectangle(brush, 3, 1, 10, 14);
                        g.DrawRectangle(pen, 3, 1, 9, 13);
                        g.DrawLine(pen, 5, 5, 10, 5);
                        g.DrawLine(pen, 5, 8, 10, 8);
                    }
                }
            }

            return bitmap;
        }

        private string GetTreeImageKey(ArchiveEntry entry)
        {
            if (entry == null || entry.IsDirectory)
                return "folder";
            string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (ext == ".tpl" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                return "texture";
            if (ext == ".brlan")
                return "animation";
            if (ext == ".brlyt")
                return "layout";
            if (ext == ".brres" || ext == ".kmp" || ext == ".kcl" || ext == ".bmg" || ext == ".brstm" || ext == ".brsar" || ext == ".breff" || ext == ".breft")
                return "resource";
            return "file";
        }

        private void RebuildTree()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            if (_archive != null)
            {
                string rootName = string.IsNullOrEmpty(_currentPath) ? "U8 Archive" : Path.GetFileName(_currentPath);
                TreeNode rootNode = new TreeNode(rootName);
                rootNode.Tag = _archive.Root;
                rootNode.ImageKey = "folder";
                rootNode.SelectedImageKey = "folder";
                AddTreeChildren(rootNode, _archive.Root);
                _tree.Nodes.Add(rootNode);
                rootNode.Expand();
                _tree.SelectedNode = rootNode;
            }

            _tree.EndUpdate();
            UpdateSelection();
        }

        private void RebuildTreeAndSelect(ArchiveEntry select)
        {
            HashSet<ArchiveEntry> expanded = CaptureExpandedEntries();
            RebuildTree();
            RestoreExpandedEntries(expanded);
            TreeNode node = FindNode(_tree.Nodes, select);
            if (node != null)
            {
                _tree.SelectedNode = node;
                node.EnsureVisible();
                if (select.IsDirectory)
                    node.Expand();
            }
        }

        private HashSet<ArchiveEntry> CaptureExpandedEntries()
        {
            HashSet<ArchiveEntry> expanded = new HashSet<ArchiveEntry>();
            CollectExpandedEntries(_tree.Nodes, expanded);
            return expanded;
        }

        private void CollectExpandedEntries(TreeNodeCollection nodes, HashSet<ArchiveEntry> expanded)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                ArchiveEntry entry = node.Tag as ArchiveEntry;
                if (entry != null && node.IsExpanded)
                    expanded.Add(entry);
                if (node.Nodes.Count > 0)
                    CollectExpandedEntries(node.Nodes, expanded);
            }
        }

        private void RestoreExpandedEntries(HashSet<ArchiveEntry> expanded)
        {
            if (expanded == null || expanded.Count == 0)
                return;
            RestoreExpandedEntries(_tree.Nodes, expanded);
        }

        private void RestoreExpandedEntries(TreeNodeCollection nodes, HashSet<ArchiveEntry> expanded)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                ArchiveEntry entry = node.Tag as ArchiveEntry;
                if (entry != null && expanded.Contains(entry))
                    node.Expand();
                if (node.Nodes.Count > 0)
                    RestoreExpandedEntries(node.Nodes, expanded);
            }
        }

        private void AddTreeChildren(TreeNode parentNode, ArchiveEntry parentEntry)
        {
            for (int i = 0; i < parentEntry.Children.Count; i++)
            {
                ArchiveEntry child = parentEntry.Children[i];
                TreeNode node = new TreeNode(child.Name);
                node.Tag = child;
                node.ImageKey = GetTreeImageKey(child);
                node.SelectedImageKey = node.ImageKey;
                if (child.IsDirectory)
                    AddTreeChildren(node, child);
                parentNode.Nodes.Add(node);
            }
        }

        private TreeNode FindNode(TreeNodeCollection nodes, ArchiveEntry entry)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                if (object.ReferenceEquals(node.Tag, entry))
                    return node;
                TreeNode found = FindNode(node.Nodes, entry);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void UpdateSelection()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null)
            {
                _selectionInfo.Text = _archive == null ? L.T("Kein Archiv geöffnet", "No archive opened") : string.Empty;
                _details.Text = _archive == null ? L.T("Öffne eine Wii .szs/.arc/.u8-Datei.", "Open a Wii .szs/.arc/.u8 archive.") : string.Empty;
                _raw.Text = string.Empty;
                ClearPreview(L.T("Wähle eine TPL- oder Bilddatei aus.", "Select a TPL or image file."));
                return;
            }

            string path = GetEntryPath(entry);
            _selectionInfo.Text = path;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(entry.IsDirectory ? L.T("ORDNER", "DIRECTORY") : L.T("DATEI", "FILE"));
            sb.AppendLine();
            sb.AppendLine(L.T("Pfad: ", "Path: ") + path);
            sb.AppendLine(L.T("Name: ", "Name: ") + (entry == _archive.Root ? "<root>" : entry.Name));
            if (entry.IsDirectory)
            {
                sb.AppendLine(L.T("Einträge: ", "Children: ") + entry.Children.Count);
                sb.AppendLine(L.T("Gesamtgrösse: ", "Total size: ") + FormatBytes(GetRecursiveSize(entry)));
                sb.AppendLine();
                sb.AppendLine(L.T("Mit + Dateien kannst du beliebige Dateien in diesen Ordner einfügen. Die Dateinamen bleiben exakt erhalten.", "Use + Files to add arbitrary files to this folder. Filenames are preserved exactly."));
                if (path.Equals("/bg/timg", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine();
                    sb.AppendLine(L.T("MKWii GIF-Hintergrund: Hier gehören bg_anim_000.tpl ... bg_anim_020.tpl hinein.", "MKWii GIF background: import bg_anim_000.tpl ... bg_anim_020.tpl here."));
                }

                ClearPreview(L.T("Ordner haben keine Bildvorschau.", "Directories do not have an image preview."));
            }
            else
            {
                int size = entry.Data == null ? 0 : entry.Data.Length;
                sb.AppendLine(L.T("Grösse: ", "Size: ") + FormatBytes(size));
                sb.AppendLine(L.T("Erweiterung: ", "Extension: ") + (Path.GetExtension(entry.Name).Length == 0 ? "-" : Path.GetExtension(entry.Name)));
                sb.AppendLine(L.T("Erkannt als: ", "Detected as: ") + DetectFileType(entry));
                if (path.Equals("/bg/anim/bg_Loop.brlan", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine();
                    sb.AppendLine(L.T("Für den GIF-Hintergrund: Ersetzen anklicken und die geänderte bg_Loop.brlan aus murums Wii Mod Studio auswählen.", "For the GIF background: click Replace and select the modified bg_Loop.brlan from murums Wii Mod Studio."));
                }

                UpdatePreview(entry, 0, false);
            }

            _details.Text = sb.ToString();
            _raw.Text = entry.IsDirectory ? L.T("Ordner besitzen keine Raw-Daten.", "Directories have no raw data.") : BuildHexPreview(entry.Data);
        }

        private long GetRecursiveSize(ArchiveEntry entry)
        {
            if (entry == null)
                return 0;
            if (!entry.IsDirectory)
                return entry.Data == null ? 0 : entry.Data.LongLength;
            long total = 0;
            int i;
            for (i = 0; i < entry.Children.Count; i++)
                total += GetRecursiveSize(entry.Children[i]);
            return total;
        }

        private string DetectFileType(ArchiveEntry entry)
        {
            if (entry == null || entry.IsDirectory)
                return L.T("Ordner", "Directory");
            byte[] d = entry.Data ?? new byte[0];
            if (d.Length >= 4)
            {
                if (d[0] == 0x00 && d[1] == 0x20 && d[2] == 0xAF && d[3] == 0x30)
                    return "TPL texture";
                if (d[0] == (byte)'R' && d[1] == (byte)'L' && d[2] == (byte)'A' && d[3] == (byte)'N')
                    return "BRLAN / RLAN";
                if (d[0] == (byte)'R' && d[1] == (byte)'L' && d[2] == (byte)'Y' && d[3] == (byte)'T')
                    return "BRLYT / RLYT";
                if (d[0] == (byte)'b' && d[1] == (byte)'r' && d[2] == (byte)'e' && d[3] == (byte)'s')
                    return "BRRES";
                if (d[0] == (byte)'Y' && d[1] == (byte)'a' && d[2] == (byte)'z' && d[3] == (byte)'0')
                    return "Yaz0";
                if (d[0] == 0x55 && d[1] == 0xAA && d[2] == 0x38 && d[3] == 0x2D)
                    return "U8 archive";
            }

            string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff")
                return ext.TrimStart('.').ToUpperInvariant() + " image";
            return L.T("Binärdatei", "Binary file");
        }

        private void ClearPreview(string message)
        {
            if (_preview != null)
                _preview.Image = null;
            if (_previewResult != null)
            {
                _previewResult.Dispose();
                _previewResult = null;
            }

            _previewEntry = null;
            _previewImageIndex = 0;
            if (_previewInfo != null)
                _previewInfo.Text = message ?? string.Empty;
            UpdatePreviewButtons();
        }

        private void UpdatePreview(ArchiveEntry entry, int imageIndex, bool keepTab)
        {
            if (entry == null || entry.IsDirectory)
            {
                ClearPreview(L.T("Keine Bildvorschau verfügbar.", "No image preview available."));
                return;
            }

            TexturePreviewResult decoded;
            string error;
            if (!TexturePreview.TryDecode(entry.Name, entry.Data, imageIndex, out decoded, out error))
            {
                ClearPreview(error == null ? L.T("Für diesen Dateityp ist keine Bildvorschau verfügbar.", "No image preview is available for this file type.") : L.T("Vorschaufehler: ", "Preview error: ") + error);
                _previewEntry = entry;
                return;
            }

            if (_preview != null)
                _preview.Image = null;
            if (_previewResult != null)
                _previewResult.Dispose();
            _previewResult = decoded;
            _previewEntry = entry;
            _previewImageIndex = decoded.ImageIndex;
            _preview.Image = decoded.Bitmap;
            StringBuilder info = new StringBuilder();
            info.Append(decoded.TypeName);
            if (!string.IsNullOrEmpty(decoded.FormatName))
                info.Append(" • ").Append(decoded.FormatName);
            info.Append(" • ").Append(decoded.Width).Append(" × ").Append(decoded.Height);
            if (decoded.ImageCount > 1)
                info.Append(" • ").Append(decoded.ImageIndex + 1).Append("/").Append(decoded.ImageCount);
            if (!string.IsNullOrEmpty(decoded.Extra))
                info.Append(" • ").Append(decoded.Extra);
            _previewInfo.Text = info.ToString();
            UpdatePreviewButtons();
            if (!keepTab && _tabs != null)
                _tabs.SelectedIndex = 0;
            if (_tabs != null)
                _tabs.Invalidate();
        }

        private void UpdatePreviewButtons()
        {
            bool has = _previewResult != null && _previewResult.Bitmap != null;
            int count = has ? _previewResult.ImageCount : 0;
            if (_previewPrevious != null)
                _previewPrevious.Enabled = has && count > 1 && _previewImageIndex > 0;
            if (_previewNext != null)
                _previewNext.Enabled = has && count > 1 && _previewImageIndex + 1 < count;
            if (_previewExport != null)
                _previewExport.Enabled = has;
            ArchiveEntry selected = SelectedEntry();
            bool selectedTpl = selected != null && !selected.IsDirectory && selected.Data != null && TplTextureEditor.IsTpl(selected.Data);
            if (_previewImportTpl != null)
                _previewImportTpl.Enabled = selectedTpl && TplTextureEditor.CanReplaceImage(selected.Data, _previewImageIndex);
        }

        private void ChangePreviewImage(int delta)
        {
            if (_previewEntry == null || _previewResult == null)
                return;
            int next = _previewImageIndex + delta;
            if (next < 0 || next >= _previewResult.ImageCount)
                return;
            UpdatePreview(_previewEntry, next, true);
        }

        private void ExportSelectedPreviewPng()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null || entry.IsDirectory)
                return;
            // Ensure the preview belongs to the entry that invoked the context menu.
            if (_previewEntry != entry || _previewResult == null || _previewResult.Bitmap == null)
                UpdatePreview(entry, 0, true);
            if (_previewResult == null || _previewResult.Bitmap == null || _previewEntry != entry)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Für diese Datei konnte keine PNG-Vorschau erzeugt werden.", "No PNG preview could be created for this file."), L.T("PNG exportieren", "Export PNG"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ExportPreviewPng();
        }

        private void ExportPreviewPng()
        {
            if (_previewResult == null || _previewResult.Bitmap == null || _previewEntry == null)
                return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                string baseName = Path.GetFileNameWithoutExtension(_previewEntry.Name);
                if (_previewResult.ImageCount > 1)
                    baseName += "_" + _previewImageIndex.ToString("000");
                dialog.FileName = baseName + ".png";
                dialog.Filter = "PNG image (*.png)|*.png";
                dialog.Title = L.T("Vorschau als PNG exportieren", "Export preview as PNG");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                _previewResult.Bitmap.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                _status.Text = L.T("PNG exportiert: ", "PNG exported: ") + Path.GetFileName(dialog.FileName);
            }
        }

        private string BuildHexPreview(byte[] data)
        {
            if (data == null || data.Length == 0)
                return L.T("Leere Datei.", "Empty file.");
            const int max = 32768;
            int length = Math.Min(data.Length, max);
            StringBuilder sb = new StringBuilder(length * 4);
            for (int offset = 0; offset < length; offset += 16)
            {
                sb.Append(offset.ToString("X8"));
                sb.Append("  ");
                int lineEnd = Math.Min(offset + 16, length);
                for (int i = offset; i < offset + 16; i++)
                {
                    if (i < lineEnd)
                        sb.Append(data[i].ToString("X2")).Append(' ');
                    else
                        sb.Append("   ");
                    if (i == offset + 7)
                        sb.Append(' ');
                }

                sb.Append(" | ");
                for (int i = offset; i < lineEnd; i++)
                {
                    byte b = data[i];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }

                sb.AppendLine();
            }

            if (data.Length > max)
            {
                sb.AppendLine();
                sb.AppendLine(L.F("Vorschau auf {0:N0} Bytes begrenzt. Dateigrösse: {1:N0} Bytes.", "Preview limited to {0:N0} bytes. File size: {1:N0} bytes.", max, data.Length));
            }

            return sb.ToString();
        }

        private string GetEntryPath(ArchiveEntry entry)
        {
            if (entry == null || _archive == null || entry == _archive.Root)
                return "/";
            string path = entry.Name;
            ArchiveEntry p = entry.Parent;
            while (p != null && p != _archive.Root)
            {
                path = p.Name + "/" + path;
                p = p.Parent;
            }

            return "/" + path;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024 * 1024)
                return (bytes / 1024.0).ToString("0.0") + " KiB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.00") + " MiB";
        }

        private void MarkDirty()
        {
            _history.Record(_archive.BuildU8());
            _dirty = _history.IsDirty;
            UpdateTitle();
        }

        private void NavigateHistory(bool redo)
        {
            ArchiveEntry selected = SelectedEntry();
            string selectedPath = selected == null ? "/" : GetEntryPath(selected);
            byte[] snapshot = redo ? _history.Redo() : _history.Undo();
            if (snapshot == null)
                return;
            _archive = U8Archive.Load(snapshot);
            _dirty = _history.IsDirty;
            RebuildTree();
            UpdateTitle();
            List<TreeNode> nodes = new List<TreeNode>();
            CollectTreeNodes(_tree.Nodes, nodes);
            foreach (TreeNode node in nodes)
            {
                ArchiveEntry entry = node.Tag as ArchiveEntry;
                if (entry != null && GetEntryPath(entry) == selectedPath)
                {
                    _tree.SelectedNode = node;
                    node.EnsureVisible();
                    break;
                }
            }

            _status.Text = redo ? L.T("Änderung wiederholt.", "Change redone.") : L.T("Änderung rückgängig gemacht.", "Change undone.");
        }

        private void UpdateTitle()
        {
            string name = string.IsNullOrEmpty(_currentPath) ? string.Empty : " — " + Path.GetFileName(_currentPath);
            Text = AppName + " v" + AppVersion + name + (_dirty ? " *" : string.Empty);
            _undoButton.Enabled = _undoMenu.Enabled = _history.CanUndo;
            _redoButton.Enabled = _redoMenu.Enabled = _history.CanRedo;
        }

        private bool ConfirmDiscardChanges()
        {
            if (!_dirty)
                return true;
            DialogResult answer = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Änderungen vor dem Fortfahren speichern?\r\n\r\nJa: Speichern\r\nNein: Änderungen verwerfen\r\nAbbrechen: Weiter bearbeiten", "Save changes before continuing?\r\n\r\nYes: Save\r\nNo: Discard changes\r\nCancel: Keep editing"), AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.No)
                return true;
            SaveArchive(false);
            return !_dirty;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }

            if (_previewResult != null)
            {
                _previewResult.Dispose();
                _previewResult = null;
            }
        }

        private void OnTreeKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                EditSelectedResource();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                RenameSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                ImportTextureSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Up)
            {
                MoveSelected(-1);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                MoveSelected(1);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                FindEntry(false);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                FindEntry(true);
                e.Handled = true;
            }
        }

        private void OnNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            _tree.SelectedNode = e.Node;
            ArchiveEntry entry = e.Node.Tag as ArchiveEntry;
            if (entry == null)
                return;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new MurumsDarkToolStripRenderer();
            menu.BackColor = DarkTheme.Panel;
            menu.ForeColor = DarkTheme.Fore;
            menu.Font = new Font("Segoe UI", 10F);
            if (entry.IsDirectory)
            {
                menu.Items.Add(L.T("Dateien importieren...", "Import files..."), null, delegate
                {
                    ImportFiles();
                });
                menu.Items.Add(L.T("Ordner importieren...", "Import folder..."), null, delegate
                {
                    ImportFolder();
                });
                menu.Items.Add(L.T("Exportieren...", "Export..."), null, delegate
                {
                    ExportSelected();
                });
                menu.Items.Add(L.T("Neuer Ordner...", "New folder..."), null, delegate
                {
                    NewFolder();
                });
            }
            else
            {
                string ext = Path.GetExtension(entry.Name ?? "").ToLowerInvariant();
                ResourceInfo info = ResourceDetector.Detect(entry.Name, entry.Data);
                if (info.Kind == ResourceKind.Brlan)
                    menu.Items.Add(L.T("BRLAN bearbeiten / GIF importieren...", "Edit BRLAN / import GIF..."), null, delegate
                    {
                        EditBrlanEntry(entry);
                    });
                else if (info.Kind == ResourceKind.Brlyt)
                    menu.Items.Add(L.T("BRLYT Layout bearbeiten...", "Edit BRLYT layout..."), null, delegate
                    {
                        EditBrlytEntry(entry);
                    });
                else if (info.Kind == ResourceKind.Tpl)
                    menu.Items.Add(L.T("TPL öffnen / bearbeiten...", "Open / edit TPL..."), null, delegate
                    {
                        EditTplEntry(entry);
                    });
                else
                    menu.Items.Add(L.T("Smart Edit / Inspector...", "Smart Edit / Inspector..."), null, delegate
                    {
                        EditSelectedResource();
                    });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Ersetzen...", "Replace..."), null, delegate
                {
                    ReplaceSelected();
                });
                if (TplTextureEditor.IsTpl(entry.Data))
                {
                    menu.Items.Add(L.T("TPL-Bild importieren...", "Import TPL texture..."), null, delegate
                    {
                        ImportTextureSelected();
                    });
                    menu.Items.Add(L.T("PNG exportieren...", "Export PNG..."), null, delegate
                    {
                        ExportSelectedPreviewPng();
                    });
                }

                menu.Items.Add(L.T("Exportieren...", "Export..."), null, delegate
                {
                    ExportSelected();
                });
            }

            if (_archive != null && entry != _archive.Root)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Duplizieren", "Duplicate"), null, delegate
                {
                    DuplicateSelected();
                });
                menu.Items.Add(L.T("Umbenennen", "Rename"), null, delegate
                {
                    RenameSelected();
                });
                menu.Items.Add(L.T("Nach oben", "Move up"), null, delegate
                {
                    MoveSelected(-1);
                });
                menu.Items.Add(L.T("Nach unten", "Move down"), null, delegate
                {
                    MoveSelected(1);
                });
                menu.Items.Add(L.T("Löschen", "Delete"), null, delegate
                {
                    DeleteSelected();
                });
            }

            menu.Show(_tree, e.Location);
        }

        private void OnNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            ArchiveEntry entry = e.Node == null ? null : e.Node.Tag as ArchiveEntry;
            if (entry == null || entry.IsDirectory)
                return;
            EditSelectedResource();
        }

        private void EditSelectedResource()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null)
            {
                return;
            }

            if (entry.IsDirectory)
            {
                // A folder is a navigation target, not an editable resource.
                // Never show a misleading "select a file" dialog when the user
                // opens/expands folders such as timg.
                if (_tree != null && _tree.SelectedNode != null)
                    _tree.SelectedNode.Expand();
                return;
            }

            ResourceInfo info = ResourceDetector.Detect(entry.Name, entry.Data);
            if (info.Kind == ResourceKind.Brlan)
                EditBrlanEntry(entry);
            else if (info.Kind == ResourceKind.Brlyt)
                EditBrlytEntry(entry);
            else if (info.Kind == ResourceKind.Tpl)
                EditTplEntry(entry);
            else
                InspectArchiveEntry(entry, info);
        }

        private void EditTplEntry(ArchiveEntry entry)
        {
            string temp = Path.Combine(Path.GetTempPath(), "murums_wii_" + Guid.NewGuid().ToString("N") + ".tpl");
            try
            {
                byte[] original = entry.Data == null ? new byte[0] : (byte[])entry.Data.Clone();
                File.WriteAllBytes(temp, original);
                using (StandaloneTextureForm editor = new StandaloneTextureForm(temp))
                    editor.ShowDialog(this);
                byte[] edited = File.ReadAllBytes(temp);
                if (!ByteArraysEqual(original, edited))
                {
                    entry.Data = edited;
                    MarkDirty();
                    RebuildTreeAndSelect(entry);
                    _status.Text = L.T("TPL aktualisiert.", "TPL updated.");
                }
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, "TPL", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void InspectArchiveEntry(ArchiveEntry entry, ResourceInfo info)
        {
            string ext = Path.GetExtension(entry.Name ?? "");
            string temp = Path.Combine(Path.GetTempPath(), "murums_wii_" + Guid.NewGuid().ToString("N") + ext);
            try
            {
                byte[] original = entry.Data == null ? new byte[0] : (byte[])entry.Data.Clone();
                File.WriteAllBytes(temp, original);
                using (FormatInspectorForm inspector = new FormatInspectorForm(temp))
                    inspector.ShowDialog(this);
                byte[] edited = File.Exists(temp) ? File.ReadAllBytes(temp) : original;
                if (!ByteArraysEqual(original, edited))
                {
                    entry.Data = edited;
                    MarkDirty();
                    RebuildTreeAndSelect(entry);
                    _status.Text = L.T("Ressource aktualisiert: ", "Resource updated: ") + entry.Name;
                }
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void EditSelectedBrlan()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null || entry.IsDirectory || !String.Equals(Path.GetExtension(entry.Name), ".brlan", StringComparison.OrdinalIgnoreCase))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle eine .brlan-Datei aus.", "Select a .brlan file."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EditBrlanEntry(entry);
        }

        private void EditSelectedBrlyt()
        {
            ArchiveEntry entry = SelectedEntry();
            if (entry == null || entry.IsDirectory || !String.Equals(Path.GetExtension(entry.Name), ".brlyt", StringComparison.OrdinalIgnoreCase))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle eine .brlyt-Datei aus.", "Select a .brlyt file."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EditBrlytEntry(entry);
        }

        private void SyncResourceEditorLanguage()
        {
            murumsWiiModStudio.Brlan.L.Current = L.Current == UiLanguage.German ? murumsWiiModStudio.Brlan.UiLanguage.German : murumsWiiModStudio.Brlan.UiLanguage.English;
        }

        private ArchiveEntry FindLayoutScope(ArchiveEntry entry)
        {
            if (entry == null)
                return null;
            ArchiveEntry parent = entry.Parent;
            if (parent != null && (String.Equals(parent.Name, "anim", StringComparison.OrdinalIgnoreCase) || String.Equals(parent.Name, "blyt", StringComparison.OrdinalIgnoreCase) || String.Equals(parent.Name, "timg", StringComparison.OrdinalIgnoreCase)))
                return parent.Parent;
            return parent;
        }

        private ArchiveEntry EnsureChildDirectory(ArchiveEntry parent, string name)
        {
            if (parent == null || !parent.IsDirectory)
                return null;
            ArchiveEntry found = parent.FindChild(name);
            if (found != null && found.IsDirectory)
                return found;
            ArchiveEntry created = new ArchiveEntry(name, true);
            parent.AddChild(created);
            return created;
        }

        private void ExportDirectoryFilesToTemp(ArchiveEntry dir, string outputDir, string extension)
        {
            if (dir == null || !dir.IsDirectory)
                return;
            Directory.CreateDirectory(outputDir);
            int i;
            for (i = 0; i < dir.Children.Count; i++)
            {
                ArchiveEntry child = dir.Children[i];
                if (child.IsDirectory)
                    continue;
                if (!String.IsNullOrEmpty(extension) && !String.Equals(Path.GetExtension(child.Name), extension, StringComparison.OrdinalIgnoreCase))
                    continue;
                File.WriteAllBytes(Path.Combine(outputDir, child.Name), child.Data ?? new byte[0]);
            }
        }

        private void EditBrlanEntry(ArchiveEntry entry)
        {
            if (entry == null || entry.IsDirectory)
                return;
            SyncResourceEditorLanguage();
            string tempRoot = Path.Combine(Path.GetTempPath(), "murums_wii_" + Guid.NewGuid().ToString("N"));
            string animDir = Path.Combine(tempRoot, "anim");
            string blytDir = Path.Combine(tempRoot, "blyt");
            string timgDir = Path.Combine(tempRoot, "timg");
            Directory.CreateDirectory(animDir);
            Directory.CreateDirectory(blytDir);
            Directory.CreateDirectory(timgDir);
            string tempBrlan = Path.Combine(animDir, entry.Name);
            byte[] original = entry.Data == null ? new byte[0] : (byte[])entry.Data.Clone();
            File.WriteAllBytes(tempBrlan, original);
            ArchiveEntry scope = FindLayoutScope(entry);
            ArchiveEntry blyt = scope == null ? null : scope.FindChild("blyt");
            ArchiveEntry timg = scope == null ? null : scope.FindChild("timg");
            ExportDirectoryFilesToTemp(blyt, blytDir, ".brlyt");
            ExportDirectoryFilesToTemp(timg, timgDir, ".tpl");
            System.Collections.Generic.List<string> generatedTpls = new System.Collections.Generic.List<string>();
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> generatedTplNamesByPrefix = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (murumsWiiModStudio.Brlan.MainForm editor = new murumsWiiModStudio.Brlan.MainForm())
                {
                    editor.ExternalGifOutputFolder = timgDir;
                    editor.ExternalHostAutoSave = true;
                    editor.ExternalGifCompleted = delegate (murumsWiiModStudio.Brlan.GifImportResult result)
                    {
                        if (result == null)
                            return;
                        string prefix = result.Prefix ?? string.Empty;
                        System.Collections.Generic.HashSet<string> names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        int i;
                        for (i = 0; i < result.TplNames.Count; i++)
                        {
                            string name = result.TplNames[i];
                            names.Add(name);
                            string path = Path.Combine(result.OutputFolder, name);
                            if (File.Exists(path) && !generatedTpls.Contains(path))
                                generatedTpls.Add(path);
                        }

                        if (!String.IsNullOrWhiteSpace(prefix))
                            generatedTplNamesByPrefix[prefix] = names;
                    };
                    editor.OpenFromPath(tempBrlan);
                    editor.ShowDialog(this);
                }

                byte[] edited = File.Exists(tempBrlan) ? File.ReadAllBytes(tempBrlan) : original;
                if (!ByteArraysEqual(original, edited))
                {
                    entry.Data = edited;
                    if (generatedTpls.Count > 0)
                    {
                        ArchiveEntry targetTimg = timg;
                        if (targetTimg == null && scope != null)
                            targetTimg = EnsureChildDirectory(scope, "timg");
                        if (targetTimg != null)
                        {
                            foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.HashSet<string>> pair in generatedTplNamesByPrefix)
                                RemoveStaleGeneratedTpls(targetTimg, pair.Key, pair.Value);
                            ImportGeneratedTpls(targetTimg, generatedTpls);
                        }
                    }

                    MarkDirty();
                    RebuildTreeAndSelect(entry);
                    _status.Text = L.F("BRLAN aktualisiert{0}.", "BRLAN updated{0}.", generatedTpls.Count > 0 ? L.F(" + {0} TPLs", " + {0} TPLs", generatedTpls.Count) : "");
                }
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("BRLAN-Editor", "BRLAN editor"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                catch
                {
                }
            }
        }

        private static void RemoveStaleGeneratedTpls(ArchiveEntry timg, string prefix, System.Collections.Generic.HashSet<string> keepNames)
        {
            if (timg == null || !timg.IsDirectory || String.IsNullOrWhiteSpace(prefix))
                return;
            string start = prefix + "_";
            for (int i = timg.Children.Count - 1; i >= 0; i--)
            {
                ArchiveEntry child = timg.Children[i];
                if (child == null || child.IsDirectory)
                    continue;
                string name = child.Name ?? string.Empty;
                if (!name.StartsWith(start, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
                    continue;
                string middle = name.Substring(start.Length, name.Length - start.Length - 4);
                int frameIndex;
                if (!Int32.TryParse(middle, out frameIndex))
                    continue;
                if (keepNames != null && keepNames.Contains(name))
                    continue;
                timg.Children.RemoveAt(i);
            }
        }

        private void ImportGeneratedTpls(ArchiveEntry timg, System.Collections.Generic.List<string> files)
        {
            int i;
            for (i = 0; i < files.Count; i++)
            {
                string path = files[i];
                if (!File.Exists(path))
                    continue;
                string name = Path.GetFileName(path);
                byte[] data = File.ReadAllBytes(path);
                ArchiveEntry existing = timg.FindChild(name);
                if (existing != null && !existing.IsDirectory)
                    existing.Data = data;
                else
                    timg.AddChild(new ArchiveEntry(name, false) { Data = data });
            }
        }

        private void EditBrlytEntry(ArchiveEntry entry)
        {
            if (entry == null || entry.IsDirectory)
                return;
            SyncResourceEditorLanguage();
            string tempRoot = Path.Combine(Path.GetTempPath(), "murums_wii_" + Guid.NewGuid().ToString("N"));
            string blytDir = Path.Combine(tempRoot, "blyt");
            Directory.CreateDirectory(blytDir);
            string timgDir = Path.Combine(tempRoot, "timg");
            Directory.CreateDirectory(timgDir);
            string tempBrlyt = Path.Combine(blytDir, entry.Name);
            byte[] original = entry.Data == null ? new byte[0] : (byte[])entry.Data.Clone();
            File.WriteAllBytes(tempBrlyt, original);
            ArchiveEntry scope = FindLayoutScope(entry);
            ArchiveEntry timg = scope == null ? null : scope.FindChild("timg");
            ExportDirectoryFilesToTemp(timg, timgDir, ".tpl");
            try
            {
                using (murumsWiiModStudio.Brlan.BrlytEditorForm editor = new murumsWiiModStudio.Brlan.BrlytEditorForm(tempBrlyt))
                    editor.ShowDialog(this);
                byte[] edited = File.Exists(tempBrlyt) ? File.ReadAllBytes(tempBrlyt) : original;
                if (!ByteArraysEqual(original, edited))
                {
                    entry.Data = edited;
                    MarkDirty();
                    RebuildTreeAndSelect(entry);
                    _status.Text = L.T("BRLYT aktualisiert.", "BRLYT updated.");
                }
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("BRLYT-Editor", "BRLYT editor"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                catch
                {
                }
            }
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

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
                return;
            if (_archive == null)
            {
                if (File.Exists(paths[0]))
                    OpenFromPath(paths[0]);
                return;
            }

            ArchiveEntry dir = SelectedDirectory();
            if (dir != null)
                ImportFilePaths(dir, paths);
        }

        private void OnTreeItemDrag(object sender, ItemDragEventArgs e)
        {
            if (_archive == null)
                return;
            TreeNode node = e.Item as TreeNode;
            ArchiveEntry entry = node == null ? null : node.Tag as ArchiveEntry;
            if (entry == null || entry == _archive.Root)
                return;
            _tree.SelectedNode = node;
            _tree.DoDragDrop(node, DragDropEffects.Move);
            ClearTreeDropHighlight();
        }

        private void OnTreeDragEnter(object sender, DragEventArgs e)
        {
            OnTreeDragOver(sender, e);
        }

        private void OnTreeDragOver(object sender, DragEventArgs e)
        {
            if (_archive == null || e.Data == null)
            {
                e.Effect = DragDropEffects.None;
                ClearTreeDropHighlight();
                return;
            }

            Point point = _tree.PointToClient(new Point(e.X, e.Y));
            AutoScrollTreeDuringDrag(point);
            TreeNode targetNode = _tree.GetNodeAt(point);
            if (e.Data.GetDataPresent(typeof(TreeNode)))
            {
                TreeNode sourceNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
                ArchiveEntry newParent;
                int newIndex;
                TreeDropPlacement placement;
                if (TryResolveTreeDrop(sourceNode, targetNode, point, out newParent, out newIndex, out placement))
                {
                    e.Effect = DragDropEffects.Move;
                    SetTreeDropHighlight(targetNode);
                    if (placement == TreeDropPlacement.Into && targetNode != null && !targetNode.IsExpanded)
                    {
                        ArchiveEntry targetEntry = targetNode.Tag as ArchiveEntry;
                        if (targetEntry != null && targetEntry.IsDirectory)
                            targetNode.Expand();
                    }

                    return;
                }

                e.Effect = DragDropEffects.None;
                ClearTreeDropHighlight();
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                ArchiveEntry target = targetNode == null ? null : targetNode.Tag as ArchiveEntry;
                ArchiveEntry dir = target == null ? null : (target.IsDirectory ? target : target.Parent);
                if (dir != null)
                {
                    e.Effect = DragDropEffects.Copy;
                    TreeNode highlight = target != null && target.IsDirectory ? targetNode : (targetNode == null ? null : targetNode.Parent);
                    SetTreeDropHighlight(highlight);
                    return;
                }
            }

            e.Effect = DragDropEffects.None;
            ClearTreeDropHighlight();
        }

        private void OnTreeDragLeave(object sender, EventArgs e)
        {
            ClearTreeDropHighlight();
        }

        private void OnTreeDragDrop(object sender, DragEventArgs e)
        {
            if (_archive == null || e.Data == null)
                return;
            Point point = _tree.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = _tree.GetNodeAt(point);
            ClearTreeDropHighlight();
            if (e.Data.GetDataPresent(typeof(TreeNode)))
            {
                TreeNode sourceNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
                ArchiveEntry newParent;
                int newIndex;
                TreeDropPlacement placement;
                if (!TryResolveTreeDrop(sourceNode, targetNode, point, out newParent, out newIndex, out placement))
                    return;
                ArchiveEntry source = sourceNode == null ? null : sourceNode.Tag as ArchiveEntry;
                if (source == null || source.Parent == null)
                    return;
                ArchiveEntry oldParent = source.Parent;
                int oldIndex = oldParent.Children.IndexOf(source);
                if (oldIndex < 0)
                    return;
                oldParent.Children.RemoveAt(oldIndex);
                if (newIndex < 0)
                    newIndex = 0;
                if (newIndex > newParent.Children.Count)
                    newIndex = newParent.Children.Count;
                source.Parent = newParent;
                newParent.Children.Insert(newIndex, source);
                MarkDirty();
                RebuildTreeAndSelect(source);
                _status.Text = L.T("Verschoben: ", "Moved: ") + GetEntryPath(source);
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                ArchiveEntry target = targetNode == null ? null : targetNode.Tag as ArchiveEntry;
                ArchiveEntry dir = target == null ? null : (target.IsDirectory ? target : target.Parent);
                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (dir != null)
                    ImportFilePaths(dir, paths);
            }
        }

        private bool TryResolveTreeDrop(TreeNode sourceNode, TreeNode targetNode, Point point, out ArchiveEntry newParent, out int newIndex, out TreeDropPlacement placement)
        {
            newParent = null;
            newIndex = -1;
            placement = TreeDropPlacement.Into;
            if (_archive == null || sourceNode == null || targetNode == null)
                return false;
            ArchiveEntry source = sourceNode.Tag as ArchiveEntry;
            ArchiveEntry target = targetNode.Tag as ArchiveEntry;
            if (source == null || target == null || source == _archive.Root || source.Parent == null)
                return false;
            if (target == _archive.Root)
            {
                placement = TreeDropPlacement.Into;
                newParent = target;
                newIndex = target.Children.Count;
            }
            else
            {
                Rectangle bounds = targetNode.Bounds;
                int height = Math.Max(1, bounds.Height);
                int relativeY = point.Y - bounds.Top;
                if (target.IsDirectory)
                {
                    int edge = Math.Max(4, height / 4);
                    if (relativeY < edge)
                        placement = TreeDropPlacement.Before;
                    else if (relativeY >= height - edge)
                        placement = TreeDropPlacement.After;
                    else
                        placement = TreeDropPlacement.Into;
                }
                else
                {
                    placement = relativeY < height / 2 ? TreeDropPlacement.Before : TreeDropPlacement.After;
                }

                if (placement == TreeDropPlacement.Into)
                {
                    newParent = target;
                    newIndex = target.Children.Count;
                }
                else
                {
                    newParent = target.Parent;
                    if (newParent == null)
                        return false;
                    int targetIndex = newParent.Children.IndexOf(target);
                    if (targetIndex < 0)
                        return false;
                    newIndex = targetIndex + (placement == TreeDropPlacement.After ? 1 : 0);
                }
            }

            if (newParent == null || !newParent.IsDirectory)
                return false;
            if (source.IsDirectory && IsSameOrDescendant(newParent, source))
                return false;
            ArchiveEntry duplicate = newParent.FindChild(source.Name);
            if (duplicate != null && !object.ReferenceEquals(duplicate, source))
                return false;
            if (object.ReferenceEquals(source.Parent, newParent))
            {
                int oldIndex = source.Parent.Children.IndexOf(source);
                if (oldIndex < 0)
                    return false;
                if (oldIndex < newIndex)
                    newIndex--;
                if (newIndex == oldIndex)
                    return false;
            }

            return true;
        }

        private bool IsSameOrDescendant(ArchiveEntry possibleChild, ArchiveEntry possibleParent)
        {
            ArchiveEntry current = possibleChild;
            while (current != null)
            {
                if (object.ReferenceEquals(current, possibleParent))
                    return true;
                current = current.Parent;
            }

            return false;
        }

        private void SetTreeDropHighlight(TreeNode node)
        {
            if (object.ReferenceEquals(_dragHoverNode, node))
                return;
            ClearTreeDropHighlight();
            _dragHoverNode = node;
            if (_dragHoverNode != null)
            {
                _dragHoverNode.BackColor = DarkTheme.AccentSoft;
                _tree.Invalidate(_dragHoverNode.Bounds);
            }
        }

        private void ClearTreeDropHighlight()
        {
            if (_dragHoverNode == null)
                return;
            TreeNode old = _dragHoverNode;
            _dragHoverNode = null;
            old.BackColor = Color.Empty;
            if (_tree != null)
                _tree.Invalidate(old.Bounds);
        }

        private void AutoScrollTreeDuringDrag(Point point)
        {
            if (_tree == null || _tree.Nodes.Count == 0)
                return;
            const int margin = 26;
            if (point.Y < margin)
            {
                TreeNode top = _tree.TopNode;
                TreeNode previous = top == null ? null : top.PrevVisibleNode;
                if (previous != null)
                    _tree.TopNode = previous;
            }
            else if (point.Y > _tree.ClientSize.Height - margin)
            {
                TreeNode nearBottom = _tree.GetNodeAt(8, Math.Max(0, _tree.ClientSize.Height - 6));
                if (nearBottom == null)
                    nearBottom = _tree.TopNode;
                TreeNode next = nearBottom == null ? null : nearBottom.NextVisibleNode;
                if (next != null)
                    next.EnsureVisible();
            }
        }

        private void ShowAbout()
        {
            using (var about = new StudioAboutForm(AppVersion))
                about.ShowDialog(this);
        }
    }
}
