using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace murumsWiiModStudio
{
    internal sealed class WaveLoop
    {
        readonly byte[] data;
        public readonly int SampleRate, Samples, BlockAlign, DataOffset, DataLength;
        public readonly int LoopStart, LoopEnd;
        int sampleBits, channelCount;
        public WaveAnalysis Analysis { get; private set; }
        public int Channels { get { return channelCount; } }

        internal sealed class WaveAnalysis
        {
            public readonly double[] PeakDbFs;
            public readonly long[] FullScaleSamples;
            internal WaveAnalysis(int[] amplitudes, long[] fullScale, int scale)
            {
                PeakDbFs = new double[amplitudes.Length];
                FullScaleSamples = fullScale;
                for (int c = 0; c < amplitudes.Length; c++)
                    PeakDbFs[c] = amplitudes[c] == 0 ? double.NegativeInfinity : 20 * Math.Log10(amplitudes[c] / (double)scale);
            }
        }
        public WaveLoop(byte[] bytes)
        {
            data = bytes;
            if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE" || (long)BitConverter.ToUInt32(bytes, 4) + 8 != bytes.Length)
                throw new InvalidDataException("Expected a complete RIFF WAV file.");
            int rate = 0, align = 0, offset = -1, length = 0, loopStart = 0, loopEnd = -1;
            for (int p = 12; p < bytes.Length;)
            {
                if (p + 8 > bytes.Length)
                    throw new InvalidDataException("Truncated WAV chunk.");
                int size = checked((int)BitConverter.ToUInt32(bytes, p + 4));
                if ((long)p + 8 + size + (size & 1) > bytes.Length)
                    throw new InvalidDataException("Invalid WAV chunk size.");
                string tag = Encoding.ASCII.GetString(bytes, p, 4);
                if (tag == "fmt ")
                {
                    if (rate != 0)
                        throw new InvalidDataException("Multiple WAV format chunks are not supported.");
                    if (size < 16 || BitConverter.ToUInt16(bytes, p + 8) != 1)
                        throw new NotSupportedException("Use an uncompressed PCM WAV. Convert other formats using FFmpeg first.");
                    int channels = BitConverter.ToUInt16(bytes, p + 10), bits = BitConverter.ToUInt16(bytes, p + 22);
                    sampleBits = bits;
                    channelCount = channels;
                    rate = checked((int)BitConverter.ToUInt32(bytes, p + 12));
                    align = BitConverter.ToUInt16(bytes, p + 20);
                    if (channels < 1 || channels > 2 || bits != 8 && bits != 16 || align != channels * bits / 8 || rate < 8000 || rate > 192000)
                        throw new NotSupportedException("Use a mono or stereo 8/16-bit PCM WAV (8–192 kHz).");
                }

                if (tag == "data")
                {
                    if (offset >= 0)
                        throw new InvalidDataException("Multiple WAV data chunks are not supported.");
                    offset = p + 8;
                    length = size;
                }

                if (tag == "smpl" && size >= 60 && BitConverter.ToUInt32(bytes, p + 36) > 0 && BitConverter.ToUInt32(bytes, p + 48) == 0)
                {
                    loopStart = checked((int)BitConverter.ToUInt32(bytes, p + 52));
                    loopEnd = checked((int)BitConverter.ToUInt32(bytes, p + 56) + 1);
                }

                p = checked(p + 8 + size + (size & 1));
            }

            if (rate == 0 || align == 0 || offset < 0 || length == 0 || length % align != 0)
                throw new InvalidDataException("Incomplete WAV audio.");
            SampleRate = rate;
            BlockAlign = align;
            DataOffset = offset;
            DataLength = length;
            Samples = length / align;
            if (loopEnd == -1)
                loopEnd = Samples;
            if (loopStart < 0 || loopEnd <= loopStart || loopEnd > Samples)
                throw new InvalidDataException("The WAV loop markers are outside the audio data.");
            LoopStart = loopStart;
            LoopEnd = loopEnd;
        }

        public decimal SampleToSeconds(int sample)
        {
            if (sample < 0 || sample > Samples) throw new ArgumentOutOfRangeException("sample");
            return (decimal)sample / SampleRate;
        }

        public int SecondsToSample(decimal seconds)
        {
            if (seconds < 0 || seconds > SampleToSeconds(Samples) + 0.5m / SampleRate)
                throw new ArgumentOutOfRangeException("seconds");
            decimal sample = decimal.Round(seconds * SampleRate, 0, MidpointRounding.AwayFromZero);
            if (sample > Samples) throw new ArgumentOutOfRangeException("seconds");
            return (int)sample;
        }

        public float[] Peaks(int width)
        {
            return Peaks(width, CancellationToken.None);
        }

        public float[] Peaks(int width, CancellationToken cancellation)
        {
            if (width < 1 || width > 4096)
                throw new ArgumentOutOfRangeException("width");
            var peaks = new float[width];
            bool analyze = Analysis == null;
            var amplitudes = new int[channelCount];
            var fullScale = new long[channelCount];
            int analyzedSample = -1;
            int scale = sampleBits == 8 ? 128 : 32768;
            for (int x = 0; x < width; x++)
            {
                cancellation.ThrowIfCancellationRequested();
                int from = (int)((long)x * Samples / width), to = Math.Max(from + 1, (int)((long)(x + 1) * Samples / width));
                for (int i = from; i < Math.Min(to, Samples); i++)
                {
                    if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                    for (int c = 0; c < channelCount; c++)
                    {
                        int sample = ReadSample(i, c), amplitude = Math.Abs(sample);
                        float v = amplitude / (float)scale;
                        if (v > peaks[x])
                            peaks[x] = v;
                        if (analyze && i > analyzedSample)
                        {
                            amplitudes[c] = Math.Max(amplitudes[c], amplitude);
                            if (sample == -scale || sample == scale - 1) fullScale[c]++;
                        }
                    }
                    analyzedSample = i;
                }
            }

            cancellation.ThrowIfCancellationRequested();
            if (analyze) Analysis = new WaveAnalysis(amplitudes, fullScale, scale);
            return peaks;
        }

        int ReadSample(int sample, int channel)
        {
            int at = DataOffset + sample * BlockAlign + channel * sampleBits / 8;
            return sampleBits == 8 ? data[at] - 128 : (short)(data[at] | data[at + 1] << 8);
        }

        public double[] LoopBoundaryDelta(int start, int end)
        {
            if (start < 0 || end <= start || end > Samples)
                throw new ArgumentOutOfRangeException("end", "Loop start must precede loop end, within the audio's sample count.");
            var delta = new double[channelCount];
            int scale = sampleBits == 8 ? 128 : 32768;
            for (int c = 0; c < channelCount; c++)
                delta[c] = Math.Abs(ReadSample(end - 1, c) - ReadSample(start, c)) / (double)scale;
            return delta;
        }

        public const int ZeroCrossingWindowMilliseconds = 5;
        const int MaximumBoundaryCandidates = 32;

        public Tuple<int, int> FindNearbyZeroCrossings(int start, int end)
        {
            if (start < 0 || end <= start || end > Samples)
                throw new ArgumentOutOfRangeException("end", "Loop start must precede loop end, within the audio's sample count.");
            int radius = SampleRate * ZeroCrossingWindowMilliseconds / 1000;
            var starts = BoundaryCandidates(start, true, radius);
            var ends = BoundaryCandidates(end, false, radius);
            int bestStart = start, bestEnd = end, bestDistance = 0;
            long originalScore = BoundaryScore(start, end), bestScore = originalScore;
            foreach (int nextStart in starts)
                foreach (int nextEnd in ends)
                {
                    if (nextStart >= nextEnd || !SeamDoesNotWorsen(start, end, nextStart, nextEnd)) continue;
                    long score = BoundaryScore(nextStart, nextEnd);
                    int distance = Math.Abs(nextStart - start) + Math.Abs(nextEnd - end);
                    if (score < bestScore || score == bestScore && score < originalScore
                        && (distance < bestDistance || distance == bestDistance
                            && (nextStart < bestStart || nextStart == bestStart && nextEnd < bestEnd)))
                    {
                        bestStart = nextStart; bestEnd = nextEnd; bestScore = score; bestDistance = distance;
                    }
                }
            return Tuple.Create(bestStart, bestEnd);
        }

        List<int> BoundaryCandidates(int original, bool isStart, int radius)
        {
            var candidates = new List<int>();
            int first = Math.Max(isStart ? 0 : 1, original - radius);
            int last = (int)Math.Min(isStart ? Samples - 1 : Samples, (long)original + radius);
            for (int boundary = first; boundary <= last; boundary++)
            {
                if (boundary == original || !HasZeroCrossing(boundary)) continue;
                bool suitable = true;
                for (int c = 0; c < channelCount; c++)
                    if (Math.Abs(ReadSample(isStart ? boundary : boundary - 1, c))
                        > Math.Abs(ReadSample(isStart ? original : original - 1, c))) suitable = false;
                if (!suitable) continue;
                int position = 0;
                while (position < candidates.Count && CompareBoundary(candidates[position], boundary, original, isStart) <= 0) position++;
                candidates.Insert(position, boundary);
                if (candidates.Count > MaximumBoundaryCandidates) candidates.RemoveAt(candidates.Count - 1);
            }
            // Die urspruengliche Grenze bleibt auch bei einer einseitigen Verbesserung zulaessig.
            candidates.Add(original);
            return candidates;
        }

        bool HasZeroCrossing(int boundary)
        {
            for (int c = 0; c < channelCount; c++)
            {
                int before = ReadSample(Math.Max(0, boundary - 1), c);
                int after = ReadSample(Math.Min(Samples - 1, boundary), c);
                if (before == 0 || after == 0 || before < 0 && after > 0 || before > 0 && after < 0) return true;
            }
            return false;
        }

        int CompareBoundary(int left, int right, int original, bool isStart)
        {
            int leftAmplitude = 0, rightAmplitude = 0;
            for (int c = 0; c < channelCount; c++)
            {
                leftAmplitude += Math.Abs(ReadSample(isStart ? left : left - 1, c));
                rightAmplitude += Math.Abs(ReadSample(isStart ? right : right - 1, c));
            }
            int order = leftAmplitude.CompareTo(rightAmplitude);
            if (order == 0) order = Math.Abs(left - original).CompareTo(Math.Abs(right - original));
            return order == 0 ? left.CompareTo(right) : order;
        }

        bool SeamDoesNotWorsen(int start, int end, int nextStart, int nextEnd)
        {
            for (int c = 0; c < channelCount; c++)
                if (Math.Abs(ReadSample(nextEnd - 1, c) - ReadSample(nextStart, c))
                    > Math.Abs(ReadSample(end - 1, c) - ReadSample(start, c))) return false;
            return true;
        }

        long BoundaryScore(int start, int end)
        {
            long score = 0;
            for (int c = 0; c < channelCount; c++)
            {
                int first = ReadSample(start, c), last = ReadSample(end - 1, c);
                score += Math.Abs(first) + Math.Abs(last) + Math.Abs(last - first);
            }
            return score;
        }

        public byte[] Build(int start, int end, bool preview)
        {
            if (start < 0 || end <= start || end > Samples)
                throw new ArgumentOutOfRangeException("end", "Loop start must precede loop end, within the audio's sample count.");
            using (var output = new MemoryStream())
            using (var w = new BinaryWriter(output))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(0);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                for (int p = 12; p < data.Length;)
                {
                    int size = (int)BitConverter.ToUInt32(data, p + 4);
                    string tag = Encoding.ASCII.GetString(data, p, 4);
                    if (tag == "data" && preview)
                    {
                        int count = (end - start) * BlockAlign;
                        w.Write(Encoding.ASCII.GetBytes("data"));
                        w.Write(count);
                        w.Write(data, DataOffset + start * BlockAlign, count);
                        if ((count & 1) != 0)
                            w.Write((byte)0);
                    }
                    else if (tag != "smpl")
                        w.Write(data, p, 8 + size + (size & 1));
                    p += 8 + size + (size & 1);
                }

                if (!preview)
                {
                    w.Write(Encoding.ASCII.GetBytes("smpl"));
                    w.Write(60);
                    w.Write(0);
                    w.Write(0);
                    w.Write((uint)(1000000000L / SampleRate));
                    w.Write(60);
                    w.Write(0);
                    w.Write(0);
                    w.Write(0);
                    w.Write(1);
                    w.Write(0);
                    w.Write(0);
                    w.Write(0);
                    w.Write(start);
                    w.Write(end - 1);
                    w.Write(0);
                    w.Write(0);
                }

                w.Flush();
                output.Position = 4;
                w.Write(checked((int)output.Length - 8));
                return output.ToArray();
            }
        }
    }
}
