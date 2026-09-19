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
            Text = "murums Wii Mod Studio — " + L.T("Start", "Start Center");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 590);
            Size = new Size(930, 660);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.5F);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            DarkTheme.Apply(this);
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            Controls.Add(root);
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = DarkTheme.Back;
            Label title = new Label();
            title.AutoSize = true;
            title.Text = L.T("Was möchtest du bearbeiten?", "What do you want to edit?");
            title.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Left = 4;
            title.Top = 4;
            header.Controls.Add(title);
            Label sub = new Label();
            sub.AutoSize = true;
            sub.Text = L.T("Du musst den Dateityp nicht kennen. Wähle einen Bereich oder öffne einfach eine Datei.", "You do not need to know the file type. Pick an area or simply open a file.");
            sub.ForeColor = DarkTheme.Muted;
            sub.Left = 6;
            sub.Top = 50;
            header.Controls.Add(sub);
            root.Controls.Add(header, 0, 0);
            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 2;
            cards.RowCount = 4;
            cards.Padding = new Padding(0, 6, 0, 6);
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int r = 0; r < 4; r++)
                cards.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            cards.Controls.Add(Card(L.T("Archive / SZS", "Archives / SZS"), L.T("SZS, U8, Yaz0 öffnen, Dateien ersetzen, importieren und speichern", "Open SZS/U8/Yaz0, replace/import files and save"), "*.szs;*.arc;*.u8;*.rarc"), 0, 0);
            cards.Controls.Add(Card(L.T("UI / Menüs", "UI / Menus"), L.T("TPL, BRLAN/GIF und visueller BRLYT-Editor", "TPL, BRLAN/GIF and visual BRLYT editor"), "*.brlan;*.brlyt;*.tpl;*.bti;*.brctr;*.brfnt"), 1, 0);
            cards.Controls.Add(Card(L.T("Strecke", "Course / Track"), L.T("KMP, KCL, LEX und Strecken-BRRES prüfen / bearbeiten", "Inspect/edit KMP, KCL, LEX and course BRRES"), "*.kmp;*.kcl;*.lex;*.brres"), 0, 1);
            cards.Controls.Add(Card(L.T("3D-Modelle", "3D Models"), L.T("BRRES, MDL0/TEX0 und Animationen mit RiiStudio-Brücke", "BRRES, MDL0/TEX0 and animations with RiiStudio bridge"), "*.brres;*.mdl0;*.tex0;*.pat0;*.srt0;*.chr0;*.clr0;*.shp0;*.scn0;*.bmd;*.bdl"), 1, 1);
            cards.Controls.Add(Card(L.T("Text", "Text"), L.T("BMG direkt als Text bearbeiten und wieder speichern", "Edit BMG directly as text and save it back"), "*.bmg"), 0, 2);
            cards.Controls.Add(Card(L.T("Musik / Sounds", "Music / Sounds"), L.T("BRSTM, BRSAR und Nintendo-Soundressourcen analysieren", "Inspect BRSTM, BRSAR and Nintendo sound resources"), "*.brstm;*.brsar;*.brseq;*.brwav;*.brbnk;*.brwsd"), 1, 2);
            cards.Controls.Add(Card(L.T("Effekte / Video", "Effects / Video"), L.T("BREFF, BREFT und THP sicher untersuchen", "Safely inspect BREFF, BREFT and THP"), "*.breff;*.breft;*.jpa;*.thp"), 0, 3);
            cards.Controls.Add(Card(L.T("Advanced", "Advanced"), L.T("REL, DOL, Disc-Images, Ghosts und Binärdateien", "REL, DOL, disc images, ghosts and binaries"), "*.rel;*.dol;*.iso;*.wbfs;*.wia;*.gcz;*.ciso;*.rkg;*.crkg;*.rksys;*.bin"), 1, 3);
            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            DarkTheme.StyleTabs(tabs);
            root.Controls.Add(tabs, 0, 1);
            TabPage simple = new TabPage(L.T("Einfach starten", "Easy start"));
            TabPage advanced = new TabPage(L.T("Alle Werkzeuge", "All tools"));
            tabs.TabPages.Add(simple);
            tabs.TabPages.Add(advanced);
            advanced.Controls.Add(cards);
            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.RowCount = 3;
            actions.ColumnCount = 1;
            for (int i = 0; i < 3; i++)
                actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            simple.Controls.Add(actions);
            actions.Controls.Add(Workflow(L.T("Retro-Rewind-Hintergrund ändern", "Change a Retro Rewind background"), L.T("Dein Foto oder GIF für Titel, Einzelspieler oder andere Menüs. Der Assistent führt dich durch die Auswahl.", "Use your photo or GIF for the title screen, single player or other menus. Follow the guided steps."), delegate
            {
                Hide();
                _owner.OpenRetroRewindGifWizard();
                Close();
            }), 0, 0);
            actions.Controls.Add(Workflow(L.T("Menübilder und Symbole ändern", "Change menu pictures and icons"), L.T("Menüdatei auswählen, Texturen als Bilder durchsuchen und dein eigenes Bild einsetzen.", "Choose a menu file, browse texture pictures and insert your own image."), delegate
            {
                PickTextures();
            }), 0, 1);
            actions.Controls.Add(Workflow(L.T("Vorhandene Datei öffnen", "Open an existing file"), L.T("Einzelne Texturen, Animationen oder Archive bearbeiten. Der Dateityp wird erkannt.", "Edit individual textures, animations or archives. The file type is detected automatically."), delegate
            {
                Pick(ResourceDetector.OpenFilter);
            }), 0, 2);
            Label hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = L.T("Tipp: Archivänderungen mit Strg+Z rückgängig machen. Mit „Speichern unter“ eine Testkopie erstellen.", "Tip: undo archive changes with Ctrl+Z. Use Save as to create a test copy.");
            root.Controls.Add(hint, 0, 2);
        }

        private Button Workflow(string title, string detail, EventHandler click)
        {
            Button button = new Button();
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(8);
            button.Padding = new Padding(18);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Text = title + Environment.NewLine + Environment.NewLine + detail;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = DarkTheme.Accent;
            button.BackColor = DarkTheme.Panel2;
            button.ForeColor = DarkTheme.Fore;
            button.Click += click;
            return button;
        }

        private void PickTextures()
        {
            string path = GameArchiveImportForm.Select(this, ToolArchiveFilters.Menus);
            if (path == null)
                return;
            Hide();
            _owner.BrowseTexturesFromFile(path);
            Close();
        }
        private Control Card(string titleText, string description, string pattern)
        {
            Button b = new Button();
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(6);
            b.Padding = new Padding(14, 8, 14, 8);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Font = new Font("Segoe UI", 10.5F);
            b.Text = titleText + Environment.NewLine + description;
            b.Tag = pattern;
            b.Click += delegate
            {
                Pick(titleText + "|" + pattern + "|" + L.T("Alle Dateien", "All files") + "|*.*");
            };
            return b;
        }

        private void Pick(string filter)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = filter;
                d.Title = L.T("Nintendo-Wii-Datei öffnen", "Open Nintendo Wii file");
                if (d.ShowDialog(this) != DialogResult.OK)
                    return;
                Hide();
                _owner.OpenFromPath(d.FileName);
                Close();
            }
        }
    }
}
