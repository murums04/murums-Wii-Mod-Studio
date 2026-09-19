using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class FontSymbolsForm : StudioToolForm
    {
        sealed class Symbol
        {
            internal string Font;
            internal int Code;
        }
        readonly Dictionary<string, byte[]> sources;
        internal readonly Dictionary<string, byte[]> Changes = new Dictionary<string, byte[]>();
        readonly ListView symbols = new ListView { Dock = DockStyle.Fill, View = View.LargeIcon, MultiSelect = false, HideSelection = false };
        readonly ImageList images = new ImageList { ImageSize = new Size(56, 56), ColorDepth = ColorDepth.Depth32Bit };
        readonly PictureBox preview = new murumsWiiModStudio.ZoomPanPictureBox { Dock = DockStyle.Right, Width = 240, SizeMode = PictureBoxSizeMode.Zoom, BackColor = DarkTheme.Panel2 };
        readonly Button replace, export, apply;

        internal static bool IsSymbol(int code)
        {
            return code >= 0xE000 && code <= 0xF8FF || code >= 0x2000 && code <= 0x2BFF;
        }

        internal FontSymbolsForm(Dictionary<string, byte[]> fonts) : base("Font symbols", "Browse stars, trophies and game icons • Replace individual symbols with PNG • No TTF conversion")
        {
            sources = fonts;
            replace = Action("Replace PNG…", "Fit one PNG into the original glyph cell. Character mapping and spacing remain unchanged.", Replace);
            export = Action("Export symbol PNG…", "Export the selected symbol as a transparent PNG.", Export);
            apply = ExportAction("Apply symbol changes", "Queue changes in Font Changer. Save font copies there to export.", delegate { DialogResult = DialogResult.OK; Close(); });
            symbols.LargeImageList = images;
            IntPtr imageHandle = images.Handle;
            foreach (var pair in sources)
            {
                var font = new BrfntFont(pair.Value);
                var seen = new HashSet<int>();
                var pages = new Dictionary<int, Bitmap>();
                foreach (var item in font.Characters.Where(c => IsSymbol(c.Key) && !BrfntFont.IsTextCharacter(c.Key) && !(BrfntFont.HasGameNumbers(pair.Key) && BrfntFont.GameNumberCharacter(c.Key) != c.Key)).OrderBy(c => c.Key))
                {
                    if (!seen.Add(item.Value)) continue;
                    int page = item.Value / (font.Columns * font.Rows), slot = item.Value % (font.Columns * font.Rows);
                    Bitmap atlas;
                    if (!pages.TryGetValue(page, out atlas)) pages.Add(page, atlas = font.Atlas(page));
                    using (var glyph = atlas.Clone(new Rectangle(slot % font.Columns * (font.CellWidth + 1) + 1,
                        slot / font.Columns * (font.CellHeight + 1) + 1, font.CellWidth, font.CellHeight), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    using (var thumbnail = new Bitmap(56, 56))
                    {
                        using (var g = Graphics.FromImage(thumbnail))
                        {
                            g.Clear(DarkTheme.Panel2);
                            float scale = Math.Min(52f / glyph.Width, 52f / glyph.Height);
                            g.DrawImage(glyph, (56 - glyph.Width * scale) / 2, (56 - glyph.Height * scale) / 2, glyph.Width * scale, glyph.Height * scale);
                        }
                        images.Images.Add(thumbnail);
                    }
                    symbols.Items.Add(new ListViewItem("U+" + item.Key.ToString("X4"), images.Images.Count - 1) {
                        Tag = new Symbol { Font = pair.Key, Code = item.Key },
                        ToolTipText = Path.GetFileName(pair.Key)
                    });
                }
                foreach (var pageImage in pages.Values) pageImage.Dispose();
            }
            symbols.ShowItemToolTips = true;
            symbols.SelectedIndexChanged += delegate { RefreshSymbol(); };
            Body.Controls.Add(symbols);
            Body.Controls.Add(preview);
            Finish();
            symbols.BackColor = DarkTheme.Panel2;
            symbols.ForeColor = Color.White;
            replace.Enabled = export.Enabled = apply.Enabled = false;
            Status.Text = "Symbols are never replaced by TTF import. Shared glyph aliases keep referring to the same symbol.";
        }

        byte[] Current(Symbol symbol)
        {
            byte[] bytes;
            return Changes.TryGetValue(symbol.Font, out bytes) ? bytes : sources[symbol.Font];
        }

        void RefreshSymbol()
        {
            replace.Enabled = export.Enabled = symbols.SelectedItems.Count == 1;
            if (!replace.Enabled) return;
            var symbol = (Symbol)symbols.SelectedItems[0].Tag;
            var font = new BrfntFont(Current(symbol));
            var old = preview.Image;
            preview.Image = font.GlyphImage(symbol.Code);
            if (old != null) old.Dispose();
            Status.Text = Path.GetFileName(symbol.Font) + " • U+" + symbol.Code.ToString("X4")
                + " • " + font.CellWidth + " × " + font.CellHeight + " • PNG keeps its aspect ratio.";
        }

        void Replace()
        {
            if (symbols.SelectedItems.Count != 1) return;
            using (var picker = new OpenFileDialog { Filter = "PNG image|*.png" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                var symbol = (Symbol)symbols.SelectedItems[0].Tag;
                using (var bitmap = new Bitmap(picker.FileName))
                    Changes[symbol.Font] = new BrfntFont(Current(symbol)).ReplaceSymbol(symbol.Code, bitmap);
                apply.Enabled = true;
                RefreshSymbol();
                using (var thumbnail = new Bitmap(56, 56))
                {
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {
                        graphics.Clear(DarkTheme.Panel2);
                        float scale = Math.Min(52f / preview.Image.Width, 52f / preview.Image.Height);
                        graphics.DrawImage(preview.Image, (56 - preview.Image.Width * scale) / 2,
                            (56 - preview.Image.Height * scale) / 2, preview.Image.Width * scale, preview.Image.Height * scale);
                    }
                    images.Images[symbols.SelectedItems[0].ImageIndex] = thumbnail;
                }
            }
        }

        void Export()
        {
            if (symbols.SelectedItems.Count != 1) return;
            var symbol = (Symbol)symbols.SelectedItems[0].Tag;
            using (var picker = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "symbol-" + symbol.Code.ToString("X4") + ".png" })
                if (picker.ShowDialog(this) == DialogResult.OK)
                    using (var bitmap = new BrfntFont(Current(symbol)).GlyphImage(symbol.Code))
                        bitmap.Save(picker.FileName, System.Drawing.Imaging.ImageFormat.Png);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { images.Dispose(); if (preview.Image != null) preview.Image.Dispose(); }
            base.Dispose(disposing);
        }
    }
}