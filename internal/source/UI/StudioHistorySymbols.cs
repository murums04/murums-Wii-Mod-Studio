using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioHistorySymbols
    {
        public const string Undo = "↶", Redo = "↷";
        public static Button Button(Button button, bool redo, string description)
        {
            button.Text = redo ? Redo : Undo;
            button.AutoSize = false;
            button.Size = new Size(44, 36);
            button.Padding = Padding.Empty;
            button.AccessibleName = description;
            StudioUx.SetHelp(button, description);
            return button;
        }

        public static ToolStripButton Tool(ToolStripButton button, bool redo, string description)
        {
            button.Text = redo ? Redo : Undo;
            button.ToolTipText = description;
            button.AccessibleName = description;
            return button;
        }
    }
}
