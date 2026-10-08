using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRigForm
    {
        readonly FlowLayoutPanel reviewControls = new FlowLayoutPanel {
            Name = "AnatomyReview", AutoSize = true, Width = 238,
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        readonly Label reviewHint = new Label { AutoSize = true, MaximumSize = new Size(232, 0), ForeColor = DarkTheme.Warning };
        Button confirmChain, confirmJoint, nextJoint;

        void CreateReviewControls(Control parent)
        {
            parent.Controls.Add(reviewControls);
            RigSection(reviewControls, L.T("Gelenkprüfung", "Joint review"));
            reviewControls.Controls.Add(reviewHint);
            confirmJoint = AddButton(reviewControls, L.T("Gelenk bestätigen · Weiter", "Confirm joint · Next"), delegate {
                PushUndo(); Result.ConfirmJoint(SelectedBoneIndex); FinishJointReview();
            });
            confirmJoint.Name = "ConfirmJoint";
            confirmChain = AddButton(reviewControls, L.T("Gelenkkette bestätigen", "Confirm joint chain"), delegate {
                PushUndo(); Result.ConfirmChain(SelectedBoneIndex); FinishJointReview();
            });
            confirmChain.Name = "ConfirmJointChain";
            nextJoint = AddButton(reviewControls, L.T("Nächster unsicherer Punkt", "Next uncertain joint"), SelectNextIssue);
            nextJoint.Name = "NextUncertainJoint";

        }

        void SelectNextIssue()
        {
            var issues = Result.JointIssues();
            var indices = issues.Keys.OrderBy(i => i).ToArray();
            if (indices.Length == 0) return;
            int next = Array.FindIndex(indices, i => i > SelectedBoneIndex);
            SelectBone(indices[next < 0 ? 0 : next]);
            mode.SelectedIndex = 0;
            RefreshReview();
        }

        void FinishJointReview()
        {
            pending = true;
            if (Result.JointIssues().Count == 0)
            {
                if (Reassign()) mode.SelectedIndex = 5;
            }
            else SelectNextIssue();
            RefreshReview(); preview.Invalidate();
        }

        void RefreshReview()
        {
            if (confirmChain == null || apply == null) return;
            var issues = Result.JointIssues();
            bool joints = issues.Count > 0, parts = Result.NeedsComponentReview();
            reviewControls.Visible = joints;
            confirmJoint.Visible = confirmChain.Visible = nextJoint.Visible = joints;
            confirmJoint.Enabled = issues.ContainsKey(SelectedBoneIndex);
            confirmChain.Enabled = issues.ContainsKey(SelectedBoneIndex);
            string reason;
            reviewHint.Text = joints
                ? Result.AnatomyStatus + L.T(". Nur orange Punkte prüfen. Ausgewählten Punkt bei Bedarf verschieben; vorne und seitlich kontrollieren.", ". Review only orange joints. Move the selected point if needed; check front and side views.")
                : L.T("Haare, Kleidung und Zusatzteile erhalten. Bewegung prüfen; starre Teile bei Bedarf an einen Körperteil binden.",
                    "Hair, clothes and accessories retained. Check motion; attach rigid parts to a body part if needed.");
            if (joints && issues.TryGetValue(SelectedBoneIndex, out reason))
                status.Text = ModelRigForm.BoneLabel(Result.Bones[SelectedBoneIndex].Name) + ": " + reason;
            apply.Enabled = !pending && !joints && !parts;
            bone.Visible = mode.SelectedIndex < 3 || mode.SelectedIndex == 3 || mode.SelectedIndex == 4 && editPoseJoints.Checked || mode.SelectedIndex == 5 && surfaceTarget.SelectedIndex == 7;
            RefreshSurfaceReview();
            foreach (string name in new[] { "GamePoseStep", "CheckMovementStep" })
                foreach (Control step in Controls.Find(name, true)) step.Enabled = !pending && !joints;
        }
    }
}
