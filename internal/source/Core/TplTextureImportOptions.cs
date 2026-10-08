using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace murumsWiiModStudio
{
    internal sealed class TplTextureImportOptions
    {
        // Null erhält das bestehende Profil.
        public int? Format;
        public int? MaxLod;
    }

    internal static partial class TplTextureEditor
    {
        internal static int MaximumMipLevel(int width, int height)
        {
            int level = 0;
            for (int dimension = Math.Max(width, height); dimension > 1; dimension >>= 1)
                level++;
            return level;
        }

        internal static bool IsImportFormat(int format)
        {
            return format >= 0 && format <= 6 || format == 8 || format == 9 || format == 14;
        }

        internal static byte[] ReplaceImage(byte[] target, Bitmap source, bool resizeToTarget, int imageIndex, TplTextureImportOptions options)
        {
            if (options == null || (!options.Format.HasValue && !options.MaxLod.HasValue))
                return ReplaceImage(target, source, resizeToTarget, imageIndex);
            if (source == null) throw new ArgumentNullException("source");
            TplTextureInfo info = GetImageInfo(target, imageIndex);
            int format = options.Format ?? info.Format, maxLod = options.MaxLod ?? info.MaxLod;
            if (!IsImportFormat(format))
                throw new NotSupportedException(L.T("Dieses TPL-Zielformat kann nicht kodiert werden.", "This TPL target format cannot be encoded."));
            if (maxLod < 0 || maxLod > MaximumMipLevel(info.Width, info.Height))
                throw new ArgumentOutOfRangeException("MaxLod", L.T("Die Mipmapstufe passt nicht zur Bildgrösse.", "The mipmap level does not fit the image dimensions."));
            TplLayout layout = ReadImportLayout(target);
            Bitmap resized = null;
            try
            {
                if (source.Width != info.Width || source.Height != info.Height)
                {
                    if (!resizeToTarget)
                        throw new InvalidOperationException(L.T("Quellbild und Zieltextur müssen gleich gross sein.", "The source image and target texture must have the same dimensions."));
                    resized = Resize(source, info.Width, info.Height);
                }
                byte[] imageBytes, paletteBytes = null;
                int paletteKind = 2;
                if (format == 8 || format == 9)
                {
                    TplImageRecord existing = layout.Images[imageIndex];
                    if ((info.Format == 8 || info.Format == 9) && existing.PaletteHeader != null)
                        paletteKind = checked((int)ReadU32(existing.PaletteHeader.Bytes, 4));
                    EncodeIndexedImage(resized ?? source, format, paletteKind, maxLod, out imageBytes, out paletteBytes);
                }
                else
                {
                    using (var encoded = new MemoryStream())
                    {
                        for (int level = 0; level <= maxLod; level++)
                        {
                            Bitmap mip = level == 0 ? null : Resize(resized ?? source, Math.Max(1, info.Width >> level), Math.Max(1, info.Height >> level));
                            try
                            {
                                byte[] bytes = EncodeBaseLevel(mip ?? resized ?? source, format);
                                encoded.Write(bytes, 0, bytes.Length);
                            }
                            finally { if (mip != null) mip.Dispose(); }
                        }
                        imageBytes = encoded.ToArray();
                    }
                }
                return RepackImport(layout, imageIndex, format, maxLod, imageBytes, paletteBytes, paletteKind, options.MaxLod.HasValue && maxLod != info.MaxLod);
            }
            finally { if (resized != null) resized.Dispose(); }
        }

        sealed class TplBlock
        {
            internal int Offset, Kind;
            internal byte[] Bytes;
        }

        sealed class TplImageRecord
        {
            internal TplBlock Header, PaletteHeader, ImageData, PaletteData;
        }

        sealed class TplLayout
        {
            internal byte[] Header;
            internal TplImageRecord[] Images;
            internal List<TplBlock> Blocks;
        }

        static InvalidDataException UnsafeTplLayout()
        {
            return new InvalidDataException(L.T("Diese TPL enthält unbekannte oder überlappende Daten. Format-/Mipmapänderung ist nicht sicher möglich; bitte eine reguläre TPL verwenden.",
                "This TPL contains unknown or overlapping data. Format/mipmap conversion cannot be performed safely; please use a regular TPL."));
        }

        static TplBlock AddImportBlock(byte[] data, List<TplBlock> blocks, int offset, int length, int kind)
        {
            if (offset < 0 || length < 1 || (long)offset + length > data.Length) throw UnsafeTplLayout();
            foreach (TplBlock block in blocks)
            {
                if (offset == block.Offset && length == block.Bytes.Length && kind == block.Kind) return block;
                if ((long)offset < (long)block.Offset + block.Bytes.Length && (long)block.Offset < (long)offset + length)
                    throw UnsafeTplLayout();
            }
            var result = new TplBlock { Offset = offset, Kind = kind, Bytes = new byte[length] };
            Buffer.BlockCopy(data, offset, result.Bytes, 0, length);
            blocks.Add(result);
            return result;
        }

        static TplLayout ReadImportLayout(byte[] data, bool allowAncillaryData = false)
        {
            try { return ReadImportLayoutCore(data, allowAncillaryData); }
            catch (OverflowException) { throw UnsafeTplLayout(); }
            catch (EndOfStreamException) { throw UnsafeTplLayout(); }
        }

        static TplLayout ReadImportLayoutCore(byte[] data, bool allowAncillaryData)
        {
            if (!IsTpl(data)) throw UnsafeTplLayout();
            int count = checked((int)ReadU32(data, 4)), table = checked((int)ReadU32(data, 8));
            if (count < 1 || count > 4096 || table < 12) throw UnsafeTplLayout();
            var blocks = new List<TplBlock>();
            var header = AddImportBlock(data, blocks, 0, 12, 0);
            AddImportBlock(data, blocks, table, checked(count * 8), 1);
            var layout = new TplLayout { Header = header.Bytes, Images = new TplImageRecord[count], Blocks = blocks };
            for (int i = 0; i < count; i++)
            {
                var image = new TplImageRecord();
                layout.Images[i] = image;
                image.Header = AddImportBlock(data, blocks, checked((int)ReadU32(data, table + i * 8)), 36, 2);
                int height = ReadU16(image.Header.Bytes, 0), width = ReadU16(image.Header.Bytes, 2);
                int format = checked((int)ReadU32(image.Header.Bytes, 4)), lod = image.Header.Bytes[34];
                if (width < 1 || height < 1 || width > 16384 || height > 16384 || lod > MaximumMipLevel(width, height)) throw UnsafeTplLayout();
                long length = 0;
                for (int level = 0; level <= lod; level++)
                {
                    int w = Math.Max(1, width >> level), h = Math.Max(1, height >> level);
                    int size = format == 10 ? checked(Blocks(w, 4) * Blocks(h, 4) * 32) : GetBaseLevelPayloadLength(w, h, format);
                    if (size < 1) throw UnsafeTplLayout();
                    length += size;
                }
                image.ImageData = AddImportBlock(data, blocks, checked((int)ReadU32(image.Header.Bytes, 8)), checked((int)length), 3);
                int paletteOffset = checked((int)ReadU32(data, table + i * 8 + 4));
                if (paletteOffset != 0)
                {
                    image.PaletteHeader = AddImportBlock(data, blocks, paletteOffset, 12, 4);
                    int paletteCount = ReadU16(image.PaletteHeader.Bytes, 0);
                    uint paletteKind = ReadU32(image.PaletteHeader.Bytes, 4);
                    int capacity = format == 8 ? 16 : format == 9 ? 256 : 16384;
                    if (paletteCount < 1 || paletteCount > capacity || paletteKind > 2) throw UnsafeTplLayout();
                    image.PaletteData = AddImportBlock(data, blocks, checked((int)ReadU32(image.PaletteHeader.Bytes, 8)), checked(paletteCount * 2), 5);
                }
                else if (format == 8 || format == 9 || format == 10) throw UnsafeTplLayout();
            }
            blocks.Sort(delegate(TplBlock a, TplBlock b) { return a.Offset.CompareTo(b.Offset); });
            if (allowAncillaryData) return layout;
            int end = 0;
            foreach (TplBlock block in blocks)
            {
                for (int p = end; p < block.Offset; p++) if (data[p] != 0) throw UnsafeTplLayout();
                end = checked(block.Offset + block.Bytes.Length);
            }
            for (int p = end; p < data.Length; p++) if (data[p] != 0) throw UnsafeTplLayout();
            return layout;
        }

        static int WriteImportBlock(MemoryStream output, Dictionary<TplBlock, int> offsets, TplBlock block)
        {
            int offset;
            if (offsets.TryGetValue(block, out offset)) return offset;
            offset = Align32(checked((int)output.Length));
            output.SetLength(offset);
            output.Position = offset;
            output.Write(block.Bytes, 0, block.Bytes.Length);
            offsets.Add(block, offset);
            return offset;
        }

        static byte[] RepackImport(TplLayout layout, int index, int format, int maxLod, byte[] imageBytes, byte[] paletteBytes, int paletteKind, bool changeMipProfile)
        {
            TplImageRecord selected = layout.Images[index];
            var replacement = new TplImageRecord { Header = new TplBlock { Bytes = (byte[])selected.Header.Bytes.Clone() }, ImageData = new TplBlock { Bytes = imageBytes } };
            Put32(replacement.Header.Bytes, 4, format);
            replacement.Header.Bytes[34] = (byte)maxLod;
            if (changeMipProfile)
            {
                replacement.Header.Bytes[33] = (byte)Math.Min(replacement.Header.Bytes[33], maxLod);
                uint filter = ReadU32(replacement.Header.Bytes, 20);
                if (filter > 5) throw UnsafeTplLayout();
                if (maxLod == 0 && filter >= 2) filter = filter == 2 || filter == 4 ? 0u : 1u;
                else if (maxLod > 0 && filter < 2) filter = filter == 0 ? 2u : 5u;
                Put32(replacement.Header.Bytes, 20, (int)filter);
            }
            if (paletteBytes != null)
            {
                replacement.PaletteHeader = new TplBlock { Bytes = selected.PaletteHeader == null ? new byte[12] : (byte[])selected.PaletteHeader.Bytes.Clone() };
                replacement.PaletteData = new TplBlock { Bytes = paletteBytes };
                int count = paletteBytes.Length / 2;
                replacement.PaletteHeader.Bytes[0] = (byte)(count >> 8);
                replacement.PaletteHeader.Bytes[1] = (byte)count;
                Put32(replacement.PaletteHeader.Bytes, 4, paletteKind);
            }
            layout.Images[index] = replacement;
            using (var output = new MemoryStream())
            {
                output.Write(layout.Header, 0, layout.Header.Length);
                const int table = 12;
                output.SetLength(checked(table + layout.Images.Length * 8));
                var offsets = new Dictionary<TplBlock, int>();
                foreach (TplImageRecord image in layout.Images)
                {
                    WriteImportBlock(output, offsets, image.Header);
                    if (image.PaletteHeader != null) WriteImportBlock(output, offsets, image.PaletteHeader);
                }
                foreach (TplImageRecord image in layout.Images)
                {
                    if (image.PaletteData != null) WriteImportBlock(output, offsets, image.PaletteData);
                    WriteImportBlock(output, offsets, image.ImageData);
                }
                byte[] result = output.ToArray();
                Put32(result, 8, table);
                for (int i = 0; i < layout.Images.Length; i++)
                {
                    TplImageRecord image = layout.Images[i];
                    int header = offsets[image.Header];
                    Put32(result, table + i * 8, header);
                    Put32(result, table + i * 8 + 4, image.PaletteHeader == null ? 0 : offsets[image.PaletteHeader]);
                    Put32(result, header + 8, offsets[image.ImageData]);
                    if (image.PaletteHeader != null) Put32(result, offsets[image.PaletteHeader] + 8, offsets[image.PaletteData]);
                }
                return result;
            }
        }
    }
}
