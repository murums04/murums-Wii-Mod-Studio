using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class RaceEffectsForm : StudioToolForm
    {
        sealed class Item
        {
            internal StudioArchiveCopy Archive;
            internal string Path;
            internal ParticleEffect Effect;
            internal string Category;
            public override string ToString() { return Effect.Name; }
        }
        readonly EffectCategory[] categories = EffectCategories.Create();
        readonly List<StudioArchiveCopy> archives = new List<StudioArchiveCopy>();
        readonly List<Item> items = new List<Item>();
        readonly HashSet<Item> checkedEffects = new HashSet<Item>();
        readonly PictureBox sample = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        bool filling;
        readonly Label sampleCaption = new Label { Dock = DockStyle.Fill };
        readonly Dictionary<string, Bitmap> textureCache = new Dictionary<string, Bitmap>();
        readonly CheckedListBox list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true };
        readonly ComboBox group = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Name = "EffectCategory", Width = 260, DropDownWidth = 330, MaxDropDownItems = 16, IntegralHeight = false, Margin = new Padding(8, 10, 8, 3) };
        readonly Label details = new Label { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(4), AutoEllipsis = true };
        readonly CheckBox fixedColors = new CheckBox { Checked = true, AutoSize = true, Text = L.T("Gewählte Farben festhalten", "Keep selected colours fixed") };
        readonly Button first, second, save, apply;
        readonly Label selection = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        Color primaryColor = Color.White, secondaryColor = Color.White;
        bool dirty;
        internal RaceEffectsForm() : base("RR-MKWii Race Effects Tool",
            L.T("1. RR-Archive laden • 2. Effekte auswählen • 3. Farben als Kopie speichern",
                "1. Load RR archives • 2. Select effects • 3. Save colours as copies"))
        {
            Action(L.T("Archiv hinzufügen…", "Add archive…"), L.T("Common.szs und CommonAssets.szs aus deinem RR-Pack auswählen.", "Choose Common.szs and CommonAssets.szs from your RR pack."), Add).Name = "PackSourceAction";
            Action(L.T("Auswahl leeren", "Clear selection"), "", Clear);
            group.Items.AddRange(categories);
            group.SelectedIndex = 0;
            Actions.Controls.Add(group);
            group.SelectedIndexChanged += delegate { RefreshList(); };
            Action(L.T("Gruppe anhaken", "Check group"), L.T("Hakt alle Effekte dieser Kategorie an. Bereits angehakte andere Gruppen bleiben ausgewählt.",
                "Check every effect in this category. Other checked groups remain selected."), delegate { SetVisibleChecked(true); }).Name = "CheckEffectGroup";
            Action(L.T("Gruppe abwählen", "Uncheck group"), L.T("Entfernt nur die Haken der sichtbaren Kategorie.",
                "Uncheck only the visible category."), delegate { SetVisibleChecked(false); }).Name = "UncheckEffectGroup";
            Action(L.T("Alle abwählen", "Uncheck all"), L.T("Entfernt die Haken in allen Kategorien.", "Uncheck every category."), delegate {
                checkedEffects.Clear();
                for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, false);
                UpdateSelection();
            });
            first = Action(L.T("Hauptfarbe…", "Primary colour…"), "", delegate { Choose(true); });
            second = Action(L.T("Nebenfarbe…", "Secondary colour…"), "", delegate { Choose(false); });
            Actions.Controls.Add(fixedColors);
            StudioUx.SetHelp(fixedColors, L.T(
                "Nur RGB-Farbwechsel stoppen. Transparenz, Bewegung und Größenanimationen bleiben erhalten. Ohne Haken können Original-Farbspuren die Auswahl übersteuern.",
                "Stop RGB colour cycles only. Keep alpha, movement and size animations. Unchecked, original colour tracks may override your choice."));
            apply = ExportAction(L.T("Farben anwenden", "Apply colours"), "", Apply, false);
            ExportAction(L.T("Zurücksetzen", "Reset changes"), "", ResetChanges, false);
            save = ExportAction(L.T("Effektkopien speichern…", "Save effect copies…"), "", Save);
            var view = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            view.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            view.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            view.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            view.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            view.Controls.Add(selection, 0, 0);
            view.Controls.Add(sampleCaption, 1, 0);
            view.Controls.Add(list, 0, 1);
            var preview = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            preview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var colours = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0) };
            colours.Controls.Add(first);
            colours.Controls.Add(second);
            colours.Controls.Add(fixedColors);
            colours.SetFlowBreak(second, true);
            preview.Controls.Add(colours, 0, 0);
            preview.Controls.Add(sample, 0, 1);
            view.Controls.Add(preview, 1, 1);
            Body.Controls.Add(view);
            Body.Controls.Add(details);
            list.SelectedIndexChanged += delegate {
                var item = list.SelectedItem as Item;
                if (item != null)
                {
                    var document = new ParticleEffects(item.Archive.Files[item.Path].Data);
                    var effect = document.Items.First(e => e.Name == item.Effect.Name);
                    primaryColor = document.ColorAt(effect, 0); secondaryColor = document.ColorAt(effect, 1);
                    UpdateColors();
                }
                else Detail();
            };
            list.ItemCheck += delegate(object sender, ItemCheckEventArgs e)
            {
                if (filling) return;
                var item = (Item)list.Items[e.Index];
                if (e.NewValue == CheckState.Checked) checkedEffects.Add(item); else checkedEffects.Remove(item);
                UpdateSelection();
            };
            Status.Text = L.T("RR-Quellen laden. Streckeneigene Effekte können globale Farben übersteuern.",
                "Load RR sources. Track-specific effects can override global colours.");
            Finish();
            PackSelection.SourceStep(this, L.T("Archiv hinzufügen…", "Add archive…"),
                L.T("Common.szs: Standardeffekte und Stern.\nCommonAssets.szs: zusätzliche RR-Driftstufen.\nBeide aus deinem RR-Pack hinzufügen und gewünschte Effekte anhaken.",
                    "Common.szs: standard effects and star.\nCommonAssets.szs: additional RR drift stages.\nAdd both from your RR pack and check the effects to change."), false);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (dirty && !Discard()) e.Cancel = true; };
            UpdateColors();
            UpdateSelection();
        }
        bool Discard()
        {
            return StudioMessageBox.Show(this, L.T("Ungespeicherte Effektänderungen verwerfen?", "Discard unsaved effect changes?"),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        void Clear()
        {
            if (dirty && !Discard()) return;
            foreach (var texture in textureCache.Values) if (texture != null) texture.Dispose();
            textureCache.Clear();
            archives.Clear(); items.Clear(); checkedEffects.Clear(); list.Items.Clear(); dirty = false;
            UpdateSelection(); PackSelection.SourceCleared(this); Detail();
        }
        void Add()
        {
            using (var picker = new OpenFileDialog { Multiselect = true, Filter = "RR effect archives|Common.szs;CommonAssets.szs",
                InitialDirectory = PackSelection.Folder(this) })
                if (ToolArchiveFilters.Show(picker, this) == DialogResult.OK) LoadArchives(ToolArchiveFilters.SelectedFiles(picker));
        }
        internal void LoadArchives(IEnumerable<string> paths)
        {
            var next = new List<StudioArchiveCopy>();
            var nextItems = new List<Item>();
            foreach (string path in paths)
            {
                if (archives.Any(a => a.Source.Equals(System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))) continue;
                if (archives.Concat(next).Any(a => System.IO.Path.GetFileName(a.Source).Equals(System.IO.Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("An archive with this filename is already loaded. Clear selection to change sources.");
                var archive = new StudioArchiveCopy(path);
                ReadEffects(archive, nextItems);
                next.Add(archive);
            }
            archives.AddRange(next); items.AddRange(nextItems); RefreshList();
            if (archives.Count > 0) PackSelection.SourceLoaded(this);
            Status.Text = String.Join(", ", archives.Select(a => System.IO.Path.GetFileName(a.Source)));
        }
        void ReadEffects(StudioArchiveCopy archive, List<Item> destination)
        {
            int count = destination.Count;
            foreach (var entry in archive.Files.Where(f => f.Key.EndsWith(".breff", StringComparison.OrdinalIgnoreCase)))
            {
                var document = new ParticleEffects(entry.Value.Data);
                foreach (var effect in document.Items)
                    destination.Add(new Item { Archive = archive, Path = entry.Key, Effect = effect, Category = EffectCategories.Classify(effect.Name, categories) });
            }
            if (destination.Count == count)
                throw new InvalidDataException("No supported effects: " + System.IO.Path.GetFileName(archive.Source));
        }
        bool Matches(Item item)
        {
            var category = group.SelectedItem as EffectCategory;
            return category == null || category.Id == "all" || item.Category == category.Id;
        }
        void SetVisibleChecked(bool value)
        {
            for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, value);
        }
        void RefreshList()
        {
            filling = true; list.BeginUpdate(); list.Items.Clear();
            foreach (var item in items.Where(Matches)) list.Items.Add(item, checkedEffects.Contains(item));
            list.EndUpdate(); filling = false;
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            Detail();
            UpdateSelection();
        }
        void UpdateSelection()
        {
            int hidden = checkedEffects.Count(item => !Matches(item));
            selection.Text = checkedEffects.Count + L.T(" angehakt", " checked")
                + (hidden == 0 ? "" : " (" + hidden + L.T(" ausgeblendet)", " hidden)"))
                + L.T(" • sichtbar: ", " • visible: ") + list.Items.Count;
            StudioUx.SetHelp(selection, L.T("Farben anwenden ändert alle angehakten Effekte, auch in anderen Kategorien. Unter Alle Effekte siehst du die gesamte Auswahl.",
                "Apply colours changes all checked effects, including other categories. All effects shows the full selection."));
            apply.Enabled = checkedEffects.Count > 0;
        }
        void Choose(bool firstColor)
        {
            using (var dialog = new ColorDialog { FullOpen = true, Color = firstColor ? primaryColor : secondaryColor })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (firstColor) primaryColor = dialog.Color; else secondaryColor = dialog.Color;
                    UpdateColors();
                }
        }
        void UpdateColors()
        {
            ColourButton.SetColor(first, primaryColor);
            ColourButton.SetColor(second, secondaryColor);
            first.Text = L.T("Hauptfarbe: ", "Primary: ") + ColorTranslator.ToHtml(primaryColor);
            second.Text = L.T("Nebenfarbe: ", "Secondary: ") + ColorTranslator.ToHtml(secondaryColor);
            Detail();
        }
        void Detail()
        {
            var item = list.SelectedItem as Item;
            if (item == null)
            {
                details.Text = ""; save.Enabled = dirty;
                if (sample.Image != null) { sample.Image.Dispose(); sample.Image = null; }
                return;
            }
            var document = new ParticleEffects(item.Archive.Files[item.Path].Data);
            var effect = document.Items.First(e => e.Name == item.Effect.Name);
            details.Text = System.IO.Path.GetFileName(item.Archive.Source) + " / " + item.Path + Environment.NewLine
                + L.T("Aktuelle Farben: ", "Current colours: ")
                + String.Join(" / ", Enumerable.Range(0, 4).Select(i => ColorTranslator.ToHtml(document.ColorAt(effect, i))));
            Image old = sample.Image;
            Bitmap texture = EffectTexture(item, document.Textures(effect));
            sample.Image = texture == null ? ColourSamples.Particles(Color.FromArgb(document.ColorAt(effect, 0).A, primaryColor),
                Color.FromArgb(document.ColorAt(effect, 1).A, secondaryColor), effect.Name.IndexOf("star", StringComparison.OrdinalIgnoreCase) >= 0)
                : ParticleTextures.Sample(texture, Color.FromArgb(document.ColorAt(effect, 0).A, primaryColor));
            sampleCaption.Text = texture == null ? L.T("Farbbeispiel · keine Effekttextur gefunden", "Colour sample · effect texture unavailable")
                : L.T("Originaltextur + Hauptfarbe · keine Spielsimulation", "Original texture + primary colour · not a game simulation");
            if (old != null) old.Dispose();
            StudioUx.SetHelp(sample, L.T("Beispielpartikel, keine Originalform, Bewegung oder additive Spielbeleuchtung.", "Example particles, not the original shapes, motion or additive game lighting."));
            save.Enabled = dirty;
        }
        Bitmap EffectTexture(Item item, string[] names)
        {
            foreach (string name in names)
                foreach (var archive in new[] { item.Archive }.Concat(archives.Where(a => a != item.Archive)))
                    foreach (var entry in archive.Files.Where(p => p.Key.EndsWith(".breft", StringComparison.OrdinalIgnoreCase)))
                    {
                        string key = archive.Source + "|" + entry.Key + "|" + name;
                        Bitmap texture;
                        if (!textureCache.TryGetValue(key, out texture))
                        {
                            try { texture = ParticleTextures.Read(entry.Value.Data, name); }
                            catch (InvalidDataException) { texture = null; }
                            textureCache[key] = texture;
                        }
                        if (texture != null) return texture;
                    }
            return null;
        }
        void Apply()
        {
            var selected = checkedEffects.ToArray();
            if (selected.Length == 0) throw new InvalidOperationException(L.T("Zuerst Effekte anhaken.", "Check effects first."));
            var updates = new List<Action>();
            foreach (var set in selected.GroupBy(i => i.Archive.Source + "|" + i.Path))
            {
                var item = set.First();
                var document = new ParticleEffects(item.Archive.Files[item.Path].Data);
                byte[] result = document.Recolor(set.Select(i => i.Effect.Name), primaryColor, secondaryColor, fixedColors.Checked);
                updates.Add(delegate { item.Archive.Files[item.Path].Data = result; });
            }
            foreach (var apply in updates) apply();
            dirty = true; Detail();
            Status.Text = selected.Length + L.T(" Effekte geändert. Jetzt Kopien speichern.", " effects changed. Save copies next.");
        }
        void ResetChanges()
        {
            if (dirty && !Discard()) return;
            var next = archives.Select(a => new StudioArchiveCopy(a.Source)).ToList();
            var nextItems = new List<Item>();
            foreach (var archive in next) ReadEffects(archive, nextItems);
            archives.Clear(); archives.AddRange(next);
            items.Clear(); items.AddRange(nextItems);
            checkedEffects.Clear(); dirty = false;
            RefreshList();
        }
        void Save()
        {
            if (!dirty) return;
            string folder = PackSelection.Output(this, Path.Combine(Path.GetDirectoryName(archives[0].Source), "MUR_EDITED"));
            ArchiveCopyExport.Save(archives, folder);
            dirty = false; Detail(); Status.Text = L.T("Gespeichert: ", "Saved: ") + folder;
            ExportHelp.Show(this, folder);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { foreach (var texture in textureCache.Values) if (texture != null) texture.Dispose(); textureCache.Clear(); }
            if (disposing && sample.Image != null) { sample.Image.Dispose(); sample.Image = null; }
            base.Dispose(disposing);
        }
    }
}



