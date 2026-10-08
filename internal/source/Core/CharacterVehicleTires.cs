using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class CharacterVehicleTires
    {
        static T Read<T>(CharacterAsset asset, string method)
        {
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                var member = CharacterImages.Members(asset).Single(p => Path.GetFileName(p.Key) == "kart_model.brres");
                string source = Path.Combine(work, "vehicle.brres");
                File.WriteAllBytes(source, member.Value);
                return (T)StudioModelLibrary.Call(method, source);
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
        }
        internal static bool Available(CharacterAsset asset)
        {
            return Read<string[]>(asset, "VehicleTires").Length > 0;
        }
        internal static Color? Colour(CharacterAsset asset)
        {
            int? value = Read<int?>(asset, "VehicleTireColour");
            return value.HasValue ? (Color?)Color.FromArgb(value.Value) : null;
        }
        internal static CharacterAsset Paint(CharacterAsset asset, Color colour, string memberName = "kart_model.brres")
        {
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                var member = CharacterImages.Members(asset).Single(p => Path.GetFileName(p.Key) == memberName);
                string source = Path.Combine(work, "vehicle.brres"), destination = Path.Combine(work, "tinted.brres");
                File.WriteAllBytes(source, member.Value);
                StudioModelLibrary.Call("SetVehicleTireColour", source, colour.ToArgb(), destination);
                return CharacterImages.ReplaceMember(asset, member.Key, File.ReadAllBytes(destination));
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
        }
        internal static CharacterAsset SyncMenu(CharacterAsset menu, CharacterAsset before, CharacterAsset after, string member)
        {
            var colour = Colour(after);
            return colour.HasValue && colour != Colour(before) ? Paint(menu, colour.Value, Path.GetFileName(member)) : menu;
        }
    }
}
