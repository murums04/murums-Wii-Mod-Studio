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
        readonly Button add, remove, build, save;
        public ThemeProjectForm() : base("MKWii Theme Project Tool", "Collect your edited archives, fonts and music • Save a project • Build one output folder")
        {
            Action("New project…", "Choose your custom pack as the reference for destination filenames and folders.", delegate
            {
                if (!CanDiscard())
                    return;
                string folder = Folder("");
                if (folder != null)
                {
                    project = new ThemeProject
                    {
                        PackFolder = folder,
                        OutputFolder = Path.Combine(folder, "THEME_EDITED")
                    };
                    dirty = true;
                    RefreshProject();
                }
            });
            Action("Open project…", "Load a saved .mtheme file. Replacement files stay linked at their original locations.", delegate
            {
                if (!CanDiscard())
                    return;
                string p = OpenPath("Theme project|*.mtheme");
                if (p != null)
                {
                    var next = ThemeProject.Load(p);
                    project = next;
                    dirty = false;
                    RefreshProject();
                }
            });
            save = ExportAction("Save project…", "Store the pack path, output path, notes, replacement paths and content hashes.", delegate
            {
                project.Notes = notes.Text;
                string p = SavePath("My theme.mtheme", "Theme project|*.mtheme");
                if (p != null)
                {
                    project.Save(p);
                    dirty = false;
                    Status.Text = "Saved project: " + p;
                }
            }, false);
            add = Action("Add edited file…", "First choose the original file inside your pack, then its edited replacement. This defines the build destination.", delegate
            {
                string original = OpenPath("Original destination in your pack|*.*");
                if (original == null)
                    return;
                string replacement = OpenPath("Edited replacement file|*.*");
                if (replacement == null)
                    return;
                project.Add(original, replacement);
                dirty = true;
                RefreshProject();
            });
            remove = Action("Remove selected", "Remove this file from the project list. No disk file is deleted.", delegate
            {
                if (assets.SelectedItem != null)
                {
                    project.Assets.Remove((ThemeProject.Asset)assets.SelectedItem);
                    dirty = true;
                    RefreshProject();
                }
            });
            build = ExportAction("Build theme folder…", "Copy reviewed replacements under their original names. Your pack is never overwritten by this command.", delegate
            {
                string folder = Folder(project.OutputFolder);
                if (folder != null)
                {
                    project.OutputFolder = folder;
                    project.Notes = notes.Text;
                    dirty = true;
                    project.Build();
                    Status.Text = "Built " + project.Assets.Count + " files in " + folder + ". Save the project to retain this output location.";
                }
            });
            Action("Preview selected file…", "Inspect the selected replacement before building. Archives show their resources.", delegate
            {
                var a = assets.SelectedItem as ThemeProject.Asset;
                if (a == null)
                {
                    Status.Text = "Select a project file first.";
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
                Text = "Project notes",
                Dock = DockStyle.Bottom,
                Height = 120,
                Padding = new Padding(8)
            };
            notes.Dock = DockStyle.Fill;
            notesGroup.Controls.Add(notes);
            Body.Controls.Add(assets);
            Body.Controls.Add(notesGroup);
            StudioUx.SetHelp(notes, "Project notes: record colour choices, source pictures, font settings and in-game checks. Editor controls are not automatically captured.");
            StudioUx.SetHelp(assets, "Each row maps one edited file to its destination relative to your custom pack.");
            Finish();
            RefreshProject();
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                e.Cancel = !CanDiscard();
            };
        }

        bool CanDiscard()
        {
            return !dirty || murumsWiiModStudio.StudioMessageBox.Show(this, "Continue without saving the project changes?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes;
        }

        void RefreshProject()
        {
            assets.Items.Clear();
            if (project != null)
            {
                foreach (var a in project.Assets)
                    assets.Items.Add(a);
                notes.Text = project.Notes;
            }

            add.Enabled = save.Enabled = project != null;
            build.Enabled = remove.Enabled = project != null && project.Assets.Count > 0;
            notes.Enabled = project != null;
            Status.Text = project == null ? "Create a project using your custom pack folder. Then add the edited files produced by the tools." : "Pack: " + project.PackFolder + "\nOutput: " + project.OutputFolder;
        }
    }
}
