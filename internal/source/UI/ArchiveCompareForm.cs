using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ArchiveCompareForm : StudioToolForm
    {
        StudioArchiveCopy target, donor;
        readonly EditHistory<string[]> history = new EditHistory<string[]>((a, b) => a.SequenceEqual(b));
        readonly StudioUndoRedo undoRedo;
        bool restoring;
        readonly CheckedListBox files = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            HorizontalScrollbar = true,
            IntegralHeight = false
        };
        readonly Button compare, save, openBase, openEdited, report, cancelReport;
        CancellationTokenSource reporting;
        bool closeAfterReport;
        readonly ResourcePreviewPanel before = new ResourcePreviewPanel(), after = new ResourcePreviewPanel();
        readonly StudioReadOnlyText changes = new StudioReadOnlyText { Dock = DockStyle.Fill, Name = "SemanticChanges", WordWrap = true };
        public ArchiveCompareForm() : base("MKWii Archive Compare Tool", L.T("Ressourcen vergleichen • Änderungen wählen • Kombinierte Kopie speichern", "Compare resources • Select changes • Save a combined copy"), "MenuSingle.szs ↔ MUR_EDITED/MenuSingle.szs · *.szs / *.arc / *.u8")
        {
            openBase = Action(L.T("Original öffnen…", "Open original…"), L.T("Schritt 1: das unveränderte Archiv wählen.", "Step 1: choose the original archive."), delegate
            {
                string path = OpenPath("Wii archive|*.szs;*.arc;*.u8");
                if (path != null) LoadBase(path);
            });
            openEdited = Action(L.T("Bearbeitung öffnen…", "Open edited…"), L.T("Schritt 2: bearbeitete Kopie mit genau demselben Dateinamen wählen.", "Step 2: choose the edited copy with exactly the same filename."), delegate
            {
                if (target == null) return;
                string folder = Path.Combine(Path.GetDirectoryName(target.Source), "MUR_EDITED");
                using (var picker = new OpenFileDialog {
                    Title = L.T("Bearbeitung öffnen: ", "Open edited: ") + Path.GetFileName(target.Source),
                    Filter = EditedFilter(), FileName = Path.GetFileName(target.Source), CheckFileExists = true,
                    InitialDirectory = Directory.Exists(folder) ? folder : Path.GetDirectoryName(target.Source) })
                    if (ToolArchiveFilters.Show(picker, this) == DialogResult.OK) LoadEdited(ToolArchiveFilters.SelectedFile(picker));
            });
            openBase.Name = openEdited.Name = "PackSourceAction";
            compare = Action(L.T("Vergleich aktualisieren", "Refresh comparison"), L.T("Unkomprimierte Ressourcen vergleichen. Kompression und Dateireihenfolge erzeugen keine falschen Unterschiede.", "Compare uncompressed resources. Compression and file order do not create false differences."), Compare);
            StudioActions.Icon(compare, StudioIcon.Refresh);
            save = ExportAction(L.T("Auswahl als Kopie speichern…", "Save selected copy…"), L.T("Angehakte Ressourcen in einer Kopie des Originals ersetzen. BRLYT und BRLAN werden als ganze Dateien übernommen.", "Replace checked resources in a copy of the original. BRLYT and BRLAN are copied as complete files."), Save);
            report = Action(L.T("Vergleichsbericht speichern…", "Save comparison report…"), L.T("Alle Pfade, Größen und begrenzte Wertänderungen als TXT speichern. Die Übernahmeauswahl ist unabhängig davon.", "Save all paths, sizes and bounded value changes as TXT. The copy selection is independent."), SaveReport);
            report.Name = "SaveComparisonReport";
            cancelReport = Action(L.T("Bericht abbrechen", "Cancel report"), L.T("Die Berichtsausgabe abbrechen. Eine bestehende Ausgabe bleibt erhalten.", "Cancel report output. An existing output is preserved."), delegate { if (reporting != null) reporting.Cancel(); });
            cancelReport.Name = "CancelComparisonReport";
            cancelReport.Enabled = false;
            undoRedo = new StudioUndoRedo(this, Actions, delegate { return reporting == null && history.CanUndo; }, delegate { return reporting == null && history.CanRedo; },
                delegate { RestoreSelection(history.Undo()); }, delegate { RestoreSelection(history.Redo()); });
            files.ItemCheck += delegate(object sender, ItemCheckEventArgs e)
            {
                if (restoring) return;
                history.Record(files.Items.Cast<string>().Where((item, index) => index == e.Index
                    ? e.NewValue == CheckState.Checked : files.GetItemChecked(index)).ToArray());
                int selected = files.CheckedItems.Count + (e.NewValue == CheckState.Checked ? 1 : 0) - (files.GetItemChecked(e.Index) ? 1 : 0);
                save.Enabled = selected > 0;
                undoRedo.Refresh();
            };
            var fileColumn = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
            fileColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fileColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            fileColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            fileColumn.Controls.Add(new Label { Text = L.T("Geänderte Ressourcen\nAuswahl wird übernommen", "Changed resources\nChecked items will be copied"), Dock = DockStyle.Fill }, 0, 0);
            fileColumn.Controls.Add(files, 0, 1);
            var views = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
            };
            views.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));


            views.Controls.Add(before, 0, 0);
            views.Controls.Add(after, 1, 0);
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Width = 1000,
                SplitterDistance = 248,
                Panel1MinSize = 180,
                Panel2MinSize = 360
            };
            split.Panel1.Controls.Add(fileColumn);
            before.ShowResource(L.T("Originalarchiv", "Original archive"), null);
            after.ShowResource(L.T("Bearbeitete Kopie", "Edited copy"), null);
            var comparison = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            comparison.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            comparison.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            comparison.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            comparison.Controls.Add(views, 0, 0);
            string summaryTitle = L.T("Was hat sich geändert?", "What changed?");
            comparison.Controls.Add(new Label { Text = summaryTitle, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4, 6, 0, 0) }, 0, 1);
            changes.AccessibleName = summaryTitle;
            changes.Text = L.T("Eine geänderte Ressource auswählen.", "Select a changed resource.");
            comparison.Controls.Add(StudioReadOnlyText.Surface(changes), 0, 2);
            split.Panel2.Controls.Add(comparison);
            Body.Controls.Add(split);
            files.SelectedIndexChanged += delegate
            {
                Guard(delegate
                {
                    string key = files.SelectedItem as string;
                    changes.Text = key == null ? L.T("Eine geänderte Ressource auswählen.", "Select a changed resource.")
                        : ArchiveSemanticDiff.Describe(key, target.Files[key].Data, donor.Files[key].Data);
                    before.ShowResource(L.T("Original: ", "Original: ") + key, key == null ? null : target.Files[key].Data);
                    after.ShowResource(L.T("Bearbeitung: ", "Edited: ") + key, key == null ? null : donor.Files[key].Data);
                });
            };
            StudioUx.SetHelp(files, L.T("Geänderte Dateien für die Übernahme anhaken. Hinzugefügte und entfernte Pfade werden gemeldet, aber nicht automatisch zusammengeführt.", "Check changed files to take from the edited archive. Added and removed paths are reported but are not merged automatically."));
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (reporting == null) return;
                e.Cancel = true; closeAfterReport = true; reporting.Cancel();
            };
            Finish();
            Reset();
        }

        string EditedFilter()
        {
            if (target == null) throw new InvalidOperationException("Open the base archive first.");
            string name = Path.GetFileName(target.Source);
            return name + " (edited copy)|" + name;
        }

        void LoadBase(string path)
        {
            var loaded = new StudioArchiveCopy(path);
            target = loaded;
            donor = null;
            Reset();
        }

        void LoadEdited(string path)
        {
            if (target == null) throw new InvalidOperationException("Open the base archive first.");
            if (!Path.GetFileName(path).Equals(Path.GetFileName(target.Source), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose the edited copy named " + Path.GetFileName(target.Source) + ". Different archive names or regions cannot be compared.");
            if (Path.GetFullPath(path).Equals(target.Source, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose a separate edited copy, for example from MUR_EDITED.");
            var loaded = new StudioArchiveCopy(path);
            donor = loaded;
            Reset();
            Compare();
        }
        void Reset()
        {
            files.Items.Clear();
            history.Reset(new string[0]); undoRedo.Refresh();
            before.ShowResource(L.T("Originalarchiv", "Original archive"), null);
            after.ShowResource(L.T("Bearbeitete Kopie", "Edited copy"), null);
            openEdited.Enabled = target != null;
            bool ready = target != null && donor != null;
            PackSelection.SourceStep(this, target == null ? openBase.Text : openEdited.Text,
                target == null ? L.T("Original öffnen: die unveränderte Datei wählen.\nBearbeitung öffnen: eine separate Kopie mit demselben Namen wählen.\nBeide Dateien laden, dann vergleichen.", "Open original: choose the unchanged file.\nOpen edited: choose a separate copy with the same name.\nLoad both files, then compare.")
                : L.T("Bearbeitung öffnen: ", "Open edited: ") + Path.GetFileName(target.Source) + L.T(" wählen.\nDer Dateidialog beginnt wenn vorhanden in MUR_EDITED.\nAnschliessend die Ressourcen vergleichen.", ".\nThe file dialog starts in MUR_EDITED when available.\nThen compare the resources."), ready);
            compare.Enabled = ready;
            report.Enabled = ready && reporting == null;
            save.Enabled = false;
            Status.Text = L.T("Original: ", "Original: ") + (target == null ? L.T("nicht gewählt", "not selected") : target.Source) + L.T(" • Bearbeitung: ", " • Edited: ") + (donor == null ? L.T("nicht gewählt", "not selected") : donor.Source);
        }
        void Compare()
        {
            if (target == null || donor == null) throw new InvalidOperationException("Load both matching archives first.");
            files.Items.Clear();
            int same = 0;
            foreach (string key in target.Files.Keys.OrderBy(k => k))
                if (donor.Files.ContainsKey(key))
                {
                    if (target.Files[key].Data.SequenceEqual(donor.Files[key].Data))
                        same++;
                    else
                        files.Items.Add(key);
                }

            string[] added = donor.Files.Keys.Except(target.Files.Keys).ToArray(), removed = target.Files.Keys.Except(donor.Files.Keys).ToArray();
            Status.Text = L.F("{0} geändert • {1} identisch • {2} hinzugefügt • {3} entfernt. Vorhandene Ressourcen werden als ganze Dateien übernommen.",
                "{0} changed • {1} identical • {2} added • {3} removed. Existing resources are copied as whole files.", files.Items.Count, same, added.Length, removed.Length);
            save.Enabled = false;
            history.Record(new string[0]); undoRedo.Refresh();
            if (files.Items.Count > 0) files.SelectedIndex = 0;
            if (added.Length + removed.Length > 0)
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Hinzugefügt (nicht übernommen):\n", "Added (not merged):\n") + string.Join("\n", added) + L.T("\n\nEntfernt (im Original erhalten):\n", "\n\nRemoved (preserved in original):\n") + string.Join("\n", removed), L.T("Pfadunterschiede", "Path differences"));
        }

        void RestoreSelection(string[] selected)
        {
            restoring = true;
            try
            {
                for (int i = 0; i < files.Items.Count; i++) files.SetItemChecked(i, selected.Contains((string)files.Items[i]));
            }
            finally { restoring = false; }
            save.Enabled = files.CheckedItems.Count > 0;
            undoRedo.Refresh();
        }

        byte[] BuildSelection()
        {
            if (files.CheckedItems.Count == 0)
                throw new InvalidOperationException("Check the changed resources you want to copy first.");
            var copy = new StudioArchiveCopy(target.Source, target.Original);
            foreach (string key in files.CheckedItems) copy.Files[key].Data = (byte[])donor.Files[key].Data.Clone();
            return copy.Build();
        }

        void Save()
        {
            if (files.CheckedItems.Count == 0)
                throw new InvalidOperationException("Check the changed resources you want to copy first.");
            string p = SavePath(Path.GetFileName(target.Source), "Wii archive|*.szs;*.arc;*.u8");
            if (p == null)
                return;
            string output = Path.GetFullPath(p);
            if (output.Equals(donor.Source, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a separate output file.");
            ArchiveCopyExport.SaveCopy(target.Source, target.Original, BuildSelection(), output);
            Status.Text = L.T("Kombinierte Kopie gespeichert: ", "Saved combined copy: ") + p;
            StudioMessageBox.ShowPath(this, p, L.T("Zusammengeführte Kopie gespeichert.", "Combined copy saved."), Text);
            ToolStatus.Set(this, true);
        }

        async void SaveReport()
        {
            try
            {
                if (reporting != null || target == null || donor == null) return;
                string path = SavePath(Path.GetFileNameWithoutExtension(target.Source) + "_comparison.txt", L.T("Vergleichsbericht|*.txt", "Comparison report|*.txt"));
                if (path == null) return;
                await SaveReportTo(path);
            }
            catch (Exception error)
            {
                if (!IsDisposed) StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal async Task SaveReportTo(string path)
        {
            if (reporting != null || target == null || donor == null) return;
            var original = target; var edited = donor;
            reporting = new CancellationTokenSource();
            var pending = reporting;
            openBase.Enabled = openEdited.Enabled = compare.Enabled = save.Enabled = report.Enabled = files.Enabled = false;
            cancelReport.Enabled = true;
            undoRedo.Refresh();
            Status.Text = L.T("Vergleichsbericht wird erstellt…", "Creating comparison report…");
            var progress = new Progress<int>(value =>
            {
                if (!IsDisposed && reporting == pending) Status.Text = L.F("Vergleichsbericht: {0} %", "Comparison report: {0}%", value);
            });
            try
            {
                int lastProgress = -1;
                await Task.Run(() => ArchiveComparisonReport.Save(original, edited, path, pending.Token,
                    (done, total) =>
                    {
                        int percentage = total == 0 ? 100 : (int)((long)done * 100 / total);
                        if (percentage == lastProgress) return;
                        lastProgress = percentage;
                        ((IProgress<int>)progress).Report(percentage);
                    }));
                if (!IsDisposed) Status.Text = L.T("Vergleichsbericht gespeichert: ", "Comparison report saved: ") + path;
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) Status.Text = L.T("Bericht abgebrochen. Quellen und bestehende Ausgabe blieben erhalten.", "Report cancelled. Sources and existing output were preserved.");
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    Status.Text = L.T("Bericht nicht gespeichert. Quellen und bestehende Ausgabe blieben erhalten.", "Report was not saved. Sources and existing output were preserved.");
                    StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                reporting = null; pending.Dispose();
                if (!IsDisposed)
                {
                    openBase.Enabled = files.Enabled = true;
                    openEdited.Enabled = target != null;
                    compare.Enabled = report.Enabled = target != null && donor != null;
                    save.Enabled = files.CheckedItems.Count > 0;
                    cancelReport.Enabled = false;
                    undoRedo.Refresh();
                    if (closeAfterReport) Close();
                }
            }
        }
    }
}
