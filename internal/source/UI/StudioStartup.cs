using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioStartup : IDisposable
    {
        readonly Form owner;
        readonly StudioWorkspace workspace;
        readonly Action<string> completed;
        readonly Timer steps = new Timer { Interval = 1 };
        readonly Panel cover;
        readonly Label status;
        readonly StudioProgressBar progress;
        readonly string[] keys;
        readonly List<string> failures = new List<string>();
        Form staged;
        int index;
        bool paused, disposed;
        internal static bool IsPreparing { get; private set; }
        internal bool Running { get; private set; }
        internal bool Completed { get; private set; }
        internal int PreparedCount { get { return index; } }

        internal StudioStartup(Form owner, StudioWorkspace workspace, Action<string> completed)
        {
            this.owner = owner;
            this.workspace = workspace;
            this.completed = completed;
            keys = workspace.PreparationKeys;
            cover = new Panel { Name = "StudioStartupCover", Dock = DockStyle.Fill, BackColor = DarkTheme.Back, Visible = false };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(32) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            status = new Label { Name = "StudioStartupStatus", AutoSize = true, Dock = DockStyle.Fill, ForeColor = DarkTheme.Fore,
                TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false, Font = owner.Font,
                Text = L.T("Werkzeuge werden vorbereitet…", "Preparing tools…") };
            progress = new StudioProgressBar { Name = "StudioStartupProgress", Dock = DockStyle.Fill, Margin = Padding.Empty };
            layout.Controls.Add(status, 0, 1);
            layout.Controls.Add(progress, 0, 3);
            cover.Controls.Add(layout);
            workspace.Controls.Add(cover);
            steps.Tick += Step;
            owner.Resize += OwnerResize;
            owner.Disposed += OwnerDisposed;
        }

        internal void Start()
        {
            if (disposed || Running || Completed) return;
            Running = true;
            cover.Show();
            cover.BringToFront();
            Resume();
        }

        internal void Pause() { paused = true; steps.Stop(); }
        internal void Resume()
        {
            paused = false;
            if (!disposed && Running && !owner.IsDisposed && owner.WindowState != FormWindowState.Minimized) steps.Start();
        }

        internal void RestartAfterCancelledClose()
        {
            if (disposed) return;
            Completed = false;
            Running = true;
            cover.Show();
            cover.BringToFront();
            workspace.FinishPreparation(staged);
            staged = null;
            index = 0;
            progress.Value = 0;
            failures.Clear();
            Resume();
        }

        void OwnerResize(object sender, EventArgs e)
        {
            if (owner.WindowState == FormWindowState.Minimized) steps.Stop();
            else if (Running && !paused && !disposed) steps.Start();
        }

        void Step(object sender, EventArgs e)
        {
            steps.Stop();
            if (disposed || paused || owner.IsDisposed || owner.Disposing || owner.WindowState == FormWindowState.Minimized) return;
            if (staged != null)
            {
                workspace.FinishPreparation(staged);
                staged = null;
                index++;
                progress.Value = keys.Length == 0 ? 100 : index * 100 / keys.Length;
            }
            if (index == keys.Length)
            {
                Running = false;
                Completed = true;
                cover.Hide();
                completed(failures.Count == 0 ? null : L.F("Nicht vorbereitet: {0}. Werkzeug auswählen, um es erneut zu versuchen.",
                    "Not prepared: {0}. Select the tool to retry.", String.Join(", ", failures.ToArray())));
                return;
            }
            string key = keys[index];
            status.Text = L.F("Werkzeuge vorbereiten · {0}/{1}\n{2}", "Preparing tools · {0}/{1}\n{2}", index + 1, keys.Length, workspace.PageTitle(key));
            cover.Update();
            try
            {
                // Konstruktion und erste Anzeige bleiben auf dem UI-Thread.
                IsPreparing = true;
                staged = workspace.PreparePage(key);
                cover.BringToFront();
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError("Startup preparation ({0}): {1}", key, error);
                workspace.DiscardFailedPreparation(key);
                failures.Add(workspace.PageTitle(key));
                index++;
                progress.Value = keys.Length == 0 ? 100 : index * 100 / keys.Length;
            }
            finally { IsPreparing = false; }
            // Shown und Layout-Nachrichten werden vor dem nächsten Schritt verarbeitet.
            if (!disposed && !paused && owner.WindowState != FormWindowState.Minimized) steps.Start();
        }

        void OwnerDisposed(object sender, EventArgs e) { Dispose(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Running = false;
            steps.Stop();
            steps.Dispose();
            owner.Resize -= OwnerResize;
            owner.Disposed -= OwnerDisposed;
            cover.Dispose();
            staged = null;
        }
    }
}
