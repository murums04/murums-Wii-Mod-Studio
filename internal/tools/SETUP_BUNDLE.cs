using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using murumsWiiModStudio.Setup;

internal static class SetupBundle
{
    internal static void Extract(string destination)
    {
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length != 0)
            throw new IOException("Choose an empty folder. Existing installations and your files will not be overwritten.");
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("studio.zip"))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(entry.FullName))
                    throw new InvalidDataException("Invalid package path.");
            }

            Directory.CreateDirectory(root);
            var manifest = new List<string>();
            foreach (var entry in zip.Entries)
            {
                string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var input = entry.Open())
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                    input.CopyTo(output);
                manifest.Add(entry.FullName);
            }

            File.WriteAllLines(Path.Combine(root, "install-manifest.txt"), manifest.ToArray());
        }
    }

    static string InstallId(string root)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\').ToLowerInvariant()))).Replace("-", "").Substring(0, 16);
    }

    internal static void Register(string root)
    {
        string id = InstallId(root), exe = Path.Combine(root, "murums Wii Mod Studio.exe");
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\murumsWiiStudio-" + id))
        {
            key.SetValue("DisplayName", "murums Wii Mod Studio");
            key.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(exe).ProductVersion);
            key.SetValue("Publisher", "murums");
            key.SetValue("InstallLocation", root);
            key.SetValue("DisplayIcon", exe);
            key.SetValue("UninstallString", "\"" + Path.Combine(root, "Uninstall.exe") + "\" --uninstall");
            key.SetValue("NoModify", 1);
            key.SetValue("NoRepair", 1);
        }

        using (var application = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\murums Wii Mod Studio.exe"))
        {
            application.SetValue("FriendlyAppName", "murums Wii Mod Studio");
            using (var command = application.CreateSubKey(@"shell\open\command"))
                command.SetValue("", QuoteArgument(exe) + " \"%1\"");
        }

        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(shellType);
        object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "murums Wii Mod Studio.lnk") });
        Type type = shortcut.GetType();
        type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
        type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { root });
        type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        string legacyShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "murums Wii Mod Studio " + id + ".lnk");
        if (File.Exists(legacyShortcut))
            File.Delete(legacyShortcut);
    }

    static void StartScript(string script, string root, bool uninstall)
    {
        Process.Start(new ProcessStartInfo { FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"" + (uninstall ? " -Root \"" + root + "\"" : ""), WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }

    // Read registered installations; never run a neighbouring downloaded executable.
    internal static string FindInstalledProgram(RegistryKey registrations)
    {
        if (registrations == null)
            return null;
        string best = null;
        Version bestVersion = null;
        foreach (string name in registrations.GetSubKeyNames())
        {
            if (!name.StartsWith("murumsWiiStudio-", StringComparison.Ordinal))
                continue;
            try
            {
                using (var key = registrations.OpenSubKey(name))
                {
                    string root = key.GetValue("InstallLocation") as string;
                    if (String.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root) || name != "murumsWiiStudio-" + InstallId(root))
                        continue;
                    string exe = Path.GetFullPath(Path.Combine(root, "murums Wii Mod Studio.exe"));
                    if (String.Equals(exe, Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase) || !File.Exists(exe) || !File.Exists(Path.Combine(root, "install-manifest.txt")))
                        continue;
                    var info = FileVersionInfo.GetVersionInfo(exe);
                    Version version;
                    if (info.ProductName != "murums Wii Mod Studio" || info.FileDescription != "murums Wii Mod Studio" || !Version.TryParse(info.FileVersion, out version))
                        continue;
                    if (bestVersion == null || version > bestVersion)
                    {
                        best = exe;
                        bestVersion = version;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        return best;
    }

    static bool LaunchInstalledProgram(string documentPath)
    {
        string exe;
        using (var registrations = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            exe = FindInstalledProgram(registrations);
        if (exe == null)
            return false;
        Version installedVersion, packageVersion;
        if (documentPath == null && Version.TryParse(FileVersionInfo.GetVersionInfo(exe).FileVersion, out installedVersion) && Version.TryParse(FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion, out packageVersion) && packageVersion > installedVersion)
        {
            UpdateInstaller.Show(Path.GetDirectoryName(exe));
            return true;
        }

        Process.Start(CreateLaunchInfo(exe, documentPath));
        return true;
    }

    internal static ProcessStartInfo CreateLaunchInfo(string executable, string documentPath)
    {
        return new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable),
            UseShellExecute = false,
            Arguments = documentPath == null ? "" : QuoteArgument(documentPath)
        };
    }

    internal static string QuoteArgument(string value)
    {
        var result = new StringBuilder();
        result.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
            result.Append(character);
            backslashes = 0;
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');
        return result.ToString();
    }

    internal static string GetDocumentPath(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] == "--setup"))
            return null;
        if (args.Length != 1)
            throw new ArgumentException("Open one file at a time.");
        string path = Path.GetFullPath(args[0]);
        if (!File.Exists(path))
            throw new FileNotFoundException("The selected file could not be found.", path);
        return path;
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--extract-test")
        {
            try
            {
                Extract(args[1]);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 1;
            }
        }

        if (args.Length == 1 && args[0] == "--uninstall")
        {
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            StartScript(Path.Combine(root, "internal", "tools", "UNINSTALL.ps1"), root, true);
            return 0;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && args[0] == "--update")
        {
            UpdateInstaller.Show(args[1]);
            return 0;
        }

        bool setupRequested = args.Length == 1 && args[0] == "--setup";
        if (!setupRequested)
        {
            try
            {
                string documentPath = GetDocumentPath(args);
                if (LaunchInstalledProgram(documentPath))
                    return 0;
                if (documentPath != null)
                {
                    MessageBox.Show("Install murums Wii Mod Studio first, then open this file again. Run the downloaded EXE without a file to install it.",
                        "murums Wii Mod Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Studio could not open the requested file or installation.\n" + e.Message,
                    "murums Wii Mod Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
        Application.Run(new SetupPage());
        return 0;
    }
}
