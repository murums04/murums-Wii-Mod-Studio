using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal static class BrlanEditSnapshots
    {
        internal static bool Equal(object a, object b)
        {
            if (Object.ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.GetType() != b.GetType()) return false;
            Type type = a.GetType();
            if (type.IsValueType || a is string) return a.Equals(b);
            var dictionary = a as System.Collections.IDictionary;
            if (dictionary != null)
            {
                var other = (System.Collections.IDictionary)b;
                if (dictionary.Count != other.Count) return false;
                foreach (object key in dictionary.Keys) if (!other.Contains(key) || !Equal(dictionary[key], other[key])) return false;
                return true;
            }
            var list = a as System.Collections.IList;
            if (list != null)
            {
                var other = (System.Collections.IList)b;
                if (list.Count != other.Count) return false;
                for (int i = 0; i < list.Count; i++) if (!Equal(list[i], other[i])) return false;
                return true;
            }
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (!Equal(field.GetValue(a), field.GetValue(b))) return false;
            return true;
        }
    }

    internal sealed class BrlanInputHistory
    {
        readonly Form form;
        readonly System.Collections.Generic.List<Control> inputs = new System.Collections.Generic.List<Control>();
        readonly EditHistory<System.Collections.Generic.Dictionary<Control, object>> history = new EditHistory<System.Collections.Generic.Dictionary<Control, object>>(BrlanEditSnapshots.Equal);
        bool applying, pending;
        string group;
        internal readonly StudioUndoRedo Actions;
        internal BrlanInputHistory(Form owner, Control host, Func<bool> enabled)
        {
            form = owner;
            Gather(owner);
            history.Reset(Capture());
            Actions = new StudioUndoRedo(owner, host, delegate { Flush(); return enabled() && history.CanUndo; }, delegate { Flush(); return enabled() && history.CanRedo; }, delegate { Apply(false); }, delegate { Apply(true); });
        }
        void Gather(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                var text = control as TextBoxBase;
                var numeric = control as NumericUpDown;
                var combo = control as ComboBox;
                var check = control as CheckBox;
                if (numeric != null) { inputs.Add(control); numeric.ValueChanged += Changed; }
                else if (text != null && !text.ReadOnly) { inputs.Add(control); text.TextChanged += Changed; }
                else if (combo != null) { inputs.Add(control); combo.SelectedIndexChanged += Changed; if (combo.DropDownStyle != ComboBoxStyle.DropDownList) combo.TextChanged += Changed; }
                else if (check != null) { inputs.Add(control); check.CheckedChanged += Changed; }
                else Gather(control);
            }
        }
        System.Collections.Generic.Dictionary<Control, object> Capture()
        {
            var state = new System.Collections.Generic.Dictionary<Control, object>();
            foreach (Control input in inputs)
            {
                if (input is NumericUpDown) state[input] = ((NumericUpDown)input).Value;
                else if (input is CheckBox) state[input] = ((CheckBox)input).Checked;
                else if (input is ComboBox) state[input] = new object[] { ((ComboBox)input).SelectedIndex, input.Text };
                else state[input] = input.Text;
            }
            return state;
        }
        void Changed(object sender, EventArgs e)
        {
            if (applying) return;
            pending = true;
            group = "input:" + ((Control)sender).GetHashCode().ToString();
        }
        void Flush()
        {
            if (!pending || applying || form.IsDisposed) return;
            pending = false;
            history.Record(Capture(), group);
        }
        internal void Reset()
        {
            pending = false;
            history.Reset(Capture());
            if (Actions != null) Actions.Refresh();
        }
        void Apply(bool redo)
        {
            Flush();
            if (redo ? !history.CanRedo : !history.CanUndo) return;
            var state = redo ? history.Redo() : history.Undo();
            applying = true;
            try
            {
                foreach (var pair in state)
                {
                    if (pair.Key is NumericUpDown) ((NumericUpDown)pair.Key).Value = (decimal)pair.Value;
                    else if (pair.Key is CheckBox) ((CheckBox)pair.Key).Checked = (bool)pair.Value;
                    else if (pair.Key is ComboBox)
                    {
                        var value = (object[])pair.Value; var combo = (ComboBox)pair.Key;
                        int index = (int)value[0];
                        if (index < combo.Items.Count) combo.SelectedIndex = index;
                        if (combo.DropDownStyle != ComboBoxStyle.DropDownList) combo.Text = (string)value[1];
                    }
                    else pair.Key.Text = (string)pair.Value;
                }
            }
            finally { applying = false; pending = false; }
        }
    }

    internal static class SimpleDialogs
    {
        public static string Prompt(IWin32Window owner, string title, string label, string initial)
        {
            return Prompt(owner, title, label, initial, L.T("Hier Wert eingeben...", "Enter value here..."));
        }

        public static string Prompt(IWin32Window owner, string title, string label, string initial, string cue)
        {
            Form f = new Form();
            f.Text = title;
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ClientSize = new Size(500, 145);
            f.BackColor = DarkTheme.Back;
            f.ForeColor = DarkTheme.Fore;
            f.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            try
            {
                f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            Label l = new Label();
            l.Text = label;
            l.Left = 12;
            l.Top = 12;
            l.Width = 470;
            CueTextBox box = new CueTextBox();
            box.Left = 12;
            box.Top = 40;
            box.Width = 470;
            box.Cue = cue == null ? "" : cue;
            box.Text = initial == null ? "" : initial;
            box.BackColor = DarkTheme.Panel;
            box.ForeColor = DarkTheme.Fore;
            Button ok = MakeButton("OK", 326, 88, 75);
            ok.DialogResult = DialogResult.OK;
            Button cancel = MakeButton(L.T("Abbrechen", "Cancel"), 407, 88, 75);
            cancel.DialogResult = DialogResult.Cancel;
            f.Controls.Add(l);
            f.Controls.Add(box);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            ArrangeActions(f, ok, cancel);
            new BrlanInputHistory(f, ok.Parent, delegate { return true; });
            DarkTheme.Apply(f);
            murumsWiiModStudio.DarkTheme.StylePrimary(ok);
            DialogResult result = f.ShowDialog(owner);
            string value = result == DialogResult.OK ? box.Text : null;
            f.Dispose();
            return value;
        }

        public static string PromptMultiline(IWin32Window owner, string title, string label, string initial)
        {
            return PromptMultiline(owner, title, label, initial, L.T("Eine Zeile pro Eintrag...", "One entry per line..."));
        }

        public static string PromptMultiline(IWin32Window owner, string title, string label, string initial, string cue)
        {
            Form f = new Form();
            f.Text = title;
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.Sizable;
            f.MinimizeBox = false;
            f.ClientSize = new Size(560, 420);
            f.BackColor = DarkTheme.Back;
            f.ForeColor = DarkTheme.Fore;
            f.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            try
            {
                f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            Label l = new Label();
            l.Text = label;
            l.Left = 12;
            l.Top = 12;
            l.Width = 530;
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.Left = 12;
            box.Top = 40;
            box.Width = 530;
            box.Height = 315;
            box.Text = initial == null ? "" : initial;
            box.BackColor = DarkTheme.Panel;
            box.ForeColor = DarkTheme.Fore;
            box.Font = new Font("Consolas", 10.5F);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Label ghost = new Label();
            ghost.Text = cue == null ? "" : cue;
            ghost.ForeColor = DarkTheme.Disabled;
            ghost.BackColor = DarkTheme.Panel;
            ghost.Font = new Font("Segoe UI", 10F, FontStyle.Italic);
            ghost.AutoSize = true;
            ghost.Left = 20;
            ghost.Top = 48;
            ghost.Enabled = false;
            ghost.Visible = String.IsNullOrEmpty(box.Text);
            box.TextChanged += delegate
            {
                ghost.Visible = String.IsNullOrEmpty(box.Text);
            };
            Button ok = MakeButton(L.T("Übernehmen", "Apply"), 376, 370, 80);
            ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ok.DialogResult = DialogResult.OK;
            Button cancel = MakeButton(L.T("Abbrechen", "Cancel"), 462, 370, 80);
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.DialogResult = DialogResult.Cancel;
            f.Controls.Add(l);
            f.Controls.Add(box);
            f.Controls.Add(ghost);
            ghost.BringToFront();
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            ArrangeActions(f, ok, cancel);
            new BrlanInputHistory(f, ok.Parent, delegate { return true; });
            DarkTheme.Apply(f);
            murumsWiiModStudio.DarkTheme.StylePrimary(ok);
            DialogResult result = f.ShowDialog(owner);
            string value = result == DialogResult.OK ? box.Text : null;
            f.Dispose();
            return value;
        }

        public static string ChooseTag(IWin32Window owner)
        {
            Form f = new Form();
            f.Text = L.T("Animationstag hinzufügen", "Add animation tag");
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ClientSize = new Size(500, 190);
            f.BackColor = DarkTheme.Back;
            f.ForeColor = DarkTheme.Fore;
            f.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            try
            {
                f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            Label l = new Label();
            l.Text = L.T("Tag-Typ:", "Tag type:");
            l.Left = 12;
            l.Top = 18;
            l.Width = 100;
            ComboBox combo = new ComboBox();
            combo.Left = 110;
            combo.Top = 14;
            combo.Width = 365;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.BackColor = DarkTheme.Panel;
            combo.ForeColor = DarkTheme.Fore;
            combo.Items.AddRange(BrlanNames.KnownTags);
            combo.SelectedIndex = 0;
            DarkTheme.StyleComboBox(combo);
            Label desc = new Label();
            desc.Left = 12;
            desc.Top = 55;
            desc.Width = 463;
            desc.Height = 55;
            desc.ForeColor = DarkTheme.Muted;
            desc.Text = BrlanNames.TagDescription(combo.SelectedItem.ToString());
            combo.SelectedIndexChanged += delegate
            {
                desc.Text = BrlanNames.TagDescription(combo.SelectedItem.ToString());
            };
            Button ok = MakeButton(L.T("Hinzufügen", "Add"), 318, 130, 75);
            ok.DialogResult = DialogResult.OK;
            Button cancel = MakeButton(L.T("Abbrechen", "Cancel"), 400, 130, 75);
            cancel.DialogResult = DialogResult.Cancel;
            f.Controls.Add(l);
            f.Controls.Add(combo);
            f.Controls.Add(desc);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            ArrangeActions(f, ok, cancel);
            DarkTheme.Apply(f);
            murumsWiiModStudio.DarkTheme.StylePrimary(ok);
            DialogResult result = f.ShowDialog(owner);
            string value = result == DialogResult.OK ? combo.SelectedItem.ToString() : null;
            f.Dispose();
            return value;
        }

        internal static void ArrangeActions(Form form, Button primary, Button cancel)
        {
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 12) };
            primary.AutoSize = cancel.AutoSize = true;
            primary.MinimumSize = cancel.MinimumSize = new Size(100, 34);
            primary.Margin = cancel.Margin = new Padding(4, 0, 0, 0);
            actions.Controls.Add(primary); actions.Controls.Add(cancel); form.Controls.Add(actions);
        }

        private static Button MakeButton(string text, int x, int y, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Left = x;
            b.Top = y;
            b.Width = width;
            b.Height = 31;
            b.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            return b;
        }
    }

    internal sealed class RltpWizardForm : Form
    {
        private readonly PaiSection _pai;
        private ComboBox _animation;
        private NumericUpDown _index;
        private NumericUpDown _start;
        private NumericUpDown _hold;
        private CheckBox _adjustFrames;
        public int AnimationIndex
        {
            get
            {
                return _animation.SelectedIndex;
            }
        }

        public byte TextureSlot
        {
            get
            {
                return (byte)_index.Value;
            }
        }

        public int StartFrame
        {
            get
            {
                return Decimal.ToInt32(_start.Value);
            }
        }

        public int FramesPerImage
        {
            get
            {
                return Decimal.ToInt32(_hold.Value);
            }
        }

        public bool AdjustFrames
        {
            get
            {
                return _adjustFrames.Checked;
            }
        }

        public RltpWizardForm(PaiSection pai)
        {
            _pai = pai;
            Text = "RLTP Generator";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 300);
            BackColor = DarkTheme.Back;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            int y = 20;
            AddLabel(L.T("Ziel-Animation:", "Target animation:"), 18, y);
            _animation = new ComboBox();
            _animation.Left = 180;
            _animation.Top = y - 4;
            _animation.Width = 330;
            _animation.DropDownStyle = ComboBoxStyle.DropDownList;
            _animation.BackColor = DarkTheme.Panel;
            _animation.ForeColor = DarkTheme.Fore;
            int i;
            for (i = 0; i < pai.Animations.Count; i++)
                _animation.Items.Add(i.ToString() + ": " + pai.Animations[i].Name + " – " + BrlanNames.AnimationTargetName(pai.Animations[i].TargetKind));
            if (_animation.Items.Count > 0)
                _animation.SelectedIndex = 0;
            DarkTheme.StyleComboBox(_animation);
            Controls.Add(_animation);
            y += 44;
            AddLabel(L.T("Texture-Slot / Index:", "Texture slot / index:"), 18, y);
            _index = AddNumeric(180, y - 4, 0, 255, 0);
            y += 44;
            AddLabel(L.T("Startframe:", "Start frame:"), 18, y);
            _start = AddNumeric(180, y - 4, 0, 65535, 0);
            y += 44;
            AddLabel(L.T("Frames pro Bild:", "Frames per image:"), 18, y);
            _hold = AddNumeric(180, y - 4, 1, 10000, 6);
            y += 44;
            _adjustFrames = new CheckBox();
            _adjustFrames.Left = 180;
            _adjustFrames.Top = y;
            _adjustFrames.Width = 330;
            _adjustFrames.Text = L.T("pai1-Framezahl automatisch anpassen", "Automatically adjust pai1 frame count");
            _adjustFrames.Checked = true;
            Controls.Add(_adjustFrames);
            Label info = new Label();
            info.Left = 18;
            info.Top = 230;
            info.Width = 310;
            info.Height = 45;
            info.ForeColor = DarkTheme.Muted;
            info.Text = L.T("Verwendet alle TPL-Einträge in ihrer aktuellen Reihenfolge.", "Uses all TPL entries in their current order.");
            Controls.Add(info);
            Button ok = MakeButton(L.T("RLTP erzeugen", "Create RLTP"), 340, 238, 90);
            ok.DialogResult = DialogResult.OK;
            Button cancel = MakeButton(L.T("Abbrechen", "Cancel"), 436, 238, 80);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(ClientSize.Width, Math.Max(360, ClientSize.Height));
            SimpleDialogs.ArrangeActions(this, ok, cancel);
            new BrlanInputHistory(this, ok.Parent, delegate { return true; });
            DarkTheme.Apply(this);
            murumsWiiModStudio.DarkTheme.StylePrimary(ok);
        }

        private void AddLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Left = x;
            l.Top = y;
            l.Width = 155;
            Controls.Add(l);
        }

        private NumericUpDown AddNumeric(int x, int y, decimal min, decimal max, decimal value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Left = x;
            n.Top = y;
            n.Width = 170;
            n.ReadOnly = false;
            n.TabStop = true;
            n.InterceptArrowKeys = true;
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            n.BackColor = DarkTheme.Panel;
            n.ForeColor = DarkTheme.Fore;
            Controls.Add(n);
            return n;
        }

        private static Button MakeButton(string text, int x, int y, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Left = x;
            b.Top = y;
            b.Width = width;
            b.Height = 31;
            b.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = DarkTheme.Border;
            b.BackColor = DarkTheme.Panel2;
            b.ForeColor = DarkTheme.Fore;
            return b;
        }
    }
}
