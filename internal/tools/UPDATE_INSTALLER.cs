using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace murumsWiiModStudio.Setup
{
    internal static class UpdateInstaller
    {
        internal static readonly string[] PackageFiles =
        {
            "murums Wii Mod Studio.exe",
            "Uninstall.exe",
            "LICENSE",
            "THIRD_PARTY.md",
            "internal/tools/SETUP_CHROME.dll",
            "internal/tools/SETUP_GUI.ps1",
            "internal/tools/SETUP_WORKER.ps1",
            "internal/tools/SETUP_OPTIONS.ps1",
            "internal/tools/INSTALL_TOOLCHAIN.ps1",
            "internal/tools/UNINSTALL.ps1"
        };
        internal static string ValidateRoot(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new IOException("Invalid installation folder.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            if (root == Path.GetPathRoot(root).TrimEnd(Path.DirectorySeparatorChar))
                throw new IOException("A drive root is not an installation folder.");
            CheckLinks(root);
            if (!File.Exists(SafePath(root, "install-manifest.txt")))
                throw new IOException("The installation manifest is missing. Use the normal installer for a new installation.");
            string exe = SafePath(root, PackageFiles[0]);
            if (!File.Exists(exe) || FileVersionInfo.GetVersionInfo(exe).ProductName != "murums Wii Mod Studio")
                throw new IOException("This folder is not a Studio installation.");
            return root;
        }

        internal static string SafePath(string root, string relative)
        {
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (Path.IsPathRooted(relative) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid update path.");
            CheckLinks(path);
            return path;
        }

        static void CheckLinks(string path)
        {
            for (string item = path; !String.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
                if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Updates through linked files or folders are not supported.");
        }

        internal static bool Ready(string root)
        {
            foreach (var file in PackageFiles.Concat(new[] { "install-manifest.txt" }))
            {
                string path = SafePath(root, file);
                if (!File.Exists(path))
                    continue;
                try
                {
                    using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                }
                catch (IOException)
                {
                    return false;
                }
            }

            return true;
        }

        internal static string Apply(string directory, Stream package, Action<int> afterReplace)
        {
            string root = ValidateRoot(directory);
            if (!Ready(root))
                throw new IOException("Close all Studio windows and other programs using this installation, then retry.");
            string workspace = Path.Combine(root, ".studio-update-" + Guid.NewGuid().ToString("N"));
            string stage = Path.Combine(workspace, "new"), backup = Path.Combine(workspace, "backup");
            Directory.CreateDirectory(stage);
            Directory.CreateDirectory(backup);
            var changed = new List<string>();
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool preserveBackup = false;
            try
            {
                var expected = new HashSet<string>(PackageFiles, StringComparer.Ordinal);
                using (var zip = new ZipArchive(package, ZipArchiveMode.Read, true))
                {
                    if (zip.Entries.Count != PackageFiles.Length)
                        throw new InvalidDataException("Unexpected update package contents.");
                    foreach (var entry in zip.Entries)
                    {
                        if (!expected.Remove(entry.FullName) || entry.Length > 200L * 1024 * 1024)
                            throw new InvalidDataException("Unexpected update package file.");
                        string target = SafePath(stage, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (var source = entry.Open())
                        using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                            source.CopyTo(output);
                    }
                }

                string stagedExe = SafePath(stage, PackageFiles[0]);
                var oldInfo = FileVersionInfo.GetVersionInfo(SafePath(root, PackageFiles[0]));
                var newInfo = FileVersionInfo.GetVersionInfo(stagedExe);
                Version oldVersion, newVersion;
                if (newInfo.ProductName != "murums Wii Mod Studio" || !Version.TryParse(newInfo.FileVersion, out newVersion) || !Version.TryParse(oldInfo.FileVersion, out oldVersion) || newVersion <= oldVersion)
                    throw new InvalidDataException("The package does not contain a newer Studio build.");
                // Merge the installer manifest, retaining previously registered files.
                // Unknown user files are never discovered or added to this manifest.
                var manifest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var relative in File.ReadAllLines(SafePath(root, "install-manifest.txt")))
                    if (!String.IsNullOrWhiteSpace(relative))
                    {
                        SafePath(root, relative);
                        manifest.Add(relative);
                    }

                foreach (var relative in PackageFiles)
                    manifest.Add(relative);
                File.WriteAllLines(Path.Combine(stage, "install-manifest.txt"), manifest.OrderBy(x => x).ToArray());
                // Copy every original before replacing anything. Replacements are atomic
                // per file, and the previous full installation remains in this workspace.
                string[] targets = PackageFiles.Concat(new[] { "install-manifest.txt" }).ToArray();
                foreach (var relative in targets)
                {
                    string target = SafePath(root, relative);
                    if (File.Exists(target))
                    {
                        string copy = SafePath(backup, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(copy));
                        File.Copy(target, copy, false);
                        existing.Add(relative);
                    }
                }

                if (!Ready(root))
                    throw new IOException("Studio is still running. Close it and retry.");
                try
                {
                    foreach (var relative in targets)
                    {
                        string target = SafePath(root, relative), source = SafePath(stage, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        if (existing.Contains(relative))
                            File.Replace(source, target, null);
                        else
                            File.Move(source, target);
                        changed.Add(relative);
                        if (afterReplace != null)
                            afterReplace(changed.Count);
                    }
                }
                catch (Exception failure)
                {
                    var rollbackErrors = new List<string>();
                    for (int i = changed.Count - 1; i >= 0; i--)
                    {
                        string relative = changed[i];
                        try
                        {
                            string target = SafePath(root, relative);
                            if (existing.Contains(relative))
                                File.Replace(SafePath(backup, relative), target, null);
                            else
                                File.Delete(target);
                        }
                        catch (Exception ex)
                        {
                            rollbackErrors.Add(ex.Message);
                        }
                    }

                    if (rollbackErrors.Count != 0)
                    {
                        preserveBackup = true;
                        throw new IOException("Update failed. Recovery files were kept at " + backup + ". " + String.Join("; ", rollbackErrors), failure);
                    }

                    throw new IOException("Update failed; the previous program files were restored. " + failure.Message, failure);
                }

                return newInfo.ProductVersion;
            }
            finally
            {
                // This randomly created directory contains only our stage and backups.
                if (!preserveBackup)
                {
                    try
                    {
                        Directory.Delete(workspace, true);
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

        internal static void Show(string directory)
        {
            using (var form = new Form
            {
                Text = "murums Wii Mod Studio — Update",
                ClientSize = new Size(640, 260),
                StartPosition = FormStartPosition.CenterScreen,
                Font = new Font("Segoe UI", 10),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                BackColor = Color.FromArgb(20, 21, 26),
                ForeColor = Color.White
            }

            )
            {
                try
                {
                    form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                }

                var title = new Label
                {
                    Text = "Update murums Wii Mod Studio",
                    Font = new Font("Segoe UI", 17, FontStyle.Bold),
                    Bounds = new Rectangle(24, 22, 590, 42)
                };
                var status = new Label
                {
                    Text = "Close Studio to continue. Your projects, settings and optional tools are kept.",
                    Bounds = new Rectangle(24, 80, 590, 92)
                };
                var install = new Button
                {
                    Text = "Install and restart",
                    Enabled = false,
                    Bounds = new Rectangle(360, 194, 160, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(139, 92, 246)
                };
                var close = new Button
                {
                    Text = "Cancel",
                    Bounds = new Rectangle(530, 194, 86, 38),
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.Cancel
                };
                form.Controls.AddRange(new Control[] { title, status, install, close });
                form.CancelButton = close;
                string root;
                try
                {
                    root = ValidateRoot(directory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var timer = new Timer
                {
                    Interval = 500
                }

                )
                {
                    timer.Tick += delegate
                    {
                        try
                        {
                            install.Enabled = Ready(root);
                            if (install.Enabled)
                                status.Text = "Ready to install the new version. Projects, settings and optional tools are kept.";
                        }
                        catch (Exception ex)
                        {
                            timer.Stop();
                            status.Text = ex.Message;
                        }
                    };
                    install.Click += delegate
                    {
                        timer.Stop();
                        install.Enabled = close.Enabled = false;
                        form.UseWaitCursor = true;
                        bool applied = false;
                        try
                        {
                            string version;
                            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("studio.zip"))
                                version = Apply(root, stream, null);
                            applied = true;
                            try
                            {
                                SetupBundle.Register(root);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("The program was updated, but its Windows registration could not be refreshed. " + ex.Message, form.Text);
                            }

                            Process.Start(new ProcessStartInfo { FileName = Path.Combine(root, PackageFiles[0]), WorkingDirectory = root, UseShellExecute = false });
                            form.Close();
                        }
                        catch (Exception ex)
                        {
                            status.Text = applied ? "Update installed. Start Studio from the Start menu. " + ex.Message : ex.Message;
                            close.Enabled = true;
                            close.Text = "Close";
                            if (!applied)
                                timer.Start();
                        }
                        finally
                        {
                            form.UseWaitCursor = false;
                        }
                    };
                    timer.Start();
                    form.ShowDialog();
                }
            }
        }
    }
}
