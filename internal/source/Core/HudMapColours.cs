using System;
using System.Drawing;
using System.Linq;
using System.IO;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal static class HudMapColours
    {
        public static bool IsLayout(string key)
        {
            string n = Path.GetFileName(key).ToLowerInvariant();
            return n == "common_w040_map_set_position.brlyt" || n == "common_w40_map_set_position.brlyt";
        }

        static BrlytPaneInfo Pane(BrlytDocument d)
        {
            var p = d.Panes.FirstOrDefault(x => x.Name == "race_null" && x.Magic == "pic1" && x.Size >= 0x60);
            if (p == null || p.MaterialId < 0 || p.MaterialId >= d.Materials.Count)
                throw new InvalidDataException("Minimap race_null picture/material not found.");
            return p;
        }

        public static Color[] Read(byte[] bytes)
        {
            var d = BrlytDocument.FromBytes(bytes);
            var p = Pane(d);
            var result = new Color[5];
            int m = d.Materials[p.MaterialId].Offset + 20;
            int[] v = new int[4];
            for (int i = 0; i < 4; i++)
            {
                int at = m + i * 2;
                v[i] = d.LittleEndian ? d.Data[at] | d.Data[at + 1] << 8 : d.Data[at] << 8 | d.Data[at + 1];
                if (v[i] > 255)
                    throw new InvalidDataException("Signed minimap material colours are not supported.");
            }

            result[0] = Color.FromArgb(v[3], v[0], v[1], v[2]);
            for (int i = 0; i < 4; i++)
            {
                int at = p.Offset + 0x4c + i * 4;
                result[i + 1] = Color.FromArgb(d.Data[at + 3], d.Data[at], d.Data[at + 1], d.Data[at + 2]);
            }

            return result;
        }

        public static Color Foreground(byte[] bytes)
        {
            var d = BrlytDocument.FromBytes(bytes);
            var p = Pane(d);
            int at = d.Materials[p.MaterialId].Offset + 28;
            int[] v = new int[4];
            for (int i = 0; i < 4; i++)
            {
                v[i] = d.LittleEndian ? d.Data[at + i * 2] | d.Data[at + i * 2 + 1] << 8 : d.Data[at + i * 2] << 8 | d.Data[at + i * 2 + 1];
                v[i] = Math.Min(255, v[i]);
            }

            return Color.FromArgb(v[3], v[0], v[1], v[2]);
        }

        public static byte[] Apply(byte[] bytes, Color[] colours)
        {
            if (colours == null || colours.Length != 5)
                throw new ArgumentException("Expected material colour and four corners.");
            var d = BrlytDocument.FromBytes(bytes);
            var p = Pane(d);
            int m = d.Materials[p.MaterialId].Offset + 20;
            var c = colours[0];
            byte[] v =
            {
                c.R,
                c.G,
                c.B,
                c.A
            };
            for (int i = 0; i < 4; i++)
            {
                d.Data[m + i * 2 + (d.LittleEndian ? 0 : 1)] = v[i];
                d.Data[m + i * 2 + (d.LittleEndian ? 1 : 0)] = 0;
            }

            for (int i = 0; i < 4; i++)
            {
                c = colours[i + 1];
                int at = p.Offset + 0x4c + i * 4;
                d.Data[at] = c.R;
                d.Data[at + 1] = c.G;
                d.Data[at + 2] = c.B;
                d.Data[at + 3] = c.A;
            }

            return d.Data;
        }
    }
}
