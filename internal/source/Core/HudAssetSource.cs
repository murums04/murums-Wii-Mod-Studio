using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal static class HudAssetSource
    {
        internal static string Find(string archivePath) { return Find(archivePath, "RaceAssets.szs"); }

        internal static string Find(string archivePath, string assetName)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(archivePath));
            foreach (string candidate in new[]
            {
                Path.Combine(folder, assetName),
                Path.Combine(folder, "Assets", assetName),
                Path.Combine(folder, "..", "Assets", assetName)
            })
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);

            var directory = new DirectoryInfo(folder);
            while (directory != null)
            {
                if (directory.Name.Equals("Mods", StringComparison.OrdinalIgnoreCase) && directory.Parent != null)
                {
                    string config = Path.Combine(directory.Parent.FullName, "Recomp", "UserData", "Config.toml");
                    if (!File.Exists(config)) return FindSelectedRr(assetName);
                    try
                    {
                        string line = File.ReadLines(config).FirstOrDefault(value =>
                            Regex.IsMatch(value, @"^\s*retro_rewind_root\s*="));
                        if (line == null) return FindSelectedRr(assetName);
                        var match = Regex.Match(line, "^\\s*retro_rewind_root\\s*=\\s*(\"(?:\\\\.|[^\"\\\\])*\")\\s*$");
                        if (!match.Success)
                            return null;
                        string root = new JavaScriptSerializer().Deserialize<string>(match.Groups[1].Value);
                        string assets = Path.Combine(root, "Assets", assetName);
                        return File.Exists(assets) ? assets : FindSelectedRr(assetName);
                    }
                    catch (IOException) { return null; }
                    catch (UnauthorizedAccessException) { return null; }
                    catch (ArgumentException) { return null; }
                    catch (InvalidOperationException) { return null; }
                }
                directory = directory.Parent;
            }
            return null;
        }
        static string FindSelectedRr(string name)
        {
            string root;
            if (File.Exists(RetroRewindSource.SavedPath))
                root = RetroRewindSource.Resolve(File.ReadAllText(RetroRewindSource.SavedPath).Trim());
            else
            {
                string[] found = RetroRewindSource.Discover();
                root = found.Length == 1 ? found[0] : null;
            }
            if (root == null) return null;
            string path = Path.Combine(root, "Assets", name);
            return File.Exists(path) ? path : null;
        }
    }
}