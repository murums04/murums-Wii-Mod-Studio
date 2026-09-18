using System;
using System.Collections.Generic;
using System.IO;

namespace murumsWiiModStudio
{
    internal static class BackupManager
    {
        public static string CreateBackup(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(path), ".murums_backups");
                Directory.CreateDirectory(dir);
                string baseName = Path.GetFileName(path);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + "_" + Guid.NewGuid().ToString("N");
                string backup = Path.Combine(dir, baseName + "." + stamp + ".bak");
                File.Copy(path, backup, false);
                File.SetLastWriteTimeUtc(backup, DateTime.UtcNow);
                Cleanup(dir, baseName, 5);
                return backup;
            }
            catch
            {
                return null;
            }
        }

        // Stage on the same volume, then replace atomically. A failed backup or
        // write must never destroy the user's existing archive.
        public static void WriteAllBytesSafely(string path, byte[] data)
        {
            path = Path.GetFullPath(path);
            string temp = Path.Combine(Path.GetDirectoryName(path), ".murums-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    if (CreateBackup(path) == null)
                        throw new IOException(L.T("Die Sicherung konnte nicht erstellt werden. Das Original bleibt unverändert. Verwende „Speichern unter“ mit einem neuen Dateinamen.", "Backup could not be created. The original was not changed. Use Save as with a new filename."));
                    File.Replace(temp, path, null);
                }
                else
                    File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        private static void Cleanup(string dir, string baseName, int keep)
        {
            try
            {
                string[] files = Directory.GetFiles(dir, baseName + ".*.bak");
                Array.Sort(files, delegate (string a, string b)
                {
                    return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
                });
                for (int i = keep; i < files.Length; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
