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
        readonly HudFontSettings numberSettings = new HudFontSettings();
        readonly RaceHudSession session = new RaceHudSession();
        readonly ListBox textures = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true
        };
        readonly PictureBox preview = new PictureBox
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
            Text = "Hide placement-number shadows",
            AutoSize = true
        };
        readonly TextBox output = new TextBox
        {
            Dock = DockStyle.Fill
        };
        readonly Label status = Label(), detail = Label(), sources = Label();
        readonly Button clear = StudioHistorySymbols.Button(new Button(), false, L.T("Diese vorgemerkte Ersetzung rückgängig machen", "Undo this pending replacement"));
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

        public RaceHudForm()
        {
            Text = "MKWii Race HUD Tool — murums Wii Mod Studio";
            Size = new Size(1180, 910);
            MinimumSize = new Size(1100, 860);
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
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 8
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (float height in new[]
            {
                112f,
                82f,
                48f,
                -1f,
                58f,
                38f,
                44f,
                46f
            }

            )
                grid.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent : SizeType.Absolute, height < 0 ? 100 : height));
            Controls.Add(grid);
            grid.Controls.Add(StudioChrome.Header("MKWii Race HUD Tool: pictures and placement shadows", "1  Open your pack's archives     2  Select replacements     3  Save copies to HUD_EDITED"), 0, 0);
            grid.RowStyles[1].SizeType = SizeType.AutoSize;
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            bar.Controls.Add(Button("Open Race archive…", Open, "Open your language archive, e.g. Race_E.szs. Race.szs and available RaceAssets.szs beside it are loaded automatically."));
            bar.Controls.Add(Button("Add archive…", Add, "Add Race.szs, a language archive or Retro Rewind RaceAssets.szs without losing current selections."));
            import = Button("Import matching pictures…", Match, "Scan a picture folder and its subfolders. Exact filename matches become pending replacements; no archive is saved yet.");
            bar.Controls.Add(import);
            category.Items.AddRange(new object[] { "Placement numbers", "Timer / laps / score", "Items / minimap", "Pending replacements", "All textures", "Countdown / start / finish", "Player names / warnings", "Results", "Input viewer", "Minimap / icons", "Speedometer" });
            category.SelectedIndex = 0;
            category.SelectedIndexChanged += delegate
            {
                RefreshList();
            };
            bar.Controls.Add(category);
            mapColours = Button("Map colours…", EditMapColours, "Edit the minimap material colour, opacity and four-corner gradient with a sample preview.");
            bar.Controls.Add(mapColours);
            moveHud = Button(L.T("HUD visuell verschieben…", "Move HUD visually…"), EditLayouts, "Drag the whole selected HUD layout or individual elements. Apply here, then save edited archives below.");
            bar.Controls.Add(moveHud);
            bar.Controls.Add(Button("Preview category…", delegate
            {
                using (var d = new HudOverviewForm(session.Textures(category.SelectedIndex)))
                    d.ShowDialog(this);
            }, "Review current images together, including pending replacements and separate input states. Not a game layout simulation."));
            grid.Controls.Add(bar, 0, 1);
            grid.Controls.Add(sources, 0, 2);
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Width = 1100,
                SplitterDistance = 410
            };
            split.Panel1.Controls.Add(textures);
            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            right.Controls.Add(preview, 0, 0);
            right.Controls.Add(detail, 0, 1);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };
            choose = Button("Replace selected picture…", Choose, "Choose one PNG/JPG/BMP. The preview shows the converted result at the original texture dimensions.");
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
            numberFont = Button("Number font…", GenerateNumber, "Create a digit or separator from a TTF with fill, outline and hinting. For timer, lap and score textures. Choose one entry, matching filenames or the complete number set, then review.");
            actions.Controls.Add(numberFont);
            colours = Button("Colours…", Recolour, "Change dark/base and light/outline colours of this texture. Preview retains alpha, dimensions and format. Pressed and released states can be edited separately.");
            actions.Controls.Add(colours);
            right.Controls.Add(actions, 0, 2);
            split.Panel2.Controls.Add(right);
            grid.Controls.Add(split, 0, 3);
            textures.SelectedIndexChanged += delegate
            {
                ShowPreview();
            };
            var explanation = Label();
            explanation.Text = "Import matches names such as tt_position_no_st_64x64_01.tpl-0.png. Review the ● entries under Pending replacements.\nPictures stretch to the existing texture size. Use transparent PNGs for cut-out numbers. The preview does not render game animations.";
            grid.Controls.Add(explanation, 0, 4);
            var options = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };
            options.Controls.Add(shadows);
            options.Controls.Add(new Label { AutoSize = true, UseMnemonic = false, Text = "Unchecked = keep source shadows. Does not remove shadows painted into images." });
            shadows.CheckedChanged += delegate
            {
                dirty = true;
                UpdateState();
            };
            grid.Controls.Add(options, 0, 5);
            var export = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4
            };
            export.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            export.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            export.Controls.Add(new Label { Text = "Save copies to", AutoSize = true }, 0, 0);
            export.Controls.Add(output, 1, 0);
            export.Controls.Add(Button("Browse…", delegate
            {
                using (var d = new FolderPickerDialog
                {
                    Description = "Choose where edited archive copies will be saved",
                    SelectedPath = output.Text
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                        output.Text = d.SelectedPath;
            }, "Choose a separate output folder using Explorer navigation."), 2, 0);
            save = Button("Save edited archives", Save, "Write only archives with selected changes, using their original filenames. Sources remain unchanged.");
            export.Controls.Add(save, 3, 0);
            grid.Controls.Add(status, 0, 6);
            grid.Controls.Add(export, 0, 7);
            DarkTheme.Apply(this);
            StudioUx.Attach(this);
            StudioUx.SetHelp(category, "Filter all loaded archives. Input viewer lists textures referenced by the controller-overlay layouts, including pressed and released states. Pending replacements shows the next export.");
            StudioUx.SetHelp(output, "Edited copies are saved here under their original archive names. The source folder cannot be overwritten.");
            StudioUx.SetHelp(shadows, "Checked: make position_sha transparent in the single and multiplayer layouts. Unchecked: preserve the opened source.");
            save.BackColor = DarkTheme.Accent;
            save.ForeColor = Color.White;
            save.FlatStyle = FlatStyle.Flat;
            save.UseVisualStyleBackColor = false;
            UpdateState();
            RefreshList();
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Close without saving the current selection changes?", "MKWii Race HUD Tool", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                UseWaitCursor = true;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    status.Text = "Action failed; source archives unchanged.";
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, "MKWii Race HUD Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            using (var d = new OpenFileDialog
            {
                Filter = "Race archives (*.szs)|*.szs"
            }

            )
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Discard pending changes and open another pack?", "MKWii Race HUD Tool", MessageBoxButtons.YesNo) != DialogResult.Yes)
                        return;
                    session.Open(d.FileName);
                    output.Text = Path.Combine(Path.GetDirectoryName(d.FileName), "HUD_EDITED");
                    shadows.Checked = false;
                    dirty = false;
                    RefreshList();
                    UpdateState();
                }
        }

        void Add()
        {
            using (var d = new OpenFileDialog
            {
                Filter = "Race archives (*.szs)|*.szs",
                InitialDirectory = session.Archives.Count > 0 ? Path.GetDirectoryName(session.Archives[0].Source) : ""
            }

            )
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    session.Add(d.FileName);
                    if (output.Text.Length == 0)
                        output.Text = Path.Combine(Path.GetDirectoryName(d.FileName), "HUD_EDITED");
                    RefreshList();
                    UpdateState();
                }
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
                            foreach (var p in a.Pictures)
                                using (var b = TplTextureEditor.LoadSourceBitmap(p.Value))
                                    TplTextureEditor.ReplaceFirstImage(a.Files[p.Key].Data, b, true);
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

        void RefreshList()
        {
            var selected = Selected;
            textures.BeginUpdate();
            try
            {
                textures.Items.Clear();
                foreach (var t in session.Textures(category.SelectedIndex))
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
                    detail.Text = category.SelectedIndex == 8 ? "No Input Viewer textures loaded. Use Add archive to open Retro Rewind Assets/RaceAssets.szs. Both standard and Nunchuk layouts are supported." : category.SelectedIndex == 2 ? "Items and minimap are normally in Race.szs. Use Add archive to load Race.szs from the same custom pack." : category.SelectedIndex == 3 ? "No pending picture replacements. Choose a texture in another category or import a picture folder." : "No textures in this category. Add your language's Race archive or select All textures.";
                }
            }
            finally
            {
                textures.EndUpdate();
            }

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
            colours.Enabled = choose.Enabled;
            mapColours.Enabled = session.Archives.Any(a => a.Files.Keys.Any(HudMapColours.IsLayout));
            sources.Text = loaded ? "Loaded: " + string.Join(" + ", session.Archives.Select(a => Path.GetFileName(a.Source)).ToArray()) + "   •   " + session.SelectedCount + " resource changes\n" + Path.GetDirectoryName(session.Archives[0].Source) : "Open Race_E.szs (or your game's language archive) for numbers. Race.szs in the same folder is added for items and minimap.";
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
                        TplTextureEditor.ReplaceFirstImage(t.Archive.Files[t.Key].Data, b, true);
                    t.Archive.Pictures[t.Key] = d.FileName;
                    t.Archive.Generated.Remove(t.Key);
                    dirty = true;
                    RefreshList();
                }
        }

        void EditLayouts()
        {
            if (session.Archives.Count == 0)
                return;
            var snapshots = session.Archives.Select(a => new StudioArchiveCopy(a.Source, a.Build(false))).ToList();
            using (var d = new GameHudForm(snapshots))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var change in d.Session.Changes)
                    {
                        var target = session.Archives.Single(a => a.Source == change.Archive.Source);
                        target.Generated[change.Key] = (byte[])change.Bytes.Clone();
                        target.Pictures.Remove(change.Key);
                        dirty = true;
                    }

                    RefreshList();
                }
        }

        void EditMapColours()
        {
            var layouts = session.Archives.SelectMany(a => a.Files.Keys.Where(HudMapColours.IsLayout).Select(k => new HudTexture { Archive = a, Key = k })).ToList();
            using (var d = new HudMapColourForm(layouts))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var change in d.Results)
                        change.Key.Archive.Generated[change.Key.Key] = change.Value;
                    dirty |= d.Results.Count > 0;
                    RefreshList();
                }
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
            using (var dialog = new HudTextureColorForm(t.Key, data))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    t.Archive.Generated[t.Key] = dialog.Result;
                    t.Archive.Pictures.Remove(t.Key);
                    dirty = true;
                    RefreshList();
                }
        }

        void GenerateNumber()
        {
            var t = Selected;
            if (t == null)
                return;
            using (var dialog = new HudNumberFontForm(t.Key, t.Archive.Files[t.Key].Data, numberSettings, session.Textures(4), t))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var change in dialog.Results)
                    {
                        change.Key.Archive.Generated[change.Key.Key] = change.Value;
                        change.Key.Archive.Pictures.Remove(change.Key.Key);
                    }

                    dirty = true;
                    RefreshList();
                }
        }

        void Save()
        {
            var paths = session.Save(output.Text, shadows.Checked);
            dirty = false;
            status.Text = "Saved " + paths.Count + " edited archive(s) to " + output.Text + ". Back up your pack files before copying these results into it.";
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
