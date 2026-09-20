using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class CharacterMenuPose
    {
        internal static List<CharacterAsset> Build(ModelRig rig, CharacterDefinition character, int slot,
            string root, IDictionary<string, CharacterAsset> current, string output, CancellationToken token)
        {
            var result = new List<CharacterAsset>();
            string stem = character.Code + "-" + slot;
            string menu = Path.Combine(root, "Character", "Driver", stem + ".brres");
            var standing = RigPoseReference.Load(menu, "model", "sel_wait", 1, false).Adjusted(rig, 1);
            foreach (string suffix in new[] { "", "_BT" })
            {
                string relative = "Character/AllKart/" + stem + "-allkart" + suffix + ".szs";
                string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    if (suffix.Length > 0) continue;
                    throw new FileNotFoundException(L.T("RR-Menüfahrzeuge fehlen: ", "RR menu vehicles missing: ") + relative);
                }
                token.ThrowIfCancellationRequested();
                var original = new StudioArchiveCopy(path);
                CharacterAsset existing;
                var archive = current.TryGetValue(relative, out existing) ? new StudioArchiveCopy(existing.Source, existing.Data) : new StudioArchiveCopy(path);
                var member = original.Files.Single(p => Path.GetFileName(p.Key) == "driver_anim.brres");
                string work = ModelRuntime.NewWorkFolder();
                string animationFile = Path.Combine(work, "driver_anim.brres");
                string combined = Path.Combine(work, "menu-model.brres");
                File.WriteAllBytes(animationFile, member.Value.Data);
                StudioModelLibrary.Call("CombineMenuAnimations", menu, animationFile, combined);
                var description = new System.Xml.XmlDocument { XmlResolver = null };
                description.LoadXml((string)StudioModelLibrary.Call("ReadPose", combined, "model", "kart-wait", 1f, null));
                string[] clips = description.DocumentElement.GetAttribute("available").Split('|');
                var corrections = new List<string>();
                foreach (string clip in clips)
                {
                    string prefix = clip.Split('-')[0];
                    string key = prefix == "kart" ? character.Weight + "df_kart" : prefix;
                    if (!ModelRig.ValidVehicleKey(key)) throw new InvalidDataException("Unknown RR menu vehicle animation: " + clip);
                    string vehiclePath = Path.Combine(root, "Character", key + "-" + stem + ".szs");
                    var vehicle = rig.ForVehicle(key, RigReferenceSet.ReadVehicle(vehiclePath));
                    var pose = RigPoseReference.Load(combined, "model", clip, 1, false);
                    corrections.Add(pose.FromStandingPose(vehicle, standing).Corrections);
                }
                string changed = Path.Combine(work, "driver-posed.brres");
                StudioModelLibrary.Call("AdjustMenuAnimations", combined, animationFile, corrections.ToArray(), changed);
                archive.Files.Single(p => Path.GetFileName(p.Key) == "driver_anim.brres").Value.Data = File.ReadAllBytes(changed);
                string destination = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllBytes(destination, archive.Build());
                result.Add(CharacterPackage.ReadAsset(destination, character, slot));
            }
            return result;
        }
    }
}
