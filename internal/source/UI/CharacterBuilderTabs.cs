using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class CharacterBuilderTabs : TabControl
    {
        internal CharacterBuilderTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(DarkTheme.Back);
            for (int i = 0; i < TabCount; i++)
            {
                Rectangle bounds = GetTabRect(i);
                using (var brush = new SolidBrush(i == SelectedIndex ? DarkTheme.Accent2 : DarkTheme.Panel2))
                    e.Graphics.FillRectangle(brush, bounds);
                using (var pen = new Pen(DarkTheme.Border))
                    e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, bounds, DarkTheme.Fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (Focused && i == SelectedIndex)
                    ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -4, -4));
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }
    }
}
