using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        readonly FlowLayoutPanel deletionControls = new FlowLayoutPanel { Name = "DeletionTool", AutoSize = true, Width = 238, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        readonly ComboBox deletionSelection = new ComboBox { Name = "DeletionSelection", Width = 232, DropDownWidth = 400, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly Label deletionCount = new Label { Name = "DeletionCount", AutoSize = true, MaximumSize = new Size(232, 0) };
        Button deleteSurface;
        bool updatingDeletion;
        readonly System.Collections.Generic.HashSet<int> deletionPrevious = new System.Collections.Generic.HashSet<int>();

        void CreateDeletionControls(Control parent)
        {
            parent.Controls.Add(deletionControls);
            RigSection(deletionControls, L.T("Teil entfernen (optional)", "Remove a part (optional)"));
            deletionControls.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(232, 0), Text = L.T("1 · Teil anklicken\n2 · Cyan markierte Fläche prüfen\n3 · Auswahl löschen", "1 · Click a part\n2 · Review the cyan surface\n3 · Delete selection") });
            deletionControls.Controls.Add(deletionCount);
            deleteSurface = AddButton(deletionControls, L.T("Auswahl löschen", "Delete selection"), ApplyDeletion);
            deleteSurface.Name = "DeleteSelectedSurface";
            var clear = AddButton(deletionControls, L.T("Auswahl leeren", "Clear selection"), delegate { preview.SelectedVertices.Clear(); RefreshDeletionSelection(); });
            clear.Name = "ClearDeletionSelection";
            StudioActions.Icon(clear, StudioIcon.Close);
            AddButton(deletionControls, L.T("Weiter · Gelenke prüfen", "Next · Review joints"), delegate { mode.SelectedIndex = 0; }).Name = "ContinueAfterDeletion";
            deletionControls.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(232, 0), Text = L.T("Die Originaldatei bleibt erhalten. Die Änderung wird im Projekt und beim Export übernommen.", "The original file is preserved. The change is included in the project and export.") });
            var advanced = new CheckBox { Name = "AdvancedDeletionSelection", AutoSize = true, Text = L.T("Erweiterte Auswahl", "Advanced selection") };
            deletionControls.Controls.Add(advanced);
            var options = new FlowLayoutPanel { AutoSize = true, Width = 238, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
            deletionSelection.Items.AddRange(new object[] { L.T("Zusammenhängender Teil · Klick", "Connected part · Click"), L.T("Fläche · Klick / Rechteck", "Surface · Click / rectangle"), L.T("Fläche · Pinsel", "Surface · Brush") });
            foreach (string name in Result.SourceComponentNames ?? new string[0]) deletionSelection.Items.Add(L.T("Objekt: ", "Object: ") + name);
            foreach (var material in Result.Materials) deletionSelection.Items.Add(L.T("Material: ", "Material: ") + (material.SourceName ?? material.Name));
            deletionSelection.SelectedIndex = 0;
            options.Controls.Add(deletionSelection);
            options.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(232, 0), Text = L.T("Umschalt ergänzt, Strg entfernt. Nur vollständig markierte Dreiecke werden gelöscht. Bei verbundenen Teilen Objekt, Material oder Fläche wählen.", "Shift adds, Ctrl removes. Only fully selected triangles are deleted. For joined parts choose an object, material or surface.") });
            deletionControls.Controls.Add(options);
            advanced.CheckedChanged += delegate { options.Visible = advanced.Checked; if (!advanced.Checked) deletionSelection.SelectedIndex = 0; };
            deletionSelection.SelectedIndexChanged += delegate {
                if (mode.SelectedIndex != 6) return;
                preview.BrushSelection = deletionSelection.SelectedIndex == 2;
                int index = deletionSelection.SelectedIndex - 3;
                int components = Result.SourceComponentNames == null ? 0 : Result.SourceComponentNames.Length;
                if (index >= 0) {
                    preview.SelectedVertices.Clear();
                    if (index < components && Result.VertexComponents != null) preview.SelectedVertices.UnionWith(Enumerable.Range(0, Result.Points.Length).Where(v => Result.VertexComponents[v] == index));
                    else if (index >= components) preview.SelectedVertices.UnionWith(Result.Faces.Where((f, i) => Result.FaceMaterials[i] == index - components).SelectMany(f => f));
                }
                RefreshDeletionSelection();
            };
        }

        void RefreshDeletionSelection()
        {
            if (updatingDeletion || deleteSurface == null || mode.SelectedIndex != 6) return;
            updatingDeletion = true;
            try {
                if (deletionSelection.SelectedIndex == 0 && preview.SelectedVertices.Count > 0) {
                    var part = Result.DeletionPart(preview.SelectedVertices);
                    preview.SelectedVertices.Clear(); preview.SelectedVertices.UnionWith(part);
                }
                int count = Result.DeletionFaceCount(preview.SelectedVertices);
                bool entire = count == Result.Faces.Length;
                deleteSurface.Enabled = count > 0 && !entire;
                deletionCount.Text = count == 0 ? L.T("Noch keine vollständige Fläche ausgewählt.", "No complete surface selected yet.")
                    : entire ? L.T("Gesamtes Modell ausgewählt. Auswahl verkleinern.", "Entire model selected. Reduce the selection.")
                    : count + L.T(" von ", " of ") + Result.Faces.Length + L.T(" Dreiecken ausgewählt.", " triangles selected.");
                deletionPrevious.Clear(); deletionPrevious.UnionWith(preview.SelectedVertices);
                status.Text = L.T("Cyan markierte Fläche vor dem Löschen prüfen. Rückgängig stellt sie wieder her.", "Review the cyan surface before deleting. Undo restores it.");
                preview.Invalidate();
            }
            finally { updatingDeletion = false; }
        }

        void HandleDeletionSelection()
        {
            if (deletionSelection.SelectedIndex == 0 && (ModifierKeys & Keys.Control) == Keys.Control) {
                var removed = Result.DeletionPart(deletionPrevious.Except(preview.SelectedVertices));
                preview.SelectedVertices.Clear(); preview.SelectedVertices.UnionWith(deletionPrevious.Except(removed));
            }
            RefreshDeletionSelection();
        }

        void RefreshDeletionMode()
        {
            bool active = mode.SelectedIndex == 6;
            confirmSurface.Visible = apply.Visible = !active;
            deletionControls.Visible = active;
            preview.SelectionWholeFaces = active;
            back.Visible = true;
            foreach (var step in new[] { Tuple.Create("DeleteToolStep", 6), Tuple.Create("PlaceJointsStep", 0), Tuple.Create("CheckMovementStep", 5), Tuple.Create("GamePoseStep", 4) })
                foreach (Button button in Controls.Find(step.Item1, true)) {
                    bool selected = mode.SelectedIndex == step.Item2;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = selected ? 2 : 1;
                    button.FlatAppearance.BorderColor = selected ? DarkTheme.Accent : DarkTheme.Border;
                    button.BackColor = selected ? DarkTheme.AccentSoft : DarkTheme.Panel;
                    button.AccessibleDescription = selected ? L.T("Aktiver Arbeitsschritt", "Current stage") : L.T("Arbeitsschritt öffnen", "Open stage");
                }
            if (!active) return;
            mode.Visible = bone.Visible = false;
            foreach (string name in new[] { "BodyPartLabel", "ToolLabel" })
                foreach (Control label in Controls.Find(name, true)) label.Visible = false;
            surfaceControls.Visible = humanControls.Visible = false;
            reviewControls.Visible = false;
            foreach (Control control in deletionControls.Parent.Controls)
                if (Object.Equals(control.Tag, "SourceOnly")) control.Visible = false;
            preview.EditJoints = preview.EditGameJoints = preview.ShowBones = preview.ShowWeights = false;
            referencePose.Visible = weights.Visible = false;
            preview.Marking = true; preview.BrushSelection = deletionSelection.SelectedIndex == 2;
            preview.ReferencePose = false; preview.GameContext = 0;
            preview.AnimationPoints = preview.AnimationJoints = null;
            preview.ReferenceModel = preview.VehicleModel = null; preview.SurfaceOverlay = null;
            help.Text = L.T("Unerwünschtes Teil anklicken. Die cyan markierte Fläche wird entfernt.", "Click an unwanted part. The cyan surface will be removed.");
            RefreshDeletionSelection();
        }

        void ApplyDeletion()
        {
            int count = Result.DeletionFaceCount(preview.SelectedVertices);
            if (count == 0 || count == Result.Faces.Length) return;
            PushUndo(true);
            Result.DeleteSurface(preview.SelectedVertices);
            humanPlay.Checked = false; humanBeforeIndices = null; humanBeforeWeights = null;
            surfaceComparison.Checked = false; surfaceBefore = null; surfaceReport = null;
            surfaceInclude.Clear(); surfaceExclude.Clear();
            preview.Model = Result.Preview();
            RefreshReview(); RefreshDeletionMode();
            status.Text = count + L.T(" Dreiecke entfernt. Rückgängig ist möglich; danach Bewegung erneut prüfen.", " triangles removed. Undo is available; check movement again afterwards.");
        }
    }
}
