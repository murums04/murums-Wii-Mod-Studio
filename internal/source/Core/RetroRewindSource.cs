using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml;

namespace murumsWiiModStudio
{
    internal static class RetroRewindSource
    {
        internal static string SavedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "murums Wii Mod Studio", "rr-source.txt");

        internal static bool IsBackgroundArchive(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path).Split('_')[0];
            return new[] { "Title", "MenuSingle", "MenuMulti", "Globe" }.Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        internal static string Resolve(string selected)
        {
            if (String.IsNullOrWhiteSpace(selected)) return null;
            string full = Path.GetFullPath(selected);
            if (String.Equals(Path.GetFileName(full.TrimEnd('\\', '/')), "UI", StringComparison.OrdinalIgnoreCase))
                full = Path.GetDirectoryName(full.TrimEnd('\\', '/'));
            foreach (string suffix in new[] { "", "RetroRewind6", @"WheelWizard\RetroRewind6", @"Riivolution\WheelWizard\RetroRewind6", @"Load\Riivolution\WheelWizard\RetroRewind6", @"RetroRewind\RetroRewind6" })
            {
                string candidate = Path.Combine(full, suffix);
                if (File.Exists(Path.Combine(candidate, "UI", "Title.szs"))
                    && File.Exists(Path.Combine(candidate, "UI", "MenuSingle.szs")))
                    return candidate;
            }
            return null;
        }

