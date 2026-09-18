using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal static class HudAssetSource
    {
        internal static string Find(string archivePath)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(archivePath));
            foreach (string candidate in new[]
            {
                Path.Combine(folder, "RaceAssets.szs"),
                Path.Combine(folder, "Assets", "RaceAssets.szs"),
                Path.Combine(folder, "..", "Assets", "RaceAssets.szs")
            })
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);

            var directory = new DirectoryInfo(folder);
            while (directory != null)
            {
                if (directory.Name.Equals("Mods", StringComparison.OrdinalIgnoreCase) && directory.Parent != null)
                {
                    string config = Path.Combine(directory.Parent.FullName, "Recomp", "UserData", "Config.toml");
                    if (!File.Exists(config))
                        return null;
                    try
                    {
                        string line = File.ReadLines(config).FirstOrDefault(value =>
                            Regex.IsMatch(value, @"^\s*retro_rewind_root\s*="));
                        if (line == null)
                            return null;
                        var match = Regex.Match(line, "^\\s*retro_rewind_root\\s*=\\s*(\"(?:\\\\.|[^\"\\\\])*\")\\s*$");
                        if (!match.Success)
                            return null;
                        string root = new JavaScriptSerializer().Deserialize<string>(match.Groups[1].Value);
                        string assets = Path.Combine(root, "Assets", "RaceAssets.szs");
                        return File.Exists(assets) ? assets : null;
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
    }
}