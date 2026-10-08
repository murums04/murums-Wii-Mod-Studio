using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterPreviewWorkspace : UserControl
    {
        readonly CharacterModelViewport preview;
        readonly ToolStrip toolbar = new ToolStrip { Name = "PreviewToolbar", Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden };
        readonly ToolStripDropDownButton camera = new ToolStripDropDownButton();
        readonly ToolStripDropDownButton shading = new ToolStripDropDownButton();
        readonly ToolStripButton bones;
        readonly Label status = new Label { Name = "PreviewModelStatus", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0), ForeColor = DarkTheme.Muted };
        readonly string[] views = { "3D", L.T("Vorne", "Front"), L.T("Seite", "Side"), L.T("Oben", "Top"), L.T("Hinten", "Back") };

        internal CharacterPreviewWorkspace(CharacterModelViewport viewport)
        {
            Name = "CharacterPreviewWorkspace";
            Font = new Font("Segoe UI", 10F);
            toolbar.Font = Font;
            toolbar.Margin = Padding.Empty;
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = DarkTheme.Panel2;
            preview = viewport;
            preview.Margin = Padding.Empty;
            preview.SceneGuides = true;
            preview.ShowDimensions = false;
            preview.AccessibleName = L.T("Interaktive 3D-Vorschau", "Interactive 3D preview");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(preview, 0, 1);
            layout.Controls.Add(status, 0, 2);
            var gestureHint = new Label {
                Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = DarkTheme.Muted, Padding = new Padding(8, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = L.T("Ziehen: drehen · Mausrad: zoomen · Mausrad ziehen: verschieben · F: einpassen",
                    "Drag: orbit · Wheel: zoom · Middle-drag: pan · F: fit model")
            };
            layout.Controls.Add(gestureHint, 0, 3);
            StudioUx.SetHelp(preview, gestureHint.Text);
            SizeChanged += delegate {
                bool compact = ClientSize.Height < 320;
                gestureHint.Visible = !compact;
                layout.RowStyles[3].Height = compact ? 0 : 24;
            };
            Controls.Add(layout);

            camera.Name = "PreviewCamera";
            camera.ToolTipText = L.T("Kameraansicht wählen", "Choose camera view");
            for (int index = 0; index < views.Length; index++)
            {
                int selected = index;
                var item = new ToolStripMenuItem(views[index]) { Name = "PreviewCamera" + index };
                item.Click += delegate { preview.SetView(selected); };
                camera.DropDownItems.Add(item);
            }
            shading.Name = "PreviewShading";
            string[] modes = { L.T("Texturiert", "Textured"), L.T("Form", "Solid"), L.T("Drahtgitter", "Wireframe") };
            for (int index = 0; index < modes.Length; index++)
            {
                int selected = index;
                var item = new ToolStripMenuItem(modes[index]) { Name = "PreviewShading" + index, Checked = index == 0 };
                item.Click += delegate {
                    preview.Wireframe = selected == 2;
                    preview.SolidSurface = selected == 1;
                    shading.Text = modes[selected];
                    shading.AccessibleName = shading.Text;
                    foreach (ToolStripMenuItem choice in shading.DropDownItems) choice.Checked = choice == item;
                    preview.Invalidate();
                };
                shading.DropDownItems.Add(item);
            }
            shading.Text = modes[0];
            shading.ToolTipText = L.T("Darstellung wählen: Textur, Form oder Drahtgitter", "Choose shading: textured, solid or wireframe");
            bones = new ToolStripButton(L.T("Skelett", "Bones")) {
                Name = "PreviewBones", CheckOnClick = true, DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            bones.CheckedChanged += delegate { preview.ShowBones = bones.Checked; preview.Invalidate(); };
            var guides = new ToolStripDropDownButton(L.T("Hilfen", "Guides")) { Name = "PreviewGuides" };
            AddToggle(guides, "PreviewGrid", L.T("Bodenraster", "Floor grid"), preview.ShowFloorGrid, value => preview.ShowFloorGrid = value);
            AddToggle(guides, "PreviewAxes", L.T("Achsen", "Axes"), preview.ShowAxes, value => preview.ShowAxes = value);
            AddToggle(guides, "PreviewDimensions", L.T("Modellmaße", "Model dimensions"), preview.ShowDimensions, value => preview.ShowDimensions = value);
            var fit = new ToolStripButton(L.T("Einpassen", "Fit model")) { Name = "PreviewFit", ToolTipText = L.T("Modell zentrieren und einpassen (F)", "Center and fit model (F)") };
            fit.Click += delegate { preview.FitView(); };
            var reset = new ToolStripMenuItem(L.T("Kamera zurücksetzen", "Reset camera")) { Name = "PreviewReset" };
            reset.Click += delegate { preview.ResetView(); };
            camera.DropDownItems.Add(new ToolStripSeparator());
            camera.DropDownItems.Add(reset);
            StudioActions.Tool(camera, StudioIcon.Layout, false);
            StudioActions.Tool(shading, StudioIcon.Image, false);
            StudioActions.Tool(bones, StudioIcon.Character, false);
            StudioActions.Tool(guides, StudioIcon.Settings, false);
            StudioActions.Tool(fit, StudioIcon.Fit, false);
            toolbar.Items.AddRange(new ToolStripItem[] { camera, shading, new ToolStripSeparator(), bones, guides, new ToolStripSeparator(), fit });
            DarkTheme.StyleToolStrip(toolbar, new MurumsDarkToolStripRenderer());
            preview.ViewChanged += PreviewChanged;
            RefreshState();
            StudioPreview.AddExpandButton(this, preview);
        }

        void AddToggle(ToolStripDropDownButton menu, string name, string text, bool value, Action<bool> change)
        {
            var item = new ToolStripMenuItem(text) { Name = name, CheckOnClick = true, Checked = value };
            item.CheckedChanged += delegate { change(item.Checked); preview.Invalidate(); };
            menu.DropDownItems.Add(item);
        }

        void PreviewChanged(object sender, EventArgs e) { RefreshState(); }

        void RefreshState()
        {
            var model = preview.Model;
            bool loaded = model != null && model.Points.Count > 0;
            camera.Enabled = shading.Enabled = loaded;
            toolbar.Items["PreviewFit"].Enabled = loaded;
            bones.Enabled = loaded && model.Rig != null && model.Rig.Bones.Length > 0;
            bones.ToolTipText = bones.Enabled
                ? L.T("Gelenke und Verbindungen über dem Modell anzeigen", "Show joints and connections over the model")
                : L.T("Noch kein darstellbares Skelett. Unter „Modell bearbeiten“ Gelenke zuordnen.", "No displayable skeleton yet. Assign joints in Edit model.");
            camera.Text = views[preview.CameraView];
            camera.AccessibleName = L.T("Kamera: ", "Camera: ") + camera.Text;
            for (int i = 0; i < views.Length; i++) ((ToolStripMenuItem)camera.DropDownItems[i]).Checked = i == preview.CameraView;
            status.Text = loaded
                ? Path.GetFileName(model.Source) + " · " + model.Points.Count.ToString("N0") + L.T(" Eckpunkte · ", " vertices · ")
                    + model.Faces.Count.ToString("N0") + L.T(" Flächen · Zoom ", " faces · Zoom ") + (preview.Zoom * 100).ToString("0") + "%"
                : L.T("Kein Modell geladen", "No model loaded");
            status.AccessibleName = status.Text;
            StudioUx.SetHelp(status, status.Text);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) preview.ViewChanged -= PreviewChanged;
            base.Dispose(disposing);
        }
    }
}
