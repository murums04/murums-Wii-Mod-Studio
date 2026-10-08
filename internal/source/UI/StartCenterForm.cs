using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StartCenterForm : Form
    {
        private readonly MainForm _owner;

        public StartCenterForm(MainForm owner)
        {
            _owner = owner;
            Text = "murums Wii Mod Studio — " + L.T("Start", "Start center");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 560);
            ClientSize = new Size(960, 690);
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            ShowInTaskbar = false;
            BuildUi();
            DarkTheme.Apply(this);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(StudioChrome.Header(L.T("Dein nächster Schritt", "Your next step"), L.T("Quelle öffnen · Gestalten · Eine eigene Kopie prüfen", "Open a source · Create · Review your own copy")), 0, 0);
            var tabs = new DarkTabControl { Dock = DockStyle.Fill, Margin = new Padding(16, 12, 16, 0) };
            var simple = new TabPage(L.T("Einfach starten", "Easy start"));
            var advanced = new TabPage(L.T("Arbeitsflächen", "Workspaces"));
            var files = new TabPage(L.T("Dateitypen", "File types"));
            tabs.TabPages.Add(simple);
            tabs.TabPages.Add(advanced);
            tabs.TabPages.Add(files);
            root.Controls.Add(tabs, 0, 1);
            var quick = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(6), AutoScroll = true, WrapContents = true };
            quick.Controls.Add(Tile(L.T("Ein Custom Pack vorbereiten", "Prepare a custom pack"), L.T("RR-Quelle und Region wählen. Originale behalten, in einer neuen Kopie arbeiten.", "Choose your RR source and region. Keep originals and work in a new copy."), StudioIcon.Pack, () => Open("pack")));
            quick.Controls.Add(Tile(L.T("Charakter gestalten", "Design a character"), L.T("Modell oder Projekt öffnen. Ersatzziel, Haltung und Ausgabe getrennt prüfen.", "Open a model or project. Review the target, pose and output separately."), StudioIcon.Character, () => Open("character")));
            quick.Controls.Add(Tile(L.T("Hintergründe ändern", "Change backgrounds"), L.T("Eigene Bilder oder GIFs für Titel, Einzelspieler und weitere Menüs vorbereiten.", "Prepare your pictures or GIFs for the title screen, single player and other menus."), StudioIcon.Image, () => Open("background")));
            quick.Controls.Add(Tile(L.T("Menübilder ersetzen", "Replace menu images"), L.T("Eine Textur wählen, eigenes Artwork einsetzen und die Importvorschau prüfen.", "Choose a texture, insert your artwork and review the import preview."), StudioIcon.Image, () => Open("textures")));
            quick.Controls.Add(Tile(L.T("Vorhandene Datei öffnen", "Open an existing file"), L.T("Archive, Texturen oder Animationen öffnen. Studio erkennt den Dateityp.", "Open archives, textures or animations. Studio detects the file type."), StudioIcon.Open, () => Pick(ResourceDetector.OpenFilter)));
            quick.Controls.Add(Tile(L.T("Schritte nachlesen", "Read the guide"), L.T("Anleitungen, passende Quelldateien, Tastatur und Vorschaugrenzen durchsuchen.", "Find guides, matching source files, keyboard controls and preview limits."), StudioIcon.Help, () => Open("help")));
            foreach (Control tile in quick.Controls) { tile.Dock = DockStyle.None; tile.Size = new Size(360, 152); }
            quick.SizeChanged += delegate { FitTiles(quick); };
            simple.Controls.Add(quick);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6), WrapContents = true };
            advanced.Controls.Add(tools);
            AddTool(tools, "Pack Maker", L.T("RR-Quelle und Region → eigenes Pack", "RR source and region → your own pack"), "pack", StudioIcon.Pack);
            AddTool(tools, L.T("Pack-Werkstatt", "Pack workshop"), L.T("Prüfbericht, Sicherung und getrennte Testkopie", "Check report, snapshot and separate test copy"), "workshop", StudioIcon.Check);
            AddTool(tools, L.T("Charakter", "Character"), L.T("Modell, Minimap, Emblem und Spielausgabe", "Model, minimap, emblem and game output"), "character", StudioIcon.Character);
            AddTool(tools, L.T("Hintergründe", "Backgrounds"), L.T("Bilder, GIFs, Globus und Menümodelle", "Pictures, GIFs, globe and menu models"), "background", StudioIcon.Image);
            AddTool(tools, L.T("Menübilder", "Menu images"), L.T("Texturen ersetzen und Farben bearbeiten", "Replace textures and edit colours"), "textures", StudioIcon.Image);
            AddTool(tools, L.T("Schriften", "Fonts"), L.T("Textschriften, Zahlen und Symbole prüfen", "Review text fonts, digits and symbols"), "font", StudioIcon.Font);
            AddTool(tools, L.T("Spieltexte", "Game text"), L.T("BMG-Nachrichten finden und bearbeiten", "Find and edit BMG messages"), "text", StudioIcon.Font);
            AddTool(tools, L.T("Menü-Layout", "Menu layout"), L.T("Elemente verschieben, skalieren und färben", "Move, resize and tint elements"), "menu", StudioIcon.Layout);
            AddTool(tools, L.T("Rennanzeigen", "Race HUD"), L.T("Zahlen, Items, Minimap und Input Viewer", "Digits, items, minimap and Input Viewer"), "hud", StudioIcon.Layout);
            AddTool(tools, L.T("Effekte", "Effects"), L.T("Drift-, Boost- und Partikelfarben", "Drift, boost and particle colours"), "effects", StudioIcon.Effects);
            AddTool(tools, L.T("Musik & Loops", "Music & loops"), L.T("Wellenform, Loopbereich und Export", "Waveform, loop range and export"), "audio", StudioIcon.Audio);
            AddTool(tools, "Theme Project", L.T("Bearbeitete Dateien einer Packbasis zuordnen", "Map edited files to a base pack"), "theme", StudioIcon.Pack);
            AddTool(tools, L.T("Zusammenführen", "Merge changes"), L.T("Archivkopien und ihre Konflikte prüfen", "Review archive copies and their conflicts"), "merge", StudioIcon.Import);
            AddTool(tools, L.T("Vergleichen", "Compare archives"), L.T("Original und Änderungen gegenüberstellen", "Compare the original and changes"), "compare", StudioIcon.Compare);
            tools.SizeChanged += delegate { FitTiles(tools); };
            var types = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            files.Controls.Add(types);
            AddFileType(types, L.T("Archive / SZS", "Archives / SZS"), "SZS · U8 · ARC · Yaz0", "*.szs;*.arc;*.u8;*.rarc", StudioIcon.Archive);
            AddFileType(types, L.T("UI / Menüs", "UI / Menus"), "TPL · BRLAN · BRLYT · BRCTR · BRFNT", "*.brlan;*.brlyt;*.tpl;*.bti;*.brctr;*.brfnt", StudioIcon.Layout);
            AddFileType(types, L.T("Streckendaten", "Course data"), L.T("KMP · KCL · LEX · BRRES untersuchen", "Inspect KMP · KCL · LEX · BRRES"), "*.kmp;*.kcl;*.lex;*.brres", StudioIcon.Check);
            AddFileType(types, L.T("3D & Animation", "3D & animation"), L.T("BRRES · MDL0 · TEX0 und Animationen", "BRRES · MDL0 · TEX0 and animations"), "*.brres;*.mdl0;*.tex0;*.pat0;*.srt0;*.chr0;*.clr0;*.shp0;*.scn0;*.bmd;*.bdl", StudioIcon.Character);
            AddFileType(types, L.T("Spieltexte", "Game text"), L.T("BMG-Nachrichten öffnen", "Open BMG messages"), "*.bmg", StudioIcon.Font);
            AddFileType(types, L.T("Soundressourcen", "Sound resources"), L.T("BRSTM · BRSAR und Nintendo-Audio untersuchen", "Inspect BRSTM · BRSAR and Nintendo audio"), "*.brstm;*.brsar;*.brseq;*.brwav;*.brbnk;*.brwsd", StudioIcon.Audio);
            AddFileType(types, L.T("Effekte / Video", "Effects / video"), L.T("BREFF · BREFT · THP untersuchen", "Inspect BREFF · BREFT · THP"), "*.breff;*.breft;*.jpa;*.thp", StudioIcon.Effects);
            AddFileType(types, L.T("Weitere Formate", "Other formats"), L.T("REL · DOL · Disc-Images · Ghosts · Binärdateien", "REL · DOL · Disc images · Ghosts · Binaries"), "*.rel;*.dol;*.iso;*.wbfs;*.wia;*.gcz;*.ciso;*.rkg;*.crkg;*.rksys;*.bin", StudioIcon.Search);
            types.SizeChanged += delegate { FitTiles(types); };
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(20, 8, 16, 8) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(new Label { Text = L.T("Arbeitsflächen bleiben beim Wechsel geöffnet. Separate Kopien schützen deine Quellen.", "Workspaces stay open when switching. Separate copies protect your sources."), AutoSize = true, Anchor = AnchorStyles.Left, UseMnemonic = false }, 0, 0);
            var close = StudioChrome.ActionButton(L.T("Schließen", "Close"));
            close.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(close, 1, 0);
            footer.SizeChanged += delegate { ((Label)footer.GetControlFromPosition(0, 0)).MaximumSize = new Size(Math.Max(80, footer.Width - close.Width - footer.Padding.Horizontal - 24), 0); };
            CancelButton = close;
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);
        }

        private void Open(string key)
        {
            try
            {
                Close();
                _owner.OpenWorkspace(key);
            }
            catch (Exception error) { StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static Control Tile(string title, string detail, StudioIcon icon, Action click)
        {
            var tile = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(10), BackColor = DarkTheme.Panel };
            tile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tile.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var button = new Button { Text = title, UseMnemonic = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Margin = Padding.Empty, Padding = new Padding(8, 0, 4, 0), TextImageRelation = TextImageRelation.ImageBeforeText, ImageAlign = ContentAlignment.MiddleLeft, AccessibleName = title, AccessibleDescription = detail };
            button.FlatAppearance.BorderSize = 0;
            var image = StudioIcons.Create(icon, 22, DarkTheme.Fore);
            button.Image = image;
            button.Disposed += delegate { image.Dispose(); };
            button.Click += delegate { click(); };
            StudioUx.SetHelp(button, detail);
            var description = new Label { Text = detail, Dock = DockStyle.Fill, UseMnemonic = false, ForeColor = DarkTheme.Muted, Padding = new Padding(8, 2, 4, 0), Margin = Padding.Empty };
            tile.Controls.Add(button, 0, 0);
            tile.Controls.Add(description, 0, 1);
            return tile;
        }

        private void AddTool(FlowLayoutPanel host, string title, string detail, string key, StudioIcon icon)
        {
            var tile = Tile(title, detail, icon, () => Open(key));
            tile.Dock = DockStyle.None;
            tile.Size = new Size(360, 122);
            host.Controls.Add(tile);
        }

        private void AddFileType(FlowLayoutPanel host, string title, string detail, string pattern, StudioIcon icon)
        {
            var tile = Tile(title, detail, icon, () => Pick(title + "|" + pattern + "|" + L.T("Alle Dateien", "All files") + "|*.*"));
            tile.Dock = DockStyle.None;
            tile.Size = new Size(360, 122);
            host.Controls.Add(tile);
        }

        private static void FitTiles(FlowLayoutPanel host)
        {
            int available = Math.Max(1, host.ClientSize.Width - host.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            int columns = Math.Max(1, available / 300);
            foreach (Control tile in host.Controls) tile.Width = Math.Max(100, available / columns - tile.Margin.Horizontal);
        }

        private void Pick(string filter)
        {
            using (var dialog = new OpenFileDialog { Filter = filter, Title = L.T("Nintendo-Wii-Datei öffnen", "Open Nintendo Wii file") })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Close();
                _owner.OpenWorkspace("archive");
                _owner.OpenFromPath(dialog.FileName);
            }
        }
    }
}
