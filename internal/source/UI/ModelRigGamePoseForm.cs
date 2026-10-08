using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        readonly RigReferenceSet references;
        readonly string referenceWeight;
        readonly ComboBox gameTarget = new ComboBox { Name = "GamePoseTarget", DropDownStyle = ComboBoxStyle.DropDownList, Width = 172 };
        readonly ComboBox referenceChoice = new ComboBox { Name = "ReferenceChoice", DropDownStyle = ComboBoxStyle.DropDownList, Width = 182 };
        readonly CheckBox showReference = new CheckBox { Name = "ShowOriginalModel", Text = L.T("Originalmodell", "Original model"), AutoSize = true };
        readonly Dictionary<string, NumericUpDown> gameValues = new Dictionary<string, NumericUpDown>();
        readonly Dictionary<string, RigPoseReference> animationFrames = new Dictionary<string, RigPoseReference>();
        Control gameControls;
        Button fitOriginalPose;
        readonly CheckBox showVehicle = new CheckBox { Text = L.T("Fahrzeug anzeigen", "Show vehicle"), AutoSize = true, Checked = true };
        readonly ComboBox gameVehicle = new ComboBox { Name = "GameVehicle", DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
        FlowLayoutPanel vehicleControls;
        sealed class VehicleChoice
        {
            internal string Key;
            public override string ToString() { return CharacterVehicleNames.ShortLabel(Key); }
        }
        string VehicleKey { get { return gameVehicle.SelectedItem is VehicleChoice ? ((VehicleChoice)gameVehicle.SelectedItem).Key : referenceWeight + "a_bike"; } }
        RigPoseReference SelectedReference(bool basis)
        {
            if (references == null) return null;
            return GameContext == 1 ? (basis ? references.BaseMenu : references.GetMenu(Result)) : references.GetVehicle(VehicleKey, basis);
        }
        FlowLayoutPanel referenceControls;
        readonly ComboBox gameAnimation = new ComboBox { Name = "GameAnimation", DropDownStyle = ComboBoxStyle.DropDownList, Width = 192 };
        readonly NumericUpDown motionStrength = new NumericUpDown { Name = "MotionStrength", Width = 60, Minimum = 0, Maximum = 100, Value = 100 };
        FlowLayoutPanel strengthControls;
        FlowLayoutPanel menuControls;
        FlowLayoutPanel menuPosture;
        readonly CheckBox stableMenu = new CheckBox { Name = "StableMenu", Text = L.T("Stand stabil halten", "Keep stance steady"), AutoSize = true };
        readonly NumericUpDown[] menuTilt = new NumericUpDown[2];
        Label menuSourceLabel;
        readonly ComboBox motionPreset = new ComboBox { Name = "MotionPreset", DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
        readonly ComboBox humanStyle = new ComboBox { Name = "HumanAnimationStyle", DropDownStyle = ComboBoxStyle.DropDownList, Width = 226 };
        bool updatingGame;
        static readonly int[] MotionPresets = { 0, 25, 60, 100 };
        sealed class AnimationChoice
        {
            internal string Name;
            public override string ToString()
            {
                switch (Name)
                {
                    case "sel_wait": return L.T("Menü: Warten", "Menu: idle");
                    case "sel_gut": return L.T("Menü: Bestätigen", "Menu: confirm");
                    case "drive": return L.T("Fahren / Lenken", "Drive / steering");
                    case "wait": case "wait2": return L.T("Warten", "Idle");
                    case "1st": return L.T("Siegerpose", "Victory");
                    case "good": case "gut": return L.T("Jubeln", "Celebrate");
                    case "bad": return L.T("Enttäuscht", "Disappointed");
                    case "back": return L.T("Zurückschauen", "Look back");
                    case "drift_l": return L.T("Links driften", "Drift left");
                    case "drift_r": return L.T("Rechts driften", "Drift right");
                    case "wheelie": return "Wheelie";
                    case "jump": return L.T("Sprung", "Jump");
                    case "jump_st": return L.T("Sprung: Absprung", "Jump: takeoff");
                    case "jump_ed": return L.T("Sprung: Landung", "Jump: landing");
                    case "sjump_st": return L.T("Rampentrick: Absprung", "Ramp trick: takeoff");
                    case "sjump_ed": return L.T("Rampentrick: Landung", "Ramp trick: landing");
                    case "sjump_ma": case "sjump_mb": case "sjump_mc":
                    case "sjump_sa": case "sjump_sb": case "sjump_sc":
                        return L.T("Rampentrick ", "Ramp trick ") + Name.Substring(6).ToUpperInvariant();
                    default: return Name;
                }
            }
        }
        int GameContext { get { return gameTarget.SelectedIndex == 1 ? 2 : 1; } }
        RigPoseReference ActiveReference { get { return SelectedReference(false); } }
        static GamePoseSettings ClonePose(GamePoseSettings settings)
        {
            return settings == null ? null : ModelRig.Serializer().Deserialize<GamePoseSettings>(ModelRig.Serializer().Serialize(settings));
        }
        Control CreateReferenceControls()
        {
            referenceControls = new FlowLayoutPanel { Name = "GameReferenceControls", AutoSize = true, WrapContents = true, Width = 232, MaximumSize = new Size(232, 0) };
            RigSection(referenceControls, L.T("Kontext & Referenz", "Context & reference"));
            gameTarget.Items.AddRange(new object[] { L.T("Charakterauswahl", "Character selection"), L.T("Fahren", "Driving") });
            gameTarget.SelectedIndex = 0;
            referenceChoice.Items.AddRange(new object[] {
                references == null ? L.T("RR-Variante", "RR variant") : references.VariantName,
                references == null ? L.T("Basischarakter", "Base character") : references.BaseName
            });
            referenceChoice.SelectedIndex = 0;
            var load = StudioChrome.ActionButton(L.T("Basis laden…", "Load base…"));
            load.Name = "LoadBaseReference";
            StudioActions.Icon(load, StudioIcon.Open);
            load.Click += delegate { LoadBaseReference(); };
            referenceControls.Controls.Add(gameTarget);
            referenceControls.SetFlowBreak(gameTarget, true);
            referenceControls.Controls.Add(new Label { Text = L.T("Animationsstil", "Animation style"), AutoSize = true });
            referenceControls.Controls.Add(humanStyle);
            referenceControls.Controls.Add(showReference);
            referenceControls.Controls.Add(referenceChoice);
            referenceControls.Controls.Add(load);
            vehicleControls = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 232, MinimumSize = new Size(0, 60) };
            vehicleControls.Controls.Add(new Label { Text = L.T("Fahrzeug · eigene Haltung", "Vehicle · separate pose"), AutoSize = true });
            foreach (string key in references == null || references.Weight == null ? new[] { referenceWeight + "a_bike" } : references.Vehicles)
                gameVehicle.Items.Add(new VehicleChoice { Key = key });
            gameVehicle.SelectedItem = gameVehicle.Items.Cast<VehicleChoice>().FirstOrDefault(v => v.Key == Result.ActiveVehicle);
            if (gameVehicle.SelectedIndex < 0 && gameVehicle.Items.Count > 0) gameVehicle.SelectedIndex = 0;
            vehicleControls.Controls.Add(gameVehicle);
            vehicleControls.Controls.Add(showVehicle);
            var menuReview = StudioChrome.ActionButton(L.T("Menüsitzposen prüfen…", "Review menu seating…"));
            menuReview.Name = "ReviewMenuSeating"; menuReview.Width = 232; menuReview.Enabled = references != null;
            menuReview.Click += delegate { OpenMenuVehicleReview(); };
            vehicleControls.Controls.Add(menuReview);
            showVehicle.CheckedChanged += delegate { if (!updatingGame) RefreshGameMode(); };
            Disposed += delegate { if (references != null) references.ClearVehicleVisuals(); };
            gameControls.Parent.Controls.Add(vehicleControls);
            gameControls.Parent.Controls.SetChildIndex(vehicleControls, gameControls.Parent.Controls.IndexOf(gameControls));
            gameVehicle.SelectedIndexChanged += delegate {
                if (updatingGame) return;
                play.Checked = false;
                try { RefreshGameMode(); }
                catch (Exception error) { status.Text = error.Message; }
            };
            menuControls = new FlowLayoutPanel { Name = "MenuTemplateControls", AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 232 };
            menuSourceLabel = new Label { Name = "MenuTemplateName", AutoSize = true, MaximumSize = new Size(232, 0), ForeColor = DarkTheme.Muted };
            menuControls.Controls.Add(menuSourceLabel);
            var templateActions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Width = 232, Margin = Padding.Empty };
            var choose = StudioChrome.ActionButton(L.T("Vorlage…", "Template…"));
            choose.Click += delegate { ChooseMenuTemplate(); };
            choose.MinimumSize = new Size(100, 36);
            templateActions.Controls.Add(choose);
            choose.Name = "ChooseMenuTemplate";
            choose.Enabled = references != null && Result.HasHumanJoints;
            var reset = StudioChrome.ActionButton(L.T("Standard", "Default"));
            reset.Click += delegate { ResetMenuTemplate(); };
            reset.MinimumSize = new Size(100, 36);
            templateActions.Controls.Add(reset);
            menuControls.Controls.Add(templateActions);
            reset.Name = "ResetMenuTemplate";
            reset.Enabled = references != null;
            gameControls.Parent.Controls.Add(menuControls);
            gameControls.Parent.Controls.SetChildIndex(menuControls, gameControls.Parent.Controls.IndexOf(gameControls));
            strengthControls = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 232 };
            RigSection(strengthControls, L.T("Bewegungsstärke", "Movement strength"));
            motionPreset.Items.AddRange(new object[] { L.T("Still · 0 %", "Still · 0%"), L.T("Sanft · 25 %", "Gentle · 25%"), L.T("Lebendig · 60 %", "Lively · 60%"), L.T("Volle Bewegung · 100 %", "Full movement · 100%"), L.T("Benutzerdefiniert", "Custom") });
            humanStyle.Items.AddRange(ModelRig.AnimationStyleLabels());
            humanStyle.SelectedIndexChanged += delegate {
                if (updatingGame || humanStyle.SelectedIndex < 0) return;
                PushUndo();
                Result.SetAnimationStyle(ModelRig.AnimationStyles[humanStyle.SelectedIndex]);
                RefreshGameMode();
                RefreshAnimation();
            };
            strengthControls.Controls.Add(motionPreset);
            motionPreset.SelectedIndexChanged += delegate {
                if (updatingGame || motionPreset.SelectedIndex < 0 || motionPreset.SelectedIndex >= MotionPresets.Length) return;
                motionStrength.Value = MotionPresets[motionPreset.SelectedIndex];
            };
            var strengthRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Width = 232, Margin = Padding.Empty };
            strengthRow.Controls.Add(new Label { Text = L.T("Bewegung %", "Movement %"), Width = 136, Height = 26, TextAlign = ContentAlignment.MiddleLeft });
            strengthRow.Controls.Add(motionStrength);
            strengthControls.Controls.Add(strengthRow);
            gameControls.Parent.Controls.Add(strengthControls);
            gameControls.Parent.Controls.SetChildIndex(gameControls, gameControls.Parent.Controls.IndexOf(menuControls));
            gameControls.Parent.Controls.SetChildIndex(strengthControls, gameControls.Parent.Controls.IndexOf(gameControls) + 1);
            StudioUx.SetHelp(motionStrength, L.T("0 %: gewählte Haltung bleibt stehen. 100 %: kräftige RR-Bewegung. Natürliche Haltungen begrenzen starke Verdrehungen. Für Menü und Fahren getrennt gespeichert.", "0%: holds your chosen pose. 100%: strong RR movement. Natural poses limit large twists. Saved separately for menu and driving."));
            motionStrength.ValueChanged += delegate {
                if (updatingGame) return;
                PushUndo(); Result.GameSettings(GameContext).MotionStrength = (float)motionStrength.Value;
                UpdateMotionPreset();
                if (mode.SelectedIndex == 3 || play.Checked) RefreshAnimation();
                preview.Invalidate();
            };
            gameTarget.SelectedIndexChanged += delegate { play.Checked = false; RefreshGameMode(); };
            referenceChoice.Visible = load.Visible = showReference.Checked;
            showReference.CheckedChanged += delegate {
                referenceChoice.Visible = load.Visible = showReference.Checked;
                RefreshReference();
            };
            referenceChoice.SelectedIndexChanged += delegate { RefreshReference(); };
            return referenceControls;
        }
        Control CreateGameControls()
        {
            var group = new FlowLayoutPanel { Name = "GamePoseTransform", FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 232, Visible = false };
            fitOriginalPose = AddButton(group, L.T("Natürlich anpassen", "Fit natural pose"), FitSelectedOriginalPose);
            fitOriginalPose.Name = "FitOriginalPose";
            var advanced = new CheckBox {
                Name = "AdvancedPoseToggle", UseMnemonic = false, Text = L.T("Position & Drehung", "Position & rotation"),
                AutoSize = true, MaximumSize = new Size(232, 0), Margin = new Padding(3, 10, 3, 5)
            };
            var transform = new FlowLayoutPanel {
                Name = "AdvancedPoseFields", FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoSize = true, Width = 232, Visible = false
            };
            advanced.CheckedChanged += delegate { transform.Visible = advanced.Checked; };
            menuPosture = new FlowLayoutPanel { Name = "MenuPosture", FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 232 };
            menuPosture.Controls.Add(stableMenu);
            stableMenu.CheckedChanged += delegate {
                if (updatingGame || GameContext != 1) return;
                PushUndo(); Result.MenuPose.StableMenu = stableMenu.Checked;
                if (play.Checked || mode.SelectedIndex == 3) RefreshAnimation();
                preview.Invalidate();
            };
            for (int i = 0; i < 2; i++)
            {
                int axis = i == 0 ? 0 : 2;
                var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Width = 232 };
                row.Controls.Add(new Label { Text = i == 0 ? L.T("Vor / zurück °", "Forward / back °") : L.T("Seitlich °", "Sideways °"), Width = 136, AutoSize = false, Height = 24, TextAlign = ContentAlignment.MiddleLeft });
                var tilt = new NumericUpDown { Name = "MenuTilt" + axis, Width = 75, DecimalPlaces = 1, Minimum = -180, Maximum = 180, Increment = 1 };
                menuTilt[i] = tilt;
                row.Controls.Add(tilt); menuPosture.Controls.Add(row);
                tilt.ValueChanged += delegate {
                    if (updatingGame || GameContext != 1) return;
                    gameValues["Rotation" + axis].Value = tilt.Value;
                };
            }
            group.Controls.Add(menuPosture);
            group.Controls.Add(editPoseJoints);
            editPoseJoints.CheckedChanged += delegate {
                play.Checked = false;
                preview.EditGameJoints = preview.ShowBones = editPoseJoints.Checked;
                bone.Visible = editPoseJoints.Checked;
                preview.AnimationPoints = preview.AnimationJoints = null;
                preview.Invalidate();
            };
            group.Controls.Add(advanced);
            group.Controls.Add(transform);
            foreach (string kind in new[] { "Position", "Rotation" })
            {
                transform.Controls.Add(new Label { Text = kind == "Position" ? L.T("Position · X Seite, Y Höhe, Z Tiefe", "Position · X side, Y height, Z depth") : L.T("Drehung X / Y / Z (Grad)", "Rotation X / Y / Z (degrees)"), AutoSize = true, MaximumSize = new Size(232, 0) });
                var row = new FlowLayoutPanel { AutoSize = true, Width = 232, WrapContents = false };
                for (int i = 0; i < 3; i++)
                {
                    int axis = i; string field = kind;
                    var value = new NumericUpDown { Name = "Game" + kind + i, Width = 71, DecimalPlaces = 1, Increment = 1, Minimum = kind == "Position" ? -1000 : -180, Maximum = kind == "Position" ? 1000 : 180 };
                    gameValues.Add(kind + i, value); row.Controls.Add(value);
                    value.ValueChanged += delegate {
                        if (updatingGame) return;
                        PushUndo(); var settings = Result.GameSettings(GameContext);
                        (field == "Position" ? settings.Position : settings.Rotation)[axis] = (float)value.Value;
                        if (GameContext == 1) SyncMenuPosture();
                        preview.AnimationPoints = preview.AnimationJoints = null;
                        if (play.Checked) RefreshAnimation(); ShowContactStatus(); preview.Invalidate();
                    };
                }
                transform.Controls.Add(row);
            }
            var sizeControls = CreateSizeControls();
            group.Controls.Add(sizeControls);
            group.Controls.SetChildIndex(sizeControls, 0);
            return group;
        }
        void SyncMenuPosture()
        {
            bool previous = updatingGame;
            updatingGame = true;
            stableMenu.Checked = Result.MenuPose.StableMenu;
            menuTilt[0].Value = (decimal)Result.MenuPose.Rotation[0];
            menuTilt[1].Value = (decimal)Result.MenuPose.Rotation[2];
            updatingGame = previous;
        }

        void UpdateMotionPreset()
        {
            bool previous = updatingGame;
            updatingGame = true;
            int index = Array.FindIndex(MotionPresets, v => v == motionStrength.Value);
            motionPreset.SelectedIndex = index < 0 ? MotionPresets.Length : index;
            updatingGame = previous;
        }

        void ChooseMenuTemplate()
        {
            if (references == null || GameContext != 1) return;
            play.Checked = false;
            var picker = new CharacterPickerForm(null, references.Root, true);
            StudioEditor.Open(this, picker, result => {
                if (result != DialogResult.OK) return;
                try
                {
                    var chosen = picker.Selected;
                    var loaded = ModelOperationForm.Run(this, L.T("Menüvorlage laden", "Load menu template"), token =>
                        RigPoseReference.Load(chosen.DriverPath, "model", "sel_wait", 1, true));
                    if (loaded == null) return;
                    UseMenuTemplate(chosen, loaded);
                }
                catch (Exception error) { status.Text = error.Message; }
            });
        }

        internal void UseMenuTemplate(CharacterVariant chosen, RigPoseReference reference)
        {
            if (GameContext != 1) throw new InvalidOperationException("Select Character selection first.");
            if (Result.MenuPose != null && Result.MenuPose.Animations != null)
                foreach (var track in Result.MenuPose.Animations.Where(t => t.Value.Count > 0))
                {
                    if (!reference.AvailableAnimations.Contains(track.Key))
                        throw new InvalidDataException(L.T("Die Vorlage enthält diese bearbeitete Bewegung nicht: ", "This template does not contain the edited motion: ") + track.Key);
                    var clip = RigPoseReference.Load(reference.Template, "model", track.Key, 1, false);
                    if (track.Value.Any(key => key.Frame >= clip.Frames))
                        throw new InvalidDataException(L.T("Die Vorlage ist kürzer als deine Schlüsselbilder. Zuerst die Schlüsselbilder anpassen: ",
                            "This template is shorter than your keyframes. Adjust the keyframes first: ") + track.Key);
                }
            play.Checked = false;
            PushUndo();
            try
            {
                Result.ApplyReferenceGamePose(1, reference, true);
                Result.MenuPose.MenuSourceCode = chosen.Character.Code;
                Result.MenuPose.MenuSourceSlot = chosen.Slot;
                Result.MenuPose.MenuSourceName = chosen.Name;
                Result.MenuPose.MotionStrength = Result.HumanAnimationStyle == null ? 25 : 100;
                if (references != null) references.RememberMenuReference(reference);
                animationFrames.Clear();
                RefreshGameMode();
                status.Text = L.T("Menühaltung und Bewegung übernommen. „Abspielen“ zeigt die Bewegung; Fahrhaltungen bleiben erhalten.",
                    "Menu pose and movement copied. Play previews the motion; driving poses are preserved.");
            }
            catch { RollbackHistory(); throw; }
        }

        void ResetMenuTemplate()
        {
            if (references == null || GameContext != 1) return;
            play.Checked = false;
            PushUndo();
            try
            {
                Result.MenuPose.MenuSourceCode = Result.MenuPose.MenuSourceName = null;
                Result.MenuPose.MenuSourceSlot = 0;
                Result.ApplyReferenceGamePose(1, references.Menu);
                animationFrames.Clear();
                RefreshGameMode();
            }
            catch (Exception error) { RollbackHistory(); status.Text = error.Message; }
        }

        void FitSelectedOriginalPose()
        {
            var selected = SelectedReference(referenceChoice.SelectedIndex == 1);
            if (selected == null) return;
            PushUndo();
            try
            {
                if (GameContext == 2) Result.FitNaturalRacePose(selected);
                else Result.ApplyReferenceGamePose(1, selected);
                if (GameContext == 1) { Result.MenuPose.StableMenu = true; Result.MenuPose.MotionStrength = Result.HumanAnimationStyle == null ? 25 : 100; }
                RefreshGameMode();
                ShowContactStatus();
            }
            catch (Exception error)
            {
                RollbackHistory();
                status.Text = error.Message;
            }
        }

        void OpenMenuVehicleReview()
        {
            if (references == null) return;
            List<CharacterMenuPose.References> sources = null;
            try {
                Result.RequireExportReview();
                var race = references.GetVehicle(VehicleKey, false);
                bool ready = ModelOperationForm.Run(this, L.T("Menüsitzposen laden", "Load menu seating"), token => {
                    sources = references.GetMenuVehicleReferences(Result, token);
                    return true;
                });
                if (!ready) return;
                using (var window = new ModelRigMenuReviewForm(Result, VehicleKey, race, sources, () => PushUndo())) window.ShowDialog(this);
                RefreshReview();
            }
            catch (Exception error) { status.Text = error.Message; }
            finally { if (sources != null) foreach (var source in sources) source.Dispose(); }
        }

        void RefreshGameMode()
        {
            if (referenceControls == null) return;
            bool game = mode.SelectedIndex == 4 || mode.SelectedIndex == 3;
            bool edit = mode.SelectedIndex == 4;
            string previousAnimation = AnimationName;
            int previousFrame = pose.Value;
            updatingGame = true;
            try
            {
                if (game && GameContext == 2 && references != null && !references.HasVehicleVisual(VehicleKey))
                {
                    var geometry = ModelOperationForm.Run(this, L.T("Fahrzeugvorschau laden", "Load vehicle preview"),
                        token => references.GetVehicleGeometry(VehicleKey, token));
                    if (geometry == null) return;
                }
                if (game && GameContext == 2 && ActiveReference != null)
                    Result.SelectVehiclePose(VehicleKey, ActiveReference);
                if (game) Result.InitializeGamePose(GameContext, ActiveReference);
                preview.GameContext = game && (edit || ActiveReference != null) ? GameContext : 0;
                preview.EditGameJoints = edit && editPoseJoints.Checked;
                preview.ShowBones = !edit || editPoseJoints.Checked;
                preview.AnimationPoints = null;
                preview.AnimationJoints = null;
                gameControls.Visible = edit;
                help.Visible = !edit;
                strengthControls.Visible = game;
                vehicleControls.Visible = game && GameContext == 2;
                preview.VehicleModel = game && GameContext == 2 && showVehicle.Checked && references != null
                    ? references.HasVehicleVisual(VehicleKey) ? references.GetVehicleVisual(VehicleKey, System.Threading.CancellationToken.None)
                    : ModelOperationForm.Run(this, L.T("Fahrzeugvorschau laden", "Load vehicle preview"),
                        token => references.GetVehicleVisual(VehicleKey, token)) : null;
                menuControls.Visible = game && GameContext == 1;
                menuPosture.Visible = game && GameContext == 1;
                if (game && GameContext == 1)
                {
                    SyncMenuPosture();
                    menuSourceLabel.Text = String.IsNullOrEmpty(Result.MenuPose.MenuSourceCode)
                        ? (references == null ? L.T("Gewählter Charakter", "Selected character") : references.VariantName)
                        : Result.MenuPose.MenuSourceName ?? Result.MenuPose.MenuSourceCode + "-" + Result.MenuPose.MenuSourceSlot;
                }
                if (references != null)
                    referenceChoice.Items[0] = GameContext == 1 && Result.MenuPose != null && !String.IsNullOrEmpty(Result.MenuPose.MenuSourceName)
                        ? Result.MenuPose.MenuSourceName : references.VariantName;
                gameAnimation.Visible = game && ActiveReference != null;
                foreach (Control control in gameControls.Parent.Controls)
                    if (Object.Equals(control.Tag, "SourceOnly") || Object.Equals(control.Tag, "GameHide")) control.Visible = !game;
                mode.Visible = !game;
                bone.Visible = !game || !edit || editPoseJoints.Checked;
                weights.Visible = !game;
                referenceControls.Visible = game;
                referencePose.Visible = !game && mode.SelectedIndex != 0;
                if (game)
                {
                    var settings = Result.GameSettings(GameContext);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        gameValues["Position" + axis].Value = (decimal)settings.Position[axis];
                        gameValues["Rotation" + axis].Value = (decimal)settings.Rotation[axis];
                    }
                    gameValues["Scale"].Value = (decimal)settings.Scale;
                    SyncSizeSlider();
                    humanStyle.Enabled = Result.HasHumanJoints;
                    humanStyle.SelectedIndex = Array.IndexOf(ModelRig.AnimationStyles, Result.HumanAnimationStyle);
                    motionStrength.Value = (decimal)settings.MotionStrength;
                    UpdateMotionPreset();
                    help.Text = edit
                        ? L.T("Vorlage wählen, Bewegung abspielen. „Gelenke bearbeiten“ blendet die Korrekturpunkte ein.", "Choose a template and play the movement. Edit joints shows the correction points.")
                        : L.T("Bewegung und Zeitpunkt wählen. Körperteil anklicken und mit Schlüsselbildern bearbeiten. Rechts ziehen dreht die Ansicht.", "Choose a motion and frame. Click a body part and edit it with keyframes. Right-drag rotates the view.");
                    if (GameContext == 2 && showVehicle.Checked)
                        help.Text += L.T(" Fahrzeug: feste Sitzhilfe; die Spielneigung wird nicht simuliert.",
                            " Vehicle: fixed seating guide; in-game tilt is not simulated.");
                    if (ActiveReference != null)
                    {
                        gameAnimation.Items.Clear();
                        foreach (string name in ActiveReference.AvailableAnimations ?? new[] { ActiveReference.Animation }) gameAnimation.Items.Add(new AnimationChoice { Name = name });
                        string selectedAnimation = ActiveReference.AvailableAnimations.Contains(previousAnimation) ? previousAnimation : ActiveReference.Animation;
                        for (int i = 0; i < gameAnimation.Items.Count; i++) if (((AnimationChoice)gameAnimation.Items[i]).Name == selectedAnimation) gameAnimation.SelectedIndex = i;
                        var clip = selectedAnimation == ActiveReference.Animation ? ActiveReference : RigPoseReference.Load(ActiveReference.Template, "model", selectedAnimation, 1, false);
                        pose.Minimum = 0; pose.Maximum = clip.Frames - 1;
                        pose.TickFrequency = Math.Max(1, clip.Frames / 5);
                        pose.Value = Math.Max(0, Math.Min(pose.Maximum, previousAnimation == selectedAnimation ? previousFrame : (int)clip.Frame - 1));
                    }
                    status.Text = edit ? L.T("Spielhaltung: Gelenke ziehen oder ganze Figur verschieben, drehen und skalieren.", "Game pose: drag joints or move, rotate and scale the whole figure.") : help.Text;
                }
                else
                {
                    pose.Minimum = -90; pose.Maximum = 90; pose.Value = 0;
                }
            }
            finally { updatingGame = false; }
            RefreshReference();
            RefreshKeyEditor();
            if (game && !edit) RefreshAnimation();
            if (game && edit) ShowContactStatus();
            preview.Invalidate();
        }
        void RefreshReference()
        {
            var selected = SelectedReference(referenceChoice.SelectedIndex == 1);
            fitOriginalPose.Enabled = selected != null && Result.HasHumanJoints;
            fitOriginalPose.Text = GameContext == 1 ? L.T("Aufrecht & entspannt", "Upright & relaxed") : L.T("Natürliche Rennposition", "Natural racing pose");
            preview.ReferenceModel = showReference.Checked && preview.GameContext > 0 && selected != null ? selected.Visual : null;
            if (showReference.Checked && selected == null)
                status.Text = L.T("Diese Referenz fehlt. „Basis laden“ öffnet deine ISO/WBFS oder die passende Originaldatei. Das Modell dient nur als Hilfe und wird nicht exportiert.", "This reference is missing. Load base opens your ISO/WBFS or matching original file. This model is only a guide and is never exported.");
            preview.Invalidate();
        }
        void ShowContactStatus()
        {
            RefreshReview();
            if (!Result.HasHumanJoints)
            {
                status.Text = L.T("Originalbewegungen: Diese Vorlage hat kein vollständiges menschliches Skelett. Für natürliche Arme/Beine im Character Builder eine menschliche Bewegungsquelle wählen.",
                    "Original movement: this template has no complete human skeleton. For natural arms/legs, choose a human movement source in Character Builder.");
                return;
            }
            if (Result.GameSettings(GameContext) == null || !Result.GameSettings(GameContext).NaturalHuman)
            {
                status.Text = GameContext == 1
                    ? L.T("Gespeicherte Haltung beibehalten. „Aufrecht & entspannt“ berechnet eine neue Ausgangshaltung.", "Saved pose preserved. Upright & relaxed calculates a new starting pose.")
                    : L.T("Gespeicherte Haltung beibehalten. „Natürliche Rennposition“ berechnet Sitz, Arm-/Beinbeugung und passende Fahrgröße neu. Menügröße bleibt erhalten.", "Saved pose preserved. Natural racing pose recalculates seating, arm/leg bends and riding size. Menu size stays the same.");
                return;
            }
            var missed = Result.MissedContacts(GameContext);
            status.Text = missed.Length == 0
                ? L.T("Größe und Proportionen erhalten. Haltung und Bewegung prüfen; manuelle Korrekturen bleiben möglich.", "Size and proportions preserved. Review pose and movement; manual correction remains available.")
                : L.T("Orange: Kontakt nicht erreichbar — ", "Orange: contact out of reach — ")
                    + String.Join(", ", missed.Select(c => BoneLabel(c.Joint)))
                    + L.T(". Größe bleibt erhalten; Haltung bei Bedarf korrigieren.", ". Size is preserved; correct the pose as needed.");
        }
        void ChangeAnimation()
        {
            if (updatingGame || ActiveReference == null || !(gameAnimation.SelectedItem is AnimationChoice)) return;
            play.Checked = false;
            var frame = RigPoseReference.Load(ActiveReference.Template, "model", ((AnimationChoice)gameAnimation.SelectedItem).Name, 1, false);
            pose.Minimum = 0; pose.Maximum = frame.Frames - 1; pose.TickFrequency = Math.Max(1, frame.Frames / 5); pose.Value = 0;
            if (mode.SelectedIndex == 3 || play.Checked) RefreshAnimation();
            else RefreshGameMode();
            preview.Invalidate();
        }
        void RefreshAnimation()
        {
            if (updatingGame || ActiveReference == null) return;
            try
            {
                string animation = gameAnimation.SelectedItem is AnimationChoice ? ((AnimationChoice)gameAnimation.SelectedItem).Name : ActiveReference.Animation;
                string key = ActiveReference.Template + ":" + GameContext + ":" + VehicleKey + ":" + animation + ":" + pose.Value;
                RigPoseReference frame;
                if (!animationFrames.TryGetValue(key, out frame))
                {
                    frame = RigPoseReference.Load(ActiveReference.Template, "model", animation, pose.Value + 1, false);
                    if (animationFrames.Count >= 240) animationFrames.Clear();
                    animationFrames[key] = frame;
                }
                preview.EditGameJoints = false;
                preview.AnimationPoints = Result.GameAnimationPreview(GameContext, ActiveReference, frame);
                var adjusted = ActiveReference.MotionAnchor(Result, GameContext, frame.Animation).Adjusted(Result, GameContext);
                var moving = frame.ApplyCorrections(adjusted);
                preview.AnimationJoints = Result.Bones.Select((b, index) => {
                    int match = moving.MatchBone(Result, index);
                    return match < 0 ? Result.GameJoints(GameContext)[index] : moving.Joints[match];
                }).ToArray();
                RefreshKeyEditor();
                preview.Invalidate();
            }
            catch (Exception error)
            {
                timer.Stop(); status.Text = error.Message; preview.AnimationPoints = preview.AnimationJoints = null;
            }
        }
        void LoadBaseReference()
        {
            if (references == null) { status.Text = L.T("Referenzen im Character Builder öffnen.", "Open references from the Character Builder."); return; }
            using (var dialog = new OpenFileDialog { Title = L.T("Unveränderten Basischarakter laden", "Load unchanged base character"), Filter = "ISO / WBFS / SZS / BRRES|*.iso;*.wbfs;*.szs;*.brres" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var loaded = ModelOperationForm.Run(this, L.T("Basisreferenz laden", "Load base reference"), token => {
                        references.ImportBase(dialog.FileName, GameContext, referenceWeight, VehicleKey); return references;
                    });
                    if (loaded == null) return;
                    referenceChoice.SelectedIndex = 1; showReference.Checked = true; RefreshReference();
                }
                catch (Exception error) { status.Text = error.Message; }
            }
        }
    }
}
