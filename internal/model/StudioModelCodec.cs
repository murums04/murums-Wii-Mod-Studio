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
using BrawlLib.Wii.Graphics;
using BrawlLib.Modeling.Collada;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.SSBB.Types;
public static class StudioModelCodec
{
    static bool IsWindowMaterial(MDL0MaterialNode material)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(material.Name,
            @"(^|_)(screen|windscreen|windshield|window|glass)([0-9_]|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    static bool IsTireMaterial(MDL0MaterialNode material)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(material.Name, @"(^|_)(tire|tyre)([0-9]*$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    static MDL0MaterialNode[] WindowMaterials(BRRESNode root, bool windows = true)
    {
        var models = root.GetFolder<MDL0Node>();
        if (models == null) return new MDL0MaterialNode[0];
        var result = new List<MDL0MaterialNode>();
        foreach (var model in models.Children.OfType<MDL0Node>())
        {
            model.Populate();
            if (model.MaterialList != null) result.AddRange(model.MaterialList.OfType<MDL0MaterialNode>().Where(m => windows ? IsWindowMaterial(m) : IsTireMaterial(m)));
        }
        return result.ToArray();
    }
    public static string[] VehicleWindows(string path) { return VehicleMaterials(path, true); }
    public static string[] VehicleTires(string path) { return VehicleMaterials(path, false); }
    static string[] VehicleMaterials(string path, bool windows)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            if (root == null) throw new InvalidDataException("Expected vehicle BRRES.");
            return WindowMaterials(root, windows).Select(m => m.Name).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
    static bool HasWindowColour(MDL0MaterialNode material)
    {
        var shader = material.ShaderNode;
        if (shader == null || shader.Children.Count == 0) return false;
        var stage = shader.Children.OfType<MDL0TEVStageNode>().First();
        return stage.ColorSelectionD == ColorArg.Zero &&
            (stage.ColorSelectionA == ColorArg.Color2 && stage.ColorSelectionB == ColorArg.TextureColor && stage.ColorSelectionC == ColorArg.ConstantColorSelection && stage.ConstantColorSelection == TevKColorSel.Constant1_8
            || stage.ColorSelectionA == ColorArg.Zero && stage.ColorSelectionB == ColorArg.Color2 && (stage.ColorSelectionC == ColorArg.One || stage.ColorSelectionC == ColorArg.RasterColor));
    }
    static bool HasTireColour(MDL0MaterialNode material)
    {
        var stage = material.ShaderNode == null ? null : material.ShaderNode.Children.OfType<MDL0TEVStageNode>().FirstOrDefault();
        return stage != null && stage.ColorSelectionA == ColorArg.Zero && stage.ColorSelectionB == ColorArg.TextureColor
            && stage.ColorSelectionC == ColorArg.Color2 && stage.ColorSelectionD == ColorArg.Zero;
    }
    public static int? VehicleWindowColour(string path) { return VehicleColour(path, true); }
    public static int? VehicleTireColour(string path) { return VehicleColour(path, false); }
    static int? VehicleColour(string path, bool windows)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            if (root == null) throw new InvalidDataException("Expected vehicle BRRES.");
            var material = WindowMaterials(root, windows).FirstOrDefault(m => windows ? HasWindowColour(m) : HasTireColour(m));
            if (material == null) return null;
            var color = material.Color2;
            return Color.FromArgb(255, Math.Max(0, Math.Min(255, (int)color.R)), Math.Max(0, Math.Min(255, (int)color.G)), Math.Max(0, Math.Min(255, (int)color.B))).ToArgb();
        }
    }
    public static void SetVehicleWindowColour(string path, int argb, string destination) { SetVehicleColour(path, argb, destination, true); }
    public static void SetVehicleTireColour(string path, int argb, string destination) { SetVehicleColour(path, argb, destination, false); }
    static void SetVehicleColour(string path, int argb, string destination, bool windows)
    {
        if (File.Exists(destination)) throw new IOException("Destination exists.");
        var temporary = new List<string>();
        try
        {
            using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
            {
                if (root == null) throw new InvalidDataException("Expected vehicle BRRES.");
                var materials = WindowMaterials(root, windows);
                if (materials.Length == 0) throw new InvalidDataException("No separate colourable materials found.");
                var color = Color.FromArgb(argb);
                foreach (var material in materials)
                {
                    var shader = material.ShaderNode;
                    if (shader == null || shader.Children.Count == 0) throw new InvalidDataException("Material shader is missing.");
                    if (!(windows ? HasWindowColour(material) : HasTireColour(material)))
                    {
                        if (shader.Children.OfType<MDL0TEVStageNode>().Any(s =>
                            new[] { s.ColorSelectionA, s.ColorSelectionB, s.ColorSelectionC, s.ColorSelectionD }.Any(a => a == ColorArg.Color2 || a == ColorArg.Alpha2) ||
                            new[] { s.AlphaSelectionA, s.AlphaSelectionB, s.AlphaSelectionC, s.AlphaSelectionD }.Any(a => a == AlphaArg.Alpha2) || s.ColorRegister == TevColorRegID.Color2))
                            throw new InvalidDataException("This material already uses the colour register required for tinting.");
                        string copyPath = Path.Combine(Path.GetDirectoryName(destination), Guid.NewGuid().ToString("N") + ".shader");
                        temporary.Add(copyPath); shader.Export(copyPath);
                        var copy = (MDL0ShaderNode)NodeFactory.FromFile(null, copyPath, typeof(MDL0ShaderNode));
                        shader.Parent.AddChild(copy);
                        copy.Name = "StudioColour_" + material.Name;
                        material.ShaderNode = copy;
                        var stage = copy.Children.OfType<MDL0TEVStageNode>().First();
                        if (!windows)
                        {
                            stage.ColorSelectionA = ColorArg.Zero;
                            stage.ColorSelectionB = ColorArg.TextureColor;
                            stage.ColorSelectionC = ColorArg.Color2;
                        }
                        else if (copy.Children.Count == 1)
                        {
                            stage.ColorSelectionA = ColorArg.Color2;
                            stage.ColorSelectionB = ColorArg.TextureColor;
                            stage.ColorSelectionC = ColorArg.ConstantColorSelection;
                            stage.ConstantColorSelection = TevKColorSel.Constant1_8;
                        }
                        else
                        {
                            stage.ColorSelectionA = ColorArg.Zero;
                            stage.ColorSelectionB = ColorArg.Color2;
                            stage.ColorSelectionC = stage.RasterColor == ColorSelChan.Zero ? ColorArg.One : ColorArg.RasterColor;
                        }
                        stage.ColorSelectionD = ColorArg.Zero;
                        stage.ColorBias = Bias.Zero;
                        stage.ColorScale = TevScale.MultiplyBy1;
                        stage.ColorOperation = TevColorOp.Add;
                        stage.ColorClamp = true;
                    }
                    material.Color2 = new GXColorS10(material.Color2.A, color.R, color.G, color.B);
                }
                root.Rebuild(); root.Export(destination);
            }
        }
        finally { foreach (string file in temporary) if (File.Exists(file)) File.Delete(file); }
    }

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
        foreach (var bone in model.AllBones.Where(b => !FiniteBind(b) && b.Children.Count == 0 &&
            b.Users.Count == 0 && b.VisibilityDrawCalls.Length == 0).ToArray())
        {
            if (!PermanentlyCollapsed(model, bone, animations)) continue;
            // Dauerhaft unsichtbare Hilfsobjekte besitzen absichtlich keine invertierbare Bindepose.
            foreach (var polygon in bone.SingleBindObjects) polygon.Remove(false);
            bone.Remove();
            model._linker.RegenerateBoneCache();
        }
        var invalid = model.AllBones.FirstOrDefault(b => !FiniteBind(b));
        if (invalid != null)
            throw new InvalidDataException("The RR reference contains invalid joint data: " + invalid.Name +
                ". The joint is in use or has no verified original counterpart. Choose an intact RR reference.");
    }

    static bool PermanentlyCollapsed(MDL0Node model, MDL0BoneNode bone, BRESGroupNode animations)
    {
        Func<Matrix, bool> collapsed = matrix => Enumerable.Range(0, 16).All(i =>
            !Single.IsNaN(matrix[i]) && !Single.IsInfinity(matrix[i])) &&
            new[] { 0, 1, 2, 4, 5, 6, 8, 9, 10 }.All(i => matrix[i] == 0);
        if (!collapsed(bone.BindMatrix)) return false;
        var clips = animations == null ? new CHR0Node[0] : animations.Children.OfType<CHR0Node>().ToArray();
        if (clips.Any(a => a.FrameCount < 0) || clips.Sum(a => (long)a.FrameCount) > 100000) return false;
        try
        {
            foreach (var clip in clips)
                for (int frame = 1; frame <= clip.FrameCount; frame++)
                {
                    model.ApplyCHR(clip, frame);
                    if (!collapsed(bone._frameMatrix)) return false;
                }
            return true;
        }
        finally { model.ApplyCHR(null, 0); }
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
    public static float[][] ModelVertices(string path, string name)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            var model = root.GetFolder<MDL0Node>().Children.OfType<MDL0Node>().Single(m => m.Name == name);
            PrepareReference(model);
            model.ApplyCHR(null, 0);
            return model.PolygonGroup.Children.OfType<MDL0ObjectNode>().SelectMany(p => p.Vertices)
                .Select(v => new[] { v.WeightedPosition._x, v.WeightedPosition._y, v.WeightedPosition._z }).ToArray();
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
            var local = parent == null ? bone.BindMatrix : parent.BindMatrix.Invert() * bone.BindMatrix;
            // RR kann gespeicherte Bindematrizen besitzen, die von den lokalen SRT-Werten abweichen.
            foreach (var transform in element.ChildNodes.OfType<XmlElement>().Where(node =>
                node.LocalName == "translate" || node.LocalName == "rotate" || node.LocalName == "scale" || node.LocalName == "matrix").ToArray())
                element.RemoveChild(transform);
            var matrix = document.CreateElement("matrix", element.NamespaceURI);
            matrix.InnerText = String.Join(" ", Enumerable.Range(0, 16)
                .Select(i => local[(i % 4) * 4 + i / 4].ToString("R", CultureInfo.InvariantCulture)));
            element.PrependChild(matrix);
        }
        foreach (var material in model.MaterialList.OfType<MDL0MaterialNode>().Where(IsWindowMaterial))
        {
            var effect = document.SelectNodes("//*[local-name()='effect']").Cast<XmlElement>()
                .FirstOrDefault(e => e.GetAttribute("id") == material.Name + "-fx");
            if (effect == null) continue;
            var diffuse = effect.SelectSingleNode(".//*[local-name()='diffuse']") as XmlElement;
            if (diffuse == null) continue;
            if (HasWindowColour(material))
            {
                diffuse.RemoveAll();
                var color = document.CreateElement("color", diffuse.NamespaceURI);
                var tint = material.Color2;
                color.InnerText = String.Join(" ", new[] { tint.R / 255f, tint.G / 255f, tint.B / 255f, 1f }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                diffuse.AppendChild(color);
            }
            float? opacity = WindowPreviewOpacity(material);
            if (!opacity.HasValue) continue;
            var shading = diffuse.ParentNode;
            foreach (XmlNode previous in shading.SelectNodes("*[local-name()='transparent' or local-name()='transparency']"))
                shading.RemoveChild(previous);
            var transparent = document.CreateElement("transparent", diffuse.NamespaceURI);
            transparent.SetAttribute("opaque", "A_ONE");
            var alpha = document.CreateElement("color", diffuse.NamespaceURI);
            alpha.InnerText = "1 1 1 " + opacity.Value.ToString("R", CultureInfo.InvariantCulture);
            transparent.AppendChild(alpha);
            shading.AppendChild(transparent);
            var transparency = document.CreateElement("transparency", diffuse.NamespaceURI);
            var amount = document.CreateElement("float", diffuse.NamespaceURI);
            amount.InnerText = "1";
            transparency.AppendChild(amount);
            shading.AppendChild(transparency);
        }
        foreach (var material in model.MaterialList.OfType<MDL0MaterialNode>().Where(m => IsTireMaterial(m) && HasTireColour(m)))
        {
            var effect = document.SelectNodes("//*[local-name()='effect']").Cast<XmlElement>()
                .FirstOrDefault(e => e.GetAttribute("id") == material.Name + "-fx");
            var surface = effect == null ? null : effect.SelectSingleNode(".//*[local-name()='surface']/*[local-name()='init_from']");
            if (surface == null) continue;
            var original = document.SelectNodes("//*[local-name()='image']").Cast<XmlElement>().FirstOrDefault(e => e.GetAttribute("id") == surface.InnerText);
            var textures = ((BRRESNode)model.Parent.Parent).GetFolder<TEX0Node>();
            var texture = textures == null ? null : textures.Children.OfType<TEX0Node>().FirstOrDefault(t => t.Name == material.Children[0].Name);
            if (original == null || texture == null) continue;
            string name = "tire-colour-" + Guid.NewGuid().ToString("N");
            using (var bitmap = texture.GetImage(0))
            {
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, pixel.R * material.Color2.R / 255, pixel.G * material.Color2.G / 255, pixel.B * material.Color2.B / 255));
                }
                bitmap.Save(Path.Combine(Path.GetDirectoryName(destination), name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            var image = (XmlElement)original.CloneNode(true);
            image.SetAttribute("id", name);
            var imagePath = image.SelectSingleNode("*[local-name()='init_from']/*[local-name()='ref']") ?? image.SelectSingleNode("*[local-name()='init_from']");
            imagePath.InnerText = name + ".png";
            foreach (XmlElement instance in effect.SelectNodes(".//*[local-name()='instance_image']"))
                if (instance.GetAttribute("url") == "#" + original.GetAttribute("id")) instance.SetAttribute("url", "#" + name);
            original.ParentNode.AppendChild(image);
            surface.InnerText = name;
        }
        document.Save(destination);
    }
    static float? WindowPreviewOpacity(MDL0MaterialNode material)
    {
        if (!material.EnableBlend || material.ShaderNode == null) return null;
        var stage = material.ShaderNode.Children.OfType<MDL0TEVStageNode>().LastOrDefault();
        // RR-Scheiben geben ihren konstanten Alphawert in der letzten Stufe aus.
        if (stage == null || stage.AlphaSelectionA != AlphaArg.ConstantAlphaSelection
            || stage.AlphaSelectionC != AlphaArg.Zero
            || stage.AlphaSelectionD != AlphaArg.Zero || stage.AlphaOperation != TevAlphaOp.Add
            || stage.AlphaBias != Bias.Zero || stage.AlphaScale != TevScale.MultiplyBy1
            || stage.AlphaRegister.ToString() != "OutputAlpha") return null;
        string selection = stage.ConstantAlphaSelection.ToString();
        var colors = new[] { material.ConstantColor0, material.ConstantColor1, material.ConstantColor2, material.ConstantColor3 };
        for (int i = 0; i < colors.Length; i++)
            if (selection == "ConstantColor" + i + "_Alpha") return colors[i].A / 255f;
        return null;
    }

    public static Dictionary<string, float[][][]> ReadJointAnimations(string path, string modelName,
        string[] names, System.Threading.CancellationToken cancellation)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            if (root == null) throw new InvalidDataException("Expected a BRRES model.");
            var model = root.GetFolder<MDL0Node>().Children.OfType<MDL0Node>().Single(m => m.Name == modelName);
            PrepareReference(model);
            var group = root.GetFolder<CHR0Node>();
            var result = new Dictionary<string, float[][][]>();
            if (group == null) return result;
            var bones = model.AllBones.ToDictionary(b => b.Name);
            int total = 0;
            foreach (var animation in group.Children.OfType<CHR0Node>())
            {
                cancellation.ThrowIfCancellationRequested();
                if (animation.FrameCount < 0 || animation.FrameCount > 100000 - total)
                    throw new InvalidDataException("Animation is too large to check: " + animation.Name);
                total += animation.FrameCount;
                var frames = new float[animation.FrameCount][][];
                for (int frame = 0; frame < frames.Length; frame++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    model.ApplyCHR(animation, frame + 1);
                    NormalizePoseAliases(model);
                    frames[frame] = names.Select(name => {
                        MDL0BoneNode bone, alias;
                        bones.TryGetValue(name, out bone);
                        if ((bone == null || !UsablePose(bone._frameMatrix)) && bones.TryGetValue("pcd_" + name, out alias))
                            bone = alias;
                        return bone == null ? null : new[] { bone._frameMatrix[12], bone._frameMatrix[13], bone._frameMatrix[14] };
                    }).ToArray();
                }
                result.Add(animation.Name, frames);
            }
            return result;
        }
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
            var clips = animations == null ? new CHR0Node[0] : animations.Children.OfType<CHR0Node>().ToArray();
            var animation = clips.FirstOrDefault(a => a.Name == animationName);
            if (animation == null && clips.Length != 0) throw new InvalidDataException("RR animation missing: " + animationName);
            // Statische Originalmodelle verwenden ihre Bindepose ohne erfundene Animation.
            float poseFrame = animation == null ? 0 : Math.Max(1, Math.Min(animation.FrameCount, frame));
            model.ApplyCHR(animation, poseFrame);
            NormalizePoseAliases(model);
            var doc = new XmlDocument { XmlResolver = null };
            var pose = doc.CreateElement("pose"); doc.AppendChild(pose);
            pose.SetAttribute("animation", animation == null ? "" : animation.Name);
            pose.SetAttribute("available", String.Join("|", clips.Select(a => a.Name)));
            pose.SetAttribute("frames", (animation == null ? 1 : animation.FrameCount).ToString(CultureInfo.InvariantCulture));
            foreach (var bone in model.AllBones)
            {
                var node = doc.CreateElement("bone"); pose.AppendChild(node);
                node.SetAttribute("name", bone.Name);
                node.SetAttribute("parent", bone.Parent is MDL0BoneNode ? bone.Parent.Name : "");
                bool foot = bone.Name.StartsWith("ankle_", StringComparison.Ordinal) || bone.Name.StartsWith("pcd_ankle_", StringComparison.Ordinal);
                if (foot || bone.Name.StartsWith("wrist_", StringComparison.Ordinal) || bone.Name.StartsWith("pcd_wrist_", StringComparison.Ordinal))
                {
                    var hand = model.PolygonGroup.Children.OfType<MDL0ObjectNode>().SelectMany(p => p.Vertices)
                        .Where(v => v.GetBoneWeights() != null && v.GetBoneWeights().Where(w => w.Bone == bone).Sum(w => w.Weight) >= .75f)
                        .Select(v => v.WeightedPosition).ToArray();
                    if (hand.Length >= 4)
                    {
                        var center = new[] { (hand.Min(v => v._x) + hand.Max(v => v._x)) * .5f,
                            foot ? hand.Min(v => v._y) : (hand.Min(v => v._y) + hand.Max(v => v._y)) * .5f,
                            (hand.Min(v => v._z) + hand.Max(v => v._z)) * .5f };
                        node.SetAttribute("contact", String.Join(" ", center.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                    }
                }
                node.SetAttribute("bind", String.Join(" ", Enumerable.Range(0, 16).Select(i => bone.BindMatrix[(i % 4) * 4 + i / 4].ToString("R", CultureInfo.InvariantCulture))));
                var skin = bone._frameMatrix * bone.InverseBindMatrix;
                node.SetAttribute("skin", String.Join(" ", Enumerable.Range(0, 16).Select(i => skin[(i % 4) * 4 + i / 4].ToString("R", CultureInfo.InvariantCulture))));
                node.SetAttribute("joint", String.Join(" ", new[] { bone._frameMatrix[12], bone._frameMatrix[13], bone._frameMatrix[14] }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
            }
            if (!String.IsNullOrEmpty(previewPath))
            {
                model.ApplyCHR(animation, poseFrame);
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
        internal bool Natural, Continuous, RootMotion, StableMenu;
        internal string Name, Parent;
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
        return document.DocumentElement.ChildNodes.OfType<XmlElement>().Where(n => n.Name == "bone").ToDictionary(n => n.GetAttribute("name"), n => new PoseAdjustment {
            Offset = ParsePoseMatrix(n.GetAttribute("matrix")),
            Anchor = ParsePoseMatrix(n.GetAttribute("anchor")),
            Strength = strength,
            Natural = document.DocumentElement.GetAttribute("natural") == "true",
            StableMenu = document.DocumentElement.GetAttribute("stableMenu") == "true",
            Parent = n.GetAttribute("parent"),
            Continuous = document.DocumentElement.GetAttribute("continuous") == "true",
            RootMotion = n.GetAttribute("rootMotion") == "true",
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
        string name = adjustment.Name.StartsWith("pcd_", StringComparison.Ordinal) ? adjustment.Name.Substring(4) : adjustment.Name;
        // Im stabilen Menue bleiben Becken und Beine in der gewaehlten Haltung.
        if (adjustment.StableMenu && (adjustment.RootMotion || name.StartsWith("leg_") || name.StartsWith("ankle_")))
            return anchor;
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
        if (adjustment.Continuous)
        {
            var translation = start._translate;
            if (adjustment.RootMotion) translation += PoseDirection(adjustment.Offset, to._translate - from._translate) * adjustment.Strength;
            return new FrameState(start._scale, PoseState(rotation, "continuous movement")._rotate, translation)._transform;
        }
        var result = PoseState(corrected, "natural movement");
        var a = RotationQuaternion(anchor, start._scale);
        var b = RotationQuaternion(corrected, result._scale);
        double dot = Math.Min(1, Math.Abs(a.Select((v, i) => v * b[i]).Sum()));
        double angle = 2 * Math.Acos(dot) * 180 / Math.PI;
        double limit = adjustment.Name.StartsWith("arm_") || adjustment.Name.StartsWith("leg_") ? 65
            : adjustment.Name.StartsWith("wrist_") || adjustment.Name.StartsWith("ankle_") ? 50 : 30;
        if (adjustment.StableMenu && name == "spin") limit = 8;
        if (adjustment.StableMenu && name.StartsWith("face_")) limit = 15;
        if (angle > limit) result = PoseState(BlendPose(anchor, corrected, (float)(limit / angle)), "limited natural movement");
        // Feste Gelenkabstände und Skalierung verhindern Strecken durch fremde Animationen.
        return new FrameState(start._scale, result._rotate, start._translate)._transform;
    }

    public static float[][] CorrectPose(string corrections, string[] names, float[][] localMatrices)
    {
        return CorrectAnimationPose(corrections, names, localMatrices, null, 0);
    }

    sealed class AnimationKey
    {
        internal int Frame;
        internal Vector3 Rotation, Position;
    }

    static Dictionary<string, AnimationKey[]> ReadAnimationKeys(string corrections, string clip)
    {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(corrections);
        return document.DocumentElement.ChildNodes.OfType<XmlElement>()
            .Where(n => n.Name == "animation" && n.GetAttribute("name") == clip)
            .SelectMany(n => n.ChildNodes.OfType<XmlElement>()).GroupBy(n => n.GetAttribute("bone"))
            .ToDictionary(group => group.Key, group => group.Select(n => new AnimationKey {
                Frame = Int32.Parse(n.GetAttribute("frame"), CultureInfo.InvariantCulture),
                Rotation = ParseKeyVector(n.GetAttribute("rotation")),
                Position = ParseKeyVector(n.GetAttribute("position"))
            }).OrderBy(k => k.Frame).ToArray());
    }

    static Vector3 ParseKeyVector(string text)
    {
        var values = text.Split(' ').Select(v => Single.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != 3 || values.Any(v => Single.IsNaN(v) || Single.IsInfinity(v)))
            throw new InvalidDataException("Invalid animation keyframe vector.");
        return new Vector3(values[0], values[1], values[2]);
    }

    static Matrix ApplyAnimationKeys(Matrix matrix, string bone, float frame, Dictionary<string, AnimationKey[]> tracks)
    {
        AnimationKey[] keys;
        if (!tracks.TryGetValue(bone, out keys) || keys.Length == 0) return matrix;
        var left = keys.LastOrDefault(k => k.Frame <= frame) ?? keys[0];
        var right = keys.FirstOrDefault(k => k.Frame >= frame) ?? keys[keys.Length - 1];
        float amount = left.Frame == right.Frame ? 0 : (frame - left.Frame) / (right.Frame - left.Frame);
        var state = PoseState(matrix, "edited animation: " + bone);
        for (int axis = 0; axis < 3; axis++)
        {
            state._rotate[axis] += left.Rotation[axis] + amount * (right.Rotation[axis] - left.Rotation[axis]);
            state._translate[axis] += left.Position[axis] + amount * (right.Position[axis] - left.Position[axis]);
        }
        return new FrameState(state._scale, state._rotate, state._translate)._transform;
    }

    public static float[][] CorrectAnimationPose(string corrections, string[] names, float[][] localMatrices, string animation, float frame)
    {
        var offsets = ReadAdjustments(corrections);
        var tracks = ReadAnimationKeys(corrections, animation);
        var source = names.Select((name, i) => new { name, i }).ToDictionary(p => p.name, p => {
            var matrix = Matrix.Identity;
            for (int element = 0; element < 16; element++) matrix[(element % 4) * 4 + element / 4] = localMatrices[p.i][element];
            return matrix;
        });
        var settings = new XmlDocument { XmlResolver = null }; settings.LoadXml(corrections);
        int count;
        if (!Int32.TryParse(settings.DocumentElement.GetAttribute("frames"), out count)) count = 1;
        var result = AnimationPose(offsets, source, settings.DocumentElement, animation, frame, count);
        return names.Select(name => {
            var corrected = ApplyAnimationKeys(result[name], name, frame, tracks);
            return Enumerable.Range(0, 16).Select(element => corrected[(element % 4) * 4 + element / 4]).ToArray();
        }).ToArray();
    }

    static Dictionary<string, Matrix> PoseWorlds(Dictionary<string, Matrix> local, Dictionary<string, PoseAdjustment> offsets)
    {
        var result = new Dictionary<string, Matrix>();
        Func<string, Matrix> world = null;
        world = name => {
            Matrix value;
            if (result.TryGetValue(name, out value)) return value;
            PoseAdjustment adjustment;
            string parent = offsets.TryGetValue(name, out adjustment) ? adjustment.Parent : null;
            value = String.IsNullOrEmpty(parent) || !local.ContainsKey(parent) ? local[name] : world(parent) * local[name];
            result.Add(name, value);
            return value;
        };
        foreach (string name in local.Keys) world(name);
        return result;
    }
    static Vector3 Position(Matrix m) { return new Vector3(m[12], m[13], m[14]); }
    static Vector3 PoseDirection(Matrix matrix, Vector3 direction)
    {
        return new Vector3(matrix[0] * direction._x + matrix[4] * direction._y + matrix[8] * direction._z,
            matrix[1] * direction._x + matrix[5] * direction._y + matrix[9] * direction._z,
            matrix[2] * direction._x + matrix[6] * direction._y + matrix[10] * direction._z);
    }
    static Matrix AlignSegment(Matrix world, Vector3 before, Vector3 after, Vector3 position)
    {
        Matrix rotation = Matrix.Identity;
        if (before.TrueDistance() > .0001 && after.TrueDistance() > .0001)
        {
            var a = before.Normalize(); var b = after.Normalize();
            if (a.Dot(b) < -.99999f)
            {
                var axis = a.Cross(Math.Abs(a._x) < .8 ? new Vector3(1,0,0) : new Vector3(0,1,0)).Normalize();
                for (int row=0;row<3;row++) for (int column=0;column<3;column++)
                    rotation[column*4+row]=2*axis[row]*axis[column]-(row==column?1:0);
            }
            else if (a.Dot(b) < .99999f) rotation = Matrix.AxisAngleMatrix(a,b);
        }
        var result = rotation * world;
        result[12]=position._x; result[13]=position._y; result[14]=position._z;
        return result;
    }
    static Dictionary<string, Matrix> AnimationPose(Dictionary<string, PoseAdjustment> offsets,
        Dictionary<string, Matrix> source, XmlElement settings, string animation, float frame, int count)
    {
        string style = settings.GetAttribute("humanStyle");
        if (String.IsNullOrEmpty(style)) return CorrectSkeleton(offsets, source);
        if (style != "neutral" && style != "feminine" && style != "masculine")
            throw new InvalidDataException("Unknown human animation style.");
        var local = source.ToDictionary(p => p.Key, p => offsets.ContainsKey(p.Key)
            ? offsets[p.Key].Offset * offsets[p.Key].Anchor : p.Value);
        var anchor = PoseWorlds(local, offsets);
        Func<string, string> bone = name => offsets.Keys.FirstOrDefault(n => HumanBoneName(n) == name);
        string leftShoulder = bone("arm_l1"), rightShoulder = bone("arm_r1"), head = bone("face_1"), hip = bone("skl_root");
        if (leftShoulder == null || rightShoulder == null || head == null || hip == null)
            throw new InvalidDataException("Human animation requires a complete human joint template.");
        var across = (Position(anchor[leftShoulder]) - Position(anchor[rightShoulder])).Normalize();
        var up = (Position(anchor[head]) - Position(anchor[hip])).Normalize();
        var forward = across.Cross(up).Normalize();
        up = forward.Cross(across).Normalize();
        float strength = offsets.Values.First().Strength;
        float phase = count <= 1 ? .5f : Math.Max(0, Math.Min(1, frame / (count - 1)));
        animation = HumanClipName(animation);
        // Lenkframes sind Eingabewerte; nur zeitliche Bewegungen sanft ein- und auslaufen lassen.
        if (!HumanSteeringClip(animation)) phase = phase * phase * phase * (10 + phase * (-15 + phase * 6));
        double wave = Math.Sin(phase * Math.PI * 2), pulse = Math.Pow(Math.Sin(phase * Math.PI), 2);
        bool menu = settings.GetAttribute("menu") == "true";
        bool confirm = animation == "sel_gut";
        bool victory = animation == "1st" || animation == "good" || animation == "gut";
        bool trick = animation != null && (animation.StartsWith("sjump_m", StringComparison.Ordinal) || animation.StartsWith("sjump_s", StringComparison.Ordinal) && animation != "sjump_st");
        bool feminine = style == "feminine", masculine = style == "masculine";
        var released = new HashSet<string>();
        Action<string, Vector3, double> rotate = (name, axis, degrees) => {
            foreach (string actual in offsets.Keys.Where(n => HumanBoneName(n) == name))
                RotateHumanBone(local, offsets, actual, axis, (float)(degrees * strength));
        };
        if (menu)
        {
            rotate("spin", up, wave * (feminine ? 2 : masculine ? .7 : 1));
            rotate("spin", across, wave * .4);
            rotate("face_1", up, -wave * 2);
            rotate("face_1", forward, wave * (feminine ? 1.5 : .5));
            rotate("arm_l1", forward, wave * .8);
            rotate("arm_r1", forward, -wave * .8);
        }
        else
        {
            double lean = animation == "drive" || animation == "dash" ? (phase * 2 - 1) * 5
                : animation == "drift_l" ? 7 * pulse : animation == "drift_r" ? -7 * pulse : 0;
            rotate("spin", forward, lean);
            if (animation == "back") { rotate("spin", up, 8 * pulse); rotate("face_1", up, 28 * pulse); }
            else if (animation == "wheelie") rotate("spin", across, -7 * pulse);
            else if (animation == "jump_st" || animation == "jump_ed" || animation == "sjump_st" || animation == "sjump_ed")
                rotate("spin", across, 6 * pulse);
            else if (animation == "damage" || animation == "bad") rotate("face_1", across, 7 * pulse);
            else rotate("face_1", across, wave * .6);
        }
        if (confirm || victory || trick)
        {
            string side = feminine ? "l" : "r";
            foreach (string arm in victory && masculine ? new[] { "l", "r" } : new[] { side })
            {
                float sign = arm == "l" ? 1 : -1;
                var upper = across * (sign * (victory ? .65f : masculine ? .35f : .6f))
                    + up * (victory ? .4f : -.4f) + forward * .15f;
                var lower = up + across * (-sign * .15f) + forward * (masculine ? .3f : .1f);
                if (trick) { upper = across * (sign * .7f) + up * .15f; lower = up + forward * .2f; }
                PoseHumanArm(local, offsets, arm, upper, lower, (float)pulse * strength);
                rotate("wrist_" + arm + "1", forward, feminine || !masculine
                    ? sign * 12 * Math.Sin(phase * Math.PI * 6) * pulse : sign * 8 * pulse);
                released.Add("wrist_" + arm + "1");
            }
            rotate("face_1", across, -3 * pulse);
        }
        if (animation == "throw_f" || animation == "throw_b" || animation == "throw_f_ed")
        {
            rotate("arm_r1", across, (animation == "throw_b" ? 55 : -70) * pulse);
            rotate("arm_r2", across, -25 * pulse);
            released.Add("wrist_r1");
        }
        if (!menu)
        {
            var contacts = offsets.Values.Where(a => (HumanBoneName(a.Name).StartsWith("wrist_") || HumanBoneName(a.Name).StartsWith("ankle_"))
                && !released.Contains(HumanBoneName(a.Name))).ToArray();
            LimitHumanLean(local, offsets, anchor, contacts);
            foreach (var end in contacts) SolvePoseLimb(local, offsets, end, Position(anchor[end.Name]));
        }
        return local;
    }

    static string HumanClipName(string animation)
    {
        return animation == null ? null : animation.Substring(animation.LastIndexOf('-') + 1);
    }

    static bool HumanSteeringClip(string animation)
    {
        string name = HumanClipName(animation);
        return name == "drive" || name == "dash";
    }

    static void LimitHumanLean(Dictionary<string, Matrix> local, Dictionary<string, PoseAdjustment> offsets,
        Dictionary<string, Matrix> anchor, PoseAdjustment[] contacts)
    {
        var spine = offsets.Values.Where(a => HumanBoneName(a.Name) == "spin").ToArray();
        var requested = spine.ToDictionary(a => a.Name, a => local[a.Name]);
        float low = 0, high = 1;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float amount = attempt == 0 ? 1 : (low + high) * .5f;
            foreach (var bone in spine) local[bone.Name] = BlendPose(bone.Offset * bone.Anchor, requested[bone.Name], amount);
            var worlds = PoseWorlds(local, offsets);
            bool reachable = contacts.All(end => {
                PoseAdjustment middle, start;
                if (!offsets.TryGetValue(end.Parent, out middle) || !offsets.TryGetValue(middle.Parent, out start)) return true;
                var a = Position(worlds[start.Name]); var b = Position(worlds[middle.Name]); var c = Position(worlds[end.Name]);
                float upper = (b - a).TrueDistance(), lower = (c - b).TrueDistance();
                float distance = (Position(anchor[end.Name]) - a).TrueDistance();
                float maximumReach = upper + lower;
                if (HumanBoneName(end.Name).StartsWith("wrist_", StringComparison.Ordinal))
                {
                    var upperAnchor = Position(anchor[middle.Name]) - Position(anchor[start.Name]);
                    var lowerAnchor = Position(anchor[end.Name]) - Position(anchor[middle.Name]);
                    double originalBend = Math.Acos(Math.Max(-1, Math.Min(1,
                        upperAnchor.Normalize().Dot(lowerAnchor.Normalize()))));
                    // Eine leichte Beugung verhindert instabile, einseitig durchgedrückte Ellbogen.
                    double minimumBend = Math.Min(originalBend, 12 * Math.PI / 180);
                    maximumReach = (float)Math.Sqrt(upper * upper + lower * lower
                        + 2 * upper * lower * Math.Cos(minimumBend));
                }
                return distance <= maximumReach + .0001 && distance >= Math.Abs(upper - lower) - .0001;
            });
            if (reachable) { low = amount; if (attempt == 0) return; }
            else high = amount;
        }
        foreach (var bone in spine) local[bone.Name] = BlendPose(bone.Offset * bone.Anchor, requested[bone.Name], low);
    }

    static void PoseHumanArm(Dictionary<string, Matrix> local, Dictionary<string, PoseAdjustment> offsets,
        string side, Vector3 upper, Vector3 lower, float amount)
    {
        foreach (var wrist in offsets.Values.Where(a => HumanBoneName(a.Name) == "wrist_" + side + "1"))
        {
            PoseAdjustment elbow, shoulder;
            if (!offsets.TryGetValue(wrist.Parent, out elbow) || !offsets.TryGetValue(elbow.Parent, out shoulder)) continue;
            var originalShoulder = local[shoulder.Name]; var originalElbow = local[elbow.Name];
            var worlds = PoseWorlds(local, offsets);
            var start = Position(worlds[shoulder.Name]); var middle = Position(worlds[elbow.Name]);
            var oriented = AlignSegment(worlds[shoulder.Name], middle - start, upper, start);
            local[shoulder.Name] = String.IsNullOrEmpty(shoulder.Parent) ? oriented : worlds[shoulder.Parent].Invert() * oriented;
            worlds = PoseWorlds(local, offsets);
            middle = Position(worlds[elbow.Name]);
            oriented = AlignSegment(worlds[elbow.Name], Position(worlds[wrist.Name]) - middle, lower, middle);
            var elbowPose = worlds[shoulder.Name].Invert() * oriented;
            local[shoulder.Name] = BlendPose(originalShoulder, local[shoulder.Name], amount);
            local[elbow.Name] = BlendPose(originalElbow, elbowPose, amount);
        }
    }

    static string HumanBoneName(string name)
    {
        return name.StartsWith("pcd_", StringComparison.Ordinal) ? name.Substring(4) : name;
    }

    static void RotateHumanBone(Dictionary<string, Matrix> local, Dictionary<string, PoseAdjustment> offsets,
        string name, Vector3 axis, float degrees)
    {
        if (Math.Abs(degrees) < .000001) return;
        axis = axis.Normalize();
        double radians = degrees * Math.PI / 180, c = Math.Cos(radians), s = Math.Sin(radians), t = 1 - c;
        var rotation = Matrix.Identity;
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                rotation[column * 4 + row] = (float)(t * axis[row] * axis[column] + (row == column ? c : 0));
        rotation[4] -= (float)(s * axis._z); rotation[8] += (float)(s * axis._y);
        rotation[1] += (float)(s * axis._z); rotation[9] -= (float)(s * axis._x);
        rotation[2] -= (float)(s * axis._y); rotation[6] += (float)(s * axis._x);
        var worlds = PoseWorlds(local, offsets);
        var world = rotation * worlds[name];
        for (int axisIndex = 0; axisIndex < 3; axisIndex++) world[12 + axisIndex] = worlds[name][12 + axisIndex];
        string parent = offsets[name].Parent;
        local[name] = String.IsNullOrEmpty(parent) || !worlds.ContainsKey(parent) ? world : worlds[parent].Invert() * world;
    }

    static void SolvePoseLimb(Dictionary<string, Matrix> local, Dictionary<string, PoseAdjustment> offsets,
        PoseAdjustment end, Vector3 target)
    {
        PoseAdjustment middle, start;
        if (!offsets.TryGetValue(end.Parent, out middle) || !offsets.TryGetValue(middle.Parent, out start)) return;
        if (!middle.Name.Contains("arm_") && !middle.Name.Contains("leg_")) return;
        var world = PoseWorlds(local, offsets);
        var a = Position(world[start.Name]); var b = Position(world[middle.Name]); var c = Position(world[end.Name]);
        float upper = (b - a).TrueDistance(), lower = (c - b).TrueDistance();
        if (upper < .0001 || lower < .0001) return;
        var delta = target - a;
        float distance = Math.Max(Math.Abs(upper - lower) + .0001f, Math.Min(upper + lower - .0001f, delta.TrueDistance()));
        var direction = delta.TrueDistance() > .0001 ? delta.Normalize() : (c - a).Normalize();
        var bend = b - a - direction * (b - a).Dot(direction);
        if (bend.TrueDistance() < .0001) bend = direction.Cross(new Vector3(0, 1, 0));
        if (bend.TrueDistance() < .0001) bend = direction.Cross(new Vector3(1, 0, 0));
        float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
        var joint = a + direction * along + bend.Normalize() * (float)Math.Sqrt(Math.Max(0, upper * upper - along * along));
        var tip = a + direction * distance;
        var startWorld = AlignSegment(world[start.Name], b - a, joint - a, a);
        var middleWorld = AlignSegment(world[middle.Name], c - b, tip - joint, joint);
        var endWorld = world[end.Name]; endWorld[12] = tip._x; endWorld[13] = tip._y; endWorld[14] = tip._z;
        local[start.Name] = String.IsNullOrEmpty(start.Parent) || !world.ContainsKey(start.Parent) ? startWorld : world[start.Parent].Invert() * startWorld;
        local[middle.Name] = startWorld.Invert() * middleWorld;
        local[end.Name] = middleWorld.Invert() * endWorld;
    }

    static Dictionary<string, Matrix> CorrectSkeleton(Dictionary<string, PoseAdjustment> offsets, Dictionary<string, Matrix> source)
    {
        var local = source.ToDictionary(p => p.Key, p => offsets.ContainsKey(p.Key) ? CorrectFrame(offsets[p.Key], p.Value) : p.Value);
        if (!offsets.Values.Any(a => a.Natural && a.Continuous)) return local;
        var sourceWorld = PoseWorlds(source, offsets);
        var sourceAnchor = PoseWorlds(offsets.ToDictionary(p => p.Key, p => p.Value.Anchor), offsets);
        var desiredAnchor = PoseWorlds(offsets.ToDictionary(p => p.Key, p => p.Value.Offset * p.Value.Anchor), offsets);
        foreach (var end in offsets.Values.Where(a => a.Natural && a.Continuous && (a.Name.Contains("wrist_") || a.Name.Contains("ankle_"))))
        {
            PoseAdjustment root = end, parent;
            while (!root.RootMotion && offsets.TryGetValue(root.Parent ?? "", out parent)) root = parent;
            // Bewegungswege muessen derselben gedrehten Haltung folgen wie die Gelenke.
            var motionFrame = desiredAnchor[root.Name] * sourceAnchor[root.Name].Invert();
            var movement = PoseDirection(motionFrame, Position(sourceWorld[end.Name]) - Position(sourceAnchor[end.Name]));
            var target = Position(desiredAnchor[end.Name]) + movement * end.Strength;
            SolvePoseLimb(local, offsets, end, target);
        }
        return local;
    }

    public static int TriangleCount(string path, string name)
    {
        using (var root = NodeFactory.FromFile(null, path) as BRRESNode)
        {
            var model = root.GetFolder<MDL0Node>().Children.OfType<MDL0Node>().Single(m => m.Name == name);
            model.Populate();
            return model.PolygonGroup.Children.OfType<MDL0ObjectNode>().Sum(p => p.FaceCount);
        }
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
        CombineMenuAnimationsForBones(modelPath, animationPath, destination, new string[0]);
    }

    public static void CombineMenuAnimationsForBones(string modelPath, string animationPath, string destination, string[] activeBones)
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
            var skeleton = Driver(model);
            foreach (var clip in group.Children.OfType<CHR0Node>())
            {
                skeleton.ApplyCHR(clip, 1);
                var bones = skeleton.AllBones.ToDictionary(b => b.Name);
                var aliases = new Dictionary<string, string>();
                foreach (string name in activeBones)
                {
                    MDL0BoneNode bone, visible;
                    string other = name.StartsWith("pcd_", StringComparison.Ordinal) ? name.Substring(4) : "pcd_" + name;
                    if (bones.TryGetValue(name, out bone) && !UsablePose(bone._frameMatrix)
                        && bones.TryGetValue(other, out visible) && UsablePose(visible._frameMatrix)) aliases[name] = other;
                }
                var frames = aliases.ToDictionary(p => p.Key, p => new FrameState[clip.FrameCount]);
                for (int frame = 0; frame < clip.FrameCount && aliases.Count > 0; frame++)
                {
                    skeleton.ApplyCHR(clip, frame + 1);
                    foreach (var pair in aliases) frames[pair.Key][frame] = bones[pair.Value]._frameState;
                }
                foreach (var pair in frames)
                {
                    var entry = clip.FindChild(pair.Key, false) as CHR0EntryNode ?? clip.CreateEntry(pair.Key);
                    var keys = entry.Keyframes;
                    keys._keyArrays = new KeyframeCollection(9, keys.FrameLimit, 1, 1, 1) { Loop = keys.Loop }._keyArrays;
                    for (int frame = 0; frame < pair.Value.Length; frame++)
                    {
                        entry.SetKeyframeOnlyScale(frame, pair.Value[frame]._scale);
                        entry.SetKeyframeOnlyRot(frame, pair.Value[frame]._rotate);
                        entry.SetKeyframeOnlyTrans(frame, pair.Value[frame]._translate);
                    }
                }
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
            var animationGroup = root.GetFolder<CHR0Node>();
            foreach (var animation in animationGroup == null ? Enumerable.Empty<CHR0Node>() : animationGroup.Children.OfType<CHR0Node>())
            {
                var settings = separate ? descriptions.Single(d => d.DocumentElement.GetAttribute("animation") == animation.Name) : descriptions[0];
                var offsets = ReadAdjustments(settings.OuterXml);
                var tracks = ReadAnimationKeys(settings.OuterXml, animation.Name);
                if (tracks.Values.Any(keys => keys.Any(k => k.Frame >= animation.FrameCount)))
                    throw new InvalidDataException("A keyframe is outside animation " + animation.Name + ".");
                var originalAnimation = source.GetFolder<CHR0Node>().Children.OfType<CHR0Node>().Single(a => a.Name == animation.Name);
                var clipOffsets = offsets;
                if (settings.DocumentElement.GetAttribute("humanStyle").Length == 0 && settings.DocumentElement.GetAttribute("natural") == "true" && settings.DocumentElement.GetAttribute("continuous") != "true")
                {
                    float startFrame = animation.Name == settings.DocumentElement.GetAttribute("animation")
                        ? Single.Parse(settings.DocumentElement.GetAttribute("frame"), CultureInfo.InvariantCulture) : 1;
                    foreach (var model in models) { model.ApplyCHR(originalAnimation, startFrame); NormalizePoseAliases(model); }
                    clipOffsets = offsets.ToDictionary(pair => pair.Key, pair => {
                        var anchor = bones[pair.Key]._frameState._transform;
                        return new PoseAdjustment { Name = pair.Key, Parent = pair.Value.Parent, Natural = true, Strength = pair.Value.Strength,
                            StableMenu = pair.Value.StableMenu, RootMotion = pair.Value.RootMotion,
                            Anchor = anchor, Offset = pair.Value.Offset * pair.Value.Anchor * anchor.Invert() };
                    });
                }
                var frames = new Dictionary<string, FrameState[]>();
                for (int frame = 0; frame < animation.FrameCount; frame++)
                {
                    foreach (var model in models) { model.ApplyCHR(originalAnimation, frame + 1); NormalizePoseAliases(model); }
                    var correctedPose = AnimationPose(clipOffsets, bones.ToDictionary(p => p.Key, p => p.Value._frameState._transform), settings.DocumentElement, animation.Name, frame, animation.FrameCount);
                    foreach (var pair in clipOffsets)
                    {
                        if (!frames.ContainsKey(pair.Key)) frames.Add(pair.Key, new FrameState[animation.FrameCount]);
                        var corrected = correctedPose[pair.Key];
                        frames[pair.Key][frame] = PoseState(ApplyAnimationKeys(corrected, pair.Key, frame, tracks), animation.Name + "/" + pair.Key + "/" + frame);
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
                        var previousRotation = frame > 0 ? pair.Value[frame - 1]._rotate : state._rotate;
                        for (int axis = 0; axis < 3; axis++)
                        {
                            while (state._rotate[axis] - previousRotation[axis] > 180) state._rotate[axis] -= 360;
                            while (state._rotate[axis] - previousRotation[axis] < -180) state._rotate[axis] += 360;
                        }
                        pair.Value[frame] = state;
                        entry.SetKeyframeOnlyScale(frame, state._scale);
                        entry.SetKeyframeOnlyRot(frame, state._rotate);
                        entry.SetKeyframeOnlyTrans(frame, state._translate);
                    }
                    if (settings.DocumentElement.GetAttribute("humanStyle").Length > 0
                        && !HumanSteeringClip(animation.Name) && !tracks.ContainsKey(pair.Key))
                    {
                        for (int axis = 0; axis < 9; axis++)
                        {
                            keys.GetKeyframe(axis, 0)._tangent = 0;
                            keys.GetKeyframe(axis, animation.FrameCount - 1)._tangent = 0;
                        }
                    }
                    CompactAnimationKeys(entry, animation.FrameCount);
                }
            }
            root.Export(destination);
        }
    }

    static void CompactAnimationKeys(CHR0EntryNode entry, int frameCount)
    {
        var keys = entry.Keyframes;
        for (int axis = 0; axis < 9; axis++)
        {
            var dense = keys._keyArrays[axis];
            float tolerance = axis < 3 ? .00001f : .0005f;
            var keep = new SortedSet<int> { 0, frameCount - 1 };
            var pending = new Stack<Tuple<int, int>>();
            if (frameCount > 1) pending.Push(Tuple.Create(0, frameCount - 1));
            while (pending.Count > 0)
            {
                var span = pending.Pop();
                if (span.Item2 - span.Item1 < 2) continue;
                var first = keys.GetKeyframe(axis, span.Item1);
                var last = keys.GetKeyframe(axis, span.Item2);
                float worst = tolerance;
                int split = -1;
                // Hermite-Tangenten erhalten; auch zwischen ganzen Frames prüfen.
                for (float frame = span.Item1 + .25f; frame < span.Item2; frame += .25f)
                {
                    float value = first.Interpolate(frame - span.Item1, span.Item2 - span.Item1, last);
                    float error = Math.Abs(value - dense.GetFrameValue(frame));
                    if (error > worst)
                    {
                        worst = error;
                        split = Math.Max(span.Item1 + 1, Math.Min(span.Item2 - 1, (int)Math.Round(frame)));
                    }
                }
                if (split < 0) continue;
                keep.Add(split);
                pending.Push(Tuple.Create(span.Item1, split));
                pending.Push(Tuple.Create(split, span.Item2));
            }
            var compact = new KeyframeArray(keys.FrameLimit, axis < 3 ? 1 : 0) { Loop = keys.Loop };
            foreach (int frame in keep)
            {
                var original = keys.GetKeyframe(axis, frame);
                var key = compact.SetFrameValue(frame, original._value);
                key._tangent = original._tangent;
            }
            var values = Enumerable.Range(0, frameCount).Select(frame => dense.GetFrameValue(frame)).ToArray();
            if (values.Max() - values.Min() < tolerance)
            {
                compact = new KeyframeArray(keys.FrameLimit, axis < 3 ? 1 : 0) { Loop = keys.Loop };
                compact.SetFrameValue(0, values[0]);
            }
            keys._keyArrays[axis] = compact;
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
        ConvertDriverModels(template, dae, lod, destination, false, true);
    }

    public static void ConvertDriverWithMainLod(string template, string dae, string lod, string destination)
    {
        ConvertDriverModels(template, dae, lod, destination, true, true);
    }

    public static void ConvertDriverWithOptions(string template, string dae, string lod, string destination, bool mainSkeletonForLod, bool starEffect)
    {
        ConvertDriverModels(template, dae, lod, destination, mainSkeletonForLod, starEffect);
    }

    static void ConvertDriverModels(string template, string dae, string lod, string destination, bool mainSkeletonForLod, bool starEffect)
    {
        if (File.Exists(destination)) throw new IOException("Destination exists.");
        using (var root = NodeFactory.FromFile(null, template))
        {
            var original = Driver(root);
            var brres = (BRRESNode)root;
            var models = original.Parent.Children.OfType<MDL0Node>().Where(m => m.Name == "model" || m.Name == "model_lod").ToArray();
            if (models.Length == 0) throw new InvalidDataException("Expected RR driver model.");
            var textureSizes = GameTextureSizes(dae, lod);
            var orderedModels = mainSkeletonForLod ? models.OrderBy(m => m.Name == "model" ? 1 : 0).ToArray() : models;
            foreach (var old in orderedModels)
            using (var importer = new Collada())
            {
                Collada._importOptions = new Collada.ImportOptions();
                Collada._importOptions._mdlType = Collada.ImportOptions.MDLType.Character;
                Collada._importOptions._modelVersion = old.Version;
                string input = old.Name == "model_lod" ? lod : dae;
                var model = importer.ImportModel(input, Collada.ImportType.MDL0) as MDL0Node;
                if (model == null) throw new InvalidDataException("Internal Wii model conversion failed.");
                model.Populate();
                var skeleton = mainSkeletonForLod && old.Name == "model_lod" ? original : old;
                PrepareReference(skeleton);
                var document = new XmlDocument { XmlResolver = null };
                document.Load(input);
                ConfigureImportedMaterials(model, old.MaterialList == null || old.MaterialList.Count == 0 ? original : old, document, starEffect);
                var expectedMaterials = document.SelectNodes("//*[local-name()='geometry']/*[local-name()='mesh']/*[local-name()='triangles']")
                    .Cast<XmlElement>().Where(element => element.GetAttribute("count") != "0")
                    .Select(element => element.GetAttribute("material")).Distinct(StringComparer.Ordinal).OrderBy(name => name).ToArray();
                var importedMaterials = model.MaterialList.Select(material => material.Name).Distinct(StringComparer.Ordinal).OrderBy(name => name).ToArray();
                if (!expectedMaterials.SequenceEqual(importedMaterials))
                    throw new InvalidDataException("Wii conversion lost a material region. Expected: " + String.Join(", ", expectedMaterials) + "; imported: " + String.Join(", ", importedMaterials));
                if (model.AllBones.Count + model.Influences.Count > 2048)
                    throw new InvalidDataException("Wii model exceeds the Studio matrix budget. Simplify the movement assignment.");
                var expected = skeleton.AllBones.OrderBy(b => b.Name).ToArray();
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
                        Size size = textureSizes[name];
                        if (bitmap.Size == size) target.ReplaceRaw(TextureConverter.CMPR.EncodeTEX0Texture(bitmap, 1));
                        else using (var scaled = new Bitmap(bitmap, size))
                            target.ReplaceRaw(TextureConverter.CMPR.EncodeTEX0Texture(scaled, 1));
                        target.Name = name;
                    }
                }
            }
            root.Export(destination);
        }
    }

    static Dictionary<string, Size> GameTextureSizes(params string[] files)
    {
        var sizes = new Dictionary<string, Size>(StringComparer.Ordinal);
        Func<int, int> blockSize = value => Math.Max(8, (value + 7) / 8 * 8);
        foreach (string file in files.Distinct())
        {
            var document = new XmlDocument { XmlResolver = null };
            document.Load(file);
            foreach (XmlElement image in document.SelectNodes("//*[local-name()='library_images']/*[local-name()='image']/*[local-name()='init_from']"))
            {
                string path = Path.Combine(Path.GetDirectoryName(file), image.InnerText);
                string name = Path.GetFileNameWithoutExtension(path);
                if (sizes.ContainsKey(name)) continue;
                // Vollstaendige CMPR-Bloecke verhindern undefinierte Randpixel bei Kleinsttexturen.
                using (var bitmap = new Bitmap(path)) sizes.Add(name, new Size(blockSize(bitmap.Width), blockSize(bitmap.Height)));
            }
        }
        Func<Size, long> bytes = size => ((size.Width + 7L) / 8) * ((size.Height + 7L) / 8) * 32 + 64;
        // Viele Materialtexturen teilen ein Gesamtbudget im begrenzten Wii-Ladespeicher.
        while (sizes.Values.Sum(bytes) > 1024 * 1024)
        {
            var largest = sizes.Where(p => p.Value.Width > 8 || p.Value.Height > 8).OrderByDescending(p => bytes(p.Value)).First();
            sizes[largest.Key] = new Size(blockSize(largest.Value.Width / 2), blockSize(largest.Value.Height / 2));
        }
        return sizes;
    }

    static void ConfigureImportedMaterials(MDL0Node model, MDL0Node reference, XmlDocument document, bool starEffect)
    {
        var sided = new Dictionary<string, bool>();
        foreach (XmlElement element in document.SelectNodes("//*[local-name()='library_materials']/*[local-name()='material']")) {
            var value = element.SelectSingleNode("*[local-name()='extra']/*[local-name()='technique'][@profile='STUDIO']/*[local-name()='double_sided']");
            bool both;
            if (value != null && Boolean.TryParse(value.InnerText, out both)) sided[element.GetAttribute("id")] = both;
        }
        var lighting = reference.MaterialList == null ? null : reference.MaterialList.OfType<MDL0MaterialNode>().FirstOrDefault();
        // Der Brawl-Standardshader verstärkt Farben vierfach und passt nicht zur MKW-Beleuchtung.
        foreach (var shader in model.MaterialList.OfType<MDL0MaterialNode>().Select(m => m.ShaderNode).Distinct())
        {
            foreach (var stage in shader.Children.ToArray()) stage.Remove();
            shader.AddChild(new MDL0TEVStageNode {
                TextureEnabled = true,
                TextureMapID = TexMapID.TexMap0,
                TextureCoordID = TexCoordID.TexCoord0,
                RasterColor = ColorSelChan.LightChannel0,
                ColorSelectionA = ColorArg.Zero,
                ColorSelectionB = ColorArg.TextureColor,
                ColorSelectionC = ColorArg.RasterColor,
                ColorSelectionD = starEffect ? ColorArg.Color1 : ColorArg.Zero,
                ColorScale = TevScale.MultiplyBy1,
                AlphaSelectionA = AlphaArg.Zero,
                AlphaSelectionB = AlphaArg.Zero,
                AlphaSelectionC = AlphaArg.Zero,
                AlphaSelectionD = AlphaArg.TextureAlpha
            });
        }
        foreach (var material in model.MaterialList.OfType<MDL0MaterialNode>())
        {
            bool both;
            if (sided.TryGetValue(material.Name, out both)) material.CullMode = both ? CullMode.Cull_None : CullMode.Cull_Inside;
            material.ActiveShaderStages = 1;
            material.LightSetIndex = lighting == null ? (sbyte)0 : lighting.LightSetIndex;
            material.FogIndex = lighting == null ? (sbyte)-1 : lighting.FogIndex;
            material.C1MaterialColor = new RGBAPixel(255, 255, 255, 255);
            material.C1ColorMaterialSource = GXColorSrc.Register;
            material.C1ColorAmbientSource = GXColorSrc.Register;
            if (lighting != null)
            {
                material.C1AmbientColor = lighting.C1AmbientColor;
                material.C1ColorEnabled = lighting.C1ColorEnabled;
                material.C1ColorDiffuseFunction = lighting.C1ColorDiffuseFunction;
                material.C1ColorAttenuation = lighting.C1ColorAttenuation;
                material.C1ColorLights = lighting.C1ColorLights;
            }
            material.C1AlphaEnabled = false;
            material.C1AlphaMaterialSource = GXColorSrc.Register;
            material.C2ColorEnabled = false;
            material.C2AlphaEnabled = false;
        }
    }
}
