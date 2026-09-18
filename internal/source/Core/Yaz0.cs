using System;
using System.Collections.Generic;
using System.IO;

namespace murumsWiiModStudio
{
    public static class Yaz0
    {
        public static bool IsYaz0(byte[] data)
        {
            return data != null && data.Length >= 16 && data[0] == (byte)'Y' && data[1] == (byte)'a' && data[2] == (byte)'z' && (data[3] == (byte)'0' || data[3] == (byte)'1');
        }

        public static byte[] Decompress(byte[] input)
        {
            if (!IsYaz0(input))
                throw new InvalidDataException("Not a Yaz0/Yaz1 stream.");
            uint outSizeU = BigEndian.ReadUInt32(input, 4);
            if (outSizeU > int.MaxValue)
                throw new InvalidDataException("Decompressed file is too large.");
            int outSize = (int)outSizeU;
            // Even an all-match stream cannot produce more than 273 bytes per
            // input byte. Reject impossible headers before allocating memory.
            if (outSize > (long)(input.Length - 16) * 273L)
                throw new InvalidDataException("Invalid Yaz0 decompressed size.");
            byte[] output = new byte[outSize];
            int src = 16;
            int dst = 0;
            int validBits = 0;
            int code = 0;
            while (dst < outSize)
            {
                if (validBits == 0)
                {
                    if (src >= input.Length)
                        throw new InvalidDataException("Truncated Yaz0 stream.");
                    code = input[src++];
                    validBits = 8;
                }

                if ((code & 0x80) != 0)
                {
                    if (src >= input.Length)
                        throw new InvalidDataException("Truncated Yaz0 literal.");
                    output[dst++] = input[src++];
                }
                else
                {
                    if (src + 1 >= input.Length)
                        throw new InvalidDataException("Truncated Yaz0 back-reference.");
                    int b1 = input[src++];
                    int b2 = input[src++];
                    int distance = ((b1 & 0x0F) << 8) | b2;
                    int copySrc = dst - (distance + 1);
                    if (copySrc < 0)
                        throw new InvalidDataException("Invalid Yaz0 back-reference distance.");
                    int length = b1 >> 4;
                    if (length == 0)
                    {
                        if (src >= input.Length)
                            throw new InvalidDataException("Truncated Yaz0 long back-reference.");
                        length = input[src++] + 0x12;
                    }
                    else
                    {
                        length += 2;
                    }

                    if (length > outSize - dst)
                        throw new InvalidDataException("Yaz0 back-reference exceeds the declared output size.");
                    for (int i = 0; i < length; i++)
                    {
                        output[dst] = output[copySrc];
                        dst++;
                        copySrc++;
                    }
                }

                code <<= 1;
                validBits--;
            }

            return output;
        }

        // Format detection only needs four decoded bytes. Never allocate the
        // declared full file size merely to identify a resource.
        public static byte[] ReadMagic(byte[] input)
        {
            if (!IsYaz0(input) || input.Length < 17 || BigEndian.ReadUInt32(input, 4) < 4)
                throw new InvalidDataException("Invalid Yaz0 resource header.");
            byte[] prefix = new byte[4];
            int src = 17, dst = 0, code = input[16];
            while (dst < 4)
            {
                if ((code & 128) != 0)
                {
                    if (src >= input.Length)
                        throw new InvalidDataException("Truncated Yaz0 prefix.");
                    prefix[dst++] = input[src++];
                }
                else
                {
                    if (src + 1 >= input.Length)
                        throw new InvalidDataException("Truncated Yaz0 prefix.");
                    int first = input[src++], second = input[src++];
                    int from = dst - (((first & 15) << 8) | second) - 1;
                    if (from < 0)
                        throw new InvalidDataException("Invalid Yaz0 prefix distance.");
                    int length = (first >> 4) + 2;
                    if ((first >> 4) == 0)
                    {
                        if (src >= input.Length)
                            throw new InvalidDataException("Truncated Yaz0 prefix.");
                        length = input[src++] + 18;
                    }

                    while (length-- > 0 && dst < 4)
                        prefix[dst++] = prefix[from++];
                }

                code <<= 1;
            }

            return prefix;
        }

