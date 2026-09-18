using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal static class DarkTheme
    {
        public static readonly Color Back = Color.FromArgb(20, 21, 26);
        public static readonly Color Panel = Color.FromArgb(27, 29, 36);
        public static readonly Color Panel2 = Color.FromArgb(34, 37, 46);
        public static readonly Color Panel3 = Color.FromArgb(42, 45, 56);
        public static readonly Color Border = Color.FromArgb(66, 71, 87);
        public static readonly Color Fore = Color.FromArgb(245, 247, 252);
        public static readonly Color Muted = Color.FromArgb(190, 196, 210);
        public static readonly Color Disabled = Color.FromArgb(132, 138, 151);
        public static readonly Color Accent = Color.FromArgb(139, 92, 246);
        public static readonly Color Accent2 = Color.FromArgb(103, 72, 190);
        public static readonly Color AccentSoft = Color.FromArgb(73, 55, 117);
        public static readonly Color Error = Color.FromArgb(244, 96, 96);
        public static readonly Color Warning = Color.FromArgb(245, 189, 74);
        public static readonly Color Success = Color.FromArgb(103, 209, 138);
        public static void Apply(Control control)
        {
            if (control == null)
                return;
            murumsWiiModStudio.StudioUx.Attach(control);
            control.ForeColor = Fore;
            ComboBox combo = control as ComboBox;
            if (combo != null)
                StyleComboBox(combo);
            NumericUpDown numeric = control as NumericUpDown;
            if (numeric != null)
            {
                numeric.BackColor = Panel;
                numeric.ForeColor = Fore;
                numeric.BorderStyle = BorderStyle.FixedSingle;
            }

            if (control is Form)
                control.BackColor = Back;
            else if (control is TextBox || control is RichTextBox || control is ListBox || control is TreeView)
                control.BackColor = Panel;
            else if (control is TabPage)
                control.BackColor = Back;
            else if (control.BackColor == SystemColors.Control)
                control.BackColor = Back;
            foreach (Control child in control.Controls)
                Apply(child);
        }

        public static void StyleComboBox(ComboBox combo)
        {
            if (combo == null)
                return;
            combo.BackColor = Panel;
            combo.ForeColor = Fore;
            combo.FlatStyle = FlatStyle.Flat;
            // Windows otherwise paints the opened drop-down with a light system
            // background while keeping our light foreground, making items almost
            // unreadable. Owner-draw every normal ComboBox once so both the closed
            // field and the drop-down use the same dark palette.
            if (combo.DrawMode == DrawMode.Normal)
            {
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                if (combo.ItemHeight < 24)
                    combo.ItemHeight = 24;
                combo.DrawItem += delegate (object sender, DrawItemEventArgs e)
                {
                    bool selected = (e.State & DrawItemState.Selected) != 0;
                    Color bg = selected ? Accent2 : Panel;
                    Color fg = combo.Enabled ? Fore : Disabled;
                    using (SolidBrush b = new SolidBrush(bg))
                        e.Graphics.FillRectangle(b, e.Bounds);
                    string text = combo.Text ?? "";
                    if (e.Index >= 0 && e.Index < combo.Items.Count && combo.Items[e.Index] != null)
                        text = combo.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.Left + 7, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 11), e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, text, combo.Font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    if ((e.State & DrawItemState.Focus) != 0)
                        e.DrawFocusRectangle();
                };
            }
        }

        public static void StyleTree(TreeView tree)
        {
            tree.BackColor = Panel;
            tree.ForeColor = Fore;
            tree.LineColor = Border;
            tree.BorderStyle = BorderStyle.None;
            tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            tree.DrawNode += delegate (object sender, DrawTreeNodeEventArgs e)
            {
                bool selected = (e.State & TreeNodeStates.Selected) != 0;
                Color customBack = e.Node == null ? Color.Empty : e.Node.BackColor;
                Color back = selected ? Accent2 : (customBack.IsEmpty ? Panel : customBack);
                Color fore = Fore;
                Rectangle row = new Rectangle(e.Bounds.X, e.Bounds.Y, Math.Max(0, tree.ClientSize.Width - e.Bounds.X), e.Bounds.Height);
                using (SolidBrush bg = new SolidBrush(back))
                    e.Graphics.FillRectangle(bg, row);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, e.Bounds, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
        }

        public static void StyleTabs(TabControl tabs)
        {
            if (tabs == null)
                return;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(145, 36);
            tabs.Padding = new Point(16, 6);
            tabs.DrawItem += delegate (object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= tabs.TabPages.Count)
                    return;
                bool selected = e.Index == tabs.SelectedIndex;
                Rectangle r = e.Bounds;
                Color bg = selected ? Accent2 : Panel2;
                using (SolidBrush b = new SolidBrush(bg))
                    e.Graphics.FillRectangle(b, r);
                using (Pen border = new Pen(Border))
                    e.Graphics.DrawRectangle(border, r.Left, r.Top, Math.Max(0, r.Width - 1), Math.Max(0, r.Height - 1));
                if (selected)
                {
                    using (SolidBrush a = new SolidBrush(Accent))
                        e.Graphics.FillRectangle(a, new Rectangle(r.Left, r.Bottom - 3, r.Width, 3));
                }

                string text = tabs.TabPages[e.Index].Text;
                TextRenderer.DrawText(e.Graphics, text, tabs.Font, r, Fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
        }

        public static void StyleListBox(ListBox list)
        {
            if (list == null)
                return;
            list.BackColor = Panel;
            list.ForeColor = Fore;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            if (list.ItemHeight < 26)
                list.ItemHeight = 28;
            list.DrawItem += delegate (object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= list.Items.Count)
                    return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color bg = selected ? Accent2 : list.BackColor;
                Color fg = list.Enabled ? Fore : Disabled;
                using (SolidBrush b = new SolidBrush(bg))
                    e.Graphics.FillRectangle(b, e.Bounds);
                Rectangle textRect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), list.Font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Panel;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Panel2;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grid.RowHeadersDefaultCellStyle.BackColor = Panel2;
            grid.RowHeadersDefaultCellStyle.ForeColor = Fore;
            grid.DefaultCellStyle.BackColor = Panel;
            grid.DefaultCellStyle.ForeColor = Fore;
            grid.DefaultCellStyle.SelectionBackColor = Accent2;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(30, 32, 40);
        }

        public static void StylePropertyGrid(PropertyGrid grid)
        {
            grid.BackColor = Panel;
            grid.ViewBackColor = Panel;
            grid.ViewForeColor = Fore;
            grid.HelpBackColor = Panel2;
            grid.HelpForeColor = Fore;
            grid.CommandsBackColor = Panel2;
            grid.CommandsForeColor = Fore;
            grid.LineColor = Border;
            grid.CategoryForeColor = Color.FromArgb(218, 211, 255);
            grid.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        }

        public static void StyleToolStrip(ToolStrip strip, ToolStripRenderer renderer)
        {
            if (strip == null || renderer == null)
                return;
            strip.BackColor = Panel;
            strip.ForeColor = Fore;
            strip.Renderer = renderer;
            foreach (ToolStripItem item in strip.Items)
            {
                item.ForeColor = item.Enabled ? Fore : Disabled;
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem != null)
                {
                    menuItem.BackColor = Panel;
                    menuItem.ForeColor = Fore;
                    if (menuItem.DropDownItems.Count > 0)
                        StyleDropDown(menuItem.DropDown, renderer);
                }
            }
        }

        private static void StyleDropDown(ToolStripDropDown dropDown, ToolStripRenderer renderer)
        {
            if (dropDown == null)
                return;
            dropDown.BackColor = Panel;
            dropDown.ForeColor = Fore;
            dropDown.Renderer = renderer;
            dropDown.Padding = new Padding(2);
            foreach (ToolStripItem child in dropDown.Items)
            {
                child.BackColor = Panel;
                child.ForeColor = child.Enabled ? Fore : Disabled;
                if (!(child is ToolStripSeparator))
                    child.Padding = new Padding(8, 5, 8, 5);
                ToolStripMenuItem nested = child as ToolStripMenuItem;
                if (nested != null && nested.DropDownItems.Count > 0)
                    StyleDropDown(nested.DropDown, renderer);
            }
        }
    }

    internal sealed class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
        }
    }

    // Vollständig dunkler Renderer. Insbesondere Dropdown-Menüs werden nicht mehr
    // teilweise mit Windows-Systemfarben gezeichnet. Damit bleibt Text auch bei
    // anderen Windows-Themes und DPI-Skalierungen lesbar.
    internal sealed class MurumsDarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public MurumsDarkToolStripRenderer() : base(new MurumsDarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(DarkTheme.Panel))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = new Rectangle(0, 0, Math.Max(0, e.ToolStrip.Width - 1), Math.Max(0, e.ToolStrip.Height - 1));
            using (Pen p = new Pen(DarkTheme.Border))
                e.Graphics.DrawRectangle(p, r);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(DarkTheme.Panel2))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            Color bg = DarkTheme.Panel;
            if (e.Item.Selected || e.Item.Pressed)
                bg = DarkTheme.Accent2;
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, r);
            if (e.Item.Selected || e.Item.Pressed)
            {
                using (Pen p = new Pen(DarkTheme.Accent))
                    e.Graphics.DrawRectangle(p, 0, 0, Math.Max(0, r.Width - 1), Math.Max(0, r.Height - 1));
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripButton button = e.Item as ToolStripButton;
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            Color bg = DarkTheme.Panel;
            if (button != null && (button.Pressed || button.Checked))
                bg = DarkTheme.Accent2;
            else if (e.Item.Selected)
                bg = DarkTheme.Panel3;
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, r);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            Color bg = e.Item.Selected ? DarkTheme.Panel3 : DarkTheme.Panel;
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, r);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? DarkTheme.Fore : DarkTheme.Disabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            int left = e.Vertical ? e.Item.Width / 2 : 8;
            int right = e.Vertical ? left : Math.Max(left, e.Item.Width - 8);
            using (Pen p = new Pen(DarkTheme.Border))
            {
                if (e.Vertical)
                    e.Graphics.DrawLine(p, left, 4, left, Math.Max(4, e.Item.Height - 4));
                else
                    e.Graphics.DrawLine(p, left, y, right, y);
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? DarkTheme.Fore : DarkTheme.Disabled;
            base.OnRenderArrow(e);
        }
    }

    internal sealed class MurumsDarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color ToolStripGradientMiddle
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color ToolStripGradientEnd
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color ToolStripContentPanelGradientBegin
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color ToolStripContentPanelGradientEnd
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color MenuStripGradientBegin
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color MenuStripGradientEnd
        {
            get
            {
                return DarkTheme.Panel;
            }
        }

        public override Color StatusStripGradientBegin
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color StatusStripGradientEnd
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color ImageMarginGradientBegin
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color ImageMarginGradientMiddle
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color ImageMarginGradientEnd
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color MenuItemSelected
        {
            get
            {
                return DarkTheme.Accent2;
            }
        }

        public override Color MenuItemBorder
        {
            get
            {
                return DarkTheme.Accent;
            }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get
            {
                return DarkTheme.Accent2;
            }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get
            {
                return DarkTheme.Accent2;
            }
        }

        public override Color MenuItemPressedGradientBegin
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color MenuItemPressedGradientMiddle
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color MenuItemPressedGradientEnd
        {
            get
            {
                return DarkTheme.Panel2;
            }
        }

        public override Color ButtonSelectedGradientBegin
        {
            get
            {
                return DarkTheme.Panel3;
            }
        }

        public override Color ButtonSelectedGradientMiddle
        {
            get
            {
                return DarkTheme.Panel3;
            }
        }

        public override Color ButtonSelectedGradientEnd
        {
            get
            {
                return DarkTheme.Panel3;
            }
        }

        public override Color ButtonPressedGradientBegin
        {
            get
            {
                return DarkTheme.Accent2;
            }
        }

        public override Color ButtonPressedGradientMiddle
        {
            get
            {
                return DarkTheme.Accent2;
            }
        }

        public override Color ButtonPressedGradientEnd
        {
            get
            {
                return DarkTheme.Accent2;
            }
        }

        public override Color SeparatorDark
        {
            get
            {
                return DarkTheme.Border;
            }
        }

        public override Color SeparatorLight
        {
            get
            {
                return DarkTheme.Border;
            }
        }

        public override Color ToolStripBorder
        {
            get
            {
                return DarkTheme.Border;
            }
        }

        public override Color ToolStripDropDownBackground
        {
            get
            {
                return DarkTheme.Panel;
            }
        }
    }
}
