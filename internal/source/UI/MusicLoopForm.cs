using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class MusicLoopForm : StudioToolForm
    {
        readonly EditHistory<Tuple<decimal, decimal>> loopHistory = new EditHistory<Tuple<decimal, decimal>>();
        bool restoringLoop;
        WaveLoop wave;
        string source;
        SoundPlayer player;
        MemoryStream sound;
        bool converting;
        CancellationTokenSource loading;
        int draggingBoundary;
        readonly NumericUpDown start = new NumericUpDown
        {
            Maximum = int.MaxValue,
            Width = 160
        };
        readonly NumericUpDown end = new NumericUpDown
        {
            Maximum = int.MaxValue,
            Width = 160
        };
        readonly ComboBox loopUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly NumericUpDown startSeconds = new NumericUpDown { DecimalPlaces = 9, Increment = 0.001m, Dock = DockStyle.Fill };
        readonly NumericUpDown endSeconds = new NumericUpDown { DecimalPlaces = 9, Increment = 0.001m, Dock = DockStyle.Fill };
        readonly Label startLabel = new Label { AutoSize = true, Dock = DockStyle.Fill };
        readonly Label endLabel = new Label { AutoSize = true, Dock = DockStyle.Fill };
        bool syncingSeconds;
        readonly Label info = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0)
        };
        readonly Button export, play, cancelLoad, findCrossings;
        readonly Label diagnostics = new Label { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(232, 0), Margin = new Padding(3, 12, 3, 0) };
        readonly PictureBox waveform = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Width = 790,
            Height = 155
        };
        float[] peaks;
        public MusicLoopForm() : base(L.T("MKWii Musik & Loops Tool", "MKWii Music & Loops Tool"), L.T("Audio wählen • Loop festlegen und anhören • WAV mit Loop-Markierungen für BRSTM exportieren", "Choose audio • Set and preview a loop • Export a WAV with loop markers for BRSTM conversion"), "*.wav · *.mp3 / *.flac / *.ogg (FFmpeg) → *.wav → *.brstm")
        {
            var open = Action(L.T("Datei öffnen…", "Open file…"), L.T("Mono- oder Stereo-WAV öffnen. Loop-Positionen zählen Audio-Samples, keine Bytes.", "Read a mono or stereo WAV. Loop positions count audio samples, not bytes."), async delegate
            {
                string p = OpenPath("PCM WAV|*.wav");
                if (p != null)
                    await LoadWave(p);
            });
            StudioActions.Icon(open, StudioIcon.Open);
            open.Name = "PackSourceAction";
            Action(L.T("Audio umwandeln…", "Convert other audio…"), L.T("MP3, FLAC, OGG oder andere Audiodateien mit dem eingerichteten FFmpeg vorübergehend in eine 16-Bit-Stereo-WAV umwandeln.", "Use the connected FFmpeg to make a temporary 16-bit stereo PCM WAV from MP3, FLAC, OGG or another audio file."), ConvertAudio).Name = "PackSourceAction";
            cancelLoad = Action(L.T("Laden abbrechen", "Cancel loading"), L.T("WAV-Laden und Pegelmessung abbrechen. Die bisherige Quelle bleibt erhalten.", "Cancel WAV loading and level analysis. Keep the previous source."), delegate { if (loading != null) loading.Cancel(); });
            cancelLoad.Name = "PackClearAction";
            cancelLoad.Enabled = false;
            play = Action(L.T("Loop anhören", "Preview loop"), L.T("Den gewählten Abschnitt wiederholt abspielen, um den Loop-Übergang zu hören.", "Play only the selected sample range repeatedly, so you can hear the loop seam."), delegate
            {
                Stop();
                sound = new MemoryStream(wave.Build((int)start.Value, (int)end.Value, true));
                player = new SoundPlayer(sound);
                player.PlayLooping();
            });
            StudioActions.Icon(play, StudioIcon.Play);
            StudioActions.Icon(Action(L.T("Stopp", "Stop"), L.T("Wiedergabe stoppen.", "Stop audio playback."), Stop), StudioIcon.Stop);
            findCrossings = Action(L.T("Nahe Nulldurchgänge suchen", "Find nearby zero crossings"),
                L.T("Loopmarken höchstens ±5 ms verschieben, wenn Randamplitude und Loop-Sprung in keinem Kanal steigen. Stereo behält gemeinsame Grenzen. Nur Markierungen ändern; danach anhören.",
                    "Move loop markers by at most ±5 ms if boundary amplitudes and loop jump do not increase in any channel. Stereo keeps shared boundaries. Only markers change; listen afterwards."), FindCrossings);
            export = ExportAction(L.T("WAV mit Loop speichern…", "Save looped WAV…"), L.T("Die gesamte Audiodatei mit Loop-Markierungen speichern. Das angezeigte Loop-Ende gehört nicht mehr zum Loop.", "Write the full audio with a standard smpl loop chunk. The loop end in the UI is exclusive."), Save);
            var converter = Action(L.T("BRSTM-Konverter öffnen", "Open BRSTM converter"), L.T("Looping Audio Converter öffnen. Exportierte WAV hinzufügen, BRSTM wählen und Loop-Markierungen beibehalten.", "Launch Looping Audio Converter. Add your exported WAV, select BRSTM and retain its loop markers."), delegate
            {
                Launch("LoopingAudioConverter", null);
            });
            var inspector = Action(L.T("BRSTM prüfen…", "Inspect BRSTM…"), L.T("Eine BRSTM im Formatprüfer mit BrawlCrate-Anbindung öffnen.", "Open a selected BRSTM with the format inspector and BrawlCrate connection."), delegate
            {
                string p = OpenPath(L.T("BRSTM-Audio|*.brstm", "BRSTM audio|*.brstm"));
                if (p != null)
                    StudioEditor.Open(this, new FormatInspectorForm(p), delegate { });
            });
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(3) };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 264));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var properties = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            properties.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) properties.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var propertyHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DarkTheme.Panel };
            propertyHost.Controls.Add(properties);
            workspace.Controls.Add(propertyHost, 1, 0);
            waveform.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (wave == null || wave.Samples < 1 || e.Button != MouseButtons.Left) return;
                PointF point = ((ZoomPanPictureBox)waveform).ImagePoint(e.Location);
                double position = point.X / 790 * wave.Samples;
                draggingBoundary = (ModifierKeys & Keys.Shift) != 0 || Math.Abs(position - (double)end.Value) < Math.Abs(position - (double)start.Value) ? 2 : 1;
                waveform.Capture = true;
                DragBoundary(e.Location);
            };
            waveform.MouseMove += delegate(object sender, MouseEventArgs e) { if (draggingBoundary != 0) DragBoundary(e.Location); };
            waveform.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { draggingBoundary = 0; waveform.Capture = false; }
            };
            waveform.MouseCaptureChanged += delegate { if (!waveform.Capture) draggingBoundary = 0; };
            waveform.Dock = DockStyle.Fill;
            waveform.MinimumSize = new Size(0, 60);
            waveform.BackColor = DarkTheme.Panel;
            waveform.SizeMode = PictureBoxSizeMode.StretchImage;
            workspace.Controls.Add(waveform, 0, 0);
            var range = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 8, Margin = new Padding(0, 8, 0, 6) };
            range.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 8; i++) range.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            range.Controls.Add(new Label { Text = L.T("Loop-Einheit", "Loop unit"), AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
            loopUnit.Items.AddRange(new object[] { "Samples", L.T("Sekunden", "Seconds") });
            loopUnit.SelectedIndex = 0;
            loopUnit.AccessibleName = L.T("Loop-Einheit", "Loop unit");
            range.Controls.Add(loopUnit, 0, 1);
            range.Controls.Add(startLabel, 0, 2);
            range.Controls.Add(endLabel, 0, 5);
            start.Dock = end.Dock = DockStyle.Fill;
            range.Controls.Add(start, 0, 3);
            range.Controls.Add(startSeconds, 0, 4);
            range.Controls.Add(end, 0, 6);
            range.Controls.Add(endSeconds, 0, 7);
            loopUnit.SelectedIndexChanged += delegate { UpdateLoopUnit(); };
            startSeconds.ValueChanged += delegate { ApplySeconds(startSeconds, start); };
            endSeconds.ValueChanged += delegate { ApplySeconds(endSeconds, end); };
            UpdateLoopUnit();
            properties.Controls.Add(range, 0, 0);
            info.Dock = DockStyle.Fill;
            info.MaximumSize = new Size(232, 0);
            properties.Controls.Add(info, 0, 1);
            var guide = new Label { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 8, 3, 0),
                Text = L.T("Loop-Punkte im Bild ziehen • Mausrad: Zoom • Vorschau anhören, dann WAV speichern.\nFür das Spiel die WAV anschließend mit dem BRSTM-Konverter umwandeln.",
                    "Drag loop points in the waveform • Wheel: zoom • Listen to the preview, then save WAV.\nFor the game, convert the WAV with the BRSTM converter afterwards.") };
            var brstmActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
            brstmActions.Controls.AddRange(new Control[] { converter, inspector });
            var brstm = new CheckBox { Text = L.T("BRSTM-Konvertierung", "BRSTM conversion"), AutoSize = true, Margin = new Padding(3, 16, 3, 3) };
            brstm.CheckedChanged += delegate { brstmActions.Visible = brstm.Checked; };
            properties.RowCount = 6;
            properties.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            properties.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            properties.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            properties.Controls.Add(diagnostics, 0, 2);
            properties.Controls.Add(guide, 0, 3);
            properties.Controls.Add(brstm, 0, 4);
            properties.Controls.Add(brstmActions, 0, 5);
            properties.SizeChanged += delegate
            {
                int width = Math.Max(1, properties.ClientSize.Width - properties.Padding.Horizontal - 6);
                info.MaximumSize = guide.MaximumSize = diagnostics.MaximumSize = new Size(width, 0);
            };
            StudioUx.SetHelp(diagnostics, L.T("Sample-Pegel, keine True-Peak- oder Lautheitsmessung. Beide PCM-Grenzwerte zählen als Vollaussteuerung. Der Loop-Sprung ist die absolute Differenz von letztem zu erstem Sample, bezogen auf Vollaussteuerung; Stereo wird getrennt gemessen.", "Sample levels, not true peak or loudness. Both PCM limits count as full scale. Loop jump is the absolute last-to-first sample difference relative to full scale; stereo channels are measured separately."));
            Body.Controls.Add(workspace);
            start.ValueChanged += delegate
            {
                if (!restoringLoop) SyncSeconds();
                UpdateInfo();
                RecordLoop("start");
            };
            end.ValueChanged += delegate
            {
                if (!restoringLoop) SyncSeconds();
                UpdateInfo();
                RecordLoop("end");
            };
            StudioUx.SetHelp(start, L.T("Zu diesem Sample springt die Wiedergabe zurück. Die Zeit in Sekunden steht darunter.", "The sample that playback jumps back to. Seconds are shown below."));
            StudioUx.SetHelp(end, L.T("Vor diesem Sample springt die Wiedergabe zurück. Für einen Loop bis zum Ende die gesamte Sample-Anzahl wählen.", "Playback jumps back just before this sample. Use the total sample count to loop to the end."));
            StudioUx.SetHelp(startSeconds, L.T("Sekunden werden auf das nächste Sample gerundet; genau halbe Samples aufwärts. Pfeilschritt: 0,001 s. Der Einheitenwechsel erhält die genaue Sample-Position.", "Seconds round to the nearest sample; exact half samples round upwards. Arrow step: 0.001 s. Switching units retains the exact sample position."));
            StudioUx.SetHelp(endSeconds, L.T("Das Ende ist exklusiv: Dieses Sample gehört nicht mehr zum Loop. Sekunden werden wie beim Anfang gerundet.", "The end is exclusive: this sample is outside the loop. Seconds use the same rounding as the start."));
            new StudioUndoRedo(this, Actions, () => wave != null && loading == null && loopHistory.CanUndo,
                () => wave != null && loading == null && loopHistory.CanRedo, () => RestoreLoop(loopHistory.Undo()), () => RestoreLoop(loopHistory.Redo()));
            Finish();
            play.Enabled = export.Enabled = findCrossings.Enabled = false;
            start.Enabled = end.Enabled = startSeconds.Enabled = endSeconds.Enabled = loopUnit.Enabled = false;
            Status.Text = L.T("WAV-Loops direkt bearbeiten. BRSTM umwandeln mit Looping Audio Converter; BRSTM prüfen mit BrawlCrate.", "PCM WAV loop editing is built in. BRSTM conversion uses Looping Audio Converter; BRSTM inspection uses BrawlCrate.");
            FormClosed += delegate
            {
                if (loading != null) loading.Cancel();
                Stop();
            };
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                if (converting)
                {
                    e.Cancel = true;
                    Status.Text = L.T("Die Audioumwandlung läuft noch. Sie wird nach spätestens zwei Minuten abgebrochen.", "Audio conversion is still running. The backend has a two-minute timeout.");
                }
            };
        }

        async Task<bool> LoadWave(string path)
        {
            if (loading != null) loading.Cancel();
            var pending = new CancellationTokenSource();
            loading = pending;
            Stop();
            start.Enabled = end.Enabled = startSeconds.Enabled = endSeconds.Enabled = loopUnit.Enabled = waveform.Enabled = play.Enabled = export.Enabled = findCrossings.Enabled = false;
            cancelLoad.Enabled = true;
            Status.Text = L.T("WAV wird geladen und gemessen…", "Loading WAV and measuring sample levels…");
            try
            {
                var result = await ToolStatus.RunAsync(this, delegate
                {
                    byte[] bytes;
                    using (var input = File.OpenRead(path))
                    {
                        if (input.Length > int.MaxValue) throw new IOException(L.T("Die WAV ist zu groß. Bitte eine kleinere Datei wählen.", "The WAV is too large. Choose a smaller file."));
                        bytes = new byte[(int)input.Length];
                        int offset = 0;
                        while (offset < bytes.Length)
                        {
                            pending.Token.ThrowIfCancellationRequested();
                            int count = input.Read(bytes, offset, Math.Min(65536, bytes.Length - offset));
                            if (count == 0) throw new EndOfStreamException("Incomplete WAV audio.");
                            offset += count;
                        }
                    }
                    pending.Token.ThrowIfCancellationRequested();
                    var next = new WaveLoop(bytes);
                    return Tuple.Create(next, next.Peaks(790, pending.Token));
                });
                if (IsDisposed || pending.IsCancellationRequested || loading != pending) return false;
                source = path;
                wave = result.Item1;
                peaks = result.Item2;
                restoringLoop = true;
                try
                {
                    start.Maximum = end.Maximum = int.MaxValue;
                    start.Value = wave.LoopStart;
                    end.Value = wave.LoopEnd;
                    start.Maximum = end.Maximum = wave.Samples;
                }
                finally { restoringLoop = false; }
                SyncSeconds();
                loopHistory.Reset(Tuple.Create(start.Value, end.Value));
                PackSelection.SourceLoaded(this);
                Status.Text = L.T("WAV geladen. Pegel und Loop-Sprung sind nur Hinweise; den Übergang mit der Vorschau prüfen.", "WAV loaded. Levels and loop jump are indicators; check the transition with the preview.");
                return true;
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && loading == pending) Status.Text = L.T("WAV-Laden abgebrochen. Die bisherige Quelle bleibt erhalten.", "WAV loading cancelled. The previous source is retained.");
                return false;
            }
            catch (Exception ex)
            {
                if (!IsDisposed && loading == pending)
                {
                    Status.Text = L.T("WAV konnte nicht geladen werden. Bitte eine vollständige PCM-WAV wählen.", "Could not load WAV. Choose a complete PCM WAV.");
                    StudioMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
            finally
            {
                if (loading == pending)
                {
                    loading = null;
                    if (!IsDisposed)
                    {
                        start.Enabled = end.Enabled = startSeconds.Enabled = endSeconds.Enabled = loopUnit.Enabled = waveform.Enabled = wave != null;
                        cancelLoad.Enabled = false;
                        UpdateInfo();
                    }
                }
                pending.Dispose();
            }
        }

        void RecordLoop(string field)
        {
            if (!restoringLoop && wave != null)
                loopHistory.Record(Tuple.Create(start.Value, end.Value), draggingBoundary != 0 ? "drag" : field);
        }

        void RestoreLoop(Tuple<decimal, decimal> value)
        {
            if (value == null) return;
            restoringLoop = true;
            try { start.Value = value.Item1; end.Value = value.Item2; SyncSeconds(); }
            finally { restoringLoop = false; }
            UpdateInfo();
        }

        void FindCrossings()
        {
            if (wave == null || loading != null || start.Value >= end.Value || end.Value > wave.Samples) return;
            Stop();
            int beforeStart = (int)start.Value, beforeEnd = (int)end.Value;
            var result = wave.FindNearbyZeroCrossings(beforeStart, beforeEnd);
            if (result.Item1 == beforeStart && result.Item2 == beforeEnd)
            {
                Status.Text = L.T("Keine geeignete Verbesserung innerhalb ±5 ms gefunden. Markierungen unverändert; Loop anhören.",
                    "No suitable improvement found within ±5 ms. Markers unchanged; preview the loop.");
                return;
            }
            RestoreLoop(Tuple.Create((decimal)result.Item1, (decimal)result.Item2));
            // Ohne Gruppe bleibt die gemeinsame Aenderung ein eigener Undo-Schritt.
            loopHistory.Record(Tuple.Create(start.Value, end.Value));
            Status.Text = L.T("Loopmarken verschoben (Samples): Anfang ", "Loop markers moved (samples): start ")
                + (result.Item1 - beforeStart).ToString("+0;-0;0") + L.T(", Ende ", ", end ")
                + (result.Item2 - beforeEnd).ToString("+0;-0;0")
                + L.T(". Nur Markierungen geändert; den Übergang jetzt anhören.", ". Only markers changed; preview the transition now.");
        }

        void UpdateLoopUnit()
        {
            bool seconds = loopUnit.SelectedIndex == 1;
            start.Visible = end.Visible = !seconds;
            startSeconds.Visible = endSeconds.Visible = seconds;
            startLabel.Text = seconds ? L.T("Loop-Anfang (Sekunden)", "Loop start (seconds)") : L.T("Loop-Anfang — erstes Sample", "Loop start — sample index (inclusive)");
            endLabel.Text = seconds ? L.T("Loop-Ende (Sekunden, exklusiv)", "Loop end (seconds, exclusive)") : L.T("Loop-Ende — erstes Sample danach", "Loop end — sample index (exclusive)");
            start.AccessibleName = startSeconds.AccessibleName = startLabel.Text;
            end.AccessibleName = endSeconds.AccessibleName = endLabel.Text;
        }

        void SyncSeconds()
        {
            if (wave == null || syncingSeconds) return;
            syncingSeconds = true;
            try
            {
                // Aufrunden erlaubt die Gesamtlaenge auch bei periodischen Dezimalzeiten.
                decimal maximum = decimal.Ceiling(wave.SampleToSeconds(wave.Samples) * 1000000000m) / 1000000000m;
                startSeconds.Maximum = endSeconds.Maximum = Math.Max(maximum, Math.Max(startSeconds.Value, endSeconds.Value));
                startSeconds.Value = decimal.Round(wave.SampleToSeconds((int)start.Value), 9, MidpointRounding.AwayFromZero);
                endSeconds.Value = decimal.Round(wave.SampleToSeconds((int)end.Value), 9, MidpointRounding.AwayFromZero);
                startSeconds.Maximum = endSeconds.Maximum = maximum;
            }
            finally { syncingSeconds = false; }
        }

        void ApplySeconds(NumericUpDown seconds, NumericUpDown samples)
        {
            if (syncingSeconds || restoringLoop || wave == null || loading != null) return;
            int position = wave.SecondsToSample(seconds.Value);
            if (samples.Value == position) { SyncSeconds(); return; }
            // Nur die kanonische Sample-Aenderung erzeugt einen History-Eintrag.
            samples.Value = position;
        }

        void UpdateInfo()
        {
            Stop();
            DrawWave();
            play.Enabled = export.Enabled = findCrossings.Enabled = wave != null && loading == null && start.Value < end.Value && end.Value <= wave.Samples;
            if (wave != null)
                info.Text = Path.GetFileName(source) + "\n" + wave.SampleRate + " Hz • " + wave.Samples + L.T(" Samples • ", " samples • ") + (wave.Samples / (double)wave.SampleRate).ToString("F3") + L.T(" Sekunden\nLoop: ", " seconds\nLoop: ") + (start.Value / wave.SampleRate).ToString("F3") + " s → " + (end.Value / wave.SampleRate).ToString("F3") + " s";
            if (wave != null) info.Text += "\n" + L.T("Samples: ", "Samples: ") + start.Value + " → " + end.Value + "\n" + L.T("Sekunden: nächstes Sample; halbe aufwärts. Ende exklusiv.", "Seconds: nearest sample; halves round up. End exclusive.");
            UpdateDiagnostics();
        }

        void UpdateDiagnostics()
        {
            if (wave == null || wave.Analysis == null) { diagnostics.Text = ""; return; }
            bool valid = start.Value < end.Value && end.Value <= wave.Samples;
            double[] delta = valid ? wave.LoopBoundaryDelta((int)start.Value, (int)end.Value) : null;
            var text = new StringBuilder(L.T("Sample-Pegel · gesamte WAV", "Sample levels · whole WAV"));
            for (int c = 0; c < wave.Channels; c++)
            {
                string channel = wave.Channels == 1 ? "Mono" : c == 0 ? L.T("Links", "Left") : L.T("Rechts", "Right");
                double level = wave.Analysis.PeakDbFs[c];
                text.Append("\n").Append(channel).Append(": ").Append(double.IsNegativeInfinity(level) ? "−∞" : level.ToString("F2")).Append(" dBFS");
                text.Append("\n").Append(L.T("Vollaussteuerung: ", "Full-scale samples: ")).Append(wave.Analysis.FullScaleSamples[c].ToString("N0"));
            }
            text.Append("\n\n").Append(L.T("Loop-Sprung (letztes → erstes)", "Loop jump (last → first)"));
            if (!valid) text.Append("\n").Append(L.T("Ungültiger Loop-Bereich.", "Invalid loop range."));
            else for (int c = 0; c < delta.Length; c++)
                text.Append("\n").Append(wave.Channels == 1 ? "Mono" : c == 0 ? L.T("Links", "Left") : L.T("Rechts", "Right")).Append(": ").Append((delta[c] * 100).ToString("F2")).Append(" % FS");
            text.Append("\n\n").Append(L.T("Vollaussteuerung kann auf Clipping hinweisen. Diese Messwerte beweisen weder Verzerrung noch einen hörbar sauberen Loop.", "Full scale can indicate clipping. These values prove neither distortion nor an audibly clean loop."));
            diagnostics.Text = text.ToString();
        }

        void DragBoundary(Point point)
        {
            if (wave == null || waveform.Image == null || wave.Samples < 1) return;
            PointF imagePoint = ((ZoomPanPictureBox)waveform).ImagePoint(point);
            decimal position = (decimal)Math.Max(0, Math.Min(wave.Samples, Math.Round(imagePoint.X / waveform.Image.Width * wave.Samples)));
            if (draggingBoundary == 1) start.Value = Math.Min(position, Math.Max(0, end.Value - 1));
            else if (draggingBoundary == 2) end.Value = Math.Max(position, Math.Min(wave.Samples, start.Value + 1));
        }

        void DrawWave()
        {
            if (wave == null || peaks == null)
                return;
            var b = waveform.Image as Bitmap ?? new Bitmap(790, 155);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(DarkTheme.Panel2);
                float left = (float)start.Value / wave.Samples * 790, right = (float)end.Value / wave.Samples * 790;
                using (var brush = new SolidBrush(Color.FromArgb(65, DarkTheme.Accent)))
                    if (right > left)
                        g.FillRectangle(brush, left, 0, right - left, 155);
                using (var pen = new Pen(DarkTheme.Fore))
                    for (int i = 0; i < peaks.Length; i++)
                        g.DrawLine(pen, i, 77 - peaks[i] * 70, i, 77 + peaks[i] * 70);
                using (var pen = new Pen(DarkTheme.Cyan)) g.DrawLine(pen, left, 0, left, 155);
                using (var pen = new Pen(DarkTheme.Accent)) g.DrawLine(pen, right, 0, right, 155);
            }

            var old = waveform.Image;
            waveform.Image = b;
            if (old != null && !ReferenceEquals(old, b)) old.Dispose();
            waveform.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (loading != null) loading.Cancel();
                Stop();
                if (waveform.Image != null)
                {
                    waveform.Image.Dispose();
                    waveform.Image = null;
                }
            }

            base.Dispose(disposing);
        }

        void Stop()
        {
            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }

            if (sound != null)
            {
                sound.Dispose();
                sound = null;
            }
        }

        void Save()
        {
            string p = SavePath(Path.GetFileNameWithoutExtension(source) + "_loop.wav", L.T("WAV mit Loop|*.wav", "Looped WAV|*.wav"));
            if (p == null)
                return;
            if (string.Equals(Path.GetFullPath(p), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Bitte eine andere Datei wählen, damit die Quelle erhalten bleibt.", "Choose a different file to preserve your source."));
            BackupManager.WriteAllBytesSafely(p, wave.Build((int)start.Value, (int)end.Value, false));
            ToolStatus.Set(this, true); Status.Text = L.T("Loop-Markierungen gespeichert: ", "Saved loop markers: ") + p + L.T("\nDiese WAV in Looping Audio Converter hinzufügen und BRSTM als Ausgabe wählen.", "\nAdd this WAV in Looping Audio Converter and choose BRSTM output.");
            StudioMessageBox.ShowPath(this, p, L.T("WAV mit Loop-Markierungen gespeichert.", "WAV with loop markers saved."), Text);
        }

        void Launch(string id, string path)
        {
            string error;
            if (!ToolchainManager.Launch(ToolchainManager.FindById(id), path, false, out error))
            {
                StudioEditor.Open(this, new ToolchainForm(), delegate { });
                Status.Text = error;
            }
        }

        async void ConvertAudio()
        {
            string p = OpenPath("Audio|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.m4a");
            if (p == null)
                return;
            string temp = Path.Combine(Path.GetTempPath(), "murums_audio_" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                Enabled = false;
                converting = true;
                string output = null, error = null;
                bool ok = await ToolStatus.RunAsync(this, delegate
                {
                    return ToolchainManager.RunCapture(ToolchainManager.FindById("ffmpeg"), "-nostdin -i " + ToolchainManager.QuoteArgument(p) + " -vn -ac 2 -ar 32000 -c:a pcm_s16le " + ToolchainManager.QuoteArgument(temp), out output, out error);
                });
                if (!ok)
                    throw new IOException(error);
                Enabled = true;
                if (!await LoadWave(temp)) return;
                source = p;
                UpdateInfo();
                Status.Text = L.T("In 32-kHz-Stereo-PCM umgewandelt. Das Ergebnis als WAV mit Loop speichern.", "Converted to 32 kHz stereo PCM in memory. Save a looped WAV to keep your result.");
            }
            catch (Exception ex)
            {
                murumsWiiModStudio.StudioMessageBox.Show(this, ex.Message, Text);
            }
            finally
            {
                Enabled = true;
                converting = false;
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}

