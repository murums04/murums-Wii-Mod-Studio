using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class PackArchivePicker : Form
    {
        readonly string imagePath;
        readonly ListView archives = new ListView {
            Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true,
            FullRowSelect = true, HideSelection = false, MultiSelect = false, BackColor = DarkTheme.Panel
        };
        readonly TextBox search = new TextBox { Dock = DockStyle.Fill };
        readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };
        readonly Button add = new Button { AutoSize = true, Enabled = false, MinimumSize = new Size(175, 34) };
        readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        string[] available = new string[0];
        bool rebuilding;
        bool busy;
        internal string[] ImportedPaths { get; private set; }

        internal PackArchivePicker(string path)
        {
            imagePath = path;
            Text = L.T("Archive aus ISO/WBFS auswählen", "Choose archives from ISO/WBFS");
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1080, 720);
            MinimumSize = new Size(740, 460);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Padding = new Padding(14);
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Insert(0, new RowStyle(SizeType.Absolute, 118));
            grid.Controls.Add(StudioChrome.Header(L.T("Archive auswählen", "Choose archives"),
                L.T("Empfohlene Menü-/HUD-Dateien • Englische Varianten vorausgewählt • Frei anpassbar", "Recommended menu/HUD files • English variants preselected • Adjust your selection")), 0, 0);
            grid.Controls.Add(new Label { AutoSize = true, Text = L.T(
                "Suche in Name, Pfad und Erklärung. Zusätzliche Mod-Dateien sind nicht in der ISO enthalten.",
                "Search names, paths and descriptions. Extra mod files are not included in the ISO.") }, 0, 1);
            grid.Controls.Add(search, 0, 2);
            archives.Columns.Add(L.T("Datei", "File"), 200);
            archives.Columns.Add(L.T("Verwendung", "Purpose"), 270);
            archives.Columns.Add(L.T("Pfad in ISO", "Path in ISO"), 350);
            archives.Columns.Add(L.T("Empfehlung", "Recommendation"), 145);
            archives.OwnerDraw = true;
            archives.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
            {
                using (var brush = new SolidBrush(DarkTheme.Panel2)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, archives.Font, e.Bounds, DarkTheme.Fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            archives.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            archives.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e) { e.DrawDefault = true; };
            grid.Controls.Add(archives, 0, 3);
            grid.Controls.Add(status, 0, 4);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = L.T("Abbrechen", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            add.Text = L.T("Auswahl hinzufügen", "Add selected");
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(add);
            grid.Controls.Add(buttons, 0, 5);
            Controls.Add(grid);
            CancelButton = cancel;
            DarkTheme.Apply(this);
            StudioUx.DisableHover(search);
            search.TextChanged += delegate { RefreshFiles(); };
            archives.ItemChecked += delegate(object sender, ItemCheckedEventArgs e)
            {
                if (rebuilding) return;
                string entry = (string)e.Item.Tag;
                if (e.Item.Checked) selected.Add(entry);
                else selected.Remove(entry);
                UpdateCount();
            };
            add.Click += async delegate { await ImportSelected(); };
            Shown += async delegate
            {
                if (Owner != null) Icon = Owner.Icon;
                busy = true;
                status.Text = L.T("ISO-Dateiliste wird gelesen…", "Reading ISO file list…");
                try
                {
                    available = await Task.Run(() => GameArchiveImport.List(imagePath, true));
                    foreach (string entry in available.Where(IsRecommended)) selected.Add(entry);
                    RefreshFiles();
                }
                catch (Exception error) { status.Text = error.Message; }
                finally { busy = false; UpdateCount(); }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
        }

        internal static bool IsRecommended(string path)
        {
            if (path == "files/contents/globe.arc") return true;
            if (path == "files/Scene/Model/Earth.szs" || path == "files/Scene/Model/BackModel.szs") return true;
            if (path == "files/Race/Common.szs" || path == "files/Race/Common_E.szs" || path == "files/Race/Common_U.szs") return true;
            if (!path.StartsWith("files/Scene/UI/", StringComparison.Ordinal)) return false;
            string name = Path.GetFileNameWithoutExtension(path);
            string[] parts = name.Split('_');
            if (parts.Length > 1 && parts[1] != "E" && parts[1] != "U") return false;
            return new[] { "Award", "Channel", "Common", "Font", "MenuSingle", "MenuMulti", "Race", "Title" }.Contains(parts[0]);
        }

        internal static string Purpose(string path)
        {
            if (path == "files/contents/globe.arc") return L.T("Zusätzliche Weltkugel-Ressourcen", "Additional globe resources");
            string name = Path.GetFileNameWithoutExtension(path);
            string stem = name.Split('_')[0];
            if (path.StartsWith("files/Scene/UI/", StringComparison.OrdinalIgnoreCase))
            {
                switch (stem.ToLowerInvariant())
                {
                    case "race": return L.T("Renn-HUD: Anzeigen, Symbole und Zahlen", "Race HUD: displays, icons and numbers");
                    case "title": return L.T("Titelbildschirm und Lizenzmenü", "Title screen and license menu");
                    case "menusingle": return L.T("Einzelspieler-Menüs", "Single-player menus");
                    case "menumulti": return L.T("Mehrspieler-Menüs", "Multiplayer menus");
                    case "menufriend": return L.T("Freundes- und Online-Menüs", "Friend and online menus");
                    case "menuchannel": return L.T("Mario-Kart-Kanal-Menüs", "Mario Kart Channel menus");
                    case "font": return L.T("Schriftarten", "Fonts");
                    case "common": return L.T("Gemeinsame Menüelemente", "Shared menu elements");
                    case "globe": return L.T("Online-Menü und Weltkugel-Oberfläche", "Online menu and globe interface");
                    default: return L.T("Menü-Oberfläche und zugehörige Grafiken", "Menu interface and related graphics");
                }
            }
            if (name.Equals("Earth", StringComparison.OrdinalIgnoreCase)) return L.T("Weltkugel und Himmel", "Globe and sky");
            if (name.Equals("BackModel", StringComparison.OrdinalIgnoreCase)) return L.T("3D-Menühintergründe", "3D menu backgrounds");
            if (path.StartsWith("files/Race/Course/", StringComparison.OrdinalIgnoreCase))
                return name.EndsWith("_d", StringComparison.OrdinalIgnoreCase)
                    ? L.T("Strecke/Arena – Mehrspieler-Variante", "Track/arena – multiplayer variant")
                    : L.T("Strecke oder Kampfarena", "Race track or battle arena");
            if (path.StartsWith("files/Race/Kart/", StringComparison.OrdinalIgnoreCase)) return L.T("Fahrer- und Fahrzeugmodelle", "Driver and vehicle models");
            if (path.StartsWith("files/Race/Competition/", StringComparison.OrdinalIgnoreCase)) return L.T("Wettbewerbsobjekte", "Competition objects");
            if (path.StartsWith("files/Race/", StringComparison.OrdinalIgnoreCase)) return L.T("Gemeinsame Rennressourcen", "Shared race resources");
            if (path.StartsWith("files/Scene/Model/", StringComparison.OrdinalIgnoreCase)) return L.T("3D-Modelle für Menüs", "3D models for menus");
            if (path.StartsWith("files/Boot/", StringComparison.OrdinalIgnoreCase)) return L.T("Start- und Sicherheitshinweise", "Startup and safety screens");
            if (path.StartsWith("files/Demo/", StringComparison.OrdinalIgnoreCase)) return L.T("Siegerehrung oder Zwischensequenz", "Awards or cutscene");
            if (path.StartsWith("files/hbm/", StringComparison.OrdinalIgnoreCase)) return L.T("HOME-Menü", "HOME menu");
            if (path.StartsWith("files/Driver/", StringComparison.OrdinalIgnoreCase)) return L.T("Fahrermodelle", "Driver models");
            if (path.StartsWith("files/Item/", StringComparison.OrdinalIgnoreCase)) return L.T("Item-Modelle und Effekte", "Item models and effects");
            return L.T("Spielressourcen – genaue Verwendung unbekannt", "Game resources – exact purpose unknown");
        }

        void RefreshFiles()
        {
            rebuilding = true;
            archives.BeginUpdate();
            try
            {
                archives.Items.Clear();
                string query = search.Text.Trim();
                foreach (string path in available.OrderByDescending(IsRecommended).ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    string purpose = Purpose(path);
                    if ((path + " " + purpose).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var item = new ListViewItem(new[] { Path.GetFileName(path), purpose, path, IsRecommended(path) ? L.T("Empfohlen", "Recommended") : "" }) { Tag = path, Checked = selected.Contains(path) };
                    archives.Items.Add(item);
                }
            }
            finally { archives.EndUpdate(); rebuilding = false; }
            UpdateCount();
        }

        void UpdateCount()
        {
            add.Enabled = !busy && selected.Count > 0;
            if (available.Length > 0)
                status.Text = archives.Items.Count + " / " + available.Length + L.T(" Dateien • Ausgewählt: ", " files • Selected: ") + selected.Count;
        }

        async Task ImportSelected()
        {
            if (busy || selected.Count == 0) return;
            string[] paths = selected.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
            {
                status.Text = L.T("Gleiche Dateinamen: Bitte nur eine Variante je Name wählen; der Pack-Ordner kann beide nicht unterscheiden.",
                    "Duplicate filenames: choose one variant per name; the pack folder cannot distinguish them.");
                return;
            }
            busy = true;
            Enabled = false;
            status.Text = L.T("Ausgewählte Dateien werden importiert…", "Importing selected files…");
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "murums Wii Mod Studio", "GameSources", Guid.NewGuid().ToString("N"));
            try
            {
                await Task.Run(() => GameArchiveImport.Extract(imagePath, paths, cache, true));
                ImportedPaths = paths.Select(p => Path.Combine(cache, Path.GetFileName(p))).ToArray();
                busy = false;
                DialogResult = DialogResult.OK;
            }
            catch (Exception error) { status.Text = error.Message; }
            finally { busy = false; Enabled = true; }
        }
    }
}
