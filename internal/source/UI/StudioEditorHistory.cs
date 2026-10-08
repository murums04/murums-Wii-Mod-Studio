using System;
using System.Collections.Generic;

namespace murumsWiiModStudio
{
    // Diese Editoren ersetzen Ressourcenpuffer durch neue Ergebnisse.
    internal sealed class SpecialEditorHistory
    {
        readonly System.Windows.Forms.Form owner;
        readonly Func<object[]> capture;
        readonly Action<object[]> restore;
        readonly Func<string> source;
        readonly Func<bool> enabled;
        readonly HashSet<System.Windows.Forms.Control> watched = new HashSet<System.Windows.Forms.Control>();
        readonly EditHistory<object[]> history = new EditHistory<object[]>(Same, 12);
        string sourceKey;
        bool pendingObservation;
        string pendingGroup;
        internal bool Restoring { get; private set; }
        internal readonly StudioUndoRedo Binding;
        internal SpecialEditorHistory(System.Windows.Forms.Form form, System.Windows.Forms.Control host,
            Func<object[]> read, Action<object[]> write, Func<string> sourceIdentity = null, Func<bool> isEnabled = null)
        {
            owner = form; capture = read; restore = write; source = sourceIdentity; enabled = isEnabled ?? delegate { return true; };
            Reset();
            Watch(form);
            Binding = new StudioUndoRedo(form, host,
                delegate { ObservePending(); return enabled() && (history.CanUndo || HasGridEdit(owner)); }, delegate { ObservePending(); return enabled() && history.CanRedo; },
                delegate { Step(false); }, delegate { Step(true); });
        }
        internal void Reset()
        {
            sourceKey = source == null ? "" : source(); pendingGroup = null; history.Reset(capture());
        }
        void Watch(System.Windows.Forms.Control control)
        {
            if ((control is System.Windows.Forms.Form && control != owner) || !watched.Add(control)) return;
            string identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(control).ToString();
            EventHandler changed = delegate { pendingObservation = true; pendingGroup = null; };
            EventHandler typed = delegate { pendingObservation = true; pendingGroup = "field:" + identity; };
            var button = control as System.Windows.Forms.Button; if (button != null) button.Click += changed;
            var number = control as System.Windows.Forms.NumericUpDown; if (number != null) number.ValueChanged += typed;
            var check = control as System.Windows.Forms.CheckBox; if (check != null) check.CheckedChanged += changed;
            var choice = control as System.Windows.Forms.ComboBox; if (choice != null) choice.SelectedIndexChanged += changed;
            var text = control as System.Windows.Forms.TextBox; if (text != null && !text.ReadOnly) text.TextChanged += typed;
            var list = control as System.Windows.Forms.ListBox; if (list != null) list.SelectedIndexChanged += changed;
            var checks = control as System.Windows.Forms.CheckedListBox;
            if (checks != null) checks.ItemCheck += delegate { pendingObservation = true; pendingGroup = null; };
            var grid = control as System.Windows.Forms.DataGridView;
            if (grid != null) grid.CellEndEdit += delegate(object sender, System.Windows.Forms.DataGridViewCellEventArgs e) {
                pendingObservation = true; pendingGroup = "cell:" + identity + ":" + e.RowIndex + ":" + e.ColumnIndex;
            };
            control.ControlAdded += delegate(object sender, System.Windows.Forms.ControlEventArgs e) { Watch(e.Control); };
            control.Disposed += delegate { watched.Remove(control); };
            foreach (System.Windows.Forms.Control child in control.Controls) Watch(child);
        }
        void ObservePending() { if (pendingObservation) Observe(); }
        internal void Observe()
        {
            var active = System.Windows.Forms.Form.ActiveForm;
            var top = owner.TopLevelControl as System.Windows.Forms.Form ?? owner;
            if (Restoring || !enabled() || !owner.Enabled || owner.IsDisposed || (active != null && active != top)) return;
            pendingObservation = false;
            string key = source == null ? "" : source();
            if (key != sourceKey) { Reset(); return; }
            string group = pendingGroup; pendingGroup = null;
            history.Record(capture(), group);
        }
        bool HasGridEdit(System.Windows.Forms.Control control)
        {
            if (control is System.Windows.Forms.Form && control != owner) return false;
            var grid = control as System.Windows.Forms.DataGridView;
            if (grid != null && grid.IsCurrentCellDirty && grid.CurrentCell != null
                && !Object.Equals(grid.CurrentCell.EditedFormattedValue, grid.CurrentCell.FormattedValue)) return true;
            foreach (System.Windows.Forms.Control child in control.Controls) if (HasGridEdit(child)) return true;
            return false;
        }
        bool EndGridEdits(System.Windows.Forms.Control control)
        {
            if (control is System.Windows.Forms.Form && control != owner) return true;
            var grid = control as System.Windows.Forms.DataGridView;
            if (grid != null && !grid.EndEdit()) return false;
            foreach (System.Windows.Forms.Control child in control.Controls) if (!EndGridEdits(child)) return false;
            return true;
        }
        void Step(bool forward)
        {
            if (!enabled() || !EndGridEdits(owner)) return;
            Observe(); if (!(forward ? history.CanRedo : history.CanUndo)) return;
            Restoring = true;
            try { restore(forward ? history.Redo() : history.Undo()); }
            finally { Restoring = false; pendingObservation = false; }
        }
        internal static Dictionary<K, V> Copy<K, V>(Dictionary<K, V> values) { return new Dictionary<K, V>(values, values.Comparer); }
        internal static void Replace<K, V>(Dictionary<K, V> target, Dictionary<K, V> values)
        {
            target.Clear(); foreach (var pair in values) target.Add(pair.Key, pair.Value);
        }
        internal static bool Same(object[] left, object[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (!ValueSame(left[i], right[i])) return false;
            return true;
        }
        static bool ValueSame(object left, object right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            var a = left as System.Collections.IDictionary; var b = right as System.Collections.IDictionary;
            if (a != null && b != null)
            {
                if (a.Count != b.Count) return false;
                foreach (System.Collections.DictionaryEntry entry in a)
                    if (!b.Contains(entry.Key) || !ValueSame(entry.Value, b[entry.Key])) return false;
                return true;
            }
            var x = left as object[]; var y = right as object[];
            if (x != null && y != null) return Same(x, y);
            return left.Equals(right);
        }
    }
}
