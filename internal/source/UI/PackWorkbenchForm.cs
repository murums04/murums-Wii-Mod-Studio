using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class PackWorkbenchForm : StudioToolForm
    {
        string folder, preset;
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
        readonly Label location = new Label { Dock = DockStyle.Top, Height = 30, AutoEllipsis = true };
        readonly Button check, cancel, start, report;
        readonly TabControl pages = new TabControl { Dock = DockStyle.Fill };
        List<PackFinding> findings = new List<PackFinding>();
        CancellationTokenSource cancellation;
        bool busy;
        internal PackWorkbenchForm() : base("RR-MKWii Pack Workshop Tool",
            L.T("Pack prüfen • Projektstand sichern • Getrennte RR-Testkopie starten",
                "Check your pack • Save a snapshot • Launch a separate RR test copy"))
        {
            Action(L.T("Pack-Ordner öffnen…", "Open pack folder…"), "", Open).Name = "PackSourceAction";
            check = Action(L.T("Pack prüfen", "Check pack"), "", delegate { Check(); });
            cancel = Action(L.T("Abbrechen", "Cancel"), "", delegate { if (cancellation != null) cancellation.Cancel(); });
            report = Action(L.T("Bericht speichern…", "Save report…"), "", SaveReport);
            var checks = Page(L.T("Pack prüfen", "Check pack"), L.T(
                "Dateien auf lesbare Archive, Texturen und fehlende Verweise prüfen.",
                "Check archive and texture integrity, and find missing references."), new[] { check, cancel, report });
            checks.Controls.Add(grid);
            grid.BringToFront();
            var snapshot = Action(L.T("Projektstand sichern…", "Save snapshot…"), "", Snapshot);
            var restore = Action(L.T("Wiederherstellen…", "Restore snapshot…"), "", Restore);
            Page(L.T("Sicherungen", "Snapshots"), L.T(
                "Sichere den geöffneten Pack vor größeren Änderungen.\nBeim Wiederherstellen entsteht eine neue Pack-Kopie. Bestehende Dateien bleiben erhalten.",
                "Save the opened pack before larger changes.\nRestoring creates a new pack copy and keeps your existing files."), new[] { snapshot, restore });
            var build = Action(L.T("1. Testprofil erstellen…", "1. Build test profile…"), "", TestProfile);
            start = Action(L.T("2. Dolphin starten…", "2. Start Dolphin…"), "", StartTest);
            Page(L.T("Im Spiel testen", "Test in-game"), L.T(
                "Benötigt: dein RR.json-Preset, deine MKWii-ISO und Dolphin.\nStudio erstellt eine getrennte Pack-Kopie mit eigenem Dolphin-Profil und eigenen Spielständen.\nPrüfe deine Änderungen anschließend sichtbar in einem Offline-Rennen.",
                "Requires: your RR.json preset, MKWii ISO and Dolphin.\nStudio creates a separate pack copy, Dolphin profile and saves.\nThen inspect your changes in an offline race."), new[] { build, start });
            Body.Controls.Add(pages);
            Body.Controls.Add(location);
            DarkTheme.StyleTabs(pages);
            Status.Text = L.T("Prüfergebnisse sind kein Ingame-Nachweis.", "Check results are not in-game verification.");
            UpdateActions();
            DarkTheme.StyleGrid(grid);
            Finish();
            check.BackColor = snapshot.BackColor = build.BackColor = DarkTheme.Accent;
            check.ForeColor = snapshot.ForeColor = build.ForeColor = Color.White;
            PackSelection.SourceStep(this, L.T("Pack-Ordner öffnen…", "Open pack folder…"),
                L.T("Dein RR-Custom-Pack oder einen exportierten Pack-Ordner öffnen.\nPrüfe Dateien, sichere einen Projektstand oder erstelle eine getrennte Dolphin-Testkopie.",
                    "Open your RR custom pack or an exported pack folder.\nCheck files, save a snapshot or create a separate Dolphin test copy."), false);
            FormClosing += delegate(object s, FormClosingEventArgs e) { if (busy) { if (cancellation != null) cancellation.Cancel(); e.Cancel = true; } };
        }
        TabPage Page(string title, string help, Button[] buttons)
        {
            var page = new TabPage(title) { Padding = new Padding(8) };
            var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
            var explanation = new Label { Text = help, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(3, 8, 3, 8) };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            actions.Controls.AddRange(buttons);
            header.Controls.Add(explanation, 0, 0);
            header.Controls.Add(actions, 0, 1);
            page.Controls.Add(header);
            pages.TabPages.Add(page);
            return page;
        }
        void UpdateActions()
        {
            check.Enabled = !busy && folder != null;
            cancel.Enabled = busy;
            start.Enabled = !busy && preset != null;
            report.Enabled = !busy && findings.Count > 0;
        }
        void Open()
        {
            if (busy) return;
            using (var picker = new FolderPickerDialog { SelectedPath = PackSelection.Folder(this),
                Description = L.T("RR-Pack oder exportierten Pack-Ordner wählen", "Choose an RR pack or exported pack folder") })
                if (picker.ShowDialog(this) == DialogResult.OK) LoadFolder(picker.SelectedPath);
        }
        internal void LoadFolder(string path)
        {
            if (busy) throw new InvalidOperationException("Wait for the check to finish.");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            folder = Path.GetFullPath(path); location.Text = folder; preset = null;
            findings.Clear(); grid.DataSource = null;
            PackSelection.SourceLoaded(this); UpdateActions();
        }
        async void Check()
        {
            if (busy || folder == null) return;
            busy = true; cancellation = new CancellationTokenSource(); UpdateActions();
            Status.Text = L.T("Pack wird geprüft…", "Checking pack…");
            try
            {
                string source = folder;
                findings = await Task.Run(() => PackWorkspace.Check(source, cancellation.Token));
                grid.DataSource = findings;
                grid.Columns["Level"].HeaderText = L.T("Ergebnis", "Result");
                grid.Columns["File"].HeaderText = L.T("Datei", "File");
                grid.Columns["Detail"].HeaderText = L.T("Hinweis", "Details");
                grid.Columns["Level"].FillWeight = 15; grid.Columns["File"].FillWeight = 30; grid.Columns["Detail"].FillWeight = 55;
                Status.Text = findings[0].Detail;
            }
            catch (OperationCanceledException) { Status.Text = L.T("Prüfung abgebrochen.", "Check cancelled."); }
            catch (Exception ex) { StudioMessageBox.Show(this, ex.Message, Text); }
            finally { busy = false; cancellation.Dispose(); cancellation = null; UpdateActions(); }
        }
        void Snapshot()
        {
            if (busy || folder == null) return;
            using (var picker = new FolderPickerDialog { Description = L.T("Ordner für den neuen Projektstand wählen", "Choose a parent folder for the new snapshot") })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    string destination = Path.Combine(picker.SelectedPath, "RR-Snapshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    Status.Text = PackWorkspace.Snapshot(folder, destination);
                }
        }
        void Restore()
        {
            if (busy) return;
            string manifest = FileChoice(L.T("manifest.json des Studio-Projektstands auswählen", "Select the Studio snapshot manifest.json"), "Studio snapshot|manifest.json");
            if (manifest == null) return;
            using (var picker = new FolderPickerDialog { Description = L.T("Übergeordneten Ordner für eine neue Pack-Kopie wählen", "Choose a parent folder for a new pack copy") })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    string target = Path.Combine(picker.SelectedPath, "RR-Restored-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    Status.Text = PackWorkspace.Restore(Path.GetDirectoryName(manifest), target);
                }
        }

        string FileChoice(string title, string filter)
        {
            using (var picker = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true })
                return picker.ShowDialog(this) == DialogResult.OK ? picker.FileName : null;
        }
        void TestProfile()
        {
            if (busy || folder == null) return;
            string rr = FileChoice(L.T("Vorhandenes RR-Dolphin-Preset wählen (RR.json)", "Choose the existing RR Dolphin preset (RR.json)"), "Dolphin RR preset|*.json");
            if (rr == null) return;
            string iso = FileChoice(L.T("Deine Mario-Kart-Wii-ISO wählen", "Choose your Mario Kart Wii ISO"), "Mario Kart Wii ISO|*.iso");
            if (iso == null) return;
            using (var picker = new FolderPickerDialog { Description = L.T("Ordner für die getrennte Testkopie wählen", "Choose a parent folder for the separate test copy") })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    preset = DolphinTestProfile.Create(folder, rr, iso, Path.Combine(picker.SelectedPath, "RR-Test-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
                    UpdateActions();
                    Status.Text = L.T("Testkopie bereit. Neues Dolphin-Profil mit eigenen Spielständen: ", "Test copy ready. New Dolphin profile with separate saves: ") + preset;
                }
        }
        void StartTest()
        {
            if (busy) return;
            if (preset == null) throw new InvalidOperationException(L.T("Zuerst ein RR-Testprofil erstellen.", "Build an RR test profile first."));
            string exe = FileChoice(L.T("Installierte Dolphin.exe wählen", "Choose your installed Dolphin.exe"), "Dolphin|Dolphin.exe");
            if (exe != null)
            {
                DolphinTestProfile.Start(exe, preset);
                Status.Text = L.T("Dolphin gestartet. Sichtbare Ergebnisse im Spiel prüfen; Start allein bestätigt keine Kompatibilität.",
                    "Dolphin started. Inspect the results in-game; launching alone does not confirm compatibility.");
            }
        }
        void SaveReport()
        {
            if (findings.Count == 0) throw new InvalidOperationException(L.T("Zuerst Pack prüfen.", "Check the pack first."));
            using (var picker = new SaveFileDialog { Filter = "Text report|*.txt", FileName = "RR-Pack-Check.txt" })
                if (picker.ShowDialog(this) == DialogResult.OK)
                    BackupManager.WriteAllBytesSafely(picker.FileName, System.Text.Encoding.UTF8.GetBytes(
                        String.Join(Environment.NewLine, findings.Select(f => f.Level + " | " + f.File + " | " + f.Detail))));
        }
    }
}
