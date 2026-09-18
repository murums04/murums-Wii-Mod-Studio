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
        private Label _note;
        public ToolchainForm()
        {
            Text = "murums Wii Mod Studio — Toolchain";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 620);
            MinimumSize = new Size(820, 500);
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
            DarkTheme.Apply(this);
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            Controls.Add(root);
            Panel head = StudioChrome.Header(L.T("Zusätzliche Programme", "Additional programs"), L.T("Spezialwerkzeuge installieren oder vorhandene Programme verbinden.", "Install specialist tools or connect programs you already have."));
            root.Controls.Add(head, 0, 0);
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Columns.Add("Tool", "Tool");
            _grid.Columns.Add("Purpose", L.T("Zweck", "Purpose"));
            _grid.Columns.Add("Status", "Status");
            _grid.Columns.Add("Path", L.T("Pfad", "Path"));
            _grid.Columns[0].FillWeight = 23;
            _grid.Columns[1].FillWeight = 37;
            _grid.Columns[2].FillWeight = 13;
            _grid.Columns[3].FillWeight = 47;
            DarkTheme.StyleGrid(_grid);
            root.Controls.Add(_grid, 0, 1);
            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.Padding = new Padding(10, 7, 10, 4);
            actions.BackColor = DarkTheme.Panel;
            actions.WrapContents = true;
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.Controls.Add(MakeButton(L.T("Ausgewähltes installieren", "Install selected"), delegate
            {
                InstallSelected();
            }));
            actions.Controls.Add(MakeButton(L.T("Alle fehlenden installieren", "Install all missing"), delegate
            {
                InstallAllMissing();
            }));
            actions.Controls.Add(MakeButton(L.T("Ausgewähltes Tool suchen...", "Locate selected..."), delegate
            {
                LocateSelected();
            }));
            actions.Controls.Add(MakeButton(L.T("Benutzerpfad entfernen", "Clear custom path"), delegate
            {
                ClearSelected();
            }));
            actions.Controls.Add(MakeButton(L.T("Programm starten", "Launch program"), delegate
            {
                var tool = SelectedTool();
                if (tool == null)
                    return;
                if (tool.Id != "NintyFont" && tool.Id != "LoopingAudioConverter" && tool.Id != "BrawlCrate" && tool.Id != "RiiStudio" && tool.Id != "SwitchToolbox")
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, "This is a command-line backend. Use its matching Studio tool.");
                    return;
                }

                string error;
                if (!ToolchainManager.Launch(tool, null, false, out error))
                    murumsWiiModStudio.StudioMessageBox.Show(this, error);
            }));
            actions.Controls.Add(MakeButton(L.T("Website öffnen", "Open website"), delegate
            {
                OpenWebsite();
            }));
            actions.Controls.Add(MakeButton(L.T("Neu erkennen", "Refresh detection"), delegate
            {
                RefreshGrid();
            }));
            root.Controls.Add(actions, 0, 2);
            _note = new Label();
            _note.Dock = DockStyle.Fill;
            _note.Padding = new Padding(14, 8, 14, 8);
            _note.ForeColor = DarkTheme.Muted;
            _note.BackColor = DarkTheme.Panel;
            _note.Text = L.T("Fehlende Backends können direkt über den mitgelieferten Installer in den lokalen tools-Ordner geladen werden. Bereits installierte Programme werden automatisch über tools, PATH, WinGet/Scoop und typische Installationsordner erkannt.", "Missing backends can be downloaded directly by the bundled installer into the local tools folder. Existing programs are detected automatically through tools, PATH, WinGet/Scoop and common install folders.");
            root.Controls.Add(_note, 0, 3);
            RefreshGrid();
        }

        private Button MakeButton(string text, EventHandler click)
        {
            Button b = new Button();
            b.AutoSize = true;
            b.Height = 32;
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            b.Margin = new Padding(4, 0, 4, 0);
            b.Click += click;
            return b;
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
                int row = _grid.Rows.Add(td.DisplayName, td.Purpose, status, p ?? "");
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
