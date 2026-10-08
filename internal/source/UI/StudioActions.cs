using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioActions
    {
        sealed class Binding
        {
            internal StudioIcon Icon;
            internal Bitmap Image, Disabled;
            internal int Size;
            internal Color Color;
        }
        static readonly ConditionalWeakTable<Button, Binding> buttons = new ConditionalWeakTable<Button, Binding>();
        static readonly ConditionalWeakTable<ToolStripItem, Binding> tools = new ConditionalWeakTable<ToolStripItem, Binding>();

        internal static bool IsIcon(Button button)
        {
            Binding binding;
            return button != null && buttons.TryGetValue(button, out binding);
        }

        internal static void Icon(Button button, StudioIcon icon)
        {
            button.AccessibleName = button.Text;
            Binding binding;
            if (buttons.TryGetValue(button, out binding))
            {
                if (binding.Icon != icon) { binding.Icon = icon; DisposeImages(binding); button.Invalidate(); }
                return;
            }
            binding = new Binding { Icon = icon };
            buttons.Add(button, binding);
            StudioUx.Attach(button);
            button.AccessibleName = button.Text;
            if (String.IsNullOrEmpty(button.AccessibleDescription)) StudioUx.SetHelp(button, button.Text);
            button.AutoSize = false;
            button.MinimumSize = new Size(34, 32);
            button.Size = new Size(36, 34);
            button.Padding = Padding.Empty;
            button.Paint += delegate(object sender, PaintEventArgs e)
            {
                int size = Math.Max(16, Math.Min(Math.Min(button.Width, button.Height) - 10, button.Font.Height + 5));
                Color color = StudioSurface.Foreground(button);
                if (binding.Image == null || binding.Size != size || binding.Color != color)
                {
                    DisposeImages(binding);
                    binding.Size = size;
                    binding.Color = color;
                    binding.Image = StudioIcons.Create(binding.Icon, size, color);
                    binding.Disabled = StudioIcons.Create(binding.Icon, size, DarkTheme.Disabled);
                }
                StudioSurface.ButtonBackground(button, e.Graphics);
                e.Graphics.DrawImageUnscaled(button.Enabled ? binding.Image : binding.Disabled, (button.Width - size) / 2, (button.Height - size) / 2);
            };
            button.Disposed += delegate { DisposeImages(binding); };
            button.BackColorChanged += delegate { button.Invalidate(); };
            button.ForeColorChanged += delegate { button.Invalidate(); };
            button.EnabledChanged += delegate { button.Invalidate(); };
            button.TextChanged += delegate { button.AccessibleName = button.Text; };
        }

        internal static void Tool(ToolStripItem item, StudioIcon icon, bool iconOnly)
        {
            item.AccessibleName = item.Text;
            if (String.IsNullOrEmpty(item.ToolTipText)) item.ToolTipText = item.Text;
            Binding binding;
            if (!tools.TryGetValue(item, out binding))
            {
                binding = new Binding();
                tools.Add(item, binding);
                item.Disposed += delegate { DisposeImages(binding); };
                item.ForeColorChanged += delegate { UpdateToolImage(item, binding); };
                item.EnabledChanged += delegate { UpdateToolImage(item, binding); };
            }
            DisposeImages(binding);
            binding.Icon = icon;
            UpdateToolImage(item, binding);
            item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            item.DisplayStyle = iconOnly ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText;
            item.Padding = new Padding(6, 3, 6, 3);
        }

        static void UpdateToolImage(ToolStripItem item, Binding binding)
        {
            Color color = DarkTheme.ToolForeground(item);
            if (binding.Image != null && binding.Color == color) return;
            DisposeImages(binding);
            binding.Color = color;
            binding.Image = StudioIcons.Create(binding.Icon, 32, color);
            item.Image = binding.Image;
            item.Invalidate();
        }

        static void DisposeImages(Binding binding)
        {
            if (binding.Image != null) binding.Image.Dispose();
            if (binding.Disabled != null) binding.Disabled.Dispose();
            binding.Image = binding.Disabled = null;
        }
    }
}
