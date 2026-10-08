using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class PackSelectionStrip : TableLayoutPanel
    {
        bool required;
        bool paused;
        bool sourceRequired;
        internal string HighlightedSource;
        internal bool SourceRequired
        {
            get { return sourceRequired; }
            set { if (sourceRequired == value) return; sourceRequired = value; UpdateSourceButtons(); }
        }
        internal bool Required
        {
            get { return required; }
            set { if (required == value) return; required = value; Invalidate(); }
        }
        internal bool Paused
        {
            get { return paused; }
            set { paused = value; UpdateAnimation(); }
        }
        internal bool Animating { get { return false; } }

        internal PackSelectionStrip()
        {
            Name = "CustomPackBar";
            DoubleBuffered = true;
            BackColor = DarkTheme.Panel;
            VisibleChanged += delegate { UpdateAnimation(); };
            EnabledChanged += delegate { UpdateAnimation(); };
        }

        internal void UpdateAnimation()
        {
            UpdateSourceButtons();
        }

        private void UpdateSourceButtons()
        {
            foreach (Button button in Controls.OfType<Button>().Where(b => b.Name == "PackSourceAction"))
            {
                bool next = sourceRequired && (HighlightedSource == null || button.Text == HighlightedSource);
                if (next && button.BackColor != DarkTheme.Accent2) DarkTheme.StylePrimary(button);
                else if (!next && button.BackColor != DarkTheme.Panel2) DarkTheme.StyleNeutral(button);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (var brush = new SolidBrush(required ? DarkTheme.Accent : DarkTheme.Border))
                e.Graphics.FillRectangle(brush, 0, 6, required ? 2 : 1, Math.Max(0, Height - 12));
        }

    }
}
