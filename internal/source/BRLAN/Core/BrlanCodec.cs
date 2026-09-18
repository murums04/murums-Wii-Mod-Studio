using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace murumsWiiModStudio.Brlan
{
    internal static class BrlanCodec
    {
        public static BrlanDocument Parse(byte[] data)
        {
            if (data == null || data.Length < 16)
                throw new InvalidDataException(L.T("Die Datei ist zu klein für einen BRLAN-Header.", "The file is too small for a BRLAN header."));
            if (Ascii(data, 0, 4) != "RLAN")
                throw new InvalidDataException(L.T("Magic ist nicht RLAN. Die Datei ist keine unterstützte BRLAN.", "Magic is not RLAN. The file is not a supported BRLAN."));
            ushort bomBig = ReadU16RawBig(data, 4);
            bool little;
            if (bomBig == 0xFEFF)
                little = false;
            else if (bomBig == 0xFFFE)
                little = true;
            else
                throw new InvalidDataException(L.T("Ungültige Byte-Order-Mark: 0x", "Invalid byte-order mark: 0x") + bomBig.ToString("X4"));
            ushort version = ReadU16(data, 6, little);
            uint fileSize = ReadU32(data, 8, little);
            ushort headerSize = ReadU16(data, 12, little);
            ushort sectionCount = ReadU16(data, 14, little);
            if (headerSize < 16 || headerSize > data.Length)
                throw new InvalidDataException(L.T("Ungültige Headergrösse: ", "Invalid header size: ") + headerSize.ToString(CultureInfo.InvariantCulture));
            if (fileSize > data.Length)
                throw new InvalidDataException(L.T("Die deklarierte Dateigrösse ist grösser als die tatsächliche Datei.", "The declared file size is larger than the actual file."));
            BrlanDocument doc = new BrlanDocument();
            doc.Bom = 0xFEFF;
            doc.LittleEndian = little;
            doc.Version = version;
            doc.HeaderSize = headerSize;
            doc.HeaderRaw = Slice(data, 0, headerSize);
            doc.OriginalBytes = Clone(data);
            int pos = headerSize;
            int s;
            for (s = 0; s < sectionCount; s++)
            {
                if (pos + 8 > data.Length)
                    throw new InvalidDataException(L.T("Sektionsheader liegt ausserhalb der Datei.", "Section header is outside the file."));
                string magic = Ascii(data, pos, 4);
                int size = CheckedInt(ReadU32(data, pos + 4, little));
                if (size < 8 || pos + size > data.Length)
                    throw new InvalidDataException(L.T("Ungültige Sektionsgrösse bei ", "Invalid section size at ") + magic + ".");
                BrlanSection section = new BrlanSection();
                section.Magic = magic;
                section.Raw = Slice(data, pos, size);
                if (magic == "pai1")
                {
                    if (doc.Pai != null)
                        throw new InvalidDataException(L.T("Mehr als eine pai1-Sektion wird derzeit nicht unterstützt.", "More than one pai1 section is currently not supported."));
                    doc.Pai = ParsePai(section.Raw, little);
                    section.IsPai = true;
                }

                doc.Sections.Add(section);
                pos += size;
                if (s + 1 < sectionCount)
                {
                    int aligned = (pos + 3) & ~3;
                    if (aligned > data.Length)
                        throw new InvalidDataException(L.T("Sektionspadding liegt ausserhalb der Datei.", "Section padding is outside the file."));
                    section.TrailingPadding = Slice(data, pos, aligned - pos);
                    pos = aligned;
                }
                else
                {
                    section.TrailingPadding = new byte[0];
                }
            }

            int declaredEnd = fileSize < (uint)data.Length ? (int)fileSize : data.Length;
            if (pos < declaredEnd)
                doc.TrailingFileData = Slice(data, pos, declaredEnd - pos);
            else
                doc.TrailingFileData = new byte[0];
            if (doc.Pai == null)
                throw new InvalidDataException(L.T("Keine pai1-Sektion gefunden. Diese Datei kann nicht strukturiert bearbeitet werden.", "No pai1 section found. This file cannot be edited structurally."));
            return doc;
        }

        private static PaiSection ParsePai(byte[] raw, bool little)
        {
            if (raw.Length < 20 || Ascii(raw, 0, 4) != "pai1")
                throw new InvalidDataException(L.T("Ungültige pai1-Sektion.", "Invalid pai1 section."));
            int sectionSize = CheckedInt(ReadU32(raw, 4, little));
            if (sectionSize > raw.Length)
                throw new InvalidDataException(L.T("pai1-Sektionsgrösse ist inkonsistent.", "pai1 section size is inconsistent."));
            PaiSection pai = new PaiSection();
            pai.OriginalRaw = Clone(raw);
            pai.Frames = ReadU16(raw, 8, little);
            pai.Flags = raw[10];
            pai.UnknownByte = raw[11];
            int textureCount = ReadU16(raw, 12, little);
            int animationCount = ReadU16(raw, 14, little);
            int animationOffsetTable = CheckedInt(ReadU32(raw, 16, little));
            if (20 + textureCount * 4 > raw.Length)
                throw new InvalidDataException(L.T("TPL-Offsetliste liegt ausserhalb von pai1.", "TPL offset list is outside pai1."));
            int textureTableStart = 20;
            int i;
            for (i = 0; i < textureCount; i++)
            {
                int rel = CheckedInt(ReadU32(raw, textureTableStart + i * 4, little));
                int absolute = textureTableStart + rel;
                if (absolute < 0 || absolute >= raw.Length)
                    throw new InvalidDataException(L.T("Ungültiger TPL-Dateinamen-Offset.", "Invalid TPL filename offset."));
                pai.Textures.Add(ReadZAscii(raw, absolute, raw.Length));
            }

            if (animationOffsetTable < 20 || animationOffsetTable + animationCount * 4 > raw.Length)
                throw new InvalidDataException(L.T("Animations-Offsetliste liegt ausserhalb von pai1.", "Animation offset list is outside pai1."));
            int[] animOffsets = new int[animationCount];
            for (i = 0; i < animationCount; i++)
            {
                animOffsets[i] = CheckedInt(ReadU32(raw, animationOffsetTable + i * 4, little));
                if (animOffsets[i] < 0 || animOffsets[i] >= raw.Length)
                    throw new InvalidDataException(L.T("Ungültiger Animations-Offset.", "Invalid animation offset."));
            }

            for (i = 0; i < animationCount; i++)
            {
                int start = animOffsets[i];
                int end = (i + 1 < animationCount) ? animOffsets[i + 1] : raw.Length;
                pai.Animations.Add(ParseAnimation(raw, start, end, little));
            }

            return pai;
        }

        private static AnimationModel ParseAnimation(byte[] raw, int start, int end, bool little)
        {
            if (start < 0 || end > raw.Length || start + 24 > end)
                throw new InvalidDataException(L.T("Animationseintrag ist beschädigt oder zu klein.", "Animation entry is damaged or too small."));
            AnimationModel anim = new AnimationModel();
            anim.Name = Ascii(raw, start, 20);
            int tagCount = raw[start + 20];
            anim.TargetKind = raw[start + 21];
            anim.Unknown16 = ReadU16(raw, start + 22, little);
            if (start + 24 + tagCount * 4 > end)
                throw new InvalidDataException(L.T("Tag-Offsetliste liegt ausserhalb des Animationseintrags.", "Tag offset list is outside the animation entry."));
            int[] tagOffsets = new int[tagCount];
            int i;
            for (i = 0; i < tagCount; i++)
            {
                tagOffsets[i] = CheckedInt(ReadU32(raw, start + 24 + i * 4, little));
                if (tagOffsets[i] < 24 || start + tagOffsets[i] >= end)
                    throw new InvalidDataException(L.T("Ungültiger Animationstag-Offset.", "Invalid animation tag offset."));
            }

            for (i = 0; i < tagCount; i++)
            {
                int tagStart = start + tagOffsets[i];
                int tagEnd = (i + 1 < tagCount) ? start + tagOffsets[i + 1] : end;
                anim.Tags.Add(ParseTag(raw, tagStart, tagEnd, little));
            }

            return anim;
        }

        private static TagModel ParseTag(byte[] data, int start, int end, bool little)
        {
            if (start + 8 > end)
                throw new InvalidDataException(L.T("Animationstag ist zu klein.", "Animation tag is too small."));
            TagModel tag = new TagModel();
            tag.Magic = Ascii(data, start, 4);
            tag.HeaderUnknown1 = data[start + 5];
            tag.HeaderUnknown2 = data[start + 6];
            tag.HeaderUnknown3 = data[start + 7];
            tag.Raw = Slice(data, start, end - start);
            if (!BrlanNames.IsKnownTag(tag.Magic))
            {
                tag.RawOnly = true;
                return tag;
            }

            int entryCount = data[start + 4];
            if (start + 8 + entryCount * 4 > end)
            {
                tag.RawOnly = true;
                return tag;
            }

            int[] entryOffsets = new int[entryCount];
            int i;
            for (i = 0; i < entryCount; i++)
            {
                entryOffsets[i] = CheckedInt(ReadU32(data, start + 8 + i * 4, little));
                if (entryOffsets[i] < 8 || start + entryOffsets[i] >= end)
                {
                    tag.RawOnly = true;
                    tag.Entries.Clear();
                    return tag;
                }
            }

            for (i = 0; i < entryCount; i++)
            {
                int es = start + entryOffsets[i];
                int ee = (i + 1 < entryCount) ? start + entryOffsets[i + 1] : end;
                EntryModel entry = ParseEntry(data, es, ee, little);
                if (entry == null)
                {
                    tag.RawOnly = true;
                    tag.Entries.Clear();
                    return tag;
                }

                tag.Entries.Add(entry);
            }

            tag.RawOnly = false;
            return tag;
        }

        private static EntryModel ParseEntry(byte[] data, int start, int end, bool little)
        {
            if (start + 12 > end)
                return null;
            EntryModel entry = new EntryModel();
            entry.Index = data[start + 0];
            entry.Target = data[start + 1];
            entry.KeyType = data[start + 2];
            entry.UnknownByte = data[start + 3];
            int keyCount = ReadU16(data, start + 4, little);
            entry.Unknown16 = ReadU16(data, start + 6, little);
            int keyOffset = CheckedInt(ReadU32(data, start + 8, little));
            if (entry.KeyType != 1 && entry.KeyType != 2)
                return null;
            int p = start + keyOffset;
            int stride = entry.KeyType == 1 ? 8 : 12;
            if (p < start || p + keyCount * stride > end)
                return null;
            int i;
            for (i = 0; i < keyCount; i++)
            {
                KeyframeModel key = new KeyframeModel();
                key.Frame = ReadF32(data, p, little);
                if (entry.KeyType == 1)
                {
                    key.UIntValue = ReadU16(data, p + 4, little);
                    key.Padding = ReadU16(data, p + 6, little);
                }
                else
                {
                    key.FloatValue = ReadF32(data, p + 4, little);
                    key.Blend = ReadF32(data, p + 8, little);
                }

                entry.Keys.Add(key);
                p += stride;
            }

            return entry;
        }

        public static byte[] Build(BrlanDocument doc)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");
            if (doc.Pai == null)
                throw new InvalidDataException(L.T("Keine pai1-Sektion vorhanden.", "No pai1 section present."));
            List<byte[]> sections = new List<byte[]>();
            List<byte[]> paddings = new List<byte[]>();
            int i;
            ushort oldPaiFrames = ReadU16(doc.Pai.OriginalRaw, 8, doc.LittleEndian);
            for (i = 0; i < doc.Sections.Count; i++)
            {
                BrlanSection section = doc.Sections[i];
                byte[] raw;
                if (section.IsPai)
                    raw = BuildPai(doc.Pai, doc.LittleEndian);
                else
                    raw = Clone(section.Raw);
                if (section.Magic == "pat1")
                    PatchPat1FrameHeuristics(raw, doc.Pai.Frames, doc.LittleEndian, section.Raw, oldPaiFrames);
                sections.Add(raw);
                if (i + 1 < doc.Sections.Count)
                {
                    int required = (4 - (raw.Length & 3)) & 3;
                    if (section.TrailingPadding != null && section.TrailingPadding.Length == required)
                        paddings.Add(Clone(section.TrailingPadding));
                    else
                        paddings.Add(new byte[required]);
                }
                else
                {
                    paddings.Add(new byte[0]);
                }
            }

            byte[] trailer = doc.TrailingFileData == null ? new byte[0] : Clone(doc.TrailingFileData);
            int total = doc.HeaderSize + trailer.Length;
            for (i = 0; i < sections.Count; i++)
                total += sections[i].Length + paddings[i].Length;
            byte[] header;
            if (doc.HeaderRaw != null && doc.HeaderRaw.Length == doc.HeaderSize)
                header = Clone(doc.HeaderRaw);
            else
                header = new byte[doc.HeaderSize];
            WriteAscii(header, 0, "RLAN");
            if (doc.LittleEndian)
            {
                header[4] = 0xFF;
                header[5] = 0xFE;
            }
            else
            {
                header[4] = 0xFE;
                header[5] = 0xFF;
            }

            WriteU16(header, 6, doc.Version, doc.LittleEndian);
            WriteU32(header, 8, (uint)total, doc.LittleEndian);
            WriteU16(header, 12, doc.HeaderSize, doc.LittleEndian);
            WriteU16(header, 14, (ushort)sections.Count, doc.LittleEndian);
            byte[] output = new byte[total];
            Buffer.BlockCopy(header, 0, output, 0, header.Length);
            int pos = doc.HeaderSize;
            for (i = 0; i < sections.Count; i++)
            {
                Buffer.BlockCopy(sections[i], 0, output, pos, sections[i].Length);
                pos += sections[i].Length;
                if (paddings[i].Length > 0)
                {
                    Buffer.BlockCopy(paddings[i], 0, output, pos, paddings[i].Length);
                    pos += paddings[i].Length;
                }
            }

            if (trailer.Length > 0)
                Buffer.BlockCopy(trailer, 0, output, pos, trailer.Length);
            return output;
        }

        private static byte[] BuildPai(PaiSection pai, bool little)
        {
            int textureCount = pai.Textures.Count;
            int animationCount = pai.Animations.Count;
            if (textureCount > 65535)
                throw new InvalidDataException(L.T("Zu viele TPL-Dateinamen.", "Too many TPL filenames."));
            if (animationCount > 65535)
                throw new InvalidDataException(L.T("Zu viele Animationseinträge.", "Too many animation entries."));
            byte[] header = new byte[20];
            WriteAscii(header, 0, "pai1");
            WriteU16(header, 8, pai.Frames, little);
            header[10] = pai.Flags;
            header[11] = pai.UnknownByte;
            WriteU16(header, 12, (ushort)textureCount, little);
            WriteU16(header, 14, (ushort)animationCount, little);
            byte[] textureOffsets = new byte[textureCount * 4];
            MemoryStream strings = new MemoryStream();
            int textureTableStart = 20;
            int stringSectionOffset = textureTableStart + textureOffsets.Length;
            int i;
            for (i = 0; i < textureCount; i++)
            {
                string name = pai.Textures[i] == null ? "" : pai.Textures[i];
                EnsureAscii(name, "TPL-Dateiname");
                int absoluteInPai = stringSectionOffset + (int)strings.Length;
                int relativeToOffsetArray = absoluteInPai - textureTableStart;
                WriteU32(textureOffsets, i * 4, (uint)relativeToOffsetArray, little);
                byte[] nameBytes = Encoding.ASCII.GetBytes(name);
                strings.Write(nameBytes, 0, nameBytes.Length);
                strings.WriteByte(0);
            }

            MemoryStream prefixStream = new MemoryStream();
            prefixStream.Write(header, 0, header.Length);
            prefixStream.Write(textureOffsets, 0, textureOffsets.Length);
            byte[] stringBytes = strings.ToArray();
            prefixStream.Write(stringBytes, 0, stringBytes.Length);
            while ((prefixStream.Length & 3) != 0)
                prefixStream.WriteByte(0);
            byte[] prefix = prefixStream.ToArray();
            int animationOffsetTable = prefix.Length;
            WriteU32(prefix, 16, (uint)animationOffsetTable, little);
            List<byte[]> animations = new List<byte[]>();
            for (i = 0; i < pai.Animations.Count; i++)
                animations.Add(BuildAnimation(pai.Animations[i], little));
            byte[] animationOffsets = new byte[animationCount * 4];
            int animationPos = animationOffsetTable + animationOffsets.Length;
            for (i = 0; i < animations.Count; i++)
            {
                WriteU32(animationOffsets, i * 4, (uint)animationPos, little);
                animationPos += animations[i].Length;
            }

            MemoryStream result = new MemoryStream();
            result.Write(prefix, 0, prefix.Length);
            result.Write(animationOffsets, 0, animationOffsets.Length);
            for (i = 0; i < animations.Count; i++)
                result.Write(animations[i], 0, animations[i].Length);
            byte[] bytes = result.ToArray();
            WriteU32(bytes, 4, (uint)bytes.Length, little);
            return bytes;
        }

        private static byte[] BuildAnimation(AnimationModel anim, bool little)
        {
            EnsureAscii(anim.Name, "Animationsname");
            if (Encoding.ASCII.GetByteCount(anim.Name) > 20)
                throw new InvalidDataException(L.T("Animationsname ist länger als 20 ASCII-Zeichen: ", "Animation name is longer than 20 ASCII characters: ") + anim.Name);
            if (anim.Tags.Count > 255)
                throw new InvalidDataException(L.T("Eine Animation kann höchstens 255 Tags enthalten.", "An animation can contain at most 255 tags."));
            List<byte[]> tags = new List<byte[]>();
            int i;
            for (i = 0; i < anim.Tags.Count; i++)
                tags.Add(BuildTag(anim.Tags[i], little));
            int headerSize = 24 + tags.Count * 4;
            byte[] header = new byte[headerSize];
            WriteFixedAscii(header, 0, 20, anim.Name);
            header[20] = (byte)tags.Count;
            header[21] = anim.TargetKind;
            WriteU16(header, 22, anim.Unknown16, little);
            int pos = headerSize;
            for (i = 0; i < tags.Count; i++)
            {
                WriteU32(header, 24 + i * 4, (uint)pos, little);
                pos += tags[i].Length;
            }

            MemoryStream ms = new MemoryStream();
            ms.Write(header, 0, header.Length);
            for (i = 0; i < tags.Count; i++)
                ms.Write(tags[i], 0, tags[i].Length);
            return ms.ToArray();
        }

        internal static byte[] BuildTag(TagModel tag, bool little)
        {
            if (tag.RawOnly)
            {
                if (tag.Raw == null || tag.Raw.Length < 8)
                    throw new InvalidDataException(L.T("Raw-Tag ", "Raw tag ") + tag.Magic + L.T(" besitzt keine gültigen Rohdaten.", " has no valid raw data."));
                return Clone(tag.Raw);
            }

            if (!BrlanNames.IsKnownTag(tag.Magic))
            {
                if (tag.Raw != null)
                    return Clone(tag.Raw);
                throw new InvalidDataException(L.T("Unbekannter Tag ohne Raw-Daten: ", "Unknown tag without raw data: ") + tag.Magic);
            }

            if (tag.Entries.Count > 255)
                throw new InvalidDataException(L.T("Ein Tag kann höchstens 255 Entries enthalten.", "A tag can contain at most 255 entries."));
            List<byte[]> entries = new List<byte[]>();
            int i;
            for (i = 0; i < tag.Entries.Count; i++)
                entries.Add(BuildEntry(tag.Entries[i], little));
            int headSize = 8 + entries.Count * 4;
            byte[] head = new byte[headSize];
            WriteAscii(head, 0, tag.Magic);
            head[4] = (byte)entries.Count;
            head[5] = tag.HeaderUnknown1;
            head[6] = tag.HeaderUnknown2;
            head[7] = tag.HeaderUnknown3;
            int pos = headSize;
            for (i = 0; i < entries.Count; i++)
            {
                WriteU32(head, 8 + i * 4, (uint)pos, little);
                pos += entries[i].Length;
            }

            MemoryStream ms = new MemoryStream();
            ms.Write(head, 0, head.Length);
            for (i = 0; i < entries.Count; i++)
                ms.Write(entries[i], 0, entries[i].Length);
            return ms.ToArray();
        }

        internal static byte[] BuildEntry(EntryModel entry, bool little)
        {
            if (entry.KeyType != 1 && entry.KeyType != 2)
                throw new InvalidDataException(L.T("Nur Keyframe-Typ 1 und 2 sind dokumentiert. Gefunden: ", "Only keyframe types 1 and 2 are documented. Found: ") + entry.KeyType.ToString());
            if (entry.Keys.Count > 65535)
                throw new InvalidDataException(L.T("Zu viele Keyframes in einem Entry.", "Too many keyframes in an entry."));
            int stride = entry.KeyType == 1 ? 8 : 12;
            byte[] result = new byte[12 + entry.Keys.Count * stride];
            result[0] = entry.Index;
            result[1] = entry.Target;
            result[2] = entry.KeyType;
            result[3] = entry.UnknownByte;
            WriteU16(result, 4, (ushort)entry.Keys.Count, little);
            WriteU16(result, 6, entry.Unknown16, little);
            WriteU32(result, 8, 12, little);
            int p = 12;
            int i;
            for (i = 0; i < entry.Keys.Count; i++)
            {
                KeyframeModel key = entry.Keys[i];
                WriteF32(result, p, key.Frame, little);
                if (entry.KeyType == 1)
                {
                    WriteU16(result, p + 4, key.UIntValue, little);
                    WriteU16(result, p + 6, key.Padding, little);
                }
                else
                {
                    WriteF32(result, p + 4, key.FloatValue, little);
                    WriteF32(result, p + 8, key.Blend, little);
                }

                p += stride;
            }

            return result;
        }

        private static void PatchPat1FrameHeuristics(byte[] pat, ushort newFrames, bool little, byte[] originalPat, ushort oldPaiFrames)
        {
            if (pat == null || originalPat == null)
                return;
            if (pat.Length < 0x1C || originalPat.Length < 0x1C)
                return;
            if (Ascii(originalPat, 0, 4) != "pat1")
                return;
            // pat1 ist nur teilweise dokumentiert. Darum werden ausschliesslich
            // Felder angepasst, deren ORIGINALWERT exakt zur alten pai1-Framezahl passt.
            // Alles andere bleibt bytegenau unverändert.
            short negFrames = ReadI16(originalPat, 0x0A, little);
            if (negFrames < 0 && -(int)negFrames == oldPaiFrames && newFrames <= 32767)
                WriteI16(pat, 0x0A, (short)-(int)newFrames, little);
            short negTotalFrames = ReadI16(originalPat, 0x14, little);
            if (negTotalFrames < 0 && -(int)negTotalFrames == oldPaiFrames && newFrames <= 32767)
                WriteI16(pat, 0x14, (short)-(int)newFrames, little);
            // Einige MKW-Dateien (darunter bg_Loop.brlan) tragen die positive
            // Framezahl im laut Dokumentation unbekannten UInt16 bei +0x16.
            // Auch dieses Feld wird nur bei exakter Übereinstimmung geändert.
            ushort possiblePositiveFrames = ReadU16(originalPat, 0x16, little);
            if (possiblePositiveFrames == oldPaiFrames)
                WriteU16(pat, 0x16, newFrames, little);
        }

        public static List<ValidationIssue> Validate(BrlanDocument doc)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            if (doc == null)
            {
                issues.Add(new ValidationIssue("Fehler", "Datei", L.T("Keine BRLAN geladen.", "No BRLAN loaded.")));
                return issues;
            }

            if (doc.Pai == null)
            {
                issues.Add(new ValidationIssue("Fehler", "pai1", L.T("Keine pai1-Sektion vorhanden.", "No pai1 section present.")));
                return issues;
            }

            if (doc.Pai.Frames == 0)
                issues.Add(new ValidationIssue("Warnung", "pai1", L.T("Framezahl ist 0.", "Frame count is 0.")));
            Dictionary<string, int> textureNames = new Dictionary<string, int>(StringComparer.Ordinal);
            int i, j, k;
            for (i = 0; i < doc.Pai.Textures.Count; i++)
            {
                string name = doc.Pai.Textures[i] == null ? "" : doc.Pai.Textures[i];
                if (name.Length == 0)
                    issues.Add(new ValidationIssue("Fehler", "TPL[" + i.ToString() + "]", L.T("Leerer TPL-Dateiname.", "Empty TPL filename.")));
                if (!name.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new ValidationIssue("Warnung", "TPL[" + i.ToString() + "]", L.T("Dateiname endet nicht auf .tpl: ", "Filename does not end in .tpl: ") + name));
                if (!IsAscii(name))
                    issues.Add(new ValidationIssue("Fehler", "TPL[" + i.ToString() + "]", L.T("Dateiname enthält Nicht-ASCII-Zeichen.", "Filename contains non-ASCII characters.")));
                if (textureNames.ContainsKey(name))
                    issues.Add(new ValidationIssue("Warnung", "TPL[" + i.ToString() + "]", L.T("Doppelter TPL-Dateiname: ", "Duplicate TPL filename: ") + name));
                else
                    textureNames[name] = i;
            }

            // Pane and material animations may legally share the same visible name.
            // Only name + target kind together must be unique.
            Dictionary<string, int> animNames = new Dictionary<string, int>(StringComparer.Ordinal);
            for (i = 0; i < doc.Pai.Animations.Count; i++)
            {
                AnimationModel anim = doc.Pai.Animations[i];
                string loc = "Animation[" + i.ToString() + "] " + anim.Name;
                if (!IsAscii(anim.Name))
                    issues.Add(new ValidationIssue("Fehler", loc, L.T("Animationsname enthält Nicht-ASCII-Zeichen.", "Animation name contains non-ASCII characters.")));
                if (Encoding.ASCII.GetByteCount(anim.Name) > 20)
                    issues.Add(new ValidationIssue("Fehler", loc, L.T("Animationsname ist länger als 20 Bytes.", "Animation name is longer than 20 bytes.")));
                if (anim.TargetKind > 1)
                    issues.Add(new ValidationIssue("Warnung", loc, L.T("Unbekannter Animation-Target-Typ: ", "Unknown animation target type: ") + anim.TargetKind.ToString()));
                string animIdentity = anim.TargetKind.ToString(CultureInfo.InvariantCulture) + "|" + (anim.Name ?? "");
                if (animNames.ContainsKey(animIdentity))
                    issues.Add(new ValidationIssue("Warnung", loc, L.T("Doppeltes Animationsziel (gleicher Name und Target-Typ).", "Duplicate animation target (same name and target type).")));
                else
                    animNames[animIdentity] = i;
                for (j = 0; j < anim.Tags.Count; j++)
                {
                    TagModel tag = anim.Tags[j];
                    string tagLoc = loc + " / " + tag.Magic;
                    if (tag.RawOnly)
                    {
                        issues.Add(new ValidationIssue("Info", tagLoc, L.T("Raw-only: unbekannte oder nicht vollständig dokumentierte Struktur wird bytegenau erhalten.", "Raw-only: unknown or incompletely documented structure is preserved byte-for-byte.")));
                        continue;
                    }

                    if (!BrlanNames.IsKnownTag(tag.Magic))
                        issues.Add(new ValidationIssue("Warnung", tagLoc, L.T("Unbekannter Tag-Typ.", "Unknown tag type.")));
                    for (k = 0; k < tag.Entries.Count; k++)
                    {
                        EntryModel entry = tag.Entries[k];
                        string entryLoc = tagLoc + " / Entry " + k.ToString();
                        if (entry.KeyType != 1 && entry.KeyType != 2)
                            issues.Add(new ValidationIssue("Fehler", entryLoc, L.T("Nicht unterstützter Keyframe-Typ ", "Unsupported keyframe type ") + entry.KeyType.ToString() + "."));
                        float last = Single.NegativeInfinity;
                        int q;
                        for (q = 0; q < entry.Keys.Count; q++)
                        {
                            KeyframeModel key = entry.Keys[q];
                            if (Single.IsNaN(key.Frame) || Single.IsInfinity(key.Frame))
                                issues.Add(new ValidationIssue("Fehler", entryLoc, L.T("Keyframe ", "Keyframe ") + q.ToString() + L.T(" besitzt ungültigen Frame-Wert.", " has an invalid frame value.")));
                            if (key.Frame < last)
                                issues.Add(new ValidationIssue("Warnung", entryLoc, L.T("Keyframes sind nicht aufsteigend sortiert.", "Keyframes are not sorted in ascending order.")));
                            last = key.Frame;
                            // Nintendo/MKW BRLANs may legitimately place a terminal keyframe
                            // exactly at pai1.Frames (the original bg_Loop does this at 3500).
                            // Only values strictly beyond the declared duration are out of range.
                            if (key.Frame > doc.Pai.Frames)
                                issues.Add(new ValidationIssue("Warnung", entryLoc, L.T("Keyframe ", "Keyframe ") + key.Frame.ToString(CultureInfo.InvariantCulture) + L.T(" liegt ausserhalb der pai1-Framezahl ", " is outside the pai1 frame count ") + doc.Pai.Frames.ToString() + "."));
                            if (tag.Magic == "RLTP" && entry.KeyType == 1 && key.UIntValue >= doc.Pai.Textures.Count)
                                issues.Add(new ValidationIssue("Fehler", entryLoc, L.T("RLTP verweist auf TPL-Index ", "RLTP references TPL index ") + key.UIntValue.ToString() + L.T(", aber es existieren nur ", ", but only ") + doc.Pai.Textures.Count.ToString() + L.T(" TPL-Einträge.", " TPL entries exist.")));
                        }
                    }
                }
            }

            if (issues.Count == 0)
                issues.Add(new ValidationIssue("OK", "Datei", L.T("Keine strukturellen Probleme erkannt.", "No structural problems detected.")));
            return issues;
        }

        public static string HexDump(byte[] data, int maxBytes)
        {
            if (data == null)
                return "";
            int count = Math.Min(data.Length, maxBytes);
            StringBuilder sb = new StringBuilder();
            int i;
            for (i = 0; i < count; i += 16)
            {
                sb.Append(i.ToString("X6"));
                sb.Append("  ");
                int j;
                for (j = 0; j < 16; j++)
                {
                    if (i + j < count)
                        sb.Append(data[i + j].ToString("X2") + " ");
                    else
                        sb.Append("   ");
                }

                sb.Append(" ");
                for (j = 0; j < 16 && i + j < count; j++)
                {
                    byte b = data[i + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }

                sb.AppendLine();
            }

            if (data.Length > maxBytes)
                sb.AppendLine("... Raw-Ansicht gekürzt (" + data.Length.ToString() + " Bytes gesamt).");
            return sb.ToString();
        }

        public static byte[] GetRawForTag(TagModel tag, bool little)
        {
            if (tag == null)
                return null;
            return BuildTag(tag, little);
        }

        private static bool IsAscii(string text)
        {
            if (text == null)
                return true;
            int i;
            for (i = 0; i < text.Length; i++)
                if (text[i] > 127)
                    return false;
            return true;
        }

        private static void EnsureAscii(string text, string label)
        {
            if (!IsAscii(text))
                throw new InvalidDataException(label + L.T(" enthält Nicht-ASCII-Zeichen: ", " contains non-ASCII characters: ") + text);
        }

        private static int CheckedInt(uint value)
        {
            if (value > Int32.MaxValue)
                throw new InvalidDataException(L.T("Offset ist zu gross.", "Offset is too large."));
            return (int)value;
        }

        internal static byte[] Clone(byte[] data)
        {
            if (data == null)
                return null;
            byte[] result = new byte[data.Length];
            Buffer.BlockCopy(data, 0, result, 0, data.Length);
            return result;
        }

        internal static byte[] Slice(byte[] data, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(data, offset, result, 0, length);
            return result;
        }

        internal static string Ascii(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new InvalidDataException(L.T("ASCII-String liegt ausserhalb der Datei.", "ASCII string is outside the file."));
            int end = offset;
            int max = offset + length;
            while (end < max && data[end] != 0)
                end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static string ReadZAscii(byte[] data, int offset, int limit)
        {
            int end = offset;
            while (end < limit && data[end] != 0)
                end++;
            if (end >= limit)
                throw new InvalidDataException(L.T("Nicht terminierter ASCII-String.", "Unterminated ASCII string."));
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static ushort ReadU16RawBig(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        internal static ushort ReadU16(byte[] data, int offset, bool little)
        {
            if (little)
                return (ushort)(data[offset] | (data[offset + 1] << 8));
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        internal static short ReadI16(byte[] data, int offset, bool little)
        {
            return unchecked((short)ReadU16(data, offset, little));
        }

        internal static uint ReadU32(byte[] data, int offset, bool little)
        {
            if (little)
            {
                return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
            }

            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        internal static float ReadF32(byte[] data, int offset, bool little)
        {
            byte[] b = new byte[4];
            if (BitConverter.IsLittleEndian == little)
            {
                Buffer.BlockCopy(data, offset, b, 0, 4);
            }
            else
            {
                b[0] = data[offset + 3];
                b[1] = data[offset + 2];
                b[2] = data[offset + 1];
                b[3] = data[offset];
            }

            return BitConverter.ToSingle(b, 0);
        }

        internal static void WriteU16(byte[] data, int offset, ushort value, bool little)
        {
            if (little)
            {
                data[offset] = (byte)value;
                data[offset + 1] = (byte)(value >> 8);
            }
            else
            {
                data[offset] = (byte)(value >> 8);
                data[offset + 1] = (byte)value;
            }
        }

        internal static void WriteI16(byte[] data, int offset, short value, bool little)
        {
            WriteU16(data, offset, unchecked((ushort)value), little);
        }

        internal static void WriteU32(byte[] data, int offset, uint value, bool little)
        {
            if (little)
            {
                data[offset] = (byte)value;
                data[offset + 1] = (byte)(value >> 8);
                data[offset + 2] = (byte)(value >> 16);
                data[offset + 3] = (byte)(value >> 24);
            }
            else
            {
                data[offset] = (byte)(value >> 24);
                data[offset + 1] = (byte)(value >> 16);
                data[offset + 2] = (byte)(value >> 8);
                data[offset + 3] = (byte)value;
            }
        }

        internal static void WriteF32(byte[] data, int offset, float value, bool little)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian == little)
            {
                Buffer.BlockCopy(b, 0, data, offset, 4);
            }
            else
            {
                data[offset] = b[3];
                data[offset + 1] = b[2];
                data[offset + 2] = b[1];
                data[offset + 3] = b[0];
            }
        }

        internal static void WriteAscii(byte[] data, int offset, string text)
        {
            byte[] b = Encoding.ASCII.GetBytes(text);
            Buffer.BlockCopy(b, 0, data, offset, b.Length);
        }

        internal static void WriteFixedAscii(byte[] data, int offset, int length, string text)
        {
            byte[] b = Encoding.ASCII.GetBytes(text == null ? "" : text);
            int count = Math.Min(length, b.Length);
            Buffer.BlockCopy(b, 0, data, offset, count);
        }
    }
}
