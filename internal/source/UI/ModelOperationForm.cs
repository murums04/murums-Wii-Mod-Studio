using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class ModelOperationForm : Form
    {
        static Task<T> RunSta<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>();
            var thread = new Thread(() => {
                try { completion.SetResult(action()); }
                catch (Exception error) { completion.SetException(error); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
        internal static T Run<T>(Form owner, string title, Func<CancellationToken, T> operation)
        {
            using (var form = new ModelOperationForm())
            using (var cancellation = new CancellationTokenSource())
            {
                T result = default(T); Exception failure = null; bool complete = false;
                form.Text = title;
                form.Font = new Font("Segoe UI", 10); form.ClientSize = new Size(580, 280);
                form.StartPosition = FormStartPosition.CenterParent; form.ShowInTaskbar = false;
                form.FormBorderStyle = FormBorderStyle.FixedDialog; form.MaximizeBox = false; form.MinimizeBox = false;
                var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 4, ColumnCount = 1 };
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, StudioChrome.HeaderHeight)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 12)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                panel.Controls.Add(StudioChrome.Header(title, L.T("Verarbeitung innerhalb von Studio", "Processing inside Studio")), 0, 0);
                var message = new Label { Dock = DockStyle.Fill, Text = L.T("Bitte warten. Beim ersten Modellvorgang wird die interne Komponente vorbereitet. Deine Quelldatei bleibt erhalten.", "Please wait. The first model operation prepares the internal component. Your source file is kept."), Padding = new Padding(6) };
                panel.Controls.Add(message, 0, 1);
                var progress = new StudioProgressBar { Indeterminate = true, Margin = new Padding(6, 4, 6, 4) };
                panel.Controls.Add(progress, 0, 2);
                var cancel = new Button { Text = L.T("Abbrechen", "Cancel"), AutoSize = true, Anchor = AnchorStyles.Right };
                cancel.Click += delegate { cancellation.Cancel(); cancel.Enabled = false; message.Text = L.T("Vorgang wird abgebrochen…", "Cancelling…"); };
                panel.Controls.Add(cancel, 0, 3); form.Controls.Add(panel); DarkTheme.Apply(form);
                form.FormClosing += delegate(object sender, FormClosingEventArgs args) { if (!complete) { cancellation.Cancel(); args.Cancel = true; } };
                form.Shown += async delegate {
                    try
                    {
                        result = await RunSta(() => ModelRuntime.RunOperation(() => {
                            var value = operation(cancellation.Token);
                            cancellation.Token.ThrowIfCancellationRequested();
                            return value;
                        }, (percent, text) => {
                            if (form.IsDisposed || !form.IsHandleCreated) return;
                            form.BeginInvoke(new Action(() => {
                                if (complete || cancellation.IsCancellationRequested) return;
                                progress.Indeterminate = percent < 0;
                                if (percent >= 0) progress.Value = percent;
                                if (!String.IsNullOrEmpty(text)) message.Text = text;
                            }));
                        }));
                    }
                    catch (Exception error) { failure = error; }
                    complete = true; progress.Indeterminate = false; form.DialogResult = failure == null && !cancellation.IsCancellationRequested ? DialogResult.OK : DialogResult.Cancel;
                    form.Close();
                };
                var status = form.ShowDialog(owner);
                if (failure != null && !(failure is OperationCanceledException)) throw failure;
                return status == DialogResult.OK ? result : default(T);
            }
        }
    }
}
