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
        readonly PictureBox image = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(48, 48, 54)
        };
        readonly TextBox text = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both
        };
        readonly Label caption = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            AutoEllipsis = true
        };
        public ResourcePreviewPanel()
        {
            Dock = DockStyle.Fill;
            Controls.Add(image);
            Controls.Add(text);
            Controls.Add(caption);
        }

        public void ShowResource(string name, byte[] bytes)
        {
            var old = image.Image;
            image.Image = null;
            if (old != null)
                old.Dispose();
            caption.Text = name + "\n" + (bytes == null ? "No resource selected" : bytes.Length + " bytes");
            image.Visible = false;
            text.Visible = true;
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
                text.Visible = false;
                return;
            }

            try
            {
                if (name.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase))
                {
                    var layout = BrlytDocument.FromBytes(bytes);
                    text.Text = "Layout structure (not a game render)\r\n" + string.Join("\r\n", layout.Panes.Select(p => p.Name + "  " + p.Magic + "  " + p.Width + " × " + p.Height + "  position " + p.X + ", " + p.Y).ToArray());
                    return;
                }
            }
            catch (Exception e)
            {
                text.Text = e.Message + "\r\n";
            }

            text.AppendText("Binary preview — first 512 bytes\r\n");
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
        public FilePreviewForm(string path) : base("File Preview", Path.GetFileName(path))
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
            Status.Text = "Decoded image or resource structure. This preview does not simulate game animations.";
        }
    }
}
