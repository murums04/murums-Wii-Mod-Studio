using System;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioEditor
    {
        internal static void Open(Form owner, Form editor, Action<DialogResult> completed)
        {
            var main = owner == null ? null : owner.TopLevelControl as MainForm;
            if (main != null) { main.ShowEditor(editor, completed); return; }
            using (editor)
            {
                var result = editor.ShowDialog(owner);
                if (completed != null) completed(result);
            }
        }
    }
}
