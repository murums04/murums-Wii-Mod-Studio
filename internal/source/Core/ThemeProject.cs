using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace murumsWiiModStudio
{
    internal sealed class ThemeProject
    {
        public sealed class Asset
        {
            public string RelativePath, Replacement, Hash;
            public override string ToString()
            {
                return RelativePath + "  ←  " + Replacement;
            }
        }

        public string PackFolder, OutputFolder, Notes = "";
        public readonly List<Asset> Assets = new List<Asset>();
        public static string Hash(string path)
        {
            using (var h = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(h.ComputeHash(stream)).Replace("-", "");
        }

        static string B64(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        }

        static string Un64(string s)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }

        public static string Child(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(s => s == ".." || s == "." || s.Length == 0 || s.Contains(":")))
                throw new InvalidDataException("Invalid project destination path.");
            string dir = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(dir, relative));
            if (!result.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The destination must stay inside the project folder.");
            return result;
        }

        public void Add(string original, string replacement)
        {
            string root = Path.GetFullPath(PackFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(original);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new IOException("Choose an existing destination file inside your custom pack.");
            string relative = full.Substring(root.Length);
            Child(root, relative);
            string repl = Path.GetFullPath(replacement);
            string hash = Hash(repl);
            Assets.RemoveAll(a => a.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
            Assets.Add(new Asset { RelativePath = relative, Replacement = repl, Hash = hash });
        }

        internal void AddEditedFiles(IEnumerable<string> paths)
        {
            string editedRoot = Path.GetFullPath(Path.Combine(PackFolder, "MUR_EDITED")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pending = new ThemeProject { PackFolder = PackFolder };
            foreach (string path in paths)
            {
                string full = Path.GetFullPath(path);
                string relative = full.StartsWith(editedRoot, StringComparison.OrdinalIgnoreCase) ? full.Substring(editedRoot.Length) : Path.GetFileName(full);
                string original = Child(PackFolder, relative);
                if (!File.Exists(original)) throw new IOException(L.T("Kein passendes Original im Pack: ", "No matching original in the pack: ") + relative
                    + L.T(". Bei abweichenden Namen die manuelle Zuordnung verwenden.", ". Use manual mapping if filenames differ."));
                if (full.Equals(original, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("Die bearbeitete Kopie aus MUR_EDITED wählen.", "Choose the edited copy from MUR_EDITED."));
                pending.Add(original, full);
            }
            foreach (var item in pending.Assets)
            {
                Assets.RemoveAll(a => a.RelativePath.Equals(item.RelativePath, StringComparison.OrdinalIgnoreCase));
                Assets.Add(item);
            }
        }

        public void Save(string path)
        {
            string full = Path.GetFullPath(path);
            if (Assets.Any(a => string.Equals(full, a.Replacement, StringComparison.OrdinalIgnoreCase) || string.Equals(full, Child(PackFolder, a.RelativePath), StringComparison.OrdinalIgnoreCase)))
                throw new IOException("The project file cannot overwrite a pack or replacement file.");
            var lines = new List<string>
            {
                "MURUMS-THEME-1",
                B64(PackFolder),
                B64(OutputFolder),
                B64(Notes)
            };
            lines.AddRange(Assets.Select(a => B64(a.RelativePath) + "\t" + B64(a.Replacement) + "\t" + a.Hash));
            BackupManager.WriteAllBytesSafely(path, Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        }

        public static ThemeProject Load(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length < 4 || lines[0] != "MURUMS-THEME-1")
                throw new InvalidDataException("Unsupported theme project.");
            var p = new ThemeProject
            {
                PackFolder = Un64(lines[1]),
                OutputFolder = Un64(lines[2]),
                Notes = Un64(lines[3])
            };
            Path.GetFullPath(p.PackFolder);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines.Skip(4))
            {
                if (line.Length == 0)
                    continue;
                string[] cells = line.Split('\t');
                if (cells.Length != 3 || cells[2].Length != 64 || cells[2].Any(c => !Uri.IsHexDigit(c)))
                    throw new InvalidDataException("Invalid theme asset record.");
                string relative = Un64(cells[0]);
                Child(p.PackFolder, relative);
                if (!used.Add(relative))
                    throw new InvalidDataException("Duplicate theme destination.");
                p.Assets.Add(new Asset { RelativePath = relative, Replacement = Path.GetFullPath(Un64(cells[1])), Hash = cells[2] });
            }

            return p;
        }

        internal static string SuggestedOutput(string pack)
        {
            string full = Path.GetFullPath(pack).TrimEnd(Path.DirectorySeparatorChar);
            return full + "_Theme";
        }

        public void Build()
        {
            BuildTransaction(null);
        }

        internal void BuildTransaction(Action<int> afterWrite)
        {
            if (Assets.Count == 0)
                throw new InvalidOperationException(L.T("Zuerst bearbeitete Dateien hinzufügen.", "Add edited files to the theme first."));
            if (string.IsNullOrWhiteSpace(OutputFolder))
                throw new IOException(L.T("Ausgabeordner wählen.", "Choose an output folder."));
            string output = Path.GetFullPath(OutputFolder).TrimEnd(Path.DirectorySeparatorChar);
            string pack = Path.GetFullPath(PackFolder).TrimEnd(Path.DirectorySeparatorChar);
            if (output.Equals(pack, StringComparison.OrdinalIgnoreCase)
                || output.StartsWith(pack + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Der Ausgabeordner muss außerhalb des Originalpacks liegen.", "The output folder must be outside the original pack."));

            // Validate every asset before writing any output. Linked files must still match the reviewed version.
            var writes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in Assets)
            {
                if (Hash(a.Replacement) != a.Hash)
                    throw new IOException(L.T("Ersatzdatei wurde geändert. Entferne sie und füge sie erneut hinzu: ", "Replacement changed. Remove and add it again: ") + a.Replacement);
                string dest = Child(output, a.RelativePath);
                if (Assets.Any(s => string.Equals(dest, s.Replacement, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException(L.T("Die Ausgabe würde eine Quelldatei überschreiben. Separaten Ordner wählen.", "The build would overwrite a source. Choose a separate output folder."));
                if (Directory.Exists(dest))
                    throw new IOException(L.T("Am Dateiziel liegt bereits ein Ordner: ", "A folder already exists at the file destination: ") + dest);
                for (string parent = Path.GetDirectoryName(dest); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                {
                    if (File.Exists(parent))
                        throw new IOException(L.T("Ein Dateiname blockiert den Ausgabeordner: ", "A file blocks the output folder: ") + parent);
                    if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException(L.T("Ausgabeordner ohne Ordnerverknüpfungen wählen.", "Choose an output path without linked folders."));
                }
                if (File.Exists(dest) && (File.GetAttributes(dest) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(L.T("Die Ausgabedatei ist eine Verknüpfung.", "The output is a linked file."));
                writes.Add(dest, File.ReadAllBytes(a.Replacement));
            }
            foreach (string dest in writes.Keys)
                if (writes.Keys.Any(other => other.StartsWith(dest + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException(L.T("Die Ausgabe enthält widersprüchliche Datei- und Ordnerpfade.", "The output contains conflicting file and folder paths."));

            string recovery = Path.Combine(Path.GetTempPath(), "murums-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(recovery);
            var originals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var completed = new List<string>();
            bool keepRecovery = false;
            try
            {
                foreach (string dest in writes.Keys)
                {
                    string backup = null;
                    if (File.Exists(dest))
                    {
                        backup = Path.Combine(recovery, originals.Count + ".bak");
                        File.Copy(dest, backup);
                    }
                    originals.Add(dest, backup);
                }
                foreach (var item in writes)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Key));
                    BackupManager.WriteAllBytesSafely(item.Key, item.Value);
                    completed.Add(item.Key);
                    if (afterWrite != null) afterWrite(completed.Count);
                }
            }
            catch (Exception error)
            {
                var failures = new List<Exception>();
                // Bereits geschriebene Dateien rückwärts wiederherstellen.
                foreach (string dest in completed.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (originals[dest] == null) File.Delete(dest);
                        else BackupManager.WriteAllBytesSafely(dest, File.ReadAllBytes(originals[dest]));
                    }
                    catch (Exception rollbackError) { failures.Add(rollbackError); }
                }
                if (failures.Count > 0)
                {
                    keepRecovery = true;
                    File.WriteAllLines(Path.Combine(recovery, "paths.txt"), originals.Select(p => p.Value + "\t" + p.Key));
                    throw new IOException(L.T("Wiederherstellung unvollständig. Sicherungen: ", "Recovery incomplete. Backups: ") + recovery, error);
                }
                throw;
            }
            finally
            {
                if (!keepRecovery)
                    try { Directory.Delete(recovery, true); } catch (IOException) { }
            }
        }
    }
}