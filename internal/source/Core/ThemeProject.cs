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

        public void Build()
        {
            if (Assets.Count == 0)
                throw new InvalidOperationException("Add edited files to the theme first.");
            if (string.IsNullOrWhiteSpace(OutputFolder))
                throw new IOException("Choose an output folder.");
            // Validate every asset before writing any output. Linked files must still match the reviewed version.
            var writes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in Assets)
            {
                if (Hash(a.Replacement) != a.Hash)
                    throw new IOException("Replacement changed since it was added: " + a.Replacement + ". Remove and add it again to review the new version.");
                string dest = Child(OutputFolder, a.RelativePath);
                if (Assets.Any(s => string.Equals(dest, s.Replacement, StringComparison.OrdinalIgnoreCase) || string.Equals(dest, Child(PackFolder, s.RelativePath), StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("The build would overwrite a source. Choose a separate output folder.");
                for (string parent = Path.GetDirectoryName(dest); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                    if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Choose an output path without linked folders.");
                if (File.Exists(dest) && (File.GetAttributes(dest) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("The output is a linked file.");
                writes.Add(dest, File.ReadAllBytes(a.Replacement));
            }

            foreach (var item in writes)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Key));
                BackupManager.WriteAllBytesSafely(item.Key, item.Value);
            }
        }
    }
}
