using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
        readonly PictureBox before = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom },
            after = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly Button apply;
        internal byte[] Result;
        internal TextureImportPreviewForm(byte[] target, int imageIndex, Bitmap picture)
            : base(L.T("Texturbild importieren", "Import texture image"),
                L.T("Zuschnitt wählen • Gespeicherte Farben prüfen • Bild übernehmen",
                    "Choose fitting • Check encoded colours • Apply image"))
        {
            original = (byte[])target.Clone(); source = picture; index = imageIndex;
            info = TplTextureEditor.GetImageInfo(original, index);
            fitting.Items.AddRange(new object[] {
                L.T("Einpassen – Seitenverhältnis erhalten", "Fit – preserve aspect ratio"),
                L.T("Füllen – Ränder abschneiden", "Fill – crop edges"),
                L.T("Strecken – Ziel vollständig füllen", "Stretch – fill target dimensions") });
            Actions.Controls.Add(fitting);
            var description = new Label { AutoSize = true, MaximumSize = new Size(560, 0),
                Text = source.Width + " × " + source.Height + " → " + info.Width + " × " + info.Height + " • " + info.FormatName
                    + L.T(" • Bild ", " • Image ") + (index + 1) };
            Actions.Controls.Add(description);
            var views = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); views.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            views.Controls.Add(new Label { Text = L.T("Original", "Original"), Dock = DockStyle.Fill }, 0, 0);
            views.Controls.Add(new Label { Text = L.T("Nach Import / Komprimierung", "After import / encoding"), Dock = DockStyle.Fill }, 1, 0);
            views.Controls.Add(before, 0, 1); views.Controls.Add(after, 1, 1);
            Body.Controls.Add(views);
            apply = ExportAction(L.T("Bild übernehmen", "Apply image"), "", delegate { if (Result != null) { DialogResult = DialogResult.OK; Close(); } });
            SetImage(before, original);
            fitting.SelectedIndexChanged += delegate { Guard(Render); };
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
        void Render()
        {
            apply.Enabled = false; Result = null;
            using (var fitted = Fit(source, info.Width, info.Height, fitting.SelectedIndex))
                Result = TplTextureEditor.ReplaceImage(original, fitted, false, index);
            SetImage(after, Result);
            apply.Enabled = true;
            Status.Text = L.T("Vorschau aus den tatsächlich codierten TPL-Daten. Format, Größe und weitere Bilder bleiben erhalten.\nTransparenz, Graustufen und Paletten hängen vom Zielformat ab; Spieltönung wird nicht simuliert.",
                "Preview decoded from the actual encoded TPL. Format, dimensions and other images are preserved.\nAlpha, grayscale and palettes depend on the target format; game tinting is not simulated.");
        }
        void SetImage(PictureBox view, byte[] bytes)
        {
            TexturePreviewResult decoded; string error;
            if (!TexturePreview.TryDecode("texture.tpl", bytes, index, out decoded, out error)) throw new InvalidDataException(error);
            using (decoded)
            {
                Image previous = view.Image; view.Image = new Bitmap(decoded.Bitmap);
                if (previous != null) previous.Dispose();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var view in new[] { before, after })
                    if (view.Image != null) { view.Image.Dispose(); view.Image = null; }
            base.Dispose(disposing);
        }
    }
}
