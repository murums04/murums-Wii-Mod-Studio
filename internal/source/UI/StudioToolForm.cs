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
        protected StudioToolForm(string title, string subtitle)
        {
            Text = title + " — murums Wii Mod Studio";
            Size = new Size(1120, 800);
            MinimumSize = new Size(950, 680);
            Font = new Font("Segoe UI", 10);
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(StudioChrome.Header(title, subtitle), 0, 0);
            root.Controls.Add(Actions, 0, 1);
            root.Controls.Add(Body, 0, 2);
            root.Controls.Add(Status, 0, 3);
            root.Controls.Add(Footer, 0, 4);
            Controls.Add(root);
        }

        protected void Finish()
        {
            DarkTheme.Apply(this);
            StudioUx.Attach(this);
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
            button.MinimumSize = new Size(200, 36);
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

        protected string OpenPath(string filter)
        {
            using (var d = new OpenFileDialog
            {
                Filter = filter
            }

            )
                return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        }

        protected string SavePath(string name, string filter)
        {
            using (var d = new SaveFileDialog
            {
                FileName = name,
                Filter = filter
            }

            )
                return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        }

        protected string Folder(string path)
        {
            using (var d = new FolderPickerDialog
            {
                SelectedPath = path,
                Description = "Choose a folder"
            }

            )
                return d.ShowDialog(this) == DialogResult.OK ? d.SelectedPath : null;
        }
    }
}
