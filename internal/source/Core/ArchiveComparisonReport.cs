using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class ArchiveComparisonReport
    {
        const int DetailResourceLimit = 200;
        const int DetailCharacterLimit = 1024 * 1024;

        static string Text(bool german, string de, string en) { return german ? de : en; }

        internal static string RelativePath(string path)
        {
            if (path == null) throw new InvalidDataException("Missing archive resource path.");
            if (path.StartsWith("./", StringComparison.Ordinal)) path = path.Substring(2);
            if (path.Length == 0 || path[0] == '/' || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0
                || path.Split('/').Any(p => p.Length == 0 || p == "." || p == ".."))
                throw new InvalidDataException(L.T("Ungültiger relativer Archivpfad.", "Invalid relative archive path."));
            return Escape(path);
        }

        static string Escape(string text)
        {
            var result = new StringBuilder();
            foreach (char value in text)
            {
                if (Char.IsControl(value)) result.Append("\\u").Append(((int)value).ToString("X4", CultureInfo.InvariantCulture));
                else result.Append(value);
            }
            return result.ToString();
        }

        static bool Equal(byte[] first, byte[] second, CancellationToken token)
        {
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++)
            {
                if ((i & 65535) == 0) token.ThrowIfCancellationRequested();
                if (first[i] != second[i]) return false;
            }
            return true;
        }

        internal static void Write(TextWriter output, StudioArchiveCopy original, StudioArchiveCopy edited,
            CancellationToken token, Action<int, int> progress = null)
        {
            if (output == null || original == null || edited == null) throw new ArgumentNullException();
            bool german = L.IsGerman;
            string[] keys = original.Files.Keys.Union(edited.Files.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var states = new byte[keys.Length];
            int identical = 0, changed = 0, added = 0, removed = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                RelativePath(keys[i]);
                ArchiveEntry before, after;
                bool hasBefore = original.Files.TryGetValue(keys[i], out before), hasAfter = edited.Files.TryGetValue(keys[i], out after);
                if (!hasBefore) { states[i] = 2; added++; }
                else if (!hasAfter) { states[i] = 3; removed++; }
                else if (Equal(before.Data, after.Data, token)) identical++;
                else { states[i] = 1; changed++; }
                if (progress != null && (i % 32 == 0 || i + 1 == keys.Length)) progress(i + 1, keys.Length * 2);
            }
            output.WriteLine(Text(german, "Archivvergleich", "Archive comparison"));
            output.WriteLine(Text(german, "Original: ", "Original: ") + Escape(Path.GetFileName(original.Source)));
            output.WriteLine(Text(german, "Bearbeitung: ", "Edited: ") + Escape(Path.GetFileName(edited.Source)));
            output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                Text(german, "{0} geändert | {1} identisch | {2} hinzugefügt | {3} entfernt", "{0} changed | {1} identical | {2} added | {3} removed"), changed, identical, added, removed));
            output.WriteLine(Text(german, "Analyse aller Pfade; unabhängig von der Übernahmeauswahl. Kein Patch. Hinzugefügte/entfernte Pfade werden nicht zusammengeführt.",
                "Analysis of all paths, independent of the copy selection. Not a patch. Added/removed paths are not merged."));
            output.WriteLine(Text(german, "Semantische Details: höchstens 200 geänderte Ressourcen und 1.048.576 Zeichen; je Ressource gelten die sichtbaren Analysegrenzen. Weitere Binäränderungen sind möglich.",
                "Semantic details: at most 200 changed resources and 1,048,576 characters; each resource retains its displayed analysis limits. Other binary changes are possible."));
            string[] labels = german ? new[] { "Identisch", "Geändert", "Hinzugefügt", "Entfernt" }
                : new[] { "Identical", "Changed", "Added", "Removed" };
            int details = 0, characters = 0, omitted = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                ArchiveEntry before, after;
                original.Files.TryGetValue(keys[i], out before); edited.Files.TryGetValue(keys[i], out after);
                output.WriteLine();
                output.WriteLine(labels[states[i]] + " | " + RelativePath(keys[i]));
                output.WriteLine(Text(german, "Bytes Original → Bearbeitung: ", "Bytes original → edited: ")
                    + (before == null ? "—" : before.Data.Length.ToString(CultureInfo.InvariantCulture)) + " → "
                    + (after == null ? "—" : after.Data.Length.ToString(CultureInfo.InvariantCulture)));
                if (states[i] == 1)
                {
                    if (details < DetailResourceLimit && characters < DetailCharacterLimit)
                    {
                        string description = ArchiveSemanticDiff.Describe(keys[i], before.Data, after.Data);
                        token.ThrowIfCancellationRequested();
                        if (description.Length <= DetailCharacterLimit - characters)
                        { output.WriteLine(description); details++; characters += description.Length; }
                        else { omitted++; characters = DetailCharacterLimit; }
                    }
                    else omitted++;
                }
                if (progress != null && (i % 32 == 0 || i + 1 == keys.Length)) progress(keys.Length + i + 1, keys.Length * 2);
            }
            if (omitted > 0) output.WriteLine(Text(german, "Semantische Details wegen Berichtgrenze ausgelassen: ", "Semantic details omitted due to report limit: ") + omitted.ToString(CultureInfo.InvariantCulture));
            token.ThrowIfCancellationRequested();
        }

        internal static void Save(StudioArchiveCopy original, StudioArchiveCopy edited, string destination,
            CancellationToken token, Action<int, int> progress = null)
        {
            string full = Path.GetFullPath(destination), folder = Path.GetDirectoryName(full);
            if (String.Equals(full, original.Source, StringComparison.OrdinalIgnoreCase)
                || String.Equals(full, edited.Source, StringComparison.OrdinalIgnoreCase)
                || !String.Equals(Path.GetExtension(full), ".txt", StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Eine separate TXT-Berichtdatei wählen. Die Quellen bleiben unverändert.", "Choose a separate TXT report file. Sources remain unchanged."));
            RrMissingFiles.ValidatePath(folder, full);
            token.ThrowIfCancellationRequested();
            string temporary = Path.Combine(folder, ".studio-report-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    Write(writer, original, edited, token, progress);
                    writer.Flush(); stream.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                RrMissingFiles.ValidatePath(folder, full);
                if (File.Exists(full)) File.Replace(temporary, full, null);
                else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
