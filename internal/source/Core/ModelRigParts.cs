using System;
using System.Collections.Generic;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public int[] DisabledBones;
        internal bool BoneEnabled(int index) { return index >= 0 && index < Bones.Length && !(DisabledBones ?? new int[0]).Contains(index); }
        internal string OptionalPart(int index)
        {
            string name = Bones[index].Name.ToLowerInvariant();
            foreach (string part in new[] { "tail", "tie", "mouth", "wing", "ear", "antenna" })
                if (name == part || name.StartsWith(part + "_")) return part;
            return null;
        }
        internal void SetPartEnabled(string part, bool enabled)
        {
            var members = new HashSet<int>(Enumerable.Range(0, Bones.Length).Where(i => OptionalPart(i) == part));
            if (members.Count == 0 || members.Any(i => Bones[i].Parent < 0)) throw new ArgumentException("Unknown optional model part.");
            for (int i = 0; i < Bones.Length; i++)
                if (members.Contains(Bones[i].Parent)) members.Add(i);
            var disabled = new HashSet<int>(DisabledBones ?? new int[0]);
            if (enabled) disabled.ExceptWith(members); else disabled.UnionWith(members);
            DisabledBones = disabled.OrderBy(i => i).ToArray();
            // Manuelle Korrekturen bleiben erhalten; abgeschaltete Gewichte gehen zum aktiven Vorfahren.
            for (int vertex = 0; vertex < Points.Length; vertex++)
            {
                var weights = new Dictionary<int, float>();
                for (int j = 0; j < BoneIndices[vertex].Length; j++)
                {
                    int target = BoneIndices[vertex][j];
                    while (disabled.Contains(target)) target = Bones[target].Parent;
                    weights[target] = (weights.ContainsKey(target) ? weights[target] : 0) + BoneWeights[vertex][j];
                }
                BoneIndices[vertex] = weights.Keys.ToArray();
                BoneWeights[vertex] = weights.Values.ToArray();
            }
            InvalidateAlignment();
            if (JointGuides != null && String.IsNullOrEmpty(BindingMethod)) ReassignFromGuides();
        }
    }
}
