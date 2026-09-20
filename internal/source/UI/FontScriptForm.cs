using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class FontScriptForm : StudioToolForm
    {
        readonly string[] paths = new string[3];
        readonly Label[] names = new Label[3];
        readonly Label[] coverage = new Label[3];
        readonly Button apply;
        readonly Button[] mainButtons = new Button[3];
        int mainScript;
        internal FontScriptSources Selection { get; private set; }

        internal FontScriptForm(FontScriptSources current)
            : base(L.T("Schriften auswählen", "Choose your fonts"), L.T("Eine Hauptschrift genügt • Weitere Schriften sind optional", "One main font is enough • Additional fonts are optional"))
        {
            MinimumSize = new Size(900, 660);
            Size = new Size(980, 680);
            mainScript = current.MainScript;
            paths[0] = current.Latin;
            paths[1] = current.Japanese;
            paths[2] = current.Other;
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 7, Padding = new Padding(4) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            string[] titles = {
                L.T("Latein und Zahlen\nABC · äöü · 0123", "Latin and numbers\nABC · äöü · 0123"),
                L.T("Japanisch\n日本語 · あいう · アイウ", "Japanese\n日本語 · あいう · アイウ"),
                L.T("Weitere Alphabete\nα β γ · Д Ж Я · 한글", "More alphabets\nα β γ · Д Ж Я · 한글")
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                grid.Controls.Add(new Label { Text = titles[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, i * 2);
                names[i] = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
                grid.Controls.Add(names[i], 1, i * 2);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
                var choose = new Button { Text = L.T("TTF wählen…", "Choose TTF…"), AutoSize = true, Height = 32 };
                choose.Click += delegate { Guard(delegate { Choose(index); }); };
                buttons.Controls.Add(choose);
                var main = new Button { AutoSize = true, Height = 32 };
                main.Click += delegate { Guard(delegate {
                    if (String.IsNullOrEmpty(paths[index])) Choose(index);
                    if (!String.IsNullOrEmpty(paths[index])) { mainScript = index; RefreshRows(); }
                }); };
                mainButtons[i] = main;
                buttons.Controls.Add(main);
                grid.Controls.Add(buttons, 2, i * 2);
                coverage[i] = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
                grid.Controls.Add(coverage[i], 1, i * 2 + 1);
                grid.SetColumnSpan(coverage[i], 2);
            }
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var guide = new Label { Dock = DockStyle.Fill, Text = L.T(
                "Wähle eine Schrift und markiere sie als Hauptschrift. Jede der drei Zeilen kann die Hauptschrift sein.\nOhne eigene Auswahl wird die Hauptschrift verwendet. Fehlt ihr ein Zeichen, bleibt das Original erhalten.\nSterne, Pokale und Positionsnummern bleiben unverändert.",
                "Choose a font and mark it as Main. Any of the three rows can be your main font.\nWithout a separate selection, the main font is used. Characters it does not contain stay original.\nStars, trophies and position numbers stay unchanged.") };
            grid.Controls.Add(guide, 0, 6);
            grid.SetColumnSpan(guide, 3);
            Body.Controls.Add(grid);
            apply = ExportAction(L.T("Schriften übernehmen", "Use these fonts"), L.T("Auswahl für Vorschau und Export übernehmen.", "Use this selection for preview and export."), delegate {
                if (String.IsNullOrEmpty(paths[mainScript])) throw new InvalidOperationException(L.T("Wähle zuerst deine Hauptschrift.", "Choose your main font first."));
                for (int i = 0; i < 3; i++)
                    if (!String.IsNullOrEmpty(paths[i])) FontScriptSources.Validate(paths[i], i);
                var result = new FontScriptSources { Latin = paths[0], Japanese = paths[1], Other = paths[2], MainScript = mainScript };
                using (var check = new FontScriptCollection(result)) { }
                Selection = result;
                DialogResult = DialogResult.OK;
                Close();
            });
            var cancel = ExportAction(L.T("Abbrechen", "Cancel"), L.T("Bisherige Schriften behalten.", "Keep the previous fonts."), delegate { DialogResult = DialogResult.Cancel; Close(); }, false);
            CancelButton = cancel;
            Finish();
            RefreshRows();
        }

        void RefreshRows()
        {
            for (int i = 0; i < 3; i++)
            {
                names[i].Text = String.IsNullOrEmpty(paths[i]) ? (i == mainScript ? L.T("Noch keine Schrift gewählt", "No font chosen yet") : L.T("Hauptschrift verwenden", "Use the main font")) : Path.GetFileName(paths[i]);
                StudioUx.SetHelp(names[i], paths[i] ?? L.T("Fehlende Zeichen bleiben im Original erhalten.", "Missing characters keep their original appearance."));
                try
                {
                    string example = i == 0 ? L.T("Lateinischer Menütext, Zahlen, TIME / LAP und km/h.", "Latin menu text, numbers, TIME / LAP and km/h.")
                        : i == 1 ? L.T("Kanji und Kana, z. B. in japanischen Spielernamen.", "Kanji and Kana, for example in Japanese player names.")
                        : L.T("Zum Beispiel Griechisch, Kyrillisch und Koreanisch.", "For example Greek, Cyrillic and Korean.");
                    coverage[i].Text = example + (String.IsNullOrEmpty(paths[i]) ? "" : "\n" + FontScriptSources.Validate(paths[i], i));
                }
                catch (Exception e) { coverage[i].Text = e.Message; }
            }
            apply.Enabled = !String.IsNullOrEmpty(paths[mainScript]);
            for (int i = 0; i < mainButtons.Length; i++)
            {
                mainButtons[i].Text = i == mainScript ? L.T("Hauptschrift", "Main") : L.T("Als Hauptschrift", "Use as main");
                mainButtons[i].BackColor = i == mainScript ? DarkTheme.Accent : DarkTheme.Accent2;
                mainButtons[i].ForeColor = Color.White;
                StudioUx.SetHelp(mainButtons[i], L.T("Für Bereiche ohne eigene Schrift verwenden. Bestehende Zuordnungen bleiben erhalten.", "Use for ranges without their own font. Existing assignments are kept."));
            }
            Status.Text = apply.Enabled ? L.T("Bereit. Zusätzliche Schriften sind freiwillig; die Zeichenauswahl wird geprüft.", "Ready. Additional fonts are optional; character support is checked.")
                : L.T("1. Wähle zuerst deine Hauptschrift.", "1. Choose your main font first.");
        }

        void Choose(int index)
        {
            using (var dialog = new OpenFileDialog { Filter = "TrueType font (*.ttf)|*.ttf", Title = index == 1 ? L.T("Japanische Schrift wählen (Kanji / Kana)", "Choose a Japanese font (Kanji / Kana)") : index == 2 ? L.T("Schrift für weitere Alphabete wählen, z. B. Griechisch", "Choose a font for more alphabets, e.g. Greek") : L.T("Schrift für Latein und Zahlen wählen", "Choose a font for Latin and numbers"), CheckFileExists = true })
            {
                if (!String.IsNullOrEmpty(paths[index])) dialog.InitialDirectory = Path.GetDirectoryName(paths[index]);
                dialog.FileOk += delegate(object sender, System.ComponentModel.CancelEventArgs e) {
                    try
                    {
                        FontScriptSources.Validate(dialog.FileName, index);
                        using (var face = new FontScriptCollection.Face(dialog.FileName)) { }
                    }
                    catch (Exception error)
                    {
                        e.Cancel = true;
                        StudioMessageBox.Show(this, error.Message, L.T("Schrift enthält keine passenden Zeichen", "Font does not contain matching characters"), MessageBoxButtons.OK);
                    }
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    paths[index] = dialog.FileName;
                    RefreshRows();
                }
            }
        }
    }
}
