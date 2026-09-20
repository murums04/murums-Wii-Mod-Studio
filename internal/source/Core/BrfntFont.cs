using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;

namespace murumsWiiModStudio
{
    internal sealed class BrfntFont
    {
        readonly byte[] data;
        public readonly Dictionary<int, int> Characters = new Dictionary<int, int>();
        readonly Dictionary<int, int> widths = new Dictionary<int, int>();
        readonly Dictionary<int, int> lefts = new Dictionary<int, int>(), advances = new Dictionary<int, int>();
        public int NominalWidth, NominalHeight;
        public int CellWidth, CellHeight, Baseline, Columns, Rows, Width, Height, Sheets, Format, SheetSize, TextureOffset;
        static int U16(byte[] b, int p)
        {
            if (p < 0 || p + 2 > b.Length)
                throw new InvalidDataException("Truncated BRFNT.");
            return b[p] * 256 + b[p + 1];
        }

        static int U32(byte[] b, int p)
        {
            return checked((int)BigEndian.ReadUInt32(b, p));
        }

        public BrfntFont(byte[] bytes)
        {
            data = (byte[])bytes.Clone();
            if (bytes.Length < 48 || Encoding.ASCII.GetString(bytes, 0, 4) != "RFNT" || U16(bytes, 4) != 0xFEFF || U16(bytes, 6) != 0x104 || U32(bytes, 8) != bytes.Length)
                throw new InvalidDataException("Expected a big-endian Wii BRFNT (RFNT 1.4).");
            int p = U16(bytes, 12);
            bool finf = false, tglp = false;
            for (int block = 0; block < U16(bytes, 14); block++)
            {
                int length = U32(bytes, p + 4), end = checked(p + length);
                if (length < 8 || end > bytes.Length)
                    throw new InvalidDataException("Invalid BRFNT section.");
                string tag = Encoding.ASCII.GetString(bytes, p, 4);
                if (tag == "FINF")
                {
                    if (length < 32 || bytes[p + 15] != 1)
                        throw new NotSupportedException("Only UTF-16 Wii fonts are supported.");
                    NominalHeight = bytes[p + 28]; NominalWidth = bytes[p + 29];
                    finf = true;
                }

                if (tag == "TGLP")
                {
                    if (tglp || length < 32)
                        throw new InvalidDataException("Invalid font atlas.");
                    tglp = true;
                    CellWidth = bytes[p + 8];
                    CellHeight = bytes[p + 9];
                    Baseline = bytes[p + 10];
                    SheetSize = U32(bytes, p + 12);
                    Sheets = U16(bytes, p + 16);
                    Format = U16(bytes, p + 18);
                    Columns = U16(bytes, p + 20);
                    Rows = U16(bytes, p + 22);
                    Width = U16(bytes, p + 24);
                    Height = U16(bytes, p + 26);
                    TextureOffset = U32(bytes, p + 28);
                    if (Columns < 1 || Rows < 1 || Sheets < 1 || CellWidth < 1 || CellHeight < 1 || Width > 4096 || Height > 4096 || Columns * (CellWidth + 1) > Width || Rows * (CellHeight + 1) > Height || TextureOffset < p + 32 || (long)TextureOffset + (long)SheetSize * Sheets > end)
                        throw new InvalidDataException("Invalid BRFNT atlas dimensions.");
                    if (Format != 0 && Format != 1 && Format != 2 && Format != 3 && Format != 5 && Format != 6)
                        throw new NotSupportedException("Font import supports I4, I8, IA4, IA8, RGB5A3 and RGBA32 atlases.");
                }

                if (tag == "CWDH")
                {
                    if (length < 16)
                        throw new InvalidDataException("Invalid font widths.");
                    int first = U16(bytes, p + 8), last = U16(bytes, p + 10);
                    if (last < first || 16L + (last - first + 1) * 3 > length)
                        throw new InvalidDataException("Truncated font widths.");
                    for (int i = first; i <= last; i++)
                    {
                        int at = p + 16 + (i - first) * 3;
                        lefts[i] = unchecked((sbyte)bytes[at]);
                        widths[i] = bytes[at + 1];
                        advances[i] = bytes[at + 2];
                    }
                }

                if (tag == "CMAP")
                {
                    if (length < 22)
                        throw new InvalidDataException("Invalid font character map.");
                    int first = U16(bytes, p + 8), last = U16(bytes, p + 10), method = U16(bytes, p + 12);
                    if (last < first)
                        throw new InvalidDataException("Invalid character range.");
                    if (method == 0)
                        for (int c = first; c <= last; c++)
                            Add(c, U16(bytes, p + 20) + c - first);
                    else if (method == 1)
                    {
                        if (20L + (last - first + 1) * 2 > length)
                            throw new InvalidDataException("Truncated character map.");
                        for (int c = first; c <= last; c++)
                            Add(c, U16(bytes, p + 20 + (c - first) * 2));
                    }
                    else if (method == 2)
                    {
                        int count = U16(bytes, p + 20);
                        if (22L + count * 4 > length)
                            throw new InvalidDataException("Truncated character map.");
                        for (int i = 0; i < count; i++)
                            Add(U16(bytes, p + 22 + i * 4), U16(bytes, p + 24 + i * 4));
                    }
                    else
                        throw new NotSupportedException("Unknown BRFNT character map method.");
                }

                p = end;
            }

            if (!finf || !tglp || Characters.Count == 0 || Characters.Values.Any(i => i >= Columns * Rows * Sheets || !widths.ContainsKey(i)))
                throw new InvalidDataException("Incomplete BRFNT.");
            if (TplTextureEditor.GetFirstImageInfo(SheetTpl(0)).PayloadLength != SheetSize)
                throw new NotSupportedException("Padded or mipmapped font sheets are not supported.");
        }

