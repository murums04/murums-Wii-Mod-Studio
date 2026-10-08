using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioHelpForm : Form
    {
        private readonly Panel header;

        public StudioHelpForm()
        {
            Text = L.T("Studio-Hilfe", "Studio help");
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1080, 760);
            MinimumSize = new Size(850, 580);
            StartPosition = FormStartPosition.CenterParent;
            var body = new Panel { Dock = DockStyle.Fill };
            StudioHelpCards.Build(body, new RichTextBox());
            Controls.Add(body);
            header = StudioChrome.Header(Text, L.T("Konkrete Beispiele und echte Programmbilder • Quelle, Bearbeitung und Ausgabe", "Worked examples and actual program pictures • Source, edit and output"));
            header.Dock = DockStyle.Top;
            header.Height = StudioChrome.HeaderHeight;
            Controls.Add(header);
            DarkTheme.Apply(this);
        }

        internal void PrepareWorkspace()
        {
            header.Visible = false;
        }
    }

    internal static class StudioHelpWindow
    {
        public static void Show(Form owner, Control content, string title)
        {
            var dialog = new Form
            {
                Text = title,
                Size = new Size(1080, 760),
                MinimumSize = new Size(850, 580),
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Segoe UI", 10),
                AutoScaleMode = AutoScaleMode.Font,
                ShowInTaskbar = false
            };
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
                Padding = Padding.Empty
            };
            dialog.Controls.Add(host);
            var header = StudioChrome.Header(title, L.T("Thema suchen • Schritte durchgehen • Kopie prüfen", "Find a topic • Follow the steps • Review your copy"));
            header.Dock = DockStyle.Top;
            header.Height = StudioChrome.HeaderHeight;
            dialog.Controls.Add(header);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8), WrapContents = false };
            var close = StudioChrome.ActionButton(L.T("Schließen", "Close"));
            close.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(close);
            dialog.Controls.Add(footer);
            dialog.CancelButton = close;
            var controls = new Control[content.Controls.Count];
            content.Controls.CopyTo(controls, 0);
            Action restore = delegate {
                if (content.IsDisposed) return;
                foreach (var c in controls)
                    if (!c.IsDisposed && c.Parent == host) content.Controls.Add(c);
            };
            dialog.FormClosing += delegate { restore(); };
            try
            {
                foreach (var c in controls)
                    host.Controls.Add(c);
                DarkTheme.Apply(dialog);
                if (owner != null && owner.TopLevelControl is MainForm) header.Visible = false;
                StudioEditor.Open(owner, dialog, delegate { restore(); });
            }
            catch
            {
                restore();
                dialog.Dispose();
                throw;
            }
        }
    }
}
