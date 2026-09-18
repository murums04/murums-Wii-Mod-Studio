using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal static class FormatInspector
    {
        public static string Inspect(string path, ResourceInfo info)
        {
            byte[] data = File.ReadAllBytes(path);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("File: " + Path.GetFileName(path));
            sb.AppendLine("Type: " + info.DisplayName);
            sb.AppendLine("Module: " + info.Module);
            sb.AppendLine("Size: " + data.Length.ToString("N0") + " bytes");
            if (!String.IsNullOrEmpty(info.Magic))
                sb.AppendLine("Magic: " + info.Magic);
            sb.AppendLine();
            sb.AppendLine(info.Description);
            sb.AppendLine();
            if (Yaz0.IsYaz0(data))
            {
                data = Yaz0.Decompress(data);
                sb.AppendLine("Decompressed size: " + data.Length.ToString("N0") + " bytes");
            }

            switch (info.Kind)
            {
                case ResourceKind.Kmp:
                    InspectKmp(data, sb);
                    break;
                case ResourceKind.Kcl:
                    InspectKcl(data, sb);
                    break;
                case ResourceKind.Brres:
                    InspectBrres(data, sb);
                    break;
                case ResourceKind.Brstm:
                    InspectBrstm(data, sb);
                    break;
                case ResourceKind.Bmg:
                    InspectSectioned(data, sb, new string[] { "INF1", "DAT1", "MID1", "FLW1", "FLI1" });
                    break;
                case ResourceKind.Brsar:
                    InspectSectioned(data, sb, new string[] { "SYMB", "INFO", "FILE" });
                    break;
                case ResourceKind.Breff:
                    InspectSectioned(data, sb, new string[] { "REFF" });
                    break;
                case ResourceKind.Breft:
                    InspectSectioned(data, sb, new string[] { "REFT" });
                    break;
                case ResourceKind.Brfnt:
                    InspectSectioned(data, sb, new string[] { "FINF", "TGLP", "CWDH", "CMAP" });
                    break;
                default:
                    InspectCommonMagics(data, sb);
                    break;
            }

            return sb.ToString();
        }

        private static void InspectKmp(byte[] data, StringBuilder sb)
        {
            if (data.Length < 0x10 || Ascii(data, 0, 4) != "RKMD")
            {
                sb.AppendLine("KMP header not recognized.");
                return;
            }

            uint size = BE32(data, 4);
            ushort sectionCount = BE16(data, 8);
            ushort headerSize = BE16(data, 10);
            sb.AppendLine("KMP header");
            sb.AppendLine("  Declared size: " + size);
            sb.AppendLine("  Sections: " + sectionCount);
            sb.AppendLine("  Header size: 0x" + headerSize.ToString("X"));
            if (headerSize >= 4 && headerSize <= data.Length)
                sb.AppendLine("  Version: 0x" + BE32(data, headerSize - 4).ToString("X"));
            sb.AppendLine();
            sb.AppendLine("Sections");
            int offsetBase = headerSize;
            for (int i = 0; i < sectionCount && 0x10 + i * 4 <= data.Length; i++)
            {
                int rel = (int)BE32(data, 0x10 + i * 4);
                int off = offsetBase + rel;
                if (off < 0 || off + 8 > data.Length)
                    continue;
                string tag = Ascii(data, off, 4);
                ushort count = BE16(data, off + 4);
                sb.AppendLine("  " + tag + "  count=" + count + "  @0x" + off.ToString("X"));
            }
        }

        private static void InspectKcl(byte[] data, StringBuilder sb)
        {
            if (data.Length < 0x38)
            {
                sb.AppendLine("KCL file is too small for a Mario Kart Wii KCL header.");
                return;
            }

            uint pos = BE32(data, 0);
            uint nrm = BE32(data, 4);
            uint prism = BE32(data, 8);
            uint block = BE32(data, 12);
            sb.AppendLine("KCL header");
            sb.AppendLine("  Position data: 0x" + pos.ToString("X"));
            sb.AppendLine("  Normal data:   0x" + nrm.ToString("X"));
            sb.AppendLine("  Prism data:    0x" + prism.ToString("X"));
            sb.AppendLine("  Octree data:   0x" + block.ToString("X"));
            if (nrm > pos)
                sb.AppendLine("  Position vectors (estimated): " + ((nrm - pos) / 12));
            if (prism > nrm)
                sb.AppendLine("  Normal vectors (estimated): " + ((prism - nrm) / 12));
            if (block > prism)
                sb.AppendLine("  Prism records (estimated): " + ((block - prism) / 16));
        }

        private static void InspectBrres(byte[] data, StringBuilder sb)
        {
            if (data.Length < 0x10 || Ascii(data, 0, 4) != "bres")
            {
                sb.AppendLine("BRRES header not recognized.");
                return;
            }

            sb.AppendLine("BRRES header");
            sb.AppendLine("  Declared size: " + BE32(data, 8));
            sb.AppendLine("  Root offset: 0x" + BE16(data, 12).ToString("X"));
            sb.AppendLine();
            sb.AppendLine("Detected subresources");
            string[] tags = new string[]
            {
                "MDL0",
                "TEX0",
                "CHR0",
                "SRT0",
                "PAT0",
                "CLR0",
                "SHP0",
                "SCN0",
                "PLT0",
                "VIS0"
            };
            for (int t = 0; t < tags.Length; t++)
            {
                List<int> hits = FindAscii(data, tags[t], 64);
                if (hits.Count == 0)
                    continue;
                sb.Append("  " + tags[t] + ": " + hits.Count + " found");
                if (hits.Count <= 6)
                {
                    sb.Append(" (");
                    for (int i = 0; i < hits.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        sb.Append("0x" + hits[i].ToString("X"));
                    }

                    sb.Append(")");
                }

                sb.AppendLine();
            }
        }

        private static void InspectBrstm(byte[] data, StringBuilder sb)
        {
            if (data.Length < 0x40 || Ascii(data, 0, 4) != "RSTM")
            {
                sb.AppendLine("BRSTM header not recognized.");
                return;
            }

            sb.AppendLine("BRSTM header");
            sb.AppendLine("  Version: 0x" + BE16(data, 6).ToString("X4"));
            sb.AppendLine("  Declared size: " + BE32(data, 8));
            sb.AppendLine("  HEAD: 0x" + BE32(data, 0x10).ToString("X") + " / " + BE32(data, 0x14) + " bytes");
            sb.AppendLine("  ADPC: 0x" + BE32(data, 0x18).ToString("X") + " / " + BE32(data, 0x1C) + " bytes");
            sb.AppendLine("  DATA: 0x" + BE32(data, 0x20).ToString("X") + " / " + BE32(data, 0x24) + " bytes");
        }

        private static void InspectSectioned(byte[] data, StringBuilder sb, string[] tags)
        {
            sb.AppendLine("Detected sections");
            for (int t = 0; t < tags.Length; t++)
            {
                List<int> hits = FindAscii(data, tags[t], 32);
                for (int i = 0; i < hits.Count; i++)
                    sb.AppendLine("  " + tags[t] + " @0x" + hits[i].ToString("X"));
            }
        }

        private static void InspectCommonMagics(byte[] data, StringBuilder sb)
        {
            string[] tags = new string[]
            {
                "RLAN",
                "RLYT",
                "bres",
                "MDL0",
                "TEX0",
                "RKMD",
                "RSTM",
                "RSAR",
                "MESG",
                "REFF",
                "REFT",
                "RFNT"
            };
            InspectSectioned(data, sb, tags);
        }

        public static string HexPreview(byte[] data, int maxBytes)
        {
            if (data == null)
                return "";
            int len = Math.Min(data.Length, maxBytes);
            StringBuilder sb = new StringBuilder();
            for (int offset = 0; offset < len; offset += 16)
            {
                sb.Append(offset.ToString("X8"));
                sb.Append("  ");
                for (int i = 0; i < 16; i++)
                {
                    int p = offset + i;
                    if (p < len)
                        sb.Append(data[p].ToString("X2") + " ");
                    else
                        sb.Append("   ");
                    if (i == 7)
                        sb.Append(" ");
                }

                sb.Append(" |");
                for (int i = 0; i < 16 && offset + i < len; i++)
                {
                    byte b = data[offset + i];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }

                sb.AppendLine("|");
            }

            if (data.Length > len)
                sb.AppendLine("... preview truncated ...");
            return sb.ToString();
        }

        private static List<int> FindAscii(byte[] data, string text, int max)
        {
            List<int> r = new List<int>();
            byte[] q = Encoding.ASCII.GetBytes(text);
            for (int i = 0; i <= data.Length - q.Length && r.Count < max; i++)
            {
                bool ok = true;
                for (int j = 0; j < q.Length; j++)
                    if (data[i + j] != q[j])
                    {
                        ok = false;
                        break;
                    }

                if (ok)
                    r.Add(i);
            }

            return r;
        }

        private static string Ascii(byte[] d, int o, int n)
        {
            if (o < 0 || o + n > d.Length)
                return "";
            return Encoding.ASCII.GetString(d, o, n);
        }

        private static ushort BE16(byte[] d, int o)
        {
            if (o < 0 || o + 2 > d.Length)
                return 0;
            return (ushort)((d[o] << 8) | d[o + 1]);
        }

        private static uint BE32(byte[] d, int o)
        {
            if (o < 0 || o + 4 > d.Length)
                return 0;
            return ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];
        }
    }
}
