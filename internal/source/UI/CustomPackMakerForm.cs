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
        readonly NumericUpDown modId = new NumericUpDown { Minimum = -1, Maximum = Int32.MaxValue, Value = -1, Width = 95 };
        readonly NumericUpDown priority = new NumericUpDown { Minimum = Int32.MinValue, Maximum = Int32.MaxValue, Width = 95 };
        readonly CheckBox enabled = new CheckBox { Text = "IsEnabled", AutoSize = true };
        readonly TextBox author = new TextBox { Dock = DockStyle.Fill, MaxLength = 120 };
        readonly TextBox description = new TextBox { Dock = DockStyle.Fill, Multiline = true, MaxLength = 2000, ScrollBars = ScrollBars.Vertical };
        readonly ComboBox template = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox destination = new TextBox { Dock = DockStyle.Fill };
        readonly ListBox files = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
        readonly ListBox packs = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
        readonly Label preview = new Label { Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false };
        readonly ComboBox region = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        static readonly string[] RegionCodes = { null, "E", "U", "J" };
        readonly Button create;
        readonly Button chooseRr;
        readonly Button addIso;
        readonly Button addModels;
        static readonly string[] RequiredModels = { "Earth.szs", "BackModel.szs", "globe.arc" };
        string rememberedFolder;
        string rrFolder;
        string[] rrFiles = new string[0];
        readonly TextBox rrPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        readonly PackSelectionStrip sourceBar = new PackSelectionStrip { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4), ColumnCount = 6 };
        readonly Label sourceHint = new Label
        {
            Name = "PackFilesHint", TextAlign = ContentAlignment.MiddleCenter,
            BackColor = DarkTheme.AccentSoft, ForeColor = DarkTheme.Fore, Padding = new Padding(12),
            Text = L.T("Wähle zuerst deinen Retro-Rewind-Ordner.\nRR-Dateien bilden die Grundlage deines Packs.\nErgänze danach nur fehlende Dateien aus deiner ISO/WBFS.",
                "Select your Retro Rewind folder first.\nRR files form the base of your pack.\nThen add only missing files from your ISO/WBFS.")
        };

        internal CustomPackMakerForm() : base("MKWii Custom Pack Maker",
            L.T("RR-Ordner wählen • Fehlende Dateien ergänzen • Pack erstellen", "Choose RR folder • Add missing files • Create pack"))
        {
            chooseRr = Action(L.T("1. RR-Ordner wählen…", "1. Choose RR folder…"), "Select your installed Retro Rewind version as the pack base.", ChooseRrFolder);
            addIso = Action(L.T("2. ISO ergänzen…", "2. Add missing from ISO…"), "Only files absent from RR are offered. RR menu backgrounds are never replaced.", delegate
            {
                RequireRrFolder();
                using (var picker = new OpenFileDialog { Filter = "ISO / WBFS|*.iso;*.wbfs;*.wia;*.ciso;*.wdf" })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        using (var dialog = new PackArchivePicker(picker.FileName, rrFiles.Select(Path.GetFileName).ToArray()))
                            if (dialog.ShowDialog(this) == DialogResult.OK)
                                AddFiles(dialog.ImportedPaths);
            });
            addModels = Action(L.T("Modelldateien hinzufügen…", "Add model files…"), "Add missing Earth.szs, BackModel.szs or globe.arc. RR files take priority.", delegate
            {
                RequireRrFolder();
                using (var picker = new OpenFileDialog { Filter = "Tool model sources|Earth.szs;BackModel.szs;globe.arc", Multiselect = true })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        AddFiles(picker.FileNames);
            });
            Action(L.T("Pack hinzufügen…", "Add existing pack…"), "Add an existing pack to your saved packs.", RememberPack);
            var removeFiles = Action(L.T("Auswahl entfernen", "Remove selected"), "Remove files from this list only.", delegate
            {
                foreach (object file in files.SelectedItems.Cast<object>().ToArray()) files.Items.Remove(file);
                UpdateSourceHint();
            });
            removeFiles.Enabled = false;
            files.SelectedIndexChanged += delegate { removeFiles.Enabled = files.SelectedItems.Count > 0; };
            var actionLayout = (TableLayoutPanel)Actions.Parent;
            int actionRow = actionLayout.GetRow(Actions);
            int column = 0;
            foreach (Button button in Actions.Controls.OfType<Button>().ToArray())
            {
                button.Padding = new Padding(4, 0, 4, 0);
                button.Margin = new Padding(3);
                button.MinimumSize = new Size(button.MinimumSize.Width, 30);
                button.MaximumSize = new Size(0, 30);
                button.Height = 30;
                button.Anchor = AnchorStyles.Left;
                button.BackColor = DarkTheme.Panel2;
                sourceBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                sourceBar.Controls.Add(button, column++, 0);
            }
            sourceBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionLayout.Controls.Remove(Actions);
            actionLayout.Controls.Add(sourceBar, 0, actionRow);
            sourceHint.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
            sourceHint.Disposed += delegate { sourceHint.Font.Dispose(); };
            sourceHint.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(92, 94, 104), 2))
                    e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(0, sourceHint.Width - 3), Math.Max(0, sourceHint.Height - 3));
            };
            VisibleChanged += delegate { sourceBar.UpdateAnimation(); };
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
            var fileArea = new Panel { Dock = DockStyle.Fill, Margin = files.Margin };
            fileArea.Controls.Add(files);

            grid.Controls.Add(fileArea, 0, 6);
            grid.SetColumnSpan(fileArea, 3);
            var note = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = L.T(
                "Vorlagen sind Vorschläge; portable/eigene Pfade über Browse wählen. Kopiert ausgewählte .szs-Dateien und globe.arc. IsEnabled ist standardmäßig aus.",
                "Presets are suggestions; use Browse for portable/custom paths. Copies selected .szs files and globe.arc. IsEnabled is off by default.") };
            var packActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            packActions.Controls.Add(new Label { Text = L.T("Gespeicherte Packs", "Saved packs"), AutoSize = true, Anchor = AnchorStyles.Left });
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

            packActions.Controls.Add(remove);
            grid.Controls.Add(packActions, 0, 7);
            grid.SetColumnSpan(packActions, 3);
            grid.Controls.Add(packs, 0, 8);
            grid.SetColumnSpan(packs, 3);
            grid.Controls.Add(note, 0, 9);
            grid.SetColumnSpan(note, 3);
            grid.RowCount++;
            foreach (Control control in grid.Controls.Cast<Control>().OrderByDescending(c => grid.GetRow(c)).ToArray())
                if (grid.GetRow(control) >= 1) grid.SetRow(control, grid.GetRow(control) + 1);
            grid.RowStyles.Insert(1, new RowStyle(SizeType.AutoSize));
            AddRow(grid, 1, L.T("Pack-Autor", "Pack author"), author);
            StudioUx.DisableHover(author);
            var metadata = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            metadata.Controls.Add(new Label { Text = "ModID", AutoSize = true, Anchor = AnchorStyles.Left });
            metadata.Controls.Add(modId);
            metadata.Controls.Add(new Label { Text = "Priority", AutoSize = true, Anchor = AnchorStyles.Left });
            metadata.Controls.Add(priority);
            metadata.Controls.Add(enabled);
            grid.RowCount++;
            foreach (Control control in grid.Controls.Cast<Control>().OrderByDescending(c => grid.GetRow(c)).ToArray())
                if (grid.GetRow(control) >= 3) grid.SetRow(control, grid.GetRow(control) + 1);
            grid.RowStyles.Insert(3, new RowStyle(SizeType.AutoSize));
            AddRow(grid, 3, L.T("INI-Einstellungen", "INI settings"), metadata);
            grid.SetColumnSpan(metadata, 2);
            StudioUx.SetHelp(modId, L.T("-1 für ein eigenes lokales Pack beibehalten, sofern keine ModID vorliegt.", "Keep -1 for a local custom pack unless you have a ModID."));
            StudioUx.SetHelp(priority, L.T("Prioritätswert in der Pack-INI. Standard: 0.", "Priority value written to the pack INI. Default: 0."));
            StudioUx.SetHelp(enabled, L.T("Pack in der INI als aktiviert markieren.", "Mark the pack as enabled in its INI."));
            grid.RowCount++;
            foreach (Control control in grid.Controls.Cast<Control>().OrderByDescending(c => grid.GetRow(c)).ToArray())
                grid.SetRow(control, grid.GetRow(control) + 1);
            grid.RowStyles.Insert(0, new RowStyle(SizeType.AutoSize));
            AddRow(grid, 0, L.T("RR-Quelle", "RR source"), rrPath);
            grid.SetColumnSpan(rrPath, 2);
            grid.RowCount++;
            foreach (Control control in grid.Controls.Cast<Control>().OrderByDescending(c => grid.GetRow(c)).ToArray())
                if (grid.GetRow(control) >= 1) grid.SetRow(control, grid.GetRow(control) + 1);
            grid.RowStyles.Insert(1, new RowStyle(SizeType.AutoSize));
            region.Items.AddRange(new object[] {
                L.T("Spielregion auswählen…", "Choose game region…"),
                "PAL / Europe (_E)", "USA / NTSC-U (_U)", "Japan / NTSC-J (_J)"
            });
            region.SelectedIndex = 0;
            AddRow(grid, 1, L.T("Spielregion", "Game region"), region);
            grid.SetColumnSpan(region, 2);
            StudioUx.SetHelp(region, L.T(
                "Region deiner Spielkopie. Title_U, Race_U und Common_U werden nur im neuen Pack passend benannt. Die RR-Menüsprache bleibt unverändert.",
                "Region of your game copy. Title_U, Race_U and Common_U are renamed only in the new pack. RR menu language stays unchanged."));
            region.SelectedIndexChanged += delegate { files.Refresh(); UpdateSourceHint(); };
            Body.Controls.Add(grid);
            create = ExportAction(L.T("Pack erstellen", "Create pack"), "Create a new pack without overwriting existing files.", Create);
            template.SelectedIndexChanged += delegate
            {
                destination.Text = template.SelectedIndex == 2 ? CustomPacks.LegacyDolphinRoot() : CustomPacks.DefaultRoot(template.SelectedIndex == 0);
                modId.Enabled = priority.Enabled = enabled.Enabled = template.SelectedIndex == 0;
                UpdatePreview();
            };
            packName.TextChanged += delegate { UpdatePreview(); };
            destination.TextChanged += delegate { UpdatePreview(); };
            template.SelectedIndex = 0;
            StudioUx.DisableHover(packName);
            StudioUx.DisableHover(description);
            StudioUx.DisableHover(destination);
            MinimumSize = new Size(950, 870);
            Size = new Size(1140, 900);
            files.FormattingEnabled = true;
            files.Format += delegate(object sender, ListControlConvertEventArgs e)
            {
                string path = e.ListItem as string;
                if (path != null)
                {
                    string name = CustomPacks.RegionalFileName(path, RegionCodes[Math.Max(0, region.SelectedIndex)]);
                    e.Value = name + (name == Path.GetFileName(path) ? "" : " ← " + Path.GetFileName(path))
                        + (rrFiles.Contains(path) ? " — Retro Rewind" : " — " + Path.GetDirectoryName(path));
                }
            };
            RefreshPacks();
            Finish();
            Controls.Add(sourceHint);
            Layout += delegate { if (!sourceHint.IsDisposed) PositionSourceHint(); };
            UpdateSourceHint();
        }

        void RequireRrFolder()
        {
            if (String.IsNullOrEmpty(rrFolder))
                throw new InvalidOperationException(L.T("Zuerst RR-Ordner wählen.", "Choose your RR folder first."));
        }

        void ChooseRrFolder()
        {
            string dolphin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dolphin Emulator", "Load", "Riivolution", "RetroRewind6");
            string wheelWizard = Path.Combine(Path.GetDirectoryName(dolphin), "WheelWizard", "RetroRewind6");
            string[] detected = RetroRewindSource.Discover();
            string[] found = new[] { wheelWizard, dolphin }.Concat(detected).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            using (var dialog = new Form { Text = L.T("Retro-Rewind-Quelle", "Retro Rewind source"), Font = new Font("Segoe UI", 9), Icon = Icon, AutoScaleMode = AutoScaleMode.Font, ClientSize = new Size(900, 380),
                MinimumSize = new Size(800, 390), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 4 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var header = StudioChrome.Header(L.T("Retro-Rewind-Quelle", "Retro Rewind source"),
                    L.T("Dolphin oder WheelWizard • Deine RR-Dateien als Pack-Grundlage", "Dolphin or WheelWizard • Start your pack from your RR files"));
                layout.Controls.Add(header, 0, 0);
                layout.SetColumnSpan(header, 2);
                var help = new Label { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 12, 3, 12), Text = L.T(
                    "Dolphin ohne WheelWizard: RR unter Load/Riivolution/RetroRewind6 wird ebenfalls erkannt.\nEigener Pfad oder portables Dolphin: Browse → Dolphin-Benutzerordner, Dolphin-Ordner oder RetroRewind6.\nWähle die RR-Version, die du spielst. Mods/Patches sind keine RR-Quelle.",
                    "Dolphin without WheelWizard: RR in Load/Riivolution/RetroRewind6 is also detected.\nCustom path or portable Dolphin: Browse → Dolphin user folder, Dolphin folder or RetroRewind6.\nChoose the RR version you play. Mods/Patches are not an RR source.") };
                layout.Controls.Add(help, 0, 1); layout.SetColumnSpan(help, 2);
                var paths = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                paths.FormattingEnabled = true;
                paths.Format += delegate(object sender, ListControlConvertEventArgs e)
                {
                    string path = e.ListItem as string;
                    string label = String.Equals(path, wheelWizard, StringComparison.OrdinalIgnoreCase)
                        ? L.T("WheelWizard – Standardpfad", "WheelWizard – default path")
                        : String.Equals(path, dolphin, StringComparison.OrdinalIgnoreCase)
                            ? L.T("Dolphin direkt – Standardpfad", "Direct Dolphin – default path")
                            : L.T("Eigener / erkannter Pfad", "Custom / detected path");
                    e.Value = label + " — " + path;
                };
                paths.Items.AddRange(found);
                paths.SelectedIndex = Math.Max(0, Array.FindIndex(found, p => detected.Contains(p, StringComparer.OrdinalIgnoreCase)));
                var browse = new Button { Text = "Browse", AutoSize = true, Height = 32 };
                browse.Click += delegate {
                    using (var picker = new FolderPickerDialog { Description = L.T("Dolphin-, Dolphin-Benutzer- oder RetroRewind6-Ordner auswählen", "Select the Dolphin, Dolphin user or RetroRewind6 folder"), SelectedPath = paths.SelectedItem as string })
                        if (picker.ShowDialog(dialog) == DialogResult.OK) {
                            paths.Items.Add(picker.SelectedPath); paths.SelectedIndex = paths.Items.Count - 1;
                        }
                };
                layout.Controls.Add(paths, 0, 2); layout.Controls.Add(browse, 1, 2);
                var use = new Button { Text = L.T("RR-Dateien übernehmen", "Use RR files"), AutoSize = true, Height = 34, Anchor = AnchorStyles.Right, Enabled = paths.SelectedIndex >= 0 };
                paths.SelectedIndexChanged += delegate { use.Enabled = paths.SelectedIndex >= 0; };
                use.Click += delegate {
                    try { LoadRrFolder((string)paths.SelectedItem); dialog.DialogResult = DialogResult.OK; }
                    catch (Exception error) { StudioMessageBox.Show(dialog, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                layout.Controls.Add(use, 0, 3); layout.SetColumnSpan(use, 2);
                dialog.Controls.Add(layout); DarkTheme.Apply(dialog); dialog.ShowDialog(this);
            }
        }

        void LoadRrFolder(string folder)
        {
            string[] staged = RetroRewindSource.Stage(folder);
            // Ein Wechsel der RR-Version verwirft nur die vorgemerkte Auswahl.
            files.Items.Clear();
            rrFolder = RetroRewindSource.Resolve(folder);
            rrFiles = staged;
            rrPath.Text = rrFolder;
            files.Items.AddRange(staged);
            Status.Text = staged.Length + L.T(" RR-Dateien bereit. ISO ergänzt nur fehlende Dateien.", " RR files ready. ISO adds missing files only.");
            UpdateSourceHint();
        }

        void AddFiles(string[] paths)
        {
            RequireRrFolder();
            foreach (string stale in files.Items.Cast<string>().Where(p => !File.Exists(p)).ToArray())
                files.Items.Remove(stale);
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                string name = Path.GetFileName(path);
                if (!RetroRewindSource.AllowIso(name, rrFiles.Select(Path.GetFileName))) continue;
                if (!files.Items.Cast<string>().Any(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase)))
                    files.Items.Add(path);
            }
            Status.Text = files.Items.Count + L.T(" Dateien ausgewählt.", " files selected.");
            UpdateSourceHint();
        }

        void UpdateSourceHint()
        {
            bool available = files.Items.Count > 0 || (!String.IsNullOrEmpty(rememberedFolder) && Directory.Exists(rememberedFolder));
            Body.Enabled = available;
            Footer.Enabled = available;
            sourceHint.Visible = !available;
            bool rrReady = String.IsNullOrEmpty(RrProblem());
            bool ready = String.IsNullOrEmpty(SourceProblem());
            addIso.Enabled = addModels.Enabled = rrReady;
            chooseRr.Name = rrReady ? "" : "PackSourceAction";
            addIso.Name = rrReady && !ready ? "PackSourceAction" : "";
            chooseRr.BackColor = addIso.BackColor = DarkTheme.Panel2;
            sourceBar.Required = !ready;
            sourceBar.SourceRequired = !ready;
            UpdatePreview();
            PositionSourceHint();
            if (sourceHint.Visible) sourceHint.BringToFront();
        }

        void PositionSourceHint()
        {
            if (sourceHint.Parent == null) return;
            float scale = Font.Size / 9f;
            int top = PointToClient(sourceBar.PointToScreen(new Point(0, sourceBar.Height))).Y;
            int width = Math.Max(1, Math.Min((int)(720 * scale), ClientSize.Width - (int)(48 * scale)));
            int height = Math.Min((int)(132 * scale), Math.Max(1, ClientSize.Height - top - (int)(50 * scale)));
            sourceHint.Bounds = new Rectangle((ClientSize.Width - width) / 2,
                top + Math.Max(12, (ClientSize.Height - top - height) / 2), width, height);
            sourceHint.BringToFront();
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
                    RegisterExistingPack(picker.SelectedPath);
                }
        }

        void RegisterExistingPack(string folder)
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            CustomPacks.Register(new CustomPack {
                Name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),
                Folder = folder, FilesFolder = folder,
                Description = "", Template = "Existing"
            });
            rememberedFolder = folder;
            RefreshPacks();
            UpdateSourceHint();
            Status.Text = L.T("Pack gespeichert. In anderen Tools die Pack-Liste aktualisieren.", "Pack saved. Refresh the pack list in other tools.");
        }

        static void AddRow(TableLayoutPanel grid, int row, string text, Control field)
        {
            grid.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            grid.Controls.Add(field, 1, row);
        }

        string RrProblem()
        {
            if (String.IsNullOrEmpty(rrFolder) || rrFiles.Length == 0)
                return L.T("Schritt 1: RR-Ordner wählen.", "Step 1: Choose your RR folder.");
            if (rrFiles.Any(p => !File.Exists(p) || !files.Items.Contains(p)))
                return L.T("RR-Dateien fehlen. Wähle den RR-Ordner erneut.",
                    "RR files are missing. Choose your RR folder again.");
            return null;
        }

        string SourceProblem()
        {
            string rrProblem = RrProblem();
            if (rrProblem != null) return rrProblem;
            if (region.SelectedIndex <= 0)
                return L.T("Spielregion wählen: PAL, USA oder Japan. Dateinamen werden im neuen Pack angepasst.",
                    "Choose game region: PAL, USA or Japan. Filenames are adjusted in the new pack.");
            string[] present = files.Items.Cast<string>().Where(File.Exists).Select(Path.GetFileName).ToArray();
            string[] missing = RequiredModels.Where(name => !present.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (missing.Length == 0) return null;
            return L.T("Schritt 2: ISO ergänzen — benötigt: ", "Step 2: Add missing from ISO — required: ")
                + String.Join(", ", missing) + ".";
        }

        void RequireCompleteSources()
        {
            string problem = SourceProblem();
            if (problem == null) return;
            UpdateSourceHint();
            throw new InvalidOperationException(problem);
        }

        void UpdatePreview()
        {
            create.Enabled = false;
            string problem = SourceProblem();
            if (problem != null)
            {
                preview.Text = problem;
                preview.ForeColor = DarkTheme.Accent2;
                return;
            }
            preview.ForeColor = DarkTheme.Fore;
            try
            {
                CustomPacks.ValidateName(packName.Text);
                string folder = Path.Combine(Path.GetFullPath(destination.Text), packName.Text);
                preview.Text = L.T("RR + Modelldateien vollständig. Dateien: ", "RR + model files complete. Files: ")
                    + Path.Combine(folder, template.SelectedIndex == 0 ? packName.Text : "Files");
                create.Enabled = true;
            }
            catch
            {
                preview.Text = L.T("Dateien vollständig. Pack-Name und Speicherort eingeben.",
                    "Files complete. Enter a pack name and location.");
            }
        }
        void Create()
        {
            RequireRrFolder();
            RequireCompleteSources();
            var pack = CustomPacks.Create(destination.Text, packName.Text, description.Text,
                template.SelectedIndex == 0, files.Items.Cast<string>(), author.Text,
                template.SelectedIndex == 0 ? (int)modId.Value : -1,
                template.SelectedIndex == 0 && enabled.Checked,
                template.SelectedIndex == 0 ? (int)priority.Value : 0, RegionCodes[region.SelectedIndex]);
            string instructions = template.SelectedIndex == 0
                ? (pack.IsEnabled ? L.T("Pack ist aktiviert. Starte dein Spiel neu.", "The pack is enabled. Restart your game.")
                    : L.T("Pack in deiner Mod-Liste aktivieren und das Spiel neu starten.", "Enable the pack in your mod list and restart your game."))
                : L.T("Mario Kart Wii mit Riivolution-Patches starten, die neue XML unter riivolution öffnen und das Pack aktivieren.",
                    "Start Mario Kart Wii with Riivolution patches, open the new XML under riivolution and enable the pack.");
            try { CustomPacks.Register(pack); RefreshPacks(); }
            catch (Exception error)
            {
                StudioMessageBox.Show(this, L.T("Pack erstellt, aber Liste konnte nicht gespeichert werden: ", "Pack created, but its list entry could not be saved: ") + error.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Status.Text = L.T("Pack erstellt: ", "Pack created: ") + pack.FilesFolder; ToolStatus.Set(this, true);
            StudioMessageBox.ShowPath(this, pack.FilesFolder, instructions + "\n\n"
                + L.T("In anderen Tools: Custom pack auswählen. Bearbeitete Kopien landen in MUR_EDITED; danach gewünschte Pack-Dateien ersetzen.",
                    "In other tools: select Custom pack. Edited copies go to MUR_EDITED; then replace the intended pack files."),
                L.T("Pack erstellt", "Pack created"));
        }
    }
}