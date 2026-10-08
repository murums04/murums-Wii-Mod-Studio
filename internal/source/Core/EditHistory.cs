using System;
using System.Collections.Generic;

namespace murumsWiiModStudio
{
    internal sealed class EditHistory<T>
    {
        readonly List<T> states = new List<T>();
        readonly Func<T, T, bool> same;
        readonly int limit;
        int position = -1;
        string group;
        DateTime lastRecord;

        internal EditHistory(Func<T, T, bool> sameState = null, int retainedStates = 24)
        {
            same = sameState ?? EqualityComparer<T>.Default.Equals;
            limit = Math.Max(2, retainedStates);
        }

        internal bool CanUndo { get { return position > 0; } }
        internal bool CanRedo { get { return position >= 0 && position + 1 < states.Count; } }
        internal T Current { get { return position < 0 ? default(T) : states[position]; } }
        internal event EventHandler Changed;

        internal void Reset(T initial)
        {
            states.Clear(); states.Add(initial); position = 0; group = null;
            Notify();
        }

        // Zustände bleiben unveränderlich; veränderbare Daten beim Aufnehmen und Anwenden kopieren.
        internal void Record(T value, string editGroup = null)
        {
            if (position >= 0 && same(states[position], value)) return;
            bool merge = position > 0 && !CanRedo && editGroup != null && editGroup == group
                && (DateTime.UtcNow - lastRecord).TotalMilliseconds < 650;
            if (CanRedo) states.RemoveRange(position + 1, states.Count - position - 1);
            if (merge) states[position] = value;
            else { states.Add(value); position++; }
            while (states.Count > limit) { states.RemoveAt(0); position--; }
            group = editGroup; lastRecord = DateTime.UtcNow;
            Notify();
        }

        internal T Undo()
        {
            if (!CanUndo) return Current;
            group = null; position--; Notify(); return Current;
        }

        internal T Redo()
        {
            if (!CanRedo) return Current;
            group = null; position++; Notify(); return Current;
        }

        void Notify() { if (Changed != null) Changed(this, EventArgs.Empty); }
    }
}
