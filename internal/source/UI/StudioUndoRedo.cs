using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioUndoRedo : IDisposable
    {
        static readonly List<StudioUndoRedo> bindings = new List<StudioUndoRedo>();
        static bool filterInstalled;
        readonly Form form;
        readonly Func<bool> canUndo, canRedo;
        readonly Action undo, redo;
        internal readonly Button UndoButton, RedoButton;
        internal readonly FlowLayoutPanel Panel;
        bool disposed;

        internal StudioUndoRedo(Form owner, Control host, Func<bool> hasUndo, Func<bool> hasRedo, Action stepBack, Action stepForward)
        {
            form = owner; canUndo = hasUndo; canRedo = hasRedo; undo = stepBack; redo = stepForward;
            Panel = new FlowLayoutPanel { Name = "UndoRedoActions", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            UndoButton = StudioHistorySymbols.Button(new Button { Name = "StudioUndo", Margin = new Padding(3) }, false,
                L.T("Rückgängig (Strg+Z)", "Undo (Ctrl+Z)"));
            RedoButton = StudioHistorySymbols.Button(new Button { Name = "StudioRedo", Margin = new Padding(3) }, true,
                L.T("Wiederholen (Strg+Y / Strg+Umschalt+Z)", "Redo (Ctrl+Y / Ctrl+Shift+Z)"));
            Panel.Controls.Add(UndoButton); Panel.Controls.Add(RedoButton);
            if (host != null) host.Controls.Add(Panel);
            UndoButton.Click += delegate { Execute(false); };
            RedoButton.Click += delegate { Execute(true); };
            DarkTheme.Apply(Panel); StudioUx.Attach(Panel);
            bindings.Add(this);
            if (!filterInstalled) { Application.AddMessageFilter(new ShortcutFilter()); filterInstalled = true; }
            Application.Idle += OnIdle;
            form.Disposed += OwnerDisposed;
            Refresh();
        }

        internal void Refresh()
        {
            if (disposed) return;
            UndoButton.Enabled = canUndo(); RedoButton.Enabled = canRedo();
        }

        void OnIdle(object sender, EventArgs e) { if (StudioUx.CanPaint(form)) Refresh(); }
        void OwnerDisposed(object sender, EventArgs e) { Dispose(); }

        void Execute(bool forward)
        {
            if (disposed || !(forward ? canRedo() : canUndo())) return;
            try { if (forward) redo(); else undo(); }
            catch (Exception error) { StudioMessageBox.Show(form, error.Message, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Refresh(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; bindings.Remove(this);
            Application.Idle -= OnIdle; form.Disposed -= OwnerDisposed;
        }

        internal static bool HandleShortcut(Control focused, Keys keys)
        {
            bool forward = keys == (Keys.Control | Keys.Y) || keys == (Keys.Control | Keys.Shift | Keys.Z);
            if (!forward && keys != (Keys.Control | Keys.Z)) return false;
            for (Control current = focused; current != null; current = current.Parent)
            {
                var owner = current as Form;
                if (owner == null) continue;
                for (int i = bindings.Count - 1; i >= 0; i--)
                {
                    var binding = bindings[i];
                    if (binding.form != owner || !binding.Panel.Visible || !binding.Panel.Enabled) continue;
                    binding.Execute(forward);
                    return true;
                }
            }
            return false;
        }

        sealed class ShortcutFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message message)
            {
                if (message.Msg != 0x100) return false;
                Keys keys = (Keys)(int)message.WParam | Control.ModifierKeys;
                return HandleShortcut(Control.FromChildHandle(message.HWnd), keys);
            }
        }
    }
}
