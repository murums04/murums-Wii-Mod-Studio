using System;
using System.IO;

namespace murumsWiiModStudio
{
    internal static class MenuModelSource
    {
        internal static string CacheRoot
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "murums Wii Mod Studio", "GameSources");
            }
        }

        internal static void Validate(string path, string archiveName)
        {
            var archive = U8Archive.Load(File.ReadAllBytes(path));
            string modelName = archiveName == "Earth.szs" ? "earth_with_dummy_tex.brres" : "baloon.brres";
            var model = SceneColorTools.Find(archive.Root, modelName);
            if (model == null || model.Data == null || model.Data.Length < 16)
                throw new InvalidDataException(L.T("Dieses Archiv enthält nicht die benötigten Mario-Kart-Wii-Modelle.", "This archive does not contain the required Mario Kart Wii models."));
            if (archiveName == "Earth.szs" && SceneColorTools.Find(archive.Root, "galaxy.brres") == null)
                throw new InvalidDataException("Earth.szs does not contain galaxy.brres.");
            if (BrresModelVisibility.IsHidden(model.Data))
                throw new InvalidDataException(L.T("Bitte eine unveränderte Spielquelle verwenden.", "Please use an unchanged game source."));
        }

        internal static string FindCached(string archiveName)
        {
            return FindCached(archiveName, CacheRoot);
        }

        internal static string FindCached(string archiveName, string cacheRoot)
        {
            try
            {
                string pointer = Path.Combine(cacheRoot, "current.txt");
                if (!File.Exists(pointer))
                    return null;
                string directoryName = File.ReadAllText(pointer).Trim();
                Guid identifier;
                if (!Guid.TryParseExact(directoryName, "N", out identifier))
                    return null;
                string path = Path.Combine(cacheRoot, directoryName, archiveName);
                Validate(path, archiveName);
                return path;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        internal static string ImportDisc(string imagePath, string archiveName)
        {
            return ImportDisc(imagePath, archiveName, CacheRoot);
        }

        internal static string ImportDisc(string imagePath, string archiveName, string cacheRoot)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("The selected game image was not found.", imagePath);
            var tool = ToolchainManager.FindById("wit");
            if (String.IsNullOrEmpty(ToolchainManager.Find(tool)))
                throw new InvalidOperationException(L.T("Zum Import aus ISO/WBFS bitte Wiimms ISO Tools über die Toolchain-Verwaltung installieren.", "To import from ISO/WBFS, install Wiimms ISO Tools using the toolchain manager."));
            string directoryName = Guid.NewGuid().ToString("N");
            string destination = Path.Combine(cacheRoot, directoryName);
            Directory.CreateDirectory(destination);
            bool completed = false;
            try
            {
                string arguments = "extract " + ToolchainManager.QuoteArgument(Path.GetFullPath(imagePath)) + " --dest " + ToolchainManager.QuoteArgument(destination) + " --psel data --flat --files " + ToolchainManager.QuoteArgument("+/files/Scene/Model/Earth.szs;+/files/Scene/Model/BackModel.szs;+/files/contents/globe.arc;-*");
                string output, error;
                if (!ToolchainManager.RunCapture(tool, arguments, out output, out error))
                    throw new IOException(error ?? output ?? "Game model import failed.");
                Validate(Path.Combine(destination, "Earth.szs"), "Earth.szs");
                Validate(Path.Combine(destination, "BackModel.szs"), "BackModel.szs");
                var globe = U8Archive.Load(File.ReadAllBytes(Path.Combine(destination, "globe.arc")));
                if (SceneColorTools.Find(globe.Root, "earth.brres.LZ") == null)
                    throw new InvalidDataException("The game image does not contain the expected globe model.");
                BackupManager.WriteAllBytesSafely(Path.Combine(cacheRoot, "current.txt"), System.Text.Encoding.UTF8.GetBytes(directoryName));
                completed = true;
                return Path.Combine(destination, archiveName);
            }
            finally
            {
                // Bei Fehlern nur den Ordner dieses Imports entfernen.
                if (!completed)
                {
                    try
                    {
                        Directory.Delete(destination, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        internal static string FindGlobeArchive(string modelPath)
        {
            string folder = Path.GetDirectoryName(modelPath);
            string alongside = Path.Combine(folder, "globe.arc");
            if (File.Exists(alongside))
                return alongside;
            string extracted = Path.GetFullPath(Path.Combine(folder, "..", "..", "contents", "globe.arc"));
            return File.Exists(extracted) ? extracted : alongside;
        }
    }
}
