using System;
using System.ComponentModel;

namespace murumsWiiModStudio.Brlan
{
    internal sealed class HeaderView
    {
        private readonly BrlanDocument _doc;
        public HeaderView(BrlanDocument doc)
        {
            _doc = doc;
        }

        [Category("RLAN"), DisplayName("Version"), Description("BRLAN-Formatversion / BRLAN format version. Wird beim Speichern beibehalten / preserved on save.")]
        public ushort Version
        {
            get
            {
                return _doc.Version;
            }

            set
            {
                _doc.Version = value;
            }
        }

        [Category("RLAN"), DisplayName("Byte Order"), Description("Byte-Reihenfolge / byte order. Mario Kart Wii verwendet / uses nahezu immer / almost always Big Endian.")]
        public string ByteOrder
        {
            get
            {
                return _doc.LittleEndian ? "Little Endian" : "Big Endian";
            }
        }

        [Category("RLAN"), DisplayName("Headergrösse / Header size"), Description("Grösse des RLAN-Headers in Bytes / RLAN header size in bytes.")]
        public ushort HeaderSize
        {
            get
            {
                return _doc.HeaderSize;
            }
        }

        [Category("RLAN"), DisplayName("Sektionen / Sections"), Description("Anzahl der Sektionen / number of sections in the RLAN header.")]
        public int SectionCount
        {
            get
            {
                return _doc.Sections.Count;
            }
        }
    }

    internal sealed class PaiView
    {
        private readonly PaiSection _pai;
        public PaiView(PaiSection pai)
        {
            _pai = pai;
        }

        [Category("pai1"), DisplayName("Frames"), Description("Gesamtlänge der Animation / total animation length.")]
        public ushort Frames
        {
            get
            {
                return _pai.Frames;
            }

            set
            {
                _pai.Frames = value;
            }
        }

        [Category("pai1"), DisplayName("Flags"), Description("Originales Flags-Byte. Unbekannte Bits werden nicht automatisch verändert.")]
        public byte Flags
        {
            get
            {
                return _pai.Flags;
            }

            set
            {
                _pai.Flags = value;
            }
        }

        [Category("pai1"), DisplayName("Loop (Bit 0)"), Description("Komfortschalter für Bit 0 des Flags-Bytes.")]
        public bool Loop
        {
            get
            {
                return (_pai.Flags & 1) != 0;
            }

            set
            {
                if (value)
                    _pai.Flags = (byte)(_pai.Flags | 1);
                else
                    _pai.Flags = (byte)(_pai.Flags & 0xFE);
            }
        }

        [Category("pai1"), DisplayName("Unknown Byte"), Description("Unbekanntes Byte bei pai1+0x0B. Wird beibehalten.")]
        public byte UnknownByte
        {
            get
            {
                return _pai.UnknownByte;
            }

            set
            {
                _pai.UnknownByte = value;
            }
        }

        [Category("pai1"), DisplayName("TPL-Einträge / TPL entries")]
        public int TextureCount
        {
            get
            {
                return _pai.Textures.Count;
            }
        }

        [Category("pai1"), DisplayName("Animationen / Animations")]
        public int AnimationCount
        {
            get
            {
                return _pai.Animations.Count;
            }
        }
    }

    internal sealed class AnimationView
    {
        private readonly AnimationModel _animation;
        public AnimationView(AnimationModel animation)
        {
            _animation = animation;
        }

        [Category("Animation"), DisplayName("Name"), Description("Name des Pane-/Material-Ziels. Maximal 20 ASCII-Bytes.")]
        public string Name
        {
            get
            {
                return _animation.Name;
            }

            set
            {
                _animation.Name = value == null ? "" : value;
            }
        }

        [Category("Animation"), DisplayName("Target-Klasse / Target class"), Description("0 = Pane, 1 = Material.")]
        public byte TargetKind
        {
            get
            {
                return _animation.TargetKind;
            }

            set
            {
                _animation.TargetKind = value;
            }
        }

        [Category("Animation"), DisplayName("Target-Klasse / Target class (text)")]
        public string TargetKindText
        {
            get
            {
                return BrlanNames.AnimationTargetName(_animation.TargetKind);
            }
        }

        [Category("Animation"), DisplayName("Unknown UInt16")]
        public ushort Unknown16
        {
            get
            {
                return _animation.Unknown16;
            }

            set
            {
                _animation.Unknown16 = value;
            }
        }

        [Category("Animation"), DisplayName("Tags")]
        public int TagCount
        {
            get
            {
                return _animation.Tags.Count;
            }
        }
    }

    internal sealed class TagView
    {
        private readonly TagModel _tag;
        public TagView(TagModel tag)
        {
            _tag = tag;
        }

        [Category("Tag"), DisplayName("Typ / Type")]
        public string Magic
        {
            get
            {
                return _tag.Magic;
            }
        }

        [Category("Tag"), DisplayName("Beschreibung / Description")]
        public string DescriptionText
        {
            get
            {
                return BrlanNames.TagDescription(_tag.Magic);
            }
        }