        void Add(int c, int glyph)
        {
            if (glyph != 65535)
                Characters[c] = glyph;
        }

        byte[] SheetTpl(int sheet)
        {
            byte[] tpl = new byte[64 + SheetSize];
            BigEndian.WriteUInt32(tpl, 0, 0x0020AF30);
            BigEndian.WriteUInt32(tpl, 4, 1);
            BigEndian.WriteUInt32(tpl, 8, 12);
            BigEndian.WriteUInt32(tpl, 12, 20);
            tpl[20] = (byte)(Height >> 8);
            tpl[21] = (byte)Height;
            tpl[22] = (byte)(Width >> 8);
            tpl[23] = (byte)Width;
            BigEndian.WriteUInt32(tpl, 24, (uint)Format);
            BigEndian.WriteUInt32(tpl, 28, 64);
            Buffer.BlockCopy(data, TextureOffset + sheet * SheetSize, tpl, 64, SheetSize);
            return tpl;
        }

        public Bitmap Atlas(int sheet)
        {
            if (sheet < 0 || sheet >= Sheets)
                throw new ArgumentOutOfRangeException("sheet");
            TexturePreviewResult result;
            string error;
            if (!TexturePreview.TryDecode("font.tpl", SheetTpl(sheet), 0, out result, out error))
                throw new InvalidDataException(error);
            using (result)
            {
                var bitmap = new Bitmap(result.Bitmap);
                if (Format == 0 || Format == 1)
                    for (int y = 0; y < bitmap.Height; y++)
                        for (int x = 0; x < bitmap.Width; x++)
                            bitmap.SetPixel(x, y, Color.FromArgb(bitmap.GetPixel(x, y).R, 255, 255, 255));
                return bitmap;
            }
        }

