using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        readonly FlowLayoutPanel surfaceControls = new FlowLayoutPanel { Name = "SurfaceCorrection", AutoSize = true, Width = 238, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        readonly ComboBox surfaceTool = new ComboBox { Name = "SurfaceSelection", Width = 232, DropDownWidth = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox surfaceTarget = new ComboBox { Name = "SurfaceTarget", Width = 232, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox surfacePose = new ComboBox { Name = "SurfacePose", Width = 232, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox surfaceIssues = new ComboBox { Name = "SurfaceIssues", Width = 232, DropDownWidth = 570, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly CheckBox surfaceComparison = new CheckBox { Name = "SurfaceBefore", AutoSize = true, Text = L.T("Vor letzter Korrektur zeigen", "Show before last correction") };
        readonly CheckBox surfaceWarnings = new CheckBox { AutoSize = true, Checked = true, Text = L.T("Hinweise orange zeigen", "Show warnings in orange") };
        readonly Label surfaceHint = new Label { AutoSize = true, MaximumSize = new Size(232, 0) };
        readonly HashSet<int> surfaceInclude = new HashSet<int>(), surfaceExclude = new HashSet<int>();
        Button correctSurface, confirmSurface;
        ModelRig.SurfaceReport surfaceReport;
        float[][][] surfaceBefore;
        bool refreshingSurface;

        void CreateSurfaceControls(Control parent)
        {
            parent.Controls.Add(surfaceControls);
            RigSection(surfaceControls, L.T("Oberflächenzuordnung", "Surface assignment"));
            surfaceControls.Controls.Add(new Label { AutoSize = true, Text = L.T("Fläche auswählen", "Select a surface") });
            surfaceControls.Controls.Add(surfaceTool);
            surfaceTool.Items.Add(L.T("Klick / Rechteck", "Click / rectangle"));
            surfaceTool.Items.Add(L.T("Pinsel", "Brush"));
            foreach (string name in Result.SourceComponentNames ?? new string[0]) surfaceTool.Items.Add(L.T("Objekt: ", "Object: ") + name);
            foreach (var material in Result.Materials) surfaceTool.Items.Add(L.T("Material: ", "Material: ") + (material.SourceName ?? material.Name));
            surfaceTool.SelectedIndex = 0;
            surfaceTool.SelectedIndexChanged += delegate {
                if (mode.SelectedIndex != 5) return;
                preview.BrushSelection = surfaceTool.SelectedIndex == 1;
                int index = surfaceTool.SelectedIndex - 2;
                if (index >= 0) {
                    int components = Result.SourceComponentNames == null ? 0 : Result.SourceComponentNames.Length;
                    if (index < components && Result.VertexComponents != null)
                        SelectSurface(Enumerable.Range(0, Result.Points.Length).Where(v => Result.VertexComponents[v] == index));
                    else if (index >= components)
                        SelectSurface(Result.Faces.Where((f, i) => Result.FaceMaterials[i] == index - components).SelectMany(f => f));
                }
            };
            var areaRow = new FlowLayoutPanel { AutoSize = true, Width = 238, WrapContents = false };
            SurfaceSmallButton(areaRow, L.T("Leeren", "Clear"), delegate { surfaceInclude.Clear(); surfaceExclude.Clear(); SelectSurface(new int[0]); });
            SurfaceSmallButton(areaRow, L.T("Verbunden", "Connected"), delegate { SelectSurface(Result.ConnectedSurface(preview.SelectedVertices)); });
            surfaceControls.Controls.Add(areaRow);
            var separate = new CheckBox { Text = L.T("Verbundene Bereiche trennen", "Separate connected areas"), AutoSize = true };
            surfaceControls.Controls.Add(separate);
            var seeds = new FlowLayoutPanel { AutoSize = true, Width = 238, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
            seeds.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(232, 0), Text = L.T("Einige Stellen im Zielbereich und im Nachbarbereich markieren. Auswahl danach kontrollieren.", "Mark a few spots in the target and neighbouring areas. Then review the selection.") });
            var seedRow = new FlowLayoutPanel { AutoSize = true, Width = 238, WrapContents = false };
            SurfaceSmallButton(seedRow, L.T("Gehört dazu", "Include"), delegate { StoreSurfaceSeeds(true); });
            SurfaceSmallButton(seedRow, L.T("Bleibt draußen", "Exclude"), delegate { StoreSurfaceSeeds(false); });
            seeds.Controls.Add(seedRow);
            AddButton(seeds, L.T("Auswahl ausdehnen", "Grow selection"), delegate {
                try { SelectSurface(Result.GrowSurfaceSelection(surfaceInclude, surfaceExclude)); }
                catch (System.IO.InvalidDataException error) { status.Text = error.Message; }
            });
            surfaceControls.Controls.Add(seeds);
            separate.CheckedChanged += delegate { seeds.Visible = separate.Checked; };
            surfaceControls.Controls.Add(new Label { AutoSize = true, Text = L.T("Auswahl folgt künftig", "Make selection follow") });
            surfaceTarget.Items.AddRange(ModelRig.SurfaceTargets.Select(ModelRig.SurfaceTargetLabel).Cast<object>().ToArray());
            surfaceTarget.SelectedIndex = 0; surfaceControls.Controls.Add(surfaceTarget);
            surfaceTarget.SelectedIndexChanged += delegate { RefreshReview(); };
            correctSurface = AddButton(surfaceControls, L.T("Fläche zuordnen", "Assign surface"), ApplySurfaceCorrection);
            correctSurface.Name = "ApplySurfaceCorrection";
            surfacePose.Items.AddRange(new object[] { L.T("Neutral", "Neutral"), L.T("Menü", "Menu"), L.T("Fahren", "Driving"), L.T("Linken Arm beugen", "Bend left arm"), L.T("Rechten Arm beugen", "Bend right arm"), L.T("Linkes Knie beugen", "Bend left knee"), L.T("Rechtes Knie beugen", "Bend right knee"), L.T("Eigene Prüfanimation", "Own test animation") });
            surfacePose.SelectedIndex = 1; surfaceControls.Controls.Add(surfacePose);
            surfacePose.SelectedIndexChanged += delegate { humanPlay.Checked = false; RefreshSurfacePose(); };
            surfaceControls.Controls.Add(surfaceComparison);
            surfaceComparison.CheckedChanged += delegate { RefreshSurfacePose(); };
            surfaceControls.Controls.Add(surfaceWarnings);
            surfaceWarnings.CheckedChanged += delegate { RefreshSurfaceOverlay(); };
            surfaceControls.Controls.Add(surfaceHint);
            surfaceControls.Controls.Add(surfaceIssues);
            AddButton(surfaceControls, L.T("Problemstelle zeigen", "Show problem area"), delegate {
                var issue = surfaceIssues.SelectedItem as ModelRig.SurfaceIssue;
                if (issue == null) return;
                surfaceComparison.Checked = false;
                if (issue.Pose == L.T("Menü", "Menu")) surfacePose.SelectedIndex = 1;
                else if (issue.Pose == L.T("Fahren", "Driving")) surfacePose.SelectedIndex = 2;
                else surfacePose.SelectedIndex = 0;
                SelectSurface(issue.Vertices); preview.Refresh(); preview.FocusSurface();
                status.Text = issue.ToString();
            });
            AddButton(surfaceControls, L.T("Bewegung erneut prüfen", "Check movement again"), delegate { RunSurfaceCheck(true); });
            confirmSurface = AddButton(surfaceControls, L.T("Bewegung geprüft", "Movement reviewed"), delegate {
                Result.ConfirmSurfaceReview(); RefreshReview();
                status.Text = L.T("Prüfung gespeichert. Weitere Änderungen erfordern eine neue Prüfung.", "Review saved. Further changes require another review.");
            });
            confirmSurface.Name = "ConfirmSurfaceReview";
        }

        void SurfaceSmallButton(Control parent, string text, Action action)
        {
            var button = AddButton(parent, text, action); button.Width = 110; button.Height = 34;
        }

        void StoreSurfaceSeeds(bool include)
        {
            var vertices = Result.ExpandSurfaceSelection(preview.SelectedVertices);
            (include ? surfaceInclude : surfaceExclude).UnionWith(vertices);
            (include ? surfaceExclude : surfaceInclude).ExceptWith(vertices);
            preview.SelectedVertices.Clear(); RefreshSurfaceSelection();
            status.Text = L.T("Markierung gespeichert. Cyan: gehört dazu. Blau: bleibt draußen.", "Marks saved. Cyan: include. Blue: exclude.");
        }

        void SelectSurface(IEnumerable<int> vertices)
        {
            int[] expanded = Result.ExpandSurfaceSelection(vertices);
            preview.SelectedVertices.Clear(); preview.SelectedVertices.UnionWith(expanded);
            RefreshSurfaceSelection();
        }

        void RefreshSurfaceSelection()
        {
            if (correctSurface == null) return;
            correctSurface.Enabled = !pending && !surfaceComparison.Checked && !humanPlay.Checked && preview.SelectedVertices.Count > 0;
            status.Text = preview.SelectedVertices.Count == 0
                ? L.T("Bereich anklicken oder einrahmen. Umschalt ergänzt, Strg entfernt.", "Click or frame an area. Shift adds, Ctrl removes.")
                : L.T("Cyan markierte Fläche kontrollieren, dann einem Körperteil zuordnen. Gelenke bleiben an ihrer Position.", "Review the cyan selection, then assign it to a body part. Joint positions stay unchanged.");
            RefreshSurfaceOverlay();
        }

        void ApplySurfaceCorrection()
        {
            if (preview.SelectedVertices.Count == 0 || pending) return;
            PushUndo();
            humanPlay.Checked = false;
            surfaceBefore = Enumerable.Range(0, 7).Select(SurfaceGeometry).ToArray();
            humanBeforeIndices = Result.BoneIndices.Select(v => (int[])v.Clone()).ToArray();
            humanBeforeWeights = Result.BoneWeights.Select(v => (float[])v.Clone()).ToArray();
            Result.CorrectSurface(preview.SelectedVertices, ModelRig.SurfaceTargets[surfaceTarget.SelectedIndex], SelectedBoneIndex);
            surfaceComparison.Checked = false;
            preview.SelectedVertices.Clear(); surfaceInclude.Clear(); surfaceExclude.Clear();
            RunSurfaceCheck(false); RefreshSurfacePose(); RefreshReview();
            status.Text = L.T("Nur die Auswahl wurde neu zugeordnet. Vorher/Nachher und Bewegung kontrollieren; Rückgängig ist möglich.", "Only the selection was reassigned. Compare before/after and check movement; Undo is available.");
        }

        float[][] SurfaceGeometry(int poseIndex)
        {
            if (poseIndex == 7) return Result.HumanMotionGeometry(humanTime.Value / 100d);
            if (poseIndex == 0) return Result.Points;
            if (poseIndex < 3) return Result.GameGeometry(poseIndex, false);
            string name = new[] { "arm_l2", "arm_r2", "leg_l2", "leg_r2" }[poseIndex - 3];
            return Result.Pose(Array.FindIndex(Result.Bones, b => b.Name == name), 60);
        }

        void RefreshSurfacePose()
        {
            if (mode.SelectedIndex != 5 || pending) return;
            preview.GameContext = surfacePose.SelectedIndex == 1 || surfacePose.SelectedIndex == 2 ? surfacePose.SelectedIndex : 0;
            bool human = surfacePose.SelectedIndex == 7;
            humanControls.Visible = human;
            humanPlay.Enabled = humanTime.Enabled = humanSlow.Enabled = Result.HasHumanJoints;
            help.Text = human ? Result.HasHumanJoints ? L.T("Eigene Prüfbewegung: ", "Own test movement: ") + ModelRig.HumanMotionLabel(humanTime.Value / 100d) + L.T(". Zum Korrigieren pausieren.", ". Pause to correct surfaces.") : L.T("Menschliche Gelenke fehlen", "Human joints missing") : L.T("Verzerrte Fläche wählen und dem richtigen Körperteil zuordnen.", "Select a distorted surface and assign it to the correct body part.");
            preview.AnimationPoints = surfaceComparison.Checked && surfaceBefore != null
                ? human ? Result.HumanMotionGeometry(humanTime.Value / 100d, humanBeforeIndices, humanBeforeWeights) : surfaceBefore[surfacePose.SelectedIndex]
                : SurfaceGeometry(surfacePose.SelectedIndex);
            preview.AnimationJoints = null;
            preview.Marking = !surfaceComparison.Checked && !humanPlay.Checked;
            surfaceComparison.Enabled = surfaceBefore != null;
            RefreshSurfaceSelection(); preview.Invalidate();
        }

        void RefreshSurfaceMode()
        {
            bool active = mode.SelectedIndex == 5;
            surfaceControls.Visible = active;
            humanControls.Visible = active && surfacePose.SelectedIndex == 7;
            if (!humanControls.Visible) humanPlay.Checked = false;
            surfacePose.Visible = surfaceComparison.Visible = active;
            mode.Visible = mode.SelectedIndex < 3;
            foreach (Control label in Controls.Find("ToolLabel", true)) label.Visible = mode.Visible;
            foreach (Control label in Controls.Find("BodyPartLabel", true)) label.Visible = mode.SelectedIndex < 3 || active && surfaceTarget.SelectedIndex == 7;
            referencePose.Visible = weights.Visible = mode.SelectedIndex == 1 || mode.SelectedIndex == 2 || mode.SelectedIndex == 3;
            preview.SurfaceOverlay = null;
            if (!active) { preview.ShowWeights = weights.Checked; return; }
            preview.EditJoints = preview.EditGameJoints = preview.ShowBones = preview.ShowWeights = false;
            preview.Marking = true; preview.BrushSelection = surfaceTool.SelectedIndex == 1;
            preview.ReferencePose = false; preview.ReferenceModel = preview.VehicleModel = null;
            help.Text = L.T("Verzerrte Fläche wählen und dem richtigen Körperteil zuordnen.", "Select a distorted surface and assign it to the correct body part.");
            RunSurfaceCheck(false); RefreshSurfacePose();
        }

        void RunSurfaceCheck(bool showStatus)
        {
            if (pending || Result.JointIssues().Count > 0) return;
            Result.InitializeGamePose(1, references == null ? null : references.GetMenu(Result));
            if (references != null) Result.SelectVehiclePose(VehicleKey, references.GetVehicle(VehicleKey, false)); else Result.InitializeGamePose(2, null);
            surfaceReport = Result.CheckSurface();
            RefreshReview();
            surfaceIssues.BeginUpdate(); surfaceIssues.Items.Clear();
            surfaceIssues.Items.AddRange(surfaceReport.Issues.OrderByDescending(i => i.Urgent).ThenByDescending(i => i.Vertices.Length).Cast<object>().ToArray());
            if (surfaceIssues.Items.Count > 0) surfaceIssues.SelectedIndex = 0;
            surfaceIssues.EndUpdate(); RefreshSurfaceReview(); RefreshSurfaceOverlay();
            if (showStatus) status.Text = L.T("Neutral, Menü, Fahren, Arme und Knie geprüft. Hinweise am Modell kontrollieren.", "Neutral, menu, driving, arms and knees checked. Inspect warnings on the model.");
        }

        void RefreshSurfaceReview()
        {
            if (confirmSurface == null || refreshingSurface) return;
            refreshingSurface = true;
            try {
                bool ready = !pending && Result.JointIssues().Count == 0;
                bool current = ready && surfaceReport != null && surfaceReport.Fingerprint == Result.SurfaceFingerprint();
                confirmSurface.Enabled = current;
                confirmSurface.Visible = ready && Result.NeedsComponentReview();
                surfaceHint.Text = !current ? L.T("Bewegung noch prüfen.", "Movement needs checking.")
                    : surfaceReport.Issues.Count == 0 ? L.T("Keine auffällige Verformung gefunden. Optik in allen Posen prüfen.", "No unusual deformation found. Check appearance in every pose.")
                    : L.T("Orange Stellen prüfen. Gelenke dafür nicht verschieben. Erwartete Verformungen dürfen bestätigt werden.", "Inspect orange areas. Keep correct joints in place. Expected deformations may be accepted.");
                surfaceHint.ForeColor = current && surfaceReport.Issues.Count > 0 ? Color.Orange : DarkTheme.Fore;
                surfaceIssues.Enabled = current && surfaceIssues.Items.Count > 0;
                if (apply != null) apply.Enabled = ready && !Result.NeedsComponentReview();
            }
            finally { refreshingSurface = false; }
        }

        void RefreshSurfaceOverlay()
        {
            if (mode.SelectedIndex != 5) return;
            if (surfaceComparison.Checked) { preview.SurfaceOverlay = null; preview.Invalidate(); return; }
            var colors = new int[Result.Points.Length];
            foreach (int v in Result.RigidVertices ?? new int[0]) colors[v] = 1;
            if (surfaceWarnings.Checked && surfaceReport != null) foreach (int v in surfaceReport.MarkedVertices) colors[v] = 2;
            foreach (int v in surfaceExclude) colors[v] = 3;
            foreach (int v in surfaceInclude) colors[v] = 4;
            preview.SurfaceOverlay = colors; preview.Invalidate();
        }
    }
}
