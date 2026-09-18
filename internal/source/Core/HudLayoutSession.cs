using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using murumsWiiModStudio.Brlan;

namespace murumsWiiModStudio
{
    internal sealed class HudLayoutResource
    {
        public StudioArchiveCopy Archive;
        public string Key;
        public byte[] Bytes
        {
            get
            {
                return Archive.Files[Key].Data;
            }

            set
            {
                Archive.Files[Key].Data = value;
            }
        }

        public override string ToString()
        {
            return Path.GetFileName(Key) + "  [" + Path.GetFileName(Archive.Source) + " / " + Path.GetDirectoryName(Key) + "]";
        }
    }

    internal sealed class HudLayoutSession
    {
        internal sealed class Edit
        {
            public HudLayoutResource Resource;
            public byte[] Before, After;
        }

        readonly Stack<Edit> undo = new Stack<Edit>(), redo = new Stack<Edit>();
        readonly Dictionary<StudioArchiveCopy, Dictionary<string, byte[]>> original = new Dictionary<StudioArchiveCopy, Dictionary<string, byte[]>>();
        public readonly List<StudioArchiveCopy> Archives = new List<StudioArchiveCopy>();
        public bool CanUndo
        {
            get
            {
                return undo.Count > 0;
            }
        }

        public bool CanRedo
        {
            get
            {
                return redo.Count > 0;
            }
        }

        public void Clear()
        {
            Archives.Clear();
            original.Clear();
            undo.Clear();
            redo.Clear();
        }

        public void Add(StudioArchiveCopy a)
        {
            if (Archives.Any(x => string.Equals(Path.GetFileName(x.Source), Path.GetFileName(a.Source), StringComparison.OrdinalIgnoreCase)))
                throw new IOException("An archive with this name is already loaded.");
            Archives.Add(a);
            original[a] = a.Files.ToDictionary(x => x.Key, x => (byte[])x.Value.Data.Clone());
        }

        public IEnumerable<HudLayoutResource> Layouts
        {
            get
            {
                return Archives.SelectMany(a => a.Files.Keys.Where(k => k.EndsWith(".brlyt", StringComparison.OrdinalIgnoreCase)).Select(k => new HudLayoutResource { Archive = a, Key = k }));
            }
        }

        public IEnumerable<HudLayoutResource> Changes
        {
            get
            {
                return Archives.SelectMany(a => a.Files.Keys.Where(k => !a.Files[k].Data.SequenceEqual(original[a][k])).Select(k => new HudLayoutResource { Archive = a, Key = k }));
            }
        }

        public void Set(HudLayoutResource r, byte[] bytes)
        {
            if (r.Bytes.SequenceEqual(bytes))
                return;
            undo.Push(new Edit { Resource = r, Before = (byte[])r.Bytes.Clone(), After = (byte[])bytes.Clone() });
            r.Bytes = (byte[])bytes.Clone();
            redo.Clear();
        }

        public HudLayoutResource Undo()
        {
            if (!CanUndo)
                return null;
            var e = undo.Pop();
            e.Resource.Bytes = (byte[])e.Before.Clone();
            redo.Push(e);
            return e.Resource;
        }

        public HudLayoutResource Redo()
        {
            if (!CanRedo)
                return null;
            var e = redo.Pop();
            e.Resource.Bytes = (byte[])e.After.Clone();
            undo.Push(e);
            return e.Resource;
        }

        public byte[] Opened(HudLayoutResource r)
        {
            return (byte[])original[r.Archive][r.Key].Clone();
        }

        public void Restore(HudLayoutResource r)
        {
            Set(r, original[r.Archive][r.Key]);
        }

