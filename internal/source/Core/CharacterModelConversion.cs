using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class CharacterModelConversion
    {
        static void Convert(ModelRig rig, string template, string destination, CancellationToken token, int context, RigPoseReference referencePose = null)
        {
            bool edited = rig.GameSettings(context) != null;
            string animation = context == 1 ? "sel_wait" : "drive";
            float frame = context == 1 ? 1 : 16;
            var corrections = new System.Xml.XmlDocument { XmlResolver = null }; corrections.LoadXml("<corrections/>");
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                string mainReference = Path.Combine(work, "reference.dae");
                StudioModelLibrary.Call("ExportModel", template, "model", mainReference);
                int triangles = (int)StudioModelLibrary.Call("TriangleCount", template, "model");
                int mainBudget = Math.Max(1500, Math.Min(4000, triangles * 2));
                string detailed;
                if (edited)
                {
                    var anchor = (referencePose ?? RigPoseReference.Load(template, "model", animation, frame, false)).Adjusted(rig, context);
                    corrections.LoadXml(anchor.Corrections);
                    detailed = rig.ExportDae(token, mainBudget, mainReference, context, anchor);
                }
                else detailed = rig.ExportDae(token, mainBudget, mainReference);
                string copy = Path.Combine(rig.Folder, "game-main-" + context + ".dae");
                File.Copy(detailed, copy, true);
                detailed = copy;
                string lod = detailed;
                if (((string[])StudioModelLibrary.Call("Models", template)).Contains("model_lod"))
                {
                    string reference = Path.Combine(work, "reference-lod.dae");
                    StudioModelLibrary.Call("ExportModel", template, "model_lod", reference);
                    var lodPose = edited ? RigPoseReference.Load(template, "model_lod", animation, frame, false).Adjusted(rig, context) : null;
                    int lodTriangles = (int)StudioModelLibrary.Call("TriangleCount", template, "model_lod");
                    int lodBudget = Math.Max(1000, Math.Min(1200, lodTriangles * 2));
                    lod = rig.ExportDae(token, lodBudget, reference, edited ? context : 0, lodPose);
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
                if (edited)
                {
                    StudioModelLibrary.Call("AdjustAnimations", template, converted, corrections.OuterXml, destination);
                    File.Delete(converted);
                }
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
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
            string folder = ModelRuntime.NewWorkFolder();
            string work = ModelRuntime.NewWorkFolder();
            bool complete = false;
            try
            {
                rig = rig.ExportCopy(work);
                var result = new List<CharacterAsset>();
                var vehicleReferences = new Dictionary<string, RigPoseReference>();
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
                        Convert(rig, edited ? originalPath : source.Source, destination, token, 1);
                    else
                    {
                        var archive = new StudioArchiveCopy(source.Source, source.Data);
                        var drivers = archive.Files.Where(p => Path.GetFileName(p.Key).Equals("driver_model.brres", StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (drivers.Length != 1) throw new InvalidDataException("Expected one driver_model.brres in " + source.Source);
                        string template = Path.Combine(work, Guid.NewGuid().ToString("N") + ".brres");
                        string converted = template + ".converted";
                        // Erneuter Export beginnt mit Originalbewegungen; Fahrzeugänderungen bleiben im äußeren Archiv.
                        var original = edited ? new StudioArchiveCopy(originalPath) : null;
                        var driverData = edited
                            ? original.Files.Single(p => Path.GetFileName(p.Key).Equals("driver_model.brres", StringComparison.OrdinalIgnoreCase)).Value.Data
                            : drivers[0].Value.Data;
                        File.WriteAllBytes(template, driverData);
                        var vehicleRig = rig;
                        RigPoseReference reference = null;
                        if (edited)
                        {
                            string key = Path.GetFileNameWithoutExtension(relative).Split('-')[0];
                            reference = RigPoseReference.Load(template, "model", "drive", 16, false);
                            reference.VehicleCode = key;
                            vehicleReferences.Add(key, reference);
                            vehicleRig = rig.ForVehicle(key, reference);
                        }
                        Convert(vehicleRig, template, converted, token, 2, reference);
                        drivers[0].Value.Data = File.ReadAllBytes(converted);
                        File.WriteAllBytes(destination, archive.Build());
                    }
                    result.Add(CharacterPackage.ReadAsset(destination, character, slot));
                }
                if (rig.MenuPose != null)
                    result.AddRange(CharacterMenuPose.Build(rig, character, slot, rrRoot, sources, folder, token, vehicleReferences));
                token.ThrowIfCancellationRequested();
                complete = true;
                return result;
            }
            finally
            {
                ModelRuntime.DeleteWorkFolder(work);
                if (!complete) ModelRuntime.DeleteWorkFolder(folder);
            }
        }
    }
}
