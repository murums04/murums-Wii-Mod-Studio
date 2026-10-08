using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed partial class ModelRig
    {
        public sealed class HandGrip { public float[] Point, Axis; }
        public sealed class GripVertex { public int Index; public float[] Point, Normal; }
        public Dictionary<string, HandGrip> SourceHandGrips;
        public GripVertex[] GripVertices;

        void ValidateHandGrips()
        {
            if (SourceHandGrips == null && GripVertices == null) return;
            if (SourceHandGrips == null || GripVertices == null || SourceHandGrips.Count > 2
                || SourceHandGrips.Any(p => (p.Key != "wrist_l1" && p.Key != "wrist_r1") || p.Value == null
                    || !ValidGameVector(p.Value.Point) || !ValidGameVector(p.Value.Axis)
                    || Math.Abs(RigVector.Length(p.Value.Axis) - 1) > .001)
                || GripVertices.Length > Points.Length || GripVertices.Any(v => v == null || v.Index < 0 || v.Index >= Points.Length
                    || !ValidGameVector(v.Point) || !ValidGameVector(v.Normal))
                || GripVertices.Select(v => v.Index).Distinct().Count() != GripVertices.Length)
                throw new InvalidDataException("Invalid source hand grip.");
        }

        float[][] GripGeometry(bool normals, GamePoseSettings settings)
        {
            var source = normals ? Normals : Points;
            if (GripVertices == null || settings == null || settings.Contacts == null
                || !settings.Contacts.Any(c => c.SourceAxis != null)) return source;
            var result = (float[][])source.Clone();
            var wrists = new HashSet<int>(settings.Contacts.Where(c => c.SourceAxis != null).Select(c => PoseBone(c.Joint)));
            foreach (var vertex in GripVertices)
                if (!RigidVertex(vertex.Index) && BoneIndices[vertex.Index].Any(wrists.Contains))
                    result[vertex.Index] = normals ? vertex.Normal : vertex.Point;
            return result;
        }

        double[] PoseRotation(int bone, float[] from, float[] to, GamePoseSettings settings)
        {
            var rotation = RigVector.RotationQuaternion(from, to);
            if (settings == null || settings.Contacts == null) return rotation;
            var contact = settings.Contacts.FirstOrDefault(c => c.Joint == Bones[bone].Name && c.SourceAxis != null && c.TargetAxis != null);
            if (contact == null) return rotation;
            var direction = RigVector.Unit(to);
            var source = RigVector.RotateQuaternion(contact.SourceAxis, rotation);
            source = RigVector.Unit(RigVector.Sub(source, RigVector.Scale(direction, RigVector.Dot(source, direction))));
            var target = RigVector.Unit(RigVector.Sub(contact.TargetAxis, RigVector.Scale(direction, RigVector.Dot(contact.TargetAxis, direction))));
            if (RigVector.Length(source) < .9 || RigVector.Length(target) < .9) return rotation;
            double angle = Math.Atan2(RigVector.Dot(direction, RigVector.Cross(source, target)), RigVector.Dot(source, target)) * .5;
            var roll = new[] { Math.Cos(angle), direction[0] * Math.Sin(angle), direction[1] * Math.Sin(angle), direction[2] * Math.Sin(angle) };
            return RigVector.MultiplyQuaternion(roll, rotation);
        }
    }
}