        internal static string[] Discover()
        {
            var candidates = new List<string>();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (File.Exists(SavedPath)) candidates.Add(File.ReadAllText(SavedPath).Trim());
            var wheelRoots = new List<string> { Path.Combine(roaming, "CT-MKWII"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CT-MKWII") };
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\WheelWizard"))
                    if (key != null && key.GetValue("AppDataLocation") is string)
                        wheelRoots.Add((string)key.GetValue("AppDataLocation"));
            }
            catch (System.Security.SecurityException) { }
            var userRoots = new List<string> { Path.Combine(roaming, "Dolphin Emulator"), Path.Combine(documents, "Dolphin Emulator") };
            foreach (string wheel in wheelRoots)
            {
                candidates.Add(Path.Combine(wheel, "RetroRewind"));
                string config = Path.Combine(wheel, "config.json");
                if (!File.Exists(config)) continue;
                try
                {
                    var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(config));
                    if (values == null) continue;
                    object value;
                    if (values.TryGetValue("UserFolderPath", out value) && value is string && Path.IsPathRooted((string)value))
                        userRoots.Add((string)value);
                    if (values.TryGetValue("DolphinLocation", out value) && value is string && Path.IsPathRooted((string)value))
                        userRoots.Add(Path.Combine(Path.GetDirectoryName((string)value), "User"));
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidOperationException)) throw; }
            }
            foreach (string user in userRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(user);
                string ini = Path.Combine(user, "Config", "Dolphin.ini");
                if (!File.Exists(ini)) continue;
                foreach (string line in File.ReadAllLines(ini))
                {
                    int equal = line.IndexOf('=');
                    if (equal < 0 || !line.Substring(0, equal).Trim().Equals("LoadPath", StringComparison.OrdinalIgnoreCase)) continue;
                    string load = Environment.ExpandEnvironmentVariables(line.Substring(equal + 1).Trim().Trim('"'));
                    if (!Path.IsPathRooted(load)) load = Path.Combine(user, load);
                    candidates.Add(load);
                }
            }
            var found = new List<string>();
            foreach (string candidate in candidates)
            {
                try
                {
                    string root = Resolve(candidate);
                    if (root != null) found.Add(root);
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return found.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static Dictionary<string, string> Catalog(string selected)
        {
            string root = Resolve(selected);
            if (root == null) throw new InvalidDataException(L.T(
                "RR-Installationsordner mit UI/Title.szs und UI/MenuSingle.szs auswählen; nicht Mods oder Patches.",
                "Select the RR installation containing UI/Title.szs and UI/MenuSingle.szs, not Mods or Patches."));
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in new[] { "UI", "Assets", @"Scene\Model", "Model", "contents", @"Assets\Scene\Model", @"Assets\contents" })
            {
                string directory = Path.Combine(root, folder);
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.GetFiles(directory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(file);
                    if (!name.EndsWith(".szs", StringComparison.OrdinalIgnoreCase) && !name.Equals("globe.arc", StringComparison.OrdinalIgnoreCase)) continue;
                    if (result.ContainsKey(name)) throw new InvalidDataException("Ambiguous RR source: " + name);
                    result.Add(name, file);
                }
            }
            // Sprach-Aliase aus der aktiven RR-Zuordnung übernehmen, niemals aus Patches/Mods.
            string xmlPath = Path.Combine(Path.GetDirectoryName(root), "riivolution", Path.GetFileName(root) + ".xml");
            if (!File.Exists(xmlPath)) throw new FileNotFoundException(L.T(
                "RR-XML fehlt. Wähle die vollständige RR-Installation mit dem benachbarten Ordner riivolution.",
                "RR XML is missing. Select the complete RR installation with its adjacent riivolution folder."), xmlPath);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(xmlPath, settings)) document.Load(reader);
            string prefix = "/" + Path.GetFileName(root) + "/UI/";
            foreach (XmlElement file in document.SelectNodes("//file[@external][@disc]"))
            {
                string external = file.GetAttribute("external").Replace('\\', '/');
                if (!external.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string name = external.Substring(prefix.Length);
                if (name.IndexOf('/') >= 0 || !name.EndsWith(".szs", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.GetFileName(file.GetAttribute("disc").Replace('/', Path.DirectorySeparatorChar));
                if (!target.EndsWith(".szs", StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileNameWithoutExtension(target).Split('_')[0].Equals(Path.GetFileNameWithoutExtension(name).Split('_')[0], StringComparison.OrdinalIgnoreCase)) continue;
                string source = Path.Combine(root, "UI", name);
                if (!File.Exists(source)) throw new FileNotFoundException("RR source referenced by XML is missing.", source);
                string previous;
                if (result.TryGetValue(target, out previous) && !String.Equals(previous, source, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Ambiguous RR mapping: " + target);
                result[target] = source;
            }
            RequireBackground(U8Archive.Load(File.ReadAllBytes(result["Title.szs"])));
            return result;
        }

        internal static string[] Stage(string selected)
        {
            var catalog = Catalog(selected);
            string cache = Path.Combine(Path.GetDirectoryName(SavedPath), "RRSources", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cache);
            foreach (var item in catalog)
                File.Copy(item.Value, Path.Combine(cache, item.Key));
            Directory.CreateDirectory(Path.GetDirectoryName(SavedPath));
            File.WriteAllText(SavedPath, Resolve(selected));
            return catalog.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => Path.Combine(cache, n)).ToArray();
        }

        internal static bool AllowIso(string path, IEnumerable<string> rrNames)
        {
            string name = Path.GetFileName(path);
            return new[] { "Earth.szs", "BackModel.szs", "globe.arc" }.Contains(name, StringComparer.OrdinalIgnoreCase)
                && !rrNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        internal static void RequireBackground(U8Archive archive)
        {
            // RR-Menüs können selbst Originalstrukturen behalten; nur die abweichende Titelvorlage abweisen.
            if (archive.Root.Children.Any(e => e.IsDirectory && e.Name == "." && e.FindChild("title") != null))
                throw new InvalidDataException(L.T(
                    "Abweichende Original-Titelstruktur. Für RR-Hintergrundbilder zuerst im Custom Pack Maker den RR-Ordner auswählen und ein neues Pack erstellen.",
                    "Original title structure differs from RR. For RR backgrounds, select your RR folder in Custom Pack Maker and create a new pack first."));
        }
    }
}
