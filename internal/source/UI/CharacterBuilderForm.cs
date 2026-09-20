using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal sealed class CharacterBuilderForm : StudioToolForm
    {
        CharacterCatalog catalog;
        CharacterVariant target;
        readonly PictureBox targetPortrait = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly PictureBox replacementPortrait = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly Label targetCaption = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        readonly Label replacementCaption = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        int Slot { get { return target == null ? 0 : target.Slot; } }
        readonly TextBox nameEditor = new TextBox { Name = "ReplacementName", Dock = DockStyle.Fill };
        readonly TextBox authorEditor = new TextBox { Name = "ReplacementAuthor", Dock = DockStyle.Fill };
        readonly Label nameTarget = new Label { AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false };
        bool syncingName;
        readonly DataGridView names = new DataGridView { ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = true, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        readonly ListView files = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false, ShowItemToolTips = true, BorderStyle = BorderStyle.None };
        readonly Label fileEmpty = new Label {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Text = L.T("Charakterdateien hinzufügen\n\n„Charakterdateien hinzufügen“ bietet fehlende Dateien aus deiner RR-Installation an.\nDu kannst auch eigene fertige BRRES-/SZS-Dateien wählen.\nEin eigenes Modell? Öffne den Tab „Eigenes 3D-Modell“.",
                "Add your character files\n\nAdd character files offers missing files from your RR installation.\nYou can also select your own ready-made BRRES/SZS files.\nStarting with your own model? Open the Own 3D model tab.")
        };
        readonly Label fileGuide = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8, 3, 8, 3) };
        Button addFilesButton, removeFilesButton, inspectFilesButton;
        readonly TextBox report = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        readonly TextBox modelInfo = new TextBox { Dock = DockStyle.Bottom, Height = 120, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        readonly CharacterModelViewport modelPreview = new CharacterModelViewport();
        readonly NumericUpDown modelScale = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 100, DecimalPlaces = 2, Width = 80 };
        readonly ComboBox modelView = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        readonly TabControl tabs = new CharacterBuilderTabs { Dock = DockStyle.Fill };
        readonly List<CharacterAsset> assets = new List<CharacterAsset>();
        readonly Dictionary<string, string> archives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Button build;
        CharacterModelImport model;
        ModelRig reviewedRig;
        Button prepareModelButton, convertModelButton;
        bool dirty, loading, modelNeedsConversion;
        internal sealed class Project
        {
            public int Version { get; set; }
            public int Character { get; set; }
            public int Slot { get; set; }
            public string RRRoot { get; set; }
            public string[] Files { get; set; }
            public string[] Archives { get; set; }
            public string Model { get; set; }
            public string Rig { get; set; }
            public bool ModelNeedsConversion { get; set; }
            public decimal ScalePercent { get; set; }
            public float FitScale { get; set; }
            public float ReferenceHeight { get; set; }
            public List<CharacterNameEntry> Names { get; set; }
        }
        CharacterDefinition Selected { get { return target == null ? null : target.Character; } }

        public CharacterBuilderForm() : base("RR-MKWii Character Builder Tool", L.T("Vorhandenen RR-Charakter wählen • Ersatzdateien hinzufügen • Kopien exportieren", "Choose an installed RR character • Add replacement files • Export copies"),
            "RR: driver BRRES + matching vehicle SZS · UIAssets.szs / RaceAssets.szs · Own models: GLB / glTF / BLEND / USDZ / DAE / OBJ")
        {
            Action(L.T("Charakter ersetzen…", "Replace character…"), L.T("Vorhandene RR-Variante anhand von Name und Bild auswählen.", "Choose an installed RR variant by name and picture."), NewProject).Name = "PackSourceAction";
            Action(L.T("Projekt öffnen…", "Open project…"), L.T("Gespeichertes Charakterprojekt laden.", "Load a saved character project."), OpenProject).Name = "PackSourceAction";
            var clearButton = Action(L.T("Auswahl leeren", "Clear selection"), L.T("Projekt nach Rückfrage zu ungespeicherten Änderungen leeren.", "Clear this project after confirming unsaved work."), Clear);
            clearButton.Name = "CharacterClear";
            Actions.Controls.Remove(clearButton);
            Footer.Controls.Add(clearButton);
            clearButton.MinimumSize = new Size(125, 36);
            clearButton.Margin = new Padding(3);
            clearButton.Padding = new Padding(4, 0, 4, 0);
            var selection = new TableLayoutPanel { Dock = DockStyle.Top, Height = 72, ColumnCount = 5, RowCount = 1, Padding = new Padding(5) };
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            selection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            selection.Controls.Add(targetPortrait, 0, 0);
            selection.Controls.Add(targetCaption, 1, 0);
            selection.Controls.Add(new Label { Text = "→", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 2, 0);
            selection.Controls.Add(replacementPortrait, 3, 0);
            selection.Controls.Add(replacementCaption, 4, 0);

            var assetTab = new TabPage(L.T("Charakterdateien", "Character files"));
            var modelTab = new TabPage(L.T("Eigenes 3D-Modell", "Own 3D model"));
            var nameTab = new TabPage(L.T("Namen & Autoren", "Names & authors"));
            var checkTab = new TabPage(L.T("Prüfen & Export", "Check & export"));
            tabs.TabPages.AddRange(new[] { assetTab, modelTab, nameTab, checkTab });
            files.Columns.Add(L.T("Zweck", "Purpose"), 130);
            files.Columns.Add(L.T("Ziel in RR", "Output in RR"), 340);
            files.Columns.Add(L.T("Quelle", "Source"), 420);
            var assetLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            assetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            assetLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            assetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            assetLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var assetBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(2) };
            addFilesButton = Button(assetBar, L.T("Charakterdateien hinzufügen…", "Add character files…"), AddFiles);
            removeFilesButton = Button(assetBar, L.T("Auswahl entfernen", "Remove selected"), RemoveFiles);
            inspectFilesButton = Button(assetBar, L.T("Auswahl prüfen…", "Inspect selected…"), InspectSelected);
            Button(assetBar, L.T("Bilder & Embleme…", "Images & emblems…"), EditImages);
            var fileArea = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 0) };
            fileArea.Controls.Add(files);
            fileArea.Controls.Add(fileEmpty);
            assetLayout.Controls.Add(assetBar, 0, 0);
            assetLayout.Controls.Add(fileArea, 0, 1);
            assetLayout.Controls.Add(fileGuide, 0, 2);
            assetTab.Controls.Add(assetLayout);
            files.SelectedIndexChanged += delegate { RefreshFileActions(); };
            files.SizeChanged += delegate { FitFileColumns(); };
            fileArea.SizeChanged += delegate { fileGuide.MaximumSize = new Size(Math.Max(100, fileArea.Width), 0); };
            modelTab.Controls.Add(modelPreview);
            modelTab.Controls.Add(modelInfo);
            var modelBar = Bar(modelTab);
            Button(modelBar, L.T("1 · Modell laden…", "1 · Load model…"), ImportModel);
            prepareModelButton = Button(modelBar, L.T("2 · Haltung & Bewegung…", "2 · Pose & movement…"), PrepareModel);
            convertModelButton = Button(modelBar, L.T("3 · Charakter exportieren…", "3 · Export character…"), ConvertModel);
            prepareModelButton.MinimumSize = new Size(210, 32);
            convertModelButton.MinimumSize = new Size(200, 32);
            prepareModelButton.Enabled = convertModelButton.Enabled = false;
            modelView.Items.AddRange(new object[] { "3D", L.T("Vorne", "Front"), L.T("Seite", "Side"), L.T("Oben", "Top") });
            modelView.SelectedIndex = 0;
            modelView.Margin = new Padding(3);
            modelView.SelectedIndexChanged += delegate { modelPreview.SetView(modelView.SelectedIndex); };
            var scaleBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(4) };
            modelTab.Controls.Add(scaleBar);

            scaleBar.Controls.Add(new Label { Text = L.T("RR-Größe %", "RR size %"), AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            scaleBar.Controls.Add(modelScale);
            scaleBar.SetFlowBreak(modelScale, true);
            scaleBar.Controls.Add(modelView);
            scaleBar.SetFlowBreak(modelView, true);
            modelScale.ValueChanged += delegate {
                modelPreview.ModelScale = (model == null ? 1 : model.FitScale) * (float)modelScale.Value / 100;
                if (!loading) { dirty = true; reviewedRig = null; ModelChanged(); RenderModel(); }
            };
            Button(scaleBar, L.T("Größe zurücksetzen", "Reset scale"), delegate { modelScale.Value = 100; });
            Button(scaleBar, L.T("Skalierte Kopie speichern…", "Save scaled copy…"), SaveScaledModel);
            var wire = new CheckBox { Text = L.T("Drahtgitter", "Wireframe"), AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
            wire.CheckedChanged += delegate { modelPreview.Wireframe = wire.Checked; modelPreview.Invalidate(); };
            scaleBar.Controls.Add(wire);
            Button(scaleBar, L.T("Ansicht zurücksetzen", "Reset view"), delegate { modelPreview.ResetView(); });
            var modelLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0) };
            modelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 234));
            modelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            modelTab.Controls.Clear();
            modelTab.AutoScroll = true;
            modelLayout.Dock = DockStyle.Top;
            modelTab.Controls.Add(modelLayout);
            modelTab.SizeChanged += delegate { modelLayout.Height = Math.Max(520, modelTab.ClientSize.Height); };
            modelLayout.Height = 520;
            modelLayout.Controls.Add(modelBar, 0, 0);
            modelLayout.SetColumnSpan(modelBar, 2);
            modelLayout.Controls.Add(scaleBar, 0, 1);
            modelLayout.SetRowSpan(scaleBar, 2);
            modelLayout.Controls.Add(modelPreview, 1, 1);
            modelInfo.Dock = DockStyle.Fill;
            modelLayout.Controls.Add(modelInfo, 1, 2);
            modelInfo.Text = L.T("GLB, glTF, BLEND, USDZ, DAE oder OBJ importieren. Studio zeigt Modellgröße, Materialfarben und vorhandene Knochen. BLEND/USDZ werden intern verarbeitet. Für einen animierten RR-Fahrer muss das Skelett zum Ersatzziel passen.", "Import GLB, glTF, BLEND, USDZ, DAE or OBJ. Studio shows geometry, material colours and existing bones. BLEND/USDZ are processed internally. An animated RR driver needs a rig matching the replacement target.");

            names.Columns.Add(new DataGridViewTextBoxColumn { Name = "character", Visible = false, ReadOnly = true });
            names.Columns.Add(new DataGridViewTextBoxColumn { Name = "id", Visible = false, ReadOnly = true });
            names.Columns.Add("name", L.T("Neuer Name", "Replacement name"));
            names.Columns.Add("author", L.T("Autor", "Author"));
            names.Columns.Add(new DataGridViewTextBoxColumn { Name = "target", HeaderText = L.T("Ersetzt", "Replaces"), ReadOnly = true, DisplayIndex = 0 });
            names.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; Status.Text = L.T("Gültigen Namen eingeben.", "Enter a valid name."); };
            var nameFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4, Padding = new Padding(8) };
            nameFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            nameFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            nameFields.Controls.Add(nameTarget, 0, 0);
            nameFields.SetColumnSpan(nameTarget, 2);
            nameFields.Controls.Add(new Label { Text = L.T("Neuer Charaktername", "Replacement character name"), AutoSize = true }, 0, 1);
            nameFields.Controls.Add(new Label { Text = L.T("Autor", "Author"), AutoSize = true }, 1, 1);
            nameFields.Controls.Add(nameEditor, 0, 2);
            nameFields.Controls.Add(authorEditor, 1, 2);
            var nameHelp = new Label { Text = L.T("Direkt in die Felder schreiben. Weitere importierte Varianten unten auswählen.", "Type directly into the fields. Select other imported variants below."), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
            nameFields.Controls.Add(nameHelp, 0, 3);
            nameFields.SetColumnSpan(nameHelp, 2);
            nameTab.Controls.Add(names);
            nameTab.Controls.Add(nameFields);
            names.SelectionChanged += delegate { SyncNameFields(); };
            nameEditor.TextChanged += delegate { WriteNameField(2, nameEditor.Text); };
            authorEditor.TextChanged += delegate { WriteNameField(3, authorEditor.Text); };
            var nameBar = Bar(nameTab);
            Button(nameBar, L.T("Gewählte Variante bearbeiten", "Edit selected variant"), delegate { AddTargetName(); nameEditor.Focus(); nameEditor.SelectAll(); });
            Button(nameBar, L.T("Namen importieren…", "Import names…"), ImportNames);
            Button(nameBar, L.T("Archiv hinzufügen…", "Add archive…"), AddArchives);
            Button(nameBar, L.T("Textvorschau…", "Preview text…"), PreviewNames);
            checkTab.Controls.Add(report);
            var checkBar = Bar(checkTab);
            Button(checkBar, L.T("Dateien prüfen", "Check files"), RefreshReport);
            ExportAction(L.T("Projekt speichern…", "Save project…"), L.T("Arbeitsstand speichern. Zum Installieren anschließend Charakter exportieren wählen.", "Save your work. Use Export character to create installable game files."), SaveProject, false);
            ExportAction(L.T("Nur Namen exportieren…", "Export names only…"), L.T("Nur Namensarchive nach Character replacement files exportieren; das Modell bleibt unverändert.", "Export name archives to Character replacement files; this does not replace the model."), delegate { Export(false); }, false);
            build = ExportAction(L.T("Charakter exportieren…", "Export character…"), L.T("Fahrer, Renn-/Menüfahrzeuge und Namensarchive gemeinsam nach MUR_EDITED/Character replacement files exportieren.", "Export the driver, race/menu vehicles and name archives together to MUR_EDITED/Character replacement files."), delegate { Export(true); });
            Actions.Padding = new Padding(0);
            Footer.Padding = new Padding(8, 2, 8, 2);
            foreach (Control button in Footer.Controls) button.Margin = new Padding(3);
            Body.Controls.Add(tabs);
            Body.Controls.Add(selection);
            names.CellValueChanged += delegate { if (!loading) dirty = true; RefreshTarget(); if (!syncingName) SyncNameFields(); };
            names.RowsRemoved += delegate { if (!loading) dirty = true; RefreshTarget(); };
            RefreshReport();
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (dirty && !ConfirmDiscard()) e.Cancel = true; };
            MinimumSize = new Size(950, 680);
            Size = new Size(1120, 850);
            // Im kleinen Fenster bleibt die Modellfläche wichtiger als ein hoher Kopfbereich.
            var workspace = Body.Parent as TableLayoutPanel;
            SizeChanged += delegate {
                bool compact = ClientSize.Height < 720;
                if (workspace != null) workspace.RowStyles[0].Height = compact ? 120 : 152;
                foreach (Control hint in Controls.Find("ToolFileExamples", true)) hint.Visible = !compact;
            };
            Finish();
            DarkTheme.StyleTabs(tabs);
            DarkTheme.StyleGrid(names);
            StyleFileList();
            RefreshFileActions();
            PackSelection.SourceStep(this, L.T("Charakter ersetzen…", "Replace character…"), L.T("„Charakter ersetzen“ öffnen und eine vorhandene RR-Variante nach Bild und Name wählen.\nDanach eigene Ersatzdateien oder ein 3D-Modell hinzufügen.", "Open Replace character and choose an installed RR variant by picture and name.\nThen add your replacement files or a 3D model."), false);
        }
        internal sealed class CharacterChoice { public int Id { get; set; } public string Name { get; set; } }
        FlowLayoutPanel Bar(Control parent)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
            parent.Controls.Add(bar);
            return bar;
        }
        Button Button(FlowLayoutPanel bar, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(3) };
            button.Click += delegate { Guard(action); };
            if (text == L.T("Archiv hinzufügen…", "Add archive…")) button.Name = "InToolSourceAction";
            bar.Controls.Add(button);
            return button;
        }
        bool ConfirmDiscard()
        {
            return !dirty || StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen am Charakterprojekt verwerfen?", "Discard unsaved character project changes?"), Text, MessageBoxButtons.YesNo) == DialogResult.Yes;
        }
        void ClearData()
        {
            loading = true;
            assets.Clear(); archives.Clear(); names.Rows.Clear(); files.Items.Clear();
            model = null; reviewedRig = null; modelNeedsConversion = false;
            if (prepareModelButton != null) prepareModelButton.Enabled = convertModelButton.Enabled = false;
            modelPreview.Model = null; modelScale.Value = 100;
            targetPortrait.Image = null;
            if (replacementPortrait.Image != null) replacementPortrait.Image.Dispose();
            replacementPortrait.Image = null;
            target = null;
            if (catalog != null) catalog.Dispose();
            catalog = null;
            loading = false; dirty = false;
            RefreshFileActions();
            RefreshReport();
        }
        void Clear()
        {
            if (!ConfirmDiscard()) return;
            ClearData();
            PackSelection.SourceCleared(this);
        }
        void NewProject()
        {
            if (String.IsNullOrEmpty(PackSelection.Folder(this))) throw new InvalidOperationException(L.T("Zuerst ein eigenes Pack wählen oder erstellen.", "Choose or create your custom pack first."));
            if (!ConfirmDiscard()) return;
            using (var picker = new CharacterPickerForm(PackSelection.Folder(this), catalog == null ? null : catalog.Root))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                StartReplacement(picker.TakeCatalog(), picker.Selected);
            }
        }

        internal void StartReplacement(CharacterCatalog source, CharacterVariant variant)
        {
            if (variant == null || source.Find(variant.Character.Id, variant.Slot) != variant)
                throw new InvalidDataException("Choose a variant from this RR installation.");
            source.RequireExisting(variant.Character.Id, variant.Slot);
            ClearData();
            catalog = source; target = variant;
            foreach (var archive in source.Archives) archives[archive.Key] = archive.Value;
            loading = true;
            AddTargetName();
            loading = false; dirty = false;
            RefreshTarget(); RefreshReport();
            PackSelection.SourceLoaded(this);
            tabs.SelectedIndex = 0;
        }

        void RequireTarget()
        {
            if (catalog == null || target == null)
                throw new InvalidDataException(L.T("Zuerst einen vorhandenen RR-Charakter auswählen.", "Choose an installed RR character first."));
            catalog.RequireExisting(Selected.Id, Slot);
        }

        void AddTargetName()
        {
            RequireTarget();
            foreach (DataGridViewRow row in names.Rows)
                if (Convert.ToInt32(row.Cells[0].Value) == Selected.Id && Convert.ToInt32(row.Cells[1].Value) == Slot)
                {
                    names.CurrentCell = row.Cells[2];
                    SyncNameFields();
                    return;
                }
            int index = names.Rows.Add(Selected.Id, Slot, target.Name, target.Author, target.ToString());
            names.CurrentCell = names.Rows[index].Cells[2];
            SyncNameFields();
            RefreshTarget();
        }

        void SyncNameFields()
        {
            syncingName = true;
            try
            {
                var row = names.CurrentRow;
                nameEditor.Enabled = authorEditor.Enabled = row != null;
                nameEditor.Text = row == null ? "" : Convert.ToString(row.Cells[2].Value);
                authorEditor.Text = row == null ? "" : Convert.ToString(row.Cells[3].Value);
                nameTarget.Text = row == null ? L.T("Zuerst einen Charakter wählen.", "Choose a character first.")
                    : L.T("Du bearbeitest: ", "Editing: ") + Convert.ToString(row.Cells[4].Value);
            }
            finally { syncingName = false; }
        }

        void WriteNameField(int column, string value)
        {
            if (syncingName || names.CurrentRow == null) return;
            syncingName = true;
            try { names.CurrentRow.Cells[column].Value = value; }
            finally { syncingName = false; }
        }

        void RefreshTarget()
        {
            targetPortrait.Image = target == null ? null : target.Portrait;
            targetCaption.Text = target == null ? L.T("Noch kein Ersatzziel gewählt", "No replacement target selected")
                : L.T("Ersetzt: ", "Replaces: ") + target.Name + Environment.NewLine + L.T("Basis: ", "Base: ") + target.Basis
                    + Environment.NewLine + (target.HasOwnPortrait ? L.T("RR-Icon", "RR icon") : L.T("Basisbild · kein eigenes Icon", "Base portrait · no variant icon"));
            string name = null;
            if (target != null)
                foreach (DataGridViewRow row in names.Rows)
                    if (Convert.ToInt32(row.Cells[0].Value) == Selected.Id && Convert.ToInt32(row.Cells[1].Value) == Slot) name = Convert.ToString(row.Cells[2].Value);
            replacementCaption.Text = L.T("Dein Ersatz: ", "Your replacement: ") + (String.IsNullOrWhiteSpace(name) ? L.T("Name unter „Namen & Autoren“", "Name in Names & authors") : name)
                + Environment.NewLine + assets.Count + L.T(" Dateien zugeordnet", " files assigned")
                + Environment.NewLine + (replacementPortrait.Image == null ? L.T("Noch kein eigenes Icon geladen", "No replacement icon loaded yet") : L.T("Eigenes Minimap-Icon", "Your minimap icon"));
        }

        void EditImages()
        {
            RequireTarget();
            using (var dialog = new CharacterImagesForm(target, catalog.Root, assets, () => PackSelection.Output(this, "")))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Changes.Count == 0) return;
                foreach (var pair in dialog.Changes)
                {
                    assets.RemoveAll(a => a.Target.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                    assets.Add(pair.Value);
                }
                dirty = true;
                UpdateFiles();
                RefreshReport();
            }
        }

        void RefreshReplacementImage()
        {
            if (replacementPortrait.Image != null) replacementPortrait.Image.Dispose();
            replacementPortrait.Image = null;
            var icon = assets.FirstOrDefault(a => a.Target.StartsWith("Character/Map/", StringComparison.OrdinalIgnoreCase));
            if (icon == null) return;
            TexturePreviewResult decoded; string error;
            if (TexturePreview.TryDecode(icon.Source, icon.Data, 0, out decoded, out error))
                using (decoded) replacementPortrait.Image = new Bitmap(decoded.Bitmap);
        }

        string[] Pick(string filter, bool multi)
        {
            using (var dialog = new OpenFileDialog { Filter = filter, Multiselect = multi, InitialDirectory = PackSelection.Folder(this) })
                return ToolArchiveFilters.Show(dialog, this) == DialogResult.OK ? ToolArchiveFilters.SelectedFiles(dialog) : new string[0];
        }
        void AddFiles()
        {
            RequireTarget();
            string code = Selected.Code;
            string[] selected = null;
            if (CharacterPackage.Missing(Selected, Slot, assets).Count > 0)
                selected = RrSourceRecovery.Choose(this, "*.szs;*.brres;*.tpl", catalog.Root, Selected, Slot);
            if (selected == null) selected = Pick("Character files|" + "*-" + code + "*.szs;" + code + "*.brres;" + code + "*.tpl", true);
            var next = assets.ToList();
            var nextArchives = new Dictionary<string, string>(archives, StringComparer.OrdinalIgnoreCase);
            foreach (string path in selected)
            {
                string name = Path.GetFileName(path);
                if (name.Equals("UIAssets.szs", StringComparison.OrdinalIgnoreCase) || name.Equals("RaceAssets.szs", StringComparison.OrdinalIgnoreCase))
                {
                    new StudioArchiveCopy(path);
                    nextArchives[name] = path;
                    continue;
                }
                var asset = CharacterPackage.ReadAsset(path, Selected, Slot);
                var duplicate = next.FirstOrDefault(a => a.Target.Equals(asset.Target, StringComparison.OrdinalIgnoreCase));
                if (duplicate != null)
                {
                    if (duplicate.Source.Equals(asset.Source, StringComparison.OrdinalIgnoreCase)) continue;
                    throw new InvalidDataException(L.T("Vor dem Ersetzen vorhandenen Eintrag entfernen: ", "Remove the existing entry before replacing ") + asset.Target);
                }
                next.Add(asset);
            }
            assets.Clear(); assets.AddRange(next);
            archives.Clear(); foreach (var pair in nextArchives) archives.Add(pair.Key, pair.Value);
            if (selected.Length > 0) dirty = true;
            UpdateFiles();
        }
        void UpdateFiles()
        {
            files.Items.Clear();
            foreach (var asset in assets)
                files.Items.Add(new ListViewItem(new[] { asset.Role, asset.Target, asset.Source }) { Tag = asset, ToolTipText = asset.Source });
            RefreshReplacementImage();
            RefreshTarget();
            RefreshFileActions();
            FitFileColumns();
            RefreshReport();
        }
        void StyleFileList()
        {
            files.BackColor = DarkTheme.Panel2;
            files.ForeColor = DarkTheme.Fore;
            fileEmpty.BackColor = DarkTheme.Panel2;
            fileEmpty.ForeColor = DarkTheme.Muted;
            fileGuide.ForeColor = DarkTheme.Muted;
            files.OwnerDraw = true;
            files.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e) {
                using (var brush = new SolidBrush(DarkTheme.Panel3)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, files.Font, Rectangle.Inflate(e.Bounds, -8, 0),
                    DarkTheme.Fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            files.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { };
            files.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e) {
                using (var brush = new SolidBrush(e.Item.Selected ? DarkTheme.AccentSoft : DarkTheme.Panel2))
                    e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, files.Font, Rectangle.Inflate(e.Bounds, -8, 0),
                    DarkTheme.Fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            FitFileColumns();
        }

        void FitFileColumns()
        {
            if (files.Columns.Count != 3 || files.ClientSize.Width < 100) return;
            int width = files.ClientSize.Width;
            files.Columns[0].Width = width * 18 / 100;
            files.Columns[1].Width = width * 38 / 100;
            files.Columns[2].Width = Math.Max(1, width - files.Columns[0].Width - files.Columns[1].Width);
        }

        void RefreshFileActions()
        {
            if (addFilesButton == null) return;
            fileEmpty.Visible = files.Items.Count == 0;
            files.Visible = files.Items.Count != 0;
            if (fileEmpty.Visible) fileEmpty.BringToFront();
            addFilesButton.BackColor = files.Items.Count == 0 ? DarkTheme.Accent : DarkTheme.Back;
            addFilesButton.ForeColor = Color.White;
            removeFilesButton.Enabled = inspectFilesButton.Enabled = files.SelectedItems.Count > 0;
            fileGuide.Text = files.Items.Count + L.T(" Dateien geladen · Benötigt: Fahrer + 12 Fahrzeugarchive · Optional: AllKart / Map",
                " files loaded · Required: driver + 12 vehicle archives · Optional: AllKart / Map");
        }

        void RemoveFiles()
        {
            foreach (ListViewItem item in files.SelectedItems) assets.Remove((CharacterAsset)item.Tag);
            dirty = true; UpdateFiles();
        }
        void InspectSelected()
        {
            if (files.SelectedItems.Count == 0) return;
            var asset = (CharacterAsset)files.SelectedItems[0].Tag;
            using (var dialog = new FilePreviewForm(asset.Source)) dialog.ShowDialog(this);
        }
        void AddArchives()
        {
            var selected = Pick("RR character text archives|UIAssets.szs;RaceAssets.szs", true);
            var next = new Dictionary<string, string>(archives, StringComparer.OrdinalIgnoreCase);
            foreach (string path in selected)
            {
                new StudioArchiveCopy(path);
                string name = Path.GetFileName(path);
                if (next.ContainsKey(name) && !next[name].Equals(path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("Vor der Auswahl einer anderen Datei das Projekt leeren: ", "Clear the project before choosing a different ") + name);
                next[name] = path;
            }
            archives.Clear(); foreach (var pair in next) archives.Add(pair.Key, pair.Value);
            dirty |= selected.Length > 0; RefreshReport();
        }
        List<CharacterNameEntry> ReadNames()
        {
            names.EndEdit();
            var result = new List<CharacterNameEntry>();
            foreach (DataGridViewRow row in names.Rows)
            {
                if (row.IsNewRow) continue;
                int c, id;
                if (!Int32.TryParse(Convert.ToString(row.Cells[0].Value), out c) || !Int32.TryParse(Convert.ToString(row.Cells[1].Value), out id))
                    throw new InvalidDataException(L.T("Jede Namenszeile benötigt eine vorhandene Variante.", "Choose an existing variant for every name row."));
                result.Add(new CharacterNameEntry { CharacterId = c, CustomId = id, Name = Convert.ToString(row.Cells[2].Value), Author = Convert.ToString(row.Cells[3].Value) });
            }
            CharacterNames.Validate(result);
            foreach (var entry in result)
            {
                RequireTarget();
                catalog.RequireExisting(entry.CharacterId, entry.CustomId);
            }
            return result;
        }
        void ImportNames()
        {
            var selected = Pick(L.T("Charakternamen|CharaName.bmg;CharaName.txt", "Character names|CharaName.bmg;CharaName.txt"), false);
            if (selected.Length == 0) return;
            string path = selected[0];
            var imported = CharacterNames.Read(Path.GetExtension(path).Equals(".bmg", StringComparison.OrdinalIgnoreCase) ? BmgTextDocument.Decode(File.ReadAllBytes(path)) : File.ReadAllText(path));
            var existing = ReadNames();
            CharacterNames.Validate(imported);
            RequireTarget();
            foreach (var entry in imported) catalog.RequireExisting(entry.CharacterId, entry.CustomId);
            var merged = existing.ToDictionary(e => e.NameId);
            foreach (var entry in imported) merged[entry.NameId] = entry;
            names.Rows.Clear();
            foreach (var entry in merged.Values) names.Rows.Add(entry.CharacterId, entry.CustomId, entry.Name, entry.Author, catalog.Find(entry.CharacterId, entry.CustomId).ToString());
            dirty = true;
        }
        void PreviewNames()
        {
            using (var form = new Form { Text = L.T("CharaName-Textvorschau", "CharaName text preview"), Size = new Size(740, 500), StartPosition = FormStartPosition.CenterParent })
            {
                form.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, Text = CharacterNames.Merge(null, ReadNames()).Replace("\n", Environment.NewLine) });
                DarkTheme.Apply(form);
                form.ShowDialog(this);
            }
        }
        void ImportModel()
        {
            var selected = Pick(L.T("Eigenes 3D-Modell|*.glb;*.gltf;*.blend;*.usdz;*.dae;*.obj", "Own 3D model|*.glb;*.gltf;*.blend;*.usdz;*.dae;*.obj"), false);
            if (selected.Length == 0) return;
            RequireTarget();
            var candidate = ModelOperationForm.Run(this, L.T("3D-Modell importieren", "Import 3D model"), token => {
                var imported = CharacterModelImport.LoadVisual(selected[0], token);
                var reference = StudioModelLibrary.Reference(target.DriverPath, Path.Combine(ModelRuntime.NewWorkFolder(), "reference.dae"));
                imported.FitTo(reference);
                return imported;
            });
            if (candidate == null) return;
            reviewedRig = null; model = candidate; modelScale.Value = 100; dirty = true; ModelChanged(); RenderModel();
        }
        void RenderModel()
        {
            prepareModelButton.Enabled = model != null;
            convertModelButton.Enabled = reviewedRig != null;
            if (model == null) return;
            modelPreview.Model = reviewedRig == null ? model : reviewedRig.Preview();
            modelPreview.ModelScale = reviewedRig == null ? model.FitScale * (float)modelScale.Value / 100 : 1;
            modelInfo.Text = (reviewedRig == null ? L.T("Nächster Schritt: Bewegungen automatisch zuordnen und prüfen.", "Next: assign movement automatically and review it.") : L.T("Haltung übernommen. Schritt 3: Charakter exportieren. Studio erstellt dabei automatisch alle RR-Dateien. Alles zum Kopieren liegt in MUR_EDITED/Character replacement files.", "Pose accepted. Step 3: Export character. Studio creates all RR files automatically. Everything to copy goes into MUR_EDITED/Character replacement files.")) + Environment.NewLine + Path.GetFileName(model.Source) + " — " + model.Points.Count + L.T(" Eckpunkte, ", " vertices, ") + model.Faces.Count + L.T(" Flächen, ", " faces, ") + model.Joints + L.T(" Gelenke, ", " joints, ") + model.Skins + L.T(" Skins.", " skins.") + Environment.NewLine + String.Join(Environment.NewLine, model.Warnings);
        }
        void SaveScaledModel()
        {
            if (model == null) throw new InvalidOperationException(L.T("Zuerst ein 3D-Modell importieren.", "Import a 3D model first."));
            bool integrated = model.PreparedSource != null;
            string extension = integrated ? ".glb" : Path.GetExtension(model.Source);
            using (var dialog = new SaveFileDialog {
                InitialDirectory = Path.GetDirectoryName(model.Source),
                FileName = Path.GetFileNameWithoutExtension(model.Source) + "-scaled" + extension,
                Filter = "Scaled model|*" + extension
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (integrated) IntegratedModelImport.ExportCopy(model.PreparedSource, dialog.FileName, model.FitScale * (float)modelScale.Value / 100, "glb2");
                else CharacterModelScaler.SaveCopy(model.Source, dialog.FileName, model.FitScale * (float)modelScale.Value / 100);
                modelInfo.Text = L.T("Kopie gespeichert: ", "Copy saved: ") + dialog.FileName;
                Status.Text = L.T("Skalierte Kopie gespeichert. Die RR-Größeneinstellung im Projekt bleibt erhalten.", "Scaled copy saved. The project's RR size setting is retained.");
            }
        }
        void PrepareModel()
        {
            RequireTarget();
            if (model == null) throw new InvalidOperationException(L.T("Zuerst ein 3D-Modell importieren.", "Import a 3D model first."));
            var candidate = reviewedRig ?? ModelOperationForm.Run(this, L.T("RR-Bewegungen vorbereiten", "Prepare RR movement"),
                token => {
                    var rig = ModelRig.Prepare(model, target.DriverPath, (float)modelScale.Value, 6000, token);
                    rig.FitJointGuides();
                    try { rig.BindWithBlender(token); }
                    catch (InvalidDataException error) { rig.BindingWarning = error.Message; }
                    return rig;
                });
            if (candidate == null) return;
            var references = ModelOperationForm.Run(this, L.T("Originalmodelle als Hilfe laden", "Load original reference models"),
                token => RigReferenceSet.Load(target, catalog.Root));
            if (references == null) return;
            using (var review = new ModelRigForm(candidate, references, target.Character.Weight))
            {
                if (review.ShowDialog(this) != DialogResult.OK) return;
                reviewedRig = review.Result;
            }
            dirty = true; ModelChanged(); RenderModel();
        }
        void ModelChanged()
        {
            modelNeedsConversion = model != null;
            RefreshReport();
        }
        void ConvertModel()
        {
            Export(true);
        }
        bool CreateModelFiles()
        {
            RequireTarget();
            if (reviewedRig == null) throw new InvalidOperationException(L.T("Zuerst die automatische Zuordnung prüfen und übernehmen.", "Review and accept the automatic assignment first."));
            var converted = ModelOperationForm.Run(this, L.T("RR-Charakterdateien erstellen", "Create RR character files"),
                token => CharacterModelConversion.Build(reviewedRig, Selected, Slot, catalog.Root, assets, token));
            if (converted == null) return false;
            var replaced = new HashSet<string>(converted.Select(a => a.Target), StringComparer.OrdinalIgnoreCase);
            assets.RemoveAll(a => replaced.Contains(a.Target));
            assets.AddRange(converted);
            modelNeedsConversion = false;
            dirty = true; UpdateFiles(); RefreshReport();
            return true;
        }
        void RefreshReport()
        {
            RefreshTarget();
            if (Selected == null)
            {
                if (build != null) build.Enabled = false;
                report.Clear();
                Status.Text = L.T("Vorhandene RR-Variante über „Charakter ersetzen“ auswählen.", "Choose an installed RR variant using Replace character.");
                return;
            }
            var missing = CharacterPackage.Missing(Selected, Slot, assets);
            if (build != null) build.Enabled = File.Exists(target.DriverPath) && (reviewedRig != null || (!modelNeedsConversion && missing.Count == 0)) && archives.ContainsKey("UIAssets.szs") && archives.ContainsKey("RaceAssets.szs");
            report.Text = L.T("Ersetzt: ", "Replaces: ") + target.Name + " — " + target.Basis + Environment.NewLine +
                L.T("Geladen: ", "Loaded: ") + assets.Count + L.T(" Charakterdateien; ", " character files; ") + archives.Count + L.T("/2 RR-Textarchive.", "/2 RR text archives.") + Environment.NewLine +
                (modelNeedsConversion ? L.T("Modell bereit zur Erstellung: Haltung in Schritt 2 prüfen, dann Schritt 3 „Charakter exportieren“.", "Model awaiting export: review the pose in step 2, then use step 3, Export character.") + Environment.NewLine : "") +
                L.T("Fehlende Pflichtdateien:", "Required missing files:") + Environment.NewLine + (missing.Count == 0 ? L.T("Keine.", "None.") : String.Join(Environment.NewLine, missing)) + Environment.NewLine +
                String.Join(Environment.NewLine, new[] { "UIAssets.szs", "RaceAssets.szs" }.Where(n => !archives.ContainsKey(n))) + Environment.NewLine + Environment.NewLine +
                L.T("Optional: Minikarten-TPL. Eigene Spielhaltungen exportieren auch die AllKart-Menüfahrzeuge. Originalstimmen bleiben erhalten; eigene Stimmen werden noch nicht importiert.", "Optional: minimap TPL. Custom game poses also export the allkart menu vehicles. Original voices are kept; custom voice import is not yet supported.") + Environment.NewLine +
                L.T("Passende Dateistruktur beweist keine Skelett-/Animationskompatibilität. Vor Weitergabe in Dolphin testen.", "Matching file structure does not prove skeleton/animation compatibility. Test in Dolphin before distributing.") + Environment.NewLine +
                L.T("Charakter exportieren → MUR_EDITED/Character replacement files. Alle Dateien daraus in den Custom-Pack-Hauptordner kopieren. Projekt speichern erzeugt keine installierbare Kopie; „Nur Namen“ ersetzt kein Modell.", "Export character → MUR_EDITED/Character replacement files. Copy all files from there into your custom pack root. Save project stores your work; Names only does not replace a model.");
            Status.Text = L.T("Charakterziel: ", "Character target: ") + target.Name + " • " + assets.Count + L.T(" Dateien • Namensarchive: ", " files • Names archives: ") + String.Join(", ", archives.Keys);
        }
        void Export(bool full)
        {
            RequireTarget();
            var entries = ReadNames();
            if (entries.Count == 0) throw new InvalidDataException(L.T("Mindestens einen Namen hinzufügen.", "Add at least one name."));
            if (full)
            {
                if (modelNeedsConversion)
                {
                    if (reviewedRig == null)
                    {
                        tabs.SelectedIndex = 1;
                        throw new InvalidOperationException(L.T("Schritt 2: Haltung und Bewegung prüfen und übernehmen. Danach Schritt 3: Charakter exportieren.",
                            "Step 2: review and accept Pose & movement. Then step 3: Export character."));
                    }
                    if (!archives.ContainsKey("UIAssets.szs") || !archives.ContainsKey("RaceAssets.szs"))
                        throw new InvalidDataException(L.T("Zuerst UIAssets.szs und RaceAssets.szs aus deinem RR-Pack laden.", "Load UIAssets.szs and RaceAssets.szs from your RR pack first."));
                    if (!CreateModelFiles()) return;
                }
                var missing = CharacterPackage.Missing(Selected, Slot, assets);
                if (missing.Count != 0 || !archives.ContainsKey("UIAssets.szs") || !archives.ContainsKey("RaceAssets.szs"))
                {
                    tabs.SelectedIndex = 3; RefreshReport();
                    throw new InvalidDataException(L.T("Vor dem Erstellen alle Pflichtdateien und beide RR-Textarchive hinzufügen.", "Complete the required character files and both RR text archives before building the package."));
                }
                if (!entries.Any(e => e.CharacterId == Selected.Id && e.CustomId == Slot))
                    throw new InvalidDataException(L.T("Einen Namen für die gewählte Variante hinzufügen.", "Add a name for the selected variant."));
            }
            string parent = PackSelection.Output(this, "");
            if (String.IsNullOrEmpty(parent)) parent = Folder("");
            if (parent == null) return;
            string destination = ExportCopiesTo(parent, full, entries);
            Status.Text = L.T("Exportierte Spieldateien: ", "Exported game files: ") + destination;
            StudioMessageBox.Show(this, (full ? L.T("Charakter vollständig exportiert.", "Complete character exported.")
                : L.T("Nur Namen und Autoren exportiert. Das 3D-Modell wurde dadurch nicht ersetzt.", "Only names and authors exported. This does not replace the 3D model."))
                + "\n\n" + destination + "\n\n"
                + L.T("Alle Dateien aus diesem Ordner in den Hauptordner deines Custom Packs kopieren und gleichnamige Dateien ersetzen. Danach das Pack im Launcher erneut aktivieren/starten.\n\n„Projekt speichern“ speichert nur deinen Arbeitsstand; „Charakter exportieren“ erzeugt die Spieldateien.",
                    "Copy every file from this folder into your custom pack's main folder and replace matching filenames. Then activate/launch the pack again in your launcher.\n\nSave project stores your work; Export character creates the game files."), Text, MessageBoxButtons.OK);
        }
        internal string ExportCopiesTo(string parent, bool full, IList<CharacterNameEntry> entries)
        {
            var output = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var archive in archives)
            {
                string previous = Path.Combine(parent, CharacterReplacementExport.FolderName, archive.Key);
                output.Add(archive.Key, CharacterNames.PatchArchive(File.Exists(previous) ? previous : archive.Value, entries));
            }
            if (full) foreach (var asset in assets) output.Add(asset.Target, asset.Data);
            return CharacterReplacementExport.Write(parent, output);
        }

        void SaveProject()
        {
            RequireTarget();
            string path = SavePath("Character.murcharacter", L.T("Studio-Charakterprojekt|*.murcharacter", "Studio character project|*.murcharacter"));
            if (path != null) SaveProjectTo(path);
        }
        internal void SaveProjectTo(string path)
        {
            RequireTarget();
            var data = new Project { Version = 2, RRRoot = catalog.Root, Character = Selected.Id, Slot = Slot, Archives = archives.Values.ToArray(), ScalePercent = modelScale.Value, FitScale = model == null ? 1 : model.FitScale, ReferenceHeight = model == null ? 0 : model.ReferenceHeight, Names = ReadNames(), ModelNeedsConversion = modelNeedsConversion };
            string sidecar = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "-files-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(sidecar);
            var savedFiles = new List<string>();
            foreach (var asset in assets)
            {
                string copy = Path.Combine(sidecar, asset.Target.Replace('/', Path.DirectorySeparatorChar) + ".murasset");
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.WriteAllBytes(copy, asset.Data); savedFiles.Add(copy);
            }
            data.Files = savedFiles.ToArray();
            if (model != null && Path.GetExtension(model.Source) != ".json")
            {
                data.Model = Path.Combine(sidecar, "source.glb");
                IntegratedModelImport.ExportCopy(model.PreparedSource ?? model.Source, data.Model, 1, "glb2");
            }
            if (reviewedRig != null)
            {
                string folder = Path.Combine(sidecar, "movement"); Directory.CreateDirectory(folder);
                foreach (string texture in reviewedRig.Materials.Select(m => m.Texture).Where(t => t != null).Distinct())
                    File.Copy(Path.Combine(reviewedRig.Folder, texture), Path.Combine(folder, texture));
                File.Copy(reviewedRig.Reference, Path.Combine(folder, "reference.dae"));
                data.Rig = Path.Combine(folder, "rig.json"); reviewedRig.Save(data.Rig);
            }
            BackupManager.WriteAllBytesSafely(path, Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(data)));
            dirty = false;
            Status.Text = reviewedRig == null && model != null
                ? L.T("Projekt gespeichert. Nächster Schritt: 2 · Haltung & Bewegung.", "Project saved. Next: 2 · Pose & movement.")
                : L.T("Projekt gespeichert. Nächster Schritt: 3 · Charakter exportieren. Das Projekt allein verändert das Spiel nicht.",
                    "Project saved. Next: 3 · Export character. Saving the project alone does not change the game.");
        }
        void OpenProject()
        {
            var selected = Pick(L.T("Studio-Charakterprojekt|*.murcharacter", "Studio character project|*.murcharacter"), false);
            if (selected.Length == 0 || !ConfirmDiscard()) return;
            LoadProject(selected[0]);
        }
        static CharacterAsset ReadProjectAsset(string path, CharacterDefinition character, int slot)
        {
            if (!path.EndsWith(".murasset", StringComparison.OrdinalIgnoreCase)) return CharacterPackage.ReadAsset(path, character, slot);
            // Projektkopien dürfen vom Pack-Launcher nicht als aktive Spieldateien erkannt werden.
            string copy = Path.Combine(ModelRuntime.NewWorkFolder(), Path.GetFileNameWithoutExtension(path));
            File.Copy(path, copy);
            return CharacterPackage.ReadAsset(copy, character, slot);
        }
        internal void LoadProject(string path)
        {
            var data = new JavaScriptSerializer().Deserialize<Project>(File.ReadAllText(path));
            if (data == null || (data.Version != 1 && data.Version != 2) || data.Character < 0 || data.Character >= CharacterDefinition.All.Length || data.Slot < 1 || data.Slot > 50)
                throw new InvalidDataException(L.T("Nicht unterstütztes Charakterprojekt.", "Unsupported character project."));
            if (data.ScalePercent != 0 && (data.ScalePercent < 1 || data.ScalePercent > 10000)) throw new InvalidDataException(L.T("Ungültige Modellgröße.", "Invalid model scale."));
            CharacterNames.Validate(data.Names ?? new List<CharacterNameEntry>());
            var nextAssets = (data.Files ?? new string[0]).Select(p => ReadProjectAsset(p, CharacterDefinition.All[data.Character], data.Slot)).ToList();
            if (nextAssets.Select(a => a.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nextAssets.Count) throw new InvalidDataException(L.T("Doppelte Charakterziele.", "Duplicate character targets."));
            var nextArchives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in data.Archives ?? new string[0])
            {
                string name = Path.GetFileName(p);
                if (!new[] { "UIAssets.szs", "RaceAssets.szs" }.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException(L.T("Nicht unterstütztes Textarchiv.", "Unsupported text archive."));
                new StudioArchiveCopy(p); nextArchives.Add(name, p);
            }
            var nextRig = String.IsNullOrEmpty(data.Rig) ? null : ModelRig.Read(data.Rig);
            var nextModel = String.IsNullOrEmpty(data.Model) || !File.Exists(data.Model) && nextRig != null
                ? (nextRig == null ? null : nextRig.Preview()) : CharacterModelImport.LoadVisual(data.Model, System.Threading.CancellationToken.None);
            if (nextModel != null)
            {
                if (Single.IsNaN(data.FitScale) || Single.IsInfinity(data.FitScale) || data.FitScale < 0 || data.FitScale > 1000000
                    || Single.IsNaN(data.ReferenceHeight) || Single.IsInfinity(data.ReferenceHeight) || data.ReferenceHeight < 0)
                    throw new InvalidDataException("Invalid model reference size.");
                nextModel.FitScale = data.FitScale == 0 ? 1 : data.FitScale;
                nextModel.ReferenceHeight = data.ReferenceHeight;
            }
            string rrRoot = data.RRRoot;
            if (String.IsNullOrEmpty(rrRoot))
            {
                var roots = RetroRewindSource.Discover().Where(r => Directory.Exists(Path.Combine(r, "Character", "Driver"))).ToArray();
                if (catalog != null) rrRoot = catalog.Root;
                else if (roots.Length == 1) rrRoot = roots[0];
                else throw new InvalidDataException(L.T("Zuerst über „Charakter ersetzen“ die passende RR-Installation auswählen, dann das Projekt öffnen.", "Choose the matching RR installation using Replace character, then open this project."));
            }
            var nextCatalog = CharacterCatalog.Load(rrRoot, PackSelection.Folder(this));
            try
            {
                nextCatalog.RequireExisting(data.Character, data.Slot);
                foreach (var entry in data.Names ?? new List<CharacterNameEntry>()) nextCatalog.RequireExisting(entry.CharacterId, entry.CustomId);
            }
            catch { nextCatalog.Dispose(); throw; }
            ClearData(); loading = true;
            catalog = nextCatalog; target = catalog.Find(data.Character, data.Slot);
            assets.AddRange(nextAssets);
            foreach (var pair in nextArchives) archives.Add(pair.Key, pair.Value);
            foreach (var e in data.Names ?? new List<CharacterNameEntry>()) names.Rows.Add(e.CharacterId, e.CustomId, e.Name, e.Author, catalog.Find(e.CharacterId, e.CustomId).ToString());
            modelScale.Value = data.ScalePercent == 0 ? 100 : data.ScalePercent;
            model = nextModel; reviewedRig = nextRig; modelNeedsConversion = data.ModelNeedsConversion; RenderModel(); UpdateFiles();
            if (model != null) tabs.SelectedIndex = 1;
            loading = false; dirty = false;
            RefreshFileActions(); PackSelection.SourceLoaded(this);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                modelPreview.Model = null;
                targetPortrait.Image = null;
                if (replacementPortrait.Image != null) replacementPortrait.Image.Dispose();
                replacementPortrait.Image = null;
                if (catalog != null) catalog.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
