using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class GifFrameData : IDisposable
    {
        public Bitmap Bitmap;
        public int DurationMs;
        public void Dispose()
        {
            if (Bitmap != null)
                Bitmap.Dispose();
        }
    }

    internal sealed class GifImportResult
    {
        public readonly List<string> TplNames = new List<string>();
        public readonly List<float> KeyFrames = new List<float>();
        public readonly List<ushort> TextureIndices = new List<ushort>();
        public ushort TotalFrames;
        public int ExportedFrames;
        public string OutputFolder;
        public string PngOutputFolder;
        public string Prefix;
    }

    internal sealed class GifImportOptions
    {
        public string GifPath;
        public string OutputFolder;
        public string Prefix;
        public int Width;
        public int Height;
        public int ResizeMode;
        public TplPixelFormat Format;
        public int TakeEvery;
        public bool OriginalTiming;
        public int FixedFrames;
        public string TargetMaterialName;
        public byte TextureSlot;
        public int ListMode;
        public bool UpdateBrlan;
        public bool PreserveDuration;
        public bool ExportPng;
        public string PngOutputFolder;
    }

    internal sealed class GifImportForm : Form
    {
        private readonly BrlanDocument _doc;
        private readonly Action _onDocumentChanged;
        private readonly string _defaultOutputFolder;
        private readonly Action<GifImportResult> _onImportCompleted;
        private CueTextBox _gifPath;
        private CueTextBox _outputFolder;
        private CueTextBox _prefix;
        private Label _gifInfo;
        private PictureBox _preview;
        private NumericUpDown _width;
        private NumericUpDown _height;
        private ComboBox _resizeMode;
        private ComboBox _format;
        private NumericUpDown _takeEvery;
        private ComboBox _timingMode;
        private NumericUpDown _fixedFrames;
        private ComboBox _workflow;
        private CueTextBox _brlytPath;
        private ComboBox _baseTexture;
        private Label _bindingInfo;
        private BrlytLayoutMap _layoutMap;
        private bool _updatingLayoutUi;
        // Tracks the last prefix generated from the selected Base TPL. This lets the
        // importer update the prefix when the user switches from one BRLYT layer to
        // another (for example title_bottom -> title_top), while still respecting a
        // prefix the user typed manually.
        private string _lastAutoPrefix = "bg_anim";
        private ComboBox _materialTarget;
        private NumericUpDown _textureSlot;
        private ComboBox _listMode;
        private CheckBox _updateBrlan;
        private CheckBox _preserveDuration;
        private CheckBox _exportPng;
        private ProgressBar _progress;
        private Label _status;
        private Button _import;
        private int _gifFrameCount;
        private int _gifWidth;
        private int _gifHeight;
        private int _gifTotalMs;
        private bool _busy;
        public GifImportForm(BrlanDocument doc, Action onDocumentChanged) : this(doc, onDocumentChanged, null, null)
        {
        }

        public GifImportForm(BrlanDocument doc, Action onDocumentChanged, string defaultOutputFolder, Action<GifImportResult> onImportCompleted)
        {
            _doc = doc;
            _onDocumentChanged = onDocumentChanged;
            _defaultOutputFolder = defaultOutputFolder;
            _onImportCompleted = onImportCompleted;
            Text = L.T("GIF → TPL / RLTP Import", "GIF → TPL / RLTP Import");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(960, 720);
            Size = new Size(1120, 820);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.5F);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            AutoScaleMode = AutoScaleMode.Dpi;
            AllowDrop = true;
            DragEnter += delegate (object sender, DragEventArgs e)
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            DragDrop += delegate (object sender, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && files[0].EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    _gifPath.Text = files[0];
                    if (String.IsNullOrWhiteSpace(_outputFolder.Text))
                        _outputFolder.Text = Path.Combine(Path.GetDirectoryName(files[0]), Path.GetFileNameWithoutExtension(files[0]) + "_tpl");
                    ReadGifInfo(files[0]);
                }
            };
            BuildUi();
            if (!String.IsNullOrWhiteSpace(_defaultOutputFolder))
                _outputFolder.Text = _defaultOutputFolder;
            DarkTheme.Apply(this);
            TryAutoLoadRelatedBrlyt();
            UpdateWorkflowUi();
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (_busy)
                {
                    e.Cancel = true;
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Der GIF-Import läuft noch. Warte bis er abgeschlossen ist.", "The GIF import is still running. Wait until it has finished."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            Label title = new Label();
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Text = L.T("GIF automatisch in Wii-TPLs + RLTP umwandeln", "Automatically convert GIF to Wii TPLs + RLTP");
            root.Controls.Add(title, 0, 0);
            Label intro = new Label();
            intro.AutoSize = true;
            intro.MaximumSize = new Size(950, 0);
            intro.ForeColor = Color.FromArgb(220, 224, 234);
            intro.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            intro.Margin = new Padding(0, 5, 0, 14);
            intro.Text = L.T("Der Importer extrahiert die GIF-Frames, skaliert sie, erzeugt echte TPL-Dateien und kann die geladene BRLAN automatisch mit RLTP-Keyframes ergänzen.", "The importer extracts GIF frames, resizes them, creates real TPL files and can automatically add RLTP keyframes to the loaded BRLAN.");
            root.Controls.Add(intro, 0, 1);
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.BackColor = DarkTheme.Border;
            split.Panel1.BackColor = DarkTheme.Panel2;
            split.Panel2.BackColor = DarkTheme.Panel;
            root.Controls.Add(split, 0, 2);
            Shown += delegate
            {
                try
                {
                    int minLeft = 520;
                    int minRight = 260;
                    int desired = 650;
                    int max = split.Width - minRight - split.SplitterWidth;
                    if (desired > max)
                        desired = max;
                    if (desired < minLeft)
                        desired = minLeft;
                    if (desired > 0 && desired < split.Width - split.SplitterWidth)
                        split.SplitterDistance = desired;
                }
                catch
                {
                }
            };
            TableLayoutPanel form = new TableLayoutPanel();
            form.Dock = DockStyle.Fill;
            form.Padding = new Padding(8);
            form.BackColor = DarkTheme.Panel2;
            form.ColumnCount = 3;
            form.RowCount = 20;
            form.AutoScroll = true;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            split.Panel1.Controls.Add(form);
            int row = 0;
            AddLabel(form, row, L.T("GIF-Datei", "GIF file"));
            _gifPath = NewCue(L.T("Pfad zur .gif-Datei", "Path to .gif file"));
            form.Controls.Add(_gifPath, 1, row);
            Button browseGif = NewButton(L.T("Auswählen...", "Browse..."));
            browseGif.Click += delegate
            {
                BrowseGif();
            };
            form.Controls.Add(browseGif, 2, row++);
            AddLabel(form, row, L.T("GIF-Info", "GIF info"));
            _gifInfo = new Label();
            _gifInfo.AutoSize = true;
            _gifInfo.ForeColor = Color.FromArgb(225, 229, 239);
            _gifInfo.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            _gifInfo.Text = L.T("Noch kein GIF ausgewählt.", "No GIF selected yet.");
            form.SetColumnSpan(_gifInfo, 2);
            form.Controls.Add(_gifInfo, 1, row++);
            AddLabel(form, row, L.T("Workflow", "Workflow"));
            _workflow = NewCombo(new string[] { L.T("Automatisch / BRLYT verwenden wenn gefunden (empfohlen)", "Automatic / use BRLYT when found (recommended)"), L.T("BRLYT-gestützt", "BRLYT-assisted"), L.T("Manuell", "Manual") });
            _workflow.SelectedIndex = 0;
            _workflow.SelectedIndexChanged += delegate
            {
                UpdateWorkflowUi();
            };
            form.Controls.Add(_workflow, 1, row++);
            AddLabel(form, row, L.T("BRLYT-Layout", "BRLYT layout"));
            _brlytPath = NewCue(L.T("Optional: passende .brlyt für automatische Materialzuordnung", "Optional: matching .brlyt for automatic material mapping"));
            form.Controls.Add(_brlytPath, 1, row);
            Button browseBrlyt = NewButton(L.T("Auswählen...", "Browse..."));
            browseBrlyt.Click += delegate
            {
                BrowseBrlyt();
            };
            form.Controls.Add(browseBrlyt, 2, row++);
            AddLabel(form, row, L.T("Basis-TPL", "Base TPL"));
            _baseTexture = new ComboBox();
            _baseTexture.Dock = DockStyle.Fill;
            _baseTexture.DropDownStyle = ComboBoxStyle.DropDownList;
            _baseTexture.BackColor = DarkTheme.Panel;
            _baseTexture.ForeColor = DarkTheme.Fore;
            _baseTexture.FlatStyle = FlatStyle.Flat;
            _baseTexture.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            StyleComboDropDownItems(_baseTexture);
            _baseTexture.Items.Add(L.T("Keine BRLYT-Zuordnung geladen", "No BRLYT mapping loaded"));
            _baseTexture.SelectedIndex = 0;
            _baseTexture.SelectedIndexChanged += delegate
            {
                ApplySelectedTextureBinding();
            };
            form.Controls.Add(_baseTexture, 1, row++);
            _bindingInfo = new Label();
            _bindingInfo.AutoSize = true;
            _bindingInfo.MaximumSize = new Size(720, 0);
            _bindingInfo.ForeColor = Color.FromArgb(190, 198, 216);
            _bindingInfo.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            _bindingInfo.Text = L.T("Tipp: Mit einer BRLYT kann Studio den korrekten Materialnamen und Texture-Slot automatisch bestimmen – praktisch für Title.szs und andere mehrschichtige Layouts.", "Tip: With a BRLYT, Studio can automatically resolve the correct material name and texture slot – useful for Title.szs and other multi-layer layouts.");
            form.SetColumnSpan(_bindingInfo, 2);
            form.Controls.Add(_bindingInfo, 1, row++);
            AddLabel(form, row, L.T("Ausgabeordner", "Output folder"));
            _outputFolder = NewCue(L.T("Ordner für bg_000.tpl, bg_001.tpl, ...", "Folder for bg_000.tpl, bg_001.tpl, ..."));
            form.Controls.Add(_outputFolder, 1, row);
            Button browseOut = NewButton(L.T("Auswählen...", "Browse..."));
            browseOut.Click += delegate
            {
                BrowseOutput();
            };
            form.Controls.Add(browseOut, 2, row++);
            AddLabel(form, row, L.T("Dateiname / Präfix", "Filename / prefix"));
            _prefix = NewCue(L.T("z.B. bg_anim", "e.g. bg_anim"));
            _prefix.Text = "bg_anim";
            _lastAutoPrefix = "bg_anim";
            form.Controls.Add(_prefix, 1, row++);
            AddLabel(form, row, L.T("Zielgrösse", "Target size"));
            FlowLayoutPanel sizeRow = NewFlow();
            _width = NewNumeric(1, 4096, 1024, 90);
            _height = NewNumeric(1, 4096, 512, 90);
            sizeRow.Controls.Add(_width);
            sizeRow.Controls.Add(new Label { Text = "×", AutoSize = true, ForeColor = DarkTheme.Fore, Margin = new Padding(6, 7, 6, 0) });
            sizeRow.Controls.Add(_height);
            Button original = NewButton(L.T("Original", "Original"));
            original.Width = 85;
            original.Click += delegate
            {
                if (_gifWidth > 0 && _gifHeight > 0)
                {
                    _width.Value = Math.Min(_width.Maximum, _gifWidth);
                    _height.Value = Math.Min(_height.Maximum, _gifHeight);
                }
            };
            sizeRow.Controls.Add(original);
            form.Controls.Add(sizeRow, 1, row++);
            AddLabel(form, row, L.T("Skalierung", "Resize mode"));
            _resizeMode = NewCombo(new string[] { L.T("Füllen / Crop (empfohlen)", "Fill / Crop (recommended)"), L.T("Einpassen / transparente Ränder", "Fit / transparent borders"), L.T("Strecken", "Stretch") });
            form.Controls.Add(_resizeMode, 1, row++);
            AddLabel(form, row, L.T("TPL-Format", "TPL format"));
            _format = NewCombo(new string[] { L.T("CMPR (klein, empfohlen für Hintergründe)", "CMPR (small, recommended for backgrounds)"), L.T("RGB5A3 (Alpha, hohe Qualität, grösser)", "RGB5A3 (alpha, high quality, larger)"), L.T("RGB565 (kein Alpha, hohe Qualität, grösser)", "RGB565 (no alpha, high quality, larger)") });
            form.Controls.Add(_format, 1, row++);
            AddLabel(form, row, L.T("Frames reduzieren", "Frame reduction"));
            FlowLayoutPanel everyRow = NewFlow();
            everyRow.Controls.Add(new Label { Text = L.T("Jeden", "Every"), AutoSize = true, ForeColor = DarkTheme.Fore, Margin = new Padding(0, 7, 6, 0) });
            _takeEvery = NewNumeric(1, 100, 1, 65);
            everyRow.Controls.Add(_takeEvery);
            everyRow.Controls.Add(new Label { Text = L.T("Frame übernehmen", "frame"), AutoSize = true, ForeColor = DarkTheme.Fore, Margin = new Padding(6, 7, 0, 0) });
            form.Controls.Add(everyRow, 1, row++);
            AddLabel(form, row, L.T("Timing", "Timing"));
            _timingMode = NewCombo(new string[] { L.T("Original-GIF-Timing (60 FPS)", "Original GIF timing (60 FPS)"), L.T("Feste Frames pro Bild", "Fixed frames per image") });
            _timingMode.SelectedIndexChanged += delegate
            {
                bool editable = _timingMode.SelectedIndex == 1;
                _fixedFrames.ReadOnly = !editable;
                _fixedFrames.BackColor = editable ? DarkTheme.Panel : DarkTheme.Panel3;
                _fixedFrames.ForeColor = editable ? DarkTheme.Fore : Color.FromArgb(214, 219, 232);
            };
            form.Controls.Add(_timingMode, 1, row++);
            AddLabel(form, row, L.T("Frames pro Bild", "Frames per image"));
            _fixedFrames = NewNumeric(1, 10000, 6, 90);
            _fixedFrames.ReadOnly = true;
            _fixedFrames.BackColor = DarkTheme.Panel3;
            _fixedFrames.ForeColor = Color.FromArgb(214, 219, 232);
            form.Controls.Add(_fixedFrames, 1, row++);
            AddLabel(form, row, L.T("Materialziel", "Material target"));
            _materialTarget = new ComboBox();
            _materialTarget.Dock = DockStyle.Fill;
            _materialTarget.DropDownStyle = ComboBoxStyle.DropDown;
            _materialTarget.BackColor = DarkTheme.Panel;
            _materialTarget.ForeColor = DarkTheme.Fore;
            _materialTarget.FlatStyle = FlatStyle.Flat;
            _materialTarget.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            StyleComboDropDownItems(_materialTarget);
            if (_doc != null && _doc.Pai != null)
            {
                int i;
                for (i = 0; i < _doc.Pai.Animations.Count; i++)
                {
                    AnimationModel a = _doc.Pai.Animations[i];
                    if (a.TargetKind == 1 && !String.IsNullOrWhiteSpace(a.Name) && !_materialTarget.Items.Contains(a.Name))
                        _materialTarget.Items.Add(a.Name);
                }

                string suggested = SuggestMaterialTarget();
                if (!String.IsNullOrWhiteSpace(suggested) && !_materialTarget.Items.Contains(suggested))
                    _materialTarget.Items.Insert(0, suggested);
                _materialTarget.Text = suggested;
            }
            else
            {
                _materialTarget.DropDownStyle = ComboBoxStyle.DropDownList;
                _materialTarget.Items.Add(L.T("Keine BRLAN geladen – nur TPL exportieren", "No BRLAN loaded – export TPL only"));
                _materialTarget.SelectedIndex = 0;
            }

            form.Controls.Add(_materialTarget, 1, row++);
            AddLabel(form, row, L.T("Texture-Slot", "Texture slot"));
            _textureSlot = NewNumeric(0, 255, 0, 90);
            _textureSlot.ReadOnly = false;
            _textureSlot.Enabled = true;
            form.Controls.Add(_textureSlot, 1, row++);
            AddLabel(form, row, L.T("TPL-Liste in BRLAN", "TPL list in BRLAN"));
            _listMode = NewCombo(new string[] { L.T("Vorhandene GIF-Einträge dieses Präfixes ersetzen (empfohlen)", "Replace existing GIF entries for this prefix (recommended)"), L.T("Komplette TPL-Liste durch GIF-Frames ersetzen", "Replace entire TPL list with GIF frames"), L.T("Nur fehlende Dateinamen anhängen", "Append missing filenames only") });
            _listMode.SelectedIndex = 0;
            form.Controls.Add(_listMode, 1, row++);
            FlowLayoutPanel options = NewFlow();
            options.AutoSize = true;
            options.WrapContents = true;
            options.BackColor = DarkTheme.Panel2;
            options.Padding = new Padding(2, 4, 2, 4);
            _updateBrlan = NewCheckBox(L.T("Geladene BRLAN automatisch aktualisieren", "Automatically update loaded BRLAN"));
            _updateBrlan.Checked = _doc != null && _doc.Pai != null;
            _updateBrlan.AutoCheck = _doc != null && _doc.Pai != null;
            _preserveDuration = NewCheckBox(L.T("Bestehende BRLAN-Dauer beibehalten und GIF wiederholen (empfohlen)", "Keep existing BRLAN duration and repeat GIF (recommended)"));
            _preserveDuration.Checked = _doc != null && _doc.Pai != null && _doc.Pai.Frames > 0;
            _preserveDuration.AutoCheck = _doc != null && _doc.Pai != null;
            _exportPng = NewCheckBox(L.T("Zusätzlich PNG-Frames exportieren", "Also export PNG frames"));
            options.Controls.Add(_updateBrlan);
            options.Controls.Add(_preserveDuration);
            options.Controls.Add(_exportPng);
            form.SetColumnSpan(options, 3);
            form.Controls.Add(options, 0, row++);
            TableLayoutPanel previewLayout = new TableLayoutPanel();
            previewLayout.Dock = DockStyle.Fill;
            previewLayout.BackColor = DarkTheme.Panel;
            previewLayout.RowCount = 2;
            previewLayout.ColumnCount = 1;
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _preview = new murumsWiiModStudio.ZoomPanPictureBox();
            _preview.Dock = DockStyle.Fill;
            _preview.BackColor = Color.FromArgb(12, 13, 17);
            _preview.SizeMode = PictureBoxSizeMode.Zoom;
            previewLayout.Controls.Add(_preview, 0, 0);
            Label previewHint = new Label();
            previewHint.AutoSize = true;
            previewHint.ForeColor = Color.FromArgb(225, 229, 239);
            previewHint.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            previewHint.Padding = new Padding(6, 9, 6, 5);
            previewHint.Text = L.T("Vorschau des ersten GIF-Frames", "Preview of the first GIF frame");
            previewLayout.Controls.Add(previewHint, 0, 1);
            split.Panel2.Controls.Add(previewLayout);
            _progress = new ProgressBar();
            _progress.Dock = DockStyle.Fill;
            _progress.Height = 22;
            root.Controls.Add(_progress, 0, 3);
            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = DarkTheme.Back;
            bottom.Padding = new Padding(0, 6, 0, 0);
            bottom.ColumnCount = 2;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            _status = new Label();
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.FromArgb(232, 235, 244);
            _status.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            _status.Text = L.T("Bereit. Wähle zuerst ein GIF.", "Ready. Select a GIF first.");
            bottom.Controls.Add(_status, 0, 0);
            _import = NewButton(L.T("Import starten", "Start import"));
            _import.Width = 180;
            _import.Height = 36;
            _import.Click += delegate
            {
                StartImport();
            };
            bottom.Controls.Add(_import, 1, 0);
            root.Controls.Add(bottom, 0, 4);
        }

        private string SuggestMaterialTarget()
        {
            if (_doc == null || _doc.Pai == null)
                return "";
            int i;
            for (i = 0; i < _doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = _doc.Pai.Animations[i];
                if (a.TargetKind == 1 && String.Equals(a.Name, "P_pict", StringComparison.Ordinal))
                    return "P_pict";
            }

            string fileName = "";
            try
            {
                if (!String.IsNullOrWhiteSpace(_doc.SourcePath))
                    fileName = Path.GetFileName(_doc.SourcePath);
            }
            catch
            {
            }

            // Confirmed MenuSingle mapping:
            // bg.tpl is used by material P_pict; line0 is only the pattern/overlay.
            if (!String.IsNullOrEmpty(fileName) && fileName.IndexOf("bg_loop", StringComparison.OrdinalIgnoreCase) >= 0)
                return "P_pict";
            for (i = 0; i < _doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = _doc.Pai.Animations[i];
                if (a.TargetKind == 1 && !String.IsNullOrWhiteSpace(a.Name))
                    return a.Name;
            }

            return "";
        }

        private AnimationModel FindAnimationByName(string name)
        {
            if (_doc == null || _doc.Pai == null || String.IsNullOrEmpty(name))
                return null;
            int i;
            for (i = 0; i < _doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = _doc.Pai.Animations[i];
                if (String.Equals(a.Name, name, StringComparison.Ordinal))
                    return a;
            }

            return null;
        }

        private static bool IsAsciiText(string text)
        {
            if (text == null)
                return true;
            int i;
            for (i = 0; i < text.Length; i++)
                if (text[i] > 0x7F)
                    return false;
            return true;
        }

        private void TryAutoLoadRelatedBrlyt()
        {
            if (_doc == null || String.IsNullOrWhiteSpace(_doc.SourcePath))
                return;
            string nearby = BrlytInspector.FindNearbyLayout(_doc.SourcePath);
            if (String.IsNullOrWhiteSpace(nearby) || !File.Exists(nearby))
                return;
            try
            {
                LoadBrlytMapping(nearby, false);
            }
            catch
            {
            // Auto-detection must never block the GIF importer.
            }
        }

        private void BrowseBrlyt()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "BRLYT (*.brlyt)|*.brlyt|" + L.T("Alle Dateien", "All files") + " (*.*)|*.*";
                if (!String.IsNullOrWhiteSpace(_brlytPath.Text))
                {
                    try
                    {
                        dlg.InitialDirectory = Path.GetDirectoryName(_brlytPath.Text);
                    }
                    catch
                    {
                    }
                }

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                LoadBrlytMapping(dlg.FileName, true);
            }
        }

        private void LoadBrlytMapping(string path, bool showErrors)
        {
            try
            {
                BrlytLayoutMap map = BrlytInspector.Load(path);
                _layoutMap = map;
                _brlytPath.Text = path;
                _updatingLayoutUi = true;
                try
                {
                    _baseTexture.Items.Clear();
                    _baseTexture.Items.Add(L.T("TPL auswählen…", "Select TPL…"));
                    int i;
                    for (i = 0; i < map.Textures.Count; i++)
                    {
                        string texture = map.Textures[i];
                        if (!String.IsNullOrWhiteSpace(texture))
                            _baseTexture.Items.Add(texture);
                    }

                    _baseTexture.SelectedIndex = 0;
                    for (i = 0; i < map.Bindings.Count; i++)
                    {
                        string material = map.Bindings[i].MaterialName;
                        if (!String.IsNullOrWhiteSpace(material) && !_materialTarget.Items.Contains(material))
                            _materialTarget.Items.Add(material);
                    }

                    string suggestedTexture = SuggestBaseTextureFromLayout(map);
                    if (!String.IsNullOrWhiteSpace(suggestedTexture))
                    {
                        int idx = _baseTexture.Items.IndexOf(suggestedTexture);
                        if (idx >= 0)
                            _baseTexture.SelectedIndex = idx;
                    }
                }
                finally
                {
                    _updatingLayoutUi = false;
                }

                if (_baseTexture.SelectedIndex > 0)
                    ApplySelectedTextureBinding();
                else
                    _bindingInfo.Text = L.Format("BRLYT geladen: {0} TPL-Dateien, {1} Bindings. Wähle die Basis-TPL, die du animieren willst.", "BRLYT loaded: {0} TPL files, {1} bindings. Select the base TPL you want to animate.", map.Textures.Count, map.Bindings.Count);
            }
            catch (Exception ex)
            {
                _layoutMap = null;
                if (showErrors)
                    murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("BRLYT konnte nicht gelesen werden", "Could not read BRLYT"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string SuggestBaseTextureFromLayout(BrlytLayoutMap map)
        {
            if (map == null)
                return "";
            string sourceName = "";
            try
            {
                sourceName = _doc == null ? "" : Path.GetFileName(_doc.SourcePath);
            }
            catch
            {
            }

            if (!String.IsNullOrWhiteSpace(sourceName) && sourceName.IndexOf("bg_loop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int i;
                for (i = 0; i < map.Textures.Count; i++)
                    if (String.Equals(map.Textures[i], "bg.tpl", StringComparison.OrdinalIgnoreCase))
                        return map.Textures[i];
            }

            if (!String.IsNullOrWhiteSpace(sourceName) && sourceName.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int i;
                for (i = 0; i < map.Textures.Count; i++)
                    if (String.Equals(map.Textures[i], "tt_title_screen_mario0.tpl", StringComparison.OrdinalIgnoreCase))
                        return map.Textures[i];
            }

            string currentMaterial = _materialTarget == null ? "" : (_materialTarget.Text ?? "").Trim();
            if (!String.IsNullOrWhiteSpace(currentMaterial))
            {
                BrlytTextureBinding existing = map.FindFirstByMaterial(currentMaterial);
                if (existing != null && !String.IsNullOrWhiteSpace(existing.TextureName))
                    return existing.TextureName;
            }

            if (map.Bindings.Count == 1)
                return map.Bindings[0].TextureName;
            return "";
        }

        private void ApplySelectedTextureBinding()
        {
            if (_updatingLayoutUi || _layoutMap == null || _baseTexture == null || _baseTexture.SelectedIndex <= 0)
                return;
            string textureName = _baseTexture.SelectedItem as string;
            if (String.IsNullOrWhiteSpace(textureName))
                return;
            List<BrlytTextureBinding> matches = _layoutMap.FindByTexture(textureName);
            if (matches.Count == 0)
            {
                _bindingInfo.Text = L.T("Für diese TPL wurde in der BRLYT kein Material-Binding gefunden.", "No material binding was found for this TPL in the BRLYT.");
                return;
            }

            BrlytTextureBinding selected = matches[0];
            if (!_materialTarget.Items.Contains(selected.MaterialName))
                _materialTarget.Items.Add(selected.MaterialName);
            _materialTarget.Text = selected.MaterialName;
            if (selected.Slot >= _textureSlot.Minimum && selected.Slot <= _textureSlot.Maximum)
                _textureSlot.Value = selected.Slot;
            if (_prefix != null)
            {
                string currentPrefix = (_prefix.Text ?? "").Trim();
                // If the current value is still the value Studio generated for the
                // previous Base TPL, follow the newly selected layer automatically.
                // A genuinely manual prefix is preserved.
                if (currentPrefix.Length == 0 || String.Equals(currentPrefix, "bg_anim", StringComparison.OrdinalIgnoreCase) || String.Equals(currentPrefix, _lastAutoPrefix ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    string stem = Path.GetFileNameWithoutExtension(textureName);
                    if (!String.IsNullOrWhiteSpace(stem))
                    {
                        string autoPrefix = stem + "_anim";
                        _prefix.Text = autoPrefix;
                        _lastAutoPrefix = autoPrefix;
                    }
                }
            }

            string tplDetails = "";
            string siblingTpl = TplMetadataReader.FindSiblingTpl(_layoutMap.SourcePath, textureName);
            TplMetadata metadata;
            if (!String.IsNullOrWhiteSpace(siblingTpl) && TplMetadataReader.TryRead(siblingTpl, out metadata))
            {
                if (metadata.Width >= _width.Minimum && metadata.Width <= _width.Maximum)
                    _width.Value = metadata.Width;
                if (metadata.Height >= _height.Minimum && metadata.Height <= _height.Maximum)
                    _height.Value = metadata.Height;
                if (metadata.Format == (uint)TplPixelFormat.CMPR)
                    _format.SelectedIndex = 0;
                else if (metadata.Format == (uint)TplPixelFormat.RGB5A3)
                    _format.SelectedIndex = 1;
                else if (metadata.Format == (uint)TplPixelFormat.RGB565)
                    _format.SelectedIndex = 2;
                tplDetails = L.Format(" • Original-TPL: {0}×{1}, Format {2}", " • Original TPL: {0}×{1}, format {2}", metadata.Width, metadata.Height, metadata.Format);
            }

            if (matches.Count == 1)
            {
                _bindingInfo.Text = L.Format("Zuordnung: {0} → Material «{1}», Texture-Slot {2}. Dieses Ziel wird für RLTP verwendet.", "Mapping: {0} → material '{1}', texture slot {2}. This target will be used for RLTP.", textureName, selected.MaterialName, selected.Slot) + tplDetails;
            }
            else
            {
                _bindingInfo.Text = L.Format("{0} wird von {1} Materialien verwendet. Vorausgewählt: «{2}», Slot {3}. Du kannst das Material unten manuell ändern.", "{0} is used by {1} materials. Preselected: '{2}', slot {3}. You can change the material manually below.", textureName, matches.Count, selected.MaterialName, selected.Slot) + tplDetails;
            }
        }

        private void UpdateWorkflowUi()
        {
            if (_workflow == null || _bindingInfo == null)
                return;
            if (_workflow.SelectedIndex == 2)
            {
                _bindingInfo.Text = L.T("Manueller Modus: Materialziel und Texture-Slot werden genau so verwendet, wie du sie unten einträgst.", "Manual mode: the material target and texture slot are used exactly as entered below.");
            }
            else if (_workflow.SelectedIndex == 1 && _layoutMap == null)
            {
                _bindingInfo.Text = L.T("BRLYT-gestützt: Wähle eine passende .brlyt. Danach kannst du eine Basis-TPL wählen und Studio setzt Material + Slot automatisch.", "BRLYT-assisted: choose a matching .brlyt. Then select a base TPL and Studio will set material + slot automatically.");
            }
            else if (_layoutMap != null && _baseTexture != null && _baseTexture.SelectedIndex > 0)
            {
                ApplySelectedTextureBinding();
            }
            else
            {
                _bindingInfo.Text = L.T("Automatik: Studio versucht eine benachbarte BRLYT zu finden. Falls keine gefunden wird, bleibt die normale manuelle Materialauswahl verfügbar.", "Automatic: Studio tries to find a nearby BRLYT. If none is found, the normal manual material selection remains available.");
            }
        }

        private void BrowseGif()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "GIF (*.gif)|*.gif|" + L.T("Alle Dateien", "All files") + " (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                _gifPath.Text = dlg.FileName;
                if (String.IsNullOrWhiteSpace(_outputFolder.Text))
                    _outputFolder.Text = Path.Combine(Path.GetDirectoryName(dlg.FileName), Path.GetFileNameWithoutExtension(dlg.FileName) + "_tpl");
                ReadGifInfo(dlg.FileName);
            }
        }

        private void ReadGifInfo(string path)
        {
            try
            {
                using (Image img = Image.FromFile(path))
                {
                    FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
                    _gifFrameCount = img.GetFrameCount(dim);
                    _gifWidth = img.Width;
                    _gifHeight = img.Height;
                    int[] durations = ReadGifDurations(img, _gifFrameCount);
                    _gifTotalMs = 0;
                    int i;
                    for (i = 0; i < durations.Length; i++)
                        _gifTotalMs += durations[i];
                    img.SelectActiveFrame(dim, 0);
                    Bitmap first = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(first))
                        g.DrawImageUnscaled(img, 0, 0);
                    if (_preview.Image != null)
                        _preview.Image.Dispose();
                    _preview.Image = first;
                }

                _gifInfo.Text = L.Format("{0} × {1} px • {2} Frame(s) • {3:0.00} s", "{0} × {1} px • {2} frame(s) • {3:0.00} s", _gifWidth, _gifHeight, _gifFrameCount, _gifTotalMs / 1000.0);
                _status.Text = L.T("GIF erkannt. Einstellungen prüfen und Import starten.", "GIF detected. Review settings and start the import.");
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, L.T("GIF konnte nicht gelesen werden", "Could not read GIF"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BrowseOutput()
        {
            using (murumsWiiModStudio.FolderPickerDialog dlg = new murumsWiiModStudio.FolderPickerDialog())
            {
                dlg.Description = L.T("Ausgabeordner für die erzeugten TPL-Dateien wählen", "Choose output folder for generated TPL files");
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _outputFolder.Text = dlg.SelectedPath;
            }
        }

        private void StartImport()
        {
            string gif = _gifPath.Text.Trim();
            string output = _outputFolder.Text.Trim();
            string prefix = SanitizePrefix(_prefix.Text.Trim());
            if (!File.Exists(gif))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Bitte zuerst eine gültige GIF-Datei auswählen.", "Please select a valid GIF file first."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (String.IsNullOrWhiteSpace(output))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Bitte einen Ausgabeordner angeben.", "Please choose an output folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (String.IsNullOrWhiteSpace(prefix))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Bitte ein Dateipräfix angeben, z.B. bg_anim.", "Please enter a filename prefix, e.g. bg_anim."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_updateBrlan.Checked && (_doc == null || _doc.Pai == null))
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Für die BRLAN-Aktualisierung muss eine gültige BRLAN geladen sein.", "A valid BRLAN must be loaded to update the BRLAN."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_workflow != null && _workflow.SelectedIndex != 2 && !String.IsNullOrWhiteSpace(_brlytPath.Text))
            {
                string requestedLayout = _brlytPath.Text.Trim();
                if (_layoutMap == null || !String.Equals(_layoutMap.SourcePath, requestedLayout, StringComparison.OrdinalIgnoreCase))
                {
                    LoadBrlytMapping(requestedLayout, true);
                    if (_layoutMap == null)
                        return;
                }
            }

            if (_workflow != null && _workflow.SelectedIndex == 1)
            {
                if (_layoutMap == null)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Im BRLYT-gestützten Modus musst du zuerst eine passende .brlyt auswählen.", "In BRLYT-assisted mode, select a matching .brlyt first."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_baseTexture == null || _baseTexture.SelectedIndex <= 0)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Wähle die Basis-TPL, die animiert werden soll. Studio bestimmt daraus Material und Texture-Slot.", "Select the base TPL to animate. Studio will resolve its material and texture slot."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ApplySelectedTextureBinding();
            }

            string targetMaterial = _materialTarget == null ? "" : (_materialTarget.Text ?? "").Trim();
            if (_updateBrlan.Checked)
            {
                if (String.IsNullOrWhiteSpace(targetMaterial))
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Bitte ein Materialziel angeben. Für den MenuSingle-Hintergrund ist dies P_pict.", "Enter a material target. For the MenuSingle background this is P_pict."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!IsAsciiText(targetMaterial) || Encoding.ASCII.GetByteCount(targetMaterial) > 20)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Der Materialname muss ASCII sein und darf höchstens 20 Bytes lang sein.", "The material name must be ASCII and at most 20 bytes long."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // A BRLAN animation entry is identified by both its name and target kind.
                // It is valid for a pane and a material to share the same name (e.g. title_bottom).
                // RLTP must target the material entry, so do not reject a same-name pane entry here.
                // Safety guard for multi-layer layouts. Reusing a generated filename prefix
                // on another material would intentionally replace the first layer's TPL block
                // in the recommended list mode. Catch that before modifying the document.
                if (_listMode.SelectedIndex == 0 && PrefixIsUsedByAnotherMaterial(prefix, targetMaterial))
                {
                    string suggested = SuggestedPrefixForCurrentBinding();
                    if (!String.IsNullOrWhiteSpace(suggested) && !String.Equals(prefix, suggested, StringComparison.OrdinalIgnoreCase))
                    {
                        _prefix.Text = suggested;
                        _lastAutoPrefix = suggested;
                        prefix = suggested;
                    }
                    else
                    {
                        murumsWiiModStudio.StudioMessageBox.Show(this, L.F("Das Präfix '{0}' wird bereits von einer anderen Material-RLTP-Animation verwendet. Verwende für jeden Layout-Layer ein eigenes Präfix.", "The prefix '{0}' is already used by another material RLTP animation. Use a separate prefix for each layout layer.", prefix), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            int every = Decimal.ToInt32(_takeEvery.Value);
            int estimatedFrames = Math.Max(1, (_gifFrameCount + every - 1) / every);
            if (_updateBrlan.Checked && _doc != null && _doc.Pai != null)
            {
                int projectedTextureCount;
                if (_listMode.SelectedIndex == 1)
                {
                    projectedTextureCount = estimatedFrames;
                }
                else if (_listMode.SelectedIndex == 0)
                {
                    projectedTextureCount = estimatedFrames;
                    int q;
                    for (q = 0; q < _doc.Pai.Textures.Count; q++)
                        if (!IsGeneratedTextureName(_doc.Pai.Textures[q], prefix))
                            projectedTextureCount++;
                }
                else
                {
                    projectedTextureCount = _doc.Pai.Textures.Count;
                    HashSet<string> existingNames = new HashSet<string>(_doc.Pai.Textures, StringComparer.OrdinalIgnoreCase);
                    int q;
                    for (q = 0; q < estimatedFrames; q++)
                    {
                        string generatedName = prefix + "_" + q.ToString("D3", CultureInfo.InvariantCulture) + ".tpl";
                        if (existingNames.Add(generatedName))
                            projectedTextureCount++;
                    }
                }

                if (projectedTextureCount > 65535)
                {
                    murumsWiiModStudio.StudioMessageBox.Show(this, L.T("Zu viele TPL-Einträge für die BRLAN. Reduziere die GIF-Frames.", "Too many TPL entries for the BRLAN. Reduce the GIF frame count."), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            int targetWidth = Decimal.ToInt32(_width.Value);
            int targetHeight = Decimal.ToInt32(_height.Value);
            TplPixelFormat selectedFormat = _format.SelectedIndex == 0 ? TplPixelFormat.CMPR : (_format.SelectedIndex == 2 ? TplPixelFormat.RGB565 : TplPixelFormat.RGB5A3);
            long bytesPerFrame = selectedFormat == TplPixelFormat.CMPR ? (long)((targetWidth + 7) / 8) * ((targetHeight + 7) / 8) * 32L : (long)((targetWidth + 3) / 4) * ((targetHeight + 3) / 4) * 32L;
            long estimatedBytes = bytesPerFrame * estimatedFrames;
            if (estimatedFrames > 120)
            {
                if (murumsWiiModStudio.StudioMessageBox.Show(this, L.Format("Das GIF erzeugt ungefähr {0} TPL-Dateien. Das kann die SZS stark vergrössern. Fortfahren?", "The GIF will create about {0} TPL files. This can make the SZS much larger. Continue?", estimatedFrames), L.T("Viele Frames", "Many frames"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            GifImportOptions options = new GifImportOptions();
            options.GifPath = gif;
            options.OutputFolder = output;
            options.Prefix = prefix;
            options.Width = targetWidth;
            options.Height = targetHeight;
            options.ResizeMode = _resizeMode.SelectedIndex;
            options.Format = selectedFormat;
            options.TakeEvery = every;
            options.OriginalTiming = _timingMode.SelectedIndex == 0;
            options.FixedFrames = Decimal.ToInt32(_fixedFrames.Value);
            options.TargetMaterialName = targetMaterial;
            options.TextureSlot = (byte)_textureSlot.Value;
            options.ListMode = _listMode.SelectedIndex;
            options.UpdateBrlan = _updateBrlan.Checked;
            options.PreserveDuration = _preserveDuration.Checked;
            options.ExportPng = _exportPng.Checked;
            if (options.ExportPng)
            {
                string gifDirectory = Path.GetDirectoryName(options.GifPath);
                if (String.IsNullOrWhiteSpace(gifDirectory))
                    gifDirectory = Environment.CurrentDirectory;
                string gifBaseName = Path.GetFileNameWithoutExtension(options.GifPath);
                if (String.IsNullOrWhiteSpace(gifBaseName))
                    gifBaseName = "gif";
                options.PngOutputFolder = Path.Combine(gifDirectory, gifBaseName + "_frames");
            }

            _busy = true;
            _import.Enabled = false;
            _progress.Value = 0;
            _progress.Maximum = Math.Max(1, estimatedFrames);
            _status.Text = L.Format("Import läuft… geschätzte Rohdaten: {0:0.0} MB", "Importing… estimated raw data: {0:0.0} MB", estimatedBytes / 1024.0 / 1024.0);
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                e.Result = ImportGif(options, worker);
            };
            worker.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
            {
                _progress.Value = Math.Min(_progress.Maximum, Math.Max(_progress.Minimum, e.ProgressPercentage));
            };
            worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                _busy = false;
                _import.Enabled = true;
                if (e.Error != null)
                {
                    _status.Text = L.T("Import fehlgeschlagen.", "Import failed.");
                    murumsWiiModStudio.StudioMessageBox.Show(this, e.Error.Message, L.T("GIF-Import fehlgeschlagen", "GIF import failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                GifImportResult result = (GifImportResult)e.Result;
                _progress.Value = _progress.Maximum;
                _status.Text = L.Format("Fertig: {0} TPL-Dateien erzeugt.", "Done: {0} TPL files created.", result.ExportedFrames);
                if (_onDocumentChanged != null && options.UpdateBrlan)
                    _onDocumentChanged();
                if (_onImportCompleted != null)
                    _onImportCompleted(result);
                string targetLine = options.UpdateBrlan ? "\r\n" + L.T("Materialziel: ", "Material target: ") + options.TargetMaterialName + "\r\n" + L.T("BRLAN-Frames: ", "BRLAN frames: ") + (_doc != null && _doc.Pai != null ? _doc.Pai.Frames.ToString() : "-") : "";
                string pngLine = options.ExportPng && !String.IsNullOrWhiteSpace(result.PngOutputFolder) ? "\r\n" + L.T("PNG-Frames: ", "PNG frames: ") + result.PngOutputFolder : "";
                murumsWiiModStudio.StudioMessageBox.Show(this, L.Format("Import abgeschlossen.\r\n\r\nTPL-Dateien: {0}\r\nTPL-Ausgabe: {1}\r\nBRLAN aktualisiert: {2}", "Import complete.\r\n\r\nTPL files: {0}\r\nTPL output: {1}\r\nBRLAN updated: {2}", result.ExportedFrames, result.OutputFolder, options.UpdateBrlan ? L.T("Ja", "Yes") : L.T("Nein", "No")) + pngLine + targetLine, L.T("GIF-Import fertig", "GIF import complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            worker.RunWorkerAsync();
        }

        private GifImportResult ImportGif(GifImportOptions options, BackgroundWorker worker)
        {
            Directory.CreateDirectory(options.OutputFolder);
            if (options.ExportPng && !String.IsNullOrWhiteSpace(options.PngOutputFolder))
            {
                Directory.CreateDirectory(options.PngOutputFolder);
                // The PNG folder belongs exclusively to this source GIF. Remove stale
                // frame exports so a re-export with fewer frames cannot leave old files behind.
                string[] oldPngFrames = Directory.GetFiles(options.PngOutputFolder, "frame_*.png");
                int oldIndex;
                for (oldIndex = 0; oldIndex < oldPngFrames.Length; oldIndex++)
                {
                    try
                    {
                        File.Delete(oldPngFrames[oldIndex]);
                    }
                    catch
                    {
                    }
                }
            }

            List<GifFrameData> frames = LoadSelectedFrames(options.GifPath, options.TakeEvery);
            try
            {
                GifImportResult result = new GifImportResult();
                result.OutputFolder = options.OutputFolder;
                result.Prefix = options.Prefix;
                result.PngOutputFolder = options.ExportPng ? options.PngOutputFolder : null;
                float timeline = 0.0f;
                int i;
                for (i = 0; i < frames.Count; i++)
                {
                    string name = options.Prefix + "_" + i.ToString("D3", CultureInfo.InvariantCulture) + ".tpl";
                    string tplPath = Path.Combine(options.OutputFolder, name);
                    using (Bitmap resized = ResizeFrame(frames[i].Bitmap, options.Width, options.Height, options.ResizeMode))
                    {
                        TplEncoder.Save(resized, tplPath, options.Format);
                        if (options.ExportPng && !String.IsNullOrWhiteSpace(options.PngOutputFolder))
                            resized.Save(Path.Combine(options.PngOutputFolder, "frame_" + i.ToString("D3", CultureInfo.InvariantCulture) + ".png"), ImageFormat.Png);
                    }

                    result.TplNames.Add(name);
                    result.KeyFrames.Add(timeline);
                    // The final BRLAN texture index is assigned in ApplyToBrlan().
                    // This makes repeat imports idempotent and avoids duplicate-name index drift.
                    result.TextureIndices.Add((ushort)i);
                    float durationFrames = options.OriginalTiming ? Math.Max(1.0f, frames[i].DurationMs * 60.0f / 1000.0f) : options.FixedFrames;
                    timeline += durationFrames;
                    worker.ReportProgress(i + 1);
                }

                result.ExportedFrames = frames.Count;
                if (timeline > 65535.0f)
                    throw new InvalidDataException(L.T("Die GIF-Animation wäre länger als 65535 BRLAN-Frames. Reduziere Frames oder Timing.", "The GIF animation would exceed 65535 BRLAN frames. Reduce frames or timing."));
                result.TotalFrames = (ushort)Math.Max(1, (int)Math.Ceiling(timeline));
                if (options.UpdateBrlan)
                    ApplyToBrlan(result, options);
                WriteManifest(result, options.GifPath, options.OutputFolder, options.Prefix, options.Width, options.Height, options.Format, options.OriginalTiming, options.FixedFrames, options.TakeEvery);
                return result;
            }
            finally
            {
                int i;
                for (i = 0; i < frames.Count; i++)
                    frames[i].Dispose();
            }
        }

        private List<GifFrameData> LoadSelectedFrames(string path, int every)
        {
            List<GifFrameData> selected = new List<GifFrameData>();
            using (Image img = Image.FromFile(path))
            {
                FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
                int count = img.GetFrameCount(dim);
                int[] durations = ReadGifDurations(img, count);
                int start;
                for (start = 0; start < count; start += every)
                {
                    int end = Math.Min(count, start + every);
                    int groupMs = 0;
                    int q;
                    for (q = start; q < end; q++)
                        groupMs += durations[q];
                    img.SelectActiveFrame(dim, start);
                    Bitmap frame = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(img, 0, 0);
                    }

                    selected.Add(new GifFrameData { Bitmap = frame, DurationMs = Math.Max(10, groupMs) });
                }
            }

            return selected;
        }

        private static int[] ReadGifDurations(Image img, int count)
        {
            int[] durations = new int[count];
            int i;
            for (i = 0; i < count; i++)
                durations[i] = 100;
            try
            {
                PropertyItem item = img.GetPropertyItem(0x5100); // FrameDelay, 1/100 second units
                byte[] data = item.Value;
                for (i = 0; i < count && i * 4 + 3 < data.Length; i++)
                {
                    int hundredths = BitConverter.ToInt32(data, i * 4);
                    durations[i] = Math.Max(10, hundredths * 10);
                }
            }
            catch
            {
            }

            return durations;
        }

        private static Bitmap ResizeFrame(Bitmap source, int width, int height, int mode)
        {
            Bitmap target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(target))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                RectangleF dst;
                if (mode == 2) // Stretch
                {
                    dst = new RectangleF(0, 0, width, height);
                }
                else
                {
                    float sx = width / (float)source.Width;
                    float sy = height / (float)source.Height;
                    float scale = mode == 0 ? Math.Max(sx, sy) : Math.Min(sx, sy);
                    float w = source.Width * scale;
                    float h = source.Height * scale;
                    dst = new RectangleF((width - w) / 2.0f, (height - h) / 2.0f, w, h);
                }

                g.DrawImage(source, dst, new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }

            return target;
        }

        private void ApplyToBrlan(GifImportResult result, GifImportOptions options)
        {
            if (_doc == null || _doc.Pai == null)
                return;
            // RLTP animation names are material names from the BRLYT, not filenames.
            // MenuSingle's actual background material is P_pict; line0 is only the
            // moving pattern/overlay. Older Studio builds incorrectly wrote RLTP to
            // whichever material animation already existed (usually line0).
            AnimationModel anim = GetOrCreateMaterialAnimation(options.TargetMaterialName);
            TagModel rltp = null;
            int t;
            for (t = 0; t < anim.Tags.Count; t++)
            {
                if (anim.Tags[t].Magic == "RLTP" && !anim.Tags[t].RawOnly)
                {
                    rltp = anim.Tags[t];
                    break;
                }
            }

            if (rltp == null)
            {
                rltp = new TagModel();
                rltp.Magic = "RLTP";
                rltp.RawOnly = false;
                anim.Tags.Add(rltp);
            }

            byte slot = options.TextureSlot;
            EntryModel entry = null;
            for (t = 0; t < rltp.Entries.Count; t++)
            {
                if (rltp.Entries[t].Index == slot)
                {
                    entry = rltp.Entries[t];
                    break;
                }
            }

            if (entry == null)
            {
                entry = new EntryModel();
                entry.Index = slot;
                rltp.Entries.Add(entry);
            }

            // Keep a copy of the old table so unrelated RLTP entries can be remapped
            // by filename after the TPL table is rebuilt.
            List<string> oldTextures = new List<string>(_doc.Pai.Textures);
            // Remove the specific broken GIF RLTP left by older Studio builds on a
            // different material (for example line0). Only entries that point
            // exclusively to this generated prefix and use the same texture slot are
            // considered ours, so unrelated RLTP animations are left untouched.
            RemoveStaleGifRltpEntries(options.Prefix, slot, anim, oldTextures);
            result.TextureIndices.Clear();
            if (options.ListMode == 1)
            {
                // Explicit destructive mode: the GIF owns the complete texture filename table.
                _doc.Pai.Textures.Clear();
                for (t = 0; t < result.TplNames.Count; t++)
                {
                    _doc.Pai.Textures.Add(result.TplNames[t]);
                    result.TextureIndices.Add((ushort)t);
                }
            }
            else if (options.ListMode == 2)
            {
                // Append only names which are not already present. Re-importing the same GIF
                // therefore does not create a second 000..NNN block.
                for (t = 0; t < result.TplNames.Count; t++)
                {
                    int existing = IndexOfTexture(_doc.Pai.Textures, result.TplNames[t]);
                    if (existing < 0)
                    {
                        existing = _doc.Pai.Textures.Count;
                        _doc.Pai.Textures.Add(result.TplNames[t]);
                    }

                    result.TextureIndices.Add((ushort)existing);
                }
            }
            else
            {
                // Recommended/idempotent mode. Remove every old generated frame for this prefix,
                // including duplicates from older Studio versions, then add one clean sequence.
                List<string> cleaned = new List<string>();
                for (t = 0; t < _doc.Pai.Textures.Count; t++)
                {
                    string name = _doc.Pai.Textures[t];
                    if (!IsGeneratedTextureName(name, options.Prefix))
                        cleaned.Add(name);
                }

                _doc.Pai.Textures.Clear();
                for (t = 0; t < cleaned.Count; t++)
                    _doc.Pai.Textures.Add(cleaned[t]);
                for (t = 0; t < result.TplNames.Count; t++)
                {
                    int index = _doc.Pai.Textures.Count;
                    _doc.Pai.Textures.Add(result.TplNames[t]);
                    result.TextureIndices.Add((ushort)index);
                }
            }

            // Remap every other RLTP entry by filename after the table changed. The selected
            // target entry is rebuilt below, so it does not need remapping.
            RemapOtherRltpEntries(oldTextures, _doc.Pai.Textures, entry);
            entry.Index = slot;
            entry.Target = 0; // RLTP target 0 = Image
            entry.KeyType = 1; // frame(float) + TPL index(UInt16) + padding(UInt16)
            entry.UnknownByte = 0;
            entry.Unknown16 = 0;
            entry.Keys.Clear();
            int targetFrames;
            if (options.PreserveDuration && _doc.Pai.Frames > 0)
                targetFrames = Math.Max((int)_doc.Pai.Frames, (int)result.TotalFrames);
            else
                targetFrames = Math.Max(1, (int)result.TotalFrames);
            if (options.PreserveDuration && targetFrames > result.TotalFrames)
            {
                // Keep the original BRLAN duration (e.g. bg_Loop = 3500 frames) and
                // repeat the GIF throughout it. This preserves existing animations such
                // as line0/RLTS instead of forcing them into the much shorter GIF cycle.
                int cycleFrames = Math.Max(1, (int)result.TotalFrames);
                int cycleStart;
                for (cycleStart = 0; cycleStart < targetFrames; cycleStart += cycleFrames)
                {
                    for (t = 0; t < result.KeyFrames.Count; t++)
                    {
                        float frame = cycleStart + result.KeyFrames[t];
                        if (frame >= targetFrames)
                            break;
                        if (entry.Keys.Count >= 65535)
                            throw new InvalidDataException(L.T("Zu viele RLTP-Keyframes nach dem Wiederholen des GIFs. Verkürze die BRLAN-Dauer oder reduziere GIF-Frames.", "Too many RLTP keyframes after repeating the GIF. Shorten the BRLAN duration or reduce GIF frames."));
                        KeyframeModel k = new KeyframeModel();
                        k.Frame = frame;
                        k.UIntValue = result.TextureIndices[t];
                        k.Padding = 0;
                        entry.Keys.Add(k);
                    }
                }
            }
            else
            {
                for (t = 0; t < result.KeyFrames.Count; t++)
                {
                    KeyframeModel k = new KeyframeModel();
                    k.Frame = result.KeyFrames[t];
                    k.UIntValue = result.TextureIndices[t];
                    k.Padding = 0;
                    entry.Keys.Add(k);
                }
            }

            // GIF imports are intended to loop. In NW4R pai1 this byte is the loop flag.
            _doc.Pai.Flags = 1;
            _doc.Pai.Frames = (ushort)Math.Min(65535, targetFrames);
        }

        private AnimationModel GetOrCreateMaterialAnimation(string materialName)
        {
            string wanted = (materialName ?? "").Trim();
            if (wanted.Length == 0)
                throw new InvalidDataException(L.T("Kein Materialziel für RLTP angegeben.", "No material target specified for RLTP."));
            int i;
            // Name alone is not unique in BRLAN: the same identifier can legally exist
            // once as a pane target (TargetKind 0) and once as a material target
            // (TargetKind 1). Prefer an existing material entry and ignore same-name
            // pane entries. If no material entry exists, create one below.
            for (i = 0; i < _doc.Pai.Animations.Count; i++)
            {
                AnimationModel existing = _doc.Pai.Animations[i];
                if (existing.TargetKind == 1 && String.Equals(existing.Name, wanted, StringComparison.Ordinal))
                    return existing;
            }

            // Proven MenuSingle compatibility path:
            // the stock bg_Loop BRLAN animates "line0", but bg.tpl actually belongs to
            // BRLYT material "P_pict". The working in-game test was produced by retargeting
            // that material animation to P_pict. Do the same automatically and drop the
            // old line0-only tags (RLTS), because after the rename they would otherwise be
            // applied to the background material itself.
            if (String.Equals(wanted, "P_pict", StringComparison.Ordinal) && IsMenuSingleBackgroundBrlan())
            {
                for (i = 0; i < _doc.Pai.Animations.Count; i++)
                {
                    AnimationModel legacy = _doc.Pai.Animations[i];
                    if (legacy.TargetKind == 1 && String.Equals(legacy.Name, "line0", StringComparison.Ordinal))
                    {
                        legacy.Name = "P_pict";
                        legacy.Tags.Clear();
                        return legacy;
                    }
                }
            }

            AnimationModel anim = new AnimationModel();
            anim.Name = wanted;
            anim.TargetKind = 1;
            anim.Unknown16 = 0;
            _doc.Pai.Animations.Add(anim);
            return anim;
        }

        private bool IsMenuSingleBackgroundBrlan()
        {
            if (_doc == null)
                return false;
            try
            {
                string fileName = String.IsNullOrWhiteSpace(_doc.SourcePath) ? "" : Path.GetFileName(_doc.SourcePath);
                return !String.IsNullOrEmpty(fileName) && fileName.IndexOf("bg_loop", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private void RemoveStaleGifRltpEntries(string prefix, byte slot, AnimationModel targetAnimation, List<string> oldTextures)
        {
            if (_doc == null || _doc.Pai == null || oldTextures == null)
                return;
            int a, t, e;
            for (a = _doc.Pai.Animations.Count - 1; a >= 0; a--)
            {
                AnimationModel anim = _doc.Pai.Animations[a];
                if (Object.ReferenceEquals(anim, targetAnimation))
                    continue;
                for (t = anim.Tags.Count - 1; t >= 0; t--)
                {
                    TagModel tag = anim.Tags[t];
                    if (tag.RawOnly || tag.Magic != "RLTP")
                        continue;
                    for (e = tag.Entries.Count - 1; e >= 0; e--)
                    {
                        EntryModel candidate = tag.Entries[e];
                        if (candidate.Index != slot)
                            continue;
                        if (EntryUsesOnlyGeneratedPrefix(candidate, oldTextures, prefix))
                            tag.Entries.RemoveAt(e);
                    }

                    if (tag.Entries.Count == 0)
                        anim.Tags.RemoveAt(t);
                }
            }
        }

        private static bool EntryUsesOnlyGeneratedPrefix(EntryModel entry, List<string> textures, string prefix)
        {
            if (entry == null || entry.KeyType != 1 || entry.Keys.Count == 0 || textures == null)
                return false;
            int i;
            for (i = 0; i < entry.Keys.Count; i++)
            {
                int index = entry.Keys[i].UIntValue;
                if (index < 0 || index >= textures.Count)
                    return false;
                if (!IsGeneratedTextureName(textures[index], prefix))
                    return false;
            }

            return true;
        }

        private void RemapOtherRltpEntries(List<string> oldTextures, List<string> newTextures, EntryModel skipEntry)
        {
            if (_doc == null || _doc.Pai == null || oldTextures == null || newTextures == null)
                return;
            Dictionary<string, ushort> newIndices = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            int i, j, k, q;
            for (i = 0; i < newTextures.Count && i <= 65535; i++)
            {
                string name = newTextures[i] ?? "";
                if (!newIndices.ContainsKey(name))
                    newIndices[name] = (ushort)i;
            }

            for (i = 0; i < _doc.Pai.Animations.Count; i++)
            {
                AnimationModel a = _doc.Pai.Animations[i];
                for (j = 0; j < a.Tags.Count; j++)
                {
                    TagModel tag = a.Tags[j];
                    if (tag.RawOnly || tag.Magic != "RLTP")
                        continue;
                    for (k = 0; k < tag.Entries.Count; k++)
                    {
                        EntryModel e = tag.Entries[k];
                        if (Object.ReferenceEquals(e, skipEntry) || e.KeyType != 1)
                            continue;
                        for (q = 0; q < e.Keys.Count; q++)
                        {
                            KeyframeModel key = e.Keys[q];
                            int oldIndex = key.UIntValue;
                            if (oldIndex < 0 || oldIndex >= oldTextures.Count)
                                continue;
                            string oldName = oldTextures[oldIndex] ?? "";
                            ushort mapped;
                            if (newIndices.TryGetValue(oldName, out mapped))
                                key.UIntValue = mapped;
                        }
                    }
                }
            }
        }

        private static int IndexOfTexture(List<string> textures, string name)
        {
            if (textures == null)
                return -1;
            int i;
            for (i = 0; i < textures.Count; i++)
                if (String.Equals(textures[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private bool PrefixIsUsedByAnotherMaterial(string prefix, string targetMaterial)
        {
            if (_doc == null || _doc.Pai == null || String.IsNullOrWhiteSpace(prefix))
                return false;
            List<string> textures = _doc.Pai.Textures;
            int a, t, e;
            for (a = 0; a < _doc.Pai.Animations.Count; a++)
            {
                AnimationModel anim = _doc.Pai.Animations[a];
                if (anim.TargetKind != 1)
                    continue;
                if (String.Equals(anim.Name, targetMaterial ?? "", StringComparison.Ordinal))
                    continue;
                for (t = 0; t < anim.Tags.Count; t++)
                {
                    TagModel tag = anim.Tags[t];
                    if (tag.RawOnly || tag.Magic != "RLTP")
                        continue;
                    for (e = 0; e < tag.Entries.Count; e++)
                    {
                        if (EntryUsesOnlyGeneratedPrefix(tag.Entries[e], textures, prefix))
                            return true;
                    }
                }
            }

            return false;
        }

        private string SuggestedPrefixForCurrentBinding()
        {
            if (_baseTexture != null && _baseTexture.SelectedIndex > 0)
            {
                string textureName = _baseTexture.SelectedItem as string;
                string stem = Path.GetFileNameWithoutExtension(textureName ?? "");
                if (!String.IsNullOrWhiteSpace(stem))
                    return stem + "_anim";
            }

            string material = _materialTarget == null ? "" : (_materialTarget.Text ?? "").Trim();
            if (!String.IsNullOrWhiteSpace(material))
                return SanitizePrefix(material + "_anim");
            return "";
        }

        private static bool IsGeneratedTextureName(string name, string prefix)
        {
            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(prefix))
                return false;
            string start = prefix + "_";
            if (!name.StartsWith(start, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
                return false;
            int digitStart = start.Length;
            int digitLength = name.Length - digitStart - 4;
            if (digitLength <= 0)
                return false;
            int i;
            for (i = digitStart; i < digitStart + digitLength; i++)
                if (name[i] < '0' || name[i] > '9')
                    return false;
            return true;
        }

        private static void WriteManifest(GifImportResult result, string gifPath, string outputFolder, string prefix, int width, int height, TplPixelFormat format, bool originalTiming, int fixedFrames, int every)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("murums Wii Mod Studio – GIF Import Manifest");
            sb.AppendLine("Source=" + gifPath);
            sb.AppendLine("Frames=" + result.ExportedFrames.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Size=" + width.ToString() + "x" + height.ToString());
            sb.AppendLine("TPLFormat=" + format.ToString());
            sb.AppendLine("TakeEvery=" + every.ToString());
            sb.AppendLine("Timing=" + (originalTiming ? "GIF" : "Fixed:" + fixedFrames.ToString()));
            sb.AppendLine("TotalBRLANFrames=" + result.TotalFrames.ToString());
            sb.AppendLine();
            int i;
            for (i = 0; i < result.TplNames.Count; i++)
                sb.AppendLine(i.ToString("D3") + "  " + result.TplNames[i] + "  frame=" + result.KeyFrames[i].ToString("0.###", CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(outputFolder, prefix + "_manifest.txt"), sb.ToString(), Encoding.UTF8);
        }

        private static string SanitizePrefix(string input)
        {
            if (input == null)
                return "";
            StringBuilder sb = new StringBuilder();
            int i;
            for (i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static void AddLabel(TableLayoutPanel table, int row, string text)
        {
            Label l = new Label();
            l.Text = text + ":";
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Color.FromArgb(245, 247, 252);
            l.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            l.Margin = new Padding(4, 7, 10, 7);
            table.Controls.Add(l, 0, row);
        }

        private static CueTextBox NewCue(string cue)
        {
            CueTextBox b = new CueTextBox();
            b.Dock = DockStyle.Fill;
            b.Cue = cue;
            b.BackColor = DarkTheme.Panel;
            b.ForeColor = DarkTheme.Fore;
            b.BorderStyle = BorderStyle.FixedSingle;
            b.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            b.Margin = new Padding(3, 3, 3, 5);
            return b;
        }

        private static Button NewButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(86, 92, 112);
            b.FlatAppearance.MouseOverBackColor = DarkTheme.Panel3;
            b.FlatAppearance.MouseDownBackColor = DarkTheme.AccentSoft;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            b.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            b.Margin = new Padding(3, 3, 3, 5);
            return b;
        }

        private static ComboBox NewCombo(string[] items)
        {
            ComboBox c = new ComboBox();
            c.Dock = DockStyle.Fill;
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.BackColor = DarkTheme.Panel;
            c.ForeColor = DarkTheme.Fore;
            c.FlatStyle = FlatStyle.Flat;
            c.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            c.Margin = new Padding(3, 3, 3, 5);
            StyleComboDropDownItems(c);
            if (items != null && items.Length > 0)
            {
                c.Items.AddRange(items);
                c.SelectedIndex = 0;
            }

            return c;
        }

        private static void StyleComboDropDownItems(ComboBox c)
        {
            if (c == null)
                return;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = 24;
            c.DrawItem += delegate (object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0)
                    return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color bg = selected ? DarkTheme.Accent2 : DarkTheme.Panel;
                Color fg = DarkTheme.Fore;
                using (SolidBrush b = new SolidBrush(bg))
                    e.Graphics.FillRectangle(b, e.Bounds);
                Rectangle textRect = new Rectangle(e.Bounds.Left + 7, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, c.Items[e.Index].ToString(), c.Font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
        }

        private static CheckBox NewCheckBox(string text)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = DarkTheme.Panel2;
            c.ForeColor = DarkTheme.Fore;
            c.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            c.Margin = new Padding(4, 5, 14, 5);
            c.UseVisualStyleBackColor = false;
            return c;
        }

        private static NumericUpDown NewNumeric(decimal min, decimal max, decimal value, int width)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Min(max, Math.Max(min, value));
            n.Width = width;
            n.BackColor = DarkTheme.Panel;
            n.ForeColor = DarkTheme.Fore;
            n.Font = new Font("Segoe UI", 10.25F, FontStyle.Regular);
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Margin = new Padding(3, 3, 3, 5);
            // NumericUpDown supports typing, but on some Windows themes/focus paths
            // the edit part ended up behaving as spinner-only. Keep keyboard entry
            // explicitly enabled and select the value when tabbing into the field.
            n.ReadOnly = false;
            n.TabStop = true;
            n.InterceptArrowKeys = true;
            EventHandler ensureEditorWritable = delegate
            {
                if (n.ReadOnly)
                    return;
                foreach (Control child in n.Controls)
                {
                    TextBoxBase editor = child as TextBoxBase;
                    if (editor != null)
                    {
                        editor.ReadOnly = false;
                        editor.Cursor = Cursors.IBeam;
                    }
                }
            };
            n.HandleCreated += ensureEditorWritable;
            n.Enter += delegate
            {
                ensureEditorWritable(n, EventArgs.Empty);
                if (!n.ReadOnly)
                    n.Select(0, n.Text.Length);
            };
            return n;
        }

        private static FlowLayoutPanel NewFlow()
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.Dock = DockStyle.Fill;
            f.FlowDirection = FlowDirection.LeftToRight;
            f.WrapContents = false;
            f.AutoSize = true;
            f.BackColor = DarkTheme.Panel2;
            return f;
        }
    }
}
