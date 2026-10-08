using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        readonly TrackBar characterSize = new TrackBar { Name = "CharacterSizeSlider", Minimum = 1, Maximum = 500, Value = 100, SmallChange = 1, LargeChange = 5, TickStyle = TickStyle.None, Width = 228, Height = 30, AutoSize = false };
        readonly Timer sizeTimer = new Timer { Interval = 80 };
        bool draggingSize, sizeUndoSaved;
        Label sizeHint;
        Button applySizeToVehicles;

        Control CreateSizeControls()
        {
            var group = new FlowLayoutPanel { Name = "CharacterSizeControls", AutoSize = true, Width = 232, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            RigSection(group, L.T("Größe", "Size"));
            var row = new FlowLayoutPanel { AutoSize = true, Width = 232, WrapContents = false };
            row.Controls.Add(new Label { Text = L.T("Charaktergröße %", "Character size %"), Width = 136, Height = 26, TextAlign = ContentAlignment.MiddleLeft });
            var scale = new NumericUpDown { Name = "GameScale", Width = 75, Minimum = 1, Maximum = 500, Value = 100, DecimalPlaces = 1 };
            gameValues.Add("Scale", scale);
            row.Controls.Add(scale);
            group.Controls.Add(row);
            group.Controls.Add(characterSize);
            applySizeToVehicles = AddButton(group, L.T("Auf alle Fahrzeuge übernehmen", "Apply to all vehicles"), ApplySizeToAllVehicles);
            applySizeToVehicles.Name = "ApplySizeToAllVehicles";
            sizeHint = new Label { AutoSize = true, MaximumSize = new Size(232, 0), ForeColor = DarkTheme.Muted };
            group.Controls.Add(sizeHint);
            characterSize.MouseDown += delegate { draggingSize = true; sizeUndoSaved = false; play.Checked = false; };
            characterSize.MouseUp += delegate { ApplySizeSlider(); draggingSize = false; sizeUndoSaved = false; };
            characterSize.ValueChanged += delegate {
                if (updatingGame) return;
                if (!draggingSize) ApplySizeSlider();
                // Laufenden Takt erhalten, damit auch durchgehendes Ziehen sichtbar aktualisiert.
                else if (!sizeTimer.Enabled) sizeTimer.Start();
            };
            sizeTimer.Tick += delegate { ApplySizeSlider(); };
            scale.ValueChanged += delegate {
                if (updatingGame) return;
                sizeTimer.Stop();
                ChangeCharacterSize((float)scale.Value);
            };
            Disposed += delegate { sizeTimer.Dispose(); };
            return group;
        }

        void ApplySizeSlider()
        {
            sizeTimer.Stop();
            if (!updatingGame) ChangeCharacterSize(characterSize.Value);
        }

        void ChangeCharacterSize(float percent)
        {
            var settings = Result.GameSettings(GameContext);
            if (settings == null || Math.Abs(settings.Scale - percent) < .001f) return;
            play.Checked = false;
            bool saved = false;
            try
            {
                if (!draggingSize || !sizeUndoSaved) { PushUndo(); sizeUndoSaved = saved = true; }
                Result.ResizeGamePose(GameContext, ActiveReference, percent);
                showVehicle.Checked = true;
                RefreshGameMode();
                ShowContactStatus();
            }
            catch (Exception error)
            {
                if (saved) RollbackHistory();
                RefreshGameMode();
                status.Text = error.Message;
            }
            finally { if (!draggingSize) sizeUndoSaved = false; }
        }

        void ApplySizeToAllVehicles()
        {
            if (GameContext != 2 || references == null) return;
            sizeTimer.Stop();
            play.Checked = false;
            float percent = (float)gameValues["Scale"].Value;
            try
            {
                var resized = ModelOperationForm.Run(this, L.T("Größe auf alle Fahrzeuge übernehmen", "Apply size to all vehicles"),
                    token => Result.WithVehicleSizes(references, percent, token));
                if (resized == null) return;
                PushUndo();
                Result.VehiclePoses = resized.VehiclePoses;
                Result.NaturalVehicleFitting = resized.NaturalVehicleFitting;
                showVehicle.Checked = true;
                RefreshGameMode();
                status.Text = String.Format(L.T("{0:0.#} % auf alle {1} Fahrzeuge übernommen · Sitz pro Fahrzeug angepasst.",
                    "{0:0.#}% applied to all {1} vehicles · Seating refitted for each vehicle."), percent, references.Vehicles.Count());
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        void SyncSizeSlider()
        {
            sizeTimer.Stop();
            characterSize.Value = Math.Max(characterSize.Minimum, Math.Min(characterSize.Maximum, (int)Math.Round(Result.GameSettings(GameContext).Scale)));
            applySizeToVehicles.Visible = GameContext == 2;
            applySizeToVehicles.Enabled = references != null;
            sizeHint.Text = GameContext == 2
                ? L.T("Regler: aktuelles Fahrzeug · Proportionen bleiben erhalten", "Slider: current vehicle · Preserve proportions")
                : L.T("Nur Charakterauswahl · Gleichmäßig skalieren", "Character selection only · Uniform scale");
        }
    }
}
