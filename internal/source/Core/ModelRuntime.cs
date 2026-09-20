using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace murumsWiiModStudio
{
    internal static class ModelRuntime
    {
        internal static string RootOverride, BlenderOverride;
        internal static string Root { get { return RootOverride ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "internal", "model"); } }
        internal static string NewWorkFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "murums-models", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
        internal static string Prepare(string source, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (new FileInfo(source).Length > 512L * 1024 * 1024) throw new InvalidDataException("Model exceeds the 512 MB import limit.");
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (extension != ".blend" && extension != ".usdz") return source;
            string work = NewWorkFolder();
            string script = Path.Combine(work, "import.py");
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Studio.ModelImport.py"))
            {
                if (input == null) throw new IOException("Internal model script missing.");
                using (var output = File.Create(script)) input.CopyTo(output);
            }
            string destination = Path.Combine(work, "model.glb");
            Run(Blender(cancellation), "--background --factory-startup --disable-autoexec --offline-mode --python-exit-code 1 --python "
                + Quote(script) + " -- " + Quote(Path.GetFullPath(source)) + " " + Quote(destination), work, cancellation);
            if (!File.Exists(destination) || new FileInfo(destination).Length < 20)
                throw new InvalidDataException(L.T("Das Modell konnte intern nicht konvertiert werden.", "The model could not be converted internally."));
            return destination;
        }
        internal static string SimplifyPreview(string source, CancellationToken cancellation)
        {
            string work = NewWorkFolder();
            if (Path.GetExtension(source).Equals(".dae", StringComparison.OrdinalIgnoreCase))
            {
                string converted = Path.Combine(work, "source.glb");
                IntegratedModelImport.ExportCopy(source, converted, 1, "glb2");
                source = converted;
            }
            string destination = Path.Combine(work, "preview.glb");
            ModelRig.RunScript("ModelImport.py", work, cancellation, Path.GetFullPath(source), destination, "50000");
            if (!File.Exists(destination)) throw new InvalidDataException("No simplified preview was created.");
            return destination;
        }
        internal static string Blender(CancellationToken cancellation)
        {
            if (!String.IsNullOrEmpty(BlenderOverride)) return BlenderOverride;
            string folder = Path.Combine(Path.GetTempPath(), "murums-models", "blender-5.2.2");
            string executable = Path.Combine(folder, "blender.exe");
            string marker = Path.Combine(folder, "complete.txt");
            if (File.Exists(marker) && File.Exists(executable)) return executable;
            string archive = Path.Combine(Root, "blender-5.2.2-windows-x64.zip");
            if (!File.Exists(archive)) throw new IOException(L.T("Interne Blender-Komponente fehlt. Bitte den vollständigen Studio-Installer verwenden.", "Internal Blender component missing. Use the complete Studio installer."));
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(archive))
                if (BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") != "3849D17A682CBA006075AAA3F3597ECB5C9C30EC31035B2E092C53E40679B535")
                    throw new IOException("Internal Blender archive checksum mismatch.");
            string stage = folder + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(stage);
            try
            {
                using (var zip = ZipFile.OpenRead(archive))
                    foreach (var entry in zip.Entries)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        const string archiveRoot = "blender-5.2.2-windows-x64/";
                        if (!entry.FullName.StartsWith(archiveRoot, StringComparison.Ordinal)) throw new IOException("Unexpected runtime archive root.");
                        string relative = entry.FullName.Substring(archiveRoot.Length);
                        if (relative.Length == 0) continue;
                        // Der kurze Cache-Pfad verhindert Windows-Pfadlängenfehler beim ersten Start.
                        string target = Path.GetFullPath(Path.Combine(stage, relative));
                        if (!target.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid runtime archive path.");
                        if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(target); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (var input = entry.Open()) using (var output = new FileStream(target, FileMode.CreateNew)) input.CopyTo(output);
                    }
                File.WriteAllText(Path.Combine(stage, "complete.txt"), "Blender 5.2.2");
                if (Directory.Exists(folder) && !File.Exists(marker))
                    Directory.Move(folder, folder + "-incomplete-" + Guid.NewGuid().ToString("N"));
                if (!Directory.Exists(folder)) Directory.Move(stage, folder);
                if (!File.Exists(marker)) throw new IOException("Incomplete internal model runtime. Restart Studio and retry.");
                return executable;
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        internal static void Run(string executable, string arguments, string work, CancellationToken cancellation)
        {
            var info = new ProcessStartInfo(executable, arguments) { WorkingDirectory = work, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            info.EnvironmentVariables["BLENDER_USER_CONFIG"] = Path.Combine(work, "config");
            info.EnvironmentVariables["BLENDER_USER_SCRIPTS"] = Path.Combine(work, "scripts");
            var log = new StringBuilder();
            using (var process = new Process { StartInfo = info })
            {
                DataReceivedEventHandler receive = delegate(object sender, DataReceivedEventArgs args) {
                    if (args.Data != null) lock (log) { log.AppendLine(args.Data); if (log.Length > 24000) log.Remove(0, log.Length - 24000); }
                };
                process.OutputDataReceived += receive; process.ErrorDataReceived += receive;
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                var timer = Stopwatch.StartNew();
                while (!process.WaitForExit(100))
                    if (cancellation.IsCancellationRequested || timer.Elapsed.TotalMinutes > 5)
                    {
                        process.Kill(); process.WaitForExit();
                        cancellation.ThrowIfCancellationRequested();
                        throw new TimeoutException(L.T("Modellimport nach fünf Minuten abgebrochen.", "Model import stopped after five minutes."));
                    }
                process.WaitForExit();
                File.WriteAllText(Path.Combine(work, "conversion.log"), log.ToString());
                if (process.ExitCode != 0) throw new InvalidDataException(L.T("Interner Modellimport fehlgeschlagen. Details: ", "Internal model import failed. Details: ") + Path.Combine(work, "conversion.log"));
            }
        }
        internal static string Quote(string value)
        {
            if (value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0) throw new ArgumentException("Invalid path.");
            return "\"" + value.TrimEnd('\\') + "\"";
        }
    }
}
