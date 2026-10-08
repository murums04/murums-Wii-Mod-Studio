using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class TextureImportPreviewForm : StudioToolForm
    {
        readonly byte[] original;
        readonly Bitmap source;
        readonly int index;
        readonly TplTextureInfo info;
        readonly ComboBox fitting = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        readonly ComboBox targetFormat = new ComboBox { Name = "TargetFormat", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly ComboBox mipmaps = new ComboBox { Name = "Mipmaps", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly Label profile = new Label { AutoSize = true, MaximumSize = new Size(860, 0) };
        readonly int[] formats = { 0, 1, 2, 3, 4, 5, 6, 8, 9, 14 };
        readonly TextureChannelPictureBox before = new TextureChannelPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom },
            after = new TextureChannelPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly Button apply;
        int renderVersion;
        bool rendering, disposed;
        internal byte[] Result;
        internal TextureImportPreviewForm(byte[] target, int imageIndex, Bitmap picture)
            : base(L.T("Texturbild importieren", "Import texture image"),
                L.T("Zuschnitt und Zielformat wählen • Gespeicherte Farben prüfen • Bild übernehmen",
                    "Choose fitting and target format • Check encoded colours • Apply image"))
        {
            original = (byte[])target.Clone(); source = picture; index = imageIndex;
            info = TplTextureEditor.GetImageInfo(original, index);
            fitting.Items.AddRange(new object[] {
                L.T("Einpassen – Seitenverhältnis erhalten", "Fit – preserve aspect ratio"),
                L.T("Füllen – Ränder abschneiden", "Fill – crop edges"),
                L.T("Strecken – Ziel vollständig füllen", "Stretch – fill target dimensions") });
            var options = new TableLayoutPanel { Height = 60, Width = 860, ColumnCount = 3, RowCount = 2, Margin = new Padding(0, 0, 0, 4) };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            options.Controls.Add(new Label { Text = L.T("Zuschnitt", "Fitting"), Dock = DockStyle.Fill }, 0, 0);
            options.Controls.Add(new Label { Text = L.T("Zielformat", "Target format"), Dock = DockStyle.Fill }, 1, 0);
            options.Controls.Add(new Label { Text = "Mipmaps", Dock = DockStyle.Fill }, 2, 0);
            fitting.Dock = DockStyle.Fill;
            options.Controls.Add(fitting, 0, 1); options.Controls.Add(targetFormat, 1, 1); options.Controls.Add(mipmaps, 2, 1);
            Actions.Controls.Add(options);
            Actions.SetFlowBreak(options, true);
            Actions.SizeChanged += delegate {
                options.Width = Math.Max(400, Actions.ClientSize.Width - 8);
                profile.MaximumSize = new Size(Math.Max(400, Actions.ClientSize.Width - 12), 0);
            };
            targetFormat.Items.Add(L.T("Original: ", "Original: ") + info.FormatName);
            foreach (int format in formats) targetFormat.Items.Add(FormatName(format));
            mipmaps.Items.Add(L.F("Original: {0} Zusatzstufen", "Original: {0} extra levels", info.MaxLod));
            mipmaps.Items.Add(L.T("Keine – nur Basisbild", "None – base image only"));
            for (int level = 1; level <= TplTextureEditor.MaximumMipLevel(info.Width, info.Height); level++)
                mipmaps.Items.Add(L.F("{0} Zusatzstufen – bis {1} × {2}", "{0} extra levels – down to {1} × {2}",
                    level, Math.Max(1, info.Width >> level), Math.Max(1, info.Height >> level)));
            var description = new Label { AutoSize = true, Anchor = AnchorStyles.Left, MaximumSize = new Size(560, 0),
                Text = source.Width + " × " + source.Height + " → " + info.Width + " × " + info.Height + " • " + info.FormatName
                    + L.T(" • Bild ", " • Image ") + (index + 1) };
            Actions.Controls.Add(description);
            Actions.SetFlowBreak(description, true);
            Actions.Controls.Add(profile);
            var views = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); views.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            views.Controls.Add(new Label { Text = L.T("Original", "Original"), Dock = DockStyle.Fill }, 0, 0);
            views.Controls.Add(new Label { Text = L.T("Nach Import / Komprimierung", "After import / encoding"), Dock = DockStyle.Fill }, 1, 0);
            views.Controls.Add(before, 0, 1); views.Controls.Add(after, 1, 1);
            Body.Controls.Add(views);
            Control channels = TextureChannelPreview.Selector(before, after);
            Body.Controls.Add(channels);
            views.BringToFront();
            apply = ExportAction(L.T("Bild übernehmen", "Apply image"), "", delegate { if (Result != null) { DialogResult = DialogResult.OK; Close(); } });
            var cancel = ExportAction(L.T("Abbrechen", "Cancel"), "", delegate { Result = null; DialogResult = DialogResult.Cancel; Close(); }, false);
            CancelButton = cancel;
            AcceptButton = apply;
            SetImage(before, original);
            targetFormat.SelectedIndex = 0;
            mipmaps.SelectedIndex = 0;
            fitting.SelectedIndexChanged += delegate { Render(); };
            targetFormat.SelectedIndexChanged += delegate { Render(); };
            mipmaps.SelectedIndexChanged += delegate { Render(); };
            Finish();
            fitting.SelectedIndex = 0;
        }
        internal static Bitmap Fit(Bitmap source, int width, int height, int mode)
        {
            if (width < 1 || height < 1 || mode < 0 || mode > 2) throw new ArgumentException("Invalid fitting.");
            var output = new Bitmap(width, height);
            using (var g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                RectangleF destination = new RectangleF(0, 0, width, height);
                if (mode != 2)
                {
                    float ratio = mode == 0 ? Math.Min(width / (float)source.Width, height / (float)source.Height)
                        : Math.Max(width / (float)source.Width, height / (float)source.Height);
                    destination = new RectangleF((width - source.Width * ratio) / 2, (height - source.Height * ratio) / 2,
                        source.Width * ratio, source.Height * ratio);
                }
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(source, Rectangle.Round(destination), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return output;
        }
        static string FormatName(int format)
        {
            return new[] { "I4", "I8", "IA4", "IA8", "RGB565", "RGB5A3", "RGBA32", "", "CI4", "CI8", "", "", "", "", "CMPR" }[format];
        }

        string ProfileDescription(int format, int lod)
        {
            string limits;
            if (format == 0 || format == 1) limits = (format == 0 ? "16" : "256")
                + L.T(" Grau-/Alphastufen; Alpha entspricht der Helligkeit.", " grayscale/alpha levels; alpha follows intensity.");
            else if (format == 2 || format == 3) limits = L.T("Graustufen; ", "Grayscale; ") + (format == 2 ? L.T("16 Alphastufen.", "16 alpha levels.") : L.T("256 Alphastufen.", "256 alpha levels."));
            else if (format == 4) limits = L.T("Ohne Transparenz; reduzierte Farbtiefe.", "No alpha; reduced colour depth.");
            else if (format == 5) limits = L.T("Reduzierte Farbtiefe; 8 Alphastufen oder deckend.", "Reduced colour depth; 8 alpha levels or opaque.");
            else if (format == 6) limits = L.T("Volle Farbe und 256 Alphastufen.", "Full colour and 256 alpha levels.");
            else if (format == 14) limits = L.T("Blockkomprimierung; Alpha nur transparent/deckend.", "Block compression; alpha is transparent/opaque only.");
            else
            {
                int kind = 2;
                if (info.Format == 8 || info.Format == 9)
                {
                    int table = Read32(original, 8) + index * 8;
                    kind = Read32(original, Read32(original, table + 4) + 4);
                }
                limits = (format == 8 ? L.T("Bis 16 Palettenfarben; ", "Up to 16 palette colours; ") : L.T("Bis 256 Palettenfarben; ", "Up to 256 palette colours; "))
                    + (kind == 0 ? L.T("Graustufen, 256 Alphastufen.", "grayscale, 256 alpha levels.") : kind == 1 ? L.T("ohne Transparenz.", "no alpha.") : L.T("8 Alphastufen oder deckend.", "8 alpha levels or opaque."));
            }
            return FormatName(format) + " • " + info.Width + " × " + info.Height + " • " + L.F("{0} Mipmap-Zusatzstufen", "{0} extra mipmap levels", lod) + "\n" + limits;
        }

        static int Read32(byte[] data, int offset)
        {
            return checked((int)(((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3]));
        }

        async void Render()
        {
            renderVersion++;
            apply.Enabled = false; Result = null;
            if (rendering || fitting.SelectedIndex < 0 || targetFormat.SelectedIndex < 0 || mipmaps.SelectedIndex < 0) return;
            rendering = true;
            try
            {
                do
                {
                    int revision = renderVersion, mode = fitting.SelectedIndex;
                    var options = new TplTextureImportOptions {
                        Format = targetFormat.SelectedIndex == 0 ? (int?)null : formats[targetFormat.SelectedIndex - 1],
                        MaxLod = mipmaps.SelectedIndex == 0 ? (int?)null : mipmaps.SelectedIndex - 1
                    };
                    profile.Text = ProfileDescription(options.Format ?? info.Format, options.MaxLod ?? info.MaxLod);
                    Status.Text = L.T("TPL-Vorschau wird kodiert …", "Encoding TPL preview …");
                    Image previous = after.Image; after.Image = null;
                    if (previous != null) previous.Dispose();
                    byte[] bytes = null;
                    Bitmap preview = null;
                    Exception failure = null;
                    using (var copy = new Bitmap(source))
                    {
                        try
                        {
                            await Task.Run(delegate {
                                using (var fitted = Fit(copy, info.Width, info.Height, mode))
                                    bytes = TplTextureEditor.ReplaceImage(original, fitted, false, index, options);
                                TexturePreviewResult decoded; string error;
                                if (!TexturePreview.TryDecode("texture.tpl", bytes, index, out decoded, out error)) throw new InvalidDataException(error);
                                using (decoded) preview = decoded.Bitmap.Clone(new Rectangle(0, 0, decoded.Bitmap.Width, decoded.Bitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            });
                        }
                        catch (Exception ex) { failure = ex; }
                    }
                    if (disposed || revision != renderVersion)
                    {
                        if (preview != null) preview.Dispose();
                        if (disposed) break;
                        continue;
                    }
                    if (failure != null)
                    {
                        if (preview != null) preview.Dispose();
                        Status.Text = failure.Message;
                        profile.Text = failure.Message;
                        break;
                    }
                    Result = bytes; after.Image = preview; apply.Enabled = true;
                    Status.Text = L.T("Vorschau aus den gespeicherten TPL-Daten. Andere Bilder bleiben erhalten; Spieltönung wird nicht simuliert.",
                        "Preview from the encoded TPL data. Other images are preserved; game tinting is not simulated.");
                    break;
                } while (!disposed);
            }
            catch (Exception ex)
            {
                if (!disposed) { Status.Text = ex.Message; profile.Text = ex.Message; }
            }
            finally { rendering = false; }
        }
        void SetImage(PictureBox view, byte[] bytes)
        {
            TexturePreviewResult decoded; string error;
            if (!TexturePreview.TryDecode("texture.tpl", bytes, index, out decoded, out error)) throw new InvalidDataException(error);
            using (decoded)
            {
                Image previous = view.Image; view.Image = decoded.Bitmap.Clone(new Rectangle(0, 0, decoded.Bitmap.Width, decoded.Bitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (previous != null) previous.Dispose();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                disposed = true; renderVersion++;
                foreach (var view in new[] { before, after })
                    if (view.Image != null) { view.Image.Dispose(); view.Image = null; }
            }
            base.Dispose(disposing);
        }
    }
}
