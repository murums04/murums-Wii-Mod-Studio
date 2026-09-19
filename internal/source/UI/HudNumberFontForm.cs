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
        readonly PictureBox picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(50, 50, 55)
        };
        readonly Button apply;
        byte[] candidate;
        public byte[] Result;
        readonly HudFontSettings settings;
        readonly List<HudTexture> available;
        readonly HudTexture selected;
        readonly ComboBox scope = new ComboBox
        {
            Width = 310,
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

        public HudNumberFontForm(string key, byte[] bytes, HudFontSettings remembered, List<HudTexture> all, HudTexture current) : base("HUD Number Font Tool", "Choose a TTF • Select one texture, matching names or a full set • Review • Apply")
        {
            settings = remembered;
            available = all;
            selected = current;
            ttf = settings.Ttf;
            fill = settings.Fill;
            outline = settings.Outline;
            stroke.Value = settings.Stroke;
            source = bytes;
            character.Text = Guess(key);
            hint.Items.AddRange(new object[] { "None", "Hinted / smooth", "Hinted / sharp" });
            hint.SelectedIndex = settings.Hint;
            Action("Choose TTF…", "Choose the font for this digit or punctuation mark.", delegate
            {
                var p = OpenPath("TrueType font|*.ttf");
                if (p != null)
                {
                    ttf = p;
                    InvalidatePreview();
                }
            });
            Actions.Controls.Add(new Label { Text = "Character", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(character);
            Action("Fill colour…", "Choose the inside colour; default black.", delegate
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
            Action("Outline colour…", "Choose the contour colour; default white.", delegate
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
            Actions.Controls.Add(new Label { Text = "Outline (px)", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(stroke);
            Actions.Controls.Add(new Label { Text = "Hinting", AutoSize = true, Margin = new Padding(8, 12, 2, 0) });
            Actions.Controls.Add(hint);
            Actions.SetFlowBreak(Actions.Controls[Actions.Controls.Count - 1], true);
            scope.Items.AddRange(new object[] { "Selected texture only", "Same filename in all loaded archives", "Complete digit set + separators" });
            scope.SelectedIndex = current != null && SetCharacter(key) != null ? 2 : 0;
            scope.Enabled = available != null;
            Actions.Controls.Add(scope);
            StudioUx.SetHelp(scope, "Preview lists every affected path before Apply. Complete set includes recognized tt_d_number_3d / som_d_number_3d digits, slash and punctuation across loaded archives.");
            Action("Preview number / set", "Generate and decode the actual target TPL. Original size and format are retained.", Preview);
            apply = ExportAction("Apply to selected texture", "Queue this generated picture in Race HUD. Save edited archives there to export.", delegate
            {
                Result = candidate;
                DialogResult = DialogResult.OK;
                Close();
            });
            Body.Controls.Add(picture);
            Body.Controls.Add(review);
            review.SelectedIndexChanged += delegate
            {
                ShowCandidate();
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
            StudioUx.SetHelp(character, "Check this digit or separator. Only one of 0–9, /, :, . or - is accepted. Filename guesses must be reviewed.");
            StudioUx.SetHelp(stroke, "Outline thickness in texture pixels. Zero disables it.");
            StudioUx.SetHelp(hint, "Windows TTF rasterization: no hinting, smooth grid fitting or sharp monochrome grid fitting.");
            Finish();
            InvalidatePreview();
            Status.Text = key + "\nChoose the affected textures, then preview. Settings are kept while Race HUD stays open.";
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
            candidate = null;
            Results.Clear();
            review.Items.Clear();
            if (apply != null)
                apply.Enabled = false;
            Status.Text = "Preview the current settings before applying. IA formats store grayscale; game materials may tint colours.";
        }

        void Preview()
        {
            if (ttf == null)
                throw new InvalidOperationException("Choose a TTF first.");
            if (scope.SelectedIndex != 2 && (character.Text.Length != 1 || "0123456789/:.-".IndexOf(character.Text[0]) < 0))
                throw new InvalidOperationException("Enter one digit or separator: 0–9 / : . -");
            var targets = selected == null ? new List<HudTexture>() : scope.SelectedIndex == 0 ? new List<HudTexture>
            {
                selected
            }

            : available.Where(t => scope.SelectedIndex == 1 ? string.Equals(Path.GetFileName(t.Key), Path.GetFileName(selected.Key), StringComparison.OrdinalIgnoreCase) : SetCharacter(t.Key) != null).ToList();
            if (selected != null && available != null)
            {
                var names = new HashSet<string>(targets.Select(t => Path.GetFileName(t.Key)), StringComparer.OrdinalIgnoreCase);
                foreach (var replacement in available.Where(t => Path.GetFileName(t.Archive.Source).Equals("ReplacedAssets.szs", StringComparison.OrdinalIgnoreCase)
                    && names.Contains(Path.GetFileName(t.Key))))
                    if (!targets.Contains(replacement)) targets.Add(replacement);
            }
            if (selected != null && targets.Count == 0)
                throw new InvalidOperationException("No recognized digit textures in the loaded archives.");
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
            apply.Text = targets.Count > 1 ? "Apply " + targets.Count + " textures" : "Apply to selected texture";
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
                throw new InvalidDataException(error);
            using (r)
            {
                var old = picture.Image;
                picture.Image = new Bitmap(r.Bitmap);
                if (old != null)
                    old.Dispose();
                Status.Text = (target == null ? "Selected texture" : target.ToString()) + "\n" + r.Width + " × " + r.Height + " • " + r.FormatName + " • " + Math.Max(1, Results.Count) + " queued by Apply. Review each entry above. Nothing saved yet.";
            }
        }

        internal static Bitmap Generate(byte[] source, string ttf, string text, Color fill, Color outline, float stroke, GlyphHinting hint)
        {
            var info = TplTextureEditor.GetFirstImageInfo(source);
            if (!TtfCoverage.Latin(ttf).Contains(text[0]))
                throw new InvalidOperationException("The TTF does not contain this character.");
            using (var fonts = new PrivateFontCollection())
            {
                fonts.AddFontFile(ttf);
                if (fonts.Families.Length == 0)
                    throw new InvalidDataException("Cannot load font.");
                var family = fonts.Families[0];
                var style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                using (var sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                using (var path = new GraphicsPath())
                using (var reference = new GraphicsPath())
                {
                    path.AddString(text, family, (int)style, 64, PointF.Empty, sf);
                    reference.AddString("0123456789", family, (int)style, 64, PointF.Empty, sf);
                    var bounds = path.GetBounds();
                    var digits = reference.GetBounds();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                        throw new InvalidOperationException("Character has no visible outline.");
                    float margin = 2 + stroke / 2;
                    float scale = Math.Min((info.Width - 2 * margin) / Math.Max(bounds.Width, 32), (info.Height - 2 * margin) / Math.Max(digits.Height, bounds.Height));
                    if (scale <= 0)
                        throw new InvalidOperationException("Texture is too small for this outline.");
                    float y = margin + (bounds.Y - digits.Y) * scale;
                    y = Math.Max(margin, Math.Min(y, info.Height - margin - bounds.Height * scale));
                    return GlyphRasterizer.Render(text, family, style, 64, bounds, new Size(info.Width, info.Height), new RectangleF((info.Width - bounds.Width * scale) / 2, y, bounds.Width * scale, bounds.Height * scale), fill, outline, stroke, hint);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
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
