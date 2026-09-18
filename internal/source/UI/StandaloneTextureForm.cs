using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StandaloneTextureForm : Form
    {
        private string _path;
        private byte[] _data;
        private TexturePreviewResult _preview;
        private int _index;
        private PictureBox _picture;
        private Label _info;
        private bool _dirty;
        private ToolStripButton _save, _replace, _export, _previous, _next;
        public StandaloneTextureForm(string path)
        {
            _path = path;
            _data = File.ReadAllBytes(path);
            Text = "murums Wii Mod Studio — " + Path.GetFileName(path);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 560);
            Size = new Size(1000, 720);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.5F);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            RefreshPreview();
            FormClosing += OnClosing;
            DarkTheme.Apply(this);
        }

        private void BuildUi()
        {
            ToolStrip bar = new ToolStrip();
            bar.Dock = DockStyle.Top;
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.BackColor = DarkTheme.Panel;
            bool editable = TplTextureEditor.IsTpl(_data);
            ToolStripButton save = Make(L.T("Speichern", "Save"), delegate
            {
                Save(false);
            });
            save.Enabled = editable;
            bar.Items.Add(save);
            _save = save;
            ToolStripButton saveAs = Make(L.T("Kopie speichern", "Save a copy"), delegate
            {
                Save(true);
            });
            saveAs.Enabled = editable;
            bar.Items.Add(saveAs);
            bar.Items.Add(new ToolStripSeparator());
            ToolStripButton replace = Make(L.T("Bild ersetzen", "Replace image"), delegate
            {
                ReplaceImage();
            });
            replace.Enabled = editable;
            bar.Items.Add(replace);
            _replace = replace;
            _export = Make(L.T("Als PNG exportieren", "Export as PNG"), delegate
            {
                ExportPng();
            });
            bar.Items.Add(_export);
            bar.Items.Add(new ToolStripSeparator());
            _previous = Make("◀", delegate
            {
                Change(-1);
            });
            _previous.ToolTipText = L.T("Vorheriges Bild", "Previous image");
            bar.Items.Add(_previous);
            _next = Make("▶", delegate
            {
                Change(1);
            });
            _next.ToolTipText = L.T("Nächstes Bild", "Next image");
            bar.Items.Add(_next);
            Controls.Add(bar);
            DarkTheme.StyleToolStrip(bar, new MurumsDarkToolStripRenderer());
            _picture = new PictureBox();
            _picture.Dock = DockStyle.Fill;
            _picture.SizeMode = PictureBoxSizeMode.Zoom;
            _picture.BackColor = DarkTheme.Back;
            Controls.Add(_picture);
            _picture.BringToFront();
            _info = new Label();
            _info.Dock = DockStyle.Bottom;
            _info.Height = 34;
            _info.TextAlign = ContentAlignment.MiddleLeft;
            _info.Padding = new Padding(12, 0, 0, 0);
            _info.BackColor = DarkTheme.Panel;
            _info.ForeColor = DarkTheme.Muted;
            Controls.Add(_info);
            _info.BringToFront();
            bar.BringToFront();
            var header = StudioChrome.Header(L.T("Texturen bearbeiten", "Edit textures"), L.T("Bilder ansehen • Ersetzen • Als Kopie speichern", "Preview pictures • Replace • Save a copy"));
            header.Dock = DockStyle.Top;
            header.Height = 118;
            Controls.Add(header);
            header.SendToBack();
        }

        private ToolStripButton Make(string text, EventHandler click)
        {
            ToolStripButton b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            return b;
        }

        private void RefreshPreview()
        {
            try
            {
                _picture.Image = null;
                if (_preview != null)
                {
                    _preview.Dispose();
                    _preview = null;
                }

                TexturePreviewResult decoded;
                string error;
                if (!TexturePreview.TryDecode(Path.GetFileName(_path), _data, _index, out decoded, out error) || decoded == null)
                    throw new InvalidDataException(String.IsNullOrEmpty(error) ? "Texture could not be decoded." : error);
                _preview = decoded;
                _index = _preview.ImageIndex;
                _picture.Image = _preview.Bitmap;
                _info.Text = Path.GetFileName(_path) + "   •   " + (_index + 1) + "/" + _preview.ImageCount + "   •   " + _preview.Width + "×" + _preview.Height + "   •   " + _preview.FormatName + (_dirty ? "   •   *" : "");
            }
            catch (Exception ex)
            {
                _info.Text = ex.Message;
                _picture.Image = null;
            }

            _save.Enabled = _dirty && TplTextureEditor.IsTpl(_data);
            _export.Enabled = _preview != null;
            _previous.Enabled = _preview != null && _index > 0;
            _next.Enabled = _preview != null && _index + 1 < _preview.ImageCount;
            _replace.Enabled = TplTextureEditor.CanReplaceImage(_data, _index);
            if (_preview != null && !_replace.Enabled)
                _info.Text += L.T("   •   Vorschau und PNG-Export", "   •   Preview and PNG export");
        }

        private void Change(int delta)
        {
            if (_preview == null || _preview.ImageCount <= 0)
                return;
            _index = (_index + delta + _preview.ImageCount) % _preview.ImageCount;
            RefreshPreview();
        }

        private void ReplaceImage()
        {
            if (!TplTextureEditor.IsTpl(_data))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Direktes Ersetzen ist aktuell für TPL verfügbar.", "Direct replacement is currently available for TPL."), "murums Wii Mod Studio");
                return;
            }

            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "Images/TPL|*.tpl;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    TplTextureInfo info = TplTextureEditor.GetImageInfo(_data, _index);
                    using (Bitmap source = TplTextureEditor.LoadSourceBitmap(d.FileName))
                    {
                        bool resize = source.Width != info.Width || source.Height != info.Height;
                        if (resize)
                        {
                            DialogResult r = murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Quelle {0}×{1}, Ziel {2}×{3}. Automatisch skalieren?", "Source {0}×{1}, target {2}×{3}. Resize automatically?", source.Width, source.Height, info.Width, info.Height), "TPL", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (r != DialogResult.Yes)
                                return;
                        }

                        _data = TplTextureEditor.ReplaceImage(_data, source, resize, _index);
                    }

                    _dirty = true;
                    RefreshPreview();
                }
                catch (Exception ex)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, "TPL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportPng()
        {
            if (_preview == null || _preview.Bitmap == null)
                return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "PNG image|*.png";
                d.FileName = Path.GetFileNameWithoutExtension(_path) + (_preview.ImageCount > 1 ? "_" + _index.ToString("000") : "") + ".png";
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        using (MemoryStream bytes = new MemoryStream())
                        {
                            _preview.Bitmap.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
                            BackupManager.WriteAllBytesSafely(d.FileName, bytes.ToArray());
                        }
                    }
                    catch (Exception ex)
                    {
                        murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("PNG konnte nicht gespeichert werden", "Could not save PNG"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void Save(bool saveAs)
        {
            string target = _path;
            if (saveAs)
            {
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Filter = "TPL texture|*.tpl|All files|*.*";
                    d.FileName = Path.GetFileName(_path);
                    if (d.ShowDialog(this) != DialogResult.OK)
                        return;
                    target = d.FileName;
                }
            }

            try
            {
                BackupManager.WriteAllBytesSafely(target, _data);
                _path = target;
                _dirty = false;
                Text = "murums Wii Mod Studio — " + Path.GetFileName(_path);
                RefreshPreview();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (_dirty)
            {
                DialogResult r = murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Ungespeicherte Änderungen speichern?", "Save unsaved changes?"), "murums Wii Mod Studio", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                    e.Cancel = true;
                else if (r == DialogResult.Yes)
                {
                    Save(false);
                    e.Cancel = _dirty;
                }
            }

            if (!e.Cancel && _preview != null)
            {
                _picture.Image = null;
                _preview.Dispose();
                _preview = null;
            }
        }
    }
}
