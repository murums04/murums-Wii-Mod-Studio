using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace murumsWiiModStudio
{
    internal sealed class HudFontSettings
    {
        public string Ttf;
        public Color Fill = Color.Black, Outline = Color.White;
        public decimal Stroke = 2;
        public int Hint;
    }

    internal sealed class HudNumberFontForm : StudioToolForm
    {
        SpecialEditorHistory editHistory;
        readonly byte[] source;
        string ttf;
        Color fill = Color.Black, outline = Color.White;
        readonly TextBox character = new TextBox
        {
            Width = 65,
            MaxLength = 1
        };
        readonly NumericUpDown stroke = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 4,
            Value = 2,
            DecimalPlaces = 1,
            Increment = 0.5M,
            Width = 60
        };
        readonly ComboBox hint = new ComboBox
        {
            Width = 155,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly PictureBox picture = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(50, 50, 55)
        };
        readonly Button apply, fillButton, outlineButton;
        byte[] candidate;
        public byte[] Result;
        readonly HudFontSettings settings;
        readonly List<HudTexture> available;
        readonly HudTexture selected;
        readonly ComboBox scope = new ComboBox
        {
            Width = 365,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        readonly ComboBox review = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        public readonly Dictionary<HudTexture, byte[]> Results = new Dictionary<HudTexture, byte[]>();
        public HudNumberFontForm(string key, byte[] bytes) : this(key, bytes, new HudFontSettings(), null, null)
        {
        }

        public HudNumberFontForm(string key, byte[] bytes, HudFontSettings remembered, List<HudTexture> all, HudTexture current) : base(L.T("RR-MKWii HUD-Zahlenschrift Tool", "RR-MKWii HUD Number Font Tool"), L.T("TTF wählen • Einzeltextur oder Zeichensatz auswählen • Vorschau prüfen • Übernehmen", "Choose a TTF • Select one texture, matching names or a full set • Review • Apply"))
        {
            settings = remembered ?? new HudFontSettings();
            available = all;
            selected = current;
            ttf = settings.Ttf;
            fill = settings.Fill;
            outline = settings.Outline;
            stroke.Value = settings.Stroke;
            source = bytes;
            character.Text = Guess(key);
            hint.Items.AddRange(new object[] { L.T("Keine", "None"), L.T("Geglättet", "Hinted / smooth"), L.T("Scharf", "Hinted / sharp") });
            hint.SelectedIndex = Math.Max(0, Math.Min(hint.Items.Count - 1, settings.Hint));
            var openFont = Action(L.T("TTF wählen…", "Choose TTF…"), L.T("Schrift für Ziffern und Trennzeichen auswählen.", "Choose the font for this digit or punctuation mark."), delegate
            {
                var p = OpenPath(L.T("TrueType-Schrift|*.ttf", "TrueType font|*.ttf"));
                if (p != null)
                {
                    ttf = p;
                    InvalidatePreview();
                }
            });
            StudioActions.Icon(openFont, StudioIcon.Open);
            Actions.Controls.Add(new Label { Text = L.T("Zeichen", "Character"), AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(character);
            fillButton = Action(L.T("Füllfarbe…", "Fill colour…"), L.T("Füllfarbe wählen; Standard ist Schwarz.", "Choose the inside colour; default black."), delegate
            {
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = fill
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        fill = d.Color;
                        InvalidatePreview();
                    }
            });
            outlineButton = Action(L.T("Konturfarbe…", "Outline colour…"), L.T("Konturfarbe wählen; Standard ist Weiß.", "Choose the contour colour; default white."), delegate
            {
                using (var d = new ColorDialog
                {
                    FullOpen = true,
                    Color = outline
                }

                )
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        outline = d.Color;
                        InvalidatePreview();
                    }
            });
            Actions.Controls.Add(new Label { Text = L.T("Kontur (px)", "Outline (px)"), AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(stroke);
            Actions.Controls.Add(new Label { Text = L.T("Kantendarstellung", "Hinting"), AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(hint);
            Actions.SetFlowBreak(Actions.Controls[Actions.Controls.Count - 1], true);
            scope.Items.AddRange(new object[] { L.T("Nur gewählte Textur", "Selected texture only"), L.T("Gleicher Dateiname in allen Archiven", "Same filename in all loaded archives"), L.T("Alle Ziffern und Trennzeichen", "Complete digit set + separators") });
            scope.SelectedIndex = current != null && available != null && SetCharacter(key) != null ? 2 : 0;
            scope.Enabled = available != null && selected != null;
            character.Enabled = scope.SelectedIndex != 2;
            Actions.Controls.Add(scope);
            StudioUx.SetHelp(scope, L.T("Die Vorschau zeigt alle betroffenen Pfade. Der Zeichensatz umfasst erkannte tt_d_number_3d-/som_d_number_3d-Ziffern und Trennzeichen in den geladenen Archiven.", "Preview lists every affected path before Apply. Complete set includes recognized tt_d_number_3d / som_d_number_3d digits, slash and punctuation across loaded archives."));
            Action(L.T("Zeichen vorschauen", "Preview number / set"), L.T("Zieltexturen erzeugen und anzeigen. Größe und Format bleiben erhalten.", "Generate and decode the actual target TPL. Original size and format are retained."), Preview);
            apply = ExportAction(L.T("Textur übernehmen", "Apply to selected texture"), L.T("Ergebnis ins HUD-Tool übernehmen. Dort bearbeitete Archive speichern.", "Queue this generated picture in Race HUD. Save edited archives there to export."), delegate
            {
                if (candidate == null) return;
                Result = candidate;
                DialogResult = DialogResult.OK;
                Close();
            });
            Body.Controls.Add(picture);
            Body.Controls.Add(review);
            review.SelectedIndexChanged += delegate
            {
                Guard(ShowCandidate);
            };
            scope.SelectedIndexChanged += delegate
            {
                character.Enabled = scope.SelectedIndex != 2;
                InvalidatePreview();
            };
            character.TextChanged += delegate
            {
                InvalidatePreview();
            };
            stroke.ValueChanged += delegate
            {
                InvalidatePreview();
            };
            hint.SelectedIndexChanged += delegate
            {
                InvalidatePreview();
            };
            StudioUx.SetHelp(character, L.T("Ziffer oder Trennzeichen prüfen. Erlaubt ist ein Zeichen aus 0–9, /, :, . oder -. Vorschläge aus Dateinamen überprüfen.", "Check this digit or separator. Only one of 0–9, /, :, . or - is accepted. Filename guesses must be reviewed."));
            StudioUx.SetHelp(stroke, L.T("Konturstärke in Texturpixeln. Null schaltet die Kontur aus.", "Outline thickness in texture pixels. Zero disables it."));
            StudioUx.SetHelp(hint, L.T("TTF-Kantendarstellung: ohne Anpassung, geglättet oder scharf mit monochromer Rasteranpassung.", "Windows TTF rasterization: no hinting, smooth grid fitting or sharp monochrome grid fitting."));
            Finish();
            InvalidatePreview();
            Status.Text = key + L.T("\nTexturen auswählen und Vorschau prüfen. Einstellungen bleiben erhalten, solange das HUD-Tool geöffnet ist.", "\nChoose the affected textures, then preview. Settings are kept while Race HUD stays open.");
            editHistory = new SpecialEditorHistory(this, Actions,
                delegate { return new object[] { ttf, fill, outline, stroke.Value, hint.SelectedIndex, character.Text,
                    scope.SelectedIndex, candidate, SpecialEditorHistory.Copy(Results), review.SelectedItem }; },
                delegate(object[] state) {
                    ttf = (string)state[0]; fill = (Color)state[1]; outline = (Color)state[2];
                    stroke.Value = (decimal)state[3]; hint.SelectedIndex = (int)state[4]; character.Text = (string)state[5];
                    scope.SelectedIndex = (int)state[6]; character.Enabled = scope.SelectedIndex != 2;
                    ColourButton.SetColor(fillButton, fill); ColourButton.SetColor(outlineButton, outline);
                    candidate = (byte[])state[7]; SpecialEditorHistory.Replace(Results, (Dictionary<HudTexture, byte[]>)state[8]);
                    review.Items.Clear(); foreach (var target in Results.Keys) review.Items.Add(target);
                    if (state[9] != null && review.Items.Contains(state[9])) review.SelectedItem = state[9];
                    else if (review.Items.Count > 0) review.SelectedIndex = 0;
                    if (picture.Image != null) { picture.Image.Dispose(); picture.Image = null; }
                    ShowCandidate(); apply.Enabled = candidate != null;
                });
        }

        internal static string SetCharacter(string key)
        {
            string n = Path.GetFileNameWithoutExtension(key).ToLowerInvariant();
            var m = Regex.Match(n, @"^(?:tt|som)_d_number_3d_0([0-9])$");
            if (m.Success)
                return m.Groups[1].Value;
            if (n == "tt_d_number_3d_slash")
                return "/";
            if (n == "tt_d_number_3d_coron" || n == "som_d_number_3d_coron")
                return ".";
            if (n == "tt_d_number_3d_coron_00")
                return ":";
            return null;
        }

        internal static string Guess(string key)
        {
            string n = Path.GetFileNameWithoutExtension(key).ToLowerInvariant();
            string known = SetCharacter(key);
            if (known != null)
                return known;
            if (n.Contains("slash"))
                return "/";
            if (n.Contains("coron") || n.Contains("colon"))
                return ":";
            var m = Regex.Match(n, @"(?:number|num).*_0([0-9])$");
            return m.Success ? m.Groups[1].Value : "";
        }

        void InvalidatePreview()
        {
            if (editHistory != null && editHistory.Restoring) return;
            ColourButton.SetColor(fillButton, fill);
            ColourButton.SetColor(outlineButton, outline);
            candidate = null;
            if (picture.Image != null) { picture.Image.Dispose(); picture.Image = null; }
            Results.Clear();
            review.Items.Clear();
            if (apply != null)
                apply.Enabled = false;
            Status.Text = L.T("Vor dem Übernehmen die Vorschau prüfen. IA-Formate speichern Graustufen; Spielmaterialien können Farben verändern.", "Preview the current settings before applying. IA formats store grayscale; game materials may tint colours.");
        }

        void Preview()
        {
            InvalidatePreview();
            if (String.IsNullOrEmpty(ttf))
                throw new InvalidOperationException(L.T("Zuerst eine TTF-Schrift wählen.", "Choose a TTF first."));
            if (scope.SelectedIndex != 2 && (character.Text.Length != 1 || "0123456789/:.-".IndexOf(character.Text[0]) < 0))
                throw new InvalidOperationException(L.T("Eine Ziffer oder ein Trennzeichen eingeben: 0–9 / : . -", "Enter one digit or separator: 0–9 / : . -"));
            var targets = selected == null ? new List<HudTexture>() : scope.SelectedIndex == 0 ? new List<HudTexture>
            {
                selected
            }

            : (available ?? new List<HudTexture>()).Where(t => scope.SelectedIndex == 1 ? string.Equals(Path.GetFileName(t.Key), Path.GetFileName(selected.Key), StringComparison.OrdinalIgnoreCase) : SetCharacter(t.Key) != null).ToList();
            if (selected != null && available != null)
            {
                var names = new HashSet<string>(targets.Select(t => Path.GetFileName(t.Key)), StringComparer.OrdinalIgnoreCase);
                foreach (var replacement in available.Where(t => Path.GetFileName(t.Archive.Source).Equals("ReplacedAssets.szs", StringComparison.OrdinalIgnoreCase)
                    && names.Contains(Path.GetFileName(t.Key))))
                    if (!targets.Contains(replacement)) targets.Add(replacement);
            }
            if (selected != null && targets.Count == 0)
                throw new InvalidOperationException(L.T("Die geladenen Archive enthalten keine erkannten Zifferntexturen.", "No recognized digit textures in the loaded archives."));
            var pending = new Dictionary<HudTexture, byte[]>();
            if (selected == null)
            {
                using (var b = Generate(source, ttf, character.Text, fill, outline, (float)stroke.Value, (GlyphHinting)hint.SelectedIndex))
                    candidate = TplTextureEditor.ReplaceFirstImage(source, b, true);
            }
            else
                foreach (var target in targets)
                {
                    string text = scope.SelectedIndex == 2 ? SetCharacter(target.Key) : character.Text;
                    using (var b = Generate(target.Archive.Files[target.Key].Data, ttf, text, fill, outline, (float)stroke.Value, (GlyphHinting)hint.SelectedIndex))
                        pending.Add(target, TplTextureEditor.ReplaceFirstImage(target.Archive.Files[target.Key].Data, b, true));
                }

            Results.Clear();
            foreach (var entry in pending)
                Results.Add(entry.Key, entry.Value);
            review.Items.Clear();
            foreach (var target in targets)
                review.Items.Add(target);
            if (targets.Count > 0)
                review.SelectedIndex = 0;
            else
                ShowCandidate();
            apply.Text = targets.Count > 1 ? String.Format(L.T("{0} Texturen übernehmen", "Apply {0} textures"), targets.Count) : L.T("Textur übernehmen", "Apply to selected texture");
            apply.Enabled = true;
        }

        void ShowCandidate()
        {
            var target = review.SelectedItem as HudTexture;
            if (target != null)
                candidate = Results[target];
            if (candidate == null)
                return;
            TexturePreviewResult r;
            string error;
            if (!TexturePreview.TryDecode("number.tpl", candidate, 0, out r, out error))
                throw new InvalidDataException(L.T("Die erzeugte Textur kann nicht angezeigt werden. Bitte Schrift und Einstellungen prüfen.", "The generated texture cannot be previewed. Check the font and settings."));
            using (r)
            {
                var old = picture.Image;
                picture.Image = new Bitmap(r.Bitmap);
                if (old != null)
                    old.Dispose();
                Status.Text = (target == null ? L.T("Gewählte Textur", "Selected texture") : target.ToString()) + "\n" + r.Width + " × " + r.Height + " • " + r.FormatName + " • " + Math.Max(1, Results.Count) + L.T(" Texturen zum Übernehmen bereit. Alle Einträge prüfen. Noch nichts gespeichert.", " textures ready to apply. Review every entry. Nothing saved yet.");
            }
        }

        internal static Bitmap Generate(byte[] source, string ttf, string text, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            var info = TplTextureEditor.GetFirstImageInfo(source);
            var coverage = TtfCoverage.Latin(ttf);
            if (String.IsNullOrEmpty(text) || text.Any(c => !coverage.Contains(c)))
                throw new InvalidOperationException(L.T("Die TTF-Schrift enthält dieses Zeichen nicht. Bitte eine andere Schrift wählen.", "The TTF does not contain this character."));
            using (var fonts = new PrivateFontCollection())
            {
                fonts.AddFontFile(ttf);
                if (fonts.Families.Length == 0)
                    throw new InvalidDataException(L.T("Schrift kann nicht geladen werden. Bitte eine andere TTF-Datei wählen.", "Cannot load font."));
                var family = fonts.Families[0];
                var style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                using (var sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                using (var path = new GraphicsPath())
                {
                    path.AddString(text, family, (int)style, 64, PointF.Empty, sf);
                    var bounds = path.GetBounds();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                        throw new InvalidOperationException(L.T("Das Zeichen hat keine sichtbare Kontur. Bitte ein anderes Zeichen wählen.", "Character has no visible outline."));
                    float margin = 2 + stroke / 2;
                    float commonWidth = 0, digitTop = Single.MaxValue, digitBottom = Single.MinValue;
                    float allTop = Single.MaxValue, allBottom = Single.MinValue;
                    foreach (char c in "0123456789.,:")
                    {
                        if (!coverage.Contains(c)) continue;
                        using (var glyph = new GraphicsPath())
                        {
                            glyph.AddString(c.ToString(), family, (int)style, 64, PointF.Empty, sf);
                            var cell = glyph.GetBounds();
                            commonWidth = Math.Max(commonWidth, cell.Width);
                            allTop = Math.Min(allTop, cell.Top);
                            allBottom = Math.Max(allBottom, cell.Bottom);
                            if (c >= '0' && c <= '9')
                            {
                                digitTop = Math.Min(digitTop, cell.Top);
                                digitBottom = Math.Max(digitBottom, cell.Bottom);
                            }
                        }
                    }
                    if (digitTop == Single.MaxValue) { digitTop = bounds.Top; digitBottom = bounds.Bottom; }
                    float center = (digitTop + digitBottom) / 2;
                    bool singleHudGlyph = text.Length == 1 && "0123456789.,:".Contains(text);
                    float width = singleHudGlyph ? commonWidth : Math.Max(commonWidth, bounds.Width);
                    float extent = Math.Max(center - Math.Min(allTop, bounds.Top), Math.Max(allBottom, bounds.Bottom) - center);
                    float scale = Math.Min((info.Width - 2 * margin) / Math.Max(width, 1), (info.Height - 2 * margin) / Math.Max(2 * extent, 1));
                    if (scale <= 0)
                        throw new InvalidOperationException(L.T("Die Textur ist für diese Konturstärke zu klein. Bitte die Kontur verkleinern.", "Texture is too small for this outline."));
                    // Alle HUD-Zeichen teilen dieselbe Größe und Grundlinie; schmale Ziffern werden nicht aufgeblasen.
                    float y = info.Height / 2f + (bounds.Y - center) * scale;
                    return GlyphRasterizer.Render(text, family, style, 64, bounds, new Size(info.Width, info.Height),
                        new RectangleF((info.Width - bounds.Width * scale) / 2, y, bounds.Width * scale, bounds.Height * scale), fill, outline, stroke, hint);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && DialogResult == DialogResult.OK)
            {
                settings.Ttf = ttf;
                settings.Fill = fill;
                settings.Outline = outline;
                settings.Stroke = stroke.Value;
                settings.Hint = hint.SelectedIndex;
            }

            if (disposing && picture.Image != null)
            {
                picture.Image.Dispose();
                picture.Image = null;
            }

            base.Dispose(disposing);
        }
    }
}
