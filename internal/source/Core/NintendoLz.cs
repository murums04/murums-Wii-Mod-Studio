using System;
using System.IO;

namespace murumsWiiModStudio
{
    internal static class NintendoLz
    {
        public static byte[] Decode(byte[] input)
        {
            if (input == null || input.Length < 4 || input[0] != 0x10)
                throw new InvalidDataException("Expected Nintendo LZ10 data.");
            int size = input[1] | input[2] << 8 | input[3] << 16, pos = 4;
            if (size == 0)
            {
                if (input.Length < 8)
                    throw new InvalidDataException("Truncated LZ header.");
                size = BitConverter.ToInt32(input, 4);
                pos = 8;
            }

            if (size <= 0 || size > 128 * 1024 * 1024 || (long)size > (long)input.Length * 18)
                throw new InvalidDataException("Invalid LZ size.");
            byte[] output = new byte[size];
            int dst = 0;
            while (dst < size)
            {
                if (pos >= input.Length)
                    throw new InvalidDataException("Truncated LZ flags.");
                int flags = input[pos++];
                for (int bit = 7; bit >= 0 && dst < size; bit--)
                {
                    if ((flags & (1 << bit)) == 0)
                    {
                        if (pos >= input.Length)
                            throw new InvalidDataException("Truncated LZ literal.");
                        output[dst++] = input[pos++];
                    }
                    else
                    {
                        if (pos + 2 > input.Length)
                            throw new InvalidDataException("Truncated LZ match.");
                        int a = input[pos++], b = input[pos++], count = (a >> 4) + 3, distance = ((a & 15) << 8 | b) + 1;
                        if (distance > dst || count > size - dst)
                            throw new InvalidDataException("Invalid LZ match.");
                        for (int j = 0; j < count; j++)
                        {
                            output[dst] = output[dst - distance];
                            dst++;
                        }
                    }
                }
            }

            return output;
        }

        // A bounded hash-chain encoder; retains the game's original LZ10 format.
        public static byte[] Encode(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > 128 * 1024 * 1024)
                throw new InvalidDataException("Invalid LZ input size.");
            using (var s = new MemoryStream())
            {
                s.WriteByte(0x10);
                int n = data.Length;
                bool extended = n > 0xffffff;
                for (int i = 0; i < 3; i++)
                    s.WriteByte(extended ? (byte)0 : (byte)(n >> (i * 8)));
                if (extended)
                    for (int i = 0; i < 4; i++)
                        s.WriteByte((byte)(n >> (i * 8)));
                int[] head = new int[65536], prev = new int[4096];
                for (int i = 0; i < head.Length; i++)
                    head[i] = -1;
                int p = 0;
                while (p < n)
                {
                    long flagPos = s.Position;
                    s.WriteByte(0);
                    int flags = 0;
                    for (int bit = 7; bit >= 0 && p < n; bit--)
                    {
                        int len = 0, dist = 0, hash = p + 2 < n ? ((data[p] * 251 + data[p + 1]) * 251 + data[p + 2]) & 65535 : 0;
                        if (p + 2 < n)
                        {
                            int c = head[hash], tries = 0;
                            while (c >= 0 && p - c <= 4096 && c < p && tries++ < 48)
                            {
                                int l = 0;
                                while (l < 18 && p + l < n && data[c + l] == data[p + l])
                                    l++;
                                if (l > len && l >= 3)
                                {
                                    len = l;
                                    dist = p - c;
                                    if (l == 18)
                                        break;
                                }

                                int next = prev[c & 4095];
                                if (next >= c)
                                    break;
                                c = next;
                            }
                        }

                        int consume = len >= 3 ? len : 1;
                        if (len >= 3)
                        {
                            flags |= 1 << bit;
                            int d = dist - 1;
                            s.WriteByte((byte)(((len - 3) << 4) | (d >> 8)));
                            s.WriteByte((byte)d);
                        }
                        else
                            s.WriteByte(data[p]);
                        for (int j = 0; j < consume; j++, p++)
                        {
                            if (p + 2 < n)
                            {
                                int h = ((data[p] * 251 + data[p + 1]) * 251 + data[p + 2]) & 65535;
                                prev[p & 4095] = head[h];
                                head[h] = p;
                            }
                        }
                    }

                    long end = s.Position;
                    s.Position = flagPos;
                    s.WriteByte((byte)flags);
                    s.Position = end;
                }

                return s.ToArray();
            }
        }
    }
}
