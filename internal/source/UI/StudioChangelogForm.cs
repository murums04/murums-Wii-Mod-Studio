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
                "• Custom Pack Maker: ISO-Dateiauswahl mit kurzen Erklärungen, .szs-Import und Beschreibung; Packs erstellen und in allen Tools verwalten.\n\n" + "• Updates: automatische/manuelle Prüfung, geprüfte Downloads; Projekte, Einstellungen und Tools bleiben erhalten.\n\n"
                + "• Zugeordnete Dateien öffnen per Doppelklick direkt im Studio.\n\n"
                + "• Custom Pack Maker: Originaldateien aus ISO/WBFS importieren. Bearbeitungs-Tools öffnen Dateien direkt, mit klaren Einstiegshinweisen und einheitlichen Bedienelementen.\n\n"
                + "• Export nach MUR_EDITED, auch beim Font Changer; Hinweise zum Sichern und Ersetzen der Pack-Dateien.\n\n"
                + "• Race HUD: Teilwortsuche, Scrollvorschau, Itembox-/Glaskategorie sowie Slash, km/h und Input-Texturen in der Texturliste. Verfügbare Mod-Zusatzarchive werden mitgeladen.\n\n"
                + "• Menüs: Lizenzhintergrund, obere/untere Balken und weitere Wartefenster bearbeitbar. Backgrounds öffnet Earth/globe/BackModel direkt; beschädigte Quellen schalten den Editor nicht frei. Titel-Export unterstützt auch die Originalarchive aus der ISO. Lizenz-Bildauswahl und Export freigeschaltet; Menü-Spracharchive ohne eigenen Hintergrund bleiben unverändert.\n\n"
                + "• Himmel: vorerst nur Standbilder; bei GIFs das erste Bild. Animationen folgen später.\n\n"
                + "• Oberfläche: einheitliche Pack-Auswahl und Statusleisten, kompaktere Anordnung, Hover-/Layoutkorrekturen, einheitliche MKWii-Namen und Dateihinweise in allen Tools.\n\n"
                + "• Changelog mit GitHub-Link; Buttons im Über-Fenster korrigiert.\n\n",
                "• Custom Pack Maker: ISO file selection with short descriptions, .szs import and pack descriptions; create packs and manage their shared list.\n\n" + "• Updates: automatic/manual checks, verified downloads; projects, settings and tools are preserved.\n\n"
                + "• Associated files open directly in Studio on double-click.\n\n"
                + "• Custom Pack Maker: import original files from ISO/WBFS. Editing tools open files directly, with clear entry hints and consistent controls.\n\n"
                + "• MUR_EDITED exports, including Font Changer; guidance for backing up and replacing pack files.\n\n"
                + "• Race HUD: substring search, scrolling preview, item box/glass category; slash, km/h and input textures in the texture list. Available supplementary mod archives load alongside them.\n\n"
                + "• Menus: edit license backgrounds, top/bottom bars and additional waiting screens. Backgrounds opens Earth/globe/BackModel directly; damaged sources do not unlock the editor. Title export also supports original archives extracted from an ISO. License picture selection and export enabled; menu language archives without a background remain unchanged.\n\n"
                + "• Sky: still images only for now; GIFs use the first frame. Animation will follow later.\n\n"
                + "• Interface: consistent pack selection and status bars, simpler layout, hover/layout fixes, consistent MKWii names and file hints in all tools.\n\n"
                + "• Changelog with GitHub link; corrected About buttons.\n\n")
                + "2.1.0-beta1 — Release\n\n"
                + L.T("Erste öffentliche Beta.", "First public beta.");
        }
    }
}
