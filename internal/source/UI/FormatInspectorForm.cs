using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class FormatInspectorForm : Form
    {
        private readonly string _path;
        private readonly ResourceInfo _info;
        private TextBox _infoBox;
        private TextBox _hexBox;
        private TextBox _editBox;
        private Button _external;
        private Button _saveEdit;
        private Button _reloadEdit;
        private Label _status;
        private TabControl _tabs;
        private bool _editLoading;
        private bool _editDirty;
        public bool FileModified { get; private set; }

        public FormatInspectorForm(string path)
        {
            _path = path;
            _info = ResourceDetector.Detect(path);
            Text = "murums Wii Mod Studio — " + Path.GetFileName(path);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(800, 560);
            Size = new Size(1040, 740);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.5F);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            LoadData();
            DarkTheme.Apply(this);
            FormClosing += OnClosing;
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            Controls.Add(root);
            Panel header = murumsWiiModStudio.StudioChrome.Header(_info.DisplayName, Path.GetFileName(_path));
            root.Controls.Add(header, 0, 0);
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            DarkTheme.StyleTabs(_tabs);
            TabPage infoTab = new TabPage(L.T("Übersicht", "Overview"));
            _infoBox = MakeBox(false, true);
            _infoBox.Dock = DockStyle.Fill;
            infoTab.Controls.Add(_infoBox);
            _tabs.TabPages.Add(infoTab);
            if (_info.Kind == ResourceKind.Bmg)
                _tabs.TabPages.Add(BuildBmgEditTab());
            var previewTab = new TabPage("Preview");
            var resourcePreview = new ResourcePreviewPanel();
            previewTab.Controls.Add(resourcePreview);
            resourcePreview.ShowResource(Path.GetFileName(_path), File.ReadAllBytes(_path));
            _tabs.TabPages.Add(previewTab);
            TabPage hexTab = new TabPage("Raw / Hex");
            _hexBox = MakeBox(true, true);
            _hexBox.Dock = DockStyle.Fill;
            _hexBox.Font = new Font("Consolas", 9.5F);
            hexTab.Controls.Add(_hexBox);
            _tabs.TabPages.Add(hexTab);
            root.Controls.Add(_tabs, 0, 1);
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = DarkTheme.Panel;
            _external = new Button();
            _external.AutoSize = true;
            _external.Height = 32;
            _external.Left = 12;
            _external.Top = 9;
            _external.FlatStyle = FlatStyle.Flat;
            _external.FlatAppearance.BorderColor = DarkTheme.Border;
            _external.BackColor = DarkTheme.Panel2;
            _external.ForeColor = DarkTheme.Fore;
            ToolDescriptor tool = ToolchainManager.Recommended(_info.Kind);
            if (tool == null)
                _external.Text = L.T("Kein Backend zugeordnet", "No backend mapped");
            else if (_info.Kind == ResourceKind.Kmp || _info.Kind == ResourceKind.Kcl)
                _external.Text = L.T("Mit ", "Validate with ") + tool.DisplayName + L.T(" prüfen", "");
            else if (_info.Kind == ResourceKind.Bmg)
                _external.Text = L.T("BMG-Text neu dekodieren", "Reload decoded BMG text");
            else
                _external.Text = L.T("In ", "Open in ") + tool.DisplayName;
            _external.Enabled = tool != null;
            _external.Click += delegate
            {
                LaunchExternal();
            };
            bottom.Controls.Add(_external);
            _status = new Label();
            _status.AutoEllipsis = true;
            _status.ForeColor = DarkTheme.Muted;
            _status.Left = 270;
            _status.Top = 16;
            _status.Width = 720;
            _status.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            bottom.Controls.Add(_status);
            bottom.Layout += delegate
            {
                _status.Left = _external.Right + 12;
                _status.Width = Math.Max(0, bottom.ClientSize.Width - _status.Left - 12);
            };
            root.Controls.Add(bottom, 0, 2);
        }

        private TabPage BuildBmgEditTab()
        {
            TabPage tab = new TabPage(L.T("Bearbeiten", "Edit"));
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.Padding = new Padding(8, 7, 8, 5);
            bar.WrapContents = false;
            bar.AutoScroll = true;
            bar.BackColor = DarkTheme.Panel;
            _saveEdit = MakeEditButton(L.T("BMG speichern", "Save BMG"), delegate
            {
                SaveBmg();
            });
            _reloadEdit = MakeEditButton(L.T("Neu dekodieren", "Reload decoded text"), delegate
            {
                LoadBmgEditor();
            });
            bar.Controls.Add(_saveEdit);
            bar.Controls.Add(_reloadEdit);
            Label hint = new Label();
            hint.AutoSize = true;
            hint.Margin = new Padding(16, 7, 0, 0);
            hint.ForeColor = DarkTheme.Muted;
            hint.Text = L.T("Nachrichten direkt ändern. #BMG und Steuersequenzen beibehalten.", "Edit messages directly. Keep #BMG and control sequences intact.");
            bar.Controls.Add(hint);
            root.Controls.Add(bar, 0, 0);
            _editBox = MakeBox(true, false);
            _editBox.Dock = DockStyle.Fill;
            _editBox.Font = new Font("Consolas", 10.5F);
            _editBox.AcceptsTab = true;
            _editBox.WordWrap = false;
            _editBox.TextChanged += delegate
            {
                if (!_editLoading)
                {
                    _editDirty = true;
                    UpdateEditButtons();
                }
            };
            root.Controls.Add(_editBox, 0, 1);
            tab.Controls.Add(root);
            return tab;
        }

        private Button MakeEditButton(string text, EventHandler click)
        {
            Button b = new Button();
            b.AutoSize = true;
            b.Height = 31;
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            b.Margin = new Padding(2, 0, 6, 0);
            b.Click += click;
            return b;
        }

        private TextBox MakeBox(bool mono, bool readOnly)
        {
            TextBox b = new TextBox();
            b.Multiline = true;
            b.ReadOnly = readOnly;
            b.ScrollBars = ScrollBars.Both;
            b.BorderStyle = BorderStyle.None;
            b.BackColor = DarkTheme.Panel;
            b.ForeColor = DarkTheme.Fore;
            b.WordWrap = !mono;
            return b;
        }

        private void LoadData()
        {
            try
            {
                _infoBox.Text = FormatInspector.Inspect(_path, _info) + Environment.NewLine + Environment.NewLine + L.T("Hinweis: Für unterstützte Formate stehen direkte Editoren als eigene Tabs/Fenster bereit. Komplexe Formate können zusätzlich etablierte Spezialwerkzeuge als Backend verwenden.", "Note: supported formats have direct editors in dedicated tabs/windows. Established specialist tools can additionally be used as backends for complex formats.");
                _hexBox.Text = FormatInspector.HexPreview(File.ReadAllBytes(_path), 64 * 1024);
                ToolDescriptor tool = ToolchainManager.Recommended(_info.Kind);
                if (tool != null)
                {
                    string exe = ToolchainManager.Find(tool);
                    _status.Text = exe == null ? L.T("Backend nicht gefunden — Tools > Toolchain > Ausgewähltes Tool suchen...", "Backend not found — Tools > Toolchain > Locate selected...") : L.T("Backend gefunden: ", "Backend found: ") + exe;
                }

                if (_info.Kind == ResourceKind.Bmg)
                    LoadBmgEditor();
            }
            catch (Exception ex)
            {
                _infoBox.Text = ex.ToString();
            }
        }

        private void LoadBmgEditor()
        {
            if (_editBox == null)
                return;
            if (_editDirty)
            {
                DialogResult choice = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Änderungen vor dem erneuten Laden speichern?", "Save changes before reloading?"), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel)
                    return;
                if (choice == DialogResult.Yes)
                {
                    SaveBmg();
                    if (_editDirty)
                        return;
                }
            }

            ToolDescriptor tool = ToolchainManager.FindById("wbmgt");
            string output, error;
            _editLoading = true;
            try
            {
                if (tool == null || ToolchainManager.Find(tool) == null)
                {
                    _editBox.Text = L.T("Wiimms BMG Tool (wbmgt) wurde nicht gefunden. Öffne Tools > Toolchain und wähle wbmgt.exe aus.", "Wiimms BMG Tool (wbmgt) was not found. Open Tools > Toolchain and locate wbmgt.exe.");
                    _editBox.ReadOnly = true;
                    _editDirty = false;
                    UpdateEditButtons();
                    return;
                }

                if (!ToolchainManager.RunCapture(tool, "CAT " + ToolchainManager.QuoteArgument(_path), out output, out error))
                {
                    _editBox.Text = error ?? output ?? "BMG decode failed.";
                    _editBox.ReadOnly = true;
                    _editDirty = false;
                    UpdateEditButtons();
                    return;
                }

                _editBox.ReadOnly = false;
                _editBox.Text = output ?? "";
                _editDirty = false;
                _status.Text = L.T("BMG dekodiert — im Tab Bearbeiten direkt editierbar.", "BMG decoded — edit it directly in the Edit tab.");
                UpdateEditButtons();
            }
            finally
            {
                _editLoading = false;
            }
        }

        private void SaveBmg()
        {
            if (_editBox == null || _editBox.ReadOnly)
                return;
            string text = _editBox.Text ?? "";
            if (!text.TrimStart().StartsWith("#BMG", StringComparison.Ordinal))
            {
                if (murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Der Text beginnt nicht mit #BMG. Ohne BMG-Kopf kann wbmgt die Datei anders interpretieren. Trotzdem versuchen?", "The text does not start with #BMG. Without the BMG header wbmgt may interpret it differently. Try anyway?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            ToolDescriptor tool = ToolchainManager.FindById("wbmgt");
            if (tool == null || ToolchainManager.Find(tool) == null)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("wbmgt wurde nicht gefunden. Öffne Tools > Toolchain.", "wbmgt was not found. Open Tools > Toolchain."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string temp = Path.Combine(Path.GetTempPath(), "murums_bmg_" + Guid.NewGuid().ToString("N") + ".txt");
            string encodedPath = temp + ".bmg";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                string output, error;
                string args = "ENCODE --overwrite --dest " + ToolchainManager.QuoteArgument(encodedPath) + " " + ToolchainManager.QuoteArgument(temp);
                if (!ToolchainManager.RunCapture(tool, args, out output, out error))
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, (error ?? "") + Environment.NewLine + (output ?? ""), "wbmgt", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                byte[] encoded = File.ReadAllBytes(encodedPath);
                if (encoded.Length < 32 || Encoding.ASCII.GetString(encoded, 0, 8) != "MESGbmg1")
                    throw new InvalidDataException("The encoder did not produce a valid BMG file.");
                BackupManager.WriteAllBytesSafely(_path, encoded);
                FileModified = true;
                _editDirty = false;
                _status.Text = L.T("BMG gespeichert und neu encodiert.", "BMG saved and re-encoded.");
                _infoBox.Text = FormatInspector.Inspect(_path, _info);
                _hexBox.Text = FormatInspector.HexPreview(File.ReadAllBytes(_path), 64 * 1024);
                LoadBmgEditor();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                try
                {
                    if (File.Exists(encodedPath))
                        File.Delete(encodedPath);
                }
                catch
                {
                }
            }
        }

        private void UpdateEditButtons()
        {
            if (_saveEdit != null)
                _saveEdit.Enabled = _editBox != null && !_editBox.ReadOnly && _editDirty;
        }

        private async void LaunchExternal()
        {
            ToolDescriptor tool = ToolchainManager.Recommended(_info.Kind);
            if (tool == null)
                return;
            string error;
            if (_info.Kind == ResourceKind.Kmp || _info.Kind == ResourceKind.Kcl)
            {
                string output;
                if (!ToolchainManager.RunCapture(tool, "CHECK " + ToolchainManager.QuoteArgument(_path), out output, out error))
                    murumsWiiModStudio.StudioMessageBox.Show(this, error, tool.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    _infoBox.Text += Environment.NewLine + Environment.NewLine + "==== " + tool.DisplayName + " CHECK ====" + Environment.NewLine + output;
                return;
            }

            if (_info.Kind == ResourceKind.Bmg)
            {
                LoadBmgEditor();
                if (_tabs != null && _tabs.TabPages.Count > 1)
                    _tabs.SelectedIndex = 1;
                return;
            }

            string staged = null;
            bool preserveStaged = true;
            try
            {
                byte[] original = File.ReadAllBytes(_path);
                bool compressedBrres = _info.Kind == ResourceKind.Brres && Yaz0.IsYaz0(original);
                byte[] before = compressedBrres ? Yaz0.Decompress(original) : original;
                // Edit a private copy. Closing this inspector returns its final
                // bytes to the parent archive, so wait for the specialist editor.
                staged = Path.Combine(Path.GetTempPath(), "murums-editor-" + Guid.NewGuid().ToString("N") + (compressedBrres ? ".brres" : Path.GetExtension(_path)));
                File.WriteAllBytes(staged, before);
                Enabled = false;
                _status.Text = L.T("Im Zusatzeditor speichern und ihn schließen, um Änderungen zu übernehmen.", "Save in the external editor and close it to apply changes.");
                string editorPath = staged;
                string launchError = await System.Threading.Tasks.Task.Run(delegate
                {
                    string message;
                    return ToolchainManager.Launch(tool, editorPath, true, out message) ? null : message;
                });
                if (launchError != null)
                    throw new IOException(launchError);
                byte[] after = File.ReadAllBytes(staged);
                bool changed = before.Length != after.Length;
                for (int i = 0; !changed && i < before.Length; i++)
                    changed = before[i] != after[i];
                if (changed)
                {
                    byte[] current = File.ReadAllBytes(_path);
                    bool originalChanged = current.Length != original.Length;
                    for (int i = 0; !originalChanged && i < current.Length; i++)
                        originalChanged = current[i] != original[i];
                    if (originalChanged)
                        throw new IOException(L.T("Die Quelldatei wurde inzwischen anderweitig geändert. Deine Bearbeitung bleibt als separate Datei erhalten.", "The source file was changed by another editor. Your edit is preserved as a separate file."));
                    if (_info.Kind == ResourceKind.Brres && (after.Length < 16 || Encoding.ASCII.GetString(after, 0, 4) != "bres"))
                        throw new InvalidDataException(L.T("Der Zusatzeditor hat keine gültige BRRES-Datei hinterlassen. Das Original bleibt erhalten.", "The external editor did not leave a valid BRRES file. The original was preserved."));
                    BackupManager.WriteAllBytesSafely(_path, compressedBrres ? Yaz0.Compress(after) : after);
                }

                preserveStaged = false;
                LoadData();
            }
            catch (Exception ex)
            {
                string recovery = staged != null && File.Exists(staged) ? "\r\n\r\n" + L.T("Arbeitskopie: ", "Working copy: ") + staged : "";
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message + recovery, tool.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Enabled = true;
                if (!preserveStaged && staged != null && File.Exists(staged))
                {
                    try
                    {
                        File.Delete(staged);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!_editDirty)
                return;
            DialogResult r = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ungespeicherte BMG-Änderungen speichern?", "Save unsaved BMG changes?"), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (r == DialogResult.Yes)
            {
                SaveBmg();
                if (_editDirty)
                    e.Cancel = true;
            }
        }
    }
}
