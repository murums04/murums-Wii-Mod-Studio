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
            HorizontalScrollbar = true
        };
        readonly Button compare, save;
        public ArchiveCompareForm() : base("Archive Compare Tool", "Compare resource contents • Select changed entries • Save a combined archive copy")
        {
            Action("Open base archive…", "This archive supplies all unchanged files in the combined copy.", delegate
            {
                string p = OpenPath("Wii archive|*.szs;*.arc;*.u8");
                if (p != null)
                {
                    var a = new StudioArchiveCopy(p);
                    target = a;
                    Reset();
                }
            });
            Action("Open edited archive…", "Select another version of the same archive as the source of selected changes.", delegate
            {
                string p = OpenPath("Wii archive|*.szs;*.arc;*.u8");
                if (p != null)
                {
                    var a = new StudioArchiveCopy(p);
                    donor = a;
                    Reset();
                }
            });
            compare = Action("Compare entries", "Compare uncompressed entry bytes. Archive compression and file order do not create false differences.", Compare);
            save = ExportAction("Save selected changes…", "Replace checked resources in a copy of the base archive. BRLYT and BRLAN resources are copied as complete files.", Save);
            var views = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var before = new ResourcePreviewPanel();
            var after = new ResourcePreviewPanel();
            views.Controls.Add(before, 0, 0);
            views.Controls.Add(after, 1, 0);
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Width = 1000,
                SplitterDistance = 300
            };
            split.Panel1.Controls.Add(files);
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

        void Reset()
        {
            files.Items.Clear();
            compare.Enabled = target != null && donor != null;
            save.Enabled = false;
            Status.Text = "Base: " + (target == null ? "not selected" : target.Source) + "\nEdited: " + (donor == null ? "not selected" : donor.Source);
        }

        void Compare()
        {
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
        }
    }
}
