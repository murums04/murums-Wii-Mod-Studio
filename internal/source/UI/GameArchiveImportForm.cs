using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class GameArchiveImportForm : Form
    {
        private static string lastDestination;
        private readonly string imagePath;
        private readonly string preferredName;
        private readonly ComboBox archives = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly TextBox destination = new TextBox { Dock = DockStyle.Fill };
        private readonly CheckBox related = new CheckBox { AutoSize = true, Checked = true };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly Button import = new Button { AutoSize = true, Enabled = false };
        private string[] available = new string[0];
        private bool busy;

        internal string ImportedPath { get; private set; }

        internal static string[] SelectMany(Form owner, string filter)
        {
            using (var picker = new OpenFileDialog { Title = "Add archives", Filter = filter,
                InitialDirectory = PackSelection.Folder(owner), CheckFileExists = true, Multiselect = true })
                return ToolArchiveFilters.Show(picker, owner) == DialogResult.OK ? ToolArchiveFilters.SelectedFiles(picker) : new string[0];
        }
        internal static string Select(Form owner, string filter, string preferred = null, string outputFolder = null)
        {
            using (var picker = new OpenFileDialog
            {
                Title = L.T("Datei öffnen", "Open file"),
                Filter = filter, InitialDirectory = PackSelection.Folder(owner),
                CheckFileExists = true
            })
            {
                if (ToolArchiveFilters.Show(picker, owner) != DialogResult.OK)
                    return null;
                return ToolArchiveFilters.SelectedFile(picker);
            }
        }

        internal static string Resolve(Form owner, string path, string preferred = null, string outputFolder = null)
        {
            if (!GameArchiveImport.IsDisc(path))
                return path;
            using (var dialog = new GameArchiveImportForm(path, preferred, outputFolder))
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ImportedPath : null;
        }

        internal static string Import(Form owner, string preferred = null, string outputFolder = null)
        {
            using (var picker = new OpenFileDialog
            {
                Title = L.T("Mario-Kart-Wii-ISO/WBFS wählen", "Choose your Mario Kart Wii ISO/WBFS"),
                Filter = "ISO / WBFS|*.iso;*.wbfs;*.wia;*.ciso;*.wdf",
                CheckFileExists = true
            })
            {
                if (ToolArchiveFilters.Show(picker, owner) != DialogResult.OK)
                    return null;
                return Resolve(owner, ToolArchiveFilters.SelectedFile(picker), preferred, PackSelection.Output(owner, outputFolder));
            }
        }

        internal GameArchiveImportForm(string path, string preferred, string outputFolder)
        {
            imagePath = path;
            preferredName = preferred;
            Text = L.T("Fehlende Spieldateien importieren", "Import missing game files");
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(720, 470);
            MinimumSize = new Size(640, 460);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Padding = new Padding(18);

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 7; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            AddWide(grid, new Label
            {
                Text = L.T("Wähle ein Archiv und den Speicherort. Studio importiert Originaldateien aus deinem Spielabbild; vorhandene Dateien bleiben erhalten.",
                    "Choose an archive and where to save it. Studio imports original files from your game image; existing files are kept."),
                AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16)
            }, 0);
            AddWide(grid, new Label { Text = Path.GetFileName(path), AutoSize = true, Margin = new Padding(0, 0, 0, 12) }, 1);
            AddWide(grid, new Label { Text = L.T("1. Benötigte Datei (bei Race die Spielsprache wählen)", "1. Required file (for Race, choose your game language)"), AutoSize = true }, 2);
            AddWide(grid, archives, 3);
            related.Text = L.T("Zugehörige Dateien mitnehmen (empfohlen)", "Include related files (recommended)");
            AddWide(grid, related, 4);
            AddWide(grid, new Label { Text = L.T("2. Speichern in", "2. Save to"), AutoSize = true, Margin = new Padding(0, 14, 0, 4) }, 5);
            destination.Text = !String.IsNullOrWhiteSpace(outputFolder) ? outputFolder : lastDestination ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MUR_EDITED");
            grid.Controls.Add(destination, 0, 6);
            var browse = new Button { Text = L.T("Auswählen...", "Browse..."), AutoSize = true };
            browse.Click += delegate
            {
                using (var picker = new FolderPickerDialog
                {
                    SelectedPath = destination.Text,
                    Description = L.T("Zielordner wählen oder neu erstellen", "Choose or create a destination folder")
                })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        destination.Text = picker.SelectedPath;
            };
            grid.Controls.Add(browse, 1, 6);
            status.Margin = new Padding(0, 14, 0, 12);
            AddWide(grid, status, 7);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = L.T("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            import.Text = L.T("Importieren und öffnen", "Import and open");
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(import);
            AddWide(grid, buttons, 8);
            Controls.Add(grid);
            CancelButton = cancel;
            AcceptButton = import;
            DarkTheme.Apply(this);
            import.Click += async delegate { await ImportSelected(); };
            archives.SelectedIndexChanged += delegate { UpdateSelection(); };
            related.CheckedChanged += delegate { UpdateSelection(); };
            Shown += async delegate
            {
                if (Owner != null)
                    Icon = Owner.Icon;
                await ReadArchives();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs args)
            {
                if (busy)
                    args.Cancel = true;
            };
        }

        private static void AddWide(TableLayoutPanel grid, Control control, int row)
        {
            grid.Controls.Add(control, 0, row);
            grid.SetColumnSpan(control, 2);
        }

        private async Task ReadArchives()
        {
            busy = true;
            status.Text = L.T("Dateiliste wird gelesen…", "Reading archive list…");
            try
            {
                available = await Task.Run(() => GameArchiveImport.List(imagePath));
                if (preferredName == "Earth.szs")
                    available = available.Where(path => Path.GetFileName(path) == "Earth.szs" || Path.GetFileName(path) == "globe.arc").ToArray();
                else if (preferredName == "BackModel.szs" || preferredName == "Font.szs" || preferredName == "MenuOther.szs")
                    available = available.Where(path => Path.GetFileName(path) == preferredName).ToArray();
                else if (preferredName == "Race.szs")
                    available = available.Where(path => Path.GetFileName(path) == "Race.szs" || Path.GetFileName(path).StartsWith("Race_", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (available.Length == 0)
                    throw new InvalidDataException(L.T("Das benötigte Archiv fehlt in diesem Spielabbild.", "This game image does not contain the required archive."));
                archives.Items.AddRange(available.Select(Path.GetFileName).Cast<object>().ToArray());
                int preferred = Array.FindIndex(available, path => String.Equals(Path.GetFileName(path), preferredName, StringComparison.OrdinalIgnoreCase));
                if (preferredName == "Race.szs")
                {
                    int languageArchive = Array.FindIndex(available, path => Path.GetFileName(path).StartsWith("Race_", StringComparison.OrdinalIgnoreCase));
                    if (languageArchive >= 0)
                        preferred = languageArchive;
                }
                archives.SelectedIndex = preferred >= 0 ? preferred : 0;
                import.Enabled = true;
            }
            catch (Exception error)
            {
                status.Text = error.Message;
            }
            finally
            {
                busy = false;
            }
        }

        private void UpdateSelection()
        {
            if (archives.SelectedIndex < 0)
                return;
            string[] files = GameArchiveImport.RelatedFiles(available[archives.SelectedIndex], available, related.Checked);
            status.Text = L.T("Importiert: ", "Will import: ") + String.Join(", ", files.Select(Path.GetFileName))
                + "\n\n" + L.T("Das sind Originaldateien. Zusätzliche Mod-Dateien sind nicht im Spielabbild enthalten.",
                                  "These are original game files. Additional mod files are not included in the game image.");
        }

        private async Task ImportSelected()
        {
            if (busy || archives.SelectedIndex < 0)
                return;
            string folder;
            try
            {
                if (String.IsNullOrWhiteSpace(destination.Text))
                    throw new ArgumentException(L.T("Bitte einen Zielordner wählen.", "Please choose a destination folder."));
                folder = Path.GetFullPath(destination.Text.Trim());
            }
            catch (Exception error)
            {
                status.Text = error.Message;
                return;
            }
            string[] files = GameArchiveImport.RelatedFiles(available[archives.SelectedIndex], available, related.Checked)
                .Where((path, index) => index == 0 || !File.Exists(Path.Combine(folder, Path.GetFileName(path)))).ToArray();
            busy = true;
            Enabled = false;
            status.Text = L.T("Dateien werden importiert…", "Importing files…");
            try
            {
                ImportedPath = await Task.Run(() => GameArchiveImport.Extract(imagePath, files, folder));
                lastDestination = folder;
                StudioMessageBox.Show(this,
                    L.T("Originaldateien gespeichert in:\n", "Original files saved to:\n") + folder + "\n\n"
                    + String.Join(", ", files.Select(Path.GetFileName)) + "\n\n"
                    + L.T("Die ausgewählte Datei kann jetzt bearbeitet werden.", "The selected file is now ready to edit."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                busy = false;
                DialogResult = DialogResult.OK;
            }
            catch (Exception error)
            {
                status.Text = error.Message;
            }
            finally
            {
                busy = false;
                Enabled = true;
            }
        }
    }
}
