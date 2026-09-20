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
        readonly CharacterModelViewport preview = new CharacterModelViewport { ShowBones = true };
        readonly ComboBox bone = new ComboBox { Name = "BodyPart", DropDownStyle = ComboBoxStyle.DropDownList, Width = 232, DropDownWidth = 310 };
        readonly ComboBox mode = new ComboBox { Name = "RigTool", DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
        readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true, UseMnemonic = false, Padding = new Padding(4) };
        readonly Label help = new Label { AutoSize = true, MaximumSize = new Size(232, 0), Margin = new Padding(3, 8, 3, 8) };
        readonly TrackBar pose = new TrackBar { Minimum = -90, Maximum = 90, TickFrequency = 30, Width = 228, Height = 35, AutoSize = false };
        readonly NumericUpDown strength = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 100, Width = 72 };
        readonly CheckBox referencePose = new CheckBox { Text = L.T("RR-Spielposition zeigen", "Show RR game position"), AutoSize = true };
        readonly CheckBox weights = new CheckBox { Text = L.T("Zuordnung farbig zeigen", "Show assignment colours"), AutoSize = true };
        readonly CheckBox play = new CheckBox { Text = L.T("Bewegungsprobe abspielen", "Play movement test"), AutoSize = true };
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
        readonly Timer timer = new Timer { Interval = 120 };
        bool pending;
        double phase;

        sealed class RigState
        {
            internal int[][] Indices;
            internal int[] Manual, Disabled;
            internal float[][] Weights, Guides;
            internal bool Aligned, Pending, NaturalVehicles;
            internal GamePoseSettings Menu, Race;
            internal Dictionary<string, GamePoseSettings> Vehicles;
            internal string Vehicle, BindingMethod;
        }

        internal ModelRigForm(ModelRig original) : this(original, null, "l") { }

        internal ModelRigForm(ModelRig original, RigReferenceSet references, string referenceWeight = "l")
        {
            Result = ModelRig.Serializer().Deserialize<ModelRig>(ModelRig.Serializer().Serialize(original));
            this.references = references; this.referenceWeight = referenceWeight;
            Result.Folder = original.Folder;
            Result.Reference = original.Reference;
            if (Result.JointGuides == null) Result.FitJointGuides();
            pending = !Result.AlignToReference;
            Text = L.T("Charakterbewegung prüfen", "Review character movement");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 850);
            MinimumSize = new Size(960, 700);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(StudioChrome.Header(Text, L.T("Gelenke zuordnen • Menü- und Fahrhaltung anpassen • RR-Bewegung prüfen", "Assign joints • Adjust menu and driving poses • Review RR movement")), 0, 0);
            var steps = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var placeStep = new Button { Name = "PlaceJointsStep", Text = L.T("1 · Gelenke platzieren", "1 · Place joints"), Dock = DockStyle.Fill };
            var autoStep = new Button { Name = "AssignStep", Text = L.T("2 · Automatisch zuordnen", "2 · Assign automatically"), Dock = DockStyle.Fill };
            var gameStep = new Button { Name = "GamePoseStep", Text = L.T("3 · Spielhaltung bearbeiten", "3 · Edit game pose"), Dock = DockStyle.Fill };
            gameStep.Click += delegate { mode.SelectedIndex = 4; };
            var checkStep = new Button { Name = "CheckMovementStep", Text = L.T("4 · Bewegung prüfen", "4 · Check movement"), Dock = DockStyle.Fill };
            placeStep.Click += delegate { mode.SelectedIndex = 0; };
            autoStep.Click += delegate { PushUndo(); if (Reassign()) mode.SelectedIndex = 4; };
            checkStep.Click += delegate { mode.SelectedIndex = 3; };
            steps.Controls.Add(placeStep, 0, 0); steps.Controls.Add(autoStep, 1, 0); steps.Controls.Add(gameStep, 2, 0);
            steps.Controls.Add(checkStep, 3, 0);
            layout.Controls.Add(steps, 0, 1);
            var work = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            work.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 268));
            work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            work.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2) };
            controls.Controls.Add(new Label { Tag = "GameHide", Text = L.T("Werkzeug", "Tool"), AutoSize = true });
            mode.Items.AddRange(new object[] { L.T("1 · Gelenke verschieben", "1 · Move joints"), L.T("Korrektur · Pinsel", "Correction · Brush"), L.T("Korrektur · Rechteck", "Correction · Rectangle"), L.T("4 · Bewegung prüfen", "4 · Check movement"), L.T("3 · Spielhaltung bearbeiten", "3 · Edit game pose") });
            controls.Controls.Add(mode);
            controls.Controls.Add(help);
            controls.Controls.Add(new Label { Tag = "GameHide", Text = L.T("Körperteil · links/rechts aus Sicht der Figur", "Body part · left/right from the figure's view"), AutoSize = true, MaximumSize = new Size(232, 0) });
            RefreshParts();
            controls.Controls.Add(bone);
            back = AddButton(controls, L.T("Rückgängig", "Undo"), Undo);
            var suggest = AddButton(controls, L.T("Gelenke vorplatzieren", "Suggest joint positions"), delegate { PushUndo(); Result.FitJointGuides(); Reassign(); });
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
                        PushUndo(); Result.SetPartEnabled(selectedPart, toggle.Checked); pending = false; apply.Enabled = true;
                        RefreshParts(); pose.Value = 0; preview.Invalidate();
                        status.Text = L.T("Ausgeschaltete Teile erhalten keine Flächen und keine Gelenkhilfe. Die RR-Knochen bleiben intern erhalten.", "Disabled parts receive no surfaces or joint guides. Required RR bones remain internally.");
                    };
                    partToggles.Add(part, toggle); controls.Controls.Add(toggle);
                }
                controls.Controls.Add(new Label { Tag = "SourceOnly", Text = L.T("Nicht vorhanden? Häkchen entfernen. Danach Bewegung prüfen.", "Part absent? Uncheck it, then check movement."), AutoSize = true, MaximumSize = new Size(232, 0) });
            }
            gameControls = CreateGameControls();
            controls.Controls.Add(gameControls);
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
                preview.Invalidate();
            });

            
            work.Controls.Add(controls, 0, 0);
            var scene = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            scene.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scene.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var views = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            foreach (var view in new[] { Tuple.Create("3D", 0), Tuple.Create(L.T("Vorne", "Front"), 1), Tuple.Create(L.T("Seite", "Side"), 2), Tuple.Create(L.T("Hinten", "Back"), 4) })
            {
                int index = view.Item2;
                var button = new Button { Text = view.Item1, AutoSize = true };
                button.Click += delegate { preview.SetView(index); };
                views.Controls.Add(button);
            }
            var resetView = new Button { Text = L.T("Zurücksetzen", "Reset view"), AutoSize = true };
            resetView.Click += delegate { preview.ResetView(); };
            views.Controls.Add(resetView);
            views.Controls.Add(CreateReferenceControls());
            scene.Controls.Add(views, 0, 0);
            preview.Model = Result.Preview();
            scene.Controls.Add(preview, 0, 1);
            var navigation = new Label { Text = L.T("Rechts ziehen: drehen · Mausrad: zoomen · Mausrad ziehen: verschieben\nVorne/Seite zum Platzieren verwenden. Links/rechts gehört immer zur Figur.", "Right-drag: orbit · Wheel: zoom · Middle-drag: pan\nUse Front/Side to place joints. Left/right always refers to the figure."), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4) };
            scene.Controls.Add(navigation, 0, 2);
            scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var motionBar = new FlowLayoutPanel { Name = "MotionControls", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Visible = false };
            motionBar.Controls.Add(gameAnimation); motionBar.Controls.Add(play); motionBar.Controls.Add(pose);
            gameAnimation.SelectedIndexChanged += delegate { ChangeAnimation(); };
            play.Margin = new Padding(5, 10, 5, 3);
            scene.Controls.Add(motionBar, 0, 3);
            work.Controls.Add(scene, 1, 0);
            layout.Controls.Add(work, 0, 2);
            layout.Controls.Add(status, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            apply = new Button { Text = L.T("Zuordnung übernehmen", "Use assignment"), AutoSize = true, Height = 36, Enabled = !pending };
            apply.Click += delegate { Result.InitializeGamePose(1, references == null ? null : references.Menu); if (references != null) Result.SelectVehiclePose(VehicleKey, references.GetVehicle(VehicleKey, false)); else Result.InitializeGamePose(2, null); Result.Validate(); DialogResult = DialogResult.OK; Close(); };
            footer.Controls.Add(apply);
            footer.Controls.Add(new Button { Text = L.T("Abbrechen", "Cancel"), AutoSize = true, Height = 36, DialogResult = DialogResult.Cancel });
            layout.Controls.Add(footer, 0, 4);
            Controls.Add(layout);
            mode.SelectedIndexChanged += delegate {
                play.Checked = false; pose.Value = 0;
                bool edit = mode.SelectedIndex == 0, marking = mode.SelectedIndex == 1 || mode.SelectedIndex == 2;
                preview.EditJoints = edit; preview.Marking = marking; preview.BrushSelection = mode.SelectedIndex == 1;
                if (edit) referencePose.Checked = false;
                else if (mode.SelectedIndex == 3 && Result.AlignToReference) referencePose.Checked = true;
                referencePose.Enabled = !edit && Result.AlignToReference;
                pose.Visible = play.Visible = pose.Enabled = play.Enabled = mode.SelectedIndex == 3;
                motionBar.Visible = mode.SelectedIndex == 3;
                navigation.Visible = mode.SelectedIndex != 3;
                gameStep.BackColor = mode.SelectedIndex == 4 ? DarkTheme.Accent : DarkTheme.Back;
                suggest.Visible = edit;
                placeStep.BackColor = edit ? DarkTheme.Accent : DarkTheme.Back;
                checkStep.BackColor = mode.SelectedIndex == 3 ? DarkTheme.Accent : DarkTheme.Back;
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
                    ? L.T("Weiße Gelenkpunkte links ziehen: Schulter auf Schulter, Ellbogen auf Ellbogen. Nach dem Loslassen wird neu zugeordnet. Cyan zeigt den gewählten Punkt.", "Left-drag white joints onto the matching shoulder, elbow, etc. Reassignment runs when you release. Cyan marks the selected joint.")
                    : marking ? L.T("Links markieren. Strg entfernt Flächen; beim Rechteck ergänzt Umschalt die Auswahl. Danach „Markierung zuordnen“. Rechtsziehen dreht weiterhin.", "Mark with the left button. Ctrl removes surfaces; Shift adds rectangles. Then assign the marked area. Right-drag still rotates.")
                    : L.T("Körperteil wählen und Bewegungsprobe abspielen. „RR-Spielposition“ zeigt die Anpassung an das Original-Skelett. Bei Fehlern Gelenke oder Bereiche korrigieren.", "Choose a body part and play the motion test. RR game position shows the fit to the original skeleton. Correct joints or areas if needed.");
                RefreshGameMode();
                preview.Invalidate();
            };
            referencePose.CheckedChanged += delegate { preview.ReferencePose = referencePose.Checked; preview.Invalidate(); };
            weights.CheckedChanged += delegate { preview.ShowWeights = weights.Checked; preview.Invalidate(); };
            play.CheckedChanged += delegate { timer.Enabled = play.Checked; if (!play.Checked) pose.Value = preview.GameContext > 0 && ActiveReference != null ? Math.Min(pose.Maximum, (int)ActiveReference.Frame - 1) : 0; };
            timer.Tick += delegate { if (preview.GameContext > 0 && ActiveReference != null) { pose.Value = pose.Value >= pose.Maximum ? pose.Minimum : pose.Value + 1; } else { phase += .2; pose.Value = (int)(Math.Sin(phase) * 40); } };
            pose.ValueChanged += delegate { if (preview.GameContext > 0 && ActiveReference != null && mode.SelectedIndex == 3) RefreshAnimation(); else preview.PoseDegrees = pose.Value; preview.Invalidate(); };
            bone.SelectedIndexChanged += delegate { preview.SelectedBone = SelectedBoneIndex; pose.Value = 0; preview.Invalidate(); };
            preview.BoneSelected += delegate { SelectBone(preview.SelectedBone); };
            preview.JointMoveStarted += delegate { PushUndo(); };
            preview.JointMoved += delegate { if (preview.EditGameJoints) { preview.AnimationPoints = null; ShowContactStatus(); } else Reassign(); };
            preview.SelectionChanged += delegate { RefreshSelection(); };
            int initial = Array.FindIndex(Result.Bones, b => b.Name == "arm_l1");
            SelectBone(initial < 0 ? 0 : initial);
            mode.SelectedIndex = Result.AlignToReference && references != null ? 4 : 0;
            preview.SetView(1);
            back.Enabled = assign.Enabled = false;
            status.Text = L.T("Vorschlag prüfen: Gelenkpunkte anpassen oder „Automatisch zuordnen“ drücken. Haare, Kleidung und Gelenke anschließend in Bewegung kontrollieren.", "Review the suggestion: adjust joints or click Assign automatically. Then check hair, clothes and joints in motion.");
            if (Result.AlignToReference)
                status.Text = L.T("Automatisch vorbereitet. Menü und Fahren prüfen, bei Bedarf Gelenke korrigieren, dann Haltung übernehmen.",
                    "Prepared automatically. Review Menu and Driving, adjust joints if needed, then accept the pose.");
            else if (!String.IsNullOrEmpty(Result.BindingWarning))
                status.Text = L.T("Gelenkpunkte am Modell platzieren und anschließend „Automatisch zuordnen“ wählen. ",
                    "Place the joint points on your model, then choose Assign automatically. ") + Result.BindingWarning;
            DarkTheme.Apply(this);
            apply.BackColor = DarkTheme.Accent;
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
            var button = new Button { Text = text, Width = 232, Height = 34, AutoEllipsis = true };
            button.Click += delegate { action(); };
            parent.Controls.Add(button);
            return button;
        }
        void PushUndo()
        {
            undo.Push(new RigState { BindingMethod = Result.BindingMethod, NaturalVehicles = Result.NaturalVehicleFitting, Vehicle = Result.ActiveVehicle, Vehicles = ModelRig.Serializer().Deserialize<Dictionary<string, GamePoseSettings>>(ModelRig.Serializer().Serialize(Result.VehiclePoses)), Menu = ClonePose(Result.MenuPose), Race = ClonePose(Result.RacePose), Disabled = Result.DisabledBones == null ? null : (int[])Result.DisabledBones.Clone(), Indices = Result.BoneIndices.Select(v => (int[])v.Clone()).ToArray(), Weights = Result.BoneWeights.Select(v => (float[])v.Clone()).ToArray(), Guides = Result.JointGuides.Select(v => (float[])v.Clone()).ToArray(), Aligned = Result.AlignToReference, Pending = pending, Manual = Result.ManualVertices == null ? null : (int[])Result.ManualVertices.Clone() });
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
            var state = undo.Pop();
            Result.BindingMethod = state.BindingMethod;
            Result.MenuPose = state.Menu; Result.RacePose = state.Race;
            Result.NaturalVehicleFitting = state.NaturalVehicles;
            Result.VehiclePoses = state.Vehicles; Result.ActiveVehicle = state.Vehicle;
            Result.DisabledBones = state.Disabled; RefreshParts(); Result.ManualVertices = state.Manual; Result.BoneIndices = state.Indices; Result.BoneWeights = state.Weights; Result.JointGuides = state.Guides; Result.AlignToReference = state.Aligned; pending = state.Pending;
            Result.InvalidateAlignment();
            updatingGame = true;
            try { if (state.Vehicle != null) gameVehicle.SelectedItem = gameVehicle.Items.Cast<VehicleChoice>().FirstOrDefault(v => v.Key == state.Vehicle); }
            finally { updatingGame = false; }
            back.Enabled = undo.Count > 0; apply.Enabled = !pending;
            referencePose.Enabled = mode.SelectedIndex != 0 && Result.AlignToReference;
            if (!Result.AlignToReference) referencePose.Checked = false;
            RefreshGameMode();
            status.Text = L.T("Letzte Änderung rückgängig gemacht.", "Last change undone.");
            preview.Invalidate();
        }
        bool Reassign()
        {
            pending = true; apply.Enabled = false;
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
            pending = false; apply.Enabled = true;
            referencePose.Enabled = mode.SelectedIndex != 0;
            status.Text = L.T("Automatisch neu zugeordnet. Unter „Bewegung prüfen“ die RR-Spielposition und Bewegungen kontrollieren.", "Reassigned automatically. Use Check movement to review the RR game position and motion.");
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