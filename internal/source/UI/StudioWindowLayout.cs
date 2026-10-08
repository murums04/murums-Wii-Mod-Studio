using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace murumsWiiModStudio
{
    internal static class StudioWindowLayout
    {
        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint action, uint parameter, out bool value, uint flags);

        internal static bool MotionEnabled
        {
            get
            {
                bool enabled;
                return !SystemInformation.HighContrast && SystemParametersInfo(0x1042, 0, out enabled, 0) && enabled;
            }
        }

        internal static void Attach(Form form)
        {
            form.Shown += delegate
            {
                if (!form.TopLevel || form is HoverHintWindow) return;
                Form owner = form.Owner;
                bool editor = form.FormBorderStyle == FormBorderStyle.Sizable && form.MinimumSize.Width >= 780 && form.MinimumSize.Height >= 500;
                if (editor && owner != null && owner.WindowState == FormWindowState.Maximized)
                    form.WindowState = FormWindowState.Maximized;
                KeepVisible(form);
            };
            EventHandler displayChanged = delegate
            {
                if (!form.IsDisposed && form.IsHandleCreated && form.TopLevel)
                    form.BeginInvoke((Action)delegate { if (!form.IsDisposed) KeepVisible(form); });
            };
            SystemEvents.DisplaySettingsChanged += displayChanged;
            form.Disposed += delegate { SystemEvents.DisplaySettingsChanged -= displayChanged; };
        }

        internal static void KeepVisible(Form form)
        {
            if (!form.TopLevel || form is HoverHintWindow) return;
            Rectangle work = Screen.FromControl(form.Owner ?? form).WorkingArea;
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, work.Width), Math.Min(form.MinimumSize.Height, work.Height));
            if (form.WindowState != FormWindowState.Normal) return;
            Size size = new Size(Math.Min(form.Width, work.Width), Math.Min(form.Height, work.Height));
            int x = Math.Max(work.Left, Math.Min(form.Left, work.Right - size.Width));
            int y = Math.Max(work.Top, Math.Min(form.Top, work.Bottom - size.Height));
            form.Bounds = new Rectangle(new Point(x, y), size);
        }
    }
}
