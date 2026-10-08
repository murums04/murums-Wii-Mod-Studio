using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal sealed class CharacterMotionFinding
    {
        internal string Vehicle, Animation, Detail;
        internal int FirstFrame, LastFrame, Count;
        internal bool Error;
    }

    internal sealed class CharacterMotionReport
    {
        internal int Files, Vehicles, Clips, Frames, ContactClips;
        internal readonly List<CharacterMotionFinding> Findings = new List<CharacterMotionFinding>();
        internal bool HasErrors { get { return Findings.Any(f => f.Error); } }
        internal void Add(string vehicle, string clip, string detail, int frame, bool error = false)
        {
            var finding = Findings.FirstOrDefault(f => f.Vehicle == vehicle && f.Animation == clip && f.Detail == detail);
            if (finding == null)
            {
                finding = new CharacterMotionFinding { Vehicle = vehicle, Animation = clip, Detail = detail, FirstFrame = frame, Error = error };
                Findings.Add(finding);
            }
            finding.LastFrame = frame;
            finding.Count++;
        }
        internal string Text()
        {
            var lines = new List<string> {
                L.T("Bewegungscheck: ", "Movement check: ") + Files + L.T(" Dateien, ", " files, ") + Clips
                    + L.T(" Animationen, ", " animations, ") + Frames + L.T(" Frames geprüft.", " frames checked."),
                ContactClips + L.T(" Fahr-/Wheelie-Animationen auf Kontakte und Armhaltung geprüft.", " driving/wheelie animations checked for contacts and arm posture."),
                L.T("Prüfung gespeicherter Gelenkbewegungen; keine Kollisions- oder Ingame-Freigabe.",
                    "Checks stored joint motion; does not verify collisions or in-game appearance.")
            };
            if (Findings.Count == 0)
                lines.Add(L.T("Keine Auffälligkeiten in den ausgeführten Prüfungen.", "No findings in the checks performed."));
            foreach (var finding in Findings.Take(60))
                lines.Add((finding.Error ? L.T("Fehler: ", "Error: ") : L.T("Prüfen: ", "Review: ")) + finding.Vehicle + " · " + finding.Animation
                    + (finding.FirstFrame > 0 ? " · Frame " + finding.FirstFrame + (finding.LastFrame > finding.FirstFrame ? "–" + finding.LastFrame : "") : "")
                    + " — " + finding.Detail);
            if (Findings.Count > 60) lines.Add(L.T("Weitere betroffene Bewegungen: ", "Additional affected motions: ") + (Findings.Count - 60));
            if (ContactClips == 0)
                lines.Add(L.T("Kontakt- und Symmetrieprüfung benötigt eine menschliche Animationsvorlage mit vollständigen Arm-/Beingelenken.",
                    "Contact and symmetry checks require a human animation style with complete arm/leg joints."));
            return String.Join(Environment.NewLine, lines);
        }
    }

    internal static class CharacterMotionCheck
    {
        internal static readonly string[] Joints = {
            "arm_l1", "arm_l2", "wrist_l1", "arm_r1", "arm_r2", "wrist_r1",
            "leg_l1", "leg_l2", "ankle_l1", "leg_r1", "leg_r2", "ankle_r1"
        };
        static double Distance(float[] a, float[] b) { return RigVector.Length(RigVector.Sub(a, b)); }
        static bool Finite(float[] point) { return point != null && point.Length == 3 && point.All(v => !Single.IsNaN(v) && !Single.IsInfinity(v)); }
        static double Bend(float[][] pose, int start)
        {
            var upper = RigVector.Unit(RigVector.Sub(pose[start + 1], pose[start]));
            var lower = RigVector.Unit(RigVector.Sub(pose[start + 2], pose[start + 1]));
            return Math.Acos(Math.Max(-1, Math.Min(1, RigVector.Dot(upper, lower)))) * 180 / Math.PI;
        }
        internal static void Analyze(CharacterMotionReport report, string vehicle, Dictionary<string, float[][][]> clips,
            bool human, CancellationToken cancellation)
        {
            float[][][] drive;
            var anchor = clips.TryGetValue("drive", out drive) && drive.Length > 0 ? drive[Math.Min(15, drive.Length - 1)] : null;
            bool complete = anchor != null && anchor.Length == Joints.Length && anchor.All(Finite);
            if (human && !complete)
                report.Add(vehicle, "—", L.T("Unvollständige Geradeaushaltung; Kontakt- und Symmetrieprüfung nicht möglich.",
                    "Incomplete straight-driving pose; contact and symmetry checks unavailable."), 0);
            foreach (var clip in clips)
            {
                cancellation.ThrowIfCancellationRequested();
                report.Clips++;
                bool contacts = human && complete && (clip.Key == "drive" || clip.Key == "dash" || clip.Key == "wheelie");
                if (contacts) report.ContactClips++;
                for (int frame = 0; frame < clip.Value.Length; frame++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    report.Frames++;
                    var pose = clip.Value[frame];
                    if (pose.Any(p => p != null && !Finite(p)))
                    {
                        report.Add(vehicle, clip.Key, L.T("Ungültige Gelenkposition.", "Invalid joint position."), frame + 1, true);
                        continue;
                    }
                    if (!human || !complete || pose.Length != Joints.Length || !pose.All(Finite)) continue;
                    foreach (int start in new[] { 0, 3, 6, 9 })
                    {
                        string limb = start == 0 ? L.T("Linker Arm", "Left arm") : start == 3 ? L.T("Rechter Arm", "Right arm")
                            : start == 6 ? L.T("Linkes Bein", "Left leg") : L.T("Rechtes Bein", "Right leg");
                        double length = Distance(anchor[start], anchor[start + 1]) + Distance(anchor[start + 1], anchor[start + 2]);
                        if (length < .0001) continue;
                        for (int segment = start; segment < start + 2; segment++)
                            if (Math.Abs(Distance(pose[segment], pose[segment + 1]) - Distance(anchor[segment], anchor[segment + 1])) > Math.Max(.01, length * .03))
                                report.Add(vehicle, clip.Key, limb + L.T(": veränderte Gliedlänge.", ": limb length changed."), frame + 1);
                        if (!contacts) continue;
                        if (Distance(pose[start + 2], anchor[start + 2]) > Math.Max(.01, length * .03))
                            report.Add(vehicle, clip.Key, limb + L.T(": Kontakt verrutscht gegenüber Geradeausfahrt.", ": contact moved from the straight-driving pose."), frame + 1);
                        if (start < 6 && Bend(pose, start) < 3)
                            report.Add(vehicle, clip.Key, limb + L.T(": Ellbogen fast vollständig gestreckt.", ": elbow almost fully extended."), frame + 1);
                    }
                    bool straight = clip.Key == "wheelie" || clip.Key == "drive" && frame == Math.Min(15, clip.Value.Length - 1);
                    if (contacts && straight)
                    {
                        double armLength = Distance(anchor[0], anchor[1]) + Distance(anchor[1], anchor[2]);
                        double mirrored = RigVector.Length(new[] { pose[1][0] + pose[4][0], pose[1][1] - pose[4][1], pose[1][2] - pose[4][2] });
                        if (mirrored > Math.Max(.01, armLength * .1))
                            report.Add(vehicle, clip.Key, L.T("Auffällige Links-/Rechtsabweichung der Arme; Haltung prüfen.", "Noticeable left/right arm imbalance; review the pose."), frame + 1);
                    }
                }
            }
            if (clips.Count == 0) report.Add(vehicle, "—", L.T("Keine prüfbaren Animationen vorhanden.", "No animations available to check."), 0);
            if (!clips.Values.SelectMany(c => c).Any(p => p.Any(Finite)))
                report.Add(vehicle, "—", L.T("Keine passenden Arm-/Beingelenke; Bewegungsprüfung nicht möglich.", "No matching arm/leg joints; movement check unavailable."), 0);
        }
        internal static CharacterMotionReport Run(IEnumerable<CharacterAsset> assets, bool human, CancellationToken cancellation)
        {
            var selected = assets.Where(a => a.Target.EndsWith(".brres", StringComparison.OrdinalIgnoreCase)
                || ModelRig.ValidVehicleKey(Path.GetFileNameWithoutExtension(a.Target).Split('-')[0])).ToArray();
            var report = new CharacterMotionReport();
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                foreach (var asset in selected)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string key = Path.GetFileNameWithoutExtension(asset.Target).Split('-')[0];
                    bool vehicle = ModelRig.ValidVehicleKey(key);
                    string label = vehicle ? CharacterVehicleNames.ShortLabel(key) : L.T("Charakterauswahl", "Character selection");
                    ModelRuntime.ReportProgress(report.Files * 100 / Math.Max(1, selected.Length), L.T("Bewegungen prüfen: ", "Checking movement: ") + label);
                    byte[] data = asset.Data;
                    if (vehicle)
                    {
                        var archive = new StudioArchiveCopy(asset.Source, asset.Data);
                        var drivers = archive.Files.Where(p => Path.GetFileName(p.Key) == "driver_model.brres").ToArray();
                        if (drivers.Length != 1) throw new InvalidDataException(L.T("Fahrermodell fehlt oder ist mehrdeutig: ", "Missing or ambiguous driver model: ") + label);
                        data = drivers[0].Value.Data;
                        report.Vehicles++;
                    }
                    string path = Path.Combine(work, "motion.brres");
                    File.WriteAllBytes(path, data);
                    var clips = (Dictionary<string, float[][][]>)StudioModelLibrary.Call("ReadJointAnimations", path, "model", Joints, cancellation);
                    Analyze(report, label, clips, human && vehicle, cancellation);
                    report.Files++;
                }
                if (report.Files == 0) report.Add("—", "—", L.T("Keine Charakterdateien für die Bewegungsprüfung vorhanden.", "No character files available for movement checks."), 0);
                return report;
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
        }
    }
}
