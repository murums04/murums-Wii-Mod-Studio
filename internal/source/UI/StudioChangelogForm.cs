using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioChangelogForm : Form
    {
        public StudioChangelogForm()
        {
            Text = "Changelog — murums Wii Mod Studio";
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(740, 580);
            MinimumSize = new Size(580, 440);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            Padding = new Padding(20);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = L.T("Was ist neu?", "What's new?"),
                Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                UseMnemonic = false
            }, 0, 0);

            var history = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                DetectUrls = false,
                Text = HistoryText()
            };
            layout.Controls.Add(history, 0, 1);
            var close = new Button
            {
                Text = L.T("Schliessen", "Close"),
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right,
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                FlatStyle = FlatStyle.Flat
            };
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, 8, 0, 0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var profile = new LinkLabel
            {
                Text = "github.com/murums04",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 4, 12, 4),
                LinkColor = Color.FromArgb(190, 166, 255),
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.FromArgb(190, 166, 255)
            };
            profile.LinkClicked += delegate
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/murums04") { UseShellExecute = true });
            };
            actions.Controls.Add(profile, 0, 0);
            actions.Controls.Add(close, 1, 0);
            layout.Controls.Add(actions, 0, 2);
            Controls.Add(layout);
            AcceptButton = close;
            CancelButton = close;
            DarkTheme.Apply(this);

            using (var headingFont = new Font(Font, FontStyle.Bold))
            {
                foreach (string heading in new[] { CurrentHeading(), "2.1.0-beta1 — Release" })
                {
                    int index = history.Text.IndexOf(heading, StringComparison.Ordinal);
                    if (index < 0)
                        continue;
                    history.Select(index, heading.Length);
                    history.SelectionFont = headingFont;
                    history.SelectionColor = DarkTheme.Accent;
                }
            }
            history.Select(0, 0);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Owner != null)
                Icon = Owner.Icon;
        }

        private static string CurrentHeading()
        {
            return L.T("2.1.0-beta2 — Aktueller Stand", "2.1.0-beta2 — Current version");
        }

        internal static string HistoryText()
        {
            return CurrentHeading() + "\n\n" + L.T(
                "• Updates: Prüfung beim Programmstart und Hinweis bei einer neueren Version. Manuell unter Hilfe → Nach Updates suchen.\n\n"
                + "• Update-Installation: geprüfte Downloads; Projekte, Einstellungen und optionale Tools bleiben erhalten.\n\n"
                + "• Dateien öffnen: Ein Doppelklick auf zugeordnete Dateien wie .szs öffnet sie im installierten Studio statt im Setup.\n\n"
                + "• Einheitliche Namen: Alle acht Einträge im Tools-Menü tragen den Präfix MKWii.\n\n"
                + "• Modellquellen: Earth.szs, BackModel.szs und globe.arc aus einer eigenen Mario-Kart-Wii-ISO/WBFS importieren und lokal wiederverwenden. Benötigt Wiimms ISO Tools.\n\n"
                + "• Changelog direkt in der App unter Hilfe → Changelog.\n\n",
                "• Updates: Checks at startup and a notice when a newer version is available. Check manually under Help → Check for updates.\n\n"
                + "• Update installation: Verified downloads; projects, settings and optional tools are preserved.\n\n"
                + "• Opening files: Double-clicking associated files such as .szs opens them in the installed Studio instead of setup.\n\n"
                + "• Consistent names: All eight Tools menu entries use the MKWii prefix.\n\n"
                + "• Model sources: Import Earth.szs, BackModel.szs and globe.arc from your own Mario Kart Wii ISO/WBFS and reuse them locally. Requires Wiimms ISO Tools.\n\n"
                + "• In-app changelog under Help → Changelog.\n\n")
                + "2.1.0-beta1 — Release\n\n"
                + L.T("Erste öffentliche Beta.", "First public beta.");
        }
    }
}
