using System;
using System.Collections.Generic;

namespace murumsWiiModStudio.Brlan
{
    internal enum NodeKind
    {
        Root,
        Header,
        Section,
        Pai,
        Textures,
        Texture,
        Animations,
        Animation,
        Tag,
        Entry
    }

    internal sealed class NodeRef
    {
        public NodeKind Kind;
        public object Value;
        public object Parent;
        public NodeRef(NodeKind kind, object value, object parent)
        {
            Kind = kind;
            Value = value;
            Parent = parent;
        }
    }

    internal sealed class BrlanDocument
    {
        public ushort Bom;
        public bool LittleEndian;
        public ushort Version;
        public ushort HeaderSize;
        public byte[] HeaderRaw;
        public readonly List<BrlanSection> Sections = new List<BrlanSection>();
        public PaiSection Pai;
        public byte[] OriginalBytes;
        public byte[] TrailingFileData;
        public string SourcePath;
    }

    internal sealed class BrlanSection
    {
        public string Magic;
        public byte[] Raw;
        public byte[] TrailingPadding;
        public bool IsPai;
    }

    internal sealed class PaiSection
    {
        public ushort Frames;
        public byte Flags;
        public byte UnknownByte;
        public readonly List<string> Textures = new List<string>();
        public readonly List<AnimationModel> Animations = new List<AnimationModel>();
        public byte[] OriginalRaw;
    }

    internal sealed class AnimationModel
    {
        public string Name = "";
        public byte TargetKind;
        public ushort Unknown16;
        public readonly List<TagModel> Tags = new List<TagModel>();
    }

    internal sealed class TagModel
    {
        public string Magic = "";
        public byte HeaderUnknown1;
        public byte HeaderUnknown2;
        public byte HeaderUnknown3;
        public bool RawOnly;
        public byte[] Raw;
        public readonly List<EntryModel> Entries = new List<EntryModel>();
    }

    internal sealed class EntryModel
    {
        public byte Index;
        public byte Target;
        public byte KeyType;
        public byte UnknownByte;
        public ushort Unknown16;
        public readonly List<KeyframeModel> Keys = new List<KeyframeModel>();
    }

    internal sealed class KeyframeModel
    {
        public float Frame;
        public ushort UIntValue;
        public ushort Padding;
        public float FloatValue;
        public float Blend;
        public KeyframeModel Clone()
        {
            KeyframeModel k = new KeyframeModel();
            k.Frame = Frame;
            k.UIntValue = UIntValue;
            k.Padding = Padding;
            k.FloatValue = FloatValue;
            k.Blend = Blend;
            return k;
        }
    }

    internal sealed class ValidationIssue
    {
        public string Severity;
        public string Location;
        public string Message;
        public ValidationIssue(string severity, string location, string message)
        {
            Severity = severity;
            Location = location;
            Message = message;
        }
    }

    internal static class BrlanNames
    {
        public static readonly string[] KnownTags =
        {
            "RLPA",
            "RLTS",
            "RLVI",
            "RLVC",
            "RLMC",
            "RLTP"
        };
        public static bool IsKnownTag(string magic)
        {
            int i;
            for (i = 0; i < KnownTags.Length; i++)
                if (String.Equals(KnownTags[i], magic, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static string AnimationTargetName(byte target)
        {
            if (target == 0)
                return "Pane";
            if (target == 1)
                return "Material";
            return L.T("Unbekannt (", "Unknown (") + target.ToString() + ")";
        }

        public static string TargetName(string magic, byte target)
        {
            string[] names = null;
            if (magic == "RLPA")
            {
                names = new string[]
                {
                    "TranslateX",
                    "TranslateY",
                    "TranslateZ",
                    "RotateX",
                    "RotateY",
                    "RotateZ",
                    "ScaleX",
                    "ScaleY",
                    "SizeW",
                    "SizeH"
                };
            }
            else if (magic == "RLTS")
            {
                names = new string[]
                {
                    "TranslateS",
                    "TranslateT",
                    "Rotate",
                    "ScaleS",
                    "ScaleT"
                };
            }
            else if (magic == "RLVI")
            {
                names = new string[]
                {
                    "Visibility"
                };
            }
            else if (magic == "RLVC")
            {
                names = new string[]
                {
                    "LT_r",
                    "LT_g",
                    "LT_b",
                    "LT_a",
                    "RT_r",
                    "RT_g",
                    "RT_b",
                    "RT_a",
                    "LB_r",
                    "LB_g",
                    "LB_b",
                    "LB_a",
                    "RB_r",
                    "RB_g",
                    "RB_b",
                    "RB_a",
                    "PaneAlpha"
                };
            }
            else if (magic == "RLMC")
            {
                names = new string[]
                {
                    "MaterialColor_r",
                    "MaterialColor_g",
                    "MaterialColor_b",
                    "MaterialColor_a",
                    "TevColor0_r",
                    "TevColor0_g",
                    "TevColor0_b",
                    "TevColor0_a",
                    "TevColor1_r",
                    "TevColor1_g",
                    "TevColor1_b",
                    "TevColor1_a",
                    "TevColor2_r",
                    "TevColor2_g",
                    "TevColor2_b",
                    "TevColor2_a",
                    "TevKonst0_r",
                    "TevKonst0_g",
                    "TevKonst0_b",
                    "TevKonst0_a",
                    "TevKonst1_r",
                    "TevKonst1_g",
                    "TevKonst1_b",
                    "TevKonst1_a",
                    "TevKonst2_r",
                    "TevKonst2_g",
                    "TevKonst2_b",
                    "TevKonst2_a",
                    "TevKonst3_r",
                    "TevKonst3_g",
                    "TevKonst3_b",
                    "TevKonst3_a"
                };
            }
            else if (magic == "RLTP")
            {
                names = new string[]
                {
                    "Image"
                };
            }

            if (names != null && target < names.Length)
                return names[target];
            return "Target 0x" + target.ToString("X2");
        }

        public static byte DefaultKeyType(string magic)
        {
            if (magic == "RLVI" || magic == "RLTP")
                return 1;
            return 2;
        }

        public static string TagDescription(string magic)
        {
            if (magic == "RLPA")
                return L.T("Pane SRT: Position, Rotation, Skalierung und Grösse.", "Pane SRT: position, rotation, scale and size.");
            if (magic == "RLTS")
                return L.T("Texture SRT: Texturverschiebung, Rotation und Skalierung.", "Texture SRT: texture translation, rotation and scale.");
            if (magic == "RLVI")
                return L.T("Visibility: Sichtbarkeit eines Pane-Elements.", "Visibility: visibility of a pane element.");
            if (magic == "RLVC")
                return L.T("Vertex Color: Eckfarben und Pane-Alpha.", "Vertex Color: corner colors and pane alpha.");
            if (magic == "RLMC")
                return L.T("Material Color: Material-, TEV- und Konst-Farben.", "Material Color: material, TEV and konst colors.");
            if (magic == "RLTP")
                return L.T("Texture Pattern: Umschalten zwischen TPL-Bildern.", "Texture Pattern: switch between TPL images.");
            return L.T("Unbekannter Tag. Wird unverändert als Raw-Daten erhalten.", "Unknown tag. Preserved unchanged as raw data.");
        }
    }
}
