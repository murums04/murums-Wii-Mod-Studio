using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class MenuModelPreviewForm : Form
    {
        readonly ComboBox choices = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly CharacterModelViewport viewport = new CharacterModelViewport();
        readonly Label information = new Label { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(6) };
        readonly Dictionary<string, CharacterModelImport> cache = new Dictionary<string, CharacterModelImport>();
        readonly Dictionary<string, bool> visible;
        readonly StudioArchiveCopy archive;
        bool ready;
        internal MenuModelPreviewForm(string source, Dictionary<string, bool> visibility)
        {
            Text = L.T("RR-Menümodelle – 3D-Vorschau", "RR menu models – 3D preview");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10F); AutoScaleMode = AutoScaleMode.Font;
            Size = new Size(960, 730); MinimumSize = new Size(800, 620);
            StartPosition = FormStartPosition.CenterParent;
            visible = visibility;
            archive = new StudioArchiveCopy(source);
            Controls.Add(viewport); Controls.Add(information); Controls.Add(choices);
            var header = StudioChrome.Header(L.T("Menümodelle", "Menu models"), L.T("Originalgeometrie und Texturen • Ziehen zum Drehen • Mausrad zum Zoomen", "Original geometry and textures • Drag to orbit • Wheel to zoom"));
            header.Dock = DockStyle.Top; header.Height = 115; Controls.Add(header);
            foreach (var entry in visibility.Keys) choices.Items.Add(entry);
            choices.SelectedIndexChanged += delegate { if (ready) LoadModel(); };
            Shown += delegate { ready = true; if (choices.SelectedIndex < 0 && choices.Items.Count > 0) choices.SelectedIndex = 0; };
            DarkTheme.Apply(this);
        }
        void LoadModel()
        {
            string selected = choices.SelectedItem as string;
            if (selected == null) return;
            try
            {
                CharacterModelImport model;
                if (!cache.TryGetValue(selected, out model))
                {
                    model = ModelOperationForm.Run(this, L.T("Originalmodell laden", "Load original model"), token =>
                    {
                        string folder = ModelRuntime.NewWorkFolder();
                        string brres = Path.Combine(folder, "source.brres");
                        File.WriteAllBytes(brres, archive.Files.First(p => Path.GetFileName(p.Key) == selected).Value.Data);
                        var names = (string[])StudioModelLibrary.Call("Models", brres);
                        if (names.Length == 0) throw new InvalidDataException("No model geometry in " + selected);
                        string dae = Path.Combine(folder, "preview.dae");
                        StudioModelLibrary.Call("ExportModel", brres, names[0], dae);
                        token.ThrowIfCancellationRequested();
                        var result = new CharacterModelImport { Source = dae };
                        IntegratedModelImport.ReadModel(dae, result);
                        return result;
                    });
                    if (model == null) return;
                    cache.Add(selected, model);
                }
                viewport.Model = model;
                information.Text = (visible[selected] ? L.T("Beim Export sichtbar.", "Visible in export.") : L.T("Beim Export ausgeblendet; hier zur Kontrolle gezeigt.", "Hidden in export; shown here for inspection."))
                    + "\n" + L.T("Grundstellung mit Texturen. Spielbeleuchtung, Animationen und Materialeffekte können abweichen.", "Rest pose with textures. Game lighting, animations and material effects may differ.");
            }
            catch (Exception error) { information.Text = error.Message; viewport.Model = null; }
        }
    }
}
