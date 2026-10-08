using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ToolchainForm : Form
    {
        private DataGridView _grid;
        private Label _note, _detailTitle, _purpose, _kind;
        private TextBox _path;
        private Button _install, _locate, _clear, _launch, _website;
        private TableLayoutPanel workspaceLayout;

        internal void PrepareWorkspace()
        {
            var header = workspaceLayout.GetControlFromPosition(0, 0);
            if (header != null) header.Visible = false;
            workspaceLayout.RowStyles[0].Height = 0;
        }
        public ToolchainForm()
        {
            Text = "murums Wii Mod Studio — Toolchain";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 650);
            MinimumSize = new Size(820, 500);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            DarkTheme.Apply(this);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            workspaceLayout = root;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(StudioChrome.Header(L.T("Zusätzliche Programme", "Additional programs"), L.T("Werkzeuge erkennen, verbinden und gezielt installieren", "Detect, connect and install specialist tools")), 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(18, 14, 18, 0) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _grid = new DataGridView { Name = "ToolchainTools", Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, Margin = new Padding(0, 0, 18, 0), AccessibleName = L.T("Programme und Installationsstatus", "Programs and installation status") };
            _grid.Columns.Add("Tool", L.T("Programm", "Program"));
            _grid.Columns.Add("Purpose", L.T("Zweck", "Purpose"));
            _grid.Columns.Add("Status", "Status");
            _grid.Columns.Add("Path", L.T("Pfad", "Path"));
            _grid.Columns[0].FillWeight = 62;
            _grid.Columns[2].FillWeight = 38;
            _grid.Columns[1].Visible = _grid.Columns[3].Visible = false;
            _grid.SelectionChanged += delegate { UpdateDetails(); };
            DarkTheme.StyleGrid(_grid);
            body.Controls.Add(_grid, 0, 0);
            var detailHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, BackColor = DarkTheme.Panel };
            var detail = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 8, Padding = new Padding(18), Margin = Padding.Empty, BackColor = DarkTheme.Panel };
            detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _detailTitle = new Label { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12), UseMnemonic = false };
            _purpose = new Label { AutoSize = true, Font = new Font("Segoe UI", 11), Margin = new Padding(0, 0, 0, 12), UseMnemonic = false };
            _kind = new Label { AutoSize = true, ForeColor = DarkTheme.Muted, Margin = new Padding(0, 0, 0, 24), UseMnemonic = false };
            detail.Controls.Add(_detailTitle, 0, 0);
            detail.Controls.Add(_purpose, 0, 1);
            detail.Controls.Add(_kind, 0, 2);
            detail.Controls.Add(new Label { Text = L.T("PROGRAMMPFAD", "PROGRAM PATH"), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = DarkTheme.Muted, Margin = new Padding(0, 0, 0, 8) }, 0, 3);
            _path = new TextBox { Name = "ToolchainProgramPath", ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = L.T("Erkannter Programmpfad", "Detected program path"), Margin = new Padding(0, 0, 0, 12) };
            _path.Enter += delegate { _path.SelectAll(); };
            StudioUx.DisableHover(_path);
            detail.Controls.Add(_path, 0, 4);
            var guidance = new Label { AutoSize = true, ForeColor = DarkTheme.Muted, Margin = Padding.Empty, UseMnemonic = false, Text = L.T("Vorhandene Programme werden in lokalen tools-Ordnern, PATH und üblichen Installationsordnern gesucht. Ein eigener Pfad gilt nur für das gewählte Programm. Kommandozeilen-Backends werden vom passenden Studio-Werkzeug genutzt.", "Existing programs are detected in local tools folders, PATH and common installation folders. A custom path applies only to the selected program. Command-line backends are used by the matching Studio tool.") };
            detail.Controls.Add(guidance, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 16, 0, 0) };
            _install = MakeButton(L.T("Installieren", "Install"), delegate { InstallSelected(); });
            _locate = MakeButton(L.T("Programm suchen…", "Locate program…"), delegate { LocateSelected(); });
            _clear = MakeButton(L.T("Eigenen Pfad entfernen", "Clear custom path"), delegate { ClearSelected(); });
            _launch = MakeButton(L.T("Programm starten", "Launch program"), delegate
            {
                var tool = SelectedTool();
                if (tool == null) return;
                if (!IsGui(tool))
                {
                    StudioMessageBox.Show(this, L.T("Dieses Programm ist ein Kommandozeilen-Backend. Verwende das passende Studio-Werkzeug.", "This program is a command-line backend. Use its matching Studio tool."));
                    return;
                }
                string error;
                if (!ToolchainManager.Launch(tool, null, false, out error)) StudioMessageBox.Show(this, error);
            });
            _website = MakeButton(L.T("Website öffnen ↗", "Open website ↗"), delegate { OpenWebsite(); });
            actions.Controls.AddRange(new Control[] { _install, _locate, _clear, _launch, _website });
            detail.Controls.Add(actions, 0, 7);
            detail.SizeChanged += delegate
            {
                int width = Math.Max(100, detail.ClientSize.Width - detail.Padding.Horizontal - 8);
                foreach (Control item in detail.Controls) if (item is Label) item.MaximumSize = new Size(width, 0);
            };
            detailHost.Controls.Add(detail);
            body.Controls.Add(detailHost, 1, 0);
            root.Controls.Add(body, 0, 1);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Padding = new Padding(18, 8, 18, 12), Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _note = new Label { AutoSize = true, UseMnemonic = false, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 4) };
            footer.Controls.Add(_note, 0, 0);
            var bulk = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            bulk.Controls.Add(MakeButton(L.T("Alle fehlenden installieren", "Install all missing"), delegate { InstallAllMissing(); }));
            var refresh = MakeButton(L.T("Neu erkennen", "Refresh detection"), delegate { RefreshGrid(); });
            StudioActions.Icon(refresh, StudioIcon.Refresh);
            bulk.Controls.Add(refresh);
            footer.Controls.Add(bulk, 0, 1);
            var close = MakeButton(L.T("Schließen", "Close"), delegate { Close(); });
            close.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(close, 1, 1);
            CancelButton = close;
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);
            RefreshGrid();
        }

        private Button MakeButton(string text, EventHandler click)
        {
            var button = StudioChrome.ActionButton(text);
            button.Click += click;
            return button;
        }

        private static bool IsGui(ToolDescriptor tool)
        {
            return tool.Id == "NintyFont" || tool.Id == "LoopingAudioConverter" || tool.Id == "BrawlCrate" || tool.Id == "RiiStudio" || tool.Id == "SwitchToolbox";
        }

        private void UpdateDetails()
        {
            if (_detailTitle == null) return;
            var tool = SelectedTool();
            bool selected = tool != null;
            _detailTitle.Text = selected ? tool.DisplayName : L.T("Programm auswählen", "Choose a program");
            _purpose.Text = selected ? Purpose(tool) : L.T("Wähle links ein Programm, um Zweck, Pfad und Aktionen anzuzeigen.", "Choose a program on the left to see its purpose, path and actions.");
            _path.Text = selected ? Convert.ToString(_grid.SelectedRows[0].Cells[3].Value) : "";
            _kind.Text = selected ? Convert.ToString(_grid.SelectedRows[0].Cells[2].Value) + " · " + (IsGui(tool) ? L.T("Eigenständiges Programm", "Standalone program") : L.T("Studio-Backend", "Studio backend")) : "";
            _install.Enabled = _locate.Enabled = selected;
            _clear.Enabled = selected && ToolchainManager.GetCustomPath(tool) != null;
            _launch.Enabled = selected && IsGui(tool) && !String.IsNullOrEmpty(_path.Text);
            _website.Enabled = selected && !String.IsNullOrEmpty(tool.Website);
            StudioUx.SetHelp(_launch, selected && !IsGui(tool) ? L.T("Dieses Backend wird im passenden Studio-Werkzeug genutzt.", "Use this backend through its matching Studio tool.") : L.T("Das ausgewählte Programm separat starten.", "Launch the selected program separately."));
        }

        private static string Purpose(ToolDescriptor tool)
        {
            if (!L.IsGerman) return tool.Purpose;
            switch (tool.Id)
            {
                case "wszst": return "SZS/U8/BRRES/BREFF/BREFT-Archive bearbeiten";
                case "wimgt": return "TPL/TEX0/BTI/PNG-Bilder konvertieren";
                case "wkmpt": return "KMP-Streckendaten prüfen und konvertieren";
                case "wkclt": return "KCL-Kollision prüfen und in OBJ konvertieren";
                case "wbmgt": return "BMG-Nachrichten dekodieren, bearbeiten und kodieren";
                case "wpatt": return "PAT0-Texturwechsel-Animationen konvertieren";
                case "wstrt": return "main.dol und StaticR.rel patchen";
                case "wctct": return "CT-CODE / LE-CODE konvertieren";
                case "wlect": return "LE-CODE / LEX / LPAR bearbeiten";
                case "wmdlt": return "MDL0 untersuchen und in Text umwandeln";
                case "wit": return "Wii/GameCube-Disc-Images und FST-Dateistrukturen bearbeiten";
                case "RiiStudio": return "BRRES/MDL0/TEX0-Modelle, Animationen und KMP bearbeiten";
                case "rszst": return "BRRES importieren/optimieren; KMP/KCL-JSON und SZS bearbeiten";
                case "SwitchToolbox": return "Zusätzlicher Layout-/Modelleditor und Referenzansicht";
                case "BrawlCrate": return "Spezialeditor für BRRES/BRSAR/BRSTM/DOL/REL";
                case "LoopingAudioConverter": return "WAV-Loopmarkierungen nach BRSTM und in weitere Loopformate konvertieren";
                case "NintyFont": return "Nintendo-Bitmap-Schriften einschließlich BRFNT bearbeiten";
                case "ffmpeg": return "Audio-/Video-Konvertierung für Studio-Werkzeuge";
                default: return tool.Purpose;
            }
        }
        private ToolDescriptor SelectedTool()
        {
            if (_grid.SelectedRows.Count == 0)
                return null;
            return _grid.SelectedRows[0].Tag as ToolDescriptor;
        }

        private void RefreshGrid()
        {
            ToolDescriptor selected = SelectedTool();
            _grid.Rows.Clear();
            for (int i = 0; i < ToolchainManager.KnownTools.Length; i++)
            {
                ToolDescriptor td = ToolchainManager.KnownTools[i];
                string p = ToolchainManager.Find(td);
                string custom = ToolchainManager.GetCustomPath(td);
                string status = p == null ? (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "internal", "tools", "INSTALL_TOOLCHAIN.ps1")) ? L.T("Installierbar", "Installable") : L.T("Nicht gefunden", "Not found")) : (custom != null && String.Equals(custom, p, StringComparison.OrdinalIgnoreCase) ? L.T("Bereit (manuell)", "Ready (manual)") : L.T("Bereit", "Ready"));
                int row = _grid.Rows.Add(td.DisplayName, Purpose(td), status, p ?? "");
                _grid.Rows[row].Tag = td;
            }

            if (selected != null)
                foreach (DataGridViewRow row in _grid.Rows)
                    if (Object.ReferenceEquals(row.Tag, selected))
                    {
                        _grid.ClearSelection();
                        row.Selected = true;
                        _grid.CurrentCell = row.Cells[0];
                        break;
                    }
            int ready = 0;
            foreach (DataGridViewRow row in _grid.Rows)
                if (!String.IsNullOrEmpty(Convert.ToString(row.Cells[3].Value))) ready++;
            _note.Text = ready + " / " + _grid.Rows.Count + L.T(" Programme bereit", " programs ready");
            UpdateDetails();
        }

        private void InstallSelected()
        {
            ToolDescriptor tool = SelectedTool();
            if (tool == null)
                return;
            string error;
            bool ok = ToolchainManager.InstallTool(tool, out error);
            RefreshGrid();
            if (!ok)
                murumsWiiModStudio.StudioMessageBox.Show(this, error ?? L.T("Installation fehlgeschlagen.", "Installation failed."), tool.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void InstallAllMissing()
        {
            string error;
            bool ok = ToolchainManager.InstallAllMissing(out error);
            RefreshGrid();
            if (!ok)
                murumsWiiModStudio.StudioMessageBox.Show(this, error ?? L.T("Installation fehlgeschlagen.", "Installation failed."), "Toolchain", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void LocateSelected()
        {
            ToolDescriptor tool = SelectedTool();
            if (tool == null)
                return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = L.T(tool.DisplayName + " auswählen", "Locate " + tool.DisplayName);
                d.Filter = tool.DisplayName + " (*.exe)|*.exe|" + L.T("Alle Dateien", "All files") + "|*.*";
                d.FileName = tool.Executables.Length > 0 ? tool.Executables[0] : "";
                if (d.ShowDialog(this) != DialogResult.OK)
                    return;
                ToolchainManager.SetCustomPath(tool, d.FileName);
                RefreshGrid();
            }
        }

        private void ClearSelected()
        {
            ToolDescriptor tool = SelectedTool();
            if (tool == null)
                return;
            ToolchainManager.ClearCustomPath(tool);
            RefreshGrid();
        }

        private void OpenWebsite()
        {
            ToolDescriptor tool = SelectedTool();
            if (tool == null || String.IsNullOrEmpty(tool.Website))
                return;
            try
            {
                Process.Start(new ProcessStartInfo(tool.Website) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, tool.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
