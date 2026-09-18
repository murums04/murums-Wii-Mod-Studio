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
        StudioArchiveCopy archive;
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
        public MenuTextForm() : base("Menu Text Tool", "Open a language archive or BMG • Search messages • Edit text and save a copy")
        {
            Action("Open archive / BMG…", "Open the language archive used by your pack, or an extracted BMG message file.", Open);
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
            if (dirty && murumsWiiModStudio.StudioMessageBox.Show(this, "Discard the unsaved text changes and open another file?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            string p = OpenPath("Messages / Wii archive|*.bmg;*.szs;*.arc;*.u8");
            if (p == null)
                return;
            var next = Path.GetExtension(p).Equals(".bmg", StringComparison.OrdinalIgnoreCase) ? null : new StudioArchiveCopy(p);
            var names = next == null ? new[]
            {
                Path.GetFileName(p)
            }

            : next.Files.Keys.Where(k => k.EndsWith(".bmg", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (names.Length == 0)
                throw new InvalidDataException("This archive contains no BMG messages. Open a language archive such as Common_E.szs or Race_E.szs.");
            var first = new BmgTextDocument(BmgTextDocument.Decode(next == null ? File.ReadAllBytes(p) : next.Files[names[0]].Data));
            archive = next;
            source = p;
            dirty = false;
            changed.Clear();
            documents.Clear();
            documents.Add(names[0], first);
            resource.Items.Clear();
            resource.Items.AddRange(names);
            resource.SelectedIndex = 0;
            save.Enabled = true;
        }

        void LoadMessages()
        {
            if (resource.SelectedItem == null)
                return;
            grid.EndEdit();
            string key = (string)resource.SelectedItem;
            if (!documents.ContainsKey(key))
                documents.Add(key, new BmgTextDocument(BmgTextDocument.Decode(archive.Files[key].Data)));
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

            string folder = Folder(Path.Combine(Path.GetDirectoryName(source), "TEXT_EDITED"));
            if (folder == null)
                return;
            string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(source)));
            if (string.Equals(dest, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a separate output folder.");
            var encoded = changed.ToDictionary(k => k, k => documents[k].Encode());
            Directory.CreateDirectory(folder);
            if (archive == null)
                BackupManager.WriteAllBytesSafely(dest, encoded[(string)resource.SelectedItem]);
            else
            {
                var copy = new StudioArchiveCopy(source, archive.Original);
                foreach (var p in encoded)
                    copy.Files[p.Key].Data = p.Value;
                copy.Save(dest);
            }

            dirty = false;
            Status.Text = "Saved: " + dest + "\nCheck message lengths in-game; this table does not simulate menu layout wrapping.";
        }
    }
}
