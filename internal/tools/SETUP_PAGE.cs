using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Forms;
using murumsWiiModStudio.Setup;

internal sealed class SetupPage : Form
{
    readonly TextBox path = new TextBox(), log = new TextBox();
    readonly DataGridView tools = new DataGridView();
    readonly Label status = new Label();
    readonly ProgressBar progress = new ProgressBar();
    readonly Button install = new Button(), launch = new Button(), browse = new Button(), installTools = new Button();
    readonly FlowLayoutPanel toolActions = new FlowLayoutPanel();
    readonly RowStyle toolActionRow = new RowStyle(SizeType.Absolute, 0);
    readonly Timer timer = new Timer();
    Process worker;
    string destination, statusFile, logFile;
    bool extracted, registered;
    static readonly Color Background = Color.FromArgb(20, 21, 26), PanelColor = Color.FromArgb(29, 30, 37), Purple = Color.FromArgb(139, 92, 246);
    public SetupPage()
    {
        Text = "murums Wii Mod Studio — Setup · v" + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
        ClientSize = new Size(1080, 720);
        MinimumSize = new Size(1056, 739);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(24, 16, 24, 12),
            BackColor = PanelColor
        };
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = Icon.ToBitmap(), Margin = new Padding(0, 12, 20, 12) };
        logo.Disposed += delegate { logo.Image.Dispose(); };
        heading.Controls.Add(logo, 0, 0);
        var headings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        headings.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        headings.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        headings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        headings.Controls.Add(new Label { Text = "murums Wii Mod Studio", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, 0, 0);
        headings.Controls.Add(new Label { Text = "Install Studio", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 20, FontStyle.Bold) }, 0, 1);
        headings.Controls.Add(new Label { Text = "Choose a folder • Select optional tools • Start creating", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(190, 196, 210) }, 0, 2);
        heading.Controls.Add(headings, 1, 0);
        header.Controls.Add(heading);
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 106,
            Padding = new Padding(24, 8, 24, 12),
            ColumnCount = 2,
            RowCount = 3
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));
        status.Text = "Ready to install.";
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.AutoEllipsis = true;
        footer.Controls.Add(status, 0, 0);
        progress.Dock = DockStyle.Fill;
        progress.Margin = Padding.Empty;
        footer.Controls.Add(progress, 0, 2);
        footer.SetColumnSpan(progress, 2);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 5, 0, 0)
        };
        var close = Button("Close");
        close.Width = 96;
        install.Text = "Install";
        launch.Text = "Launch program";
        launch.Visible = false;
        Style(install);
        Style(launch);
        install.Width = 116;
        launch.Width = 150;
        install.BackColor = Purple;
        launch.BackColor = Purple;
        var details = Button("Details");
        details.Width = 86;
        details.Height = 26;
        details.Anchor = AnchorStyles.Right;
        details.Margin = Padding.Empty;
        details.FlatAppearance.BorderSize = 0;
        details.ForeColor = Color.FromArgb(193, 160, 255);
        footer.Controls.Add(details, 0, 1);
        actions.Controls.Add(close);
        actions.Controls.Add(install);
        actions.Controls.Add(launch);
        footer.Controls.Add(actions, 1, 1);
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 8),
            ColumnCount = 2,
            RowCount = 1
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 254));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            BackColor = PanelColor,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 20, 0)
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        sidebar.Controls.Add(new Label { Text = "Included tools", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(193, 160, 255), Font = new Font("Segoe UI", 10, FontStyle.Bold) });
        string[] titles =
        {
            "MKWii Race & Game HUD",
            "Archives & textures",
            "Fonts & messages",
            "Backgrounds & custom packs",
            "Audio & models",
            "Projects & help"
        };
        string[] descriptions =
        {
            "Move, resize and recolour",
            "Edit, replace and export",
            "Fonts and game text",
            "Menus, skies and pack creation",
            "WAV loops and workflows",
            "Themes, previews and guides"
        };
        for (int i = 0; i < titles.Length; i++)
        {
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666f));
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            card.Controls.Add(new Label { Text = descriptions[i], Dock = DockStyle.Fill, ForeColor = Color.FromArgb(177, 179, 194), Font = new Font("Segoe UI", 9), Padding = new Padding(0, 2, 0, 0) });
            card.Controls.Add(new Label { Text = titles[i], UseMnemonic = false, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 10, FontStyle.Bold) });
            sidebar.Controls.Add(card);
        }

        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        sidebar.Controls.Add(new Label { Text = "Game files are not included.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(177, 179, 194), TextAlign = ContentAlignment.BottomLeft });
        body.Controls.Add(sidebar, 0, 0);
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int height in new[]
        {
            28,
            38,
            30,
            42
        }

        )
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(toolActionRow);
        Add(content, "Installation folder", 28, true);
        var folderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        folderRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "murums Wii Mod Studio");
        path.Text = basePath;
        int suffix = 2;
        while (Directory.Exists(path.Text) && Directory.GetFileSystemEntries(path.Text).Length > 0)
            path.Text = basePath + " " + suffix++;
        path.Dock = DockStyle.None;
        path.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        path.Margin = new Padding(0, 0, 12, 0);
        path.BackColor = PanelColor;
        path.ForeColor = Color.White;
        path.BorderStyle = BorderStyle.FixedSingle;
        browse.Text = "Browse…";
        Style(browse);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(0, 3, 0, 3);
        browse.FlatAppearance.BorderColor = Color.FromArgb(83, 85, 103);
        folderRow.Controls.Add(path, 0, 0);
        folderRow.Controls.Add(browse, 1, 0);
        content.Controls.Add(folderRow);
        Add(content, "Choose an empty folder.", 28);
        Add(content, "Optional tools  ·  Download now or add later", 38, true);
        tools.Dock = DockStyle.Fill;
        tools.BackgroundColor = PanelColor;
        tools.BorderStyle = BorderStyle.None;
        tools.ScrollBars = ScrollBars.Vertical;
        tools.AllowUserToAddRows = false;
        tools.AllowUserToDeleteRows = false;
        tools.AllowUserToResizeRows = false;
        tools.AllowUserToResizeColumns = false;
        tools.RowHeadersVisible = false;
        tools.AutoGenerateColumns = false;
        tools.EnableHeadersVisualStyles = false;
        tools.ColumnHeadersHeight = 30;
        tools.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        tools.Font = new Font("Segoe UI", 9);
        tools.DefaultCellStyle.BackColor = PanelColor;
        tools.DefaultCellStyle.ForeColor = Color.White;
        tools.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(25, 26, 33);
        tools.DefaultCellStyle.SelectionBackColor = PanelColor;
        tools.DefaultCellStyle.SelectionForeColor = Color.White;
        tools.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 40, 54);
        tools.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(199, 185, 235);
        tools.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        tools.RowTemplate.Height = 40;
        tools.GridColor = Color.FromArgb(48, 49, 62);
        tools.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        tools.Columns.Add(new DataGridViewCheckBoxColumn { Width = 30 });
        tools.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tool", Width = 166, ReadOnly = true });
        tools.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "When to install", Width = 108, ReadOnly = true });
        tools.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "What it adds", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("tools.tsv")))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] c = line.Split('\t');
                if (c.Length != 5)
                    throw new InvalidDataException("Invalid tool catalog.");
                int row = tools.Rows.Add(c[4] == "True", c[1], c[2], c[3]);
                tools.Rows[row].Tag = c[0];
            }
        }

        tools.SizeChanged += delegate
        {
            if (tools.Rows.Count > 0)
            {
                int height = Math.Max(32, (tools.ClientSize.Height - tools.ColumnHeadersHeight - 2) / tools.Rows.Count);
                foreach (DataGridViewRow row in tools.Rows)
                    row.Height = height;
            }
        };
        tools.CurrentCellDirtyStateChanged += delegate
        {
            if (tools.IsCurrentCellDirty)
                tools.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        content.Controls.Add(tools);
        toolActions.Dock = DockStyle.Fill;
        toolActions.FlowDirection = FlowDirection.RightToLeft;
        toolActions.WrapContents = false;
        toolActions.Margin = Padding.Empty;
        toolActions.Visible = false;
        installTools.Text = "Install selected tools";
        Style(installTools);
        installTools.Width = 184;
        installTools.Margin = new Padding(0, 6, 0, 0);
        toolActions.Controls.Add(installTools);
        content.Controls.Add(toolActions);
        body.Controls.Add(content, 1, 0);
        installTools.Click += delegate
        {
            BeginInstall();
        };
        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
        Controls.Add(new AccentStrip());
        details.Click += delegate
        {
            using (var dialog = CreateDetailsDialog())
                dialog.ShowDialog(this);
        };
        browse.Click += delegate
        {
            try
            {
                string initial = path.Text;
                try
                {
                    initial = Path.GetFullPath(initial);
                    while (!Directory.Exists(initial) && !String.IsNullOrEmpty(initial))
                        initial = Path.GetDirectoryName(initial);
                }
                catch
                {
                    initial = null;
                }

                using (var picker = new murumsWiiModStudio.FolderPickerDialog
                {
                    Description = "Choose an empty installation folder",
                    SelectedPath = initial
                }

                )
                    if (picker.ShowDialog(this) == DialogResult.OK)
                        path.Text = picker.SelectedPath;
            }
            catch (Exception e)
            {
                Error(e);
            }
        };
        install.Click += delegate
        {
            BeginInstall();
        };
        launch.Click += delegate
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = Path.Combine(destination, "murums Wii Mod Studio.exe"), WorkingDirectory = destination, UseShellExecute = false });
                Close();
            }
            catch (Exception e)
            {
                Error(e);
            }
        };
        close.Click += delegate
        {
            Close();
        };
        timer.Interval = 300;
        timer.Tick += delegate
        {
            Poll();
        };
        FormClosing += delegate (object sender, FormClosingEventArgs e)
        {
            if (worker != null && !worker.HasExited)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Please wait for installation to finish.", "Installation in progress");
            }
        };
    }

    internal Form CreateDetailsDialog()
    {
        var dialog = new Form
        {
            Text = "Installation details — murums Wii Mod Studio",
            Icon = Icon,
            Size = new Size(760, 460),
            MinimumSize = new Size(540, 320),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Background,
            ForeColor = Color.White,
            Font = Font
        };
        var heading = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(16, 12, 16, 10),
            BackColor = PanelColor
        };
        var logo = new PictureBox
        {
            Image = Icon.ToBitmap(),
            Dock = DockStyle.Left,
            Width = 44,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        heading.Controls.Add(new Label { Text = "Installation details", Dock = DockStyle.Fill, Padding = new Padding(14, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 16, FontStyle.Bold) });
        heading.Controls.Add(logo);
        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Background,
            ForeColor = Color.White,
            Text = String.IsNullOrEmpty(log.Text) ? "Installation has not started yet." : log.Text
        };
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };
        body.Controls.Add(text);
        dialog.Controls.Add(body);
        dialog.Controls.Add(heading);
        dialog.Controls.Add(new AccentStrip());
        var refresh = new Timer
        {
            Interval = 300
        };
        refresh.Tick += delegate
        {
            string value = log.Text;
            if (!String.IsNullOrEmpty(value) && text.Text != value)
            {
                text.Text = value;
                text.SelectionStart = text.TextLength;
                text.ScrollToCaret();
            }
        };
        refresh.Start();
        dialog.FormClosed += delegate
        {
            refresh.Dispose();
            logo.Image.Dispose();
        };
        return dialog;
    }

    internal void ShowCompletedState(int code)
    {
        install.Enabled = true;
        tools.Enabled = true;
        installTools.Enabled = true;
        launch.Visible = registered;
        if (registered)
        {
            install.Visible = false;
            toolActionRow.Height = 46;
            toolActions.Visible = true;
            installTools.Text = code == 0 ? "Install selected tools" : "Retry selected tools";
            AcceptButton = launch;
        }
        else
        {
            install.Text = "Retry";
        }

        status.Text = code == 0 ? "Installed. Ready to launch." : "Installation did not finish. Open Details, then retry.";
        if (code == 0)
            progress.Value = 100;
    }

    static Button Button(string text)
    {
        var b = new Button
        {
            Text = text
        };
        Style(b);
        return b;
    }

    static void Style(Button b)
    {
        b.Size = new Size(140, 36);
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = PanelColor;
        b.ForeColor = Color.White;
    }

    static void Add(TableLayoutPanel p, string text, int height, bool bold = false, int size = 10)
    {
        p.Controls.Add(new Label { Text = text, Dock = DockStyle.Top, Height = height, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft });
    }

    void Error(Exception e)
    {
        MessageBox.Show(this, e.Message, "murums Wii Mod Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    void BeginInstall()
    {
        if (worker != null)
            return;
        try
        {
            if (!extracted)
            {
                destination = Path.GetFullPath(path.Text);
                SetupBundle.Extract(destination);
                extracted = true;
                path.Enabled = false;
                browse.Enabled = false;
            }

            if (!registered)
            {
                SetupBundle.Register(destination);
                registered = true;
            }

            var selected = new List<string>();
            foreach (DataGridViewRow row in tools.Rows)
                if (Convert.ToBoolean(row.Cells[0].Value))
                    selected.Add((string)row.Tag);
            string token = Guid.NewGuid().ToString("N");
            statusFile = Path.Combine(Path.GetTempPath(), "murums-setup-" + token + ".status");
            logFile = Path.Combine(Path.GetTempPath(), "murums-setup-" + token + ".log");
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                WorkingDirectory = destination,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Path.Combine(destination, "internal", "tools", "SETUP_WORKER.ps1") + "\" -Root \"" + destination + "\" -StatusFile \"" + statusFile + "\" -LogFile \"" + logFile + "\" -InstallTools " + (selected.Count == 0 ? "0" : "1");
            if (selected.Count > 0)
                psi.Arguments += " -SelectedTools \"" + String.Join(",", selected.ToArray()) + "\"";
            worker = Process.Start(psi);
            installTools.Enabled = false;
            install.Enabled = false;
            tools.Enabled = false;
            launch.Visible = false;
            log.Clear();
            status.Text = "Installing…";
            progress.Value = 0;
            timer.Start();
        }
        catch (Exception e)
        {
            Error(e);
        }
    }

    static string ReadShared(string p)
    {
        if (!File.Exists(p))
            return "";
        using (var f = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var r = new StreamReader(f))
            return r.ReadToEnd();
    }

    void Poll()
    {
        try
        {
            string[] lines = ReadShared(statusFile).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int value;
            if (lines.Length > 0 && Int32.TryParse(lines[0], out value))
                progress.Value = Math.Max(0, Math.Min(100, value));
            if (lines.Length > 1)
                status.Text = lines[1];
            string text = ReadShared(logFile);
            if (log.Text != text)
            {
                log.Text = text;
                log.SelectionStart = log.TextLength;
                log.ScrollToCaret();
            }
        }
        catch (IOException)
        {
        }

        if (worker == null || !worker.HasExited)
            return;
        int code = worker.ExitCode;
        worker.Dispose();
        worker = null;
        timer.Stop();
        ShowCompletedState(code);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            if (worker != null)
                worker.Dispose();
            foreach (string file in new[]
            {
                statusFile,
                logFile
            }

            )
                try
                {
                    if (file != null)
                        File.Delete(file);
                }
                catch (IOException)
                {
                }
        }

        base.Dispose(disposing);
    }
}
