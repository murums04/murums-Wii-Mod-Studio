using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;

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
                // Alle Versionsüberschriften erkennen, damit neue Releases nicht vergessen gehen.
                foreach (Match heading in Regex.Matches(history.Text, @"^\d+\.\d+\.\d+(?:-[^\s]+)?[ \t]+—[ \t]+[^\r\n]+", RegexOptions.Multiline))
                {
                    history.Select(heading.Index, heading.Length);
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
            return L.T("2.1.0-beta4 — Release", "2.1.0-beta4 — Release");
        }

        internal static string HistoryText()
        {
            return CurrentHeading() + "\n\n" + L.T(
                "• Font Tool: geprüfte Schriftbereiche, Archivauswahl korrigiert, sichere Abstände und Textfeld-Vergleich.\n\n"
                + "• RR-Effekte: getrennte Kategorien inklusive Windschatten, Originalfarben, echte Texturmuster und MUR_EDITED-Export.\n\n"
                + "• Pack-Werkstatt: Prüfbericht, geprüfte Projektstände und getrenntes Dolphin-Testprofil.\n\n"
                + "• Mod Merge: unabhängige Archivänderungen kombinieren, Konflikte bewusst auswählen.\n\n"
                + "• Hintergründe: RR-Region automatisch (PAL/USA/Japan), Lizenz-Zuordnung korrigiert, getrennte Warteansichten, Himmel ohne verschachtelte Scrollleiste und echte Modellvorschau.\n\n"
                + "• Audio: Loop-Punkte direkt in der Wellenform verschieben.\n\n"
                + "• Oberfläche/Theme Project: einfacheres Sammeln, Farbränder, korrigiertes Vorschau-Icon und fehlende RR-Dateien nach Bestätigung ergänzen.\n\n"
                + "• Character Builder: große Modelle automatisch voranzeigen, menschliche Quell-Rigs übernehmen, Blender-Körperbindung, korrigierte Schulterpunkte und direkter Export. Sechs Modellformate, korrigierte Materialien und RR-Konvertierung samt doppelten/ausgeblendeten Menüskeletten und gemeinsamem Wii-Matrixlimit; abschaltbare Zusatzteile, getrennte Bilder und klare Bewegungsschritte. Natürliche Menü- und Fahrzeughaltungen erhalten Größe und Proportionen, markieren unerreichbare Kontaktpunkte und bleiben korrigierbar. Fahrzeugauswahl-Animationen auch für manuell angepasste Haltungen korrigiert; einfacher Kopierordner und sichere Projektkopien.\n\n"
                + "• Installer: Modellkomponenten enthalten; Update und Wiederherstellung für größere Pakete.\n\n",
                "• Font Tool: checked script assignments, corrected archive selection, safer spacing and text-field comparison.\n\n"
                + "• RR effects: separate categories including slipstream, original colours, actual texture samples and MUR_EDITED exports.\n\n"
                + "• Pack Workshop: check reports, verified snapshots and an isolated Dolphin test profile.\n\n"
                + "• Mod Merge: combine independent archive edits and resolve conflicts explicitly.\n\n"
                + "• Backgrounds: automatic RR region (PAL/USA/Japan), corrected license target, separate waiting views, sky controls without nested scrolling and actual model previews.\n\n"
                + "• Audio: drag loop points directly in the waveform.\n\n"
                + "• UI/Theme Project: simpler collection, colour borders, corrected review icon and confirmed recovery of missing RR files.\n\n"
                + "• Character Builder: automatic large-model previews, human source rigs, Blender body binding, corrected shoulders and direct export. Six model formats, corrected materials and RR conversion, including duplicate/hidden menu skeletons and the combined Wii matrix limit; optional-part toggles, separate images and clear movement steps. Natural menu and per-vehicle poses preserve size and proportions, flag unreachable contacts and remain editable. Corrected vehicle-selection animations, including manually adjusted poses; simple copy folder and safe project copies.\n\n"
                + "• Installer: model components included; update and rollback support larger packages.\n\n") + "2.1.0-beta3 — Release\n\n" + L.T(
                "• Font Tool: Menü-/HUD-Schriften wählbar, mehr Unicode-Zeichen, Symbolbearbeitung und bessere Vorschau. Positionsnummern bleiben erhalten; Ingame-Prüfung offen.\n\n"
                + "• Vorschauen: Mausrad zoomt, gedrückt ziehen verschiebt, Mausrad-Doppelklick setzt zurück.\n\n"
                + "• Archive: Mehrfachauswahl, passende Dateifilter und sicheres Leeren.\n\n"
                + "• Menütexte: RR-Unterstützung und Import korrigiert.\n\n"
                + "• Archivvergleich: geführte Auswahl zusammenpassender Dateien.\n\n",
                "• Font Tool: selectable menu/HUD fonts, more Unicode characters, symbol editing and improved previews. Position numbers preserved; in-game checks pending.\n\n"
                + "• Previews: wheel to zoom, middle-drag to pan, middle-double-click to reset.\n\n"
                + "• Archives: multi-select, matching file filters and safe clearing.\n\n"
                + "• Menu Text: improved RR support and import.\n\n"
                + "• Archive Compare: guided selection of matching files.\n\n")
                + "2.1.0-beta2 — Release\n\n" + L.T(
                "• Custom Pack Maker: ISO-Dateiauswahl mit kurzen Erklärungen, .szs-Import und Beschreibung; Packs erstellen und in allen Tools verwalten.\n\n" + "• Updates: automatische/manuelle Prüfung, geprüfte Downloads; Projekte, Einstellungen und Tools bleiben erhalten.\n\n"
                + "• Zugeordnete Dateien öffnen per Doppelklick direkt im Studio.\n\n"
                + "• Custom Pack Maker: RR-Ordner erkennen oder auswählen, mit Standardpfaden für WheelWizard/Dolphin und Browse; RR-Dateien mit unveränderten Namen übernehmen; keine Sprachkopien erzeugen. ISO ergänzt nur fehlende Earth.szs, BackModel.szs und globe.arc. Bearbeitungs-Tools öffnen Dateien direkt, mit klaren Einstiegshinweisen und einheitlichen Bedienelementen.\n\n"
                + "• Font Changer: RR-Sprach- und HOME-Schriften auswählen, einschließlich I4/I8-Schriftmasken. Export nach MUR_EDITED; Hinweise zum Sichern und Ersetzen der Pack-Dateien.\n\n"
                + "• Race HUD: Teilwortsuche, Scrollvorschau, Itembox-/Glaskategorie sowie Slash, km/h und Input-Texturen in der Texturliste. ReplacedAssets.szs wird mitgeladen und der Slash beim Zahlenschriftwechsel berücksichtigt.\n\n"
                + "• Menüs: Lizenzhintergrund, obere/untere Balken und weitere Wartefenster bearbeitbar. Backgrounds öffnet Earth/globe/BackModel direkt; beschädigte Quellen schalten den Editor nicht frei. RR-Hintergrundbilder benötigen RR-Menüarchive; abweichende Original-Titelarchive werden abgewiesen. Lizenz-Bildauswahl und Export freigeschaltet; Menü-Spracharchive ohne eigenen Hintergrund bleiben unverändert. Exportknöpfe in allen Hintergrund-Tabs sichtbar; Modell-Tabs übersichtlicher.\n\n"
                + "• Himmel: vorerst nur Standbilder; bei GIFs das erste Bild. Animationen folgen später.\n\n"
                + "• Oberfläche: einheitliche Pack-Auswahl und Statusleisten, kompaktere Anordnung, Hover-/Layoutkorrekturen, einheitliche MKWii-Namen und Dateihinweise in allen Tools.\n\n"
                + "• Changelog mit GitHub-Link; Buttons im Über-Fenster korrigiert.\n\n",
                "• Custom Pack Maker: ISO file selection with short descriptions, .szs import and pack descriptions; create packs and manage their shared list.\n\n" + "• Updates: automatic/manual checks, verified downloads; projects, settings and tools are preserved.\n\n"
                + "• Associated files open directly in Studio on double-click.\n\n"
                + "• Custom Pack Maker: select WheelWizard/Dolphin default paths or Browse for the RR folder and copy RR files with unchanged names; no generated language aliases. ISO adds only missing Earth.szs, BackModel.szs and globe.arc. Editing tools open files directly, with clear entry hints and consistent controls.\n\n"
                + "• Font Changer: RR language and HOME fonts, including I4/I8 font masks. MUR_EDITED exports; guidance for backing up and replacing pack files.\n\n"
                + "• Race HUD: substring search, scrolling preview, item box/glass category; slash, km/h and input textures in the texture list. ReplacedAssets.szs loads automatically and its slash is included when changing number fonts.\n\n"
                + "• Menus: edit license backgrounds, top/bottom bars and additional waiting screens. Backgrounds opens Earth/globe/BackModel directly; damaged sources do not unlock the editor. RR backgrounds require RR menu archives; incompatible original title archives are rejected. License picture selection and export enabled; menu language archives without a background remain unchanged. Export actions stay visible in every background tab; model tabs have a clearer layout.\n\n"
                + "• Sky: still images only for now; GIFs use the first frame. Animation will follow later.\n\n"
                + "• Interface: consistent pack selection and status bars, simpler layout, hover/layout fixes, consistent MKWii names and file hints in all tools.\n\n"
                + "• Changelog with GitHub link; corrected About buttons.\n\n")
                + "2.1.0-beta1 — Release\n\n"
                + L.T("Erste öffentliche Beta.", "First public beta.");
        }
    }
}