        public Bitmap Sample(string text)
        {
            if (text == null || text.Length > 80)
                throw new ArgumentException("Use up to 80 preview characters.");
            int length = 16;
            foreach (char c in text)
            {
                int glyph;
                length += Characters.TryGetValue(c, out glyph) ? advances[glyph] : CellWidth;
            }

            var bitmap = new Bitmap(Math.Max(32, Math.Min(22000, length + CellWidth)), CellHeight + 16, PixelFormat.Format32bppArgb);
            var atlases = new Dictionary<int, Bitmap>();
            try
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    int x = 8;
                    foreach (char c in text)
                    {
                        int glyph;
                        if (!Characters.TryGetValue(c, out glyph))
                        {
                            x += CellWidth;
                            continue;
                        }

                        int sheet = glyph / (Columns * Rows), slot = glyph % (Columns * Rows);
                        if (!atlases.ContainsKey(sheet))
                            atlases.Add(sheet, Atlas(sheet));
                        if (widths[glyph] > 0)
                            g.DrawImage(atlases[sheet], new Rectangle(x + lefts[glyph], 8, widths[glyph], CellHeight), new Rectangle(slot % Columns * (CellWidth + 1) + 1, slot / Columns * (CellHeight + 1) + 1, widths[glyph], CellHeight), GraphicsUnit.Pixel);
                        x += advances[glyph];
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
            finally
            {
                foreach (var atlas in atlases.Values)
                    atlas.Dispose();
            }
        }

        internal Bitmap LayoutSample(string text, float fontWidth, float fontHeight, float spacing, out float advance)
        {
            if (text == null || text.Length > 80 || NominalWidth < 1 || NominalHeight < 1
                || Single.IsNaN(fontWidth) || Single.IsInfinity(fontWidth) || fontWidth <= 0 || fontWidth > 512
                || Single.IsNaN(fontHeight) || Single.IsInfinity(fontHeight) || fontHeight <= 0 || fontHeight > 512
                || Single.IsNaN(spacing) || Single.IsInfinity(spacing) || Math.Abs(spacing) > 128)
                throw new InvalidDataException("Unsupported text layout metrics.");
            float scaleX = fontWidth / NominalWidth, scaleY = fontHeight / NominalHeight;
            advance = 0;
            foreach (char c in text)
            {
                int glyph;
                advance += (Characters.TryGetValue(c, out glyph) ? advances[glyph] : CellWidth) * scaleX + spacing;
            }
            if (text.Length > 0) advance -= spacing;
            int width = (int)Math.Ceiling(Math.Max(1, advance) + CellWidth * scaleX + 32);
            int height = (int)Math.Ceiling(CellHeight * scaleY + 32);
            if (width > 8192 || height > 4096) throw new InvalidDataException("Text preview is too large.");
            var bitmap = new Bitmap(width, height);
            var sheets = new Dictionary<int, Bitmap>();
            try
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    float x = 16;
                    foreach (char c in text)
                    {
                        int glyph;
                        if (!Characters.TryGetValue(c, out glyph)) { x += CellWidth * scaleX + spacing; continue; }
                        int sheetIndex = glyph / (Columns * Rows), slot = glyph % (Columns * Rows);
                        if (!sheets.ContainsKey(sheetIndex)) sheets.Add(sheetIndex, Atlas(sheetIndex));
                        if (widths[glyph] > 0)
                            g.DrawImage(sheets[sheetIndex], new RectangleF(x + lefts[glyph] * scaleX, 16, widths[glyph] * scaleX, CellHeight * scaleY),
                                new RectangleF(slot % Columns * (CellWidth + 1) + 1, slot / Columns * (CellHeight + 1) + 1, widths[glyph], CellHeight), GraphicsUnit.Pixel);
                        x += advances[glyph] * scaleX + spacing;
                    }
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
            finally { foreach (var sheetImage in sheets.Values) sheetImage.Dispose(); }
        }

        internal Bitmap GlyphImage(int character)
        {
            int glyph;
            if (!Characters.TryGetValue(character, out glyph)) throw new ArgumentException("Unknown character.");
            int slot = glyph % (Columns * Rows);
            using (var atlas = Atlas(glyph / (Columns * Rows)))
                return atlas.Clone(new Rectangle((slot % Columns) * (CellWidth + 1) + 1,
                    (slot / Columns) * (CellHeight + 1) + 1, CellWidth, CellHeight), PixelFormat.Format32bppArgb);
        }

        internal byte[] ReplaceSymbol(int character, Bitmap image)
        {
            int glyph;
            if (!Characters.TryGetValue(character, out glyph)) throw new ArgumentException("Unknown symbol.");
            int sheet = glyph / (Columns * Rows), slot = glyph % (Columns * Rows);
            int x = slot % Columns * (CellWidth + 1) + 1, y = slot / Columns * (CellHeight + 1) + 1;
            int usableWidth = Math.Min(CellWidth, widths[glyph]);
            if (usableWidth < 3 || CellHeight < 3) throw new InvalidOperationException("This glyph has no usable image area.");
            byte[] output = (byte[])data.Clone();
            using (var atlas = Atlas(sheet))
            {
                using (var g = Graphics.FromImage(atlas))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.FillRectangle(Brushes.Transparent, x, y, CellWidth, CellHeight);
                    float scale = Math.Min((usableWidth - 2f) / image.Width, (CellHeight - 2f) / image.Height);
                    float w = image.Width * scale, h = image.Height * scale;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, new RectangleF(x + (usableWidth - w) / 2, y + (CellHeight - h) / 2, w, h));
                }
                if (Format == 0 || Format == 1)
                    for (int py = 0; py < atlas.Height; py++)
                        for (int px = 0; px < atlas.Width; px++)
                        {
                            int coverage = atlas.GetPixel(px, py).A;
                            atlas.SetPixel(px, py, Color.FromArgb(255, coverage, coverage, coverage));
                        }
                byte[] encoded = TplTextureEditor.ReplaceFirstImage(SheetTpl(sheet), atlas, false);
                Buffer.BlockCopy(encoded, 64, output, TextureOffset + sheet * SheetSize, SheetSize);
            }
            return output;
        }
        internal static bool IsTextCharacter(int code)
        {
            if (code >= 33 && code <= 255 && code != 127 && !(code >= 128 && code < 161)) return true;
            if (code < 256 || code > 65535) return false;
            var category = Char.GetUnicodeCategory((char)code);
            return category == System.Globalization.UnicodeCategory.UppercaseLetter
                || category == System.Globalization.UnicodeCategory.LowercaseLetter
                || category == System.Globalization.UnicodeCategory.TitlecaseLetter
                || category == System.Globalization.UnicodeCategory.ModifierLetter
                || category == System.Globalization.UnicodeCategory.OtherLetter
                || category == System.Globalization.UnicodeCategory.DecimalDigitNumber;
        }

