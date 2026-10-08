using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal sealed class TextureBatchTarget
    {
        internal RaceHudArchive Archive;
        internal string ArchiveName, Resource;
        internal byte[] Original;
    }

    internal sealed class TextureBatchRow
    {
        internal TextureBatchTarget Target;
        internal int Index;
        internal TplTextureInfo Info;
        internal string ImagePath, State, Detail;
        internal bool Changed, Failed;
    }

    internal sealed class TextureBatchPlan
    {
        internal readonly List<TextureBatchRow> Rows = new List<TextureBatchRow>();
        internal readonly Dictionary<TextureBatchTarget, byte[]> Prepared = new Dictionary<TextureBatchTarget, byte[]>();
        internal int ExtraImages;
        internal bool CanApply { get { return Prepared.Count > 0 && !Rows.Any(r => r.Failed); } }
    }

    internal sealed class TextureBatchManifest
    {
        public int Version { get; set; }
        public List<TextureBatchMapping> Images { get; set; }
    }
    internal sealed class TextureBatchMapping
    {
        public string Archive { get; set; }
        public string Resource { get; set; }
        public int Index { get; set; }
        public string File { get; set; }
        public string PngHash { get; set; }
        public string TargetHash { get; set; }
    }

    internal static class TextureBatch
    {
        internal const string ManifestName = "texture-batch.json";
        internal static List<TextureBatchTarget> Capture(RaceHudSession session)
        {
            var targets = new List<TextureBatchTarget>();
            foreach (var archive in session.Archives)
                foreach (var file in archive.Files.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    byte[] data;
                    if (!archive.Generated.TryGetValue(file.Key, out data)) data = file.Value.Data;
                    if (!TplTextureEditor.IsTpl(data)) continue;
                    string picture;
                    if (archive.Pictures.TryGetValue(file.Key, out picture))
                        using (var image = TplTextureEditor.LoadSourceBitmap(picture))
                            data = TplTextureEditor.ReplaceFirstImage(data, image, true);
                    targets.Add(new TextureBatchTarget { Archive = archive, ArchiveName = Path.GetFileName(archive.Source),
                        Resource = file.Key, Original = (byte[])data.Clone() });
                }
            ValidateTargets(targets);
            return targets;
        }

        static string Identity(string archive, string resource, int index)
        {
            return archive + "\n" + resource + "\n" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        static void ValidateTargets(IList<TextureBatchTarget> targets)
        {
            var archives = new Dictionary<string, RaceHudArchive>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in targets)
            {
                if (String.IsNullOrEmpty(t.ArchiveName) || String.IsNullOrEmpty(t.Resource)
                    || t.ArchiveName.IndexOfAny(new[] { '\r', '\n' }) >= 0 || t.Resource.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                    throw new InvalidDataException(L.T("Ungültige Texturkennung.", "Invalid texture identity."));
                RaceHudArchive previous;
                if (archives.TryGetValue(t.ArchiveName, out previous) && previous != t.Archive)
                    throw new InvalidDataException(L.T("Gleichnamige Archive sind nicht eindeutig zuordenbar.", "Archives with the same name cannot be mapped uniquely."));
                archives[t.ArchiveName] = t.Archive;
                if (!keys.Add(Identity(t.ArchiveName, t.Resource, 0)))
                    throw new InvalidDataException(L.T("Mehrdeutige Texturpfade.", "Ambiguous texture paths."));
            }
        }
        internal static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        static string SafeName(string name)
        {
            string clean = new string(name.Select(c => Char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
            if (clean.Length > 64) clean = clean.Substring(0, 64);
            return "t_" + clean + "_" + Hash(Encoding.UTF8.GetBytes(name)).Substring(0, 16);
        }
        internal static string ImageFile(TextureBatchTarget target, int index)
        {
            return SafeName(target.ArchiveName) + "/" + SafeName(target.Resource) + "__" + index + ".png";
        }
        static int Count(byte[] bytes)
        {
            if (!TplTextureEditor.IsTpl(bytes)) throw new InvalidDataException("Invalid TPL.");
            long count = ((long)bytes[4] << 24) | ((long)bytes[5] << 16) | ((long)bytes[6] << 8) | bytes[7];
            if (count < 1 || count > 4096) throw new InvalidDataException("Invalid TPL image count.");
            return (int)count;
        }
        internal static Bitmap Decode(TextureBatchTarget target, byte[] data, int index)
        {
            if (index < 0 || index >= Count(data)) throw new InvalidDataException("Invalid TPL image index.");
            TexturePreviewResult preview; string error;
            if (!TexturePreview.TryDecode(target.Resource, data, index, out preview, out error))
                throw new InvalidDataException(error ?? L.T("Textur nicht lesbar.", "Texture could not be decoded."));
            using (preview) return new Bitmap(preview.Bitmap);
        }
        internal static string Export(IList<TextureBatchTarget> targets, string parent, CancellationToken cancel, Action<int, int> progress)
        {
            ValidateTargets(targets);
            string root = Path.GetFullPath(parent);
            Directory.CreateDirectory(root);
            RejectReparse(root);
            string suffix = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string staging = Path.Combine(root, ".texture-batch-" + suffix);
            string destination = Path.Combine(root, "Texture images " + suffix);
            var created = new List<string>();
            var manifest = new TextureBatchManifest { Version = 1, Images = new List<TextureBatchMapping>() };
            int total = targets.Sum(t => Count(t.Original)), done = 0;
            try
            {
                Directory.CreateDirectory(staging);
                foreach (var t in targets)
                {
                    string targetHash = Hash(t.Original);
                    for (int index = 0; index < Count(t.Original); index++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        string relative = ImageFile(t, index), path = Resolve(staging, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        created.Add(path);
                        using (var bitmap = Decode(t, t.Original, index)) bitmap.Save(path, ImageFormat.Png);
                        manifest.Images.Add(new TextureBatchMapping { Archive = t.ArchiveName, Resource = t.Resource, Index = index,
                            File = relative, PngHash = Hash(File.ReadAllBytes(path)), TargetHash = targetHash });
                        if (progress != null) progress(++done, total);
                    }
                }
                cancel.ThrowIfCancellationRequested();
                string manifestPath = Path.Combine(staging, ManifestName);
                File.WriteAllText(manifestPath, Serializer().Serialize(manifest), new UTF8Encoding(false));
                created.Add(manifestPath);
                cancel.ThrowIfCancellationRequested();
                Directory.Move(staging, destination);
                return destination;
            }
            catch
            {
                // Nur die selbst erzeugten Dateien und danach leere Ordner entfernen.
                foreach (var file in created) if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(staging))
                {
                    foreach (var directory in created.Select(Path.GetDirectoryName).Distinct().OrderByDescending(p => p.Length))
                        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                    if (!Directory.EnumerateFileSystemEntries(staging).Any()) Directory.Delete(staging);
                }
                throw;
            }
        }
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024, RecursionLimit = 16 }; }
        internal static string Resolve(string root, string relative)
        {
            if (String.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":") || relative.Contains("\\"))
                throw new InvalidDataException(L.T("Ungültiger Bildpfad im Zuordnungsplan.", "Invalid image path in the mapping plan."));
            string[] parts = relative.Split('/');
            if (parts.Any(p => p.Length == 0 || p == "." || p == ".." || p.EndsWith(".") || p.EndsWith(" ") || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new InvalidDataException(L.T("Unsicherer Bildpfad im Zuordnungsplan.", "Unsafe image path in the mapping plan."));
            foreach (string part in parts)
            {
                string stem = part.Split('.')[0].ToUpperInvariant();
                if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
                    throw new InvalidDataException(L.T("Reservierter Gerätename im Bildpfad.", "Reserved device name in the image path."));
            }
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Image path leaves the import folder.");
            RejectReparse(full);
            return full;
        }
        static void RejectReparse(string path)
        {
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(L.T("Verknüpfte Ordner/Bilder werden nicht importiert. Verwende einen normalen Kopieordner.", "Linked folders/images cannot be imported. Use a regular copy folder."));
        }
        static Dictionary<string, TextureBatchMapping> ReadMapping(string root, IList<TextureBatchTarget> targets)
        {
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in targets) for (int i = 0; i < Count(t.Original); i++) expected.Add(Identity(t.ArchiveName, t.Resource, i));
            string path = Resolve(root, ManifestName);
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Texture mapping plan is too large.");
            var manifest = Serializer().Deserialize<TextureBatchManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.Version != 1 || manifest.Images == null || manifest.Images.Count > 65536)
                throw new InvalidDataException(L.T("Ungültiger Textur-Zuordnungsplan.", "Invalid texture mapping plan."));
            var mapping = new Dictionary<string, TextureBatchMapping>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in manifest.Images)
            {
                if (row == null || row.Index < 0 || String.IsNullOrEmpty(row.Archive) || String.IsNullOrEmpty(row.Resource))
                    throw new InvalidDataException("Invalid texture mapping entry.");
                string key = Identity(row.Archive, row.Resource, row.Index);
                if (!expected.Contains(key) || mapping.ContainsKey(key) || !paths.Add(Resolve(root, row.File))
                    || !String.Equals(Path.GetExtension(row.File), ".png", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("Unbekannte oder doppelte Texturzuordnung.", "Unknown or duplicate texture mapping."));
                mapping.Add(key, row);
            }
            return mapping;
        }
        internal static TextureBatchPlan Prepare(IList<TextureBatchTarget> targets, string folder, bool resize,
            CancellationToken cancel, Action<int, int> progress)
        {
            ValidateTargets(targets);
            string root = Path.GetFullPath(folder);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(L.T("Der Bildordner fehlt.", "The image folder is missing."));
            RejectReparse(root);
            var mapping = ReadMapping(root, targets);
            var plan = new TextureBatchPlan();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = targets.Sum(t => Count(t.Original)), done = 0;
            foreach (var t in targets)
            {
                byte[] prepared = t.Original;
                string targetHash = Hash(t.Original);
                for (int index = 0; index < Count(t.Original); index++)
                {
                    cancel.ThrowIfCancellationRequested();
                    var row = new TextureBatchRow { Target = t, Index = index };
                    plan.Rows.Add(row);
                    try
                    {
                        TextureBatchMapping entry = null;
                        if (mapping != null) mapping.TryGetValue(Identity(t.ArchiveName, t.Resource, index), out entry);
                        row.ImagePath = Resolve(root, entry == null ? ImageFile(t, index) : entry.File);
                        used.Add(row.ImagePath);
                        try { row.Info = TplTextureEditor.GetImageInfo(t.Original, index); }
                        catch (NotSupportedException)
                        {
                            int header = Read32(t.Original, checked(Read32(t.Original, 8) + index * 8));
                            row.Info = new TplTextureInfo { Width = (t.Original[header + 2] << 8) | t.Original[header + 3],
                                Height = (t.Original[header] << 8) | t.Original[header + 1], Format = Read32(t.Original, header + 4), MaxLod = t.Original[header + 0x22] };
                        }
                        if (mapping != null && entry == null || !File.Exists(row.ImagePath))
                        {
                            row.State = L.T("Fehlt – übersprungen", "Missing – skipped");
                            continue;
                        }
                        if (entry != null && !String.Equals(entry.TargetHash, targetHash, StringComparison.Ordinal))
                            throw new InvalidDataException(L.T("Ziel seit PNG-Export verändert. Neu exportieren, damit aktuelle Änderungen erhalten bleiben.", "Target changed since PNG export. Export again to preserve current edits."));
                        byte[] png = File.ReadAllBytes(row.ImagePath);
                        if (entry != null && String.Equals(entry.PngHash, Hash(png), StringComparison.Ordinal))
                        {
                            row.State = L.T("Unverändert", "Unchanged"); continue;
                        }
                        using (var source = TplTextureEditor.LoadSourceBitmap(row.ImagePath))
                        using (var before = Decode(t, prepared, index))
                        {
                            if (SamePixels(source, before)) { row.State = L.T("Unverändert", "Unchanged"); continue; }
                            if (!TplTextureEditor.CanReplaceImage(t.Original, index)) throw new NotSupportedException(L.T("Dieses Zielformat kann nur exportiert werden: ", "This target format supports export only: ") + row.Info.FormatName);
                            row.Detail = source.Width + " × " + source.Height + " → " + row.Info.Width + " × " + row.Info.Height;
                            prepared = TplTextureEditor.ReplaceImage(DetachSharedPayload(prepared, index), source, resize, index);
                        }
                        using (var decoded = Decode(t, prepared, index)) { }
                        row.Changed = true;
                        row.State = L.T("Änderung bereit", "Change ready");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        row.Failed = true; row.State = L.T("Fehler", "Error"); row.Detail = ex.Message;
                    }
                    finally { if (progress != null) progress(++done, total); }
                }
                if (!prepared.SequenceEqual(t.Original)) plan.Prepared.Add(t, prepared);
            }
            cancel.ThrowIfCancellationRequested();
            plan.ExtraImages = CountExtraImages(root, used, new HashSet<string>(StringComparer.OrdinalIgnoreCase), cancel);
            return plan;
        }
        static int CountExtraImages(string directory, HashSet<string> used, HashSet<string> seen, CancellationToken cancel)
        {
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                cancel.ThrowIfCancellationRequested();
                if (!String.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(file)) throw new InvalidDataException(L.T("Bildpfade unterscheiden sich nur in Groß-/Kleinschreibung. Die Zuordnung ist mehrdeutig.", "Image paths differ only by letter case. The mapping is ambiguous."));
                if (!used.Contains(file)) count++;
            }
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                cancel.ThrowIfCancellationRequested();
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) count += CountExtraImages(child, used, seen, cancel);
            }
            return count;
        }
        static int Read32(byte[] data, int offset)
        {
            if (offset < 0 || (long)offset + 4 > data.Length) throw new InvalidDataException("Invalid TPL offset.");
            return checked((int)(((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3]));
        }
        static void Write32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value >> 24); data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8); data[offset + 3] = (byte)value;
        }
        static byte[] DetachSharedPayload(byte[] data, int index)
        {
            var selected = TplTextureEditor.GetImageInfo(data, index);
            if (selected.Format == 8 || selected.Format == 9) return data;
            int length = 0;
            for (int level = 0; level <= selected.MaxLod; level++)
                length = checked(length + TplTextureEditor.GetBaseLevelPayloadLength(Math.Max(1, selected.Width >> level), Math.Max(1, selected.Height >> level), selected.Format));
            int table = Read32(data, 8), count = Count(data);
            bool shared = selected.DataOffset < (long)table + count * 8
                || selected.DataOffset < (long)selected.ImageHeaderOffset + 36 && selected.ImageHeaderOffset < (long)selected.DataOffset + length;
            for (int other = 0; other < count; other++)
            {
                if (other == index) continue;
                int header = Read32(data, checked(table + other * 8));
                int start = Read32(data, header + 8);
                // Gemeinsame Header oder Daten vor dem nächsten Bild dürfen nicht mitbearbeitet werden.
                if (header == selected.ImageHeaderOffset || start >= selected.DataOffset && start < (long)selected.DataOffset + length)
                    shared = true;
                if (selected.DataOffset < (long)header + 36 && header < (long)selected.DataOffset + length) shared = true;
                int palette = Read32(data, checked(table + other * 8 + 4));
                if (palette > 0)
                {
                    if ((long)palette + 12 > data.Length) throw new InvalidDataException("Invalid TPL palette header.");
                    int paletteData = Read32(data, palette + 8), paletteLength = ((data[palette] << 8) | data[palette + 1]) * 2;
                    if (selected.DataOffset < (long)palette + 12 && palette < (long)selected.DataOffset + length
                        || selected.DataOffset < (long)paletteData + paletteLength && paletteData < (long)selected.DataOffset + length) shared = true;
                }
                else
                {
                    try
                    {
                        var info = TplTextureEditor.GetImageInfo(data, other);
                        long otherLength = 0;
                        for (int mip = 0; mip <= info.MaxLod; mip++) otherLength += TplTextureEditor.GetBaseLevelPayloadLength(Math.Max(1, info.Width >> mip), Math.Max(1, info.Height >> mip), info.Format);
                        if (selected.DataOffset >= start && selected.DataOffset < start + otherLength) shared = true;
                    }
                    catch (NotSupportedException) { shared = true; }
                }
            }
            if (!shared) return data;
            int newHeader = checked((data.Length + 31) & ~31), newData = checked((newHeader + 36 + 31) & ~31);
            var copy = new byte[checked(newData + length)];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            Buffer.BlockCopy(data, selected.ImageHeaderOffset, copy, newHeader, 36);
            Buffer.BlockCopy(data, selected.DataOffset, copy, newData, length);
            Write32(copy, table + index * 8, newHeader); Write32(copy, newHeader + 8, newData);
            return copy;
        }
        static bool SamePixels(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            BitmapData left = null, right = null;
            try
            {
                var area = new Rectangle(0, 0, a.Width, a.Height);
                left = a.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                right = b.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var first = new byte[checked(a.Width * 4)]; var second = new byte[first.Length];
                for (int y = 0; y < a.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(left.Scan0, checked(y * left.Stride)), first, 0, first.Length);
                    Marshal.Copy(IntPtr.Add(right.Scan0, checked(y * right.Stride)), second, 0, second.Length);
                    if (!first.SequenceEqual(second)) return false;
                }
                return true;
            }
            finally
            {
                if (left != null) a.UnlockBits(left);
                if (right != null) b.UnlockBits(right);
            }
        }
        internal static void Apply(RaceHudSession session, TextureBatchPlan plan)
        {
            if (plan == null || !plan.CanApply) throw new InvalidOperationException(L.T("Kein fehlerfreier Importplan vorhanden.", "No valid import plan is available."));
            var current = Capture(session);
            foreach (var target in plan.Rows.Select(r => r.Target).Distinct())
                if (!current.Any(t => t.Archive == target.Archive && t.Resource == target.Resource && t.Original.SequenceEqual(target.Original)))
                    throw new InvalidOperationException(L.T("Die Texturen wurden inzwischen geändert. Vorschau erneut erstellen.", "The textures changed meanwhile. Prepare the preview again."));
            object[] previous = session.CaptureEdits();
            try
            {
                foreach (var item in plan.Prepared)
                {
                    item.Key.Archive.Generated[item.Key.Resource] = item.Value;
                    item.Key.Archive.Pictures.Remove(item.Key.Resource);
                }
            }
            catch { session.RestoreEdits(previous); throw; }
        }
    }
}
