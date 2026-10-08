using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class TextureBatchForm : StudioToolForm
    {
        readonly List<TextureBatchTarget> targets;
        readonly TextBox folder = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        readonly CheckBox resize = new CheckBox { AutoSize = true, Text = L.T("Abweichende Größen auf Ziel strecken", "Stretch different sizes to target") };
        readonly ListView rows = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, ShowItemToolTips = true };
        readonly PictureBox before = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly PictureBox after = new ZoomPanPictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        readonly Label details = new Label { Dock = DockStyle.Fill, AutoSize = true, AutoEllipsis = true, Padding = new Padding(4), MaximumSize = new Size(1200, 96) };
        readonly StudioProgressBar progress = new StudioProgressBar { Dock = DockStyle.Fill, Height = 12 };
        readonly Button export, choose, prepare, apply, cancel;
        CancellationTokenSource cancellation;
        System.Windows.Forms.Timer closeTimer;
        bool closeAfterCancel;
        bool closingFromQueue, closeQueued;
        internal TextureBatchPlan Result;

        internal TextureBatchForm(List<TextureBatchTarget> captured)
            : base(L.T("Texturen gesammelt bearbeiten", "Batch texture images"),
                L.T("PNGs exportieren • Bilder bearbeiten • Vorschau prüfen und gemeinsam übernehmen",
                    "Export PNGs • Edit images • Review and apply together"))
        {
            targets = captured;
            folder.AccessibleName = L.T("Bildordner", "Image folder");
            resize.AccessibleName = resize.Text;
            rows.AccessibleName = L.T("Textur-Zuordnungsplan", "Texture mapping plan");
            before.AccessibleName = L.T("Textur im aktuellen Arbeitsstand", "Texture in current edit state");
            after.AccessibleName = L.T("Tatsächlich kodiertes TPL-Ergebnis", "Actual encoded TPL result");
            export = Action(L.T("PNGs exportieren…", "Export PNGs…"), L.T("Alle Bilder der geladenen Archive als neue PNG-Sammlung exportieren.", "Export every image in the loaded archives to a new PNG collection."), delegate { StartExport(); });
            StudioActions.Icon(export, StudioIcon.Export);
            choose = Action(L.T("Bildordner wählen…", "Choose image folder…"), L.T("Exportierte PNG-Sammlung oder einen Ordner mit denselben eindeutigen Bildpfaden wählen.", "Choose an exported PNG collection or a folder with the same unique image paths."), Choose);
            StudioActions.Icon(choose, StudioIcon.Folder);
            prepare = Action(L.T("Importvorschau erstellen", "Prepare import preview"), L.T("Bilder zuordnen und vollständig neu kodieren, ohne den Arbeitsstand zu verändern.", "Map images and encode the complete batch without changing current edits."), delegate { StartPrepare(); });
            StudioActions.Icon(prepare, StudioIcon.Import);
            Actions.Controls.Add(resize);
            StudioUx.SetHelp(resize, L.T("Standard: abweichende Bildgrößen als Fehler zeigen. Aktiviert: Bilder auf die vorhandene Zielgröße strecken; Format und Mipmap-Anzahl erhalten.",
                "Default: report mismatched dimensions as errors. Enabled: stretch images to existing target dimensions; preserve format and mipmap count."));
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.Controls.Add(new Label { Text = L.T("Bildordner", "Image folder"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            folderRow.Controls.Add(folder, 1, 0);
            body.Controls.Add(folderRow, 0, 0); body.Controls.Add(progress, 0, 1);
            var split = new SplitContainer { Dock = DockStyle.Fill, Width = 1000, SplitterDistance = 485, Panel1MinSize = 260, Panel2MinSize = 240 };
            rows.Columns.Add(L.T("Status", "Status"), 142);
            rows.Columns.Add(L.T("Archiv / Textur / Bild", "Archive / texture / image"), 255);
            rows.Columns.Add(L.T("Ziel", "Target"), 120);
            rows.SizeChanged += delegate
            {
                rows.Columns[0].Width = 130; rows.Columns[2].Width = 125;
                rows.Columns[1].Width = Math.Max(60, rows.ClientSize.Width - 130 - 125 - SystemInformation.VerticalScrollBarWidth - 4);
            };
            split.Panel1.Controls.Add(rows);
            var views = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); views.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            views.Controls.Add(new Label { Text = L.T("Arbeitsstand", "Current image"), Dock = DockStyle.Fill }, 0, 0);
            views.Controls.Add(new Label { Text = L.T("Kodiertes Ergebnis", "Encoded result"), Dock = DockStyle.Fill }, 1, 0);
            views.Controls.Add(before, 0, 1); views.Controls.Add(after, 1, 1);
            split.Panel2.Controls.Add(views); body.Controls.Add(split, 0, 2); body.Controls.Add(details, 0, 3);
            Body.Controls.Add(body);
            apply = ExportAction(L.T("Änderungen gemeinsam übernehmen", "Apply changes together"), L.T("Den vollständigen Plan als einen rückgängig machbaren Schritt übernehmen.", "Apply the complete plan as one undoable step."), delegate
            {
                if (Result != null && Result.CanApply) { DialogResult = DialogResult.OK; Close(); }
            });
            cancel = ExportAction(L.T("Schließen", "Close"), L.T("Ohne Übernahme schließen; laufende Verarbeitung abbrechen.", "Close without applying; cancel active processing."), Cancel, false);
            apply.AccessibleName = apply.Text;
            cancel.AccessibleName = cancel.Text;
            CancelButton = cancel;
            resize.CheckedChanged += delegate { InvalidatePlan(); };
            rows.SelectedIndexChanged += delegate { Guard(ShowRow); };
            Finish();
            SetBusy(false);
            details.Text = L.T("1. PNGs exportieren und bearbeiten. 2. Bildordner wählen. 3. Ergebnis prüfen. Originale bleiben erhalten.",
                "1. Export and edit PNGs. 2. Choose image folder. 3. Review results. Source files are preserved.");
            Status.Text = targets.Count + L.T(" TPL-Dateien aus allen geladenen Archiven; der Kategorienfilter gilt hier nicht.", " TPL files from all loaded archives; the category filter does not apply here.");
            DarkTheme.StylePrimary(export);
        }
        void Choose()
        {
            string selected = Folder(folder.Text, false);
            if (selected == null) return;
            folder.Text = selected; InvalidatePlan();
        }
        void InvalidatePlan()
        {
            Result = null; rows.Items.Clear(); SetImage(before, null); SetImage(after, null);
            apply.Enabled = false; prepare.Enabled = cancellation == null && Directory.Exists(folder.Text);
            details.Text = L.T("Importvorschau erstellen, um Zuordnungen und gespeicherte Farben zu prüfen.", "Prepare the import preview to check mappings and encoded colours.");
        }
        void SetBusy(bool busy)
        {
            export.Enabled = choose.Enabled = resize.Enabled = !busy;
            prepare.Enabled = !busy && Directory.Exists(folder.Text);
            apply.Enabled = !busy && Result != null && Result.CanApply;
            cancel.Text = busy ? L.T("Abbrechen", "Cancel") : L.T("Schließen", "Close");
            cancel.AccessibleName = cancel.Text;
        }
        void Cancel()
        {
            if (cancellation != null) { cancellation.Cancel(); return; }
            DialogResult = DialogResult.Cancel; Close();
        }
        async void StartExport()
        {
            string destination = Folder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (destination == null) return;
            await Run(async delegate(CancellationToken token, Action<int, int> report)
            {
                string exported = await Task.Run(() => TextureBatch.Export(targets, destination, token, report));
                folder.Text = exported; InvalidatePlan();
                Status.Text = L.T("PNGs und Zuordnungsplan gespeichert: ", "PNGs and mapping plan saved: ") + exported;
                details.Text = L.T("PNGs in diesem Ordner bearbeiten, danach Importvorschau erstellen. Den Zuordnungsplan beibehalten.",
                    "Edit PNGs in this folder, then prepare the import preview. Keep the mapping plan.");
            });
        }
        async void StartPrepare()
        {
            string selected = folder.Text; bool fit = resize.Checked;
            InvalidatePlan();
            await Run(async delegate(CancellationToken token, Action<int, int> report)
            {
                var plan = await Task.Run(() => TextureBatch.Prepare(targets, selected, fit, token, report));
                Result = plan;
                rows.BeginUpdate();
                try
                {
                    foreach (var row in plan.Rows)
                    {
                        var item = new ListViewItem(row.State) { Tag = row };
                        item.SubItems.Add(row.Target.ArchiveName + " / " + row.Target.Resource + L.T(" / Bild ", " / Image ") + (row.Index + 1));
                        item.SubItems.Add(row.Info == null ? "—" : row.Info.Width + "×" + row.Info.Height + " · " + row.Info.FormatName);
                        item.ToolTipText = row.Target.ArchiveName + " / " + row.Target.Resource + " / " + (row.Index + 1) + "\n" + TargetDescription(row) + "\n" + row.Detail; rows.Items.Add(item);
                    }
                }
                finally { rows.EndUpdate(); }
                Status.Text = plan.Rows.Count(r => r.Changed) + L.T(" Änderungen • ", " changes • ")
                    + plan.Rows.Count(r => r.Failed) + L.T(" Fehler • ", " errors • ")
                    + plan.Rows.Count(r => !r.Changed && !r.Failed) + L.T(" unverändert/übersprungen • ", " unchanged/skipped • ")
                    + plan.ExtraImages + L.T(" PNGs ohne Zuordnung", " unmapped PNGs");
                details.Text = plan.Rows.Any(r => r.Failed)
                    ? L.T("Fehler beheben und Vorschau neu erstellen. Bis dahin wird keine Änderung übernommen.", "Fix errors and prepare the preview again. No changes can be applied until then.")
                    : L.T("Wähle eine Zeile für die echte TPL-Ergebnisvorschau. Format, Palettentyp, Mipmaps und andere Bilder bleiben erhalten.",
                        "Select a row for the actual encoded TPL preview. Format, palette type, mipmaps and other images are preserved.");
                if (rows.Items.Count > 0) rows.Items[0].Selected = true;
            });
        }
        async Task Run(Func<CancellationToken, Action<int, int>, Task> work)
        {
            cancellation = new CancellationTokenSource(); SetBusy(true); progress.Value = 0;
            var updates = new Progress<int>(value => { if (!IsDisposed) progress.Value = value; });
            try { await work(cancellation.Token, (done, total) => ((IProgress<int>)updates).Report(total == 0 ? 0 : done * 100 / total)); }
            catch (OperationCanceledException) { Result = null; Status.Text = L.T("Abgebrochen. Der Arbeitsstand blieb unverändert.", "Cancelled. Current edits were preserved."); }
            catch (Exception ex)
            {
                Result = null; Status.Text = L.T("Verarbeitung fehlgeschlagen. Der Arbeitsstand blieb unverändert.", "Processing failed. Current edits were preserved.");
                StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cancellation.Dispose(); cancellation = null; SetBusy(false);
                if (closeAfterCancel) { DialogResult = DialogResult.Cancel; Close(); }
            }
        }
        void ShowRow()
        {
            SetImage(before, null); SetImage(after, null);
            if (rows.SelectedItems.Count != 1 || Result == null) return;
            var row = (TextureBatchRow)rows.SelectedItems[0].Tag;
            SetImage(before, TextureBatch.Decode(row.Target, row.Target.Original, row.Index));
            byte[] bytes;
            if (!row.Failed && Result.Prepared.TryGetValue(row.Target, out bytes))
                SetImage(after, TextureBatch.Decode(row.Target, bytes, row.Index));
            else if (!row.Failed) SetImage(after, TextureBatch.Decode(row.Target, row.Target.Original, row.Index));
            details.Text = row.Target.ArchiveName + " / " + row.Target.Resource + L.T(" / Bild ", " / Image ") + (row.Index + 1)
                + "\n" + TargetDescription(row) + "\n" + row.State + (String.IsNullOrEmpty(row.Detail) ? "" : " · " + row.Detail);
            StudioUx.SetHelp(details, details.Text);
        }
        static string TargetDescription(TextureBatchRow row)
        {
            if (row.Info == null) return L.T("Zielformat: nur Export", "Target format: export only");
            string text = row.Info.Width + " × " + row.Info.Height + " · " + row.Info.FormatName + " · " + row.Info.MaxLod
                + L.T(" zusätzliche Mipmap-Stufen", " additional mipmap levels");
            if (!TplTextureEditor.CanReplaceImage(row.Target.Original, row.Index)) text += L.T(" · nur Export", " · export only");
            if (row.Info.Format == 8 || row.Info.Format == 9)
            {
                byte[] data = row.Target.Original;
                int table = checked((int)(((uint)data[8] << 24) | ((uint)data[9] << 16) | ((uint)data[10] << 8) | data[11]) + row.Index * 8 + 4);
                int palette = checked((int)(((uint)data[table] << 24) | ((uint)data[table+1] << 16) | ((uint)data[table+2] << 8) | data[table+3]));
                int kind = data[palette+7];
                text += L.T(" · Palette ", " · Palette ") + (kind == 0 ? "IA8" : kind == 1 ? "RGB565" : "RGB5A3");
            }
            return text;
        }
        static void SetImage(PictureBox view, Image image)
        {
            Image old = view.Image; view.Image = image; if (old != null) old.Dispose();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (cancellation != null)
            {
                e.Cancel = true; closeAfterCancel = true; cancellation.Cancel();
                return;
            }
            if (!closingFromQueue && IsHandleCreated && e.CloseReason != CloseReason.WindowsShutDown
                && e.CloseReason != CloseReason.ApplicationExitCall && e.CloseReason != CloseReason.TaskManagerClosing)
            {
                e.Cancel = true;
                QueueClose();
                return;
            }
            base.OnFormClosing(e);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x10 && !closingFromQueue)
            {
                if (cancellation != null) { closeAfterCancel = true; cancellation.Cancel(); }
                else QueueClose();
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        void QueueClose()
        {
            if (closeQueued || IsDisposed || Disposing) return;
            closeQueued = true;
            DialogResult result = Modal && DialogResult == DialogResult.None ? DialogResult.Cancel : DialogResult;
            // BeginInvoke kann bei synchronem Invoke noch im Handleaufbau abgearbeitet werden.
            closeTimer = new System.Windows.Forms.Timer { Interval = 1 };
            closeTimer.Tick += delegate
            {
                closeTimer.Stop();
                closeTimer.Dispose();
                closeTimer = null;
                closeQueued = false;
                if (IsDisposed || Disposing) return;
                closingFromQueue = true;
                try { DialogResult = result; Close(); }
                finally { closingFromQueue = false; }
            };
            closeTimer.Start();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (closeTimer != null)
                {
                    closeTimer.Stop();
                    closeTimer.Dispose();
                    closeTimer = null;
                }
                SetImage(before, null); SetImage(after, null);
            }
            base.Dispose(disposing);
        }
    }
}
