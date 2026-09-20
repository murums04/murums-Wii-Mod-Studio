using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;


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
            return ResolveAll(selected).FirstOrDefault();
        }

        internal static string[] ResolveAll(string selected)
        {
            if (String.IsNullOrWhiteSpace(selected)) return new string[0];
            string full = Path.GetFullPath(selected);
            if (full.Length > Path.GetPathRoot(full).Length)
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (String.Equals(Path.GetFileName(full), "UI", StringComparison.OrdinalIgnoreCase))
                full = Path.GetDirectoryName(full);
            var roots = new List<string> { full, Path.Combine(full, "User") };
            foreach (string user in roots.ToArray())
            {
                string ini = Path.Combine(user, "Config", "Dolphin.ini");
                if (!File.Exists(ini)) continue;
                foreach (string line in File.ReadAllLines(ini))
                {
                    int equal = line.IndexOf('=');
                    if (equal < 0 || !line.Substring(0, equal).Trim().Equals("LoadPath", StringComparison.OrdinalIgnoreCase)) continue;
                    string load = Environment.ExpandEnvironmentVariables(line.Substring(equal + 1).Trim().Trim('"'));
                    if (load.Length == 0) continue;
                    roots.Add(Path.IsPathRooted(load) ? load : Path.Combine(user, load));
                }
            }
            string[] suffixes = {
                "", "RetroRewind6", @"Riivolution\RetroRewind6", @"Load\Riivolution\RetroRewind6",
                @"WheelWizard\RetroRewind6", @"Riivolution\WheelWizard\RetroRewind6",
                @"Load\Riivolution\WheelWizard\RetroRewind6", @"RetroRewind\RetroRewind6"
            };
            return roots.SelectMany(root => suffixes.Select(suffix => Path.GetFullPath(Path.Combine(root, suffix))))
                .Where(candidate => File.Exists(Path.Combine(candidate, "UI", "Title.szs"))
                    && File.Exists(Path.Combine(candidate, "UI", "MenuSingle.szs")))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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
                    found.AddRange(ResolveAll(candidate));
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
            string effects = Path.Combine(root, "Patches", "Common.szs");
            if (File.Exists(effects))
            {
                // RR lädt Renn-Effekte aus /patches; eine zusätzliche UI-Kopie ist keine Effektquelle.
                result["Common.szs"] = effects;
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

