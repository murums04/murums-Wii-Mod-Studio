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
                InitializeGamePose(2, reference, true);
        }

        internal ModelRig WithVehicleSizes(RigReferenceSet references, float percent, System.Threading.CancellationToken cancellation)
        {
            if (references == null) throw new ArgumentNullException("references");
            if (Single.IsNaN(percent) || Single.IsInfinity(percent) || percent < 1 || percent > 500)
                throw new ArgumentOutOfRangeException("percent");
            // Erst nach allen erfolgreichen Anpassungen uebernimmt die Oberflaeche diese Kopie.
            var copy = (ModelRig)MemberwiseClone();
            copy.VehiclePoses = VehiclePoses == null ? new Dictionary<string, GamePoseSettings>()
                : new Dictionary<string, GamePoseSettings>(VehiclePoses);
            foreach (string key in references.Vehicles)
            {
                cancellation.ThrowIfCancellationRequested();
                var reference = references.GetVehicle(key, false);
                if (reference == null) throw new InvalidOperationException(L.T("Fahrzeugvorlage fehlt: ", "Vehicle reference missing: ") + CharacterVehicleNames.ShortLabel(key));
                copy.SelectVehiclePose(key, reference);
                copy.ResizeGamePose(2, reference, percent);
            }
            cancellation.ThrowIfCancellationRequested();
            copy.ActiveVehicle = ActiveVehicle;
            return copy;
        }

        internal sealed class MenuVehicleReview
        {
            internal string Key, Fingerprint, SourceFingerprint, DerivedFingerprint;
            internal ModelRig Posed;
            internal RigPoseReference Race;
            internal SurfaceReport Report, BaseReport;
            internal bool RequiresReview;
        }

        internal static string MenuReviewKey(string vehicle, string clip, bool battle)
        {
            string key = vehicle + "/" + (battle ? "battle" : "race") + "/" + clip;
            if (!ValidMenuReviewKey(key)) throw new System.IO.InvalidDataException("Invalid menu pose context.");
            return key;
        }

        static bool ValidMenuReviewKey(string key)
        {
            if (key == null || key.Length > 112) return false;
            var parts = key.Split('/');
            if (parts.Length != 3 || !ValidVehicleKey(parts[0]) || parts[1] != "race" && parts[1] != "battle"
                || !System.Text.RegularExpressions.Regex.IsMatch(parts[2], @"^[A-Za-z0-9_]+-[A-Za-z0-9_-]+$")) return false;
            string prefix = parts[2].Split('-')[0];
            return prefix == parts[0] || prefix == "kart" && parts[0].Substring(1) == "df_kart";
        }

        internal MenuVehicleReview PreviewMenuVehicle(string key, RigPoseReference raceReference, RigPoseReference menuReference,
            string context, string referenceFingerprint)
        {
            if (!ValidMenuReviewKey(context) || !context.StartsWith(key + "/", StringComparison.Ordinal)
                || menuReference == null || context.Split('/')[2] != menuReference.Animation
                || referenceFingerprint == null || referenceFingerprint.Length != 64 || referenceFingerprint.Any(c => !Uri.IsHexDigit(c)))
                throw new System.IO.InvalidDataException("Invalid menu pose reference.");
            var copy = ForVehicle(key, raceReference);
            string sourceFingerprint = copy.SurfaceFingerprint();
            var saved = copy.GameSettings(2);
            var reviewed = copy.CheckSurface();
            if (saved.NaturalHuman) copy.DeriveMenuVehicle(saved, raceReference, menuReference);
            var report = copy.CheckSurface();
            string fingerprint;
            using (var hash = System.Security.Cryptography.SHA256.Create())
                fingerprint = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                    "Menu surface review 1\n" + context + "\n" + referenceFingerprint + "\n" + sourceFingerprint + "\n" + report.Fingerprint))).Replace("-", "");
            return new MenuVehicleReview { Key = context, Fingerprint = fingerprint, SourceFingerprint = sourceFingerprint,
                DerivedFingerprint = report.Fingerprint, Posed = copy, Race = raceReference, Report = report, BaseReport = reviewed,
                RequiresReview = AdditionalUrgentIssues(report, reviewed) };
        }

        void DeriveMenuVehicle(GamePoseSettings saved, RigPoseReference raceReference, RigPoseReference menuReference)
        {
            // Gemeinsame Menueanimationen haben eigene Kontaktpunkte; manuelle Korrekturen bleiben relativ erhalten.
            ApplyReferenceGamePose(2, raceReference);
            var race = GameSettings(2);
            menuReference.VehicleGeometry = raceReference.VehicleGeometry == null ? null : raceReference.VehicleGeometry.ForMenu();
            ApplyReferenceGamePose(2, menuReference);
            var menu = GameSettings(2);
            for (int bone = 0; bone < Bones.Length; bone++)
                menu.Joints[bone] = RigVector.Add(menu.Joints[bone], RigVector.Sub(saved.Joints[bone], race.Joints[bone]));
            menu.Position = (float[])saved.Position.Clone();
            menu.Rotation = (float[])saved.Rotation.Clone();
        }

        internal bool MenuReviewCurrent(MenuVehicleReview review)
        {
            string saved;
            if (review == null || MenuSurfaceReviews == null || !MenuSurfaceReviews.TryGetValue(review.Key, out saved) || saved != review.Fingerprint) return false;
            var current = (ModelRig)MemberwiseClone(); current.ActiveVehicle = review.Posed.ActiveVehicle;
            return current.SurfaceFingerprint() == review.SourceFingerprint && review.Posed.SurfaceFingerprint() == review.DerivedFingerprint;
        }

        internal void ConfirmMenuVehicleReview(MenuVehicleReview review)
        {
            if (review == null || !ValidMenuReviewKey(review.Key)
                || ForVehicle(review.Posed.ActiveVehicle, review.Race).SurfaceFingerprint() != review.SourceFingerprint
                || review.Posed.SurfaceFingerprint() != review.DerivedFingerprint)
                throw new System.IO.InvalidDataException(L.T("Die Haltung wurde geändert. Vorschau erneut öffnen und prüfen.", "The pose changed. Reopen and review the preview."));
            MenuSurfaceReviews = MenuSurfaceReviews == null ? new Dictionary<string, string>() : new Dictionary<string, string>(MenuSurfaceReviews);
            MenuSurfaceReviews[review.Key] = review.Fingerprint;
        }

        internal ModelRig ForMenuVehicle(string key, RigPoseReference raceReference, RigPoseReference menuReference,
            string context = null, string referenceFingerprint = null)
        {
            context = context ?? MenuReviewKey(key, menuReference.Animation, false);
            referenceFingerprint = referenceFingerprint ?? CharacterMenuPose.ReferenceFingerprint(menuReference.Template);
            var review = PreviewMenuVehicle(key, raceReference, menuReference, context, referenceFingerprint);
            if (review.RequiresReview && !MenuReviewCurrent(review))
                throw new System.IO.InvalidDataException(L.T("Die Menüsitzpose hat zusätzliche Problemstellen. Im Bewegungseditor „Menüsitzposen prüfen…“ öffnen und diese Pose kontrollieren: ",
                    "The menu seating pose has additional problem areas. Open ‘Review menu seating…’ in the movement editor and inspect this pose: ") + context);
            if (!review.RequiresReview) review.Posed.CheckDerivedVehiclePose(review.BaseReport);
            else { review.Posed.ComponentsReviewed = true; review.Posed.SurfaceReviewFingerprint = review.Report.Fingerprint; }
            return review.Posed;
        }

        static bool AdditionalUrgentIssues(SurfaceReport report, SurfaceReport reviewed)
        {
            var accepted = reviewed.Issues.Where(issue => issue.Urgent)
                .GroupBy(issue => issue.Reason + "\n" + issue.Pose)
                .ToDictionary(group => group.Key, group => new HashSet<int>(group.SelectMany(issue => issue.Vertices)));
            return report.Issues.Any(issue => issue.Urgent && (!accepted.ContainsKey(issue.Reason + "\n" + issue.Pose)
                || issue.Vertices.Any(vertex => !accepted[issue.Reason + "\n" + issue.Pose].Contains(vertex))));
        }

        void CheckDerivedVehiclePose(SurfaceReport reviewed)
        {
            var report = CheckSurface();
            if (SurfaceReviewCurrent) return;
            if (AdditionalUrgentIssues(report, reviewed))
                throw new System.IO.InvalidDataException(L.T("Die abgeleitete Fahrzeughaltung verformt ein starres Teil oder bewegt Flächen zu weit. Dieses Fahrzeug im Bewegungseditor prüfen.", "The derived vehicle pose deforms a rigid part or moves a surface too far. Review this vehicle in the movement editor."));
            // Nur intern abgeleitete Fahrzeughaltungen uebernehmen die bereits gepruefte Flaechenzuordnung.
            ComponentsReviewed = true;
            SurfaceReviewFingerprint = report.Fingerprint;
        }

        internal ModelRig ForVehicle(string key, RigPoseReference reference)
        {
            RequireExportReview();
            var reviewed = CheckSurface();
            var copy = (ModelRig)MemberwiseClone();
            copy.VehiclePoses = VehiclePoses == null ? new Dictionary<string, GamePoseSettings>() : new Dictionary<string, GamePoseSettings>(VehiclePoses);
            copy.SelectVehiclePose(key, reference);
            copy.CheckDerivedVehiclePose(reviewed);
            return copy;
        }
    }
}
