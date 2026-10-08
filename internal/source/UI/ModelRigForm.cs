using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm : Form
    {
        internal readonly ModelRig Result;
        readonly CharacterModelViewport preview = new CharacterModelViewport { ShowBones = true, ShowDimensions = false };
        readonly CheckBox editPoseJoints = new CheckBox { Name = "EditPoseJoints", Text = L.T("Gelenke bearbeiten", "Edit joints"), AutoSize = true };
        readonly ComboBox bone = new ComboBox { Name = "BodyPart", DropDownStyle = ComboBoxStyle.DropDownList, Width = 232, DropDownWidth = 310 };
        readonly ComboBox mode = new ComboBox { Name = "RigTool", DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
        readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false, Padding = new Padding(4) };
        readonly Label help = new Label { AutoSize = true, MaximumSize = new Size(232, 0), Margin = new Padding(3, 8, 3, 8) };
        readonly TrackBar pose = new TrackBar { Name = "MotionTimeline", Minimum = -90, Maximum = 90, TickFrequency = 30, Width = 164, Height = 32, AutoSize = false };
        readonly NumericUpDown strength = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 100, Width = 72 };
        readonly CheckBox referencePose = new CheckBox { Text = L.T("RR-Spielposition zeigen", "Show RR game position"), AutoSize = true };
        readonly CheckBox weights = new CheckBox { Text = L.T("Zuordnung farbig zeigen", "Show assignment colours"), AutoSize = true };
        readonly CheckBox play = new CheckBox { Text = L.T("Abspielen", "Play"), AutoSize = true };
        readonly Button apply, back, assign;
        readonly Dictionary<string, CheckBox> partToggles = new Dictionary<string, CheckBox>();
        bool refreshingParts;
        sealed class BoneChoice
        {
            internal int Index;
            internal string Label;
            public override string ToString() { return Label; }
        }
        int SelectedBoneIndex { get { return bone.SelectedItem is BoneChoice ? ((BoneChoice)bone.SelectedItem).Index : -1; } }
        readonly Stack<RigState> undo = new Stack<RigState>();
        readonly Stack<RigState> redo = new Stack<RigState>();
        RigState[] redoBeforeEdit;
        readonly StudioUndoRedo rigHistory;
        readonly Timer timer = new Timer { Interval = 33 };
        readonly System.Diagnostics.Stopwatch playbackClock = new System.Diagnostics.Stopwatch();
        int playbackStart;
        bool pending;
        double phase;

        sealed class RigState
        {
            internal ModelRig.MeshState Mesh;
            internal int[][] Indices;
            internal int[] Manual, Disabled;
            internal float[][] Weights, Guides;
            internal bool Aligned, Pending, NaturalVehicles, ComponentsReviewed;
            internal Dictionary<string, float[]> ReviewedJoints;
            internal int[] RigidComponents, RigidVertices;
            internal string SurfaceFingerprint, AnimationStyle;
            internal Dictionary<string, string> SurfaceFingerprints;
            internal Dictionary<string, string> MenuSurfaceReviews;
            internal GamePoseSettings Menu, Race;
            internal Dictionary<string, GamePoseSettings> Vehicles;
            internal string Vehicle, BindingMethod;
        }

        internal ModelRigForm(ModelRig original) : this(original, null, "l") { }

        internal ModelRigForm(ModelRig original, RigReferenceSet references, string referenceWeight = "l") : this(original, references, referenceWeight, false) { }

        internal ModelRigForm(ModelRig original, RigReferenceSet references, string referenceWeight, bool cleanupEntry)
        {
            Result = ModelRig.Serializer().Deserialize<ModelRig>(ModelRig.Serializer().Serialize(original));
            this.references = references; this.referenceWeight = referenceWeight;
            Result.Folder = original.Folder;
            Result.Reference = original.Reference;
            if (Result.JointGuides == null) Result.FitJointGuides();
            pending = !Result.AlignToReference || Result.JointIssues().Count > 0;
            Text = L.T("Modell bearbeiten", "Edit model");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 850);
            MinimumSize = new Size(960, 700);
            var layout = new TableLayoutPanel { Name = "RigWorkspace", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(StudioChrome.Header(Text, L.T("Modell bereinigen • Gelenke prüfen • Haltung und Bewegung anpassen", "Clean up model • Review joints • Adjust pose and movement")), 0, 0);
            var historyHost = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            rigHistory = new StudioUndoRedo(this, historyHost, () => undo.Count > 0, () => redo.Count > 0, Undo, Redo);
            back = rigHistory.UndoButton;
            layout.Controls.Add(header, 0, 0);
            var steps = new TableLayoutPanel { Name = "RigStages", Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 2, 0, 4) };
            for (int i = 0; i < 4; i++) steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 4));
            var placeStep = new Button { Name = "PlaceJointsStep", Text = L.T("2 · Gelenke", "2 · Joints"), Dock = DockStyle.Fill };
            var gameStep = new Button { Name = "GamePoseStep", Text = L.T("4 · Haltung && Bewegung", "4 · Pose && movement"), Dock = DockStyle.Fill };
            gameStep.Click += delegate { mode.SelectedIndex = 4; };
            var checkStep = new Button { Name = "CheckMovementStep", Text = L.T("3 · Oberfläche", "3 · Surface"), Dock = DockStyle.Fill };
            placeStep.Click += delegate { mode.SelectedIndex = 0; };
            checkStep.Click += delegate { mode.SelectedIndex = 5; };
            steps.Controls.Add(placeStep, 1, 0);
            steps.Controls.Add(checkStep, 2, 0);
            steps.Controls.Add(gameStep, 3, 0);
            var deleteStep = new Button { Name = "DeleteToolStep", Text = L.T("1 · Bereinigen", "1 · Clean up"), Dock = DockStyle.Fill };
            deleteStep.Click += delegate { mode.SelectedIndex = 6; };
            steps.Controls.Add(deleteStep, 0, 0);
            layout.Controls.Add(steps, 0, 1);
            var work = new TableLayoutPanel { Name = "RigSceneAndProperties", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            work.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286));
            work.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var controls = new FlowLayoutPanel { Name = "RigProperties", Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(9, 8, 5, 8), Margin = new Padding(6, 0, 0, 0), BackColor = DarkTheme.Panel };
            RigSection(controls, L.T("Eigenschaften", "Properties"));
            controls.Controls.Add(new Label { Name = "ToolLabel", Tag = "GameHide", Text = L.T("Werkzeug", "Tool"), AutoSize = true });
            mode.Items.AddRange(new object[] { L.T("Gelenke verschieben", "Move joints"), L.T("Korrektur · Pinsel", "Correction · Brush"), L.T("Korrektur · Rechteck", "Correction · Rectangle"), L.T("Bewegung bearbeiten", "Edit movement"), L.T("Haltung prüfen", "Review pose"), L.T("Oberfläche korrigieren", "Correct surface"), L.T("Teil löschen", "Delete part") });
            controls.Controls.Add(mode);
            controls.Controls.Add(help);
            controls.Controls.Add(new Label { Name = "BodyPartLabel", Tag = "GameHide", Text = L.T("Körperteil · links/rechts aus Sicht der Figur", "Body part · left/right from the figure's view"), AutoSize = true, MaximumSize = new Size(232, 0) });
            RefreshParts();
            controls.Controls.Add(bone);
            CreateReviewControls(controls);
            CreateSurfaceControls(controls);
            CreateDeletionControls(controls);
            var suggest = AddButton(controls, L.T("Gelenke vorplatzieren", "Suggest joint positions"), delegate { PushUndo(); Result.FitJointGuides(); Reassign(); });
            var autoStep = AddButton(controls, L.T("Automatisch neu zuordnen", "Reassign automatically"), delegate {
                PushUndo();
                if (Reassign()) mode.SelectedIndex = 4;
            });
            autoStep.Name = "AssignStep";
            var parts = Enumerable.Range(0, Result.Bones.Length).Select(Result.OptionalPart).Where(part => part != null).Distinct().ToArray();
            if (parts.Length > 0)
            {
                controls.Controls.Add(new Label { Tag = "SourceOnly", Text = L.T("Zusatzteile deines Modells", "Your model's optional parts"), AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
                foreach (string part in parts)
                {
                    string selectedPart = part;
                    var toggle = new CheckBox { Tag = "SourceOnly", Name = "Part_" + part, Text = PartLabel(part), Width = 232, Height = 25, Checked = Enumerable.Range(0, Result.Bones.Length).Where(i => Result.OptionalPart(i) == part).All(Result.BoneEnabled) };
                    toggle.CheckedChanged += delegate {
                        if (refreshingParts) return;
                        PushUndo(); Result.SetPartEnabled(selectedPart, toggle.Checked); pending = false; RefreshReview();
                        RefreshParts(); pose.Value = 0; preview.Invalidate();
                        status.Text = L.T("Ausgeschaltete Teile erhalten keine Flächen und keine Gelenkhilfe. Die RR-Knochen bleiben intern erhalten.", "Disabled parts receive no surfaces or joint guides. Required RR bones remain internally.");
                    };
                    partToggles.Add(part, toggle); controls.Controls.Add(toggle);
                }
                controls.Controls.Add(new Label { Tag = "SourceOnly", Text = L.T("Nicht vorhanden? Häkchen entfernen. Danach Bewegung prüfen.", "Part absent? Uncheck it, then check movement."), AutoSize = true, MaximumSize = new Size(232, 0) });
            }
            gameControls = CreateGameControls();
            controls.Controls.Add(gameControls);
            controls.Controls.Add(CreateAnimationEditor());
            controls.Controls.Add(referencePose);
            controls.Controls.Add(weights);
            var brushRow = new FlowLayoutPanel { AutoSize = true, Width = 232 };
            brushRow.Controls.Add(new Label { Text = L.T("Pinselgröße", "Brush size"), AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            var brushSize = new NumericUpDown { Minimum = 4, Maximum = 90, Value = 20, Width = 68 };
            brushRow.Controls.Add(brushSize);
            controls.Controls.Add(brushRow);
            brushSize.ValueChanged += delegate { preview.BrushRadius = (int)brushSize.Value; };
            var through = new CheckBox { Text = L.T("Auch verdeckte Flächen auswählen", "Also select hidden surfaces"), AutoSize = true, MaximumSize = new Size(232, 0) };
            through.CheckedChanged += delegate { preview.ThroughSelection = through.Checked; };
            controls.Controls.Add(through);
            var selectArea = AddButton(controls, L.T("Zugeordneten Bereich auswählen", "Select assigned area"), SelectAssigned);
            var clearArea = AddButton(controls, L.T("Markierung leeren", "Clear marked area"), delegate { preview.SelectedVertices.Clear(); RefreshSelection(); });
            StudioActions.Icon(clearArea, StudioIcon.Close);
            var strengthRow = new FlowLayoutPanel { AutoSize = true, Width = 232 };
            strengthRow.Controls.Add(new Label { Text = L.T("Zuordnungsstärke %", "Assignment strength %"), AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            strengthRow.Controls.Add(strength);
            controls.Controls.Add(strengthRow);
            assign = AddButton(controls, L.T("Markierung zuordnen", "Assign marked area"), delegate {
                if (preview.SelectedVertices.Count == 0) return;
                PushUndo();
                Result.Assign(preview.SelectedVertices, SelectedBoneIndex, (float)strength.Value / 100);
                weights.Checked = true;
                status.Text = L.T("Markierter Bereich zugeordnet. Jetzt die Bewegung prüfen.", "Marked area assigned. Check its movement next.");
                RefreshReview(); preview.Invalidate();
            });

            
            work.Controls.Add(controls, 1, 0);
            var scene = new TableLayoutPanel { Name = "RigScene", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = DarkTheme.Panel2 };
            scene.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scene.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var views = new ToolStrip { Name = "RigCameraToolbar", Dock = DockStyle.Fill, AutoSize = true, GripStyle = ToolStripGripStyle.Hidden, Font = Font, Padding = new Padding(4, 0, 4, 0) };
            var cameraView = new ToolStripDropDownButton(L.T("Ansicht", "View"));
            foreach (var view in new[] { Tuple.Create("3D", 0), Tuple.Create(L.T("Vorne", "Front"), 1), Tuple.Create(L.T("Seite", "Side"), 2), Tuple.Create(L.T("Oben", "Top"), 3), Tuple.Create(L.T("Hinten", "Back"), 4) })
            {
                int index = view.Item2;
                var button = new ToolStripMenuItem(view.Item1);
                button.Click += delegate { preview.SetView(index); };
                cameraView.DropDownItems.Add(button);
            }
            var resetView = new ToolStripMenuItem(L.T("Ansicht zurücksetzen", "Reset view"));
            resetView.Click += delegate { preview.ResetView(); };
            cameraView.DropDownItems.Add(new ToolStripSeparator());
            cameraView.DropDownItems.Add(resetView);
            var fitView = new ToolStripButton(L.T("Modell einpassen", "Fit model"));
            fitView.Click += delegate { preview.FitView(); };
            StudioActions.Tool(cameraView, StudioIcon.Layout, false);
            StudioActions.Tool(fitView, StudioIcon.Fit, true);
            views.Items.Add(cameraView);
            views.Items.Add(fitView);
            preview.ViewChanged += delegate {
                int index = preview.CameraView;
                cameraView.Text = cameraView.DropDownItems[index].Text;
                cameraView.AccessibleName = L.T("Kamera: ", "Camera: ") + cameraView.Text;
                for (int i = 0; i < 5; i++) ((ToolStripMenuItem)cameraView.DropDownItems[i]).Checked = i == index;
            };
            DarkTheme.StyleToolStrip(views, new MurumsDarkToolStripRenderer());
            var solid = new CheckBox { Text = L.T("Form ohne Textur", "Solid surface"), AutoSize = true, Margin = new Padding(6, 8, 3, 3) };
            solid.CheckedChanged += delegate { preview.SolidSurface = solid.Checked; preview.Invalidate(); };
            controls.Controls.Add(CreateReferenceControls());
            controls.Controls.SetChildIndex(referenceControls, controls.Controls.IndexOf(vehicleControls));
            referenceControls.Controls.Add(solid);
            scene.Controls.Add(views, 0, 0);
            preview.Model = Result.Preview();
            scene.Controls.Add(preview, 0, 1);
            var navigation = new Label { Text = L.T("Rechts ziehen: drehen · Mausrad: zoomen · Mausrad ziehen: verschieben\nVorne/Seite zum Platzieren verwenden. Links/rechts gehört immer zur Figur.", "Right-drag: orbit · Wheel: zoom · Middle-drag: pan\nUse Front/Side to place joints. Left/right always refers to the figure."), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(6), ForeColor = DarkTheme.Muted };
            scene.SizeChanged += delegate { navigation.MaximumSize = new Size(Math.Max(200, scene.ClientSize.Width - 8), 0); };
            scene.Controls.Add(navigation, 0, 2);
            scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var motionBar = new FlowLayoutPanel { Name = "MotionControls", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Visible = false };
            motionBar.Controls.Add(gameAnimation); motionBar.Controls.Add(play); motionBar.Controls.Add(pose);
            var frameLabel = new Label { Text = L.T("Bild", "Frame"), AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            motionBar.Controls.Add(frameLabel);
            motionBar.Controls.Add(animationFrame);
            gameAnimation.SelectedIndexChanged += delegate { ChangeAnimation(); };
            play.Margin = new Padding(5, 10, 5, 3);
            scene.Controls.Add(motionBar, 0, 3);
            CreateHumanMotionControls(scene);
            var humanButton = StudioChrome.ActionButton(L.T("Eigene Prüfanimation…", "Own test animation…"));
            humanButton.Name = "ReviewHumanMovement"; humanButton.Click += delegate { ShowHumanMotion(); };
            motionBar.Controls.Add(humanButton);
            work.Controls.Add(scene, 0, 0);
            layout.Controls.Add(work, 0, 2);
            var feedback = new Panel { Name = "RigFeedback", Dock = DockStyle.Fill, Height = 58, AutoScroll = true, Margin = new Padding(0, 4, 0, 2), BackColor = DarkTheme.Panel };
            status.Dock = DockStyle.Top;
            feedback.Controls.Add(status);
            feedback.SizeChanged += delegate { status.MaximumSize = new Size(Math.Max(200, feedback.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8), 0); };
            layout.RowStyles[3].SizeType = SizeType.Absolute;
            layout.RowStyles[3].Height = 62;
            layout.Controls.Add(feedback, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            apply = StudioChrome.ActionButton(L.T("Haltung übernehmen", "Use pose"));
            apply.MinimumSize = new Size(200, 36);
            StudioUx.SetHelp(apply, L.T("Geprüfte Änderungen in das Charakterprojekt übernehmen. Erst der Export erstellt Spieldateien.", "Use reviewed changes in the character project. Export creates the game files."));
            apply.Enabled = !pending;
            apply.Click += delegate {
                try {
                    Result.InitializeGamePose(1, references == null ? null : references.GetMenu(Result));
                    if (references != null) Result.SelectVehiclePose(VehicleKey, references.GetVehicle(VehicleKey, false)); else Result.InitializeGamePose(2, null);
                    Result.RequireExportReview(); Result.Validate(); DialogResult = DialogResult.OK; Close();
                }
                catch (System.IO.InvalidDataException error) { mode.SelectedIndex = 5; RunSurfaceCheck(false); RefreshReview(); status.Text = error.Message; }
            };
            footer.Controls.Add(apply);
            footer.Controls.Add(confirmSurface);
            var draft = StudioChrome.ActionButton(L.T("Entwurf behalten", "Keep draft"));
            draft.Name = "KeepRigDraft";
            draft.Click += delegate { if (pending || Result.JointIssues().Count > 0) Result.AlignToReference = false; Result.Validate(); DialogResult = DialogResult.OK; Close(); };
            StudioUx.SetHelp(draft, L.T("Bearbeitungsstand im Projekt behalten. Fehlende Prüfungen sind vor dem vollständigen Export weiter erforderlich.", "Keep your work in the project. Missing reviews are still required before full export."));
            footer.Controls.Add(draft);
            var cancel = StudioChrome.ActionButton(L.T("Abbrechen", "Cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(cancel);
            footer.Controls.Add(historyHost);
            CancelButton = cancel;
            layout.Controls.Add(footer, 0, 4);
            Controls.Add(layout);
            ParentChanged += delegate {
                if (IsDisposed || Disposing || layout.IsDisposed) return;
                bool embedded = !TopLevel;
                header.Visible = !embedded;
                layout.RowStyles[0].Height = embedded ? 0 : StudioChrome.HeaderHeight;
            };
            mode.SelectedIndexChanged += delegate {
                if (mode.SelectedIndex >= 3 && mode.SelectedIndex != 6 && (pending || Result.JointIssues().Count > 0)) { mode.SelectedIndex = 0; return; }
                play.Checked = false; pose.Value = 0;
                bool edit = mode.SelectedIndex == 0, marking = mode.SelectedIndex == 1 || mode.SelectedIndex == 2;
                preview.EditJoints = edit; preview.Marking = marking; preview.BrushSelection = mode.SelectedIndex == 1;
                if (edit) referencePose.Checked = false;
                else if (mode.SelectedIndex == 3 && Result.AlignToReference) referencePose.Checked = true;
                referencePose.Enabled = !edit && Result.AlignToReference;
                play.Visible = pose.Enabled = play.Enabled = mode.SelectedIndex == 3 || mode.SelectedIndex == 4;
                pose.Visible = animationFrame.Visible = frameLabel.Visible = mode.SelectedIndex == 3;
                motionBar.Visible = mode.SelectedIndex == 3 || mode.SelectedIndex == 4;
                navigation.Visible = mode.SelectedIndex != 3;
                navigation.Text = L.T("Rechts ziehen: drehen · Mausrad: zoomen · Mausrad ziehen: verschieben", "Right-drag: orbit · Wheel: zoom · Middle-drag: pan")
                    + (mode.SelectedIndex >= 5 ? L.T("\nLinks auswählen · Umschalt ergänzt · Strg entfernt", "\nLeft: select · Shift: add · Ctrl: remove") : L.T("\nVorne/Seite zum Platzieren verwenden. Links/rechts gehört immer zur Figur.", "\nUse Front/Side to place joints. Left/right always refers to the figure."));
                gameStep.BackColor = mode.SelectedIndex == 4 ? DarkTheme.Accent : DarkTheme.Back;
                suggest.Visible = autoStep.Visible = edit;
                placeStep.BackColor = edit ? DarkTheme.Accent : DarkTheme.Back;
                checkStep.BackColor = mode.SelectedIndex == 5 ? DarkTheme.Accent : DarkTheme.Back;
                selectArea.Visible = clearArea.Visible = marking;
                if (!marking)
                {
                    preview.SelectedVertices.Clear();
                    assign.Enabled = false;
                    status.Text = pending
                        ? L.T("Gelenkpunkte prüfen und automatisch zuordnen.", "Review joint positions and assign automatically.")
                        : L.T("Zuordnung bereit. Gelenke anpassen oder Bewegung prüfen.", "Assignment ready. Adjust joints or check movement.");
                }
                brushRow.Visible = mode.SelectedIndex == 1;
                through.Visible = strengthRow.Visible = assign.Visible = marking;
                help.Text = edit
                    ? L.T("Punkte ziehen. Vorne und Seite prüfen. Cyan zeigt die Auswahl.", "Drag joints. Check front and side views. Cyan marks the selection.")
                    : marking ? L.T("Links markieren. Strg entfernt Flächen; beim Rechteck ergänzt Umschalt die Auswahl. Danach „Markierung zuordnen“. Rechtsziehen dreht weiterhin.", "Mark with the left button. Ctrl removes surfaces; Shift adds rectangles. Then assign the marked area. Right-drag still rotates.")
                    : L.T("Körperteil wählen und Bewegungsprobe abspielen. „RR-Spielposition“ zeigt die Anpassung an das Original-Skelett. Bei Fehlern Gelenke oder Bereiche korrigieren.", "Choose a body part and play the motion test. RR game position shows the fit to the original skeleton. Correct joints or areas if needed.");
                RefreshGameMode();
                RefreshSurfaceMode();
                RefreshReview();
                RefreshDeletionMode();
                preview.Invalidate();
            };
            referencePose.CheckedChanged += delegate { preview.ReferencePose = referencePose.Checked; preview.Invalidate(); };
            weights.CheckedChanged += delegate { preview.ShowWeights = weights.Checked; preview.Invalidate(); };
            play.CheckedChanged += delegate {
                playbackStart = pose.Value;
                playbackClock.Restart();
                timer.Enabled = play.Checked;
                preview.EditGameJoints = mode.SelectedIndex == 4 && editPoseJoints.Checked && !play.Checked;
                preview.ShowBones = mode.SelectedIndex != 4 || editPoseJoints.Checked && !play.Checked;
                gameControls.Enabled = !play.Checked;
                if (mode.SelectedIndex == 4 && !play.Checked)
                {
                    preview.AnimationPoints = preview.AnimationJoints = null;
                    preview.Invalidate();
                }
                RefreshKeyEditor();
            };
            timer.Tick += delegate {
                if (preview.GameContext > 0 && ActiveReference != null)
                    pose.Value = pose.Minimum + (playbackStart - pose.Minimum + (int)(playbackClock.Elapsed.TotalSeconds * 60)) % (pose.Maximum - pose.Minimum + 1);
                else { phase += .2; pose.Value = (int)(Math.Sin(phase) * 40); }
            };
            pose.ValueChanged += delegate { if (preview.GameContext > 0 && ActiveReference != null && (mode.SelectedIndex == 3 || mode.SelectedIndex == 4)) RefreshAnimation(); else preview.PoseDegrees = pose.Value; preview.Invalidate(); };
            bone.SelectedIndexChanged += delegate { preview.SelectedBone = SelectedBoneIndex; if (mode.SelectedIndex != 3) pose.Value = 0; RefreshKeyEditor(); RefreshReview(); preview.Invalidate(); };
            preview.BoneSelected += delegate { SelectBone(preview.SelectedBone); };
            preview.JointMoveStarted += delegate { PushUndo(); };
            preview.JointMoved += delegate { if (preview.EditGameJoints) { preview.AnimationPoints = null; ShowContactStatus(); } else { Result.ConfirmJoint(preview.SelectedBone); FinishJointReview(); } };
            preview.SelectionChanged += delegate { if (mode.SelectedIndex == 6) HandleDeletionSelection(); else if (mode.SelectedIndex == 5) RefreshSurfaceSelection(); else RefreshSelection(); };
            int initial = Array.FindIndex(Result.Bones, b => b.Name == "arm_l1");
            SelectBone(initial < 0 ? 0 : initial);
            mode.SelectedIndex = cleanupEntry ? 6 : Result.AlignToReference && Result.JointIssues().Count == 0 ? 4 : 0;
            if (!cleanupEntry && Result.JointIssues().Count > 0) SelectNextIssue();
            else RunSurfaceCheck(false);
            preview.SetView(1);
            back.Enabled = assign.Enabled = false;
            status.Text = L.T("Vorschlag prüfen: Gelenkpunkte anpassen oder „Automatisch zuordnen“ drücken. Haare, Kleidung und Gelenke anschließend in Bewegung kontrollieren.", "Review the suggestion: adjust joints or click Assign automatically. Then check hair, clothes and joints in motion.");
            if (Result.AlignToReference)
                status.Text = Result.SourceJointGuides == null
                    ? L.T("Kein Quellskelett: Die Gelenke sind geschätzt. Die automatische Zuordnung bestätigt keine anatomisch richtige Haltung.",
                        "No source skeleton: joint positions are estimated. Automatic binding does not confirm an anatomically correct pose.")
                    : L.T("Quellskelett übernommen. Menü und Fahren prüfen, dann Haltung übernehmen.",
                        "Source skeleton transferred. Review Menu and Driving, then accept the pose.");
            else if (!String.IsNullOrEmpty(Result.BindingWarning))
                status.Text = L.T("Gelenkpunkte am Modell platzieren und anschließend „Automatisch zuordnen“ wählen. ",
                    "Place the joint points on your model, then choose Assign automatically. ") + Result.BindingWarning;
            RefreshReview();
            FitPropertyContent(controls);
            DarkTheme.Apply(this);
            RefreshDeletionMode();
            DarkTheme.StylePrimary(apply);
            FormClosed += delegate { timer.Stop(); };
        }

        void SelectBone(int index)
        {
            for (int i = 0; i < bone.Items.Count; i++) if (((BoneChoice)bone.Items[i]).Index == index) { bone.SelectedIndex = i; return; }
            bone.SelectedIndex = bone.Items.Count > 0 ? 0 : -1;
        }
        void RefreshParts()
        {
            int selected = SelectedBoneIndex;
            bone.Items.Clear();
            for (int i = 0; i < Result.Bones.Length; i++)
                if (Result.GuideBone(i)) bone.Items.Add(new BoneChoice { Index = i, Label = BoneLabel(Result.Bones[i].Name) });
            SelectBone(selected);
            refreshingParts = true;
            foreach (var pair in partToggles)
                pair.Value.Checked = Enumerable.Range(0, Result.Bones.Length).Where(i => Result.OptionalPart(i) == pair.Key).All(Result.BoneEnabled);
            refreshingParts = false;
        }
        static string PartLabel(string part)
        {
            switch (part)
            {
                case "tail": return L.T("Schwanz vorhanden", "Has a tail");
                case "tie": return L.T("Krawatte / Zusatz vorhanden", "Has a tie / accessory");
                case "mouth": return L.T("Separater beweglicher Mund", "Separate movable mouth");
                case "wing": return L.T("Flügel vorhanden", "Has wings");
                case "ear": return L.T("Bewegliche Ohren vorhanden", "Has movable ears");
                default: return L.T("Fühler vorhanden", "Has antennae");
            }
        }
        Button AddButton(Control parent, string text, Action action)
        {
            var button = StudioChrome.ActionButton(text);
            button.AutoSize = false;
            button.Size = new Size(232, 36);
            button.AutoEllipsis = true;
            StudioUx.SetHelp(button, text);
            button.Click += delegate { action(); };
            parent.Controls.Add(button);
            return button;
        }
        void RigSection(Control parent, string text)
        {
            parent.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(232, 0), Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 10, 3, 6) });
        }
        void FitPropertyContent(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is CheckBox) child.MaximumSize = new Size(232, 0);
                var label = child as Label;
                if (label != null && label.AutoSize) label.MaximumSize = new Size(232, 0);
                FitPropertyContent(child);
            }
        }
        RigState CaptureHistory(bool meshChange)
        {
            return new RigState { MenuSurfaceReviews = Result.MenuSurfaceReviews == null ? null : new Dictionary<string, string>(Result.MenuSurfaceReviews), Mesh = meshChange ? Result.CaptureMesh() : null, AnimationStyle = Result.HumanAnimationStyle, RigidVertices = Result.RigidVertices == null ? null : (int[])Result.RigidVertices.Clone(), SurfaceFingerprint = Result.SurfaceReviewFingerprint, SurfaceFingerprints = Result.SurfaceReviewFingerprints == null ? null : new Dictionary<string, string>(Result.SurfaceReviewFingerprints), RigidComponents = Result.RigidComponentBones == null ? null : (int[])Result.RigidComponentBones.Clone(), ReviewedJoints = Result.ReviewedJoints == null ? null : Result.ReviewedJoints.ToDictionary(p => p.Key, p => (float[])p.Value.Clone()), ComponentsReviewed = Result.ComponentsReviewed, BindingMethod = Result.BindingMethod, NaturalVehicles = Result.NaturalVehicleFitting, Vehicle = Result.ActiveVehicle, Vehicles = ModelRig.Serializer().Deserialize<Dictionary<string, GamePoseSettings>>(ModelRig.Serializer().Serialize(Result.VehiclePoses)), Menu = ClonePose(Result.MenuPose), Race = ClonePose(Result.RacePose), Disabled = Result.DisabledBones == null ? null : (int[])Result.DisabledBones.Clone(), Indices = Result.BoneIndices.Select(v => (int[])v.Clone()).ToArray(), Weights = Result.BoneWeights.Select(v => (float[])v.Clone()).ToArray(), Guides = Result.JointGuides.Select(v => (float[])v.Clone()).ToArray(), Aligned = Result.AlignToReference, Pending = pending, Manual = Result.ManualVertices == null ? null : (int[])Result.ManualVertices.Clone() };
        }
        void PushUndo(bool meshChange = false)
        {
            undo.Push(CaptureHistory(meshChange));
            redoBeforeEdit = redo.Count == 0 ? null : redo.ToArray();
            redo.Clear();
            if (undo.Count > 16)
            {
                var recent = undo.Take(16).Reverse().ToArray();
                undo.Clear();
                foreach (var state in recent) undo.Push(state);
            }
            back.Enabled = true;
        }
        void Undo()
        {
            if (undo.Count == 0) return;
            redoBeforeEdit = null;
            var state = undo.Pop();
            redo.Push(CaptureHistory(state.Mesh != null));
            RestoreHistory(state);
            status.Text = L.T("Letzte Änderung rückgängig gemacht.", "Last change undone.");
        }
        void Redo()
        {
            if (redo.Count == 0) return;
            redoBeforeEdit = null;
            var state = redo.Pop();
            undo.Push(CaptureHistory(state.Mesh != null));
            RestoreHistory(state);
            status.Text = L.T("Änderung wiederholt.", "Change redone.");
        }
        void RollbackHistory()
        {
            if (undo.Count == 0) return;
            RestoreHistory(undo.Pop());
            redo.Clear();
            if (redoBeforeEdit != null)
                foreach (var state in redoBeforeEdit.Reverse()) redo.Push(state);
            redoBeforeEdit = null;
            rigHistory.Refresh();
        }
        void RestoreHistory(RigState state)
        {
            if (state.Mesh != null) { Result.RestoreMesh(state.Mesh); preview.Model = Result.Preview(); surfaceInclude.Clear(); surfaceExclude.Clear(); }
            Result.BindingMethod = state.BindingMethod;
            Result.HumanAnimationStyle = state.AnimationStyle;
            Result.RigidVertices = state.RigidVertices; Result.SurfaceReviewFingerprint = state.SurfaceFingerprint;
            Result.SurfaceReviewFingerprints = state.SurfaceFingerprints == null ? null : new Dictionary<string, string>(state.SurfaceFingerprints);
            Result.MenuSurfaceReviews = state.MenuSurfaceReviews == null ? null : new Dictionary<string, string>(state.MenuSurfaceReviews);
            humanPlay.Checked = false; humanBeforeIndices = null; humanBeforeWeights = null;
            surfaceComparison.Checked = false; surfaceBefore = null; surfaceReport = null;
            Result.RigidComponentBones = state.RigidComponents;
            Result.ReviewedJoints = state.ReviewedJoints; Result.ComponentsReviewed = state.ComponentsReviewed;
            Result.MenuPose = state.Menu; Result.RacePose = state.Race;
            Result.NaturalVehicleFitting = state.NaturalVehicles;
            Result.VehiclePoses = state.Vehicles; Result.ActiveVehicle = state.Vehicle;
            Result.DisabledBones = state.Disabled; RefreshParts(); Result.ManualVertices = state.Manual; Result.BoneIndices = state.Indices; Result.BoneWeights = state.Weights; Result.JointGuides = state.Guides; Result.AlignToReference = state.Aligned; pending = state.Pending;
            Result.InvalidateAlignment();
            updatingGame = true;
            try { if (state.Vehicle != null) gameVehicle.SelectedItem = gameVehicle.Items.Cast<VehicleChoice>().FirstOrDefault(v => v.Key == state.Vehicle); }
            finally { updatingGame = false; }
            back.Enabled = undo.Count > 0; RefreshReview();
            referencePose.Enabled = mode.SelectedIndex != 0 && Result.AlignToReference;
            if (!Result.AlignToReference) referencePose.Checked = false;
            RefreshGameMode();
            RefreshSurfaceMode();
            RefreshDeletionMode();
            rigHistory.Refresh();
            preview.Invalidate();
        }
        bool Reassign()
        {
            pending = true; apply.Enabled = false;
            if (Result.JointIssues().Count > 0) { RefreshReview(); return false; }
            try
            {
                bool assigned = ModelOperationForm.Run(this, L.T("Körper automatisch zuordnen", "Bind body automatically"),
                    token => { Result.BindWithBlender(token); return true; });
                if (!assigned) return false;
            }
            catch (Exception error)
            {
                status.Text = error.Message;
                StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK);
                return false;
            }
            pending = false; RunSurfaceCheck(false); RefreshReview();
            referencePose.Enabled = mode.SelectedIndex != 0;
            status.Text = Result.SourceJointGuides == null
                ? L.T("Oberfläche neu gebunden. Ohne Quellskelett bleiben die Gelenkpunkte ein Vorschlag; Haltung und Bewegungen prüfen.",
                    "Surface rebound. Without a source skeleton, joint positions remain a suggestion; review the pose and movement.")
                : L.T("Automatisch neu zugeordnet. Unter „Bewegung prüfen“ die RR-Spielposition und Bewegungen kontrollieren.", "Reassigned automatically. Use Check movement to review the RR game position and motion.");
            preview.Invalidate();
            return true;
        }
        void SelectAssigned()
        {
            preview.SelectedVertices.Clear();
            for (int i = 0; i < Result.Points.Length; i++)
                for (int j = 0; j < Result.BoneIndices[i].Length; j++)
                    if (Result.BoneIndices[i][j] == SelectedBoneIndex && Result.BoneWeights[i][j] >= .2f) preview.SelectedVertices.Add(i);
            mode.SelectedIndex = 1;
            RefreshSelection();
        }
        void RefreshSelection()
        {
            assign.Enabled = preview.SelectedVertices.Count > 0 && Result.BoneEnabled(SelectedBoneIndex);
            status.Text = preview.SelectedVertices.Count + L.T(" Eckpunkte markiert. Strg + links entfernt Flächen. Erst „Markierung zuordnen“ ändert die Bewegung.", " vertices marked. Ctrl + left removes surfaces. Assign marked area applies the movement change.");
            preview.Invalidate();
        }
        internal static string BoneLabel(string name)
        {
            switch (name)
            {
                case "skl_root": return L.T("Becken", "Hips");
                case "spin": return L.T("Oberkörper", "Torso");
                case "face_1": return L.T("Kopf / Hals", "Head / neck");
                case "mouth_1": return L.T("Mund", "Mouth");
                case "tail_1": return L.T("Schwanz", "Tail");
                case "tie_1": return L.T("Zusatz / Krawatte", "Accessory / tie");
                case "arm_l1": return L.T("Linke Schulter / Oberarm", "Left shoulder / upper arm");
                case "arm_r1": return L.T("Rechte Schulter / Oberarm", "Right shoulder / upper arm");
                case "arm_l2": return L.T("Linker Ellbogen / Unterarm", "Left elbow / forearm");
                case "arm_r2": return L.T("Rechter Ellbogen / Unterarm", "Right elbow / forearm");
                case "wrist_l1": return L.T("Linkes Handgelenk / Hand", "Left wrist / hand");
                case "wrist_r1": return L.T("Rechtes Handgelenk / Hand", "Right wrist / hand");
                case "leg_l1": return L.T("Linke Hüfte / Oberschenkel", "Left hip / thigh");
                case "leg_r1": return L.T("Rechte Hüfte / Oberschenkel", "Right hip / thigh");
                case "leg_l2": return L.T("Linkes Knie / Unterschenkel", "Left knee / lower leg");
                case "leg_r2": return L.T("Rechtes Knie / Unterschenkel", "Right knee / lower leg");
                case "ankle_l1": return L.T("Linker Knöchel / Fuß", "Left ankle / foot");
                case "ankle_r1": return L.T("Rechter Knöchel / Fuß", "Right ankle / foot");
                default: return name;
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