        [Category("Tag"), DisplayName("Raw-only"), Description("True = Struktur wird nicht interpretiert und bytegenau erhalten.")]
        public bool RawOnly
        {
            get
            {
                return _tag.RawOnly;
            }
        }

        [Category("Tag"), DisplayName("Entries")]
        public int EntryCount
        {
            get
            {
                return _tag.Entries.Count;
            }
        }

        [Category("Header"), DisplayName("Unknown 1")]
        public byte HeaderUnknown1
        {
            get
            {
                return _tag.HeaderUnknown1;
            }

            set
            {
                _tag.HeaderUnknown1 = value;
            }
        }

        [Category("Header"), DisplayName("Unknown 2")]
        public byte HeaderUnknown2
        {
            get
            {
                return _tag.HeaderUnknown2;
            }

            set
            {
                _tag.HeaderUnknown2 = value;
            }
        }

        [Category("Header"), DisplayName("Unknown 3")]
        public byte HeaderUnknown3
        {
            get
            {
                return _tag.HeaderUnknown3;
            }

            set
            {
                _tag.HeaderUnknown3 = value;
            }
        }
    }

    internal sealed class EntryView
    {
        private readonly EntryModel _entry;
        private readonly string _tagMagic;
        public EntryView(EntryModel entry, string tagMagic)
        {
            _entry = entry;
            _tagMagic = tagMagic;
        }

        [Category("Entry"), DisplayName("Index"), Description("Ressourcen-/Transformationsindex innerhalb des animierten Pane/Materials.")]
        public byte Index
        {
            get
            {
                return _entry.Index;
            }

            set
            {
                _entry.Index = value;
            }
        }

        [Category("Entry"), DisplayName("Target"), Description("Welche Eigenschaft innerhalb des Tag-Typs animiert wird.")]
        public byte Target
        {
            get
            {
                return _entry.Target;
            }

            set
            {
                _entry.Target = value;
            }
        }

        [Category("Entry"), DisplayName("Target (Text)")]
        public string TargetText
        {
            get
            {
                return BrlanNames.TargetName(_tagMagic, _entry.Target);
            }
        }

        [Category("Entry"), DisplayName("Keyframe-Typ / Keyframe type"), Description("1 = Frame + UInt16, 2 = Frame + Float + Blend.")]
        public byte KeyType
        {
            get
            {
                return _entry.KeyType;
            }

            set
            {
                if (value != 1 && value != 2)
                    throw new ArgumentException(L.T("Nur Keyframe-Typ 1 und 2 sind dokumentiert.", "Only keyframe types 1 and 2 are documented."));
                if (_entry.KeyType == value)
                    return;
                int i;
                if (value == 1)
                {
                    for (i = 0; i < _entry.Keys.Count; i++)
                    {
                        float f = _entry.Keys[i].FloatValue;
                        if (Single.IsNaN(f) || Single.IsInfinity(f))
                            f = 0;
                        if (f < 0)
                            f = 0;
                        if (f > 65535)
                            f = 65535;
                        _entry.Keys[i].UIntValue = (ushort)Math.Round(f);
                    }
                }
                else
                {
                    for (i = 0; i < _entry.Keys.Count; i++)
                        _entry.Keys[i].FloatValue = _entry.Keys[i].UIntValue;
                }

                _entry.KeyType = value;
            }
        }

        [Category("Entry"), DisplayName("Keyframe-Typ / Keyframe type (text)")]
        public string KeyTypeText
        {
            get
            {
                if (_entry.KeyType == 1)
                    return "Type 1: Float Frame + UInt16 Value + UInt16 Padding";
                if (_entry.KeyType == 2)
                    return "Type 2: Float Frame + Float Value + Float Blend";
                return L.T("Unbekannt", "Unknown");
            }
        }

        [Category("Entry"), DisplayName("Unknown Byte")]
        public byte UnknownByte
        {
            get
            {
                return _entry.UnknownByte;
            }

            set
            {
                _entry.UnknownByte = value;
            }
        }

        [Category("Entry"), DisplayName("Unknown UInt16")]
        public ushort Unknown16
        {
            get
            {
                return _entry.Unknown16;
            }

            set
            {
                _entry.Unknown16 = value;
            }
        }

        [Category("Entry"), DisplayName("Keyframes")]
        public int KeyframeCount
        {
            get
            {
                return _entry.Keys.Count;
            }
        }
    }

    internal sealed class SectionView
    {
        private readonly BrlanSection _section;
        public SectionView(BrlanSection section)
        {
            _section = section;
        }

        [Category("Sektion / Section"), DisplayName("Magic")]
        public string Magic
        {
            get
            {
                return _section.Magic;
            }
        }

        [Category("Sektion / Section"), DisplayName("Grösse / Size")]
        public int Size
        {
            get
            {
                return _section.Raw == null ? 0 : _section.Raw.Length;
            }
        }

        [Category("Sektion / Section"), DisplayName("Bearbeitung / Edit mode")]
        public string EditMode
        {
            get
            {
                return _section.IsPai ? L.T("Strukturiert (pai1)", "Structured (pai1)") : L.T("Raw erhalten", "Preserve raw");
            }
        }
    }
}
