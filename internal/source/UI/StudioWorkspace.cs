using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioWorkspace : UserControl
    {
        sealed class WorkspaceGrid : TableLayoutPanel
        {
            internal WorkspaceGrid() { DoubleBuffered = true; ResizeRedraw = true; }
        }
        sealed class WorkspaceSurface : Panel
        {
            internal WorkspaceSurface() { DoubleBuffered = true; }
        }
        sealed class WorkspaceNavigation : FlowLayoutPanel
        {
            internal WorkspaceNavigation() { DoubleBuffered = true; }
        }
        sealed class Page
        {
            internal string Key, Title;
            internal Button Navigation;
            internal Func<Form> Create;
            internal Form Window;
        }

        readonly TableLayoutPanel layout;
        readonly FlowLayoutPanel navigation;
        readonly Panel surface;
        readonly Control archive;
        readonly Label heading;
        readonly Button close;
        readonly Button toggle;
        readonly List<Page> pages = new List<Page>();
        readonly List<Label> groups = new List<Label>();
        Page current;
        Form detail;
        readonly List<Form> details = new List<Form>();
        bool compact, userCollapsed, closing;
        int navigationWidth = -1, navigationPages = -1;
        internal event EventHandler ActivePageChanged;
        internal event Action<Form> PageOpening;
        internal bool HasDetail { get { return detail != null; } }
        internal bool ArchiveActive { get { return detail == null && (current == null || current.Key == "archive"); } }
        internal string ActiveKey { get { return current == null ? "archive" : current.Key; } }

        internal StudioWorkspace(Control archiveView)
        {
            Name = "StudioWorkspace";
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            archive = archiveView;
            DoubleBuffered = true;
            layout = new WorkspaceGrid { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 204));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);
            var rail = new WorkspaceGrid { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = DarkTheme.Navigation };
            rail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rail.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            rail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(rail, 0, 0);
            toggle = new Button { Name = "WorkspaceNavigationToggle", Dock = DockStyle.Fill, Text = "murums\nWII STUDIO", TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0), Margin = Padding.Empty, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            toggle.Image = StudioBrand.Logo(36);
            toggle.ImageAlign = ContentAlignment.MiddleLeft;
            toggle.TextImageRelation = TextImageRelation.ImageBeforeText;
            toggle.Disposed += delegate { toggle.Image.Dispose(); };
            StudioUx.SetHelp(toggle, L.T("Werkzeugnavigation ein-/ausklappen", "Expand or collapse tool navigation"));
            toggle.AccessibleName = L.T("Werkzeugnavigation ein-/ausklappen", "Expand or collapse tool navigation");
            toggle.Click += delegate { userCollapsed = !userCollapsed; UpdateNavigation(); };
            rail.Controls.Add(toggle, 0, 0);
            navigation = new WorkspaceNavigation { Name = "WorkspaceNavigation", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Margin = Padding.Empty, Padding = new Padding(6, 0, 6, 8), BackColor = DarkTheme.Navigation };
            rail.Controls.Add(navigation, 0, 1);
            navigation.ClientSizeChanged += delegate { UpdateNavigationWidth(); };
            navigation.Layout += delegate { UpdateNavigationWidth(); };
            var body = new WorkspaceGrid { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(body, 1, 0);
            var titlebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(14, 2, 6, 2), BackColor = DarkTheme.Panel };
            titlebar.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (SystemInformation.HighContrast || titlebar.ClientSize.Width <= 0) return;
                int y = Math.Max(0, titlebar.ClientSize.Height - 2);
                using (var line = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, y, titlebar.ClientSize.Width, 2), DarkTheme.Accent, DarkTheme.Cyan, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                    e.Graphics.FillRectangle(line, 0, y, titlebar.ClientSize.Width, 2);
            };
            titlebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titlebar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            titlebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            heading = new Label { Name = "WorkspaceTitle", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = DarkTheme.Focus, Font = new Font("Segoe UI", 12, FontStyle.Bold), Padding = new Padding(0, 1, 0, 0) };
            titlebar.Controls.Add(heading, 0, 0);
            close = new Button { Name = "CloseWorkspace", Dock = DockStyle.Fill, Text = L.T("Werkzeug schließen", "Close tool"), Margin = Padding.Empty };
            StudioActions.Icon(close, StudioIcon.Close);
            close.Click += delegate { if (detail != null) CloseDetail(); else if (current != null && current.Window != null) current.Window.Close(); };
            titlebar.Controls.Add(close, 1, 0);
            body.Controls.Add(titlebar, 0, 0);
            surface = new WorkspaceSurface { Name = "WorkspaceSurface", Dock = DockStyle.Fill, Margin = Padding.Empty };
            body.Controls.Add(surface, 0, 1);
            archive.Dock = DockStyle.Fill;
            surface.Controls.Add(archive);
            SizeChanged += delegate { UpdateNavigation(); };
        }

        internal void Group(string title)
        {
            var label = new Label { Text = title, UseMnemonic = false, Height = 29, ForeColor = DarkTheme.Cyan, TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(10, 0, 0, 6), Margin = Padding.Empty, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            groups.Add(label);
            navigation.Controls.Add(label);
        }

        internal void Add(string key, string title, StudioIcon icon, Func<Form> create)
        {
            var page = new Page { Key = key, Title = title, Create = create };
            var button = new Button { Name = "Navigate_" + key, Text = title, Height = 36, TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(10, 0, 4, 0), Margin = new Padding(0, 1, 0, 1), AccessibleName = title };
            button.Image = StudioIcons.Create(icon, 20, DarkTheme.Fore);
            button.Disposed += delegate { if (button.Image != null) button.Image.Dispose(); };
            StudioUx.SetHelp(button, title);
            button.Click += delegate
            {
                try { Open(key); }
                catch (Exception error) { StudioMessageBox.Show(FindForm(), error.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            page.Navigation = button;
            pages.Add(page);
            navigation.Controls.Add(button);
            UpdateNavigation();
        }

        internal void Open(string key)
        {
            if (detail != null) { detail.Focus(); return; }
            Page next = pages.FirstOrDefault(p => p.Key == key);
            if (next == null) throw new ArgumentException("Unknown workspace", "key");
            if (current == next && (next.Create == null ? archive.Visible : next.Window != null && next.Window.Visible)) return;
            EnsurePage(next);
            if (!closing && next.Window != null && PageOpening != null) PageOpening(next.Window);
            Page previous = current;
            surface.SuspendLayout();
            try
            {
            current = next;
            if (next.Window != null)
            {
                next.Window.Show();
                next.Window.MinimumSize = Size.Empty;
                next.Window.BringToFront();
            }
            else { archive.Show(); archive.BringToFront(); }
            if (previous != null && previous.Window != null && previous != next) previous.Window.Hide();
            if (next.Create != null) archive.Hide();
            heading.Text = next.Title;
            close.Visible = next.Window != null;
            }
            finally { surface.ResumeLayout(true); }
            if (previous != null) Highlight(previous, false);
            Highlight(next, true);
            if (ActivePageChanged != null) ActivePageChanged(this, EventArgs.Empty);
        }

        internal string[] PreparationKeys
        {
            get { return pages.Where(p => p.Create != null).Select(p => p.Key).ToArray(); }
        }

        internal string PageTitle(string key) { return pages.First(p => p.Key == key).Title; }

        internal Form PreparePage(string key)
        {
            Page page = pages.First(p => p.Key == key);
            EnsurePage(page);
            page.Window.Show();
            page.Window.MinimumSize = Size.Empty;
            page.Window.PerformLayout();
            return page.Window;
        }

        internal void FinishPreparation(Form window)
        {
            if (window != null && !window.IsDisposed && (current == null || current.Window != window)) window.Hide();
        }

        internal void DiscardFailedPreparation(string key)
        {
            Page page = pages.First(p => p.Key == key);
            Form window = page.Window;
            page.Window = null;
            if (window != null && !window.IsDisposed) window.Dispose();
        }

        void EnsurePage(Page page)
        {
            if (page.Create != null && (page.Window == null || page.Window.IsDisposed))
            {
                Form window = null;
                try
                {
                    window = page.Create();
                    window.TopLevel = false;
                    window.FormBorderStyle = FormBorderStyle.None;
                    window.ShowInTaskbar = false;
                    window.MinimumSize = Size.Empty;
                    window.Dock = DockStyle.Fill;
                    var tool = window as StudioToolForm;
                    if (tool != null) tool.PrepareWorkspace();
                    var hud = window as RaceHudForm;
                    if (hud != null) hud.PrepareWorkspace();
                    var backgrounds = window as RetroRewindGifWizard;
                    if (backgrounds != null) backgrounds.PrepareWorkspace();
                    var help = window as StudioHelpForm;
                    if (help != null) help.PrepareWorkspace();
                    var settings = window as ToolchainForm;
                    if (settings != null) settings.PrepareWorkspace();
                    page.Window = window;
                    window.FormClosed += delegate { page.Window = null; if (!closing && current == page) Open("archive"); };
                    surface.Controls.Add(window);
                }
                catch
                {
                    if (window != null) window.Dispose();
                    page.Window = null;
                    throw;
                }
            }
        }

        static void Highlight(Page page, bool active)
        {
            page.Navigation.BackColor = active ? DarkTheme.AccentSoft : DarkTheme.Navigation;
            page.Navigation.AccessibleDescription = active ? L.T("Aktives Werkzeug", "Active tool") : page.Title;
            page.Navigation.FlatAppearance.BorderSize = active ? 1 : 0;
            page.Navigation.FlatAppearance.BorderColor = DarkTheme.Focus;
        }

        internal bool CloseEditors()
        {
            while (detail != null)
            {
                Form previousDetail = detail;
                previousDetail.Close();
                if (detail == previousDetail) return false;
            }
            closing = true;
            try
            {
                foreach (Page page in pages)
                {
                    Form window = page.Window;
                    if (window == null || window.IsDisposed) continue;
                    Open(page.Key);
                    window.Close();
                    if (!window.IsDisposed) return false;
                }
                Open("archive");
                return true;
            }
            finally { closing = false; }
        }

        internal bool CloseDetail()
        {
            if (detail == null) return false;
            if (detail.CancelButton != null) detail.CancelButton.PerformClick();
            else detail.Close();
            return true;
        }

        internal void ShowDetail(Form editor, Action<DialogResult> completed)
        {
            Form previousDetail = detail;
            if (previousDetail != null) previousDetail.Hide();
            details.Add(editor);
            detail = editor;
            Form previous = current == null ? null : current.Window;
            if (previous != null) previous.Hide();
            archive.Hide();
            navigation.Enabled = false;
            heading.Text = editor.Text.Replace(" — murums Wii Mod Studio", "").Replace(" — murums Wii Studio", "");
            close.Visible = true;
            close.Text = L.T("Zurück zum Werkzeug", "Back to tool");
            StudioActions.Icon(close, StudioIcon.Previous);
            editor.TopLevel = false;
            editor.FormBorderStyle = FormBorderStyle.None;
            editor.WindowState = FormWindowState.Normal;
            editor.MinimumSize = Size.Empty;
            editor.Dock = DockStyle.Fill;
            var tool = editor as StudioToolForm;
            if (tool != null) tool.PrepareWorkspace();
            surface.Controls.Add(editor);
            BindResults(editor, editor);
            editor.FormClosed += delegate
            {
                DialogResult result = editor.DialogResult == DialogResult.None ? DialogResult.Cancel : editor.DialogResult;
                details.Remove(editor);
                detail = details.Count == 0 ? null : details[details.Count - 1];
                navigation.Enabled = detail == null;
                if (detail == null)
                {
                    Open(current == null ? "archive" : current.Key);
                    close.Text = L.T("Werkzeug schließen", "Close tool");
                    StudioActions.Icon(close, StudioIcon.Close);
                }
                else
                {
                    heading.Text = detail.Text;
                    detail.Show();
                    detail.BringToFront();
                    detail.Focus();
                }
                try { if (completed != null) completed(result); }
                catch (Exception error) { StudioMessageBox.Show(FindForm(), error.Message, editor.Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                // Form.Close gibt den nichtmodalen Editor nach FormClosed selbst frei.
            };
            editor.Show();
            editor.MinimumSize = Size.Empty;
            editor.BringToFront();
            editor.Focus();
            if (ActivePageChanged != null) ActivePageChanged(this, EventArgs.Empty);
        }

        static void BindResults(Control control, Form editor)
        {
            var button = control as Button;
            if (button != null && button.DialogResult != DialogResult.None)
                button.Click += delegate
                {
                    if (!editor.IsDisposed && editor.DialogResult != DialogResult.None) editor.Close();
                };
            foreach (Control child in control.Controls) BindResults(child, editor);
        }

        void UpdateNavigation()
        {
            if (navigation == null) return;
            var owner = TopLevelControl as Form;
            if (owner != null && owner.WindowState == FormWindowState.Minimized) return;
            bool nextCompact = userCollapsed || Width < 1180;
            if (compact == nextCompact && navigationPages == pages.Count) return;
            compact = nextCompact;
            navigationPages = pages.Count;
            layout.SuspendLayout();
            navigation.SuspendLayout();
            try
            {
            layout.ColumnStyles[0].Width = compact ? 60 : 204;
            toggle.Text = compact ? String.Empty : "murums\nWII STUDIO";
            toggle.ImageAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            toggle.Padding = compact ? Padding.Empty : new Padding(6, 0, 0, 0);
            toggle.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            foreach (Label group in groups) group.Visible = !compact;
            foreach (Page page in pages)
            {
                page.Navigation.Text = compact ? String.Empty : "  " + page.Title;
                page.Navigation.ImageAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                page.Navigation.Padding = compact ? Padding.Empty : new Padding(10, 0, 4, 0);
            }
            }
            finally { navigation.ResumeLayout(true); layout.ResumeLayout(true); }
            UpdateNavigationWidth();
        }

        void UpdateNavigationWidth()
        {
            int width = Math.Max(1, navigation.ClientSize.Width - navigation.Padding.Horizontal);
            if (navigationWidth == width && navigation.Controls.Cast<Control>().All(control => control.Width == width)) return;
            navigationWidth = width;
            navigation.SuspendLayout();
            try
            {
                foreach (Control control in navigation.Controls) control.Width = width;
            }
            finally { navigation.ResumeLayout(true); }
        }
    }
}
