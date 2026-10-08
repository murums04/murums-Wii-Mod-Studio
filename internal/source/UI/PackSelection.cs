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
        private sealed class State { internal CustomPack Pack; internal bool HasSource; internal string NextSource, SourceHint; internal Action RefreshGate; }
        private static readonly ConditionalWeakTable<Form, State> states = new ConditionalWeakTable<Form, State>();

        internal static string Folder(Form form)
        {
            State state;
            return form != null && states.TryGetValue(form, out state) && state.Pack != null && Directory.Exists(state.Pack.FilesFolder) ? state.Pack.FilesFolder : "";
        }

        internal static void SourceStep(Form form, string nextButton, string hint, bool ready)
        {
            State state;
            if (states.TryGetValue(form, out state))
            {
                state.NextSource = nextButton;
                state.SourceHint = hint;
                state.HasSource = ready;
                if (state.RefreshGate != null) state.RefreshGate();
            }
        }
        internal static void SourceCleared(Form form)
        {
            State state;
            if (states.TryGetValue(form, out state))
            {
                state.HasSource = false;
                if (state.RefreshGate != null) state.RefreshGate();
            }
        }
        internal static void SourceLoaded(Form form)
        {
            State state;
            if (form != null && states.TryGetValue(form, out state))
            {
                state.HasSource = true;
                if (state.RefreshGate != null) state.RefreshGate();
            }
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
            strip.Controls.Add(new Label { Text = "PACK", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = DarkTheme.Muted,
                Font = new Font(form.Font.FontFamily, 8, FontStyle.Bold), Margin = new Padding(4, 0, 8, 0) }, 0, 0);
            var choice = new ComboBox { Name = "CustomPackSelector", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 760 };
            strip.Controls.Add(choice, 1, 0);
            var refresh = new Button { Text = L.T("Aktualisieren", "Refresh"), AutoSize = true };
            StudioActions.Icon(refresh, StudioIcon.Refresh);
            strip.Controls.Add(refresh, 2, 0);
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var create = new Button {
                Name = "CreateCustomPack",
                Text = L.T("Neues Pack…", "New pack…"),
                AutoSize = true,
                MinimumSize = new Size(100, 30)
            };
            StudioUx.SetHelp(create, L.T("Ein neues Custom Pack erstellen oder einen vorhandenen Pack-Ordner registrieren.", "Create a new custom pack or register an existing pack folder."));
            strip.Controls.Add(create, 3, 0);
            foreach (Button source in Descendants(form).OfType<Button>().Where(b => b.FindForm() == form && (IsSourceButton(b) || b.Name == "PackClearAction" || b.Name == "PackEditAction")).ToArray())
            {
                source.Parent.Controls.Remove(source);
                if (source.Name != "PackClearAction" && source.Name != "PackEditAction") source.Name = "PackSourceAction";
                source.Dock = DockStyle.None;
                source.AutoSize = !StudioActions.IsIcon(source);
                source.MinimumSize = new Size(StudioActions.IsIcon(source) ? 34 : 110, 30);
                source.Margin = new Padding(3);
                int column = strip.ColumnCount++;
                strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                strip.Controls.Add(source, column, 0);
            }
            foreach (Button button in strip.Controls.OfType<Button>())
            {
                if (StudioActions.IsIcon(button))
                {
                    int column = strip.GetColumn(button);
                    strip.ColumnStyles[column].SizeType = SizeType.Absolute;
                    strip.ColumnStyles[column].Width = 42;
                }
                button.Padding = StudioActions.IsIcon(button) ? Padding.Empty : new Padding(4, 0, 4, 0);
                button.Margin = new Padding(3);
                button.MinimumSize = new Size(button.MinimumSize.Width, 30);
                button.MaximumSize = new Size(0, 30);
                button.Height = 30;
                button.Anchor = AnchorStyles.Left;
                button.BackColor = DarkTheme.Panel2;
            }
            form.VisibleChanged += delegate { strip.UpdateAnimation(); };
            choice.DropDown += delegate { strip.Paused = true; };
            choice.DropDownClosed += delegate { strip.Paused = false; };
            var sources = strip.Controls.OfType<Button>().Where(b => b.Name == "PackSourceAction").ToArray();
            var hint = new StudioSourceStart(form, choice, create, sources);
            Control editor = null;
            var gates = new System.Collections.Generic.List<Control>();
            Action updateGate = delegate
            {
                bool hasPack = state.Pack != null && Directory.Exists(state.Pack.FilesFolder);
                bool available = state.HasSource;
                if (!form.TopLevel)
                {
                    var parent = form.TopLevelControl as Form;
                    if (parent != null && parent != form) hasPack |= !String.IsNullOrEmpty(Folder(parent));
                }
                strip.Required = !available && !hasPack;
                strip.HighlightedSource = state.NextSource;
                strip.SourceRequired = hasPack && !available;
                hint.ShowStep(hasPack, state.SourceHint ?? EntryHint(form));
                hint.Visible = !available;
                if (editor != null) editor.Visible = available;
                if (!available) hint.BringToFront();
                foreach (Button source in sources) source.Enabled = hasPack || available;
                choice.AccessibleDescription = strip.SourceRequired
                    ? L.T("Pack ausgewählt. Öffne jetzt eine Datei über die hervorgehobenen Buttons.", "Pack selected. Open a file using the highlighted buttons.")
                    : available
                    ? L.T("Quelle ausgewählt – die Werkzeuge darunter sind bereit.", "Source selected — the tools below are ready.")
                    : L.T("Wähle ein Custom Pack aus der Liste oder erstelle eines.",
                        "Select a Custom Pack from the list or create one.");
                foreach (Control gate in gates) gate.Enabled = available;
                foreach (Control action in strip.Controls)
                    if (action.Name == "PackEditAction") action.Enabled = available;
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
                    choice.Items.Add(L.T("Pack auswählen …", "Select a pack …"));
                    foreach (var pack in packs) choice.Items.Add(pack);
                    choice.SelectedIndex = Math.Max(0, packs.FindIndex(p => p.FilesFolder == previous) + 1);
                    state.Pack = choice.SelectedItem as CustomPack;
                    if (state.Pack != null && !Directory.Exists(state.Pack.FilesFolder))
                    {
                        choice.SelectedIndex = 0;
                        state.Pack = null;
                    }
                }
                catch (Exception error)
                {
                    state.Pack = null;
                    choice.Items.Clear();
                    choice.Items.Add(L.T("Pack auswählen …", "Select a pack …"));
                    choice.SelectedIndex = 0;
                    if (StudioStartup.IsPreparing) throw;
                    StudioMessageBox.Show(form, error.Message, "Custom packs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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
                    var existing = choice.Items.OfType<CustomPack>().Select(p => p.FilesFolder).ToArray();
                    StudioEditor.Open(form, new CustomPackMakerForm(), delegate
                    {
                        reload();
                        var created = choice.Items.OfType<CustomPack>().FirstOrDefault(p => !existing.Contains(p.FilesFolder, StringComparer.OrdinalIgnoreCase));
                        if (created != null) choice.SelectedItem = created;
                    });
                }
                catch (Exception error)
                {
                    StudioMessageBox.Show(form, error.Message, "Custom packs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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
                var editing = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty, RowCount = layout.RowCount - 2 };
                editing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int row = 2; row < layout.RowStyles.Count; row++)
                    editing.RowStyles.Add(new RowStyle(layout.RowStyles[row].SizeType, layout.RowStyles[row].Height));
                foreach (Control child in layout.Controls.Cast<Control>().Where(c => layout.GetRow(c) >= 2).ToArray())
                {
                    int row = layout.GetRow(child);
                    layout.Controls.Remove(child);
                    editing.Controls.Add(child, 0, row - 2);
                }
                while (layout.RowStyles.Count > 2) layout.RowStyles.RemoveAt(layout.RowStyles.Count - 1);
                layout.RowCount = 3;
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var stage = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
                editor = editing;
                stage.Controls.Add(editing);
                stage.Controls.Add(hint);
                layout.Controls.Add(stage, 0, 2);
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
            var fallbackStage = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            editor = content;
            fallbackStage.Controls.Add(content);
            fallbackStage.Controls.Add(hint);
            wrapper.Controls.Add(fallbackStage, 0, 1);
            form.Controls.Add(wrapper);
            StudioUx.DisableHover(choice);
            StudioUx.SetHelp(refresh, L.T("Pack-Liste neu laden. Die Auswahl setzt den Startordner für Dateien und MUR_EDITED-Ausgaben.",
                "Reload packs. Selecting a pack sets the file browsing folder and MUR_EDITED output."));
            AddGate(content, gates);
            foreach (Control extra in form.Controls.Cast<Control>().Where(c => c != wrapper && !(c is AccentStrip)).ToArray())
                AddGate(extra, gates);
            updateGate();
            updateGate();
            DarkTheme.Apply(wrapper);
        }
        private static string EntryHint(Form form)
        {
            switch (form.GetType().Name)
            {
                case "GameHudForm":
                    return L.T("MenuSingle.szs / MenuMulti.szs: Einzel-/Mehrspieler-Layouts.\nTitle.szs + Title_E.szs / Title_U.szs / Title_J.szs: Titel/Lizenz.\nPassende Datei öffnen, Layout wählen und Elemente verschieben.",
                        "MenuSingle.szs / MenuMulti.szs: single/multiplayer layouts.\nTitle.szs + Title_E.szs / Title_U.szs / Title_J.szs: title/license.\nOpen the matching file, choose a layout and move its elements.");
                case "RaceHudForm":
                    return L.T("Öffne Race.szs plus Race_E.szs, Race_U.szs oder Race_J.szs.\nE = Englisch PAL · U = Englisch USA · J = Japanisch.\nRace.szs: Items/Minimap. Sprachdatei: Zahlen/Timer/Runden.\nWähle eine Textur, ersetze sie und speichere eine Archivkopie.",
                        "Open Race.szs plus Race_E.szs, Race_U.szs or Race_J.szs.\nE = English PAL · U = English USA · J = Japanese.\nRace.szs: items/minimap. Language file: numbers/timer/laps.\nSelect a texture, replace it and save an archive copy.");
                case "FontChangerForm":
                    return L.T("Add archive: Font.szs für Menüschrift und GO/Finish.\nPassende Nachbararchive werden automatisch mitgeladen.\nTimer: MenuSingle.szs, MenuMulti.szs, Globe.szs.\nHUD: Race.szs + Race_E.szs / Race_U.szs / Race_J.szs.\nRR zusätzlich: RaceAssets.szs + ReplacedAssets.szs.\nFehlende Dateien mit Add archive ergänzen; Bereiche und TTF wählen.",
                        "Add archive: Font.szs for menu text and GO/Finish.\nMatching nearby archives load automatically.\nTimers: MenuSingle.szs, MenuMulti.szs, Globe.szs.\nHUD: Race.szs + Race_E.szs / Race_U.szs / Race_J.szs.\nRR also needs RaceAssets.szs + ReplacedAssets.szs.\nUse Add archive for missing files; choose groups and TTF.");
                case "MenuTextForm":
                    return L.T("RR: UIAssets.szs für Menütexte, RaceAssets.szs für Renntexte.\nAdd archive: beide Dateien gemeinsam auswählen.\nOriginalspiel: passende Spracharchive (_E / _U / _J) oder .bmg.\nDateien ohne BMG werden übersprungen. Texte bearbeiten und Kopien speichern.",
                        "RR: UIAssets.szs for menu text, RaceAssets.szs for race text.\nAdd archive: select both files together.\nOriginal game: matching language archives (_E / _U / _J) or .bmg.\nFiles without BMG are skipped. Edit messages and save copies.");
                case "MenuTextureForm":
                    return L.T("Title_E.szs / Title_U.szs / Title_J.szs: Titel-/Lizenzgrafiken.\nTitle.szs / MenuSingle.szs: gemeinsame Menütexturen.\nÖffne die passende Datei, wähle eine Textur und ersetze das Bild.",
                        "Title_E.szs / Title_U.szs / Title_J.szs: title/license graphics.\nTitle.szs / MenuSingle.szs: shared menu textures.\nOpen the matching file, select a texture and replace its picture.");
                case "ArchiveCompareForm":
                    return L.T("Öffne Original und Kopie derselben Datei: Race.szs oder Race_E.szs.\nOpen base archive: Original. Open edited archive: bearbeitete Kopie.\nBeide müssen dieselbe Sprachvariante verwenden (E, U oder J).\nVergleiche die Einträge und speichere die ausgewählten Änderungen.",
                        "Open original and edited copies of the same file: Race.szs or Race_E.szs.\nOpen base archive: original. Open edited archive: edited copy.\nBoth must use the same language variant (E, U or J).\nCompare entries and save the selected changes.");
                case "MusicLoopForm":
                    return L.T("Datei öffnen: .wav mit PCM-Audio.\nAudio umwandeln: .mp3, .flac oder .ogg über FFmpeg.\nLoop-Anfang/-Ende setzen, Übergang anhören und .wav speichern.\nFür das Spiel anschließend mit dem BRSTM-Konverter in .brstm umwandeln.",
                        "Open file: a PCM .wav audio file.\nConvert other audio: .mp3, .flac or .ogg via FFmpeg.\nSet loop start/end, preview the seam and save a .wav.\nThen use BRSTM converter to create the game's .brstm file.");
                case "ThemeProjectForm":
                    return L.T("1. Neues Projekt für das gewählte Pack starten.\n2. Bearbeitete Dateien aus MUR_EDITED auswählen.\n3. Als gemeinsamen Theme-Ordner exportieren.\n.mtheme speichert optional deine Zusammenstellung zum Weiterarbeiten.",
                        "1. Start a new project for the selected pack.\n2. Choose edited files from MUR_EDITED.\n3. Export them together as a theme folder.\n.mtheme optionally saves your selection to continue later.");
                case "RetroRewindGifWizard":
                    return L.T("Title.szs + Title_E.szs / Title_U.szs / Title_J.szs: Titel/Lizenz.\nMenuSingle.szs / MenuMulti.szs: Einzel-/Mehrspieler-Menüs.\nÖffne die Datei oder wähle dein Pack und den passenden Tab.\nWähle ein Ersatzbild und erstelle die bearbeitete Kopie.",
                        "Title.szs + Title_E.szs / Title_U.szs / Title_J.szs: title/license.\nMenuSingle.szs / MenuMulti.szs: single/multiplayer menus.\nOpen the file or choose your pack, then select its tab.\nChoose a replacement picture and create the edited copy.");
                case "MenuModelsForm":
                    return L.T("Earth.szs: Globus-/Himmelmodell. globe.arc: zugehörige Globusdaten.\nBackModel.szs: 3D-Menühintergründe.\nÖffne die Datei für diesen Bereich und passe Bild/Farben an.\nSpeichere eine Kopie; fehlende Quellen gibt es im Custom Pack Maker.",
                        "Earth.szs: globe/sky model. globe.arc: related globe data.\nBackModel.szs: 3D menu backgrounds.\nOpen this section's file, then adjust its picture/colours.\nSave a copy; get missing source files through Custom Pack Maker.");
                default:
                    return L.T("Öffne eine passende Datei über die violette Leiste.\nDanach stehen die Werkzeuge bereit.",
                        "Open a supported file using the purple bar.\nThe editing tools will then unlock.");
            }
        }


        private static bool IsSourceButton(Button button)
        {
            if (button.Name == "InToolSourceAction") return false;
            if (button.Name == "PackSourceAction") return true;
            string text = button.Text;
            return text.StartsWith("Open file", StringComparison.Ordinal)
                || text.StartsWith("Add file", StringComparison.Ordinal)
                || text.StartsWith("Datei öffnen", StringComparison.Ordinal)
                || text.StartsWith("Datei hinzufügen", StringComparison.Ordinal)
                || text.StartsWith("Open archive", StringComparison.Ordinal)
                || text.StartsWith("Archiv öffnen", StringComparison.Ordinal)
                || text.StartsWith("Archiv hinzufügen", StringComparison.Ordinal)
                || text.StartsWith("Browse ISO", StringComparison.Ordinal)
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
