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
        readonly EditHistory<ArchiveMerge.State> history = new EditHistory<ArchiveMerge.State>(ArchiveMerge.State.Same);
        readonly StudioUndoRedo undoRedo;
        ArchiveMerge.State savedState;
        readonly Button add, save;
        internal ArchiveMergeForm() : base("RR-MKWii Mod Merge Tool",
            L.T("Gemeinsames Original + bearbeitete Kopien • Konflikte auswählen • Neue Kopie exportieren",
                "Shared original + edited copies • Resolve conflicts • Export a new copy"))
        {
            var open = Action(L.T("Originalarchiv öffnen…", "Open base archive…"), "", Open);
            open.Name = "PackSourceAction";
            StudioActions.Icon(open, StudioIcon.Open);
            add = Action(L.T("Bearbeitungen hinzufügen…", "Add edited copies…"), "", Add);
            add.Name = "PackEditAction";
            save = ExportAction(L.T("Kombinierte Kopie speichern…", "Save merged copy…"), "", Save);
            undoRedo = new StudioUndoRedo(this, Actions, delegate { return merge != null && history.CanUndo; },
                delegate { return merge != null && history.CanRedo; }, delegate { RestoreHistory(false); }, delegate { RestoreHistory(true); });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = L.T("Ressource", "Resource"), ReadOnly = true, FillWeight = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = L.T("Status", "Status"), ReadOnly = true, FillWeight = 20 });
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Choice", HeaderText = L.T("Übernehmen", "Use"), FillWeight = 25, FlatStyle = FlatStyle.Flat });
            description.AutoSize = true;
            description.Dock = DockStyle.Fill;
            description.Padding = new Padding(6, 4, 6, 8);
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.Controls.Add(description, 0, 0);
            workspace.Controls.Add(grid, 0, 1);
            Body.Controls.Add(workspace);
            description.Text = L.T("Alle Bearbeitungen müssen vom selben Original stammen. Unterschiedliche Ressourcen werden kombiniert.\nBei Änderungen derselben Ressource wählst du eine Version; keine versteckte Feldzusammenführung.",
                "All edits must share the same original. Independent resources are combined.\nChoose a version for conflicting resources; no hidden field-level merge.");
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (populating || merge == null || e.RowIndex < 0 || e.ColumnIndex != 2) return;
                var cell = (DataGridViewComboBoxCell)grid.Rows[e.RowIndex].Cells[2];
                merge.Entries[e.RowIndex].Choice = cell.Items.IndexOf(cell.Value) - 1;
                history.Record(merge.Capture());
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
            LoadOriginal(path);
        }
        void LoadOriginal(string path)
        {
            var next = new ArchiveMerge(path);
            merge = next; savedState = merge.Capture(); history.Reset(savedState);
            dirty = false; PopulateGrid(); PackSelection.SourceLoaded(this); RefreshStatus();
        }
        void Add()
        {
            if (merge == null) return;
            using (var picker = new OpenFileDialog { Multiselect = true,
                Filter = "Matching archive|" + Path.GetFileName(merge.Original.Source), InitialDirectory = Path.GetDirectoryName(merge.Original.Source) })
            {
                if (ToolArchiveFilters.Show(picker, this) != DialogResult.OK) return;
                merge.Add(ToolArchiveFilters.SelectedFiles(picker)); history.Record(merge.Capture()); PopulateGrid();
            }
        }
        void RestoreHistory(bool forward)
        {
            grid.EndEdit();
            merge.Restore(forward ? history.Redo() : history.Undo());
            PopulateGrid();
        }
        void PopulateGrid()
        {
            populating = true;
            try
            {
                grid.Rows.Clear();
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
            }
            finally { populating = false; }
            RefreshStatus();
        }
        void RefreshStatus()
        {
            add.Enabled = merge != null;
            dirty = merge != null && !ArchiveMerge.State.Same(savedState, merge.Capture());
            undoRedo.Refresh();
            int conflicts = merge == null ? 0 : merge.Entries.Count(e => e.Choice < 0);
            save.Enabled = merge != null && merge.Variants.Count > 0 && conflicts == 0;
            bool hasEdits = merge != null && merge.Variants.Count > 0;
            grid.Visible = Footer.Visible = hasEdits;
            add.Text = hasEdits ? L.T("Weitere Bearbeitung…", "Add another edit…") : L.T("Bearbeitete Kopien wählen…", "Choose edited copies…");
            add.BackColor = merge != null && !hasEdits ? DarkTheme.Accent2 : DarkTheme.Panel2;
            if (merge != null)
                description.Text = L.T("Original: ", "Original: ") + Path.GetFileName(merge.Original.Source) + "\n"
                    + (!hasEdits ? L.T("Nächster Schritt: Kopien auswählen, die aus diesem Original entstanden sind.", "Next: choose edited copies made from this original.")
                        : conflicts > 0 ? L.F("{0} Konflikte klären → für jede betroffene Ressource eine Version wählen.", "Resolve {0} conflicts → choose a version for each affected resource.", conflicts)
                        : L.T("Auswahl geprüft → kombinierte Kopie speichern. Das Original bleibt erhalten.", "Selection ready → save the merged copy. The original is kept."));
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
                BackupManager.WriteAllBytesSafely(path, bytes); savedState = merge.Capture(); dirty = false;
                Status.Text = L.T("Kombinierte Kopie gespeichert: ", "Merged copy saved: ") + path;
                StudioMessageBox.ShowPath(this, path, L.T("Kombinierte Kopie gespeichert.", "Merged copy saved."), L.T("Gespeichert", "Saved"));
            }
        }
    }
}

