using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        readonly FlowLayoutPanel humanControls = new FlowLayoutPanel { Name = "HumanMotionControls", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Visible = false };
        readonly CheckBox humanPlay = new CheckBox { Name = "HumanMotionPlay", Text = L.T("Abspielen", "Play"), AutoSize = true };
        readonly CheckBox humanSlow = new CheckBox { Name = "HumanMotionSlow", Text = L.T("Langsam", "Slow"), AutoSize = true, Checked = true };
        readonly TrackBar humanTime = new TrackBar { Name = "HumanMotionTime", Minimum = 0, Maximum = 2000, TickFrequency = 400, Width = 164, Height = 32, AutoSize = false };
        readonly Timer humanTimer = new Timer { Interval = 50 };
        readonly System.Diagnostics.Stopwatch humanClock = new System.Diagnostics.Stopwatch();
        double humanStart;
        int[][] humanBeforeIndices;
        float[][] humanBeforeWeights;

        void CreateHumanMotionControls(TableLayoutPanel scene)
        {
            humanControls.AccessibleName = L.T("Eigene Prüfanimation", "Own test animation");
            humanControls.Padding = new Padding(4, 2, 4, 2);
            StudioUx.SetHelp(humanTime, L.T("Zeitpunkt der menschlichen Prüfbewegung. Ziehen pausiert die Wiedergabe.", "Time in the human test motion. Dragging pauses playback."));
            humanControls.Controls.Add(humanPlay); humanControls.Controls.Add(humanSlow);
            humanControls.Controls.Add(humanTime);
            var rr = StudioChrome.ActionButton(L.T("RR-Bewegungen…", "RR movements…"));
            rr.Name = "ReviewRrMovement"; rr.Click += delegate { mode.SelectedIndex = 4; };
            humanControls.Controls.Add(rr);
            scene.RowCount++; scene.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scene.Controls.Add(humanControls, 0, scene.RowCount - 1);
            humanPlay.CheckedChanged += delegate {
                humanStart = humanTime.Value / 100d; humanClock.Restart(); humanTimer.Enabled = humanPlay.Checked;
                RefreshSurfacePose();
            };
            humanSlow.CheckedChanged += delegate { humanStart = humanTime.Value / 100d; humanClock.Restart(); };
            humanTime.MouseDown += delegate { humanPlay.Checked = false; };
            humanTime.KeyDown += delegate { humanPlay.Checked = false; };
            humanTime.ValueChanged += delegate { RefreshSurfacePose(); };
            humanTimer.Tick += delegate {
                if (mode.SelectedIndex != 5 || surfacePose.SelectedIndex != 7 || !Visible) { humanPlay.Checked = false; return; }
                humanTime.Value = (int)((humanStart + humanClock.Elapsed.TotalSeconds * (humanSlow.Checked ? .5 : 1)) % ModelRig.HumanMotionDuration * 100);
            };
            Disposed += delegate { humanTimer.Dispose(); };
            FormClosed += delegate { humanTimer.Stop(); };
        }

        void ShowHumanMotion()
        {
            mode.SelectedIndex = 5;
            if (mode.SelectedIndex == 5) surfacePose.SelectedIndex = 7;
        }
    }
}
