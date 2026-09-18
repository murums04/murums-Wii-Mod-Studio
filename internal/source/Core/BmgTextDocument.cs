using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace murumsWiiModStudio
{
    internal sealed class BmgTextDocument
    {
        public sealed class Row
        {
            public int Line;
            public string Id, Prefix, Text;
        }

        readonly string[] lines;
        public readonly List<Row> Rows = new List<Row>();
        public BmgTextDocument(string text)
        {
            if (!text.TrimStart().StartsWith("#BMG", StringComparison.Ordinal))
                throw new InvalidDataException("The decoder did not return a BMG text document.");
            lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i], @"^(\s*([A-Za-z0-9_]+)\s*(?:\[[^\]]*\]\s*)?=\s?)(.*)$");
                if (m.Success)
                    Rows.Add(new Row { Line = i, Id = m.Groups[2].Value, Prefix = m.Groups[1].Value, Text = m.Groups[3].Value });
            }
        }

        public string Build()
        {
            var output = (string[])lines.Clone();
            foreach (var row in Rows)
            {
                if (row.Text.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                    throw new InvalidDataException("Use the BMG escape \\n for a line break inside a message.");
                output[row.Line] = row.Prefix + row.Text;
            }

            return string.Join("\n", output);
        }

        public static string Decode(byte[] bytes)
        {
            string temp = Path.Combine(Path.GetTempPath(), "murums_message_" + Guid.NewGuid().ToString("N") + ".bmg");
            try
            {
                File.WriteAllBytes(temp, bytes);
                string output, error;
                if (!ToolchainManager.RunCapture(ToolchainManager.FindById("wbmgt"), "CAT --single-line " + ToolchainManager.QuoteArgument(temp), out output, out error))
                    throw new IOException(error);
                return output;
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        public byte[] Encode()
        {
            string temp = Path.Combine(Path.GetTempPath(), "murums_message_" + Guid.NewGuid().ToString("N") + ".txt"), dest = temp + ".bmg";
            try
            {
                File.WriteAllText(temp, Build(), new UTF8Encoding(false));
                string output, error;
                if (!ToolchainManager.RunCapture(ToolchainManager.FindById("wbmgt"), "ENCODE --dest " + ToolchainManager.QuoteArgument(dest) + " " + ToolchainManager.QuoteArgument(temp), out output, out error))
                    throw new IOException(error + "\n" + output);
                byte[] bytes = File.ReadAllBytes(dest);
                if (bytes.Length < 32 || Encoding.ASCII.GetString(bytes, 0, 8) != "MESGbmg1")
                    throw new InvalidDataException("The encoder did not produce a BMG.");
                return bytes;
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
                if (File.Exists(dest))
                    File.Delete(dest);
            }
        }
    }
}
