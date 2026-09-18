using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

static class UninstallLauncher
{
    [STAThread]
    static void Main()
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Process.Start(new ProcessStartInfo { FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Path.Combine(root, "internal", "tools", "UNINSTALL.ps1") + "\" -Root \"" + root + "\"", WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
