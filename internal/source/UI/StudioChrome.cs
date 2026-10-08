using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace murumsWiiModStudio
{
    internal sealed class AccentStrip : Control
    {
        public AccentStrip()
        {
            Height = 1;
            Dock = DockStyle.Top;
            BackColor = DarkTheme.Border;
        }
    }

    internal static class StudioChrome
    {
        internal const int HeaderHeight = 56;

        internal static Button ActionButton(string text)
        {
            return new Button {
                Text = text, UseMnemonic = false, AutoSize = true,
                MinimumSize = new Size(0, 32), Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(4)
            };
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        internal static void StyleWindowFrame(Form form)
        {
            form.HandleCreated += delegate { UpdateWindowFrame(form, form.ContainsFocus); };
            form.Activated += delegate { UpdateWindowFrame(form, true); };
            form.Deactivate += delegate { UpdateWindowFrame(form, false); };
            form.SystemColorsChanged += delegate { UpdateWindowFrame(form, form.ContainsFocus); };
            if (form.IsHandleCreated) UpdateWindowFrame(form, form.ContainsFocus);
        }

        static void UpdateWindowFrame(Form form, bool active)
        {
            if (!form.IsHandleCreated || !form.TopLevel || form.FormBorderStyle == FormBorderStyle.None) return;
            bool highContrast = SystemInformation.HighContrast;
            int dark = highContrast ? 0 : 1;
            int background = highContrast ? -1 : ColorTranslator.ToWin32(DarkTheme.Panel);
            int foreground = highContrast ? -1 : ColorTranslator.ToWin32(active ? DarkTheme.Fore : DarkTheme.Muted);
            int border = highContrast ? -1 : ColorTranslator.ToWin32(DarkTheme.Border);
            DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
            // Ältere Windows-Versionen behalten für nicht unterstützte Farbattribute ihren Systemrahmen.
            DwmSetWindowAttribute(form.Handle, 35, ref background, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 36, ref foreground, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 34, ref border, sizeof(int));
        }

        public static Panel Header(string title, string subtitle)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, HeaderHeight),
                Margin = Padding.Empty,
                BackColor = DarkTheme.Panel,
                Padding = new Padding(1)
            };
            panel.Name = "StudioHeader";
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (SystemInformation.HighContrast) return;
                int y = Math.Max(0, panel.ClientSize.Height - 2);
                using (var line = new LinearGradientBrush(new Rectangle(0, y, Math.Max(1, panel.ClientSize.Width), 2), DarkTheme.Accent, DarkTheme.Cyan, LinearGradientMode.Horizontal))
                    e.Graphics.FillRectangle(line, 0, y, panel.ClientSize.Width, 2);
            };
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12, 5, 12, 5)
            };
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var logo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 5, 12, 5)
            };
            logo.Image = StudioBrand.Logo(48);

            logo.Disposed += delegate
            {
                if (logo.Image != null)
                    logo.Image.Dispose();
            };
            grid.Controls.Add(logo, 0, 0);
            var text = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            text.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Text = title, ForeColor = DarkTheme.Focus, Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), AutoEllipsis = true, AccessibleDescription = title }, 0, 0);
            text.Controls.Add(new Label { UseMnemonic = false, Dock = DockStyle.Fill, Text = subtitle, ForeColor = DarkTheme.Muted, AutoEllipsis = true, AccessibleDescription = subtitle }, 0, 1);
            grid.Controls.Add(text, 1, 0);
            panel.Controls.Add(grid);
            return panel;
        }

        public static void EnableLinks(RichTextBox box)
        {
            box.DetectUrls = true;
            box.LinkClicked += delegate (object sender, LinkClickedEventArgs e)
            {
                OpenLink(box.FindForm(), e.LinkText);
            };
        }

        public static void OpenLink(IWin32Window owner, string address)
        {
            Uri url;
            if (!Uri.TryCreate(address, UriKind.Absolute, out url) || (url.Scheme != "https" && url.Scheme != "http"))
                return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StudioMessageBox.Show(owner, ex.Message, L.T("Link öffnen", "Open link"));
            }
        }
    }

    internal sealed class SelectionClearButton : Button
    {
        public SelectionClearButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            BackColor = DarkTheme.Panel2;
            ForeColor = DarkTheme.Fore;
            FlatAppearance.BorderColor = DarkTheme.Border;
            FlatAppearance.BorderSize = 1;
            Height = 36;
            Width = 200;
            Padding = new Padding(10, 0, 10, 0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            StudioSurface.Track(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            StudioSurface.ButtonBackground(this, e.Graphics);
            TextRenderer.DrawText(e.Graphics, "×  " + Text, Font, new Rectangle(6, 0, Math.Max(0, Width - 12), Height), StudioSurface.Foreground(this), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
    internal static class StudioPreview
    {
        internal static void AddExpandButton(Control preview, Control buttonHost = null)
        {
            Control host = buttonHost ?? preview;
            var button = new Button {
                Name = "ExpandPreview", Text = L.T("Vorschau vergrößern", "Expand preview"),
                AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = DarkTheme.Panel,
                ForeColor = DarkTheme.Fore, Padding = new Padding(6, 2, 6, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            StudioActions.Icon(button, StudioIcon.Expand);
            button.FlatAppearance.BorderColor = DarkTheme.Border;
            button.FlatAppearance.MouseOverBackColor = DarkTheme.Panel3;
            StudioUx.SetHelp(button, L.T("Vergrößerte Arbeitsansicht öffnen. Mit Esc zurück; Änderungen bleiben erhalten.",
                "Open a larger workspace. Esc returns to the editor; changes are kept."));
            Form expanded = null;
            host.Controls.Add(button);
            bool aligning = false;
            EventHandler align = delegate {
                if (aligning || button.IsDisposed || host.IsDisposed) return;
                aligning = true;
                try {
                    button.Visible = host.ClientSize.Width >= button.Width + 16 && host.ClientSize.Height >= 96;
                    var position = new Point(Math.Max(0, host.ClientSize.Width - button.Width - 8), Math.Max(0, host.ClientSize.Height - button.Height - 8));
                    if (button.Location != position) button.Location = position;
                    if (host.Controls.GetChildIndex(button) != 0) button.BringToFront();
                }
                finally { aligning = false; }
            };
            host.SizeChanged += align;
            host.Layout += delegate { align(null, EventArgs.Empty); };
            button.SizeChanged += align;
            align(null, EventArgs.Empty);
            button.Click += delegate {
                if (expanded != null) { expanded.Close(); return; }
                Control parent = preview.Parent;
                Form owner = preview.FindForm();
                if (parent == null || owner == null) return;
                DockStyle dock = preview.Dock;
                AnchorStyles anchor = preview.Anchor;
                Rectangle bounds = preview.Bounds;
                Padding margin = preview.Margin;
                var table = parent as TableLayoutPanel;
                var flow = parent as FlowLayoutPanel;
                int index = parent.Controls.GetChildIndex(preview);
                int column = table == null ? 0 : table.GetColumn(preview);
                int row = table == null ? 0 : table.GetRow(preview);
                int columns = table == null ? 1 : table.GetColumnSpan(preview);
                int rows = table == null ? 1 : table.GetRowSpan(preview);
                bool flowBreak = flow != null && flow.GetFlowBreak(preview);
                var placeholder = new Panel { Dock = dock, Anchor = anchor, Bounds = bounds, Margin = margin,
                    MinimumSize = preview.MinimumSize, MaximumSize = preview.MaximumSize };
                parent.SuspendLayout();
                parent.Controls.Remove(preview);
                parent.Controls.Add(placeholder);
                if (table != null) {
                    table.SetColumn(placeholder, column); table.SetRow(placeholder, row);
                    table.SetColumnSpan(placeholder, columns); table.SetRowSpan(placeholder, rows);
                }
                if (flow != null) flow.SetFlowBreak(placeholder, flowBreak);
                parent.Controls.SetChildIndex(placeholder, index);
                parent.ResumeLayout(true);
                var window = new Form {
                    Text = L.T("Große Vorschau — ", "Expanded preview — ") + owner.Text,
                    Font = owner.Font, Icon = owner.Icon, BackColor = DarkTheme.Back,
                    StartPosition = FormStartPosition.CenterParent, Size = owner.Size,
                    MinimumSize = new Size(640, 480), WindowState = FormWindowState.Maximized,
                    ShowInTaskbar = false, KeyPreview = true
                };
                expanded = window;
                window.KeyDown += delegate(object sender, KeyEventArgs e) {
                    if (e.KeyCode == Keys.Escape) { e.Handled = true; window.Close(); }
                };
                bool restored = false;
                Action restore = delegate {
                        if (restored) return;
                        restored = true;
                        expanded = null;
                        window.Controls.Remove(preview);
                        if (!parent.IsDisposed) {
                            parent.SuspendLayout();
                            parent.Controls.Remove(placeholder);
                            preview.Dock = dock; preview.Anchor = anchor; preview.Bounds = bounds; preview.Margin = margin;
                            parent.Controls.Add(preview);
                            if (table != null) {
                                table.SetColumn(preview, column); table.SetRow(preview, row);
                                table.SetColumnSpan(preview, columns); table.SetRowSpan(preview, rows);
                            }
                            if (flow != null) flow.SetFlowBreak(preview, flowBreak);
                            parent.Controls.SetChildIndex(preview, Math.Min(index, parent.Controls.Count - 1));
                            parent.ResumeLayout(true);
                        }
                        placeholder.Dispose();
                        button.Text = L.T("Vorschau vergrößern", "Expand preview");
                        button.AccessibleName = button.Text;
                        StudioActions.Icon(button, StudioIcon.Expand);
                        align(null, EventArgs.Empty);
                };
                try {
                    button.Text = L.T("Zurück zum Editor (Esc)", "Back to editor (Esc)");
                    button.AccessibleName = button.Text;
                    StudioActions.Icon(button, StudioIcon.Collapse);
                    preview.Dock = DockStyle.Fill;
                    window.Controls.Add(preview);
                    DarkTheme.Apply(window);
                    StudioEditor.Open(owner, window, delegate { restore(); });
                }
                catch { restore(); window.Dispose(); throw; }
            };
        }
    }

}
