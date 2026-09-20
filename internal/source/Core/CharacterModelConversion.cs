using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class CharacterModelConversion
    {
        static void Convert(ModelRig rig, string template, string detailed, string destination, CancellationToken token, int context)
        {
            bool edited = rig.GameSettings(context) != null;
            string animation = context == 1 ? "sel_wait" : "drive";
            float frame = context == 1 ? 1 : 16;
            var corrections = new System.Xml.XmlDocument { XmlResolver = null }; corrections.LoadXml("<corrections/>");
            string mainReference = Path.Combine(ModelRuntime.NewWorkFolder(), "reference.dae");
            StudioModelLibrary.Call("ExportModel", template, "model", mainReference);
            if (edited)
            {
                var anchor = RigPoseReference.Load(template, "model", animation, frame, false).Adjusted(rig, context);
                corrections.LoadXml(anchor.Corrections);
                detailed = rig.ExportDae(token, 6000, mainReference, context, anchor);
                string copy = Path.Combine(rig.Folder, "game-main-" + context + ".dae");
                File.Copy(detailed, copy, true); detailed = copy;
            }
            else detailed = rig.ExportDae(token, 6000, mainReference);
            string lod = detailed;
            if (((string[])StudioModelLibrary.Call("Models", template)).Contains("model_lod"))
            {
                string reference = Path.Combine(ModelRuntime.NewWorkFolder(), "reference.dae");
                StudioModelLibrary.Call("ExportModel", template, "model_lod", reference);
                var lodPose = edited ? RigPoseReference.Load(template, "model_lod", animation, frame, false).Adjusted(rig, context) : null;
                lod = rig.ExportDae(token, 1500, reference, edited ? context : 0, lodPose);
                if (edited)
                {
                    var lodCorrections = new System.Xml.XmlDocument { XmlResolver = null }; lodCorrections.LoadXml(lodPose.Corrections);
                    foreach (System.Xml.XmlElement entry in lodCorrections.DocumentElement.ChildNodes)
                        if (!corrections.DocumentElement.ChildNodes.OfType<System.Xml.XmlElement>().Any(e => e.GetAttribute("name") == entry.GetAttribute("name")))
                            corrections.DocumentElement.AppendChild(corrections.ImportNode(entry, true));
                }
            }
            token.ThrowIfCancellationRequested();
            string converted = edited ? destination + ".pending" : destination;
            StudioModelLibrary.Call("ConvertDriver", template, detailed, lod, converted);
            if (edited) StudioModelLibrary.Call("AdjustAnimations", template, converted, corrections.OuterXml, destination);
        }
        internal static List<CharacterAsset> Build(ModelRig rig, CharacterDefinition character, int slot,
            string rrRoot, IEnumerable<CharacterAsset> current, CancellationToken token)
        {
            rig.Validate();
            var required = CharacterPackage.Missing(character, slot, new CharacterAsset[0]);
            var sources = current.ToDictionary(a => a.Target, StringComparer.OrdinalIgnoreCase);
            foreach (string relative in required)
                if (!sources.ContainsKey(relative))
                {
                    string source = Path.Combine(rrRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(source)) throw new FileNotFoundException("RR template missing: " + relative);
                    sources.Add(relative, CharacterPackage.ReadAsset(source, character, slot));
                }
            token.ThrowIfCancellationRequested();
            string detailed = rig.ExportDae(token, 6000);
            // Beide Detailstufen werden aus der korrigierten Zuordnung erzeugt.
            string detailCopy = Path.Combine(rig.Folder, "rigged-main.dae");
            File.Copy(detailed, detailCopy, true);

            string folder = ModelRuntime.NewWorkFolder();
            var result = new List<CharacterAsset>();
            foreach (string relative in required)
            {
                token.ThrowIfCancellationRequested();
                var source = sources[relative];
                bool menu = relative.EndsWith(".brres", StringComparison.OrdinalIgnoreCase);
                bool edited = menu ? rig.MenuPose != null : rig.RacePose != null || (rig.VehiclePoses != null && rig.VehiclePoses.Count > 0);
                string originalPath = Path.Combine(rrRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (edited && !File.Exists(originalPath))
                    throw new FileNotFoundException("Original RR movement template missing: " + relative);
                string destination = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (relative.EndsWith(".brres", StringComparison.OrdinalIgnoreCase))
                    Convert(rig, edited ? originalPath : source.Source, detailCopy, destination, token, 1);
                else
                {
                    var archive = new StudioArchiveCopy(source.Source, source.Data);
                    var drivers = archive.Files.Where(p => Path.GetFileName(p.Key).Equals("driver_model.brres", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (drivers.Length != 1) throw new InvalidDataException("Expected one driver_model.brres in " + source.Source);
                    string template = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".brres");
                    string converted = template + ".converted";
                    // Erneuter Export beginnt mit Originalbewegungen; Fahrzeugänderungen bleiben im äußeren Archiv.
                    var original = edited ? new StudioArchiveCopy(originalPath) : null;
                    var driverData = edited
                        ? original.Files.Single(p => Path.GetFileName(p.Key).Equals("driver_model.brres", StringComparison.OrdinalIgnoreCase)).Value.Data
                        : drivers[0].Value.Data;
                    File.WriteAllBytes(template, driverData);
                    var vehicleRig = rig;
                    if (edited)
                    {
                        string key = Path.GetFileNameWithoutExtension(relative).Split('-')[0];
                        var reference = RigPoseReference.Load(template, "model", "drive", 16, false);
                        reference.VehicleCode = key;
                        vehicleRig = rig.ForVehicle(key, reference);
                    }
                    Convert(vehicleRig, template, detailCopy, converted, token, 2);
                    drivers[0].Value.Data = File.ReadAllBytes(converted);
                    File.WriteAllBytes(destination, archive.Build());
                }
                result.Add(CharacterPackage.ReadAsset(destination, character, slot));
            }
            if (rig.MenuPose != null)
                result.AddRange(CharacterMenuPose.Build(rig, character, slot, rrRoot, sources, folder, token));
            token.ThrowIfCancellationRequested();
            return result;
        }
    }
}
