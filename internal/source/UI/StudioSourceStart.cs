using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioSourceStart : StudioAmbientPanel
    {
        readonly Label step, title, description;
        readonly FlowLayoutPanel actions;
        readonly TextBox details;
        readonly TableLayoutPanel card;
        readonly ComboBox packs;
        readonly Button create;
        readonly Button[] sources;
        readonly string purpose;
        bool? previous;

        internal StudioSourceStart(Form tool, ComboBox packChoice, Button createPack, Button[] sourceActions)
        {
            Name = "CustomFilesHint";
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = DarkTheme.Back;
            packs = packChoice; create = createPack; sources = sourceActions;
            purpose = Purpose(tool.GetType().Name);
            card = new TableLayoutPanel { ColumnCount = 1, RowCount = 6, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(28), BackColor = DarkTheme.Panel, Margin = Padding.Empty };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            step = new Label { AutoSize = true, ForeColor = DarkTheme.Focus, Font = StudioTypography.Eyebrow, Margin = new Padding(0, 0, 0, 14) };
            title = new Label { AutoSize = true, ForeColor = DarkTheme.Fore, Font = StudioTypography.EntryTitle, Margin = new Padding(0, 0, 0, 12) };
            description = new Label { AutoSize = true, ForeColor = DarkTheme.Muted, Font = StudioTypography.EntryDescription, Margin = new Padding(0, 0, 0, 24) };
            actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
            var more = StudioChrome.ActionButton(L.T("Dateien & Hilfe", "Files & guidance"));
            more.Margin = new Padding(0, 18, 0, 0);
            details = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical, Height = 150, Visible = false, BackColor = DarkTheme.Panel, ForeColor = DarkTheme.Muted,
                Margin = new Padding(0, 12, 0, 0), AccessibleName = L.T("Benötigte Dateien", "Required files") };
            more.Click += delegate { details.Visible = !details.Visible; Arrange(); };
            card.Controls.Add(step, 0, 0); card.Controls.Add(title, 0, 1); card.Controls.Add(description, 0, 2);
            card.Controls.Add(actions, 0, 3); card.Controls.Add(more, 0, 4); card.Controls.Add(details, 0, 5);
            Controls.Add(card);
            SizeChanged += delegate { Arrange(); };
            card.SizeChanged += delegate { Arrange(); };
        }

        internal void ShowStep(bool hasPack, string help)
        {
            details.Text = help;
            if (previous == hasPack) return;
            previous = hasPack;
            step.Text = hasPack ? L.T("02  QUELLE", "02  SOURCE") : L.T("01  DEIN PACK", "01  YOUR PACK");
            title.Text = hasPack ? L.T("Womit möchtest du starten?", "Where would you like to start?") : L.T("Wähle dein Arbeits-Pack", "Choose your working pack");
            description.Text = hasPack ? purpose : L.T("Wähle oben ein vorhandenes Custom Pack aus oder erstelle ein neues. Danach führt dich das Werkzeug durch die Bearbeitung.",
                "Select an existing custom pack above or create a new one. Then this tool will guide you through editing.");
            foreach (Control action in actions.Controls.Cast<Control>().ToArray()) action.Dispose();
            if (!hasPack)
            {
                Add(L.T("Pack auswählen", "Select a pack"), delegate { packs.Focus(); packs.DroppedDown = true; }, true);
                Add(L.T("Neues Pack", "New pack"), delegate { create.PerformClick(); }, false);
            }
            else
            {
                foreach (Button source in sources)
                {
                    Button original = source;
                    Add(source.Text.TrimEnd('.', '…'), delegate { original.PerformClick(); }, true);
                }
                if (actions.Controls.Count == 0)
                    Add(L.T("Pack wechseln", "Change pack"), delegate { packs.Focus(); packs.DroppedDown = true; }, true);
            }
            DarkTheme.Apply(card);
            Arrange();
        }

        void Add(string caption, Action action, bool primary)
        {
            var button = StudioChrome.ActionButton(caption);
            button.MinimumSize = new Size(136, 40);
            button.Margin = new Padding(0, 0, 10, 8);
            button.Click += delegate { action(); };
            actions.Controls.Add(button);
            if (primary) DarkTheme.StylePrimary(button);
        }

        bool arranging;
        void Arrange()
        {
            if (arranging || card == null) return;
            arranging = true;
            try
            {
                int width = Math.Max(260, Math.Min(690, ClientSize.Width - 40));
                card.Width = width;
                int textWidth = Math.Max(120, width - card.Padding.Horizontal);
                title.MaximumSize = description.MaximumSize = new Size(textWidth, 0);
                card.Location = new Point(Math.Max(0, (ClientSize.Width - width) / 2), Math.Max(20, Math.Min(90, (ClientSize.Height - card.Height) / 3)));
            }
            finally { arranging = false; }
        }

        static string Purpose(string tool)
        {
            switch (tool)
            {
                case "CharacterBuilderForm": return L.T("Wähle den Charakter, den du ersetzen möchtest, oder setze ein gespeichertes Projekt fort.", "Choose the character you want to replace, or continue a saved project.");
                case "FontChangerForm": return L.T("Öffne Font.szs. Danach wählst du Schrift und Bereiche, prüfst die Vorschau und speicherst deine Kopie.", "Open Font.szs, choose a font and its scope, review the preview and save your copy.");
                case "GameHudForm": return L.T("Öffne die passende Menüdatei. Wähle ein Layout und gestalte seine Elemente direkt in der Vorschau.", "Open a menu archive, select a layout and edit its elements directly in the preview.");
                case "RaceHudForm": return L.T("Öffne Race.szs und die passende Sprachdatei für Zahlen, Items und Minimap.", "Open Race.szs and its language archive for numbers, items and the minimap.");
                case "MenuTextForm": return L.T("Öffne UIAssets.szs oder RaceAssets.szs. Suche anschließend den Text, den du ändern möchtest.", "Open UIAssets.szs or RaceAssets.szs, then find the text you want to change.");
                case "MenuTextureForm": return L.T("Öffne ein Menüarchiv. Wähle darin ein Bild, ersetze oder färbe es und speichere eine Kopie.", "Open a menu archive, choose an image, replace or recolour it and save a copy.");
                case "RetroRewindGifWizard": return L.T("Öffne eine RR-Menüdatei. Wähle dann den Hintergrund, das Titelbild oder das Menümodell, das du gestalten möchtest.", "Open an RR menu archive, then choose the background, title image or menu model you want to change.");
                case "ArchiveMergeForm": case "ArchiveCompareForm": return L.T("Beginne mit dem unveränderten Original. Ergänze danach die bearbeiteten Kopien und wähle die Änderungen aus.", "Start with the unchanged original, then add edited copies and choose which changes to keep.");
                case "ThemeProjectForm": return L.T("Beginne eine Zusammenstellung für dein Pack oder öffne ein Theme-Projekt zum Weiterarbeiten.", "Start assembling files for your pack, or open a theme project to continue.");
                case "MusicLoopForm": return L.T("Öffne WAV-Audio oder wandle eine Audiodatei um. Danach setzt du Anfang und Ende des Loops in der Wellenform.", "Open WAV audio or convert an audio file, then set the loop start and end in the waveform.");
                case "RaceEffectsForm": return L.T("Öffne Common.szs oder passende RR-Effektarchive. Wähle eine Effektgruppe und gestalte ihre Farben.", "Open Common.szs or matching RR effect archives, then choose an effect group and its colours.");
                case "PackWorkbenchForm": return L.T("Öffne dein Pack, um Dateien zu prüfen, einen Stand zu sichern oder eine getrennte Testkopie vorzubereiten.", "Open your pack to check files, save a snapshot or prepare a separate test copy.");
                default: return L.T("Öffne die passende Quelldatei. Die Bearbeitung erscheint, sobald sie erfolgreich geladen wurde.", "Open a supported source file. Editing appears once it has loaded successfully.");
            }
        }
    }
}
