using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class ResourcePreviewPanel : Panel
    {
        readonly PictureBox image = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = DarkTheme.Panel2
        };
        readonly StudioReadOnlyText text = new StudioReadOnlyText
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false
        };
        readonly Panel textSurface;
        readonly StudioEmptyState empty = new StudioEmptyState(String.Empty, delegate { });
        readonly Label caption = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            AutoEllipsis = true,
            Padding = new Padding(14, 6, 14, 6),
            BackColor = DarkTheme.Panel2
        };
        public ResourcePreviewPanel()
        {
            Dock = DockStyle.Fill;
            Controls.Add(image);
            textSurface = StudioReadOnlyText.Surface(text);
            Controls.Add(textSurface);
            Controls.Add(empty);
            ShowResource(String.Empty, null);
            Controls.Add(caption);
        }

        public void ShowResource(string name, byte[] bytes)
        {
            var old = image.Image;
            image.Image = null;
            if (old != null)
                old.Dispose();
            caption.Text = name + "\n" + (bytes == null ? String.Empty : bytes.Length + " bytes");
            caption.Visible = bytes != null;
            empty.SetContent(L.T("Wähle eine Ressource für die Vorschau aus.", "Select a resource to preview."), false);
            empty.Visible = bytes == null;
            image.Visible = false;
            textSurface.Visible = bytes != null;
            text.Text = "";
            if (bytes == null)
                return;
            TexturePreviewResult d;
            string error;
            if (TexturePreview.TryDecode(name, bytes, 0, out d, out error))
            {
                using (d)
                {
                    image.Image = new Bitmap(d.Bitmap);
                    caption.Text = name + "\n" + d.Width + " × " + d.Height + " • " + d.FormatName;
                }

                image.Visible = true;
                textSurface.Visible = false;
                return;
            }

            try
            {
                if (name.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase))
                {
                    var layout = BrlytDocument.FromBytes(bytes);
                    text.Text = L.T("Layout-Struktur (keine Spielansicht)\r\n", "Layout structure (not a game render)\r\n") + string.Join("\r\n", layout.Panes.Select(p => p.Name + "  " + p.Magic + "  " + p.Width + " × " + p.Height + "  position " + p.X + ", " + p.Y).ToArray());
                    return;
                }
            }
            catch (Exception e)
            {
                text.Text = e.Message + "\r\n";
            }

            text.AppendText(L.T("Binärvorschau — erste 512 Bytes\r\n", "Binary preview — first 512 bytes\r\n"));
            for (int i = 0; i < Math.Min(bytes.Length, 512); i += 16)
                text.AppendText(i.ToString("X6") + "  " + BitConverter.ToString(bytes, i, Math.Min(16, Math.Min(bytes.Length, 512) - i)).Replace('-', ' ') + "\r\n");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && image.Image != null)
            {
                image.Image.Dispose();
                image.Image = null;
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class FilePreviewForm : StudioToolForm
    {
        readonly ResourcePreviewPanel view = new ResourcePreviewPanel();
        public FilePreviewForm(string path) : base(L.T("Dateivorschau", "File Preview"), Path.GetFileName(path))
        {
            Body.Controls.Add(view);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".szs" || ext == ".arc" || ext == ".u8")
            {
                var archive = new StudioArchiveCopy(path);
                var list = new ComboBox
                {
                    Dock = DockStyle.Top,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                list.Items.AddRange(archive.Files.Keys.OrderBy(k => k).ToArray());
                list.SelectedIndexChanged += delegate
                {
                    Guard(delegate
                    {
                        view.ShowResource((string)list.SelectedItem, archive.Files[(string)list.SelectedItem].Data);
                    });
                };
                Body.Controls.Add(list);
                if (list.Items.Count > 0)
                    list.SelectedIndex = 0;
            }
            else
                view.ShowResource(Path.GetFileName(path), File.ReadAllBytes(path));
            Finish();
            Status.Text = L.T("Dekodiertes Bild oder Ressourcenstruktur. Diese Vorschau simuliert keine Spielanimationen.", "Decoded image or resource structure. This preview does not simulate game animations.");
        }
    }
}
