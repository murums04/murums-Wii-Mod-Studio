using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ModelRigMenuReviewForm : Form
    {
        sealed class Choice
        {
            internal CharacterMenuPose.References Source;
            internal string Clip;
            public override string ToString() { return (Source.Battle ? L.T("Kampf", "Battle") : L.T("Rennen", "Race")) + " · " + Clip; }
        }
        sealed class Problem
        {
            internal ModelRig.SurfaceIssue Issue;
            internal int Number;
            public override string ToString() { return Number + " · " + Issue.Reason; }
        }
        readonly ModelRig original;
        readonly RigPoseReference race;
        readonly Action beforeConfirm;
        readonly ComboBox clips = new ComboBox { Name = "MenuReviewClip", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly CharacterModelViewport preview = new CharacterModelViewport { Name = "MenuReviewPreview", ShowBones = false, ShowDimensions = false };
        readonly ListBox problems = new ListBox { Name = "MenuReviewProblems", Dock = DockStyle.Fill, IntegralHeight = false };
        readonly Label message = new Label { Name = "MenuReviewStatus", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
        readonly CheckBox showVehicle = new CheckBox { Name = "MenuReviewVehicle", Text = L.T("Fahrzeug anzeigen", "Show vehicle"), Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
        CharacterModelImport vehicleModel;
        readonly Button confirm;
        bool loading, closeAfterLoad;
        internal ModelRig.MenuVehicleReview CurrentReview { get; private set; }
        internal Exception LoadError { get; private set; }

        internal ModelRigMenuReviewForm(ModelRig original, string vehicle, RigPoseReference race,
            IEnumerable<CharacterMenuPose.References> sources, Action beforeConfirm)
        {
            this.original = original; this.race = race; this.beforeConfirm = beforeConfirm;
            Name = "MenuVehicleReview";
            Text = L.T("Menüsitzposen prüfen", "Review menu seating") + " · " + CharacterVehicleNames.ShortLabel(vehicle);
            Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent; Size = new Size(1120, 820); MinimumSize = new Size(960, 680);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.Controls.Add(StudioChrome.Header(Text, L.T("Orange Stellen kontrollieren · rechts ziehen: drehen · Mausrad: zoomen", "Inspect orange areas · right-drag: orbit · wheel: zoom")), 0, 0);
            var selection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            selection.Controls.Add(new Label { Text = L.T("Menüpose", "Menu pose"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) }, 0, 0);
            selection.Controls.Add(clips, 1, 0); selection.Controls.Add(showVehicle, 2, 0); layout.Controls.Add(selection, 0, 1);
            showVehicle.CheckedChanged += delegate { preview.VehicleModel = showVehicle.Checked ? vehicleModel : null; };
            layout.Controls.Add(preview, 0, 2); layout.Controls.Add(problems, 0, 3); layout.Controls.Add(message, 0, 4);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var close = StudioChrome.ActionButton(L.T("Schließen", "Close")); close.Name = "CloseMenuReview"; close.Click += delegate { Close(); };
            confirm = StudioChrome.ActionButton(L.T("Diese Pose geprüft", "This pose reviewed")); confirm.Name = "ConfirmMenuReview"; confirm.Enabled = false;
            confirm.Click += delegate {
                if (CurrentReview == null || loading) return;
                try {
                    if (beforeConfirm != null) beforeConfirm();
                    original.ConfirmMenuVehicleReview(CurrentReview); RefreshStatus();
                }
                catch (Exception error) { message.Text = error.Message; confirm.Enabled = false; }
            };
            var focus = StudioChrome.ActionButton(L.T("Problemstelle zeigen", "Show problem area")); focus.Name = "FocusMenuProblem";
            focus.Click += delegate {
                var selected = problems.SelectedItem as Problem;
                if (selected == null) return;
                var issue = selected.Issue;
                preview.SelectedVertices.Clear(); preview.SelectedVertices.UnionWith(issue.Vertices); preview.FocusSurface(); preview.Invalidate();
            };
            var reset = StudioChrome.ActionButton(L.T("Gesamtansicht", "Whole model")); reset.Click += delegate { preview.SelectedVertices.Clear(); preview.ResetView(); };
            actions.Controls.Add(close); actions.Controls.Add(confirm); actions.Controls.Add(focus); actions.Controls.Add(reset);
            layout.Controls.Add(actions, 0, 5); Controls.Add(layout); CancelButton = close;
            foreach (var source in sources) foreach (string clip in source.Clips.Where(c => source.Vehicle(c) == vehicle))
                clips.Items.Add(new Choice { Source = source, Clip = clip });
            if (clips.Items.Count > 0) clips.SelectedIndex = 0;
            clips.SelectedIndexChanged += async delegate { await LoadSelected(); };
            Shown += async delegate { await LoadSelected(); };
            FormClosing += delegate(object sender, FormClosingEventArgs args) {
                if (loading) { closeAfterLoad = true; args.Cancel = true; message.Text = L.T("Vorschau wird geschlossen…", "Closing preview…"); }
            };
            DarkTheme.Apply(this); DarkTheme.StylePrimary(confirm);
        }

        async Task LoadSelected()
        {
            if (loading) return;
            var choice = clips.SelectedItem as Choice;
            CurrentReview = null; LoadError = null; confirm.Enabled = false; problems.Items.Clear();
            preview.AnimationPoints = null; preview.SurfaceOverlay = null; preview.Model = null; preview.VehicleModel = vehicleModel = null;
            if (choice == null) { message.Text = L.T("Für dieses Fahrzeug gibt es keine Menüsitzpose.", "This vehicle has no menu seating pose."); return; }
            loading = true; clips.Enabled = false; message.Text = L.T("Menüpose wird geladen und geprüft…", "Loading and checking menu pose…");
            try {
                var review = await Task.Run(() => original.PreviewMenuVehicle(choice.Source.Vehicle(choice.Clip), race,
                    choice.Source.Pose(choice.Clip), choice.Source.Context(choice.Clip), choice.Source.Fingerprint));
                CurrentReview = review; preview.Model = review.Posed.Preview(); preview.GameContext = 2;
                preview.AnimationPoints = review.Posed.GameGeometry(2, false);
                vehicleModel = race.VehicleGeometry == null ? null : race.VehicleGeometry.ForMenu().Model;
                preview.VehicleModel = showVehicle.Checked ? vehicleModel : null;
                var visible = review.Report.Issues.Where(i => i.Pose == L.T("Fahren", "Driving")).ToArray();
                var colors = new int[original.Points.Length];
                foreach (var issue in visible) { problems.Items.Add(new Problem { Issue = issue, Number = problems.Items.Count + 1 }); foreach (int vertex in issue.Vertices) colors[vertex] = 2; }
                preview.SurfaceOverlay = colors;
                if (problems.Items.Count > 0) problems.SelectedIndex = 0;
                preview.Invalidate();
            }
            catch (Exception error) { LoadError = error; message.Text = error.Message; }
            finally { loading = false; clips.Enabled = true; }
            if (CurrentReview != null) RefreshStatus();
            if (closeAfterLoad) Close();
        }

        void RefreshStatus()
        {
            bool accepted = original.MenuReviewCurrent(CurrentReview); confirm.Enabled = !accepted;
            message.ForeColor = !accepted && CurrentReview.RequiresReview ? Color.Orange : DarkTheme.Fore;
            message.Text = accepted ? L.T("Diese unveränderte Menüpose ist geprüft. Andere Posen bleiben getrennt.", "This unchanged menu pose is reviewed. Other poses remain separate.")
                : CurrentReview.RequiresReview ? L.T("Zusätzliche orange Problemstellen prüfen. Nur erwartete Überschneidungen bestätigen; das Modell wird dadurch nicht repariert.", "Inspect the additional orange problem areas. Accept only expected overlaps; confirmation does not repair the model.")
                : L.T("Keine zusätzlichen dringenden Befunde gegenüber der geprüften Fahrpose. Die Optik kann hier trotzdem kontrolliert werden.", "No additional urgent findings compared with the reviewed driving pose. You can still inspect its appearance here.");
        }
    }
}
