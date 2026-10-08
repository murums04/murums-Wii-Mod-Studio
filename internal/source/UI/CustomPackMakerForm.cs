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
        SpecialEditorHistory editHistory;
        TabControl workflowSteps;
        Button nextStep, previousStep;
        readonly Label contentState = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
        readonly TextBox rrPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        readonly PackSelectionStrip sourceBar = new PackSelectionStrip { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4), ColumnCount = 6 };
        readonly Label sourceHint = new Label
        {
            Name = "PackFilesHint", TextAlign = ContentAlignment.MiddleCenter,
            BackColor = DarkTheme.AccentSoft, ForeColor = DarkTheme.Fore, Padding = new Padding(12),
            Text = L.T("Wähle zuerst deinen Retro-Rewind-Ordner.\nRR-Dateien bilden die Grundlage deines Packs.\nErgänze danach nur fehlende Dateien aus deiner ISO/WBFS.",
                "Select your Retro Rewind folder first.\nRR files form the base of your pack.\nThen add only missing files from your ISO/WBFS.")
        };

        internal CustomPackMakerForm() : base("RR-MKWii Custom Pack Maker Tool",
            L.T("RR-Ordner wählen • Fehlende Dateien ergänzen • Pack erstellen", "Choose RR folder • Add missing files • Create pack"))
        {
            chooseRr = Action(L.T("RR-Ordner wählen…", "Choose RR folder…"), L.T("Installierten Retro-Rewind-Ordner als Grundlage des Packs wählen.", "Select your installed Retro Rewind version as the pack base."), ChooseRrFolder);
            addIso = Action(L.T("ISO ergänzen…", "Add missing from ISO…"), L.T("Nur in RR fehlende Dateien ergänzen. RR-Menühintergründe bleiben erhalten.", "Only files absent from RR are offered. RR menu backgrounds are never replaced."), delegate
            {
                RequireRrFolder();
                using (var picker = new OpenFileDialog { Filter = "ISO / WBFS|*.iso;*.wbfs;*.wia;*.ciso;*.wdf" })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        var dialog = new PackArchivePicker(picker.FileName, rrFiles.Select(Path.GetFileName).ToArray());
                        StudioEditor.Open(this, dialog, delegate(DialogResult result) { if (result == DialogResult.OK) AddFiles(dialog.ImportedPaths); });
                    }
            });
            addModels = Action(L.T("Modelldateien…", "Model files…"), L.T("Fehlende Earth.szs, BackModel.szs oder globe.arc ergänzen. RR-Dateien haben Vorrang.", "Add missing Earth.szs, BackModel.szs or globe.arc. RR files take priority."), delegate
            {
                RequireRrFolder();
                using (var picker = new OpenFileDialog { Filter = "Tool model sources|Earth.szs;BackModel.szs;globe.arc", Multiselect = true })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        AddFiles(picker.FileNames);
            });
            Action(L.T("Pack hinzufügen…", "Add existing pack…"), L.T("Vorhandenen Pack-Ordner zur Pack-Liste hinzufügen.", "Add an existing pack to your saved packs."), RememberPack);
            var removeFiles = Action(L.T("Auswahl entfernen", "Remove selected"), L.T("Dateien nur aus dieser Liste entfernen.", "Remove files from this list only."), delegate
            {
                foreach (object file in files.SelectedItems.Cast<object>().ToArray()) files.Items.Remove(file);
                UpdateSourceHint();
                if (editHistory != null) editHistory.Observe();
            });
            removeFiles.Enabled = false;
            files.SelectedIndexChanged += delegate { removeFiles.Enabled = files.SelectedItems.Count > 0; };
            var actionLayout = (TableLayoutPanel)Actions.Parent;
            int actionRow = actionLayout.GetRow(Actions);
            sourceBar.ColumnCount = 1;
            sourceBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var sourceActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            foreach (Button button in Actions.Controls.OfType<Button>().ToArray())
            {
                button.Padding = new Padding(4, 0, 4, 0);
                button.Margin = new Padding(3);
                button.MinimumSize = new Size(button.MinimumSize.Width, 30);
                button.MaximumSize = new Size(0, 30);
                button.Height = 30;
                button.Anchor = AnchorStyles.Left;
                button.BackColor = DarkTheme.Panel2;
                sourceActions.Controls.Add(button);
            }
            sourceBar.Controls.Add(sourceActions, 0, 0);
            actionLayout.Controls.Remove(Actions);
            actionLayout.Controls.Add(sourceBar, 0, actionRow);
            sourceHint.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
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
            AddRow(grid, 1, L.T("Beschreibung", "Description"), description);
            AddRow(grid, 2, L.T("Pfadvorlage", "Path preset"), template);
            template.Items.AddRange(new object[] { "WheelWizard — Custom Packs", "Dolphin — Riivolution", L.T("Dolphin — Dokumente (älter)", "Dolphin — Documents (legacy)") });
            AddRow(grid, 3, L.T("Speicherort", "Location"), destination);
            var browse = new Button { Text = L.T("Speicherort wählen…", "Choose location…"), AutoSize = true };
            StudioActions.Icon(browse, StudioIcon.Folder);
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
            var fileArea = new Panel { Dock = DockStyle.Fill, Margin = files.Margin, MinimumSize = new Size(0, 100) };
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
            packs.MinimumSize = new Size(0, 80);
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
            AddRow(grid, 3, L.T("Pack-INI", "Pack INI"), metadata);
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
            MinimumSize = new Size(950, 680);
            Size = new Size(1140, 850);
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
            Body.Controls.Remove(grid);
            var sourcePages = new DarkTabControl { Dock = DockStyle.Fill };
            workflowSteps = sourcePages;
            var sourcePage = new TabPage(L.T("1  RR-Quelle", "1  RR source")) { Padding = new Padding(16) };
            var selectionPage = new TabPage(L.T("2  Pack-Inhalt", "2  Pack content")) { Padding = new Padding(6) };
            var finishPage = new TabPage(L.T("3  Pack erstellen", "3  Create pack")) { Padding = new Padding(6) };
            var savedPage = new TabPage(L.T("Pack-Liste verwalten", "Manage pack list")) { Padding = new Padding(6) };
            var sourceFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12), MaximumSize = new Size(720, 0) };
            sourceFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sourceFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var sourceTitle = new Label { Text = L.T("Wähle deine Retro-Rewind-Installation und die Spielregion.", "Choose your Retro Rewind installation and game region."), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 0, 0, 16) };
            sourceFields.Controls.Add(sourceTitle, 0, 0); sourceFields.SetColumnSpan(sourceTitle, 2);
            foreach (Control label in grid.Controls.Cast<Control>().Where(c => grid.GetRow(c) < 2 && c is Label).ToArray()) label.Dispose();
            sourceFields.Controls.Add(rrPath, 0, 1);
            sourceFields.Controls.Add(chooseRr, 1, 1);
            sourceFields.Controls.Add(new Label { Text = L.T("Region deiner Spielkopie", "Your game copy's region"), AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 14, 3, 3) }, 0, 2);
            sourceFields.SetColumnSpan(sourceFields.GetControlFromPosition(0, 2), 2);
            sourceFields.Controls.Add(region, 0, 3); sourceFields.SetColumnSpan(region, 2);
            sourceHint.AutoSize = true;
            sourceHint.Dock = DockStyle.Fill;
            sourceFields.Controls.Add(sourceHint, 0, 4); sourceFields.SetColumnSpan(sourceHint, 2);
            sourcePage.Controls.Add(sourceFields);
            var selection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            selection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            selection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            selection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            selection.Controls.Add(fileHeading, 0, 0);
            var fileActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            fileActions.Controls.AddRange(new Control[] { addIso, addModels, removeFiles });
            selection.Controls.Add(fileActions, 0, 1);
            selection.Controls.Add(fileArea, 0, 2);
            selection.Controls.Add(contentState, 0, 3);
            selectionPage.Controls.Add(selection);
            var saved = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            saved.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            saved.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            saved.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            packActions.WrapContents = true;
            saved.Controls.Add(packActions, 0, 0);
            saved.Controls.Add(packs, 0, 1);
            savedPage.Controls.Add(saved);
            sourcePages.TabPages.AddRange(new[] { sourcePage, selectionPage, finishPage, savedPage });
            DarkTheme.StyleTabs(sourcePages);
            var settings = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), BackColor = DarkTheme.Panel };
            grid.Controls.Remove(note);
            grid.RowCount = 9;
            while (grid.RowStyles.Count > 9) grid.RowStyles.RemoveAt(grid.RowStyles.Count - 1);
            grid.ColumnStyles[0].Width = 104;
            grid.Dock = DockStyle.Top;
            grid.AutoSize = true;
            grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grid.RowStyles[4].SizeType = SizeType.Absolute;
            grid.RowStyles[4].Height = 72;
            grid.RowStyles[6].SizeType = SizeType.AutoSize;
            grid.RowCount = 10;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            note.MaximumSize = new Size(306, 0);
            note.Text = L.T("Eigene Pfade über das Ordner-Symbol wählen. Kopiert ausgewählte .szs und globe.arc. IsEnabled bleibt ohne Haken deaktiviert.",
                "Use the folder icon for custom paths. Copies selected .szs files and globe.arc. IsEnabled stays off unless checked.");
            grid.Controls.Add(note, 0, 9);
            grid.SetColumnSpan(note, 3);
            settings.Controls.Add(grid);
            finishPage.Controls.Add(settings);
            Body.Controls.Add(sourcePages);
            foreach (Control remaining in sourceActions.Controls.Cast<Control>().ToArray()) remaining.Parent = packActions;
            sourceActions.Controls.Clear();
            previousStep = new Button { Text = L.T("Zurück", "Back"), AutoSize = true };
            nextStep = new Button { Text = L.T("Weiter", "Next"), AutoSize = true };
            DarkTheme.StylePrimary(nextStep);
            previousStep.Click += delegate { if (sourcePages.SelectedIndex > 0) sourcePages.SelectedIndex--; };
            nextStep.Click += delegate { if (sourcePages.SelectedIndex < 2) sourcePages.SelectedIndex++; };
            sourceActions.Controls.Add(previousStep); sourceActions.Controls.Add(nextStep);
            sourcePages.Selecting += delegate(object sender, TabControlCancelEventArgs e)
            {
                if (e.TabPageIndex == 1 && (RrProblem() != null || region.SelectedIndex <= 0)) { e.Cancel = true; Status.Text = L.T("Zuerst RR-Ordner und Spielregion wählen.", "Choose the RR folder and game region first."); }
                if (e.TabPageIndex == 2 && SourceProblem() != null) { e.Cancel = true; Status.Text = SourceProblem(); }
            };
            sourcePages.SelectedIndexChanged += delegate { UpdateWorkflowActions(); };
            removeFiles.MaximumSize = Size.Empty;
            StudioActions.Icon(removeFiles, StudioIcon.Remove);
            Finish();
            sourceFields.SizeChanged += delegate { if (!sourceHint.IsDisposed) PositionSourceHint(); };
            UpdateSourceHint();
            editHistory = new SpecialEditorHistory(this, null, CaptureSettings, RestoreSettings);
            sourceBar.RowCount = 2;
            sourceBar.Controls.Add(editHistory.Binding.Panel, 0, 1);
            sourceBar.SetColumnSpan(editHistory.Binding.Panel, sourceBar.ColumnCount);
            UpdateWorkflowActions();
        }

        void UpdateWorkflowActions()
        {
            if (workflowSteps == null || nextStep == null) return;
            int step = workflowSteps.SelectedIndex;
            previousStep.Visible = step > 0 && step < 3;
            nextStep.Visible = step < 2;
            nextStep.Enabled = step == 0 ? RrProblem() == null && region.SelectedIndex > 0 : SourceProblem() == null;
            create.Visible = step == 2;
            Footer.Visible = step == 2;
            contentState.Text = SourceProblem() ?? L.T("Alle benötigten Quellen sind vorhanden. Weiter zu Name und Speicherort.", "All required sources are available. Continue to name and location.");
        }

        object[] CaptureSettings()
        {
            return new object[] { packName.Text, author.Text, description.Text, template.SelectedIndex, destination.Text,
                modId.Value, priority.Value, enabled.Checked, region.SelectedIndex, files.Items.Cast<object>().ToArray() };
        }

        void RestoreSettings(object[] state)
        {
            packName.Text = (string)state[0]; author.Text = (string)state[1]; description.Text = (string)state[2];
            template.SelectedIndex = (int)state[3]; destination.Text = (string)state[4];
            modId.Value = (decimal)state[5]; priority.Value = (decimal)state[6]; enabled.Checked = (bool)state[7];
            region.SelectedIndex = (int)state[8]; files.Items.Clear(); files.Items.AddRange((object[])state[9]);
            files.Refresh(); UpdateSourceHint();
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
            var dialog = new Form { Text = L.T("Retro-Rewind-Quelle", "Retro Rewind source"), Font = new Font("Segoe UI", 9), Icon = Icon, AutoScaleMode = AutoScaleMode.Font, ClientSize = new Size(900, 380),
                MinimumSize = new Size(800, 390), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false };
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 4 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
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
                    try { LoadRrFolder((string)paths.SelectedItem); dialog.DialogResult = DialogResult.OK; dialog.Close(); }
                    catch (Exception error) { StudioMessageBox.Show(dialog, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                layout.Controls.Add(use, 0, 3); layout.SetColumnSpan(use, 2);
                dialog.Controls.Add(layout); DarkTheme.Apply(dialog); StudioEditor.Open(this, dialog, delegate { });
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
            if (editHistory != null) { editHistory.Reset(); editHistory.Binding.Refresh(); }
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
            if (editHistory != null) editHistory.Observe();
        }

        void UpdateSourceHint()
        {
            bool rrReady = String.IsNullOrEmpty(RrProblem());
            string[] missing = RequiredModels.Where(name => !files.Items.Cast<string>()
                .Any(path => File.Exists(path) && Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
            bool ready = rrReady && missing.Length == 0;
            Body.Enabled = true;
            Footer.Enabled = ready;
            sourceHint.Visible = !rrReady || region.SelectedIndex <= 0;
            sourceHint.Text = rrReady
                ? L.T("Wähle PAL, USA oder Japan passend zu deiner Spielkopie. Danach prüfst du den Pack-Inhalt.",
                    "Choose PAL, USA or Japan to match your game copy. Then review the pack content.")
                : L.T("Wähle zuerst deinen Retro-Rewind-Ordner.", "Select your Retro Rewind folder first.")
                    + Environment.NewLine + L.T("RR-Dateien bilden die Grundlage deines Packs.", "RR files form the base of your pack.")
                    + Environment.NewLine + L.T("Ergänze danach nur fehlende Dateien aus deiner ISO/WBFS.",
                        "Then add only missing files from your ISO/WBFS.");
            addIso.Enabled = addModels.Enabled = rrReady;
            chooseRr.Name = rrReady ? "" : "PackSourceAction";
            addIso.Name = rrReady && !ready ? "PackSourceAction" : "";
            if (rrReady) DarkTheme.StyleNeutral(chooseRr);
            else DarkTheme.StylePrimary(chooseRr);
            if (rrReady && !ready) { DarkTheme.StylePrimary(addIso); DarkTheme.StylePrimary(addModels); }
            else { DarkTheme.StyleNeutral(addIso); DarkTheme.StyleNeutral(addModels); }
            sourceBar.Required = sourceBar.SourceRequired = false;
            UpdatePreview();
            UpdateWorkflowActions();
            PositionSourceHint();
            if (sourceHint.Visible) sourceHint.BringToFront();
        }

        void PositionSourceHint()
        {
            if (sourceHint.Parent == null) return;
            sourceHint.MaximumSize = new Size(Math.Max(200, sourceHint.Parent.ClientSize.Width - 12), 0);
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
