using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class BrlytMapForm : Form
    {
        private CueTextBox _path;
        private DataGridView _grid;
        private Label _summary;
        public BrlytMapForm(string suggestedPath)
        {
            Text = L.T("BRLYT Material Mapper", "BRLYT Material Mapper");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 560);
            Size = new Size(980, 680);
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
            BuildUi();
            DarkTheme.Apply(this);
            if (!String.IsNullOrWhiteSpace(suggestedPath) && File.Exists(suggestedPath))
            {
                _path.Text = suggestedPath;
                LoadMap(suggestedPath);
            }
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            Label title = new Label();
            title.Text = L.T("BRLYT → Material / TPL Zuordnung", "BRLYT → material / TPL mapping");
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.White;
            root.Controls.Add(title, 0, 0);
            Label hint = new Label();
            hint.AutoSize = true;
            hint.MaximumSize = new Size(900, 0);
            hint.Margin = new Padding(0, 4, 0, 12);
            hint.ForeColor = DarkTheme.Muted;
            hint.Text = L.T("Zeigt, welches BRLYT-Material welchen TPL-Dateinamen in welchem Texture-Slot benutzt. Genau dieser Materialname muss als RLTP-Ziel verwendet werden.", "Shows which BRLYT material uses which TPL filename and texture slot. This material name is the correct RLTP target.");
            root.Controls.Add(hint, 0, 1);
            TableLayoutPanel pathRow = new TableLayoutPanel();
            pathRow.Dock = DockStyle.Top;
            pathRow.ColumnCount = 3;
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            pathRow.Height = 38;
            Label pathLabel = new Label();
            pathLabel.Text = "BRLYT:";
            pathLabel.Dock = DockStyle.Fill;
            pathLabel.TextAlign = ContentAlignment.MiddleLeft;
            pathLabel.ForeColor = DarkTheme.Fore;
            pathRow.Controls.Add(pathLabel, 0, 0);
            _path = new CueTextBox();
            _path.Dock = DockStyle.Fill;
            _path.Cue = L.T("Pfad zu .brlyt", "Path to .brlyt");
            pathRow.Controls.Add(_path, 1, 0);
            Button browse = new Button();
            browse.Text = L.T("Auswählen...", "Browse...");
            browse.Dock = DockStyle.Fill;
            browse.FlatStyle = FlatStyle.Flat;
            browse.Click += delegate
            {
                Browse();
            };
            pathRow.Controls.Add(browse, 2, 0);
            root.Controls.Add(pathRow, 0, 2);
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.BackgroundColor = DarkTheme.Panel;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = DarkTheme.Panel3;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.DefaultCellStyle.BackColor = DarkTheme.Panel;
            _grid.DefaultCellStyle.ForeColor = DarkTheme.Fore;
            _grid.DefaultCellStyle.SelectionBackColor = DarkTheme.Accent;
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.Columns.Add("Texture", L.T("TPL-Datei", "TPL file"));
            _grid.Columns.Add("Material", "Material");
            _grid.Columns.Add("Slot", L.T("Texture-Slot", "Texture slot"));
            _grid.Columns.Add("Id", "TPL ID");
            _grid.Columns[0].FillWeight = 42F;
            _grid.Columns[1].FillWeight = 36F;
            _grid.Columns[2].FillWeight = 12F;
            _grid.Columns[3].FillWeight = 10F;
            root.Controls.Add(_grid, 0, 3);
            _summary = new Label();
            _summary.AutoSize = true;
            _summary.Margin = new Padding(0, 10, 0, 0);
            _summary.ForeColor = DarkTheme.Muted;
            _summary.Text = L.T("Noch keine BRLYT geladen.", "No BRLYT loaded yet.");
            root.Controls.Add(_summary, 0, 4);
        }

        private void Browse()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "BRLYT (*.brlyt)|*.brlyt|" + L.T("Alle Dateien", "All files") + " (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                _path.Text = dlg.FileName;
                LoadMap(dlg.FileName);
            }
        }

        private void LoadMap(string path)
        {
            try
            {
                BrlytLayoutMap map = BrlytInspector.Load(path);
                _grid.Rows.Clear();
                int i;
                for (i = 0; i < map.Bindings.Count; i++)
                {
                    BrlytTextureBinding b = map.Bindings[i];
                    _grid.Rows.Add(b.TextureName, b.MaterialName, b.Slot.ToString(), b.TextureId.ToString());
                }

                _summary.Text = L.Format("{0} TPL-Dateien • {1} Material/Texture-Bindings • {2}", "{0} TPL files • {1} material/texture bindings • {2}", map.Textures.Count, map.Bindings.Count, Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                _grid.Rows.Clear();
                _summary.Text = L.T("BRLYT konnte nicht gelesen werden.", "Could not read BRLYT.");
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
