using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Xml;
using System.Globalization;
using BrawlLib.Imaging;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.Wii.Animations;
using System.Collections.Generic;
using BrawlLib.Wii.Textures;
using BrawlLib.Modeling.Collada;
using BrawlLib.SSBB.ResourceNodes;
public static class StudioModelCodec
{
    static MDL0Node Driver(ResourceNode resource)
    {
        var root = resource as BRRESNode;
        if (root == null) throw new InvalidDataException("Expected a BRRES driver template.");
        var group = root.GetFolder<MDL0Node>();
        var models = group == null ? new MDL0Node[0] : group.Children.OfType<MDL0Node>().ToArray();
        var selected = models.Length == 1 ? models[0] : models.FirstOrDefault(m => m.Name == "model");
        if (selected == null) throw new InvalidDataException("Choose a driver template containing model.");
        PrepareReference(selected);
        return selected;
    }
    static bool FiniteBind(MDL0BoneNode bone)
    {
        return Enumerable.Range(0, 16).All(i =>
            !Single.IsNaN(bone.BindMatrix[i]) && !Single.IsInfinity(bone.BindMatrix[i]) &&
            !Single.IsNaN(bone.InverseBindMatrix[i]) && !Single.IsInfinity(bone.InverseBindMatrix[i]));
    }

    static string OriginalBoneName(string name)
    {
        return System.Text.RegularExpressions.Regex.Replace(name, @"_ncl[0-9]+_[0-9]+$", "");
    }

    static void PrepareReference(MDL0Node model)
    {
        model.Populate();
        var bones = model.AllBones;
        if (bones.All(FiniteBind)) return;
        var archive = model.Parent.Parent as BRRESNode;
        var animations = archive == null ? null : archive.GetFolder<CHR0Node>();
        var animated = new HashSet<string>(animations == null ? new string[0] :
            animations.Children.OfType<CHR0Node>().SelectMany(a => a.Children).Select(b => b.Name));
        bool removed = false;
        foreach (var root in bones.Where(b => !(b.Parent is MDL0BoneNode)).ToArray())
        {
            if (OriginalBoneName(root.Name) == root.Name) continue;
            var branch = new[] { root }.Concat(root.GetChildrenRecursive().OfType<MDL0BoneNode>()).Distinct().ToArray();
            if (branch.All(FiniteBind)) continue;
            bool unusedDuplicate = branch.All(b => b.Users.Count == 0 && b.SingleBindObjects.Length == 0 &&
                b.VisibilityDrawCalls.Length == 0 && !animated.Contains(b.Name) &&
                OriginalBoneName(b.Name) != b.Name && bones.Any(original => original.Name == OriginalBoneName(b.Name) &&
                    FiniteBind(original) && (!(b.Parent is MDL0BoneNode) ? !(original.Parent is MDL0BoneNode) :
                        original.Parent is MDL0BoneNode && original.Parent.Name == OriginalBoneName(b.Parent.Name))));
            if (!unusedDuplicate) continue;
            // Defekte, unbenutzte Import-Duplikate nur aus der Arbeitskopie entfernen.
            root.Remove();
            removed = true;
        }
        if (removed) model._linker.RegenerateBoneCache();
        var invalid = model.AllBones.FirstOrDefault(b => !FiniteBind(b));
        if (invalid != null)
            throw new InvalidDataException("The RR reference contains invalid joint data: " + invalid.Name +
                ". The joint is in use or has no verified original counterpart. Choose an intact RR reference.");
    }

    static bool UsablePose(Matrix matrix)
    {
        double determinant = matrix[0] * (matrix[5] * matrix[10] - matrix[9] * matrix[6]) -
            matrix[4] * (matrix[1] * matrix[10] - matrix[9] * matrix[2]) +
            matrix[8] * (matrix[1] * matrix[6] - matrix[5] * matrix[2]);
        return !Double.IsNaN(determinant) && !Double.IsInfinity(determinant) && Math.Abs(determinant) > .000001;
    }

