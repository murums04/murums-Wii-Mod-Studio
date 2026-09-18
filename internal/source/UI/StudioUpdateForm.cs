using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class StudioUpdateForm : Form
    {
        static bool showing;
        readonly Label status = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false
        };
        readonly RichTextBox notes = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            DetectUrls = false
        };
        readonly ProgressBar progress = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Maximum = 100
        };
        readonly Button install = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(185, 36),
            Enabled = false
        };
        readonly Button later = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(100, 36),
            DialogResult = DialogResult.Cancel
        };
        readonly BackgroundWorker download = new BackgroundWorker
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };
        StudioUpdateRelease release;
        bool downloading;
        string installer;
        internal StudioUpdateForm(StudioUpdateRelease available)
        {
            Text = L.T("Studio aktualisieren", "Update Studio");
            Font = new Font("Segoe UI", 10);
            Size = new Size(700, 530);
            MinimumSize = new Size(570, 440);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(18)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.Controls.Add(StudioChrome.Header(L.T("Updates", "Updates"), L.T("Installiert: ", "Installed: ") + StudioVersion.Current), 0, 0);
            status.Padding = new Padding(0, 12, 0, 4);
            layout.Controls.Add(status, 0, 1);
            layout.Controls.Add(notes, 0, 2);
            layout.Controls.Add(progress, 0, 3);
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            later.Text = L.T("Später", "Later");
            install.Text = L.T("Jetzt aktualisieren", "Update now");
            buttons.Controls.Add(later);
            buttons.Controls.Add(install);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);
            CancelButton = later;
            DarkTheme.Apply(this);
            install.BackColor = DarkTheme.Accent;
            install.Click += delegate
            {
                if (!StudioUpdate.CanInstall)
                {
                    StudioChrome.OpenLink(this, StudioUpdate.ReleasesUrl);
                    return;
                }

                downloading = true;
                install.Enabled = false;
                later.Text = L.T("Abbrechen", "Cancel");
                status.Text = L.T("Update wird heruntergeladen und geprüft…", "Downloading and verifying the update…");
                download.RunWorkerAsync();
            };
            download.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                try
                {
                    e.Result = StudioUpdate.Download(release, delegate (int value)
                    {
                        download.ReportProgress(value);
                    }, delegate
                    {
                        return download.CancellationPending;
                    });
                }
                catch (OperationCanceledException)
                {
                    e.Cancel = true;
                }
            };
            download.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
            {
                if (!IsDisposed)
                    progress.Value = e.ProgressPercentage;
            };
            download.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                downloading = false;
                if (IsDisposed)
                    return;
                if (e.Cancelled || download.CancellationPending)
                {
                    Close();
                    return;
                }

                if (e.Error != null)
                {
                    status.Text = L.T("Download fehlgeschlagen. Du kannst es erneut versuchen.", "Download failed. You can retry.");
                    notes.Text = e.Error.Message;
                    progress.Value = 0;
                    install.Enabled = true;
                    later.Text = L.T("Schließen", "Close");
                    return;
                }

                installer = (string)e.Result;
                DialogResult = DialogResult.OK;
                Close();
            };
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (downloading)
                {
                    download.CancelAsync();
                    status.Text = L.T("Download wird abgebrochen…", "Cancelling download…");
                    e.Cancel = true;
                }
            };
            if (available != null)
                ShowRelease(available);
            else
                Shown += delegate
                {
                    CheckManually();
                };
        }

        void ShowRelease(StudioUpdateRelease available)
        {
            release = available;
            status.Text = L.T("Neue Version: ", "New version: ") + release.Tag + Environment.NewLine + (StudioUpdate.CanInstall ? L.T("Studio wird geschlossen. Projekte und optionale Tools bleiben erhalten.", "Studio will close. Projects and optional tools are kept.") : L.T("Für diesen Entwicklungsordner öffne bitte die Download-Seite.", "For this development folder, please open the downloads page."));
            notes.Text = String.IsNullOrWhiteSpace(release.Notes) ? L.T("Keine Versionshinweise vorhanden.", "No release notes provided.") : release.Notes;
            install.Text = StudioUpdate.CanInstall ? L.T("Jetzt aktualisieren", "Update now") : L.T("Downloads öffnen", "Open downloads");
            install.Enabled = true;
        }

        void CheckManually()
        {
            status.Text = L.T("Suche nach Updates…", "Checking for updates…");
            progress.Style = ProgressBarStyle.Marquee;
            var worker = new BackgroundWorker();
            worker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                e.Result = StudioUpdate.Check();
            };
            worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                worker.Dispose();
                if (IsDisposed)
                    return;
                progress.Style = ProgressBarStyle.Blocks;
                later.Text = L.T("Schließen", "Close");
                if (e.Error != null)
                {
                    status.Text = L.T("Updates konnten nicht geprüft werden. Studio funktioniert weiterhin offline.", "Updates could not be checked. Studio still works offline.");
                    notes.Text = e.Error.Message;
                }
                else if (e.Result == null)
                    status.Text = L.T("Du verwendest die aktuelle verfügbare Version.", "You are using the latest available version.");
                else
                    ShowRelease((StudioUpdateRelease)e.Result);
            };
            worker.RunWorkerAsync();
        }

        internal static void ShowUpdates(Form owner, StudioUpdateRelease release)
        {
            if (showing)
                return;
            showing = true;
            try
            {
                using (var dialog = new StudioUpdateForm(release))
                {
                    if (dialog.ShowDialog(owner) != DialogResult.OK || dialog.installer == null)
                        return;
                    try
                    {
                        // The separate installer waits for the installed EXE to unlock.
                        // Normal FormClosing handlers still let users save or cancel closing.
                        StudioUpdate.LaunchInstaller(dialog.installer, dialog.release);
                        var exit = new CancelEventArgs();
                        Application.Exit(exit);
                        if (exit.Cancel)
                            StudioMessageBox.Show(owner, L.T("Der Updater wartet. Speichere deine Änderungen und schließe Studio, um fortzufahren.", "The updater is waiting. Save your changes and close Studio to continue."), L.T("Update wartet", "Update waiting"));
                    }
                    catch (Exception ex)
                    {
                        StudioMessageBox.Show(owner, ex.Message, L.T("Update fehlgeschlagen", "Update failed"));
                    }
                }
            }
            finally
            {
                showing = false;
            }
        }

        internal static void CheckAtStartup(Form owner)
        {
            CheckAtStartup(owner, StudioUpdate.Check, ShowUpdates);
        }

        internal static void CheckAtStartup(Form owner, Func<StudioUpdateRelease> check, Action<Form, StudioUpdateRelease> notify)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                e.Result = check();
            };
            worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                worker.Dispose();
                // Startup errors are intentionally quiet: offline use must stay normal.
                if (owner.IsDisposed || e.Error != null || e.Cancelled || e.Result == null)
                    return;
                notify(owner, (StudioUpdateRelease)e.Result);
            };
            worker.RunWorkerAsync();
        }
    }
}
