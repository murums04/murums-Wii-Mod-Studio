using System;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ThemeProjectForm : StudioToolForm
    {
        ThemeProject project;
        bool dirty;
        readonly ListBox assets = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true
        };
        readonly TextBox notes = new TextBox
        {
            Dock = DockStyle.Bottom,
            Multiline = true,
            Height = 95,
            ScrollBars = ScrollBars.Vertical
        };
        readonly Button add, manual, remove, build, save;
        public ThemeProjectForm() : base("RR-MKWii Theme Project Tool", L.T("Deine Studio-Änderungen als ein gemeinsames Theme zusammenstellen", "Combine your Studio edits into one theme folder"), L.T("Bearbeitete Dateien: *.szs / *.brfnt / *.brstm · .mtheme = gespeichertes Studio-Projekt", "Edited files: *.szs / *.brfnt / *.brstm · .mtheme = saved Studio project"))
        {
            var newAction = Action(L.T("Neues Projekt…", "New project…"), L.T("Deinen Pack-Ordner als Basis für Dateinamen und Zielpfade wählen.", "Choose your custom pack as the reference for destination filenames and folders."), delegate
            {
                if (!CanDiscard())
                    return;
                string folder = PackSelection.Folder(this);
                if (String.IsNullOrEmpty(folder)) folder = Folder("", false);
                if (folder != null)
                {
                    project = new ThemeProject
                    {
                        PackFolder = folder,
                        OutputFolder = ThemeProject.SuggestedOutput(folder)
                    };
                    dirty = true;
                    RefreshProject();
                }
            });
            var openAction = Action(L.T("Projekt öffnen…", "Open project…"), L.T("Nur ein zuvor mit Save project gespeichertes .mtheme-Projekt öffnen. Für .szs-Dateien zuerst New project verwenden.", "Open a .mtheme project previously created with Save project. For .szs files, start with New project."), delegate
            {
                if (!CanDiscard())
                    return;
                string p = OpenPath(L.T("Theme-Projekt|*.mtheme", "Theme project|*.mtheme"));
                if (p != null)
                {
                    var next = ThemeProject.Load(p);
                    project = next;
                    dirty = false;
                    RefreshProject();
                }
            });
            save = ExportAction(L.T("Projekt speichern…", "Save project…"), L.T("Erstellt eine .mtheme-Projektdatei zum späteren Weiterarbeiten. Die verknüpften .szs-Dateien bleiben separat.", "Creates a .mtheme project to continue later. Linked .szs files remain separate."), delegate
            {
                project.Notes = notes.Text;
                string p = SavePath("My theme.mtheme", L.T("Theme-Projekt|*.mtheme", "Theme project|*.mtheme"));
                if (p != null)
                {
                    project.Save(p);
                    dirty = false;
                    Status.Text = L.T("Projekt gespeichert: ", "Saved project: ") + p; ToolStatus.Set(this, true);
                }
            }, false);
            add = Action(L.T("Bearbeitete Dateien hinzufügen…", "Add edited files…"), L.T("Kopien aus MUR_EDITED wählen. Studio erkennt die passenden Ziele im Pack anhand der Namen.", "Choose copies from MUR_EDITED. Studio matches their names to the destinations in your pack."), delegate
            {
                using (var picker = new OpenFileDialog { Multiselect = true, CheckFileExists = true,
                    InitialDirectory = Path.Combine(project.PackFolder, "MUR_EDITED"),
                    Filter = "Edited game files|*.szs;*.arc;*.brres;*.brfnt;*.brstm;*.tpl;*.brlyt;*.brlan;*.bmg|All files|*.*" })
                {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    project.AddEditedFiles(picker.FileNames);
                    dirty = true;
                    RefreshProject();
                }
            });
            manual = Action(L.T("Andere Dateinamen zuordnen…", "Map different filenames…"), L.T("Erst das Original im Pack, dann die bearbeitete Ersatzdatei wählen. So wird das Ausgabeziel festgelegt.", "First choose the original file inside your pack, then its edited replacement. This defines the build destination."), delegate
            {
                string original = OpenPath(L.T("Originaldatei in deinem Pack|*.*", "Original destination in your pack|*.*"));
                if (original == null)
                    return;
                string replacement = OpenPath(L.T("Bearbeitete Ersatzdatei|*.*", "Edited replacement file|*.*"));
                if (replacement == null)
                    return;
                project.Add(original, replacement);
                dirty = true;
                RefreshProject();
            });
            remove = Action(L.T("Auswahl entfernen", "Remove selected"), L.T("Datei nur aus der Projektliste entfernen. Die Datei bleibt erhalten.", "Remove this file from the project list. No disk file is deleted."), delegate
            {
                if (assets.SelectedItem != null)
                {
                    project.Assets.Remove((ThemeProject.Asset)assets.SelectedItem);
                    dirty = true;
                    RefreshProject();
                }
            });
            build = ExportAction(L.T("Theme-Ordner erstellen…", "Build theme folder…"), L.T("Geprüfte Ersatzdateien unter den Originalnamen in einen separaten Ordner kopieren.", "Copy reviewed replacements under their original names. Your pack is never overwritten by this command."), delegate
            {
                string current = project.OutputFolder;
                if (String.IsNullOrEmpty(current) || Path.GetFullPath(current).StartsWith(Path.GetFullPath(project.PackFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || String.Equals(Path.GetFullPath(current), Path.GetFullPath(project.PackFolder), StringComparison.OrdinalIgnoreCase)) current = ThemeProject.SuggestedOutput(project.PackFolder);
                string folder = Folder(current);
                if (folder != null)
                {
                    project.OutputFolder = folder;
                    project.Notes = notes.Text;
                    dirty = true;
                    project.Build();
                    Status.Text = L.T("Erstellt: ", "Built ") + project.Assets.Count + L.T(" Dateien in ", " files in ") + folder + L.T(". Projekt speichern, um den Ausgabeort zu behalten.", ". Save the project to retain this output location.");
                    if (project.Assets.Count > 0)
                        ExportHelp.Show(this, folder);
                }
            });
            Action(L.T("Dateivorschau…", "Preview selected file…"), L.T("Ausgewählte Ersatzdatei vor dem Export prüfen. Archive zeigen ihren Inhalt.", "Inspect the selected replacement before building. Archives show their resources."), delegate
            {
                var a = assets.SelectedItem as ThemeProject.Asset;
                if (a == null)
                {
                    Status.Text = L.T("Zuerst eine Projektdatei auswählen.", "Select a project file first.");
                    return;
                }

                using (var f = new FilePreviewForm(a.Replacement))
                    f.ShowDialog(this);
            });
            notes.TextChanged += delegate
            {
                if (project != null && project.Notes != notes.Text)
                {
                    project.Notes = notes.Text;
                    dirty = true;
                }
            };
            var notesGroup = new GroupBox
            {
                Text = L.T("Projektnotizen", "Project notes"),
                Dock = DockStyle.Bottom,
                Height = 78,
                Padding = new Padding(8)
            };
            notes.Dock = DockStyle.Fill;
            notesGroup.Controls.Add(notes);
            Body.Controls.Add(assets);
            Body.Controls.Add(notesGroup);
            var guide = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 65,
                Padding = new Padding(6),
                Text = L.T(
                    "Sammelt fertige Änderungen aus MUR_EDITED. Die Liste zeigt, wohin jede Datei gehört.\nTheme-Ordner erstellen kopiert diese Auswahl zusammen. Projekt speichern (.mtheme) ist optional zum Weiterarbeiten.",
                    "Collects finished edits from MUR_EDITED. The list shows where each file belongs.\nBuild theme folder copies this selection together. Save project (.mtheme) is optional to continue later.")
            };
            Body.Controls.Add(guide);

            StudioUx.SetHelp(notes, L.T("Projektnotizen: Farben, Quellbilder, Schriftoptionen und Ingame-Prüfungen notieren. Editor-Einstellungen werden nicht automatisch gespeichert.", "Project notes: record colour choices, source pictures, font settings and in-game checks. Editor controls are not automatically captured."));
            StudioUx.SetHelp(assets, L.T("Jede Zeile ordnet eine bearbeitete Datei ihrem Zielpfad im Pack zu.", "Each row maps one edited file to its destination relative to your custom pack."));
            MinimumSize = new System.Drawing.Size(950, 700);
            Size = new System.Drawing.Size(1120, 850);

            newAction.Name = openAction.Name = "PackSourceAction";
            Finish();
            RefreshProject();
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                e.Cancel = !CanDiscard();
            };
        }

        bool CanDiscard()
        {
            return !dirty || murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ohne Speichern der Projektänderungen fortfahren?", "Continue without saving the project changes?"), Text, MessageBoxButtons.YesNo) == DialogResult.Yes;
        }

        void RefreshProject()
        {
            if (project != null) PackSelection.SourceLoaded(this);
            assets.Items.Clear();
            if (project != null)
            {
                foreach (var a in project.Assets)
                    assets.Items.Add(a);
                notes.Text = project.Notes;
            }

            add.Enabled = manual.Enabled = save.Enabled = project != null;
            build.Enabled = remove.Enabled = project != null && project.Assets.Count > 0;
            notes.Enabled = project != null;
            Status.Text = project == null ? L.T("Neues Projekt übernimmt das gewählte Pack. Danach bearbeitete Dateien aus MUR_EDITED hinzufügen.", "New project uses the selected pack. Then add edited files from MUR_EDITED.") : "Pack: " + project.PackFolder + "\nOutput: " + project.OutputFolder;
        }
    }
}
