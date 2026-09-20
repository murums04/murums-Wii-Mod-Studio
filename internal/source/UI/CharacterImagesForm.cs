using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterImagesForm : Form
    {
        internal readonly Dictionary<string, CharacterAsset> Changes = new Dictionary<string, CharacterAsset>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, CharacterAsset> sources = new Dictionary<string, CharacterAsset>(StringComparer.OrdinalIgnoreCase);
        readonly CharacterVariant target;
        readonly Func<string> outputFolder;
        readonly PictureBox map = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly PictureBox emblem = new PictureBox { Width = 160, Height = 96, SizeMode = PictureBoxSizeMode.Zoom };
        readonly ComboBox vehicle = new ComboBox { Name = "Vehicle", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly ComboBox texture = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly EmblemRegionCanvas canvas = new EmblemRegionCanvas { Dock = DockStyle.Fill };
        readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(6), UseMnemonic = false };
        readonly Button stamp;
        Bitmap replacement;
        sealed class VehicleChoice
        {
            internal string Target;
            public override string ToString() { return CharacterVehicleNames.Label(Target); }
        }
        internal CharacterImagesForm(CharacterVariant selected, string rrRoot, IEnumerable<CharacterAsset> current, Func<string> folder)
        {
            target = selected; outputFolder = folder;
            foreach (var asset in current) sources[asset.Target] = asset;
            foreach (string relative in CharacterPackage.Missing(selected.Character, selected.Slot, new CharacterAsset[0]))
            {
                if (!relative.EndsWith(".szs", StringComparison.OrdinalIgnoreCase) || sources.ContainsKey(relative)) continue;
                string path = Path.Combine(rrRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) sources[relative] = CharacterPackage.ReadAsset(path, selected.Character, selected.Slot);
            }
            string mapTarget = MapTarget();
            string mapSource = Path.Combine(rrRoot, mapTarget.Replace('/', Path.DirectorySeparatorChar));
            if (!sources.ContainsKey(mapTarget) && File.Exists(mapSource)) sources[mapTarget] = CharacterPackage.ReadAsset(mapSource, selected.Character, selected.Slot);
            Text = L.T("Charakterbilder & Fahrzeug-Embleme", "Character images & vehicle emblems");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 810); MinimumSize = new Size(940, 690);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 4, ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(StudioChrome.Header(Text, L.T("Bilder für ", "Images for ") + selected.Name + L.T(" • Minimap und Fahrzeug getrennt bearbeiten", " • Edit minimap and vehicle separately")), 0, 0);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var mapTab = new TabPage(L.T("Minimap-Symbol", "Minimap icon"));
            var vehicleTab = new TabPage(L.T("Fahrzeug-Emblem", "Vehicle emblem"));
            tabs.TabPages.AddRange(new[] { mapTab, vehicleTab });
            var mapLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            mapLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mapLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); mapLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var mapBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            Button(mapBar, L.T("Minimap-Bild wählen…", "Choose minimap image…"), ChooseMap);
            Button(mapBar, L.T("Öffnungsstand wiederherstellen", "Restore opened icon"), delegate { Changes.Remove(MapTarget()); RefreshMap(); RefreshStatus(); });
            mapLayout.Controls.Add(mapBar, 0, 0);
            mapLayout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Text = L.T("Nur diese RR-Variante. Studio passt das Bild mit transparentem Rand auf 32 × 32 Pixel an.\nOhne eigenes Symbol zeigt Studio das Basisbild als Orientierung. Das gemeinsame Menübild bleibt unverändert.", "Only this RR variant. Studio fits the image into 32 × 32 pixels with transparent padding.\nWithout a custom icon, Studio shows the base portrait as a reference. The shared menu portrait stays unchanged."), Padding = new Padding(5) }, 0, 1);
            mapLayout.Controls.Add(map, 0, 2); mapTab.Controls.Add(mapLayout);
            var vehicleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(6) };
            vehicleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 275)); vehicleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            vehicleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); vehicleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); vehicleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            vehicleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            vehicleLayout.Controls.Add(new Label { Text = L.T("1 · Fahrzeug wählen", "1 · Choose vehicle"), AutoSize = true }, 0, 0);
            vehicleLayout.Controls.Add(vehicle, 1, 0);
            vehicleLayout.Controls.Add(new Label { Text = L.T("Texturbild", "Texture image"), AutoSize = true }, 0, 1);
            vehicleLayout.Controls.Add(texture, 1, 1);
            var tools = new FlowLayoutPanel { Name = "EmblemControls", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            tools.Controls.Add(new Label { Text = L.T("2 · Neues Emblem wählen", "2 · Choose replacement emblem"), AutoSize = true });
            Button(tools, L.T("Emblem-Bild wählen…", "Choose emblem image…"), ChooseEmblem);
            tools.Controls.Add(emblem);
            tools.Controls.Add(new Label { Text = L.T("3 · Das alte Emblem rechts einrahmen.\n\nEchte Fahrzeugtextur: Die Position ist je Fahrzeug verschieden.", "3 · Mark the old emblem on the right.\n\nActual vehicle texture: the position differs between vehicles."), AutoSize = true, MaximumSize = new Size(225, 0), Margin = new Padding(3, 5, 3, 5) });
            var imageActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            stamp = Button(imageActions, L.T("Emblem einsetzen", "Place emblem"), Stamp);
            stamp.MinimumSize = new Size(170, 35);
            Button(imageActions, L.T("Dieses Fahrzeug zurücksetzen", "Restore this vehicle"), delegate { var choice = vehicle.SelectedItem as VehicleChoice; if (choice != null) { Changes.Remove(choice.Target); LoadVehicle(); RefreshStatus(); } });
            vehicleLayout.Controls.Add(tools, 0, 2); vehicleLayout.Controls.Add(canvas, 1, 2);
            vehicleLayout.Controls.Add(imageActions, 0, 3);
            vehicleLayout.SetColumnSpan(imageActions, 2);
            vehicleTab.Controls.Add(vehicleLayout);
            foreach (var item in sources.Values.Where(a => a.Role == "Race vehicle").OrderBy(a => a.Target)) vehicle.Items.Add(new VehicleChoice { Target = item.Target });
            vehicle.SelectedIndexChanged += delegate { Safe(LoadVehicle); };
            texture.SelectedIndexChanged += delegate { Safe(LoadTexture); };
            canvas.SelectionChanged += delegate { RefreshStamp(); };
            layout.Controls.Add(tabs, 0, 1); layout.Controls.Add(status, 0, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            Button(footer, L.T("Bildkopien speichern", "Save image copies"), SaveCopies);
            Button(footer, L.T("Ins Projekt übernehmen", "Use in project"), delegate { DialogResult = DialogResult.OK; Close(); });
            Button(footer, L.T("Abbrechen", "Cancel"), delegate { DialogResult = DialogResult.Cancel; Close(); });
            layout.Controls.Add(footer, 0, 3); Controls.Add(layout);
            DarkTheme.Apply(this); DarkTheme.StyleTabs(tabs);
            RefreshMap(); if (vehicle.Items.Count > 0) vehicle.SelectedIndex = 0;
            RefreshStatus(); RefreshStamp();
        }
        Button Button(Control parent, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 35, MaximumSize = new Size(255, 0), Margin = new Padding(3) };
            button.Click += delegate { Safe(action); }; parent.Controls.Add(button); return button;
        }
        void Safe(Action action)
        {
            try { action(); }
            catch (Exception error) { StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK); }
        }
        string PickImage()
        {
            using (var dialog = new OpenFileDialog { Filter = L.T("Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.tpl", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tpl") })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }
        string MapTarget() { return "Character/Map/" + target.Character.Code + "-" + target.Slot + ".tpl"; }
        CharacterAsset Current(string path) { return Changes.ContainsKey(path) ? Changes[path] : sources[path]; }
        void ChooseMap()
        {
            string path = PickImage(); if (path == null) return;
            using (var image = TplTextureEditor.LoadSourceBitmap(path)) SetMinimap(image);
        }
        internal void SetMinimap(Bitmap image)
        {
            var asset = CharacterImages.Minimap(image, target.Character, target.Slot);
            Changes[asset.Target] = asset; RefreshMap(); RefreshStatus();
        }
        void RefreshMap()
        {
            if (map.Image != null) map.Image.Dispose();
            map.Image = sources.ContainsKey(MapTarget()) || Changes.ContainsKey(MapTarget()) ? CharacterImages.Decode(Current(MapTarget()).Data) : target.Portrait == null ? null : new Bitmap(target.Portrait);
        }
        void ChooseEmblem()
        {
            string path = PickImage(); if (path == null) return;
            var image = TplTextureEditor.LoadSourceBitmap(path);
            if (replacement != null) replacement.Dispose();
            replacement = image;
            if (emblem.Image != null) emblem.Image.Dispose();
            emblem.Image = new Bitmap(image); RefreshStamp();
        }
        void LoadVehicle()
        {
            texture.Items.Clear();
            var choice = vehicle.SelectedItem as VehicleChoice;
            if (choice != null) foreach (var item in CharacterImages.Textures(Current(choice.Target))) texture.Items.Add(item);
            if (texture.Items.Count > 0) texture.SelectedIndex = 0;
            else canvas.Image = null;
            RefreshStamp();
        }
        void LoadTexture()
        {
            var selected = texture.SelectedItem as CharacterImages.VehicleTexture;
            canvas.Image = selected == null ? null : CharacterImages.Decode(selected.Tpl);
            RefreshStamp();
        }
        void RefreshStamp() { if (stamp != null) stamp.Enabled = replacement != null && texture.SelectedItem != null && canvas.Selection.Width >= 2 && canvas.Selection.Height >= 2; }
        void Stamp()
        {
            var choice = vehicle.SelectedItem as VehicleChoice;
            var selected = texture.SelectedItem as CharacterImages.VehicleTexture;
            if (choice == null || selected == null || replacement == null) return;
            var result = CharacterImages.ReplaceEmblem(Current(choice.Target), selected, canvas.Selection, replacement);
            Changes[result.Target] = result;
            string name = selected.Name;
            LoadVehicle();
            for (int i = 0; i < texture.Items.Count; i++) if (((CharacterImages.VehicleTexture)texture.Items[i]).Name == name) texture.SelectedIndex = i;
            RefreshStatus();
        }
        void RefreshStatus() { status.Text = Changes.Count + L.T(" geänderte Dateien. Übernehmen ergänzt das Projekt; Speichern schreibt nur Kopien nach MUR_EDITED.", " edited files. Use in project adds them to the project; Save writes copies to MUR_EDITED."); }
        internal string SaveCopiesTo(string folder)
        {
            if (Changes.Count == 0) throw new InvalidOperationException(L.T("Zuerst ein Bild ändern.", "Change an image first."));
            return CharacterReplacementExport.Write(folder, Changes.ToDictionary(p => p.Key, p => p.Value.Data));
        }

        void SaveCopies()
        {
            string destination = SaveCopiesTo(outputFolder());
            StudioMessageBox.Show(this, L.T("Bildkopien gespeichert:\n", "Image copies saved:\n") + destination + L.T("\n\nAlle Dateien aus diesem Ordner in den Custom-Pack-Hauptordner kopieren und gleichnamige Dateien ersetzen. Die Änderungen werden auch ins Projekt übernommen.", "\n\nCopy all files from this folder into your custom pack root and replace matching filenames. Changes are also added to this project."), Text, MessageBoxButtons.OK);
            DialogResult = DialogResult.OK; Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (map.Image != null) map.Image.Dispose(); if (emblem.Image != null) emblem.Image.Dispose(); if (replacement != null) replacement.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class EmblemRegionCanvas : Control
    {
        Bitmap image;
        Point start;
        bool dragging;
        internal Rectangle Selection { get; private set; }
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