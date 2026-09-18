using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class GameHudForm : StudioToolForm
    {
        internal readonly HudLayoutSession Session = new HudLayoutSession();
        readonly ComboBox archiveChoice = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 230
        };
        bool loadingArchives;
        readonly bool embedded;
        readonly ComboBox layouts = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            DropDownWidth = 850
        };
        readonly ComboBox placementChoice = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            DropDownWidth = 850
        }, aspectChoice = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        bool loadingPlacement;
        BrctrPlacement placement;
        BrlytPaneInfo placementPane;
        readonly TextBox search = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 30,
            AcceptsReturn = false
        };
        readonly TreeView tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false
        };
        readonly HudLayoutCanvas canvas = new HudLayoutCanvas();
        readonly FlowLayoutPanel properties = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        readonly Dictionary<BrlytPaneInfo, Bitmap> cache = new Dictionary<BrlytPaneInfo, Bitmap>();
        readonly List<NumericUpDown> values = new List<NumericUpDown>();
        readonly HashSet<string> animationNames = new HashSet<string>();
        readonly CheckBox visible = new CheckBox
        {
            AutoSize = true,
            Text = L.T("Sichtbar", "Visible")
        };
        readonly Label selectedLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(220, 0),
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };
        readonly Label info = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(220, 0)
        };
        Button tint, replace, recolour, undo, redo, save, restore;
        CheckBox advanced;
        readonly CheckBox moveWhole = new CheckBox
        {
            AutoSize = true
        };
        bool syncing, unexported;
        HudLayoutResource current;
        BrlytDocument doc;
        BrlytPaneInfo selected;
        public GameHudForm() : this(null)
        {
        }

        internal GameHudForm(IEnumerable<StudioArchiveCopy> archives) : base(archives == null ? "MKWii Game HUD Tool" : "MKWii Race HUD Tool – Layout", L.T("Layout auswählen • Elemente auf der Arbeitsfläche verschieben • Farben und Texturen bearbeiten", "Choose a layout • Drag elements on the canvas • Edit colours and textures"))
        {
            embedded = archives != null;
            Size = new Size(1380, 900);
            MinimumSize = new Size(1120, 760);
            Status.Padding = new Padding(8, 2, 8, 2);
            Status.AutoEllipsis = false;
            if (!embedded)
            {
                Action(L.T("Archiv öffnen…", "Open archive…"), "MenuSingle.szs, MenuMulti.szs, Title.szs, Common.szs…", delegate
                {
                    OpenArchive(false);
                });
                Action(L.T("Archiv hinzufügen…", "Add archive…"), "Add archives with shared textures or language resources.", delegate
                {
                    OpenArchive(true);
                });
            }

            archiveChoice.AccessibleName = L.T("Layout-Archiv auswählen", "Choose layout archive");
            archiveChoice.SelectedIndexChanged += delegate
            {
                if (!loadingArchives)
                {
                    search.Clear();
                    RefreshLayouts();
                }
            };
            undo = StudioHistorySymbols.Button(Action(StudioHistorySymbols.Undo, "", delegate
            {
                History(false);
            }), false, L.T("Rückgängig (Strg+Z)", "Undo (Ctrl+Z)"));
            redo = StudioHistorySymbols.Button(Action(StudioHistorySymbols.Redo, "", delegate
            {
                History(true);
            }), true, L.T("Wiederholen (Strg+Y)", "Redo (Ctrl+Y)"));
            Action(L.T("Ansicht einpassen", "Fit view"), "Reset canvas zoom and pan. Mouse wheel zooms; hold the middle mouse button to pan.", delegate
            {
                canvas.ResetView();
            });
            Action(L.T("Referenzbild…", "Reference image…"), "Optional screenshot behind this layout. Visual reference only; never exported.", ReferenceImage);
            Action(L.T("Referenz entfernen", "Clear reference"), "Remove the reference screenshot.", delegate
            {
                if (canvas.Reference != null)
                    canvas.Reference.Dispose();
                canvas.Reference = null;
                canvas.Invalidate();
            });
            moveWhole.Text = embedded ? L.T("Ganzes HUD-Layout verschieben", "Move whole HUD layout") : L.T("Ganzes Layout verschieben", "Move whole layout");
            moveWhole.Checked = embedded;
            moveWhole.Margin = new Padding(8, 14, 4, 0);
            moveWhole.CheckedChanged += delegate
            {
                canvas.MoveWhole = moveWhole.Checked;
                Select(null);
                tree.SelectedNode = null;
                canvas.Invalidate();
            };
            canvas.MoveWhole = embedded;
            var grid = new CheckBox
            {
                Text = L.T("Raster", "Grid"),
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 14, 4, 0)
            };
            grid.CheckedChanged += delegate
            {
                canvas.Grid = grid.Checked;
                canvas.Invalidate();
            };
            Actions.Controls.Add(grid);
            var snap = new CheckBox
            {
                Text = L.T("Einrasten (5)", "Snap (5)"),
                AutoSize = true,
                Margin = new Padding(8, 14, 4, 0)
            };
            snap.CheckedChanged += delegate
            {
                canvas.Snap = snap.Checked;
            };
            Actions.Controls.Add(snap);
            var outlines = new CheckBox
            {
                Text = L.T("Elementrahmen", "Element outlines"),
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 14, 4, 0)
            };
            outlines.CheckedChanged += delegate
            {
                canvas.Outlines = outlines.Checked;
                canvas.Invalidate();
            };
            Actions.Controls.Add(outlines);
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Body.Controls.Add(root);
            var archiveHeader = FieldHeader(L.T("Layout-Archiv auswählen", "Choose layout archive"), archiveChoice);
            root.Controls.Add(archiveHeader, 0, 0);
            root.SetColumnSpan(archiveHeader, 3);
            root.Controls.Add(FieldHeader(L.T("1  Layouts suchen", "1  Search layouts"), search), 0, 1);
            root.Controls.Add(FieldHeader(L.T("2  Layout auswählen", "2  Choose a layout"), layouts), 1, 1);
            root.Controls.Add(FieldHeader(L.T("3  Verschieben", "3  Move"), moveWhole), 2, 1);
            root.Controls.Add(tree, 0, 3);
            root.Controls.Add(canvas, 1, 3);
            root.Controls.Add(properties, 2, 3);
            StudioUx.SetHelp(aspectChoice, L.T("Bildformat der HUD-Positionen. Ändert nicht die Renderauflösung des Spiels.", "Aspect ratio for HUD positions. Does not change game render resolution."));
            aspectChoice.Items.AddRange(new object[] { "16:9", "4:3" });
            aspectChoice.SelectedIndex = 0;
            root.Controls.Add(FieldHeader(L.T("Bildformat", "Aspect ratio"), aspectChoice), 0, 2);
            root.Controls.Add(FieldHeader(L.T("Bildschirmposition / Spieleransicht aus BRCTR", "Screen placement / player view from BRCTR"), placementChoice), 1, 2);
            root.SetColumnSpan(root.GetControlFromPosition(1, 2), 2);
            placementChoice.SelectedIndexChanged += delegate
            {
                if (!loadingPlacement)
                    ApplyPlacement();
            };
            aspectChoice.SelectedIndexChanged += delegate
            {
                if (!loadingPlacement)
                {
                    if (placementChoice.SelectedIndex == 0)
                        ApplyPlacement();
                    else
                        LoadPlacements();
                }
            };
            StudioUx.SetHelp(moveWhole, L.T("Zieht die oberste Gruppe mit allen Kindern. Gilt für das gewählte Teillayout, nicht für andere Dateien.", "Drags the top-level group with all children. Affects this component layout, not other files."));
            StudioUx.SetHelp(search, L.T("Layouts nach Dateiname filtern, z. B. button, select, position, map oder inputviewer.", "Filter layout filenames, e.g. button, select, position, map or inputviewer."));
            properties.Controls.Add(selectedLabel);
            AddNumber("X", -100000, 100000, 2);
            AddNumber("Y", -100000, 100000, 2);
            AddNumber(L.T("Breite", "Width"), 0, 100000, 2);
            AddNumber(L.T("Höhe", "Height"), 0, 100000, 2);
            AddNumber("Scale X", -1000, 1000, 3);
            AddNumber("Scale Y", -1000, 1000, 3);
            AddNumber(L.T("Drehung", "Rotation"), -36000, 36000, 2);
            AddNumber(L.T("Deckkraft", "Opacity"), 0, 255, 0);
            properties.Controls.Add(visible);
            advanced = new CheckBox
            {
                AutoSize = true,
                Text = L.T("Skalierung und Drehung anzeigen", "Show scale and rotation"),
                Margin = new Padding(0, 5, 0, 5)
            };
            properties.Controls.Add(advanced);
            for (int i = 4; i <= 6; i++)
                values[i].Parent.Visible = false;
            advanced.CheckedChanged += delegate
            {
                for (int i = 4; i <= 6; i++)
                    values[i].Parent.Visible = advanced.Checked;
            };
            tint = PropertyButton(L.T("Elementfarbe…", "Element tint…"), Tint);
            replace = PropertyButton(L.T("Textur ersetzen…", "Replace texture…"), Replace);
            recolour = PropertyButton(L.T("Texturfarben…", "Texture colours…"), Recolour);
            restore = PropertyButton(L.T("Layout zurücksetzen", "Restore opened layout"), delegate
            {
                if (current == null)
                    return;
                if (selected == placementPane && placement != null)
                {
                    var target = new HudLayoutResource
                    {
                        Archive = placement.Archive,
                        Key = placement.Control
                    };
                    var opened = BrctrPlacements.Read(target.Key, Session.Opened(target)).Single(v => v.Offset == placement.Offset);
                    bool wide = aspectChoice.SelectedIndex == 0;
                    Session.Set(target, BrctrPlacements.Write(placement, target.Bytes, wide, wide ? opened.WideX : opened.X, wide ? opened.WideY : opened.Y, wide ? opened.WideScaleX : opened.ScaleX, wide ? opened.WideScaleY : opened.ScaleY, (byte)Math.Min(255, opened.Opacity)));
                }
                else
                    Session.Restore(current);
                unexported = true;
                LoadLayout();
            });
            properties.Controls.Add(info);
            StudioUx.SetHelp(properties, L.T("Element wählen und ziehen. Ganze Gruppen lassen sich mit ihren Kindern verschieben.", "Select and drag an element. Groups move together with their children."));
            save = ExportAction(embedded ? L.T("In Race HUD übernehmen", "Apply to Race HUD") : L.T("Archivkopien speichern…", "Save archive copies…"), "Apply changes or save separate archive copies.", Export);
            search.KeyPress += delegate (object sender, KeyPressEventArgs e)
            {
                if (e.KeyChar == '\r' || e.KeyChar == '\n')
                    e.Handled = true;
            };
            search.TextChanged += delegate
            {
                RefreshLayouts();
            };
            layouts.SelectedIndexChanged += delegate
            {
                current = layouts.SelectedItem as HudLayoutResource;
                LoadLayout();
            };
            tree.AfterSelect += delegate
            {
                Select(tree.SelectedNode == null ? null : tree.SelectedNode.Tag as BrlytPaneInfo);
            };
            visible.CheckedChanged += delegate
            {
                if (!syncing && selected != null)
                {
                    selected.Visible = visible.Checked;
                    Commit();
                }
            };
            canvas.SelectPane = delegate (BrlytPaneInfo p)
            {
                Select(p);
                SelectTree(tree.Nodes, p);
            };
            canvas.CommitMove = Commit;
            canvas.PositionChanged = SyncTransformValues;
            canvas.Texture = PaneImage;
            KeyPreview = true;
            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.Z)
                {
                    History(false);
                    e.SuppressKeyPress = true;
                }

                if (e.Control && e.KeyCode == Keys.Y)
                {
                    History(true);
                    e.SuppressKeyPress = true;
                }
            };
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (unexported && Session.Changes.Any() && DialogResult != DialogResult.OK && StudioMessageBox.Show(this, L.T("Ungespeicherte HUD-Änderungen verwerfen?", "Discard unsaved HUD changes?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    e.Cancel = true;
            };
            if (archives != null)
                foreach (var a in archives)
                    Session.Add(a);
            Finish();
            search.Height = layouts.Height;
            RefreshLayouts();
            Sync();
        }

        static Control FieldHeader(string caption, Control field)
        {
            var box = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(3, 0, 6, 8)
            };
            box.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            box.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
            field.Dock = DockStyle.Top;
            field.Margin = Padding.Empty;
            field.AccessibleName = caption;
            box.Controls.Add(field, 0, 1);
            EventHandler fit = delegate
            {
                box.MinimumSize = new Size(0, 24 + field.Height);
            };
            field.SizeChanged += fit;
            fit(null, EventArgs.Empty);
            return box;
        }

        Button PropertyButton(string text, Action action)
        {
            var b = new Button
            {
                Text = text,
                Width = 220,
                Height = 34,
                Margin = new Padding(0, 6, 0, 0)
            };
            b.Click += delegate
            {
                Guard(action);
            };
            properties.Controls.Add(b);
            return b;
        }

        void AddNumber(string label, decimal min, decimal max, int places)
        {
            var row = new Panel
            {
                Width = 220,
                Height = 27,
                Margin = new Padding(0, 2, 0, 0)
            };
            row.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(0, 5) });
            var n = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = places,
                Increment = places == 3 ? .05m : 1,
                Width = 116,
                Location = new Point(102, 0)
            };
            values.Add(n);
            row.Controls.Add(n);
            properties.Controls.Add(row);
            n.ValueChanged += delegate
            {
                if (syncing || selected == null)
                    return;
                var ns = values.ToArray();
                selected.X = (float)ns[0].Value;
                selected.Y = (float)ns[1].Value;
                selected.Width = (float)ns[2].Value;
                selected.Height = (float)ns[3].Value;
                selected.ScaleX = (float)ns[4].Value;
                selected.ScaleY = (float)ns[5].Value;
                selected.RotZ = (float)ns[6].Value;
                selected.Alpha = (byte)ns[7].Value;
                Commit();
            };
        }

        void OpenArchive(bool add)
        {
            var path = OpenPath("SZS / U8 archives|*.szs;*.arc;*.u8");
            if (path == null)
                return;
            var archive = new StudioArchiveCopy(path);
            if (!archive.Files.Keys.Any(k => k.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(L.T("Dieses Archiv enthält keine BRLYT-Layouts.", "This archive contains no BRLYT layouts."));
            if (!add && Session.Archives.Count > 0)
            {
                if (unexported && StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen verwerfen und ein anderes Archiv öffnen?", "Discard unsaved changes and open another archive?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;
                Session.Clear();
                unexported = false;
            }

            Session.Add(archive);
            search.Clear();
            RefreshLayouts();
        }

        void RefreshLayouts()
        {
            string archive = archiveChoice.SelectedItem as string;
            loadingArchives = true;
            try
            {
                archiveChoice.Items.Clear();
                archiveChoice.Items.Add(L.T("Alle Archive", "All archives"));
                foreach (var a in Session.Archives)
                    archiveChoice.Items.Add(Path.GetFileName(a.Source));
                archiveChoice.SelectedIndex = archive != null && archiveChoice.Items.Contains(archive) ? archiveChoice.Items.IndexOf(archive) : 0;
            }
            finally
            {
                loadingArchives = false;
            }

            var old = current;
            layouts.BeginUpdate();
            layouts.Items.Clear();
            foreach (var r in Session.Layouts.Where(r => archiveChoice.SelectedIndex <= 0 || Path.GetFileName(r.Archive.Source) == (string)archiveChoice.SelectedItem).Where(r => r.ToString().IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(r => r.Key))
                layouts.Items.Add(r);
            int index = -1;
            for (int i = 0; i < layouts.Items.Count; i++)
            {
                var r = (HudLayoutResource)layouts.Items[i];
                if (old != null && r.Archive == old.Archive && r.Key == old.Key)
                    index = i;
            }

            layouts.EndUpdate();
            if (layouts.Items.Count > 0)
                layouts.SelectedIndex = index < 0 ? 0 : index;
            else
            {
                current = null;
                LoadLayout();
            }
        }

        void LoadLayout()
        {
            ClearCache();
            selected = null;
            doc = null;
            tree.Nodes.Clear();
            try
            {
                if (current != null)
                {
                    doc = BrlytDocument.FromBytes(current.Bytes);
                    animationNames.Clear();
                    foreach (var entry in current.Archive.Files.Where(f => f.Key.EndsWith(".brlan", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            var animation = BrlanCodec.Parse(entry.Value.Data);
                            if (animation.Pai != null)
                                foreach (var a in animation.Pai.Animations)
                                    animationNames.Add(a.Name);
                        }
                        catch (InvalidDataException)
                        {
                        }
                    }

                    foreach (var p in doc.RootPanes)
                        tree.Nodes.Add(Node(p));
                    tree.ExpandAll();
                }
            }
            catch (Exception ex)
            {
                Status.Text = ex.Message;
            }

            canvas.Document = doc;
            canvas.Selected = null;
            LoadPlacements();
            canvas.ResetView();
            Sync();
        }

        void LoadPlacements()
        {
            var old = placementChoice.SelectedItem as BrctrPlacement;
            loadingPlacement = true;
            try
            {
                placementChoice.Items.Clear();
                placementChoice.Items.Add(L.T("Lokales Layout – ohne Bildschirmposition", "Local layout – no screen placement"));
                if (current != null && doc != null)
                    foreach (var p in BrctrPlacements.Find(current, Session.Archives))
                        placementChoice.Items.Add(p);
                int index = 0;
                for (int i = 1; i < placementChoice.Items.Count; i++)
                {
                    var p = (BrctrPlacement)placementChoice.Items[i];
                    if (old != null && p.Control == old.Control && p.Name == old.Name && p.Archive == old.Archive)
                    {
                        index = i;
                        break;
                    }
                }

                if (index == 0)
                {
                    for (int i = 1; i < placementChoice.Items.Count; i++)
                    {
                        var p = (BrctrPlacement)placementChoice.Items[i];
                        if (p.Name.EndsWith("_1_0"))
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index == 0 && placementChoice.Items.Count > 1)
                        index = 1;
                }

                placementChoice.SelectedIndex = index;
            }
            finally
            {
                loadingPlacement = false;
            }

            ApplyPlacement();
        }

        void ApplyPlacement()
        {
            placement = placementChoice.SelectedItem as BrctrPlacement;
            placementPane = null;
            selected = null;
            canvas.Selected = null;
            tree.Nodes.Clear();
            if (doc != null)
            {
                foreach (var root in doc.RootPanes)
                    root.Parent = null;
                if (placement != null)
                {
                    bool wide = aspectChoice.SelectedIndex == 0;
                    placementPane = new BrlytPaneInfo
                    {
                        Name = L.T("Bildschirmposition", "Screen placement"),
                        Magic = "pan1",
                        Offset = -1,
                        Flags = 3,
                        Origin = 4,
                        Alpha = (byte)Math.Min(255, placement.Opacity),
                        X = wide ? placement.WideX : placement.X,
                        Y = wide ? placement.WideY : placement.Y,
                        ScaleX = wide ? placement.WideScaleX : placement.ScaleX,
                        ScaleY = wide ? placement.WideScaleY : placement.ScaleY
                    };
                    foreach (var root in doc.RootPanes)
                    {
                        root.Parent = placementPane;
                        placementPane.Children.Add(root);
                    }

                    tree.Nodes.Add(Node(placementPane));
                    canvas.ScreenWidth = wide ? 640f * 4 / 3 : 640;
                    canvas.ScreenHeight = 480;
                    canvas.ScreenLabel = L.T("BRCTR-Bildschirmreferenz ", "BRCTR screen reference ") + aspectChoice.Text;
                }
                else
                {
                    foreach (var root in doc.RootPanes)
                        tree.Nodes.Add(Node(root));
                    canvas.ScreenWidth = canvas.ScreenHeight = 0;
                    canvas.ScreenLabel = null;
                }

                tree.ExpandAll();
            }
            else
            {
                canvas.ScreenWidth = canvas.ScreenHeight = 0;
                canvas.ScreenLabel = null;
            }

            Sync();
            canvas.Invalidate();
        }

        TreeNode Node(BrlytPaneInfo p)
        {
            var n = new TreeNode(p.Name + " [" + p.Magic + "]")
            {
                Tag = p
            };
            foreach (var child in p.Children)
                n.Nodes.Add(Node(child));
            return n;
        }

        bool SelectTree(TreeNodeCollection nodes, BrlytPaneInfo p)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Tag == p)
                {
                    tree.SelectedNode = n;
                    return true;
                }

                if (SelectTree(n.Nodes, p))
                    return true;
            }

            return false;
        }

        void Select(BrlytPaneInfo p)
        {
            selected = p;
            canvas.Selected = p;
            Sync();
            canvas.Invalidate();
        }

        void SyncTransformValues()
        {
            bool previous = syncing;
            syncing = true;
            try
            {
                var ns = values.ToArray();
                float[] f = selected == null ? new float[8] : new[]
                {
                    selected.X,
                    selected.Y,
                    selected.Width,
                    selected.Height,
                    selected.ScaleX,
                    selected.ScaleY,
                    selected.RotZ,
                    (float)selected.Alpha
                };
                for (int i = 0; i < ns.Length; i++)
                {
                    ns[i].Value = float.IsNaN(f[i]) || float.IsInfinity(f[i]) ? 0 : Math.Max(ns[i].Minimum, Math.Min(ns[i].Maximum, (decimal)Math.Max(-100000, Math.Min(100000, f[i]))));
                }
            }
            finally
            {
                syncing = previous;
            }
        }

        void Sync()
        {
            syncing = true;
            try
            {
                selectedLabel.Text = selected == null ? L.T("Element auswählen", "Select an element") : selected.Name + (animationNames.Contains(selected.Name) ? L.T(" (Animationsziel)", " (animation target)") : "");
                SyncTransformValues();
                foreach (var n in values)
                    n.Enabled = selected != null;
                if (selected != null && selected.Children.Count > 0)
                {
                    values[2].Enabled = values[3].Enabled = false;
                    advanced.Checked = true;
                }

                if (selected == placementPane && selected != null)
                    values[6].Enabled = false;
                restore.Text = selected != null && selected == placementPane ? L.T("Bildschirmposition zurücksetzen", "Restore screen placement") : L.T("Layout zurücksetzen", "Restore opened layout");
                visible.Enabled = selected != null && selected != placementPane;
                visible.Checked = selected != null && selected.Visible;
                tint.Enabled = selected != null && (selected.Magic == "pic1" || selected.Magic == "txt1");
                var texture = SelectedTexture();
                replace.Enabled = recolour.Enabled = texture != null && TplTextureEditor.CanReplaceImage(texture.Bytes, 0);
                info.Text = L.T("Texturersatz ändert alle Verwendungen derselben Datei.", "Texture replacement affects every use of the same file.");
                info.AccessibleDescription = selected == null ? L.T("Element anklicken oder links auswählen. Gruppen verschieben ihre Kinder mit.", "Click an element or select it on the left. Moving a group moves its children.") : L.T("Statisches Layout. Spielcode und Animationen können Positionen/Farben überschreiben. Texte sind Platzhalter; 3D-Modelle, Materialeffekte und X/Y-Drehung werden nicht gerendert.", "Static layout. Game code and animations can override positions/colours. Text uses placeholders; 3D models, material effects and X/Y rotations are not rendered.") + (texture == null ? "" : "\n\n" + texture.Key + L.T("\nTexturersatz betrifft alle Verwendungen dieser Datei.", "\nReplacing a texture affects every use of this file."));
                undo.Enabled = Session.CanUndo;
                redo.Enabled = Session.CanRedo;
                save.Enabled = Session.Changes.Any();
                Status.Text = current == null ? L.T("Menüarchive wie MenuSingle.szs, Title.szs oder Common.szs öffnen. Suche oben links filtert die Layouts.", "Open menu archives such as MenuSingle.szs, Title.szs or Common.szs. Search at the upper left filters layouts.") : current.ToString() + "\n" + L.T("Ziehen: verschieben • Mausradtaste: Ansicht verschieben • 8 Ziehpunkte: Größe • Umschalt: Proportionen • Änderungen: ", "Drag: move • Middle drag: pan view • 8 handles: resize • Shift: proportions • Changed resources: ") + Session.Changes.Count() + "\n" + (placement == null ? L.T("Keine Bildschirmposition aktiv: lokale Koordinaten. Spielcode/Animationen fehlen.", "No screen placement active: local coordinates. Game code/animations are not simulated.") : L.T("BRCTR-Position aktiv. Spielcode/Animationen können sie zusätzlich verändern.", "BRCTR placement active. Game code/animations may apply additional changes.") + L.T(" Position speichern in: ", " Save placement in: ") + Path.GetFileName(placement.Archive.Source));
            }
            finally
            {
                syncing = false;
            }
        }

        void Commit()
        {
            if (current == null || selected == null)
                return;
            if (selected == placementPane && placement != null)
            {
                var target = new HudLayoutResource
                {
                    Archive = placement.Archive,
                    Key = placement.Control
                };
                Session.Set(target, BrctrPlacements.Write(placement, target.Bytes, aspectChoice.SelectedIndex == 0, selected.X, selected.Y, selected.ScaleX, selected.ScaleY, selected.Alpha));
            }
            else
            {
                doc.ApplyPane(selected);
                Session.Set(current, doc.Data);
            }

            unexported = true;
            ClearCache();
            Sync();
            canvas.Invalidate();
        }

        void History(bool forward)
        {
            var r = forward ? Session.Redo() : Session.Undo();
            if (r == null)
                return;
            unexported = true;
            var name = selected == null ? null : selected.Name;
            if (r.Key.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase))
            {
                loadingArchives = true;
                archiveChoice.SelectedIndex = 0;
                loadingArchives = false;
                search.Clear();
                current = r;
                RefreshLayouts();
            }
            else
                LoadLayout();
            if (doc != null && name != null)
            {
                var p = placementPane != null && placementPane.Name == name ? placementPane : doc.Panes.FirstOrDefault(x => x.Name == name);
                if (p != null)
                    SelectTree(tree.Nodes, p);
            }

            Sync();
        }

        HudLayoutResource SelectedTexture()
        {
            if (doc == null || selected == null || current == null)
                return null;
            var mat = doc.MaterialForPane(selected);
            return mat == null || mat.Bindings.Count == 0 ? null : Session.Texture(current, mat.Bindings[0].TextureName);
        }

        void Tint()
        {
            if (selected == null)
                return;
            using (var d = new ColorDialog
            {
                FullOpen = true,
                Color = Color.White
            }

            )
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    HudLayoutGeometry.Tint(doc, selected, d.Color);
                    Commit();
                }
        }

        void Replace()
        {
            var r = SelectedTexture();
            if (r == null)
                return;
            var path = OpenPath("Pictures|*.png;*.bmp;*.jpg;*.jpeg");
            if (path == null)
                return;
            using (var b = TplTextureEditor.LoadSourceBitmap(path))
                Session.Set(r, TplTextureEditor.ReplaceFirstImage(r.Bytes, b, true));
            unexported = true;
            ClearCache();
            Sync();
            canvas.Invalidate();
        }

        void Recolour()
        {
            var r = SelectedTexture();
            if (r == null)
                return;
            using (var d = new HudTextureColorForm(r.Key, r.Bytes))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    Session.Set(r, d.Result);
                    unexported = true;
                    ClearCache();
                    Sync();
                    canvas.Invalidate();
                }
        }

        void ReferenceImage()
        {
            var path = OpenPath("Pictures|*.png;*.bmp;*.jpg;*.jpeg");
            if (path == null)
                return;
            using (var b = TplTextureEditor.LoadSourceBitmap(path))
            {
                if (canvas.Reference != null)
                    canvas.Reference.Dispose();
                canvas.Reference = new Bitmap(b);
            }

            canvas.Invalidate();
        }

        void Export()
        {
            if (embedded)
            {
                DialogResult = DialogResult.OK;
                unexported = false;
                Close();
                return;
            }

            var folder = Folder(Session.Archives.Count == 0 ? "" : Path.GetDirectoryName(Session.Archives[0].Source));
            if (folder == null)
                return;
            Session.Save(folder);
            unexported = false;
            Status.Text = L.T("Archivkopien gespeichert: ", "Archive copies saved: ") + folder;
        }

        Bitmap PaneImage(BrlytPaneInfo p)
        {
            Bitmap image;
            if (cache.TryGetValue(p, out image))
                return image;
            cache[p] = null;
            if (p.Magic != "pic1" || p.Size < 0x60)
                return null;
            var mat = doc.MaterialForPane(p);
            if (mat == null || mat.Bindings.Count == 0)
                return null;
            var r = Session.Texture(current, mat.Bindings[0].TextureName);
            if (r == null)
                return null;
            TexturePreviewResult decoded;
            string error;
            if (!TexturePreview.TryDecode(r.Key, r.Bytes, 0, out decoded, out error))
                return null;
            using (decoded)
            {
                var src = decoded.Bitmap;
                int w = Math.Max(1, Math.Min(256, (int)Math.Abs(p.Width))), h = Math.Max(1, Math.Min(256, (int)Math.Abs(p.Height)));
                image = new Bitmap(w, h);
                var uv = p.TexCoords.FirstOrDefault();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + .5f) / w, v = (y + .5f) / h;
                        float tu = uv == null ? u : Mix(uv.U0, uv.U1, uv.U2, uv.U3, u, v), tv = uv == null ? v : Mix(uv.V0, uv.V1, uv.V2, uv.V3, u, v);
                        int ix = Math.Max(0, Math.Min(src.Width - 1, (int)(tu * src.Width))), iy = Math.Max(0, Math.Min(src.Height - 1, (int)(tv * src.Height)));
                        Color c = src.GetPixel(ix, iy);
                        int[] factors = new int[4];
                        for (int k = 0; k < 4; k++)
                            factors[k] = (int)Mix(doc.Data[p.Offset + 0x4c + k], doc.Data[p.Offset + 0x50 + k], doc.Data[p.Offset + 0x54 + k], doc.Data[p.Offset + 0x58 + k], u, v);
                        image.SetPixel(x, y, Color.FromArgb(c.A * factors[3] / 255, c.R * factors[0] / 255, c.G * factors[1] / 255, c.B * factors[2] / 255));
                    }
            }

            cache[p] = image;
            return image;
        }

        static float Mix(float a, float b, float c, float d, float x, float y)
        {
            return (a * (1 - x) + b * x) * (1 - y) + (c * (1 - x) + d * x) * y;
        }

        void ClearCache()
        {
            foreach (var b in cache.Values)
                if (b != null)
                    b.Dispose();
            cache.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ClearCache();
            base.Dispose(disposing);
        }
    }
}
