using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    ShowFatalError("UI error", e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    if (ex != null)
                        WriteCrashLog("UnhandledException", ex);
                };
                AppSettings.Load();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                StudioUx.Install();
                MainForm form = new MainForm();
                if (args != null && args.Length > 0 && File.Exists(args[0]))
                    form.OpenFromPath(args[0]);
                Application.Run(form);
            }
            catch (Exception ex)
            {
                ShowFatalError("Startup error", ex);
            }
        }

        private static void ShowFatalError(string title, Exception ex)
        {
            string logPath = WriteCrashLog(title, ex);
            try
            {
                murumsWiiModStudio.StudioMessageBox.Show(title + " in murums Wii Mod Studio.\r\n\r\n" + ex.Message + "\r\n\r\n" + "A diagnostic log was written to:\r\n" + logPath, "murums Wii Mod Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        private static string WriteCrashLog(string context, Exception ex)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(baseDir, "CRASH_LOG.txt");
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("murums Wii Mod Studio crash log");
                sb.AppendLine("================================");
                sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("Context: " + context);
                sb.AppendLine("OS: " + Environment.OSVersion);
                sb.AppendLine(".NET: " + Environment.Version);
                sb.AppendLine();
                sb.AppendLine(ex.ToString());
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                return path;
            }
            catch
            {
                return "(crash log could not be written)";
            }
        }
    }
}
