using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioAboutForm : Form
    {
        public StudioAboutForm(string version)
        {
            Text = L.T("Über murums Wii Mod Studio", "About murums Wii Mod Studio");
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(880, 670);
            MinimumSize = new Size(740, 540);
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(StudioChrome.Header("murums Wii Mod Studio", L.T("Archive · Charaktere · Menüs · Dein eigenes Pack", "Archives · Characters · Menus · Your own pack")), 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(24), Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var features = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, WordWrap = true, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, Margin = new Padding(0, 0, 22, 0), AccessibleName = L.T("Über das Studio", "About the studio") };
            StudioUx.DisableHover(features);
            body.Controls.Add(features, 0, 0);
            var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(18), BackColor = DarkTheme.Panel, Margin = Padding.Empty };
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) info.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            info.Controls.Add(new Label { Text = L.T("DEINE VERSION", "YOUR VERSION"), AutoSize = true, ForeColor = DarkTheme.Muted, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) }, 0, 0);
            info.Controls.Add(new Label { Name = "StudioDisplayVersion", Text = version, AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = DarkTheme.Accent, Margin = new Padding(0, 0, 0, 24), UseMnemonic = false }, 0, 1);
            info.Controls.Add(new Label { Text = L.T("Von murums04", "By murums04"), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) }, 0, 2);
            info.Controls.Add(new Label { Text = L.T("Copyright © 2026 murums04\nNur nichtkommerzielle Nutzung.\n\nEntwickelt von murums mit KI-Unterstützung.", "Copyright © 2026 murums04\nNoncommercial use only.\n\nDeveloped by murums with AI assistance."), AutoSize = true, Margin = new Padding(0, 0, 0, 24), UseMnemonic = false }, 0, 3);
            var profile = new LinkLabel { Text = L.T("GitHub-Profil öffnen ↗", "Open GitHub profile ↗"), AutoSize = true, LinkColor = DarkTheme.Accent, ActiveLinkColor = DarkTheme.Fore, VisitedLinkColor = DarkTheme.Accent, Margin = Padding.Empty };
            profile.LinkClicked += delegate { StudioChrome.OpenLink(this, "https://github.com/murums04"); };
            info.Controls.Add(profile, 0, 4);
            info.SizeChanged += delegate
            {
                foreach (Control item in info.Controls) if (item is Label) item.MaximumSize = new Size(Math.Max(80, info.ClientSize.Width - info.Padding.Horizontal - 6), 0);
            };
            body.Controls.Add(info, 1, 0);
            root.Controls.Add(body, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(16, 8, 16, 12), Margin = Padding.Empty };
            var close = StudioChrome.ActionButton(L.T("Schließen", "Close"));
            close.DialogResult = DialogResult.OK;
            actions.Controls.Add(close);
            var license = StudioChrome.ActionButton(L.T("Credits & Lizenzen", "Credits & licenses"));
            license.Click += delegate { using (var dialog = new StudioLicenseForm()) dialog.ShowDialog(this); };
            actions.Controls.Add(license);
            var project = StudioChrome.ActionButton(L.T("Projektseite öffnen ↗", "Open project page ↗"));
            project.Click += delegate { StudioChrome.OpenLink(this, "https://github.com/murums04/murums-Wii-Mod-Studio"); };
            actions.Controls.Add(project);
            root.Controls.Add(actions, 0, 2);
            Controls.Add(root);
            AcceptButton = close;
            CancelButton = close;
            DarkTheme.Apply(this);
            using (var titleFont = new Font("Segoe UI", 21, FontStyle.Bold))
            using (var bold = new Font(Font, FontStyle.Bold))
            {
                Add(features, L.T("Dein Wii-Modding-Studio\n", "Your Wii modding studio\n"), titleFont);
                Add(features, L.T("Von der Quelldatei zur eigenen, überprüfbaren Kopie.\n\n", "From a source file to your own reviewable copy.\n\n"), Font);
                Feature(features, bold, L.T("Spieldateien & Archive", "Game files & archives"), L.T("SZS/U8 öffnen; Dateien organisieren, ersetzen und exportieren.", "Open SZS/U8; organise, replace and export files."));
                Feature(features, bold, L.T("Charaktere & Projekte", "Characters & projects"), L.T("Modelle, Namen, Minimap und Emblem bearbeiten; Projekt und Spielausgabe getrennt speichern.", "Edit models, names, minimap and emblem; save projects and game outputs separately."));
                Feature(features, bold, L.T("Bilder, Schriften & Texte", "Images, fonts & text"), L.T("Texturen ersetzen, Schriftzeichen prüfen und BMG-Nachrichten bearbeiten.", "Replace textures, review font glyphs and edit BMG messages."));
                Feature(features, bold, L.T("Menüs, Animationen & HUD", "Menus, animations & HUD"), L.T("BRLAN/BRLYT, Bilder/GIFs, Globus, Menümodelle und Rennanzeigen bearbeiten.", "Edit BRLAN/BRLYT, pictures/GIFs, globe, menu models and race displays."));
                Feature(features, bold, L.T("Musik, Themes & Vergleich", "Music, themes & comparison"), L.T("Loops vorbereiten, Änderungen zusammenstellen und Archivressourcen vergleichen.", "Prepare loops, assemble edits and compare archive resources."));
            }
            features.Select(0, 0);
        }

        private static void Feature(RichTextBox box, Font bold, string title, string description)
        {
            Add(box, title + "\n", bold);
            Add(box, description + "\n\n", box.Font);
        }

        private static void Add(RichTextBox box, string text, Font font)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionFont = font;
            box.AppendText(text);
        }
    }
}
