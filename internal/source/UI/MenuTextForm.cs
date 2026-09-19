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
        readonly TextBox sample = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 16)
        };
        public MenuTextForm() : base("MKWii Menu Text Tool", "Add message sources • Search messages • Edit text and save copies", "RR: UIAssets.szs / RaceAssets.szs · Original: language archives (_E / _U / _J) · *.bmg")
        {
            Action("Add archive…", "Add multiple language archives or BMG files; existing edits are kept.", Open).Name = "PackSourceAction";
            Action("Clear selection", "Clear loaded files.", delegate {
                grid.EndEdit();
                if (dirty && StudioMessageBox.Show(this, "Discard unsaved changes and clear loaded files?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                resource.Items.Clear(); grid.Rows.Clear(); documents.Clear(); changed.Clear(); archives.Clear(); owners.Clear(); entryKeys.Clear();
                source = activeKey = null; dirty = false; save.Enabled = false; sample.Clear(); PackSelection.SourceCleared(this);
            });
            Actions.Controls.Add(new Label { Text = L.T("Nachrichten-Datei", "Message resource"), AutoSize = true, Margin = new Padding(3, 8, 4, 0) });
            Actions.Controls.Add(resource);
            resource.SelectedIndexChanged += delegate
            {
                Guard(LoadMessages);
            };
            Actions.Controls.Add(new Label { Text = "Find ID / text", AutoSize = true, Margin = new Padding(8, 10, 2, 0) });
            Actions.Controls.Add(search);
            search.TextChanged += delegate
            {
                Filter();
            };
            save = ExportAction("Save edited copy…", "Encode changed BMG resources and save a separate archive copy. Other archive entries remain unchanged.", Save);
            grid.Columns.Add("id", "Message ID");
            grid.Columns[0].ReadOnly = true;
            grid.Columns[0].FillWeight = 18;
            grid.Columns.Add("text", "Message text (BMG escape syntax)");
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
                Text = "Text preview — system font; game codes remain visible",
                Dock = DockStyle.Bottom,
                Height = 155,
                Padding = new Padding(8)
            };
            sampleGroup.Controls.Add(sample);
            Body.Controls.Add(grid);
            Body.Controls.Add(sampleGroup);
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
            StudioUx.SetHelp(grid, "Edit text in the second column. Preserve escapes such as \\z{...}, \\c{...} and \\n. Messages are shown on one row; use \n escapes for in-game line breaks.");
            StudioUx.SetHelp(search, "Filter message IDs and text without changing any messages.");
            Status.Text = "Uses Wiimms BMG Tool. Escapes represent colours, variables and game symbols; keep them intact.";
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                grid.EndEdit();
                if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Close without saving these text changes?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        void UpdateSample()
        {
            sample.Text = grid.CurrentRow == null ? "Select a message to preview it." : Convert.ToString(grid.CurrentRow.Cells[1].Value).Replace("\\n", Environment.NewLine);
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
                    throw new IOException("An archive with this name is already loaded.");
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
                Status.Text = skipped.Count == 0 ? "No new message sources selected."
                    : "No BMG messages found. RR: add UIAssets.szs / RaceAssets.szs. Skipped: " + String.Join(", ", skipped);
                return;
            }
            source = archives.Keys.First(); PackSelection.SourceLoaded(this);
            if (resource.SelectedIndex < 0) resource.SelectedIndex = 0;
            save.Enabled = true;
            Status.Text = archives.Count + " message sources loaded." + (skipped.Count == 0 ? "" : " No BMG; skipped: " + String.Join(", ", skipped));
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

            Status.Text = documents[key].Rows.Count + " messages. Preserve control sequences and use \\n for line breaks.";
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
                Status.Text = "No text changes to save yet.";
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
                if (dest.Equals(path, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a separate output folder.");
                var archive = archives[path];
                if (archive == null) outputs.Add(dest, encoded.First(p => owners[p.Key] == path).Value);
                else
                {
                    var copy = new StudioArchiveCopy(path, archive.Original);
                    foreach (var entry in encoded.Where(p => owners[p.Key] == path)) copy.Files[entryKeys[entry.Key]].Data = entry.Value;
                    outputs.Add(dest, copy.Build());
                }
            }
            Directory.CreateDirectory(folder);
            foreach (var output in outputs) BackupManager.WriteAllBytesSafely(output.Key, output.Value);
            dirty = false;
            Status.Text = "Saved copies: " + folder + "\nCheck message lengths in-game; this table does not simulate menu layout wrapping.";
            ExportHelp.Show(this, folder);
        }
    }
}
