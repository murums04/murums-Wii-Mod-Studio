using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace murumsWiiModStudio
{
    internal sealed class ToolDescriptor
    {
        public string Id;
        public string DisplayName;
        public string[] Executables;
        public string Purpose;
        public string Website;
    }

    internal static class ToolchainManager
    {
        private static readonly Dictionary<string, string> CustomPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        public static readonly ToolDescriptor[] KnownTools = new ToolDescriptor[]
        {
            new ToolDescriptor
            {
                Id = "wszst",
                DisplayName = "Wiimms SZS Tool",
                Executables = new string[]
                {
                    "wszst.exe",
                    "wszst"
                },
                Purpose = "SZS/U8/BRRES/BREFF/BREFT archives",
                Website = "https://szs.wiimm.de/"
            },
            new ToolDescriptor
            {
                Id = "wimgt",
                DisplayName = "Wiimms Image Tool",
                Executables = new string[]
                {
                    "wimgt.exe",
                    "wimgt"
                },
                Purpose = "TPL/TEX0/BTI/PNG image conversion",
                Website = "https://szs.wiimm.de/wimgt/"
            },
            new ToolDescriptor
            {
                Id = "wkmpt",
                DisplayName = "Wiimms KMP Tool",
                Executables = new string[]
                {
                    "wkmpt.exe",
                    "wkmpt"
                },
                Purpose = "KMP validation/conversion",
                Website = "https://szs.wiimm.de/wkmpt/"
            },
            new ToolDescriptor
            {
                Id = "wkclt",
                DisplayName = "Wiimms KCL Tool",
                Executables = new string[]
                {
                    "wkclt.exe",
                    "wkclt"
                },
                Purpose = "KCL validation/OBJ conversion",
                Website = "https://szs.wiimm.de/wkclt/"
            },
            new ToolDescriptor
            {
                Id = "wbmgt",
                DisplayName = "Wiimms BMG Tool",
                Executables = new string[]
                {
                    "wbmgt.exe",
                    "wbmgt"
                },
                Purpose = "BMG decode/edit/encode",
                Website = "https://szs.wiimm.de/wbmgt/"
            },
            new ToolDescriptor
            {
                Id = "wpatt",
                DisplayName = "Wiimms PAT Tool",
                Executables = new string[]
                {
                    "wpatt.exe",
                    "wpatt"
                },
                Purpose = "PAT0 texture-pattern animation conversion",
                Website = "https://szs.wiimm.de/wpatt/"
            },
            new ToolDescriptor
            {
                Id = "wstrt",
                DisplayName = "Wiimms StaticR Tool",
                Executables = new string[]
                {
                    "wstrt.exe",
                    "wstrt"
                },
                Purpose = "main.dol / StaticR.rel patches",
                Website = "https://szs.wiimm.de/wstrt/"
            },
            new ToolDescriptor
            {
                Id = "wctct",
                DisplayName = "Wiimms CT-CODE Tool",
                Executables = new string[]
                {
                    "wctct.exe",
                    "wctct"
                },
                Purpose = "CT-CODE / LE-CODE conversion",
                Website = "https://szs.wiimm.de/wctct/"
            },
            new ToolDescriptor
            {
                Id = "wlect",
                DisplayName = "Wiimms LE-CODE Tool",
                Executables = new string[]
                {
                    "wlect.exe",
                    "wlect"
                },
                Purpose = "LE-CODE / LEX / LPAR workflows",
                Website = "https://szs.wiimm.de/wlect/"
            },
            new ToolDescriptor
            {
                Id = "wmdlt",
                DisplayName = "Wiimms MDL Tool",
                Executables = new string[]
                {
                    "wmdlt.exe",
                    "wmdlt"
                },
                Purpose = "MDL0 analysis / text conversion",
                Website = "https://szs.wiimm.de/wmdlt/"
            },
            new ToolDescriptor
            {
                Id = "wit",
                DisplayName = "Wiimms ISO Tool",
                Executables = new string[]
                {
                    "wit.exe",
                    "wit"
                },
                Purpose = "Wii/GameCube ISO, WBFS, WIA, GCZ and FST workflows",
                Website = "https://wit.wiimm.de/"
            },
            new ToolDescriptor
            {
                Id = "RiiStudio",
                DisplayName = "RiiStudio",
                Executables = new string[]
                {
                    "RiiStudio.exe",
                    "riistudio.exe",
                    "RiiStudio"
                },
                Purpose = "BRRES/MDL0/TEX0 models, animations and KMP",
                Website = "https://github.com/snailspeed3/RiiStudio"
            },
            new ToolDescriptor
            {
                Id = "rszst",
                DisplayName = "RiiStudio CLI (rszst)",
                Executables = new string[]
                {
                    "rszst.exe",
                    "rszst"
                },
                Purpose = "BRRES import/optimize, KMP/KCL JSON and SZS workflows",
                Website = "https://github.com/snailspeed3/RiiStudio"
            },
            new ToolDescriptor
            {
                Id = "SwitchToolbox",
                DisplayName = "Switch Toolbox",
                Executables = new string[]
                {
                    "Toolbox.exe",
                    "SwitchToolbox.exe",
                    "Switch Toolbox.exe"
                },
                Purpose = "Optional layout/model fallback and reference editor",
                Website = "https://github.com/KillzXGaming/Switch-Toolbox"
            },
            new ToolDescriptor
            {
                Id = "BrawlCrate",
                DisplayName = "BrawlCrate",
                Executables = new string[]
                {
                    "BrawlCrate.exe"
                },
                Purpose = "BRRES/BRSAR/BRSTM/DOL/REL specialist fallback",
                Website = "https://github.com/soopercool101/BrawlCrate"
            },
            new ToolDescriptor
            {
                Id = "LoopingAudioConverter",
                DisplayName = "Looping Audio Converter",
                Executables = new string[]
                {
                    "LoopingAudioConverter.exe"
                },
                Purpose = "WAV loop markers to BRSTM and other looping audio formats",
                Website = "https://github.com/libertyernie/LoopingAudioConverter"
            },
            new ToolDescriptor
            {
                Id = "NintyFont",
                DisplayName = "NintyFont",
                Executables = new string[]
                {
                    "NintyFont.exe",
                    "nintyfont.exe"
                },
                Purpose = "Specialist Nintendo bitmap font editor (including BRFNT)",
                Website = "https://github.com/hadashisora/NintyFont"
            },
            new ToolDescriptor
            {
                Id = "ffmpeg",
                DisplayName = "FFmpeg",
                Executables = new string[]
                {
                    "ffmpeg.exe",
                    "ffmpeg"
                },
                Purpose = "Audio/video conversion backend",
                Website = "https://ffmpeg.org/"
            }
        };
        public static string Find(ToolDescriptor tool)
        {
            if (tool == null)
                return null;
            EnsureLoaded();
            string custom;
            if (CustomPaths.TryGetValue(tool.Id, out custom) && File.Exists(custom))
                return custom;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localTools = Path.Combine(baseDir, "internal", "tools");
            string found = FindInDirectory(localTools, tool, true);
            if (found != null)
                return found;
            // Backward compatibility with older builds that used a top-level tools folder.
            string legacyTools = Path.Combine(baseDir, "tools");
            found = FindInDirectory(legacyTools, tool, true);
            if (found != null)
                return found;
            found = FindInDirectory(baseDir, tool, false);
            if (found != null)
                return found;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] dirs = path.Split(Path.PathSeparator);
            for (int d = 0; d < dirs.Length; d++)
            {
                string dir = dirs[d].Trim().Trim('"');
                if (dir.Length == 0)
                    continue;
                found = FindInDirectory(dir, tool, false);
                if (found != null)
                    return found;
            }

            found = FindRegisteredApp(tool);
            if (found != null)
                return found;
            foreach (string dir in CommonDirectories(tool))
            {
                found = FindInDirectory(dir, tool, false);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static string FindInDirectory(string dir, ToolDescriptor tool, bool subfolders)
        {
            return FindInDirectoryRecursive(dir, tool, subfolders ? 10 : 0);
        }

        private static string FindInDirectoryRecursive(string dir, ToolDescriptor tool, int depth)
        {
            if (String.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return null;
            for (int i = 0; i < tool.Executables.Length; i++)
            {
                try
                {
                    string candidate = Path.Combine(dir, tool.Executables[i]);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            if (depth <= 0)
                return null;
            try
            {
                string[] children = Directory.GetDirectories(dir);
                for (int c = 0; c < children.Length; c++)
                {
                    string found = FindInDirectoryRecursive(children[c], tool, depth - 1);
                    if (found != null)
                        return found;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string FindRegisteredApp(ToolDescriptor tool)
        {
            if (tool == null || tool.Executables == null)
                return null;
            RegistryKey[] roots = new RegistryKey[]
            {
                Registry.CurrentUser,
                Registry.LocalMachine
            };
            string[] prefixes = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\"
            };
            for (int r = 0; r < roots.Length; r++)
            {
                for (int p = 0; p < prefixes.Length; p++)
                {
                    for (int i = 0; i < tool.Executables.Length; i++)
                    {
                        string exe = tool.Executables[i];
                        if (String.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            continue;
                        try
                        {
                            using (RegistryKey key = roots[r].OpenSubKey(prefixes[p] + exe))
                            {
                                if (key == null)
                                    continue;
                                string value = key.GetValue(null) as string;
                                if (!String.IsNullOrWhiteSpace(value))
                                {
                                    value = value.Trim().Trim('"');
                                    if (File.Exists(value))
                                        return value;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> CommonDirectories(ToolDescriptor tool)
        {
            List<string> dirs = new List<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!String.IsNullOrEmpty(pf))
            {
                dirs.Add(Path.Combine(pf, tool.DisplayName));
                dirs.Add(Path.Combine(pf, "Wiimm", "SZS"));
                dirs.Add(Path.Combine(pf, "Wiimm", "WIT"));
            }

            if (!String.IsNullOrEmpty(pfx86))
            {
                dirs.Add(Path.Combine(pfx86, tool.DisplayName));
                dirs.Add(Path.Combine(pfx86, "Wiimm", "SZS"));
                dirs.Add(Path.Combine(pfx86, "Wiimm", "WIT"));
            }

            if (!String.IsNullOrEmpty(local))
            {
                dirs.Add(Path.Combine(local, "Programs", tool.DisplayName));
                dirs.Add(Path.Combine(local, tool.DisplayName));
                dirs.Add(Path.Combine(local, "Microsoft", "WinGet", "Links"));
            }

            if (!String.IsNullOrEmpty(desktop))
                dirs.Add(desktop);
            if (!String.IsNullOrEmpty(user))
            {
                dirs.Add(Path.Combine(user, "Downloads"));
                dirs.Add(Path.Combine(user, "Documents"));
                dirs.Add(Path.Combine(user, "scoop", "shims"));
            }

            return dirs;
        }

        public static void SetCustomPath(ToolDescriptor tool, string path)
        {
            if (tool == null)
                return;
            EnsureLoaded();
            if (String.IsNullOrWhiteSpace(path))
                CustomPaths.Remove(tool.Id);
            else
                CustomPaths[tool.Id] = path;
            SaveCustomPaths();
        }

        public static void ClearCustomPath(ToolDescriptor tool)
        {
            if (tool == null)
                return;
            EnsureLoaded();
            if (CustomPaths.Remove(tool.Id))
                SaveCustomPaths();
        }

        public static string GetCustomPath(ToolDescriptor tool)
        {
            EnsureLoaded();
            string value;
            return tool != null && CustomPaths.TryGetValue(tool.Id, out value) ? value : null;
        }

        private static string ConfigPath
        {
            get
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "murums Wii Mod Studio");
                try
                {
                    Directory.CreateDirectory(root);
                }
                catch
                {
                }

                return Path.Combine(root, "toolchain_paths.ini");
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                if (!File.Exists(ConfigPath))
                    return;
                string[] lines = File.ReadAllLines(ConfigPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string id = line.Substring(0, eq).Trim();
                    string path = line.Substring(eq + 1).Trim();
                    if (id.Length > 0 && path.Length > 0)
                        CustomPaths[id] = path;
                }
            }
            catch
            {
            }
        }

        private static void SaveCustomPaths()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> pair in CustomPaths)
                    lines.Add(pair.Key + "=" + pair.Value);
                File.WriteAllLines(ConfigPath, lines.ToArray());
            }
            catch
            {
            }
        }

        public static ToolDescriptor Recommended(ResourceKind kind)
        {
            if (kind == ResourceKind.Kmp)
                return FindById("wkmpt");
            if (kind == ResourceKind.Kcl)
                return FindById("wkclt");
            if (kind == ResourceKind.Bmg)
                return FindById("wbmgt");
            if (kind == ResourceKind.Brres)
                return FindById("RiiStudio");
            if (kind == ResourceKind.U8Archive)
                return FindById("wszst");
            if (kind == ResourceKind.Tpl || kind == ResourceKind.Bti || kind == ResourceKind.Image)
                return FindById("wimgt");
            if (kind == ResourceKind.Nw4rResource)
                return FindById("RiiStudio");
            if (kind == ResourceKind.NintendoAudioResource || kind == ResourceKind.Brstm || kind == ResourceKind.Brsar)
                return FindById("BrawlCrate");
            if (kind == ResourceKind.Lex)
                return FindById("wlect");
            if (kind == ResourceKind.Rel || kind == ResourceKind.Dol)
                return FindById("wstrt");
            if (kind == ResourceKind.DiscImage)
                return FindById("wit");
            if (kind == ResourceKind.RarcArchive || kind == ResourceKind.J3dModel || kind == ResourceKind.JParticle)
                return FindById("RiiStudio");
            if (kind == ResourceKind.Breff || kind == ResourceKind.Breft)
                return FindById("wszst");
            return null;
        }

        public static ToolDescriptor FindById(string id)
        {
            for (int i = 0; i < KnownTools.Length; i++)
                if (String.Equals(KnownTools[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return KnownTools[i];
            return null;
        }

        public static bool Launch(ToolDescriptor tool, string filePath, bool wait, out string error)
        {
            error = null;
            string exe = Find(tool);
            if (String.IsNullOrEmpty(exe))
            {
                error = tool.DisplayName + " was not found. Use Tools > Toolchain > Locate selected, put it in internal\tools, use Tools > Toolchain > Locate selected, or add it to PATH.";
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = String.IsNullOrEmpty(filePath) ? "" : Quote(filePath);
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                psi.UseShellExecute = true;
                using (Process p = Process.Start(psi))
                {
                    if (wait && p != null)
                    {
                        p.WaitForExit();
                        if (p.ExitCode != 0)
                        {
                            error = tool.DisplayName + " exited with code " + p.ExitCode + ".";
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RunCapture(ToolDescriptor tool, string arguments, out string output, out string error)
        {
            output = null;
            error = null;
            string exe = Find(tool);
            if (String.IsNullOrEmpty(exe))
            {
                error = tool.DisplayName + " was not found. Use Tools > Toolchain > Locate selected.";
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = arguments ?? "";
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                try
                {
                    psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                    psi.StandardErrorEncoding = System.Text.Encoding.UTF8;
                }
                catch
                {
                }

                using (Process p = Process.Start(psi))
                {
                    // Drain both pipes concurrently; sequential reads deadlock
                    // when a tool fills stderr while stdout is still open.
                    System.Threading.Tasks.Task<string> stdoutTask = p.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> stderrTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(120000))
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch
                        {
                        }

                        error = "Tool timed out after 2 minutes.";
                        return false;
                    }

                    string stdout = stdoutTask.Result;
                    string stderr = stderrTask.Result;
                    output = stdout + (String.IsNullOrEmpty(stderr) ? "" : Environment.NewLine + stderr);
                    if (p.ExitCode != 0)
                        error = "Tool exited with code " + p.ExitCode + "." + (String.IsNullOrWhiteSpace(stderr) ? "" : Environment.NewLine + stderr.Trim());
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool InstallTool(ToolDescriptor tool, out string error)
        {
            error = null;
            if (tool == null)
            {
                error = "No tool selected.";
                return false;
            }

            return RunBundledInstaller("-ToolId " + Quote(tool.Id), out error);
        }

        public static bool InstallAllMissing(out string error)
        {
            List<string> missing = new List<string>();
            for (int i = 0; i < KnownTools.Length; i++)
            {
                if (Find(KnownTools[i]) == null)
                    missing.Add(KnownTools[i].Id);
            }

            if (missing.Count == 0)
            {
                error = null;
                return true;
            }

            return RunBundledInstaller("-ToolId " + Quote(String.Join(",", missing.ToArray())), out error);
        }

        private static bool RunBundledInstaller(string arguments, out string error)
        {
            error = null;
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "internal", "tools", "INSTALL_TOOLCHAIN.ps1");
            if (!File.Exists(script))
            {
                error = "Bundled toolchain installer was not found: " + script;
                return false;
            }

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string powershell = Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(powershell))
                powershell = "powershell.exe";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = powershell;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " + Quote(script) + " " + (arguments ?? "");
                psi.WorkingDirectory = Path.GetDirectoryName(script);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                Process p = Process.Start(psi);
                if (p == null)
                {
                    error = "Could not start the toolchain installer.";
                    return false;
                }

                System.Threading.Tasks.Task<string> stdoutTask = p.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> stderrTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(360000))
                {
                    try
                    {
                        p.Kill();
                    }
                    catch
                    {
                    }

                    error = "Toolchain installation timed out after 6 minutes.";
                    return false;
                }

                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;
                if (p.ExitCode != 0)
                {
                    string details = String.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                    error = "Toolchain installer exited with code " + p.ExitCode + "." + (String.IsNullOrWhiteSpace(details) ? "" : Environment.NewLine + details.Trim());
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string QuoteArgument(string value)
        {
            return Quote(value);
        }

        private static string Quote(string value)
        {
            if (value == null)
                return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