        public static byte[] Compress(byte[] input)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            using (MemoryStream output = new MemoryStream())
            {
                output.WriteByte((byte)'Y');
                output.WriteByte((byte)'a');
                output.WriteByte((byte)'z');
                output.WriteByte((byte)'0');
                BigEndian.WriteUInt32(output, (uint)input.Length);
                for (int i = 0; i < 8; i++)
                    output.WriteByte(0);
                if (input.Length == 0)
                    return output.ToArray();
                int[] head = new int[65536];
                for (int i = 0; i < head.Length; i++)
                    head[i] = -1;
                int[] prev = new int[input.Length];
                for (int i = 0; i < prev.Length; i++)
                    prev[i] = -1;
                int pos = 0;
                while (pos < input.Length)
                {
                    byte code = 0;
                    List<byte> group = new List<byte>(32);
                    for (int bit = 0; bit < 8 && pos < input.Length; bit++)
                    {
                        int matchPos;
                        int matchLen;
                        FindBestMatch(input, pos, head, prev, out matchPos, out matchLen);
                        if (matchLen >= 3)
                        {
                            int distance = pos - matchPos - 1;
                            if (matchLen >= 0x12)
                            {
                                group.Add((byte)((distance >> 8) & 0x0F));
                                group.Add((byte)(distance & 0xFF));
                                group.Add((byte)(matchLen - 0x12));
                            }
                            else
                            {
                                group.Add((byte)(((matchLen - 2) << 4) | ((distance >> 8) & 0x0F)));
                                group.Add((byte)(distance & 0xFF));
                            }

                            for (int k = 0; k < matchLen; k++)
                                InsertPosition(input, pos + k, head, prev);
                            pos += matchLen;
                        }
                        else
                        {
                            code |= (byte)(0x80 >> bit);
                            group.Add(input[pos]);
                            InsertPosition(input, pos, head, prev);
                            pos++;
                        }
                    }

                    output.WriteByte(code);
                    for (int i = 0; i < group.Count; i++)
                        output.WriteByte(group[i]);
                }

                return output.ToArray();
            }
        }

        private static int Hash(byte[] data, int pos)
        {
            if (pos + 2 >= data.Length)
                return -1;
            int h = data[pos];
            h = ((h * 251) ^ data[pos + 1]) & 0xFFFF;
            h = ((h * 251) ^ data[pos + 2]) & 0xFFFF;
            return h;
        }

        private static void InsertPosition(byte[] data, int pos, int[] head, int[] prev)
        {
            if (pos < 0 || pos >= data.Length)
                return;
            int h = Hash(data, pos);
            if (h < 0)
                return;
            prev[pos] = head[h];
            head[h] = pos;
        }

        private static void FindBestMatch(byte[] data, int pos, int[] head, int[] prev, out int bestPos, out int bestLen)
        {
            bestPos = -1;
            bestLen = 0;
            int h = Hash(data, pos);
            if (h < 0)
                return;
            int candidate = head[h];
            int minPos = pos - 0x1000;
            if (minPos < 0)
                minPos = 0;
            int maxLen = Math.Min(273, data.Length - pos);
            int checkedCandidates = 0;
            while (candidate >= minPos && candidate >= 0 && checkedCandidates < 128)
            {
                int len = 0;
                while (len < maxLen && data[candidate + len] == data[pos + len])
                {
                    len++;
                    if (candidate + len >= data.Length)
                        break;
                }

                if (len > bestLen)
                {
                    bestLen = len;
                    bestPos = candidate;
                    if (bestLen == maxLen)
                        break;
                }

                candidate = prev[candidate];
                checkedCandidates++;
            }

            if (bestLen < 3)
            {
                bestLen = 0;
                bestPos = -1;
            }
        }
    }
}
