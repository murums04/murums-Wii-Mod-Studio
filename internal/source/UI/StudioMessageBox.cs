using System;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioMessageBox
    {
        public static DialogResult Show(string text, string caption = "murums Wii Mod Studio", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            return Show(Form.ActiveForm, text, caption, buttons, icon, defaultButton);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption = "murums Wii Mod Studio", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            using (var dialog = new StudioMessageDialog(text, caption, buttons, icon, defaultButton))
                return dialog.ShowDialog(owner);
        }
    }

    internal sealed class StudioMessageDialog : Form
    {
        public StudioMessageDialog(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            Text = caption;
            Font = new Font("Segoe UI", 10);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            int height = TextRenderer.MeasureText(message, Font, new Size(540, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            ClientSize = new Size(620, Math.Max(220, Math.Min(560, height + 150)));
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 14, 22, 16),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            string heading = icon == MessageBoxIcon.Error ? L.T("Fehler", "Something went wrong") : buttons == MessageBoxButtons.OK ? L.T("Hinweis", "Notice") : L.T("Bitte bestätigen", "Please confirm");
            root.Controls.Add(new Label { Text = heading, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16, FontStyle.Bold) }, 0, 0);
            var content = new Label
            {
                Text = message,
                AutoSize = true,
                MaximumSize = new Size(540, 0),
                UseMnemonic = false,
                Padding = new Padding(0, 6, 0, 10)
            };
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            scroll.Controls.Add(content);
            root.Controls.Add(scroll, 0, 1);
            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 16, 0, 0)
            };
            root.Controls.Add(row, 0, 2);
            DialogResult[] results;
            switch (buttons)
            {
                case MessageBoxButtons.YesNo:
                    results = new[]
                    {
                        DialogResult.Yes,
                        DialogResult.No
                    };
                    break;
                case MessageBoxButtons.YesNoCancel:
                    results = new[]
                    {
                        DialogResult.Yes,
                        DialogResult.No,
                        DialogResult.Cancel
                    };
                    break;
                case MessageBoxButtons.OKCancel:
                    results = new[]
                    {
                        DialogResult.OK,
                        DialogResult.Cancel
                    };
                    break;
                case MessageBoxButtons.RetryCancel:
                    results = new[]
                    {
                        DialogResult.Retry,
                        DialogResult.Cancel
                    };
                    break;
                case MessageBoxButtons.AbortRetryIgnore:
                    results = new[]
                    {
                        DialogResult.Abort,
                        DialogResult.Retry,
                        DialogResult.Ignore
                    };
                    break;
                default:
                    results = new[]
                    {
                        DialogResult.OK
                    };
                    break;
            }

            bool discard = buttons == MessageBoxButtons.YesNo && (message.IndexOf("without saving", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("discard", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("verwerf", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("ohne Speichern", StringComparison.OrdinalIgnoreCase) >= 0);
            var choices = new Button[results.Length];
            for (int i = results.Length - 1; i >= 0; i--)
            {
                var result = results[i];
                string label = result == DialogResult.Yes ? L.T("Ja", "Yes") : result == DialogResult.No ? L.T("Nein", "No") : result == DialogResult.Cancel ? L.T("Abbrechen", "Cancel") : result == DialogResult.Retry ? L.T("Erneut versuchen", "Retry") : result == DialogResult.Abort ? L.T("Abbrechen", "Abort") : result == DialogResult.Ignore ? L.T("Ignorieren", "Ignore") : "OK";
                if (discard)
                    label = result == DialogResult.Yes ? L.T("Änderungen verwerfen", "Discard changes") : L.T("Weiter bearbeiten", "Keep editing");
                var b = new Button
                {
                    Text = label,
                    DialogResult = result,
                    AutoSize = true,
                    MinimumSize = new Size(110, 36),
                    Padding = new Padding(12, 4, 12, 4),
                    Margin = new Padding(8, 0, 0, 0)
                };
                choices[i] = b;
                row.Controls.Add(b);
                if (result == DialogResult.Cancel || result == DialogResult.No || results.Length == 1)
                    CancelButton = b;
            }

            int selected = Math.Min(results.Length - 1, (int)defaultButton / 256);
            if (discard)
                selected = 1;
            AcceptButton = choices[selected];
            Controls.Add(root);
            Controls.Add(new AccentStrip());
            DarkTheme.Apply(this);
            StudioUx.Attach(this);
            content.BackColor = BackColor;
            content.ForeColor = DarkTheme.Fore;
            choices[selected].BackColor = DarkTheme.Accent;
            choices[selected].ForeColor = Color.White;
            bool sizing = false;
            Action fitContent = delegate
            {
                if (sizing || IsDisposed || !IsHandleCreated)
                    return;
                sizing = true;
                try
                {
                    scroll.AutoScroll = false;
                    root.PerformLayout();
                    content.MaximumSize = new Size(Math.Max(100, scroll.ClientSize.Width), 0);
                    content.Size = content.GetPreferredSize(new Size(content.MaximumSize.Width, 0));
                    int limit = Math.Max(180, Math.Min(650, Screen.FromControl(this).WorkingArea.Height - 100));
                    int wanted = ClientSize.Height + content.Height - scroll.ClientSize.Height + 8;
                    ClientSize = new Size(ClientSize.Width, Math.Max(220, Math.Min(limit, wanted)));
                    root.PerformLayout();
                    bool overflow = content.Height > scroll.ClientSize.Height;
                    if (overflow)
                    {
                        content.MaximumSize = new Size(Math.Max(100, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2), 0);
                        content.Size = content.GetPreferredSize(new Size(content.MaximumSize.Width, 0));
                    }

                    scroll.AutoScroll = overflow;
                }
                finally
                {
                    sizing = false;
                }
            };
            Shown += delegate
            {
                fitContent();
                choices[selected].Focus();
            };
        }
    }
}
