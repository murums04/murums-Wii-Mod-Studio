using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed class MusicLoopForm : StudioToolForm
    {
        WaveLoop wave;
        string source;
        SoundPlayer player;
        MemoryStream sound;
        bool converting;
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
        readonly Label info = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0)
        };
        readonly Button export, play;
        readonly PictureBox waveform = new murumsWiiModStudio.ZoomPanPictureBox
        {
            Width = 790,
            Height = 155
        };
        float[] peaks;
        public MusicLoopForm() : base("MKWii Music & Loops Tool", "Choose audio • Set and preview a loop • Export a WAV with loop markers for BRSTM conversion", "*.wav · *.mp3 / *.flac / *.ogg (FFmpeg) → *.wav → *.brstm")
        {
            Action("Open file…", "Read a mono or stereo WAV. Loop positions count audio samples, not bytes.", delegate
            {
                string p = OpenPath("PCM WAV|*.wav");
                if (p != null)
                    LoadWave(p);
            });
            Action("Convert other audio…", "Use the connected FFmpeg to make a temporary 16-bit stereo PCM WAV from MP3, FLAC, OGG or another audio file.", ConvertAudio);
            play = Action("Preview loop", "Play only the selected sample range repeatedly, so you can hear the loop seam.", delegate
            {
                Stop();
                sound = new MemoryStream(wave.Build((int)start.Value, (int)end.Value, true));
                player = new SoundPlayer(sound);
                player.PlayLooping();
            });
            Action("Stop", "Stop audio playback.", Stop);
            export = ExportAction("Save looped WAV…", "Write the full audio with a standard smpl loop chunk. The loop end in the UI is exclusive.", Save);
            Action("Open BRSTM converter", "Launch Looping Audio Converter. Add your exported WAV, select BRSTM and retain its loop markers.", delegate
            {
                Launch("LoopingAudioConverter", null);
            });
            Action("Inspect BRSTM…", "Open a selected BRSTM with the format inspector and BrawlCrate connection.", delegate
            {
                string p = OpenPath("BRSTM audio|*.brstm");
                if (p != null)
                    using (var form = new FormatInspectorForm(p))
                        form.ShowDialog(this);
            });
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(3) };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            waveform.Dock = DockStyle.Fill;
            waveform.MinimumSize = new Size(0, 60);
            waveform.BackColor = DarkTheme.Panel;
            waveform.SizeMode = PictureBoxSizeMode.StretchImage;
            workspace.Controls.Add(waveform, 0, 0);
            var range = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 8, 0, 6) };
            range.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            range.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            range.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            range.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            range.Controls.Add(new Label { Text = "Loop start — sample index (inclusive)", AutoSize = true }, 0, 0);
            range.Controls.Add(new Label { Text = "Loop end — sample index (exclusive)", AutoSize = true }, 1, 0);
            start.Dock = end.Dock = DockStyle.Fill;
            range.Controls.Add(start, 0, 1);
            range.Controls.Add(end, 1, 1);
            workspace.Controls.Add(range, 0, 1);
            info.Dock = DockStyle.Fill;
            workspace.Controls.Add(info, 0, 2);
            var guide = new Label { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 8, 3, 0),
                Text = "Set the loop range, preview the seam, then save the looped WAV. In BRSTM converter, keep its loop markers.\nSave using your pack's music filename; add the result to a Theme Project. Final-lap music may need a separate file.\nLooping Audio Converter is connected through Tools > Toolchain status." };
            workspace.Controls.Add(guide, 0, 3);
            Body.Controls.Add(workspace);
            start.ValueChanged += delegate
            {
                UpdateInfo();
            };
            end.ValueChanged += delegate
            {
                UpdateInfo();
            };
            StudioUx.SetHelp(start, "The sample that playback jumps back to. Seconds are shown below.");
            StudioUx.SetHelp(end, "Playback jumps back just before this sample. Use the total sample count to loop to the end.");
            Finish();
            play.Enabled = export.Enabled = false;
            Status.Text = "PCM WAV loop editing is built in. BRSTM conversion uses Looping Audio Converter; BRSTM inspection uses BrawlCrate.";
            FormClosed += delegate
            {
                Stop();
            };
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                if (converting)
                {
                    e.Cancel = true;
                    Status.Text = "Audio conversion is still running. The backend has a two-minute timeout.";
                }
            };
        }

        void LoadWave(string path)
        {
            var next = new WaveLoop(File.ReadAllBytes(path));
            Stop();
            source = path;
            wave = next;
            PackSelection.SourceLoaded(this);
            peaks = wave.Peaks(790);
            start.Maximum = int.MaxValue;
            end.Maximum = int.MaxValue;
            start.Value = next.LoopStart;
            end.Value = next.LoopEnd;
            start.Maximum = next.Samples;
            end.Maximum = next.Samples;
            play.Enabled = export.Enabled = true;
            UpdateInfo();
        }

        void UpdateInfo()
        {
            Stop();
            DrawWave();
            if (wave != null)
                play.Enabled = export.Enabled = start.Value < end.Value && end.Value <= wave.Samples;
            if (wave != null)
                info.Text = Path.GetFileName(source) + "\n" + wave.SampleRate + " Hz • " + wave.Samples + " samples • " + (wave.Samples / (double)wave.SampleRate).ToString("F3") + " seconds\nLoop: " + (start.Value / wave.SampleRate).ToString("F3") + " s → " + (end.Value / wave.SampleRate).ToString("F3") + " s";
        }

        void DrawWave()
        {
            if (wave == null || peaks == null)
                return;
            var b = new Bitmap(790, 155);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(30, 30, 36));
                float left = (float)start.Value / wave.Samples * 790, right = (float)end.Value / wave.Samples * 790;
                using (var brush = new SolidBrush(Color.FromArgb(80, 130, 80, 230)))
                    if (right > left)
                        g.FillRectangle(brush, left, 0, right - left, 155);
                using (var pen = new Pen(Color.White))
                    for (int i = 0; i < peaks.Length; i++)
                        g.DrawLine(pen, i, 77 - peaks[i] * 70, i, 77 + peaks[i] * 70);
                g.DrawLine(Pens.LimeGreen, left, 0, left, 155);
                g.DrawLine(Pens.OrangeRed, right, 0, right, 155);
            }

            var old = waveform.Image;
            waveform.Image = b;
            if (old != null)
                old.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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
            string p = SavePath(Path.GetFileNameWithoutExtension(source) + "_loop.wav", "Looped WAV|*.wav");
            if (p == null)
                return;
            if (string.Equals(Path.GetFullPath(p), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a different file to preserve your source.");
            BackupManager.WriteAllBytesSafely(p, wave.Build((int)start.Value, (int)end.Value, false));
            ToolStatus.Set(this, true); Status.Text = "Saved loop markers: " + p + "\nAdd this WAV in Looping Audio Converter and choose BRSTM output.";
        }

        void Launch(string id, string path)
        {
            string error;
            if (!ToolchainManager.Launch(ToolchainManager.FindById(id), path, false, out error))
            {
                using (var f = new ToolchainForm())
                    f.ShowDialog(this);
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
                bool ok = await System.Threading.Tasks.Task.Run(delegate
                {
                    return ToolchainManager.RunCapture(ToolchainManager.FindById("ffmpeg"), "-nostdin -i " + ToolchainManager.QuoteArgument(p) + " -vn -ac 2 -ar 32000 -c:a pcm_s16le " + ToolchainManager.QuoteArgument(temp), out output, out error);
                });
                if (!ok)
                    throw new IOException(error);
                LoadWave(temp);
                source = p;
                UpdateInfo();
                Status.Text = "Converted to 32 kHz stereo PCM in memory. Save a looped WAV to keep your result.";
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
