using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class Prompt
    {
        public static string Show(IWin32Window owner, string title, string message, string initialValue)
        {
            using (Form form = new Form())
            using (Label label = new Label())
            using (TextBox textBox = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(470, 150);
                form.BackColor = DarkTheme.Back;
                form.ForeColor = DarkTheme.Fore;
                form.Font = new Font("Segoe UI", 10.5F);
                try
                {
                    form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                }

                label.Text = message;
                label.Left = 14;
                label.Top = 14;
                label.Width = 440;
                label.ForeColor = DarkTheme.Fore;
                textBox.Left = 14;
                textBox.Top = 44;
                textBox.Width = 440;
                textBox.Text = initialValue ?? string.Empty;
                textBox.BackColor = DarkTheme.Panel2;
                textBox.ForeColor = DarkTheme.Fore;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                ok.Text = "OK";
                ok.Left = 282;
                ok.Top = 96;
                ok.Width = 80;
                ok.Height = 32;
                ok.DialogResult = DialogResult.OK;
                StyleButton(ok, true);
                cancel.Text = L.T("Abbrechen", "Cancel");
                cancel.Left = 374;
                cancel.Top = 96;
                cancel.Width = 80;
                cancel.Height = 32;
                cancel.DialogResult = DialogResult.Cancel;
                StyleButton(cancel, false);
                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                DialogResult result = form.ShowDialog(owner);
                return result == DialogResult.OK ? textBox.Text : null;
            }
        }

        private static void StyleButton(Button button, bool accent)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = accent ? DarkTheme.Accent : DarkTheme.Border;
            button.BackColor = accent ? DarkTheme.Accent2 : DarkTheme.Panel2;
            button.ForeColor = DarkTheme.Fore;
        }
    }
}
