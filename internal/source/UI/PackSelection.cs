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
        private sealed class State { internal CustomPack Pack; internal Action RefreshGate; }
        private static readonly ConditionalWeakTable<Form, State> states = new ConditionalWeakTable<Form, State>();

        internal static string Folder(Form form)
        {
            State state;
            return form != null && states.TryGetValue(form, out state) && state.Pack != null && Directory.Exists(state.Pack.FilesFolder) ? state.Pack.FilesFolder : "";
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
            var strip = new PackSelectionStrip { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, Padding = new Padding(4) };
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            strip.Controls.Add(new Label { Text = "Custom", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
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
            foreach (Button source in Descendants(form).OfType<Button>().Where(b => b.FindForm() == form && IsSourceButton(b)).ToArray())
            {
                source.Parent.Controls.Remove(source);
                source.Name = "PackSourceAction";
                source.Dock = DockStyle.None;
                source.AutoSize = true;
                source.MinimumSize = new Size(130, 30);
                source.Margin = new Padding(3);
                int column = strip.ColumnCount++;
                strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                strip.Controls.Add(source, column, 0);
            }
            form.VisibleChanged += delegate { strip.UpdateAnimation(); };
            choice.DropDown += delegate { strip.Paused = true; };
            choice.DropDownClosed += delegate { strip.Paused = false; };
            var gates = new System.Collections.Generic.List<Control>();
            Action updateGate = delegate
            {
                bool available = state.Pack != null && Directory.Exists(state.Pack.FilesFolder);
                if (!form.TopLevel)
                {
                    var parent = form.TopLevelControl as Form;
                    if (parent != null && parent != form) available |= !String.IsNullOrEmpty(Folder(parent));
                }
                strip.Required = !available;
                choice.AccessibleDescription = available
                    ? L.T("Pack ausgewählt – die Werkzeuge darunter sind bereit.", "Pack selected — the tools below are ready.")
                    : L.T("Zuerst ein Custom Pack auswählen oder erstellen, um die Werkzeuge freizuschalten.",
                        "Select or create a Custom Pack first to unlock the tools below.");
                foreach (Control gate in gates) gate.Enabled = available;
                foreach (Form child in Descendants(form).OfType<Form>())
                {
                    State nested;
                    if (states.TryGetValue(child, out nested) && nested.RefreshGate != null) nested.RefreshGate();
                }
            };
            state.RefreshGate = updateGate;
            form.Activated += delegate { updateGate(); };
            form.ParentChanged += delegate { updateGate(); };
            bool loading = false;
            Action reload = delegate
            {
                string previous = state.Pack == null ? "" : state.Pack.FilesFolder;
                loading = true;
                try
                {
                    var packs = CustomPacks.Load();
                    choice.Items.Clear();
                    choice.Items.Add(L.T("Kein Pack ausgewählt – zuerst auswählen oder erstellen", "No pack selected — select or create one first"));
                    foreach (var pack in packs) choice.Items.Add(pack);
                    choice.SelectedIndex = Math.Max(0, packs.FindIndex(p => p.FilesFolder == previous) + 1);
                    state.Pack = choice.SelectedItem as CustomPack;
                    if (state.Pack != null && !Directory.Exists(state.Pack.FilesFolder))
                    {
                        choice.SelectedIndex = 0;
                        state.Pack = null;
                    }
                }
                catch (Exception error) { state.Pack = null; StudioMessageBox.Show(form, error.Message, "Custom packs", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { loading = false; updateGate(); }
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
                updateGate();
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
                foreach (Control child in layout.Controls.Cast<Control>().ToArray())
                    if (layout.GetRow(child) >= 2) AddGate(child, gates);
                updateGate();
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
            AddGate(content, gates);
            foreach (Control extra in form.Controls.Cast<Control>().Where(c => c != wrapper && !(c is AccentStrip)).ToArray())
                AddGate(extra, gates);
            updateGate();
            DarkTheme.Apply(wrapper);
        }
        private static bool IsSourceButton(Button button)
        {
            if (button.Name == "PackSourceAction") return true;
            string text = button.Text;
            return text.StartsWith("Browse ISO", StringComparison.Ordinal)
                || text.StartsWith("ISO/WBFS auswählen", StringComparison.Ordinal)
                || text.StartsWith("Add archive", StringComparison.Ordinal)
                || text.StartsWith("Archiv / ISO hinzufügen", StringComparison.Ordinal)
                || text.StartsWith("Open base archive", StringComparison.Ordinal)
                || text.StartsWith("Open edited archive", StringComparison.Ordinal)
                || text.StartsWith("Open PCM WAV", StringComparison.Ordinal)
                || text.StartsWith("Convert other audio", StringComparison.Ordinal)
                || text.StartsWith("New project", StringComparison.Ordinal)
                || text.StartsWith("Open project", StringComparison.Ordinal);
        }
        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control control)
        {
            foreach (Control child in control.Controls)
            {
                yield return child;
                foreach (Control nested in Descendants(child)) yield return nested;
            }
        }

        private static void AddGate(Control control, System.Collections.Generic.List<Control> gates)
        {
            if (control is Label || control is StudioProgressBar) return;
            if (Descendants(control).Any(c => c is StudioProgressBar) && !Descendants(control).Any(c => c is Button)) return;
            if (Descendants(control).OfType<Button>().Any(b => b.Text == "Close" || b.Text == "Schliessen"))
            {
                foreach (Control child in control.Controls.Cast<Control>().ToArray()) AddGate(child, gates);
                return;
            }
            var button = control as Button;
            if (button != null && (button.Text == "Close" || button.Text == "Schliessen")) return;
            if (control is Button)
            {
                var parent = control.Parent;
                int index = parent.Controls.GetChildIndex(control);
                var wrapper = new Panel { AutoSize = true, Margin = control.Margin, MinimumSize = control.Size };
                parent.Controls.Remove(control);
                control.Margin = Padding.Empty;
                wrapper.Controls.Add(control);
                parent.Controls.Add(wrapper);
                parent.Controls.SetChildIndex(wrapper, index);
                gates.Add(wrapper);
            }
            else gates.Add(control);
        }
    }
}