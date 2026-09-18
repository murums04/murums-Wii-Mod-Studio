using System;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal sealed class WaveLoop
    {
        readonly byte[] data;
        public readonly int SampleRate, Samples, BlockAlign, DataOffset, DataLength;
        public readonly int LoopStart, LoopEnd;
        int sampleBits, channelCount;
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

        public float[] Peaks(int width)
        {
            if (width < 1 || width > 4096)
                throw new ArgumentOutOfRangeException("width");
            var peaks = new float[width];
            for (int x = 0; x < width; x++)
            {
                int from = (int)((long)x * Samples / width), to = Math.Max(from + 1, (int)((long)(x + 1) * Samples / width));
                for (int i = from; i < Math.Min(to, Samples); i++)
                    for (int c = 0; c < channelCount; c++)
                    {
                        int at = DataOffset + i * BlockAlign + c * sampleBits / 8;
                        float v = sampleBits == 8 ? Math.Abs((data[at] - 128) / 128f) : Math.Abs(BitConverter.ToInt16(data, at) / 32768f);
                        if (v > peaks[x])
                            peaks[x] = v;
                    }
            }

            return peaks;
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
