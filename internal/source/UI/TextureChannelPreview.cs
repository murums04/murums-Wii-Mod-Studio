using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal enum TexturePreviewChannel { Rgba, Rgb, Alpha }

    internal static class TextureChannelPreview
    {
        internal static Bitmap Create(Bitmap source, TexturePreviewChannel channel)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (channel != TexturePreviewChannel.Rgb && channel != TexturePreviewChannel.Alpha)
                throw new ArgumentOutOfRangeException("channel");
            var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            try
            {
                var bounds = new Rectangle(0, 0, source.Width, source.Height);
                BitmapData input = source.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    BitmapData target = output.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        // Direkte Pixel erhalten auch RGB bei Alpha 0; DrawImage würde vormultiplizieren.
                        byte[] row = new byte[checked(source.Width * 4)];
                        for (int y = 0; y < source.Height; y++)
                        {
                            Marshal.Copy(IntPtr.Add(input.Scan0, checked(y * input.Stride)), row, 0, row.Length);
                            for (int x = 0; x < row.Length; x += 4)
                            {
                                if (channel == TexturePreviewChannel.Alpha)
                                    row[x] = row[x + 1] = row[x + 2] = row[x + 3];
                                row[x + 3] = 255;
                            }
                            Marshal.Copy(row, 0, IntPtr.Add(target.Scan0, checked(y * target.Stride)), row.Length);
                        }
                    }
                    finally { output.UnlockBits(target); }
                }
                finally { source.UnlockBits(input); }
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }

        internal static Control Selector(params TextureChannelPictureBox[] views)
        {
            var panel = new TableLayoutPanel { Name = "TextureChannelSelector", Dock = DockStyle.Top,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 44, ColumnCount = 2, RowCount = 1, Padding = new Padding(8, 4, 8, 4),
                BackColor = DarkTheme.Panel, Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            string caption = L.T("Vorschaukanal", "Preview channel");
            var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 12, 0) };
            var choice = new ComboBox { Name = "TexturePreviewChannel", AccessibleName = caption,
                DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = Padding.Empty };
            choice.Items.AddRange(new object[] {
                L.T("RGBA – Normal", "RGBA – Normal"),
                L.T("RGB – ohne Transparenz", "RGB – ignore alpha"),
                L.T("Alpha – Graustufen", "Alpha – grayscale") });
            choice.SelectedIndex = 0;
            choice.SelectedIndexChanged += delegate {
                foreach (var view in views) view.Channel = (TexturePreviewChannel)choice.SelectedIndex;
            };
            StudioUx.SetHelp(choice, L.T("Ändert nur die Vorschau. Import, Speichern und PNG-Export behalten die tatsächlichen Bilddaten.",
                "Changes the preview only. Import, saving and PNG export keep the actual image data."));
            panel.Controls.Add(label, 0, 0);
            panel.Controls.Add(choice, 1, 0);
            EventHandler fitHeight = delegate {
                int height = Math.Max(label.PreferredHeight, Math.Max(choice.Height, choice.PreferredHeight));
                if (panel.RowStyles[0].Height != height) panel.RowStyles[0].Height = height;
                Size minimum = new Size(0, height + panel.Padding.Vertical);
                if (panel.MinimumSize != minimum) panel.MinimumSize = minimum;
            };
            choice.FontChanged += fitHeight;
            choice.SizeChanged += fitHeight;
            label.FontChanged += fitHeight;
            fitHeight(null, EventArgs.Empty);
            return panel;
        }
    }

    internal sealed class TextureChannelPictureBox : ZoomPanPictureBox
    {
        TexturePreviewChannel channel;
        Image cachedSource;
        Bitmap channelImage;
        string channelFailure;

        internal TexturePreviewChannel Channel
        {
            get { return channel; }
            set
            {
                if (value < TexturePreviewChannel.Rgba || value > TexturePreviewChannel.Alpha)
                    throw new ArgumentOutOfRangeException("value");
                if (value == channel) return;
                ClearChannelImage();
                channel = value;
                Invalidate();
            }
        }

        internal Image ChannelImage
        {
            get
            {
                if (!ReferenceEquals(cachedSource, Image)) ClearChannelImage();
                if (channel == TexturePreviewChannel.Rgba || Image == null) return Image;
                if (channelImage == null && channelFailure == null)
                {
                    cachedSource = Image;
                    Bitmap bitmap = Image as Bitmap;
                    if (bitmap == null) SetChannelFailure();
                    else
                    {
                        try { channelImage = TextureChannelPreview.Create(bitmap, channel); }
                        catch (OutOfMemoryException) { SetChannelFailure(); }
                        catch (ExternalException) { SetChannelFailure(); }
                        catch (ArgumentException) { SetChannelFailure(); }
                    }
                }
                return channelImage ?? Image;
            }
        }

        void ClearChannelImage()
        {
            if (channelImage != null) channelImage.Dispose();
            channelImage = null;
            cachedSource = null;
            channelFailure = null;
        }

        void SetChannelFailure()
        {
            channelFailure = L.T("Kanalvorschau nicht verfügbar. RGBA wird angezeigt.",
                "Channel preview unavailable. Showing RGBA.");
        }

        protected override void PaintPreview(PaintEventArgs e)
        {
            Image preview = ChannelImage;
            if (channel == TexturePreviewChannel.Rgba || preview == null || channelFailure != null)
            {
                base.PaintPreview(e);
                return;
            }
            e.Graphics.DrawImage(preview, ImageRectangle());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (channelFailure == null) return;
            var bounds = new Rectangle(8, 8, Math.Max(1, Width - 16), Font.Height * 2 + 12);
            using (var background = new SolidBrush(DarkTheme.Panel)) e.Graphics.FillRectangle(background, bounds);
            TextRenderer.DrawText(e.Graphics, channelFailure, Font, Rectangle.Inflate(bounds, -6, -4), DarkTheme.Fore,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ClearChannelImage();
            base.Dispose(disposing);
        }
    }
}