    static void NormalizePoseAliases(MDL0Node model)
    {
        var bones = model.AllBones;
        foreach (var bone in bones)
        {
            if (UsablePose(bone._frameMatrix)) continue;
            string otherName = bone.Name.StartsWith("pcd_", StringComparison.Ordinal) ? bone.Name.Substring(4) : "pcd_" + bone.Name;
            var visible = bones.FirstOrDefault(b => b.Name == otherName && UsablePose(b._frameMatrix));
            if (visible == null || new[] { 12, 13, 14 }.Sum(i => Math.Pow(bone.BindMatrix[i] - visible.BindMatrix[i], 2)) >= .01) continue;
            // Manche RR-Menüs verstecken eine parallele Körpervariante durch Skalierung.
            var parent = bone.Parent as MDL0BoneNode;
            var local = parent == null ? visible._frameMatrix : parent._frameMatrix.Invert() * visible._frameMatrix;
            bone._frameState = PoseState(local, bone.Name);
            bone._frameMatrix = parent == null ? bone._frameState._transform : parent._frameMatrix * bone._frameState._transform;
            bone._inverseFrameMatrix = bone._frameMatrix.Invert();
        }
    }

    public static string[] Models(string path)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            if (root == null) throw new InvalidDataException("Expected BRRES.");
            var group = root.GetFolder<MDL0Node>();
            return group == null ? new string[0] : group.Children.OfType<MDL0Node>().Select(m => m.Name).ToArray();
        }
    }
    public static void ExportModel(string path, string name, string destination)
    {
        if (File.Exists(destination)) throw new IOException("Destination exists.");
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            if (root == null) throw new InvalidDataException("Expected BRRES.");
            var group = root.GetFolder<MDL0Node>();
            var model = group == null ? null : group.Children.OfType<MDL0Node>().FirstOrDefault(m => m.Name == name);
            if (model == null) throw new InvalidDataException("Model missing: " + name);
            WriteReference(model, destination);
            var textures = root.GetFolder<TEX0Node>();
            if (textures != null) foreach (var texture in textures.Children.OfType<TEX0Node>())
            {
                if (Path.GetFileName(texture.Name) != texture.Name) throw new InvalidDataException("Invalid texture name.");
                using (var bitmap = texture.GetImage(0)) bitmap.Save(Path.Combine(Path.GetDirectoryName(destination), texture.Name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
    static void WriteReference(MDL0Node model, string destination)
    {
        PrepareReference(model);
        Collada.Serialize(model, destination);
        var document = new XmlDocument { XmlResolver = null };
        document.Load(destination);
        foreach (var bone in model.AllBones)
        {
            var element = document.SelectNodes("//*[local-name()='node' and @type='JOINT']").Cast<XmlElement>()
                .Single(node => node.GetAttribute("sid") == bone.Name);
            var parent = bone.Parent as MDL0BoneNode;
            var local = parent == null ? bone.BindMatrix : parent.InverseBindMatrix * bone.BindMatrix;
            // RR kann gespeicherte Bindematrizen besitzen, die von den lokalen SRT-Werten abweichen.
            foreach (var transform in element.ChildNodes.OfType<XmlElement>().Where(node =>
                node.LocalName == "translate" || node.LocalName == "rotate" || node.LocalName == "scale" || node.LocalName == "matrix").ToArray())
                element.RemoveChild(transform);
            var matrix = document.CreateElement("matrix", element.NamespaceURI);
            matrix.InnerText = String.Join(" ", Enumerable.Range(0, 16)
                .Select(i => local[(i % 4) * 4 + i / 4].ToString("R", CultureInfo.InvariantCulture)));
            element.PrependChild(matrix);
        }
        document.Save(destination);
    }
    public static string ReadPose(string path, string modelName, string animationName, float frame, string previewPath)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            if (root == null) throw new InvalidDataException("Expected a BRRES reference.");
            var model = root.GetFolder<MDL0Node>().Children.OfType<MDL0Node>().Single(m => m.Name == modelName);
            PrepareReference(model);
            if (!String.IsNullOrEmpty(previewPath)) Collada.Serialize(model, previewPath);
            var animations = root.GetFolder<CHR0Node>();
            var animation = animations == null ? null : animations.Children.OfType<CHR0Node>().FirstOrDefault(a => a.Name == animationName);
            if (animation == null) throw new InvalidDataException("RR animation missing: " + animationName);
            model.ApplyCHR(animation, Math.Max(1, Math.Min(animation.FrameCount, frame)));
            NormalizePoseAliases(model);
            var doc = new XmlDocument { XmlResolver = null };
            var pose = doc.CreateElement("pose"); doc.AppendChild(pose);
            pose.SetAttribute("animation", animation.Name);
            pose.SetAttribute("available", String.Join("|", animations.Children.OfType<CHR0Node>().Select(a => a.Name)));
            pose.SetAttribute("frames", animation.FrameCount.ToString(CultureInfo.InvariantCulture));
            foreach (var bone in model.AllBones)
            {
                var node = doc.CreateElement("bone"); pose.AppendChild(node);
                node.SetAttribute("name", bone.Name);
                node.SetAttribute("parent", bone.Parent is MDL0BoneNode ? bone.Parent.Name : "");
                node.SetAttribute("bind", String.Join(" ", Enumerable.Range(0, 16).Select(i => bone.BindMatrix[(i % 4) * 4 + i / 4].ToString("R", CultureInfo.InvariantCulture))));
                var skin = bone._frameMatrix * bone.InverseBindMatrix;
                node.SetAttribute("skin", String.Join(" ", Enumerable.Range(0, 16).Select(i => skin[(i % 4) * 4 + i / 4].ToString("R", CultureInfo.InvariantCulture))));
                node.SetAttribute("joint", String.Join(" ", new[] { bone._frameMatrix[12], bone._frameMatrix[13], bone._frameMatrix[14] }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
            }
            if (!String.IsNullOrEmpty(previewPath))
            {
                model.ApplyCHR(animation, Math.Max(1, Math.Min(animation.FrameCount, frame)));
                var visual = new XmlDocument { XmlResolver = null }; visual.Load(previewPath);
                foreach (var poly in model.PolygonGroup.Children.OfType<MDL0ObjectNode>())
                {
                    var array = visual.SelectNodes("//*[local-name()='float_array']").Cast<XmlElement>().Single(e => e.GetAttribute("id") == poly.Name + "_PosArr");
                    array.InnerText = String.Join(" ", poly.Vertices.SelectMany(v => new[] { v.WeightedPosition._x, v.WeightedPosition._y, v.WeightedPosition._z }).Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                }
                visual.Save(previewPath);
            }
            return doc.OuterXml;
        }
    }

    static FrameState PoseState(Matrix matrix, string location)
    {
        double sx = Math.Sqrt(matrix[0] * matrix[0] + matrix[1] * matrix[1] + matrix[2] * matrix[2]);
        double sy = Math.Sqrt(matrix[4] * matrix[4] + matrix[5] * matrix[5] + matrix[6] * matrix[6]);
        double sz = Math.Sqrt(matrix[8] * matrix[8] + matrix[9] * matrix[9] + matrix[10] * matrix[10]);
        if (Math.Min(sx, Math.Min(sy, sz)) < .000001) throw new InvalidDataException("RR animation contains a zero scale.");
        double determinant = matrix[0] * (matrix[5] * matrix[10] - matrix[9] * matrix[6])
            - matrix[4] * (matrix[1] * matrix[10] - matrix[9] * matrix[2]) + matrix[8] * (matrix[1] * matrix[6] - matrix[5] * matrix[2]);
        if (determinant < 0) sz = -sz;
        double y = Math.Asin(Math.Max(-1, Math.Min(1, -matrix[2] / sx)));
        double x, z;
        if (Math.Abs(Math.Cos(y)) < .0001)
        {
            z = 0;
            x = y > 0 ? Math.Atan2(matrix[4] / sy, matrix[8] / sz) : Math.Atan2(-matrix[4] / sy, -matrix[8] / sz);
        }
        else
        {
            x = Math.Atan2(matrix[6] / sy, matrix[10] / sz);
            z = Math.Atan2(matrix[1] / sx, matrix[0] / sx);
        }
        var result = new FrameState(new Vector3((float)sx, (float)sy, (float)sz),
            new Vector3((float)(x * 180 / Math.PI), (float)(y * 180 / Math.PI), (float)(z * 180 / Math.PI)),
            new Vector3(matrix[12], matrix[13], matrix[14]));
        for (int i = 0; i < 16; i++)
            if (Single.IsNaN(result._transform[i]) || Single.IsInfinity(result._transform[i]) || Math.Abs(result._transform[i] - matrix[i]) > .02f)
                throw new InvalidDataException("The edited pose cannot preserve this RR animation (" + location + ", matrix " + i + ", delta " + Math.Abs(result._transform[i] - matrix[i]) + "). No character package was created.");
        return result;
    }

    sealed class PoseAdjustment
    {
        internal Matrix Offset, Anchor;
        internal float Strength;
        internal bool Natural;
        internal string Name;
    }

    static Matrix ParsePoseMatrix(string text)
    {
        var values = text.Split(' ').Select(v => Single.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != 16 || values.Any(v => Single.IsNaN(v) || Single.IsInfinity(v)))
            throw new InvalidDataException("Invalid game pose matrix.");
        var matrix = Matrix.Identity;
        for (int i = 0; i < 16; i++) matrix[(i % 4) * 4 + i / 4] = values[i];
        return matrix;
    }

    static Dictionary<string, PoseAdjustment> ReadAdjustments(string corrections)
    {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(corrections);
        float strength = Single.Parse(document.DocumentElement.GetAttribute("strength"), CultureInfo.InvariantCulture);
        if (Single.IsNaN(strength) || strength < 0 || strength > 1)
            throw new InvalidDataException("Movement strength must be between 0 and 100 percent.");
        return document.DocumentElement.ChildNodes.OfType<XmlElement>().ToDictionary(n => n.GetAttribute("name"), n => new PoseAdjustment {
            Offset = ParsePoseMatrix(n.GetAttribute("matrix")),
            Anchor = ParsePoseMatrix(n.GetAttribute("anchor")),
            Strength = strength,
            Natural = document.DocumentElement.GetAttribute("natural") == "true",
            Name = n.GetAttribute("name")
        });
    }

    static double[] RotationQuaternion(Matrix matrix, Vector3 scale)
    {
        var m = new double[3, 3];
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++) m[row, column] = matrix[column * 4 + row] / scale[column];
        var q = new double[4];
        double trace = m[0, 0] + m[1, 1] + m[2, 2];
        if (trace > 0)
        {
            double size = Math.Sqrt(trace + 1) * 2;
            q[3] = size / 4;
            q[0] = (m[2, 1] - m[1, 2]) / size;
            q[1] = (m[0, 2] - m[2, 0]) / size;
            q[2] = (m[1, 0] - m[0, 1]) / size;
        }
        else
        {
            int axis = m[0, 0] > m[1, 1] ? 0 : 1;
            if (m[2, 2] > m[axis, axis]) axis = 2;
            int next = (axis + 1) % 3, last = (axis + 2) % 3;
            double size = Math.Sqrt(1 + m[axis, axis] - m[next, next] - m[last, last]) * 2;
            q[axis] = size / 4;
            q[next] = (m[axis, next] + m[next, axis]) / size;
            q[last] = (m[axis, last] + m[last, axis]) / size;
            q[3] = (m[last, next] - m[next, last]) / size;
        }
        double length = Math.Sqrt(q.Sum(v => v * v));
        return q.Select(v => v / length).ToArray();
    }

    static Matrix BlendPose(Matrix anchor, Matrix frame, float strength)
    {
        if (strength == 0) return anchor;
        if (strength == 1) return frame;
        var from = PoseState(anchor, "movement anchor");
        var to = PoseState(frame, "movement frame");
        var a = RotationQuaternion(anchor, from._scale);
        var b = RotationQuaternion(frame, to._scale);
        double dot = a.Select((v, i) => v * b[i]).Sum();
        if (dot < 0) { b = b.Select(v => -v).ToArray(); dot = -dot; }
        double left = 1 - strength, right = strength;
        if (dot < .9995)
        {
            double angle = Math.Acos(Math.Min(1, dot));
            left = Math.Sin((1 - strength) * angle) / Math.Sin(angle);
            right = Math.Sin(strength * angle) / Math.Sin(angle);
        }
        var q = a.Select((v, i) => left * v + right * b[i]).ToArray();
        double length = Math.Sqrt(q.Sum(v => v * v));
        q = q.Select(v => v / length).ToArray();
        double x = q[0], y = q[1], z = q[2], w = q[3];
        var rotation = new[,] {
            { 1 - 2 * (y*y + z*z), 2 * (x*y - z*w), 2 * (x*z + y*w) },
            { 2 * (x*y + z*w), 1 - 2 * (x*x + z*z), 2 * (y*z - x*w) },
            { 2 * (x*z - y*w), 2 * (y*z + x*w), 1 - 2 * (x*x + y*y) }
        };
        var result = Matrix.Identity;
        for (int axis = 0; axis < 3; axis++)
        {
            float scale = from._scale[axis] + strength * (to._scale[axis] - from._scale[axis]);
            for (int row = 0; row < 3; row++) result[axis * 4 + row] = (float)rotation[row, axis] * scale;
            result[12 + axis] = from._translate[axis] + strength * (to._translate[axis] - from._translate[axis]);
        }
        return result;
    }

    static Matrix CorrectFrame(PoseAdjustment adjustment, Matrix frame)
    {
        if (!adjustment.Natural)
            return adjustment.Offset * BlendPose(adjustment.Anchor, frame, adjustment.Strength);
        var anchor = adjustment.Offset * adjustment.Anchor;
        var start = PoseState(anchor, "natural anchor");
        var from = PoseState(adjustment.Anchor, "source anchor");
        var to = PoseState(frame, "source movement");
        var unit = new Vector3(1, 1, 1);
        var zero = new Vector3();
        var originalRotation = new FrameState(unit, from._rotate, zero)._transform;
        var frameRotation = new FrameState(unit, to._rotate, zero)._transform;
        var desiredRotation = new FrameState(unit, start._rotate, zero)._transform;
        var rotation = desiredRotation * originalRotation.Invert() * BlendPose(originalRotation, frameRotation, adjustment.Strength);
        var corrected = new FrameState(start._scale, PoseState(rotation, "natural rotation")._rotate, start._translate)._transform;
        var result = PoseState(corrected, "natural movement");
        var a = RotationQuaternion(anchor, start._scale);
        var b = RotationQuaternion(corrected, result._scale);
        double dot = Math.Min(1, Math.Abs(a.Select((v, i) => v * b[i]).Sum()));
        double angle = 2 * Math.Acos(dot) * 180 / Math.PI;
        double limit = adjustment.Name.StartsWith("arm_") || adjustment.Name.StartsWith("leg_") ? 65
            : adjustment.Name.StartsWith("wrist_") || adjustment.Name.StartsWith("ankle_") ? 50 : 30;
        if (angle > limit) result = PoseState(BlendPose(anchor, corrected, (float)(limit / angle)), "limited natural movement");
        // Feste Gelenkabstände und Skalierung verhindern Strecken durch fremde Animationen.
        return new FrameState(start._scale, result._rotate, start._translate)._transform;
    }

    public static float[][] CorrectPose(string corrections, string[] names, float[][] localMatrices)
    {
        var offsets = ReadAdjustments(corrections);
        return names.Select((name, i) => {
            var matrix = Matrix.Identity;
            for (int element = 0; element < 16; element++) matrix[(element % 4) * 4 + element / 4] = localMatrices[i][element];
            PoseAdjustment adjustment;
            var corrected = offsets.TryGetValue(name, out adjustment) ? CorrectFrame(adjustment, matrix) : matrix;
            return Enumerable.Range(0, 16).Select(element => corrected[(element % 4) * 4 + element / 4]).ToArray();
        }).ToArray();
    }

    public static void AdjustAnimations(string template, string path, string corrections, string destination)
    {
        AdjustAnimationFile(template, path, new[] { corrections }, destination, false);
    }

    public static void AdjustMenuAnimations(string template, string path, string[] corrections, string destination)
    {
        AdjustAnimationFile(template, path, corrections, destination, true);
    }

    public static void CombineMenuAnimations(string modelPath, string animationPath, string destination)
    {
        using (var model = NodeFactory.FromFile(null, modelPath) as BRRESNode)
        using (var animations = NodeFactory.FromFile(null, animationPath) as BRRESNode)
        {
            var group = model.GetOrCreateFolder<CHR0Node>();
            foreach (var child in group.Children.ToArray()) child.Remove();
            group = model.GetOrCreateFolder<CHR0Node>();
            foreach (var animation in animations.GetFolder<CHR0Node>().Children.ToArray())
            {
                animation.Remove(); group.AddChild(animation);
            }
            model.Export(destination);
        }
    }

    static void AdjustAnimationFile(string template, string path, string[] corrections, string destination, bool separate)
    {
        var descriptions = corrections.Select(xml => { var doc = new XmlDocument { XmlResolver = null }; doc.LoadXml(xml); return doc; }).ToArray();
        using (var source = NodeFactory.FromFile(null, template) as BRRESNode)
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            var models = source.GetFolder<MDL0Node>().Children.OfType<MDL0Node>().Where(m => m.Name == "model" || m.Name == "model_lod").ToArray();
            foreach (var model in models) PrepareReference(model);
            var bones = models.SelectMany(m => m.AllBones).GroupBy(b => b.Name).ToDictionary(g => g.Key, g => g.First());
            foreach (var animation in root.GetFolder<CHR0Node>().Children.OfType<CHR0Node>())
            {
                var settings = separate ? descriptions.Single(d => d.DocumentElement.GetAttribute("animation") == animation.Name) : descriptions[0];
                var offsets = ReadAdjustments(settings.OuterXml);
                var originalAnimation = source.GetFolder<CHR0Node>().Children.OfType<CHR0Node>().Single(a => a.Name == animation.Name);
                var clipOffsets = offsets;
                if (settings.DocumentElement.GetAttribute("natural") == "true")
                {
                    float startFrame = animation.Name == settings.DocumentElement.GetAttribute("animation")
                        ? Single.Parse(settings.DocumentElement.GetAttribute("frame"), CultureInfo.InvariantCulture) : 1;
                    foreach (var model in models) { model.ApplyCHR(originalAnimation, startFrame); NormalizePoseAliases(model); }
                    clipOffsets = offsets.ToDictionary(pair => pair.Key, pair => {
                        var anchor = bones[pair.Key]._frameState._transform;
                        return new PoseAdjustment { Name = pair.Key, Natural = true, Strength = pair.Value.Strength,
                            Anchor = anchor, Offset = pair.Value.Offset * pair.Value.Anchor * anchor.Invert() };
                    });
                }
                var frames = new Dictionary<string, FrameState[]>();
                for (int frame = 0; frame < animation.FrameCount; frame++)
                {
                    foreach (var model in models) { model.ApplyCHR(originalAnimation, frame + 1); NormalizePoseAliases(model); }
                    foreach (var pair in clipOffsets)
                    {
                        if (!frames.ContainsKey(pair.Key)) frames.Add(pair.Key, new FrameState[animation.FrameCount]);
                        frames[pair.Key][frame] = PoseState(CorrectFrame(pair.Value, bones[pair.Key]._frameState._transform), animation.Name + "/" + pair.Key + "/" + frame);
                    }
                }
                foreach (var pair in frames)
                {
                    var entry = animation.FindChild(pair.Key, false) as CHR0EntryNode ?? animation.CreateEntry(pair.Key);
                    // Alle Frames werden neu geschrieben; alte doppelte Schlüssel dürfen nicht übrig bleiben.
                    var keys = entry.Keyframes;
                    keys._keyArrays = new KeyframeCollection(9, keys.FrameLimit, 1, 1, 1) { Loop = keys.Loop }._keyArrays;
                    for (int frame = 0; frame < pair.Value.Length; frame++)
                    {
                        var state = pair.Value[frame];
                        if (frame > 0)
                            for (int axis = 0; axis < 3; axis++)
                            {
                                while (state._rotate[axis] - pair.Value[frame - 1]._rotate[axis] > 180) state._rotate[axis] -= 360;
                                while (state._rotate[axis] - pair.Value[frame - 1]._rotate[axis] < -180) state._rotate[axis] += 360;
                            }
                        pair.Value[frame] = state;
                        entry.SetKeyframeOnlyScale(frame, state._scale);
                        entry.SetKeyframeOnlyRot(frame, state._rotate);
                        entry.SetKeyframeOnlyTrans(frame, state._translate);
                    }
                    // Nur konstante Spuren kürzen; Plateau-Bereinigung verändert Bewegungsübergänge.
                    for (int axis = 0; axis < 9; axis++)
                    {
                        var values = Enumerable.Range(0, animation.FrameCount).Select(frame => keys.GetFrameValue(axis, frame)).ToArray();
                        if (values.Max() - values.Min() < .0005f)
                            for (int frame = 1; frame < animation.FrameCount; frame++) entry.RemoveKeyframe(axis, frame);
                    }
                }
            }
            root.Export(destination);
        }
    }

    public static string Inspect(string path)
    {
        using (var root = NodeFactory.FromFile(null, path))
        {
            var model = Driver(root);
            return model.Name + "\n" + String.Join("\n", model.AllBones.Select(b => b.Name));
        }
    }
    public static void ExportReference(string template, string destination)
    {
        if (File.Exists(destination)) throw new IOException("Destination exists.");
        using (var root = NodeFactory.FromFile(null, template))
            WriteReference(Driver(root), destination);
    }
    public static void ConvertDriver(string template, string dae, string lod, string destination)
    {
        if (File.Exists(destination)) throw new IOException("Destination exists.");
        using (var root = NodeFactory.FromFile(null, template))
        {
            var original = Driver(root);
            var brres = (BRRESNode)root;
            var models = original.Parent.Children.OfType<MDL0Node>().Where(m => m.Name == "model" || m.Name == "model_lod").ToArray();
            if (models.Length == 0) throw new InvalidDataException("Expected RR driver model.");
            foreach (var old in models)
            using (var importer = new Collada())
            {
                Collada._importOptions = new Collada.ImportOptions();
                Collada._importOptions._mdlType = Collada.ImportOptions.MDLType.Character;
                Collada._importOptions._modelVersion = old.Version;
                string input = old.Name == "model_lod" ? lod : dae;
                var model = importer.ImportModel(input, Collada.ImportType.MDL0) as MDL0Node;
                if (model == null) throw new InvalidDataException("Internal Wii model conversion failed.");
                model.Populate(); PrepareReference(old);
                var document = new XmlDocument { XmlResolver = null };
                document.Load(input);
                var expectedMaterials = document.SelectNodes("//*[local-name()='geometry']/*[local-name()='mesh']/*[local-name()='triangles']")
                    .Cast<XmlElement>().Where(element => element.GetAttribute("count") != "0")
                    .Select(element => element.GetAttribute("material")).Distinct(StringComparer.Ordinal).OrderBy(name => name).ToArray();
                var importedMaterials = model.MaterialList.Select(material => material.Name).Distinct(StringComparer.Ordinal).OrderBy(name => name).ToArray();
                if (!expectedMaterials.SequenceEqual(importedMaterials))
                    throw new InvalidDataException("Wii conversion lost a material region. Expected: " + String.Join(", ", expectedMaterials) + "; imported: " + String.Join(", ", importedMaterials));
                if (model.AllBones.Count + model.Influences.Count > 2048)
                    throw new InvalidDataException("Wii model exceeds the Studio matrix budget. Simplify the movement assignment.");
                var expected = old.AllBones.OrderBy(b => b.Name).ToArray();
                var actual = model.AllBones.OrderBy(b => b.Name).ToArray();
                if (!expected.Select(b => b.Name).SequenceEqual(actual.Select(b => b.Name)))
                    throw new InvalidDataException("RR skeleton mismatch in " + old.Name + ": expected " + String.Join(",", expected.Select(b => b.Name)) + "; received " + String.Join(",", actual.Select(b => b.Name)));
                for (int i = 0; i < expected.Length; i++)
                {
                    string parentA = expected[i].Parent is MDL0BoneNode ? expected[i].Parent.Name : "";
                    string parentB = actual[i].Parent is MDL0BoneNode ? actual[i].Parent.Name : "";
                    if (parentA != parentB) throw new InvalidDataException("RR bone hierarchy mismatch: " + expected[i].Name);
                    for (int element = 0; element < 16; element++)
                        if (Single.IsNaN(actual[i].BindMatrix[element]) || Single.IsInfinity(actual[i].BindMatrix[element])
                            || Math.Abs(expected[i].BindMatrix[element] - actual[i].BindMatrix[element]) > .02f)
                            throw new InvalidDataException("The RR template could not be preserved during conversion (" + old.Name + ", joint " + expected[i].Name + "). Reopen movement review using the selected RR character and try again. No character package was created.");
                }
                model.Name = old.Name;
                var group = old.Parent;
                group.AddChild(model); old.Remove();
                foreach (var texture in model.TextureList)
                {
                    string name = texture.Name;
                    if (Path.GetFileName(name) != name) throw new InvalidDataException("Invalid texture name.");
                    string path = Path.Combine(Path.GetDirectoryName(input), name + ".png");
                    if (!File.Exists(path)) throw new FileNotFoundException("Converted texture missing: " + name);
                    using (var bitmap = new Bitmap(path))
                    {
                        if (bitmap.Width > 1024 || bitmap.Height > 1024) throw new InvalidDataException("Wii texture is too large.");
                        var textures = brres.GetFolder<TEX0Node>();
                        var target = textures == null ? null : textures.Children.OfType<TEX0Node>().FirstOrDefault(t => t.Name == name);
                        if (target == null) target = brres.CreateResource<TEX0Node>(name);
                        target.ReplaceRaw(TextureConverter.CMPR.EncodeTEX0Texture(bitmap, 1));
                        target.Name = name;
                    }
                }
            }
            root.Export(destination);
        }
    }
}
