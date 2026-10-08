using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace murumsWiiModStudio
{
    internal sealed class FontCharacterReviewForm : StudioToolForm
    {
        internal FontCharacterReviewForm(Dictionary<string, Dictionary<int, string>> reports)
            : base(L.T("Zeichenprüfung", "Character check"), L.T("Ergebnisse der Schriftumwandlung • Fehlende Zeichen behalten ihr ursprüngliches Aussehen", "Actual font conversion results • Missing characters keep their original appearance"))
        {
            var filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            // Filter verwenden die unveränderten Reportwerte, unabhängig von der Anzeigesprache.
            string[] prefixes = { "", "Replaced", "Missing in TTF", "Protected", "No usable outline" };
            filter.Items.AddRange(new object[] {
                L.T("Alle Zeichen", "All characters"), L.T("Ersetzt", "Replaced"),
                L.T("Fehlt in TTF", "Missing in TTF"), L.T("Geschützt", "Protected"),
                L.T("Keine nutzbare Kontur", "No usable outline")
            });
            var search = new TextBox { Width = 220 };
            Actions.Controls.Add(filter);
            Actions.Controls.Add(new Label { Text = L.T("Zeichen / U+-Code suchen", "Find character / U+ code"), AutoSize = true, Margin = new Padding(8) });
            Actions.Controls.Add(search);
            var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            list.Columns.Add(L.T("Zeichen", "Character"), 90);
            list.Columns.Add(L.T("Code", "Code"), 90);
            list.Columns.Add(L.T("Ergebnis", "Result"), 310);
            list.Columns.Add(L.T("Schrift", "Font"), 300);
            Body.Controls.Add(list);
            Action refresh = delegate {
                list.BeginUpdate();
                list.Items.Clear();
                foreach (var font in reports)
                    foreach (var item in font.Value.OrderBy(x => x.Key))
                    {
                        string glyph = Char.IsControl((char)item.Key) ? "" : Char.ConvertFromUtf32(item.Key);
                        string code = "U+" + item.Key.ToString("X4");
                        if (filter.SelectedIndex > 0 && !item.Value.StartsWith(prefixes[filter.SelectedIndex], StringComparison.Ordinal)) continue;
                        if (search.Text.Length > 0 && !glyph.Contains(search.Text) && code.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        list.Items.Add(new ListViewItem(new[] { glyph, code, DisplayResult(item.Value), System.IO.Path.GetFileName(font.Key) }));
                    }
                list.EndUpdate();
                Status.Text = L.F("{0} Zeichenzuordnungen angezeigt. Gemeinsame Glyphen bleiben verknüpft. HUD-Bildarchive sind keine BRFNT-Schriften.", "{0} character mappings shown. Shared glyph aliases remain linked. HUD picture archives are not BRFNT fonts.", list.Items.Count);
            };
            filter.SelectedIndexChanged += delegate { refresh(); };
            search.TextChanged += delegate { refresh(); };
            Finish();
            list.BackColor = DarkTheme.Panel2;
            list.ForeColor = Color.White;
            filter.SelectedIndex = 0;
        }

        static string DisplayResult(string result)
        {
            if (result.StartsWith("Replaced — ", StringComparison.Ordinal))
                return L.T("Ersetzt — ", "Replaced — ") + result.Substring("Replaced — ".Length);
            switch (result)
            {
                case "Protected symbol / control":
                    return L.T("Geschütztes Symbol / Steuerzeichen", result);
                case "Missing in TTF - original kept":
                    return L.T("Fehlt in TTF – Original erhalten", result);
                case "Protected shared glyph - original kept":
                    return L.T("Geschützte gemeinsame Glyphe – Original erhalten", result);
                case "No usable outline / space - original kept":
                    return L.T("Keine nutzbare Kontur / Leerzeichen – Original erhalten", result);
                default:
                    return result;
            }
        }
    }

    internal sealed class FontArchiveInfoForm : StudioToolForm
    {
        internal FontArchiveInfoForm(string details) : base(L.T("Schriftarchivübersicht", "Font archive overview"), L.T("Benötigte Quellen • Geladene Dateien • Fehlende Dateien", "Required sources • Loaded files • Missing files"))
        {
            var text = new StudioReadOnlyText { Text = details.Replace("\n", Environment.NewLine) };
            Body.Controls.Add(StudioReadOnlyText.Surface(text));
            Finish();
        }
    }
}
