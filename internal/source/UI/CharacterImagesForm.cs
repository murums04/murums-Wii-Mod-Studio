using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterImagesForm : Form
    {
        internal readonly Dictionary<string, CharacterAsset> Changes = new Dictionary<string, CharacterAsset>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, CharacterAsset> sources = new Dictionary<string, CharacterAsset>(StringComparer.OrdinalIgnoreCase);
        readonly EditHistory<Dictionary<string, CharacterAsset>> history = new EditHistory<Dictionary<string, CharacterAsset>>();
        readonly StudioUndoRedo historyActions;
        internal Func<bool> ProjectCanUndo, ProjectCanRedo;
        internal Action ProjectUndo, ProjectRedo;
        readonly CharacterVariant target;
        internal event EventHandler ChangesChanged;
        readonly Func<string> outputFolder;
        readonly ComboBox vehicle = new ComboBox { Name = "Vehicle", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly ComboBox scope = new ComboBox { Name = "StyleScope", DropDownStyle = ComboBoxStyle.DropDownList, Width = 248 };
        readonly ComboBox texture = new ComboBox { Name = "StyleTexture", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 540 };
        readonly EmblemRegionCanvas canvas = new EmblemRegionCanvas { Dock = DockStyle.Fill };
        readonly CharacterModelViewport preview = new CharacterModelViewport(false) { Name = "StylePreview", ShowDimensions = false };
        readonly CheckBox showCharacter = new CheckBox { Name = "StyleShowCharacter", Text = L.T("Charakter anzeigen", "Show character"), AutoSize = true, Checked = true, Margin = new Padding(8) };
        readonly CharacterModelImport characterModel;
        readonly string sourceRoot;
        CharacterModelImport sceneCharacter, sceneVehicle;
        string previewDriverFolder, previewVisualFolder;
        int sceneContext;
        string characterPreviewError;
        readonly PictureBox map = new PictureBox { Width = 48, Height = 48, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(60, 60, 68) };
        readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(6), UseMnemonic = false };
        readonly ToolTip help = new ToolTip();
        readonly Label logoHelp = new Label { AutoSize = true, MaximumSize = new Size(250, 0), UseMnemonic = false };
        readonly DarkTabControl views = new DarkTabControl { Dock = DockStyle.Fill };
        readonly Button paintButton, logoButton, markButton, windowButton, applyWindowsButton, tireButton, applyTiresButton;
        readonly Label tireHelp = new Label { AutoSize = true, MaximumSize = new Size(250, 0), UseMnemonic = false };
        readonly Dictionary<string, bool> tireSupport = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Color tireColour = Color.White;
        readonly Label windowHelp = new Label { AutoSize = true, MaximumSize = new Size(250, 0), UseMnemonic = false };
        readonly Dictionary<string, bool> windowSupport = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Color windowColour = Color.LightSkyBlue;
        Color paint = Color.FromArgb(150, 45, 185);
        Bitmap replacement;
        bool loading;
        sealed class VehicleChoice
        {
            internal string Target;
            public override string ToString() { return CharacterVehicleNames.ShortLabel(Path.GetFileName(Target).Split('-')[0]); }
        }
        sealed class TextureChoice
        {
            internal CharacterImages.VehicleTexture Texture;
            public override string ToString() { return Texture.Name; }
        }
        internal CharacterImagesForm(CharacterVariant selected, string rrRoot, IEnumerable<CharacterAsset> current, Func<string> folder)
            : this(selected, rrRoot, current, folder, null) { }
        internal CharacterImagesForm(CharacterVariant selected, string rrRoot, IEnumerable<CharacterAsset> current, Func<string> folder, CharacterModelImport model, bool embedded = false)
        {
            target = selected; outputFolder = folder; characterModel = model; sourceRoot = rrRoot;
            foreach (var asset in current) sources[asset.Target] = asset;
            var required = CharacterPackage.Missing(selected.Character, selected.Slot, new CharacterAsset[0]);
            required.Add(MapTarget());
            required.Add(MenuTarget(false)); required.Add(MenuTarget(true));
            foreach (string relative in required)
            {
                string path = Path.Combine(rrRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!sources.ContainsKey(relative) && File.Exists(path)) sources[relative] = CharacterPackage.ReadAsset(path, selected.Character, selected.Slot);
            }
            Text = L.T("RR-MKWii Logo & Farben Tool", "RR-MKWii Logo & Colours Tool");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1160, 820); MinimumSize = new Size(960, 700);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 5, ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (!embedded) layout.Controls.Add(StudioChrome.Header(Text, selected.Name + L.T(" · Vorschau prüfen · Ins Projekt übernehmen", " · Review preview · Use in project")), 0, 0);
            else layout.RowStyles[0].Height = 0;
            var selector = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(4) };
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            selector.Controls.Add(new Label { Text = L.T("Fahrzeug", "Vehicle"), AutoSize = true, Margin = new Padding(4, 7, 12, 4) });
            selector.Controls.Add(vehicle);
            selector.Controls.Add(new Label { Text = L.T("Anwenden auf", "Apply to"), AutoSize = true, Margin = new Padding(12, 7, 8, 4) });
            scope.Items.AddRange(new object[] { L.T("Gewähltes Fahrzeug", "Selected vehicle"), L.T("Alle Fahrzeuge dieses Charakters", "All vehicles for this character") });
            scope.SelectedIndex = 0;
            scope.Dock = DockStyle.Fill;
            selector.Controls.Add(scope);
            layout.Controls.Add(selector, 0, 1);
            var body = new TableLayoutPanel { Name = "CharacterStyleWorkspace", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
            var logoTools = tools;
            var toolTabs = new DarkTabControl { Name = "StyleTools", Dock = DockStyle.Fill };
            var logoTab = new TabPage(L.T("Logo", "Logo"));
            var paintTab = new TabPage(L.T("Farbe", "Colour"));
            var tireTab = new TabPage(L.T("Reifen", "Tires"));
            var windowTab = new TabPage(L.T("Scheiben", "Windows"));
            logoTab.Controls.Add(logoTools);
            toolTabs.TabPages.AddRange(new[] { logoTab, paintTab, windowTab, tireTab });
            tools.Controls.Add(new Label { Text = L.T("Minimap & Emblem", "Minimap & emblem"), Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 8, 3, 10) });
            var logoChoice = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            logoChoice.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logoChoice.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logoChoice.Controls.Add(map, 0, 0);
            var chooseLogo = Button(logoChoice, L.T("Logo laden…", "Load logo…"), ChooseLogo);
            help.SetToolTip(chooseLogo, L.T("Logobild wählen. Wird als Minimap-Symbol gespeichert (32 × 32).", "Choose a logo image. Saved as the minimap icon (32 × 32)."));
            tools.Controls.Add(logoChoice);
            logoButton = Button(tools, L.T("Logo auf Fahrzeuge anwenden", "Apply logo to vehicles"), delegate { ApplyLogo(null); });
            tools.Controls.Add(logoHelp);
            tools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
            paintTab.Controls.Add(tools);
            tools.Controls.Add(new Label { Text = L.T("Fahrzeuglack", "Vehicle paint"), Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 8, 3, 10) });
            paintButton = Button(tools, L.T("Lackfarbe wählen…", "Choose paint colour…"), ChooseColor);
            var palette = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(250, 0) };
            var modelPalette = CharacterVehicleStyle.Palette(model);
            if (modelPalette.Length > 0) paint = modelPalette[0];
            foreach (Color color in modelPalette)
            {
                Color choice = color;
                var swatch = new Button { Width = 32, Height = 28, BackColor = color, FlatStyle = FlatStyle.Flat, AccessibleName = ColorTranslator.ToHtml(color), UseVisualStyleBackColor = false };
                swatch.Click += delegate { paint = choice; RefreshPaint(); };
                palette.Controls.Add(swatch);
            }
            if (palette.Controls.Count > 0)
            {
                palette.AccessibleName = L.T("Farben aus deinem Modell", "Colours from your model");
                help.SetToolTip(palette, palette.AccessibleName);
                tools.Controls.Add(palette);
            }
            var applyPaint = Button(tools, L.T("Farbe anwenden", "Apply colour"), ApplyPaint);
            help.SetToolTip(applyPaint, L.T("Nur Lackflächen; Metall, Reifen und erkannte Logos bleiben erhalten.", "Painted areas only; metal, tires and recognised logos are preserved."));
            var windowTools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
            windowTab.Controls.Add(windowTools);
            windowTools.Controls.Add(new Label { Text = L.T("Scheiben", "Windows"), Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 8, 3, 10) });
            windowButton = Button(windowTools, L.T("Scheibenfarbe wählen…", "Choose window colour…"), ChooseWindowColour);
            applyWindowsButton = Button(windowTools, L.T("Scheibenfarbe anwenden", "Apply window colour"), ApplyWindows);
            windowTools.Controls.Add(windowHelp);
            help.SetToolTip(applyWindowsButton, L.T("Färbt erkannte Scheiben im Menü und Rennen. Transparenz bleibt erhalten.", "Colours recognised windows in menus and races. Transparency is preserved."));
            var tireTools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
            tireTab.Controls.Add(tireTools);
            tireTools.Controls.Add(new Label { Text = L.T("Reifen", "Tires"), Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 8, 3, 10) });
            tireButton = Button(tireTools, L.T("Reifenfarbe wählen…", "Choose tire colour…"), ChooseTireColour);
            applyTiresButton = Button(tireTools, L.T("Reifenfarbe anwenden", "Apply tire colour"), ApplyTires);
            tireTools.Controls.Add(tireHelp);
            body.Controls.Add(toolTabs, 1, 0);
            var modelTab = new TabPage(L.T("3D-Vorschau", "3D preview"));
            var textureTab = new TabPage(L.T("Texturen", "Textures"));
            modelTab.Controls.Add(new CharacterPreviewWorkspace(preview));
            showCharacter.Dock = DockStyle.Top;
            modelTab.Controls.Add(showCharacter);
            showCharacter.CheckedChanged += delegate { ApplyPreviewScene(); };
            var textureLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            textureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); textureLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); textureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textureLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            texture.Margin = new Padding(4);
            textureLayout.Controls.Add(texture, 0, 0); textureLayout.Controls.Add(canvas, 0, 1);
            var textureActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            markButton = Button(textureActions, L.T("Logo in Markierung einsetzen", "Place logo in selection"), delegate { ApplyLogo(canvas.Selection); });
            StudioActions.Icon(Button(textureActions, L.T("PNG speichern…", "Save PNG…"), SaveTexture), StudioIcon.SaveAs);
            textureLayout.Controls.Add(textureActions, 0, 2); textureTab.Controls.Add(textureLayout);
            views.TabPages.AddRange(new[] { modelTab, textureTab }); body.Controls.Add(views, 0, 0);
            layout.Controls.Add(body, 0, 2); layout.Controls.Add(status, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var accept = Button(footer, L.T("Ins Projekt übernehmen", "Use in project"), delegate { DialogResult = DialogResult.OK; Close(); });
            Button(footer, L.T("Dateien exportieren…", "Export files…"), SaveCopies);
            var cancel = Button(footer, L.T("Abbrechen", "Cancel"), delegate { DialogResult = DialogResult.Cancel; Close(); }); CancelButton = cancel;
            history.Reset(new Dictionary<string, CharacterAsset>(Changes, StringComparer.OrdinalIgnoreCase));
            historyActions = new StudioUndoRedo(this, footer,
                () => ProjectCanUndo == null ? history.CanUndo : ProjectCanUndo(),
                () => ProjectCanRedo == null ? history.CanRedo : ProjectCanRedo(),
                () => { if (ProjectUndo == null) Undo(); else ProjectUndo(); },
                () => { if (ProjectRedo == null) Redo(); else ProjectRedo(); });
            layout.Controls.Add(footer, 0, 4); Controls.Add(layout);
            foreach (var asset in sources.Values.Where(a => a.Role == "Race vehicle").OrderBy(a => a.Target)) vehicle.Items.Add(new VehicleChoice { Target = asset.Target });
            vehicle.SelectedIndexChanged += delegate { Safe(RefreshVehicle); };
            texture.SelectedIndexChanged += delegate { Safe(LoadTexture); };
            canvas.SelectionChanged += delegate { RefreshButtons(); };
            Shown += delegate { Safe(RefreshPreview); };
            DarkTheme.Apply(this); DarkTheme.StyleTabs(views); DarkTheme.StyleTabs(toolTabs); toolTabs.Padding = new Point(7, 5); accept.BackColor = DarkTheme.Accent; accept.ForeColor = Color.White; accept.UseVisualStyleBackColor = false;
            foreach (Control swatch in palette.Controls) { swatch.BackColor = ColorTranslator.FromHtml(swatch.AccessibleName); ((Button)swatch).UseVisualStyleBackColor = false; }
            if (embedded)
            {
                TopLevel = false;
                FormBorderStyle = FormBorderStyle.None;
                MinimumSize = Size.Empty;
                Dock = DockStyle.Fill;
                layout.Padding = new Padding(4, 2, 4, 2);
                selector.Padding = Padding.Empty;
                foreach (Control action in footer.Controls) action.Visible = action == historyActions.Panel;
                var feedback = new TableLayoutPanel { Name = "CharacterStyleFeedback", AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
                feedback.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                feedback.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                feedback.Controls.Add(status, 0, 0);
                feedback.Controls.Add(historyActions.Panel, 1, 0);
                historyActions.Panel.Visible = false;
                feedback.SizeChanged += delegate { status.MaximumSize = new Size(Math.Max(180, feedback.ClientSize.Width - 16), 0); };
                layout.Controls.Remove(footer);
                footer.Dispose();
                layout.Controls.Add(feedback, 0, 3);
                layout.RowStyles[4].SizeType = SizeType.Absolute;
                layout.RowStyles[4].Height = 0;
            }
            foreach (var propertyPage in new[] { logoTab, paintTab, windowTab, tireTab })
            {
                foreach (Button propertyAction in propertyPage.Controls[0].Controls.OfType<Button>())
                {
                    propertyAction.AutoSize = false;
                    propertyAction.MinimumSize = new Size(0, 34);
                    propertyAction.Size = new Size(244, 36);
                    propertyAction.AutoEllipsis = true;
                    StudioUx.SetHelp(propertyAction, propertyAction.Text);
                }
            }
            scope.SelectedIndexChanged += delegate { Safe(RefreshButtons); };
            ColourButton.SetColor(windowButton, windowColour);
            ColourButton.SetColor(tireButton, tireColour);
            RefreshMap(); RefreshPaint();
            if (vehicle.Items.Count > 0) vehicle.SelectedIndex = 0;
            RefreshStatus();
        }
        Button Button(Control parent, string text, Action action)
        {
            var button = StudioChrome.ActionButton(text);
            button.Click += delegate { Safe(action); }; parent.Controls.Add(button); return button;
        }
        void Safe(Action action)
        {
            try { action(); }
            catch (Exception error) { StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK); }
        }
        string MapTarget() { return "Character/Map/" + target.Character.Code + "-" + target.Slot + ".tpl"; }
        string MenuTarget(bool battle) { return "Character/AllKart/" + target.Character.Code + "-" + target.Slot + "-allkart" + (battle ? "_BT" : "") + ".szs"; }
        CharacterAsset Current(string path) { return Changes.ContainsKey(path) ? Changes[path] : sources[path]; }
        CharacterAsset SelectedVehicle { get { var item = vehicle.SelectedItem as VehicleChoice; return item == null ? null : Current(item.Target); } }
        void Commit(Dictionary<string, CharacterAsset> changed)
        {
            foreach (var item in changed) Changes[item.Key] = item.Value;
            // Die Stiloperationen erzeugen neue Assets und ändern ihre Eingaben nicht.
            history.Record(new Dictionary<string, CharacterAsset>(Changes, StringComparer.OrdinalIgnoreCase));
            if (ChangesChanged != null) ChangesChanged(this, EventArgs.Empty);
            RefreshMap(); RefreshVehicle(); RefreshStatus();
        }
        void Undo()
        {
            if (!history.CanUndo) return;
            RestoreChanges(history.Undo());
        }
        void Redo()
        {
            if (!history.CanRedo) return;
            RestoreChanges(history.Redo());
        }
        void RestoreChanges(Dictionary<string, CharacterAsset> snapshot)
        {
            Changes.Clear(); foreach (var item in snapshot) Changes.Add(item.Key, item.Value);
            if (ChangesChanged != null) ChangesChanged(this, EventArgs.Empty);
            RefreshMap(true); RefreshVehicle(); RefreshStatus();
        }
        void ChooseLogo()
        {
            using (var dialog = new OpenFileDialog { Filter = L.T("Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.tpl", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tpl") })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    using (var image = TplTextureEditor.LoadSourceBitmap(dialog.FileName)) SetMinimap(image);
        }
        internal void SetMinimap(Bitmap image)
        {
            var asset = CharacterImages.Minimap(image, target.Character, target.Slot);
            Commit(new Dictionary<string, CharacterAsset> { { asset.Target, asset } });
            if (replacement != null) replacement.Dispose(); replacement = new Bitmap(image);
            RefreshButtons();
        }
        void RefreshMap(bool resetLogo = false)
        {
            if (map.Image != null) map.Image.Dispose();
            if (resetLogo && replacement != null) { replacement.Dispose(); replacement = null; }
            if (sources.ContainsKey(MapTarget()) || Changes.ContainsKey(MapTarget()))
            {
                map.Image = CharacterImages.Decode(Current(MapTarget()).Data); if (replacement == null) replacement = new Bitmap(map.Image);
            }
            else map.Image = target.Portrait == null ? null : new Bitmap(target.Portrait);
        }
        void ChooseColor()
        {
            using (var dialog = new ColorDialog { Color = paint, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) { paint = dialog.Color; RefreshPaint(); }
        }
        void ChooseWindowColour()
        {
            using (var dialog = new ColorDialog { Color = windowColour, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    windowColour = dialog.Color;
                    ColourButton.SetColor(windowButton, windowColour);
                }
        }
        bool HasWindows(CharacterAsset asset)
        {
            if (asset == null) return false;
            bool supported;
            if (!windowSupport.TryGetValue(asset.Target, out supported))
                windowSupport[asset.Target] = supported = CharacterVehicleWindows.Available(asset);
            return supported;
        }
        internal void ApplyWindows()
        {
            var pending = new Dictionary<string, CharacterAsset>();
            foreach (var source in ScopeVehicles().Where(HasWindows))
                AddVehicleChange(pending, source, CharacterVehicleWindows.Paint(source, windowColour));
            if (pending.Count > 0) Commit(pending);
        }
        void ChooseTireColour()
        {
            using (var dialog = new ColorDialog { Color = tireColour, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    tireColour = dialog.Color;
                    ColourButton.SetColor(tireButton, tireColour);
                }
        }
        bool HasTires(CharacterAsset asset)
        {
            if (asset == null) return false;
            bool supported;
            if (!tireSupport.TryGetValue(asset.Target, out supported))
                tireSupport[asset.Target] = supported = CharacterVehicleTires.Available(asset);
            return supported;
        }
        internal void ApplyTires()
        {
            var pending = new Dictionary<string, CharacterAsset>();
            foreach (var source in ScopeVehicles().Where(HasTires))
                AddVehicleChange(pending, source, CharacterVehicleTires.Paint(source, tireColour));
            if (pending.Count > 0) Commit(pending);
        }
        void RefreshPaint() { ColourButton.SetColor(paintButton, paint); }
        IEnumerable<CharacterAsset> ScopeVehicles()
        {
            return scope.SelectedIndex == 1 ? vehicle.Items.Cast<VehicleChoice>().Select(v => Current(v.Target))
                : SelectedVehicle == null ? new CharacterAsset[0] : new[] { SelectedVehicle };
        }
        void AddVehicleChange(Dictionary<string, CharacterAsset> pending, CharacterAsset before, CharacterAsset after)
        {
            pending[after.Target] = after;
            foreach (string path in new[] { MenuTarget(false), MenuTarget(true) })
            {
                if (!sources.ContainsKey(path)) { if (path == MenuTarget(false)) throw new InvalidDataException(L.T("AllKart-Menüarchiv fehlt. RR-Installation vervollständigen.", "AllKart menu archive is missing. Complete the RR installation.")); continue; }
                var menu = pending.ContainsKey(path) ? pending[path] : Current(path);
                if (path == MenuTarget(true) && !CharacterImages.Members(menu).Keys.Any(k => Path.GetFileNameWithoutExtension(k) == CharacterVehicleStyle.Key(after))) continue;
                pending[path] = CharacterVehicleStyle.SyncMenu(menu, before, after);
            }
        }
        internal void ApplyPaint()
        {
            var pending = new Dictionary<string, CharacterAsset>();
            foreach (var source in ScopeVehicles())
            {
                var regions = CharacterVehicleStyle.LogoRegions(CharacterVehicleStyle.Key(source), CharacterVehicleStyle.Body(source));
                AddVehicleChange(pending, source, CharacterVehicleStyle.Paint(source, paint, regions, sources[source.Target]));
            }
            if (pending.Count > 0) Commit(pending);
        }
        internal void ApplyLogo(Rectangle? manual)
        {
            if (replacement == null) throw new InvalidOperationException(L.T("Zuerst ein Logobild wählen.", "Choose a logo image first."));
            var pending = new Dictionary<string, CharacterAsset>();
            var skipped = new List<string>();
            var choice = texture.SelectedItem as TextureChoice;
            foreach (var source in manual.HasValue ? new[] { SelectedVehicle } : ScopeVehicles())
            {
                if (source == null) continue;
                CharacterAsset changed;
                if (!manual.HasValue && source.LogoRegions != null && source.LogoRegions.Length > 0)
                {
                    changed = source;
                    foreach (var group in source.LogoRegions.GroupBy(r => r.Texture))
                        changed = CharacterVehicleStyle.Logo(changed, replacement, group.Select(r => r.Bounds).ToArray(), group.Key);
                }
                else
                {
                    var regions = manual.HasValue ? new[] { manual.Value } : CharacterVehicleStyle.LogoRegions(CharacterVehicleStyle.Key(source), CharacterVehicleStyle.Body(source));
                    if (regions.Length == 0) { skipped.Add(CharacterVehicleNames.ShortLabel(CharacterVehicleStyle.Key(source))); continue; }
                    changed = CharacterVehicleStyle.Logo(source, replacement, regions, manual.HasValue && choice != null && choice.Texture != null ? choice.Texture.Name : null);
                }
                AddVehicleChange(pending, source, changed);
            }
            if (pending.Count > 0) Commit(pending);
            if (skipped.Count > 0)
            {
                views.SelectedIndex = 1;
                status.Text = L.T("Logo manuell markieren für: ", "Mark the logo manually for: ") + String.Join(", ", skipped);
            }
        }
        void RefreshVehicle()
        {
            LoadTextures();
            var source = SelectedVehicle;
            int count = source == null ? 0 : source.LogoRegions != null && source.LogoRegions.Length > 0 ? source.LogoRegions.Length : CharacterVehicleStyle.LogoRegions(CharacterVehicleStyle.Key(source), CharacterVehicleStyle.Body(source)).Length;
            logoHelp.Text = count == 0 ? L.T("Logostelle unter Texturen einrahmen und einsetzen.", "Mark the logo under Textures, then place it.")
                : count + L.T(" Logostellen erkannt. Unter Texturen anpassbar.", " logo areas found. Adjust under Textures.");
            windowColour = HasWindows(source) ? CharacterVehicleWindows.Colour(source) ?? Color.LightSkyBlue : Color.LightSkyBlue;
            ColourButton.SetColor(windowButton, windowColour);
            tireColour = HasTires(source) ? CharacterVehicleTires.Colour(source) ?? Color.White : Color.White;
            ColourButton.SetColor(tireButton, tireColour);
            RefreshButtons();
            if (Visible) RefreshPreview();
        }
        void LoadTextures()
        {
            loading = true;
            string selected = texture.SelectedItem == null ? null : texture.SelectedItem.ToString();
            texture.Items.Clear();
            try
            {
                if (SelectedVehicle != null)
                    foreach (var item in CharacterImages.Textures(SelectedVehicle, "kart_model.brres"))
                        texture.Items.Add(new TextureChoice { Texture = item });
                if (texture.Items.Count > 0)
                {
                    var retained = texture.Items.Cast<TextureChoice>().FirstOrDefault(c => c.ToString() == selected);
                    texture.SelectedItem = retained ?? texture.Items[0];
                }
            }
            finally { loading = false; }
            LoadTexture();
        }
        void LoadTexture()
        {
            if (loading) return;
            var item = texture.SelectedItem as TextureChoice;
            canvas.Image = item == null ? null : CharacterImages.Decode(item.Texture.Tpl);
            canvas.SuggestedRegions = item != null && item.Texture != null && SelectedVehicle != null
                && item.Texture.Name == CharacterVehicleStyle.Body(SelectedVehicle).Name
                ? CharacterVehicleStyle.LogoRegions(CharacterVehicleStyle.Key(SelectedVehicle), item.Texture) : new Rectangle[0];
            if (SelectedVehicle != null && item != null && item.Texture != null && SelectedVehicle.LogoRegions != null)
            {
                var regions = SelectedVehicle.LogoRegions.Where(r => r.Texture == item.Texture.Name).Select(r => r.Bounds).ToArray();
                if (regions.Length > 0) canvas.SuggestedRegions = regions;
            }
            RefreshButtons();
        }
        internal static CharacterModelImport BuildPreview(CharacterAsset vehicleAsset, CharacterAsset menu, CancellationToken token, bool gameUnits = false)
        {
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                var members = CharacterImages.Members(menu);
                string member = members.Keys.SingleOrDefault(k => Path.GetFileNameWithoutExtension(k) == CharacterVehicleStyle.Key(vehicleAsset));
                if (member == null) throw new InvalidDataException("Menu vehicle missing.");
                string source = Path.Combine(work, "vehicle.brres"), dae = Path.Combine(work, "vehicle.dae");
                File.WriteAllBytes(source, members[member]);
                StudioModelLibrary.Call("ExportModel", source, "menu", dae);
                token.ThrowIfCancellationRequested();
                if (gameUnits)
                {
                    // Gemeinsame Spielkoordinaten statt der metrischen DAE-Vorschau verwenden.
                    var document = new System.Xml.XmlDocument { XmlResolver = null };
                    document.Load(dae);
                    var unit = (System.Xml.XmlElement)document.SelectSingleNode("//*[local-name()='asset']/*[local-name()='unit']");
                    if (unit != null) unit.SetAttribute("meter", "1");
                    document.Save(dae);
                }
                return CharacterModelImport.LoadVisual(dae, token);
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
        }
        void RefreshPreview()
        {
            ClearPreview();
            var selected = SelectedVehicle;
            if (selected == null || !sources.ContainsKey(MenuTarget(false))) return;
            sceneVehicle = ModelOperationForm.Run(this, L.T("Charakter und Fahrzeug laden", "Load character and vehicle"), token => {
                string key = CharacterVehicleStyle.Key(selected);
                var geometry = VehiclePoseGeometry.Load(sourceRoot, target.Character.Code, target.Slot, key,
                    sources.Keys.Select(Current), token);
                try {
                    previewDriverFolder = ModelRuntime.NewWorkFolder();
                    string driver = Path.Combine(previewDriverFolder, "driver_model.brres");
                    var member = CharacterImages.Members(selected).Single(p => Path.GetFileName(p.Key) == "driver_model.brres");
                    File.WriteAllBytes(driver, member.Value);
                    var reference = RigPoseReference.Load(driver, "model", "drive", 16, characterModel == null);
                    reference.VehicleGeometry = geometry;
                    if (characterModel == null) {
                        sceneCharacter = reference.Visual;
                        previewVisualFolder = Path.GetDirectoryName(reference.Visual.Source);
                        sceneContext = 0;
                    }
                    else if (characterModel.Rig != null && characterModel.Rig.JointGuides != null) {
                        var serializer = ModelRig.Serializer();
                        var rig = serializer.Deserialize<ModelRig>(serializer.Serialize(characterModel.Rig));
                        rig.Folder = characterModel.Rig.Folder; rig.Reference = characterModel.Rig.Reference;
                        rig.SelectVehiclePose(key, reference);
                        rig.InitializeGamePose(2, reference);
                        sceneCharacter = rig.Preview(); sceneContext = 2;
                    }
                    else if (characterModel.Rig != null) {
                        var serializer = ModelRig.Serializer();
                        var rig = serializer.Deserialize<ModelRig>(serializer.Serialize(characterModel.Rig));
                        rig.Folder = characterModel.Rig.Folder; rig.Reference = characterModel.Rig.Reference;
                        rig.Points = rig.Points.Select(p => p.Select(v => v * characterModel.FitScale).ToArray()).ToArray();
                        sceneCharacter = rig.Preview(); sceneContext = 0;
                    }
                    else {
                        sceneCharacter = new CharacterModelImport { Source = characterModel.Source };
                        sceneCharacter.Points.AddRange(characterModel.Points.Select(p => p.Select(v => v * characterModel.FitScale).ToArray()));
                        sceneCharacter.Faces.AddRange(characterModel.Faces);
                        sceneCharacter.FaceColors.AddRange(characterModel.FaceColors);
                        sceneContext = 0;
                    }
                    token.ThrowIfCancellationRequested();
                    return geometry.Model;
                }
                catch (OperationCanceledException) {
                    if (geometry.Model.Rig != null) ModelRuntime.DeleteWorkFolder(geometry.Model.Rig.Folder);
                    throw;
                }
                catch (Exception error) {
                    sceneCharacter = null;
                    characterPreviewError = error.Message;
                    if (previewDriverFolder != null) ModelRuntime.DeleteWorkFolder(previewDriverFolder);
                    if (previewVisualFolder != null) ModelRuntime.DeleteWorkFolder(previewVisualFolder);
                    previewDriverFolder = previewVisualFolder = null;
                    return geometry.Model;
                }
            });
            if (sceneVehicle == null) { ClearPreview(); showCharacter.Enabled = false; return; }
            ApplyPreviewScene();
        }
        void ApplyPreviewScene()
        {
            bool paired = showCharacter.Checked && sceneCharacter != null;
            preview.GameContext = paired ? sceneContext : 0;
            preview.VehicleModel = paired ? sceneVehicle : null;
            preview.Model = paired ? sceneCharacter : sceneVehicle;
            preview.ModelScale = 1;
            showCharacter.Enabled = sceneCharacter != null;
            string hint = sceneCharacter != null ? L.T("Charakter zusammen mit dem Fahrzeug anzeigen.", "Show the character together with the vehicle.")
                : L.T("Fahrzeugvorschau verfügbar. Der Charakter konnte nicht geladen werden.", "Vehicle preview is available. The character could not be loaded.");
            help.SetToolTip(showCharacter, hint);
            showCharacter.AccessibleDescription = hint;
            if (characterPreviewError != null) status.Text = hint;
        }
        void ClearPreview()
        {
            preview.VehicleModel = null; preview.Model = null;
            if (sceneVehicle != null && sceneVehicle.Rig != null) ModelRuntime.DeleteWorkFolder(sceneVehicle.Rig.Folder);
            if (previewDriverFolder != null) ModelRuntime.DeleteWorkFolder(previewDriverFolder);
            if (previewVisualFolder != null) ModelRuntime.DeleteWorkFolder(previewVisualFolder);
            sceneVehicle = sceneCharacter = null;
            previewDriverFolder = previewVisualFolder = null;
            characterPreviewError = null;
        }
        void RefreshButtons()
        {
            if (tireButton != null)
            {
                int count = ScopeVehicles().Count(HasTires);
                tireButton.Enabled = applyTiresButton.Enabled = count > 0;
                tireHelp.Text = count == 0 ? L.T("Keine separat bearbeitbaren Reifen vorhanden.", "No separately editable tires available.")
                    : L.T("Färbt das Reifengummi; Profil und Felgen bleiben erhalten. Weiß setzt die Tönung zurück.", "Tints the rubber; preserves tread and rims. White resets the tint.");
            }
            if (windowButton != null)
            {
                int count = ScopeVehicles().Count(HasWindows);
                windowButton.Enabled = applyWindowsButton.Enabled = count > 0;
                windowHelp.Text = count == 0 ? L.T("Keine separat bearbeitbare Scheibe vorhanden.", "No separately editable window available.")
                    : L.T("Scheibenfarbe für ", "Window colour for ") + count + (count == 1 ? L.T(" Fahrzeug. Transparenz bleibt erhalten.", " vehicle. Transparency is preserved.") : L.T(" Fahrzeuge. Transparenz bleibt erhalten.", " vehicles. Transparency is preserved."));
            }
            if (logoButton != null) logoButton.Enabled = replacement != null && SelectedVehicle != null;
            if (markButton != null) markButton.Enabled = replacement != null && texture.SelectedItem != null && canvas.Selection.Width >= 2 && canvas.Selection.Height >= 2;
        }
        void RefreshStatus()
        {
            status.Text = Changes.Count + L.T(" geänderte Dateien · Menü + Rennen · Originale bleiben erhalten.", " changed files · Menu + race · Originals are preserved.");
            if (historyActions != null) historyActions.Refresh();
            RefreshButtons();
        }
        void SaveTexture()
        {
            if (canvas.Image == null) return;
            using (var dialog = new SaveFileDialog { Filter = "PNG|*.png", FileName = "texture.png" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    canvas.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    StudioMessageBox.ShowPath(this, dialog.FileName, L.T("Textur gespeichert.", "Texture saved."), Text);
                }
        }
        internal string SaveCopiesTo(string folder)
        {
            if (String.IsNullOrEmpty(folder)) return null;
            if (Changes.Count == 0) throw new InvalidOperationException(L.T("Zuerst Logo oder Farbe ändern.", "Change a logo or colour first."));
            return CharacterReplacementExport.Write(folder, Changes.ToDictionary(p => p.Key, p => p.Value.Data));
        }
        void SaveCopies()
        {
            string destination = SaveCopiesTo(outputFolder());
            if (destination == null) return;
            StudioMessageBox.ShowPath(this, destination, L.T("Kopien gespeichert.", "Copies saved.") + L.T("\nAlle Dateien daraus in den Custom-Pack-Hauptordner kopieren. Die Änderungen werden auch ins Projekt übernommen.", "\nCopy all these files into the custom pack root. Changes are also added to this project."), Text);
            DialogResult = DialogResult.OK; Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { help.Dispose(); ClearPreview(); if (map.Image != null) map.Image.Dispose(); if (replacement != null) replacement.Dispose(); }
            base.Dispose(disposing);
        }
    }
    internal sealed class EmblemRegionCanvas : Control
    {
        Bitmap image;
        Point start;
        bool dragging;
        internal Rectangle Selection { get; private set; }
        internal Rectangle[] SuggestedRegions = new Rectangle[0];
        internal void SelectRegion(Rectangle region) { Selection = region; if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); Invalidate(); }
        internal event EventHandler SelectionChanged;
        internal Bitmap Image { get { return image; } set { if (image != null) image.Dispose(); image = value; Selection = Rectangle.Empty; Invalidate(); } }
        internal EmblemRegionCanvas() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); Cursor = Cursors.Cross; }
        RectangleF BoundsForImage()
        {
            if (image == null) return RectangleF.Empty;
            float scale = Math.Min((Width - 20f) / image.Width, (Height - 20f) / image.Height);
            return new RectangleF((Width - image.Width * scale) / 2, (Height - image.Height * scale) / 2, image.Width * scale, image.Height * scale);
        }
        Point Pixel(Point point)
        {
            var box = BoundsForImage();
            return new Point((int)Math.Max(0, Math.Min(image.Width, (point.X - box.Left) * image.Width / Math.Max(1, box.Width))), (int)Math.Max(0, Math.Min(image.Height, (point.Y - box.Top) * image.Height / Math.Max(1, box.Height))));
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (image != null && e.Button == MouseButtons.Left && BoundsForImage().Contains(e.Location)) { start = Pixel(e.Location); dragging = true; Capture = true; }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                var point = Pixel(e.Location);
                Selection = Rectangle.FromLTRB(Math.Min(start.X, point.X), Math.Min(start.Y, point.Y), Math.Max(start.X, point.X), Math.Max(start.Y, point.Y));
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                Invalidate();
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = false; Capture = false; } base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) dragging = false;
            base.OnMouseCaptureChanged(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(DarkTheme.Panel2);
            if (image == null) { TextRenderer.DrawText(e.Graphics, L.T("Fahrzeugtextur wählen", "Choose a vehicle texture"), Font, ClientRectangle, DarkTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
            var box = BoundsForImage(); e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(image, box);
            using (var guide = new Pen(Color.FromArgb(160, 150, 95, 255), 2))
                foreach (var r in SuggestedRegions)
                    e.Graphics.DrawRectangle(guide, box.X + r.X * box.Width / image.Width, box.Y + r.Y * box.Height / image.Height,
                        r.Width * box.Width / image.Width, r.Height * box.Height / image.Height);
            if (!Selection.IsEmpty)
            {
                var region = new RectangleF(box.X + Selection.X * box.Width / image.Width, box.Y + Selection.Y * box.Height / image.Height, Selection.Width * box.Width / image.Width, Selection.Height * box.Height / image.Height);
                using (var pen = new Pen(Color.Cyan, 2)) e.Graphics.DrawRectangle(pen, region.X, region.Y, region.Width, region.Height);
                TextRenderer.DrawText(e.Graphics, Selection.Width + " × " + Selection.Height, Font, new Point((int)region.X, (int)region.Bottom + 3), Color.Cyan);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing && image != null) image.Dispose(); base.Dispose(disposing); }
    }
}
