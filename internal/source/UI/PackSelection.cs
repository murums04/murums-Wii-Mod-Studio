using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class PackSelection
    {
        private sealed class State { internal CustomPack Pack; }
        private static readonly ConditionalWeakTable<Form, State> states = new ConditionalWeakTable<Form, State>();

        internal static string Folder(Form form)
        {
            State state;
            return form != null && states.TryGetValue(form, out state) && state.Pack != null ? state.Pack.FilesFolder : "";
        }

        internal static string Output(Form form, string fallback)
        {
            string folder = Folder(form);
            return String.IsNullOrEmpty(folder) ? fallback : Path.Combine(folder, "MUR_EDITED");
        }

        internal static void Attach(Form form, Action<CustomPack> changed = null)
        {
            State state;
            if (states.TryGetValue(form, out state)) return;
            state = new State();
            states.Add(form, state);
            var strip = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, Padding = new Padding(4) };
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            strip.Controls.Add(new Label { Text = "Custom pack", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            var choice = new ComboBox { Name = "CustomPackSelector", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 760 };
            strip.Controls.Add(choice, 1, 0);
            var refresh = new Button { Text = L.T("Aktualisieren", "Refresh"), AutoSize = true };
            strip.Controls.Add(refresh, 2, 0);
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var create = new Button {
                Name = "CreateCustomPack",
                Text = L.T("Custom Pack erstellen…", "Create custom pack…"),
                AutoSize = true,
                MinimumSize = new Size(170, 30)
            };
            strip.Controls.Add(create, 3, 0);
            bool loading = false;
            Action reload = delegate
            {
                string previous = state.Pack == null ? "" : state.Pack.FilesFolder;
                loading = true;
                try
                {
                    var packs = CustomPacks.Load();
                    choice.Items.Clear();
                    choice.Items.Add(L.T("Kein Pack ausgewählt", "No pack selected"));
                    foreach (var pack in packs) choice.Items.Add(pack);
                    choice.SelectedIndex = Math.Max(0, packs.FindIndex(p => p.FilesFolder == previous) + 1);
                    state.Pack = choice.SelectedItem as CustomPack;
                }
                catch (Exception error) { StudioMessageBox.Show(form, error.Message, "Custom packs", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { loading = false; }
            };
            choice.SelectedIndexChanged += delegate
            {
                if (loading) return;
                var selected = choice.SelectedItem as CustomPack;
                if (selected != null && !Directory.Exists(selected.FilesFolder))
                {
                    StudioMessageBox.Show(form, L.T("Der Pack-Ordner wurde verschoben oder entfernt.", "The pack folder was moved or removed."),
                        "Custom packs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    choice.SelectedIndex = 0;
                    return;
                }
                state.Pack = selected;
                if (changed != null && selected != null) changed(selected);
            };
            refresh.Click += delegate { reload(); };
            create.Click += delegate
            {
                try
                {
                    using (var maker = new CustomPackMakerForm())
                        maker.ShowDialog(form.TopLevelControl as Form ?? form);
                }
                catch (Exception error)
                {
                    StudioMessageBox.Show(form, error.Message, "Custom packs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { reload(); }
            };
            reload();
            Control content = form.Controls.Cast<Control>().FirstOrDefault(c => c.Dock == DockStyle.Fill);
            if (content == null) return;
            var layout = content as TableLayoutPanel;
            if (layout != null && layout.ColumnCount == 1)
            {
                layout.SuspendLayout();
                layout.RowCount++;
                foreach (Control child in layout.Controls.Cast<Control>().OrderByDescending(c => layout.GetRow(c)).ToArray())
                {
                    int row = layout.GetRow(child);
                    if (row >= 1) layout.SetRow(child, row + 1);
                }
                layout.RowStyles.Insert(1, new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(strip, 0, 1);
                layout.ResumeLayout(true);
                StudioUx.DisableHover(choice);
                DarkTheme.Apply(strip);
                return;
            }
            form.Controls.Remove(content);
            var wrapper = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            wrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            wrapper.Controls.Add(strip, 0, 0);
            wrapper.Controls.Add(content, 0, 1);
            form.Controls.Add(wrapper);
            StudioUx.DisableHover(choice);
            StudioUx.SetHelp(refresh, L.T("Pack-Liste neu laden. Die Auswahl setzt den Startordner für Dateien und MUR_EDITED-Ausgaben.",
                "Reload packs. Selecting a pack sets the file browsing folder and MUR_EDITED output."));
            DarkTheme.Apply(wrapper);
        }
    }
}