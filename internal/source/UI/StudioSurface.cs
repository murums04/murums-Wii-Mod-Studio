using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioSurface
    {
        sealed class InputState
        {
            internal bool Hover, Pressed, KeyboardPressed;
            internal float HoverAmount, HoverFrom, HoverTo;
            internal long HoverStarted;
        }
        static readonly ConditionalWeakTable<ButtonBase, InputState> inputs = new ConditionalWeakTable<ButtonBase, InputState>();
        static readonly HashSet<ButtonBase> transitions = new HashSet<ButtonBase>();
        static Timer hoverTimer;
        static bool exitRegistered;
        static readonly PropertyInfo doubleBuffered = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly PropertyInfo showFocusCues = typeof(Control).GetProperty("ShowFocusCues", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void BufferContainer(Control control)
        {
            if (control is Panel || control is UserControl || control is TabPage)
                doubleBuffered.SetValue(control, true, null);
        }

        internal static int Scale(Control control, int value)
        {
            return Math.Max(1, (int)Math.Round(value * control.DeviceDpi / 96.0));
        }

        internal static bool HasKeyboardFocus(Control control)
        {
            return control.Focused && HasVisibleFocus(control);
        }

        internal static bool HasKeyboardFocusWithin(Control control)
        {
            return control.ContainsFocus && HasVisibleFocus(control);
        }

        static bool HasVisibleFocus(Control control)
        {
            return control.Enabled && (bool)showFocusCues.GetValue(control, null);
        }

        internal static void Track(ButtonBase button)
        {
            InputState state;
            if (inputs.TryGetValue(button, out state)) return;
            state = new InputState();
            inputs.Add(button, state);
            button.MouseEnter += delegate { ChangeHover(button, state, true); };
            button.MouseLeave += delegate { ChangeHover(button, state, false); };
            button.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) state.Pressed = true; button.Invalidate(); };
            button.MouseUp += delegate { state.Pressed = false; button.Invalidate(); };
            button.MouseCaptureChanged += delegate { if (!button.Capture) state.Pressed = false; button.Invalidate(); };
            button.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Space) state.KeyboardPressed = true; button.Invalidate(); };
            button.KeyUp += delegate { state.KeyboardPressed = false; button.Invalidate(); };
            button.GotFocus += delegate { button.Invalidate(); };
            button.ChangeUICues += delegate { button.Invalidate(); };
            button.LostFocus += delegate { state.KeyboardPressed = false; button.Invalidate(); };
            button.EnabledChanged += delegate
            {
                if (!button.Enabled)
                {
                    state.Pressed = false; state.KeyboardPressed = false; state.Hover = false;
                    state.HoverAmount = state.HoverTo = 0;
                    RemoveTransition(button);
                }
                button.Invalidate();
            };
            button.VisibleChanged += delegate
            {
                if (!StudioUx.CanPaint(button))
                {
                    state.Hover = false; state.HoverAmount = state.HoverTo = 0;
                    RemoveTransition(button);
                }
            };
            button.Disposed += delegate { RemoveTransition(button); };
        }

        static void ChangeHover(ButtonBase button, InputState state, bool hover)
        {
            state.Hover = hover;
            float target = button.Enabled && hover ? 1 : 0;
            if (!StudioUx.CanPaint(button) || !StudioWindowLayout.MotionEnabled)
            {
                state.HoverAmount = state.HoverTo = target;
                RemoveTransition(button);
                if (StudioUx.CanPaint(button)) button.Invalidate();
                return;
            }
            state.HoverFrom = state.HoverAmount;
            state.HoverTo = target;
            state.HoverStarted = Stopwatch.GetTimestamp();
            if (state.HoverFrom == target) { RemoveTransition(button); return; }
            transitions.Add(button);
            if (hoverTimer == null)
            {
                hoverTimer = new Timer { Interval = 16 };
                hoverTimer.Tick += HoverFrame;
                if (!exitRegistered)
                {
                    exitRegistered = true;
                    Application.ApplicationExit += delegate
                    {
                        Timer current = hoverTimer;
                        hoverTimer = null;
                        if (current != null) { current.Stop(); current.Dispose(); }
                        transitions.Clear();
                    };
                }
            }
            hoverTimer.Start();
        }

        static void RemoveTransition(ButtonBase button)
        {
            transitions.Remove(button);
            if (transitions.Count == 0 && hoverTimer != null) hoverTimer.Stop();
        }

        static void HoverFrame(object sender, EventArgs e)
        {
            bool motion = StudioWindowLayout.MotionEnabled;
            long now = Stopwatch.GetTimestamp();
            var active = new ButtonBase[transitions.Count];
            transitions.CopyTo(active);
            foreach (ButtonBase button in active)
            {
                InputState state;
                if (!inputs.TryGetValue(button, out state) || !StudioUx.CanPaint(button))
                {
                    if (state != null) { state.HoverAmount = state.HoverTo = 0; state.Hover = false; }
                    RemoveTransition(button);
                    continue;
                }
                float time = motion ? Math.Min(1f, (float)((now - state.HoverStarted) * 1000.0 / Stopwatch.Frequency / 160.0)) : 1;
                float ease = time * time * (3 - 2 * time);
                state.HoverAmount = state.HoverFrom + (state.HoverTo - state.HoverFrom) * ease;
                if (time >= 1) RemoveTransition(button);
                button.Invalidate();
            }
        }

        internal static Color Mix(Color from, Color to, float amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb((int)Math.Round(from.A + (to.A - from.A) * amount), (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount), (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        static float HoverAmount(ButtonBase button)
        {
            InputState state;
            return button.Enabled && inputs.TryGetValue(button, out state) ? state.HoverAmount : 0;
        }

        internal static bool IsPrimary(Button button)
        {
            return button.BackColor == DarkTheme.Accent2 || button.BackColor == DarkTheme.Accent;
        }

        internal static Color Foreground(Button button)
        {
            if (!button.Enabled) return DarkTheme.Disabled;
            if (IsPrimary(button)) return DarkTheme.PrimaryInk;
            if (SystemInformation.HighContrast && (HoverAmount(button) > 0 || Pressed(button))) return SystemColors.HighlightText;
            return button.ForeColor == DarkTheme.PrimaryInk ? DarkTheme.Fore : button.ForeColor;
        }

        static bool Pressed(ButtonBase button)
        {
            InputState state;
            return button.Enabled && inputs.TryGetValue(button, out state) && (state.KeyboardPressed || state.Pressed && state.Hover);
        }

        internal static GraphicsPath Shape(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter < 2) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void ButtonBackground(Button button, Graphics graphics)
        {
            Color surrounding = button.Parent == null ? DarkTheme.Back : button.Parent.BackColor;
            graphics.Clear(surrounding.A == 0 ? DarkTheme.Back : surrounding);
            float hover = HoverAmount(button);
            bool pressed = Pressed(button);
            bool primary = IsPrimary(button);
            Color fill = !button.Enabled ? DarkTheme.Panel : pressed ? (primary ? DarkTheme.PrimaryPressed : DarkTheme.AccentSoft)
                : Mix(button.BackColor, DarkTheme.Panel3, hover);
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Shape(new Rectangle(1, 1, Math.Max(1, button.Width - 3), Math.Max(1, button.Height - 3)), Scale(button, 6)))
            {
                if (primary && button.Enabled && !SystemInformation.HighContrast)
                {
                    float lift = pressed ? .16f : hover * .10f;
                    using (var brush = new LinearGradientBrush(shape.GetBounds(), Mix(DarkTheme.Accent, Color.White, lift), Mix(DarkTheme.Cyan, Color.White, lift * .6f), LinearGradientMode.Horizontal)) graphics.FillPath(brush, shape);
                }
                else if (button.Enabled && !SystemInformation.HighContrast && (hover > 0 || button.BackColor == DarkTheme.AccentSoft))
                {
                    float accent = button.BackColor == DarkTheme.AccentSoft ? .48f : hover * .4f;
                    using (var brush = new LinearGradientBrush(shape.GetBounds(), Mix(fill, DarkTheme.AccentSoft, accent), Mix(fill, DarkTheme.CyanSoft, accent), LinearGradientMode.Horizontal)) graphics.FillPath(brush, shape);
                }
                else using (var brush = new SolidBrush(fill)) graphics.FillPath(brush, shape);
                if (button.FlatAppearance.BorderSize > 0)
                    using (var border = new Pen(Mix(button.FlatAppearance.BorderColor, DarkTheme.Cyan, hover * .65f), Math.Min(2, button.FlatAppearance.BorderSize))) graphics.DrawPath(border, shape);
                if (HasKeyboardFocus(button))
                    using (var focus = new Pen(DarkTheme.Focus, Scale(button, 2))) graphics.DrawPath(focus, shape);
            }
            graphics.SmoothingMode = previous;
        }

        internal static void PaintButton(Button button, Graphics graphics)
        {
            if (button.Width < 4 || button.Height < 4) return;
            ButtonBackground(button, graphics);
            Color foreground = Foreground(button);
            if (StudioHistorySymbols.IsArrow(button.Text))
            {
                StudioHistorySymbols.Draw(graphics, button.Text, button.Font, button.ClientRectangle, foreground);
                return;
            }
            Rectangle text = new Rectangle(3 + button.Padding.Left, 2 + button.Padding.Top,
                Math.Max(1, button.Width - 6 - button.Padding.Horizontal), Math.Max(1, button.Height - 4 - button.Padding.Vertical));
            if (button.Image != null)
            {
                Image image = button.Image;
                int x = String.IsNullOrEmpty(button.Text) ? (button.Width - image.Width) / 2 : text.Left;
                int y = (button.Height - image.Height) / 2;
                if (button.Enabled) graphics.DrawImageUnscaled(image, x, y);
                else ControlPaint.DrawImageDisabled(graphics, image, x, y, button.BackColor);
                text.X += image.Width + 6;
                text.Width = Math.Max(1, text.Width - image.Width - 6);
            }
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | (button.Text.IndexOf('\n') >= 0 ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
            if (!button.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            if (button.AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
            if (button.TextAlign == ContentAlignment.MiddleLeft || button.TextAlign == ContentAlignment.TopLeft || button.TextAlign == ContentAlignment.BottomLeft) flags |= TextFormatFlags.Left;
            else if (button.TextAlign == ContentAlignment.MiddleRight) flags |= TextFormatFlags.Right;
            else flags |= TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(graphics, button.Text, button.Font, text, foreground, flags);
        }

        internal static void PaintChoice(ButtonBase button, Graphics graphics)
        {
            var check = button as CheckBox;
            var radio = button as RadioButton;
            if (check == null && radio == null) return;
            Color background = button.BackColor;
            if (background.A == 0) background = button.Parent == null ? DarkTheme.Back : button.Parent.BackColor;
            graphics.Clear(background);
            bool selected = check != null ? check.CheckState != CheckState.Unchecked : radio.Checked;
            bool indeterminate = check != null && check.CheckState == CheckState.Indeterminate;
            bool appearanceButton = check != null ? check.Appearance == Appearance.Button : radio.Appearance == Appearance.Button;
            float hoverAmount = HoverAmount(button);
            bool pressed = Pressed(button);
            Color foreground = button.Enabled ? button.ForeColor : DarkTheme.Disabled;
            if (appearanceButton && button.Enabled && SystemInformation.HighContrast && (selected || pressed || hoverAmount > 0)) foreground = SystemColors.HighlightText;
            Rectangle content = new Rectangle(button.Padding.Left, button.Padding.Top,
                Math.Max(0, button.Width - button.Padding.Horizontal), Math.Max(0, button.Height - button.Padding.Vertical));
            if (content.Width < 1 || content.Height < 1) return;
            Rectangle text = content;
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (appearanceButton)
            {
                Color fill = !button.Enabled ? DarkTheme.Panel : pressed ? DarkTheme.Accent2 : selected ? DarkTheme.AccentSoft : Mix(DarkTheme.Panel2, DarkTheme.Panel3, hoverAmount);
                using (var shape = Shape(Rectangle.Inflate(content, -1, -1), Scale(button, 6)))
                {
                    using (var brush = new SolidBrush(fill)) graphics.FillPath(brush, shape);
                    using (var pen = new Pen(HasKeyboardFocus(button) ? DarkTheme.Focus : selected ? DarkTheme.Accent : DarkTheme.Border)) graphics.DrawPath(pen, shape);
                }
                text.Inflate(-Scale(button, 6), -Scale(button, 2));
            }
            else
            {
                ContentAlignment align = check != null ? check.CheckAlign : radio.CheckAlign;
                bool right = align == ContentAlignment.TopRight || align == ContentAlignment.MiddleRight || align == ContentAlignment.BottomRight;
                if (button.RightToLeft == RightToLeft.Yes) right = !right;
                bool top = align == ContentAlignment.TopLeft || align == ContentAlignment.TopCenter || align == ContentAlignment.TopRight;
                bool bottom = align == ContentAlignment.BottomLeft || align == ContentAlignment.BottomCenter || align == ContentAlignment.BottomRight;
                int size = Math.Min(Scale(button, 17), Math.Min(content.Width, content.Height) - 2);
                if (size < 2) { graphics.SmoothingMode = previous; return; }
                int y = top ? content.Top + 1 : bottom ? content.Bottom - size - 1 : content.Top + (content.Height - size) / 2;
                Rectangle glyph = new Rectangle(right ? content.Right - size - 1 : content.Left + 1, y, size, size);
                Color fill = !button.Enabled ? DarkTheme.Panel : selected ? pressed ? DarkTheme.Accent2 : DarkTheme.Accent : Mix(DarkTheme.Panel2, DarkTheme.Panel3, hoverAmount);
                Color edge = !button.Enabled ? DarkTheme.Disabled : selected ? DarkTheme.Accent : Mix(DarkTheme.Border, DarkTheme.Accent, hoverAmount);
                using (Brush brush = selected && button.Enabled && !SystemInformation.HighContrast
                    ? (Brush)new LinearGradientBrush(glyph, Mix(DarkTheme.Accent, Color.White, pressed ? .16f : hoverAmount * .05f), DarkTheme.Cyan, LinearGradientMode.Horizontal)
                    : new SolidBrush(fill))
                using (var pen = new Pen(edge, Scale(button, 1)))
                {
                    if (radio != null) { graphics.FillEllipse(brush, glyph); graphics.DrawEllipse(pen, glyph); }
                    else using (var shape = Shape(glyph, Scale(button, 4))) { graphics.FillPath(brush, shape); graphics.DrawPath(pen, shape); }
                }
                if (selected)
                {
                    Color mark = !button.Enabled ? DarkTheme.Disabled : DarkTheme.PrimaryInk;
                    using (var pen = new Pen(mark, Math.Max(2, size / 7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    {
                        if (indeterminate) graphics.DrawLine(pen, glyph.Left + size * .25f, glyph.Top + size * .5f, glyph.Left + size * .75f, glyph.Top + size * .5f);
                        else if (radio != null) using (var brush = new SolidBrush(mark)) graphics.FillEllipse(brush, Rectangle.Inflate(glyph, -size / 3, -size / 3));
                        else graphics.DrawLines(pen, new[] { new PointF(glyph.Left + size * .23f, glyph.Top + size * .5f), new PointF(glyph.Left + size * .43f, glyph.Top + size * .7f), new PointF(glyph.Left + size * .77f, glyph.Top + size * .3f) });
                    }
                }
                int reserve = size + Scale(button, 9);
                if (!right) text.X += reserve;
                text.Width = Math.Max(0, text.Width - reserve);
                if (HasKeyboardFocus(button))
                    ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(content, -1, -1), DarkTheme.Focus, background);
            }
            graphics.SmoothingMode = previous;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            if (!button.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            flags |= button.Text.IndexOfAny(new[] { '\r', '\n' }) >= 0 || button.MaximumSize.Width > 0 ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine;
            if (button.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            if (appearanceButton || button.TextAlign == ContentAlignment.MiddleCenter) flags |= TextFormatFlags.HorizontalCenter;
            else if (button.TextAlign == ContentAlignment.MiddleRight) flags |= TextFormatFlags.Right;
            TextRenderer.DrawText(graphics, button.Text, button.Font, text, foreground, flags);
        }
    }
}
