using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class CharacterMenuPose
    {
        internal sealed class References : IDisposable
        {
            internal string Work, ArchivePath, AnimationFile, Combined, Fingerprint, Weight;
            internal bool Battle;
            internal RigPoseReference Standing;
            internal string[] Clips;
            readonly Dictionary<string, RigPoseReference> poses = new Dictionary<string, RigPoseReference>();
            internal string Vehicle(string clip)
            {
                if (!Clips.Contains(clip)) throw new InvalidDataException("Unknown menu vehicle clip.");
                string prefix = clip.Split('-')[0];
                string key = prefix == "kart" ? Weight + "df_kart" : prefix;
                ModelRig.MenuReviewKey(key, clip, Battle);
                return key;
            }
            internal string Context(string clip) { return ModelRig.MenuReviewKey(Vehicle(clip), clip, Battle); }
            internal RigPoseReference Pose(string clip)
            {
                string vehicle = Vehicle(clip);
                RigPoseReference pose;
                if (!poses.TryGetValue(clip, out pose)) {
                    pose = RigPoseReference.Load(Combined, "model", clip, 1, false);
                    pose.VehicleCode = vehicle; poses.Add(clip, pose);
                }
                return pose;
            }
            public void Dispose() { if (Work != null) { ModelRuntime.DeleteWorkFolder(Work); Work = null; } }
        }

        internal static string ReferenceFingerprint(string file)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(file)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }

        internal static References OpenReferences(ModelRig rig, string root, string movementStem, string weight, bool battle)
        {
            string path = Path.Combine(root, "Character", "AllKart", movementStem + "-allkart" + (battle ? "_BT" : "") + ".szs");
            if (!File.Exists(path)) {
                if (battle) return null;
                throw new FileNotFoundException(L.T("RR-Menüfahrzeuge fehlen: ", "RR menu vehicles missing: ") + Path.GetFileName(path));
            }
            var result = new References { Work = ModelRuntime.NewWorkFolder(), ArchivePath = path, Weight = weight, Battle = battle };
            try {
                string menu = CharacterModelConversion.MenuTemplate(rig, root, Path.Combine(root, "Character", "Driver", movementStem + ".brres"));
                result.Standing = rig.MenuPose == null ? null : RigPoseReference.Load(menu, "model", "sel_wait", 1, false).Adjusted(rig, 1);
                var original = new StudioArchiveCopy(path);
                var member = original.Files.Single(p => Path.GetFileName(p.Key) == "driver_anim.brres");
                result.AnimationFile = Path.Combine(result.Work, "driver_anim.brres");
                result.Combined = Path.Combine(result.Work, "menu-model.brres");
                File.WriteAllBytes(result.AnimationFile, member.Value.Data);
                StudioModelLibrary.Call("CombineMenuAnimationsForBones", menu, result.AnimationFile, result.Combined,
                    result.Standing == null ? new string[0] : result.Standing.ActiveBoneNames(rig));
                var description = new System.Xml.XmlDocument { XmlResolver = null };
                description.LoadXml((string)StudioModelLibrary.Call("ReadPose", result.Combined, "model", "kart-wait", 1f, null));
                result.Clips = description.DocumentElement.GetAttribute("available").Split('|');
                foreach (string clip in result.Clips) result.Vehicle(clip);
                result.Fingerprint = ReferenceFingerprint(result.Combined);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        internal static List<CharacterAsset> Build(ModelRig rig, CharacterDefinition character, int slot,
            string root, IDictionary<string, CharacterAsset> current, string output, CancellationToken token,
            IDictionary<string, RigPoseReference> vehicleReferences = null, CharacterVariant movement = null)
        {
            var result = new List<CharacterAsset>();
            vehicleReferences = vehicleReferences ?? new Dictionary<string, RigPoseReference>();
            string stem = character.Code + "-" + slot;
            string movementStem = movement == null ? stem : movement.Character.Code + "-" + movement.Slot;
            foreach (string suffix in new[] { "", "_BT" })
            {
                string relative = "Character/AllKart/" + stem + "-allkart" + suffix + ".szs";
                token.ThrowIfCancellationRequested();
                using (var references = OpenReferences(rig, root, movementStem, character.Weight, suffix.Length > 0))
                {
                    if (references == null) continue;
                    CharacterAsset existing;
                    string targetPath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                    var archive = current.TryGetValue(relative, out existing) ? new StudioArchiveCopy(existing.Source, existing.Data)
                        : File.Exists(targetPath) ? new StudioArchiveCopy(targetPath) : new StudioArchiveCopy(references.ArchivePath);
                    var corrections = new List<string>();
                    foreach (string clip in references.Standing == null ? new string[0] : references.Clips)
                    {
                        string key = references.Vehicle(clip);
                        string vehiclePath = Path.Combine(root, "Character", key + "-" + movementStem + ".szs");
                        var pose = references.Pose(clip);
                        RigPoseReference race;
                        if (!vehicleReferences.TryGetValue(key, out race))
                        {
                            race = RigReferenceSet.ReadVehicle(vehiclePath, false);
                            vehicleReferences.Add(key, race);
                        }
                        var vehicle = rig.ForMenuVehicle(key, race, pose, references.Context(clip), references.Fingerprint);
                        corrections.Add(pose.FromStandingPose(vehicle, references.Standing).Corrections);
                    }
                    string changed = Path.Combine(references.Work, "driver-posed.brres");
                    if (references.Standing == null) File.Copy(references.AnimationFile, changed);
                    else StudioModelLibrary.Call("AdjustMenuAnimations", references.Combined, references.AnimationFile, corrections.ToArray(), changed);
                    archive.Files.Single(p => Path.GetFileName(p.Key) == "driver_anim.brres").Value.Data = File.ReadAllBytes(changed);
                    string destination = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.WriteAllBytes(destination, archive.Build());
                    result.Add(CharacterPackage.ReadAsset(destination, character, slot));
                }
            }
            return result;
        }
    }
}
