using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterTextureView : UserControl
    {
        readonly ComboBox textures = new ComboBox { Name = "CharacterTexture", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly PictureBox image = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = DarkTheme.Panel2 };
        readonly Func<CharacterModelImport> model;
        readonly Func<IEnumerable<CharacterAsset>> assets;
        readonly Button save;
        readonly TabPage textureTab;
        readonly DarkTabControl tabs;
        sealed class Choice
        {
            internal string Path, Name;
            internal byte[] Tpl;
            public override string ToString() { return Name; }
        }
        internal CharacterTextureView(Control preview, Func<CharacterModelImport> source, Func<IEnumerable<CharacterAsset>> files)
        {
            Dock = DockStyle.Fill;
            model = source; assets = files;
            tabs = new DarkTabControl { Dock = DockStyle.Fill };
            var modelTab = new TabPage(L.T("3D-Vorschau", "3D preview"));
            textureTab = new TabPage(L.T("Charaktertexturen", "Character textures"));
            var viewport = preview as CharacterModelViewport;
            modelTab.Controls.Add(viewport == null ? preview : new CharacterPreviewWorkspace(viewport));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(textures, 0, 0);
            layout.Controls.Add(image, 0, 1);
            save = StudioChrome.ActionButton(L.T("PNG speichern…", "Save PNG…"));
            save.Enabled = false;
            save.Click += delegate {
                using (var dialog = new SaveFileDialog { Filter = "PNG|*.png", FileName = "character-texture.png" })
                {
                    if (image.Image == null || dialog.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        image.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        StudioMessageBox.ShowPath(this, dialog.FileName, L.T("Textur gespeichert.", "Texture saved."), L.T("Gespeichert", "Saved"));
                    }
                    catch (Exception error) { StudioMessageBox.Show(this, error.Message, L.T("Textur", "Texture"), MessageBoxButtons.OK); }
                }
            };
            layout.Controls.Add(save, 0, 2);
            textureTab.Controls.Add(layout);
            tabs.TabPages.AddRange(new[] { modelTab, textureTab });
            Controls.Add(tabs);
            DarkTheme.StyleTabs(tabs);
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedTab == textureTab) LoadTextures(); };
            textures.SelectedIndexChanged += delegate { LoadImage(); };
        }
        internal void RefreshSource()
        {
            if (tabs.SelectedTab == textureTab) LoadTextures();
            else { textures.Items.Clear(); ClearImage(); }
        }
        void LoadTextures()
        {
            textures.Items.Clear();
            ClearImage();
            try
            {
                var source = model();
                if (source != null && source.Rig != null)
                {
                    foreach (string name in source.Rig.Materials.Select(m => m.Texture).Where(n => n != null).Distinct())
                    {
                        string path = System.IO.Path.Combine(source.Rig.Folder, name);
                        if (File.Exists(path)) textures.Items.Add(new Choice { Name = name, Path = path });
                    }
                }
                else
                {
                    var vehicle = assets().FirstOrDefault(a => a.Role == "Race vehicle");
                    if (vehicle != null)
                        foreach (var item in CharacterImages.Textures(vehicle, "driver_model.brres"))
                            textures.Items.Add(new Choice { Name = item.Name, Tpl = item.Tpl });
                }
                if (textures.Items.Count > 0) textures.SelectedIndex = 0;
            }
            catch (Exception error) { StudioMessageBox.Show(this, error.Message, L.T("Charaktertexturen", "Character textures"), MessageBoxButtons.OK); }
        }
        void ClearImage()
        {
            var previous = image.Image;
            image.Image = null;
            if (previous != null) previous.Dispose();
            save.Enabled = false;
        }
        void LoadImage()
        {
            ClearImage();
            var selected = textures.SelectedItem as Choice;
            if (selected == null) return;
            try
            {
                image.Image = selected.Tpl == null ? TplTextureEditor.LoadSourceBitmap(selected.Path) : CharacterImages.Decode(selected.Tpl);
                save.Enabled = true;
            }
            catch (Exception error) { StudioMessageBox.Show(this, error.Message, L.T("Textur", "Texture"), MessageBoxButtons.OK); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) ClearImage();
            base.Dispose(disposing);
        }
    }
}
