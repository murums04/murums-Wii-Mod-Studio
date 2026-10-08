using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace murumsWiiModStudio
{
    internal sealed class RigReferenceSet
    {
        internal RigPoseReference Menu, Race, BaseMenu, BaseRace;
        internal string Code, VariantName, BaseName;
        internal string Root, Weight;
        internal int Slot;
        internal readonly Dictionary<string, RigPoseReference> VehicleReferences = new Dictionary<string, RigPoseReference>();
        internal readonly Dictionary<string, RigPoseReference> BaseVehicles = new Dictionary<string, RigPoseReference>();
        CharacterVariant vehicleTarget;
        CharacterAsset[] vehicleAssets;
        readonly Dictionary<string, VehiclePoseGeometry> vehicleVisuals = new Dictionary<string, VehiclePoseGeometry>();
        internal void SetVehicleSources(CharacterVariant target, IEnumerable<CharacterAsset> assets)
        {
            ClearVehicleVisuals();
            vehicleTarget = target;
            vehicleAssets = (assets ?? new CharacterAsset[0]).ToArray();
        }
        internal bool HasVehicleVisual(string key) { return vehicleVisuals.ContainsKey(key); }
        internal CharacterModelImport GetVehicleVisual(string key, System.Threading.CancellationToken token)
        {
            return GetVehicleGeometry(key, token).Model;
        }
        internal VehiclePoseGeometry GetVehicleGeometry(string key, System.Threading.CancellationToken token)
        {
            VehiclePoseGeometry visual;
            if (vehicleVisuals.TryGetValue(key, out visual)) return visual;
            visual = VehiclePoseGeometry.Load(Root, vehicleTarget == null ? Code : vehicleTarget.Character.Code,
                vehicleTarget == null ? Slot : vehicleTarget.Slot, key, vehicleAssets, token);
            vehicleVisuals.Add(key, visual);
            return visual;
        }
        internal void ClearVehicleVisuals()
        {
            foreach (var visual in vehicleVisuals.Values)
                if (visual.Model.Rig != null) ModelRuntime.DeleteWorkFolder(visual.Model.Rig.Folder);
            vehicleVisuals.Clear();
            foreach (var reference in VehicleReferences.Values.Concat(BaseVehicles.Values).Concat(new[] { Race, BaseRace }))
                if (reference != null) reference.VehicleGeometry = null;
        }
        string menuTemplate;
        RigPoseReference menuSource;
        internal void RememberMenuReference(RigPoseReference reference)
        {
            menuSource = reference;
            menuTemplate = Path.GetFullPath(reference.Template);
        }
        internal RigPoseReference GetMenu(ModelRig rig)
        {
            string path = Path.GetFullPath(CharacterModelConversion.MenuTemplate(rig, Root, Menu.Template));
            if (path == Path.GetFullPath(Menu.Template)) return Menu;
            if (menuSource == null || menuTemplate != path)
            {
                menuSource = RigPoseReference.Load(path, "model", "sel_wait", 1, true);
                menuTemplate = path;
            }
            return menuSource;
        }
        internal List<CharacterMenuPose.References> GetMenuVehicleReferences(ModelRig rig, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
        {
            var result = new List<CharacterMenuPose.References>();
            try {
                foreach (bool battle in new[] { false, true }) {
                    token.ThrowIfCancellationRequested();
                    var source = CharacterMenuPose.OpenReferences(rig, Root, Code + "-" + Slot, Weight, battle);
                    if (source != null) result.Add(source);
                }
                token.ThrowIfCancellationRequested();
                return result;
            }
            catch { foreach (var source in result) source.Dispose(); throw; }
        }
        internal string[] Vehicles { get { return new[] { "a", "b", "c", "d", "e", "df" }.SelectMany(v => new[] { Weight + v + "_bike", Weight + v + "_kart" }).ToArray(); } }
        internal RigPoseReference GetVehicle(string key, bool basis)
        {
            if (!ModelRig.ValidVehicleKey(key) || String.IsNullOrEmpty(Root)) return basis ? BaseRace : Race;
            var cache = basis ? BaseVehicles : VehicleReferences;
            RigPoseReference result;
            if (cache.TryGetValue(key, out result))
            {
                if (result.VehicleGeometry == null) result.VehicleGeometry = GetVehicleGeometry(key, System.Threading.CancellationToken.None);
                return result;
            }
            if (basis)
            {
                if (key == Weight + "a_bike" && BaseRace != null)
                {
                    BaseRace.VehicleGeometry = GetVehicleGeometry(key, System.Threading.CancellationToken.None);
                    return BaseRace;
                }
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "murums Wii Mod Studio", "CharacterReferences", Code, key + ".brres");
                if (!File.Exists(path)) return null;
                result = RigPoseReference.Load(path, "model", "drive", 16, true);
                result.VehicleCode = key;
            }
            else
            {
                string path = Path.Combine(Root, "Character", key + "-" + Code + "-" + Slot + ".szs");
                if (!File.Exists(path)) throw new FileNotFoundException("RR vehicle reference missing: " + key);
                result = ReadVehicle(path);
            }
            result.VehicleGeometry = GetVehicleGeometry(key, System.Threading.CancellationToken.None);
            cache[key] = result;
            return result;
        }
        internal RigPoseReference Get(int context, bool basis)
        {
            return context == 1 ? (basis ? BaseMenu : Menu) : (basis ? BaseRace : Race);
        }
        internal static RigReferenceSet Load(CharacterVariant target, string rrRoot)
        {
            var result = new RigReferenceSet { Root = rrRoot, Weight = target.Character.Weight, Slot = target.Slot, Code = target.Character.Code, VariantName = target.Name, BaseName = target.Character.Name };
            result.Menu = RigPoseReference.Load(target.DriverPath, "model", "sel_wait", 1, true);
            string vehicle = Path.Combine(rrRoot, "Character", target.Character.Weight + "a_bike-" + result.Code + "-" + target.Slot + ".szs");
            if (File.Exists(vehicle)) { result.Race = ReadVehicle(vehicle); result.VehicleReferences[target.Character.Weight + "a_bike"] = result.Race; }
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "murums Wii Mod Studio", "CharacterReferences", result.Code);
            if (File.Exists(Path.Combine(cache, "menu.brres"))) result.BaseMenu = RigPoseReference.Load(Path.Combine(cache, "menu.brres"), "model", "sel_wait", 1, true);
            if (File.Exists(Path.Combine(cache, "race.brres"))) result.BaseRace = RigPoseReference.Load(Path.Combine(cache, "race.brres"), "model", "drive", 16, true);
            return result;
        }
        internal static RigPoseReference ReadVehicle(string path, bool visual = true)
        {
            var archive = new StudioArchiveCopy(path);
            var entry = archive.Files.Single(p => Path.GetFileName(p.Key) == "driver_model.brres");
            string model = Path.Combine(ModelRuntime.NewWorkFolder(), "driver_model.brres");
            File.WriteAllBytes(model, entry.Value.Data);
            var result = RigPoseReference.Load(model, "model", "drive", 16, visual);
            result.VehicleCode = Path.GetFileNameWithoutExtension(path).Split('-')[0];
            return result;
        }
        internal void ImportBase(string source, int context, string weight, string vehicle = null)
        {
            string key = ModelRig.ValidVehicleKey(vehicle) ? vehicle : weight + "a_bike";
            string path = source;
            if (GameArchiveImport.IsDisc(source))
            {
                string work = ModelRuntime.NewWorkFolder();
                string archive = context == 1 ? "files/Scene/Model/Driver.szs" : "files/Race/Kart/" + key + "-" + Code + ".szs";
                GameArchiveImport.Extract(source, new[] { archive }, work, true);
                path = Path.Combine(work, Path.GetFileName(archive));
            }
            if (Path.GetExtension(path).Equals(".szs", StringComparison.OrdinalIgnoreCase))
            {
                var archive = new StudioArchiveCopy(path);
                string name = context == 1 ? Code + ".brres" : "driver_model.brres";
                var entry = archive.Files.SingleOrDefault(p => Path.GetFileName(p.Key).Equals(name, StringComparison.OrdinalIgnoreCase));
                if (entry.Value == null) throw new InvalidDataException("Reference archive does not contain " + name);
                path = Path.Combine(ModelRuntime.NewWorkFolder(), name);
                File.WriteAllBytes(path, entry.Value.Data);
            }
            var reference = RigPoseReference.Load(path, "model", context == 1 ? "sel_wait" : "drive", context == 1 ? 1 : 16, true);
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "murums Wii Mod Studio", "CharacterReferences", Code);
            Directory.CreateDirectory(cache);
            BackupManager.WriteAllBytesSafely(Path.Combine(cache, context == 1 ? "menu.brres" : key + ".brres"), File.ReadAllBytes(path));
            if (context == 1) BaseMenu = reference;
            else { reference.VehicleCode = key; BaseVehicles[key] = reference; if (key == weight + "a_bike") BaseRace = reference; }
        }
    }
}
