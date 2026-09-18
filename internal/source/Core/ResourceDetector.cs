using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    internal enum ResourceKind
    {
        Unknown,
        U8Archive,
        Brlan,
        Brlyt,
        Tpl,
        Brres,
        Kmp,
        Kcl,
        Bmg,
        Brstm,
        Brsar,
        Breff,
        Breft,
        Brfnt,
        Brctr,
        Rel,
        Dol,
        Thp,
        Nw4rResource,
        NintendoAudioResource,
        Bti,
        Lex,
        Ghost,
        DiscImage,
        RarcArchive,
        J3dModel,
        JParticle,
        Image,
        Audio,
        Binary
    }

    internal sealed class ResourceInfo
    {
        public ResourceKind Kind;
        public string DisplayName;
        public string Extension;
        public string Magic;
        public bool NativeEdit;
        public string Module;
        public string Description;
    }

    internal static class ResourceDetector
    {
        public static ResourceInfo Detect(string path)
        {
            byte[] head = new byte[64];
            int count = 0;
            try
            {
                using (FileStream fs = File.OpenRead(path))
                    count = fs.Read(head, 0, head.Length);
            }
            catch
            {
            }

            return Detect(Path.GetFileName(path), head, count);
        }

        public static ResourceInfo Detect(string name, byte[] data)
        {
            return Detect(name, data, data == null ? 0 : Math.Min(data.Length, 64));
        }

        private static ResourceInfo Detect(string name, byte[] head, int count)
        {
            string ext = Path.GetExtension(name ?? "").ToLowerInvariant();
            string fileName = Path.GetFileName(name ?? "").ToLowerInvariant();
            string magic = count >= 4 ? Encoding.ASCII.GetString(head, 0, 4) : "";
            ResourceKind kind = ResourceKind.Unknown;
            if (magic == "Yaz0" || magic == "Yaz1")
            {
                try
                {
                    byte[] inner = Yaz0.ReadMagic(head);
                    if (Encoding.ASCII.GetString(inner) == "bres")
                    {
                        ResourceInfo compressedInfo = Describe(ResourceKind.Brres);
                        compressedInfo.Extension = ext;
                        compressedInfo.Magic = magic + " → bres";
                        compressedInfo.Description += " Yaz0-compressed BRRES; not a U8 archive.";
                        return compressedInfo;
                    }
                }
                catch (InvalidDataException)
                {
                }
            }

            if (magic == "RARC")
                kind = ResourceKind.RarcArchive;
            else if (magic == "bres")
                kind = ResourceKind.Brres;
            else if (magic == "Yaz0" || magic == "Yaz1" || IsU8Magic(head, count) || ext == ".szs" || ext == ".arc" || ext == ".u8")
                kind = ResourceKind.U8Archive;
            else if (magic == "RLAN" || ext == ".brlan")
                kind = ResourceKind.Brlan;
            else if (magic == "RLYT" || ext == ".brlyt")
                kind = ResourceKind.Brlyt;
            else if (IsTplMagic(head, count) || ext == ".tpl")
                kind = ResourceKind.Tpl;
            else if (magic == "bres" || ext == ".brres")
                kind = ResourceKind.Brres;
            else if (magic == "RKMD" || ext == ".kmp")
                kind = ResourceKind.Kmp;
            else if (ext == ".kcl")
                kind = ResourceKind.Kcl;
            else if (magic == "MESG" || ext == ".bmg")
                kind = ResourceKind.Bmg;
            else if (magic == "RSTM" || ext == ".brstm")
                kind = ResourceKind.Brstm;
            else if (magic == "RSAR" || ext == ".brsar")
                kind = ResourceKind.Brsar;
            else if (magic == "REFF" || ext == ".breff")
                kind = ResourceKind.Breff;
            else if (magic == "REFT" || ext == ".breft")
                kind = ResourceKind.Breft;
            else if (magic == "RFNT" || ext == ".brfnt")
                kind = ResourceKind.Brfnt;
            else if (ext == ".brctr")
                kind = ResourceKind.Brctr;
            else if (ext == ".rel")
                kind = ResourceKind.Rel;
            else if (ext == ".dol")
                kind = ResourceKind.Dol;
            else if (magic == "THP\0" || ext == ".thp")
                kind = ResourceKind.Thp;
            else if (magic == "MDL0" || magic == "TEX0" || magic == "PAT0" || magic == "SRT0" || magic == "CHR0" || magic == "CLR0" || magic == "SHP0" || magic == "SCN0" || ext == ".mdl0" || ext == ".tex0" || ext == ".pat0" || ext == ".srt0" || ext == ".chr0" || ext == ".clr0" || ext == ".shp0" || ext == ".scn0")
                kind = ResourceKind.Nw4rResource;
            else if (magic == "RSEQ" || magic == "RWAV" || magic == "RBNK" || magic == "RWSD" || ext == ".brseq" || ext == ".brwav" || ext == ".brbnk" || ext == ".brwsd")
                kind = ResourceKind.NintendoAudioResource;
            else if (ext == ".bti")
                kind = ResourceKind.Bti;
            else if (ext == ".lex")
                kind = ResourceKind.Lex;
            else if (ext == ".rkg" || ext == ".crkg" || ext == ".rksys" || fileName == "rksys.dat")
                kind = ResourceKind.Ghost;
            else if (ext == ".iso" || ext == ".wbfs" || ext == ".wia" || ext == ".gcz" || ext == ".ciso" || ext == ".wdf" || ext == ".wbi")
                kind = ResourceKind.DiscImage;
            else if (magic == "RARC" || ext == ".rarc")
                kind = ResourceKind.RarcArchive;
            else if (magic == "J3D2" || ext == ".bmd" || ext == ".bdl")
                kind = ResourceKind.J3dModel;
            else if (magic == "JEFF" || magic == "JPAC" || ext == ".jpa")
                kind = ResourceKind.JParticle;
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff")
                kind = ResourceKind.Image;
            else if (ext == ".wav" || ext == ".mp3" || ext == ".flac" || ext == ".ogg")
                kind = ResourceKind.Audio;
            else if (ext == ".bin")
                kind = ResourceKind.Binary;
            ResourceInfo info = Describe(kind);
            info.Extension = ext;
            info.Magic = SanitizeMagic(magic);
            return info;
        }

        public static ResourceInfo Describe(ResourceKind kind)
        {
            ResourceInfo i = new ResourceInfo();
            i.Kind = kind;
            switch (kind)
            {
                case ResourceKind.U8Archive:
                    i.DisplayName = "SZS / U8 / Yaz0";
                    i.Module = "Archive";
                    i.NativeEdit = true;
                    i.Description = "Wii U8 archive, optionally Yaz0-compressed.";
                    break;
                case ResourceKind.Brlan:
                    i.DisplayName = "BRLAN";
                    i.Module = "UI Animation";
                    i.NativeEdit = true;
                    i.Description = "Wii layout animation (RLAN), including RLTP texture switching.";
                    break;
                case ResourceKind.Brlyt:
                    i.DisplayName = "BRLYT";
                    i.Module = "UI Layout";
                    i.NativeEdit = true;
                    i.Description = "Wii layout definition (RLYT): panes, materials, texture links and transforms.";
                    break;
                case ResourceKind.Tpl:
                    i.DisplayName = "TPL";
                    i.Module = "Textures";
                    i.NativeEdit = true;
                    i.Description = "Wii/GameCube texture container.";
                    break;
                case ResourceKind.Brres:
                    i.DisplayName = "BRRES";
                    i.Module = "Models";
                    i.Description = "NW4R resource archive containing MDL0/TEX0 and model animations.";
                    break;
                case ResourceKind.Kmp:
                    i.DisplayName = "KMP";
                    i.Module = "Course";
                    i.Description = "Mario Kart Wii course parameter file: starts, routes, checkpoints, objects, cameras and areas.";
                    break;
                case ResourceKind.Kcl:
                    i.DisplayName = "KCL";
                    i.Module = "Collision";
                    i.Description = "Course/object collision geometry and collision flags.";
                    break;
                case ResourceKind.Bmg:
                    i.DisplayName = "BMG";
                    i.Module = "Text";
                    i.NativeEdit = true;
                    i.Description = "Nintendo message file used for localized text.";
                    break;
                case ResourceKind.Brstm:
                    i.DisplayName = "BRSTM";
                    i.Module = "Audio";
                    i.Description = "Nintendo streamed audio used for Mario Kart Wii music.";
                    break;
                case ResourceKind.Brsar:
                    i.DisplayName = "BRSAR";
                    i.Module = "Audio";
                    i.Description = "Nintendo sound archive containing sound metadata and references.";
                    break;
                case ResourceKind.Breff:
                    i.DisplayName = "BREFF";
                    i.Module = "Effects";
                    i.Description = "NW4R particle/effect definitions.";
                    break;
                case ResourceKind.Breft:
                    i.DisplayName = "BREFT";
                    i.Module = "Effects";
                    i.Description = "NW4R effect texture resources.";
                    break;
                case ResourceKind.Brfnt:
                    i.DisplayName = "BRFNT";
                    i.Module = "Fonts";
                    i.Description = "NW4R font resource.";
                    break;
                case ResourceKind.Brctr:
                    i.DisplayName = "BRCTR";
                    i.Module = "UI Control";
                    i.Description = "Mario Kart Wii layout control resource.";
                    break;
                case ResourceKind.Rel:
                    i.DisplayName = "REL";
                    i.Module = "Game code";
                    i.Description = "Wii relocatable executable module.";
                    break;
                case ResourceKind.Dol:
                    i.DisplayName = "DOL";
                    i.Module = "Game code";
                    i.Description = "Wii executable image.";
                    break;
                case ResourceKind.Thp:
                    i.DisplayName = "THP";
                    i.Module = "Video";
                    i.Description = "Nintendo THP video container.";
                    break;
                case ResourceKind.Nw4rResource:
                    i.DisplayName = "NW4R Resource";
                    i.Module = "Models / Animation";
                    i.Description = "NW4R model, texture or animation subresource such as MDL0/TEX0/PAT0/SRT0/CHR0/CLR0.";
                    break;
                case ResourceKind.NintendoAudioResource:
                    i.DisplayName = "Nintendo Sound Resource";
                    i.Module = "Audio";
                    i.Description = "Nintendo sound subresource such as BRSEQ/BRWAV/BRBNK/BRWSD.";
                    break;
                case ResourceKind.Bti:
                    i.DisplayName = "BTI";
                    i.Module = "Textures";
                    i.Description = "Nintendo texture image used by many GameCube/Wii titles.";
                    break;
                case ResourceKind.Lex:
                    i.DisplayName = "LEX";
                    i.Module = "Course / Extensions";
                    i.Description = "LE-CODE track extension data.";
                    break;
                case ResourceKind.Ghost:
                    i.DisplayName = "Ghost / Save";
                    i.Module = "Game Data";
                    i.Description = "Mario Kart ghost or save data such as RKG/CRKG/rksys.dat.";
                    break;
                case ResourceKind.DiscImage:
                    i.DisplayName = "Wii Disc Image";
                    i.Module = "Disc";
                    i.Description = "Wii/GameCube disc or container image such as ISO/WBFS/WIA/GCZ/WDF.";
                    break;
                case ResourceKind.RarcArchive:
                    i.DisplayName = "RARC";
                    i.Module = "Archive";
                    i.Description = "Nintendo RARC archive used by several GameCube/Wii titles.";
                    break;
                case ResourceKind.J3dModel:
                    i.DisplayName = "BMD / BDL";
                    i.Module = "Models";
                    i.Description = "Nintendo J3D model resource supported by RiiStudio.";
                    break;
                case ResourceKind.JParticle:
                    i.DisplayName = "JPA";
                    i.Module = "Effects";
                    i.Description = "Nintendo JParticle collection/effect resource.";
                    break;
                case ResourceKind.Image:
                    i.DisplayName = "Image";
                    i.Module = "Textures";
                    i.NativeEdit = true;
                    i.Description = "Standard image file.";
                    break;
                case ResourceKind.Audio:
                    i.DisplayName = "Audio";
                    i.Module = "Audio";
                    i.Description = "Standard audio source file.";
                    break;
                case ResourceKind.Binary:
                    i.DisplayName = "Binary";
                    i.Module = "Binary data";
                    i.Description = "Generic binary data.";
                    break;
                default:
                    i.DisplayName = "Unknown";
                    i.Module = "Inspector";
                    i.Description = "Unknown or unsupported resource.";
                    break;
            }

            return i;
        }

        public static string OpenFilter
        {
            get
            {
                return "Nintendo Wii resources|*.szs;*.arc;*.u8;*.brlan;*.brlyt;*.tpl;*.bti;*.brres;*.mdl0;*.tex0;*.pat0;*.srt0;*.chr0;*.clr0;*.shp0;*.scn0;*.kmp;*.kcl;*.bmg;*.brstm;*.brsar;*.brseq;*.brwav;*.brbnk;*.brwsd;*.breff;*.breft;*.brfnt;*.brctr;*.rel;*.dol;*.thp;*.lex;*.rkg;*.crkg;*.rksys;*.iso;*.wbfs;*.wia;*.gcz;*.ciso;*.wdf;*.wbi;*.rarc;*.bmd;*.bdl;*.jpa;*.bin;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|" + "Archives (*.szs;*.arc;*.u8;*.brres)|*.szs;*.arc;*.u8;*.brres|" + "UI / Layout (*.brlan;*.brlyt;*.tpl;*.bti;*.brctr;*.brfnt)|*.brlan;*.brlyt;*.tpl;*.bti;*.brctr;*.brfnt|" + "Models / animations|*.brres;*.mdl0;*.tex0;*.pat0;*.srt0;*.chr0;*.clr0;*.shp0;*.scn0;*.bmd;*.bdl|" + "Course (*.kmp;*.kcl;*.lex)|*.kmp;*.kcl;*.lex|" + "Text / Audio / Effects|*.bmg;*.brstm;*.brsar;*.brseq;*.brwav;*.brbnk;*.brwsd;*.breff;*.breft;*.thp|" + "Disc / game data|*.iso;*.wbfs;*.wia;*.gcz;*.ciso;*.wdf;*.wbi;*.rkg;*.crkg;*.rksys;*.rel;*.dol|" + "All files (*.*)|*.*";
            }
        }

        private static bool IsU8Magic(byte[] b, int count)
        {
            return count >= 4 && b[0] == 0x55 && b[1] == 0xAA && b[2] == 0x38 && b[3] == 0x2D;
        }

        private static bool IsTplMagic(byte[] b, int count)
        {
            return count >= 4 && b[0] == 0x00 && b[1] == 0x20 && b[2] == 0xAF && b[3] == 0x30;
        }

        private static string SanitizeMagic(string magic)
        {
            if (String.IsNullOrEmpty(magic))
                return "";
            StringBuilder sb = new StringBuilder();
            for (int n = 0; n < magic.Length; n++)
            {
                char c = magic[n];
                sb.Append(c >= 32 && c <= 126 ? c : '.');
            }

            return sb.ToString();
        }
    }
}
