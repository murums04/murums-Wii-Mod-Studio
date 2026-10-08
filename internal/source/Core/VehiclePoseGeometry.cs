using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace murumsWiiModStudio
{
    internal sealed class VehiclePoseGeometry
    {
        internal CharacterModelImport Model;
        internal float[][] Handle;
        internal float[] MenuOffset;
        internal VehiclePoseGeometry ForMenu()
        {
            if (MenuOffset == null) return this;
            var copy = new CharacterModelImport { Rig = Model.Rig };
            copy.Points.AddRange(Model.Points.Select(p => RigVector.Add(p, MenuOffset)));
            copy.Faces.AddRange(Model.Faces);
            copy.FaceColors.AddRange(Model.FaceColors);
            return new VehiclePoseGeometry { Model = copy,
                Handle = Handle == null ? null : Handle.Select(p => RigVector.Add(p, MenuOffset)).ToArray() };
        }
        static float[] DriverOffset(string root, CharacterDefinition character, string key)
        {
            string path = Path.Combine(root, "UI", "Common.szs");
            var archive = new StudioArchiveCopy(path);
            var data = archive.Files.Single(p => Path.GetFileName(p.Key) == "kartDriverDispParam.bin").Value.Data;
            Func<int, int> integer = offset => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
            int vehicles = integer(0), characters = integer(4);
            int type = Array.IndexOf(new[] { "df", "a", "b", "c", "d", "e" }, key.Split('_')[0].Substring(1));
            int vehicle = "sml".IndexOf(key[0]) + type * 3 + (key.EndsWith("_bike") ? 18 : 0);
            if (vehicles != 36 || characters != 48 || character.Id >= characters || vehicle < 0 || vehicle >= vehicles
                || data.Length != 8 + vehicles * characters * 56)
                throw new InvalidDataException("Invalid vehicle seating parameters: " + path);
            int entry = 8 + (vehicle * characters + character.Id) * 56;
            Func<int, float> number = offset => BitConverter.ToSingle(data.Skip(offset).Take(4).Reverse().ToArray(), 0);
            var result = new[] { 0f, number(entry), number(entry + 4) };
            if (result.Any(v => Single.IsNaN(v) || Single.IsInfinity(v))) throw new InvalidDataException("Invalid vehicle seating position.");
            return result;
        }
        static float[] BodyOffset(float[][] body, List<float[]> menu)
        {
            var distinct = body.GroupBy(PointKey).Select(g => g.First()).ToArray();
            var samples = Enumerable.Range(0, Math.Min(60, distinct.Length))
                .Select(i => distinct[i * distinct.Length / Math.Min(60, distinct.Length)]).ToArray();
            var offsets = new Dictionary<string, Tuple<float[], int>>();
            foreach (var a in samples)
            foreach (var b in menu.Where(p => Math.Abs(p[0] - a[0]) < .02))
            {
                var delta = RigVector.Sub(b, a);
                string key = PointKey(delta);
                Tuple<float[], int> current;
                offsets.TryGetValue(key, out current);
                offsets[key] = Tuple.Create(delta, current == null ? 1 : current.Item2 + 1);
            }
            foreach (var candidate in offsets.Values.OrderByDescending(o => o.Item2).Take(8))
                if (samples.Count(a => menu.Any(b => RigVector.Length(RigVector.Sub(b, RigVector.Add(a, candidate.Item1))) < .05)) >= Math.Max(12, samples.Length * .4))
                    return candidate.Item1;
            // Vereinfachte Menuekarosserien behalten verteilte Anker trotz anderer Tessellierung.
            var anchors = offsets.Values.OrderByDescending(o => o.Item2).Take(8).Select(candidate => new {
                Offset = candidate.Item1,
                Points = distinct.Where(a => menu.Any(b => RigVector.Length(RigVector.Sub(b, RigVector.Add(a, candidate.Item1))) < .05)).ToArray()
            }).OrderByDescending(candidate => candidate.Points.Length).ToArray();
            if (anchors.Length > 0)
            {
                var best = anchors[0];
                bool distributed = best.Points.Length >= Math.Max(12, distinct.Length * .2)
                    && Enumerable.Range(0, 3).All(axis => {
                        double span = distinct.Max(p => p[axis]) - distinct.Min(p => p[axis]);
                        return span > .1 && best.Points.Max(p => p[axis]) - best.Points.Min(p => p[axis]) >= span * .75;
                    });
                if (distributed && (anchors.Length == 1 || best.Points.Length >= anchors[1].Points.Length * 2)) return best.Offset;
            }
            throw new InvalidDataException("The race vehicle and menu vehicle do not share matching body geometry.");
        }
        static string PointKey(float[] point)
        {
            return String.Join(",", point.Select(v => Math.Round(v * 10).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        internal static VehiclePoseGeometry Load(string root, string code, int slot, string key, IEnumerable<CharacterAsset> assets, CancellationToken token)
        {
            var current = (assets ?? new CharacterAsset[0]).ToArray();
            var character = CharacterDefinition.All.Single(c => c.Code == code);
            string relative = "Character/AllKart/" + code + "-" + slot + "-allkart.szs";
            var menu = current.FirstOrDefault(a => a.Target == relative);
            if (menu == null && File.Exists(Path.Combine(root, relative)))
                menu = CharacterPackage.ReadAsset(Path.Combine(root, relative), character, slot);
            relative = "Character/" + key + "-" + code + "-" + slot + ".szs";
            var vehicle = current.FirstOrDefault(a => a.Target == relative)
                ?? CharacterPackage.ReadAsset(Path.Combine(root, relative), character, slot);
            if (menu == null) return FromRace(root, character, key, vehicle, token);
            var result = new VehiclePoseGeometry { Model = CharacterImagesForm.BuildPreview(vehicle, menu, token, true) };
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                var entry = CharacterImages.Members(vehicle).Single(p => Path.GetFileName(p.Key) == "kart_model.brres");
                string path = Path.Combine(work, "vehicle.brres");
                File.WriteAllBytes(path, entry.Value);
                if (((string[])StudioModelLibrary.Call("Models", path)).Contains("handle"))
                {
                    var handle = (float[][])StudioModelLibrary.Call("ModelVertices", path, "handle");
                    var points = result.Model.Points;
                    var lookup = new HashSet<string>(points.Select(PointKey));
                    var distinct = handle.GroupBy(PointKey).Select(g => g.First()).ToArray();
                    var samples = Enumerable.Range(0, Math.Min(24, distinct.Length)).Select(i => distinct[i * distinct.Length / Math.Min(24, distinct.Length)]).ToArray();
                    if (samples.Length > 1)
                    {
                        var first = distinct.OrderByDescending(p => p[0]).First();
                        var second = distinct.OrderByDescending(p => Math.Pow(p[1]-first[1], 2)+Math.Pow(p[2]-first[2], 2)).First();
                        double sourceY = second[1]-first[1], sourceZ = second[2]-first[2];
                        double length = Math.Sqrt(sourceY*sourceY+sourceZ*sourceZ);
                        foreach (var a in points.Where(p => Math.Abs(p[0]-first[0]) < .02))
                        foreach (var b in points.Where(p => Math.Abs(p[0]-second[0]) < .02))
                        {
                            double y = b[1]-a[1], z = b[2]-a[2];
                            if (Math.Abs(Math.Sqrt(y*y+z*z)-length) > .05) continue;
                            double angle = Math.Atan2(z,y)-Math.Atan2(sourceZ,sourceY);
                            double cos = Math.Cos(angle), sin = Math.Sin(angle);
                            Func<float[], float[]> transform = p => new[] { p[0],
                                (float)(a[1]+(p[1]-first[1])*cos-(p[2]-first[2])*sin),
                                (float)(a[2]+(p[1]-first[1])*sin+(p[2]-first[2])*cos) };
                            if (samples.Count(p => lookup.Contains(PointKey(transform(p)))) < samples.Length * .9) continue;
                            result.Handle = distinct.Select(transform).ToArray();
                            break;
                        }
                    }
                }
                var body = (float[][])StudioModelLibrary.Call("ModelVertices", path, "body");
                result.MenuOffset = RigVector.Add(BodyOffset(body, result.Model.Points), DriverOffset(root, character, key));
                foreach (var point in result.Model.Points)
                    for (int axis = 0; axis < 3; axis++) point[axis] -= result.MenuOffset[axis];
                if (result.Handle != null) foreach (var point in result.Handle)
                    for (int axis = 0; axis < 3; axis++) point[axis] -= result.MenuOffset[axis];
                return result;
            }
            catch
            {
                if (result.Model.Rig != null) ModelRuntime.DeleteWorkFolder(result.Model.Rig.Folder);
                throw;
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
        }
        static VehiclePoseGeometry FromRace(string root, CharacterDefinition character, string key,
            CharacterAsset vehicle, CancellationToken token)
        {
            string work = ModelRuntime.NewWorkFolder();
            try
            {
                var entry = CharacterImages.Members(vehicle).Single(p => Path.GetFileName(p.Key) == "kart_model.brres");
                string path = Path.Combine(work, "vehicle.brres");
                File.WriteAllBytes(path, entry.Value);
                var names = (string[])StudioModelLibrary.Call("Models", path);
                if (!names.Contains("body")) throw new InvalidDataException("Race vehicle body is missing.");
                var result = new VehiclePoseGeometry { Model = new CharacterModelImport(), MenuOffset = DriverOffset(root, character, key) };
                // Ohne eigenes Menuearchiv genuegt die echte Rennkarosserie samt Lenker als Gelenkhilfe.
                foreach (string name in new[] { "body", "handle" }.Where(names.Contains))
                {
                    token.ThrowIfCancellationRequested();
                    string dae = Path.Combine(work, name + ".dae");
                    StudioModelLibrary.Call("ExportModel", path, name, dae);
                    var document = new System.Xml.XmlDocument { XmlResolver = null }; document.Load(dae);
                    var unit = (System.Xml.XmlElement)document.SelectSingleNode("//*[local-name()='asset']/*[local-name()='unit']");
                    if (unit != null) unit.SetAttribute("meter", "1");
                    document.Save(dae);
                    var part = CharacterModelImport.LoadVisual(dae, token);
                    try
                    {
                        int offset = result.Model.Points.Count;
                        result.Model.Points.AddRange(part.Points.Select(p => RigVector.Sub(p, result.MenuOffset)));
                        result.Model.Faces.AddRange(part.Faces.Select(f => f.Select(i => i + offset).ToArray()));
                        result.Model.FaceColors.AddRange(part.FaceColors);
                    }
                    finally { if (part.Rig != null) ModelRuntime.DeleteWorkFolder(part.Rig.Folder); }
                }
                if (names.Contains("handle")) result.Handle = ((float[][])StudioModelLibrary.Call("ModelVertices", path, "handle"))
                    .Select(p => RigVector.Sub(p, result.MenuOffset)).ToArray();
                return result;
            }
            finally { ModelRuntime.DeleteWorkFolder(work); }
        }
        internal float[] Grip(float[] expected, out float[] axis)
        {
            axis = new[] { expected[0] < 0 ? -1f : 1f, 0f, 0f };
            if (Handle == null || Handle.Length == 0) return expected;
            float side = expected[0] < 0 ? -1 : 1;
            var half = Handle.Where(p => p[0] * side > 0).ToArray();
            if (half.Length < 4) return expected;
            float outer = half.Max(p => p[0] * side);
            var section = half.Where(p => outer - p[0] * side < .1f).ToArray();
            if (section.Length < 3)
            {
                var tip = half.OrderByDescending(p => p[0] * side).First();
                var nearby = half.Where(p => RigVector.Length(RigVector.Sub(p, tip)) < outer * .32).ToArray();
                if (nearby.Length >= 6)
                {
                    var center = Enumerable.Range(0, 3).Select(i => nearby.Average(p => p[i])).ToArray();
                    var direction = RigVector.Unit(RigVector.Sub(tip, center));
                    for (int step = 0; step < 20; step++)
                    {
                        var next = new float[3];
                        foreach (var point in nearby)
                        {
                            var delta = RigVector.Sub(point, center);
                            var part = RigVector.Scale(delta, RigVector.Dot(delta, direction));
                            for (int i = 0; i < 3; i++) next[i] += part[i];
                        }
                        direction = RigVector.Unit(next);
                    }
                    if (direction[0] * side < 0) direction = RigVector.Scale(direction, -1);
                    var projections = nearby.Select(p => RigVector.Dot(RigVector.Sub(p, center), direction)).ToArray();
                    double low = projections.Min(), high = projections.Max();
                    if (high - low > 6)
                    {
                        axis = direction;
                        return RigVector.Add(center, RigVector.Scale(direction, (low + high) * .5));
                    }
                }
            }
            float y = (section.Min(p => p[1]) + section.Max(p => p[1])) * .5f;
            float z = (section.Min(p => p[2]) + section.Max(p => p[2])) * .5f;
            double radius = Math.Max(1, section.Max(p => Math.Sqrt(Math.Pow(p[1]-y,2)+Math.Pow(p[2]-z,2))));
            var grip = half.Where(p => Math.Sqrt(Math.Pow(p[1]-y,2)+Math.Pow(p[2]-z,2)) <= radius * 1.2).ToArray();
            float inner = grip.Min(p => p[0] * side);
            return new[] { side * Math.Max(inner, Math.Min(outer, Math.Abs(expected[0]))), y, z };
        }
        internal float? SideSurface(float[] point, float side)
        {
            float? outside = null;
            foreach (var face in Model.Faces)
            {
                var a = Model.Points[face[0]];
                var b = Model.Points[face[1]];
                var c = Model.Points[face[2]];
                double d = (b[2] - c[2]) * (a[1] - c[1]) + (c[1] - b[1]) * (a[2] - c[2]);
                if (Math.Abs(d) < .00001) continue;
                double u = ((b[2] - c[2]) * (point[1] - c[1]) + (c[1] - b[1]) * (point[2] - c[2])) / d;
                double v = ((c[2] - a[2]) * (point[1] - c[1]) + (a[1] - c[1]) * (point[2] - c[2])) / d;
                if (u < 0 || v < 0 || u + v > 1) continue;
                float x = side * (float)(u * a[0] + v * b[0] + (1 - u - v) * c[0]);
                if (x >= 0 && (!outside.HasValue || x > outside.Value))
                {
                    outside = x;
                }
            }
            return outside;
        }

        internal float[] FootSupport(float[] expected, float side, double clearance = 0, double height = 0)
        {
            float[] best = expected;
            double cost = 25 * 25;
            foreach (var face in Model.Faces)
            {
                var a = Model.Points[face[0]]; var b = Model.Points[face[1]]; var c = Model.Points[face[2]];
                var normal = RigVector.Unit(RigVector.Cross(RigVector.Sub(b, a), RigVector.Sub(c, a)));
                if (normal[1] < .65) continue;
                var candidates = new List<float[]>();
                for (double outward = 0; outward <= (clearance > 0 ? 25 : 0); outward += Math.Max(1, clearance * .5))
                {
                    var probe = RigVector.Add(expected, new[] { (float)(side * outward), 0f, 0f });
                    candidates.Add(RigVector.Sub(probe, RigVector.Scale(normal, RigVector.Dot(RigVector.Sub(probe, a), normal))));
                    candidates.Add(NearestEdge(probe, a, b));
                    candidates.Add(NearestEdge(probe, b, c));
                    candidates.Add(NearestEdge(probe, c, a));
                }
                foreach (var point in candidates)
                {
                    if (point[0] * side < Math.Abs(expected[0]) * .5 || Math.Abs(point[1] - expected[1]) > 15) continue;
                    bool inside = new[] { new[] { a, b }, new[] { b, c }, new[] { c, a } }.All(edge =>
                        RigVector.Dot(RigVector.Cross(RigVector.Sub(edge[1], edge[0]), RigVector.Sub(point, edge[0])), normal) >= -.0001);
                    if (!inside) continue;
                    if (clearance > 0 && Enumerable.Range(1, 4).Any(step => {
                        var surface = SideSurface(RigVector.Add(point, new[] { 0f, (float)(height * step / 4), 0f }), side);
                        return surface.HasValue && surface.Value + clearance > point[0] * side;
                    })) continue;
                    var delta = RigVector.Sub(point, expected);
                    double distance = RigVector.Dot(delta, delta);
                    if (distance < cost) { cost = distance; best = point; }
                }
            }
            return (float[])best.Clone();
        }
        static float[] NearestEdge(float[] point, float[] a, float[] b)
        {
            var edge = RigVector.Sub(b, a);
            double amount = Math.Max(0, Math.Min(1, RigVector.Dot(RigVector.Sub(point, a), edge) / Math.Max(.00001, RigVector.Dot(edge, edge))));
            return RigVector.Add(a, RigVector.Scale(edge, amount));
        }

        internal float? SeatHeight(float[] hip)
        {
            float? height = null;
            foreach (var face in Model.Faces)
            {
                var a = Model.Points[face[0]]; var b = Model.Points[face[1]]; var c = Model.Points[face[2]];
                double d = (b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2]);
                if (Math.Abs(d) < .00001 || RigVector.Unit(RigVector.Cross(RigVector.Sub(b, a), RigVector.Sub(c, a)))[1] < .8) continue;
                double u = ((b[2]-c[2])*(hip[0]-c[0])+(c[0]-b[0])*(hip[2]-c[2]))/d;
                double v = ((c[2]-a[2])*(hip[0]-c[0])+(a[0]-c[0])*(hip[2]-c[2]))/d;
                if (u < 0 || v < 0 || u+v > 1) continue;
                float y = (float)(u*a[1]+v*b[1]+(1-u-v)*c[1]);
                if (y > hip[1] + 25 || hip[1]-y > 60) continue;
                if (!height.HasValue || y > height.Value) height = y;
            }
            return height;
        }
    }
}
