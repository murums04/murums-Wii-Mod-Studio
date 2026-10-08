using System;
using System.Collections.Generic;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        void FitSeparatedPoseArms(float[] min, float[] max, Dictionary<string, float[]> reviewed)
        {
            float height = max[1] - min[1];
            if (height <= .0001f || Faces == null || Faces.Length == 0) return;
            var lowerBody = Points.Where(p => p[1] > min[1] + height * .15f && p[1] < min[1] + height * .45f).ToArray();
            float center = lowerBody.Length < 12 ? (min[0] + max[0]) * .5f : Quantile(lowerBody.Select(p => p[0]), .5);
            var origin = new[] { center, min[1], (min[2] + max[2]) * .5f };
            var normalized = Points.Select(p => RigVector.Scale(RigVector.Sub(p, origin), 1 / height)).ToArray();
            foreach (string side in new[] { "l", "r" })
            {
                string[] names = { "arm_" + side + "1", "arm_" + side + "2", "wrist_" + side + "1" };
                if (names.All(n => SourceJointGuides != null && SourceJointGuides.ContainsKey(n)
                    || reviewed != null && reviewed.ContainsKey(n))) continue;
                float sign = side == "l" ? 1 : -1;
                var local = normalized.Select(p => new[] { sign * p[0], p[1], p[2] }).ToArray();
                float[][] joints;
                if (!TryTracePoseArm(local, Faces, out joints)) continue;
                var world = joints.Select(p => RigVector.Add(origin, RigVector.Scale(new[] { sign * p[0], p[1], p[2] }, height))).ToArray();
                bool compatible = true;
                for (int j = 0; j < names.Length; j++)
                {
                    float[] known;
                    if (reviewed != null && reviewed.TryGetValue(names[j], out known)
                        || SourceJointGuides != null && SourceJointGuides.TryGetValue(names[j], out known))
                        if (known == null || known.Length != 3 || RigVector.Length(RigVector.Sub(known, world[j])) > height * .04) compatible = false;
                }
                if (!compatible) continue;
                for (int j = 0; j < names.Length; j++)
                {
                    int bone = PoseBone(names[j]);
                    if (bone < 0) continue;
                    JointGuides[bone] = world[j];
                }
            }
        }

        // Geodaetische Querschnitte folgen auch geknickten Armen, ohne nahe Flaechen zu verbinden.
        internal static bool TryTracePoseArm(float[][] points, int[][] faces, out float[][] joints)
        {
            joints = null;
            var vertices = new List<float[]>();
            var welded = new Dictionary<string, int>(StringComparer.Ordinal);
            var map = new int[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (p[0] < .055f || p[1] < .42f) { map[i] = -1; continue; }
                string key = String.Join(",", p.Select(v => Math.Round(v / .00005).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                int index;
                if (!welded.TryGetValue(key, out index)) { index = vertices.Count; welded.Add(key, index); vertices.Add(p); }
                map[i] = index;
            }
            var adjacency = vertices.Select(p => new HashSet<int>()).ToArray();
            foreach (var face in faces)
                for (int e = 0; e < 3; e++)
                {
                    int a = map[face[e]], b = map[face[(e + 1) % 3]];
                    if (a >= 0 && b >= 0 && a != b) { adjacency[a].Add(b); adjacency[b].Add(a); }
                }
            var seen = new bool[vertices.Count];
            var candidates = new List<float[][]>();
            for (int first = 0; first < vertices.Count; first++)
            {
                if (seen[first]) continue;
                var component = new List<int> { first }; seen[first] = true;
                for (int cursor = 0; cursor < component.Count; cursor++)
                    foreach (int next in adjacency[component[cursor]])
                        if (!seen[next]) { seen[next] = true; component.Add(next); }
                if (component.Count < 48) continue;
                float[][] candidate;
                if (TracePoseComponent(vertices, adjacency, component, out candidate)
                    || TraceSparsePoseComponent(vertices, adjacency, component, out candidate)) candidates.Add(candidate);
            }
            if (candidates.Count != 1) return false;
            joints = candidates[0];
            return true;
        }

        // Kantenquerungen bleiben auch nach einer groben Meshvereinfachung brauchbare Querschnitte.
        static bool TraceSparsePoseComponent(List<float[]> points, HashSet<int>[] edges, List<int> component, out float[][] joints)
        {
            joints = null;
            float inner = component.Min(i => points[i][0]);
            if (inner > .13 || component.Count < 80) return false;
            var roots = component.Where(i => points[i][0] < inner + .008 && points[i][1] > .70).ToArray();
            if (roots.Length < 4) return false;
            var root = Enumerable.Range(0, 3).Select(a => roots.Average(i => points[i][a])).ToArray();
            if (root[1] > .86 || roots.Any(i => RigVector.Length(RigVector.Sub(points[i], root)) > .085)) return false;
            var distances = Enumerable.Repeat(Double.PositiveInfinity, points.Count).ToArray();
            var queue = new SortedSet<Tuple<double, int>>();
            foreach (int i in roots) { distances[i] = 0; queue.Add(Tuple.Create(0d, i)); }
            while (queue.Count > 0)
            {
                var item = queue.Min; queue.Remove(item);
                foreach (int next in edges[item.Item2])
                {
                    double distance = item.Item1 + RigVector.Length(RigVector.Sub(points[item.Item2], points[next]));
                    if (distance >= distances[next]) continue;
                    queue.Remove(Tuple.Create(distances[next], next));
                    distances[next] = distance; queue.Add(Tuple.Create(distance, next));
                }
            }
            double reach = component.Max(i => distances[i]);
            if (reach < .22 || reach > .55) return false;
            var sections = new List<float[]>(); var radii = new List<double>();
            var links = component.SelectMany(i => edges[i].Where(n => n > i).Select(n => new[] { i, n })).ToArray();
            for (double distance = .015; distance < reach - .015; distance += .01)
            {
                var ring = new List<float[]>();
                foreach (var link in links)
                {
                    double a = distances[link[0]], b = distances[link[1]];
                    if (Math.Abs(a - b) < 1e-8 || distance < Math.Min(a, b) || distance >= Math.Max(a, b)) continue;
                    ring.Add(RigVector.Add(points[link[0]], RigVector.Scale(RigVector.Sub(points[link[1]], points[link[0]]), (distance - a) / (b - a))));
                }
                if (ring.Count < 4) return false;
                var center = Enumerable.Range(0, 3).Select(a => ring.Average(p => p[a])).ToArray();
                double radius = ring.Average(p => RigVector.Length(RigVector.Sub(p, center)));
                if (radius < .006 || radius > .045 || ring.Any(p => RigVector.Length(RigVector.Sub(p, center)) > .075)) return false;
                sections.Add(center); radii.Add(radius);
            }
            if (sections.Count < 16) return false;
            int wrist = -1; double smallest = Double.PositiveInfinity;
            for (int i = sections.Count / 2; i < sections.Count - 5; i++)
            {
                double local = radii.Skip(Math.Max(0, i - 1)).Take(3).Average();
                double hand = radii.Skip(i + 3).Take(3).Average();
                double remaining = reach - (.015 + i * .01);
                if (remaining < .045 || remaining > .13 || hand < local * 1.18 || local >= smallest) continue;
                smallest = local; wrist = i;
            }
            if (wrist < 10) return false;
            var shaft = sections.Take(wrist + 1).ToArray();
            int split = -1; double error = Double.PositiveInfinity;
            for (int i = 5; i < shaft.Length - 5; i++)
            {
                double upper = RigVector.Length(RigVector.Sub(shaft[i], shaft[0]));
                double lower = RigVector.Length(RigVector.Sub(shaft.Last(), shaft[i]));
                if (upper < .07 || lower < .07 || upper / lower < .45 || upper / lower > 2.2) continue;
                double candidate = shaft.Take(i + 1).Sum(p => RigVector.SegmentDistanceSquared(p, shaft[0], shaft[i]))
                    + shaft.Skip(i).Sum(p => RigVector.SegmentDistanceSquared(p, shaft[i], shaft.Last()));
                if (candidate < error) { error = candidate; split = i; }
            }
            if (split < 0 || Math.Sqrt(error / shaft.Length) > .018) return false;
            var upperAxis = RigVector.Unit(RigVector.Sub(shaft[split], shaft[0]));
            var lowerAxis = RigVector.Unit(RigVector.Sub(shaft.Last(), shaft[split]));
            if (upperAxis[0] < .2 || RigVector.Dot(upperAxis, lowerAxis) > .985) return false;
            double extension = Math.Max(0, (shaft[0][0] - .07) / upperAxis[0]);
            if (extension > .09) return false;
            var shoulder = RigVector.Sub(shaft[0], RigVector.Scale(upperAxis, extension));
            if (shoulder[1] < .70 || shoulder[1] > .88) return false;
            joints = new[] { shoulder, shaft[split], shaft.Last() };
            return true;
        }

        static bool TracePoseComponent(List<float[]> points, HashSet<int>[] edges, List<int> component, out float[][] joints)
        {
            joints = null;
            var roots = component.Where(i => points[i][0] < .11f).ToArray();
            if (roots.Length < 4) return false;
            var root = Enumerable.Range(0, 3).Select(a => roots.Average(i => points[i][a])).ToArray();
            if (root[1] < .62f || root[1] > .88f || roots.Any(i => RigVector.Length(RigVector.Sub(points[i], root)) > .055)) return false;
            var distances = Enumerable.Repeat(Double.PositiveInfinity, points.Count).ToArray();
            var queue = new SortedSet<Tuple<double, int>>();
            foreach (int i in roots) { distances[i] = 0; queue.Add(Tuple.Create(0d, i)); }
            while (queue.Count > 0)
            {
                var item = queue.Min; queue.Remove(item);
                int current = item.Item2;
                foreach (int next in edges[current])
                {
                    double d = item.Item1 + RigVector.Length(RigVector.Sub(points[current], points[next]));
                    if (d >= distances[next]) continue;
                    queue.Remove(Tuple.Create(distances[next], next));
                    distances[next] = d; queue.Add(Tuple.Create(d, next));
                }
            }
            double reach = component.Max(i => distances[i]);
            if (reach < .22 || reach > .55) return false;
            var sections = new List<float[]> { root };
            var radii = new List<double> { 0 };
            for (double distance = .02; distance < reach - .008; distance += .0125)
            {
                var ring = component.Where(i => Math.Abs(distances[i] - distance) <= .009).ToArray();
                if (ring.Length < 4) return false;
                var center = Enumerable.Range(0, 3).Select(a => ring.Average(i => points[i][a])).ToArray();
                double radius = ring.Average(i => RigVector.Length(RigVector.Sub(points[i], center)));
                // Breite Aermel, Haare und mehrere Aeste liefern keine eindeutige Armachse.
                if (radius < .006 || radius > .04 || ring.Any(i => RigVector.Length(RigVector.Sub(points[i], center)) > .06)) return false;
                sections.Add(center);
                radii.Add(radius);
            }
            if (sections.Count < 12) return false;
            int wrist = -1;
            for (int i = sections.Count * 2 / 3; i < sections.Count - 2; i++)
            {
                double shaft = radii.Skip(Math.Max(1, i - 4)).Take(4).Average();
                if (radii[i + 1] > shaft * 1.35 && radii[i + 2] > shaft * 1.35
                    && reach - (.02 + (i - 1) * .0125) < .10)
                { wrist = i; break; }
            }
            // Ohne erkennbare Handbasis waere das Ende auch ein Finger, Haar oder Aermel.
            if (wrist < 10) return false;
            sections = sections.Take(wrist + 1).ToList();
            int split = -1; double error = Double.PositiveInfinity;
            for (int i = 4; i < sections.Count - 4; i++)
            {
                double upper = RigVector.Length(RigVector.Sub(sections[i], root));
                double lower = RigVector.Length(RigVector.Sub(sections.Last(), sections[i]));
                if (upper < .085 || lower < .085 || upper / lower < .5 || upper / lower > 2) continue;
                double candidate = sections.Take(i + 1).Sum(p => RigVector.SegmentDistanceSquared(p, root, sections[i]))
                    + sections.Skip(i).Sum(p => RigVector.SegmentDistanceSquared(p, sections[i], sections.Last()));
                if (candidate < error) { error = candidate; split = i; }
            }
            if (split < 0 || Math.Sqrt(error / sections.Count) > .012) return false;
            var upperDirection = RigVector.Unit(RigVector.Sub(sections[split], root));
            var lowerDirection = RigVector.Unit(RigVector.Sub(sections.Last(), sections[split]));
            // Gerade Arme bleiben beim vorhandenen fitter; hier muss ein sichtbarer Knick vorliegen.
            if (RigVector.Dot(upperDirection, lowerDirection) > .94) return false;
            joints = new[] { root, sections[split], sections.Last() };
            return true;
        }
    }
}
