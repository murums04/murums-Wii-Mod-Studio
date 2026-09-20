using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace murumsWiiModStudio
{
    internal sealed class CharacterDefinition
    {
        public int Id;
        public string Name, Code, Weight;
        public override string ToString() { return Name; }
        internal static readonly CharacterDefinition[] All = Create();
        static CharacterDefinition[] Create()
        {
            string[] rows = {
                "Mario|mr|m", "Baby Peach|bpc|s", "Waluigi|wl|l", "Bowser|kp|l",
                "Baby Daisy|bds|s", "Dry Bones|ka|s", "Baby Mario|bmr|s", "Luigi|lg|m",
                "Toad|ko|s", "Donkey Kong|dk|l", "Yoshi|ys|m", "Wario|wr|l",
                "Baby Luigi|blg|s", "Toadette|kk|s", "Koopa Troopa|nk|s", "Daisy|ds|m",
                "Peach|pc|m", "Birdo|ca|m", "Diddy Kong|dd|m", "King Boo|kt|l",
                "Bowser Jr.|jr|m", "Dry Bowser|bk|l", "Funky Kong|fk|l", "Rosalina|rs|l"
            };
            return rows.Select((s, i) => {
                var p = s.Split('|');
                return new CharacterDefinition { Id = i, Name = p[0], Code = p[1], Weight = p[2] };
            }).ToArray();
        }
    }

    internal sealed class CharacterNameEntry
    {
        public int CharacterId { get; set; }
        public int CustomId { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        internal uint NameId { get { return ((uint)CharacterId << 16) | 0x6a00u | (uint)CustomId; } }
        internal uint AuthorId { get { return ((uint)CharacterId << 16) | 0x7a00u | (uint)CustomId; } }
        internal void Validate()
        {
            if (CharacterId < 0 || CharacterId >= CharacterDefinition.All.Length || CustomId < 1 || CustomId > 50)
                throw new InvalidDataException("Choose a standard character and a custom ID from 1 to 50.");
            if (String.IsNullOrWhiteSpace(Name) || Name.Length > 128 || (Author ?? "").Length > 128)
                throw new InvalidDataException("Enter a character name; name and author may each contain up to 128 characters.");
            if ((Name + Author).Any(c => Char.IsControl(c)) || (Name + Author).Contains("\\"))
                throw new InvalidDataException("Names must be plain text without control characters or BMG escape sequences.");
        }
    }

    internal static class CharacterNames
    {
        internal static void Validate(IEnumerable<CharacterNameEntry> entries)
        {
            var seen = new HashSet<uint>();
            foreach (var entry in entries)
            {
                entry.Validate();
                if (!seen.Add(entry.NameId))
                    throw new InvalidDataException("A character/custom-ID combination appears more than once.");
            }
        }

        internal static string Merge(string original, IEnumerable<CharacterNameEntry> entries)
        {
            var list = entries.ToArray();
            Validate(list);
            var changes = new Dictionary<uint, string>();
            foreach (var e in list)
            {
                changes[e.NameId] = e.Name;
                changes[e.AuthorId] = e.Author ?? "";
            }
            var doc = new BmgTextDocument(original ?? "#BMG\n");
            foreach (var row in doc.Rows)
            {
                uint id;
                if (UInt32.TryParse(row.Id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id) && changes.ContainsKey(id))
                {
                    row.Text = changes[id];
                    changes.Remove(id);
                }
            }
            var text = new StringBuilder(doc.Build().TrimEnd() + "\n");
            foreach (var pair in changes.OrderBy(p => p.Key))
                text.Append(pair.Key.ToString("x")).Append(" = ").Append(pair.Value).Append('\n');
            return text.ToString();
        }

        internal static List<CharacterNameEntry> Read(string text)
        {
            var values = new Dictionary<uint, string>();
            foreach (var row in new BmgTextDocument(text).Rows)
            {
                uint id;
                if (UInt32.TryParse(row.Id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
                    values[id] = row.Text;
            }
            var result = new List<CharacterNameEntry>();
            foreach (var c in CharacterDefinition.All)
                for (int slot = 1; slot <= 50; slot++)
                {
                    var entry = new CharacterNameEntry { CharacterId = c.Id, CustomId = slot };
                    string name, author;
                    if (!values.TryGetValue(entry.NameId, out name)) continue;
                    entry.Name = name;
                    entry.Author = values.TryGetValue(entry.AuthorId, out author) ? author : "";
                    result.Add(entry);
                }
            return result;
        }

        internal static byte[] PatchArchive(string path, IEnumerable<CharacterNameEntry> entries)
        {
            if (!new[] { "UIAssets.szs", "RaceAssets.szs" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose UIAssets.szs or RaceAssets.szs.");
            var copy = new StudioArchiveCopy(path);
            var existing = copy.Files.FirstOrDefault(p => p.Key.Equals("message/CharaName.bmg", StringComparison.OrdinalIgnoreCase));
            string text = existing.Value == null ? null : BmgTextDocument.Decode(existing.Value.Data);
            byte[] bmg = new BmgTextDocument(Merge(text, entries)).Encode();
            if (existing.Value != null)
                existing.Value.Data = bmg;
            else
            {
                var folder = copy.Archive.Root.FindChild("message");
                if (folder == null)
                {
                    folder = new ArchiveEntry("message", true);
                    copy.Archive.Root.AddChild(folder);
                }
                if (!folder.IsDirectory) throw new InvalidDataException("The message entry is not a folder.");
                folder.AddChild(new ArchiveEntry("CharaName.bmg", false) { Data = bmg });
            }
            return copy.Build();
        }
    }

    internal sealed class CharacterAsset
    {
        public string Source, Target, Role;
        public byte[] Data;
    }

    internal static class CharacterPackage
    {
        internal static CharacterAsset ReadAsset(string path, CharacterDefinition character, int slot)
        {
            if (slot < 1 || slot > 50) throw new InvalidDataException("Custom ID must be 1–50.");
            string name = Path.GetFileName(path);
            string code = Regex.Escape(character.Code);
            string suffix = character.Code + "-" + slot;
            string target, role;
            var vehicle = Regex.Match(name, @"^(" + character.Weight + @"(?:a|b|c|d|e|df)_(?:kart|bike))-" + code + @"(?:-\d+)?(\.szs)$", RegexOptions.IgnoreCase);
            if (vehicle.Success)
            {
                target = "Character/" + vehicle.Groups[1].Value.ToLowerInvariant() + "-" + suffix + ".szs";
                role = "Race vehicle";
            }
            else if (Regex.IsMatch(name, "^" + code + @"(?:-\d+)?\.brres$", RegexOptions.IgnoreCase))
            {
                target = "Character/Driver/" + suffix + ".brres";
                role = "Menu driver";
            }
            else if (Regex.IsMatch(name, "^" + code + @"(?:-\d+)?-allkart(?:_BT)?\.szs$", RegexOptions.IgnoreCase))
            {
                target = "Character/AllKart/" + suffix + "-allkart" + (name.IndexOf("_BT", StringComparison.OrdinalIgnoreCase) >= 0 ? "_BT" : "") + ".szs";
                role = "Menu vehicles";
            }
            else if (Regex.IsMatch(name, "^" + code + @"(?:-\d+)?\.tpl$", RegexOptions.IgnoreCase))
            {
                target = "Character/Map/" + suffix + ".tpl";
                role = "Minimap icon";
            }
            else
                throw new InvalidDataException(name + ": this filename does not match " + character.Name + ". Choose a matching driver, vehicle, allkart or map file.");
            byte[] data = File.ReadAllBytes(path);
            if (name.EndsWith(".szs", StringComparison.OrdinalIgnoreCase))
            {
                var archive = new StudioArchiveCopy(path, data);
                var models = archive.Files.Where(p => p.Key.EndsWith(".brres", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (models.Length == 0) throw new InvalidDataException(name + ": no BRRES model found.");
                foreach (var model in models)
                {
                    if (role == "Menu vehicles" && Path.GetFileName(model.Key).Equals("driver_anim.brres", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] animation = model.Value.Data;
                        if (animation.Length < 16 || System.Text.Encoding.ASCII.GetString(animation, 0, 4) != "bres")
                            throw new InvalidDataException(name + ": invalid menu animation resource.");
                        int dictionary = (animation[12] << 8) | animation[13];
                        if (!BrresModelVisibility.Dictionary(animation, dictionary + 8).Any(g => g.Name == "AnmChr(NW4R)"))
                            throw new InvalidDataException(name + ": no menu character animations.");
                    }
                    else BrresModelVisibility.Models(model.Value.Data);
                }
            }
            else if (name.EndsWith(".brres", StringComparison.OrdinalIgnoreCase))
                BrresModelVisibility.Models(data);
            else
            {
                TexturePreviewResult decoded;
                string error;
                if (!TexturePreview.TryDecode(name, data, 0, out decoded, out error)) throw new InvalidDataException(error);
                decoded.Dispose();
            }
            return new CharacterAsset { Source = Path.GetFullPath(path), Data = data, Target = target, Role = role };
        }

        internal static List<string> Missing(CharacterDefinition character, int slot, IEnumerable<CharacterAsset> assets)
        {
            var paths = new HashSet<string>(assets.Select(a => a.Target), StringComparer.OrdinalIgnoreCase);
            string suffix = character.Code + "-" + slot;
            var required = new List<string> { "Character/Driver/" + suffix + ".brres" };
            foreach (string vehicle in new[] { "a", "b", "c", "d", "e", "df" })
                foreach (string type in new[] { "kart", "bike" })
                    required.Add("Character/" + character.Weight + vehicle + "_" + type + "-" + suffix + ".szs");
            return required.Where(p => !paths.Contains(p)).ToList();
        }

        internal static void WriteNew(string destination, IDictionary<string, byte[]> files)
        {
            string root = Path.GetFullPath(destination);
            if (Directory.Exists(root) || File.Exists(root))
                throw new IOException("Choose a new output folder; existing packs are never overwritten.");
            foreach (string relative in files.Keys)
            {
                string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (Path.IsPathRooted(relative) || !full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unsafe output path.");
            }
            string parent = Path.GetDirectoryName(root);
            Directory.CreateDirectory(parent);
            string stage = Path.Combine(parent, ".mur-character-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                foreach (var file in files)
                {
                    string full = Path.Combine(stage, file.Key.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write))
                        stream.Write(file.Value, 0, file.Value.Length);
                }
                Directory.Move(stage, root);
            }
            catch
            {
                // Nur den eigens angelegten temporären Export entfernen.
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                throw;
            }
        }
    }
}
