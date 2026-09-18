using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class CueTextBox : TextBox
    {
        private const int EM_SETCUEBANNER = 0x1501;
        private string _cue = "";
        [System.ComponentModel.Category("Appearance")]
        public string Cue
        {
            get
            {
                return _cue;
            }

            set
            {
                _cue = value == null ? "" : value;
                ApplyCue();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCue();
        }

        private void ApplyCue()
        {
            if (IsHandleCreated && !Multiline)
                SendMessage(Handle, EM_SETCUEBANNER, (IntPtr)1, _cue);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }

    internal sealed class EmptyStatePanel : Panel
    {
        private readonly Label _label;
        public EmptyStatePanel()
        {
            BackColor = DarkTheme.Panel;
            _label = new Label();
            _label.Dock = DockStyle.Fill;
            _label.TextAlign = ContentAlignment.MiddleCenter;
            _label.ForeColor = DarkTheme.Muted;
            _label.Font = new Font("Segoe UI", 11F, FontStyle.Italic);
            Controls.Add(_label);
        }

        public string Message
        {
            get
            {
                return _label.Text;
            }

            set
            {
                _label.Text = value;
            }
        }
    }
}
