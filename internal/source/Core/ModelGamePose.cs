using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml;

namespace murumsWiiModStudio
{
    internal sealed class GamePoseSettings
    {
        public float[][] Joints;
        public float[] Position = new float[3];
        public float[] Rotation = new float[3];
        public float Scale = 100;
        public float MotionStrength = 100;
        public bool NaturalHuman;
        public PoseContact[] Contacts;
    }

    internal sealed class RigPoseReference
    {
        internal string Template, Animation, VehicleCode, ModelName;
        RigPoseReference orientationSource;
        float[][] targetWorldOverride;
        readonly Dictionary<string, RigPoseReference> clipAnchors = new Dictionary<string, RigPoseReference>();
        internal float Frame;
        internal int Frames;
        internal string[] Names, Parents, AvailableAnimations;
        internal float[][] Matrices, Joints, Binds;
        internal string Corrections;
        internal CharacterModelImport Visual;

        internal static RigPoseReference Load(string template, string model, string animation, float frame, bool visual)
        {
            string preview = visual ? Path.Combine(ModelRuntime.NewWorkFolder(), "reference.dae") : null;
            string xml = (string)StudioModelLibrary.Call("ReadPose", template, model, animation, frame, preview);
            var doc = new XmlDocument { XmlResolver = null }; doc.LoadXml(xml);
            var nodes = doc.DocumentElement.ChildNodes.OfType<XmlElement>().ToArray();
            return new RigPoseReference {
                Template = template, ModelName = model, Animation = animation, Frame = frame,
                Frames = Int32.Parse(doc.DocumentElement.GetAttribute("frames"), CultureInfo.InvariantCulture),
                AvailableAnimations = doc.DocumentElement.GetAttribute("available").Split('|'),
                Names = nodes.Select(n => n.GetAttribute("name")).ToArray(),
                Parents = nodes.Select(n => n.GetAttribute("parent")).ToArray(),
                Binds = nodes.Select(n => Numbers(n.GetAttribute("bind"))).ToArray(),
                Matrices = nodes.Select(n => Numbers(n.GetAttribute("skin"))).ToArray(),
                Joints = nodes.Select(n => Numbers(n.GetAttribute("joint"))).ToArray(),
                Visual = visual ? CharacterModelImport.Load(preview) : null
            };
        }
        static float[] Numbers(string text) { return text.Split(' ').Select(v => Single.Parse(v, CultureInfo.InvariantCulture)).ToArray(); }
        bool VisibleTransform(int index)
        {
            if (index < 0) return false;
            var m = RigMatrix.Multiply(Matrices[index], Binds[index]);
            double determinant = m[0] * (m[5] * m[10] - m[6] * m[9]) -
                m[1] * (m[4] * m[10] - m[6] * m[8]) + m[2] * (m[4] * m[9] - m[5] * m[8]);
            return !Double.IsNaN(determinant) && !Double.IsInfinity(determinant) && Math.Abs(determinant) > .000001;
        }

        internal int MatchBone(ModelRig rig, int index)
        {
            int primary = rig.SharedBodyBone(index);
            int exact = Array.IndexOf(Names, rig.Bones[index].Name);
            if (VisibleTransform(exact)) return exact;
            foreach (int candidate in Enumerable.Range(0, rig.Bones.Length).Where(i => rig.SharedBodyBone(i) == primary))
            {
                int found = Array.IndexOf(Names, rig.Bones[candidate].Name);
                if (VisibleTransform(found)) return found;
            }
            return exact;
        }

        bool[] ActiveBones(ModelRig rig)
        {
            var active = new bool[Names.Length];
            foreach (int source in rig.BoneIndices.SelectMany(indices => indices).Distinct())
            {
                int found = MatchBone(rig, source);
                for (int ancestor = rig.Bones[source].Parent; found < 0 && ancestor >= 0; ancestor = rig.Bones[ancestor].Parent)
                    found = MatchBone(rig, ancestor);
                if (found < 0 && Names.Length == 1) found = 0;
                if (found < 0) throw new InvalidDataException("The reference skeleton cannot preserve a used joint.");
                for (int i = found; i >= 0 && !active[i]; i = Array.IndexOf(Names, Parents[i])) active[i] = true;
            }
            return active;
        }

