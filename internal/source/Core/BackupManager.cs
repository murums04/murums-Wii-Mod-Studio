using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class BackupManager
    {
        sealed class PendingWrite
        {
            internal string Path, Temporary, Backup, BackupFolder;
            internal byte[] Data;
            internal bool Installed, RecoveryFailed, Delete;
        }

        static readonly object SaveLock = new object();

        // Stage on the same volume, then replace atomically. A failed backup or
        // write must never destroy the user's existing archive.
        public static void WriteAllBytesSafely(string path, byte[] data)
        {
            WriteBatch(new[] { new KeyValuePair<string, byte[]>(path, data) });
        }

        static string ExportBackupFolder(string path)
        {
            string parent = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)).ToUpperInvariant();
            using (var hash = SHA256.Create())
            {
                string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(parent))).Replace("-", "");
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "murums Wii Mod Studio", "ExportRecovery", key);
            }
        }

        internal static void WriteBatch(IEnumerable<KeyValuePair<string, byte[]>> files,
            Action<int> afterWrite = null, string backupFolder = null, bool exportCopy = false,
            IEnumerable<string> deletePaths = null)
        {
            lock (SaveLock)
            {
                var pending = new List<PendingWrite>();
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    if (file.Value == null) throw new ArgumentNullException("data");
                    pending.Add(PrepareWrite(file.Key, file.Value, false, paths, backupFolder, exportCopy));
                }
                if (deletePaths != null)
                    foreach (string path in deletePaths)
                        pending.Add(PrepareWrite(path, null, true, paths, backupFolder, exportCopy));
                if (pending.Any(p => paths.Any(other => other.StartsWith(p.Path + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))))
                    throw new IOException("The output contains conflicting file and folder paths.");

                bool saved = false;
                try
                {
                    // Erst alle Inhalte und Sicherungen bereitstellen, dann Dateien ersetzen.
                    foreach (var file in pending)
                    {
                        if (!file.Delete && MatchesFile(file.Path, file.Data)) continue;
                        string parent = System.IO.Path.GetDirectoryName(file.Path);
                        if (!file.Delete)
                        {
                            Directory.CreateDirectory(parent);
                            file.Temporary = System.IO.Path.Combine(parent, ".murums-" + Guid.NewGuid().ToString("N") + ".tmp");
                            using (var stream = new FileStream(file.Temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            {
                                stream.Write(file.Data, 0, file.Data.Length);
                                stream.Flush(true);
                            }
                        }
                        if (File.Exists(file.Path))
                        {
                            Directory.CreateDirectory(file.BackupFolder);
                            if ((File.GetAttributes(file.BackupFolder) & FileAttributes.ReparsePoint) != 0)
                                throw new IOException("The backup folder is a link: " + file.BackupFolder);
                            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + "_" + Guid.NewGuid().ToString("N");
                            file.Backup = System.IO.Path.Combine(file.BackupFolder, System.IO.Path.GetFileName(file.Path) + "." + stamp + ".bak");
                            File.Copy(file.Path, file.Backup, false);
                            File.SetLastWriteTimeUtc(file.Backup, DateTime.UtcNow);
                        }
                    }
                    int completed = 0;
                    foreach (var file in pending.Where(p => p.Temporary != null || p.Delete && p.Backup != null))
                    {
                        if (file.Delete) File.Delete(file.Path);
                        else if (file.Backup == null) File.Move(file.Temporary, file.Path);
                        else File.Replace(file.Temporary, file.Path, null);
                        file.Installed = true;
                        completed++;
                        if (afterWrite != null) afterWrite(completed);
                    }
                    saved = true;
                }
                catch (Exception failure)
                {
                    var errors = new List<Exception> { failure };
                    foreach (var file in pending.Where(p => p.Installed).Reverse())
                    {
                        try
                        {
                            if (file.Backup == null) File.Delete(file.Path);
                            else if (File.Exists(file.Path)) File.Replace(file.Backup, file.Path, null);
                            else File.Move(file.Backup, file.Path);
                        }
                        catch (Exception error)
                        {
                            file.RecoveryFailed = true;
                            errors.Add(error);
                        }
                    }
                    if (errors.Count > 1)
                        throw new IOException(L.T("Speichern fehlgeschlagen; Wiederherstellung unvollständig. Diese Dateien/Sicherungen bleiben erhalten: ",
                            "Save failed; recovery is incomplete. These files/backups were kept: ")
                            + String.Join(", ", pending.Where(p => p.RecoveryFailed).Select(p => p.Backup ?? p.Path)), new AggregateException(errors));
                    throw;
                }
                finally
                {
                    foreach (var file in pending)
                    {
                        TryDelete(file.Temporary);
                        if (saved) Cleanup(file.BackupFolder, System.IO.Path.GetFileName(file.Path), file.Backup);
                        // Ein fehlgeschlagener Austausch darf die vorherige Sicherung nicht verdrängen.
                        else if (!file.RecoveryFailed) TryDelete(file.Backup);
                    }
                }
            }
        }

        static PendingWrite PrepareWrite(string name, byte[] data, bool delete, HashSet<string> paths,
            string backupFolder, bool exportCopy)
        {
            string path = System.IO.Path.GetFullPath(name);
            if (!paths.Add(path)) throw new IOException("Duplicate output file: " + path);
            if (Directory.Exists(path)) throw new IOException("A folder occupies the output file: " + path);
            return new PendingWrite
            {
                Path = path,
                Data = data,
                Delete = delete,
                BackupFolder = exportCopy ? ExportBackupFolder(path) : backupFolder == null
                    ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), ".murums_backups")
                    : System.IO.Path.GetFullPath(backupFolder)
            };
        }

        static bool MatchesFile(string path, byte[] data)
        {
            if (!File.Exists(path)) return false;
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length != data.Length) return false;
                var buffer = new byte[65536];
                int position = 0, count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < count; i++)
                        if (buffer[i] != data[position + i]) return false;
                    position += count;
                }
                return position == data.Length;
            }
        }

        static void TryDelete(string path)
        {
            if (path == null) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException error) { Trace.WriteLine(error.Message); }
            catch (UnauthorizedAccessException error) { Trace.WriteLine(error.Message); }
        }

        static void Cleanup(string directory, string baseName, string latest)
        {
            try
            {
                if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
                string pattern = "^" + Regex.Escape(baseName)
                    + @"\.(?:\d{8}_\d{6}_\d{7}_[0-9a-f]{32}|\d{8}-\d{6}-\d{7}-[0-9a-f]{32})\.bak$";
                var files = Directory.GetFiles(directory, baseName + ".*.bak")
                    .Where(file => Regex.IsMatch(System.IO.Path.GetFileName(file), pattern))
                    .OrderByDescending(file => String.Equals(file, latest, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(File.GetLastWriteTimeUtc).Skip(1);
                foreach (string file in files) TryDelete(file);
            }
            catch (IOException error) { Trace.WriteLine(error.Message); }
            catch (UnauthorizedAccessException error) { Trace.WriteLine(error.Message); }
        }
    }
}
