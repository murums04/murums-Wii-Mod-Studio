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
            return GameContext == 1 ? references.Get(1, basis) : references.GetVehicle(VehicleKey, basis);
        }
        FlowLayoutPanel referenceControls;
        readonly ComboBox gameAnimation = new ComboBox { Name = "GameAnimation", DropDownStyle = ComboBoxStyle.DropDownList, Width = 192 };
        readonly NumericUpDown motionStrength = new NumericUpDown { Name = "MotionStrength", Width = 60, Minimum = 0, Maximum = 100, Value = 100 };
        FlowLayoutPanel strengthControls;
        bool updatingGame;
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
                    case "wait": return L.T("Warten", "Idle");
                    case "back": return L.T("Zurückschauen", "Look back");
                    case "drift_l": return L.T("Links driften", "Drift left");
                    case "drift_r": return L.T("Rechts driften", "Drift right");
                    case "wheelie": return "Wheelie";
                    case "jump": return L.T("Sprung", "Jump");
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
            referenceControls = new FlowLayoutPanel { Name = "GameReferenceControls", AutoSize = true, WrapContents = true, MaximumSize = new Size(790, 0) };
            gameTarget.Items.AddRange(new object[] { L.T("Charakterauswahl", "Character selection"), L.T("Fahren", "Driving") });
            gameTarget.SelectedIndex = 0;
            referenceChoice.Items.AddRange(new object[] {
                references == null ? L.T("RR-Variante", "RR variant") : references.VariantName,
                references == null ? L.T("Basischarakter", "Base character") : references.BaseName
            });
            referenceChoice.SelectedIndex = 0;
            var load = new Button { Name = "LoadBaseReference", Text = L.T("Basis laden…", "Load base…"), AutoSize = true };
            load.Click += delegate { LoadBaseReference(); };
            referenceControls.Controls.Add(gameTarget);
            referenceControls.Controls.Add(showReference);
            referenceControls.Controls.Add(referenceChoice);
            referenceControls.Controls.Add(load);
            vehicleControls = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 232 };
            vehicleControls.Controls.Add(new Label { Text = L.T("Fahrzeug · eigene Haltung", "Vehicle · separate pose"), AutoSize = true });
            foreach (string key in references == null || references.Weight == null ? new[] { referenceWeight + "a_bike" } : references.Vehicles)
                gameVehicle.Items.Add(new VehicleChoice { Key = key });
            gameVehicle.SelectedIndex = 0;
            vehicleControls.Controls.Add(gameVehicle);
            referenceControls.Controls.Add(vehicleControls);
            gameVehicle.SelectedIndexChanged += delegate {
                if (updatingGame) return;
                play.Checked = false;
                try { RefreshGameMode(); }
                catch (Exception error) { status.Text = error.Message; }
            };
            strengthControls = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            strengthControls.Controls.Add(new Label { Text = L.T("Bewegungsstärke %", "Movement strength %"), AutoSize = true, Margin = new Padding(3, 5, 3, 3) });
            strengthControls.Controls.Add(motionStrength);
            gameControls.Parent.Controls.Add(strengthControls);
            gameControls.Parent.Controls.SetChildIndex(strengthControls, gameControls.Parent.Controls.IndexOf(gameControls));
            new ToolTip().SetToolTip(motionStrength, L.T("0 %: gewählte Haltung bleibt stehen. 100 %: kräftige RR-Bewegung. Natürliche Haltungen begrenzen starke Verdrehungen. Für Menü und Fahren getrennt gespeichert.", "0%: holds your chosen pose. 100%: strong RR movement. Natural poses limit large twists. Saved separately for menu and driving."));
            motionStrength.ValueChanged += delegate {
                if (updatingGame) return;
                PushUndo(); Result.GameSettings(GameContext).MotionStrength = (float)motionStrength.Value;
                if (mode.SelectedIndex == 3) RefreshAnimation();
                preview.Invalidate();
            };
            gameTarget.SelectedIndexChanged += delegate { play.Checked = false; RefreshGameMode(); };
            showReference.CheckedChanged += delegate { RefreshReference(); };
            referenceChoice.SelectedIndexChanged += delegate { RefreshReference(); };
            return referenceControls;
        }
        Control CreateGameControls()
        {
            var group = new FlowLayoutPanel { Name = "GamePoseTransform", FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 232, Visible = false };
            foreach (string kind in new[] { "Position", "Rotation" })
            {
                group.Controls.Add(new Label { Text = kind == "Position" ? L.T("Position · X Seite, Y Höhe, Z Tiefe", "Position · X side, Y height, Z depth") : L.T("Drehung X / Y / Z (Grad)", "Rotation X / Y / Z (degrees)"), AutoSize = true, MaximumSize = new Size(232, 0) });
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
                        preview.AnimationPoints = null; ShowContactStatus(); preview.Invalidate();
                    };
                }
                group.Controls.Add(row);
            }
            var sizeRow = new FlowLayoutPanel { AutoSize = true, Width = 232 };
            sizeRow.Controls.Add(new Label { Text = L.T("Größe %", "Size %"), AutoSize = true, Margin = new Padding(3, 5, 3, 3) });
            var scale = new NumericUpDown { Name = "GameScale", Width = 92, Minimum = 1, Maximum = 500, Value = 100, DecimalPlaces = 1 };
            gameValues.Add("Scale", scale); sizeRow.Controls.Add(scale); group.Controls.Add(sizeRow);
            scale.ValueChanged += delegate {
                if (updatingGame) return;
                PushUndo(); Result.GameSettings(GameContext).Scale = (float)scale.Value;
                preview.AnimationPoints = null; ShowContactStatus(); preview.Invalidate();
            };
            fitOriginalPose = AddButton(group, L.T("Natürlich anpassen", "Fit natural pose"), FitSelectedOriginalPose);
            fitOriginalPose.Name = "FitOriginalPose";
            return group;
        }
        void FitSelectedOriginalPose()
        {
            var selected = SelectedReference(referenceChoice.SelectedIndex == 1);
            if (selected == null) return;
            PushUndo();
            try
            {
                Result.ApplyReferenceGamePose(GameContext, selected);
                RefreshGameMode();
                ShowContactStatus();
            }
            catch (Exception error)
            {
                Undo();
                status.Text = error.Message;
            }
        }

        void RefreshGameMode()
        {
            if (referenceControls == null) return;
            bool game = mode.SelectedIndex == 4 || mode.SelectedIndex == 3;
            bool edit = mode.SelectedIndex == 4;
            updatingGame = true;
            try
            {
                if (game && GameContext == 2 && ActiveReference != null)
                    Result.SelectVehiclePose(VehicleKey, ActiveReference);
                if (game) Result.InitializeGamePose(GameContext, ActiveReference);
                preview.GameContext = game && (edit || ActiveReference != null) ? GameContext : 0;
                preview.EditGameJoints = edit;
                preview.AnimationPoints = null;
                gameControls.Visible = edit;
                strengthControls.Visible = game;
                vehicleControls.Visible = game && GameContext == 2;
                gameAnimation.Visible = game && ActiveReference != null;
                foreach (Control control in gameControls.Parent.Controls)
                    if (Object.Equals(control.Tag, "SourceOnly") || Object.Equals(control.Tag, "GameHide")) control.Visible = !game;
                mode.Visible = !game;
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
                    motionStrength.Value = (decimal)settings.MotionStrength;
                    help.Text = edit
                        ? L.T("Die Ausgangshaltung wird automatisch berechnet. Menü oder Fahrzeug wählen und prüfen. Nur bei Bedarf Gelenke verschieben; Größe bleibt erhalten.", "The starting pose is fitted automatically. Choose menu or vehicle and review it. Move joints only if needed; size is preserved.")
                        : L.T("Echte Animation der ersetzten RR-Variante. Abspielen oder den Regler ziehen. Danach auch im Spiel prüfen; andere Fahrzeuge und Bewegungen können abweichen.", "Actual animation of the replaced RR variant. Play it or drag the slider. Check in game too; other vehicles and motions can differ.");
                    if (ActiveReference != null)
                    {
                        gameAnimation.Items.Clear();
                        foreach (string name in ActiveReference.AvailableAnimations ?? new[] { ActiveReference.Animation }) gameAnimation.Items.Add(new AnimationChoice { Name = name });
                        for (int i = 0; i < gameAnimation.Items.Count; i++) if (((AnimationChoice)gameAnimation.Items[i]).Name == ActiveReference.Animation) gameAnimation.SelectedIndex = i;
                        pose.Minimum = 0; pose.Maximum = ActiveReference.Frames - 1;
                        pose.TickFrequency = Math.Max(1, ActiveReference.Frames / 5);
                        pose.Value = (int)ActiveReference.Frame - 1;
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
            if (game && edit) ShowContactStatus();
            preview.Invalidate();
        }
        void RefreshReference()
        {
            var selected = SelectedReference(referenceChoice.SelectedIndex == 1);
            fitOriginalPose.Enabled = selected != null;
            preview.ReferenceModel = showReference.Checked && preview.GameContext > 0 && selected != null ? selected.Visual : null;
            if (showReference.Checked && selected == null)
                status.Text = L.T("Diese Referenz fehlt. „Basis laden“ öffnet deine ISO/WBFS oder die passende Originaldatei. Das Modell dient nur als Hilfe und wird nicht exportiert.", "This reference is missing. Load base opens your ISO/WBFS or matching original file. This model is only a guide and is never exported.");
            preview.Invalidate();
        }
        void ShowContactStatus()
        {
            if (Result.GameSettings(GameContext) == null || !Result.GameSettings(GameContext).NaturalHuman)
            {
                status.Text = L.T("Gespeicherte Haltung beibehalten. „Natürlich anpassen“ berechnet eine neue Ausgangshaltung.", "Saved pose preserved. Fit natural pose calculates a new starting pose.");
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
            RefreshAnimation(); preview.Invalidate();
        }
        void RefreshAnimation()
        {
            if (updatingGame || ActiveReference == null) return;
            try
            {
                string animation = gameAnimation.SelectedItem is AnimationChoice ? ((AnimationChoice)gameAnimation.SelectedItem).Name : ActiveReference.Animation;
                string key = GameContext + ":" + VehicleKey + ":" + animation + ":" + pose.Value;
                RigPoseReference frame;
                if (!animationFrames.TryGetValue(key, out frame))
                {
                    frame = RigPoseReference.Load(ActiveReference.Template, "model", animation, pose.Value + 1, false);
                    animationFrames[key] = frame;
                }
                preview.AnimationPoints = Result.GameAnimationPreview(GameContext, ActiveReference, frame);
            }
            catch (Exception error)
            {
                timer.Stop(); status.Text = error.Message; preview.AnimationPoints = null;
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
