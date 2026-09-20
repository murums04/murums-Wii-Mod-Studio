using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ArchiveMergeForm : StudioToolForm
    {
        ArchiveMerge merge;
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        readonly Label description = new Label { Dock = DockStyle.Top, Height = 58 };
        bool dirty, populating;
        readonly Button add, save;
        internal ArchiveMergeForm() : base("RR-MKWii Mod Merge Tool",
            L.T("Gemeinsames Original + bearbeitete Kopien • Konflikte auswählen • Neue Kopie exportieren",
                "Shared original + edited copies • Resolve conflicts • Export a new copy"))
        {
            Action(L.T("Originalarchiv öffnen…", "Open base archive…"), "", Open).Name = "PackSourceAction";
            add = Action(L.T("Bearbeitungen hinzufügen…", "Add edited copies…"), "", Add);
            save = ExportAction(L.T("Kombinierte Kopie speichern…", "Save merged copy…"), "", Save);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = L.T("Ressource", "Resource"), ReadOnly = true, FillWeight = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = L.T("Status", "Status"), ReadOnly = true, FillWeight = 20 });
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Choice", HeaderText = L.T("Übernehmen", "Use"), FillWeight = 25, FlatStyle = FlatStyle.Flat });
            Body.Controls.Add(grid); Body.Controls.Add(description);
            description.Text = L.T("Alle Bearbeitungen müssen vom selben Original stammen. Unterschiedliche Ressourcen werden kombiniert.\nBei Änderungen derselben Ressource wählst du eine Version; keine versteckte Feldzusammenführung.",
                "All edits must share the same original. Independent resources are combined.\nChoose a version for conflicting resources; no hidden field-level merge.");
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (populating || merge == null || e.RowIndex < 0 || e.ColumnIndex != 2) return;
                dirty = true;
                var cell = (DataGridViewComboBoxCell)grid.Rows[e.RowIndex].Cells[2];
                merge.Entries[e.RowIndex].Choice = cell.Items.IndexOf(cell.Value) - 1;
                RefreshStatus();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (dirty && !ConfirmDiscard()) e.Cancel = true;
            };
            DarkTheme.StyleGrid(grid);
            Finish();
            PackSelection.SourceStep(this, L.T("Originalarchiv öffnen…", "Open base archive…"), description.Text, false);
            RefreshStatus();
        }
        bool ConfirmDiscard()
        {
            return StudioMessageBox.Show(this, L.T("Aktuelle Zusammenführung verwerfen?", "Discard the current merge?"),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        void Open()
        {
            string path = OpenPath("Wii archives|*.szs;*.arc;*.u8");
            if (path == null) return;
            if (dirty && !ConfirmDiscard()) return;
            var next = new ArchiveMerge(path);
            merge = next; dirty = false; grid.Rows.Clear(); PackSelection.SourceLoaded(this); RefreshStatus();
        }
        void Add()
        {
            if (merge == null) return;
            using (var picker = new OpenFileDialog { Multiselect = true,
                Filter = "Matching archive|" + Path.GetFileName(merge.Original.Source), InitialDirectory = Path.GetDirectoryName(merge.Original.Source) })
            {
                if (ToolArchiveFilters.Show(picker, this) != DialogResult.OK) return;
                merge.Add(ToolArchiveFilters.SelectedFiles(picker)); dirty = true; populating = true; grid.Rows.Clear();
                foreach (var entry in merge.Entries)
                {
                    int row = grid.Rows.Add(entry.Path, entry.Conflict ? L.T("Konflikt", "Conflict") : L.T("Eindeutig", "Unambiguous"));
                    var cell = (DataGridViewComboBoxCell)grid.Rows[row].Cells[2];
                    cell.Items.Add(L.T("Bitte auswählen", "Choose a version"));
                    cell.Items.Add(L.T("Original behalten", "Keep original"));
                    for (int i = 0; i < entry.Candidates.Count; i++)
                        cell.Items.Add((i + 1) + ": " + (entry.Candidates[i] == null ? L.T("Entfernen — ", "Remove — ") : "") + entry.Sources[i]);
                    cell.Value = cell.Items[entry.Choice + 1];
                }
                populating = false; RefreshStatus();
            }
        }
        void RefreshStatus()
        {
            add.Enabled = merge != null;
            int conflicts = merge == null ? 0 : merge.Entries.Count(e => e.Choice < 0);
            save.Enabled = merge != null && merge.Variants.Count > 0 && conflicts == 0;
            Status.Text = merge == null ? L.T("Gemeinsames Original laden.", "Load the shared original.")
                : merge.Entries.Count + L.T(" Änderungen • ungelöste Konflikte: ", " changes • unresolved conflicts: ") + conflicts;
        }
        void Save()
        {
            if (merge == null) return;
            grid.EndEdit();
            byte[] bytes = merge.Build();
            using (var picker = new SaveFileDialog { FileName = Path.GetFileName(merge.Original.Source), Filter = "Matching archive|" + Path.GetFileName(merge.Original.Source) })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                string path = Path.GetFullPath(picker.FileName);
                if (merge.Variants.Select(v => v.Source).Concat(new[] { merge.Original.Source }).Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException(L.T("Eine neue Ausgabedatei wählen.", "Choose a separate output file."));
                BackupManager.WriteAllBytesSafely(path, bytes); dirty = false;
                Status.Text = L.T("Kombinierte Kopie gespeichert: ", "Merged copy saved: ") + path;
            }
        }
    }
}