        internal RigPoseReference Adjusted(ModelRig rig, int context)
        {
            var oldWorld = Matrices.Select((m, i) => RigMatrix.Multiply(m, Binds[i])).ToArray();
            var orientationWorld = orientationSource == null ? oldWorld
                : orientationSource.Matrices.Select((m, i) => RigMatrix.Multiply(m, orientationSource.Binds[i])).ToArray();
            var targets = rig.GameJoints(context);
            var active = ActiveBones(rig);
            var targetWorld = new float[Names.Length][];
            for (int i = 0; i < Names.Length; i++)
            {
                if (!active[i]) { targetWorld[i] = oldWorld[i]; continue; }
                int bone = Array.FindIndex(rig.Bones, b => b.Name == Names[i]);
                int match = bone < 0 ? 0 : rig.SharedBodyBone(bone);
                var world = (float[])orientationWorld[i].Clone();
                if (rig.GameSettings(context).NaturalHuman)
                {
                    var x = RigVector.Unit(new[] { world[0], world[4], world[8] });
                    var y = new[] { world[1], world[5], world[9] };
                    y = RigVector.Unit(RigVector.Sub(y, RigVector.Scale(x, RigVector.Dot(x, y))));
                    var z = RigVector.Unit(RigVector.Cross(x, y));
                    for (int axis = 0; axis < 3; axis++) { world[axis * 4] = x[axis]; world[axis * 4 + 1] = y[axis]; world[axis * 4 + 2] = z[axis]; }
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    var column = new[] { world[axis], world[axis + 4], world[axis + 8] };
                    var rotated = RigVector.Scale(rig.TransformGamePoint(column, rig.GameSettings(context), true, false), RigVector.Length(column));
                    world[axis] = rotated[0]; world[axis + 4] = rotated[1]; world[axis + 8] = rotated[2];
                }
                world[3] = targets[match][0]; world[7] = targets[match][1]; world[11] = targets[match][2];
                targetWorld[i] = world;
            }
            if (targetWorldOverride != null)
            {
                for (int i = 0; i < Names.Length; i++)
                {
                    if (!active[i]) continue;
                    int parent = Array.IndexOf(Names, Parents[i]);
                    var local = parent < 0 ? targetWorldOverride[i]
                        : RigMatrix.Multiply(RigMatrix.Inverse(targetWorld[parent]), targetWorldOverride[i]);
                    // Wii-Animationen speichern keine Scherung; Gelenkpositionen bleiben erhalten.
                    local = RigMatrix.ProjectSrt(local);
                    targetWorld[i] = parent < 0 ? local : RigMatrix.Multiply(targetWorld[parent], local);
                }
            }
            for (int i = 0; i < Names.Length; i++)
                if (!active[i]) targetWorld[i] = InheritUnchangedWorld(i, active, oldWorld, targetWorld);
            var doc = new XmlDocument { XmlResolver = null }; var root = doc.CreateElement("corrections"); doc.AppendChild(root);
            root.SetAttribute("natural", rig.GameSettings(context).NaturalHuman ? "true" : "false");
            root.SetAttribute("animation", Animation);
            root.SetAttribute("frame", Frame.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("strength", (rig.GameSettings(context).MotionStrength / 100).ToString("R", CultureInfo.InvariantCulture));
            for (int i = 0; i < Names.Length; i++)
            {
                int parent = Array.IndexOf(Names, Parents[i]);
                if (!active[i]) continue;
                var oldLocal = parent < 0 ? oldWorld[i] : RigMatrix.Multiply(RigMatrix.Inverse(oldWorld[parent]), oldWorld[i]);
                var newLocal = parent < 0 ? targetWorld[i] : RigMatrix.Multiply(RigMatrix.Inverse(targetWorld[parent]), targetWorld[i]);
                var node = doc.CreateElement("bone"); root.AppendChild(node); node.SetAttribute("name", Names[i]);
                node.SetAttribute("anchor", String.Join(" ", oldLocal.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                node.SetAttribute("matrix", String.Join(" ", RigMatrix.Multiply(newLocal, RigMatrix.Inverse(oldLocal)).Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
            }
            return new RigPoseReference { Names = Names, Parents = Parents, Binds = Binds, Template = Template, Animation = Animation, Frame = Frame, Frames = Frames,
                Matrices = targetWorld.Select((m, i) => RigMatrix.Multiply(m, RigMatrix.Inverse(Binds[i]))).ToArray(), Joints = targetWorld.Select(m => new[] { m[3], m[7], m[11] }).ToArray(), Corrections = doc.OuterXml };
        }
        internal RigPoseReference FromStandingPose(ModelRig rig, RigPoseReference standing)
        {
            var from = rig.GameBoneTransforms(1);
            var to = rig.GameBoneTransforms(2);
            var standingWorld = standing.Matrices.Select((m, i) => RigMatrix.Multiply(m, standing.Binds[i])).ToArray();
            var copy = (RigPoseReference)MemberwiseClone();
            var active = ActiveBones(rig);
            copy.targetWorldOverride = Names.Select((name, index) => {
                if (!active[index]) return RigMatrix.Multiply(Matrices[index], Binds[index]);
                int bone = Array.FindIndex(rig.Bones, b => b.Name == name);
                int original = Array.IndexOf(standing.Names, name);
                if (bone >= 0) bone = rig.SharedBodyBone(bone);
                if (bone < 0 || original < 0) throw new InvalidDataException("Menu vehicle skeleton differs from the selected character.");
                return RigMatrix.Multiply(RigMatrix.Multiply(to[bone], RigMatrix.Inverse(from[bone])), standingWorld[original]);
            }).ToArray();
            return copy.Adjusted(rig, 2);
        }

        internal RigPoseReference MotionAnchor(ModelRig rig, int context, string animation)
        {
            if (!rig.GameSettings(context).NaturalHuman || animation == Animation) return this;
            RigPoseReference result;
            if (!clipAnchors.TryGetValue(animation, out result))
            {
                result = Load(Template, ModelName ?? "model", animation, 1, false);
                result.orientationSource = this;
                clipAnchors[animation] = result;
            }
            return result;
        }
        float[] InheritUnchangedWorld(int index, bool[] changed, float[][] original, float[][] adjusted)
        {
            int parent = Array.IndexOf(Names, Parents[index]);
            while (parent >= 0 && !changed[parent]) parent = Array.IndexOf(Names, Parents[parent]);
            if (parent < 0) return original[index];
            return RigMatrix.Multiply(RigMatrix.Multiply(adjusted[parent], RigMatrix.Inverse(original[parent])), original[index]);
        }

        internal RigPoseReference ApplyCorrections(RigPoseReference anchor)
        {
            var world = new float[Names.Length][];
            var originalWorld = Matrices.Select((m, i) => RigMatrix.Multiply(m, Binds[i])).ToArray();
            var local = new float[Names.Length][];
            var changes = new XmlDocument { XmlResolver = null }; changes.LoadXml(anchor.Corrections);
            var changedNames = new HashSet<string>(changes.DocumentElement.ChildNodes.OfType<XmlElement>().Select(n => n.GetAttribute("name")));
            for (int i = 0; i < Names.Length; i++)
            {
                int parent = Array.IndexOf(Names, Parents[i]);
                if (!changedNames.Contains(Names[i])) { local[i] = Binds[i]; continue; }
                local[i] = parent < 0 ? originalWorld[i] : RigMatrix.Multiply(RigMatrix.Inverse(originalWorld[parent]), originalWorld[i]);
            }
            var corrected = (float[][])StudioModelLibrary.Call("CorrectPose", anchor.Corrections, Names, local);
            var changed = Names.Select(changedNames.Contains).ToArray();
            for (int i = 0; i < Names.Length; i++)
            {
                int parent = Array.IndexOf(Names, Parents[i]);
                if (!changed[i]) { world[i] = InheritUnchangedWorld(i, changed, originalWorld, world); continue; }
                world[i] = parent < 0 ? corrected[i] : RigMatrix.Multiply(world[parent], corrected[i]);
            }
            return new RigPoseReference { Names = Names, Parents = Parents, Binds = Binds, Matrices = world.Select((m, i) => RigMatrix.Multiply(m, RigMatrix.Inverse(Binds[i]))).ToArray() };
        }
        internal float[][] ForRig(ModelRig rig)
        {
            return Enumerable.Range(0, rig.Bones.Length).Select(i => {
                int found = -1;
                for (int bone = i; bone >= 0 && found < 0; bone = rig.Bones[bone].Parent) found = MatchBone(rig, bone);
                if (found < 0 && Names.Length == 1) found = 0;
                if (found < 0 && !rig.BoneIndices.Any(indices => indices.Contains(i)))
                    return new[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f };
                if (found < 0) throw new InvalidDataException("The reference skeleton does not match the selected RR character.");
                return Matrices[found];
            }).ToArray();
        }
    }

    internal sealed partial class ModelRig
    {
        public GamePoseSettings MenuPose, RacePose;
        public Dictionary<string, GamePoseSettings> VehiclePoses = new Dictionary<string, GamePoseSettings>();
        internal string ActiveVehicle;
        internal GamePoseSettings GameSettings(int context)
        {
            GamePoseSettings vehicle;
            if (context == 2 && ActiveVehicle != null && VehiclePoses != null && VehiclePoses.TryGetValue(ActiveVehicle, out vehicle)) return vehicle;
            return context == 1 ? MenuPose : RacePose;
        }
        internal void SetGameSettings(int context, GamePoseSettings settings)
        {
            if (context == 1) MenuPose = settings;
            else if (context == 2 && ActiveVehicle != null)
            {
                if (VehiclePoses == null) VehiclePoses = new Dictionary<string, GamePoseSettings>();
                if (settings == null) VehiclePoses.Remove(ActiveVehicle); else VehiclePoses[ActiveVehicle] = settings;
            }
            else if (context == 2) RacePose = settings;
            else throw new ArgumentOutOfRangeException("context");
        }
        internal void InitializeGamePose(int context, RigPoseReference reference)
        {
            if (GameSettings(context) != null) return;
            if (JointGuides == null) throw new InvalidOperationException("Assign source joints first.");
            if (reference != null)
            {
                ApplyReferenceGamePose(context, reference);
                return;
            }
            var settings = new GamePoseSettings { MotionStrength = context == 1 ? 25 : 100, Joints = JointGuides.Select(p => (float[])p.Clone()).ToArray() };
            SetGameSettings(context, settings);
            if (context == 1)
            {
                foreach (string side in new[] { "l", "r" })
                {
                    int shoulder = Array.FindIndex(Bones, b => b.Name == "arm_" + side + "1");
                    if (shoulder < 0) continue;
                    float sign = side == "l" ? 1 : -1;
                    var direction = new[] { sign * .26f, -.96f, 0f };
                    foreach (string part in new[] { "arm_" + side + "2", "wrist_" + side + "1" })
                    {
                        int joint = Array.FindIndex(Bones, b => b.Name == part);
                        if (joint >= 0) settings.Joints[joint] = RigVector.Add(settings.Joints[shoulder], RigVector.Scale(direction, RigVector.Length(RigVector.Sub(JointGuides[joint], JointGuides[shoulder]))));
                    }
                }
            }
            else
            {
                int root = Array.FindIndex(Bones, b => b.Name == "skl_root");
                int originalRoot = reference == null ? -1 : Array.IndexOf(reference.Names, "skl_root");
                var hip = originalRoot < 0 ? new[] { 0f, ReferenceHeight * .38f, 0f } : reference.Joints[originalRoot];
                var sourceHip = root < 0 ? new[] { 0f, ReferenceHeight * .55f, 0f } : JointGuides[root];
                for (int i = 0; i < settings.Joints.Length; i++) settings.Joints[i] = RigVector.Add(JointGuides[i], RigVector.Sub(hip, sourceHip));
                foreach (string side in new[] { "l", "r" })
                {
                    float sign = side == "l" ? 1 : -1;
                    SetSeatedChain(settings, "leg_" + side + "1", "leg_" + side + "2", "ankle_" + side + "1", new[] { sign * .08f, -.18f, .98f }, new[] { 0f, -.98f, -.18f });
                    SetSeatedChain(settings, "arm_" + side + "1", "arm_" + side + "2", "wrist_" + side + "1", new[] { sign * .08f, -.75f, .66f }, new[] { -sign * .2f, .1f, .975f });
                }
            }
        }
        void SetSeatedChain(GamePoseSettings settings, string a, string b, string c, float[] first, float[] second)
        {
            int ia = Array.FindIndex(Bones, x => x.Name == a), ib = Array.FindIndex(Bones, x => x.Name == b), ic = Array.FindIndex(Bones, x => x.Name == c);
            if (ia < 0 || ib < 0 || ic < 0) return;
            settings.Joints[ib] = RigVector.Add(settings.Joints[ia], RigVector.Scale(first, RigVector.Length(RigVector.Sub(JointGuides[ib], JointGuides[ia]))));
            settings.Joints[ic] = RigVector.Add(settings.Joints[ib], RigVector.Scale(second, RigVector.Length(RigVector.Sub(JointGuides[ic], JointGuides[ib]))));
        }
        internal void ValidateGamePoses()
        {
            if (VehiclePoses != null && (VehiclePoses.Count > 36 || VehiclePoses.Any(p => !ValidVehicleKey(p.Key) || p.Value == null)))
                throw new InvalidDataException("Invalid vehicle poses.");
            foreach (var settings in new[] { MenuPose, RacePose }.Concat(VehiclePoses == null ? Enumerable.Empty<GamePoseSettings>() : VehiclePoses.Values).Where(p => p != null))
            {
                if (JointGuides == null || settings.Position != null && settings.Position.Any(v => Math.Abs(v) > 1000) || settings.Rotation != null && settings.Rotation.Any(v => Math.Abs(v) > 180) || settings.Joints == null || settings.Joints.Length != Bones.Length || settings.Joints.Any(p => !ValidGameVector(p))
                    || !ValidGameVector(settings.Position) || !ValidGameVector(settings.Rotation) || Single.IsNaN(settings.Scale) || Single.IsInfinity(settings.Scale) || settings.Scale < 1 || settings.Scale > 500
                    || Single.IsNaN(settings.MotionStrength) || Single.IsInfinity(settings.MotionStrength) || settings.MotionStrength < 0 || settings.MotionStrength > 100)
                    throw new InvalidDataException("Invalid game pose settings.");
                if (settings.Contacts != null && (settings.Contacts.Length > 4 || settings.Contacts.Any(c => c == null || !ValidGameVector(c.Target) || !Bones.Any(b => b.Name == c.Joint))))
                    throw new InvalidDataException("Invalid vehicle contacts.");
            }
        }
        static bool ValidGameVector(float[] value) { return value != null && value.Length == 3 && value.All(v => !Single.IsNaN(v) && !Single.IsInfinity(v) && Math.Abs(v) <= 10000); }
        internal float[] TransformGamePoint(float[] point, GamePoseSettings settings, bool normal, bool inverse)
        {
            var result = (float[])point.Clone();
            if (inverse && !normal) result = RigVector.Sub(result, settings.Position);
            for (int step = 0; step < 3; step++)
            {
                int axis = inverse ? 2 - step : step;
                double angle = settings.Rotation[axis] * Math.PI / 180 * (inverse ? -1 : 1);
                int a = (axis + 1) % 3, b = (axis + 2) % 3;
                float x = result[a], y = result[b];
                result[a] = (float)(x * Math.Cos(angle) - y * Math.Sin(angle));
                result[b] = (float)(x * Math.Sin(angle) + y * Math.Cos(angle));
            }
            if (!normal) result = RigVector.Scale(result, inverse ? 100 / settings.Scale : settings.Scale / 100);
            if (!inverse && !normal) result = RigVector.Add(result, settings.Position);
            return normal ? RigVector.Unit(result) : result;
        }
        internal float[][] GameJoints(int context)
        {
            var settings = GameSettings(context);
            return settings.Joints.Select(p => TransformGamePoint(p, settings, false, false)).ToArray();
        }
        internal void SetGameJoint(int context, int index, float[] point)
        {
            if (!BoneEnabled(index) || !ValidGameVector(point)) throw new ArgumentException("Invalid game joint.");
            var settings = GameSettings(context);
            settings.Joints[index] = TransformGamePoint(point, settings, false, true);
        }
        internal float[][] GameGeometry(int context, bool normals)
        {
            var settings = GameSettings(context);
            if (settings == null) return AlignedGeometry(normals);
            var mapped = MapGeometry(normals, settings.Joints);
            return mapped.Select(p => TransformGamePoint(p, settings, normals, false)).ToArray();
        }
        internal float[][] GameExportGeometry(int context, bool normals, RigPoseReference reference)
        {
            var points = GameGeometry(context, normals);
            var matrices = reference.ForRig(this);
            return Enumerable.Range(0, points.Length).Select(v => {
                var matrix = BlendMatrix(v, matrices);
                return normals ? RigMatrix.NormalTranspose(matrix, points[v]) : RigMatrix.Point(RigMatrix.Inverse(matrix), points[v]);
            }).ToArray();
        }
        internal float[][] GameAnimationPreview(int context, RigPoseReference anchor, RigPoseReference frame)
        {
            var adjusted = anchor.MotionAnchor(this, context, frame.Animation).Adjusted(this, context);
            var exported = GameExportGeometry(context, false, adjusted);
            var matrices = frame.ApplyCorrections(adjusted).ForRig(this);
            return Enumerable.Range(0, exported.Length).Select(v => RigMatrix.Point(BlendMatrix(v, matrices), exported[v])).ToArray();
        }
        float[] BlendMatrix(int vertex, float[][] matrices)
        {
            var result = new float[16];
            for (int j = 0; j < BoneIndices[vertex].Length; j++)
                for (int k = 0; k < 16; k++) result[k] += matrices[BoneIndices[vertex][j]][k] * BoneWeights[vertex][j];
            return result;
        }
    }
    internal static class RigMatrix
    {
        internal static float[] Multiply(float[] a, float[] b)
        {
            var result = new float[16];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    for (int k = 0; k < 4; k++) result[row * 4 + column] += a[row * 4 + k] * b[k * 4 + column];
            return result;
        }
        internal static float[] Point(float[] m, float[] p)
        {
            return new[] { m[0]*p[0]+m[1]*p[1]+m[2]*p[2]+m[3], m[4]*p[0]+m[5]*p[1]+m[6]*p[2]+m[7], m[8]*p[0]+m[9]*p[1]+m[10]*p[2]+m[11] };
        }
        internal static float[] NormalTranspose(float[] m, float[] p)
        {
            return RigVector.Unit(new[] { m[0]*p[0]+m[4]*p[1]+m[8]*p[2], m[1]*p[0]+m[5]*p[1]+m[9]*p[2], m[2]*p[0]+m[6]*p[1]+m[10]*p[2] });
        }
        internal static float[] ProjectSrt(float[] matrix)
        {
            if (matrix == null || matrix.Length != 16 || matrix.Any(v => Single.IsNaN(v) || Single.IsInfinity(v)))
                throw new InvalidDataException("Invalid pose matrix.");
            var basis = new double[9];
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++) basis[row * 3 + column] = matrix[row * 4 + column];
            double norm = Math.Sqrt(basis.Sum(v => v * v) / 3);
            if (norm < 1e-12) throw new InvalidDataException("Pose matrix has no usable scale.");
            var rotation = basis.Select(v => v / norm).ToArray();
            bool converged = false;
            // Polare Zerlegung findet die nächstliegende Rotation statt einer achsenabhängigen Korrektur.
            for (int iteration = 0; iteration < 40; iteration++)
            {
                double a = rotation[0], b = rotation[1], c = rotation[2];
                double d = rotation[3], e = rotation[4], f = rotation[5];
                double g = rotation[6], h = rotation[7], i = rotation[8];
                double determinant = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
                if (Math.Abs(determinant) < 1e-12)
                    throw new InvalidDataException("Pose matrix is singular.");
                var inverseTranspose = new[] { e*i-f*h, f*g-d*i, d*h-e*g,
                    c*h-b*i, a*i-c*g, b*g-a*h, b*f-c*e, c*d-a*f, a*e-b*d };
                double change = 0;
                for (int element = 0; element < 9; element++)
                {
                    double next = (rotation[element] + inverseTranspose[element] / determinant) * .5;
                    change = Math.Max(change, Math.Abs(next - rotation[element]));
                    rotation[element] = next;
                }
                if (change < 1e-10) { converged = true; break; }
            }
            if (!converged) throw new InvalidDataException("Pose rotation did not converge.");
            var result = (float[])matrix.Clone();
            for (int column = 0; column < 3; column++)
            {
                double scale = 0;
                for (int row = 0; row < 3; row++) scale += rotation[row * 3 + column] * basis[row * 3 + column];
                if (Math.Abs(scale) < 1e-6) throw new InvalidDataException("Pose matrix has a zero scale.");
                for (int row = 0; row < 3; row++) result[row * 4 + column] = (float)(rotation[row * 3 + column] * scale);
            }
            return result;
        }

        internal static float[] Inverse(float[] m)
        {
            double a=m[0],b=m[1],c=m[2],d=m[4],e=m[5],f=m[6],g=m[8],h=m[9],i=m[10];
            double determinant=a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g);
            if (Math.Abs(determinant)<.00001) throw new InvalidDataException(L.T("Diese Zuordnung lässt sich in der RR-Pose nicht stabil abbilden. Den betroffenen Bereich klarer einem Körperteil zuordnen.", "This assignment cannot be mapped reliably in the RR pose. Assign the affected area more clearly to one body part."));
            var result=new[] {(float)((e*i-f*h)/determinant),(float)((c*h-b*i)/determinant),(float)((b*f-c*e)/determinant),0f,
                (float)((f*g-d*i)/determinant),(float)((a*i-c*g)/determinant),(float)((c*d-a*f)/determinant),0f,
                (float)((d*h-e*g)/determinant),(float)((b*g-a*h)/determinant),(float)((a*e-b*d)/determinant),0f,0f,0f,0f,1f};
            var translation=Point(result,new[] {-m[3],-m[7],-m[11]});result[3]=translation[0];result[7]=translation[1];result[11]=translation[2];return result;
        }
    }
}
