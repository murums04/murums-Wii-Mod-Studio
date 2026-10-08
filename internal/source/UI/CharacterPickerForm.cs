using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterVariantTile : Button
    {
        internal readonly CharacterVariant Variant;
        internal bool Chosen;
        internal CharacterVariantTile(CharacterVariant variant)
        {
            Variant = variant;
            Text = variant.Name;
            AccessibleName = variant.Name + " — " + variant.Basis;
            AccessibleDescription = variant.HasOwnPortrait ? L.T("Eigenes RR-Minimap-Icon", "Variant's RR minimap icon") : L.T("Basisbild; kein eigenes Variantenbild vorhanden", "Base portrait; no variant portrait available");
            Size = new Size(152, 166);
            Margin = new Padding(5);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float unit = Height / 166f;
            int inset = Math.Max(5, (int)(7 * unit));
            int portraitTop = (int)(10 * unit), portraitSize = (int)(72 * unit);
            bool hover = Enabled && ClientRectangle.Contains(PointToClient(Control.MousePosition));
            g.Clear(Chosen ? DarkTheme.AccentSoft : hover ? DarkTheme.Panel3 : DarkTheme.Panel2);
            using (var pen = new Pen(Chosen ? DarkTheme.Accent : DarkTheme.Border, Chosen ? 3 : 1))
                g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            if (Variant.Portrait != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Min((float)portraitSize / Variant.Portrait.Width, (float)portraitSize / Variant.Portrait.Height);
                int w = (int)(Variant.Portrait.Width * scale), h = (int)(Variant.Portrait.Height * scale);
                g.DrawImage(Variant.Portrait, (Width - w) / 2, portraitTop + (portraitSize - h) / 2, w, h);
            }
            else TextRenderer.DrawText(g, "?", Font, new Rectangle(0, portraitTop, Width, portraitSize), DarkTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Variant.Name, Font, new Rectangle(inset, (int)(86 * unit), Width - 2 * inset, (int)(42 * unit)), DarkTheme.Fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, Variant.Character.Name, Font, new Rectangle(inset, (int)(128 * unit), Width - 2 * inset, (int)(20 * unit)), DarkTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (!Variant.HasOwnPortrait)
                using (var small = new Font(Font.FontFamily, Font.Size * .8f))
                TextRenderer.DrawText(g, L.T("Basisbild", "Base portrait"), small, new Rectangle(inset, (int)(147 * unit), Width - 2 * inset, (int)(16 * unit)), DarkTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(5, 5, Width - 11, Height - 11));
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    }

    internal sealed class CharacterPickerForm : Form
    {
        readonly string pack;
        readonly bool menuPose;
        readonly ComboBox source = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 850 };
        readonly ComboBox basis = new ComboBox { Name = "CharacterBaseFilter", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 260 };
        readonly TextBox search = new TextBox { Name = "CharacterSearch", Dock = DockStyle.Fill };
        readonly FlowLayoutPanel gallery = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(3) };
        readonly PictureBox portrait = new PictureBox { Dock = DockStyle.Top, Height = 152, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(12) };
        readonly Label description = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), UseMnemonic = false };
        readonly Label count = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly Button use = StudioChrome.ActionButton("");
        internal CharacterCatalog Catalog;
        internal CharacterVariant Selected;
        bool changing, transferred;

        internal CharacterPickerForm(string packFolder, string preferredRoot, bool menuPose = false)
        {
            pack = packFolder;
            this.menuPose = menuPose;
            use.Enabled = false;
            use.MinimumSize = new Size(220, 36);
            Text = (menuPose ? L.T("Menüvorlage wählen", "Choose menu template") : L.T("Vorhandenen RR-Charakter ersetzen", "Replace an installed RR character")) + " — murums Wii Mod Studio";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            Size = new Size(1040, 800);
            MinimumSize = new Size(850, 660);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.Controls.Add(StudioChrome.Header(menuPose ? L.T("Welche Menühaltung möchtest du übernehmen?", "Which menu pose would you like to use?") : L.T("Wen möchtest du ersetzen?", "Who would you like to replace?"),
                menuPose ? L.T("Haltung und Menübewegung übernehmen · Fahrhaltungen bleiben erhalten", "Copy pose and menu movement · Driving poses are preserved") : L.T("Vorhandene RR-Variante auswählen • Namen und Bilder aus deinen Dateien", "Choose an installed RR variant • Names and pictures from your files")), 0, 0);
            var sourceBar = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 6, 0, 4) };
            sourceBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sourceBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sourceBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sourceBar.Controls.Add(new Label { Text = L.T("RR-Installation", "RR installation"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            sourceBar.Controls.Add(source, 1, 0);
            var browse = StudioChrome.ActionButton(L.T("RR-Ordner wählen…", "Choose RR folder…"));
            StudioActions.Icon(browse, StudioIcon.Folder);
            sourceBar.Controls.Add(browse, 2, 0);
            browse.Click += delegate {
                using (var dialog = new FolderBrowserDialog { Description = L.T("RetroRewind6 mit Character/Driver auswählen", "Choose RetroRewind6 containing Character/Driver"), ShowNewFolderButton = false })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string root = RetroRewindSource.Resolve(dialog.SelectedPath) ?? dialog.SelectedPath;
                    if (!source.Items.Contains(root)) source.Items.Add(root);
                    source.SelectedItem = root;
                }
            };
            layout.Controls.Add(sourceBar, 0, 1);
            sourceBar.Visible = !menuPose;
            var filters = new TableLayoutPanel { Name = "CharacterFilters", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 5, 0, 5) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            filters.Controls.Add(new Label { Text = L.T("Suchen", "Search"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            filters.Controls.Add(search, 1, 0);
            filters.Controls.Add(new Label { Text = L.T("Basis", "Base character"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(14, 3, 5, 3) }, 2, 0);
            basis.Items.Add(L.T("Alle Charaktere", "All characters"));
            foreach (var character in CharacterDefinition.All.OrderBy(c => c.Name)) basis.Items.Add(character);
            basis.SelectedIndex = 0;
            filters.Controls.Add(basis, 3, 0);
            StudioUx.SetHelp(search, L.T("Varianten nach Name oder Basischarakter filtern.", "Filter variants by name or base character."));
            search.AccessibleName = L.T("Charakter suchen", "Search characters");
            basis.AccessibleName = L.T("Basischarakter filtern", "Filter base character");
            layout.Controls.Add(filters, 0, 2);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 242));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(gallery, 0, 0);
            var detail = new Panel { Name = "CharacterSelectionDetails", Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8), Margin = new Padding(8, 0, 0, 0) };
            detail.Controls.Add(description);
            detail.Controls.Add(portrait);
            detail.SizeChanged += delegate { description.MaximumSize = new Size(Math.Max(160, detail.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - detail.Padding.Horizontal), 0); };
            content.Controls.Add(detail, 1, 0);
            layout.Controls.Add(content, 0, 3);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(count, 0, 0);
            var cancel = StudioChrome.ActionButton(L.T("Abbrechen", "Cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            cancel.MinimumSize = new Size(100, 36);
            footer.Controls.Add(cancel, 1, 0);
            use.Text = menuPose ? L.T("Menüvorlage übernehmen", "Use menu template") : L.T("Diesen Charakter ersetzen", "Replace this character");
            use.Click += delegate { if (Selected != null) { DialogResult = DialogResult.OK; Close(); } };
            footer.Controls.Add(use, 2, 0);
            layout.Controls.Add(footer, 0, 4);
            Controls.Add(layout);
            ParentChanged += delegate {
                if (IsDisposed || Disposing || layout.IsDisposed) return;
                bool embedded = !TopLevel;
                layout.GetControlFromPosition(0, 0).Visible = !embedded;
                layout.RowStyles[0].Height = embedded ? 0 : StudioChrome.HeaderHeight;
            };
            DarkTheme.Apply(this);
            DarkTheme.StylePrimary(use);
            gallery.BackColor = detail.BackColor = DarkTheme.Panel2;
            CancelButton = cancel;
            AcceptButton = use;
            source.SelectedIndexChanged += delegate { if (!changing && source.SelectedItem != null) LoadSource(source.SelectedItem.ToString()); };
            search.TextChanged += delegate { Filter(); };
            basis.SelectedIndexChanged += delegate { Filter(); };
            try
            {
                foreach (string root in RetroRewindSource.Discover().Where(r => Directory.Exists(Path.Combine(r, "Character", "Driver")))) source.Items.Add(root);
            }
            catch (UnauthorizedAccessException)
            {
                count.Text = L.T("Eine gespeicherte Quelle ist nicht zugänglich. Wähle den RR-Ordner direkt.", "A saved source is inaccessible. Choose the RR folder directly.");
            }
            catch (IOException)
            {
                count.Text = L.T("Eine gespeicherte Quelle konnte nicht gelesen werden. Wähle den RR-Ordner direkt.", "A saved source could not be read. Choose the RR folder directly.");
            }
            if (!String.IsNullOrEmpty(preferredRoot) && Directory.Exists(Path.Combine(preferredRoot, "Character", "Driver")))
            {
                if (!source.Items.Contains(preferredRoot)) source.Items.Add(preferredRoot);
                source.SelectedItem = preferredRoot;
            }
            else if (source.Items.Count > 0) source.SelectedIndex = 0;
            else if (String.IsNullOrEmpty(count.Text)) count.Text = L.T("Zuerst deinen RR-Ordner auswählen.", "Choose your RR installation folder first.");
            if (Selected == null) description.Text = menuPose ? L.T("Eine menschliche Vorlage wählen. Haltung und Bewegung danach am eigenen Modell prüfen.", "Choose a human template, then review pose and movement on your own model.") : L.T("Klicke links auf die Variante, die du ersetzen möchtest.\n\nEs werden nur vorhandene RR-Varianten angeboten.", "Select the variant you want to replace.\n\nOnly installed RR variants are offered.");
        }

        internal void LoadSource(string root)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                var next = CharacterCatalog.Load(root, pack);
                portrait.Image = null;
                foreach (Control tile in gallery.Controls.Cast<Control>().ToArray()) tile.Dispose();
                if (Catalog != null) Catalog.Dispose();
                Catalog = next;
                Selected = null; use.Enabled = false;
                foreach (var variant in Catalog.Variants)
                {
                    var tile = new CharacterVariantTile(variant);
                    tile.Click += delegate { SelectVariant(tile.Variant); };
                    gallery.Controls.Add(tile);
                }
                Filter();
                description.Text = L.T("Variante auswählen. Die technische Zuordnung übernimmt Studio.\n\nBasisbilder sind gekennzeichnet, wenn eine Variante kein eigenes Icon enthält.",
                    "Choose a variant. Studio handles the file mapping.\n\nBase portraits are labelled when a variant has no icon of its own.");
            }
            catch (Exception ex)
            {
                changing = true;
                source.SelectedItem = Catalog == null ? null : Catalog.Root;
                changing = false;
                StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK);
            }
            finally { Cursor = Cursors.Default; }
        }

        internal void SelectVariant(CharacterVariant variant)
        {
            Selected = variant;
            portrait.Image = variant.Portrait;
            description.Text = variant.Name + Environment.NewLine + Environment.NewLine + L.T("Basis: ", "Base: ") + variant.Basis
                + Environment.NewLine + Environment.NewLine + (variant.HasOwnPortrait ? L.T("Eigenes RR-Minimap-Icon", "Variant's RR minimap icon") : L.T("Basisbild – kein eigenes Variantenbild vorhanden.", "Base portrait — no variant portrait available."))
                + Environment.NewLine + Environment.NewLine + (menuPose
                    ? L.T("Nur Menü: Haltung und Bewegungen übernehmen. Größe und Gliedmaßenlängen des eigenen Modells bleiben erhalten.", "Menu only: copy pose and movement. Your model keeps its size and limb lengths.")
                    : L.T("Skelett und Gewichtsklasse werden von der Basis übernommen.", "Skeleton and weight class come from the base character."));
            foreach (CharacterVariantTile tile in gallery.Controls) { tile.Chosen = tile.Variant == variant; tile.Invalidate(); }
            use.Enabled = true;
            gallery.ScrollControlIntoView(gallery.Controls.OfType<CharacterVariantTile>().First(t => t.Variant == variant));
        }

        void Filter()
        {
            var character = basis.SelectedItem as CharacterDefinition;
            string query = search.Text.Trim();
            int shown = 0;
            gallery.SuspendLayout();
            foreach (CharacterVariantTile tile in gallery.Controls)
            {
                bool matches = (character == null || tile.Variant.Character == character)
                    && (tile.Variant.Name + " " + tile.Variant.Character.Name).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
                tile.Visible = matches;
                if (matches) shown++;
            }
            gallery.ResumeLayout();
            if (Selected != null && !gallery.Controls.OfType<CharacterVariantTile>().Any(t => t.Variant == Selected && t.Visible))
            {
                Selected = null;
                portrait.Image = null;
                use.Enabled = false;
                description.Text = L.T("Wähle eine Variante aus den Suchergebnissen.", "Choose a variant from the search results.");
                foreach (CharacterVariantTile tile in gallery.Controls) { tile.Chosen = false; tile.Invalidate(); }
            }
            count.Text = shown == 0 ? L.T("Keine passenden Varianten gefunden.", "No matching variants found.") : shown + L.T(" vorhandene Varianten", " installed variants");
        }

        internal CharacterCatalog TakeCatalog() { transferred = true; return Catalog; }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                portrait.Image = null;
                if (!transferred && Catalog != null) Catalog.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
