using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    // Browse archive textures by picture without requiring knowledge of timg paths.
    internal sealed class TextureBrowserForm : Form
    {
        private readonly List<ListViewItem> _items = new List<ListViewItem>();
        private readonly ListView _list = new ListView();
        private readonly ImageList _images = new ImageList();
        private readonly TextBox _search = new TextBox();
        private readonly Label _status = new Label();
        private readonly Timer _timer = new Timer();
        private int _next;
        public ArchiveEntry SelectedTexture { get; private set; }

        public TextureBrowserForm(ArchiveEntry root)
        {
            Text = L.T("Texturen finden — Bild auswählen", "Find textures — choose an image");
            Size = new Size(1000, 720);
            MinimumSize = new Size(850, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(14)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(layout);
            layout.Controls.Add(StudioChrome.Header(L.T("Texturen finden", "Find textures"), L.T("Nach Name suchen • Bild doppelklicken • Im Archiv ersetzen und speichern", "Search by name • Double-click a picture • Replace and save in the archive")), 0, 0);
            StudioUx.SetHelp(_search, L.T("Filtert nach Dateiname oder Archivpfad. Strg+F fokussiert das Suchfeld.", "Filter by filename or archive path. Ctrl+F focuses the search field."));
            _search.Dock = DockStyle.Fill;
            _search.AccessibleName = L.T("Texturen suchen", "Search textures");
            _search.TextChanged += delegate
            {
                Filter();
            };
            layout.Controls.Add(_search, 0, 1);
            _images.ImageSize = new Size(96, 96);
            _images.ColorDepth = ColorDepth.Depth32Bit;
            using (Bitmap blank = new Bitmap(96, 96))
                _images.Images.Add(blank);
            _list.Dock = DockStyle.Fill;
            _list.View = View.LargeIcon;
            _list.LargeImageList = _images;
            _list.MultiSelect = false;
            _list.HideSelection = false;
            _list.ShowItemToolTips = true;
            _list.ItemActivate += delegate
            {
                Choose();
            };
            layout.Controls.Add(_list, 0, 2);
            TableLayoutPanel footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(_status, 0, 0);
            Button choose = new Button
            {
                Dock = DockStyle.Fill,
                Enabled = false,
                Text = L.T("Bild auswählen", "Choose image")
            };
            _list.SelectedIndexChanged += delegate
            {
                choose.Enabled = _list.SelectedItems.Count > 0;
            };
            AcceptButton = choose;
            KeyPreview = true;
            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                    Close();
                if (e.Control && e.KeyCode == Keys.F)
                {
                    _search.Focus();
                    _search.SelectAll();
                    e.SuppressKeyPress = true;
                }
            };
            choose.Click += delegate
            {
                Choose();
            };
            footer.Controls.Add(choose, 1, 0);
            layout.Controls.Add(footer, 0, 3);
            Collect(root, "");
            Filter();
            _timer.Interval = 30;
            _timer.Tick += delegate
            {
                LoadNextPreview();
            };
            Shown += delegate
            {
                _timer.Start();
            };
            DarkTheme.Apply(this);
        }

        private void Collect(ArchiveEntry directory, string path)
        {
            foreach (ArchiveEntry entry in directory.Children)
            {
                string full = path + "/" + entry.Name;
                if (entry.IsDirectory)
                    Collect(entry, full);
                else if (TplTextureEditor.IsTpl(entry.Data))
                {
                    ListViewItem item = new ListViewItem(entry.Name, 0);
                    item.Tag = entry;
                    item.Name = full;
                    item.ToolTipText = full;
                    _items.Add(item);
                }
            }
        }

        private void Filter()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (ListViewItem item in _items)
                if (item.Name.IndexOf(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    _list.Items.Add(item);
            _list.EndUpdate();
            _status.Text = L.F("{0} von {1} TPL-Texturen", "{0} of {1} TPL textures", _list.Items.Count, _items.Count);
            if (_items.Count == 0)
                _status.Text = L.T("Keine TPL gefunden. BRRES-Texturen über „Bearbeiten“ in RiiStudio öffnen.", "No TPL found. Open BRRES textures in RiiStudio using Edit.");
        }

        private void LoadNextPreview()
        {
            if (_next >= _items.Count)
            {
                _timer.Stop();
                return;
            }

            ListViewItem item = _items[_next++];
            ArchiveEntry entry = (ArchiveEntry)item.Tag;
            TexturePreviewResult preview;
            string error;
            if (!TexturePreview.TryDecode(entry.Name, entry.Data, 0, out preview, out error) || preview == null)
            {
                item.ToolTipText += "\r\n" + (error ?? L.T("Keine Vorschau verfügbar", "No preview available"));
                return;
            }

            using (preview)
            using (Bitmap thumb = new Bitmap(96, 96))
            {
                using (Graphics graphics = Graphics.FromImage(thumb))
                {
                    graphics.Clear(Color.FromArgb(55, 55, 65));
                    float scale = Math.Min(96F / preview.Width, 96F / preview.Height);
                    int width = Math.Max(1, (int)(preview.Width * scale)), height = Math.Max(1, (int)(preview.Height * scale));
                    graphics.DrawImage(preview.Bitmap, (96 - width) / 2, (96 - height) / 2, width, height);
                }

                _images.Images.Add(thumb);
                item.ImageIndex = _images.Images.Count - 1;
                item.ToolTipText += "\r\n" + preview.Width + "×" + preview.Height + " • " + preview.FormatName;
            }
        }

        private void Choose()
        {
            if (_list.SelectedItems.Count == 0)
                return;
            SelectedTexture = (ArchiveEntry)_list.SelectedItems[0].Tag;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _list.LargeImageList = null;
                _images.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
