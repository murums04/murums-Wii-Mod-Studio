using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using murumsWiiModStudio;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class BrlytEditorForm : Form
    {
        private readonly string _path;
        private BrlytDocument _doc;
        private TreeView _tree;
        private Panel _editorHost;
        private HudLayoutCanvas _preview;
        private Label _summary;
        private ToolStripStatusLabel _status;
        private bool _dirty;
        private bool _updating;
        private readonly Dictionary<string, Bitmap> _textureCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private BrlytPaneInfo _selectedPane;
        private BrlytMaterialInfo _selectedMaterial;
        private CheckBox _visible;
        private NumericUpDown _alpha;
        private TextBox _name;
        private NumericUpDown _x, _y, _z, _rx, _ry, _rz, _sx, _sy, _w, _h;
        private Label _materialInfo;
        private TextBox _materialName;
        private ComboBox _originX, _originY, _parentOriginX, _parentOriginY, _paneMaterial, _paneFont, _lineAlignment;
        private NumericUpDown _fontW, _fontH, _charSize, _lineSize;
        private bool _showGrid = true;
        private sealed class TextureNodeTag
        {
            public int Index;
            public string Name;
        }

        private sealed class FontNodeTag
        {
            public int Index;
            public string Name;
        }

        private sealed class PaneEditState
        {
            public string Name;
            public byte Flags, Origin, Alpha;
            public float X, Y, Z, RotX, RotY, RotZ, ScaleX, ScaleY, Width, Height;
            public int MaterialId;
            public int FontId;
            public byte TextOrigin, LineAlignment;
            public float FontWidth, FontHeight, CharSize, LineSize;
        }

        private sealed class MaterialEditState
        {
            public string Name;
            public ushort[] TextureIds;
        }

        private sealed class EditorState
        {
            public PaneEditState[] Panes;
            public MaterialEditState[] Materials;
        }

        private readonly Stack<EditorState> _undo = new Stack<EditorState>();
        private readonly Stack<EditorState> _redo = new Stack<EditorState>();
        private DateTime _lastUndoRecord = DateTime.MinValue;
        private bool _applyingUndo;
        public bool Saved { get; private set; }

        public BrlytEditorForm(string path)
        {
            _path = path;
            Text = L.T("BRLYT Layout Editor", "BRLYT Layout Editor");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1050, 700);
            Size = new Size(1280, 820);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10F);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += BrlytEditorKeyDown;
            BuildUi();
            DarkTheme.Apply(this);
            LoadDocument();
            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                DisposeTextureCache();
            };
        }

        private void BrlytEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.Z)
            {
                RedoEdit();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                RedoEdit();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Z)
            {
                UndoEdit();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                Save();
                e.SuppressKeyPress = true;
            }
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            Controls.Add(root);
            Panel header = murumsWiiModStudio.StudioChrome.Header(L.T("Menü-Layout bearbeiten", "Edit menu layout"), L.T("Element auswählen • Position, Größe und Aussehen ändern", "Select an element • Change position, size and appearance"));
            root.Controls.Add(header, 0, 0);
            ToolStrip bar = new ToolStrip();
            bar.Dock = DockStyle.Fill;
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding(8, 5, 0, 5);
            bar.Items.Add(MakeTool(L.T("Speichern", "Save"), delegate
            {
                Save();
            }));
            bar.Items.Add(MakeTool(L.T("Neu laden", "Reload"), delegate
            {
                Reload();
            }));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(StudioHistorySymbols.Tool(MakeTool(StudioHistorySymbols.Undo, delegate
            {
                UndoEdit();
            }), false, L.T("Rückgängig (Strg+Z)", "Undo (Ctrl+Z)")));
            bar.Items.Add(StudioHistorySymbols.Tool(MakeTool(StudioHistorySymbols.Redo, delegate
            {
                RedoEdit();
            }), true, L.T("Wiederholen (Strg+Y)", "Redo (Ctrl+Y)")));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(MakeTool(L.T("Pane zentrieren", "Center pane"), delegate
            {
                CenterSelectedPane();
            }));
            bar.Items.Add(MakeTool(L.T("Scale = 1", "Scale = 1"), delegate
            {
                ResetScale();
            }));
            ToolStripButton gridButton = MakeTool(L.T("Raster", "Grid"), delegate
            {
                _showGrid = !_showGrid;
                DrawPreview();
            });
            gridButton.CheckOnClick = true;
            gridButton.Checked = true;
            bar.Items.Add(gridButton);
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(MakeTool(L.T("Hilfe", "Help"), delegate
            {
                ShowHelp();
            }));
            root.Controls.Add(bar, 0, 1);
            SplitContainer outer = new SplitContainer();
            outer.Dock = DockStyle.Fill;
            outer.FixedPanel = FixedPanel.Panel1;
            outer.SplitterWidth = 4;
            outer.BackColor = DarkTheme.Border;
            // Do not set Panel1MinSize/Panel2MinSize here. During BuildUi the
            // SplitContainer has not been laid out yet and WinForms can throw
            // InvalidOperationException when its default SplitterDistance is
            // outside the future minimum-size range. Configure it after Shown.
            root.Controls.Add(outer, 0, 2);
            _tree = new TreeView();
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.AfterSelect += delegate
            {
                SelectNode();
            };
            outer.Panel1.Controls.Add(_tree);
            SplitContainer right = new SplitContainer();
            right.Dock = DockStyle.Fill;
            right.Orientation = Orientation.Horizontal;
            right.SplitterWidth = 4;
            right.BackColor = DarkTheme.Border;
            outer.Panel2.Controls.Add(right);
            _editorHost = new Panel();
            _editorHost.Dock = DockStyle.Fill;
            _editorHost.Padding = new Padding(14);
            _editorHost.AutoScroll = true;
            right.Panel1.Controls.Add(_editorHost);
            TableLayoutPanel previewPanel = new TableLayoutPanel();
            previewPanel.Dock = DockStyle.Fill;
            previewPanel.RowCount = 2;
            previewPanel.ColumnCount = 1;
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            _preview = new HudLayoutCanvas();
            _preview.Texture = GetPaneTexture;
            _preview.SelectPane = SelectPaneInTree;
            _preview.BeginTransform = delegate
            {
                RecordUndo(true);
            };
            _preview.PositionChanged = SyncPanePositionControls;
            _preview.CommitMove = delegate
            {
                _dirty = true;
                _status.Text = L.T("Layout geändert – noch nicht gespeichert", "Layout changed – not saved yet");
                DrawPreview();
            };
            _summary = new Label();
            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = DarkTheme.Muted;
            previewPanel.Controls.Add(_preview, 0, 0);
            previewPanel.Controls.Add(_summary, 0, 1);
            right.Panel2.Controls.Add(previewPanel);
            StatusStrip ss = new StatusStrip();
            ss.Dock = DockStyle.Fill;
            ss.SizingGrip = false;
            _status = new ToolStripStatusLabel(L.T("Bereit", "Ready"));
            ss.Items.Add(_status);
            root.Controls.Add(ss, 0, 3);
            Shown += delegate
            {
                try
                {
                    ConfigureOuterSplitter(outer);
                    ConfigureRightSplitter(right);
                }
                catch
                {
                // Layout must never prevent the editor from opening.
                }
            };
        }

        private static void ConfigureOuterSplitter(SplitContainer split)
        {
            if (split == null || split.Width <= 0)
                return;
            const int desiredDefault = 300;
            const int minimumLeft = 240;
            const int minimumRight = 600;
            // Start without constraints, move the splitter to a valid position,
            // then apply the constraints. This order is safe on high-DPI and on
            // systems where WinForms gives a SplitContainer a tiny initial size.
            split.Panel1MinSize = 0;
            split.Panel2MinSize = 0;
            int available = split.Width - split.SplitterWidth;
            if (available <= 1)
                return;
            int desired = desiredDefault;
            if (desired >= available)
                desired = Math.Max(1, available / 3);
            bool canApplyMinimums = available > minimumLeft + minimumRight;
            if (canApplyMinimums)
            {
                int maxForRight = available - minimumRight;
                if (desired < minimumLeft)
                    desired = minimumLeft;
                if (desired > maxForRight)
                    desired = maxForRight;
            }
            else
            {
                // Small window/DPI fallback: keep both panels usable without
                // imposing impossible minimum-size constraints.
                int softMin = Math.Min(160, Math.Max(1, available / 4));
                int softMax = Math.Max(softMin, available - softMin);
                if (desired < softMin)
                    desired = softMin;
                if (desired > softMax)
                    desired = softMax;
            }

            if (desired > 0 && desired < available)
                split.SplitterDistance = desired;
            if (canApplyMinimums)
            {
                split.Panel1MinSize = minimumLeft;
                split.Panel2MinSize = minimumRight;
            }
        }

        private static void ConfigureRightSplitter(SplitContainer split)
        {
            if (split == null || split.Height <= 0)
                return;
            int available = split.Height - split.SplitterWidth;
            if (available <= 1)
                return;
            int desired = 360;
            int minimumTop = Math.Min(220, Math.Max(1, available / 3));
            int minimumBottom = Math.Min(180, Math.Max(1, available / 4));
            int max = available - minimumBottom;
            if (desired < minimumTop)
                desired = minimumTop;
            if (desired > max)
                desired = max;
            if (desired > 0 && desired < available)
                split.SplitterDistance = desired;
        }

        private ToolStripButton MakeTool(string text, EventHandler click)
        {
            ToolStripButton b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            return b;
        }

        private void LoadDocument()
        {
            _doc = BrlytDocument.Load(_path);
            _dirty = false;
            Saved = false;
            _undo.Clear();
            _redo.Clear();
            _lastUndoRecord = DateTime.MinValue;
            RebuildTree();
            DrawPreview();
            _summary.Text = L.Format("{0} × {1} • {2} Panes • {3} Materialien • {4} Texturen • {5} Fonts", "{0} × {1} • {2} panes • {3} materials • {4} textures • {5} fonts", _doc.LayoutWidth.ToString("0.##", CultureInfo.InvariantCulture), _doc.LayoutHeight.ToString("0.##", CultureInfo.InvariantCulture), _doc.Panes.Count, _doc.Materials.Count, _doc.Textures.Count, _doc.Fonts.Count);
            _status.Text = Path.GetFileName(_path);
        }

        private void RebuildTree()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode quick = new TreeNode(L.T("Layout / Panes", "Layout / Panes"));
            quick.Tag = "panes";
            _tree.Nodes.Add(quick);
            int i;
            for (i = 0; i < _doc.RootPanes.Count; i++)
                AddPaneNode(quick.Nodes, _doc.RootPanes[i]);
            TreeNode mats = new TreeNode(L.T("Materialien", "Materials") + " (" + _doc.Materials.Count + ")");
            mats.Tag = "materials";
            _tree.Nodes.Add(mats);
            for (i = 0; i < _doc.Materials.Count; i++)
            {
                TreeNode n = new TreeNode(_doc.Materials[i].Name);
                n.Tag = _doc.Materials[i];
                mats.Nodes.Add(n);
            }

            TreeNode tex = new TreeNode(L.T("Texturen", "Textures") + " (" + _doc.Textures.Count + ")");
            tex.Tag = "textures";
            _tree.Nodes.Add(tex);
            for (i = 0; i < _doc.Textures.Count; i++)
            {
                TreeNode tn = new TreeNode("[" + i + "] " + _doc.Textures[i]);
                TextureNodeTag tag = new TextureNodeTag();
                tag.Index = i;
                tag.Name = _doc.Textures[i];
                tn.Tag = tag;
                tex.Nodes.Add(tn);
            }

            TreeNode fonts = new TreeNode(L.T("Fonts", "Fonts") + " (" + _doc.Fonts.Count + ")");
            fonts.Tag = "fonts";
            _tree.Nodes.Add(fonts);
            for (i = 0; i < _doc.Fonts.Count; i++)
            {
                TreeNode fn = new TreeNode("[" + i + "] " + _doc.Fonts[i]);
                FontNodeTag tag = new FontNodeTag();
                tag.Index = i;
                tag.Name = _doc.Fonts[i];
                fn.Tag = tag;
                fonts.Nodes.Add(fn);
            }

            quick.Expand();
            mats.Expand();
            _tree.EndUpdate();
        }

        private void AddPaneNode(TreeNodeCollection nodes, BrlytPaneInfo pane)
        {
            TreeNode n = new TreeNode(pane.ToString());
            n.Tag = pane;
            nodes.Add(n);
            int i;
            for (i = 0; i < pane.Children.Count; i++)
                AddPaneNode(n.Nodes, pane.Children[i]);
        }

        private void SelectNode()
        {
            _selectedPane = null;
            _selectedMaterial = null;
            if (_tree.SelectedNode == null)
            {
                ShowInfo(L.T("Wähle links ein Pane oder Material aus.", "Select a pane or material on the left."));
                return;
            }

            BrlytPaneInfo pane = _tree.SelectedNode.Tag as BrlytPaneInfo;
            if (pane != null)
            {
                _selectedPane = pane;
                BuildPaneEditor(pane);
                DrawPreview();
                return;
            }

            BrlytMaterialInfo mat = _tree.SelectedNode.Tag as BrlytMaterialInfo;
            if (mat != null)
            {
                _selectedMaterial = mat;
                BuildMaterialEditor(mat);
                DrawPreview();
                return;
            }

            TextureNodeTag texture = _tree.SelectedNode.Tag as TextureNodeTag;
            if (texture != null)
            {
                BuildTextureEditor(texture);
                DrawPreview();
                return;
            }

            FontNodeTag font = _tree.SelectedNode.Tag as FontNodeTag;
            if (font != null)
            {
                BuildFontEditor(font);
                DrawPreview();
                return;
            }

            ShowInfo(L.T("Wähle einen konkreten Eintrag aus.", "Select a concrete item."));
        }

        private void ClearEditor()
        {
            _editorHost.Controls.Clear();
        }

        private void ShowInfo(string text)
        {
            ClearEditor();
            Label l = new Label();
            l.Dock = DockStyle.Top;
            l.AutoSize = true;
            l.MaximumSize = new Size(700, 0);
            l.ForeColor = DarkTheme.Muted;
            l.Text = text;
            _editorHost.Controls.Add(l);
        }

        private void BuildPaneEditor(BrlytPaneInfo p)
        {
            _updating = true;
            ClearEditor();
            TableLayoutPanel t = NewFormTable(22);
            _editorHost.Controls.Add(t);
            int r = 0;
            Label title = new Label();
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Text = p.ToString();
            t.SetColumnSpan(title, 2);
            t.Controls.Add(title, 0, r++);
            Label hint = new Label();
            hint.AutoSize = true;
            hint.MaximumSize = new Size(620, 0);
            hint.ForeColor = DarkTheme.Muted;
            hint.Text = L.T("Tipp: Das Pane kann auch direkt in der Vorschau angeklickt und mit der Maus verschoben werden.", "Tip: You can also click the pane directly in the preview and drag it with the mouse.");
            t.SetColumnSpan(hint, 2);
            t.Controls.Add(hint, 0, r++);
            _name = AddText(t, r++, L.T("Name", "Name"), p.Name);
            _visible = new CheckBox();
            _visible.Text = L.T("Sichtbar", "Visible");
            _visible.Checked = p.Visible;
            _visible.AutoSize = true;
            _visible.CheckedChanged += PaneFieldChanged;
            AddControl(t, r++, L.T("Anzeige", "Display"), _visible);
            _alpha = AddNum(t, r++, "Alpha (0–255)", p.Alpha, 0, 255, 0);
            string[] xOrigins = new string[]
            {
                L.T("Mitte", "Center"),
                L.T("Links", "Left"),
                L.T("Rechts", "Right"),
                L.T("Unbekannt (3)", "Unknown (3)")
            };
            string[] yOrigins = new string[]
            {
                L.T("Mitte", "Center"),
                L.T("Oben", "Top"),
                L.T("Unten", "Bottom"),
                L.T("Unbekannt (3)", "Unknown (3)")
            };
            _originX = AddCombo(t, r++, L.T("Origin X", "Origin X"), xOrigins, p.Origin & 0x03);
            _originY = AddCombo(t, r++, L.T("Origin Y", "Origin Y"), yOrigins, (p.Origin >> 2) & 0x03);
            _parentOriginX = AddCombo(t, r++, L.T("Parent Origin X", "Parent origin X"), xOrigins, (p.Origin >> 4) & 0x03);
            _parentOriginY = AddCombo(t, r++, L.T("Parent Origin Y", "Parent origin Y"), yOrigins, (p.Origin >> 6) & 0x03);
            _x = AddNum(t, r++, "Position X", p.X, -100000, 100000, 3);
            _y = AddNum(t, r++, "Position Y", p.Y, -100000, 100000, 3);
            _z = AddNum(t, r++, "Position Z", p.Z, -100000, 100000, 3);
            _rx = AddNum(t, r++, "Rotation X", p.RotX, -36000, 36000, 3);
            _ry = AddNum(t, r++, "Rotation Y", p.RotY, -36000, 36000, 3);
            _rz = AddNum(t, r++, "Rotation Z", p.RotZ, -36000, 36000, 3);
            _sx = AddNum(t, r++, "Scale X", p.ScaleX, -1000, 1000, 4);
            _sy = AddNum(t, r++, "Scale Y", p.ScaleY, -1000, 1000, 4);
            _w = AddNum(t, r++, L.T("Breite", "Width"), p.Width, 0, 100000, 3);
            _h = AddNum(t, r++, L.T("Höhe", "Height"), p.Height, 0, 100000, 3);
            BrlytMaterialInfo mat = _doc.MaterialForPane(p);
            if ((p.Magic == "pic1" || p.Magic == "txt1" || p.Magic == "wnd1") && _doc.Materials.Count > 0)
            {
                string[] items = new string[_doc.Materials.Count];
                for (int mi = 0; mi < _doc.Materials.Count; mi++)
                    items[mi] = "[" + mi + "] " + _doc.Materials[mi].Name;
                _paneMaterial = AddCombo(t, r++, p.Magic == "txt1" ? L.T("Text Material", "Text material") : L.T("Picture Material", "Picture material"), items, p.MaterialId >= 0 && p.MaterialId < items.Length ? p.MaterialId : 0);
                _paneMaterial.SelectedIndexChanged += PaneFieldChanged;
            }
            else
            {
                _paneMaterial = null;
                _materialInfo = new Label();
                _materialInfo.AutoSize = true;
                _materialInfo.MaximumSize = new Size(600, 0);
                _materialInfo.ForeColor = DarkTheme.Muted;
                if (mat == null)
                    _materialInfo.Text = L.T("Kein direktes Picture-Material erkannt.", "No direct picture material detected.");
                else
                {
                    string binding = mat.Name;
                    if (mat.Bindings.Count > 0)
                        binding += " → " + mat.Bindings[0].TextureName;
                    _materialInfo.Text = binding;
                }

                AddControl(t, r++, L.T("Material", "Material"), _materialInfo);
            }

            if (p.Magic == "txt1")
            {
                if (_doc.Fonts.Count > 0)
                {
                    string[] fontItems = new string[_doc.Fonts.Count];
                    for (int fi = 0; fi < _doc.Fonts.Count; fi++)
                        fontItems[fi] = "[" + fi + "] " + _doc.Fonts[fi];
                    _paneFont = AddCombo(t, r++, L.T("Font", "Font"), fontItems, p.FontId >= 0 && p.FontId < fontItems.Length ? p.FontId : 0);
                    _paneFont.SelectedIndexChanged += PaneFieldChanged;
                }
                else
                    _paneFont = null;
                string[] alignItems = new string[]
                {
                    L.T("Nicht angegeben", "Not specified"),
                    L.T("Links", "Left"),
                    L.T("Mitte", "Center"),
                    L.T("Rechts", "Right")
                };
                _lineAlignment = AddCombo(t, r++, L.T("Textausrichtung", "Text alignment"), alignItems, p.LineAlignment <= 3 ? p.LineAlignment : 0);
                _lineAlignment.SelectedIndexChanged += PaneFieldChanged;
                _fontW = AddNum(t, r++, L.T("Font Breite", "Font width"), p.FontWidth, 0, 10000, 3);
                _fontH = AddNum(t, r++, L.T("Font Höhe", "Font height"), p.FontHeight, 0, 10000, 3);
                _charSize = AddNum(t, r++, L.T("Zeichenabstand", "Character spacing"), p.CharSize, -10000, 10000, 3);
                _lineSize = AddNum(t, r++, L.T("Zeilenabstand", "Line spacing"), p.LineSize, -10000, 10000, 3);
                _fontW.ValueChanged += PaneFieldChanged;
                _fontH.ValueChanged += PaneFieldChanged;
                _charSize.ValueChanged += PaneFieldChanged;
                _lineSize.ValueChanged += PaneFieldChanged;
                TextBox embedded = AddText(t, r++, L.T("Eingebetteter Text", "Embedded text"), p.EmbeddedText);
                embedded.ReadOnly = true;
                embedded.ForeColor = DarkTheme.Muted;
                murumsWiiModStudio.StudioUx.SetHelp(embedded, L.T("Nur Anzeige. Viele Wii-Spiele laden sichtbaren Text aus BMG-Dateien.", "Display only. Many Wii games load visible text from BMG files."));
            }
            else
            {
                _paneFont = null;
                _lineAlignment = null;
                _fontW = null;
                _fontH = null;
                _charSize = null;
                _lineSize = null;
            }

            _name.TextChanged += PaneFieldChanged;
            _alpha.ValueChanged += PaneFieldChanged;
            _x.ValueChanged += PaneFieldChanged;
            _y.ValueChanged += PaneFieldChanged;
            _z.ValueChanged += PaneFieldChanged;
            _rx.ValueChanged += PaneFieldChanged;
            _ry.ValueChanged += PaneFieldChanged;
            _rz.ValueChanged += PaneFieldChanged;
            _sx.ValueChanged += PaneFieldChanged;
            _sy.ValueChanged += PaneFieldChanged;
            _w.ValueChanged += PaneFieldChanged;
            _h.ValueChanged += PaneFieldChanged;
            _originX.SelectedIndexChanged += PaneFieldChanged;
            _originY.SelectedIndexChanged += PaneFieldChanged;
            _parentOriginX.SelectedIndexChanged += PaneFieldChanged;
            _parentOriginY.SelectedIndexChanged += PaneFieldChanged;
            _updating = false;
        }

        private void BuildMaterialEditor(BrlytMaterialInfo m)
        {
            _updating = true;
            ClearEditor();
            TableLayoutPanel t = NewFormTable(Math.Max(10, 5 + m.Bindings.Count));
            _editorHost.Controls.Add(t);
            int r = 0;
            Label title = new Label();
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Text = L.T("Material", "Material") + " #" + m.Index;
            t.SetColumnSpan(title, 2);
            t.Controls.Add(title, 0, r++);
            _materialName = AddText(t, r++, L.T("Materialname", "Material name"), m.Name);
            _materialName.MaxLength = 19;
            _materialName.TextChanged += MaterialFieldChanged;
            if (m.Bindings.Count == 0)
            {
                Label none = new Label();
                none.AutoSize = true;
                none.ForeColor = DarkTheme.Muted;
                none.Text = L.T("Dieses Material hat keine Texture Maps.", "This material has no texture maps.");
                AddControl(t, r++, L.T("Texturen", "Textures"), none);
            }
            else
            {
                string[] textureItems = new string[_doc.Textures.Count];
                for (int ti = 0; ti < _doc.Textures.Count; ti++)
                    textureItems[ti] = "[" + ti + "] " + _doc.Textures[ti];
                for (int i = 0; i < m.Bindings.Count; i++)
                {
                    BrlytTextureBinding binding = m.Bindings[i];
                    ComboBox combo = AddCombo(t, r++, L.T("Texture Slot ", "Texture slot ") + binding.Slot, textureItems, binding.TextureId < textureItems.Length ? binding.TextureId : 0);
                    combo.Tag = binding;
                    combo.SelectedIndexChanged += MaterialTextureChanged;
                }
            }

            Label safe = new Label();
            safe.AutoSize = true;
            safe.MaximumSize = new Size(620, 0);
            safe.ForeColor = DarkTheme.Muted;
            safe.Text = L.T("Texture-Zuordnungen und Materialname sind direkt editierbar. Komplexe TEV-/Blend-/Shader-Daten bleiben bewusst unverändert, damit unbekannte Wii-Materialdaten nicht beschädigt werden.", "Texture bindings and the material name are directly editable. Complex TEV/blend/shader data is intentionally preserved so unknown Wii material data is not damaged.");
            t.SetColumnSpan(safe, 2);
            t.Controls.Add(safe, 0, r++);
            _updating = false;
        }

        private void BuildTextureEditor(TextureNodeTag texture)
        {
            ClearEditor();
            TableLayoutPanel t = NewFormTable(8);
            _editorHost.Controls.Add(t);
            int r = 0;
            Label title = new Label();
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Text = L.T("Textur", "Texture") + " #" + texture.Index;
            t.SetColumnSpan(title, 2);
            t.Controls.Add(title, 0, r++);
            Label name = new Label();
            name.AutoSize = true;
            name.ForeColor = DarkTheme.Fore;
            name.Text = texture.Name;
            AddControl(t, r++, L.T("TPL-Datei", "TPL file"), name);
            int usageCount = 0;
            string usages = "";
            for (int mi = 0; mi < _doc.Materials.Count; mi++)
            {
                BrlytMaterialInfo mat = _doc.Materials[mi];
                for (int bi = 0; bi < mat.Bindings.Count; bi++)
                {
                    if (mat.Bindings[bi].TextureId == texture.Index)
                    {
                        if (usages.Length > 0)
                            usages += Environment.NewLine;
                        usages += mat.Name + "  •  Slot " + mat.Bindings[bi].Slot;
                        usageCount++;
                    }
                }
            }

            Label used = new Label();
            used.AutoSize = true;
            used.MaximumSize = new Size(600, 0);
            used.ForeColor = usageCount == 0 ? DarkTheme.Muted : DarkTheme.Fore;
            used.Text = usageCount == 0 ? L.T("Nicht direkt von einem Material referenziert.", "Not directly referenced by a material.") : usages;
            AddControl(t, r++, L.T("Verwendet von", "Used by"), used);
            Bitmap bmp = GetTextureByName(texture.Name);
            if (bmp != null)
            {
                PictureBox pic = new PictureBox();
                pic.Width = 420;
                pic.Height = 240;
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.BackColor = Color.FromArgb(28, 28, 34);
                pic.Image = new Bitmap(bmp);
                AddControl(t, r++, L.T("Vorschau", "Preview"), pic);
            }
        }

        private void BuildFontEditor(FontNodeTag font)
        {
            ClearEditor();
            TableLayoutPanel t = NewFormTable(6);
            _editorHost.Controls.Add(t);
            int r = 0;
            Label title = new Label();
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Text = L.T("Font", "Font") + " #" + font.Index;
            t.SetColumnSpan(title, 2);
            t.Controls.Add(title, 0, r++);
            Label name = new Label();
            name.AutoSize = true;
            name.ForeColor = DarkTheme.Fore;
            name.Text = font.Name;
            AddControl(t, r++, L.T("Font-Datei", "Font file"), name);
            int usageCount = 0;
            string usages = "";
            for (int i = 0; i < _doc.Panes.Count; i++)
            {
                BrlytPaneInfo pane = _doc.Panes[i];
                if (pane.Magic == "txt1" && pane.FontId == font.Index)
                {
                    if (usages.Length > 0)
                        usages += Environment.NewLine;
                    usages += pane.Name;
                    usageCount++;
                }
            }

            Label used = new Label();
            used.AutoSize = true;
            used.MaximumSize = new Size(600, 0);
            used.ForeColor = usageCount == 0 ? DarkTheme.Muted : DarkTheme.Fore;
            used.Text = usageCount == 0 ? L.T("Von keinem txt1-Pane referenziert.", "Not referenced by a txt1 pane.") : usages;
            AddControl(t, r++, L.T("Verwendet von", "Used by"), used);
        }

        private TableLayoutPanel NewFormTable(int rows)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Top;
            t.AutoSize = true;
            t.ColumnCount = 2;
            t.RowCount = rows;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return t;
        }

        private TextBox AddText(TableLayoutPanel t, int row, string label, string value)
        {
            TextBox box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.Text = value ?? "";
            AddControl(t, row, label, box);
            return box;
        }

        private ComboBox AddCombo(TableLayoutPanel t, int row, string label, string[] items, int selectedIndex)
        {
            ComboBox combo = new ComboBox();
            combo.Dock = DockStyle.Fill;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.BackColor = DarkTheme.Panel2;
            combo.ForeColor = DarkTheme.Fore;
            if (items != null)
                combo.Items.AddRange(items);
            if (combo.Items.Count > 0)
            {
                if (selectedIndex < 0 || selectedIndex >= combo.Items.Count)
                    selectedIndex = 0;
                combo.SelectedIndex = selectedIndex;
            }

            AddControl(t, row, label, combo);
            return combo;
        }

        private NumericUpDown AddNum(TableLayoutPanel t, int row, string label, double value, decimal min, decimal max, int decimals)
        {
            NumericUpDown n = new NumericUpDown();
            n.Dock = DockStyle.Fill;
            n.DecimalPlaces = decimals;
            n.Minimum = min;
            n.Maximum = max;
            n.Increment = decimals > 0 ? 0.1M : 1M;
            n.ReadOnly = false;
            n.TabStop = true;
            n.InterceptArrowKeys = true;
            decimal v;
            try
            {
                v = (decimal)value;
            }
            catch
            {
                v = 0;
            }

            if (v < min)
                v = min;
            if (v > max)
                v = max;
            n.Value = v;
            AddControl(t, row, label, n);
            return n;
        }

        private void AddControl(TableLayoutPanel t, int row, string label, Control control)
        {
            Label l = new Label();
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Text = label + ":";
            l.ForeColor = DarkTheme.Fore;
            l.Margin = new Padding(4, 6, 8, 6);
            murumsWiiModStudio.StudioUx.SetHelp(control, murumsWiiModStudio.StudioUx.FieldHelp(label));
            control.Margin = new Padding(4, 4, 4, 4);
            t.Controls.Add(l, 0, row);
            t.Controls.Add(control, 1, row);
        }

        private void PaneFieldChanged(object sender, EventArgs e)
        {
            if (_updating || _selectedPane == null)
                return;
            RecordUndoIfNeeded();
            try
            {
                _selectedPane.Name = _name.Text.Trim();
                if (_selectedPane.Name.Length > 15)
                    _selectedPane.Name = _selectedPane.Name.Substring(0, 15);
                _selectedPane.Visible = _visible.Checked;
                _selectedPane.Alpha = (byte)_alpha.Value;
                if (_originX != null && _originY != null && _parentOriginX != null && _parentOriginY != null)
                    _selectedPane.Origin = (byte)((_originX.SelectedIndex & 3) | ((_originY.SelectedIndex & 3) << 2) | ((_parentOriginX.SelectedIndex & 3) << 4) | ((_parentOriginY.SelectedIndex & 3) << 6));
                _selectedPane.X = (float)_x.Value;
                _selectedPane.Y = (float)_y.Value;
                _selectedPane.Z = (float)_z.Value;
                _selectedPane.RotX = (float)_rx.Value;
                _selectedPane.RotY = (float)_ry.Value;
                _selectedPane.RotZ = (float)_rz.Value;
                _selectedPane.ScaleX = (float)_sx.Value;
                _selectedPane.ScaleY = (float)_sy.Value;
                _selectedPane.Width = (float)_w.Value;
                _selectedPane.Height = (float)_h.Value;
                if (_paneMaterial != null && _paneMaterial.SelectedIndex >= 0)
                    _selectedPane.MaterialId = _paneMaterial.SelectedIndex;
                if (_selectedPane.Magic == "txt1")
                {
                    if (_paneFont != null && _paneFont.SelectedIndex >= 0)
                        _selectedPane.FontId = _paneFont.SelectedIndex;
                    if (_lineAlignment != null && _lineAlignment.SelectedIndex >= 0)
                        _selectedPane.LineAlignment = (byte)_lineAlignment.SelectedIndex;
                    if (_fontW != null)
                        _selectedPane.FontWidth = (float)_fontW.Value;
                    if (_fontH != null)
                        _selectedPane.FontHeight = (float)_fontH.Value;
                    if (_charSize != null)
                        _selectedPane.CharSize = (float)_charSize.Value;
                    if (_lineSize != null)
                        _selectedPane.LineSize = (float)_lineSize.Value;
                }

                _dirty = true;
                UpdateSelectedTreeText();
                DrawPreview();
                _status.Text = L.T("Geändert – noch nicht gespeichert", "Modified – not saved yet");
            }
            catch
            {
            }
        }

        private void MaterialFieldChanged(object sender, EventArgs e)
        {
            if (_updating || _selectedMaterial == null)
                return;
            RecordUndoIfNeeded();
            _selectedMaterial.Name = _materialName.Text.Trim();
            _dirty = true;
            UpdateSelectedTreeText();
            _status.Text = L.T("Geändert – noch nicht gespeichert", "Modified – not saved yet");
        }

        private void MaterialTextureChanged(object sender, EventArgs e)
        {
            if (_updating || _selectedMaterial == null)
                return;
            RecordUndoIfNeeded();
            ComboBox combo = sender as ComboBox;
            BrlytTextureBinding binding = combo == null ? null : combo.Tag as BrlytTextureBinding;
            if (binding == null || combo.SelectedIndex < 0 || combo.SelectedIndex >= _doc.Textures.Count)
                return;
            binding.TextureId = (ushort)combo.SelectedIndex;
            binding.TextureName = _doc.Textures[combo.SelectedIndex];
            _dirty = true;
            DrawPreview();
            _status.Text = L.T("Texture-Zuordnung geändert – noch nicht gespeichert", "Texture binding changed – not saved yet");
        }

        private void UpdateSelectedTreeText()
        {
            if (_tree.SelectedNode == null)
                return;
            if (_selectedPane != null)
                _tree.SelectedNode.Text = _selectedPane.ToString();
            else if (_selectedMaterial != null)
                _tree.SelectedNode.Text = _selectedMaterial.Name;
        }

        private void RecordUndoIfNeeded()
        {
            if (_applyingUndo || _doc == null)
                return;
            if ((DateTime.UtcNow - _lastUndoRecord).TotalMilliseconds < 350 && _undo.Count > 0)
                return;
            RecordUndo(false);
        }

        private void RecordUndo(bool force)
        {
            if (_applyingUndo || _doc == null)
                return;
            if (!force && (DateTime.UtcNow - _lastUndoRecord).TotalMilliseconds < 350 && _undo.Count > 0)
                return;
            _undo.Push(CaptureEditorState());
            while (_undo.Count > 100)
            {
                // Stack has no trim API; 100+ edits is already more than enough for this lightweight editor.
                break;
            }

            _redo.Clear();
            _lastUndoRecord = DateTime.UtcNow;
        }

        private EditorState CaptureEditorState()
        {
            EditorState state = new EditorState();
            state.Panes = new PaneEditState[_doc.Panes.Count];
            for (int i = 0; i < _doc.Panes.Count; i++)
            {
                BrlytPaneInfo p = _doc.Panes[i];
                PaneEditState ps = new PaneEditState();
                ps.Name = p.Name;
                ps.Flags = p.Flags;
                ps.Origin = p.Origin;
                ps.Alpha = p.Alpha;
                ps.X = p.X;
                ps.Y = p.Y;
                ps.Z = p.Z;
                ps.RotX = p.RotX;
                ps.RotY = p.RotY;
                ps.RotZ = p.RotZ;
                ps.ScaleX = p.ScaleX;
                ps.ScaleY = p.ScaleY;
                ps.Width = p.Width;
                ps.Height = p.Height;
                ps.MaterialId = p.MaterialId;
                ps.FontId = p.FontId;
                ps.TextOrigin = p.TextOrigin;
                ps.LineAlignment = p.LineAlignment;
                ps.FontWidth = p.FontWidth;
                ps.FontHeight = p.FontHeight;
                ps.CharSize = p.CharSize;
                ps.LineSize = p.LineSize;
                state.Panes[i] = ps;
            }

            state.Materials = new MaterialEditState[_doc.Materials.Count];
            for (int i = 0; i < _doc.Materials.Count; i++)
            {
                BrlytMaterialInfo m = _doc.Materials[i];
                MaterialEditState ms = new MaterialEditState();
                ms.Name = m.Name;
                ms.TextureIds = new ushort[m.Bindings.Count];
                for (int b = 0; b < m.Bindings.Count; b++)
                    ms.TextureIds[b] = m.Bindings[b].TextureId;
                state.Materials[i] = ms;
            }

            return state;
        }

        private void ApplyEditorState(EditorState state)
        {
            if (state == null || _doc == null)
                return;
            BrlytPaneInfo keepPane = _selectedPane;
            BrlytMaterialInfo keepMaterial = _selectedMaterial;
            _applyingUndo = true;
            _updating = true;
            try
            {
                int count = Math.Min(state.Panes.Length, _doc.Panes.Count);
                for (int i = 0; i < count; i++)
                {
                    BrlytPaneInfo p = _doc.Panes[i];
                    PaneEditState ps = state.Panes[i];
                    p.Name = ps.Name;
                    p.Flags = ps.Flags;
                    p.Origin = ps.Origin;
                    p.Alpha = ps.Alpha;
                    p.X = ps.X;
                    p.Y = ps.Y;
                    p.Z = ps.Z;
                    p.RotX = ps.RotX;
                    p.RotY = ps.RotY;
                    p.RotZ = ps.RotZ;
                    p.ScaleX = ps.ScaleX;
                    p.ScaleY = ps.ScaleY;
                    p.Width = ps.Width;
                    p.Height = ps.Height;
                    p.MaterialId = ps.MaterialId;
                    p.FontId = ps.FontId;
                    p.TextOrigin = ps.TextOrigin;
                    p.LineAlignment = ps.LineAlignment;
                    p.FontWidth = ps.FontWidth;
                    p.FontHeight = ps.FontHeight;
                    p.CharSize = ps.CharSize;
                    p.LineSize = ps.LineSize;
                }

                count = Math.Min(state.Materials.Length, _doc.Materials.Count);
                for (int i = 0; i < count; i++)
                {
                    BrlytMaterialInfo m = _doc.Materials[i];
                    MaterialEditState ms = state.Materials[i];
                    m.Name = ms.Name;
                    int bc = Math.Min(ms.TextureIds.Length, m.Bindings.Count);
                    for (int b = 0; b < bc; b++)
                    {
                        m.Bindings[b].TextureId = ms.TextureIds[b];
                        m.Bindings[b].TextureName = ms.TextureIds[b] < _doc.Textures.Count ? _doc.Textures[ms.TextureIds[b]] : "";
                    }
                }
            }
            finally
            {
                _updating = false;
                _applyingUndo = false;
            }

            RebuildTree();
            if (keepPane != null)
                SelectPaneInTree(keepPane);
            else if (keepMaterial != null)
            {
                TreeNode n = FindNodeByTag(_tree.Nodes, keepMaterial);
                if (n != null)
                    _tree.SelectedNode = n;
            }

            DrawPreview();
            _dirty = true;
        }

        private void UndoEdit()
        {
            if (_undo.Count == 0)
            {
                _status.Text = L.T("Nichts zum Rückgängigmachen", "Nothing to undo");
                return;
            }

            _redo.Push(CaptureEditorState());
            ApplyEditorState(_undo.Pop());
            _status.Text = L.T("Rückgängig", "Undo");
        }

        private void RedoEdit()
        {
            if (_redo.Count == 0)
            {
                _status.Text = L.T("Nichts zum Wiederholen", "Nothing to redo");
                return;
            }

            _undo.Push(CaptureEditorState());
            ApplyEditorState(_redo.Pop());
            _status.Text = L.T("Wiederholt", "Redo");
        }

        private void CenterSelectedPane()
        {
            if (_selectedPane == null)
                return;
            _x.Value = 0;
            _y.Value = 0;
        }

        private void ResetScale()
        {
            if (_selectedPane == null)
                return;
            _sx.Value = 1;
            _sy.Value = 1;
        }

        private void Save()
        {
            try
            {
                _doc.Save(_path);
                _dirty = false;
                Saved = true;
                _status.Text = L.T("Gespeichert", "Saved");
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Reload()
        {
            if (_dirty && murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen verwerfen?", "Discard unsaved changes?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            LoadDocument();
        }

        private void DrawPreview()
        {
            if (_preview == null)
                return;
            _preview.Document = _doc;
            _preview.Selected = _selectedPane;
            _preview.Grid = _showGrid;
            _preview.Invalidate();
            if (_summary != null)
                _summary.Text = L.T("Ziehen: verschieben • 8 Ziehpunkte: Größe • Umschalt: Proportionen • Mausrad: Zoom", "Drag: move • 8 handles: resize • Shift: proportions • Wheel: zoom");
        }

        private void SyncPanePositionControls()
        {
            if (_selectedPane == null || _x == null)
                return;
            _updating = true;
            try
            {
                var controls = new[]
                {
                    _x,
                    _y,
                    _w,
                    _h,
                    _sx,
                    _sy
                };
                var values = new[]
                {
                    _selectedPane.X,
                    _selectedPane.Y,
                    _selectedPane.Width,
                    _selectedPane.Height,
                    _selectedPane.ScaleX,
                    _selectedPane.ScaleY
                };
                for (int i = 0; i < controls.Length; i++)
                    if (controls[i] != null)
                        controls[i].Value = Math.Max(controls[i].Minimum, Math.Min(controls[i].Maximum, (decimal)values[i]));
            }
            finally
            {
                _updating = false;
            }
        }

        private void SelectPaneInTree(BrlytPaneInfo pane)
        {
            TreeNode node = FindNodeByTag(_tree.Nodes, pane);
            if (node != null)
                _tree.SelectedNode = node;
        }

        private TreeNode FindNodeByTag(TreeNodeCollection nodes, object tag)
        {
            foreach (TreeNode node in nodes)
            {
                if (Object.ReferenceEquals(node.Tag, tag))
                    return node;
                TreeNode child = FindNodeByTag(node.Nodes, tag);
                if (child != null)
                    return child;
            }

            return null;
        }

        private Bitmap GetPaneTexture(BrlytPaneInfo pane)
        {
            try
            {
                BrlytMaterialInfo mat = _doc.MaterialForPane(pane);
                if (mat == null || mat.Bindings.Count == 0)
                    return null;
                return GetTextureByName(mat.Bindings[0].TextureName);
            }
            catch
            {
                return null;
            }
        }

        private Bitmap GetTextureByName(string name)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(name))
                    return null;
                Bitmap cached;
                if (_textureCache.TryGetValue(name, out cached))
                    return cached;
                string blytDir = Path.GetDirectoryName(_path);
                if (String.IsNullOrEmpty(blytDir))
                    return null;
                string tplPath = Path.Combine(blytDir, "timg", name);
                if (!File.Exists(tplPath))
                    tplPath = Path.Combine(blytDir, name);
                if (!File.Exists(tplPath))
                {
                    DirectoryInfo parent = Directory.GetParent(blytDir);
                    if (parent != null)
                        tplPath = Path.Combine(parent.FullName, "timg", name);
                }

                if (!File.Exists(tplPath))
                    return null;
                TexturePreviewResult result;
                string error;
                if (!TexturePreview.TryDecode(name, File.ReadAllBytes(tplPath), 0, out result, out error) || result == null || result.Bitmap == null)
                    return null;
                Bitmap copy = new Bitmap(result.Bitmap);
                result.Dispose();
                _textureCache[name] = copy;
                return copy;
            }
            catch
            {
                return null;
            }
        }

        private void ShowHelp()
        {
            murumsWiiModStudio.StudioMessageBox.Show(this, L.T("BRLYT kann direkt visuell bearbeitet werden: Pane in der Vorschau anklicken/ziehen, Position/Rotation/Scale/Grösse/Alpha/Origin ändern, Picture-/Window-/Text-Material wechseln, txt1-Font und Textlayout bearbeiten und Material-Texture-Slots neu zuordnen.\r\n\r\nKomplexe TEV-, Blend-, UV- und unbekannte Daten werden weiterhin bytegenau beibehalten. So bleiben Nintendo-spezifische Materialdaten sicher, während die häufigsten Layout-Aufgaben direkt im Studio möglich sind.", "BRLYT can be edited visually: click/drag panes in the preview, edit position/rotation/scale/size/alpha/origin, change picture/window/text materials, edit txt1 font/text layout, and remap material texture slots.\r\n\r\nComplex TEV, blend, UV and unknown data is still preserved byte-for-byte. This keeps Nintendo-specific material data safe while covering the common layout tasks directly in the studio."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DisposeTextureCache()
        {
            foreach (KeyValuePair<string, Bitmap> pair in _textureCache)
                if (pair.Value != null)
                    pair.Value.Dispose();
            _textureCache.Clear();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_dirty)
                return;
            DialogResult r = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Änderungen vor dem Schliessen speichern?", "Save changes before closing?"), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (r == DialogResult.Yes)
            {
                Save();
                if (_dirty)
                    e.Cancel = true;
            }
        }
    }
}
