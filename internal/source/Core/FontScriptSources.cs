using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed class FontScriptSources
    {
        internal string Latin, Japanese, Other;
        internal int MainScript;
        internal string MainPath
        {
            get
            {
                if (MainScript < 0 || MainScript > 2) throw new InvalidOperationException("Invalid main font selection.");
                return MainScript == 1 ? Japanese : MainScript == 2 ? Other : Latin;
            }
        }

        internal static int Script(int code)
        {
            if (code == 0x3005 || code == 0x3006 || code == 0x3007 || (code >= 0x3040 && code <= 0x30FF) || (code >= 0x31F0 && code <= 0x31FF)
                || (code >= 0x3400 && code <= 0x4DBF) || (code >= 0x4E00 && code <= 0x9FFF)
                || (code >= 0xF900 && code <= 0xFAFF) || (code >= 0xFF66 && code <= 0xFF9F)
                || (code >= 0x20000 && code <= 0x323AF))
                return 1;
            if (code <= 0x02AF || (code >= 0x0300 && code <= 0x036F)
                || (code >= 0x1D00 && code <= 0x1EFF) || (code >= 0x2C60 && code <= 0x2C7F)
                || (code >= 0xA720 && code <= 0xA7FF) || (code >= 0xFB00 && code <= 0xFB06) || (code >= 0xAB30 && code <= 0xAB6F)
                || (code >= 0xFF01 && code <= 0xFF60))
                return 0;
            return 2;
        }

        internal string ForCharacter(int code)
        {
            int script = Script(code);
            string assigned = script == 1 ? Japanese : script == 2 ? Other : Latin;
            return String.IsNullOrEmpty(assigned) ? MainPath : assigned;
        }

        internal IEnumerable<string> Paths
        {
            get { return new[] { Latin, Japanese, Other }.Where(p => !String.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase); }
        }

        internal static string Validate(string path, int script)
        {
            var coverage = TtfCoverage.Unicode(path);
            var letters = coverage.Where(c => c <= 65535 && Char.IsLetterOrDigit((char)c) && BrfntFont.IsTextCharacter(c) && Script(c) == script).ToArray();
            if (letters.Length == 0)
                throw new InvalidDataException(script == 1 ? L.T("Diese Schrift enthält keine passenden Kanji oder Kana. Wähle eine japanische Schrift.", "This font contains no supported Kanji or Kana. Choose a Japanese font.")
                    : script == 2 ? L.T("Diese Schrift enthält keine passenden Zeichen für weitere Alphabete, z. B. Griechisch oder Kyrillisch.", "This font contains no supported letters for more alphabets, such as Greek or Cyrillic.")
                    : L.T("Diese Schrift enthält keine passenden lateinischen Buchstaben oder Zahlen.", "This font contains no supported Latin letters or digits."));
            if (script == 1)
            {
                int kana = letters.Count(c => (c >= 0x3040 && c <= 0x30FF) || (c >= 0x31F0 && c <= 0x31FF) || (c >= 0xFF66 && c <= 0xFF9F));
                int kanji = letters.Length - kana;
                return kana + " Kana, " + kanji + " Kanji" + (kana == 0 || kanji == 0 ? L.T(" — unvollständig; fehlende Zeichen bleiben original", " — incomplete; missing characters stay original") : "");
            }
            return letters.Length + L.T(" passende Zeichen in dieser Schrift", " matching characters in this font");
        }
    }

    internal sealed class FontScriptCollection : IDisposable
    {
        internal sealed class Face : IDisposable
        {
            readonly PrivateFontCollection collection = new PrivateFontCollection();
            internal readonly HashSet<int> Coverage;
            internal readonly FontFamily Family;
            internal readonly FontStyle Style;

            internal Face(string path)
            {
                try
                {
                    Coverage = TtfCoverage.Unicode(path);
                    collection.AddFontFile(path);
                    if (collection.Families.Length == 0) throw new InvalidDataException("Cannot load TTF: " + path);
                    Family = collection.Families[0];
                    Style = Family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                }
                catch { collection.Dispose(); throw; }
            }

            public void Dispose()
            {
                if (Family != null) Family.Dispose();
                collection.Dispose();
            }
        }

        readonly FontScriptSources sources;
        readonly Dictionary<string, Face> faces = new Dictionary<string, Face>(StringComparer.OrdinalIgnoreCase);

        internal FontScriptCollection(FontScriptSources sources)
        {
            if (sources == null || String.IsNullOrEmpty(sources.MainPath))
                throw new InvalidOperationException("Choose a main font first.");
            this.sources = sources;
            try
            {
                foreach (string path in sources.Paths) faces.Add(path, new Face(path));
            }
            catch { Dispose(); throw; }
        }

        internal Face ForCharacter(int code) { return faces[sources.ForCharacter(code)]; }
        internal bool Supports(int code) { return ForCharacter(code).Coverage.Contains(code); }

        public void Dispose()
        {
            foreach (var face in faces.Values) face.Dispose();
            faces.Clear();
        }
    }
}
