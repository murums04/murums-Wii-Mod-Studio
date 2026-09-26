using System;
using System.Collections.Generic;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public bool NaturalVehicleFitting;

        internal static bool ValidVehicleKey(string value)
        {
            return value != null && System.Text.RegularExpressions.Regex.IsMatch(value, @"^[sml](?:[a-e]|df)_(?:bike|kart)$");
        }

        internal void SelectVehiclePose(string key, RigPoseReference reference)
        {
            if (!ValidVehicleKey(key)) throw new ArgumentException("Invalid vehicle.");
            ActiveVehicle = key;
            if (VehiclePoses == null) VehiclePoses = new Dictionary<string, GamePoseSettings>();
            if (VehiclePoses.ContainsKey(key)) return;
            if (RacePose != null && !RacePose.NaturalHuman && !NaturalVehicleFitting)
                VehiclePoses[key] = Serializer().Deserialize<GamePoseSettings>(Serializer().Serialize(RacePose));
            else
                ApplyReferenceGamePose(2, reference);
        }

        internal ModelRig ForMenuVehicle(string key, RigPoseReference raceReference, RigPoseReference menuReference)
        {
            var copy = ForVehicle(key, raceReference);
            var saved = copy.GameSettings(2);
            if (!saved.NaturalHuman) return copy;
            // Gemeinsame Menueanimationen haben eigene Kontaktpunkte; manuelle Korrekturen bleiben relativ erhalten.
            copy.ApplyReferenceGamePose(2, raceReference);
            var race = copy.GameSettings(2);
            copy.ApplyReferenceGamePose(2, menuReference);
            var menu = copy.GameSettings(2);
            for (int bone = 0; bone < Bones.Length; bone++)
                menu.Joints[bone] = RigVector.Add(menu.Joints[bone], RigVector.Sub(saved.Joints[bone], race.Joints[bone]));
            menu.Position = (float[])saved.Position.Clone();
            menu.Rotation = (float[])saved.Rotation.Clone();
            return copy;
        }

        internal ModelRig ForVehicle(string key, RigPoseReference reference)
        {
            var copy = (ModelRig)MemberwiseClone();
            copy.VehiclePoses = VehiclePoses == null ? new Dictionary<string, GamePoseSettings>() : new Dictionary<string, GamePoseSettings>(VehiclePoses);
            copy.SelectVehiclePose(key, reference);
            return copy;
        }
    }
}
