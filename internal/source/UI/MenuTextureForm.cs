using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class MenuTextureForm : StudioToolForm
    {
        readonly RaceHudSession session = new RaceHudSession();
        readonly ComboBox area = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        readonly ListBox textures = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly TextBox output = new TextBox { Width = 430 };
        readonly Button replace, colours, save;

        public MenuTextureForm() : base("MKWii Menu Textures Tool",
            "License settings • Top / bottom bars • Shared menu textures",
            "Title_E.szs / Title_U.szs / Title_J.szs · Title.szs / MenuSingle.szs · PNG / JPG")
        {
            Action("Add archive…", "Add multiple menu archives without discarding current edits.", Open).Name = "PackSourceAction";
            Action("Clear selection", "Clear loaded archives.", delegate {
                if (session.SelectedCount > 0 && StudioMessageBox.Show(this, "Discard pending changes?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                session.Archives.Clear(); RefreshTextures(); PackSelection.SourceCleared(this);
            });
            area.Items.AddRange(new object[] { "Top bar", "Bottom bar", "Menu background", "All textures" });
            area.SelectedIndex = 0;
            area.SelectedIndexChanged += delegate { RefreshTextures(); };
            Actions.Controls.Add(new Label { Text = L.T("Kategorie", "Category"), AutoSize = true, Margin = new Padding(3, 8, 4, 0) });
            Actions.Controls.Add(area);
            replace = Action("Replace picture...", "Replace this texture; all layouts using it are affected.", Replace);
            colours = Action("Colours...", "Recolour the selected texture.", Recolour);
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360, Width = 950 };
            split.SizeChanged += delegate
            {
                if (split.ClientSize.Width > 250)
                    split.SplitterDistance = (split.ClientSize.Width - split.SplitterWidth) * 2 / 5;
            };
            split.Panel1.Controls.Add(textures);
            split.Panel2.Controls.Add(preview);
            Body.Controls.Add(split);
            textures.SelectedIndexChanged += delegate { ShowTexture(); };
            Footer.Controls.Add(new Label { Text = "Output location", AutoSize = true });
            Footer.Controls.Add(output);
            var browse = ExportAction("Browse...", "Choose the output folder.", delegate
            {
                using (var picker = new FolderPickerDialog { SelectedPath = output.Text })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        output.Text = picker.SelectedPath;
            }, false);
            browse.MinimumSize = new Size(100, 36);
            save = ExportAction("Save edited archive", "Save a copy; the opened source stays unchanged.", delegate
            {
                session.Save(output.Text, false);
                ExportHelp.Show(this, output.Text);
            });
            var outputLabel = Footer.Controls.OfType<Label>().FirstOrDefault();
            Footer.Controls.Clear();
            if (outputLabel != null) outputLabel.Dispose();
            var exportRow = new TableLayoutPanel { AutoSize = true, MinimumSize = new Size(0, 52), ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            exportRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            exportRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            exportRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            exportRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            exportRow.Controls.Add(new Label { Text = "Output location", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            output.Dock = DockStyle.Fill;
            output.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            exportRow.Controls.Add(output, 1, 0);
            exportRow.Controls.Add(browse, 2, 0);
            exportRow.Controls.Add(save, 3, 0);
            Footer.FlowDirection = FlowDirection.LeftToRight;
            Footer.Controls.Add(exportRow);
            Footer.SizeChanged += delegate { exportRow.Width = Math.Max(200, Footer.ClientSize.Width - Footer.Padding.Horizontal); };
            output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MUR_EDITED");
            FormClosed += delegate { if (preview.Image != null) preview.Image.Dispose(); };
            Finish();
            RefreshTextures();
        }

        protected override void OnPackSelected(CustomPack pack)
        {
            output.Text = Path.Combine(pack.FilesFolder, "MUR_EDITED");
        }

        void Open()
        {
            AddSelectedPaths(GameArchiveImportForm.SelectMany(this, ToolArchiveFilters.MenuTextures));
        }

        void AddSelectedPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return;
            var loaded = new System.Collections.Generic.List<RaceHudArchive>();
            var skipped = new System.Collections.Generic.List<string>();
            foreach (string path in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (session.Archives.Any(a => a.Source.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (session.Archives.Concat(loaded).Any(a => Path.GetFileName(a.Source).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("An archive with this name is already loaded.");
                var archive = new RaceHudArchive(path);
                if (archive.Files.Keys.Any(k => k.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase)))
                    loaded.Add(archive);
                else
                    skipped.Add(Path.GetFileName(path));
            }
            if (loaded.Count > 0)
            {
                session.Archives.AddRange(loaded);
                PackSelection.SourceLoaded(this);
                if (output.Text.Length == 0)
                    output.Text = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(session.Archives[0].Source), "MUR_EDITED"));
                RefreshTextures();
            }
            if (skipped.Count > 0)
                Status.Text = L.T("Ohne Texturen übersprungen: ", "Skipped without textures: ") + string.Join(", ", skipped)
                    + "\n" + L.T("Menütexturen aus Title.szs, MenuSingle.szs oder MenuMulti.szs hinzufügen.", "Add menu textures from Title.szs, MenuSingle.szs or MenuMulti.szs.");
        }
        internal static System.Collections.Generic.List<HudTexture> AreaTextures(RaceHudSession session, int area)
        {
            var all = session.Textures(4);
            if (area == 3) return all;
            string layout = area == 0 ? "obi_top.brlyt" : area == 1 ? "obi_bottom.brlyt" : "bg.brlyt";
            return all.Where(texture => texture.Archive.Files.Any(file =>
                ('/' + file.Key.Replace('\\', '/').TrimStart('.', '/')).EndsWith("/bg/blyt/" + layout, StringComparison.OrdinalIgnoreCase)
                && BrlytDocument.FromBytes(file.Value.Data).Textures.Any(name =>
                    String.Equals(Path.GetFileName(name), Path.GetFileName(texture.Key), StringComparison.OrdinalIgnoreCase)))).ToList();
        }

        void RefreshTextures()
        {
            textures.Items.Clear();
            foreach (var texture in AreaTextures(session, area.SelectedIndex))
                textures.Items.Add(texture);
            if (textures.Items.Count > 0) textures.SelectedIndex = 0;
            else ShowTexture();
            Status.Text = L.T("Gemeinsam verwendete Texturen ändern sich in allen zugehörigen Menüs. Schrift und Materialfarben lassen sich im Game HUD Tool bearbeiten.",
                "Shared textures change in every menu that uses them. Edit text panes and material colours in the Game HUD Tool.");
        }

        byte[] SelectedData(HudTexture texture)
        {
            byte[] generated;
            return texture.Archive.Generated.TryGetValue(texture.Key, out generated)
                ? generated : texture.Archive.Files[texture.Key].Data;
        }

        void ShowTexture()
        {
            if (preview.Image != null) preview.Image.Dispose();
            preview.Image = null;
            var texture = textures.SelectedItem as HudTexture;
            replace.Enabled = colours.Enabled = texture != null;
            save.Enabled = session.SelectedCount > 0;
            if (texture == null) return;
            TexturePreviewResult decoded;
            string error;
            if (TexturePreview.TryDecode(texture.Key, SelectedData(texture), 0, out decoded, out error))
                using (decoded) preview.Image = new Bitmap(decoded.Bitmap);
            else Status.Text = error;
        }

        void Replace()
        {
            var texture = textures.SelectedItem as HudTexture;
            if (texture == null) return;
            using (var picker = new OpenFileDialog { Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp" })
                if (picker.ShowDialog(this) == DialogResult.OK)
                    using (var image = TplTextureEditor.LoadSourceBitmap(picker.FileName))
                        texture.Archive.Generated[texture.Key] = TplTextureEditor.ReplaceFirstImage(SelectedData(texture), image, true);
            ShowTexture();
        }

        void Recolour()
        {
            var texture = textures.SelectedItem as HudTexture;
            if (texture == null) return;
            using (var editor = new HudTextureColorForm(texture.Key, SelectedData(texture)))
                if (editor.ShowDialog(this) == DialogResult.OK && editor.Result != null)
                    texture.Archive.Generated[texture.Key] = editor.Result;
            ShowTexture();
        }
    }
}