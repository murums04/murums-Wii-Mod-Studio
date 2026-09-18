using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioHelpWindow
    {
        public static void Show(Form owner, Control content, string title)
        {
            using (var dialog = new Form
            {
                Text = title,
                Size = new Size(1080, 760),
                MinimumSize = new Size(850, 580),
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Segoe UI", 10)
            }

            )
            {
                try
                {
                    dialog.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                }

                var host = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(14)
                };
                dialog.Controls.Add(host);
                var header = StudioChrome.Header(title, L.T("Thema suchen • Schritte durchgehen • Kopie prüfen", "Find a topic • Follow the steps • Review your copy"));
                header.Dock = DockStyle.Top;
                header.Height = 118;
                dialog.Controls.Add(header);
                var close = new Button
                {
                    Text = L.T("Schließen", "Close"),
                    Dock = DockStyle.Bottom,
                    Height = 36,
                    DialogResult = DialogResult.Cancel
                };
                dialog.Controls.Add(close);
                dialog.CancelButton = close;
                var controls = new Control[content.Controls.Count];
                content.Controls.CopyTo(controls, 0);
                try
                {
                    foreach (var c in controls)
                        host.Controls.Add(c);
                    DarkTheme.Apply(dialog);
                    dialog.ShowDialog(owner);
                }
                finally
                {
                    foreach (var c in controls)
                        content.Controls.Add(c);
                }
            }
        }
    }
}
