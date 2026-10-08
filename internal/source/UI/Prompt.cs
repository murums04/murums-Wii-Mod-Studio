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

                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                label.Text = message;
                label.AutoSize = true; label.MaximumSize = new Size(438, 0); label.Dock = DockStyle.Fill;
                label.Margin = new Padding(0, 0, 0, 8);
                textBox.Text = initialValue ?? string.Empty;
                textBox.Dock = DockStyle.Fill;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 12, 0, 0) };
                ok.Text = "OK";
                ok.AutoSize = cancel.AutoSize = true;
                ok.MinimumSize = cancel.MinimumSize = new Size(100, 36);
                ok.DialogResult = DialogResult.OK;
                cancel.Text = L.T("Abbrechen", "Cancel");
                cancel.DialogResult = DialogResult.Cancel;
                actions.Controls.Add(ok); actions.Controls.Add(cancel);
                layout.Controls.Add(label, 0, 0); layout.Controls.Add(textBox, 0, 1); layout.Controls.Add(actions, 0, 2);
                form.Controls.Add(layout);
                form.ClientSize = new Size(470, System.Math.Max(170, layout.GetPreferredSize(new Size(470, 0)).Height));
                DarkTheme.Apply(form);
                StyleButton(ok, true); StyleButton(cancel, false);
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
            if (accent) DarkTheme.StylePrimary(button);
        }
    }
}
