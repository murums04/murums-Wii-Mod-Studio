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
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(940, 670);
            MinimumSize = new Size(720, 480);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(StudioChrome.Header(L.T("Was ist neu?", "What's new?"), L.T("Versionen, Änderungen und bekannte Grenzen", "Versions, changes and known limits")), 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var sidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 16, 8, 12), BackColor = DarkTheme.Panel, Margin = Padding.Empty };
            var versions = new ListBox { Name = "ChangelogVersions", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, AccessibleName = L.T("Version auswählen", "Choose a version") };
            sidebar.Controls.Add(versions);
            sidebar.Controls.Add(new Label { Text = L.T("VERSIONEN", "VERSIONS"), Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = DarkTheme.Muted });
            body.Controls.Add(sidebar, 0, 0);
            var readerHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 12), Margin = Padding.Empty };
            var history = new RichTextBox { Name = "ChangelogReader", ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical, WordWrap = true, DetectUrls = false, Text = HistoryText(), Font = new Font("Segoe UI", 10.5F), AccessibleName = L.T("Änderungen und bekannte Grenzen", "Changes and known limits") };
            readerHost.Controls.Add(history);
            readerHost.SizeChanged += delegate
            {
                int width = Math.Max(100, Math.Min(readerHost.ClientSize.Width - readerHost.Padding.Horizontal, 850 * history.Font.Height / 17));
                history.Bounds = new Rectangle(readerHost.Padding.Left + Math.Max(0, (readerHost.ClientSize.Width - readerHost.Padding.Horizontal - width) / 2), readerHost.Padding.Top, width, Math.Max(1, readerHost.ClientSize.Height - readerHost.Padding.Vertical));
            };
            StudioUx.DisableHover(history);
            body.Controls.Add(readerHost, 1, 0);
            root.Controls.Add(body, 0, 1);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 1, Padding = new Padding(18, 8, 18, 12), Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
            actions.Controls.Add(CreateLink("GitHub ↗", "GitHub ↗", "https://github.com/murums04/murums-Wii-Mod-Studio"), 1, 0);
            actions.Controls.Add(CreateLink("Fehler melden ↗", "Report a bug ↗", "https://github.com/murums04/murums-Wii-Mod-Studio/issues"), 2, 0);
            actions.Controls.Add(CreateLink("Downloads & Versionen ↗", "Downloads & releases ↗", "https://github.com/murums04/murums-Wii-Mod-Studio/releases"), 3, 0);
            var close = StudioChrome.ActionButton(L.T("Schließen", "Close"));
            close.DialogResult = DialogResult.OK;
            actions.Controls.Add(close, 4, 0);
            root.Controls.Add(actions, 0, 2);
            Controls.Add(root);
            AcceptButton = close;
            CancelButton = close;
            DarkTheme.Apply(this);
            DarkTheme.StyleListBox(versions);
            var headings = Regex.Matches(history.Text, @"^\d+\.\d+\.\d+(?:-[^\s]+)?(?:[ \t]+—[ \t]+[^\r\n]+)?$", RegexOptions.Multiline);
            using (var headingFont = new Font(history.Font, FontStyle.Bold))
            {
                // Alle Versionsüberschriften erkennen, damit neue Releases nicht vergessen gehen.
                foreach (Match heading in headings)
                {
                    history.Select(heading.Index, heading.Length);
                    history.SelectionFont = headingFont;
                    history.SelectionColor = DarkTheme.Accent;
                    versions.Items.Add(heading.Value.Split(new[] { ' ' }, 2)[0]);
                }
            }
            versions.SelectedIndexChanged += delegate
            {
                if (versions.SelectedIndex < 0) return;
                history.Select(headings[versions.SelectedIndex].Index, 0);
                history.ScrollToCaret();
            };
            if (versions.Items.Count > 0) versions.SelectedIndex = 0;
            history.Select(0, 0);
        }

        LinkLabel CreateLink(string german, string english, string address)
        {
            var link = new LinkLabel { Text = L.T(german, english), AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = new Padding(12, 3, 0, 3), LinkColor = DarkTheme.Accent, ActiveLinkColor = DarkTheme.Fore, VisitedLinkColor = DarkTheme.Accent,
                AccessibleName = L.T(german, english) };
            link.LinkClicked += delegate { StudioChrome.OpenLink(this, address); };
            return link;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Owner != null)
                Icon = Owner.Icon;
        }

        private static string CurrentHeading()
        {
            return StudioVersion.Current + L.T(" — Release", " — Release");
        }

        internal static string HistoryText()
        {
            return CurrentHeading() + "\n\n"
                + L.T("• Texturen/GIF: Sammelimport/-export; Format, Mips, Vorschau; Undo.\n", "• Textures/GIF: batch import/export; format, mipmaps, preview; undo.\n")
                + L.T("• Texturvorschau: RGBA, RGB und Alphakanal getrennt ansehen.\n", "• Texture preview: inspect RGBA, RGB and alpha separately.\n")
                + L.T("• Archive/Packs: Vergleich und Prüfung von Layouts, Animationen, Texturen und Verweisen.\n", "• Archives/packs: compare and check layouts, animations, textures and references.\n")
                + L.T("• Archivvergleich: lesbare TXT-Berichte mit Änderungsdetails.\n", "• Archive comparison: readable TXT reports with change details.\n")
                + L.T("• Musik: Samples/Sekunden; Kanalpegel; Nulldurchgangshilfe mit Undo; Laden abbrechbar.\n", "• Audio: samples/seconds; channel levels; zero-crossing assist with undo; cancellable loading.\n")
                + L.T("• Modelle: Fahrhaltungen und manuelle Anpassungen beim Speichern erhalten.\n", "• Models: preserve driving poses and manual adjustments when saving.\n")
                + L.T("• Rig/Export: Arm-/Fingerzuordnung; Kontaktprüfung; Knochenvorschau; Poseprüfung inkompatibler Rigs.\n", "• Rig/export: arm/finger mapping; contact checks; bone preview; review incompatible rigs.\n")
                + L.T("• Bewegung: Menü-/Fahranimationen; Prüfablauf vor Export; Rückgängig.\n", "• Motion: menu/driving animations; pre-export review; undo.\n")
                + L.T("• Charakter: geführter Editor; Texturen, Logos, Farben und Vorschau.\n", "• Character: guided editor; textures, logos, colours and preview.\n")
                + L.T("• Studio: gemeinsame Arbeitsfläche; klare Bedienung; bebilderte DE/EN-Hilfe.\n", "• Studio: shared workspace; clear controls; illustrated DE/EN guides.\n")
                + L.T("• Schriften: Zeichenprüfung auf Deutsch/Englisch; klarere Quellen- und Archivhinweise.\n", "• Fonts: German/English character checks; clearer source and archive guidance.\n")
                + L.T("• Fenster: Wiederherstellung, Skalierung und Scrollleisten stabil; neutraler Startfokus.\n", "• Window: stable restore, scaling and scrollbars; neutral startup focus.\n")
                + L.T("• Stil: Purpur-/Cyan-Elemente und Ladebalken; dunkle Tabellenköpfe und Bereichsnavigation; interaktive freie Flächen.\n", "• Style: purple/cyan controls and progress bars; dark table headers and section navigation; interactive open surfaces.\n")
                + L.T("• Hilfe: aktuelle Programmbilder; geprüfte Werkzeugwege; GitHub und Fehlerberichte.\n", "• Help: current program pictures; verified tool guidance; GitHub and bug reports.\n")
                + L.T("• Offen: Originalbewegungen; mehrdeutige Sonderposen und Fingerzuordnung.\n", "• Open: original motion; ambiguous special poses and finger mapping.\n")
                + L.T("• Offen: Haar-/Kleidungskollisionen; allgemeine Mehrmodell-Leistung.\n", "• Open: hair/clothing collisions; general multi-model performance.\n")
                + "\n\n"
                + "2.1.0-beta6 — Release\n\n"
                + L.T("• Character Builder: DPI-Texturen; Skelett- und Schattenansicht; geschätzte Gelenke markiert.\n", "• Character Builder: DPI textures; skeleton and shaded views; estimated joints marked.\n")
                + L.T("• Zuverlässigkeit: Export-Rollback; sichere Einstellungen; Packprüfung und Projektbereinigung.\n", "• Reliability: export rollback; safe settings; pack checks and project cleanup.\n")
                + L.T("• Modelle: Arbeitsordner; geteilte Fahrzeugreferenzen; weniger Kopien; passende Vorschaugrenzen.\n", "• Models: clean work folders; shared vehicle references; fewer copies; correct preview bounds.\n")
                + L.T("• Tool-Exporte: Font-, Race-, HUD- und Menütext-Änderungen erhalten.\n", "• Tool exports: preserve font, race, HUD and menu text edits.\n")
                + L.T("• Speichern: eine Vorgängersicherung; keine Sicherung beim Öffnen; Unverändertes übersprungen.\n", "• Saving: one previous backup; no backup on opening; unchanged files skipped.\n")
                + L.T("• Offen: Automapping ungeriggter Sonderposen.\n\n", "• Open: automatic mapping of unrigged special poses.\n\n")
                + "2.1.0-beta5 — Release\n\n"
                + L.T("• Installer: vollständige, geprüfte Updatepakete.\n", "• Installer: complete, verified update packages.\n")
                + L.T("• Character Builder: leichtere Modelle; Gesichts-/Kleidungsdetails; Licht, Materialien, glTF/USDZ.\n", "• Character Builder: lighter models; face/clothing detail; lighting, materials, glTF/USDZ.\n")
                + L.T("• Haltung: stabilere Bindung; Peach-Gelenke; Gliedmaßenvolumen; Ellbogen; Handgriffe.\n", "• Poses: steadier binding; Peach joints; limb volume; elbows; hand grips.\n")
                + L.T("• Animation: eigene Schlüsselbilder für Menü und Fahrzeug; Export.\n", "• Animation: custom menu and vehicle keyframes; export.\n")
                + L.T("• Offen: Sonderposen, Fingergriffe, Mehrmodell-FPS, Mint-Zubehör, Baby Daisy und King Boo.\n\n", "• Open: special poses, finger grips, multi-model FPS, Mint accessories, Baby Daisy and King Boo.\n\n")
                + "2.1.0-beta4 — Release\n\n"
                + L.T("• Font Tool: Schriftbereiche; Archivauswahl; sichere Abstände; Textvergleich.\n", "• Font Tool: script assignments; archive selection; safe spacing; text comparison.\n")
                + L.T("• RR-Effekte: Kategorien, Windschatten, Originalfarben, Vorschau und MUR_EDITED-Export.\n", "• RR effects: categories, slipstream, source colours, preview and MUR_EDITED export.\n")
                + L.T("• Pack-Werkstatt/Mod Merge: Prüfberichte; Projektstände; Dolphin-Profil; Konfliktauswahl.\n", "• Pack Workshop/Mod Merge: reports; snapshots; Dolphin profile; conflict selection.\n")
                + L.T("• Hintergründe/Theme Project: RR-Erkennung; Himmelsteuerung; Vorschau; Farbränder; fehlende Dateien.\n", "• Backgrounds/Theme Project: RR detection; sky controls; preview; colour borders; missing files.\n")
                + L.T("• Audio: Loop-Punkte per Wellenform verschieben.\n", "• Audio: move loop points in the waveform.\n")
                + L.T("• Character Builder: sechs Formate; Quell-Rigs; Körperbindung; Schulterkorrektur; direkter Export.\n", "• Character Builder: six formats; source rigs; body binding; shoulder correction; direct export.\n")
                + L.T("• Haltungen/Export: getrennte Posen; Kontaktwarnungen; Material-, DAE/RR- und Matrixkorrekturen.\n", "• Poses/export: separate poses; contact warnings; material, DAE/RR and matrix fixes.\n")
                + L.T("• Installer: Modellkomponenten; Update und Rollback großer Pakete.\n\n", "• Installer: model components; large-package update and rollback.\n\n")
                + "2.1.0-beta3 — Release\n\n"
                + L.T("• Font Tool: Menü-/HUD-Schriften; Unicode; Symbole; Positionsnummern. Ingame-Prüfung offen.\n", "• Font Tool: menu/HUD fonts; Unicode; symbols; position numbers. In-game checks pending.\n")
                + L.T("• Vorschauen: Mausrad-Zoom; mittlere Maustaste zum Verschieben; Doppelklick zum Zurücksetzen.\n", "• Previews: wheel zoom; middle-drag pan; double-click reset.\n")
                + L.T("• Archive: Mehrfachauswahl; Filter; sicheres Leeren. Menütexte: RR-Import korrigiert.\n", "• Archives: multi-select; filters; safe clearing. Menu text: corrected RR import.\n")
                + L.T("• Archivvergleich: geführte Dateiauswahl.\n\n", "• Archive comparison: guided file selection.\n\n")
                + "2.1.0-beta2 — Release\n\n"
                + L.T("• Custom Pack Maker: RR-Ordner, Pack-Verwaltung, ISO/SZS-Import und fehlende Dateien ergänzen.\n", "• Custom Pack Maker: RR folders, pack management, ISO/SZS import and missing-file recovery.\n")
                + L.T("• Updates: automatische/manuelle Prüfung; Downloads geprüft; Projekte, Einstellungen und Tools erhalten.\n", "• Updates: automatic/manual checks; verified downloads; preserve projects, settings and tools.\n")
                + L.T("• Dateien: Doppelklick-Start; einheitliche Quellen; verständliche Einstiegshilfen.\n", "• Files: open by double-click; consistent sources; clear starting guidance.\n")
                + L.T("• Font Changer: RR-Sprach-/HOME-Schriften; I4/I8-Masken; MUR_EDITED-Export.\n", "• Font Changer: RR language/HOME fonts; I4/I8 masks; MUR_EDITED export.\n")
                + L.T("• Race HUD: Suche, Scrollvorschau, Itembox/Glas, Texturen und ReplacedAssets.\n", "• Race HUD: search, scrolling preview, item box/glass, textures and ReplacedAssets.\n")
                + L.T("• Menüs: Hintergründe, Balken, Wartefenster; Quellenprüfung und sichtbare Exporte.\n", "• Menus: backgrounds, bars, waiting screens; source checks and visible exports.\n")
                + L.T("• Himmel: Standbilder und erstes GIF-Bild; Animation offen.\n", "• Sky: still images and first GIF frame; animation pending.\n")
                + L.T("• Oberfläche: Packwahl, Status, Layout/Hover, MKWii-Namen, Changelog und Über-Dialog.\n\n", "• Interface: pack choice, status, layout/hover, MKWii names, changelog and About dialog.\n\n")
                + "2.1.0-beta1 — Release\n\n"
                + L.T("• Erste öffentliche Beta.\n\n", "• First public beta.\n\n")
                + "2.1.0-alpha56\n\n"
                + L.T("• HUD-Zahlengruppen; gespeicherte Sitzungseinstellungen; sichtbare Ressourcenpfade.\n\n", "• HUD number groups; saved session settings; visible resource paths.\n\n")
                + "2.1.0-alpha55\n\n"
                + L.T("• Installer-Kopfbereich; violetter Akzent.\n\n", "• Installer header; violet accent.\n\n")
                + "2.1.0-alpha54\n\n"
                + L.T("• Weniger Scrollen in Meldungsdialogen.\n\n", "• Less scrolling in message dialogs.\n\n")
                + "2.1.0-alpha53\n\n"
                + L.T("• Sicherer Sprachwechsel in Haupt- und BRLAN-Editor.\n\n", "• Safe language switching in the main and BRLAN editors.\n\n")
                + "2.1.0-alpha52\n\n"
                + L.T("• TTF-Hinting; Race-HUD-Zahlenschriften.\n\n", "• TTF hinting; Race HUD number fonts.\n\n")
                + "2.1.0-alpha51\n\n"
                + L.T("• Schrift-/Konturfarben; Konturbreite.\n\n", "• Font/outline colours; outline width.\n\n")
                + "2.1.0-alpha50\n\n"
                + L.T("• TTF-Basislinienkorrektur für BRFNT.\n\n", "• TTF baseline correction for BRFNT.\n\n")
                + "2.1.0-alpha49\n\n"
                + L.T("• Hover-Flackern bei Fensterwechsel behoben.\n\n", "• Fixed hover flicker when switching windows.\n\n")
                + "2.1.0-alpha48\n\n"
                + L.T("• Keine Hover-Popups über Tabs.\n\n", "• No hover popups over tabs.\n\n")
                + "2.1.0-alpha47\n\n"
                + L.T("• Keine Hover-Hilfe auf leeren Flächen.\n\n", "• No hover help on empty areas.\n\n")
                + "2.1.0-alpha46\n\n"
                + L.T("• Regressionsprüfung; sichere Verwerfen-Vorgaben.\n\n", "• Regression checks; safe discard defaults.\n\n")
                + "2.1.0-alpha45\n\n"
                + L.T("• Hover-Flackern bei ruhender Maus behoben.\n\n", "• Fixed hover flicker with a stationary pointer.\n\n")
                + "2.1.0-alpha44\n\n"
                + L.T("• Einheitliche Exportaktionen; dunkle Bestätigungsdialoge.\n\n", "• Consistent export actions; dark confirmation dialogs.\n\n")
                + "2.1.0-alpha43\n\n"
                + L.T("• Hover-Absturz nach Tool-Schließen behoben.\n\n", "• Fixed hover crash after closing a tool.\n\n")
                + "2.1.0-alpha42\n\n"
                + L.T("• Durchklickbare Hover-Hinweise.\n\n", "• Click-through hover hints.\n\n")
                + "2.1.0-alpha41\n\n"
                + L.T("• Historischer Entwicklungsstand.\n\n", "• Historical development snapshot.\n\n")
                + "2.1.0-alpha40\n\n"
                + L.T("• Historischer Entwicklungsstand.\n\n", "• Historical development snapshot.\n\n")
                + "2.1.0-alpha10\n\n"
                + L.T("• BrawlCrate-EXE-Erkennung; Download-Fallback; unabhängige optionale Tools; Wiederholungsaktion.\n\n", "• BrawlCrate EXE detection; download fallback; optional tools; retry action.\n\n")
                + "2.1.0-alpha7\n\n"
                + L.T("• Toolchain-Parser korrigiert; parallele Tool-Installation; kleinerer FFmpeg-Download; Installer-Layout.\n\n", "• Toolchain parser; parallel tool installation; smaller FFmpeg download; installer layout.\n\n");
        }
    }
}

