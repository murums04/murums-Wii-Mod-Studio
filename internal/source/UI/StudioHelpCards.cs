using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioHelpCards
    {
        public static void Build(Control host, RichTextBox legacy)
        {
            host.Controls.Add(new HelpView(legacy) { Dock = DockStyle.Fill });
        }

        private sealed class HelpView : TableLayoutPanel
        {
            readonly List<HelpTopic> topics = StudioHelpTopics.All();
            readonly TextBox search;
            readonly ListBox nav;
            readonly Label count;
            readonly Panel canvas;
            readonly FlowLayoutPanel page;
            readonly Button home;
            bool filtering, fitting;

            internal HelpView(RichTextBox legacy)
            {
                Font = new Font("Segoe UI", 10);
                ColumnCount = 2;
                RowCount = 1;
                Margin = Padding.Empty;
                ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 228));
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12, 14, 12, 12), Margin = Padding.Empty, BackColor = DarkTheme.Panel };
                sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 4; i++) sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                sidebar.Controls.Add(new Label { Text = L.T("DEIN NÄCHSTER SCHRITT", "YOUR NEXT STEP"), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) }, 0, 0);
                home = StudioChrome.ActionButton(L.T("Alle Aufgaben", "All tasks"));
                home.Name = "HelpHome";
                home.AccessibleDescription = L.T("Zur Aufgabenübersicht zurückkehren. Alt+Pfeil links.", "Return to all tasks. Alt+Left arrow.");
                AddImage(home, StudioIcon.Home);
                home.Dock = DockStyle.Fill;
                home.Margin = new Padding(0, 0, 0, 12);
                home.Click += delegate
                {
                    if (search.TextLength > 0) search.Clear();
                    else if (nav.SelectedIndex >= 0) nav.ClearSelected();
                    else Render();
                };
                sidebar.Controls.Add(home, 0, 1);
                search = new TextBox { Name = "HelpSearch", Dock = DockStyle.Fill, AccessibleName = L.T("Hilfethemen durchsuchen", "Search help topics"), Margin = new Padding(0, 0, 0, 8) };
                sidebar.Controls.Add(search, 0, 2);
                count = new Label { AutoSize = true, ForeColor = DarkTheme.Muted, Margin = new Padding(0, 0, 0, 8), UseMnemonic = false };
                sidebar.Controls.Add(count, 0, 3);
                nav = new ListBox { Name = "HelpTopics", Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, AccessibleName = L.T("Hilfethemen", "Help topics"), Margin = Padding.Empty };
                sidebar.Controls.Add(nav, 0, 4);
                sidebar.Controls.Add(new Label { Text = L.T("Strg+F suchen · ↑ ↓ auswählen\nEnter öffnet die Anleitung", "Ctrl+F search · ↑ ↓ select\nEnter opens the guide"), AutoSize = true, MaximumSize = new Size(200, 0), ForeColor = DarkTheme.Muted, Margin = new Padding(0, 10, 0, 0), UseMnemonic = false }, 0, 5);
                var studioActions = new TableLayoutPanel { Name = "HelpStudioActions", ColumnCount = 1, RowCount = 3, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 12, 0, 0) };
                studioActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var update = UpdateButton();
                update.Name = "HelpCheckForUpdatesSidebar";
                var changelog = StudioActionButton("Changelog", "HelpChangelog", StudioIcon.Menu, main => main.ShowChangelog());
                var about = StudioActionButton(L.T("Über Studio", "About Studio"), "HelpAbout", StudioIcon.Help, main => main.ShowAbout());
                foreach (var button in new[] { update, changelog, about })
                {
                    button.Dock = DockStyle.Fill;
                    button.Margin = new Padding(0, 0, 0, 4);
                    studioActions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    studioActions.Controls.Add(button);
                }
                sidebar.Controls.Add(studioActions, 0, 6);
                canvas = new StudioAmbientPanel { Name = "HelpContent", Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 18, 20, 16), Margin = Padding.Empty };
                page = new FlowLayoutPanel { Name = "HelpTaskPage", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
                canvas.Controls.Add(page);
                legacy.Visible = false;
                canvas.Controls.Add(legacy);
                Controls.Add(sidebar, 0, 0);
                Controls.Add(canvas, 1, 0);
                DarkTheme.Apply(this);
                DarkTheme.StyleListBox(nav);
                nav.SelectedIndexChanged += delegate { if (!filtering) Render(); };
                search.TextChanged += delegate { Filter(); };
                search.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if ((e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter) && nav.Items.Count > 0)
                    {
                        nav.SelectedIndex = 0;
                        nav.Focus();
                        e.SuppressKeyPress = true;
                    }
                };
                canvas.SizeChanged += delegate { Fit(); };
                StudioUx.SetHelp(search, L.T("Suche nach einem Werkzeug, einer Datei oder einem Arbeitsschritt. Escape leert die Suche.", "Search for a tool, file or workflow step. Escape clears the search."));
                Filter();
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == (Keys.Control | Keys.F)) { search.Focus(); search.SelectAll(); return true; }
                if (keyData == Keys.Escape && search.Text.Length > 0) { search.Clear(); return true; }
                if (keyData == (Keys.Alt | Keys.Left)) { home.PerformClick(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            void Filter()
            {
                filtering = true;
                nav.BeginUpdate();
                try
                {
                    nav.Items.Clear();
                    foreach (var topic in topics)
                    {
                        string text = topic.Title + " " + topic.Summary + " " + topic.Route + " " + topic.Note + " " + string.Join(" ", topic.Steps) + " " + string.Join(" ", topic.VisualSteps) + " " + topic.ExampleTitle + " " + topic.ExampleResult + " " + string.Join(" ", topic.ExampleSteps) + (topic.IncludeLinks ? " " + StudioHelp.Links() : "");
                        if (text.IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) nav.Items.Add(topic);
                    }
                }
                finally { nav.EndUpdate(); filtering = false; }
                count.Text = nav.Items.Count + L.T(" Themen", " topics");
                Render();
            }

            void Render()
            {
                page.SuspendLayout();
                try
                {
                    while (page.Controls.Count > 0) page.Controls[0].Dispose();
                    canvas.AutoScrollPosition = Point.Empty;
                    var topic = nav.SelectedItem as HelpTopic;
                    home.BackColor = topic == null ? DarkTheme.AccentSoft : DarkTheme.Panel2;
                    if (topic == null) HomePage();
                    else TopicPage(topic);
                    DarkTheme.Apply(page);
                    Fit();
                }
                finally { page.ResumeLayout(true); }
            }

            void HomePage()
            {
                bool hasSearch = search.Text.Trim().Length > 0;
                var hero = new TableLayoutPanel { Name = "HelpHomeHero", ColumnCount = 2, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 16) };
                hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
                hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                hero.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                hero.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var logo = new PictureBox { Image = StudioBrand.Logo(64), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, MinimumSize = new Size(48, 48), Margin = new Padding(0, 4, 14, 4), TabStop = false };
                logo.Disposed += delegate { if (logo.Image != null) logo.Image.Dispose(); };
                hero.Controls.Add(logo, 0, 0);
                hero.SetRowSpan(logo, 2);
                hero.Controls.Add(new Label { Name = "HelpHomeTitle", Text = hasSearch ? L.T("Passende Aufgaben", "Matching tasks") : L.T("Was möchtest du machen?", "What do you want to do?"), AutoSize = true, Font = new Font("Segoe UI", 23, FontStyle.Bold), UseMnemonic = false, Margin = new Padding(0, 0, 0, 6) }, 1, 0);
                hero.Controls.Add(new Label { Text = hasSearch ? L.T("Wähle eine Aufgabe für Schritte, Beispiel und passende Bilder.", "Choose a task for steps, a worked example and relevant pictures.") : L.T("Wähle dein Ziel. Konkrete Beispiele und echte Programmbilder begleiten dich von der Quelle zur Ausgabe.", "Choose your goal. Worked examples and actual program pictures guide you from source to output."), AutoSize = true, ForeColor = DarkTheme.Muted, UseMnemonic = false, Margin = Padding.Empty }, 1, 1);
                page.Controls.Add(hero);
                if (nav.Items.Count == 0)
                {
                    var empty = new HelpStepCard(StudioIcon.Search, 0, L.T("Keine Treffer", "No matches"), L.T("Versuche einen anderen Begriff oder leere die Suche.", "Try a different term or clear the search."));
                    var emptyGrid = new HelpGrid(1, 240);
                    emptyGrid.Controls.Add(empty);
                    page.Controls.Add(emptyGrid);
                    var clear = StudioChrome.ActionButton(L.T("Suche leeren", "Clear search"));
                    clear.Click += delegate { search.Clear(); search.Focus(); };
                    page.Controls.Add(clear);
                    return;
                }
                if (!hasSearch)
                {
                    var direct = Actions();
                    foreach (var item in new[] { new[] { "pack", "Pack Maker" }, new[] { "character", L.T("Charakter", "Character") }, new[] { "background", L.T("Hintergründe", "Backgrounds") } })
                    {
                        string key = item[0];
                        var topic = topics.First(t => t.WorkspaceKey == key);
                        var button = StudioChrome.ActionButton(item[1] + " →");
                        AddImage(button, topic.Icon);
                        button.AccessibleDescription = L.T("Werkzeug direkt öffnen", "Open the tool directly");
                        button.Click += delegate { OpenTool(key); };
                        direct.Controls.Add(button);
                    }
                    page.Controls.Add(direct);
                }
                foreach (string group in new[] { "start", "create", "review", "guide" })
                {
                    var matches = nav.Items.Cast<HelpTopic>().Where(t => t.Group == group).ToArray();
                    if (matches.Length == 0) continue;
                    string title = group == "start" ? L.T("VORBEREITEN & ÖFFNEN", "PREPARE & OPEN") : group == "create" ? L.T("GESTALTEN", "CREATE") : group == "review" ? L.T("ZUSAMMENSTELLEN & PRÜFEN", "ASSEMBLE & REVIEW") : L.T("ORIENTIERUNG & QUELLEN", "GUIDANCE & SOURCES");
                    Heading(title, 9);
                    var grid = new HelpGrid(3, 240);
                    foreach (var topic in matches)
                    {
                        var tile = new HelpTaskButton(topic);
                        tile.Click += delegate { nav.SelectedItem = topic; };
                        grid.Controls.Add(tile);
                    }
                    page.Controls.Add(grid);
                }
            }

            void TopicPage(HelpTopic topic)
            {
                var heading = Heading(topic.Title, 21);
                heading.Name = "HelpTopicTitle";
                Paragraph(topic.Summary, DarkTheme.Muted, 10);
                var route = Paragraph(topic.Route, DarkTheme.Accent, 9);
                route.Name = "HelpTopicRoute";
                var actionRow = Actions();
                if (topic.ChecksUpdates)
                {
                    var update = UpdateButton();
                    DarkTheme.StylePrimary(update);
                    actionRow.Controls.Add(update);
                }
                if (!String.IsNullOrEmpty(topic.WorkspaceKey))
                {
                    var open = StudioChrome.ActionButton(L.T("Werkzeug öffnen →", "Open tool →"));
                    open.Name = "HelpOpenWorkspace";
                    AddImage(open, topic.Icon);
                    open.Click += delegate { OpenTool(topic.WorkspaceKey); };
                    DarkTheme.StylePrimary(open);
                    actionRow.Controls.Add(open);
                }
                var detailsToggle = StudioChrome.ActionButton(L.T("Details & Quellen anzeigen", "Show details & sources"));
                detailsToggle.Name = "HelpDetailsToggle";
                AddImage(detailsToggle, StudioIcon.Down);
                actionRow.Controls.Add(detailsToggle);
                page.Controls.Add(actionRow);
                var exampleHeading = Heading(L.T("BEISPIEL · ", "EXAMPLE · ") + topic.ExampleTitle, 12);
                exampleHeading.Name = "HelpExampleTitle";
                var steps = new HelpGrid(4, 210) { Name = "HelpSteps" };
                for (int i = 0; i < topic.ExampleSteps.Length; i++)
                {
                    string title = topic.ExampleHeadings[i];
                    steps.Controls.Add(new HelpStepCard(topic.ExampleIcons[i], i + 1, title, topic.ExampleSteps[i]));
                }
                page.Controls.Add(steps);
                var result = Paragraph(L.T("Ergebnis: ", "Result: ") + topic.ExampleResult, DarkTheme.Fore, 10);
                result.Name = "HelpExampleResult";
                result.Padding = new Padding(12, 10, 12, 10);
                result.BackColor = DarkTheme.AccentSoft;
                var note = Paragraph(topic.VisualNote, DarkTheme.Muted, 10);
                note.Name = "HelpImportantNote";
                note.Padding = new Padding(12, 10, 12, 10);
                note.BackColor = DarkTheme.Panel;
                if (topic.Pictures.Length > 0)
                {
                    Heading(L.T("IM PROGRAMM · BILD ZUM VERGRÖSSERN ÖFFNEN", "IN STUDIO · OPEN A PICTURE TO ENLARGE"), 9);
                    foreach (var picture in topic.Pictures) page.Controls.Add(new HelpPictureCard(picture));
                }
                var details = new TableLayoutPanel { Name = "HelpDetails", ColumnCount = 1, RowCount = 2, Visible = false, Margin = new Padding(0, 8, 0, 12), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
                details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                details.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
                details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var reader = new RichTextBox { Name = "HelpReader", Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical, WordWrap = true, Font = new Font("Segoe UI", 10.5F), Text = DetailText(topic), AccessibleName = L.T("Ausführliche Anleitung und Quellen", "Full guide and sources"), Margin = new Padding(12) };
                StudioChrome.EnableLinks(reader);
                StudioUx.DisableHover(reader);
                details.Controls.Add(reader, 0, 0);
                var copyRow = Actions();
                var copy = StudioChrome.ActionButton(L.T("Anleitung kopieren", "Copy guide"));
                copy.Click += delegate
                {
                    try { Clipboard.SetText(reader.Text); copy.Text = L.T("Kopiert", "Copied"); }
                    catch (System.Runtime.InteropServices.ExternalException) { reader.SelectAll(); reader.Focus(); copy.Text = L.T("Erneut kopieren", "Retry copy"); }
                };
                copyRow.Controls.Add(copy);
                details.Controls.Add(copyRow, 0, 1);
                page.Controls.Add(details);
                detailsToggle.Click += delegate
                {
                    details.Visible = !details.Visible;
                    detailsToggle.Text = details.Visible ? L.T("Details ausblenden", "Hide details") : L.T("Details & Quellen anzeigen", "Show details & sources");
                    Fit();
                    if (details.Visible) canvas.ScrollControlIntoView(details);
                };
                var navigation = Actions();
                var previous = StudioChrome.ActionButton(L.T("Vorheriges Thema", "Previous topic"));
                var next = StudioChrome.ActionButton(L.T("Nächstes Thema", "Next topic"));
                StudioActions.Icon(previous, StudioIcon.Previous);
                StudioActions.Icon(next, StudioIcon.Next);
                previous.Enabled = nav.SelectedIndex > 0;
                next.Enabled = nav.SelectedIndex < nav.Items.Count - 1;
                previous.Click += delegate { if (nav.SelectedIndex > 0) nav.SelectedIndex--; };
                next.Click += delegate { if (nav.SelectedIndex < nav.Items.Count - 1) nav.SelectedIndex++; };
                navigation.Controls.Add(previous);
                navigation.Controls.Add(next);
                navigation.Controls.Add(new Label { Text = (nav.SelectedIndex + 1) + " / " + nav.Items.Count, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 8, 4, 4), ForeColor = DarkTheme.Muted });
                page.Controls.Add(navigation);
            }

            Label Heading(string text, float size)
            {
                var label = new Label { Text = text, AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", size, FontStyle.Bold), Margin = new Padding(0, size < 10 ? 16 : 0, 0, 8), ForeColor = size < 10 ? DarkTheme.Muted : DarkTheme.Fore };
                page.Controls.Add(label);
                return label;
            }

            Label Paragraph(string text, Color color, float size)
            {
                var label = new Label { Text = text, AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", size), ForeColor = color, Margin = new Padding(0, 0, 0, 12) };
                page.Controls.Add(label);
                return label;
            }

            static FlowLayoutPanel Actions()
            {
                return new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 0, 0, 10) };
            }

            void Fit()
            {
                if (fitting || canvas == null) return;
                fitting = true;
                try
                {
                    int width = Math.Max(160, canvas.ClientSize.Width - canvas.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                    page.MinimumSize = new Size(width, 0);
                    page.MaximumSize = new Size(width, 0);
                    page.Width = width;
                    foreach (Control child in page.Controls)
                    {
                        child.Width = width - child.Margin.Horizontal;
                        var label = child as Label;
                        if (label != null) label.MaximumSize = new Size(width, 0);
                        else
                        {
                            child.MinimumSize = new Size(width - child.Margin.Horizontal, 0);
                            child.MaximumSize = new Size(width - child.Margin.Horizontal, 0);
                        }
                        var grid = child as HelpGrid;
                        if (grid != null) grid.FitCards(child.Width);
                        var picture = child as HelpPictureCard;
                        if (picture != null) picture.FitPicture(child.Width);
                        var details = child as TableLayoutPanel;
                        if (details != null && child.Name == "HelpDetails")
                        {
                            details.RowStyles[0].Height = Math.Max(180, Math.Min(360 * Font.Height / 16, canvas.ClientSize.Height / 2));
                            foreach (Control row in details.Controls) row.Width = Math.Max(100, child.Width - row.Margin.Horizontal);
                        }
                        else if (details != null)
                        {
                            int logoWidth = 66 * Font.Height / 16;
                            details.ColumnStyles[0].Width = logoWidth;
                            foreach (Control row in details.Controls)
                                if (row is Label) row.MaximumSize = new Size(Math.Max(100, child.Width - logoWidth), 0);
                        }
                    }
                }
                finally { fitting = false; }
            }

            Button UpdateButton()
            {
                return StudioActionButton(L.T("Nach Updates suchen…", "Check for updates…"), "HelpCheckForUpdates", StudioIcon.Refresh, main => main.CheckForUpdates());
            }

            Button StudioActionButton(string text, string name, StudioIcon icon, Action<MainForm> action)
            {
                var button = StudioChrome.ActionButton(text);
                button.Name = name;
                AddImage(button, icon);
                button.Click += delegate { RunStudioAction(action, text); };
                return button;
            }

            void OpenTool(string key)
            {
                RunStudioAction(main => main.OpenWorkspace(key), L.T("Werkzeug öffnen", "Open tool"));
            }

            void RunStudioAction(Action<MainForm> action, string title)
            {
                var form = FindForm();
                var main = form as MainForm ?? (form == null ? null : form.Owner as MainForm);
                if (main == null) main = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                if (main == null) return;
                Action open = delegate
                {
                    if (main.IsDisposed) return;
                    try { action(main); }
                    catch (Exception error) { StudioMessageBox.Show(main, error.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                if (form != null && form.Modal)
                {
                    form.Close();
                    if (main.IsHandleCreated) main.BeginInvoke(open);
                    else open();
                }
                else open();
            }
        }

        private static string DetailText(HelpTopic topic)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine(topic.Title);
            text.AppendLine(topic.Summary);
            text.AppendLine(topic.Route);
            text.AppendLine();
            for (int i = 0; i < topic.Steps.Length; i++) text.AppendLine((i + 1) + ". " + topic.Steps[i].Replace("|", ": ") + "\n");
            text.AppendLine(topic.Note);
            text.AppendLine("\n" + L.T("Beispiel: ", "Example: ") + topic.ExampleTitle);
            for (int i = 0; i < topic.ExampleSteps.Length; i++) text.AppendLine((i + 1) + ". " + topic.ExampleSteps[i]);
            text.AppendLine(L.T("Ergebnis: ", "Result: ") + topic.ExampleResult);
            foreach (var picture in topic.Pictures) text.AppendLine(picture.Caption);
            if (topic.IncludeLinks) text.AppendLine("\n" + StudioHelp.Links());
            return text.ToString();
        }

        private static void AddImage(Button button, StudioIcon icon)
        {
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            Bitmap bitmap = null;
            int lastSize = 0;
            Color lastColor = Color.Empty;
            Action update = delegate
            {
                if (button.IsDisposed) return;
                int size = Math.Max(18, button.Font.Height + 4);
                Color color = StudioSurface.Foreground(button);
                if (bitmap != null && lastSize == size && lastColor == color) return;
                lastSize = size;
                lastColor = color;
                if (bitmap != null) bitmap.Dispose();
                bitmap = StudioIcons.Create(icon, size, color);
                button.Image = bitmap;
            };
            update();
            button.FontChanged += delegate { update(); };
            button.ForeColorChanged += delegate { update(); };
            button.BackColorChanged += delegate { update(); };
            button.EnabledChanged += delegate { update(); };
            button.Resize += delegate { update(); };
            button.Disposed += delegate { if (bitmap != null) { bitmap.Dispose(); bitmap = null; } };
        }

        private static Bitmap LoadPicture(HelpPicture picture)
        {
            using (var stream = typeof(StudioHelpCards).Assembly.GetManifestResourceStream("Studio." + picture.File))
            {
                if (stream == null) throw new InvalidOperationException(L.T("Das Hilfebild fehlt: ", "The help picture is missing: ") + picture.File);
                using (var image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }

        private sealed class HelpPictureCard : Panel
        {
            readonly HelpPicture picture;
            readonly PictureButton thumbnail;
            readonly Label caption;
            internal HelpPictureCard(HelpPicture picture)
            {
                this.picture = picture;
                Name = "HelpPictureCard";
                Margin = new Padding(0, 0, 0, 16);
                BackColor = DarkTheme.Panel;
                thumbnail = new PictureButton(LoadPicture(picture)) { Name = "HelpPictureOpen", AccessibleName = L.T("Bild vergrößern: ", "Enlarge picture: ") + picture.Caption, AccessibleDescription = L.T("Enter öffnet das Bild. Im Bildfenster Einpassen oder 100 % wählen.", "Enter opens the picture. Choose Fit or 100% in the viewer."), Cursor = Cursors.Hand };
                caption = new Label { Name = "HelpPictureCaption", Text = picture.Caption, AutoSize = true, UseMnemonic = false, ForeColor = DarkTheme.Muted };
                Controls.Add(thumbnail);
                Controls.Add(caption);
                thumbnail.Click += delegate {
                    StudioEditor.Open(FindForm(), new HelpPictureForm(this.picture), delegate { if (!thumbnail.IsDisposed) thumbnail.Focus(); });
                };
            }
            internal void FitPicture(int width)
            {
                int pad = Math.Max(10, Font.Height / 2);
                int contentWidth = Math.Max(80, width - pad * 2);
                int imageHeight = Math.Max(130, Math.Min(360 * Font.Height / 16, (int)(contentWidth * thumbnail.AspectRatio)));
                thumbnail.Bounds = new Rectangle(pad, pad, contentWidth, imageHeight);
                caption.MaximumSize = new Size(contentWidth, 0);
                caption.Location = new Point(pad, thumbnail.Bottom + pad);
                Height = caption.Bottom + pad;
            }
        }

        private sealed class PictureButton : Button
        {
            Bitmap picture;
            internal float AspectRatio { get { return picture.Height / (float)picture.Width; } }
            internal PictureButton(Bitmap picture)
            {
                this.picture = picture;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Text = "";
                TabStop = true;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(DarkTheme.Panel2);
                float scale = Math.Min(Width / (float)picture.Width, Height / (float)picture.Height);
                var size = new Size(Math.Max(1, (int)(picture.Width * scale)), Math.Max(1, (int)(picture.Height * scale)));
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(picture, new Rectangle((Width - size.Width) / 2, (Height - size.Height) / 2, size.Width, size.Height));
                if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), DarkTheme.Focus, DarkTheme.Panel2);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing && picture != null) { picture.Dispose(); picture = null; }
                base.Dispose(disposing);
            }
        }

        private sealed class HelpPictureForm : Form
        {
            readonly Bitmap picture;
            readonly Panel viewport;
            readonly PictureBox image;
            readonly Label scaleLabel;
            float scale = 1;
            bool fit = true;
            internal HelpPictureForm(HelpPicture guide)
            {
                Name = "HelpPictureViewer";
                Text = L.T("Hilfebild", "Help picture");
                Font = new Font("Segoe UI", 10);
                AutoScaleMode = AutoScaleMode.Font;
                Size = new Size(1080, 760);
                MinimumSize = new Size(700, 480);
                picture = LoadPicture(guide);
                viewport = new Panel { Name = "HelpPictureViewport", Dock = DockStyle.Fill, AutoScroll = true, BackColor = DarkTheme.Back };
                image = new PictureBox { Name = "HelpPictureImage", Image = picture, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = guide.Caption, TabStop = false };
                viewport.Controls.Add(image);
                Controls.Add(viewport);
                var header = StudioChrome.Header(Text, guide.Caption);
                header.Dock = DockStyle.Top;
                Controls.Add(header);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new Padding(12, 8, 12, 8) };
                var fitButton = StudioChrome.ActionButton(L.T("Einpassen", "Fit")); fitButton.Name = "HelpPictureFit";
                var actual = StudioChrome.ActionButton("100 %"); actual.Name = "HelpPictureActual";
                var smaller = StudioChrome.ActionButton("−"); smaller.AccessibleName = L.T("Verkleinern", "Zoom out");
                var larger = StudioChrome.ActionButton("+"); larger.AccessibleName = L.T("Vergrößern", "Zoom in");
                scaleLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 8, 12, 4) };
                var close = StudioChrome.ActionButton(L.T("Schließen", "Close")); close.Name = "HelpPictureClose";
                close.Click += delegate { Close(); };
                CancelButton = close;
                fitButton.Click += delegate { fit = true; UpdateImage(); };
                actual.Click += delegate { fit = false; scale = 1; UpdateImage(); };
                smaller.Click += delegate { fit = false; scale = Math.Max(.25F, scale / 1.25F); UpdateImage(); };
                larger.Click += delegate { fit = false; scale = Math.Min(4, scale * 1.25F); UpdateImage(); };
                actions.Controls.AddRange(new Control[] { fitButton, actual, smaller, larger, scaleLabel, close });
                Controls.Add(actions);
                viewport.SizeChanged += delegate { UpdateImage(); };
                DarkTheme.Apply(this);
                Shown += delegate { UpdateImage(); fitButton.Focus(); };
            }
            void UpdateImage()
            {
                if (viewport.ClientSize.Width < 1 || viewport.ClientSize.Height < 1) return;
                if (fit) scale = Math.Min(1, Math.Min(viewport.ClientSize.Width / (float)picture.Width, viewport.ClientSize.Height / (float)picture.Height));
                viewport.AutoScrollPosition = Point.Empty;
                image.Size = new Size(Math.Max(1, (int)(picture.Width * scale)), Math.Max(1, (int)(picture.Height * scale)));
                image.Location = new Point(Math.Max(0, (viewport.ClientSize.Width - image.Width) / 2), Math.Max(0, (viewport.ClientSize.Height - image.Height) / 2));
                scaleLabel.Text = Math.Round(scale * 100) + " %";
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { image.Image = null; picture.Dispose(); }
                base.Dispose(disposing);
            }
        }

        private sealed class HelpGrid : FlowLayoutPanel
        {
            readonly int maxColumns, minWidth;
            internal HelpGrid(int maxColumns, int minWidth)
            {
                this.maxColumns = maxColumns;
                this.minWidth = minWidth;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                WrapContents = true;
                Margin = new Padding(0, 0, 0, 8);
            }

            internal void FitCards(int width)
            {
                int columns = Math.Min(Math.Min(maxColumns, Controls.Count), Math.Max(1, width / Math.Max(140, minWidth * Font.Height / 16)));
                if (maxColumns == 4 && Controls.Count == 4 && columns == 3) columns = 2;
                int margin = Math.Max(4, 6 * Font.Height / 16);
                int cardWidth = Math.Max(100, width / Math.Max(1, columns) - margin * 2);
                int height = 0;
                foreach (Control card in Controls) height = Math.Max(height, card.GetPreferredSize(new Size(cardWidth, 0)).Height);
                foreach (Control card in Controls)
                {
                    card.Margin = new Padding(margin);
                    card.Size = new Size(cardWidth, height);
                }
            }
        }

        private sealed class HelpTaskButton : Button
        {
            readonly HelpTopic topic;
            Bitmap icon;
            int iconSize;
            internal HelpTaskButton(HelpTopic topic)
            {
                this.topic = topic;
                Tag = topic;
                Text = topic.Title;
                Name = "HelpTask";
                AccessibleName = topic.Title;
                AccessibleDescription = topic.Summary;
                UseMnemonic = false;
                AutoSize = false;
                BackColor = DarkTheme.Panel2;
                Margin = new Padding(6);
                Cursor = Cursors.Hand;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                MinimumSize = new Size(0, 80);
            }

            public override Size GetPreferredSize(Size proposedSize)
            {
                int pad = Math.Max(12, Font.Height);
                int width = Math.Max(100, proposedSize.Width - pad * 2 - Font.Height * 3 - 12);
                using (var bold = new Font(Font.FontFamily, Font.Size * 1.12F, FontStyle.Bold))
                {
                    int title = TextRenderer.MeasureText(topic.Title, bold, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    int summary = TextRenderer.MeasureText(topic.Summary, Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    return new Size(proposedSize.Width, Math.Max(Font.Height * 5, title + summary + pad * 2 + 8));
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                StudioSurface.ButtonBackground(this, e.Graphics);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int pad = Math.Max(12, Font.Height);
                int circle = Font.Height * 3;
                int size = Font.Height * 2;
                if (icon == null || size != iconSize)
                {
                    if (icon != null) icon.Dispose();
                    icon = StudioIcons.Create(topic.Icon, size, DarkTheme.Accent);
                    iconSize = size;
                }
                using (var brush = new SolidBrush(DarkTheme.AccentSoft)) e.Graphics.FillEllipse(brush, pad, pad, circle, circle);
                e.Graphics.DrawImageUnscaled(icon, pad + (circle - size) / 2, pad + (circle - size) / 2);
                int x = pad + circle + 12;
                int width = Math.Max(40, Width - x - pad);
                using (var bold = new Font(Font.FontFamily, Font.Size * 1.12F, FontStyle.Bold))
                {
                    int height = TextRenderer.MeasureText(topic.Title, bold, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    TextRenderer.DrawText(e.Graphics, topic.Title, bold, new Rectangle(x, pad, width, height), DarkTheme.Fore, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(e.Graphics, topic.Summary, Font, new Rectangle(x, pad + height + 8, width, Math.Max(1, Height - pad * 2 - height - 8)), DarkTheme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                }
                if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), DarkTheme.Focus, BackColor);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && icon != null) { icon.Dispose(); icon = null; }
                base.Dispose(disposing);
            }
        }

        private sealed class HelpStepCard : Control
        {
            readonly StudioIcon stepIcon;
            readonly int number;
            readonly string title, detail;
            Bitmap icon;
            int iconSize;
            internal HelpStepCard(StudioIcon stepIcon, int number, string title, string detail)
            {
                this.stepIcon = stepIcon;
                this.number = number;
                this.title = title;
                this.detail = detail;
                Name = "HelpStep" + number;
                Text = title;
                AccessibleName = (number > 0 ? number + ". " : "") + title;
                AccessibleDescription = detail;
                AccessibleRole = AccessibleRole.Grouping;
                Margin = new Padding(6);
                TabStop = false;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            public override Size GetPreferredSize(Size proposedSize)
            {
                int pad = Math.Max(12, Font.Height);
                int width = Math.Max(70, proposedSize.Width - pad * 2);
                using (var bold = new Font(Font, FontStyle.Bold))
                {
                    int titleHeight = TextRenderer.MeasureText(title, bold, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    int detailHeight = TextRenderer.MeasureText(detail, Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    return new Size(proposedSize.Width, pad * 2 + Font.Height * 3 + titleHeight + detailHeight + 12);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(DarkTheme.Panel2);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int pad = Math.Max(12, Font.Height);
                int size = Font.Height * 2;
                int circle = Font.Height * 3;
                if (icon == null || size != iconSize)
                {
                    if (icon != null) icon.Dispose();
                    icon = StudioIcons.Create(stepIcon, size, DarkTheme.Fore);
                    iconSize = size;
                }
                using (var brush = new SolidBrush(DarkTheme.AccentSoft)) e.Graphics.FillEllipse(brush, pad, pad, circle, circle);
                e.Graphics.DrawImageUnscaled(icon, pad + (circle - size) / 2, pad + (circle - size) / 2);
                if (number > 0) TextRenderer.DrawText(e.Graphics, number.ToString("00") + " →", Font, new Rectangle(Width - pad - circle, pad, circle, circle), DarkTheme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                int y = pad + circle + 8;
                int width = Math.Max(40, Width - pad * 2);
                using (var bold = new Font(Font, FontStyle.Bold))
                {
                    int titleHeight = TextRenderer.MeasureText(title, bold, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    TextRenderer.DrawText(e.Graphics, title, bold, new Rectangle(pad, y, width, titleHeight), DarkTheme.Fore, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    y += titleHeight + 4;
                    TextRenderer.DrawText(e.Graphics, detail, Font, new Rectangle(pad, y, width, Math.Max(1, Height - y - pad)), DarkTheme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                }
                base.OnPaint(e);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && icon != null) { icon.Dispose(); icon = null; }
                base.Dispose(disposing);
            }
        }
    }
}
