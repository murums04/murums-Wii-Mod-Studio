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
            : base("Character check", "Actual font conversion results • Missing characters keep their original appearance")
        {
            var filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            filter.Items.AddRange(new object[] { "All characters", "Replaced", "Missing in TTF", "Protected", "No usable outline" });
            var search = new TextBox { Width = 220 };
            Actions.Controls.Add(filter);
            Actions.Controls.Add(new Label { Text = "Find character / U+ code", AutoSize = true, Margin = new Padding(8) });
            Actions.Controls.Add(search);
            var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            list.Columns.Add("Character", 90);
            list.Columns.Add("Code", 90);
            list.Columns.Add("Result", 310);
            list.Columns.Add("Font", 300);
            Body.Controls.Add(list);
            Action refresh = delegate {
                list.BeginUpdate(); list.Items.Clear();
                foreach (var font in reports)
                    foreach (var item in font.Value.OrderBy(x => x.Key))
                    {
                        string glyph = Char.IsControl((char)item.Key) ? "" : Char.ConvertFromUtf32(item.Key);
                        string code = "U+" + item.Key.ToString("X4");
                        if (filter.SelectedIndex > 0 && !item.Value.StartsWith((string)filter.SelectedItem, StringComparison.Ordinal)) continue;
                        if (search.Text.Length > 0 && !glyph.Contains(search.Text) && code.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        list.Items.Add(new ListViewItem(new[] { glyph, code, item.Value, System.IO.Path.GetFileName(font.Key) }));
                    }
                list.EndUpdate();
                Status.Text = list.Items.Count + " character mappings shown. Shared glyph aliases remain linked. HUD picture archives are not BRFNT fonts.";
            };
            filter.SelectedIndexChanged += delegate { refresh(); };
            search.TextChanged += delegate { refresh(); };
            Finish();
            list.BackColor = DarkTheme.Panel2;
            list.ForeColor = Color.White;
            filter.SelectedIndex = 0;
        }
    }

    internal sealed class FontArchiveInfoForm : StudioToolForm
    {
        internal FontArchiveInfoForm(string details) : base("Font archive overview", "Required sources • Loaded files • Missing files")
        {
            var text = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = details.Replace("\n", Environment.NewLine) };
            Body.Controls.Add(text);
            Finish();
        }
    }
}