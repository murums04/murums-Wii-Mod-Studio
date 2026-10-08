using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        FlowLayoutPanel animationEditor;
        readonly List<NumericUpDown> keyValues = new List<NumericUpDown>();
        readonly NumericUpDown animationFrame = new NumericUpDown { Name = "AnimationFrame", Minimum = 1, Maximum = 100001, Width = 70 };
        readonly Label keyStatus = new Label { AutoSize = true, MaximumSize = new Size(232, 0) };
        Button addKey, removeKey;
        bool updatingKey;
        string AnimationName { get { return gameAnimation.SelectedItem is AnimationChoice ? ((AnimationChoice)gameAnimation.SelectedItem).Name : null; } }
        string AnimationBone { get { return SelectedBoneIndex < 0 ? null : Result.Bones[SelectedBoneIndex].Name; } }

        Control CreateAnimationEditor()
        {
            animationEditor = new FlowLayoutPanel { Name = "AnimationKeyEditor", AutoSize = true, Width = 232, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
            RigSection(animationEditor, L.T("Schlüsselbilder", "Keyframes"));
            animationEditor.Controls.Add(new Label { Text = L.T("Gewählter Körperteil", "Selected body part"), AutoSize = true, MaximumSize = new Size(232, 0) });
            animationEditor.Controls.Add(keyStatus);
            addKey = AddButton(animationEditor, L.T("Schlüsselbild hinzufügen", "Add keyframe"), delegate {
                if (AnimationName == null || AnimationBone == null) return;
                play.Checked = false; PushUndo();
                Result.SetAnimationKey(GameContext, AnimationName, Result.AnimationOffset(GameContext, AnimationName, AnimationBone, pose.Value));
                RefreshAnimation();
            });
            for (int kind = 0; kind < 2; kind++)
            {
                animationEditor.Controls.Add(new Label { Text = kind == 0 ? L.T("Drehung X / Y / Z (Grad)", "Rotation X / Y / Z (degrees)") : L.T("Gelenkversatz X / Y / Z", "Joint offset X / Y / Z"), AutoSize = true });
                var row = new FlowLayoutPanel { AutoSize = true, Width = 232, WrapContents = false };
                for (int axis = 0; axis < 3; axis++)
                {
                    var value = new NumericUpDown { Name = "AnimationKey" + kind + axis, Width = 71, DecimalPlaces = 1, Minimum = kind == 0 ? -180 : -1000, Maximum = kind == 0 ? 180 : 1000 };
                    value.ValueChanged += delegate { SaveKeyValues(); };
                    keyValues.Add(value); row.Controls.Add(value);
                }
                animationEditor.Controls.Add(row);
            }
            removeKey = AddButton(animationEditor, L.T("Schlüsselbild löschen", "Delete keyframe"), delegate {
                List<JointAnimationKey> keys;
                var settings = Result.GameSettings(GameContext);
                if (settings == null || settings.Animations == null || AnimationName == null || !settings.Animations.TryGetValue(AnimationName, out keys)) return;
                play.Checked = false; PushUndo();
                keys.RemoveAll(k => k.Bone == AnimationBone && k.Frame == pose.Value);
                if (keys.Count == 0) settings.Animations.Remove(AnimationName);
                RefreshAnimation();
            });
            animationEditor.Controls.Add(new Label {
                Text = L.T("Drehung erhält Gelenkabstände. Zwischen Schlüsseln wird überblendet; außerhalb gilt der jeweilige Randwert.",
                    "Rotation preserves joint lengths. Offsets blend between keys and hold outside the keyed range."),
                AutoSize = true, MaximumSize = new Size(232, 0)
            });
            animationFrame.ValueChanged += delegate {
                if (updatingKey || (mode.SelectedIndex != 3 && mode.SelectedIndex != 4)) return;
                play.Checked = false;
                pose.Value = Math.Max(pose.Minimum, Math.Min(pose.Maximum, (int)animationFrame.Value - 1));
            };
            return animationEditor;
        }

        void SaveKeyValues()
        {
            if (updatingKey || AnimationName == null || AnimationBone == null || mode.SelectedIndex != 3) return;
            play.Checked = false; PushUndo();
            var key = new JointAnimationKey { Frame = pose.Value, Bone = AnimationBone,
                Rotation = keyValues.Take(3).Select(v => (float)v.Value).ToArray(),
                Position = keyValues.Skip(3).Select(v => (float)v.Value).ToArray() };
            Result.SetAnimationKey(GameContext, AnimationName, key);
            RefreshAnimation(); preview.Invalidate();
        }

        void RefreshKeyEditor()
        {
            if (animationEditor == null) return;
            animationEditor.Visible = mode.SelectedIndex == 3 && ActiveReference != null;
            updatingKey = true;
            try
            {
                animationFrame.Maximum = Math.Max(1, pose.Maximum + 1);
                animationFrame.Value = Math.Max(1, pose.Value + 1);
            }
            finally { updatingKey = false; }
            if (!animationEditor.Visible) return;
            updatingKey = true;
            try
            {
                var offset = Result.AnimationOffset(GameContext, AnimationName, AnimationBone, pose.Value);
                List<JointAnimationKey> keys = null;
                var settings = Result.GameSettings(GameContext);
                if (settings.Animations != null && AnimationName != null) settings.Animations.TryGetValue(AnimationName, out keys);
                var track = (keys ?? new List<JointAnimationKey>()).Where(k => k.Bone == AnimationBone).OrderBy(k => k.Frame).ToArray();
                bool exact = track.Any(k => k.Frame == pose.Value);
                keyStatus.Text = (exact ? L.T("Schlüsselbild", "Keyframe") : L.T("Vorschau", "Preview")) + " · " + (pose.Value + 1) + " / " + (pose.Maximum + 1)
                    + (track.Length == 0 ? "" : "\n" + L.T("Schlüssel: ", "Keys: ") + String.Join(", ", track.Take(8).Select(k => (k.Frame + 1).ToString())) + (track.Length > 8 ? " … (" + track.Length + ")" : ""));
                for (int i = 0; i < keyValues.Count; i++)
                {
                    keyValues[i].Value = (decimal)(i < 3 ? offset.Rotation[i] : offset.Position[i - 3]);
                    keyValues[i].Enabled = exact && !play.Checked;
                }
                addKey.Enabled = AnimationBone != null && !exact && !play.Checked;
                removeKey.Enabled = exact && !play.Checked;
            }
            finally { updatingKey = false; }
        }
    }
}
