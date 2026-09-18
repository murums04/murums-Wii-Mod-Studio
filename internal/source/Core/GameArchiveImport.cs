using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal static class GameArchiveImport
    {
        private const string Selection = "+/files/Scene/UI/*.szs;+/files/Scene/Model/Earth.szs;+/files/Scene/Model/BackModel.szs;+/files/contents/globe.arc;-*";

        internal static bool IsDisc(string path)
        {
            string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
            return new[] { ".iso", ".wbfs", ".wia", ".ciso", ".wdf" }.Contains(extension);
        }

        internal static string WithDiscFilter(string filter)
        {
            string[] parts = filter.Split('|');
            if (parts.Length < 2)
                return filter;
            return L.T("Datei oder Spielabbild", "File or game image") + "|" + parts[1]
                + ";*.iso;*.wbfs;*.wia;*.ciso;*.wdf|" + filter
                + "|ISO / WBFS|*.iso;*.wbfs;*.wia;*.ciso;*.wdf";
        }

        private static ToolDescriptor RequireTool()
        {
            var tool = ToolchainManager.FindById("wit");
            if (String.IsNullOrEmpty(ToolchainManager.Find(tool)))
                throw new InvalidOperationException(L.T(
                    "Bitte zuerst Wiimms ISO Tools in der Toolchain-Verwaltung installieren. Danach kannst du die Archive aus deinem Spielabbild importieren.",
                    "Install Wiimms ISO Tools in the toolchain manager first. You can then import archives from your game image."));
            return tool;
        }

        internal static string[] List(string imagePath)
        {
            string output, error;
            string arguments = "files " + ToolchainManager.QuoteArgument(Path.GetFullPath(imagePath))
                + " --psel data --files " + ToolchainManager.QuoteArgument(Selection);
            if (!ToolchainManager.RunCapture(RequireTool(), arguments, out output, out error))
                throw new IOException(error ?? output);
            var paths = new List<string>();
            foreach (string line in (output ?? "").Split('\n'))
            {
                string path = line.Trim();
                if (path.StartsWith("./", StringComparison.Ordinal))
                    path = path.Substring(2);
                if (IsSupportedPath(path))
                    paths.Add(path);
            }
            if (paths.Count == 0)
                throw new InvalidDataException(L.T(
                    "Keine passenden Menüarchive gefunden. Bitte dein Mario-Kart-Wii-Spielabbild auswählen.",
                    "No matching menu archives found. Please choose your Mario Kart Wii game image."));
            return paths.Distinct(StringComparer.Ordinal).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static bool IsSupportedPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || path.Contains("..") || path.Contains("\\")
                || path.IndexOfAny(new[] { ';', '*', '?', ':', '\r', '\n' }) >= 0)
                return false;
            string name = Path.GetFileName(path);
            if (path == "files/contents/globe.arc")
                return true;
            if (path == "files/Scene/Model/Earth.szs" || path == "files/Scene/Model/BackModel.szs")
                return true;
            return path == "files/Scene/UI/" + name && name.EndsWith(".szs", StringComparison.OrdinalIgnoreCase);
        }

        internal static string[] RelatedFiles(string selected, string[] available, bool includeRelated)
        {
            var selectedPaths = new List<string> { selected };
            if (includeRelated)
            {
                string dependency = null;
                string name = Path.GetFileName(selected);
                if (name == "Earth.szs")
                    dependency = "files/contents/globe.arc";
                else if (name.StartsWith("Race_", StringComparison.OrdinalIgnoreCase))
                    dependency = "files/Scene/UI/Race.szs";
                if (dependency != null && available.Contains(dependency))
                    selectedPaths.Add(dependency);
            }
            return selectedPaths.ToArray();
        }

        internal static string Extract(string imagePath, string[] paths, string destination)
        {
            if (paths == null || paths.Length == 0 || paths.Any(path => !IsSupportedPath(path)))
                throw new InvalidDataException("Invalid archive selection.");
            destination = Path.GetFullPath(destination);
            string[] names = paths.Select(Path.GetFileName).ToArray();
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
                throw new InvalidDataException("Duplicate output filenames.");
            foreach (string name in names)
            {
                string target = Path.Combine(destination, name);
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException(L.T("Bereits vorhanden: ", "Already exists: ") + target + "\n\n"
                        + L.T("Wähle einen anderen Zielordner oder öffne die vorhandene Datei. Es wird nichts überschrieben.",
                              "Choose another destination or open the existing file. Nothing will be overwritten."));
            }

            var tool = RequireTool();
            string staging = Path.Combine(Path.GetTempPath(), "murums-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                string rules = String.Join(";", paths.Select(path => "+/" + path)) + ";-*";
                string arguments = "extract " + ToolchainManager.QuoteArgument(Path.GetFullPath(imagePath))
                    + " --dest " + ToolchainManager.QuoteArgument(staging)
                    + " --psel data --flat --files " + ToolchainManager.QuoteArgument(rules);
                string output, error;
                if (!ToolchainManager.RunCapture(tool, arguments, out output, out error))
                    throw new IOException(error ?? output);
                foreach (string name in names)
                {
                    string file = Path.Combine(staging, name);
                    if (!File.Exists(file))
                        throw new FileNotFoundException(L.T("Im Spielabbild nicht gefunden: ", "Not found in game image: ") + name);
                    U8Archive.Load(File.ReadAllBytes(file));
                    if (name == "Earth.szs" || name == "BackModel.szs")
                        MenuModelSource.Validate(file, name);
                }
                Directory.CreateDirectory(destination);
                foreach (string name in names)
                    File.Copy(Path.Combine(staging, name), Path.Combine(destination, name), false);
                return Path.Combine(destination, names[0]);
            }
            finally
            {
                try { Directory.Delete(staging, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
