using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class MainForm : Form
    {
        private const string AppName = "murums Wii Mod Studio — BRLAN Editor";
        private const string AppVersion = murumsWiiModStudio.StudioVersion.Current;
        private BrlanDocument _doc;
        private string _sourcePath;
        // Integration hooks used by murums Wii Mod Studio when this editor is opened
        // directly from an SZS/U8 archive. Generated TPLs are staged by the host
        // and committed together with the edited BRLAN when the editor closes.
        internal string ExternalGifOutputFolder { get; set; }
        internal Action<GifImportResult> ExternalGifCompleted { get; set; }
        internal bool ExternalHostAutoSave { get; set; }

        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private TreeView _tree;
        private PropertyGrid _properties;
        private DataGridView _keys;
        private TextBox _raw;
        private ListBox _textures;
        private ListView _issues;
        private ListBox _helpNav;
        private RichTextBox _helpContent;
        private Label _helpHeading;
        private TabControl _tabs;
        private TabPage _helpTab;
        private ToolStripStatusLabel _status;
        private Label _selectionInfo;
        private EntryModel _gridEntry;
        private TagModel _gridTag;
        private bool _loadingGrid;
        private Button _addKey;
        private Button _deleteKey;
        private Button _sortKeys;
        // BrawlBox/Brawl Studio style tree interaction.
        private TreeNode _dragHoverNode;
        private int _textureDragIndex = -1;
        private Point _textureDragStart;
        public MainForm()
        {
            Text = AppName + " v" + AppVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 720);
            Size = new Size(1380, 860);
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
            // Robustes Hauptlayout: keine überlappenden Dock-Controls mehr.
            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(0);
            shell.Padding = new Padding(0);
            shell.ColumnCount = 1;
            shell.RowCount = 5;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F)); // Branding
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F)); // Menü
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F)); // Toolbar
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Workspace
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // Status
            Controls.Add(shell);
            Panel brand = murumsWiiModStudio.StudioChrome.Header(L.T("Menüanimationen bearbeiten", "Edit menu animations"), L.T("Animation öffnen • Bilder und Keyframes bearbeiten • Kopie speichern", "Open an animation • Edit pictures and keyframes • Save a copy"));
            shell.Controls.Add(brand, 0, 0);
            _menu = new MenuStrip();
            _menu.Dock = DockStyle.Fill;
            _menu.Margin = new Padding(0);
            _menu.Padding = new Padding(8, 3, 0, 3);
            _menu.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            ToolStripMenuItem file = new ToolStripMenuItem(L.T("Datei", "File"));
            ToolStripMenuItem openMenu = new ToolStripMenuItem(L.T("Öffnen...", "Open..."), null, delegate
            {
                OpenDialog();
            });
            openMenu.ShortcutKeys = Keys.Control | Keys.O;
            file.DropDownItems.Add(openMenu);
            ToolStripMenuItem saveMenu = new ToolStripMenuItem(L.T("Speichern", "Save"), null, delegate
            {
                Save();
            });
            saveMenu.ShortcutKeys = Keys.Control | Keys.S;
            file.DropDownItems.Add(saveMenu);
            ToolStripMenuItem saveAsMenu = new ToolStripMenuItem(L.T("Speichern unter...", "Save as..."), null, delegate
            {
                SaveAs();
            });
            saveAsMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            file.DropDownItems.Add(saveAsMenu);
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(L.T("Neu laden", "Reload"), null, delegate
            {
                ReloadOriginal();
            });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(L.T("Beenden", "Exit"), null, delegate
            {
                Close();
            });
            ToolStripMenuItem edit = new ToolStripMenuItem(L.T("Bearbeiten", "Edit"));
            edit.DropDownItems.Add(L.T("Animation hinzufügen", "Add animation"), null, delegate
            {
                AddAnimation();
            });
            edit.DropDownItems.Add(L.T("Tag hinzufügen", "Add tag"), null, delegate
            {
                AddTag();
            });
            edit.DropDownItems.Add(L.T("Entry hinzufügen", "Add entry"), null, delegate
            {
                AddEntry();
            });
            edit.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem duplicateMenu = new ToolStripMenuItem(L.T("Auswahl duplizieren", "Duplicate selection"), null, delegate
            {
                DuplicateSelected();
            });
            duplicateMenu.ShortcutKeys = Keys.Control | Keys.D;
            edit.DropDownItems.Add(duplicateMenu);
            ToolStripMenuItem moveUpMenu = new ToolStripMenuItem(L.T("Nach oben", "Move up"), null, delegate
            {
                MoveTreeSelected(-1);
            });
            moveUpMenu.ShortcutKeys = Keys.Control | Keys.Up;
            edit.DropDownItems.Add(moveUpMenu);
            ToolStripMenuItem moveDownMenu = new ToolStripMenuItem(L.T("Nach unten", "Move down"), null, delegate
            {
                MoveTreeSelected(1);
            });
            moveDownMenu.ShortcutKeys = Keys.Control | Keys.Down;
            edit.DropDownItems.Add(moveDownMenu);
            ToolStripMenuItem deleteMenu = new ToolStripMenuItem(L.T("Auswahl löschen", "Delete selection"), null, delegate
            {
                DeleteSelected();
            });
            deleteMenu.ShortcutKeys = Keys.Delete;
            edit.DropDownItems.Add(deleteMenu);
            ToolStripMenuItem tools = new ToolStripMenuItem(L.T("Tools", "Tools"));
            tools.DropDownItems.Add(L.T("RLTP Generator...", "RLTP Generator..."), null, delegate
            {
                RunRltpWizard();
            });
            tools.DropDownItems.Add(L.T("GIF Import...", "GIF Import..."), null, delegate
            {
                OpenGifImporter();
            });
            tools.DropDownItems.Add(L.T("BRLYT Material Mapper...", "BRLYT Material Mapper..."), null, delegate
            {
                OpenBrlytMapper();
            });
            tools.DropDownItems.Add(L.T("Validieren", "Validate"), null, delegate
            {
                ValidateDocument();
            });
            tools.DropDownItems.Add(L.T("Roundtrip-Selbsttest", "Roundtrip self-test"), null, delegate
            {
                RunRoundtripSelfTest();
            });
            tools.DropDownItems.Add(L.T("Alle Keyframes sortieren", "Sort all keyframes"), null, delegate
            {
                SortAllKeyframes();
            });
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
            ToolStripMenuItem help = new ToolStripMenuItem(L.T("Hilfe", "Help"));
            var helpOverview = new ToolStripMenuItem(L.T("Funktionsübersicht", "Feature overview"), null, delegate
            {
                ShowHelpTab();
            });
            helpOverview.ShortcutKeys = Keys.F1;
            help.DropDownItems.Add(helpOverview);
            help.DropDownItems.Add(L.T("Über ", "About ") + AppName, null, delegate
            {
                ShowAbout();
            });
            _menu.Items.Add(file);
            _menu.Items.Add(edit);
            _menu.Items.Add(tools);
            _menu.Items.Add(language);
            _menu.Items.Add(help);
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
            _toolbar.Items.Add(MakeToolButton(L.T("Speichern", "Save"), delegate
            {
                Save();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Speichern unter", "Save as"), delegate
            {
                SaveAs();
            }));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton(L.T("+ Animation", "+ Animation"), delegate
            {
                AddAnimation();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("+ Tag", "+ Tag"), delegate
            {
                AddTag();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("+ Entry", "+ Entry"), delegate
            {
                AddEntry();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Duplizieren", "Duplicate"), delegate
            {
                DuplicateSelected();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Löschen", "Delete"), delegate
            {
                DeleteSelected();
            }));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton(L.T("RLTP Generator", "RLTP Generator"), delegate
            {
                RunRltpWizard();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("GIF Import", "GIF Import"), delegate
            {
                OpenGifImporter();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("BRLYT Map", "BRLYT Map"), delegate
            {
                OpenBrlytMapper();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Validieren", "Validate"), delegate
            {
                ValidateDocument();
            }));
            _toolbar.Items.Add(MakeToolButton(L.T("Selbsttest", "Self-test"), delegate
            {
                RunRoundtripSelfTest();
            }));
            shell.Controls.Add(_toolbar, 0, 2);
            SplitContainer mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Margin = new Padding(0);
            mainSplit.FixedPanel = FixedPanel.Panel1;
            mainSplit.SplitterWidth = 4;
            mainSplit.BackColor = DarkTheme.Border;
            shell.Controls.Add(mainSplit, 0, 3);
            // Wichtig: SplitterDistance erst setzen, nachdem das Fenster seine
            // echte Breite besitzt. Panel-MinSize-Werte während BuildUi können
            // bei WinForms sonst bereits beim Start eine InvalidOperationException
            // auslösen.
            Shown += delegate
            {
                try
                {
                    int desired = 285;
                    int minimumLeft = 180;
                    int minimumRight = 420;
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
                // Layout darf den Programmstart niemals verhindern.
                }
            };
            // Linke Seite: explizite Zeilen statt überlappender Dock-Controls.
            TableLayoutPanel leftLayout = new TableLayoutPanel();
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.Margin = new Padding(0);
            leftLayout.Padding = new Padding(0);
            leftLayout.ColumnCount = 1;
            leftLayout.RowCount = 2;
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label structureLabel = new Label();
            structureLabel.Text = L.T("STRUKTUR", "STRUCTURE");
            structureLabel.Dock = DockStyle.Fill;
            structureLabel.Padding = new Padding(12, 11, 0, 0);
            structureLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            structureLabel.ForeColor = DarkTheme.Muted;
            structureLabel.BackColor = DarkTheme.Panel2;
            leftLayout.Controls.Add(structureLabel, 0, 0);
            _tree = new TreeView();
            _tree.Dock = DockStyle.Fill;
            _tree.Margin = new Padding(0);
            _tree.HideSelection = false;
            _tree.FullRowSelect = true;
            _tree.ShowLines = true;
            _tree.ShowPlusMinus = true;
            _tree.ShowRootLines = true;
            _tree.Indent = 22;
            _tree.ItemHeight = 27;
            _tree.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _tree.AllowDrop = true;
            _tree.AfterSelect += TreeAfterSelect;
            _tree.NodeMouseClick += OnTreeNodeMouseClick;
            _tree.KeyDown += OnTreeKeyDown;
            _tree.ItemDrag += OnTreeItemDrag;
            _tree.DragEnter += OnTreeDragEnter;
            _tree.DragOver += OnTreeDragOver;
            _tree.DragLeave += OnTreeDragLeave;
            _tree.DragDrop += OnTreeDragDrop;
            leftLayout.Controls.Add(_tree, 0, 1);
            mainSplit.Panel1.Controls.Add(leftLayout);
            _tabs = new DarkTabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Margin = new Padding(0);
            _tabs.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            TabPage editorTab = MakeTabPage(L.T("Editor", "Editor"));
            SplitContainer editorSplit = new SplitContainer();
            editorSplit.Dock = DockStyle.Fill;
            editorSplit.Margin = new Padding(0);
            editorSplit.Orientation = Orientation.Horizontal;
            editorSplit.SplitterWidth = 6;
            editorSplit.BackColor = DarkTheme.Border;
            Shown += delegate
            {
                try
                {
                    int minTop = 180;
                    int minBottom = 160;
                    int desired = 340;
                    int max = editorSplit.Height - minBottom - editorSplit.SplitterWidth;
                    if (desired > max)
                        desired = max;
                    if (desired < minTop)
                        desired = minTop;
                    if (desired > 0 && desired < editorSplit.Height - editorSplit.SplitterWidth)
                        editorSplit.SplitterDistance = desired;
                }
                catch
                {
                }
            };
            TableLayoutPanel propertyLayout = new TableLayoutPanel();
            propertyLayout.Dock = DockStyle.Fill;
            propertyLayout.Margin = new Padding(0);
            propertyLayout.Padding = new Padding(0);
            propertyLayout.ColumnCount = 1;
            propertyLayout.RowCount = 2;
            propertyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            propertyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            propertyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _selectionInfo = new Label();
            _selectionInfo.Dock = DockStyle.Fill;
            _selectionInfo.Padding = new Padding(12, 12, 8, 4);
            _selectionInfo.Text = L.T("Keine Auswahl. Wähle links einen Eintrag aus.", "Nothing selected. Choose an item in the tree on the left.");
            _selectionInfo.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            _selectionInfo.BackColor = DarkTheme.Panel2;
            _selectionInfo.ForeColor = Color.White;
            propertyLayout.Controls.Add(_selectionInfo, 0, 0);
            _properties = new PropertyGrid();
            _properties.Dock = DockStyle.Fill;
            _properties.Margin = new Padding(0);
            _properties.ToolbarVisible = false;
            _properties.HelpVisible = true;
            _properties.HelpVisible = true;
            _properties.PropertySort = PropertySort.Categorized;
            _properties.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _properties.PropertyValueChanged += PropertyChanged;
            propertyLayout.Controls.Add(_properties, 0, 1);
            editorSplit.Panel1.Controls.Add(propertyLayout);
            TableLayoutPanel keyLayout = new TableLayoutPanel();
            keyLayout.Dock = DockStyle.Fill;
            keyLayout.Margin = new Padding(0);
            keyLayout.Padding = new Padding(0);
            keyLayout.ColumnCount = 1;
            keyLayout.RowCount = 2;
            keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            keyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            keyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            FlowLayoutPanel keyTools = new FlowLayoutPanel();
            keyTools.Dock = DockStyle.Fill;
            keyTools.Margin = new Padding(0);
            keyTools.FlowDirection = FlowDirection.LeftToRight;
            keyTools.WrapContents = false;
            keyTools.AutoScroll = true;
            keyTools.Padding = new Padding(8, 7, 0, 0);
            keyTools.BackColor = DarkTheme.Panel2;
            _addKey = MakeButton(L.T("+ Keyframe", "+ Keyframe"), 112);
            _addKey.Click += delegate
            {
                AddKeyframe();
            };
            _deleteKey = MakeButton(L.T("Keyframe löschen", "Delete keyframe"), 138);
            _deleteKey.Click += delegate
            {
                DeleteKeyframe();
            };
            _sortKeys = MakeButton(L.T("Nach Frame sortieren", "Sort by frame"), 155);
            _sortKeys.Click += delegate
            {
                SortCurrentKeyframes();
            };
            keyTools.Controls.Add(_addKey);
            keyTools.Controls.Add(_deleteKey);
            keyTools.Controls.Add(_sortKeys);
            keyLayout.Controls.Add(keyTools, 0, 0);
            _keys = new DataGridView();
            _keys.Dock = DockStyle.Fill;
            _keys.Margin = new Padding(0);
            _keys.AllowUserToAddRows = false;
            _keys.AllowUserToDeleteRows = false;
            _keys.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _keys.RowHeadersVisible = false;
            _keys.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _keys.MultiSelect = true;
            _keys.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _keys.RowTemplate.Height = 28;
            _keys.ColumnHeadersHeight = 32;
            _keys.CellEndEdit += KeyCellEndEdit;
            keyLayout.Controls.Add(_keys, 0, 1);
            editorSplit.Panel2.Controls.Add(keyLayout);
            editorTab.Controls.Add(editorSplit);
            _tabs.TabPages.Add(editorTab);
            TabPage textureTab = MakeTabPage(L.T("TPL-Dateien", "TPL Files"));
            BuildTextureTab(textureTab);
            _tabs.TabPages.Add(textureTab);
            TabPage validateTab = MakeTabPage(L.T("Validator", "Validator"));
            BuildValidatorTab(validateTab);
            _tabs.TabPages.Add(validateTab);
            TabPage rawTab = MakeTabPage(L.T("Raw / Hex", "Raw / Hex"));
            _raw = new TextBox();
            _raw.Dock = DockStyle.Fill;
            _raw.Margin = new Padding(8);
            _raw.Multiline = true;
            _raw.ReadOnly = true;
            _raw.ScrollBars = ScrollBars.Both;
            _raw.WordWrap = false;
            _raw.Font = new Font("Consolas", 10.5F, FontStyle.Regular);
            rawTab.Controls.Add(_raw);
            _tabs.TabPages.Add(rawTab);
            _helpTab = MakeTabPage(L.T("Hilfe", "Help"));
            BuildHelpTab(_helpTab);
            Disposed += delegate
            {
                _helpTab.Dispose();
            };
            mainSplit.Panel2.Controls.Add(_tabs);
            StatusStrip statusStrip = new StatusStrip();
            statusStrip.Dock = DockStyle.Fill;
            statusStrip.Margin = new Padding(0);
            statusStrip.SizingGrip = false;
            statusStrip.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            _status = new ToolStripStatusLabel();
            _status.Text = L.T("Bereit. Ziehe eine .brlan auf das Fenster oder wähle Datei > Öffnen.", "Ready. Drop a .brlan onto the window or choose File > Open.");
            _status.Spring = true;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            statusStrip.Items.Add(_status);
            shell.Controls.Add(statusStrip, 0, 4);
            SetKeyControlsEnabled(false);
            ResumeLayout(true);
        }

        private TabPage MakeTabPage(string title)
        {
            TabPage page = new TabPage(title);
            page.BackColor = DarkTheme.Back;
            page.ForeColor = DarkTheme.Fore;
            page.Padding = new Padding(8);
            page.UseVisualStyleBackColor = false;
            return page;
        }

        private void BuildTextureTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _textures = new ListBox();
            _textures.Dock = DockStyle.Fill;
            _textures.Margin = new Padding(0, 0, 8, 0);
            _textures.Font = new Font("Consolas", 10.5F, FontStyle.Regular);
            _textures.IntegralHeight = false;
            _textures.AllowDrop = true;
            _textures.MouseDown += OnTextureMouseDown;
            _textures.MouseMove += OnTextureMouseMove;
            _textures.DragEnter += OnTextureDragEnter;
            _textures.DragOver += OnTextureDragOver;
            _textures.DragDrop += OnTextureDragDrop;
            _textures.KeyDown += OnTextureKeyDown;
            layout.Controls.Add(_textures, 0, 0);
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0);
            buttons.FlowDirection = FlowDirection.TopDown;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(8);
            buttons.BackColor = DarkTheme.Panel2;
            Label textureHint = new Label();
            textureHint.Width = 160;
            textureHint.Height = 82;
            textureHint.ForeColor = DarkTheme.Muted;
            textureHint.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            textureHint.Text = L.T("Einsteiger-Tipp:\nEine TPL pro Bild. RLTP verwendet die Nummer in [Klammern] als Bildindex.", "Beginner tip:\nOne TPL per image. RLTP uses the number in [brackets] as the image index.");
            buttons.Controls.Add(textureHint);
            Button add = MakeButton(L.T("Hinzufügen", "Add"), 160);
            add.Click += delegate
            {
                AddTexture();
            };
            Button rename = MakeButton(L.T("Umbenennen", "Rename"), 160);
            rename.Click += delegate
            {
                RenameTexture();
            };
            Button del = MakeButton(L.T("Löschen", "Delete"), 160);
            del.Click += delegate
            {
                DeleteTexture();
            };
            Button up = MakeButton(L.T("Nach oben", "Move up"), 160);
            up.Click += delegate
            {
                MoveTexture(-1);
            };
            Button down = MakeButton(L.T("Nach unten", "Move down"), 160);
            down.Click += delegate
            {
                MoveTexture(1);
            };
            Button paste = MakeButton(L.T("Liste einfügen...", "Paste list..."), 160);
            paste.Click += delegate
            {
                PasteTextureList();
            };
            buttons.Controls.Add(add);
            buttons.Controls.Add(rename);
            buttons.Controls.Add(del);
            buttons.Controls.Add(up);
            buttons.Controls.Add(down);
            buttons.Controls.Add(paste);
            layout.Controls.Add(buttons, 1, 0);
            tab.Controls.Add(layout);
        }

        private void BuildValidatorTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Panel commandBar = new Panel();
            commandBar.Dock = DockStyle.Fill;
            commandBar.Margin = new Padding(0);
            commandBar.BackColor = DarkTheme.Panel2;
            Button validate = MakeButton(L.T("Jetzt validieren", "Validate now"), 160);
            validate.Left = 8;
            validate.Top = 8;
            validate.Click += delegate
            {
                ValidateDocument();
            };
            commandBar.Controls.Add(validate);
            layout.Controls.Add(commandBar, 0, 0);
            _issues = new ListView();
            _issues.Dock = DockStyle.Fill;
            _issues.Margin = new Padding(0);
            _issues.View = View.Details;
            _issues.FullRowSelect = true;
            _issues.GridLines = true;
            _issues.HideSelection = false;
            _issues.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _issues.Columns.Add("Status", 100);
            _issues.Columns.Add(L.T("Ort", "Location"), 330);
            _issues.Columns.Add(L.T("Beschreibung", "Description"), 760);
            layout.Controls.Add(_issues, 0, 1);
            tab.Controls.Add(layout);
        }

        private void ApplyTheme()
        {
            DarkTheme.Apply(this);
            DarkTheme.StyleTree(_tree);
            DarkTheme.StylePropertyGrid(_properties);
            DarkTheme.StyleGrid(_keys);
            DarkTheme.StyleTabs(_tabs);
            DarkTheme.StyleListBox(_textures);
            DarkTheme.StyleListBox(_helpNav);
            _raw.BackColor = DarkTheme.Panel;
            _raw.ForeColor = DarkTheme.Fore;
            _helpNav.BackColor = DarkTheme.Panel2;
            _helpNav.ForeColor = DarkTheme.Fore;
            _helpContent.BackColor = DarkTheme.Panel;
            _helpContent.ForeColor = DarkTheme.Fore;
            _helpHeading.BackColor = DarkTheme.Panel2;
            _helpHeading.ForeColor = Color.White;
            _textures.BackColor = DarkTheme.Panel;
            _textures.ForeColor = DarkTheme.Fore;
            _issues.BackColor = DarkTheme.Panel;
            _issues.ForeColor = DarkTheme.Fore;
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

        private ToolStripButton MakeToolButton(string text, EventHandler click)
        {
            ToolStripButton b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            return b;
        }

        private Button MakeButton(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            b.Margin = new Padding(3);
            return b;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
                OpenFromPath(files[0]);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            CommitGridToEntry(true);
            if (ExternalHostAutoSave && _doc != null && !String.IsNullOrEmpty(_sourcePath))
            {
                if (!PrepareForSave() || !WriteDocumentToPath(_sourcePath, false))
                    e.Cancel = true;
            }
        }

        private void OpenDialog()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = L.T("BRLAN-Dateien (*.brlan)|*.brlan|Alle Dateien (*.*)|*.*", "BRLAN files (*.brlan)|*.brlan|All files (*.*)|*.*");
                dlg.Title = L.T("BRLAN öffnen", "Open BRLAN");
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    OpenFromPath(dlg.FileName);
            }
        }

        public void OpenFromPath(string path)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                BrlanDocument parsed = BrlanCodec.Parse(bytes);
                parsed.SourcePath = path;
                _doc = parsed;
                _sourcePath = path;
                Text = AppName + " v" + AppVersion + " — " + Path.GetFileName(path);
                RefreshAllUi();
                SetStatus(L.T("Geöffnet: ", "Opened: ") + Path.GetFileName(path) + " | Version " + _doc.Version.ToString() + " | " + _doc.Pai.Frames.ToString() + " Frames | " + _doc.Pai.Animations.Count.ToString() + L.T(" Animation(en)", " animation(s)"));
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("BRLAN konnte nicht geöffnet werden", "Could not open BRLAN"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus(L.T("Fehler beim Öffnen: ", "Open error: ") + ex.Message);
            }
        }

        private void ReloadOriginal()
        {
            if (String.IsNullOrEmpty(_sourcePath) || !File.Exists(_sourcePath))
                return;
            if (murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Alle nicht gespeicherten Änderungen verwerfen und Original neu laden?", "Discard all unsaved changes and reload the original?"), L.T("Neu laden", "Reload"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                OpenFromPath(_sourcePath);
        }

        private bool PrepareForSave()
        {
            if (_doc == null)
                return false;
            if (!CommitGridToEntry(false))
                return false;
            List<ValidationIssue> issues = BrlanCodec.Validate(_doc);
            bool hasErrors = false;
            int i;
            for (i = 0; i < issues.Count; i++)
                if (issues[i].Severity == "Fehler")
                    hasErrors = true;
            if (hasErrors)
            {
                if (murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Der Validator meldet mindestens einen Fehler. Trotzdem speichern?\r\n\r\nEmpfehlung: zuerst im Validator prüfen.", "The validator reports at least one error. Save anyway?\r\n\r\nRecommendation: review the Validator first."), L.T("Validierungsfehler", "Validation error"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    ValidateDocument();
                    return false;
                }
            }

            return true;
        }

        private bool WriteDocumentToPath(string path, bool showSuccessDialog)
        {
            try
            {
                byte[] output = BrlanCodec.Build(_doc);
                // Zuerst den erzeugten Speicherinhalt erneut parsen. Dadurch wird
                // eine bestehende Datei nicht überschrieben, falls der Build selbst
                // bereits ungültige BRLAN-Daten erzeugt hätte.
                BrlanCodec.Parse(output);
                murumsWiiModStudio.BackupManager.WriteAllBytesSafely(path, output);
                _sourcePath = path;
                Text = AppName + " v" + AppVersion + " — " + Path.GetFileName(path);
                SetStatus(L.T("Gespeichert und Roundtrip-validiert: ", "Saved and roundtrip-validated: ") + path + " | " + output.Length.ToString() + " Bytes");
                if (showSuccessDialog)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("BRLAN wurde gespeichert und anschliessend erfolgreich erneut geparst.", "The BRLAN was saved and successfully parsed again."), L.T("Speichern erfolgreich", "Save successful"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Speichern fehlgeschlagen", "Save failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus(L.T("Speichern fehlgeschlagen: ", "Save failed: ") + ex.Message);
                return false;
            }
        }

        private void Save()
        {
            if (_doc == null)
                return;
            // Noch nie geöffnet/gespeichert: normales Save verhält sich wie Save As.
            if (String.IsNullOrEmpty(_sourcePath))
            {
                SaveAs();
                return;
            }

            if (!PrepareForSave())
                return;
            // Bewusst ohne zusätzlichen Dateidialog: Ctrl+S überschreibt genau die
            // aktuell geöffnete BRLAN-Datei.
            WriteDocumentToPath(_sourcePath, false);
        }

        private void SaveAs()
        {
            if (_doc == null)
                return;
            if (!PrepareForSave())
                return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = L.T("BRLAN-Dateien (*.brlan)|*.brlan|Alle Dateien (*.*)|*.*", "BRLAN files (*.brlan)|*.brlan|All files (*.*)|*.*");
                dlg.Title = L.T("BRLAN speichern", "Save BRLAN");
                string baseName = String.IsNullOrEmpty(_sourcePath) ? "animation" : Path.GetFileNameWithoutExtension(_sourcePath);
                dlg.FileName = baseName + "_murums.brlan";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                WriteDocumentToPath(dlg.FileName, true);
            }
        }

        private void RefreshAllUi()
        {
            CommitGridToEntry(true);
            RebuildTree();
            RefreshTextureList();
            ValidateDocument();
        }

        private void RebuildTree()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            if (_doc == null)
            {
                _tree.EndUpdate();
                return;
            }

            TreeNode root = new TreeNode("RLAN — " + Path.GetFileName(_sourcePath));
            root.Tag = new NodeRef(NodeKind.Root, _doc, null);
            TreeNode header = new TreeNode("Header — v" + _doc.Version.ToString());
            header.Tag = new NodeRef(NodeKind.Header, _doc, null);
            root.Nodes.Add(header);
            int i, j, k;
            for (i = 0; i < _doc.Sections.Count; i++)
            {
                BrlanSection s = _doc.Sections[i];
                if (s.IsPai)
                {
                    TreeNode pai = new TreeNode("pai1 — " + _doc.Pai.Frames.ToString() + " Frames");
                    pai.Tag = new NodeRef(NodeKind.Pai, _doc.Pai, s);
                    TreeNode textures = new TreeNode(L.T("TPL-Dateien (", "TPL Files (") + _doc.Pai.Textures.Count.ToString() + ")");
                    textures.Tag = new NodeRef(NodeKind.Textures, _doc.Pai.Textures, _doc.Pai);
                    for (j = 0; j < _doc.Pai.Textures.Count; j++)
                    {
                        TreeNode tex = new TreeNode("[" + j.ToString() + "] " + _doc.Pai.Textures[j]);
                        tex.Tag = new NodeRef(NodeKind.Texture, j, _doc.Pai);
                        textures.Nodes.Add(tex);
                    }

                    pai.Nodes.Add(textures);
                    TreeNode animations = new TreeNode(L.T("Animationen (", "Animations (") + _doc.Pai.Animations.Count.ToString() + ")");
                    animations.Tag = new NodeRef(NodeKind.Animations, _doc.Pai.Animations, _doc.Pai);
                    for (j = 0; j < _doc.Pai.Animations.Count; j++)
                    {
                        AnimationModel anim = _doc.Pai.Animations[j];
                        TreeNode an = new TreeNode(anim.Name + " — " + BrlanNames.AnimationTargetName(anim.TargetKind));
                        an.Tag = new NodeRef(NodeKind.Animation, anim, _doc.Pai);
                        for (k = 0; k < anim.Tags.Count; k++)
                        {
                            TagModel tag = anim.Tags[k];
                            string suffix = tag.RawOnly ? " [RAW]" : " (" + tag.Entries.Count.ToString() + ")";
                            TreeNode tn = new TreeNode(tag.Magic + suffix);
                            tn.Tag = new NodeRef(NodeKind.Tag, tag, anim);
                            if (!tag.RawOnly)
                            {
                                int q;
                                for (q = 0; q < tag.Entries.Count; q++)
                                {
                                    EntryModel entry = tag.Entries[q];
                                    TreeNode en = new TreeNode(L.T("Entry ", "Entry ") + q.ToString() + " • I" + entry.Index.ToString() + " • " + BrlanNames.TargetName(tag.Magic, entry.Target) + " • K" + entry.Keys.Count.ToString());
                                    en.Tag = new NodeRef(NodeKind.Entry, entry, tag);
                                    tn.Nodes.Add(en);
                                }
                            }

                            an.Nodes.Add(tn);
                        }

                        animations.Nodes.Add(an);
                    }

                    pai.Nodes.Add(animations);
                    root.Nodes.Add(pai);
                }
                else
                {
                    TreeNode sec = new TreeNode(s.Magic + " — RAW " + s.Raw.Length.ToString() + " Bytes");
                    sec.Tag = new NodeRef(NodeKind.Section, s, null);
                    root.Nodes.Add(sec);
                }
            }

            _tree.Nodes.Add(root);
            root.Expand();
            // pai1 und die beiden wichtigsten Gruppen direkt sichtbar machen.
            foreach (TreeNode child in root.Nodes)
            {
                if (child.Text.StartsWith("pai1", StringComparison.Ordinal))
                {
                    child.Expand();
                    foreach (TreeNode grandChild in child.Nodes)
                    {
                        if (grandChild.Text.StartsWith(L.T("Animationen", "Animations"), StringComparison.Ordinal) || grandChild.Text.StartsWith(L.T("TPL-Dateien", "TPL Files"), StringComparison.Ordinal))
                            grandChild.Expand();
                    }
                }
            }

            _tree.EndUpdate();
            if (_tree.Nodes.Count > 0 && _tree.SelectedNode == null)
                _tree.SelectedNode = root;
        }

        private void RebuildTreeAndSelect(NodeKind kind, object value)
        {
            RebuildTree();
            TreeNode found = FindTreeNode(_tree.Nodes, kind, value);
            if (found == null)
                return;
            TreeNode parent = found.Parent;
            while (parent != null)
            {
                parent.Expand();
                parent = parent.Parent;
            }

            _tree.SelectedNode = found;
            found.EnsureVisible();
        }

        private TreeNode FindTreeNode(TreeNodeCollection nodes, NodeKind kind, object value)
        {
            int i;
            for (i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                NodeRef nr = node.Tag as NodeRef;
                if (nr != null && nr.Kind == kind)
                {
                    if (kind == NodeKind.Texture)
                    {
                        if (nr.Value is int && value is int && (int)nr.Value == (int)value)
                            return node;
                    }
                    else if (object.ReferenceEquals(nr.Value, value))
                    {
                        return node;
                    }
                }

                TreeNode child = FindTreeNode(node.Nodes, kind, value);
                if (child != null)
                    return child;
            }

            return null;
        }

        private void OnTreeNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node == null)
                return;
            _tree.SelectedNode = e.Node;
            if (e.Button != MouseButtons.Right)
                return;
            NodeRef nr = e.Node.Tag as NodeRef;
            if (nr == null)
                return;
            ContextMenuStrip menu = new ContextMenuStrip();
            murumsWiiModStudio.StudioUx.TrackDropDown(menu);
            if (nr.Kind == NodeKind.Root || nr.Kind == NodeKind.Pai || nr.Kind == NodeKind.Animations)
            {
                menu.Items.Add(L.T("Animation hinzufügen", "Add animation"), null, delegate
                {
                    AddAnimation();
                });
            }
            else if (nr.Kind == NodeKind.Textures)
            {
                menu.Items.Add(L.T("TPL hinzufügen", "Add TPL"), null, delegate
                {
                    AddTexture();
                });
                menu.Items.Add(L.T("TPL-Liste einfügen...", "Paste TPL list..."), null, delegate
                {
                    PasteTextureList();
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("GIF importieren...", "Import GIF..."), null, delegate
                {
                    OpenGifImporter();
                });
            }
            else if (nr.Kind == NodeKind.Texture)
            {
                int index = (int)nr.Value;
                if (index >= 0 && index < _textures.Items.Count)
                    _textures.SelectedIndex = index;
                menu.Items.Add(L.T("Umbenennen", "Rename"), null, delegate
                {
                    RenameTexture();
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Nach oben", "Move up"), null, delegate
                {
                    MoveTreeSelected(-1);
                });
                menu.Items.Add(L.T("Nach unten", "Move down"), null, delegate
                {
                    MoveTreeSelected(1);
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Löschen", "Delete"), null, delegate
                {
                    DeleteSelected();
                });
            }
            else if (nr.Kind == NodeKind.Animation)
            {
                menu.Items.Add(L.T("Tag hinzufügen", "Add tag"), null, delegate
                {
                    AddTag();
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Duplizieren", "Duplicate"), null, delegate
                {
                    DuplicateSelected();
                });
                menu.Items.Add(L.T("Nach oben", "Move up"), null, delegate
                {
                    MoveTreeSelected(-1);
                });
                menu.Items.Add(L.T("Nach unten", "Move down"), null, delegate
                {
                    MoveTreeSelected(1);
                });
                menu.Items.Add(L.T("Löschen", "Delete"), null, delegate
                {
                    DeleteSelected();
                });
            }
            else if (nr.Kind == NodeKind.Tag)
            {
                TagModel tag = nr.Value as TagModel;
                if (tag != null && !tag.RawOnly)
                    menu.Items.Add(L.T("Entry hinzufügen", "Add entry"), null, delegate
                    {
                        AddEntry();
                    });
                if (menu.Items.Count > 0)
                    menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Duplizieren", "Duplicate"), null, delegate
                {
                    DuplicateSelected();
                });
                menu.Items.Add(L.T("Nach oben", "Move up"), null, delegate
                {
                    MoveTreeSelected(-1);
                });
                menu.Items.Add(L.T("Nach unten", "Move down"), null, delegate
                {
                    MoveTreeSelected(1);
                });
                menu.Items.Add(L.T("Löschen", "Delete"), null, delegate
                {
                    DeleteSelected();
                });
            }
            else if (nr.Kind == NodeKind.Entry)
            {
                menu.Items.Add(L.T("Duplizieren", "Duplicate"), null, delegate
                {
                    DuplicateSelected();
                });
                menu.Items.Add(L.T("Nach oben", "Move up"), null, delegate
                {
                    MoveTreeSelected(-1);
                });
                menu.Items.Add(L.T("Nach unten", "Move down"), null, delegate
                {
                    MoveTreeSelected(1);
                });
                menu.Items.Add(L.T("Löschen", "Delete"), null, delegate
                {
                    DeleteSelected();
                });
            }

            if (menu.Items.Count == 0)
            {
                menu.Dispose();
                return;
            }

            DarkTheme.StyleToolStrip(menu, new MurumsDarkToolStripRenderer());
            menu.Show(_tree, e.Location);
        }

        private void OnTreeKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Up)
            {
                MoveTreeSelected(-1);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                MoveTreeSelected(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                NodeRef nr = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as NodeRef;
                if (nr != null && nr.Kind == NodeKind.Texture)
                {
                    int index = (int)nr.Value;
                    if (index >= 0 && index < _textures.Items.Count)
                        _textures.SelectedIndex = index;
                    RenameTexture();
                    e.Handled = true;
                }
            }
        }

        private void MoveTreeSelected(int direction)
        {
            if (_doc == null || _tree.SelectedNode == null)
                return;
            CommitGridToEntry(true);
            NodeRef nr = _tree.SelectedNode.Tag as NodeRef;
            if (nr == null)
                return;
            if (nr.Kind == NodeKind.Texture)
            {
                int oldIndex = (int)nr.Value;
                int newIndex = oldIndex + direction;
                if (!MoveTextureToIndex(oldIndex, newIndex))
                    return;
                RefreshTextureList();
                if (newIndex >= 0 && newIndex < _textures.Items.Count)
                    _textures.SelectedIndex = newIndex;
                RebuildTreeAndSelect(NodeKind.Texture, newIndex);
                SetStatus(L.T("TPL-Reihenfolge geändert; RLTP-Indizes wurden automatisch angepasst.", "TPL order changed; RLTP indices were adjusted automatically."));
                return;
            }

            if (nr.Kind == NodeKind.Animation)
            {
                AnimationModel item = nr.Value as AnimationModel;
                int oldIndex = _doc.Pai.Animations.IndexOf(item);
                int newIndex = oldIndex + direction;
                if (!MoveListItem(_doc.Pai.Animations, item, newIndex))
                    return;
                RebuildTreeAndSelect(NodeKind.Animation, item);
            }
            else if (nr.Kind == NodeKind.Tag)
            {
                AnimationModel parent = nr.Parent as AnimationModel;
                TagModel item = nr.Value as TagModel;
                if (parent == null || item == null)
                    return;
                int oldIndex = parent.Tags.IndexOf(item);
                int newIndex = oldIndex + direction;
                if (!MoveListItem(parent.Tags, item, newIndex))
                    return;
                RebuildTreeAndSelect(NodeKind.Tag, item);
            }
            else if (nr.Kind == NodeKind.Entry)
            {
                TagModel parent = nr.Parent as TagModel;
                EntryModel item = nr.Value as EntryModel;
                if (parent == null || item == null)
                    return;
                int oldIndex = parent.Entries.IndexOf(item);
                int newIndex = oldIndex + direction;
                if (!MoveListItem(parent.Entries, item, newIndex))
                    return;
                RebuildTreeAndSelect(NodeKind.Entry, item);
            }
            else
            {
                return;
            }

            SetStatus(L.T("Reihenfolge geändert.", "Order changed."));
        }

        private bool MoveListItem<T>(List<T> list, T item, int newIndex)
        {
            if (list == null)
                return false;
            int oldIndex = list.IndexOf(item);
            if (oldIndex < 0 || newIndex < 0 || newIndex >= list.Count || oldIndex == newIndex)
                return false;
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
            return true;
        }

        private void OnTreeItemDrag(object sender, ItemDragEventArgs e)
        {
            TreeNode node = e.Item as TreeNode;
            NodeRef nr = node == null ? null : node.Tag as NodeRef;
            if (!CanDragNode(nr))
                return;
            _tree.SelectedNode = node;
            _tree.DoDragDrop(node, DragDropEffects.Move);
            ClearTreeDropHighlight();
        }

        private bool CanDragNode(NodeRef nr)
        {
            if (nr == null)
                return false;
            return nr.Kind == NodeKind.Texture || nr.Kind == NodeKind.Animation || nr.Kind == NodeKind.Tag || nr.Kind == NodeKind.Entry;
        }

        private void OnTreeDragEnter(object sender, DragEventArgs e)
        {
            OnTreeDragOver(sender, e);
        }

        private void OnTreeDragOver(object sender, DragEventArgs e)
        {
            if (_doc == null || e.Data == null || !e.Data.GetDataPresent(typeof(TreeNode)))
            {
                e.Effect = DragDropEffects.None;
                ClearTreeDropHighlight();
                return;
            }

            Point point = _tree.PointToClient(new Point(e.X, e.Y));
            AutoScrollTreeDuringDrag(point);
            TreeNode targetNode = _tree.GetNodeAt(point);
            TreeNode sourceNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            int newIndex;
            if (TryResolveTreeReorder(sourceNode, targetNode, point, out newIndex))
            {
                e.Effect = DragDropEffects.Move;
                SetTreeDropHighlight(targetNode);
            }
            else
            {
                e.Effect = DragDropEffects.None;
                ClearTreeDropHighlight();
            }
        }

        private void OnTreeDragLeave(object sender, EventArgs e)
        {
            ClearTreeDropHighlight();
        }

        private void OnTreeDragDrop(object sender, DragEventArgs e)
        {
            if (_doc == null || e.Data == null || !e.Data.GetDataPresent(typeof(TreeNode)))
                return;
            Point point = _tree.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = _tree.GetNodeAt(point);
            TreeNode sourceNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            int newIndex;
            ClearTreeDropHighlight();
            if (!TryResolveTreeReorder(sourceNode, targetNode, point, out newIndex))
                return;
            ApplyTreeReorder(sourceNode, newIndex);
        }

        private bool TryResolveTreeReorder(TreeNode sourceNode, TreeNode targetNode, Point point, out int newIndex)
        {
            newIndex = -1;
            if (_doc == null || sourceNode == null || targetNode == null)
                return false;
            NodeRef source = sourceNode.Tag as NodeRef;
            NodeRef target = targetNode.Tag as NodeRef;
            if (!CanDragNode(source) || target == null)
                return false;
            bool after = point.Y >= targetNode.Bounds.Top + Math.Max(1, targetNode.Bounds.Height) / 2;
            int oldIndex;
            int insertionIndex;
            int count;
            if (source.Kind == NodeKind.Texture)
            {
                oldIndex = (int)source.Value;
                count = _doc.Pai.Textures.Count;
                if (target.Kind == NodeKind.Texture && object.ReferenceEquals(source.Parent, target.Parent))
                    insertionIndex = (int)target.Value + (after ? 1 : 0);
                else if (target.Kind == NodeKind.Textures && object.ReferenceEquals(source.Parent, target.Parent))
                    insertionIndex = count;
                else
                    return false;
            }
            else if (source.Kind == NodeKind.Animation)
            {
                AnimationModel sourceItem = source.Value as AnimationModel;
                oldIndex = _doc.Pai.Animations.IndexOf(sourceItem);
                count = _doc.Pai.Animations.Count;
                if (target.Kind == NodeKind.Animation && object.ReferenceEquals(source.Parent, target.Parent))
                    insertionIndex = _doc.Pai.Animations.IndexOf((AnimationModel)target.Value) + (after ? 1 : 0);
                else if (target.Kind == NodeKind.Animations && object.ReferenceEquals(source.Parent, target.Parent))
                    insertionIndex = count;
                else
                    return false;
            }
            else if (source.Kind == NodeKind.Tag)
            {
                AnimationModel parent = source.Parent as AnimationModel;
                TagModel sourceItem = source.Value as TagModel;
                if (parent == null || sourceItem == null)
                    return false;
                oldIndex = parent.Tags.IndexOf(sourceItem);
                count = parent.Tags.Count;
                if (target.Kind == NodeKind.Tag && object.ReferenceEquals(source.Parent, target.Parent))
                    insertionIndex = parent.Tags.IndexOf((TagModel)target.Value) + (after ? 1 : 0);
                else if (target.Kind == NodeKind.Animation && object.ReferenceEquals(target.Value, source.Parent))
                    insertionIndex = count;
                else
                    return false;
            }
            else if (source.Kind == NodeKind.Entry)
            {
                TagModel parent = source.Parent as TagModel;
                EntryModel sourceItem = source.Value as EntryModel;
                if (parent == null || sourceItem == null)
                    return false;
                oldIndex = parent.Entries.IndexOf(sourceItem);
                count = parent.Entries.Count;
                if (target.Kind == NodeKind.Entry && object.ReferenceEquals(source.Parent, target.Parent))
                    insertionIndex = parent.Entries.IndexOf((EntryModel)target.Value) + (after ? 1 : 0);
                else if (target.Kind == NodeKind.Tag && object.ReferenceEquals(target.Value, source.Parent))
                    insertionIndex = count;
                else
                    return false;
            }
            else
            {
                return false;
            }

            if (oldIndex < 0 || count <= 1)
                return false;
            newIndex = NormalizeDropIndex(oldIndex, insertionIndex, count);
            return newIndex >= 0 && newIndex < count && newIndex != oldIndex;
        }

        private int NormalizeDropIndex(int oldIndex, int insertionIndex, int count)
        {
            if (count <= 0)
                return -1;
            if (insertionIndex < 0)
                insertionIndex = 0;
            if (insertionIndex > count)
                insertionIndex = count;
            if (oldIndex < insertionIndex)
                insertionIndex--;
            if (insertionIndex < 0)
                insertionIndex = 0;
            if (insertionIndex >= count)
                insertionIndex = count - 1;
            return insertionIndex;
        }

        private void ApplyTreeReorder(TreeNode sourceNode, int newIndex)
        {
            NodeRef source = sourceNode == null ? null : sourceNode.Tag as NodeRef;
            if (source == null)
                return;
            CommitGridToEntry(true);
            if (source.Kind == NodeKind.Texture)
            {
                int oldIndex = (int)source.Value;
                if (!MoveTextureToIndex(oldIndex, newIndex))
                    return;
                RefreshTextureList();
                if (newIndex >= 0 && newIndex < _textures.Items.Count)
                    _textures.SelectedIndex = newIndex;
                RebuildTreeAndSelect(NodeKind.Texture, newIndex);
                ValidateDocument();
                SetStatus(L.T("TPL per Drag & Drop verschoben; RLTP-Indizes wurden automatisch angepasst.", "TPL moved by drag & drop; RLTP indices were adjusted automatically."));
                return;
            }

            if (source.Kind == NodeKind.Animation)
            {
                AnimationModel item = source.Value as AnimationModel;
                if (!MoveListItem(_doc.Pai.Animations, item, newIndex))
                    return;
                RebuildTreeAndSelect(NodeKind.Animation, item);
            }
            else if (source.Kind == NodeKind.Tag)
            {
                AnimationModel parent = source.Parent as AnimationModel;
                TagModel item = source.Value as TagModel;
                if (parent == null || !MoveListItem(parent.Tags, item, newIndex))
                    return;
                RebuildTreeAndSelect(NodeKind.Tag, item);
            }
            else if (source.Kind == NodeKind.Entry)
            {
                TagModel parent = source.Parent as TagModel;
                EntryModel item = source.Value as EntryModel;
                if (parent == null || !MoveListItem(parent.Entries, item, newIndex))
                    return;
                RebuildTreeAndSelect(NodeKind.Entry, item);
            }
            else
            {
                return;
            }

            SetStatus(L.T("Per Drag & Drop neu angeordnet.", "Reordered by drag & drop."));
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

        private const string TextureDragFormat = "murumsWiiModStudio.Brlan.TextureIndex";
        private void OnTextureMouseDown(object sender, MouseEventArgs e)
        {
            int index = _textures.IndexFromPoint(e.Location);
            if (index >= 0)
                _textures.SelectedIndex = index;
            if (e.Button == MouseButtons.Left)
            {
                _textureDragIndex = index;
                _textureDragStart = e.Location;
                return;
            }

            if (e.Button != MouseButtons.Right)
                return;
            ContextMenuStrip menu = new ContextMenuStrip();
            murumsWiiModStudio.StudioUx.TrackDropDown(menu);
            if (index >= 0)
            {
                menu.Items.Add(L.T("Umbenennen", "Rename"), null, delegate
                {
                    RenameTexture();
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Nach oben", "Move up"), null, delegate
                {
                    MoveTexture(-1);
                });
                menu.Items.Add(L.T("Nach unten", "Move down"), null, delegate
                {
                    MoveTexture(1);
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("Löschen", "Delete"), null, delegate
                {
                    DeleteTexture();
                });
            }
            else
            {
                menu.Items.Add(L.T("TPL hinzufügen", "Add TPL"), null, delegate
                {
                    AddTexture();
                });
                menu.Items.Add(L.T("TPL-Liste einfügen...", "Paste TPL list..."), null, delegate
                {
                    PasteTextureList();
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(L.T("GIF importieren...", "Import GIF..."), null, delegate
                {
                    OpenGifImporter();
                });
            }

            DarkTheme.StyleToolStrip(menu, new MurumsDarkToolStripRenderer());
            menu.Show(_textures, e.Location);
        }

        private void OnTextureMouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == 0 || _textureDragIndex < 0)
                return;
            Size dragSize = SystemInformation.DragSize;
            Rectangle dragBox = new Rectangle(_textureDragStart.X - dragSize.Width / 2, _textureDragStart.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height);
            if (dragBox.Contains(e.Location))
                return;
            DataObject data = new DataObject();
            data.SetData(TextureDragFormat, _textureDragIndex);
            _textures.DoDragDrop(data, DragDropEffects.Move);
            _textureDragIndex = -1;
        }

        private void OnTextureDragEnter(object sender, DragEventArgs e)
        {
            OnTextureDragOver(sender, e);
        }

        private void OnTextureDragOver(object sender, DragEventArgs e)
        {
            if (_doc != null && e.Data != null && e.Data.GetDataPresent(TextureDragFormat))
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        private void OnTextureDragDrop(object sender, DragEventArgs e)
        {
            if (_doc == null || e.Data == null || !e.Data.GetDataPresent(TextureDragFormat))
                return;
            object raw = e.Data.GetData(TextureDragFormat);
            if (!(raw is int))
                return;
            int oldIndex = (int)raw;
            if (oldIndex < 0 || oldIndex >= _doc.Pai.Textures.Count)
                return;
            Point point = _textures.PointToClient(new Point(e.X, e.Y));
            int targetIndex = _textures.IndexFromPoint(point);
            int insertionIndex;
            if (targetIndex < 0)
            {
                insertionIndex = _doc.Pai.Textures.Count;
            }
            else
            {
                Rectangle bounds = _textures.GetItemRectangle(targetIndex);
                bool after = point.Y >= bounds.Top + Math.Max(1, bounds.Height) / 2;
                insertionIndex = targetIndex + (after ? 1 : 0);
            }

            int newIndex = NormalizeDropIndex(oldIndex, insertionIndex, _doc.Pai.Textures.Count);
            if (newIndex == oldIndex || !MoveTextureToIndex(oldIndex, newIndex))
                return;
            RefreshTextureList();
            _textures.SelectedIndex = newIndex;
            RebuildTreeAndSelect(NodeKind.Texture, newIndex);
            ValidateDocument();
            SetStatus(L.T("TPL per Drag & Drop verschoben; RLTP-Indizes wurden automatisch angepasst.", "TPL moved by drag & drop; RLTP indices were adjusted automatically."));
        }

        private void OnTextureKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteTexture();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                RenameTexture();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Up)
            {
                MoveTexture(-1);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                MoveTexture(1);
                e.Handled = true;
            }
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            CommitGridToEntry(true);
            NodeRef nr = e.Node.Tag as NodeRef;
            _gridEntry = null;
            _gridTag = null;
            _properties.SelectedObject = null;
            _keys.Columns.Clear();
            _keys.Rows.Clear();
            SetKeyControlsEnabled(false);
            if (nr == null)
                return;
            if (nr.Kind == NodeKind.Header || nr.Kind == NodeKind.Root)
            {
                _selectionInfo.Text = "RLAN Header";
                _properties.SelectedObject = new HeaderView(_doc);
                _raw.Text = BrlanCodec.HexDump(_doc.OriginalBytes, 65536);
            }
            else if (nr.Kind == NodeKind.Pai)
            {
                _selectionInfo.Text = L.T("pai1 – globale Animationseinstellungen", "pai1 – global animation settings");
                _properties.SelectedObject = new PaiView((PaiSection)nr.Value);
                _raw.Text = BrlanCodec.HexDump(((BrlanSection)nr.Parent).Raw, 65536);
            }
            else if (nr.Kind == NodeKind.Section)
            {
                BrlanSection section = (BrlanSection)nr.Value;
                _selectionInfo.Text = section.Magic + L.T(" – Raw-Sektion", " – raw section");
                _properties.SelectedObject = new SectionView(section);
                _raw.Text = BrlanCodec.HexDump(section.Raw, 65536);
            }
            else if (nr.Kind == NodeKind.Animation)
            {
                AnimationModel anim = (AnimationModel)nr.Value;
                _selectionInfo.Text = L.T("Animation – ", "Animation – ") + anim.Name;
                _properties.SelectedObject = new AnimationView(anim);
                _raw.Text = L.T("Animation wird strukturiert dargestellt. Wähle einen Tag für dessen Raw-/Hex-Ansicht.", "The animation is shown structurally. Select a tag for its raw/hex view.");
            }
            else if (nr.Kind == NodeKind.Tag)
            {
                TagModel tag = (TagModel)nr.Value;
                _selectionInfo.Text = tag.Magic + " – " + BrlanNames.TagDescription(tag.Magic);
                _properties.SelectedObject = new TagView(tag);
                try
                {
                    _raw.Text = BrlanCodec.HexDump(BrlanCodec.GetRawForTag(tag, _doc.LittleEndian), 65536);
                }
                catch (Exception ex)
                {
                    _raw.Text = L.T("Raw-Vorschau nicht möglich: ", "Raw preview unavailable: ") + ex.Message;
                }
            }
            else if (nr.Kind == NodeKind.Entry)
            {
                EntryModel entry = (EntryModel)nr.Value;
                TagModel tag = (TagModel)nr.Parent;
                _selectionInfo.Text = tag.Magic + " Entry – " + BrlanNames.TargetName(tag.Magic, entry.Target);
                _properties.SelectedObject = new EntryView(entry, tag.Magic);
                _gridEntry = entry;
                _gridTag = tag;
                PopulateKeyGrid();
                SetKeyControlsEnabled(true);
                try
                {
                    _raw.Text = BrlanCodec.HexDump(BrlanCodec.BuildEntry(entry, _doc.LittleEndian), 65536);
                }
                catch (Exception ex)
                {
                    _raw.Text = L.T("Raw-Vorschau nicht möglich: ", "Raw preview unavailable: ") + ex.Message;
                }
            }
            else if (nr.Kind == NodeKind.Textures || nr.Kind == NodeKind.Texture)
            {
                _selectionInfo.Text = L.T("TPL-Dateien – verwalte sie im Tab «TPL-Dateien».", "TPL Files – manage them in the TPL Files tab.");
                _raw.Text = "";
            }
            else if (nr.Kind == NodeKind.Animations)
            {
                _selectionInfo.Text = L.T("Animationen – ", "Animations – ") + _doc.Pai.Animations.Count.ToString() + L.T(" Einträge", " entries");
                _raw.Text = "";
            }
        }

        private void PropertyChanged(object sender, PropertyValueChangedEventArgs e)
        {
            CommitGridToEntry(true);
            _properties.Refresh();
            RebuildTree();
            RefreshTextureList();
            SetStatus(L.T("Eigenschaft geändert: ", "Property changed: ") + e.ChangedItem.Label);
        }

        private void PopulateKeyGrid()
        {
            _loadingGrid = true;
            _keys.Columns.Clear();
            _keys.Rows.Clear();
            if (_gridEntry == null)
            {
                _loadingGrid = false;
                return;
            }

            _keys.Columns.Add("Frame", "Frame");
            if (_gridEntry.KeyType == 1)
            {
                _keys.Columns.Add("UIntValue", _gridTag != null && _gridTag.Magic == "RLTP" ? "TPL-Index / UInt16" : L.T("Wert / UInt16", "Value / UInt16"));
                _keys.Columns.Add("Padding", "Padding / UInt16");
            }
            else
            {
                _keys.Columns.Add("FloatValue", L.T("Wert / Float", "Value / Float"));
                _keys.Columns.Add("Blend", "Blend / Float");
            }

            int i;
            for (i = 0; i < _gridEntry.Keys.Count; i++)
            {
                KeyframeModel key = _gridEntry.Keys[i];
                if (_gridEntry.KeyType == 1)
                {
                    _keys.Rows.Add(key.Frame.ToString("0.######", CultureInfo.InvariantCulture), key.UIntValue.ToString(CultureInfo.InvariantCulture), key.Padding.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    _keys.Rows.Add(key.Frame.ToString("0.######", CultureInfo.InvariantCulture), key.FloatValue.ToString("0.######", CultureInfo.InvariantCulture), key.Blend.ToString("0.######", CultureInfo.InvariantCulture));
                }
            }

            _loadingGrid = false;
        }

        private bool CommitGridToEntry(bool silent)
        {
            if (_loadingGrid || _gridEntry == null)
                return true;
            try
            {
                List<KeyframeModel> parsed = new List<KeyframeModel>();
                int i;
                for (i = 0; i < _keys.Rows.Count; i++)
                {
                    DataGridViewRow row = _keys.Rows[i];
                    if (row.IsNewRow)
                        continue;
                    KeyframeModel key = new KeyframeModel();
                    key.Frame = ParseFloatCell(row.Cells[0].Value, "Frame");
                    if (_gridEntry.KeyType == 1)
                    {
                        key.UIntValue = ParseUShortCell(row.Cells[1].Value, "UInt16-Wert");
                        key.Padding = ParseUShortCell(row.Cells[2].Value, "Padding");
                    }
                    else
                    {
                        key.FloatValue = ParseFloatCell(row.Cells[1].Value, "Float-Wert");
                        key.Blend = ParseFloatCell(row.Cells[2].Value, "Blend");
                    }

                    parsed.Add(key);
                }

                _gridEntry.Keys.Clear();
                _gridEntry.Keys.AddRange(parsed);
                return true;
            }
            catch (Exception ex)
            {
                if (!silent)
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Ungültiger Keyframe", "Invalid keyframe"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private float ParseFloatCell(object value, string label)
        {
            float f;
            string s = value == null ? "" : value.ToString().Trim().Replace(',', '.');
            if (!Single.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                throw new InvalidDataException(label + " ist keine gültige Zahl: " + s);
            return f;
        }

        private ushort ParseUShortCell(object value, string label)
        {
            ushort v;
            string s = value == null ? "" : value.ToString().Trim();
            if (!UInt16.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                throw new InvalidDataException(label + " ist keine gültige UInt16-Zahl: " + s);
            return v;
        }

        private void KeyCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_loadingGrid || _gridEntry == null)
                return;
            if (CommitGridToEntry(false))
                SetStatus("Keyframes aktualisiert.");
        }

        private void SetKeyControlsEnabled(bool enabled)
        {
            _addKey.Enabled = enabled;
            _deleteKey.Enabled = enabled;
            _sortKeys.Enabled = enabled;
        }

        private void AddKeyframe()
        {
            if (_gridEntry == null)
                return;
            if (!CommitGridToEntry(false))
                return;
            KeyframeModel key = new KeyframeModel();
            if (_gridEntry.Keys.Count > 0)
                key.Frame = _gridEntry.Keys[_gridEntry.Keys.Count - 1].Frame + 1.0f;
            _gridEntry.Keys.Add(key);
            PopulateKeyGrid();
            SetStatus(L.T("Keyframe hinzugefügt.", "Keyframe added."));
        }

        private void DeleteKeyframe()
        {
            if (_gridEntry == null)
                return;
            if (!CommitGridToEntry(false))
                return;
            List<int> indices = new List<int>();
            foreach (DataGridViewRow row in _keys.SelectedRows)
                indices.Add(row.Index);
            indices.Sort();
            indices.Reverse();
            int i;
            for (i = 0; i < indices.Count; i++)
                if (indices[i] >= 0 && indices[i] < _gridEntry.Keys.Count)
                    _gridEntry.Keys.RemoveAt(indices[i]);
            PopulateKeyGrid();
            SetStatus(indices.Count.ToString() + L.T(" Keyframe(s) gelöscht.", " keyframe(s) deleted."));
        }

        private void SortCurrentKeyframes()
        {
            if (_gridEntry == null)
                return;
            if (!CommitGridToEntry(false))
                return;
            _gridEntry.Keys.Sort(delegate (KeyframeModel a, KeyframeModel b)
            {
                return a.Frame.CompareTo(b.Frame);
            });
            PopulateKeyGrid();
            SetStatus("Keyframes nach Frame sortiert.");
        }

        private void SortAllKeyframes()
        {
            if (_doc == null)
                return;
            CommitGridToEntry(true);
            int count = 0;
            int i, j, k;
            for (i = 0; i < _doc.Pai.Animations.Count; i++)
                for (j = 0; j < _doc.Pai.Animations[i].Tags.Count; j++)
                    if (!_doc.Pai.Animations[i].Tags[j].RawOnly)
                        for (k = 0; k < _doc.Pai.Animations[i].Tags[j].Entries.Count; k++)
                        {
                            _doc.Pai.Animations[i].Tags[j].Entries[k].Keys.Sort(delegate (KeyframeModel a, KeyframeModel b)
                            {
                                return a.Frame.CompareTo(b.Frame);
                            });
                            count++;
                        }

            PopulateKeyGrid();
            SetStatus("Keyframes in " + count.ToString() + " Entries sortiert.");
        }

        private AnimationModel SelectedAnimation()
        {
            if (_tree.SelectedNode == null)
                return null;
            NodeRef nr = _tree.SelectedNode.Tag as NodeRef;
            if (nr == null)
                return null;
            if (nr.Kind == NodeKind.Animation)
                return nr.Value as AnimationModel;
            if (nr.Kind == NodeKind.Tag)
                return nr.Parent as AnimationModel;
            if (nr.Kind == NodeKind.Entry)
            {
                TagModel tag = nr.Parent as TagModel;
                return FindAnimationForTag(tag);
            }

            return null;
        }

        private TagModel SelectedTag()
        {
            if (_tree.SelectedNode == null)
                return null;
            NodeRef nr = _tree.SelectedNode.Tag as NodeRef;
            if (nr == null)
                return null;
            if (nr.Kind == NodeKind.Tag)
                return nr.Value as TagModel;
            if (nr.Kind == NodeKind.Entry)
                return nr.Parent as TagModel;
            return null;
        }

        private AnimationModel FindAnimationForTag(TagModel tag)
        {
            if (_doc == null || tag == null)
                return null;
            int i, j;
            for (i = 0; i < _doc.Pai.Animations.Count; i++)
                for (j = 0; j < _doc.Pai.Animations[i].Tags.Count; j++)
                    if (Object.ReferenceEquals(_doc.Pai.Animations[i].Tags[j], tag))
                        return _doc.Pai.Animations[i];
            return null;
        }

        private void AddAnimation()
        {
            if (_doc == null)
                return;
            string name = SimpleDialogs.Prompt(this, L.T("Animation hinzufügen", "Add animation"), L.T("Name (max. 20 ASCII-Zeichen):", "Name (max. 20 ASCII characters):"), "", "new_animation");
            if (name == null)
                return;
            DialogResult target = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Soll die Animation ein MATERIAL animieren?\r\n\r\nJa = Material\r\nNein = Pane", "Should this animation target a MATERIAL?\r\n\r\nYes = Material\r\nNo = Pane"), L.T("Target-Klasse", "Target class"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (target == DialogResult.Cancel)
                return;
            AnimationModel anim = new AnimationModel();
            anim.Name = name;
            anim.TargetKind = target == DialogResult.Yes ? (byte)1 : (byte)0;
            _doc.Pai.Animations.Add(anim);
            RebuildTree();
            SetStatus(L.T("Animation hinzugefügt: ", "Animation added: ") + name);
        }

        private void AddTag()
        {
            if (_doc == null)
                return;
            AnimationModel anim = SelectedAnimation();
            if (anim == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle zuerst eine Animation im Strukturbaum.", "Select an animation in the structure tree first."), L.T("Tag hinzufügen", "Add tag"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string magic = SimpleDialogs.ChooseTag(this);
            if (magic == null)
                return;
            TagModel tag = new TagModel();
            tag.Magic = magic;
            tag.RawOnly = false;
            anim.Tags.Add(tag);
            RebuildTree();
            SetStatus(L.T("Tag ", "Tag ") + magic + L.T(" hinzugefügt.", " added."));
        }

        private void AddEntry()
        {
            if (_doc == null)
                return;
            TagModel tag = SelectedTag();
            if (tag == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle zuerst einen Tag oder Entry im Strukturbaum.", "Select a tag or entry in the structure tree first."), L.T("Entry hinzufügen", "Add entry"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (tag.RawOnly)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Raw-only-Tags können nicht strukturell erweitert werden.", "Raw-only tags cannot be extended structurally."), L.T("Entry hinzufügen", "Add entry"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EntryModel entry = new EntryModel();
            entry.Index = 0;
            entry.Target = 0;
            entry.KeyType = BrlanNames.DefaultKeyType(tag.Magic);
            tag.Entries.Add(entry);
            RebuildTree();
            SetStatus(L.T("Entry zu ", "Entry added to ") + tag.Magic + L.T(" hinzugefügt.", "."));
        }

        private void DuplicateSelected()
        {
            if (_doc == null || _tree.SelectedNode == null)
                return;
            CommitGridToEntry(true);
            NodeRef nr = _tree.SelectedNode.Tag as NodeRef;
            if (nr == null)
                return;
            if (nr.Kind == NodeKind.Animation)
            {
                AnimationModel src = (AnimationModel)nr.Value;
                AnimationModel copy = DeepCloneAnimation(src);
                copy.Name = TrimTo20Ascii(src.Name + "_copy");
                _doc.Pai.Animations.Add(copy);
                SetStatus(L.T("Animation dupliziert.", "Animation duplicated."));
            }
            else if (nr.Kind == NodeKind.Tag)
            {
                TagModel src = (TagModel)nr.Value;
                AnimationModel parent = (AnimationModel)nr.Parent;
                parent.Tags.Add(DeepCloneTag(src));
                SetStatus(L.T("Tag dupliziert.", "Tag duplicated."));
            }
            else if (nr.Kind == NodeKind.Entry)
            {
                EntryModel src = (EntryModel)nr.Value;
                TagModel parent = (TagModel)nr.Parent;
                parent.Entries.Add(DeepCloneEntry(src));
                SetStatus(L.T("Entry dupliziert.", "Entry duplicated."));
            }
            else
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Duplizieren ist für Animation, Tag und Entry verfügbar.", "Duplication is available for animations, tags and entries."), L.T("Duplizieren", "Duplicate"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RebuildTree();
        }

        private void DeleteSelected()
        {
            if (_doc == null || _tree.SelectedNode == null)
                return;
            CommitGridToEntry(true);
            NodeRef nr = _tree.SelectedNode.Tag as NodeRef;
            if (nr == null)
                return;
            if (nr.Kind == NodeKind.Texture)
            {
                int textureIndex = (int)nr.Value;
                if (textureIndex >= 0 && textureIndex < _textures.Items.Count)
                    _textures.SelectedIndex = textureIndex;
                DeleteTextureAt(textureIndex);
                return;
            }

            if (murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ausgewähltes Element wirklich löschen?", "Really delete the selected item?"), L.T("Löschen", "Delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            if (nr.Kind == NodeKind.Animation)
                _doc.Pai.Animations.Remove((AnimationModel)nr.Value);
            else if (nr.Kind == NodeKind.Tag)
                ((AnimationModel)nr.Parent).Tags.Remove((TagModel)nr.Value);
            else if (nr.Kind == NodeKind.Entry)
                ((TagModel)nr.Parent).Entries.Remove((EntryModel)nr.Value);
            else
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Löschen ist für TPL, Animation, Tag und Entry verfügbar.", "Delete is available for TPLs, animations, tags and entries."), L.T("Löschen", "Delete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _properties.SelectedObject = null;
            _gridEntry = null;
            _gridTag = null;
            RebuildTree();
            SetStatus(L.T("Element gelöscht.", "Item deleted."));
        }

        private AnimationModel DeepCloneAnimation(AnimationModel src)
        {
            AnimationModel a = new AnimationModel();
            a.Name = src.Name;
            a.TargetKind = src.TargetKind;
            a.Unknown16 = src.Unknown16;
            int i;
            for (i = 0; i < src.Tags.Count; i++)
                a.Tags.Add(DeepCloneTag(src.Tags[i]));
            return a;
        }

        private TagModel DeepCloneTag(TagModel src)
        {
            TagModel t = new TagModel();
            t.Magic = src.Magic;
            t.HeaderUnknown1 = src.HeaderUnknown1;
            t.HeaderUnknown2 = src.HeaderUnknown2;
            t.HeaderUnknown3 = src.HeaderUnknown3;
            t.RawOnly = src.RawOnly;
            t.Raw = BrlanCodec.Clone(src.Raw);
            int i;
            for (i = 0; i < src.Entries.Count; i++)
                t.Entries.Add(DeepCloneEntry(src.Entries[i]));
            return t;
        }

        private EntryModel DeepCloneEntry(EntryModel src)
        {
            EntryModel e = new EntryModel();
            e.Index = src.Index;
            e.Target = src.Target;
            e.KeyType = src.KeyType;
            e.UnknownByte = src.UnknownByte;
            e.Unknown16 = src.Unknown16;
            int i;
            for (i = 0; i < src.Keys.Count; i++)
                e.Keys.Add(src.Keys[i].Clone());
            return e;
        }

        private string TrimTo20Ascii(string s)
        {
            if (s == null)
                return "";
            byte[] b = Encoding.ASCII.GetBytes(s);
            if (b.Length <= 20)
                return s;
            return Encoding.ASCII.GetString(b, 0, 20);
        }

        private void RefreshTextureList()
        {
            _textures.Items.Clear();
            if (_doc == null)
                return;
            int i;
            for (i = 0; i < _doc.Pai.Textures.Count; i++)
                _textures.Items.Add("[" + i.ToString() + "] " + _doc.Pai.Textures[i]);
        }

        private void AddTexture()
        {
            if (_doc == null)
                return;
            string name = SimpleDialogs.Prompt(this, L.T("TPL hinzufügen", "Add TPL"), L.T("Dateiname:", "Filename:"), "", L.T("z.B. bg_01.tpl", "e.g. bg_01.tpl"));
            if (name == null)
                return;
            name = name.Trim();
            if (name.Length == 0)
                return;
            _doc.Pai.Textures.Add(name);
            RefreshTextureList();
            RebuildTree();
            SetStatus(L.Format("TPL hinzugefügt: {0}", "TPL added: {0}", name));
        }

        private void RenameTexture()
        {
            if (_doc == null || _textures.SelectedIndex < 0)
                return;
            int index = _textures.SelectedIndex;
            string old = _doc.Pai.Textures[index];
            string name = SimpleDialogs.Prompt(this, L.T("TPL umbenennen", "Rename TPL"), L.T("Neuer Dateiname:", "New filename:"), old, L.T("z.B. bg_01.tpl", "e.g. bg_01.tpl"));
            if (name == null)
                return;
            _doc.Pai.Textures[index] = name.Trim();
            RefreshTextureList();
            _textures.SelectedIndex = index;
            RebuildTree();
            SetStatus(L.T("TPL umbenannt.", "TPL renamed."));
        }

        private void DeleteTexture()
        {
            if (_doc == null || _textures.SelectedIndex < 0)
                return;
            DeleteTextureAt(_textures.SelectedIndex);
        }

        private bool DeleteTextureAt(int index)
        {
            if (_doc == null || index < 0 || index >= _doc.Pai.Textures.Count)
                return false;
            if (murumsWiiModStudio.StudioMessageBox.Show(this, L.Format("TPL-Index {0} löschen?\r\n\r\nRLTP-Keyframes werden automatisch angepasst: Referenzen auf diesen Index werden entfernt, höhere Indizes werden um 1 reduziert.", "Delete TPL index {0}?\r\n\r\nRLTP keyframes will be adjusted automatically: references to this index are removed and higher indices are shifted down by 1.", index), L.T("TPL löschen", "Delete TPL"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;
            string deletedName = _doc.Pai.Textures[index] ?? "";
            int replacementBeforeDelete = -1;
            int q;
            for (q = 0; q < _doc.Pai.Textures.Count; q++)
            {
                if (q != index && String.Equals(_doc.Pai.Textures[q] ?? "", deletedName, StringComparison.OrdinalIgnoreCase))
                {
                    replacementBeforeDelete = q;
                    break;
                }
            }

            _doc.Pai.Textures.RemoveAt(index);
            int replacementAfterDelete = replacementBeforeDelete;
            if (replacementAfterDelete > index)
                replacementAfterDelete--;
            RemapRltpAfterTextureDelete(index, replacementAfterDelete);
            RefreshTextureList();
            RebuildTree();
            ValidateDocument();
            SetStatus(L.T("TPL gelöscht; RLTP-Indizes wurden automatisch angepasst.", "TPL deleted; RLTP indices were adjusted automatically."));
            return true;
        }

        private void MoveTexture(int direction)
        {
            if (_doc == null || _textures.SelectedIndex < 0)
                return;
            int index = _textures.SelectedIndex;
            int target = index + direction;
            if (!MoveTextureToIndex(index, target))
                return;
            RefreshTextureList();
            _textures.SelectedIndex = target;
            RebuildTreeAndSelect(NodeKind.Texture, target);
            SetStatus(L.T("TPL-Reihenfolge geändert; RLTP-Indizes wurden automatisch angepasst.", "TPL order changed; RLTP indices were adjusted automatically."));
        }

        private bool MoveTextureToIndex(int oldIndex, int newIndex)
        {
            if (_doc == null)
                return false;
            int count = _doc.Pai.Textures.Count;
            if (oldIndex < 0 || oldIndex >= count || newIndex < 0 || newIndex >= count || oldIndex == newIndex)
                return false;
            string item = _doc.Pai.Textures[oldIndex];
            _doc.Pai.Textures.RemoveAt(oldIndex);
            _doc.Pai.Textures.Insert(newIndex, item);
            RemapRltpAfterTextureMove(oldIndex, newIndex);
            return true;
        }

        private void PasteTextureList()
        {
            if (_doc == null)
                return;
            StringBuilder current = new StringBuilder();
            int i;
            for (i = 0; i < _doc.Pai.Textures.Count; i++)
            {
                if (i > 0)
                    current.AppendLine();
                current.Append(_doc.Pai.Textures[i]);
            }

            string text = SimpleDialogs.PromptMultiline(this, L.T("TPL-Liste einfügen", "Paste TPL list"), L.T("Eine TPL pro Zeile. Bestehende Liste wird ersetzt.", "One TPL per line. The existing list will be replaced."), current.ToString(), L.T("bg.tpl\r\nbg_01.tpl\r\nbg_02.tpl", "bg.tpl\r\nbg_01.tpl\r\nbg_02.tpl"));
            if (text == null)
                return;
            string[] parts = text.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            _doc.Pai.Textures.Clear();
            for (i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (item.Length > 0)
                    _doc.Pai.Textures.Add(item);
            }

            RefreshTextureList();
            RebuildTree();
            ValidateDocument();
            SetStatus(L.Format("TPL-Liste ersetzt: {0} Einträge. RLTP bitte im Validator prüfen.", "TPL list replaced: {0} entries. Please check RLTP in the validator.", _doc.Pai.Textures.Count));
        }

        private void RemapRltpAfterTextureDelete(int deletedIndex, int replacementIndex)
        {
            if (_doc == null || _doc.Pai == null)
                return;
            int a, t, e, k;
            for (a = 0; a < _doc.Pai.Animations.Count; a++)
            {
                AnimationModel anim = _doc.Pai.Animations[a];
                for (t = 0; t < anim.Tags.Count; t++)
                {
                    TagModel tag = anim.Tags[t];
                    if (tag.RawOnly || tag.Magic != "RLTP")
                        continue;
                    for (e = 0; e < tag.Entries.Count; e++)
                    {
                        EntryModel entry = tag.Entries[e];
                        if (entry.KeyType != 1)
                            continue;
                        for (k = entry.Keys.Count - 1; k >= 0; k--)
                        {
                            ushort value = entry.Keys[k].UIntValue;
                            if (value == deletedIndex)
                            {
                                // If an identical filename still exists elsewhere in the
                                // table, preserve the keyframe and redirect it to that copy.
                                // This prevents deleting a duplicate TPL row from wiping an
                                // entire RLTP sequence, as happened with older builds.
                                if (replacementIndex >= 0)
                                    entry.Keys[k].UIntValue = (ushort)replacementIndex;
                                else
                                    entry.Keys.RemoveAt(k);
                            }
                            else if (value > deletedIndex)
                            {
                                entry.Keys[k].UIntValue = (ushort)(value - 1);
                            }
                        }
                    }
                }
            }
        }

        private void RemapRltpAfterTextureSwap(int first, int second)
        {
            if (_doc == null || _doc.Pai == null)
                return;
            int a, t, e, k;
            for (a = 0; a < _doc.Pai.Animations.Count; a++)
            {
                AnimationModel anim = _doc.Pai.Animations[a];
                for (t = 0; t < anim.Tags.Count; t++)
                {
                    TagModel tag = anim.Tags[t];
                    if (tag.RawOnly || tag.Magic != "RLTP")
                        continue;
                    for (e = 0; e < tag.Entries.Count; e++)
                    {
                        EntryModel entry = tag.Entries[e];
                        if (entry.KeyType != 1)
                            continue;
                        for (k = 0; k < entry.Keys.Count; k++)
                        {
                            if (entry.Keys[k].UIntValue == first)
                                entry.Keys[k].UIntValue = (ushort)second;
                            else if (entry.Keys[k].UIntValue == second)
                                entry.Keys[k].UIntValue = (ushort)first;
                        }
                    }
                }
            }
        }

        private void RemapRltpAfterTextureMove(int oldIndex, int newIndex)
        {
            if (_doc == null || _doc.Pai == null || oldIndex == newIndex)
                return;
            int a, t, e, k;
            for (a = 0; a < _doc.Pai.Animations.Count; a++)
            {
                AnimationModel anim = _doc.Pai.Animations[a];
                for (t = 0; t < anim.Tags.Count; t++)
                {
                    TagModel tag = anim.Tags[t];
                    if (tag.RawOnly || tag.Magic != "RLTP")
                        continue;
                    for (e = 0; e < tag.Entries.Count; e++)
                    {
                        EntryModel entry = tag.Entries[e];
                        if (entry.KeyType != 1)
                            continue;
                        for (k = 0; k < entry.Keys.Count; k++)
                        {
                            int value = entry.Keys[k].UIntValue;
                            if (value == oldIndex)
                                entry.Keys[k].UIntValue = (ushort)newIndex;
                            else if (oldIndex < newIndex && value > oldIndex && value <= newIndex)
                                entry.Keys[k].UIntValue = (ushort)(value - 1);
                            else if (oldIndex > newIndex && value >= newIndex && value < oldIndex)
                                entry.Keys[k].UIntValue = (ushort)(value + 1);
                        }
                    }
                }
            }
        }

        private void OpenBrlytMapper()
        {
            string suggested = null;
            if (_doc != null && !String.IsNullOrWhiteSpace(_doc.SourcePath))
                suggested = BrlytInspector.FindNearbyLayout(_doc.SourcePath);
            using (BrlytMapForm form = new BrlytMapForm(suggested))
                form.ShowDialog(this);
        }

        private void OpenGifImporter()
        {
            using (GifImportForm importer = new GifImportForm(_doc, delegate
            {
                RebuildTree();
                RefreshTextureList();
                ValidateDocument();
                SetStatus(L.T("GIF-Import übernommen. TPL-Liste und RLTP wurden aktualisiert.", "GIF import applied. TPL list and RLTP were updated."));
            }, ExternalGifOutputFolder, ExternalGifCompleted))
            {
                importer.ShowDialog(this);
            }
        }

        private void RunRltpWizard()
        {
            if (_doc == null)
                return;
            if (_doc.Pai.Textures.Count == 0)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Lege zuerst im Tab «TPL-Dateien» mindestens einen TPL-Dateinamen an.", "Add at least one TPL filename in the TPL Files tab first."), "RLTP Generator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_doc.Pai.Animations.Count == 0)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Es existiert keine Animation. Lege zuerst eine Animation an.", "No animation exists. Add an animation first."), "RLTP Generator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (RltpWizardForm wizard = new RltpWizardForm(_doc.Pai))
            {
                if (wizard.ShowDialog(this) != DialogResult.OK)
                    return;
                AnimationModel anim = _doc.Pai.Animations[wizard.AnimationIndex];
                TagModel rltp = null;
                int i;
                for (i = 0; i < anim.Tags.Count; i++)
                    if (anim.Tags[i].Magic == "RLTP" && !anim.Tags[i].RawOnly)
                    {
                        rltp = anim.Tags[i];
                        break;
                    }

                if (rltp == null)
                {
                    rltp = new TagModel();
                    rltp.Magic = "RLTP";
                    rltp.RawOnly = false;
                    anim.Tags.Add(rltp);
                }

                EntryModel entry = null;
                for (i = 0; i < rltp.Entries.Count; i++)
                    if (rltp.Entries[i].Index == wizard.TextureSlot)
                    {
                        entry = rltp.Entries[i];
                        break;
                    }

                if (entry == null)
                {
                    entry = new EntryModel();
                    entry.Index = wizard.TextureSlot;
                    entry.Target = 0;
                    entry.KeyType = 1;
                    rltp.Entries.Add(entry);
                }

                entry.Target = 0;
                entry.KeyType = 1;
                entry.Keys.Clear();
                int frame = wizard.StartFrame;
                for (i = 0; i < _doc.Pai.Textures.Count; i++)
                {
                    KeyframeModel key = new KeyframeModel();
                    key.Frame = frame;
                    key.UIntValue = (ushort)i;
                    key.Padding = 0;
                    entry.Keys.Add(key);
                    frame += wizard.FramesPerImage;
                }

                if (wizard.AdjustFrames)
                {
                    int total = wizard.StartFrame + _doc.Pai.Textures.Count * wizard.FramesPerImage;
                    if (total > 65535)
                        total = 65535;
                    _doc.Pai.Frames = (ushort)Math.Max(1, total);
                }

                RebuildTree();
                ValidateDocument();
                SetStatus(L.T("RLTP erzeugt: ", "RLTP created: ") + _doc.Pai.Textures.Count.ToString() + L.T(" Bilder × ", " images × ") + wizard.FramesPerImage.ToString() + " Frames.");
            }
        }

        private void ValidateDocument()
        {
            _issues.Items.Clear();
            if (_doc == null)
                return;
            CommitGridToEntry(true);
            List<ValidationIssue> issues = BrlanCodec.Validate(_doc);
            int i;
            for (i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                string severityText = issue.Severity;
                if (!L.IsGerman)
                {
                    if (severityText == "Fehler")
                        severityText = "Error";
                    else if (severityText == "Warnung")
                        severityText = "Warning";
                    else if (severityText == "Info")
                        severityText = "Info";
                }

                ListViewItem item = new ListViewItem(severityText);
                item.SubItems.Add(issue.Location);
                item.SubItems.Add(issue.Message);
                if (issue.Severity == "Fehler")
                    item.ForeColor = DarkTheme.Error;
                else if (issue.Severity == "Warnung")
                    item.ForeColor = DarkTheme.Warning;
                else if (issue.Severity == "OK")
                    item.ForeColor = DarkTheme.Success;
                else
                    item.ForeColor = DarkTheme.Muted;
                _issues.Items.Add(item);
            }

            int errors = 0, warnings = 0;
            for (i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == "Fehler")
                    errors++;
                if (issues[i].Severity == "Warnung")
                    warnings++;
            }

            SetStatus("Validator: " + errors.ToString() + L.T(" Fehler, ", " errors, ") + warnings.ToString() + L.T(" Warnungen.", " warnings."));
        }

        private void RunRoundtripSelfTest()
        {
            if (_doc == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Keine BRLAN geladen.", "No BRLAN loaded."), L.T("Roundtrip-Selbsttest", "Roundtrip self-test"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CommitGridToEntry(true);
            try
            {
                byte[] rebuilt = BrlanCodec.Build(_doc);
                BrlanDocument parsed = BrlanCodec.Parse(rebuilt);
                List<ValidationIssue> issues = BrlanCodec.Validate(parsed);
                int errors = 0;
                int warnings = 0;
                int i;
                for (i = 0; i < issues.Count; i++)
                {
                    if (issues[i].Severity == "Fehler")
                        errors++;
                    else if (issues[i].Severity == "Warnung")
                        warnings++;
                }

                bool shapeMatches = parsed.Version == _doc.Version && parsed.Pai.Frames == _doc.Pai.Frames && parsed.Pai.Textures.Count == _doc.Pai.Textures.Count && parsed.Pai.Animations.Count == _doc.Pai.Animations.Count;
                if (!shapeMatches || errors > 0)
                    throw new InvalidDataException(L.T("Die neu serialisierte Datei konnte zwar gelesen werden, aber die Kernstruktur stimmt nicht vollständig überein.", "The newly serialized file could be parsed, but the core structure does not fully match."));
                SetStatus(L.T("Roundtrip-Selbsttest OK: ", "Roundtrip self-test OK: ") + rebuilt.Length.ToString() + " Bytes | " + warnings.ToString() + L.T(" Warnung(en).", " warning(s)."));
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Roundtrip-Selbsttest erfolgreich.\r\n\r\nDie aktuelle Struktur wurde im Speicher als BRLAN serialisiert und anschliessend erneut geparst.\r\nValidator-Fehler: 0\r\nValidator-Warnungen: ", "Roundtrip self-test successful.\r\n\r\nThe current structure was serialized to BRLAN in memory and parsed again.\r\nValidator errors: 0\r\nValidator warnings: ") + warnings.ToString(), L.T("Selbsttest erfolgreich", "Self-test successful"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus(L.T("Roundtrip-Selbsttest fehlgeschlagen: ", "Roundtrip self-test failed: ") + ex.Message);
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("Selbsttest fehlgeschlagen", "Self-test failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowHelpTab()
        {
            if (_tabs != null && _helpTab != null)
            {
                murumsWiiModStudio.StudioHelpWindow.Show(this, _helpTab, L.T("BRLAN-Hilfe", "BRLAN help"));
                if (_helpNav != null && _helpNav.SelectedIndex < 0)
                    _helpNav.SelectedIndex = 0;
            }
        }

        private void ShowAbout()
        {
            murumsWiiModStudio.StudioMessageBox.Show(this, AppName + " v" + AppVersion + "\r\n\r\n" + L.T("BRLAN-Editor für Mario Kart Wii und andere Wii-Layouts.\r\n" + "Animationen, TPL-Verwaltung, GIF-Import, Validator, Kontextmenüs und Drag & Drop.\r\n\r\n" + "Entwickelt von murums mit KI-Unterstützung.", "BRLAN editor for Mario Kart Wii and other Wii layouts.\r\n" + "Animations, TPL management, GIF import, validator, context menus and drag & drop.\r\n\r\n" + "Developed by murums with AI assistance."), L.T("Über ", "About ") + AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildHelpTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            TableLayoutPanel navigation = new TableLayoutPanel();
            navigation.Dock = DockStyle.Fill;
            navigation.Margin = new Padding(0, 0, 8, 0);
            navigation.Padding = new Padding(0);
            navigation.ColumnCount = 1;
            navigation.RowCount = 2;
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label navTitle = new Label();
            navTitle.Text = L.T("HILFE & REFERENZ", "HELP & REFERENCE");
            navTitle.Dock = DockStyle.Fill;
            navTitle.TextAlign = ContentAlignment.MiddleLeft;
            navTitle.Padding = new Padding(12, 0, 0, 0);
            navTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            navTitle.BackColor = DarkTheme.Panel2;
            navTitle.ForeColor = DarkTheme.Muted;
            navigation.Controls.Add(navTitle, 0, 0);
            _helpNav = new ListBox();
            _helpNav.Dock = DockStyle.Fill;
            _helpNav.Margin = new Padding(0);
            _helpNav.BorderStyle = BorderStyle.None;
            _helpNav.IntegralHeight = false;
            _helpNav.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _helpNav.ItemHeight = 28;
            _helpNav.Items.AddRange(new object[] { L.T("Schnellstart", "Quick Start"), L.T("Oberfläche", "Interface"), L.T("BRLAN-Struktur", "BRLAN Structure"), L.T("Animationstags", "Animation Tags"), L.T("Keyframes", "Keyframes"), L.T("RLTP-Bildanimation", "RLTP Image Animation"), L.T("GIF-Import", "GIF Import"), L.T("Validator", "Validator"), L.T("Sicherheit & Workflow", "Safety & Workflow"), L.T("Kurzreferenz", "Quick Reference") });
            _helpNav.SelectedIndexChanged += delegate
            {
                UpdateHelpTopic();
            };
            navigation.Controls.Add(_helpNav, 0, 1);
            layout.Controls.Add(navigation, 0, 0);
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);
            content.Padding = new Padding(0);
            content.ColumnCount = 1;
            content.RowCount = 2;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _helpHeading = new Label();
            _helpHeading.Dock = DockStyle.Fill;
            _helpHeading.TextAlign = ContentAlignment.MiddleLeft;
            _helpHeading.Padding = new Padding(16, 0, 12, 0);
            _helpHeading.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _helpHeading.BackColor = DarkTheme.Panel2;
            _helpHeading.ForeColor = Color.White;
            content.Controls.Add(_helpHeading, 0, 0);
            _helpContent = new RichTextBox();
            _helpContent.Dock = DockStyle.Fill;
            _helpContent.Margin = new Padding(0);
            _helpContent.Padding = new Padding(12);
            _helpContent.ReadOnly = true;
            _helpContent.BorderStyle = BorderStyle.None;
            _helpContent.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            _helpContent.BackColor = DarkTheme.Panel;
            _helpContent.ForeColor = DarkTheme.Fore;
            murumsWiiModStudio.StudioChrome.EnableLinks(_helpContent);
            _helpContent.WordWrap = true;
            content.Controls.Add(_helpContent, 0, 1);
            layout.Controls.Add(content, 1, 0);
            tab.Controls.Add(layout);
            _helpNav.SelectedIndex = 0;
        }

        private void UpdateHelpTopic()
        {
            if (_helpNav == null || _helpHeading == null || _helpContent == null)
                return;
            int index = _helpNav.SelectedIndex;
            if (index < 0)
                index = 0;
            string heading;
            string body;
            GetHelpTopic(index, out heading, out body);
            _helpHeading.Text = heading;
            _helpContent.Text = body + "\r\n\r\n" + murumsWiiModStudio.StudioHelp.AnimationGuide();
            _helpContent.SelectAll();
            _helpContent.SelectionIndent = 14;
            _helpContent.SelectionRightIndent = 14;
            _helpContent.SelectionLength = 0;
            _helpContent.SelectionStart = 0;
            _helpContent.ScrollToCaret();
        }

        private void GetHelpTopic(int index, out string heading, out string body)
        {
            heading = L.T("Hilfe", "Help");
            body = "";
            if (L.IsGerman)
            {
                if (index == 0)
                {
                    heading = "Schnellstart";
                    body = @"1. BRLAN öffnen
Ziehe eine .brlan ins Fenster oder wähle Datei > Öffnen.

2. Links im Strukturbaum auswählen
RLAN > pai1 > Animation > Tag > Entry. Du musst nicht alles verstehen: Wähle nur den Teil, den du ändern willst.

3. Rechts bearbeiten
Das obere Feld zeigt Eigenschaften. Wenn du einen Entry auswählst, erscheinen unten die Keyframes.

4. Für Bildanimationen
Nutze entweder RLTP Generator oder den separaten GIF Import. Der GIF Import erzeugt TPL-Dateien und RLTP automatisch.

5. Validieren
Vor dem Speichern immer Validator ausführen.

6. Speichern
Ctrl+S überschreibt die aktuell geöffnete BRLAN direkt. Mit Speichern unter erzeugst du eine neue Datei. Der Editor prüft die erzeugte BRLAN nochmals automatisch.

7. Testen
Erst nach einem erfolgreichen Test auf Hardware übernehmen.";
                }
                else if (index == 1)
                {
                    heading = "Oberfläche";
                    body = @"STRUKTUR – links
Komplette BRLAN-Hierarchie. [RAW] bedeutet: Daten sind nicht vollständig dokumentiert und werden konservativ erhalten.

EDITOR – rechts
Eigenschaften des ausgewählten Elements plus Keyframe-Tabelle.

TPL-DATEIEN
Dateinamen, auf welche RLTP über einen Index zugreift. Beim Verschieben oder Löschen passt Studio RLTP-Indizes automatisch an.

RECHTSKLICK & DRAG & DROP
TPLs, Animationen, Tags und Entries besitzen kontextabhängige Rechtsklick-Menüs. Ziehe Elemente mit der Maus innerhalb ihres logischen Elternbereichs, um sie neu anzuordnen. TPL-Reihenfolgen werden dabei inklusive RLTP-Indizes sicher angepasst.

TASTATUR
Delete = löschen, Ctrl+D = duplizieren, Ctrl+↑/↓ = verschieben, F2 = TPL umbenennen.

VALIDATOR
Fehler und Warnungen vor dem Speichern.

RAW / HEX
Technische Rohansicht zum Vergleichen und Debuggen.

HILFE
Diese gegliederte Anleitung. Sprache kannst du oben über Sprache > Deutsch / English wechseln.";
                }
                else if (index == 2)
                {
                    heading = "BRLAN-Struktur";
                    body = @"RLAN
Dateikopf mit Version, Byte Order und Sektionen.

pat1
Nur teilweise dokumentiert. Studio verändert diese Sektion bewusst konservativ.

pai1
Zentrale Animationssektion mit Framezahl, Flags, TPL-Liste und Animationen.

ANIMATION
Zielt auf ein Pane oder Material.

TAG
Bestimmt die Animationsart, z.B. RLTS oder RLTP.

ENTRY
Enthält Index, Target, Keyframe-Typ und Keyframes.";
                }
                else if (index == 3)
                {
                    heading = "Animationstags";
                    body = @"RLPA – Pane SRT
Position, Rotation, Skalierung und Grösse.

RLTS – Texture SRT
Texturverschiebung, Rotation und Skalierung.

RLVI – Visibility
Sichtbarkeit.

RLVC – Vertex Color
RGBA der Ecken und PaneAlpha.

RLMC – Material Color
Material-, TEV- und Konst-Farben.

RLTP – Texture Pattern
Wechselt zwischen TPL-Bildern. Bei Type 1 ist der UInt16-Wert der TPL-Index.

[RAW]
Unbekannte oder nicht vollständig parsebare Tags werden nicht erfunden, sondern roh erhalten.";
                }
                else if (index == 4)
                {
                    heading = "Keyframes";
                    body = @"TYPE 1
Frame (Float) + Wert (UInt16) + Padding (UInt16). Typisch für RLVI und RLTP.

TYPE 2
Frame (Float) + Wert (Float) + Blend (Float). Typisch für kontinuierliche Transformationen und Farben.

BEDIENUNG
+ Keyframe = neuer Eintrag.
Keyframe löschen = markierte Zeilen entfernen.
Nach Frame sortieren = zeitlich sortieren.

Tipp für Einsteiger: Wenn du nur ein GIF importieren willst, musst du Keyframes nicht manuell anlegen.";
                }
                else if (index == 5)
                {
                    heading = "RLTP-Bildanimation";
                    body = @"RLTP schaltet zwischen TPL-Bildern um.

MANUELL
1. TPL-Dateien im Tab TPL-Dateien anlegen.
2. RLTP Generator öffnen.
3. Zielanimation wählen.
4. Frames pro Bild festlegen.
5. Erzeugen und validieren.

Beispiel bei 6 Frames pro Bild:
0 -> TPL 0
6 -> TPL 1
12 -> TPL 2
18 -> TPL 3

Einfacher ist für echte GIFs der separate GIF Import.";
                }
                else if (index == 6)
                {
                    heading = "GIF-Import";
                    body = @"Der GIF Import ist bewusst ein separates Fenster.

ER MACHT AUTOMATISCH
• GIF-Frames extrahieren
• auf Zielgrösse skalieren oder croppen
• echte Wii-TPL-Dateien erzeugen
• RGB5A3 oder RGB565 verwenden
• GIF-Timing auf 60-FPS-BRLAN-Frames übertragen
• optional Frames reduzieren
• RLTP erzeugen/aktualisieren
• TPL-Liste ergänzen oder ersetzen
• optional BRLYT einlesen und TPL → Material → Texture-Slot automatisch auflösen
• Original-TPL-Auflösung und unterstütztes Format aus einem benachbarten timg-Ordner übernehmen
• Manifest-Datei mit allen erzeugten Frames schreiben

BRLYT-GESTÜTZTER MODUS
Lade die passende .brlyt und wähle die Basis-TPL. Studio setzt Materialziel und Texture-Slot automatisch. Das ist besonders sinnvoll für Title.szs und andere Layouts mit mehreren Bildschichten.

EMPFOHLEN FÜR MENU SINGLE / bg_Loop
Zielgrösse: 1024 × 512
Skalierung: Füllen / Crop
TPL: CMPR
Timing: Original-GIF
Materialziel: P_pict
TPL-Liste: Vorhandene GIF-Einträge dieses Präfixes ersetzen
BRLAN-Dauer: beibehalten + GIF wiederholen

WICHTIG
RLTP animiert einen BRLYT-Materialnamen. Beim MenuSingle-Hintergrund hängt bg.tpl an P_pict; line0 ist nur das Pattern/Overlay. Studio erkennt bg_Loop.brlan deshalb automatisch und verwendet P_pict.

Beim Import in eine originale bg_Loop.brlan wird die alte line0-Materialanimation im bestätigten Kompatibilitätsmodus auf P_pict umgestellt und der alte RLTS-Tag entfernt. Das verhindert den früheren Standbild-Fehler.

Wenn ein GIF extrem viele Frames besitzt, warnt Studio vor einer sehr grossen SZS.";
                }
                else if (index == 7)
                {
                    heading = "Validator";
                    body = @"PRÜFT UNTER ANDEREM
• leere/doppelte TPL-Namen
• Nicht-ASCII-Namen
• zu lange Animationsnamen
• unbekannte Targets
• nicht unterstützte Keyframe-Typen
• unsortierte Keyframes
• Keyframes ausserhalb der Framezahl
• ungültige RLTP-Indizes
• Raw-only-Tags

Rot = Fehler, vor dem Speichern beheben.
Gelb = Warnung, bewusst prüfen.
Info = technischer Hinweis.";
                }
                else if (index == 8)
                {
                    heading = "Sicherheit & Workflow";
                    body = @"EMPFOHLENER WORKFLOW
1. Original behalten.
2. BRLAN öffnen.
3. Nur benötigte Werte ändern.
4. Validator ausführen.
5. Selbsttest ausführen.
6. Mit Ctrl+S direkt speichern oder mit Speichern unter eine neue Datei erzeugen.
7. In BrawlBox/BrawlCrate ersetzen.
8. Testen.
9. Erst dann auf Hardware.

Studio versucht unbekannte Daten möglichst unverändert zu erhalten. Nicht dokumentierte proprietäre Sonderformen können trotzdem nicht garantiert vollständig editierbar sein.";
                }
                else
                {
                    heading = "Kurzreferenz";
                    body = @"Target-Klasse
0 = Pane
1 = Material

Tags
RLPA = Pane SRT
RLTS = Texture SRT
RLVI = Visibility
RLVC = Vertex Color
RLMC = Material Color
RLTP = Texture Pattern

Keyframe Type 1
Float Frame + UInt16 Value + UInt16 Padding

Keyframe Type 2
Float Frame + Float Value + Float Blend

RLTP
UInt16 Value = TPL-Index

Vor dem Einsatz
Validator -> Selbsttest -> Speichern / Speichern unter -> Test";
                }
            }
            else
            {
                if (index == 0)
                {
                    heading = "Quick Start";
                    body = @"1. Open a BRLAN
Drop a .brlan onto the window or choose File > Open.

2. Select an item in the structure tree
RLAN > pai1 > Animation > Tag > Entry. You do not need to understand everything; select only what you want to edit.

3. Edit on the right
The upper area shows properties. Selecting an Entry displays its keyframes below.

4. For image animations
Use the RLTP Generator or the separate GIF Import. GIF Import can create the TPL files and RLTP automatically.

5. Validate
Always run the Validator before saving.

6. Save
Ctrl+S overwrites the currently opened BRLAN. Save As writes a new file. The editor automatically reparses the generated BRLAN.

7. Test
Only move to real hardware after a successful test.";
                }
                else if (index == 1)
                {
                    heading = "Interface";
                    body = @"STRUCTURE – left
The complete BRLAN hierarchy. [RAW] means the data is not fully documented and is preserved conservatively.

EDITOR – right
Properties of the selected item plus its keyframe table.

TPL FILES
Filenames referenced by RLTP through numeric indices. When you move or delete TPL entries, Studio automatically remaps RLTP indices.

RIGHT-CLICK & DRAG & DROP
TPLs, animations, tags and entries have context-sensitive right-click menus. Drag items with the mouse inside their logical parent to reorder them. TPL reordering also remaps RLTP indices safely.

KEYBOARD
Delete = delete, Ctrl+D = duplicate, Ctrl+Up/Down = move, F2 = rename TPL.

VALIDATOR
Shows errors and warnings before saving.

RAW / HEX
Technical raw view for comparison and debugging.

HELP
This structured guide. Change language via Language > Deutsch / English.";
                }
                else if (index == 2)
                {
                    heading = "BRLAN Structure";
                    body = @"RLAN
File header containing version, byte order and sections.

pat1
Only partially documented. Studio modifies it conservatively.

pai1
Main animation section containing frame count, flags, TPL list and animations.

ANIMATION
Targets a Pane or Material.

TAG
Defines the animation type, such as RLTS or RLTP.

ENTRY
Contains index, target, keyframe type and keyframes.";
                }
                else if (index == 3)
                {
                    heading = "Animation Tags";
                    body = @"RLPA – Pane SRT
Position, rotation, scale and size.

RLTS – Texture SRT
Texture translation, rotation and scale.

RLVI – Visibility
Visibility.

RLVC – Vertex Color
Corner RGBA values and PaneAlpha.

RLMC – Material Color
Material, TEV and konst colors.

RLTP – Texture Pattern
Switches between TPL images. For Type 1, the UInt16 value is the TPL index.

[RAW]
Unknown or incompletely parsed tags are preserved as raw data instead of being guessed.";
                }
                else if (index == 4)
                {
                    heading = "Keyframes";
                    body = @"TYPE 1
Frame (Float) + Value (UInt16) + Padding (UInt16). Common for RLVI and RLTP.

TYPE 2
Frame (Float) + Value (Float) + Blend (Float). Common for continuous transforms and colors.

CONTROLS
+ Keyframe = add a new entry.
Delete keyframe = remove selected rows.
Sort by frame = sort chronologically.

Beginner tip: if you only want to import a GIF, you do not need to create keyframes manually.";
                }
                else if (index == 5)
                {
                    heading = "RLTP Image Animation";
                    body = @"RLTP switches between TPL images.

MANUAL METHOD
1. Add TPL filenames in the TPL Files tab.
2. Open RLTP Generator.
3. Select the target animation.
4. Set frames per image.
5. Generate and validate.

Example at 6 frames per image:
0 -> TPL 0
6 -> TPL 1
12 -> TPL 2
18 -> TPL 3

For real GIFs, the separate GIF Import is easier.";
                }
                else if (index == 6)
                {
                    heading = "GIF Import";
                    body = @"GIF Import is intentionally a separate window.

AUTOMATIC FEATURES
• extract GIF frames
• resize/crop to the target resolution
• create real Wii TPL files
• RGB5A3 or RGB565 output
• convert GIF timing to 60-FPS BRLAN frames
• optionally reduce frame count
• create/update RLTP
• append to or replace the TPL list
• optionally read a BRLYT and resolve TPL → material → texture slot automatically
• inherit original TPL size and supported format from a nearby timg folder
• write a manifest with all generated frames

BRLYT-ASSISTED MODE
Load the matching .brlyt and select the base TPL. Studio automatically sets the material target and texture slot. This is especially useful for Title.szs and other multi-layer layouts.

RECOMMENDED FOR MENU SINGLE / bg_Loop
Target size: 1024 × 512
Resize: Fill / Crop
TPL: CMPR
Timing: Original GIF
Material target: P_pict
TPL list: Replace existing GIF entries for this prefix
BRLAN duration: keep + repeat GIF

IMPORTANT
RLTP targets a BRLYT material name. In MenuSingle, bg.tpl belongs to P_pict; line0 is only the pattern/overlay. Studio therefore detects bg_Loop.brlan and automatically uses P_pict.

When importing into a stock bg_Loop.brlan, Studio uses the confirmed compatibility path: the old line0 material animation is retargeted to P_pict and the legacy RLTS tag is removed. This fixes the previous still-image failure.

Studio warns when a GIF would create a very large number of TPL files.";
                }
                else if (index == 7)
                {
                    heading = "Validator";
                    body = @"CHECKS INCLUDE
• empty/duplicate TPL filenames
• non-ASCII names
• overlong animation names
• unknown targets
• unsupported keyframe types
• unsorted keyframes
• keyframes outside the total frame count
• invalid RLTP indices
• raw-only tags

Red = error, fix before saving.
Yellow = warning, review intentionally.
Info = technical note.";
                }
                else if (index == 8)
                {
                    heading = "Safety & Workflow";
                    body = @"RECOMMENDED WORKFLOW
1. Keep the original.
2. Open the BRLAN.
3. Change only what is needed.
4. Run Validator.
5. Run Self-test.
6. Save with Ctrl+S or use Save As for a new file.
7. Replace it in BrawlBox/BrawlCrate.
8. Test.
9. Only then use real hardware.

Studio tries to preserve unknown data unchanged. Undocumented proprietary variants still cannot be guaranteed to be fully editable.";
                }
                else
                {
                    heading = "Quick Reference";
                    body = @"Target class
0 = Pane
1 = Material

Tags
RLPA = Pane SRT
RLTS = Texture SRT
RLVI = Visibility
RLVC = Vertex Color
RLMC = Material Color
RLTP = Texture Pattern

Keyframe Type 1
Float Frame + UInt16 Value + UInt16 Padding

Keyframe Type 2
Float Frame + Float Value + Float Blend

RLTP
UInt16 Value = TPL index

Before use
Validator -> Self-test -> Save / Save As -> test";
                }
            }
        }

        private void SetStatus(string text)
        {
            _status.Text = text;
        }
    }
}
