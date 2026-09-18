using System;
using System.IO;

namespace murumsWiiModStudio
{
    internal static class BigEndian
    {
        public static uint ReadUInt32(byte[] data, int offset)
        {
            if (data == null || offset < 0 || (long)offset + 4 > data.Length)
                throw new InvalidDataException("Unexpected end of file while reading UInt32.");
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        public static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        public static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
