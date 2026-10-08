using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioEmptyState : StudioAmbientPanel
    {
        private readonly Label message;
        private readonly Button action;
        private bool showAction;

        internal StudioEmptyState(string actionText, EventHandler onAction)
        {
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = DarkTheme.Panel2;
            message = new Label {
                TextAlign = ContentAlignment.MiddleCenter, ForeColor = DarkTheme.Muted,
                UseMnemonic = false, Margin = Padding.Empty, BackColor = Color.Transparent
            };
            action = StudioChrome.ActionButton(actionText);
            action.AutoSize = false;
            action.Visible = false;
            action.Click += onAction;
            DarkTheme.StylePrimary(action);
            Controls.Add(message);
            Controls.Add(action);
        }

        internal void SetContent(string text, bool canAct)
        {
            message.Text = text ?? String.Empty;
            showAction = canAct;
            action.Visible = canAct;
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (message == null || action == null) return;
            int width = Math.Max(1, Math.Min(520, ClientSize.Width - 32));
            Size textSize = message.GetPreferredSize(new Size(width, 0));
            Size buttonSize = action.GetPreferredSize(Size.Empty);
            int actionHeight = showAction ? buttonSize.Height + 16 : 0;
            int height = Math.Min(textSize.Height, Math.Max(1, ClientSize.Height - actionHeight - 32));
            int top = Math.Max(16, (ClientSize.Height - height - actionHeight) / 2);
            message.Bounds = new Rectangle((ClientSize.Width - width) / 2, top, width, height);
            if (showAction)
                action.Bounds = new Rectangle((ClientSize.Width - buttonSize.Width) / 2,
                    message.Bottom + 16, buttonSize.Width, buttonSize.Height);
        }
    }

    internal sealed class StudioReadOnlyText : RichTextBox
    {
        internal StudioReadOnlyText()
        {
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BorderStyle = BorderStyle.None;
            ReadOnly = true;
            WordWrap = true;
            DetectUrls = false;
            ScrollBars = RichTextBoxScrollBars.Vertical;
            BackColor = DarkTheme.Panel2;
            ForeColor = DarkTheme.Fore;
            Font = new Font("Segoe UI", 10F);
        }

        internal static Panel Surface(StudioReadOnlyText text)
        {
            var surface = new Panel {
                Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = new Padding(14), BackColor = DarkTheme.Panel2
            };
            text.Dock = DockStyle.Fill;
            surface.Controls.Add(text);
            return surface;
        }
    }
}
