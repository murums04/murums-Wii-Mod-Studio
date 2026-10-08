using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace murumsWiiModStudio
{
    internal static class StudioHistorySymbols
    {
        public const string Undo = "↶", Redo = "↷";
        static readonly Font ArrowFont = new Font("Segoe UI Symbol", 16F, FontStyle.Regular);
        internal static bool IsArrow(string text) { return text == Undo || text == Redo; }

        internal static void Draw(Graphics graphics, string symbol, Font font, Rectangle bounds, Color colour)
        {
            using (var path = new GraphicsPath())
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                path.AddString(symbol, font.FontFamily, (int)font.Style, font.SizeInPoints * graphics.DpiY / 72f, Point.Empty, format);
                RectangleF ink = path.GetBounds();
                using (var transform = new Matrix())
                {
                    transform.Translate(bounds.Left + (bounds.Width - ink.Width) / 2f - ink.Left,
                        bounds.Top + (bounds.Height - ink.Height) / 2f - ink.Top);
                    path.Transform(transform);
                }
                var smoothing = graphics.SmoothingMode;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(colour)) graphics.FillPath(brush, path);
                graphics.SmoothingMode = smoothing;
            }
        }
        public static Button Button(Button button, bool redo, string description)
        {
            button.Text = redo ? Redo : Undo;
            button.AutoSize = false;
            button.AutoEllipsis = false;
            button.UseMnemonic = false;
            button.MinimumSize = new Size(40, 34);
            button.MaximumSize = Size.Empty;
            button.Size = new Size(44, 36);
            button.Font = ArrowFont;
            button.Padding = Padding.Empty;
            button.AccessibleName = description;
            StudioUx.SetHelp(button, description);
            return button;
        }

        public static ToolStripButton Tool(ToolStripButton button, bool redo, string description)
        {
            button.Text = redo ? Redo : Undo;
            button.Font = ArrowFont;
            button.ToolTipText = description;
            button.AccessibleName = description;
            return button;
        }
    }
}
