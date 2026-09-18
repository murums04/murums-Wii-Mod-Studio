using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioAboutForm : Form
    {
        public StudioAboutForm(string version)
        {
            Text = L.T("Über murums Wii Mod Studio", "About murums Wii Mod Studio");
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(860, 700);
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Padding = new Padding(16);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(StudioChrome.Header(L.T("Dein Wii-Modding-Studio", "Your Wii modding studio"), L.T("Werkzeuge für Archive, Texturen und Menüs • Beta-Version", "Tools for archives, textures and menus • Beta release")), 0, 0);
            var features = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(4, 14, 4, 4)
            };
            features.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddFeature(features, 0, L.T("Spieldateien & Archive", "Game files & archives"), L.T("SZS/U8 öffnen, Dateien organisieren, ersetzen und exportieren.", "Open SZS/U8 archives; organize, replace and export their files."));
            AddFeature(features, 1, L.T("Bilder & Texturen", "Pictures & textures"), L.T("TPL-Texturen ansehen und eigene Bilder für Menüs importieren.", "Preview TPL textures and import your own menu pictures."));
            AddFeature(features, 2, L.T("Menüanimationen & Layouts", "Menu animations & layouts"), L.T("BRLAN-Animationen und BRLYT-Menülayouts bearbeiten.", "Edit BRLAN animations and BRLYT menu layouts."));
            AddFeature(features, 3, L.T("Geführte Hintergrund-Werkzeuge", "Guided background tools"), L.T("Bilder/GIFs, Globusfarben und 3D-Modelle als bearbeitete Kopien erstellen.", "Create edited copies with pictures/GIFs, globe colours and 3D model options."));
            AddFeature(features, 4, L.T("Schriften, Texte & Musik", "Fonts, messages & music"), L.T("TTF-Zeichen in BRFNT, durchsuchbare BMG-Texte und WAV-Loopmarkierungen.", "TTF glyphs in BRFNT, searchable BMG messages and WAV loop markers."));
            AddFeature(features, 5, L.T("Theme-Projekte & Vergleich", "Theme projects & comparison"), L.T("Bearbeitete Dateien zusammenstellen und Archivänderungen gezielt übernehmen.", "Collect edited files and selectively apply changed archive resources."));
            layout.Controls.Add(features, 0, 1);
            layout.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Padding = new Padding(4, 5, 0, 0), Text = L.T("Copyright © 2026 murums04 · Nur nichtkommerzielle Nutzung\nEntwickelt von murums mit KI-Unterstützung.", "Copyright © 2026 murums04 · Noncommercial use only\nDeveloped by murums with AI assistance.") }, 0, 2);
            var close = new Button
            {
                Text = L.T("Schliessen", "Close"),
                DialogResult = DialogResult.OK,
                Size = new Size(130, 36),
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.Accent2,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderColor = DarkTheme.Accent;
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            actions.Controls.Add(close);
            var license = new Button
            {
                Text = L.T("Credits & Lizenzen", "Credits & licenses"),
                Size = new Size(170, 36),
                FlatStyle = FlatStyle.Flat
            };
            license.Click += delegate
            {
                using (var dialog = new StudioLicenseForm())
                    dialog.ShowDialog(this);
            };
            actions.Controls.Add(license);
            var profile = new LinkLabel
            {
                Text = "github.com/murums04",
                AutoSize = true,
                Margin = new Padding(8, 10, 16, 0)
            };
            profile.LinkClicked += delegate
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/murums04") { UseShellExecute = true });
            };
            actions.Controls.Add(profile);
            layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout);
            AcceptButton = close;
            CancelButton = close;
            DarkTheme.Apply(this);
        }

        private static void AddFeature(TableLayoutPanel panel, int row, string title, string description)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 6));
            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(10, 4, 8, 3),
                BackColor = DarkTheme.Panel
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(new Label { UseMnemonic = false, Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }, 0, 0);
            card.Controls.Add(new Label { UseMnemonic = false, Text = description, Dock = DockStyle.Fill }, 0, 1);
            panel.Controls.Add(card, 0, row);
        }
    }
}
