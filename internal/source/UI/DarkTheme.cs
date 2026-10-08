using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class DarkTheme
    {
        public static Color Back { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(10, 9, 17); } }
        public static Color Panel { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(15, 13, 25); } }
        public static Color Panel2 { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(22, 18, 35); } }
        public static Color Panel3 { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(39, 30, 59); } }
        public static Color Border { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(82, 65, 111); } }
        public static Color Fore { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(238, 239, 246); } }
        public static Color Muted { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(176, 172, 195); } }
        public static Color Disabled { get { return SystemInformation.HighContrast ? SystemColors.GrayText : Color.FromArgb(128, 139, 159); } }
        public static Color Accent { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(139, 92, 246); } }
        public static Color Cyan { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(34, 211, 238); } }
        public static Color CyanSoft { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(17, 60, 72); } }
        public static Color PrimaryInk { get { return SystemInformation.HighContrast ? SystemColors.HighlightText : Color.FromArgb(8, 11, 20); } }
        public static Color Accent2 { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(105, 66, 182); } }
        public static Color AccentSoft { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(64, 39, 99); } }
        public static Color Error { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(242, 155, 155); } }
        public static Color Warning { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(231, 188, 120); } }
        public static Color Success { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(139, 200, 162); } }
        public static Color Navigation { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(11, 10, 20); } }
        public static Color Focus { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(192, 171, 255); } }
        public static Color PrimaryPressed { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(99, 68, 180); } }
        private sealed class Styled { }
        private sealed class TabState { internal int Hover = -1; }
        private sealed class ListState { internal ListViewItem Hover; }
        private static readonly ConditionalWeakTable<Control, Styled> styled = new ConditionalWeakTable<Control, Styled>();
        private static readonly ConditionalWeakTable<TabControl, TabState> tabStates = new ConditionalWeakTable<TabControl, TabState>();
        private static readonly ConditionalWeakTable<ListView, ListState> listStates = new ConditionalWeakTable<ListView, ListState>();
        private static readonly ConditionalWeakTable<ToolStripItem, Styled> primaryTools = new ConditionalWeakTable<ToolStripItem, Styled>();
        private static readonly ConditionalWeakTable<Button, Styled> neutralButtons = new ConditionalWeakTable<Button, Styled>();

        private static bool FirstStyle(Control control)
        {
            Styled marker;
            if (styled.TryGetValue(control, out marker)) return false;
            styled.Add(control, new Styled());
            return true;
        }

        public static void StylePrimary(Button button)
        {
            neutralButtons.Remove(button);
            StudioUx.Attach(button);
            button.BackColor = Accent2;
            button.ForeColor = PrimaryInk;
            button.FlatAppearance.MouseOverBackColor = Accent;
            button.FlatAppearance.MouseDownBackColor = PrimaryPressed;
        }

        public static void StyleNeutral(Button button)
        {
            neutralButtons.GetValue(button, delegate { return new Styled(); });
            StudioUx.Attach(button);
            button.BackColor = Panel2;
            button.ForeColor = Fore;
            button.FlatAppearance.MouseOverBackColor = Panel3;
            button.FlatAppearance.MouseDownBackColor = AccentSoft;
        }

        public static void StylePrimary(ToolStripItem item)
        {
            primaryTools.GetValue(item, delegate { return new Styled(); });
            item.BackColor = Accent2;
            item.ForeColor = ToolForeground(item);
            item.Invalidate();
        }

        public static void StyleNeutral(ToolStripItem item)
        {
            primaryTools.Remove(item);
            item.BackColor = Panel;
            item.ForeColor = ToolForeground(item);
            item.Invalidate();
        }

        internal static bool IsPrimary(ToolStripItem item)
        {
            Styled marker;
            return primaryTools.TryGetValue(item, out marker);
        }

        internal static Color ToolForeground(ToolStripItem item)
        {
            return !item.Enabled ? Disabled : IsPrimary(item) ? PrimaryInk : Fore;
        }

        public static void Apply(Control control)
        {
            if (control == null)
                return;
            murumsWiiModStudio.StudioUx.Attach(control);
            if (SystemInformation.HighContrast || control.ForeColor == SystemColors.ControlText || control.ForeColor == SystemColors.WindowText)
                control.ForeColor = Fore;
            if (SystemInformation.HighContrast)
            {
                bool primary = control is Button && (control.BackColor == Color.FromArgb(105, 66, 182) || control.BackColor == Color.FromArgb(139, 92, 246) || control.BackColor == SystemColors.Highlight);
                control.BackColor = primary ? SystemColors.Highlight : SystemColors.Window;
                if (primary) control.ForeColor = SystemColors.HighlightText;
            }
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
            else if (control is StudioReadOnlyText)
                control.BackColor = Panel2;
            else if (control is TextBox || control is RichTextBox || control is ListBox || control is TreeView)
                control.BackColor = Panel;
            else if (control is TabPage)
                control.BackColor = Back;
            else if (control.BackColor == SystemColors.Control)
                control.BackColor = Back;
            if (control is TreeView) StyleTree((TreeView)control);
            if (control is TabControl) StyleTabs((TabControl)control);
            if (control is PropertyGrid) StylePropertyGrid((PropertyGrid)control);
            if (control is DataGridView) StyleGrid((DataGridView)control);
            var list = control as ListBox;
            if (list != null && !(list is CheckedListBox) && list.DrawMode == DrawMode.Normal) StyleListBox(list);
            var listView = control as ListView;
            if (listView != null) StyleListView(listView);
            var strip = control as ToolStrip;
            if (strip != null) StyleToolStrip(strip, new MurumsDarkToolStripRenderer());
            foreach (Control child in control.Controls)
                Apply(child);
            var window = control as Form;
            Styled neutral;
            if (window != null && window.AcceptButton is Button && window.AcceptButton != window.CancelButton
                && !neutralButtons.TryGetValue((Button)window.AcceptButton, out neutral))
                StylePrimary((Button)window.AcceptButton);
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
                    Color bg = selected ? AccentSoft : Panel2;
                    Color fg = !combo.Enabled ? Disabled : selected && SystemInformation.HighContrast ? SystemColors.HighlightText : Fore;
                    using (SolidBrush b = new SolidBrush(bg))
                        e.Graphics.FillRectangle(b, e.Bounds);
                    string text = combo.Text ?? "";
                    if (e.Index >= 0 && e.Index < combo.Items.Count && combo.Items[e.Index] != null)
                        text = combo.GetItemText(combo.Items[e.Index]);
                    int inset = StudioSurface.Scale(combo, 7);
                    Rectangle textRect = new Rectangle(e.Bounds.Left + inset, e.Bounds.Top, Math.Max(0, e.Bounds.Width - inset * 2), e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, text, combo.Font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    if ((e.State & DrawItemState.Focus) != 0 && StudioSurface.HasKeyboardFocus(combo))
                        e.DrawFocusRectangle();
                };
            }
            // Erst nach DrawMode/ItemHeight aktivieren, damit keine alte Höhe zwischengespeichert wird.
            combo.FormattingEnabled = true;
            if (FirstStyle(combo))
                combo.FontChanged += delegate { UpdateComboMetrics(combo); };
            UpdateComboMetrics(combo);
        }

        static void UpdateComboMetrics(ComboBox combo)
        {
            if (combo.DrawMode == DrawMode.OwnerDrawFixed)
            {
                int itemHeight = Math.Max(StudioSurface.Scale(combo, 24), combo.Font.Height + StudioSurface.Scale(combo, 6));
                if (combo.ItemHeight != itemHeight) combo.ItemHeight = itemHeight;
            }
            if (combo.DropDownStyle != ComboBoxStyle.Simple)
            {
                int height = combo.PreferredHeight;
                if (combo.Height != height) combo.Height = height;
                if (combo.MinimumSize.Height != height) combo.MinimumSize = new Size(combo.MinimumSize.Width, height);
            }
        }

        public static void StyleTree(TreeView tree)
        {
            tree.BackColor = Navigation;
            tree.ForeColor = Fore;
            tree.LineColor = Border;
            tree.BorderStyle = BorderStyle.None;
            tree.HideSelection = false;
            tree.ShowNodeToolTips = true;
            UpdateTreeMetrics(tree);
            tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            if (!FirstStyle(tree)) return;
            tree.FontChanged += delegate { UpdateTreeMetrics(tree); };
            tree.GotFocus += delegate { tree.Invalidate(); };
            tree.LostFocus += delegate { tree.Invalidate(); };
            tree.DrawNode += delegate (object sender, DrawTreeNodeEventArgs e)
            {
                bool selected = e.Node == tree.SelectedNode;
                Color customBack = e.Node == null ? Color.Empty : e.Node.BackColor;
                Color back = selected ? AccentSoft : (customBack.IsEmpty ? tree.BackColor : customBack);
                Color fore = tree.Enabled ? Fore : Disabled;
                Rectangle row = new Rectangle(e.Bounds.X, e.Bounds.Y, Math.Max(0, tree.ClientSize.Width - e.Bounds.X), e.Bounds.Height);
                using (SolidBrush bg = new SolidBrush(back))
                    e.Graphics.FillRectangle(bg, row);
                if (selected)
                    using (var indicator = new SolidBrush(Accent))
                        e.Graphics.FillRectangle(indicator, row.Left, row.Top, 2, row.Height);
                Rectangle text = new Rectangle(row.Left + 5, row.Top, Math.Max(0, row.Width - 9), row.Height);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, text, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                if (selected && StudioSurface.HasKeyboardFocus(tree))
                    ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(row, -3, -2), Focus, back);
            };
        }

        public static void StyleTabs(TabControl tabs)
        {
            if (tabs == null)
                return;
            tabs.ShowToolTips = true;
            tabs.HotTrack = false;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Normal;
            tabs.ItemSize = new Size(0, Math.Max(StudioSurface.Scale(tabs, 32), tabs.Font.Height + StudioSurface.Scale(tabs, 12)));
            tabs.Padding = new Point(StudioSurface.Scale(tabs, 16), StudioSurface.Scale(tabs, 5));
            if (!FirstStyle(tabs)) return;
            var state = new TabState();
            tabStates.Add(tabs, state);
            tabs.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                int hover = -1;
                for (int i = 0; i < tabs.TabCount; i++) if (tabs.GetTabRect(i).Contains(e.Location)) { hover = i; break; }
                if (state.Hover != hover) { state.Hover = hover; tabs.Invalidate(); }
            };
            tabs.MouseLeave += delegate { state.Hover = -1; tabs.Invalidate(); };
            tabs.EnabledChanged += delegate { tabs.Invalidate(); };
            tabs.FontChanged += delegate
            {
                tabs.ItemSize = new Size(0, Math.Max(StudioSurface.Scale(tabs, 32), tabs.Font.Height + StudioSurface.Scale(tabs, 12)));
                tabs.Invalidate();
            };
            tabs.DrawItem += delegate(object sender, DrawItemEventArgs e) { DrawTab(tabs, e.Graphics, e.Index, e.Bounds); };
            tabs.GotFocus += delegate { tabs.Invalidate(); };
            tabs.LostFocus += delegate { tabs.Invalidate(); };
        }

        internal static void DrawTab(TabControl tabs, Graphics graphics, int index, Rectangle bounds)
        {
            if (index < 0 || index >= tabs.TabCount) return;
            bool selected = index == tabs.SelectedIndex;
            TabState state;
            bool hover = tabs.Enabled && tabStates.TryGetValue(tabs, out state) && state.Hover == index;
            bool pressed = hover && (Control.MouseButtons & MouseButtons.Left) != 0;
            Color background = pressed ? Accent2 : selected ? AccentSoft : hover ? Panel3 : Panel;
            using (var brush = new SolidBrush(Panel)) graphics.FillRectangle(brush, bounds);
            Rectangle surface = Rectangle.Inflate(bounds, -StudioSurface.Scale(tabs, 3), -StudioSurface.Scale(tabs, 2));
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = StudioSurface.Shape(surface, StudioSurface.Scale(tabs, 6)))
            using (var brush = new SolidBrush(background)) graphics.FillPath(brush, shape);
            graphics.SmoothingMode = previous;
            Color text = !tabs.Enabled ? Disabled : SystemInformation.HighContrast && (selected || hover) ? SystemColors.HighlightText : selected ? Fore : Muted;
            TextRenderer.DrawText(graphics, tabs.TabPages[index].Text, tabs.Font, bounds, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (selected)
            {
                Rectangle line = new Rectangle(bounds.Left + StudioSurface.Scale(tabs, 8), bounds.Bottom - StudioSurface.Scale(tabs, 3), Math.Max(1, bounds.Width - StudioSurface.Scale(tabs, 16)), StudioSurface.Scale(tabs, 2));
                using (var brush = new LinearGradientBrush(line, Accent, Cyan, LinearGradientMode.Horizontal)) graphics.FillRectangle(brush, line);
                if (StudioSurface.HasKeyboardFocus(tabs))
                    ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -4, -4), Focus, background);
            }
        }

        static void UpdateTreeMetrics(TreeView tree)
        {
            int height = Math.Max(StudioSurface.Scale(tree, 26), tree.Font.Height + StudioSurface.Scale(tree, 8));
            if (tree.ItemHeight != height) tree.ItemHeight = height;
        }

        static void UpdateListMetrics(ListBox list)
        {
            int height = Math.Max(StudioSurface.Scale(list, 28), list.Font.Height + StudioSurface.Scale(list, 8));
            if (list.ItemHeight != height) list.ItemHeight = height;
        }

        public static void StyleListBox(ListBox list)
        {
            if (list == null)
                return;
            list.BackColor = Panel;
            list.ForeColor = Fore;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            UpdateListMetrics(list);
            if (!FirstStyle(list)) return;
            list.FontChanged += delegate { UpdateListMetrics(list); };
            list.DrawItem += delegate (object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= list.Items.Count)
                    return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color bg = selected ? AccentSoft : list.BackColor;
                Color fg = list.Enabled ? Fore : Disabled;
                using (SolidBrush b = new SolidBrush(bg))
                    e.Graphics.FillRectangle(b, e.Bounds);
                Rectangle textRect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
                TextRenderer.DrawText(e.Graphics, list.GetItemText(list.Items[e.Index]), list.Font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
        }

        public static void StyleListView(ListView list)
        {
            if (list == null) return;
            list.BackColor = Panel;
            list.ForeColor = Fore;
            list.BorderStyle = BorderStyle.None;
            list.HideSelection = false;
            if (list.OwnerDraw || (list.View != View.Details && list.View != View.LargeIcon) || !FirstStyle(list)) return;
            var state = new ListState();
            listStates.Add(list, state);
            list.OwnerDraw = true;
            StudioListHeader.Attach(list);
            list.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                ListViewItem next = list.Enabled ? list.GetItemAt(e.X, e.Y) : null;
                if (state.Hover == next) return;
                InvalidateListItem(list, state.Hover);
                state.Hover = next;
                InvalidateListItem(list, next);
            };
            list.MouseLeave += delegate { InvalidateListItem(list, state.Hover); state.Hover = null; };
            list.EnabledChanged += delegate { state.Hover = null; list.Invalidate(); };
            list.GotFocus += delegate { list.Invalidate(); };
            list.LostFocus += delegate { list.Invalidate(); };
            list.ChangeUICues += delegate { list.Invalidate(); };
            list.ItemChecked += delegate(object sender, ItemCheckedEventArgs e) { InvalidateListItem(list, e.Item); };
            list.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
            {
                if (SystemInformation.HighContrast) { e.DrawDefault = true; return; }
                using (var brush = new SolidBrush(Panel2)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, Rectangle.Inflate(e.Bounds, -StudioSurface.Scale(list, 6), 0), list.Enabled ? Fore : Disabled,
                    ListTextFlags(e.Header.TextAlign));
            };
            list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e)
            {
                if (SystemInformation.HighContrast || (list.View != View.Details && list.View != View.LargeIcon)) { e.DrawDefault = true; return; }
                if (list.View == View.Details) return;
                Color background = ListBackground(list, e.Item, state);
                using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
                DrawListImage(e.Graphics, list, e.Item, list.LargeImageList);
                Rectangle label = e.Item.GetBounds(ItemBoundsPortion.Label);
                TextRenderer.DrawText(e.Graphics, e.Item.Text, list.Font, label, ListForeground(list, e.Item.ForeColor),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                DrawListFocus(e.Graphics, list, e.Item, e.Bounds, background);
            };
            list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                if (SystemInformation.HighContrast) { e.DrawDefault = true; return; }
                Color background = ListBackground(list, e.Item, state);
                using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
                Rectangle text = Rectangle.Inflate(e.Bounds, -StudioSurface.Scale(list, 6), 0);
                if (e.ColumnIndex == 0)
                {
                    if (list.CheckBoxes) DrawListCheck(e.Graphics, list, e.Item);
                    DrawListImage(e.Graphics, list, e.Item, list.SmallImageList);
                    Rectangle label = e.Item.GetBounds(ItemBoundsPortion.Label);
                    int left = Math.Max(text.Left, label.Left + StudioSurface.Scale(list, 3));
                    text = new Rectangle(left, text.Top, Math.Max(0, text.Right - left), text.Height);
                }
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, text, ListForeground(list, e.SubItem.ForeColor), ListTextFlags(e.Header.TextAlign));
                DrawListFocus(e.Graphics, list, e.Item, e.Bounds, background);
            };
        }

        static void InvalidateListItem(ListView list, ListViewItem item)
        {
            if (list.IsHandleCreated && !list.IsDisposed && item != null && item.ListView == list) list.Invalidate(item.Bounds);
        }

        static Color ListBackground(ListView list, ListViewItem item, ListState state)
        {
            return item.Selected ? AccentSoft : list.Enabled && state.Hover == item ? Panel3 : list.BackColor;
        }

        static Color ListForeground(ListView list, Color color)
        {
            return !list.Enabled ? Disabled : color.IsEmpty || color == SystemColors.WindowText || color == Color.Black ? Fore : color;
        }

        static TextFormatFlags ListTextFlags(HorizontalAlignment alignment)
        {
            return TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                | (alignment == HorizontalAlignment.Right ? TextFormatFlags.Right : alignment == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
        }

        static void DrawListImage(Graphics graphics, ListView list, ListViewItem item, ImageList images)
        {
            if (images == null) return;
            int index = String.IsNullOrEmpty(item.ImageKey) ? item.ImageIndex : images.Images.IndexOfKey(item.ImageKey);
            if (index < 0 || index >= images.Images.Count) return;
            Rectangle icon = item.GetBounds(ItemBoundsPortion.Icon);
            images.Draw(graphics, icon.Left + Math.Max(0, (icon.Width - images.ImageSize.Width) / 2),
                icon.Top + Math.Max(0, (icon.Height - images.ImageSize.Height) / 2), index);
        }

        static void DrawListFocus(Graphics graphics, ListView list, ListViewItem item, Rectangle bounds, Color background)
        {
            if (item.Focused && StudioSurface.HasKeyboardFocus(list))
                ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -1, -1), Focus, background);
        }

        static void DrawListCheck(Graphics graphics, ListView list, ListViewItem item)
        {
            // Die native State-Image-Breite bestimmt auch den unveraenderten Checkbox-Hitbereich.
            IntPtr images = SendListMessage(list.Handle, 0x1002, (IntPtr)2, IntPtr.Zero);
            int width, height;
            if (images == IntPtr.Zero || !ImageList_GetIconSize(images, out width, out height)) return;
            Rectangle icon = item.GetBounds(ItemBoundsPortion.Icon);
            Rectangle area = new Rectangle(icon.Left - width, item.Bounds.Top, width, item.Bounds.Height);
            int size = Math.Min(StudioSurface.Scale(list, 16), Math.Min(area.Width, area.Height) - 2);
            if (size < 3) return;
            Rectangle glyph = new Rectangle(area.Left + (area.Width - size) / 2, area.Top + (area.Height - size) / 2, size, size);
            var saved = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = StudioSurface.Shape(glyph, StudioSurface.Scale(list, 4)))
            {
                using (var brush = new SolidBrush(!list.Enabled ? Panel : item.Checked ? Accent : Panel2)) graphics.FillPath(brush, shape);
                using (var pen = new Pen(!list.Enabled ? Disabled : item.Checked ? Accent : Border)) graphics.DrawPath(pen, shape);
            }
            if (item.Checked)
                using (var pen = new Pen(list.Enabled ? Color.White : Disabled, Math.Max(2, size / 7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    graphics.DrawLines(pen, new[] { new PointF(glyph.Left + size * .23f, glyph.Top + size * .5f), new PointF(glyph.Left + size * .43f, glyph.Top + size * .7f), new PointF(glyph.Left + size * .77f, glyph.Top + size * .3f) });
            graphics.Restore(saved);
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendListMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("comctl32.dll")] static extern bool ImageList_GetIconSize(IntPtr images, out int width, out int height);

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Panel;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Panel2;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
            StudioTypography.StyleGrid(grid);
            grid.RowHeadersDefaultCellStyle.BackColor = Panel2;
            grid.RowHeadersDefaultCellStyle.ForeColor = Fore;
            grid.DefaultCellStyle.BackColor = Panel;
            grid.DefaultCellStyle.ForeColor = Fore;
            grid.DefaultCellStyle.SelectionBackColor = AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Panel;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
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
            grid.CategoryForeColor = Fore;
        }

        public static void StyleToolStrip(ToolStrip strip, ToolStripRenderer renderer)
        {
            if (strip == null || renderer == null)
                return;
            strip.BackColor = Panel;
            strip.ForeColor = Fore;
            strip.Renderer = renderer;
            strip.ShowItemToolTips = !(strip is MenuStrip || strip is ToolStripDropDown);
            strip.ImageScalingSize = new Size(Math.Max(20, strip.Font.Height + 5), Math.Max(20, strip.Font.Height + 5));
            foreach (ToolStripItem item in strip.Items)
            {
                item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                item.ForeColor = ToolForeground(item);
                ToolStripDropDownItem menuItem = item as ToolStripDropDownItem;
                if (menuItem != null)
                {
                    menuItem.BackColor = IsPrimary(menuItem) ? Accent2 : Panel;
                    menuItem.ForeColor = ToolForeground(menuItem);
                    if (menuItem.DropDownItems.Count > 0)
                        StyleDropDown(menuItem.DropDown, renderer);
                }
            }
            if (FirstStyle(strip))
            {
                strip.FontChanged += delegate { strip.ImageScalingSize = new Size(Math.Max(20, strip.Font.Height + 5), Math.Max(20, strip.Font.Height + 5)); };
                strip.ItemAdded += delegate(object sender, ToolStripItemEventArgs e)
                {
                    e.Item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                    e.Item.ForeColor = ToolForeground(e.Item);
                    var menu = e.Item as ToolStripDropDownItem;
                    if (menu != null) StyleDropDown(menu.DropDown, renderer);
                };
            }
        }

        private static void StyleDropDown(ToolStripDropDown dropDown, ToolStripRenderer renderer)
        {
            if (dropDown == null)
                return;
            dropDown.BackColor = Panel;
            dropDown.ForeColor = Fore;
            dropDown.Renderer = renderer;
            dropDown.ShowItemToolTips = false;
            dropDown.ImageScalingSize = new Size(Math.Max(20, dropDown.Font.Height + 5), Math.Max(20, dropDown.Font.Height + 5));
            dropDown.Padding = new Padding(2);
            foreach (ToolStripItem child in dropDown.Items)
            {
                child.BackColor = IsPrimary(child) ? Accent2 : Panel;
                child.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                child.ForeColor = ToolForeground(child);
                if (!(child is ToolStripSeparator))
                    child.Padding = new Padding(8, 5, 8, 5);
                ToolStripDropDownItem nested = child as ToolStripDropDownItem;
                if (nested != null && nested.DropDownItems.Count > 0)
                    StyleDropDown(nested.DropDown, renderer);
            }
        }
    }

    internal sealed class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(DarkTheme.Panel);
            for (int i = 0; i < TabCount; i++)
                DarkTheme.DrawTab(this, e.Graphics, i, GetTabRect(i));
            StudioComboChrome.TabOverflow(this);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (IsHandleCreated) StudioComboChrome.TabOverflow(this);
        }
    }

    // Vollständig dunkler Renderer. Insbesondere Dropdown-Menüs werden nicht mehr
    // teilweise mit Windows-Systemfarben gezeichnet. Damit bleibt Text auch bei
    // anderen Windows-Themes und DPI-Skalierungen lesbar.
    internal class MurumsDarkToolStripRenderer : ToolStripProfessionalRenderer
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
            if (!(e.ToolStrip is ToolStripDropDown)) return;
            Rectangle r = new Rectangle(0, 0, Math.Max(0, e.ToolStrip.Width - 1), Math.Max(0, e.ToolStrip.Height - 1));
            using (Pen p = new Pen(DarkTheme.Border))
                e.Graphics.DrawRectangle(p, r);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(DarkTheme.Panel))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(1, 1, Math.Max(1, e.Item.Width - 2), Math.Max(1, e.Item.Height - 2));
            Color bg = DarkTheme.Panel;
            using (var background = new SolidBrush(DarkTheme.Panel))
                e.Graphics.FillRectangle(background, new Rectangle(Point.Empty, e.Item.Size));
            if (DarkTheme.IsPrimary(e.Item))
            {
                DrawItemSurface(e, e.Item.Enabled ? DarkTheme.Accent2 : DarkTheme.Panel, e.Item.Enabled && e.Item.Selected);
                return;
            }
            if (!e.Item.Enabled || !(e.Item.Selected || e.Item.Pressed)) return;
            if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed)) bg = DarkTheme.AccentSoft;
            using (var shape = StudioSurface.Shape(r, Math.Max(4, e.Item.Font.Height / 3)))
            {
                using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillPath(b, shape);
                if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed))
                    using (Pen p = new Pen(DarkTheme.Focus)) e.Graphics.DrawPath(p, shape);
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripButton button = e.Item as ToolStripButton;
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            Color bg = DarkTheme.Panel;
            if (!e.Item.Enabled)
                bg = DarkTheme.Panel;
            else if (button != null && (button.Pressed || button.Checked))
                bg = DarkTheme.AccentSoft;
            else if (e.Item.Selected)
                bg = DarkTheme.Panel3;
            DrawItemSurface(e, bg, e.Item.Enabled && e.Item.Selected);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            Color bg = !e.Item.Enabled ? DarkTheme.Panel : e.Item.Pressed ? DarkTheme.AccentSoft : e.Item.Selected ? DarkTheme.Panel3 : DarkTheme.Panel;
            DrawItemSurface(e, bg, e.Item.Enabled && e.Item.Selected);
        }

        static void DrawItemSurface(ToolStripItemRenderEventArgs e, Color color, bool focus)
        {
            using (var shape = StudioSurface.Shape(new Rectangle(1, 1, Math.Max(1, e.Item.Width - 3), Math.Max(1, e.Item.Height - 3)), Math.Max(4, e.Item.Font.Height / 3)))
            {
                if (DarkTheme.IsPrimary(e.Item) && e.Item.Enabled && !SystemInformation.HighContrast)
                {
                    float lift = e.Item.Pressed ? .16f : e.Item.Selected ? .10f : 0;
                    using (var brush = new LinearGradientBrush(shape.GetBounds(), StudioSurface.Mix(DarkTheme.Accent, Color.White, lift), StudioSurface.Mix(DarkTheme.Cyan, Color.White, lift * .6f), LinearGradientMode.Horizontal)) e.Graphics.FillPath(brush, shape);
                }
                else if (e.Item.Enabled && !SystemInformation.HighContrast && (focus || e.Item.Pressed))
                    using (var brush = new LinearGradientBrush(shape.GetBounds(), DarkTheme.AccentSoft, DarkTheme.CyanSoft, LinearGradientMode.Horizontal)) e.Graphics.FillPath(brush, shape);
                else using (var brush = new SolidBrush(DarkTheme.IsPrimary(e.Item) && e.Item.Enabled ? DarkTheme.Accent2 : color)) e.Graphics.FillPath(brush, shape);
                if (focus) using (var pen = new Pen(DarkTheme.Focus)) e.Graphics.DrawPath(pen, shape);
            }
        }

        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
        {
            DrawItemSurface(e, !e.Item.Enabled ? DarkTheme.Panel : e.Item.Pressed ? DarkTheme.AccentSoft : e.Item.Selected ? DarkTheme.Panel3 : DarkTheme.Panel, e.Item.Enabled && e.Item.Selected);
            var split = e.Item as ToolStripSplitButton;
            if (split != null)
            {
                using (var pen = new Pen(DarkTheme.Border)) e.Graphics.DrawLine(pen, split.SplitterBounds.Left, 5, split.SplitterBounds.Left, Math.Max(5, e.Item.Height - 5));
                OnRenderArrow(new ToolStripArrowRenderEventArgs(e.Graphics, e.Item, split.DropDownButtonBounds, DarkTheme.ToolForeground(e.Item), ArrowDirection.Down));
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle bounds = e.ImageRectangle;
            if (bounds.Width < 4 || bounds.Height < 4) bounds = new Rectangle(5, Math.Max(1, (e.Item.Height - 16) / 2), 16, 16);
            using (var shape = StudioSurface.Shape(bounds, 4))
            {
                using (var brush = new SolidBrush(e.Item.Enabled ? DarkTheme.AccentSoft : DarkTheme.Panel2)) e.Graphics.FillPath(brush, shape);
                using (var pen = new Pen(e.Item.Enabled ? DarkTheme.Accent : DarkTheme.Disabled)) e.Graphics.DrawPath(pen, shape);
            }
            using (var pen = new Pen(e.Item.Enabled ? SystemInformation.HighContrast ? SystemColors.HighlightText : DarkTheme.Fore : DarkTheme.Disabled, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                e.Graphics.DrawLines(pen, new[] { new PointF(bounds.Left + bounds.Width * .2f, bounds.Top + bounds.Height * .5f), new PointF(bounds.Left + bounds.Width * .43f, bounds.Top + bounds.Height * .7f), new PointF(bounds.Left + bounds.Width * .8f, bounds.Top + bounds.Height * .3f) });
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = DarkTheme.ToolForeground(e.Item);
            if (e.Item.Enabled && !DarkTheme.IsPrimary(e.Item) && SystemInformation.HighContrast && (e.Item.Selected || e.Item.Pressed)) e.TextColor = SystemColors.HighlightText;
            if (StudioHistorySymbols.IsArrow(e.Text))
            {
                StudioHistorySymbols.Draw(e.Graphics, e.Text, e.TextFont, new Rectangle(Point.Empty, e.Item.Size), e.TextColor);
                return;
            }
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
            e.ArrowColor = DarkTheme.ToolForeground(e.Item);
            if (e.Item.Enabled && !DarkTheme.IsPrimary(e.Item) && SystemInformation.HighContrast && (e.Item.Selected || e.Item.Pressed)) e.ArrowColor = SystemColors.HighlightText;
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
