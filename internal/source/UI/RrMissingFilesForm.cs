using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class RrMissingFilesForm : Form
    {
        readonly string pack, patterns;
        readonly CharacterDefinition character;
        readonly int slot;
        readonly bool multiple;
        string rrRoot;
        List<RrMissingFile> plan = new List<RrMissingFile>();
        readonly CheckedListBox list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        readonly ComboBox region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 135 };
        readonly Label detail = new Label { Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false };
        readonly Label total = new Label { AutoSize = true, Dock = DockStyle.Fill };
        readonly Button copy = new Button { AutoSize = true, MinimumSize = new Size(195, 36) };
        internal string[] SelectedPaths = new string[0];

        internal RrMissingFilesForm(string folder, string filter, string source, CharacterDefinition selected = null, int selectedSlot = 0, bool allowMultiple = true)
        {
            multiple = allowMultiple; pack = folder; patterns = filter; rrRoot = source; character = selected; slot = selectedSlot;
            Text = L.T("Fehlende RR-Dateien ergänzen", "Add missing RR files");
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(820, 630); MinimumSize = new Size(680, 550);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(16) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(StudioChrome.Header(Text, L.T("RR-Dateien als Arbeitskopien in dein Pack übernehmen", "Copy RR files into your pack for editing")), 0, 0);
            detail.Padding = new Padding(4, 12, 4, 10);
            layout.Controls.Add(detail, 0, 1);
            var sources = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var browse = new Button { Text = L.T("RR-Ordner wählen…", "Choose RR folder…"), AutoSize = true, Height = 34 };
            browse.Click += delegate {
                using (var picker = new FolderPickerDialog { Description = L.T("Retro-Rewind-Installation wählen", "Choose Retro Rewind installation"), SelectedPath = rrRoot })
                    if (picker.ShowDialog(this) == DialogResult.OK) {
                        rrRoot = RetroRewindSource.Resolve(picker.SelectedPath);
                        if (rrRoot == null) { ShowError(L.T("RR-Ordner mit UI und Assets auswählen.", "Choose the RR folder containing UI and Assets.")); return; }
                        RefreshPlan();
                    }
            };
            sources.Controls.Add(browse);
            sources.Controls.Add(new Label { Text = L.T("Spielregion", "Game region"), AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
            region.Items.AddRange(new object[] { L.T("Bitte wählen…", "Choose…"), "PAL (E)", "USA (U)", "Japan (J)" });
            string inferred = RrMissingFiles.InferRegion(pack);
            region.SelectedIndex = inferred == "E" ? 1 : inferred == "U" ? 2 : inferred == "J" ? 3 : 0;
            sources.Controls.Add(region);
            region.SelectedIndexChanged += delegate { RefreshPlan(); };
            layout.Controls.Add(sources, 0, 2);
            layout.Controls.Add(list, 0, 3);
            total.Padding = new Padding(4, 6, 4, 6); layout.Controls.Add(total, 0, 4);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            copy.Text = L.T("Ergänzen und laden", "Add and load");
            copy.Click += delegate { CopySelected(); };
            var cancel = new Button { Text = L.T("Abbrechen", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Height = 36 };
            var own = new Button { Text = L.T("Eigene Dateien wählen…", "Choose your own files…"), AutoSize = true, DialogResult = DialogResult.Ignore, Height = 36 };
            buttons.Controls.Add(copy); buttons.Controls.Add(own); buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 5); Controls.Add(layout);
            CancelButton = cancel; AcceptButton = copy;
            list.ItemCheck += delegate(object sender, ItemCheckEventArgs args) {
                if (!multiple && args.NewValue == CheckState.Checked)
                    foreach (int index in list.CheckedIndices.Cast<int>().Where(i => i != args.Index).ToArray()) list.SetItemChecked(index, false);
                if (IsHandleCreated) BeginInvoke((Action)UpdateTotal);
            };
            DarkTheme.Apply(this); copy.BackColor = DarkTheme.Accent;
            RefreshPlan();
        }

        void RefreshPlan()
        {
            list.Items.Clear(); plan.Clear();
            detail.Text = L.T("Studio kopiert nur angehakte, fehlende Dateien aus RR in dieses Pack. Vorhandene Pack-Dateien und RR-Originale bleiben erhalten. Danach werden die Dateien im Tool geladen.",
                "Studio copies only checked, missing RR files into this pack. Existing pack files and RR originals are kept. The files are then loaded in the tool.")
                + Environment.NewLine + L.T("Ziel: ", "Destination: ") + pack;
            if (!multiple) detail.Text += Environment.NewLine + L.T("Eine Datei auswählen, die anschließend geöffnet wird.", "Select one file to open after copying.");
            if (String.IsNullOrEmpty(rrRoot)) { total.Text = L.T("Zuerst den RR-Installationsordner auswählen.", "Choose the RR installation folder first."); copy.Enabled = false; return; }
            try
            {
                string selectedRegion = region.SelectedIndex == 1 ? "E" : region.SelectedIndex == 2 ? "U" : region.SelectedIndex == 3 ? "J" : null;
                plan = RrMissingFiles.Plan(rrRoot, pack, patterns, selectedRegion, character, slot);
                bool needsRegion = character == null && RrMissingFiles.Plan(rrRoot, pack, patterns, null).Any(p => new[] { "Title_U.szs", "Race_U.szs", "Common_U.szs" }.Contains(Path.GetFileName(p.Source)));
                region.Enabled = needsRegion;
                if (needsRegion && selectedRegion == null) {
                    total.Text = L.T("Spielregion wählen: Spracharchive werden im Pack passend benannt.", "Choose game region: language archives will be named for your pack.");
                    copy.Enabled = false; return;
                }
                foreach (var file in plan)
                    list.Items.Add(file.RelativePath + (file.Exists ? L.T("  • vorhanden, bleibt erhalten", "  • existing, kept") : "  • " + (file.Size / 1048576.0).ToString("0.0") + " MB"), multiple || list.Items.Count == 0);
                detail.Text += Environment.NewLine + L.T("Quelle: ", "Source: ") + rrRoot;
                UpdateTotal();
            }
            catch (Exception error) { total.Text = error.Message; copy.Enabled = false; }
        }
        void UpdateTotal()
        {
            var selected = list.CheckedIndices.Cast<int>().Where(i => i < plan.Count).Select(i => plan[i]).ToArray();
            int missing = selected.Count(f => !f.Exists);
            total.Text = missing + L.T(" Dateien ergänzen; ", " files to add; ") + selected.Count(f => f.Exists) + L.T(" vorhandene Dateien laden.", " existing files to load.");
            copy.Enabled = selected.Length > 0;
        }
        void CopySelected()
        {
            try
            {
                SelectedPaths = RrMissingFiles.CopyMissing(pack, list.CheckedIndices.Cast<int>().Select(i => plan[i]));
                DialogResult = DialogResult.OK;
            }
            catch (Exception error) { ShowError(error.Message); }
        }
        void ShowError(string text) { StudioMessageBox.Show(this, text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    internal static class RrSourceRecovery
    {
        internal static string Patterns(Form form)
        {
            if (form == null) return null;
            switch (form.GetType().Name)
            {
                case "RaceHudForm": return ToolArchiveFilters.RacePatterns;
                case "RaceEffectsForm": return "Common.szs;CommonAssets.szs";
                case "FontChangerForm": return "Font.szs;" + ToolArchiveFilters.MenuPatterns + ";" + ToolArchiveFilters.RacePatterns;
                case "GameHudForm": return ToolArchiveFilters.LayoutMenuPatterns + ";" + ToolArchiveFilters.RacePatterns;
                case "MenuTextForm": return ToolArchiveFilters.Messages.Split('|')[1];
                case "MenuTextureForm": return ToolArchiveFilters.LayoutMenuPatterns;
                case "RetroRewindGifWizard": return ToolArchiveFilters.Backgrounds.Split('|')[1];
                case "MenuModelsForm": return "Earth.szs;BackModel.szs;globe.arc";

                case "ArchiveCompareForm": case "ArchiveMergeForm": case "PackWorkbenchForm": return "*.szs;globe.arc";
                default: return null;
            }
        }
        internal static string[] Choose(Form owner, string patterns, string rrRoot = null, CharacterDefinition character = null, int slot = 0, bool multiple = true)
        {
            string folder = PackSelection.Folder(owner);
            if (String.IsNullOrEmpty(folder)) return null;
            string[] found = new string[0];
            try { found = String.IsNullOrEmpty(rrRoot) ? RetroRewindSource.Discover() : new[] { rrRoot }; }
            catch (System.IO.IOException) { }
            catch (UnauthorizedAccessException) { }
            using (var dialog = new RrMissingFilesForm(folder, patterns, found.Length == 1 ? found[0] : null, character, slot, multiple))
            {
                var result = dialog.ShowDialog(owner);
                return result == DialogResult.OK ? dialog.SelectedPaths : result == DialogResult.Ignore ? null : new string[0];
            }
        }
    }
}
