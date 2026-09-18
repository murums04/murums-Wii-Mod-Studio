using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CustomPackMakerForm : StudioToolForm
    {
        readonly TextBox packName = new TextBox { Dock = DockStyle.Fill, MaxLength = 70 };
        readonly TextBox description = new TextBox { Dock = DockStyle.Fill, Multiline = true, MaxLength = 2000, ScrollBars = ScrollBars.Vertical };
        readonly ComboBox template = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox destination = new TextBox { Dock = DockStyle.Fill };
        readonly ListBox files = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
        readonly ListBox packs = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
        readonly Label preview = new Label { Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false };
        readonly Button create;

        internal CustomPackMakerForm() : base("MKWii Custom Pack Maker",
            L.T("ISO/WBFS empfohlen • Pack benennen • Dateien wählen", "ISO/WBFS recommended • Name your pack • Select files"))
        {
            Action("Browse ISO/WBFS…", "ISO/WBFS recommended: choose archives from the complete game file list.", delegate
            {
                using (var picker = new OpenFileDialog { Filter = "ISO / WBFS (recommended)|*.iso;*.wbfs;*.wia;*.ciso;*.wdf" })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        using (var dialog = new PackArchivePicker(picker.FileName))
                            if (dialog.ShowDialog(this) == DialogResult.OK)
                                AddFiles(dialog.ImportedPaths);
            });
            Action(L.T("Browse archives…", "Browse archives…"), "Add existing .szs files or globe.arc. Originals are kept.", delegate
            {
                using (var picker = new OpenFileDialog { Filter = "SZS / globe archives|*.szs;globe.arc", Multiselect = true })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        AddFiles(picker.FileNames);
            });
            Action(L.T("Auswahl entfernen", "Remove selected"), "Remove files from this list only.", delegate
            {
                foreach (object file in files.SelectedItems.Cast<object>().ToArray()) files.Items.Remove(file);
            });
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 10 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            AddRow(grid, 0, L.T("Pack-Name", "Pack name"), packName);
            AddRow(grid, 1, L.T("Pack-Beschreibung", "Pack description"), description);
            AddRow(grid, 2, L.T("Pfadvorlage", "Path preset"), template);
            template.Items.AddRange(new object[] { "WheelWizard — Custom Packs", "Dolphin — Riivolution", L.T("Dolphin — Dokumente (älter)", "Dolphin — Documents (legacy)") });
            AddRow(grid, 3, L.T("Pack-Speicherort", "Pack location"), destination);
            var browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += delegate
            {
                using (var picker = new FolderPickerDialog { SelectedPath = destination.Text })
                    if (picker.ShowDialog(this) == DialogResult.OK) destination.Text = picker.SelectedPath;
            };
            grid.Controls.Add(browse, 2, 3);
            grid.Controls.Add(preview, 0, 4);
            grid.SetColumnSpan(preview, 3);
            var fileHeading = new Label { Text = L.T("Ausgewählte Dateien für das neue Pack", "Selected files for the new pack"), AutoSize = true };
            grid.Controls.Add(fileHeading, 0, 5);
            grid.SetColumnSpan(fileHeading, 3);
            grid.Controls.Add(files, 0, 6);
            grid.SetColumnSpan(files, 3);
            var note = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = L.T(
                "Vorlagen sind Vorschläge; portable/eigene Pfade über Browse wählen. Kopiert ausgewählte .szs-Dateien und globe.arc. Ein neues Pack bleibt zunächst deaktiviert.",
                "Presets are suggestions; use Browse for portable/custom paths. Copies selected .szs files and globe.arc. New packs start disabled.") };
            var packActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            packActions.Controls.Add(new Label { Text = L.T("Gespeicherte Packs", "Saved packs"), AutoSize = true, Anchor = AnchorStyles.Left });
            var remember = new Button { Text = L.T("Pack hinzufügen…", "Add existing pack…"), AutoSize = true };
            remember.Click += delegate { Guard(RememberPack); };
            var remove = new Button { Text = L.T("Aus Liste entfernen", "Remove from list"), AutoSize = true, MinimumSize = new Size(170, 32) };
            remove.Click += delegate
            {
                Guard(delegate
                {
                    var pack = packs.SelectedItem as CustomPack;
                    if (pack == null) return;
                    CustomPacks.Remove(pack.FilesFolder);
                    RefreshPacks();
                    Status.Text = L.T("Pack aus Liste entfernt. Dateien bleiben erhalten.", "Pack removed from list. Files are kept.");
                });
            };
            packs.SelectedIndexChanged += delegate { remove.Enabled = packs.SelectedItem != null; };
            remove.Enabled = false;
            packActions.Controls.Add(remember);
            packActions.Controls.Add(remove);
            grid.Controls.Add(packActions, 0, 7);
            grid.SetColumnSpan(packActions, 3);
            grid.Controls.Add(packs, 0, 8);
            grid.SetColumnSpan(packs, 3);
            grid.Controls.Add(note, 0, 9);
            grid.SetColumnSpan(note, 3);
            Body.Controls.Add(grid);
            create = ExportAction(L.T("Pack erstellen", "Create pack"), "Create a new pack without overwriting existing files.", Create);
            template.SelectedIndexChanged += delegate
            {
                destination.Text = template.SelectedIndex == 2 ? CustomPacks.LegacyDolphinRoot() : CustomPacks.DefaultRoot(template.SelectedIndex == 0);
                UpdatePreview();
            };
            packName.TextChanged += delegate { UpdatePreview(); };
            destination.TextChanged += delegate { UpdatePreview(); };
            template.SelectedIndex = 0;
            StudioUx.DisableHover(packName);
            StudioUx.DisableHover(description);
            StudioUx.DisableHover(destination);
            MinimumSize = new Size(950, 820);
            Size = new Size(1140, 900);
            RefreshPacks();
            Finish();
        }

        void AddFiles(string[] paths)
        {
            foreach (string path in paths)
                if (!files.Items.Cast<string>().Any(p => String.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                    files.Items.Add(path);
            Status.Text = files.Items.Count + L.T(" Dateien ausgewählt.", " files selected.");
        }

        void RefreshPacks()
        {
            packs.Items.Clear();
            packs.Items.AddRange(CustomPacks.Load().Cast<object>().ToArray());
        }

        void RememberPack()
        {
            using (var picker = new FolderPickerDialog { Description = L.T("Ordner mit den Pack-Dateien wählen", "Choose the folder containing the pack files") })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    CustomPacks.Register(new CustomPack {
                        Name = Path.GetFileName(picker.SelectedPath.TrimEnd(Path.DirectorySeparatorChar)),
                        Folder = picker.SelectedPath, FilesFolder = picker.SelectedPath,
                        Description = "", Template = "Existing"
                    });
                    RefreshPacks();
                    Status.Text = L.T("Pack gespeichert. In anderen Tools die Pack-Liste aktualisieren.", "Pack saved. Refresh the pack list in other tools.");
                }
        }

        static void AddRow(TableLayoutPanel grid, int row, string text, Control field)
        {
            grid.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            grid.Controls.Add(field, 1, row);
        }

        void UpdatePreview()
        {
            try
            {
                CustomPacks.ValidateName(packName.Text);
                string folder = Path.Combine(Path.GetFullPath(destination.Text), packName.Text);
                preview.Text = L.T("Dateien: ", "Files: ") + Path.Combine(folder, template.SelectedIndex == 0 ? packName.Text : "Files");
                create.Enabled = true;
            }
            catch { preview.Text = L.T("Pack-Name und Speicherort eingeben.", "Enter a pack name and location."); create.Enabled = false; }
        }

        void Create()
        {
            var pack = CustomPacks.Create(destination.Text, packName.Text, description.Text,
                template.SelectedIndex == 0, files.Items.Cast<string>());
            string instructions = template.SelectedIndex == 0
                ? L.T("Pack in deiner Mod-Liste aktivieren und das Spiel neu starten.", "Enable the pack in your mod list and restart your game.")
                : L.T("Mario Kart Wii mit Riivolution-Patches starten, die neue XML unter riivolution öffnen und das Pack aktivieren.",
                    "Start Mario Kart Wii with Riivolution patches, open the new XML under riivolution and enable the pack.");
            try { CustomPacks.Register(pack); RefreshPacks(); }
            catch (Exception error)
            {
                StudioMessageBox.Show(this, L.T("Pack erstellt, aber Liste konnte nicht gespeichert werden: ", "Pack created, but its list entry could not be saved: ") + error.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Status.Text = L.T("Pack erstellt: ", "Pack created: ") + pack.FilesFolder; ToolStatus.Set(this, true);
            StudioMessageBox.Show(this, Status.Text + "\n\n" + instructions + "\n\n"
                + L.T("In anderen Tools: Custom pack auswählen. Bearbeitete Kopien landen in MUR_EDITED; danach gewünschte Pack-Dateien ersetzen.",
                    "In other tools: select Custom pack. Edited copies go to MUR_EDITED; then replace the intended pack files."),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}