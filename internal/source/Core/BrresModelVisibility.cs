using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class BrresModelVisibility
    {
        internal sealed class Entry
        {
            public string Name;
            public int Data, Pointer;
        }

        static int U(byte[] b, int p)
        {
            return checked((int)BigEndian.ReadUInt32(b, p));
        }

        static void Check(byte[] b, int p, int n)
        {
            if (p < 0 || n < 0 || (long)p + n > b.Length)
                throw new InvalidDataException("BRRES offset out of bounds.");
        }

        internal static List<Entry> Dictionary(byte[] b, int p)
        {
            Check(b, p, 24);
            int count = U(b, p + 4);
            if (count > 65535)
                throw new InvalidDataException("Invalid dictionary count.");
            Check(b, p, checked(24 + count * 16));
            var list = new List<Entry>();
            for (int i = 0; i < count; i++)
            {
                int item = p + 24 + i * 16;
                int text = checked(p + U(b, item + 8)), data = checked(p + U(b, item + 12));
                Check(b, text, 1);
                Check(b, data, 1);
                int end = text;
                while (end < b.Length && b[end] != 0 && end - text < 1024)
                    end++;
                if (end == b.Length || end - text == 1024)
                    throw new InvalidDataException("Invalid BRRES name.");
                list.Add(new Entry { Name = Encoding.ASCII.GetString(b, text, end - text), Data = data, Pointer = item + 12 });
            }

            return list;
        }

        internal static List<Entry> Models(byte[] b)
        {
            Check(b, 0, 16);
            if (Encoding.ASCII.GetString(b, 0, 4) != "bres")
                throw new InvalidDataException("Expected BRRES.");
            int root = (b[12] << 8) | b[13];
            foreach (var group in Dictionary(b, root + 8))
                if (group.Name == "3DModels(NW4R)")
                    return Dictionary(b, group.Data);
            throw new InvalidDataException("No model dictionary.");
        }

        public static bool IsHidden(byte[] b)
        {
            bool found = false;
            foreach (var model in Models(b))
            {
                int dict = checked(model.Data + U(b, model.Data + 16));
                foreach (var entry in Dictionary(b, dict))
                {
                    if (entry.Name != "DrawOpa" && entry.Name != "DrawXlu")
                        continue;
                    found = true;
                    Check(b, entry.Data, 1);
                    if (b[entry.Data] != 1)
                        return false;
                }
            }

            if (!found)
                throw new InvalidDataException("No model draw lists found.");
            return true;
        }

        public static byte[] Hide(byte[] input)
        {
            byte[] b = (byte[])input.Clone();
            int edits = 0;
            foreach (var model in Models(b))
            {
                Check(b, model.Data, 24);
                if (Encoding.ASCII.GetString(b, model.Data, 4) != "MDL0")
                    throw new InvalidDataException("Expected MDL0.");
                int dict = checked(model.Data + U(b, model.Data + 16));
                foreach (var entry in Dictionary(b, dict))
                {
                    if (entry.Name != "DrawOpa" && entry.Name != "DrawXlu")
                        continue;
                    int pos = entry.Data;
                    while (true)
                    {
                        Check(b, pos, 1);
                        if (b[pos] == 1)
                            break;
                        if (b[pos] != 4)
                            throw new InvalidDataException("Unknown draw instruction; model unchanged.");
                        Check(b, pos, 8);
                        pos += 8;
                    }

                    BigEndian.WriteUInt32(b, entry.Pointer, (uint)(pos - dict));
                    edits++;
                }
            }

            if (edits == 0)
                throw new InvalidDataException("No model draw lists found.");
            return b;
        }
    }
}
