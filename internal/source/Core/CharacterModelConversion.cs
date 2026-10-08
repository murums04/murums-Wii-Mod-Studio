using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class CharacterModelConversion
    {
        internal static readonly string[] HumanMovementCodes = { "mr", "lg", "pc", "ds", "rs", "wl", "wr", "bmr", "blg", "bpc", "bds" };

        internal static CharacterVariant MovementTarget(string code, int slot, CharacterDefinition target, int targetSlot, string root)
        {
            bool original = String.IsNullOrEmpty(code);
            var definition = original ? target : CharacterDefinition.All.FirstOrDefault(c => c.Code == code);
            int number = original ? targetSlot : slot;
            if (definition == null || definition.Weight != target.Weight || number < 1 || number > 50
                || !original && !HumanMovementCodes.Contains(code))
                throw new InvalidDataException(L.T("Ungültige Bewegungsquelle. Eine Vorlage derselben Gewichtsklasse wählen.",
                    "Invalid movement source. Choose a template of the same weight class."));
            string driver = Path.Combine(root, "Character", "Driver", definition.Code + "-" + number + ".brres");
            if (!File.Exists(driver)) throw new FileNotFoundException(L.T("Bewegungsquelle fehlt: ", "Movement source missing: ") + Path.GetFileName(driver));
            return new CharacterVariant { Character = definition, Slot = number, DriverPath = driver,
                Name = definition.Name + " (" + number + ")", Author = "" };
        }

        internal static string MenuTemplate(ModelRig rig, string root, string fallback)
        {
            var pose = rig.MenuPose;
            if (pose == null || String.IsNullOrEmpty(pose.MenuSourceCode)) return fallback;
            if (!CharacterDefinition.All.Any(c => c.Code == pose.MenuSourceCode) || pose.MenuSourceSlot < 1 || pose.MenuSourceSlot > 50)
                throw new InvalidDataException(L.T("Ungültige Menüvorlage. Die Vorlage erneut auswählen.", "Invalid menu template. Select the template again."));
            string path = Path.Combine(root, "Character", "Driver", pose.MenuSourceCode + "-" + pose.MenuSourceSlot + ".brres");
            if (!File.Exists(path)) throw new FileNotFoundException(L.T("Menüvorlage fehlt: ", "Menu template missing: ") + Path.GetFileName(path));
            return path;
        }

        static string MovementPath(string relative, CharacterDefinition target, int slot, CharacterVariant movement)
        {
            return relative.Replace(target.Code + "-" + slot + ".", movement.Character.Code + "-" + movement.Slot + ".")
                .Replace(target.Code + "-" + slot + "-", movement.Character.Code + "-" + movement.Slot + "-");
        }

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
                else
                {
                    var mapping = referencePose ?? RigPoseReference.Load(template, "model", animation, frame, false);
                    mapping.RequireUneditedMapping(rig, context);
                    detailed = rig.ExportDae(token, mainBudget, mainReference, 0, null, mapping);
                }
                string copy = Path.Combine(rig.Folder, "game-main-" + context + ".dae");
                File.Copy(detailed, copy, true);
                detailed = copy;
                string lod = detailed;
                bool mainSkeletonForLod = false;
                if (((string[])StudioModelLibrary.Call("Models", template)).Contains("model_lod"))
                {
                    string reference = Path.Combine(work, "reference-lod.dae");
                    var lodReference = RigPoseReference.Load(template, "model_lod", animation, frame, false);
                    mainSkeletonForLod = !lodReference.CanMap(rig);
                    if (mainSkeletonForLod)
                    {
                        // Fremde LOD-Skelette können die geprüften Gewichte nicht darstellen.
                        reference = mainReference;
                        lodReference = referencePose ?? RigPoseReference.Load(template, "model", animation, frame, false);
                    }
                    else StudioModelLibrary.Call("ExportModel", template, "model_lod", reference);
                    var lodPose = edited ? lodReference.Adjusted(rig, context) : null;
                    if (!edited) lodReference.RequireUneditedMapping(rig, context);
                    int lodTriangles = (int)StudioModelLibrary.Call("TriangleCount", template, "model_lod");
                    int lodBudget = Math.Max(1000, Math.Min(1200, lodTriangles * 2));
                    lod = rig.ExportDae(token, lodBudget, reference, edited ? context : 0, lodPose, edited ? null : lodReference);
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
                StudioModelLibrary.Call("ConvertDriverWithOptions", template, detailed, lod, converted, mainSkeletonForLod, rig.StarEffect);
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
            ModelRuntime.ReportProgress(-1, L.T("Vorlagen vorbereiten…", "Preparing templates…"));
            rig.Validate();
            rig.RequireExportReview();
            var movement = MovementTarget(rig.MovementCode, rig.MovementSlot, character, slot, rrRoot);
            bool separateMovement = movement.Character != character || movement.Slot != slot;
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
            var vehicleReferences = new Dictionary<string, RigPoseReference>();
            try
            {
                rig = rig.ExportCopy(work);
                var result = new List<CharacterAsset>();
                int completed = 0;
                int total = required.Count + (rig.MenuPose != null || separateMovement ? 1 : 0);
                foreach (string relative in required)
                {
                    token.ThrowIfCancellationRequested();
                    ModelRuntime.ReportProgress(completed * 100 / total,
                        L.T("Datei erstellen: ", "Creating file: ") + Path.GetFileName(relative) + " (" + completed + "/" + total + ")");
                    var source = sources[relative];
                    bool menu = relative.EndsWith(".brres", StringComparison.OrdinalIgnoreCase);
                    bool edited = menu ? rig.MenuPose != null : rig.RacePose != null || (rig.VehiclePoses != null && rig.VehiclePoses.Count > 0);
                    bool useTemplate = edited || separateMovement;
                    string originalPath = Path.Combine(rrRoot, MovementPath(relative, character, slot, movement).Replace('/', Path.DirectorySeparatorChar));
                    if (menu) originalPath = MenuTemplate(rig, rrRoot, originalPath);
                    if (useTemplate && !File.Exists(originalPath))
                        throw new FileNotFoundException("Original RR movement template missing: " + relative);
                    string destination = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if (relative.EndsWith(".brres", StringComparison.OrdinalIgnoreCase))
                        Convert(rig, useTemplate ? originalPath : source.Source, destination, token, 1);
                    else
                    {
                        var archive = new StudioArchiveCopy(source.Source, source.Data);
                        var drivers = archive.Files.Where(p => Path.GetFileName(p.Key).Equals("driver_model.brres", StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (drivers.Length != 1) throw new InvalidDataException("Expected one driver_model.brres in " + source.Source);
                        string template = Path.Combine(work, Guid.NewGuid().ToString("N") + ".brres");
                        string converted = template + ".converted";
                        // Erneuter Export beginnt mit Originalbewegungen; Fahrzeugänderungen bleiben im äußeren Archiv.
                        var original = useTemplate ? new StudioArchiveCopy(originalPath) : null;
                        var driverData = useTemplate
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
                            reference.VehicleGeometry = VehiclePoseGeometry.Load(rrRoot, character.Code, slot, key, sources.Values, token);
                            vehicleReferences.Add(key, reference);
                            vehicleRig = rig.ForVehicle(key, reference);
                        }
                        Convert(vehicleRig, template, converted, token, 2, reference);
                        drivers[0].Value.Data = File.ReadAllBytes(converted);
                        File.WriteAllBytes(destination, archive.Build());
                    }
                    var created = CharacterPackage.ReadAsset(destination, character, slot);
                    created.LogoRegions = source.LogoRegions;
                    created.PaintBase = source.PaintBase;
                    result.Add(created);
                    completed++;
                }
                if (rig.MenuPose != null || separateMovement)
                {
                    ModelRuntime.ReportProgress(completed * 100 / total, L.T("Menüfahrzeuge erstellen…", "Creating menu vehicles…"));
                    result.AddRange(CharacterMenuPose.Build(rig, character, slot, rrRoot, sources, folder, token, vehicleReferences, movement));
                }
                ModelRuntime.ReportProgress(100, L.T("Charakterdateien erstellt.", "Character files created."));
                token.ThrowIfCancellationRequested();
                complete = true;
                return result;
            }
            finally
            {
                foreach (var reference in vehicleReferences.Values)
                    if (reference.VehicleGeometry != null && reference.VehicleGeometry.Model.Rig != null)
                        ModelRuntime.DeleteWorkFolder(reference.VehicleGeometry.Model.Rig.Folder);
                ModelRuntime.DeleteWorkFolder(work);
                if (!complete) ModelRuntime.DeleteWorkFolder(folder);
            }
        }
    }
}
