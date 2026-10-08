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
            Text = L.T("Fertige Spieldateien hinzufügen\n\nWähle eigene BRRES-/SZS-Dateien oder ergänze fehlende Dateien aus deiner RR-Installation.\nZurück führt zur Auswahl deiner Änderung.",
                "Add ready-made game files\n\nChoose your BRRES/SZS files or add missing files from your RR installation.\nBack returns to the choice of changes.")
        };
        readonly Label fileGuide = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8, 3, 8, 3) };
        Button addFilesButton, removeFilesButton, inspectFilesButton;
        readonly StudioReadOnlyText report = new StudioReadOnlyText();
        readonly Label reviewOverview = new Label { Name = "CharacterReviewOverview", Dock = DockStyle.Fill, Padding = new Padding(18), UseMnemonic = false };
        Button reviewFix;
        Button exportNamesButton;
        FlowLayoutPanel checkActions;
        readonly Label modelGuide = new Label {
            Name = "ModelNextStep", Dock = DockStyle.Fill, AutoSize = true,
            Padding = new Padding(6, 3, 6, 5), UseMnemonic = false
        };
        readonly Label modelSummary = new Label { Name = "CharacterTargetContext", Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(4), ForeColor = DarkTheme.Fore, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft };
        readonly StudioReadOnlyText modelInfo = new StudioReadOnlyText();
        readonly CharacterModelViewport modelPreview = new CharacterModelViewport(false);
        readonly NumericUpDown modelScale = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 100, DecimalPlaces = 2, Width = 80 };
        readonly ComboBox movementSource = new ComboBox { Name = "MovementSource", DropDownStyle = ComboBoxStyle.DropDownList, Width = 192, DropDownWidth = 420 };
        readonly CheckBox starEffect = new CheckBox { Name = "CharacterStarEffect", AutoSize = true, Checked = true,
            Text = L.T("Stern-Effekt auf Charakter", "Star effect on character"), Margin = new Padding(3, 8, 3, 3) };
        readonly ComboBox animationStyle = new ComboBox { Name = "HumanAnimationStyle", DropDownStyle = ComboBoxStyle.DropDownList, Width = 192 };
        readonly CharacterPages tabs = new CharacterPages { Name = "CharacterTaskPages", Dock = DockStyle.Fill };
        readonly Panel styleTab = new Panel { Text = L.T("Logo & Farben", "Logo & colours") };
        CharacterImagesForm styleEditor;
        CharacterModelImport styleModel;
        ModelRig styleRig;
        bool applyingStyle;
        readonly List<CharacterAsset> assets = new List<CharacterAsset>();
        readonly Dictionary<string, string> archives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Button build;
        Button motionCheck, testExport;
        CharacterMotionReport motionReport;
        string lastExport, lastExportPack;
        CharacterExportSnapshot exportSnapshot;
        bool exportChanged;
        Button compareExport;
        readonly Label exportGuide = new Label { Name = "CharacterExportGuide", Dock = DockStyle.Top,
            AutoSize = true, UseMnemonic = false, Padding = new Padding(8), ForeColor = DarkTheme.Fore };
        CharacterModelImport model;
        ModelRig reviewedRig;
        Button importModelButton, prepareModelButton, resetScaleButton, scaledCopyButton;
        readonly Label editedModelHint = new Label { AutoSize = true, MaximumSize = new Size(232, 0), Visible = false };
        bool dirty, loading, modelNeedsConversion;
        readonly EditHistory<BuilderState> history = new EditHistory<BuilderState>(SameState);
        StudioUndoRedo historyActions;
        CharacterTextureView textureView;
        bool restoringHistory;
        ModelRig capturedRig, frozenRig;
        BuilderState savedHistoryState;
        sealed class BuilderState
        {
            internal CharacterModelImport Model;
            internal ModelRig Rig;
            internal CharacterAsset[] Assets;
            internal Dictionary<string, string> Archives;
            internal List<CharacterNameEntry> Names;
            internal decimal Scale;
            internal int Movement, Style, NameRow;
            internal bool Star, NeedsConversion;
        }
        readonly Label workflowHint = new Label { Name = "CharacterWorkflowHint", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        Button workflowNext;
        Button workflowBack, chooseModel, chooseFiles, saveProjectButton;
        int lastWorkPage = 5;
        System.Action nextWorkflowAction;
        sealed class CharacterPages : Panel
        {
            readonly List<Panel> pages = new List<Panel>();
            int selected = -1;
            internal event EventHandler SelectedIndexChanged;
            internal Panel SelectedTab { get { return selected < 0 ? null : pages[selected]; } }
            internal int SelectedIndex {
                get { return selected; }
                set {
                    if (value == selected) return;
                    if (value < 0 || value >= pages.Count) throw new ArgumentOutOfRangeException("value");
                    SuspendLayout();
                    if (selected >= 0) pages[selected].Visible = false;
                    selected = value;
                    pages[value].Visible = true;
                    pages[value].BringToFront();
                    ResumeLayout(true);
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
            internal void AddPages(params Panel[] items)
            {
                foreach (Panel item in items) {
                    item.Dock = DockStyle.Fill;
                    item.Margin = Padding.Empty;
                    item.Visible = false;
                    pages.Add(item);
                    Controls.Add(item);
                }
                SelectedIndex = pages.Count - 1;
            }
        }
        internal sealed class Project
        {
            public int Version { get; set; }
            public int Character { get; set; }
            public int Slot { get; set; }
            public string RRRoot { get; set; }
            public string[] Files { get; set; }
            public Dictionary<string, CharacterLogoRegion[]> LogoRegions { get; set; }
            public Dictionary<string, string> PaintBases { get; set; }
            public string[] Archives { get; set; }
            public string Model { get; set; }
            public string Rig { get; set; }
            public string MovementCode { get; set; }
            public string AnimationStyle { get; set; }
            public int MovementSlot { get; set; }
            public bool ModelNeedsConversion { get; set; }
            public bool? CharacterStarEffect { get; set; }
            public decimal ScalePercent { get; set; }
            public float FitScale { get; set; }
            public float ReferenceHeight { get; set; }
            public List<CharacterNameEntry> Names { get; set; }
        }
        CharacterDefinition Selected { get { return target == null ? null : target.Character; } }

        public CharacterBuilderForm() : base("RR-MKWii Character Builder Tool", L.T("Vorhandenen RR-Charakter wählen • Ersatzdateien hinzufügen • Kopien exportieren", "Choose an installed RR character • Add replacement files • Export copies"),
            "RR: driver BRRES + matching vehicle SZS · UIAssets.szs / RaceAssets.szs · Own models: GLB / glTF / BLEND / USDZ / DAE / OBJ")
        {
            loading = true;
            Action(L.T("Charakter ersetzen…", "Replace character…"), L.T("Vorhandene RR-Variante anhand von Name und Bild auswählen.", "Choose an installed RR variant by name and picture."), NewProject).Name = "PackSourceAction";
            Action(L.T("Projekt öffnen…", "Open project…"), L.T("Gespeichertes Charakterprojekt laden.", "Load a saved character project."), OpenProject).Name = "PackSourceAction";
            var clearButton = Action(L.T("Auswahl leeren", "Clear selection"), L.T("Projekt nach Rückfrage zu ungespeicherten Änderungen leeren.", "Clear this project after confirming unsaved work."), Clear);
            clearButton.Name = "CharacterClear";
            Actions.Controls.Remove(clearButton);
            Footer.Controls.Add(clearButton);
            StudioActions.Icon(clearButton, StudioIcon.Close);
            clearButton.Margin = new Padding(3);
            clearButton.Padding = new Padding(4, 0, 4, 0);
            var selection = new TableLayoutPanel { Name = "CharacterReplacementContext", Dock = DockStyle.Top, Height = 64, ColumnCount = 5, RowCount = 1, Padding = new Padding(4), BackColor = DarkTheme.Panel };
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            selection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            selection.Controls.Add(targetPortrait, 0, 0);
            selection.Controls.Add(targetCaption, 1, 0);
            selection.Controls.Add(new Label { Text = "→", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 2, 0);
            selection.Controls.Add(replacementPortrait, 3, 0);
            selection.Controls.Add(replacementCaption, 4, 0);

            var assetTab = new Panel { Text = L.T("Charakterdateien", "Character files") };
            var modelTab = new Panel { Text = L.T("Eigenes 3D-Modell", "Own 3D model") };
            var nameTab = new Panel { Text = L.T("Namen & Autoren", "Names & authors") };
            var checkTab = new Panel { Text = L.T("Prüfen & Export", "Check & export") };
            var changeChoice = new Panel { Name = "CharacterChangeChoice" };
            tabs.AddPages(assetTab, modelTab, nameTab, styleTab, checkTab, changeChoice);
            BuildChangeChoice(changeChoice);
            tabs.SelectedIndexChanged += delegate {
                if (tabs.SelectedTab == styleTab) Guard(EnsureStyleEditor);
            };
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
            StudioActions.Icon(removeFilesButton, StudioIcon.Remove);
            StudioActions.Icon(inspectFilesButton, StudioIcon.Search);
            AddOtherChanges(assetBar, 0);
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
            modelBar.Padding = new Padding(4, 0, 4, 0);
            importModelButton = Button(modelBar, L.T("Modell laden…", "Load model…"), ImportModel);
            prepareModelButton = Button(modelBar, L.T("Modell bearbeiten…", "Edit model…"), PrepareModel);
            prepareModelButton.UseMnemonic = false;
            prepareModelButton.Enabled = false;
            prepareModelButton.Name = "CharacterEditModel";
            DarkTheme.StylePrimary(prepareModelButton);
            StudioUx.SetHelp(prepareModelButton, L.T("Teile entfernen, Gelenke zuordnen und Menü- und Fahrhaltung prüfen.", "Remove parts, assign joints and review menu and driving poses."));
            var scaleBar = new FlowLayoutPanel { Name = "CharacterModelProperties", Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 8, 6, 8), Margin = Padding.Empty, BackColor = DarkTheme.Panel };
            modelTab.Controls.Add(scaleBar);
            scaleBar.Controls.Add(new Label { Text = L.T("Modelleinstellungen", "Model settings"), AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 0, 3, 10) });
            modelGuide.ForeColor = DarkTheme.Muted;
            modelGuide.Margin = Padding.Empty;

            animationStyle.Items.AddRange(ModelRig.AnimationStyleLabels());
            animationStyle.SelectedIndex = 0;
            scaleBar.Controls.Add(new Label { Text = L.T("Animationsstil", "Animation style"), AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            scaleBar.Controls.Add(animationStyle);
            scaleBar.SetFlowBreak(animationStyle, true);
            animationStyle.SelectedIndexChanged += delegate {
                if (loading || animationStyle.SelectedIndex < 0) return;
                string style = ModelRig.AnimationStyles[animationStyle.SelectedIndex];
                if (reviewedRig != null) { reviewedRig = CopyRig(reviewedRig); reviewedRig.SetAnimationStyle(style); }
                dirty = true; ModelChanged(); RenderModel();
                RecordHistory();
            };
            scaleBar.Controls.Add(starEffect);
            scaleBar.SetFlowBreak(starEffect, true);
            var starHelp = new Label { AutoSize = true, MaximumSize = new Size(202, 0),
                Text = L.T("Farbwechsel bei aktivem Stern.", "Colour cycling while using a Star.") };
            scaleBar.Controls.Add(starHelp);
            scaleBar.SetFlowBreak(starHelp, true);
            starEffect.CheckedChanged += delegate {
                if (loading) return;
                if (reviewedRig != null) { reviewedRig = CopyRig(reviewedRig); reviewedRig.StarEffect = starEffect.Checked; }
                dirty = true;
                ModelChanged();
                RecordHistory();
            };
            scaleBar.Controls.Add(new Label { Text = L.T("Gelenkvorlage", "Joint template"), AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            scaleBar.Controls.Add(movementSource);
            scaleBar.SetFlowBreak(movementSource, true);
            movementSource.SelectedIndexChanged += delegate {
                if (loading) return;
                reviewedRig = null;
                dirty = true;
                ModelChanged();
                RenderModel();
                RecordHistory();
            };
            var movementHelp = new Label { AutoSize = true, MaximumSize = new Size(232, 0),
                Text = L.T("Ein Wechsel setzt die Haltung zurück. Das Ersatzziel bleibt gleich.",
                    "Changing the template resets the pose. The replacement target stays the same.") };
            scaleBar.Controls.Add(movementHelp);
            scaleBar.SetFlowBreak(movementHelp, true);
            scaleBar.Controls.Add(new Label { Text = L.T("RR-Größe %", "RR size %"), AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            scaleBar.Controls.Add(modelScale);
            scaleBar.SetFlowBreak(modelScale, true);
            modelScale.ValueChanged += delegate {
                modelPreview.ModelScale = (model == null ? 1 : model.FitScale) * (float)modelScale.Value / 100;
                if (!loading) { dirty = true; reviewedRig = null; ModelChanged(); RenderModel(); RecordHistory("scale"); }
            };
            resetScaleButton = Button(scaleBar, L.T("Größe zurücksetzen", "Reset scale"), delegate { modelScale.Value = 100; });
            StudioActions.Icon(resetScaleButton, StudioIcon.Refresh);
            scaledCopyButton = Button(scaleBar, L.T("Skalierte Kopie speichern…", "Save scaled copy…"), SaveScaledModel);
            editedModelHint.Text = L.T("Teile entfernt. Größe unter „Modell bearbeiten → Haltung“ ändern. Die Gelenkvorlage bleibt erhalten.",
                "Parts removed. Change size in Edit model → Pose. The joint template stays fixed.");
            scaleBar.Controls.Add(editedModelHint);
            scaleBar.Controls.SetChildIndex(editedModelHint, 2);
            scaleBar.SetFlowBreak(editedModelHint, true);
            var modelLayout = new TableLayoutPanel { Name = "CharacterModelWorkspace", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Margin = Padding.Empty };
            modelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            modelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            modelTab.Controls.Clear();
            modelTab.AutoScroll = false;
            modelTab.Controls.Add(modelLayout);
            modelLayout.Controls.Add(modelBar, 0, 0);
            modelLayout.SetColumnSpan(modelBar, 2);
            modelLayout.Controls.Add(modelGuide, 0, 1);
            modelLayout.SetColumnSpan(modelGuide, 2);
            modelLayout.SizeChanged += delegate { modelGuide.MaximumSize = new Size(Math.Max(100, modelLayout.ClientSize.Width - 12), 0); };
            AddOtherChanges(modelBar, 1);
            scaleBar.Visible = false;
            var settings = Button(modelBar, L.T("Modelleinstellungen", "Model settings"), delegate {
                bool show = modelLayout.ColumnStyles[1].Width == 0;
                int propertiesWidth = scaleBar.Controls.Cast<Control>().Max(c => c.Width + c.Margin.Horizontal)
                    + scaleBar.Padding.Horizontal + SystemInformation.VerticalScrollBarWidth + 8;
                modelLayout.SuspendLayout();
                modelLayout.ColumnStyles[1].Width = show ? propertiesWidth : 0;
                scaleBar.Visible = show;
                modelLayout.ResumeLayout(true);
            });
            settings.Name = "ModelSettingsToggle";
            StudioUx.SetHelp(settings, L.T("Modelleinstellungen rechts ein- oder ausblenden. Die Vorschau nutzt den verfügbaren Platz.", "Show or hide model settings on the right. The preview uses the available space."));
            modelLayout.Controls.Add(scaleBar, 1, 2);
            modelLayout.SetRowSpan(scaleBar, 2);
            textureView = new CharacterTextureView(modelPreview, () => reviewedRig == null ? model : reviewedRig.Preview(), () => assets);
            modelLayout.Controls.Add(textureView, 0, 2);
            modelInfo.Dock = DockStyle.Fill;
            modelInfo.Height = 96;
            var modelDetails = StudioReadOnlyText.Surface(modelInfo);
            modelDetails.Visible = false;
            var detailsToggle = new CheckBox { Name = "ModelDetailsToggle", Text = L.T("Modelldetails anzeigen", "Show model details"), AutoSize = true };
            detailsToggle.CheckedChanged += delegate {
                modelDetails.Visible = detailsToggle.Checked;
                modelLayout.RowStyles[3].Height = detailsToggle.Checked ? Math.Max(80, Font.Height * 5) : 0;
            };
            scaleBar.Controls.Add(detailsToggle);
            foreach (Button propertyAction in scaleBar.Controls.OfType<Button>())
            {
                if (propertyAction == resetScaleButton) continue;
                propertyAction.AutoSize = false;
                propertyAction.MinimumSize = new Size(0, 34);
                propertyAction.Size = new Size(232, 34);
                propertyAction.AutoEllipsis = true;
                StudioUx.SetHelp(propertyAction, propertyAction.Text);
            }
            modelLayout.Controls.Add(modelDetails, 0, 3);
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
            AddOtherChanges(nameBar, 2);
            Button(nameBar, L.T("Logo & Farben →", "Logo & colours →"), delegate { tabs.SelectedIndex = 3; });
            var editName = Button(nameBar, L.T("Variante bearbeiten", "Edit variant"), delegate { AddTargetName(); nameEditor.Focus(); nameEditor.SelectAll(); });
            StudioUx.SetHelp(editName, L.T("Gewählte Variante in den Namensfeldern bearbeiten.", "Edit the selected variant in the name fields."));
            StudioActions.Icon(Button(nameBar, L.T("Namen importieren…", "Import names…"), ImportNames), StudioIcon.Import);
            StudioActions.Icon(Button(nameBar, L.T("Archiv hinzufügen…", "Add archive…"), AddArchives), StudioIcon.Add);
            Button(nameBar, L.T("Textvorschau…", "Preview text…"), PreviewNames);
            var reportSurface = StudioReadOnlyText.Surface(report);
            reportSurface.Visible = false;
            checkTab.Controls.Add(reviewOverview);
            checkTab.Controls.Add(reportSurface);
            checkTab.Controls.Add(exportGuide);
            checkTab.SizeChanged += delegate { exportGuide.MaximumSize = new Size(Math.Max(200, checkTab.ClientSize.Width), 0); };
            var checkBar = Bar(checkTab);
            checkActions = checkBar;
            StudioActions.Icon(Button(checkBar, L.T("Dateien prüfen", "Check files"), RefreshReport), StudioIcon.Refresh);
            reviewFix = Button(checkBar, L.T("Fehlendes ergänzen…", "Complete missing items…"), delegate {
                if (model != null && (reviewedRig == null || !reviewedRig.ReadyForCharacterExport)) { tabs.SelectedIndex = 1; PrepareModel(); }
                else if (!archives.ContainsKey("UIAssets.szs") || !archives.ContainsKey("RaceAssets.szs")) { tabs.SelectedIndex = 2; AddArchives(); }
                else { tabs.SelectedIndex = 0; AddFiles(); }
            });
            var showReport = Button(checkBar, L.T("Dateidetails", "File details"), delegate {
                reportSurface.Visible = !reportSurface.Visible;
                reviewOverview.Visible = !reportSurface.Visible;
                if (reportSurface.Visible) reportSurface.BringToFront(); else reviewOverview.BringToFront();
            });
            showReport.Name = "CharacterReportDetails";
            motionCheck = Button(checkBar, L.T("Bewegungen prüfen", "Check movement"), delegate { CheckMovement(); });
            motionCheck.Name = "CheckCharacterMovement";
            testExport = Button(checkBar, L.T("Letzten Export testen…", "Test last export…"), delegate { OpenExportTest(lastExport); });
            testExport.Name = "TestCharacterExport";
            compareExport = Button(checkBar, L.T("Pack-Dateien abgleichen", "Compare pack files"), CompareExport);
            compareExport.Name = "CompareCharacterExport";
            compareExport.Padding = new Padding(12, 0, 12, 0);
            motionCheck.Padding = testExport.Padding = new Padding(12, 0, 12, 0);
            saveProjectButton = ExportAction(L.T("Projekt speichern…", "Save project…"), L.T("Arbeitsstand speichern. Zum Installieren anschließend Charakter exportieren wählen.", "Save your work. Use Export character to create installable game files."), SaveProject, false);
            StudioActions.Icon(saveProjectButton, StudioIcon.Save);
            var exportNames = ExportAction(L.T("Nur Namen exportieren…", "Export names only…"), L.T("Nur Namensarchive nach Character replacement files exportieren; das Modell bleibt unverändert.", "Export name archives to Character replacement files; this does not replace the model."), delegate { Export(false); }, false);
            exportNamesButton = exportNames;
            Footer.Controls.Remove(exportNames);
            checkBar.Controls.Add(exportNames);
            build = ExportAction(L.T("Charakter exportieren…", "Export character…"), L.T("Fahrer, Renn-/Menüfahrzeuge und Namensarchive gemeinsam nach MUR_EDITED/Character replacement files exportieren.", "Export the driver, race/menu vehicles and name archives together to MUR_EDITED/Character replacement files."), delegate { Export(true); });
            Actions.Padding = new Padding(0);
            Footer.Padding = new Padding(8, 2, 8, 2);
            foreach (Control button in Footer.Controls) button.Margin = new Padding(3);
            Body.Controls.Add(tabs);
            selection.Height = Math.Max(56, Font.Height * 2 + 22);
            selection.Controls.Clear();
            selection.Controls.Add(targetPortrait, 0, 0);
            selection.Controls.Add(modelSummary, 1, 0);
            selection.SetColumnSpan(modelSummary, 4);
            Body.Controls.Add(selection);
            var workflow = new TableLayoutPanel { Name = "CharacterWorkflow", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(4, 4, 4, 4), BackColor = DarkTheme.Panel };
            workflow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            workflow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workflow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            workflowBack = StudioChrome.ActionButton(L.T("← Zurück", "← Back"));
            workflowBack.Name = "CharacterWorkflowBack";
            workflowBack.Click += delegate { tabs.SelectedIndex = tabs.SelectedIndex == 4 ? lastWorkPage : 5; };
            workflow.Controls.Add(workflowBack, 0, 0);
            workflowHint.Font = new Font(Font, FontStyle.Bold);
            workflow.Controls.Add(workflowHint, 1, 0);
            workflowNext = StudioChrome.ActionButton(L.T("Weiter", "Continue"));
            workflowNext.Name = "CharacterWorkflowNext";
            workflowNext.Click += delegate { if (nextWorkflowAction != null) Guard(nextWorkflowAction); };
            workflow.Controls.Add(workflowNext, 2, 0);
            workflow.SizeChanged += delegate { workflowHint.MaximumSize = new Size(Math.Max(180, workflow.ClientSize.Width - workflowNext.Width - workflowBack.Width - 38), 0); };
            Body.Controls.Add(workflow);
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedIndex < 4) lastWorkPage = tabs.SelectedIndex; RefreshWorkflow(); };
            names.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) {
                if (!loading) { dirty = true; InvalidateExport(); RecordHistory("name:" + e.RowIndex + ":" + e.ColumnIndex); }
                RefreshTarget(); if (!syncingName) SyncNameFields();
            };
            names.RowsRemoved += delegate { if (!loading) { dirty = true; InvalidateExport(); RecordHistory(); } RefreshTarget(); };
            RefreshReport();
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (dirty && !ConfirmDiscard()) e.Cancel = true; };
            MinimumSize = new Size(950, 680);
            Size = new Size(1120, 850);
            // Im kleinen Fenster bleibt die Modellfläche wichtiger als ein hoher Kopfbereich.
            Action fitWorkspace = delegate {
                bool compact = ClientSize.Height < 720;
                selection.Visible = target != null && !(compact && tabs.SelectedIndex == 3);
                modelGuide.Padding = compact ? new Padding(6, 1, 6, 3) : new Padding(6, 3, 6, 5);
                foreach (Control hint in Controls.Find("ToolFileExamples", true))
                {
                    hint.Visible = !compact;
                    var header = hint.Parent as TableLayoutPanel;
                    if (header != null && header.RowStyles.Count > 1)
                        header.RowStyles[1].Height = compact ? 0 : 26;
                }
            };
            SizeChanged += delegate { fitWorkspace(); };
            tabs.SelectedIndexChanged += delegate { fitWorkspace(); };
            Finish();
            fitWorkspace();
            DarkTheme.StyleGrid(names);
            StyleFileList();
            RefreshFileActions();
            RenderModel();
            PackSelection.SourceStep(this, L.T("Charakter ersetzen…", "Replace character…"), L.T("„Charakter ersetzen“ öffnen und eine vorhandene RR-Variante nach Bild und Name wählen.\nDanach eigene Ersatzdateien oder ein 3D-Modell hinzufügen.", "Open Replace character and choose an installed RR variant by picture and name.\nThen add your replacement files or a 3D model."), false);
            loading = false;
            ResetHistory();
            historyActions = new StudioUndoRedo(this, Footer, () => history.CanUndo || HasPendingNameEdit(), () => history.CanRedo, UndoHistory, RedoHistory);
            if (Actions.Controls.Count == 0) Actions.Visible = false;
        }
        static ModelRig CopyRig(ModelRig source)
        {
            if (source == null) return null;
            var serializer = ModelRig.Serializer();
            var copy = serializer.Deserialize<ModelRig>(serializer.Serialize(source));
            copy.Folder = source.Folder; copy.Reference = source.Reference;
            return copy;
        }
        BuilderState CaptureState()
        {
            if (capturedRig != reviewedRig)
            {
                capturedRig = reviewedRig; frozenRig = CopyRig(reviewedRig);
            }
            return new BuilderState { Model = model, Rig = frozenRig, Assets = assets.ToArray(),
                Archives = new Dictionary<string, string>(archives, StringComparer.OrdinalIgnoreCase),
                Names = names.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new CharacterNameEntry {
                    CharacterId = Convert.ToInt32(r.Cells[0].Value), CustomId = Convert.ToInt32(r.Cells[1].Value),
                    Name = Convert.ToString(r.Cells[2].Value), Author = Convert.ToString(r.Cells[3].Value) }).ToList(),
                Scale = modelScale.Value, Movement = movementSource.SelectedIndex, Style = animationStyle.SelectedIndex,
                Star = starEffect.Checked, NeedsConversion = modelNeedsConversion, NameRow = names.CurrentRow == null ? 0 : names.CurrentRow.Index };
        }
        static bool SameState(BuilderState a, BuilderState b)
        {
            return a.Model == b.Model && a.Rig == b.Rig && a.Scale == b.Scale && a.Movement == b.Movement && a.Style == b.Style
                && a.Star == b.Star && a.NeedsConversion == b.NeedsConversion && a.Assets.SequenceEqual(b.Assets)
                && a.Archives.Count == b.Archives.Count && a.Archives.All(p => b.Archives.ContainsKey(p.Key) && b.Archives[p.Key] == p.Value)
                && a.Names.Count == b.Names.Count && a.Names.Zip(b.Names, (x, y) => x.CharacterId == y.CharacterId && x.CustomId == y.CustomId && x.Name == y.Name && x.Author == y.Author).All(equal => equal);
        }
        void ResetHistory()
        {
            savedHistoryState = CaptureState(); history.Reset(savedHistoryState);
        }
        void RecordHistory(string group = null)
        {
            if (!loading && !restoringHistory && target != null) history.Record(CaptureState(), group);
            if (historyActions != null) historyActions.Refresh();
        }
        bool HasPendingNameEdit()
        {
            return names.IsCurrentCellDirty && names.CurrentCell != null
                && !Object.Equals(names.CurrentCell.EditedFormattedValue, names.CurrentCell.FormattedValue);
        }
        void UndoHistory() { if (names.EndEdit() && history.CanUndo) RestoreState(history.Undo()); }
        void RedoHistory() { if (names.EndEdit() && history.CanRedo) RestoreState(history.Redo()); }
        void RestoreState(BuilderState state)
        {
            restoringHistory = loading = true;
            try
            {
                ClearStyleEditor();
                model = state.Model; reviewedRig = CopyRig(state.Rig);
                modelPreview.Model = null;
                if (model == null) modelInfo.Text = "";
                capturedRig = reviewedRig; frozenRig = state.Rig;
                assets.Clear(); assets.AddRange(state.Assets);
                archives.Clear(); foreach (var pair in state.Archives) archives.Add(pair.Key, pair.Value);
                names.Rows.Clear();
                foreach (var entry in state.Names) names.Rows.Add(entry.CharacterId, entry.CustomId, entry.Name, entry.Author, catalog.Find(entry.CharacterId, entry.CustomId).ToString());
                if (names.Rows.Count > 0) names.CurrentCell = names.Rows[Math.Max(0, Math.Min(state.NameRow, names.Rows.Count - 1))].Cells[2];
                movementSource.SelectedIndex = state.Movement; animationStyle.SelectedIndex = state.Style;
                starEffect.Checked = state.Star; modelScale.Value = state.Scale; modelNeedsConversion = state.NeedsConversion;
                dirty = savedHistoryState == null || !SameState(state, savedHistoryState); motionReport = null; InvalidateExport(); UpdateFiles(); RenderModel(); SyncNameFields();
                if (textureView != null) textureView.RefreshSource();
            }
            finally { loading = restoringHistory = false; }
            historyActions.Refresh();
        }
        void RefreshWorkflow()
        {
            if (workflowNext == null) return;
            int page = tabs.SelectedIndex;
            bool namesOnly = target != null && model == null && assets.Count == 0;
            workflowBack.Visible = target != null && page != 5;
            workflowBack.Text = page == 0 || page == 1 ? L.T("← Quelle wählen", "← Choose source") : L.T("← Zurück", "← Back");
            workflowNext.Visible = target != null && page != 5 && page != 4;
            if (build != null) build.Visible = target != null && page == 4 && !namesOnly;
            if (exportNamesButton != null && checkActions != null) {
                if (namesOnly && page == 4) {
                    if (exportNamesButton.Parent != Footer) Footer.Controls.Add(exportNamesButton);
                    Footer.Controls.SetChildIndex(exportNamesButton, 0);
                    DarkTheme.StylePrimary(exportNamesButton);
                }
                else {
                    if (exportNamesButton.Parent != checkActions) checkActions.Controls.Add(exportNamesButton);
                    exportNamesButton.BackColor = DarkTheme.Panel2;
                    exportNamesButton.ForeColor = DarkTheme.Fore;
                    exportNamesButton.FlatAppearance.MouseOverBackColor = DarkTheme.Panel3;
                    exportNamesButton.FlatAppearance.MouseDownBackColor = DarkTheme.Panel;
                }
            }
            if (saveProjectButton != null) saveProjectButton.Visible = target != null;
            if (chooseModel != null) chooseModel.Text = model == null ? L.T("3D-Modell laden…", "Load 3D model…") : L.T("Modell weiterbearbeiten", "Continue editing model");
            if (chooseFiles != null) chooseFiles.Text = assets.Count == 0 ? L.T("Spieldateien wählen…", "Choose game files…") : L.T("Spieldateien verwalten", "Manage game files");
            if (chooseModel != null) {
                chooseModel.Enabled = model != null || assets.Count == 0;
                if (!chooseModel.Enabled) chooseModel.Text = L.T("3D-Modell: neues Projekt", "3D model: new project");
                StudioUx.SetHelp(chooseModel, chooseModel.Enabled
                    ? L.T("Eigenes 3D-Modell laden oder weiterbearbeiten.", "Load or continue editing your own 3D model.")
                    : L.T("Spieldateien sind die Quelle dieses Projekts. Für ein 3D-Modell zuerst das Projekt speichern und einen neuen Charakterersatz beginnen.", "Game files are this project's source. To use a 3D model, save the project and start a new character replacement."));
            }
            if (chooseFiles != null) {
                chooseFiles.Enabled = model == null;
                if (!chooseFiles.Enabled) chooseFiles.Text = L.T("Spieldateien: neues Projekt", "Game files: new project");
                StudioUx.SetHelp(chooseFiles, chooseFiles.Enabled
                    ? L.T("Fertige BRRES-/SZS-Dateien wählen oder verwalten.", "Choose or manage ready-made BRRES/SZS files.")
                    : L.T("Das 3D-Modell ist die Quelle dieses Projekts. Für fertige Spieldateien zuerst das Projekt speichern und einen neuen Charakterersatz beginnen.", "The 3D model is this project's source. To use ready-made game files, save the project and start a new character replacement."));
            }
            string caption = L.T("Prüfen & Export →", "Check & export →");
            nextWorkflowAction = delegate { tabs.SelectedIndex = 4; RefreshReport(); };
            if (target == null)
            {
                workflowHint.Text = L.T("1 · Ersatzziel wählen", "1 · Choose replacement target");
            }
            else if (page == 5)
            {
                workflowHint.Text = model != null
                    ? L.T("2 · Aktuelle Quelle: eigenes 3D-Modell", "2 · Current source: your 3D model")
                    : assets.Count > 0 ? L.T("2 · Aktuelle Quelle: fertige Spieldateien", "2 · Current source: ready-made game files")
                    : L.T("2 · Quelle wählen: 3D-Modell oder Spieldateien", "2 · Choose source: 3D model or game files");
            }
            else if (page == 1)
            {
                workflowHint.Text = L.T("2 · Modell laden → bearbeiten → prüfen", "2 · Load model → edit → review");
                if (model == null) {
                    caption = L.T("Modell laden…", "Load model…"); nextWorkflowAction = OpenModelWork;
                }
            }
            else if (page == 0)
            {
                workflowHint.Text = L.T("2 · Fertige Spieldateien", "2 · Ready-made game files");
            }
            else if (page == 2)
            {
                workflowHint.Text = L.T("2 · Name & Autor", "2 · Name & author");
                if (!archives.ContainsKey("UIAssets.szs") || !archives.ContainsKey("RaceAssets.szs")) {
                    caption = L.T("Namensarchive ergänzen…", "Add name archives…"); nextWorkflowAction = AddArchives;
                }
            }
            else if (page == 3) workflowHint.Text = L.T("2 · Logo & Farben · ", "2 · Logo & colours · ") + target.Name;
            else workflowHint.Text = L.T("3 · Prüfen & Export", "3 · Check & export");
            workflowNext.Text = caption;
            workflowNext.AccessibleName = caption;
            StudioUx.SetHelp(workflowNext, workflowHint.Text);
        }

        void OpenModelWork()
        {
            if (model == null && assets.Count > 0) {
                Status.Text = L.T("Für ein 3D-Modell zuerst das Projekt speichern und einen neuen Charakterersatz beginnen.", "To use a 3D model, save the project and start a new character replacement.");
                return;
            }
            if (model == null) ImportModel();
            tabs.SelectedIndex = model == null ? 5 : 1;
        }

        void BuildChangeChoice(Panel parent)
        {
            var choices = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8), Margin = Padding.Empty };
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            choices.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var modelActions = ChoiceCard(choices, 0, L.T("Eigenes 3D-Modell", "Your 3D model"),
                L.T("Eigene Figur laden, Gelenke und Haltung prüfen. Studio erstellt beim Export passende Spieldateien.", "Load your character and review its joints and pose. Studio creates matching game files when you export."));
            chooseModel = Button(modelActions, L.T("3D-Modell laden…", "Load 3D model…"), OpenModelWork);
            chooseModel.Name = "CharacterChooseOwnModel";
            DarkTheme.StylePrimary(chooseModel);
            var fileActions = ChoiceCard(choices, 1, L.T("Fertige Spieldateien", "Ready-made game files"),
                L.T("Vorhandene BRRES-/SZS-Dateien zuordnen. Fehlendes aus RR ergänzen und als Charakterersatz exportieren.", "Assign existing BRRES/SZS files. Add missing files from RR and export your character replacement."));
            chooseFiles = Button(fileActions, L.T("Spieldateien wählen…", "Choose game files…"), delegate {
                if (model != null) {
                    Status.Text = L.T("Für fertige Spieldateien zuerst das Projekt speichern und einen neuen Charakterersatz beginnen.", "To use ready-made game files, save the project and start a new character replacement.");
                    return;
                }
                if (assets.Count == 0) AddFiles();
                tabs.SelectedIndex = assets.Count == 0 ? 5 : 0;
            });
            chooseFiles.Name = "CharacterChooseGameFiles";
            DarkTheme.StylePrimary(chooseFiles);
            var identityActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(5), Padding = new Padding(8) };
            identityActions.Controls.Add(new Label { Text = L.T("Für beide Wege oder allein:", "For either source, or on their own:"), AutoSize = true, Margin = new Padding(3, 10, 8, 3), ForeColor = DarkTheme.Muted });
            Button(identityActions, L.T("Name & Bilder bearbeiten", "Edit name & images"), delegate { tabs.SelectedIndex = 2; nameEditor.Focus(); });
            choices.Controls.Add(identityActions, 0, 1);
            choices.SetColumnSpan(identityActions, 2);
            parent.Controls.Add(choices);
        }

        FlowLayoutPanel ChoiceCard(TableLayoutPanel parent, int column, string title, string text)
        {
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(14), Margin = new Padding(5), BackColor = DarkTheme.Panel2, AutoScroll = true };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var heading = new Label { Text = title, AutoSize = true, Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 12, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12), UseMnemonic = false };
            var description = new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, ForeColor = DarkTheme.Muted, Margin = Padding.Empty, UseMnemonic = false };
            card.Controls.Add(heading, 0, 0);
            card.Controls.Add(description, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            card.Controls.Add(actions, 0, 3);
            card.SizeChanged += delegate {
                int width = Math.Max(150, card.ClientSize.Width - card.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
                heading.MaximumSize = description.MaximumSize = new Size(width, 0);
                foreach (Button action in actions.Controls) {
                    action.MinimumSize = new Size(0, 34);
                    action.MaximumSize = new Size(width, 0);
                    action.AutoSize = true;
                    StudioUx.SetHelp(action, action.Text);
                }
            };
            parent.Controls.Add(card, column, 0);
            return actions;
        }

        void AddOtherChanges(FlowLayoutPanel parent, int currentPage)
        {
            var menu = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, AutoSize = true, Dock = DockStyle.None, Font = Font, Margin = new Padding(8, 3, 3, 3) };
            var more = new ToolStripDropDownButton(L.T("Weitere Änderungen", "Other changes"));
            string[] captions = { L.T("Fertige Spieldateien", "Ready-made game files"), L.T("Eigenes 3D-Modell", "Your 3D model"), L.T("Name & Autor", "Name & author"), L.T("Logo & Farben", "Logo & colours") };
            for (int i = 0; i < captions.Length; i++) {
                if (i < 2) continue;
                if (i == currentPage || currentPage == 2 && i == 3) continue;
                int page = i;
                var item = new ToolStripMenuItem(captions[i]);
                item.Click += delegate { Guard(delegate { if (page == 1) OpenModelWork(); else tabs.SelectedIndex = page; }); };
                more.DropDownItems.Add(item);
            }
            menu.Items.Add(more);
            DarkTheme.StyleToolStrip(menu, new MurumsDarkToolStripRenderer());
            parent.Controls.Add(menu);
        }

        protected override void OnPackSelected(CustomPack pack)
        {
            RefreshWorkflow();
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
            var button = new Button { Text = text, Font = Font, AutoSize = true, Height = 32, Margin = new Padding(3),
                Padding = new Padding(10, 0, 10, 0), MinimumSize = new Size(TextRenderer.MeasureText(text, Font).Width + 26, 32) };
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
            ClearStyleEditor();
            loading = true;
            assets.Clear(); archives.Clear(); names.Rows.Clear(); files.Items.Clear();
            model = null; reviewedRig = null; modelNeedsConversion = false;
            starEffect.Checked = true;
            motionReport = null; lastExport = lastExportPack = null; exportSnapshot = null; exportChanged = false;
            animationStyle.SelectedIndex = 0;
            movementSource.Items.Clear();
            if (prepareModelButton != null) prepareModelButton.Enabled = false;
            modelPreview.Model = null; modelScale.Value = 100;
            targetPortrait.Image = null;
            if (replacementPortrait.Image != null) replacementPortrait.Image.Dispose();
            replacementPortrait.Image = null;
            target = null;
            if (catalog != null) catalog.Dispose();
            catalog = null;
            loading = false; dirty = false;
            tabs.SelectedIndex = 5;
            lastWorkPage = 5;
            RenderModel();
            RefreshFileActions();
            RefreshReport();
            if (textureView != null) textureView.RefreshSource();
        }
        void Clear()
        {
            if (!ConfirmDiscard()) return;
            ClearData();
            ResetHistory();
            PackSelection.SourceCleared(this);
        }
        void NewProject()
        {
            if (String.IsNullOrEmpty(PackSelection.Folder(this))) throw new InvalidOperationException(L.T("Zuerst ein eigenes Pack wählen oder erstellen.", "Choose or create your custom pack first."));
            if (!ConfirmDiscard()) return;
            var picker = new CharacterPickerForm(PackSelection.Folder(this), catalog == null ? null : catalog.Root);
            StudioEditor.Open(this, picker, result => {
                if (result == DialogResult.OK) Guard(delegate { StartReplacement(picker.TakeCatalog(), picker.Selected); });
            });
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
            FillMovementSources(null, 0);
            AddTargetName();
            loading = false; dirty = false;
            RefreshTarget(); RefreshReport();
            PackSelection.SourceLoaded(this);
            tabs.SelectedIndex = 5;
            ResetHistory();
        }

        CharacterVariant MovementTarget { get { return movementSource.SelectedItem as CharacterVariant ?? target; } }

        void FillMovementSources(string code, int slot)
        {
            movementSource.Items.Clear();
            movementSource.Items.Add(L.T("Originalbewegungen der RR-Variante", "Original RR variant movement"));
            foreach (var variant in catalog.Variants.Where(v => v.Character.Weight == target.Character.Weight && CharacterModelConversion.HumanMovementCodes.Contains(v.Character.Code)))
                movementSource.Items.Add(variant);
            movementSource.SelectedIndex = 0;
            if (!String.IsNullOrEmpty(code))
            {
                var selected = movementSource.Items.OfType<CharacterVariant>().FirstOrDefault(v => v.Character.Code == code && v.Slot == slot);
                if (selected == null) throw new InvalidDataException(L.T("Die gespeicherte Bewegungsquelle fehlt in dieser RR-Installation.",
                    "The saved movement source is missing from this RR installation."));
                movementSource.SelectedItem = selected;
            }
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
            if (!loading) { dirty = true; InvalidateExport(); RecordHistory(); }
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
            modelSummary.Text = target == null ? L.T("Noch kein Ersatzziel gewählt", "No replacement target selected")
                : L.T("Ersatzziel: ", "Replacement target: ") + target.Name + " · " + L.T("Basis: ", "Base: ") + target.Basis
                    + Environment.NewLine + (model != null ? Path.GetFileName(model.Source)
                        : assets.Count > 0 ? assets.Count + L.T(" Spieldateien zugeordnet", " game files assigned")
                        : !String.IsNullOrWhiteSpace(name) && name != target.Name ? L.T("Neuer Name: ", "New name: ") + name
                        : L.T("Original als Grundlage · Eigene Änderung wählen", "Based on the original · Choose your change"));
            replacementCaption.Text = L.T("Dein Ersatz: ", "Your replacement: ") + (String.IsNullOrWhiteSpace(name) ? L.T("Name unter „Namen & Autoren“", "Name in Names & authors") : name)
                + Environment.NewLine + assets.Count + L.T(" Dateien zugeordnet", " files assigned")
                + Environment.NewLine + (replacementPortrait.Image == null ? L.T("Noch kein eigenes Icon geladen", "No replacement icon loaded yet") : L.T("Eigenes Minimap-Icon", "Your minimap icon"));
            StudioUx.SetHelp(modelSummary, modelSummary.Text + Environment.NewLine + targetCaption.Text + Environment.NewLine + replacementCaption.Text);
        }

        void ClearStyleEditor()
        {
            if (styleEditor == null) return;
            styleEditor.Dispose();
            styleEditor = null;
            styleModel = null;
            styleRig = null;
        }
        void EnsureStyleEditor()
        {
            if (target == null) return;
            if (styleEditor != null && styleModel == model && styleRig == reviewedRig) return;
            ClearStyleEditor();
            var baseline = assets.ToArray();
            styleModel = model;
            styleRig = reviewedRig;
            styleEditor = new CharacterImagesForm(target, catalog.Root, baseline, () => PackSelection.Output(this, ""), reviewedRig == null ? model : reviewedRig.Preview(), true);
            styleEditor.ProjectCanUndo = () => history.CanUndo;
            styleEditor.ProjectCanRedo = () => history.CanRedo;
            styleEditor.ProjectUndo = UndoHistory;
            styleEditor.ProjectRedo = RedoHistory;
            styleEditor.ChangesChanged += delegate {
                applyingStyle = true;
                try
                {
                    assets.Clear();
                    assets.AddRange(baseline.Where(a => !styleEditor.Changes.ContainsKey(a.Target)));
                    assets.AddRange(styleEditor.Changes.Values);
                    dirty = true;
                    UpdateFiles();
                    RefreshReport();
                    RecordHistory();
                }
                finally { applyingStyle = false; }
            };
            styleTab.Controls.Add(styleEditor);
            styleEditor.Show();
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
            if (!loading) InvalidateExport();
            motionReport = null;
            if (!applyingStyle) ClearStyleEditor();
            files.Items.Clear();
            foreach (var asset in assets)
                files.Items.Add(new ListViewItem(new[] { asset.Role, asset.Target, asset.Source }) { Tag = asset, ToolTipText = asset.Source });
            RefreshReplacementImage();
            RefreshTarget();
            RefreshFileActions();
            FitFileColumns();
            RefreshReport();
            if (!applyingStyle && tabs.SelectedTab == styleTab && target != null) EnsureStyleEditor();
            if (textureView != null) textureView.RefreshSource();
            RecordHistory();
        }
        void StyleFileList()
        {
            DarkTheme.StyleListView(files);
            fileEmpty.BackColor = DarkTheme.Panel2;
            fileEmpty.ForeColor = DarkTheme.Muted;
            fileGuide.ForeColor = DarkTheme.Muted;
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
            StudioEditor.Open(this, new FilePreviewForm(asset.Source), delegate { });
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
            dirty |= selected.Length > 0; if (selected.Length > 0) InvalidateExport(); RefreshReport();
            RecordHistory();
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
            loading = true;
            try
            {
                names.Rows.Clear();
                foreach (var entry in merged.Values) names.Rows.Add(entry.CharacterId, entry.CustomId, entry.Name, entry.Author, catalog.Find(entry.CharacterId, entry.CustomId).ToString());
            }
            finally { loading = false; }
            dirty = true;
            SyncNameFields(); InvalidateExport(); RecordHistory();
        }
        void PreviewNames()
        {
            var form = new Form { Text = L.T("CharaName-Textvorschau", "CharaName text preview"), Size = new Size(740, 500), StartPosition = FormStartPosition.CenterParent };
            form.Controls.Add(StudioReadOnlyText.Surface(new StudioReadOnlyText { WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both, Text = CharacterNames.Merge(null, ReadNames()).Replace("\n", Environment.NewLine) }));
            DarkTheme.Apply(form);
            StudioEditor.Open(this, form, delegate { });
        }
        void ImportModel()
        {
            var selected = Pick(L.T("Eigenes 3D-Modell|*.glb;*.gltf;*.blend;*.usdz;*.dae;*.obj", "Own 3D model|*.glb;*.gltf;*.blend;*.usdz;*.dae;*.obj"), false);
            if (selected.Length == 0) return;
            RequireTarget();
            var movement = MovementTarget;
            var candidate = ModelOperationForm.Run(this, L.T("3D-Modell importieren", "Import 3D model"), token => {
                var imported = CharacterModelImport.LoadVisual(selected[0], token);
                var reference = StudioModelLibrary.Reference(movement.DriverPath, Path.Combine(ModelRuntime.NewWorkFolder(), "reference.dae"));
                imported.FitTo(reference);
                return imported;
            });
            if (candidate == null) return;
            loading = true;
            try { reviewedRig = null; model = candidate; modelScale.Value = 100; }
            finally { loading = false; }
            dirty = true; ModelChanged(); RenderModel();
            if (textureView != null) textureView.RefreshSource();
            RecordHistory();
        }
        void RenderModel()
        {
            if (model == null && tabs.SelectedIndex == 1) tabs.SelectedIndex = 5;
            RefreshWorkflow();
            bool removedParts = reviewedRig != null && reviewedRig.RemovedSurfaceCount > 0;
            modelScale.Enabled = resetScaleButton.Enabled = !removedParts;
            scaledCopyButton.Enabled = model != null && !removedParts;
            editedModelHint.Visible = removedParts;
            bool needsReview = reviewedRig == null || !reviewedRig.ReadyForCharacterExport;
            modelGuide.Visible = true;
            starEffect.Enabled = model != null;
            animationStyle.Enabled = reviewedRig == null || reviewedRig.HasHumanJoints;
            movementSource.Enabled = !removedParts && (model == null || !Path.GetExtension(model.Source).Equals(".json", StringComparison.OrdinalIgnoreCase));
            prepareModelButton.Enabled = model != null;
            importModelButton.BackColor = model == null ? DarkTheme.Accent : DarkTheme.Panel;
            prepareModelButton.BackColor = model != null && needsReview ? DarkTheme.Accent : DarkTheme.Panel;
            modelGuide.Text = model == null
                ? L.T("Schritt 1: Dein 3D-Modell laden. Danach bereitet Studio die Gelenkzuordnung vor.",
                    "Step 1: load your 3D model. Studio then prepares its joint assignment.")
                : needsReview
                    ? L.T("Nächster Schritt: „Modell bearbeiten“ → Teile, Gelenke und Haltung prüfen und übernehmen.",
                        "Next: Edit model → review and accept parts, joints and pose.")
                    : !archives.ContainsKey("UIAssets.szs") || !archives.ContainsKey("RaceAssets.szs")
                        ? L.T("Haltung übernommen. „Prüfen & Export“ → fehlende Namensarchive ergänzen.",
                            "Pose accepted. Check & export → add missing name archives.")
                    : L.T("Modell vorbereitet. „Prüfen & Export“ öffnet die Abschlussprüfung und den Export.",
                        "Model prepared. Check & export opens the final review and export.");
            if (model == null) return;
            modelPreview.GameContext = reviewedRig == null ? 0 : 1;
            modelPreview.Model = reviewedRig == null ? model : reviewedRig.Preview();
            modelPreview.ModelScale = reviewedRig == null ? model.FitScale * (float)modelScale.Value / 100 : 1;
            modelInfo.Text = (needsReview ? L.T("Nächster Schritt: Bewegungen automatisch zuordnen und prüfen.", "Next: assign movement automatically and review it.") : L.T("Haltung übernommen. Schritt 3: Charakter exportieren. Studio erstellt dabei automatisch alle RR-Dateien. Alles zum Kopieren liegt in MUR_EDITED/Character replacement files.", "Pose accepted. Step 3: Export character. Studio creates all RR files automatically. Everything to copy goes into MUR_EDITED/Character replacement files.")) + Environment.NewLine + Path.GetFileName(model.Source) + " — " + model.Points.Count + L.T(" Eckpunkte, ", " vertices, ") + model.Faces.Count + L.T(" Flächen, ", " faces, ") + model.Joints + L.T(" Gelenke, ", " joints, ") + model.Skins + L.T(" Skins.", " skins.") + Environment.NewLine + String.Join(Environment.NewLine, model.Warnings);
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
                StudioMessageBox.ShowPath(this, dialog.FileName, Status.Text, L.T("Gespeichert", "Saved"));
            }
        }
        void PrepareModel()
        {
            RequireTarget();
            if (model == null) throw new InvalidOperationException(L.T("Zuerst ein 3D-Modell importieren.", "Import a 3D model first."));
            var movement = MovementTarget;
            bool separateMovement = movementSource.SelectedIndex > 0;
            float size = (float)modelScale.Value, height = model.ReferenceHeight;
            var candidate = CopyRig(reviewedRig) ?? ModelOperationForm.Run(this, L.T("RR-Bewegungen vorbereiten", "Prepare RR movement"),
                token => {
                    var rig = ModelRig.Prepare(model, movement.DriverPath, size, 20000, token, height);
                    if (separateMovement)
                    {
                        rig.MovementCode = movement.Character.Code;
                        rig.MovementSlot = movement.Slot;
                    }
                    rig.FitJointGuides();
                    try { rig.BindWithBlender(token); }
                    catch (InvalidDataException error) { rig.BindingWarning = error.Message; }
                    return rig;
                });
            if (candidate == null) return;
            candidate.SetAnimationStyle(ModelRig.AnimationStyles[Math.Max(0, animationStyle.SelectedIndex)]);
            candidate.StarEffect = starEffect.Checked;
            var references = ModelOperationForm.Run(this, L.T("Originalmodelle als Hilfe laden", "Load original reference models"),
                token => RigReferenceSet.Load(movement, catalog.Root));
            if (references == null) return;
            references.SetVehicleSources(target, assets);
            var review = new ModelRigForm(candidate, references, target.Character.Weight, reviewedRig == null);
            StudioEditor.Open(this, review, result => {
                if (result != DialogResult.OK) return;
                Guard(delegate {
                    reviewedRig = review.Result;
                    loading = true;
                    try { animationStyle.SelectedIndex = Array.IndexOf(ModelRig.AnimationStyles, reviewedRig.HumanAnimationStyle); }
                    finally { loading = false; }
                    dirty = true; ModelChanged(); RenderModel();
                    RecordHistory();
                });
            });
        }
        void ModelChanged()
        {
            InvalidateExport();
            motionReport = null;
            modelNeedsConversion = model != null;
            RefreshReport();
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
            RefreshWorkflow();
            if (motionCheck != null) { motionCheck.Enabled = Selected != null && (modelNeedsConversion
                ? reviewedRig != null && reviewedRig.ReadyForCharacterExport : assets.Count > 0); motionCheck.Visible = model != null || assets.Count > 0; }
            if (testExport != null) { testExport.Enabled = lastExport != null && !exportChanged && Directory.Exists(lastExport); testExport.Visible = lastExport != null; }
            if (compareExport != null) { compareExport.Enabled = exportSnapshot != null && !exportChanged && !String.IsNullOrEmpty(lastExportPack); compareExport.Visible = exportSnapshot != null; }
            RefreshExportGuide();
            if (Selected == null)
            {
                if (build != null) build.Enabled = false;
                reviewOverview.Text = "";
                if (reviewFix != null) reviewFix.Visible = false;
                report.Clear();
                Status.Text = L.T("Vorhandene RR-Variante über „Charakter ersetzen“ auswählen.", "Choose an installed RR variant using Replace character.");
                return;
            }
            var missing = CharacterPackage.Missing(Selected, Slot, assets);
            if (build != null) build.Enabled = File.Exists(target.DriverPath) && ((reviewedRig != null && reviewedRig.ReadyForCharacterExport) || (!modelNeedsConversion && missing.Count == 0)) && archives.ContainsKey("UIAssets.szs") && archives.ContainsKey("RaceAssets.szs");
            bool needsPose = model != null && (reviewedRig == null || !reviewedRig.ReadyForCharacterExport);
            bool needsNames = !archives.ContainsKey("UIAssets.szs") || !archives.ContainsKey("RaceAssets.szs");
            bool needsFiles = model == null && missing.Count > 0;
            bool namesOnly = model == null && assets.Count == 0;
            if (reviewFix != null) {
                reviewFix.Visible = needsPose || needsNames || needsFiles && !namesOnly;
                reviewFix.Text = needsPose ? L.T("Gelenke & Haltung prüfen…", "Review joints & pose…")
                    : needsNames ? L.T("Namensarchive ergänzen…", "Add name archives…")
                    : L.T("Fehlende Spieldateien ergänzen…", "Add missing game files…");
            }
            reviewOverview.Text = target.Name + L.T(" ersetzen", " replacement") + Environment.NewLine + Environment.NewLine
                + (namesOnly && !needsNames ? L.T("Namen und Autoren sind bereit. Unten „Nur Namen exportieren“ wählen.", "Names and authors are ready. Choose Export names only below.")
                    : build != null && build.Enabled ? L.T("Bereit für den vollständigen Export.", "Ready for the full export.")
                    : needsPose ? L.T("Noch zu erledigen: Gelenke und Haltung prüfen und übernehmen.", "Next: review and accept the joints and pose.")
                    : needsNames ? L.T("Noch zu erledigen: Namensarchive ergänzen.", "Next: add the name archives.")
                    : needsFiles ? L.T("Für einen vollständigen Charakterersatz fehlen noch Spieldateien.", "Game files are still needed for a full character replacement.")
                    : L.T("Dateidetails und Hinweise vor dem Export prüfen.", "Review the file details and findings before export."))
                + Environment.NewLine + Environment.NewLine
                + L.T("Charakter exportieren erstellt die Spieldateien zum Kopieren ins Custom Pack.\nNur Namen exportieren ändert Namen und Autoren; das Modell bleibt erhalten.\nProjekt speichern bewahrt deinen bearbeitbaren Arbeitsstand.",
                    "Export character creates game files to copy into your Custom Pack.\nExport names only changes names and authors; the model is preserved.\nSave project keeps your editable work.")
                + (motionReport == null ? "" : Environment.NewLine + Environment.NewLine + L.T("Bewegungsprüfung vorhanden: Hinweise unter Dateidetails lesen.", "Movement review available: read the findings under File details."));
            report.Text = L.T("Ersetzt: ", "Replaces: ") + target.Name + " — " + target.Basis + Environment.NewLine +
                L.T("Geladen: ", "Loaded: ") + assets.Count + L.T(" Charakterdateien; ", " character files; ") + archives.Count + L.T("/2 RR-Textarchive.", "/2 RR text archives.") + Environment.NewLine +
                (reviewedRig != null && !reviewedRig.ReadyForCharacterExport ? L.T("Entwurf: Gelenke und Zusatzteile in Schritt 2 prüfen. Projekt speichern ist möglich.", "Draft: review joints and accessories in step 2. You can save the project.") + Environment.NewLine : "") +
                (modelNeedsConversion && (reviewedRig == null || reviewedRig.ReadyForCharacterExport) ? L.T("Modell bereit zur Erstellung: Haltung in Schritt 2 prüfen, dann Schritt 3 „Charakter exportieren“.", "Model awaiting export: review the pose in step 2, then use step 3, Export character.") + Environment.NewLine : "") +
                (modelNeedsConversion ? L.T("Beim Charakterexport automatisch erzeugte Dateien:", "Files created automatically during character export:") : L.T("Fehlende Pflichtdateien:", "Required missing files:")) + Environment.NewLine + (missing.Count == 0 ? L.T("Keine.", "None.") : String.Join(Environment.NewLine, missing)) + Environment.NewLine +
                String.Join(Environment.NewLine, new[] { "UIAssets.szs", "RaceAssets.szs" }.Where(n => !archives.ContainsKey(n))) + Environment.NewLine + Environment.NewLine +
                L.T("Optional: Minikarten-TPL. Eigene Spielhaltungen exportieren auch die AllKart-Menüfahrzeuge. Originalstimmen bleiben erhalten; eigene Stimmen werden noch nicht importiert.", "Optional: minimap TPL. Custom game poses also export the allkart menu vehicles. Original voices are kept; custom voice import is not yet supported.") + Environment.NewLine +
                L.T("Passende Dateistruktur beweist keine Skelett-/Animationskompatibilität. Vor Weitergabe in Dolphin testen.", "Matching file structure does not prove skeleton/animation compatibility. Test in Dolphin before distributing.") + Environment.NewLine +
                L.T("Charakter exportieren → MUR_EDITED/Character replacement files. Alle Dateien daraus in den Custom-Pack-Hauptordner kopieren. Projekt speichern erzeugt keine installierbare Kopie; „Nur Namen“ ersetzt kein Modell.", "Export character → MUR_EDITED/Character replacement files. Copy all files from there into your custom pack root. Save project stores your work; Names only does not replace a model.");
            if (motionReport != null) report.Text = motionReport.Text() + Environment.NewLine + Environment.NewLine + report.Text;
            Status.Text = L.T("Charakterziel: ", "Character target: ") + target.Name + " • " + assets.Count + L.T(" Dateien • Namensarchive: ", " files • Names archives: ") + String.Join(", ", archives.Keys);
        }
        CharacterMotionReport CheckMovement()
        {
            RequireTarget();
            motionReport = null;
            RefreshReport();
            if (modelNeedsConversion && !CreateModelFiles()) return null;
            var result = ModelOperationForm.Run(this, L.T("Charakterbewegungen prüfen", "Check character movement"),
                token => CharacterMotionCheck.Run(assets, reviewedRig != null && reviewedRig.HumanAnimationStyle != null, token));
            if (result == null) return null;
            motionReport = result;
            RefreshReport();
            tabs.SelectedIndex = 4;
            Status.Text = result.Findings.Count == 0 ? L.T("Bewegungscheck abgeschlossen.", "Movement check complete.")
                : L.T("Bewegungscheck: Auffälligkeiten im Bericht prüfen.", "Movement check: review the findings in the report.");
            return result;
        }
        bool ReviewMovement()
        {
            var result = CheckMovement();
            if (result == null) return false;
            if (result.HasErrors)
            {
                StudioMessageBox.Show(this, result.Text() + "\n\n" + L.T("Ungültige Bewegungsdaten zuerst korrigieren.", "Correct invalid motion data before exporting."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (result.Findings.Count == 0) return true;
            return StudioMessageBox.Show(this, result.Text() + "\n\n" + L.T("Die Hinweise können auch beabsichtigte Posen betreffen. Trotzdem exportieren?",
                "Findings may also describe intentional poses. Export anyway?"), Text, MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        void InvalidateExport()
        {
            if (exportSnapshot != null) exportChanged = true;
            if (testExport != null) testExport.Enabled = false;
            if (compareExport != null) compareExport.Enabled = false;
            RefreshExportGuide();
        }
        void RefreshExportGuide()
        {
            exportGuide.Text = exportSnapshot == null
                ? L.T("3 · Charakter exportieren erstellt die Spieldateien. „Projekt speichern“ sichert Modell und Einstellungen für spätere Korrekturen.\n4 · Export ins Pack kopieren, Pack-Dateien abgleichen und im Spiel testen.",
                    "3 · Export character creates the game files. Save project keeps your model and settings for later edits.\n4 · Copy the export into your pack, compare pack files and test in-game.")
                : exportChanged ? L.T("Änderungen seit dem Export: zuerst erneut „Charakter exportieren“. Gespeicherte Projekte und ältere Testkopien aktualisieren sich nicht automatisch.",
                    "Changed since export: use Export character again. Saved projects and older test copies do not update automatically.")
                : L.T("Letzter Export: ", "Last export: ") + exportSnapshot.Created.ToString("HH:mm:ss") + " · " + (target == null ? "" : target.Basis)
                    + "\n" + L.T("4 · Dateien ins Pack kopieren → Pack-Dateien abgleichen → Pack im Launcher neu starten. Der direkte Spieltest verwendet eine getrennte Exportkopie.",
                        "4 · Copy files into the pack → Compare pack files → restart the pack in your launcher. The direct game test uses a separate export copy.");
        }
        void CompareExport()
        {
            if (exportSnapshot == null || exportChanged || String.IsNullOrEmpty(lastExportPack)) return;
            var different = exportSnapshot.Differences(lastExportPack);
            exportGuide.Text = different.Length == 0
                ? L.T("Alle Dateien dieses Exports liegen identisch im Pack. Jetzt das Pack im Launcher neu starten.\nPack: ",
                    "All files from this export match the pack. Restart the pack in your launcher now.\nPack: ") + lastExportPack
                : L.T("Noch nicht vollständig im Pack: ", "Not fully copied into the pack: ") + String.Join(", ", different)
                    + "\n" + L.T("Alle Exportdateien kopieren von: ", "Copy all exported files from: ") + lastExport
                    + "\n" + L.T("Nach: ", "To: ") + lastExportPack;
        }
        void OpenExportTest(string path)
        {
            if (exportChanged)
                throw new InvalidOperationException(L.T("Seit dem letzten Export wurde etwas geändert. Zuerst erneut „Charakter exportieren“ wählen.",
                    "Changes were made since the last export. Use Export character again first."));
            if (exportSnapshot != null && exportSnapshot.Differences(path).Length > 0)
                throw new InvalidOperationException(L.T("Die Exportdateien wurden verschoben oder verändert. Zuerst erneut exportieren, damit der Test deinen aktuellen Stand verwendet.",
                    "Export files were moved or changed. Export again so the test uses your current work."));
            if (String.IsNullOrEmpty(path) || !Directory.Exists(path))
                throw new DirectoryNotFoundException(L.T("Zuerst den Charakter exportieren.", "Export the character first."));
            StudioEditor.Open(this, new PackWorkbenchForm(path), delegate { });
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
                    tabs.SelectedIndex = 4; RefreshReport();
                    throw new InvalidDataException(L.T("Vor dem Erstellen alle Pflichtdateien und beide RR-Textarchive hinzufügen.", "Complete the required character files and both RR text archives before building the package."));
                }
                if (!entries.Any(e => e.CharacterId == Selected.Id && e.CustomId == Slot))
                    throw new InvalidDataException(L.T("Einen Namen für die gewählte Variante hinzufügen.", "Add a name for the selected variant."));
            }
            if (full && !ReviewMovement()) return;
            string parent = PackSelection.Output(this, "");
            if (String.IsNullOrEmpty(parent)) parent = Folder("");
            if (parent == null) return;
            string destination = ExportCopiesTo(parent, full, entries);
            Status.Text = L.T("Exportierte Spieldateien: ", "Exported game files: ") + destination;
            if (full)
            {
                lastExport = destination; lastExportPack = PackSelection.Folder(this);
                exportSnapshot = new CharacterExportSnapshot(destination, assets.Select(a => Path.GetFileName(a.Target)).Concat(archives.Keys));
                exportChanged = false; testExport.Enabled = true; RefreshReport();
            }
            else InvalidateExport();
            var next = StudioMessageBox.ShowPath(this, destination, (full ? L.T("Spieldateien erstellt. Im aktiven Pack noch nicht überprüft.", "Game files created. Active pack has not been checked yet.")
                : L.T("Nur Namen und Autoren exportiert. Das 3D-Modell wurde dadurch nicht ersetzt.", "Only names and authors exported. This does not replace the 3D model."))
                + "\n\n" + L.T("Ersatzziel: ", "Replacement target: ") + target.Basis
                + "\n" + L.T("Zielordner des Packs: ", "Pack destination: ") + PackSelection.Folder(this)
                + "\n\n"
                + L.T("Alle Dateien aus diesem Ordner in den Hauptordner deines Custom Packs kopieren und gleichnamige Dateien ersetzen. Danach „Pack-Dateien abgleichen“ wählen und das Pack im Launcher erneut aktivieren/starten.\n\n„Projekt speichern“ speichert nur deinen Arbeitsstand; „Charakter exportieren“ erzeugt die Spieldateien.",
                    "Copy every file from this folder into your custom pack's main folder and replace matching filenames. Then choose Compare pack files and activate/launch the pack again in your launcher.\n\nSave project stores your work; Export character creates the game files."), Text,
                full ? L.T("Im Spiel testen…", "Test in-game…") : null);
            if (full && next == DialogResult.Yes) OpenExportTest(destination);
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
            var data = new Project { CharacterStarEffect = starEffect.Checked, AnimationStyle = ModelRig.AnimationStyles[Math.Max(0, animationStyle.SelectedIndex)], Version = reviewedRig != null && reviewedRig.MenuPose != null && !String.IsNullOrEmpty(reviewedRig.MenuPose.MenuSourceCode) ? 4 : movementSource.SelectedIndex > 0 ? 3 : 2, MovementCode = movementSource.SelectedIndex > 0 ? MovementTarget.Character.Code : null,
                MovementSlot = movementSource.SelectedIndex > 0 ? MovementTarget.Slot : 0, RRRoot = catalog.Root, Character = Selected.Id, Slot = Slot, Archives = archives.Values.ToArray(), ScalePercent = modelScale.Value, FitScale = model == null ? 1 : model.FitScale, ReferenceHeight = model == null ? 0 : model.ReferenceHeight, Names = ReadNames(), ModelNeedsConversion = modelNeedsConversion };
            CharacterPackage.SaveProject(path, sidecar =>
            {
                var savedFiles = new List<string>();
                foreach (var asset in assets)
                {
                    string copy = ThemeProject.Child(sidecar, asset.Target + ".murasset");
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.WriteAllBytes(copy, asset.Data); savedFiles.Add(copy);
                }
                data.Files = savedFiles.ToArray();
                data.PaintBases = assets.Where(a => a.PaintBase != null).ToDictionary(a => a.Target, a => Convert.ToBase64String(a.PaintBase));
                data.LogoRegions = assets.Where(a => a.LogoRegions != null && a.LogoRegions.Length > 0)
                    .ToDictionary(a => a.Target, a => a.LogoRegions);
                if (model != null && Path.GetExtension(model.Source) != ".json")
                {
                    data.Model = Path.Combine(sidecar, "source.glb");
                    IntegratedModelImport.ExportCopy(model.PreparedSource ?? model.Source, data.Model, 1, "glb2");
                }
                if (reviewedRig != null)
                {
                    string folder = Path.Combine(sidecar, "movement"); Directory.CreateDirectory(folder);
                    foreach (string texture in reviewedRig.Materials.Select(m => m.Texture).Where(t => t != null).Distinct())
                    {
                        string copy = ThemeProject.Child(folder, texture);
                        Directory.CreateDirectory(Path.GetDirectoryName(copy));
                        File.Copy(Path.Combine(reviewedRig.Folder, texture), copy);
                    }
                    File.Copy(reviewedRig.Reference, Path.Combine(folder, "reference.dae"));
                    data.Rig = Path.Combine(folder, "rig.json"); reviewedRig.Save(data.Rig);
                }
                return Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(data));
            });
            dirty = false; savedHistoryState = CaptureState();
            Status.Text = model != null && (reviewedRig == null || !reviewedRig.ReadyForCharacterExport)
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
            if (data == null || (data.Version != 1 && data.Version != 2 && data.Version != 3 && data.Version != 4) || data.Character < 0 || data.Character >= CharacterDefinition.All.Length || data.Slot < 1 || data.Slot > 50)
                throw new InvalidDataException(L.T("Nicht unterstütztes Charakterprojekt.", "Unsupported character project."));
            if (data.ScalePercent != 0 && (data.ScalePercent < 1 || data.ScalePercent > 10000)) throw new InvalidDataException(L.T("Ungültige Modellgröße.", "Invalid model scale."));
            CharacterNames.Validate(data.Names ?? new List<CharacterNameEntry>());
            var nextAssets = (data.Files ?? new string[0]).Select(p => ReadProjectAsset(p, CharacterDefinition.All[data.Character], data.Slot)).ToList();
            if (nextAssets.Select(a => a.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nextAssets.Count) throw new InvalidDataException(L.T("Doppelte Charakterziele.", "Duplicate character targets."));
            if (data.LogoRegions != null)
                foreach (var asset in nextAssets)
                {
                    CharacterLogoRegion[] regions;
                    if (!data.LogoRegions.TryGetValue(asset.Target, out regions)) continue;
                    if (regions == null || regions.Length > 64) throw new InvalidDataException("Invalid saved logo areas.");
                    var textures = CharacterImages.Textures(asset);
                    foreach (var region in regions)
                    {
                        var texture = region == null ? null : textures.SingleOrDefault(t => t.Name == region.Texture);
                        if (texture == null) throw new InvalidDataException("Saved logo texture is missing.");
                        var info = TplTextureEditor.GetImageInfo(texture.Tpl, 0);
                        if (region.X < 0 || region.Y < 0 || region.Width < 2 || region.Height < 2
                            || (long)region.X + region.Width > info.Width || (long)region.Y + region.Height > info.Height)
                            throw new InvalidDataException("Saved logo area is outside its texture.");
                    }
                    asset.LogoRegions = regions;
                }
            if (data.PaintBases != null)
                foreach (var asset in nextAssets)
                {
                    string encoded;
                    if (!data.PaintBases.TryGetValue(asset.Target, out encoded)) continue;
                    if (String.IsNullOrEmpty(encoded) || encoded.Length > 8 * 1024 * 1024) throw new InvalidDataException("Invalid saved paint texture.");
                    byte[] paintBase = Convert.FromBase64String(encoded);
                    var body = CharacterVehicleStyle.Body(asset);
                    if (body == null) throw new InvalidDataException("Saved vehicle paint texture is missing.");
                    var info = TplTextureEditor.GetImageInfo(paintBase, 0);
                    var currentInfo = TplTextureEditor.GetImageInfo(body.Tpl, 0);
                    if (info.Width != currentInfo.Width || info.Height != currentInfo.Height || info.Format != currentInfo.Format || info.MaxLod != currentInfo.MaxLod)
                        throw new InvalidDataException("Saved paint texture does not match the vehicle.");
                    using (var decoded = CharacterImages.Decode(paintBase)) { }
                    asset.PaintBase = paintBase;
                }
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
                if (Path.GetExtension(nextModel.Source).Equals(".json", StringComparison.OrdinalIgnoreCase)) nextModel.Warnings.Add(L.T(
                    "Die Modelldatei fehlt. Für eine andere Bewegungsquelle das ursprüngliche Modell erneut laden.",
                    "The model source is missing. Reload the original model to choose a different movement source."));
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
                if (nextRig != null) CharacterModelConversion.MenuTemplate(nextRig, rrRoot, "");
                string movementCode = data.MovementCode ?? (nextRig == null ? null : nextRig.MovementCode);
                int movementSlot = data.MovementSlot != 0 ? data.MovementSlot : nextRig == null ? 0 : nextRig.MovementSlot;
                CharacterModelConversion.MovementTarget(movementCode, movementSlot, CharacterDefinition.All[data.Character], data.Slot, rrRoot);
                if (nextRig != null && (nextRig.MovementCode != movementCode || nextRig.MovementSlot != movementSlot))
                    throw new InvalidDataException(L.T("Projekt und Haltung verwenden verschiedene Bewegungsquellen.",
                        "The project and pose use different movement sources."));
                foreach (var entry in data.Names ?? new List<CharacterNameEntry>()) nextCatalog.RequireExisting(entry.CharacterId, entry.CustomId);
            }
            catch { nextCatalog.Dispose(); throw; }
            string nextStyle = data.AnimationStyle ?? (nextRig == null ? null : nextRig.HumanAnimationStyle);
            if (!ModelRig.AnimationStyles.Contains(nextStyle)) { nextCatalog.Dispose(); throw new InvalidDataException("Unknown animation style."); }
            if (nextRig != null) nextRig.SetAnimationStyle(nextStyle);
            ClearData(); loading = true;
            animationStyle.SelectedIndex = Array.IndexOf(ModelRig.AnimationStyles, nextStyle);
            starEffect.Checked = data.CharacterStarEffect ?? true;
            if (nextRig != null) nextRig.StarEffect = starEffect.Checked;
            catalog = nextCatalog; target = catalog.Find(data.Character, data.Slot);
            FillMovementSources(data.MovementCode ?? (nextRig == null ? null : nextRig.MovementCode),
                data.MovementSlot != 0 ? data.MovementSlot : nextRig == null ? 0 : nextRig.MovementSlot);
            assets.AddRange(nextAssets);
            foreach (var pair in nextArchives) archives.Add(pair.Key, pair.Value);
            foreach (var e in data.Names ?? new List<CharacterNameEntry>()) names.Rows.Add(e.CharacterId, e.CustomId, e.Name, e.Author, catalog.Find(e.CharacterId, e.CustomId).ToString());
            AddTargetName();
            modelScale.Value = data.ScalePercent == 0 ? 100 : data.ScalePercent;
            model = nextModel; reviewedRig = nextRig; modelNeedsConversion = data.ModelNeedsConversion || model != null && !data.CharacterStarEffect.HasValue; RenderModel(); UpdateFiles();
            if (model != null) tabs.SelectedIndex = 1;
            loading = false; dirty = false;
            RefreshFileActions(); PackSelection.SourceLoaded(this);
            ResetHistory();
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
