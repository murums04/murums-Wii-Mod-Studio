using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace murumsWiiModStudio
{
    internal sealed class ParticleEffect
    {
        internal string Name;
        internal int Offset, Length, ColorOffset, AnimationCount;
        internal readonly List<int> ColorTracks = new List<int>();
        public override string ToString() { return Name; }
    }

    internal sealed class ParticleEffects
    {
        readonly byte[] original;
        internal readonly List<ParticleEffect> Items = new List<ParticleEffect>();
        internal ParticleEffects(byte[] bytes)
        {
            original = (byte[])bytes.Clone();
            Require(0, 0x28);
            if (Tag(0) != "REFF" || Word(4) != 0xfeff || Word(6) != 9
                || Number(8) != bytes.Length || Word(12) != 16 || Word(14) != 1 || Tag(16) != "REFF")
                throw new InvalidDataException("Expected a Wii REFF version 9 resource.");
            int table = checked(0x18 + Number(0x18)), size = Number(table), count = Word(table + 4);
            Require(table, size);
            if (size < 8) throw new InvalidDataException("Invalid effect table.");
            int cursor = table + 8;
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                int length = Word(cursor);
                Require(cursor + 2, length + 8);
                if (length < 1 || cursor + 2 + length + 8 > table + size || original[cursor + 1 + length] != 0)
                    throw new InvalidDataException("Invalid effect name.");
                string name = Encoding.ASCII.GetString(original, cursor + 2, length - 1);
                if (!names.Add(name)) throw new InvalidDataException("Duplicate effect: " + name);
                int offset = checked(table + Number(cursor + 2 + length)), bytesCount = Number(cursor + 6 + length);
                Require(offset, bytesCount);
                if (offset < table + size || bytesCount < 12) throw new InvalidDataException("Invalid effect bounds.");
                if (Items.Any(item => offset < (long)item.Offset + item.Length && item.Offset < (long)offset + bytesCount))
                    throw new InvalidDataException("Overlapping effect data: " + name);
                int emitterSize = Number(offset + 4);
                if (emitterSize != 0x14c) throw new NotSupportedException("Unsupported emitter layout: " + name);
                int particle = checked(offset + 8 + emitterSize), particleSize = Number(particle);
                if (particleSize < 0x88 || (long)particle + 4 + particleSize > (long)offset + bytesCount)
                    throw new InvalidDataException("Invalid particle bounds: " + name);
                int animation = particle + 4 + particleSize;
                Require(animation, 4);
                int animationCount = Word(animation);
                var effect = new ParticleEffect { Name = name, Offset = offset, Length = bytesCount,
                    ColorOffset = particle + 4, AnimationCount = animationCount };
                int emitterTable = checked(animation + 4 + animationCount * 8);
                int emitters = Word(emitterTable);
                int track = checked(emitterTable + 4 + emitters * 8);
                if (track > offset + bytesCount) throw new InvalidDataException("Invalid animation table.");
                for (int a = 0; a < animationCount + emitters; a++)
                {
                    int tableOffset = a < animationCount ? animation + 4 + animationCount * 4 + a * 4
                        : emitterTable + 4 + emitters * 4 + (a - animationCount) * 4;
                    int trackSize = Number(tableOffset);
                    Require(track, trackSize);
                    if (trackSize < 32 || (long)track + trackSize > (long)offset + bytesCount
                        || original[track] != 0xab && original[track] != 0xac)
                        throw new InvalidDataException("Invalid effect animation: " + name);
                    if (a < animationCount && (original[track + 2] & 0x0f) == 0
                        && new[] { 0, 4, 8, 12 }.Contains((int)original[track + 1]))
                        effect.ColorTracks.Add(track);
                    track += trackSize;
                }
                Items.Add(effect);
                cursor += 2 + length + 8;
            }
        }
        void Require(int offset, int count)
        {
            if (offset < 0 || count < 0 || (long)offset + count > original.Length)
                throw new InvalidDataException("Truncated particle effect.");
        }
        int Word(int offset) { Require(offset, 2); return original[offset] * 256 + original[offset + 1]; }
        int Number(int offset) { Require(offset, 4); return checked((int)BigEndian.ReadUInt32(original, offset)); }
        string Tag(int offset) { Require(offset, 4); return Encoding.ASCII.GetString(original, offset, 4); }
        internal string[] Textures(ParticleEffect effect)
        {
            if (!Items.Contains(effect)) throw new ArgumentException("Unknown effect.");
            int cursor = effect.ColorOffset + 0x88;
            var result = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int length = Word(cursor);
                Require(cursor + 2, length);
                if ((long)cursor + 2 + length > effect.ColorOffset + Number(effect.ColorOffset - 4))
                    throw new InvalidDataException("Invalid effect texture name.");
                if (length > 1) result.Add(Encoding.ASCII.GetString(original, cursor + 2, length - 1));
                cursor += 2 + length;
            }
            return result.ToArray();
        }
        internal Color ColorAt(ParticleEffect effect, int slot)
        {
            if (!Items.Contains(effect) || slot < 0 || slot > 3) throw new ArgumentException("Invalid effect colour.");
            int p = effect.ColorOffset + slot * 4;
            return Color.FromArgb(original[p + 3], original[p], original[p + 1], original[p + 2]);
        }
        internal byte[] Recolor(IEnumerable<string> names, Color primary, Color secondary, bool fixedColor = true)
        {
            var selected = new HashSet<string>(names, StringComparer.Ordinal);
            if (selected.Count == 0 || selected.Any(n => !Items.Any(i => i.Name == n)))
                throw new ArgumentException("Choose existing effects.");
            var bytes = (byte[])original.Clone();
            foreach (var effect in Items.Where(i => selected.Contains(i.Name)))
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    int p = effect.ColorOffset + slot * 4;
                    Color color = slot % 2 == 0 ? primary : secondary;
                    bytes[p] = color.R; bytes[p + 1] = color.G; bytes[p + 2] = color.B;
                }
                if (fixedColor)
                    foreach (int track in effect.ColorTracks)
                        bytes[track + 4] |= 8;
            }
            return bytes;
        }
    }
}