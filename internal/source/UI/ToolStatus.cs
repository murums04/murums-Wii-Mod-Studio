using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioProgressBar : Control
    {
        private int value;
        public int Minimum { get { return 0; } set { } }
        public int Maximum { get { return 100; } set { } }
        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        internal StudioProgressBar()
        {
            Name = "StudioStatusProgress";
            Height = 4;
            Dock = DockStyle.Fill;
            Margin = new Padding(0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(232, 232, 232));
            if (Value > 0)
                using (var brush = new SolidBrush(Color.FromArgb(24, 128, 32)))
                    e.Graphics.FillRectangle(brush, 0, 0, Width * Value / 100, Height);
            using (var pen = new Pen(DarkTheme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
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