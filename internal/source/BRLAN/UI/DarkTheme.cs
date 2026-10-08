using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal static class DarkTheme
    {
        public static readonly Color Back = murumsWiiModStudio.DarkTheme.Back;
        public static readonly Color Panel = murumsWiiModStudio.DarkTheme.Panel;
        public static readonly Color Panel2 = murumsWiiModStudio.DarkTheme.Panel2;
        public static readonly Color Panel3 = murumsWiiModStudio.DarkTheme.Panel3;
        public static readonly Color Border = murumsWiiModStudio.DarkTheme.Border;
        public static readonly Color Fore = murumsWiiModStudio.DarkTheme.Fore;
        public static readonly Color Muted = murumsWiiModStudio.DarkTheme.Muted;
        public static readonly Color Disabled = murumsWiiModStudio.DarkTheme.Disabled;
        public static readonly Color Accent = murumsWiiModStudio.DarkTheme.Accent;
        public static readonly Color Accent2 = murumsWiiModStudio.DarkTheme.Accent2;
        public static readonly Color AccentSoft = murumsWiiModStudio.DarkTheme.AccentSoft;
        public static readonly Color Error = murumsWiiModStudio.DarkTheme.Error;
        public static readonly Color Warning = murumsWiiModStudio.DarkTheme.Warning;
        public static readonly Color Success = murumsWiiModStudio.DarkTheme.Success;

        public static void Apply(Control control)
        {
            murumsWiiModStudio.DarkTheme.Apply(control);
        }

            // Windows otherwise paints the opened drop-down with a light system
            // background while keeping our light foreground, making items almost
            // unreadable. Owner-draw every normal ComboBox once so both the closed
            // field and the drop-down use the same dark palette.

        public static void StyleComboBox(ComboBox combo)
        {
            murumsWiiModStudio.DarkTheme.StyleComboBox(combo);
        }

        public static void StyleTree(TreeView tree)
        {
            murumsWiiModStudio.DarkTheme.StyleTree(tree);
        }

        public static void StyleTabs(TabControl tabs)
        {
            murumsWiiModStudio.DarkTheme.StyleTabs(tabs);
        }

        public static void StyleListBox(ListBox list)
        {
            murumsWiiModStudio.DarkTheme.StyleListBox(list);
        }

        public static void StyleGrid(DataGridView grid)
        {
            murumsWiiModStudio.DarkTheme.StyleGrid(grid);
        }

        public static void StylePropertyGrid(PropertyGrid grid)
        {
            murumsWiiModStudio.DarkTheme.StylePropertyGrid(grid);
        }

        public static void StyleToolStrip(ToolStrip strip, ToolStripRenderer renderer)
        {
            murumsWiiModStudio.DarkTheme.StyleToolStrip(strip, renderer);
        }
    }

    // Vollständig dunkler Renderer. Insbesondere Dropdown-Menüs werden nicht mehr
    // teilweise mit Windows-Systemfarben gezeichnet. Damit bleibt Text auch bei
    // anderen Windows-Themes und DPI-Skalierungen lesbar.
    internal sealed class MurumsDarkToolStripRenderer : murumsWiiModStudio.MurumsDarkToolStripRenderer { }
}
