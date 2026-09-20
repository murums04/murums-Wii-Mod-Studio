using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class ColourButton
    {
        sealed class State { internal Color Color; }
        static readonly ConditionalWeakTable<Button, State> states = new ConditionalWeakTable<Button, State>();

        internal static void SetColor(Button button, Color color)
        {
            if (button == null) return;
            StudioUx.Attach(button);
            State state;
            if (!states.TryGetValue(button, out state))
            {
                state = new State();
                states.Add(button, state);
                button.Paint += PaintBorder;
            }
            // Deckend anzeigen, auch wenn die eigentliche Farbe transparent ist.
            state.Color = Color.FromArgb(color.R, color.G, color.B);
            button.FlatAppearance.BorderColor = state.Color;
            button.FlatAppearance.BorderSize = 3;
            button.Invalidate();
        }

        static void PaintBorder(object sender, PaintEventArgs e)
        {
            var button = (Button)sender;
            State state;
            if (!states.TryGetValue(button, out state) || button.Width < 8 || button.Height < 8) return;
            using (var pen = new Pen(state.Color, 3) { Alignment = PenAlignment.Inset })
                e.Graphics.DrawRectangle(pen, 0, 0, button.Width - 1, button.Height - 1);
            if (state.Color.GetBrightness() < .15f)
                using (var pen = new Pen(DarkTheme.Muted))
                    e.Graphics.DrawRectangle(pen, 3, 3, button.Width - 7, button.Height - 7);
        }
    }
}
