using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal static class ArchiveSemanticDiff
    {
        sealed class Report
        {
            internal readonly List<string> Lines = new List<string>();
            internal int Changes;
            bool truncated;
            internal void Change(string name, object before, object after)
            {
                string a = Value(before), b = Value(after);
                if (a == b) return;
                Changes++;
                if (Lines.Count < 250) Lines.Add(name + ": " + Short(a) + " → " + Short(b));
                else truncated = true;
            }
            internal void Note(string text) { if (Lines.Count < 250) Lines.Add(text); else truncated = true; }
            internal string Finish()
            {
                string heading = Changes + L.T(" erkannte Wertänderungen", " recognised value changes");
                if (truncated) heading += L.T(" (Anzeige gekürzt)", " (display shortened)");
                return heading + "\n\n" + String.Join("\n", Lines) + "\n\n" + L.T(
                    "Die Dateien unterscheiden sich auch binär. Diese Übersicht erklärt die unterstützten Werte; weitere Daten können geändert sein. Übernommen wird weiterhin die ganze ausgewählte Ressource.",
                    "The files also differ as binary data. This summary explains supported values; other data may have changed. The entire selected resource will still be copied.");
            }
        }

        static string Value(object value)
        {
            if (value == null) return "—";
            if (value is float) return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            if (value is bool) return (bool)value ? L.T("Ja", "Yes") : L.T("Nein", "No");
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        static string Short(string text)
        {
            text = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            return text.Length > 240 ? text.Substring(0, 240) + "…" : text;
        }

        internal static string Describe(string path, byte[] before, byte[] after)
        {
            if (before == null || after == null) return L.T("Eine Quelle fehlt.", "A source is missing.");
            if (before.SequenceEqual(after)) return L.T("Keine Änderungen in dieser Ressource.", "No changes in this resource.");
            string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
            if (extension != ".brlyt" && extension != ".brlan" && extension != ".tpl")
                return L.T("Geänderte Dateidaten. Für dieses Format gibt es noch keinen Vergleich einzelner Werte.",
                    "File data changed. Individual values are not compared for this format yet.");
            if (before.Length > 16 * 1024 * 1024 || after.Length > 16 * 1024 * 1024)
                return L.T("Große Ressource: Die Vorschau der Wertänderungen ist auf 16 MB je Datei begrenzt.",
                    "Large resource: the value comparison is limited to 16 MB per file.");
            try
            {
                var report = new Report();
                if (extension == ".brlyt") Layout(report, BrlytDocument.FromBytes(before), BrlytDocument.FromBytes(after));
                else if (extension == ".brlan") Animation(report, BrlanCodec.Parse(before), BrlanCodec.Parse(after));
                else Textures(report, before, after);
                return report.Finish();
            }
            catch (Exception error)
            {
                if (!(error is InvalidDataException || error is ArgumentException || error is OverflowException || error is IndexOutOfRangeException || error is NotSupportedException)) throw;
                return L.T("Werte konnten nicht sicher gelesen werden: ", "Values could not be read safely: ") + error.Message + "\n" +
                    L.T("Die Binäränderung bleibt sichtbar; es wurden keine Dateien verändert.", "The binary change remains visible; no files were changed.");
            }
        }

        static int TextureCount(byte[] data)
        {
            if (!TplTextureEditor.IsTpl(data)) throw new InvalidDataException(L.T("Ungültige TPL.", "Invalid TPL."));
            long count = ((long)data[4] << 24) | ((long)data[5] << 16) | ((long)data[6] << 8) | data[7];
            if (count < 1 || count > 4096) throw new InvalidDataException(L.T("Ungültige TPL-Bildanzahl.", "Invalid TPL image count."));
            return (int)count;
        }
        static void Textures(Report report, byte[] before, byte[] after)
        {
            int a = TextureCount(before), b = TextureCount(after);
            report.Change(L.T("Bildanzahl", "Image count"), a, b);
            for (int i = 0; i < Math.Max(a, b); i++)
            {
                string label = L.T("Bild ", "Image ") + (i + 1);
                var x = i < a ? TplTextureEditor.GetImageInfo(before, i) : null;
                var y = i < b ? TplTextureEditor.GetImageInfo(after, i) : null;
                report.Change(label + " / " + L.T("Breite", "Width"), x == null ? null : (object)x.Width, y == null ? null : (object)y.Width);
                report.Change(label + " / " + L.T("Höhe", "Height"), x == null ? null : (object)x.Height, y == null ? null : (object)y.Height);
                report.Change(label + " / Format", x == null ? null : x.FormatName, y == null ? null : y.FormatName);
                report.Change(label + " / " + L.T("Mipmap-Stufen", "Mipmap levels"), x == null ? null : (object)x.MaxLod, y == null ? null : (object)y.MaxLod);
            }
            report.Note(L.T("Bildinhalt und Palette bitte in den beiden Vorschauen vergleichen.", "Compare image content and palette in the two previews."));
        }

        static Dictionary<string, T> Entries<T>(IEnumerable<T> source, Func<T, string> name)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T entry in source)
            {
                string label = name(entry) ?? "";
                int count; counts.TryGetValue(label, out count); counts[label] = count + 1;
                result.Add(label + "\0" + count, entry);
            }
            return result;
        }
        static void Collection<T>(Report report, string prefix, IEnumerable<T> before, IEnumerable<T> after,
            Func<T, string> name, Action<string, T, T> compare)
        {
            var a = Entries(before, name); var b = Entries(after, name);
            if (a.Keys.Concat(b.Keys).Any(k => !k.EndsWith("\0" + "0", StringComparison.Ordinal)))
                report.Note(prefix + L.T(": Gleichnamige Einträge werden in Dateireihenfolge verglichen.", ": Repeated names are compared in file order."));
            foreach (string key in a.Keys.Concat(b.Keys).Distinct(StringComparer.Ordinal))
            {
                int separator = key.LastIndexOf('\0');
                string label = prefix + " / " + Short(key.Substring(0, separator)) + " [" +
                    (Int32.Parse(key.Substring(separator + 1), CultureInfo.InvariantCulture) + 1) + "]";
                if (!a.ContainsKey(key)) report.Change(label, null, L.T("Hinzugefügt", "Added"));
                else if (!b.ContainsKey(key)) report.Change(label, L.T("Vorhanden", "Present"), null);
                else compare(label, a[key], b[key]);
            }
        }
        static void Indexed(Report report, string prefix, IList<string> before, IList<string> after)
        {
            for (int i = 0; i < Math.Max(before.Count, after.Count); i++)
                report.Change(prefix + " [" + i + "]", i < before.Count ? before[i] : null, i < after.Count ? after[i] : null);
        }
        static void Layout(Report r, BrlytDocument a, BrlytDocument b)
        {
            r.Change(L.T("Menübreite", "Layout width"), a.LayoutWidth, b.LayoutWidth);
            r.Change(L.T("Menühöhe", "Layout height"), a.LayoutHeight, b.LayoutHeight);
            Indexed(r, L.T("Textur", "Texture"), a.Textures, b.Textures);
            Indexed(r, L.T("Schrift", "Font"), a.Fonts, b.Fonts);
            Collection(r, L.T("Material", "Material"), a.Materials, b.Materials, m => m.Name, (label, x, y) => {
                Indexed(r, label + " / " + L.T("Texturbindung", "Texture binding"),
                    x.Bindings.Select(t => t.Slot + ": " + t.TextureId + " / " + t.TextureName).ToArray(),
                    y.Bindings.Select(t => t.Slot + ": " + t.TextureId + " / " + t.TextureName).ToArray());
            });
            Collection(r, L.T("Element", "Element"), a.Panes, b.Panes, p => p.Name, (label, x, y) => {
                r.Change(label + " / " + L.T("Typ", "Type"), x.Magic, y.Magic);
                r.Change(label + " / " + L.T("Elternelement", "Parent element"), x.Parent == null ? null : x.Parent.Name, y.Parent == null ? null : y.Parent.Name);
                r.Change(label + " / " + L.T("Sichtbar", "Visible"), x.Visible, y.Visible);
                r.Change(label + " / " + L.T("Deckkraft", "Opacity"), x.Alpha, y.Alpha);
                r.Change(label + " / " + L.T("Ausrichtung", "Origin"), x.Origin, y.Origin);
                string[] names = { "X", "Y", "Z", L.T("Drehung X", "Rotation X"), L.T("Drehung Y", "Rotation Y"), L.T("Drehung Z", "Rotation Z"),
                    L.T("Skalierung X", "Scale X"), L.T("Skalierung Y", "Scale Y"), L.T("Breite", "Width"), L.T("Höhe", "Height") };
                float[] av = { x.X, x.Y, x.Z, x.RotX, x.RotY, x.RotZ, x.ScaleX, x.ScaleY, x.Width, x.Height };
                float[] bv = { y.X, y.Y, y.Z, y.RotX, y.RotY, y.RotZ, y.ScaleX, y.ScaleY, y.Width, y.Height };
                for (int i = 0; i < names.Length; i++) r.Change(label + " / " + names[i], av[i], bv[i]);
                r.Change(label + " / Material", Material(a, x), Material(b, y));
                Indexed(r, label + " / " + L.T("Farbe RGBA", "RGBA colour"), Colours(a, x), Colours(b, y));
                if (x.Magic == "txt1" || y.Magic == "txt1")
                {
                    r.Change(label + " / Text", x.EmbeddedText, y.EmbeddedText);
                    r.Change(label + " / " + L.T("Schrift", "Font"), Font(a, x), Font(b, y));
                    r.Change(label + " / " + L.T("Schriftbreite", "Font width"), x.FontWidth, y.FontWidth);
                    r.Change(label + " / " + L.T("Schrifthöhe", "Font height"), x.FontHeight, y.FontHeight);
                    r.Change(label + " / " + L.T("Zeichenabstand", "Character spacing"), x.CharSize, y.CharSize);
                    r.Change(label + " / " + L.T("Zeilenabstand", "Line spacing"), x.LineSize, y.LineSize);
                }
            });
        }
        static string Material(BrlytDocument d, BrlytPaneInfo p)
        { return p.MaterialId + ": " + (d.MaterialForPane(p) == null ? "—" : d.MaterialForPane(p).Name); }
        static string Font(BrlytDocument d, BrlytPaneInfo p)
        { return p.FontId + ": " + (p.FontId >= 0 && p.FontId < d.Fonts.Count ? d.Fonts[p.FontId] : "—"); }
        static string[] Colours(BrlytDocument d, BrlytPaneInfo p)
        {
            int offset = p.Magic == "pic1" && p.Size >= 0x60 ? 0x4c : p.Magic == "txt1" && p.Size >= 0x74 ? 0x5c : -1;
            int count = offset < 0 ? 0 : p.Magic == "pic1" ? 4 : 2;
            return Enumerable.Range(0, count).Select(i => "#" + String.Concat(d.Data.Skip(p.Offset + offset + i * 4).Take(4).Select(v => v.ToString("X2")))).ToArray();
        }
        static void Animation(Report r, BrlanDocument a, BrlanDocument b)
        {
            if (a.Pai == null || b.Pai == null)
            { r.Note(L.T("Kein gemeinsam lesbarer Animationsabschnitt.", "No animation section could be read in both files.")); return; }
            r.Change(L.T("Animationsdauer (Frames)", "Animation length (frames)"), a.Pai.Frames, b.Pai.Frames);
            r.Change(L.T("Wiederholung / Flags", "Loop / flags"), a.Pai.Flags, b.Pai.Flags);
            Indexed(r, L.T("Animationstextur", "Animation texture"), a.Pai.Textures, b.Pai.Textures);
            Collection(r, L.T("Animation", "Animation"), a.Pai.Animations, b.Pai.Animations, n => n.Name + " / " + BrlanNames.AnimationTargetName(n.TargetKind), (label, x, y) => {
                Collection(r, label, x.Tags, y.Tags, t => t.Magic, (tag, tx, ty) => {
                    if (tx.RawOnly || ty.RawOnly)
                    { r.Note(tag + L.T(": Unbekannte Animationsdaten; nur Binärvergleich.", ": Unknown animation data; binary comparison only.")); return; }
                    Collection(r, tag, tx.Entries, ty.Entries, e => e.Index + " / " + BrlanNames.TargetName(tx.Magic, e.Target), (entry, ex, ey) => {
                        r.Change(entry + " / " + L.T("Interpolation", "Interpolation"), ex.KeyType, ey.KeyType);
                        Collection(r, entry, ex.Keys, ey.Keys, k => L.T("Frame ", "Frame ") + Value(k.Frame), (key, kx, ky) => {
                            r.Change(key + " / " + L.T("Wert", "Value"), ex.KeyType == 1 ? (object)kx.UIntValue : kx.FloatValue, ey.KeyType == 1 ? (object)ky.UIntValue : ky.FloatValue);
                            r.Change(key + " / " + L.T("Steigung", "Slope"), kx.Blend, ky.Blend);
                        });
                    });
                });
            });
        }
    }
}
