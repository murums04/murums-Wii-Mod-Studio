using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class FontSourcePicker : Form
    {
        internal string SelectedPath;
        internal static string[] RrFonts(string root)
        {
            var files = new List<string>();
            string common = Path.Combine(root, "UI", "Font.szs");
            if (File.Exists(common)) files.Add(common);
            string languages = Path.Combine(root, "Language");
            if (Directory.Exists(languages))
                foreach (string language in Directory.GetDirectories(languages).OrderBy(p => p))
                {
                    string font = Path.Combine(language, "Font", "Font.szs");
                    if (File.Exists(font)) files.Add(font);
                    string home = Path.Combine(language, "WiiStuffs");
                    if (Directory.Exists(home)) files.AddRange(Directory.GetFiles(home, "homeBtn*.szs"));
                }
            return files.ToArray();
        }

        internal FontSourcePicker(string[] roots)
        {
            Text = L.T("Schriftdatei öffnen", "Open font file");
            Font = new Font("Segoe UI", 9);
            ClientSize = new Size(900, 440);
            MinimumSize = new Size(700, 350);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 12), Text = L.T(
                "RR-Schriften: UI/Font.szs enthält die Spielschriften. Wähle bei Bedarf deine Sprachvariante.\nWiiStuffs/homeBtn betrifft nur das HOME-Menü. Bildschrift und Rundenzahlen: Race HUD / Menu Textures.\nÜber Browse kannst du auch die Font.szs aus deinem Custom Pack öffnen.",
                "RR fonts: UI/Font.szs contains the game fonts. Choose your language variant where needed.\nWiiStuffs/homeBtn is for the HOME menu only. Picture lettering and lap numbers: Race HUD / Menu Textures.\nUse Browse to open Font.szs from your custom pack.") }, 0, 0);
            var list = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
            list.FormattingEnabled = true;
            list.Format += delegate(object sender, ListControlConvertEventArgs e) {
                string path = e.ListItem as string;
                if (path != null) {
                    string root = roots.FirstOrDefault(r => path.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
                    e.Value = root == null ? path : path.Substring(root.Length).TrimStart('\\') + " — " + root;
                }
            };
            list.Items.AddRange(roots.SelectMany(RrFonts).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            layout.Controls.Add(list, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var cancel = new Button { Text = L.T("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var open = new Button { Text = L.T("Auswahl öffnen", "Open selected"), AutoSize = true, Enabled = false };
            var browse = new Button { Text = "Browse", AutoSize = true };
            list.SelectedIndexChanged += delegate { open.Enabled = list.SelectedIndex >= 0; };
            open.Click += delegate { SelectedPath = (string)list.SelectedItem; DialogResult = DialogResult.OK; };
            browse.Click += delegate {
                using (var picker = new OpenFileDialog { Filter = "Wii fonts|*.szs;*.arc;*.brfnt" })
                    if (picker.ShowDialog(this) == DialogResult.OK) { SelectedPath = picker.FileName; DialogResult = DialogResult.OK; }
            };
            buttons.Controls.Add(cancel); buttons.Controls.Add(open); buttons.Controls.Add(browse);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout); CancelButton = cancel; DarkTheme.Apply(this);
        }
    }
}
