using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace murumsWiiModStudio
{
    internal sealed class CharacterVariant
    {
        internal CharacterDefinition Character;
        internal int Slot;
        internal string Name, Author, DriverPath, ImageSource;
        internal Bitmap Portrait;
        internal bool HasOwnPortrait;
        internal string Key { get { return Character.Id + ":" + Slot; } }
        internal string Basis { get { return Character.Name + " · " + (Character.Weight == "s" ? L.T("leicht", "light") : Character.Weight == "m" ? L.T("mittel", "medium") : L.T("schwer", "heavy")); } }
        public override string ToString() { return Name + " — " + Character.Name; }
    }

    internal sealed class CharacterCatalog : IDisposable
    {
        internal readonly string Root;
        internal readonly List<CharacterVariant> Variants = new List<CharacterVariant>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly Dictionary<string, string> Archives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] PortraitNames = {
            "mario", "baby_peach", "waluigi", "koopa", "baby_daisy", "karon", "baby_mario", "luigi",
            "kinopio", "donky", "yoshi", "wario", "baby_luigi", "kinopico", "noko", "daisy",
            "peach", "catherine", "didy", "teresa", "koopa_jr", "hone_koopa", "funky", "roseta"
        };

        CharacterCatalog(string root) { Root = root; }

        internal static CharacterCatalog Load(string root, string pack)
        {
            root = Path.GetFullPath(root);
            string drivers = Path.Combine(root, "Character", "Driver");
            if (!Directory.Exists(drivers))
                throw new InvalidDataException(L.T("RR-Ordner mit Character/Driver auswählen.", "Choose the RR folder containing Character/Driver."));
            var result = new CharacterCatalog(root);
            try
            {
                foreach (string file in Directory.GetFiles(drivers, "*.brres").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(Path.GetFileName(file), @"^([a-z]+)-(\d+)\.brres$", RegexOptions.IgnoreCase);
                    int slot;
                    if (!match.Success || !Int32.TryParse(match.Groups[2].Value, out slot) || slot < 1 || slot > 50) continue;
                    var character = CharacterDefinition.All.FirstOrDefault(c => c.Code.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                    if (character == null) continue;
                    if (result.Find(character.Id, slot) != null) throw new InvalidDataException("Duplicate RR character: " + Path.GetFileName(file));
                    result.Variants.Add(new CharacterVariant { Character = character, Slot = slot, DriverPath = file,
                        Name = character.Name + L.T(" · Name fehlt (", " · name unavailable (") + Path.GetFileNameWithoutExtension(file) + ")", Author = "" });
                }
                if (result.Variants.Count == 0) throw new InvalidDataException(L.T("Keine vorhandenen RR-Charaktervarianten gefunden.", "No installed RR character variants found."));
                var portraits = new Dictionary<int, KeyValuePair<string, byte[]>>();
                foreach (string folder in new[] { root, pack }.Where(p => !String.IsNullOrEmpty(p)).SelectMany(p => p == root ? new[] { Path.Combine(p, "Assets") } : new[] { Path.Combine(p, "Assets"), p }).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (String.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
                    foreach (string name in new[] { "RaceAssets.szs", "UIAssets.szs" })
                    {
                        string path = Path.Combine(folder, name);
                        if (!File.Exists(path)) continue;
                        var archive = new StudioArchiveCopy(path);
                        result.Archives[name] = path;
                        foreach (string message in new[] { "message/Common.bmg", "message/CharaName.bmg" })
                        {
                            ArchiveEntry entry;
                            if (!archive.Files.TryGetValue(message, out entry)) continue;
                            foreach (var text in CharacterNames.Read(BmgTextDocument.Decode(entry.Data)))
                            {
                                var variant = result.Find(text.CharacterId, text.CustomId);
                                if (variant == null) continue;
                                if (!String.IsNullOrWhiteSpace(text.Name)) variant.Name = DisplayText(text.Name);
                                variant.Author = DisplayText(text.Author);
                            }
                        }
                        for (int i = 0; i < PortraitNames.Length; i++)
                        {
                            string filename = "tt_" + PortraitNames[i] + "_64x64.tpl";
                            var entry = archive.Files.OrderBy(p => p.Key, StringComparer.Ordinal).FirstOrDefault(p => Path.GetFileName(p.Key).Equals(filename, StringComparison.OrdinalIgnoreCase));
                            if (entry.Value != null) portraits[i] = new KeyValuePair<string, byte[]>(path + " / " + entry.Key, entry.Value.Data);
                        }
                    }
                }
                foreach (var variant in result.Variants)
                {
                    string mapName = variant.Character.Code + "-" + variant.Slot + ".tpl";
                    string icon = new[] { pack, root }.Where(p => !String.IsNullOrEmpty(p))
                        .Select(p => Path.Combine(p, "Character", "Map", mapName)).FirstOrDefault(File.Exists);
                    if (icon != null)
                    {
                        variant.Portrait = result.DecodePortrait(icon, File.ReadAllBytes(icon));
                        variant.HasOwnPortrait = variant.Portrait != null;
                        variant.ImageSource = icon;
                    }
                    if (variant.Portrait == null && portraits.ContainsKey(variant.Character.Id))
                    {
                        var original = portraits[variant.Character.Id];
                        variant.Portrait = result.DecodePortrait(original.Key, original.Value);
                        variant.ImageSource = original.Key;
                    }
                }
                result.Variants.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        static string DisplayText(string text)
        {
            string value = Regex.Replace(text ?? "", @"\\[A-Za-z](?:\{[^}]*\})?", "");
            return new string(value.Where(c => !Char.IsControl(c)).ToArray()).Trim();
        }

        Bitmap DecodePortrait(string source, byte[] data)
        {
            TexturePreviewResult decoded;
            string error;
            if (!TexturePreview.TryDecode(source, data, 0, out decoded, out error))
            {
                Warnings.Add(L.T("Bild nicht lesbar: ", "Cannot read image: ") + source);
                return null;
            }
            using (decoded) return new Bitmap(decoded.Bitmap);
        }

        internal CharacterVariant Find(int character, int slot)
        {
            return Variants.FirstOrDefault(v => v.Character.Id == character && v.Slot == slot);
        }

        internal void RequireExisting(int character, int slot)
        {
            var variant = Find(character, slot);
            if (variant == null || !File.Exists(variant.DriverPath))
                throw new InvalidDataException(L.T("Dieses Ersatzziel ist in der gewählten RR-Installation nicht vorhanden. Vorhandenen Charakter auswählen.",
                    "This replacement target is not present in the selected RR installation. Choose an installed character."));
        }

        public void Dispose()
        {
            foreach (var variant in Variants)
            {
                if (variant.Portrait != null) variant.Portrait.Dispose();
                variant.Portrait = null;
            }
        }
    }
}
