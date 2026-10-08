using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioMessageBox
    {
        internal static DialogResult ShowPath(IWin32Window owner, string path, string message, string caption, string nextAction = null)
        {
            using (var dialog = new StudioMessageDialog(message, caption, MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, path, nextAction))
                return dialog.ShowDialog(owner);
        }

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
            : this(message, caption, buttons, icon, defaultButton, null) { }

        internal StudioMessageDialog(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, string path, string nextAction = null)
        {
            message = (message ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
            Text = caption;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
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
                Padding = Padding.Empty,
                ColumnCount = 1,
                RowCount = 3
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight));
            bool hasPath = !String.IsNullOrEmpty(path);
            if (hasPath)
            {
                root.RowCount = 4;
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2,
                    Margin = new Padding(22, 14, 22, 0) };
                pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var pathBox = new TextBox { Name = "CopyablePath", Text = path, ReadOnly = true,
                    AccessibleName = L.T("Datei- oder Ordnerpfad", "File or folder path"),
                    Anchor = AnchorStyles.Left | AnchorStyles.Right, BorderStyle = BorderStyle.FixedSingle };
                pathBox.Enter += delegate { pathBox.SelectAll(); };
                StudioUx.DisableHover(pathBox);
                var copy = new Button { Text = L.T("Pfad kopieren", "Copy path"), AutoSize = true,
                    MinimumSize = new Size(115, 34), Margin = new Padding(8, 0, 0, 0) };
                copy.Click += delegate
                {
                    try { Clipboard.SetText(path); copy.Text = L.T("Kopiert", "Copied"); }
                    catch (System.Runtime.InteropServices.ExternalException)
                    {
                        pathBox.Focus();
                        pathBox.SelectAll();
                        copy.Text = L.T("Erneut kopieren", "Retry copy");
                    }
                };
                pathRow.Controls.Add(pathBox, 0, 0);
                pathRow.Controls.Add(copy, 1, 0);
                root.Controls.Add(pathRow, 0, 1);
            }
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            string heading = icon == MessageBoxIcon.Error ? L.T("Fehler", "Something went wrong")
                : icon == MessageBoxIcon.Warning ? L.T("Bitte prüfen", "Please review")
                : buttons == MessageBoxButtons.OK ? L.T("Hinweis", "Notice") : L.T("Bitte bestätigen", "Please confirm");
            root.Controls.Add(StudioChrome.Header(heading, caption), 0, 0);
            var content = new TextBox
            {
                Name = "SelectableMessage",
                Text = message,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = Padding.Empty,
                AccessibleName = L.T("Meldung", "Message")
            };
            var scroll = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 18, 22, 0) };
            scroll.Controls.Add(content);
            root.Controls.Add(scroll, 0, hasPath ? 2 : 1);
            StudioUx.DisableHover(content);
            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(14, 16, 22, 16),
                WrapContents = true,
                Margin = Padding.Empty
            };
            root.Controls.Add(row, 0, hasPath ? 3 : 2);
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

            if (hasPath)
            {
                var open = StudioChrome.ActionButton(L.T("Dateispeicherort öffnen", "Open file location"));
                open.Name = "OpenFileLocation";
                open.Enabled = Directory.Exists(path) || File.Exists(path);
                open.Click += delegate
                {
                    try
                    {
                        string full = Path.GetFullPath(path);
                        if (!Directory.Exists(full) && !File.Exists(full))
                            throw new FileNotFoundException(L.T("Der Ausgabeort existiert nicht mehr.", "The output location no longer exists."));
                        Process.Start(new ProcessStartInfo("explorer.exe", (File.Exists(full) ? "/select," : "") + "\"" + full + "\"") { UseShellExecute = true });
                    }
                    catch (Exception error) { StudioMessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                row.Controls.Add(open);
            }
            Button nextButton = null;
            if (hasPath && !String.IsNullOrEmpty(nextAction))
            {
                nextButton = StudioChrome.ActionButton(nextAction);
                nextButton.Name = "PathNextAction";
                nextButton.DialogResult = DialogResult.Yes;
                nextButton.Enabled = Directory.Exists(path);
                row.Controls.Add(nextButton);
            }
            bool discard = buttons == MessageBoxButtons.YesNo && (message.IndexOf("without saving", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("discard", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("verwerf", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("ohne Speichern", StringComparison.OrdinalIgnoreCase) >= 0);
            var choices = new Button[results.Length];
            for (int i = 0; i < results.Length; i++)
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
            DarkTheme.Apply(this);
            StudioUx.Attach(this);
            content.BackColor = DarkTheme.Back;
            content.ForeColor = DarkTheme.Fore;
            DarkTheme.StylePrimary(nextButton ?? choices[selected]);
            bool sizing = false;
            Action fitContent = delegate
            {
                if (sizing || IsDisposed || !IsHandleCreated)
                    return;
                sizing = true;
                try
                {
                    root.PerformLayout();
                    int width = Math.Max(100, content.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
                    int textHeight = TextRenderer.MeasureText(message, content.Font, new Size(width, Int32.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height + 16;
                    int limit = Math.Max(180, Math.Min(650, Screen.FromControl(this).WorkingArea.Height - 100));
                    int wanted = ClientSize.Height + textHeight - content.ClientSize.Height;
                    ClientSize = new Size(ClientSize.Width, Math.Max(220, Math.Min(limit, wanted)));
                    root.PerformLayout();
                    content.ScrollBars = textHeight > content.ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
                    root.PerformLayout();
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
