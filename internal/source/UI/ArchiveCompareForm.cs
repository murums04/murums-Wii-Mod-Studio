using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ArchiveCompareForm : StudioToolForm
    {
        StudioArchiveCopy target, donor;
        readonly CheckedListBox files = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            HorizontalScrollbar = true,
            IntegralHeight = false
        };
        readonly Button compare, save, openBase, openEdited;
        readonly ResourcePreviewPanel before = new ResourcePreviewPanel(), after = new ResourcePreviewPanel();
        public ArchiveCompareForm() : base("MKWii Archive Compare Tool", "Compare resource contents • Select changed entries • Save a combined archive copy", "MenuSingle.szs ↔ MUR_EDITED/MenuSingle.szs · *.szs / *.arc / *.u8")
        {
            openBase = Action("Open base archive…", "Step 1: choose the original archive.", delegate
            {
                string path = OpenPath("Wii archive|*.szs;*.arc;*.u8");
                if (path != null) LoadBase(path);
            });
            openEdited = Action("Open edited archive…", "Step 2: choose the edited copy with exactly the same filename.", delegate
            {
                if (target == null) return;
                string folder = Path.Combine(Path.GetDirectoryName(target.Source), "MUR_EDITED");
                using (var picker = new OpenFileDialog {
                    Title = "Open edited " + Path.GetFileName(target.Source),
                    Filter = EditedFilter(), FileName = Path.GetFileName(target.Source), CheckFileExists = true,
                    InitialDirectory = Directory.Exists(folder) ? folder : Path.GetDirectoryName(target.Source) })
                    if (ToolArchiveFilters.Show(picker, this) == DialogResult.OK) LoadEdited(picker.FileName);
            });
            compare = Action("Compare entries", "Compare uncompressed entry bytes. Archive compression and file order do not create false differences.", Compare);
            save = ExportAction("Save selected changes…", "Replace checked resources in a copy of the base archive. BRLYT and BRLAN resources are copied as complete files.", Save);
            var fileColumn = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
            fileColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fileColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            fileColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            fileColumn.Controls.Add(new Label { Text = "Changed resources\nCheck entries to copy", Dock = DockStyle.Fill }, 0, 0);
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
                SplitterDistance = 300
            };
            split.Panel1.Controls.Add(fileColumn);
            before.ShowResource("Base archive", null);
            after.ShowResource("Edited archive", null);
            split.Panel2.Controls.Add(views);
            Body.Controls.Add(split);
            files.SelectedIndexChanged += delegate
            {
                Guard(delegate
                {
                    string key = files.SelectedItem as string;
                    before.ShowResource("Base: " + key, key == null ? null : target.Files[key].Data);
                    after.ShowResource("Edited: " + key, key == null ? null : donor.Files[key].Data);
                });
            };
            StudioUx.SetHelp(files, "Check changed files to take from the edited archive. Added and removed paths are reported but are not merged automatically.");
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
        }
        void Reset()
        {
            files.Items.Clear();
            before.ShowResource("Base archive", null);
            after.ShowResource("Edited archive", null);
            openEdited.Enabled = target != null;
            bool ready = target != null && donor != null;
            PackSelection.SourceStep(this, target == null ? openBase.Text : openEdited.Text,
                target == null ? "1. Open base archive: choose the original file.\n2. Open edited archive: choose a separate copy with the same filename.\nThe comparison unlocks after both files are loaded."
                : "Open edited archive: choose " + Path.GetFileName(target.Source) + ".\nThe file dialog starts in MUR_EDITED when available.\nThe comparison unlocks after the edited copy is loaded.", ready);
            compare.Enabled = ready;
            save.Enabled = false;
            Status.Text = "Base: " + (target == null ? "not selected" : target.Source) + "\nEdited: " + (donor == null ? "not selected" : donor.Source);
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
            Status.Text = files.Items.Count + " changed • " + same + " identical • " + added.Length + " added • " + removed.Length + " removed. Only existing changed paths can be selected. Whole-file replacement; no field-level merge.";
            save.Enabled = files.Items.Count > 0;
            if (added.Length + removed.Length > 0)
                murumsWiiModStudio.StudioMessageBox.Show(this, "Added (not merged):\n" + string.Join("\n", added) + "\n\nRemoved (preserved in base):\n" + string.Join("\n", removed), "Path differences");
        }

        void Save()
        {
            if (files.CheckedItems.Count == 0)
                throw new InvalidOperationException("Check the changed resources you want to copy first.");
            string p = SavePath(Path.GetFileName(target.Source), "Wii archive|*.szs;*.arc;*.u8");
            if (p == null)
                return;
            var copy = new StudioArchiveCopy(target.Source, target.Original);
            foreach (string key in files.CheckedItems)
                copy.Files[key].Data = donor.Files[key].Data;
            copy.Save(p, donor.Source);
            Status.Text = "Saved combined copy: " + p;
            ToolStatus.Set(this, true);
        }
    }
}