        public HudLayoutResource Texture(HudLayoutResource layout, string name)
        {
            string local = Path.GetDirectoryName(layout.Key).Replace('\\', '/');
            int slash = local.LastIndexOf('/');
            string expected = (slash < 0 ? "" : local.Substring(0, slash) + "/") + "timg/" + name;
            string key = layout.Archive.Files.Keys.FirstOrDefault(k => string.Equals(k, expected, StringComparison.OrdinalIgnoreCase));
            if (key != null)
                return new HudLayoutResource
                {
                    Archive = layout.Archive,
                    Key = key
                };
            var matches = layout.Archive.Files.Keys.Where(k => string.Equals(Path.GetFileName(k), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1)
                return new HudLayoutResource
                {
                    Archive = layout.Archive,
                    Key = matches[0]
                };
            var all = Archives.SelectMany(a => a.Files.Keys.Where(k => string.Equals(Path.GetFileName(k), name, StringComparison.OrdinalIgnoreCase)).Select(k => new HudLayoutResource { Archive = a, Key = k })).ToList();
            return all.Count == 1 ? all[0] : null;
        }

        public void Save(string folder)
        {
            var changes = Changes.ToList();
            if (changes.Count == 0)
                throw new InvalidOperationException("No changes to save.");
            var builds = new Dictionary<string, byte[]>();
            foreach (var a in changes.Select(r => r.Archive).Distinct())
            {
                string dest = Path.GetFullPath(Path.Combine(folder, Path.GetFileName(a.Source)));
                if (Archives.Any(x => string.Equals(x.Source, dest, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("Choose a separate output folder.");
                builds.Add(dest, a.Build());
            }

            Directory.CreateDirectory(folder);
            foreach (var b in builds)
                BackupManager.WriteAllBytesSafely(b.Key, b.Value);
        }
    }

    internal static class HudLayoutGeometry
    {
        // Wii NW4R basePosition is an index in a 3 x 3 grid (not the later BFLYT bit field).
        public static PointF[] Corners(BrlytPaneInfo p)
        {
            float left = -(p.Origin % 3) * p.Width / 2, top = (p.Origin / 3) * p.Height / 2;
            var pts = new[]
            {
                new PointF(left, top),
                new PointF(left + p.Width, top),
                new PointF(left + p.Width, top - p.Height),
                new PointF(left, top - p.Height)
            };
            using (var m = World(p))
                m.TransformPoints(pts);
            return pts;
        }

        public static Matrix World(BrlytPaneInfo p)
        {
            var m = new Matrix();
            for (var q = p; q != null; q = q.Parent)
            {
                m.Scale(q.ScaleX, q.ScaleY, MatrixOrder.Append);
                m.Rotate(q.RotZ, MatrixOrder.Append);
                m.Translate(q.X, q.Y, MatrixOrder.Append);
            }

            return m;
        }

        public static PointF LocalDelta(BrlytPaneInfo p, float x, float y)
        {
            using (var m = World(p.Parent))
            {
                if (!m.IsInvertible)
                    throw new InvalidOperationException("The parent has zero scale; movement is unavailable.");
                m.Invert();
                var pts = new[]
                {
                    new PointF(x, y)
                };
                m.TransformVectors(pts);
                return pts[0];
            }
        }

        public static bool Visible(BrlytPaneInfo p)
        {
            for (var q = p; q != null; q = q.Parent)
                if (!q.Visible)
                    return false;
            return true;
        }

        public static float Alpha(BrlytPaneInfo p)
        {
            float a = p.Alpha / 255f;
            for (var q = p.Parent; q != null; q = q.Parent)
                if ((q.Flags & 2) != 0)
                    a *= q.Alpha / 255f;
            return a;
        }

        public static void Tint(BrlytDocument d, BrlytPaneInfo p, Color c)
        {
            int start, count;
            if (p.Magic == "pic1" && p.Size >= 0x60)
            {
                start = 0x4c;
                count = 4;
            }
            else if (p.Magic == "txt1" && p.Size >= 0x74)
            {
                start = 0x5c;
                count = 2;
            }
            else
                throw new InvalidOperationException("Select a picture or text element for tint.");
            for (int i = 0; i < count; i++)
            {
                int at = p.Offset + start + i * 4;
                d.Data[at] = c.R;
                d.Data[at + 1] = c.G;
                d.Data[at + 2] = c.B;
                d.Data[at + 3] = c.A;
            }
        }
    }
}
