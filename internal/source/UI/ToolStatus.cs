using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace murumsWiiModStudio
{
    internal sealed class StudioProgressBar : Control
    {
        private int value;
        private readonly Timer activity = new Timer { Interval = 40 };
        private readonly Func<bool> motionPreference;
        private readonly Func<bool> highContrast;
        private readonly List<Control> ancestors = new List<Control>();
        private Form owner;
        private bool disposed;
        private int phase;
        private bool indeterminate;
        internal bool Indeterminate
        {
            get { return indeterminate; }
            set
            {
                if (indeterminate == value) return;
                indeterminate = value;
                UpdateActivity(this, EventArgs.Empty);
                Invalidate();
            }
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateActivity(this, e);
        }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); UpdateActivity(this, e); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SystemEvents.UserPreferenceChanged += PreferenceChanged;
            UpdateActivity(this, e);
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= PreferenceChanged;
            if (!disposed) activity.Stop();
            UnbindOwner();
            UnbindAncestors();
            base.OnHandleDestroyed(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                disposed = true;
                SystemEvents.UserPreferenceChanged -= PreferenceChanged;
                UnbindOwner();
                UnbindAncestors();
                activity.Dispose();
            }
            base.Dispose(disposing);
        }
        public int Minimum { get { return 0; } set { } }
        public int Maximum { get { return 100; } set { } }
        public int Value
        {
            get { return value; }
            set {
                int next = Math.Max(0, Math.Min(100, value));
                if (this.value == next) return;
                this.value = next;
                Invalidate();
            }
        }
        internal StudioProgressBar() : this(delegate { return StudioWindowLayout.MotionEnabled; }, delegate { return SystemInformation.HighContrast; }) { }

        internal StudioProgressBar(Func<bool> motionPreference, Func<bool> highContrast)
        {
            if (motionPreference == null) throw new ArgumentNullException("motionPreference");
            if (highContrast == null) throw new ArgumentNullException("highContrast");
            this.motionPreference = motionPreference;
            this.highContrast = highContrast;
            activity.Tick += delegate {
                if (!CanAnimate()) { activity.Stop(); Invalidate(); return; }
                phase = (phase + 1) % 200;
                Invalidate();
            };
            Name = "StudioStatusProgress";
            Height = 4;
            Dock = DockStyle.Fill;
            Margin = new Padding(0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private bool CanAnimate()
        {
            return !disposed && !Disposing && IsHandleCreated && indeterminate && Width > 0 && Height > 0
                && !highContrast() && !SystemInformation.TerminalServerSession && motionPreference() && StudioUx.CanPaint(this);
        }

        private void UpdateActivity(object sender, EventArgs e)
        {
            if (disposed) return;
            var next = TopLevelControl as Form;
            if (owner != next)
            {
                UnbindOwner();
                owner = next;
                if (owner != null) owner.Resize += UpdateActivity;
            }
            BindAncestors();
            activity.Enabled = CanAnimate();
        }

        private void UnbindOwner()
        {
            if (owner == null) return;
            owner.Resize -= UpdateActivity;
            owner = null;
        }

        private void BindAncestors()
        {
            int index = 0;
            Control parent = Parent;
            while (parent != null && index < ancestors.Count && ancestors[index] == parent)
            {
                index++;
                parent = parent.Parent;
            }
            if (parent == null && index == ancestors.Count) return;
            UnbindAncestors();
            for (parent = Parent; parent != null; parent = parent.Parent)
            {
                ancestors.Add(parent);
                parent.VisibleChanged += UpdateActivity;
                parent.ParentChanged += UpdateActivity;
            }
        }

        private void UnbindAncestors()
        {
            foreach (Control parent in ancestors)
            {
                parent.VisibleChanged -= UpdateActivity;
                parent.ParentChanged -= UpdateActivity;
            }
            ancestors.Clear();
        }

        private void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (disposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate { if (!disposed) { UpdateActivity(this, EventArgs.Empty); Invalidate(); } }); }
            catch (InvalidOperationException) { }
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); UpdateActivity(this, e); }
        protected override void OnSystemColorsChanged(EventArgs e) { base.OnSystemColorsChanged(e); UpdateActivity(this, e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            UpdateActivity(this, EventArgs.Empty);
            bool contrast = highContrast();
            e.Graphics.Clear(contrast ? SystemColors.Control : DarkTheme.Panel3);
            if (Width < 1 || Height < 1) return;
            int width = indeterminate ? Math.Max(1, (int)((long)Width * 30 / 100)) : (int)((long)Width * Value / 100);
            if (width < 1) return;
            int travel = Width - width;
            int position = 0;
            if (indeterminate)
                position = activity.Enabled ? (int)((long)travel * (phase <= 100 ? phase : 200 - phase) / 100) : travel / 2;
            var fill = new Rectangle(position, 0, width, Height);
            if (contrast || width == 1)
            {
                using (var brush = new SolidBrush(contrast ? SystemColors.Highlight : DarkTheme.Accent)) e.Graphics.FillRectangle(brush, fill);
            }
            else
            {
                using (var brush = new LinearGradientBrush(fill, DarkTheme.Accent, DarkTheme.Cyan, 0F)) e.Graphics.FillRectangle(brush, fill);
            }
        }
    }

    internal static class ToolStatus
    {
        private sealed class State
        {
            internal StudioProgressBar Bar;
        }
        private static readonly ConditionalWeakTable<Form, State> states = new ConditionalWeakTable<Form, State>();
        private static readonly ConditionalWeakTable<Control, object> tracked = new ConditionalWeakTable<Control, object>();

        internal static Control Wrap(Form owner, Label status)
        {
            var bar = new StudioProgressBar();
            Register(owner, bar);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));
            status.Dock = DockStyle.Fill;
            status.Margin = new Padding(0);
            status.Padding = new Padding(4, 0, 4, 0); status.AutoSize = false; status.AutoEllipsis = true;
            if (String.IsNullOrEmpty(status.Text)) status.Text = L.T("Bereit.", "Ready.");
            panel.Controls.Add(bar, 0, 1);
            panel.Controls.Add(status, 0, 0);
            Action resizeStatus = delegate
            {
                var root = panel.Parent as TableLayoutPanel;
                if (root == null) return;
                int row = root.GetRow(panel);
                if (row < 0 || row >= root.RowStyles.Count) return;
                int lines = Math.Min(3, status.Text.Split('\n').Length);
                root.RowStyles[row].SizeType = SizeType.Absolute;
                root.RowStyles[row].Height = Math.Max(30, status.Font.Height * lines + 12);
            };
            status.TextChanged += delegate { resizeStatus(); };
            panel.ParentChanged += delegate { resizeStatus(); };
            owner.ParentChanged += delegate
            {
                bar.Visible = owner.TopLevel;
                panel.RowStyles[1].Height = owner.TopLevel ? 4 : 0;
            };
            return panel;
        }

        internal static void Register(Form owner, StudioProgressBar bar)
        {
            states.GetOrCreateValue(owner).Bar = bar;
        }

        internal static async System.Threading.Tasks.Task<T> RunAsync<T>(Form owner, Func<T> operation)
        {
            State state;
            StudioProgressBar bar = owner != null && states.TryGetValue(owner, out state) ? state.Bar : null;
            if (bar != null) { bar.Value = 0; bar.Indeterminate = true; }
            try { return await System.Threading.Tasks.Task.Run(operation); }
            finally { if (bar != null && !bar.IsDisposed) bar.Indeterminate = false; }
        }

        internal static async System.Threading.Tasks.Task RunAsync(Form owner, Action operation)
        {
            await RunAsync(owner, delegate { operation(); return true; });
        }

        internal static void Set(Form owner, bool success)
        {
            State state;
            if (owner != null && states.TryGetValue(owner, out state) && state.Bar != null)
                state.Bar.Value = success ? 100 : 0;
            if (owner != null && !owner.TopLevel && owner.Parent != null)
            {
                Form parent = owner.Parent.FindForm();
                if (parent != owner) Set(parent, success);
            }
        }

        internal static void Watch(Form owner)
        {
            WatchControl(owner, owner);
        }

        private static void WatchControl(Form owner, Control control)
        {
            object marker;
            if (tracked.TryGetValue(control, out marker)) return;
            tracked.Add(control, new object());
            control.ControlAdded += delegate(object sender, ControlEventArgs e) { WatchControl(owner, e.Control); };
            if (control is ButtonBase || control is TextBoxBase || control is ComboBox
                || control is ListControl || control is TreeView || control is NumericUpDown
                || control is DataGridView || control is PictureBox)
            {
                control.MouseDown += delegate { Set(owner, false); };
                control.KeyDown += delegate { Set(owner, false); };
            }
            var combo = control as ComboBox;
            if (combo != null) combo.SelectedIndexChanged += delegate { Set(owner, false); };
            var text = control as TextBoxBase;
            if (text != null) text.TextChanged += delegate { Set(owner, false); };
            var number = control as NumericUpDown;
            if (number != null) number.ValueChanged += delegate { Set(owner, false); };
            var check = control as CheckBox;
            if (check != null) check.CheckedChanged += delegate { Set(owner, false); };
            var tabs = control as TabControl;
            if (tabs != null) tabs.SelectedIndexChanged += delegate { Set(owner, false); };
            foreach (Control child in control.Controls)
            {
                var childForm = child as Form;
                if (childForm != null && childForm != owner) continue;
                WatchControl(owner, child);
            }
        }
    }
}
