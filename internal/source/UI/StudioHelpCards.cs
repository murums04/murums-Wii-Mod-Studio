using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioHelpCards
    {
        public static void Build(TabPage tab, RichTextBox legacy)
        {
            var topics = StudioHelpTopics.All();
            var root = new Panel
            {
                Dock = DockStyle.Fill
            };
            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 230,
                Padding = new Padding(0, 0, 12, 0)
            };
            var nav = new ListBox
            {
                Name = "HelpTopics",
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.None,
                AccessibleName = L.T("Hilfethemen", "Help topics")
            };
            var search = new TextBox
            {
                Name = "HelpSearch",
                Dock = DockStyle.Top,
                AccessibleName = L.T("Hilfethemen durchsuchen", "Search help topics")
            };
            sidebar.Controls.Add(nav);
            sidebar.Controls.Add(search);
            sidebar.Controls.Add(new Label { Text = L.T("Thema suchen", "Find a topic"), Dock = DockStyle.Top, Height = 25 });
            var body = new FlowLayoutPanel
            {
                Name = "HelpContent",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12, 0, 12, 12)
            };
            legacy.Visible = false;
            root.Controls.Add(legacy);
            root.Controls.Add(body);
            root.Controls.Add(sidebar);
            tab.Controls.Add(root);
            DarkTheme.StyleListBox(nav);
            bool fitting = false;
            Action fit = delegate
            {
                if (fitting)
                    return;
                fitting = true;
                body.SuspendLayout();
                try
                {
                    int width = Math.Max(160, body.ClientSize.Width - body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
                    foreach (Control tile in body.Controls)
                    {
                        // Only autosizing cards receive width constraints. A zero maximum
                        // height on RichTextBox can collapse it entirely in a flow layout.
                        tile.MinimumSize = new Size(width, 0);
                        tile.MaximumSize = new Size(width, 0);
                        tile.Width = width;
                        foreach (Control child in tile.Controls)
                            if (child is Label)
                                child.MaximumSize = new Size(Math.Max(80, width - tile.Padding.Horizontal - 4), 0);
                    }
                }
                finally
                {
                    body.ResumeLayout(true);
                    fitting = false;
                }
            };
            Action<string, string, bool, string> card = delegate (string title, string content, bool prominent, string url)
            {
                var panel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    Padding = new Padding(14),
                    Margin = new Padding(0, 0, 0, 12),
                    BorderStyle = BorderStyle.FixedSingle
                };
                Label heading;
                if (url == null)
                    heading = new Label();
                else
                {
                    var link = new LinkLabel
                    {
                        LinkColor = Color.FromArgb(193, 160, 255),
                        ActiveLinkColor = Color.White,
                        VisitedLinkColor = Color.FromArgb(193, 160, 255),
                        TabStop = true,
                        AccessibleDescription = url,
                        Tag = url,
                        Cursor = Cursors.Hand
                    };
                    link.LinkClicked += delegate
                    {
                        StudioChrome.OpenLink(link.FindForm(), url);
                    };
                    heading = link;
                }

                heading.Text = title;
                heading.UseMnemonic = false;
                heading.AutoSize = true;
                heading.Font = new Font("Segoe UI", prominent ? 18 : 12, FontStyle.Bold);
                heading.Margin = new Padding(0, 0, 0, 8);
                panel.Controls.Add(heading);
                panel.Controls.Add(new Label { Text = content, AutoSize = true, Font = new Font("Segoe UI", 10.5f), Margin = Padding.Empty, UseMnemonic = false });
                body.Controls.Add(panel);
            };
            Action render = delegate
            {
                body.SuspendLayout();
                try
                {
                    body.AutoScrollPosition = Point.Empty;
                    while (body.Controls.Count > 0)
                        body.Controls[0].Dispose();
                    var topic = nav.SelectedItem as HelpTopic;
                    if (topic == null)
                        card(L.T("Keine Treffer", "No matches"), L.T("Versuche einen anderen Suchbegriff.", "Try a different search term."), true, null);
                    else
                    {
                        card(topic.Title, topic.Summary, true, null);
                        card(L.T("Hier starten", "Start here"), topic.Route, false, null);
                        for (int i = 0; i < topic.Steps.Length; i++)
                        {
                            string[] parts = topic.Steps[i].Split(new[] { '|' }, 2);
                            card((i + 1).ToString("00") + "   " + parts[0], parts.Length > 1 ? parts[1] : "", false, null);
                        }

                        card(L.T("Gut zu wissen", "Good to know"), topic.Note, false, null);
                        if (topic.IncludeLinks)
                            foreach (var link in StudioHelp.ReferenceLinks())
                                card(link.Title, link.Url, false, link.Url);
                    }

                    DarkTheme.Apply(body);
                    fit();
                }
                finally
                {
                    body.ResumeLayout(true);
                }
            };
            bool filtering = false;
            nav.SelectedIndexChanged += delegate
            {
                if (!filtering)
                    render();
            };
            search.TextChanged += delegate
            {
                var selected = nav.SelectedItem;
                filtering = true;
                nav.BeginUpdate();
                try
                {
                    nav.Items.Clear();
                    foreach (var topic in topics)
                    {
                        string searchable = topic.Title + " " + topic.Summary + " " + topic.Route + " " + topic.Note + " " + string.Join(" ", topic.Steps) + (topic.IncludeLinks ? " " + StudioHelp.Links() : "");
                        if (searchable.IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                            nav.Items.Add(topic);
                    }

                    if (selected != null && nav.Items.Contains(selected))
                        nav.SelectedItem = selected;
                    else if (nav.Items.Count > 0)
                        nav.SelectedIndex = 0;
                }
                finally
                {
                    nav.EndUpdate();
                    filtering = false;
                }

                render();
            };
            body.SizeChanged += delegate
            {
                fit();
            };
            nav.Items.AddRange(topics.ToArray());
            nav.SelectedIndex = 0;
            StudioUx.SetHelp(search, L.T("Suche nach einem Tool, einer Datei, einem Link oder einem Arbeitsschritt.", "Search for a tool, file, link or workflow step."));
        }
    }
}
