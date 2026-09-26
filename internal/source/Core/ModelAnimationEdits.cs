using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace murumsWiiModStudio
{
    internal sealed class JointAnimationKey
    {
        public int Frame;
        public string Bone;
        public float[] Rotation = new float[3];
        public float[] Position = new float[3];
    }

    internal sealed partial class ModelRig
    {
        internal JointAnimationKey AnimationOffset(int context, string animation, string bone, int frame)
        {
            List<JointAnimationKey> keys;
            var settings = GameSettings(context);
            var result = new JointAnimationKey { Frame = frame, Bone = bone };
            if (settings == null || settings.Animations == null || String.IsNullOrEmpty(animation)
                || String.IsNullOrEmpty(bone) || !settings.Animations.TryGetValue(animation, out keys)) return result;
            var track = keys.Where(k => k.Bone == bone).OrderBy(k => k.Frame).ToArray();
            if (track.Length == 0) return result;
            var left = track.LastOrDefault(k => k.Frame <= frame) ?? track[0];
            var right = track.FirstOrDefault(k => k.Frame >= frame) ?? track[track.Length - 1];
            float amount = left.Frame == right.Frame ? 0 : (float)(frame - left.Frame) / (right.Frame - left.Frame);
            for (int axis = 0; axis < 3; axis++)
            {
                result.Rotation[axis] = left.Rotation[axis] + amount * (right.Rotation[axis] - left.Rotation[axis]);
                result.Position[axis] = left.Position[axis] + amount * (right.Position[axis] - left.Position[axis]);
            }
            return result;
        }

        internal void SetAnimationKey(int context, string animation, JointAnimationKey key)
        {
            var settings = GameSettings(context);
            if (settings == null) throw new InvalidOperationException("Choose a game pose first.");
            if (settings.Animations == null) settings.Animations = new Dictionary<string, List<JointAnimationKey>>();
            List<JointAnimationKey> keys;
            if (!settings.Animations.TryGetValue(animation, out keys))
                settings.Animations.Add(animation, keys = new List<JointAnimationKey>());
            keys.RemoveAll(k => k.Bone == key.Bone && k.Frame == key.Frame);
            keys.Add(key);
        }

        void ValidateAnimationEdits(GamePoseSettings settings)
        {
            if (settings.Animations == null) return;
            if (settings.Animations.Count > 256) throw new InvalidDataException("Too many edited animations.");
            foreach (var clip in settings.Animations)
            {
                if (String.IsNullOrWhiteSpace(clip.Key) || clip.Key.Length > 128 || clip.Value == null || clip.Value.Count > 16000)
                    throw new InvalidDataException("Invalid animation track.");
                var seen = new HashSet<string>();
                foreach (var key in clip.Value)
                    if (key == null || key.Frame < 0 || key.Frame > 100000 || !Bones.Any(b => b.Name == key.Bone)
                        || !ValidGameVector(key.Position) || key.Position.Any(v => Math.Abs(v) > 1000)
                        || !ValidGameVector(key.Rotation) || key.Rotation.Any(v => Math.Abs(v) > 180)
                        || !seen.Add(key.Bone + ":" + key.Frame))
                        throw new InvalidDataException("Invalid animation keyframe.");
            }
        }

        internal void AppendAnimationEdits(XmlElement corrections, int context)
        {
            var settings = GameSettings(context);
            if (settings == null || settings.Animations == null) return;
            var document = corrections.OwnerDocument;
            var animatedBones = new HashSet<string>(corrections.ChildNodes.OfType<XmlElement>()
                .Where(n => n.Name == "bone").Select(n => n.GetAttribute("name")));
            foreach (var clip in settings.Animations)
            {
                var animation = document.CreateElement("animation");
                animation.SetAttribute("name", clip.Key);
                foreach (var key in clip.Value.OrderBy(k => k.Frame))
                {
                    string target = key.Bone;
                    if (!animatedBones.Contains(target))
                    {
                        int source = Array.FindIndex(Bones, b => b.Name == key.Bone);
                        int primary = SharedBodyBone(source);
                        target = Enumerable.Range(0, Bones.Length).Where(i => SharedBodyBone(i) == primary)
                            .Select(i => Bones[i].Name).FirstOrDefault(animatedBones.Contains);
                        if (target == null) continue;
                    }
                    var node = document.CreateElement("key");
                    node.SetAttribute("bone", target);
                    node.SetAttribute("frame", key.Frame.ToString(CultureInfo.InvariantCulture));
                    node.SetAttribute("rotation", String.Join(" ", key.Rotation.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                    node.SetAttribute("position", String.Join(" ", key.Position.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                    animation.AppendChild(node);
                }
                corrections.AppendChild(animation);
            }
        }
    }
}
