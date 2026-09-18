using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal class StudioToolForm : Form
    {
        protected readonly FlowLayoutPanel Actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 0, 0, 8)
        };
        protected readonly FlowLayoutPanel Footer = new FlowLayoutPanel
        {
            Name = "ExportActions",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4)
        };
        Button primary;
        protected readonly Panel Body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        protected readonly Label Status = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Padding = new Padding(8)
        };
        protected StudioToolForm(string title, string subtitle, string fileExamples = null)
        {
            Text = title + " — murums Wii Mod Studio";
            Size = new Size(1120, 800);
            MinimumSize = new Size(950, 680);
            Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, fileExamples == null ? 118 : 152));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Control header = StudioChrome.Header(title, subtitle);
            root.Controls.Add(fileExamples == null ? header : ToolFileHint.Wrap(header, fileExamples), 0, 0);
            root.Controls.Add(Actions, 0, 1);
            root.Controls.Add(Body, 0, 2);
            root.Controls.Add(ToolStatus.Wrap(this, Status), 0, 4);
            root.Controls.Add(Footer, 0, 3);
            Controls.Add(root);
        }

        protected void Finish()
        {
            string[] packTools = { "FontChangerForm", "GameHudForm", "ThemeProjectForm", "ArchiveCompareForm", "MenuTextForm", "MusicLoopForm", "MenuTextureForm" };
            if (Array.IndexOf(packTools, GetType().Name) >= 0)
                PackSelection.Attach(this, OnPackSelected);
            DarkTheme.Apply(this);
            StudioUx.Attach(this);
            ToolStatus.Watch(this);
            if (primary != null)
            {
                primary.BackColor = DarkTheme.Accent;
                primary.ForeColor = Color.White;
            }
        }

        protected Button Action(string caption, string help, Action action)
        {
            var b = new Button
            {
                Text = caption,
                AutoSize = true,
                Height = 34,
                Padding = new Padding(8, 3, 8, 3),
                Margin = new Padding(3, 8, 3, 3)
            };
            b.Click += delegate
            {
                Guard(action);
            };
            StudioUx.SetHelp(b, help);
            Actions.Controls.Add(b);
            return b;
        }

        protected Button ExportAction(string caption, string help, Action action, bool isPrimary = true)
        {
            var button = Action(caption, help, action);
            Actions.Controls.Remove(button);
            Footer.Controls.Add(button);
            button.MinimumSize = new Size(130, 36);
            if (isPrimary)
            {
                primary = button;
                Footer.Controls.SetChildIndex(button, 0);
            }

            return button;
        }

        protected void Guard(Action action)
        {
            try
            {
                ToolStatus.Set(this, false);
                UseWaitCursor = true;
                action();
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        protected virtual void OnPackSelected(CustomPack pack) { }

        protected string OpenPath(string filter)
        {
            if (filter.Contains("*.szs") || filter.Contains("*.arc") || filter.Contains("*.u8")
                || filter.Contains("Original destination in your pack"))
            {
                string preferred = Text.Contains("Font Changer") ? "Font.szs" : null;
                return GameArchiveImportForm.Select(this, filter, preferred);
            }
            using (var d = new OpenFileDialog
            {
                InitialDirectory = PackSelection.Folder(this), Filter = filter
            }

            )
                return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        }

        protected string SavePath(string name, string filter)
        {
            string output = PackSelection.Output(this, "");
            if (output.Length > 0) Directory.CreateDirectory(output);
            using (var d = new SaveFileDialog
            {
                FileName = name,
                InitialDirectory = output, Filter = filter
            }

            )
                return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        }

        protected string Folder(string path, bool forExport = true)
        {
            using (var d = new FolderPickerDialog
            {
                SelectedPath = forExport ? PackSelection.Output(this, path) : (String.IsNullOrEmpty(PackSelection.Folder(this)) ? path : PackSelection.Folder(this)),
                Description = "Choose a folder"
            }

            )
                return d.ShowDialog(this) == DialogResult.OK ? d.SelectedPath : null;
        }
    }
}