        internal static int GameNumberCharacter(int code)
        {
            if (code >= 0x2460 && code <= 0x2469) return '0' + code - 0x2460;
            switch (code)
            {
                case 0x246A: return ':';
                case 0x246B: return '.';
                case 0x246C: return '/';
                case 0x246D: return '-';
                default: return code;
            }
        }

        internal static bool HasGameNumbers(string name)
        {
            return System.IO.Path.GetFileName(name).Equals("kart_kanji_font.brfnt", StringComparison.OrdinalIgnoreCase);
        }
        public byte[] ImportLatin(string ttf, out int replaced)
        {
            return ImportLatin(ttf, Color.White, Color.White, 0, out replaced);
        }

        public byte[] ImportLatin(string ttf, Color fill, Color outlineColor, float outlineWidth, out int replaced)
        {
            return ImportLatin(ttf, fill, outlineColor, outlineWidth, GlyphHinting.None, out replaced);
        }

        public byte[] ImportLatin(string ttf, Color fill, Color outlineColor, float outlineWidth, GlyphHinting hinting, out int replaced, bool gameNumbers = false, Dictionary<int, string> report = null)
        {
            return ImportFonts(new FontScriptSources { Latin = ttf }, fill, outlineColor, outlineWidth, hinting, out replaced, gameNumbers, report);
        }

        float SafeGlyphLeft(int glyph)
        {
            // Zeichen inklusive Kontur innerhalb ihres tatsächlichen Vorschubs halten.
            return Math.Max(0, -lefts[glyph]) + 1;
        }

