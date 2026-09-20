using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace murumsWiiModStudio
{
    internal static class DolphinTestProfile
    {
        internal static string Create(string pack, string rrPreset, string iso, string output)
        {
            rrPreset = Path.GetFullPath(rrPreset);
            string presetFolder = Path.GetDirectoryName(rrPreset);
            var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            var profile = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(rrPreset));
            object type, riivo;
            if (!profile.TryGetValue("type", out type) || (string)type != "dolphin-game-mod-descriptor"
                || !profile.TryGetValue("riivolution", out riivo))
                throw new InvalidDataException("Select a Dolphin Retro Rewind preset.");
            var settings = riivo as Dictionary<string, object>;
            if (settings == null || !settings.ContainsKey("patches")) throw new InvalidDataException("Missing Riivolution patches.");
            var patches = ((IEnumerable)settings["patches"]).Cast<object>().ToList();
            bool rr = false;
            var documents = new List<XmlDocument>();
            foreach (var patch in patches.Cast<Dictionary<string, object>>())
            {
                if (!patch.ContainsKey("root") || !patch.ContainsKey("xml")) throw new InvalidDataException("Invalid patch source.");
                string root = Path.GetFullPath(Path.Combine(presetFolder, (string)patch["root"]));
                string xml = Path.GetFullPath(Path.Combine(presetFolder, (string)patch["xml"]));
                if (!File.Exists(xml)) throw new FileNotFoundException("Riivolution XML is missing.", xml);
                var doc = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    doc.Load(reader);
                if (doc.SelectSingleNode("//section[@name='Retro Rewind']") != null)
                {
                    object options;
                    if (!patch.TryGetValue("options", out options))
                        throw new InvalidDataException("Enable Retro Rewind in the selected preset.");
                    rr = ((IEnumerable)options).Cast<Dictionary<string, object>>().Any(option =>
                        option.ContainsKey("section-name") && (string)option["section-name"] == "Retro Rewind"
                        && option.ContainsKey("option-name") && (string)option["option-name"] == "Pack"
                        && option.ContainsKey("choice") && Convert.ToInt32(option["choice"]) > 0);
                }
                // Externe Speicherumleitungen dürfen nie das normale RR-Profil beschreiben.
                foreach (XmlNode save in doc.SelectNodes("//savegame").Cast<XmlNode>().ToArray())
                    save.ParentNode.RemoveChild(save);
                PreserveRelativeRoots(doc, root, xml);
                documents.Add(doc);
                patch["root"] = root; patch["xml"] = Path.GetFullPath(xml);
            }
            if (!rr) throw new InvalidDataException("The preset does not reference Retro Rewind.");
            iso = Path.GetFullPath(iso);
            byte[] header = new byte[6];
            using (var stream = File.OpenRead(iso)) if (stream.Read(header, 0, 6) != 6) throw new InvalidDataException("Incomplete ISO.");
            string id = Encoding.ASCII.GetString(header);
            if (!new[] { "RMCP01", "RMCE01", "RMCJ01", "RMCK01" }.Contains(id)) throw new InvalidDataException("Select a Mario Kart Wii ISO.");
            var packFiles = PackWorkspace.Files(pack).ToArray();
            if (packFiles.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidDataException("Duplicate filenames cannot be mapped safely. Resolve them before creating a test profile.");
            PackWorkspace.Separate(Path.GetDirectoryName(rrPreset), output);
            foreach (var patch in patches.Cast<Dictionary<string, object>>())
                PackWorkspace.Separate((string)patch["root"], output);
            output = Path.GetFullPath(output);
            PackWorkspace.Snapshot(pack, output, delegate(string stage, string final)
            {
                for (int i = 0; i < documents.Count; i++)
                {
                    string safeXml = Path.Combine(output, "source-patch-" + i + ".xml");
                    documents[i].Save(Path.Combine(stage, Path.GetFileName(safeXml)));
                    ((Dictionary<string, object>)patches[i])["xml"] = safeXml;
                }
                string patchFile = Path.Combine(output, "studio-test.xml");
                var xmlText = new StringBuilder("<wiidisc version=\"1\"><id game=\"RMC\"/><options><section name=\"Studio test\"><option name=\"Pack\"><choice name=\"Enabled\"><patch id=\"studio\"/></choice></option></section></options><patch id=\"studio\">");
                foreach (string file in packFiles)
                {
                    string relative = PackWorkspace.Relative(pack, file);
                    if (!new[] { ".szs", ".arc", ".brstm", ".brres", ".bin", ".tpl" }.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                    foreach (string target in Targets(Path.GetFileName(file), relative, documents))
                        xmlText.Append("<file disc=\"").Append(SecurityElement.Escape(target))
                            .Append("\" external=\"").Append(SecurityElement.Escape("/files/" + relative))
                            .Append("\"/>");
                }
                xmlText.Append("</patch></wiidisc>");
                File.WriteAllText(Path.Combine(stage, "studio-test.xml"), xmlText.ToString(), new UTF8Encoding(false));
                patches.Add(new { root = output, xml = patchFile, options = new[] {
                    new Dictionary<string, object> { { "section-name", "Studio test" }, { "option-name", "Pack" }, { "choice", 1 } }
                } });
                settings["patches"] = patches;
                profile["base-file"] = iso;
                profile["display-name"] = "RR — Studio test";
                profile["version"] = 1;

                File.WriteAllText(Path.Combine(stage, "RR-Studio-Test.json"), json.Serialize(profile), new UTF8Encoding(false));
                string config = Path.Combine(stage, "DolphinUser", "Config");
                Directory.CreateDirectory(config);
                File.WriteAllText(Path.Combine(config, "Dolphin.ini"), "[Core]\nSIDevice0 = 6\n[Interface]\nConfirmStop = False\n[Analytics]\nEnabled = False\n");
            });
            return Path.Combine(output, "RR-Studio-Test.json");
        }
        static IEnumerable<string> Targets(string name, string relative, IEnumerable<XmlDocument> documents)
        {
            // RR kann neben dem Original gleichnamige Laufzeitdateien unter /patches laden.
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name, "/" + name, "/patches/" + name };
            foreach (var document in documents)
                foreach (XmlElement node in document.SelectNodes("/wiidisc/patch/file"))
                {
                    string path = node.GetAttribute("disc");
                    if (path.StartsWith("/") && Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))
                        targets.Add(path);
                }
            // Nicht von RR ersetzte UI-Dateien brauchen weiterhin ihren Disc-Pfad.
            string stem = Path.GetFileNameWithoutExtension(name).Split('_')[0];
            if (Path.GetExtension(name).Equals(".szs", StringComparison.OrdinalIgnoreCase)
                && new[] { "MenuOther", "Title", "MenuSingle", "MenuMulti", "Race", "Globe", "Font", "Channel", "Award" }.Contains(stem))
                targets.Add("/Scene/UI/" + name);
            if (name.Equals("globe.arc", StringComparison.OrdinalIgnoreCase))
                targets.Add("/contents/globe.arc");
            if (name.Equals("Earth.szs", StringComparison.OrdinalIgnoreCase) || name.Equals("BackModel.szs", StringComparison.OrdinalIgnoreCase))
                targets.Add("/Scene/Model/" + name);
            if (Path.GetExtension(name).Equals(".brstm", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add("/sound/strm/" + name);
                targets.Add("/patches/sound/strm/" + name);
            }
            string normalized = relative.Replace('\\', '/');
            int characterIndex = normalized.IndexOf("Character/", StringComparison.OrdinalIgnoreCase);
            if (characterIndex >= 0)
            {
                string sourceFolder = normalized.Substring(characterIndex);
                sourceFolder = sourceFolder.Substring(0, sourceFolder.LastIndexOf('/'));
                foreach (var document in documents)
                    foreach (XmlElement folder in document.SelectNodes("/wiidisc/patch/folder"))
                        if (folder.GetAttribute("external").TrimEnd('/').EndsWith("/" + sourceFolder, StringComparison.OrdinalIgnoreCase))
                            targets.Add(folder.GetAttribute("disc").TrimEnd('/') + "/" + name);
            }
            return targets.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }
        static void PreserveRelativeRoots(XmlDocument document, string root, string xml)
        {
            foreach (XmlElement patch in document.SelectNodes("/wiidisc/patch"))
            {
                bool relative = patch.ChildNodes.OfType<XmlElement>().Any(node =>
                    new[] { "external", "valuefile" }.Any(attribute => node.HasAttribute(attribute)
                        && node.GetAttribute(attribute).Length > 0 && !node.GetAttribute(attribute).StartsWith("/")));
                if (!relative) continue;
                string prefix = patch.HasAttribute("root") ? patch.GetAttribute("root") : document.DocumentElement.GetAttribute("root");
                if (prefix.StartsWith("/")) continue;
                // Relative Ressourcen bleiben nach dem Kopieren der XML an ihrer bisherigen Quelle.
                string original = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(xml), prefix));
                string baseFolder = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string suffix = original.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Equals(baseFolder, StringComparison.OrdinalIgnoreCase) ? "" : PackWorkspace.Relative(root, original);
                patch.SetAttribute("root", "/" + suffix);
            }
        }
        internal static Process Start(string dolphin, string preset)
        {
            dolphin = Path.GetFullPath(dolphin); preset = Path.GetFullPath(preset);
            if (!File.Exists(dolphin) || !Path.GetFileName(dolphin).Equals("Dolphin.exe", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Select the installed Dolphin.exe.", dolphin);
            if (!File.Exists(preset)) throw new FileNotFoundException("Build the test profile first.", preset);
            string user = Path.Combine(Path.GetDirectoryName(preset), "DolphinUser");
            return Process.Start(new ProcessStartInfo(dolphin, "-u " + Quote(user) + " -e " + Quote(preset)) {
                UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(dolphin)
            });
        }
        static string Quote(string path)
        {
            if (path.IndexOf('"') >= 0 || path.IndexOf('\n') >= 0) throw new ArgumentException("Invalid path.");
            return "\"" + path.TrimEnd('\\') + "\"";
        }
    }
}



