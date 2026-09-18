using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal sealed class CustomPack
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Folder { get; set; }
        public string FilesFolder { get; set; }
        public string Template { get; set; }
        public override string ToString() { return Name + " — " + FilesFolder; }
    }

    internal static class CustomPacks
    {
        internal static string StorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "murums Wii Mod Studio", "custom-packs.json");

        internal static List<CustomPack> Load()
        {
            if (!File.Exists(StorePath))
                return new List<CustomPack>();
            return new JavaScriptSerializer().Deserialize<List<CustomPack>>(File.ReadAllText(StorePath)) ?? new List<CustomPack>();
        }

        internal static void Register(CustomPack pack)
        {
            var packs = Load();
            packs.RemoveAll(p => String.Equals(p.FilesFolder, pack.FilesFolder, StringComparison.OrdinalIgnoreCase));
            packs.Add(pack);
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
            string temporary = StorePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(packs), new UTF8Encoding(false));
            if (File.Exists(StorePath))
                File.Replace(temporary, StorePath, null);
            else
                File.Move(temporary, StorePath);
        }

        internal static string DefaultRoot(bool retroRewind)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (retroRewind)
                return Path.Combine(roaming, "CT-MKWII", "Mods");
            return Path.Combine(roaming, "Dolphin Emulator", "Load", "Riivolution");
        }

        internal static string LegacyDolphinRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Dolphin Emulator", "Load", "Riivolution");
        }

        internal static void ValidateName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 70 || name != name.Trim()
                || !Regex.IsMatch(name, @"^[\p{L}\p{N} _-]+$")
                || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9]|Temp|riivolution)$", RegexOptions.IgnoreCase))
                throw new ArgumentException(L.T("Pack-Name: 1–70 Buchstaben, Zahlen, Leerzeichen, - oder _. Keine reservierten Namen.",
                    "Pack name: 1–70 letters, numbers, spaces, - or _. Reserved names are not allowed."));
        }

        internal static string DolphinXml(string name)
        {
            string escaped = SecurityElement.Escape(name);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<wiidisc version=\"1\">\n  <id game=\"RMC\" />\n  <options>\n"
                + "    <section name=\"" + escaped + "\"><option name=\"Custom pack\" default=\"0\">"
                + "<choice name=\"Enabled\"><patch id=\"mur-pack\" /></choice></option></section>\n"
                + "  </options>\n  <patch id=\"mur-pack\"><folder external=\"/" + escaped
                + "/Files\" recursive=\"false\" resize=\"true\" create=\"false\" /></patch>\n</wiidisc>\n";
        }

        internal static CustomPack Create(string root, string name, string description, bool retroRewind, IEnumerable<string> sources)
        {
            ValidateName(name);
            if (String.IsNullOrWhiteSpace(root))
                throw new ArgumentException(L.T("Bitte einen Zielordner wählen.", "Please choose a destination folder."));
            root = Path.GetFullPath(root);
            string folder = Path.Combine(root, name);
            string xml = Path.Combine(root, "riivolution", name + ".xml");
            if (Directory.Exists(folder) || File.Exists(folder) || (!retroRewind && (File.Exists(xml) || Directory.Exists(xml))))
                throw new IOException(L.T("Dieses Pack existiert bereits. Bitte einen anderen Namen wählen.",
                    "This pack already exists. Please choose another name."));
            string[] files = sources.Select(Path.GetFullPath).ToArray();
            if (files.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
                throw new IOException(L.T("Zwei Dateien haben denselben Namen. Bitte nur eine auswählen.", "Two files share the same name. Please select only one."));
            foreach (string file in files)
                if (!File.Exists(file) || !Path.GetExtension(file).Equals(".szs", StringComparison.OrdinalIgnoreCase))
                    throw new IOException(L.T("Bitte nur vorhandene .szs-Dateien wählen: ", "Please select existing .szs files only: ") + file);
            var pack = new CustomPack {
                Name = name, Description = description ?? "", Folder = folder,
                FilesFolder = Path.Combine(folder, retroRewind ? name : "Files"),
                Template = retroRewind ? "WheelWizard" : "Dolphin / Riivolution"
            };
            Directory.CreateDirectory(root);
            string stage = Path.Combine(root, ".mur-pack-" + Guid.NewGuid().ToString("N"));
            bool createdXml = false;
            try
            {
                string content = Path.Combine(stage, retroRewind ? name : "Files");
                Directory.CreateDirectory(content);
                foreach (string file in files)
                    File.Copy(file, Path.Combine(content, Path.GetFileName(file)), false);
                if (retroRewind)
                {
                    string summary = Regex.Replace(pack.Description, @"[\r\n\0]+", " ");
                    File.WriteAllText(Path.Combine(stage, name + ".ini"),
                        "[Mod]\nName = " + name + "\nAuthor = \nDescription = " + summary
                        + "\nModID = -1\nIsEnabled = False\nPriority = 0\n", new UTF8Encoding(false));
                }
                else
                {
                    File.WriteAllText(Path.Combine(stage, "Description.txt"), pack.Description, new UTF8Encoding(false));
                    Directory.CreateDirectory(Path.GetDirectoryName(xml));
                    using (var stream = new FileStream(xml, FileMode.CreateNew, FileAccess.Write))
                    {
                        createdXml = true;
                        byte[] bytes = Encoding.UTF8.GetBytes(DolphinXml(name));
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                Directory.Move(stage, folder);
            }
            catch
            {
                if (createdXml) File.Delete(xml);
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                throw;
            }
            return pack;
        }
    }
}