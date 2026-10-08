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
        SpecialEditorHistory editHistory;
        readonly RaceHudSession session = new RaceHudSession();
        readonly ComboBox area = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        readonly ListBox textures = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly TextBox output = new TextBox { Width = 430 };
        readonly Button replace, colours, save, batch;
        readonly Label textureCaption = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 8) };

        public MenuTextureForm() : base("MKWii Menu Textures Tool",
            L.T("Lizenz-Einstellungen • Menüleisten • Gemeinsame Menütexturen", "License settings • Menu bars • Shared menu textures"),
            "Title_E.szs / Title_U.szs / Title_J.szs · Title.szs / MenuSingle.szs · PNG / JPG")
        {
            var addArchive = Action(L.T("Archiv hinzufügen…", "Add archive…"), L.T("Weitere Menüarchive hinzufügen; aktuelle Änderungen bleiben erhalten.", "Add multiple menu archives without discarding current edits."), Open);
            addArchive.Name = "PackSourceAction";
            StudioActions.Icon(addArchive, StudioIcon.Add);
            var clearSelection = Action(L.T("Auswahl leeren", "Clear selection"), L.T("Geladene Archive entfernen.", "Clear loaded archives."), delegate {
                if (session.SelectedCount > 0 && StudioMessageBox.Show(this, L.T("Vorgemerkte Änderungen verwerfen?", "Discard pending changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                session.Archives.Clear(); RefreshTextures(); PackSelection.SourceCleared(this);
            });
            StudioActions.Icon(clearSelection, StudioIcon.Remove);
            area.Items.AddRange(new object[] { L.T("Obere Leiste", "Top bar"), L.T("Untere Leiste", "Bottom bar"), L.T("Menühintergrund", "Menu background"), L.T("Alle Texturen", "All textures") });
            area.SelectedIndex = 0;
            area.SelectedIndexChanged += delegate { RefreshTextures(); };
            replace = Action(L.T("Bild ersetzen…", "Replace picture…"), L.T("Textur ersetzen; betrifft alle Layouts, die sie verwenden.", "Replace this texture; all layouts using it are affected."), Replace);
            StudioActions.Icon(replace, StudioIcon.Import);
            colours = Action(L.T("Farben…", "Colours…"), L.T("Ausgewählte Textur umfärben.", "Recolour the selected texture."), Recolour);
            batch = Action(L.T("Texturen gesammelt…", "Batch textures…"), L.T("Alle TPL-Bilder der geladenen Archive als PNGs exportieren und nach Ergebnisvorschau gemeinsam importieren.", "Export all TPL images in loaded archives as PNGs and import them together after reviewing encoded results."), Batch);
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 278, Width = 950, Panel1MinSize = 180, Panel2MinSize = 280 };
            var browser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            browser.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            browser.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            browser.Controls.Add(new Label { Text = L.T("Kategorie", "Category"), AutoSize = true }, 0, 0);
            area.Dock = DockStyle.Fill;
            browser.Controls.Add(area, 0, 1);
            browser.Controls.Add(textures, 0, 2);
            split.Panel1.Controls.Add(browser);
            var imageWorkspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            imageWorkspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            imageWorkspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            imageWorkspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            imageWorkspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            imageWorkspace.Controls.Add(textureCaption, 0, 0);
            imageWorkspace.Controls.Add(preview, 0, 1);
            var textureActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            textureActions.Controls.Add(replace); textureActions.Controls.Add(colours);
            imageWorkspace.Controls.Add(textureActions, 0, 2);
            split.Panel2.Controls.Add(imageWorkspace);
            Body.Controls.Add(split);
            textures.SelectedIndexChanged += delegate { ShowTexture(); };
            Footer.Controls.Add(new Label { Text = "Output location", AutoSize = true });
            Footer.Controls.Add(output);
            var browse = ExportAction(L.T("Ausgabeordner wählen…", "Choose output folder…"), L.T("Ausgabeordner wählen.", "Choose the output folder."), delegate
            {
                using (var picker = new FolderPickerDialog { SelectedPath = output.Text })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        output.Text = picker.SelectedPath;
            }, false);
            StudioActions.Icon(browse, StudioIcon.Folder);
            save = ExportAction(L.T("Archivkopien speichern", "Save archive copies"), L.T("Separate Kopien speichern; geöffnete Quellen bleiben unverändert.", "Save separate copies; opened sources stay unchanged."), delegate
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
            exportRow.Controls.Add(new Label { Text = L.T("Ausgabeordner", "Output folder"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
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
            InitializeHistory();
        }

        void InitializeHistory()
        {
            editHistory = new SpecialEditorHistory(this, Actions, session.CaptureEdits,
                delegate(object[] state) { session.RestoreEdits(state); RefreshTextures(); },
                delegate { return session.SourceIdentity; });
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
            batch.Enabled = session.Archives.Count > 0;
            textureCaption.Text = texture == null ? L.T("Wähle links eine Textur.", "Choose a texture on the left.") : Path.GetFileName(texture.Key);
            if (texture != null) StudioUx.SetHelp(textureCaption, Path.GetFileName(texture.Archive.Source) + "\n" + texture.Key);
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
            var editor = new HudTextureColorForm(texture.Key, SelectedData(texture));
            StudioEditor.Open(this, editor, delegate(DialogResult result) {
                if (result == DialogResult.OK && editor.Result != null)
                    texture.Archive.Generated[texture.Key] = editor.Result;
                ShowTexture();
                if (result == DialogResult.OK && editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
            });
        }

        void Batch()
        {
            var editor = new TextureBatchForm(TextureBatch.Capture(session));
            StudioEditor.Open(this, editor, delegate(DialogResult result)
            {
                if (result != DialogResult.OK || editor.Result == null) return;
                Guard(delegate
                {
                    TextureBatch.Apply(session, editor.Result);
                    if (editHistory != null) { editHistory.Observe(); editHistory.Binding.Refresh(); }
                    RefreshTextures();
                    Status.Text = L.T("Textur-Sammelimport übernommen. Rückgängig stellt den gesamten vorherigen Arbeitsstand wieder her.",
                        "Batch texture import applied. Undo restores the entire previous edit state.");
                });
            });
        }
    }
}
