using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class MenuTextForm : StudioToolForm
    {
        SpecialEditorHistory editHistory;
        readonly Dictionary<string, StudioArchiveCopy> archives = new Dictionary<string, StudioArchiveCopy>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> owners = new Dictionary<string, string>();
        readonly Dictionary<string, string> entryKeys = new Dictionary<string, string>();
        string source, activeKey;
        bool loading, dirty;
        readonly Dictionary<string, BmgTextDocument> documents = new Dictionary<string, BmgTextDocument>();
        readonly HashSet<string> changed = new HashSet<string>();
        readonly ComboBox resource = new ComboBox
        {
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly TextBox search = new TextBox
        {
            Width = 230
        };
        readonly DataGridView grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly Button save;
        readonly StudioReadOnlyText sample = new StudioReadOnlyText
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 14)
        };
        public MenuTextForm() : base("RR-MKWii Menu Text Tool", L.T("Nachrichten laden • Texte suchen und bearbeiten • Kopien speichern", "Add message sources • Search messages • Edit text and save copies"), "RR: UIAssets.szs / RaceAssets.szs · Original: language archives (_E / _U / _J) · *.bmg")
        {
            var addArchive = Action(L.T("Archiv hinzufügen…", "Add archive…"), L.T("Mehrere Spracharchive oder BMG-Dateien ergänzen; bisherige Änderungen bleiben erhalten.", "Add multiple language archives or BMG files; existing edits are kept."), Open);
            addArchive.Name = "PackSourceAction";
            StudioActions.Icon(addArchive, StudioIcon.Add);
            var clearSelection = Action(L.T("Auswahl leeren", "Clear selection"), L.T("Geladene Dateien schliessen.", "Clear loaded files."), delegate {
                grid.EndEdit();
                if (dirty && StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen verwerfen und geladene Dateien schliessen?", "Discard unsaved changes and clear loaded files?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                resource.Items.Clear(); grid.Rows.Clear(); documents.Clear(); changed.Clear(); archives.Clear(); owners.Clear(); entryKeys.Clear();
                source = activeKey = null; dirty = false; save.Enabled = false; sample.Clear(); PackSelection.SourceCleared(this);
            });
            StudioActions.Icon(clearSelection, StudioIcon.Remove);
            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, AutoSize = true };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            filters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            filters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            filters.Controls.Add(new Label { Text = L.T("Nachrichten-Datei", "Message resource"), AutoSize = true }, 0, 0);
            resource.Dock = DockStyle.Fill;
            filters.Controls.Add(resource, 0, 1);
            resource.SelectedIndexChanged += delegate
            {
                Guard(LoadMessages);
            };
            filters.Controls.Add(new Label { Text = L.T("ID / Text suchen", "Find ID / text"), AutoSize = true }, 1, 0);
            search.Dock = DockStyle.Fill;
            filters.Controls.Add(search, 1, 1);
            search.TextChanged += delegate
            {
                Filter();
            };
            save = ExportAction(L.T("Bearbeitete Kopie speichern…", "Save edited copy…"), L.T("Geänderte BMG-Ressourcen in eine separate Archivkopie speichern. Andere Einträge bleiben unverändert.", "Encode changed BMG resources and save a separate archive copy. Other archive entries remain unchanged."), Save);
            grid.Columns.Add("id", L.T("Nachrichten-ID", "Message ID"));
            grid.Columns[0].ReadOnly = true;
            grid.Columns[0].FillWeight = 18;
            grid.Columns.Add("text", L.T("Nachrichtentext (BMG-Steuerzeichen)", "Message text (BMG escape syntax)"));
            grid.Columns[1].FillWeight = 82;
            grid.CellEndEdit += delegate (object sender, DataGridViewCellEventArgs e)
            {
                if (loading || e.RowIndex < 0)
                    return;
                var row = (BmgTextDocument.Row)grid.Rows[e.RowIndex].Tag;
                row.Text = Convert.ToString(grid.Rows[e.RowIndex].Cells[1].Value);
                changed.Add(activeKey);
                dirty = true;
                save.Enabled = true;
            };
            grid.DataError += delegate (object s, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            var sampleGroup = new GroupBox
            {
                Text = L.T("Textvorschau", "Text preview"),
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };
            var previewContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            previewContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            previewContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewContent.Controls.Add(new Label { Text = L.T("Systemschrift; Spielcodes bleiben sichtbar.", "System font; game codes remain visible."), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8) }, 0, 0);
            previewContent.Controls.Add(StudioReadOnlyText.Surface(sample), 0, 1);
            sampleGroup.Controls.Add(previewContent);
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 278));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.Controls.Add(filters, 0, 0);
            workspace.SetColumnSpan(filters, 2);
            workspace.Controls.Add(grid, 0, 1);
            workspace.Controls.Add(sampleGroup, 1, 1);
            Body.Controls.Add(workspace);
            grid.SelectionChanged += delegate
            {
                UpdateSample();
            };
            grid.CellValueChanged += delegate
            {
                UpdateSample();
            };
            Finish();
            DarkTheme.StyleGrid(grid);
            save.Enabled = false;
            editHistory = new SpecialEditorHistory(this, Actions, CaptureHistory, RestoreHistory,
                delegate { return String.Join("|", archives.Keys); });
            StudioUx.SetHelp(grid, "Edit text in the second column. Preserve escapes such as \\z{...}, \\c{...} and \\n. Messages are shown on one row; use \n escapes for in-game line breaks.");
            StudioUx.SetHelp(search, L.T("Nachrichten-IDs und Texte filtern; Inhalte bleiben unverändert.", "Filter message IDs and text without changing any messages."));
            Status.Text = L.T("Verwendet Wiimms BMG Tool. Steuerzeichen für Farben, Variablen und Spielsymbole unverändert lassen.", "Uses Wiimms BMG Tool. Escapes represent colours, variables and game symbols; keep them intact.");
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                grid.EndEdit();
                if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ohne Speichern der Textänderungen schliessen?", "Close without saving these text changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        object[] CaptureHistory()
        {
            return new object[] { documents.ToDictionary(p => p.Key, p => p.Value.Rows.Select(r => (object)r.Text).ToArray()),
                changed.OrderBy(k => k).Cast<object>().ToArray() };
        }
        void RestoreHistory(object[] state)
        {
            loading = true;
            try
            {
                foreach (var pair in (Dictionary<string, object[]>)state[0])
                    for (int i = 0; i < pair.Value.Length; i++) documents[pair.Key].Rows[i].Text = (string)pair.Value[i];
                changed.Clear(); foreach (string key in (object[])state[1]) changed.Add(key);
                dirty = changed.Count > 0; save.Enabled = dirty;
            }
            finally { loading = false; }
            LoadMessages(); UpdateSample();
        }

        void UpdateSample()
        {
            sample.Text = grid.CurrentRow == null ? L.T("Wähle eine Nachricht für die Vorschau.", "Select a message to preview it.") : Convert.ToString(grid.CurrentRow.Cells[1].Value).Replace("\\n", Environment.NewLine);
        }

        void Open()
        {
            grid.EndEdit();
            AddSelectedPaths(GameArchiveImportForm.SelectMany(this, ToolArchiveFilters.Messages));
        }

        void AddSelectedPaths(string[] paths)
        {
            var skipped = new List<string>();
            var incoming = new Dictionary<string, StudioArchiveCopy>(StringComparer.OrdinalIgnoreCase);
            var parsed = new Dictionary<string, BmgTextDocument>();
            var keys = new Dictionary<string, string>();
            var files = new Dictionary<string, string>();
            foreach (string input in paths)
            {
                string path = Path.GetFullPath(input);
                if (archives.ContainsKey(path) || incoming.ContainsKey(path)) continue;
                if (archives.Keys.Concat(incoming.Keys).Any(p => Path.GetFileName(p).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                    throw new IOException(L.T("Ein Archiv mit diesem Namen ist bereits geladen.", "An archive with this name is already loaded."));
                var next = Path.GetExtension(path).Equals(".bmg", StringComparison.OrdinalIgnoreCase) ? null : new StudioArchiveCopy(path);
                var names = next == null ? new[] { Path.GetFileName(path) } : next.Files.Keys.Where(k => k.EndsWith(".bmg", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (names.Length == 0) { skipped.Add(Path.GetFileName(path)); continue; }
                incoming.Add(path, next);
                foreach (string name in names)
                {
                    string label = Path.GetFileName(path) + " / " + name;
                    parsed.Add(label, new BmgTextDocument(BmgTextDocument.Decode(next == null ? File.ReadAllBytes(path) : next.Files[name].Data)));
                    keys.Add(label, name); files.Add(label, path);
                }
            }
            foreach (var item in incoming) archives.Add(item.Key, item.Value);
            foreach (var item in parsed) { documents.Add(item.Key, item.Value); owners.Add(item.Key, files[item.Key]); entryKeys.Add(item.Key, keys[item.Key]); resource.Items.Add(item.Key); }
            if (archives.Count == 0)
            {
                Status.Text = skipped.Count == 0 ? L.T("Keine neuen Nachrichtenquellen ausgewählt.", "No new message sources selected.")
                    : L.T("Keine BMG-Nachrichten gefunden. RR: UIAssets.szs / RaceAssets.szs hinzufügen. Ausgelassen: ", "No BMG messages found. RR: add UIAssets.szs / RaceAssets.szs. Skipped: ") + String.Join(", ", skipped);
                return;
            }
            source = archives.Keys.First(); PackSelection.SourceLoaded(this);
            if (resource.SelectedIndex < 0) resource.SelectedIndex = 0;
            save.Enabled = true;
            Status.Text = archives.Count + L.T(" Nachrichtenquellen geladen.", " message sources loaded.") + (skipped.Count == 0 ? "" : L.T(" Ohne BMG; ausgelassen: ", " No BMG; skipped: ") + String.Join(", ", skipped));
        }
        void LoadMessages()
        {
            if (resource.SelectedItem == null)
                return;
            grid.EndEdit();
            string key = (string)resource.SelectedItem;

            activeKey = key;
            loading = true;
            try
            {
                grid.Rows.Clear();
                foreach (var row in documents[key].Rows)
                {
                    int i = grid.Rows.Add(row.Id, row.Text);
                    grid.Rows[i].Tag = row;
                }

                Filter();
            }
            finally
            {
                loading = false;
            }

            Status.Text = documents[key].Rows.Count + L.T(" Nachrichten. Steuerzeichen erhalten; \\n für Zeilenumbrüche verwenden.", " messages. Preserve control sequences and use \\n for line breaks.");
        }

        void Filter()
        {
            grid.CurrentCell = null;
            foreach (DataGridViewRow row in grid.Rows)
                row.Visible = (Convert.ToString(row.Cells[0].Value) + " " + Convert.ToString(row.Cells[1].Value)).IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Save()
        {
            grid.EndEdit();
            if (changed.Count == 0)
            {
                Status.Text = L.T("Noch keine Textänderungen zum Speichern.", "No text changes to save yet.");
                return;
            }

            string folder = Folder(Path.Combine(Path.GetDirectoryName(source), "MUR_EDITED"));
            if (folder == null)
                return;
            var encoded = changed.ToDictionary(k => k, k => documents[k].Encode());
            var outputs = new Dictionary<string, byte[]>();
            foreach (string path in changed.Select(k => owners[k]).Distinct())
            {
                string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(path)));
                if (archives.Keys.Any(p => dest.Equals(Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase)))
                    throw new IOException(L.T("Wähle einen separaten Ausgabeordner.", "Choose a separate output folder."));
                var archive = archives[path];
                if (archive == null) outputs.Add(dest, encoded.First(p => owners[p.Key] == path).Value);
                else
                {
                    var copy = new StudioArchiveCopy(path, archive.Original);
                    foreach (var entry in encoded.Where(p => owners[p.Key] == path)) copy.Files[entryKeys[entry.Key]].Data = entry.Value;
                    outputs.Add(dest, ArchiveCopyExport.PrepareCopy(path, archive.Original, copy.Build(), dest));
                }
            }
            Directory.CreateDirectory(folder);
            BackupManager.WriteBatch(outputs);
            dirty = false;
            Status.Text = L.T("Kopien gespeichert: ", "Saved copies: ") + folder + L.T("\nTextlängen im Spiel prüfen; diese Tabelle simuliert keine Menü-Zeilenumbrüche.", "\nCheck message lengths in-game; this table does not simulate menu layout wrapping.");
            ExportHelp.Show(this, folder);
        }
    }
}