        float SafeGlyphRight(int glyph)
        {
            return Math.Min(Math.Min(widths[glyph], CellWidth), advances[glyph] - lefts[glyph]) - 1;
        }
        internal byte[] ImportFonts(FontScriptSources sources, Color fill, Color outlineColor, float outlineWidth, GlyphHinting hinting, out int replaced, bool gameNumbers = false, Dictionary<int, string> report = null)
        {
            if (float.IsNaN(outlineWidth) || outlineWidth < 0 || outlineWidth > 4)
                throw new ArgumentOutOfRangeException("outlineWidth");
            // I4/I8-Schriften speichern Deckung; die Farbe kommt aus dem Spielmaterial.
            if (Format == 0 || Format == 1)
            {
                fill = Color.White;
                outlineColor = Color.White;
            }
            byte[] output = (byte[])data.Clone();
            replaced = 0;

            var replacedGlyphs = new HashSet<int>();
            using (var faces = new FontScriptCollection(sources))
            {
                var protectedGlyphs = new HashSet<int>(Characters.Where(c => { int code = gameNumbers ? GameNumberCharacter(c.Key) : c.Key; return !IsTextCharacter(code) || !faces.Supports(code); }).Select(c => c.Value));
                // Ein gemeinsam genutztes Atlasfeld darf nicht zwei verschiedenen TTFs gehören.
                foreach (var group in Characters.GroupBy(p => p.Value))
                    if (group.Select(p => sources.ForCharacter(gameNumbers ? GameNumberCharacter(p.Key) : p.Key))
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                        protectedGlyphs.Add(group.Key);
                if (report != null)
                    foreach (var pair in Characters)
                    {
                        int code = gameNumbers ? GameNumberCharacter(pair.Key) : pair.Key;
                        report[pair.Key] = !IsTextCharacter(code) ? "Protected symbol / control"
                            : !faces.Supports(code) ? "Missing in TTF - original kept"
                            : protectedGlyphs.Contains(pair.Value) ? "Protected shared glyph - original kept"
                            : "No usable outline / space - original kept";
                    }
                // Measure the outlines actually imported, not the family's line spacing.
                // Decorative fonts can have very large metric descents; BRFNT baselines
                // can also lie below the atlas cell. Neither may collapse visible ink.

                float inkAbove = 0, inkBelow = 0;
                var widthFits = new List<float>();
                foreach (var pair in Characters)
                {
                    int c = gameNumbers ? GameNumberCharacter(pair.Key) : pair.Key;
                    if (!faces.Supports(c) || !IsTextCharacter(c) || protectedGlyphs.Contains(pair.Value) || widths[pair.Value] == 0)
                        continue;
                    var face = faces.ForCharacter(c);
                    var family = face.Family;
                    var style = face.Style;
                    float ascent = family.GetCellAscent(style) / (float)family.GetEmHeight(style) * CellHeight;
                    using (var outline = new GraphicsPath())
                    using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
                    {
                        outline.AddString(char.ConvertFromUtf32(c), family, (int)style, CellHeight, PointF.Empty, format);
                        var bounds = outline.GetBounds();
                        if (bounds.Width <= 0 || bounds.Height <= 0)
                            continue;
                        float usable = SafeGlyphRight(pair.Value) - SafeGlyphLeft(pair.Value) - outlineWidth;
                        if (usable > 0 && Char.IsLetterOrDigit((char)c))
                            widthFits.Add(usable / bounds.Width);
                        inkAbove = Math.Max(inkAbove, ascent - bounds.Top);
                        inkBelow = Math.Max(inkBelow, bounds.Bottom - ascent);
                    }
                }

                if (inkAbove + inkBelow <= 0)
                    return output;
                float margin = 1 + outlineWidth / 2;
                float fit = Math.Max(1, CellHeight - 2 * margin) / (inkAbove + inkBelow);
                float baseline = Math.Min(Math.Max(margin, Baseline), CellHeight - margin - inkBelow * fit);
                float sy = Math.Min(fit, (baseline - margin) / Math.Max(1, inkAbove));
                // Breite Schriften gemeinsam verkleinern, ohne einzelne schmale Zeichen aufzublasen.
                if (widthFits.Count > 0)
                {
                    widthFits.Sort();
                    sy = Math.Min(sy, widthFits[(widthFits.Count - 1) / 4]);
                }
                // Preserve spacing and icon aliases; only cells for existing printable Latin characters are replaced.
                for (int sheet = 0; sheet < Sheets; sheet++)
                    using (Bitmap atlas = Atlas(sheet))
                    using (Graphics g = Graphics.FromImage(atlas))
                    {
                        bool changed = false;
                        var done = new HashSet<int>();
                        foreach (var pair in Characters.OrderBy(c => c.Key))
                        {
                            int c = gameNumbers ? GameNumberCharacter(pair.Key) : pair.Key, glyph = pair.Value;
                            if (!faces.Supports(c) || !IsTextCharacter(c) || protectedGlyphs.Contains(glyph) || glyph / (Columns * Rows) != sheet || !done.Add(glyph) || widths[glyph] == 0)
                                continue;
                            var face = faces.ForCharacter(c);
                            var family = face.Family;
                            var style = face.Style;
                            float ascent = family.GetCellAscent(style) / (float)family.GetEmHeight(style) * CellHeight;
                            using (var path = new GraphicsPath())
                            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
                            {
                                path.AddString(char.ConvertFromUtf32(c), family, (int)style, CellHeight, PointF.Empty, format);
                                RectangleF bounds = path.GetBounds();
                                if (bounds.Width <= 0 || bounds.Height <= 0)
                                    continue;
                                int slot = glyph % (Columns * Rows), x = (slot % Columns) * (CellWidth + 1) + 1, y = (slot / Columns) * (CellHeight + 1) + 1;
                                float safeLeft = SafeGlyphLeft(glyph), safeRight = SafeGlyphRight(glyph);
                                float sx = Math.Min(sy, (safeRight - safeLeft - outlineWidth) / bounds.Width);
                                float origin = safeLeft + (safeRight - safeLeft - bounds.Width * sx) / 2;
                                if (sx <= 0 || sy <= 0)
                                    continue;
                                if (hinting != GlyphHinting.None)
                                {
                                    var target = new RectangleF(origin, baseline + (bounds.Y - ascent) * sy, bounds.Width * sx, bounds.Height * sy);
                                    using (var glyphImage = GlyphRasterizer.Render(char.ConvertFromUtf32(c), family, style, CellHeight, bounds, new Size(CellWidth, CellHeight), target, fill, outlineColor, outlineWidth, hinting))
                                    {
                                        g.CompositingMode = CompositingMode.SourceCopy;
                                        g.DrawImageUnscaled(glyphImage, x, y);
                                    }
                                }
                                else
                                {
                                    using (var transform = new Matrix(sx, 0, 0, sy, x + origin - bounds.X * sx, y + baseline - ascent * sy))
                                        path.Transform(transform);
                                    g.SetClip(new Rectangle(x, y, CellWidth, CellHeight));
                                    g.CompositingMode = CompositingMode.SourceCopy;
                                    using (var clear = new SolidBrush(Color.Transparent))
                                        g.FillRectangle(clear, x, y, CellWidth, CellHeight);
                                    g.SmoothingMode = SmoothingMode.AntiAlias;
                                    g.CompositingMode = CompositingMode.SourceOver;
                                    if (outlineWidth > 0)
                                        using (var pen = new Pen(outlineColor, outlineWidth)
                                        {
                                            LineJoin = LineJoin.Round
                                        }

                                        )
                                            g.DrawPath(pen, path);
                                    using (var brush = new SolidBrush(fill))
                                        g.FillPath(brush, path);
                                    g.ResetClip();
                                    changed = true;
                                    replaced++;
                                    replacedGlyphs.Add(glyph);
                                }

                                if (hinting != GlyphHinting.None)
                                {
                                    changed = true;
                                    replaced++;
                                    replacedGlyphs.Add(glyph);
                                }
                            }
                        }

                        if (changed)
                        {
                            if (Format == 0 || Format == 1)
                                for (int y = 0; y < atlas.Height; y++)
                                    for (int x = 0; x < atlas.Width; x++)
                                    {
                                        int coverage = atlas.GetPixel(x, y).A;
                                        atlas.SetPixel(x, y, Color.FromArgb(255, coverage, coverage, coverage));
                                    }
                            byte[] encoded = TplTextureEditor.ReplaceFirstImage(SheetTpl(sheet), atlas, false);
                            Buffer.BlockCopy(encoded, 64, output, TextureOffset + sheet * SheetSize, SheetSize);
                        }
                    }
            }


            if (report != null)
                foreach (var pair in Characters)
                    if (replacedGlyphs.Contains(pair.Value)) report[pair.Key] = "Replaced — " + Path.GetFileName(sources.ForCharacter(gameNumbers ? GameNumberCharacter(pair.Key) : pair.Key));
            return output;
        }
    }
}

