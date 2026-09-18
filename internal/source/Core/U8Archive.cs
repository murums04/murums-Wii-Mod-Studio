using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace murumsWiiModStudio
{
    public sealed class U8Archive
    {
        private const uint Magic = 0x55AA382D;
        public ArchiveEntry Root { get; private set; }
        public bool WasCompressed { get; private set; }

        private sealed class NodeInfo
        {
            public int Type;
            public int NameOffset;
            public uint DataOffset;
            public uint Size;
            public string Name;
        }

        private sealed class BuildNode
        {
            public ArchiveEntry Entry;
            public int Type;
            public int NameOffset;
            public uint DataOffset;
            public uint Size;
            public int ParentIndex;
        }

        public static U8Archive Load(byte[] source)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            bool compressed = Yaz0.IsYaz0(source);
            byte[] data = compressed ? Yaz0.Decompress(source) : source;
            if (data.Length < 0x20 || BigEndian.ReadUInt32(data, 0) != Magic)
                throw new InvalidDataException("The file is not a supported U8 archive (or Yaz0-compressed U8/SZS).");
            uint rootOffsetU = BigEndian.ReadUInt32(data, 4);
            if (rootOffsetU > int.MaxValue)
                throw new InvalidDataException("Invalid U8 root offset.");
            int rootOffset = (int)rootOffsetU;
            if (rootOffset < 0x20 || (long)rootOffset + 12 > data.Length)
                throw new InvalidDataException("Invalid U8 root node.");
            long headerEnd = (long)rootOffset + BigEndian.ReadUInt32(data, 8);
            uint dataStart = BigEndian.ReadUInt32(data, 12);
            if (headerEnd > data.Length || headerEnd > dataStart || dataStart > data.Length)
                throw new InvalidDataException("Invalid U8 header or data offset.");
            uint rootTypeName = BigEndian.ReadUInt32(data, rootOffset);
            int rootType = (int)(rootTypeName >> 24);
            if (rootType != 1)
                throw new InvalidDataException("U8 root node is not a directory.");
            uint nodeCountU = BigEndian.ReadUInt32(data, rootOffset + 8);
            if (nodeCountU == 0 || nodeCountU > 1000000)
                throw new InvalidDataException("Invalid U8 node count.");
            int nodeCount = (int)nodeCountU;
            long nodesEndLong = (long)rootOffset + (long)nodeCount * 12L;
            if (nodesEndLong >= headerEnd)
                throw new InvalidDataException("U8 node table extends beyond file bounds.");
            int stringTable = (int)nodesEndLong;
            List<NodeInfo> nodes = new List<NodeInfo>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                int off = rootOffset + i * 12;
                uint typeName = BigEndian.ReadUInt32(data, off);
                NodeInfo node = new NodeInfo();
                node.Type = (int)(typeName >> 24);
                node.NameOffset = (int)(typeName & 0x00FFFFFF);
                node.DataOffset = BigEndian.ReadUInt32(data, off + 4);
                node.Size = BigEndian.ReadUInt32(data, off + 8);
                node.Name = ReadCString(data, (long)stringTable + node.NameOffset, (int)headerEnd);
                if (i > 0)
                    ValidateEntryName(node.Name, node.Type == 1);
                if (node.Type == 0 && node.DataOffset < dataStart)
                    throw new InvalidDataException("U8 file overlaps archive metadata: " + node.Name);
                nodes.Add(node);
            }

            ArchiveEntry root = new ArchiveEntry(string.Empty, true);
            int next;
            ParseDirectory(data, nodes, 0, root, 0, out next);
            if (next != nodeCount)
                throw new InvalidDataException("U8 directory tree does not consume the full node table.");
            U8Archive archive = new U8Archive();
            archive.Root = root;
            archive.WasCompressed = compressed;
            return archive;
        }

        private static void ParseDirectory(byte[] data, List<NodeInfo> nodes, int dirIndex, ArchiveEntry directory, int depth, out int nextIndex)
        {
            if (depth > 256)
                throw new InvalidDataException("U8 directory nesting exceeds the supported limit (256).");
            NodeInfo dirNode = nodes[dirIndex];
            if (dirNode.Type != 1)
                throw new InvalidDataException("Expected a directory node.");
            int end = checked((int)dirNode.Size);
            if (end <= dirIndex || end > nodes.Count)
                throw new InvalidDataException("Invalid U8 directory end index.");
            int i = dirIndex + 1;
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (i < end)
            {
                NodeInfo node = nodes[i];
                if (!names.Add(node.Name))
                    throw new InvalidDataException("Duplicate U8 filename: " + node.Name);
                if (node.Type == 1)
                {
                    if (node.Size > end)
                        throw new InvalidDataException("U8 child directory extends beyond its parent: " + node.Name);
                    ArchiveEntry childDir = new ArchiveEntry(node.Name, true);
                    directory.AddChild(childDir);
                    int childNext;
                    ParseDirectory(data, nodes, i, childDir, depth + 1, out childNext);
                    i = childNext;
                }
                else if (node.Type == 0)
                {
                    long start = node.DataOffset;
                    long size = node.Size;
                    if (start < 0 || size < 0 || start + size > data.Length)
                        throw new InvalidDataException("File node points outside archive data: " + node.Name);
                    byte[] fileData = new byte[(int)size];
                    Buffer.BlockCopy(data, (int)start, fileData, 0, (int)size);
                    ArchiveEntry childFile = new ArchiveEntry(node.Name, false);
                    childFile.Data = fileData;
                    directory.AddChild(childFile);
                    i++;
                }
                else
                {
                    throw new InvalidDataException("Unsupported U8 node type: " + node.Type);
                }
            }

            nextIndex = end;
        }

        private static string ReadCString(byte[] data, long nameOffset, int limit)
        {
            if (nameOffset < 0 || nameOffset >= limit)
                throw new InvalidDataException("U8 filename points outside the string table.");
            int offset = (int)nameOffset;
            int end = offset;
            while (end < limit && data[end] != 0)
                end++;
            if (end == limit)
                throw new InvalidDataException("Unterminated U8 filename.");
            int len = end - offset;
            if (len == 0)
                return string.Empty;
            try
            {
                return new UTF8Encoding(false, true).GetString(data, offset, len);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("Invalid UTF-8 U8 filename.");
            }
        }

        internal static void ValidateEntryName(string name, bool isDirectory = false)
        {
            // Nintendo/Wiimm archives may contain an explicit current-directory
            // wrapper. It stays inside the extraction root; '..' never does.
            if (isDirectory && name == ".")
                return;
            if (String.IsNullOrEmpty(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" "))
                throw new InvalidDataException("Unsafe or unsupported archive filename: " + name);
            string stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'))
                throw new InvalidDataException("Reserved Windows archive filename: " + name);
        }

        public byte[] BuildU8()
        {
            if (Root == null || !Root.IsDirectory)
                throw new InvalidOperationException("Archive has no valid root directory.");
            List<BuildNode> nodes = new List<BuildNode>();
            AddDirectory(nodes, Root, 0, true, new HashSet<ArchiveEntry>(), 0);
            MemoryStream strings = new MemoryStream();
            strings.WriteByte(0); // root name at offset 0
            nodes[0].NameOffset = 0;
            for (int i = 1; i < nodes.Count; i++)
            {
                nodes[i].NameOffset = checked((int)strings.Position);
                if (nodes[i].NameOffset > 0xFFFFFF)
                    throw new InvalidDataException("U8 filename table exceeds the 24-bit offset limit.");
                byte[] name = Encoding.UTF8.GetBytes(nodes[i].Entry.Name ?? string.Empty);
                strings.Write(name, 0, name.Length);
                strings.WriteByte(0);
            }

            const int rootOffset = 0x20;
            int nodeTableSize = checked(nodes.Count * 12);
            int headerSize = checked(nodeTableSize + (int)strings.Length);
            int dataOffset = Align(rootOffset + headerSize, 0x40);
            int cursor = dataOffset;
            for (int i = 0; i < nodes.Count; i++)
            {
                BuildNode node = nodes[i];
                if (node.Type == 0)
                {
                    cursor = Align(cursor, 0x20);
                    node.DataOffset = (uint)cursor;
                    int len = node.Entry.Data == null ? 0 : node.Entry.Data.Length;
                    node.Size = (uint)len;
                    cursor = checked(cursor + len);
                }
            }

            byte[] output = new byte[cursor];
            BigEndian.WriteUInt32(output, 0, Magic);
            BigEndian.WriteUInt32(output, 4, (uint)rootOffset);
            BigEndian.WriteUInt32(output, 8, (uint)headerSize);
            BigEndian.WriteUInt32(output, 12, (uint)dataOffset);
            for (int i = 0; i < nodes.Count; i++)
            {
                BuildNode node = nodes[i];
                int off = rootOffset + i * 12;
                uint typeName = ((uint)node.Type << 24) | ((uint)node.NameOffset & 0x00FFFFFF);
                BigEndian.WriteUInt32(output, off, typeName);
                if (node.Type == 1)
                {
                    BigEndian.WriteUInt32(output, off + 4, (uint)node.ParentIndex);
                    BigEndian.WriteUInt32(output, off + 8, node.Size);
                }
                else
                {
                    BigEndian.WriteUInt32(output, off + 4, node.DataOffset);
                    BigEndian.WriteUInt32(output, off + 8, node.Size);
                }
            }

            byte[] stringBytes = strings.ToArray();
            Buffer.BlockCopy(stringBytes, 0, output, rootOffset + nodeTableSize, stringBytes.Length);
            for (int i = 0; i < nodes.Count; i++)
            {
                BuildNode node = nodes[i];
                if (node.Type == 0 && node.Entry.Data != null && node.Entry.Data.Length > 0)
                    Buffer.BlockCopy(node.Entry.Data, 0, output, (int)node.DataOffset, node.Entry.Data.Length);
            }

            return output;
        }

        private static int AddDirectory(List<BuildNode> nodes, ArchiveEntry directory, int parentIndex, bool isRoot, HashSet<ArchiveEntry> visited, int depth)
        {
            if (depth > 256 || !visited.Add(directory))
                throw new InvalidDataException("Archive contains excessive nesting or a repeated directory.");
            int index = nodes.Count;
            BuildNode dirNode = new BuildNode();
            dirNode.Entry = directory;
            dirNode.Type = 1;
            dirNode.ParentIndex = isRoot ? 0 : parentIndex;
            nodes.Add(dirNode);
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < directory.Children.Count; i++)
            {
                ArchiveEntry child = directory.Children[i];
                if (child == null || child.Parent != directory)
                    throw new InvalidDataException("Archive contains an invalid parent link.");
                ValidateEntryName(child.Name, child.IsDirectory);
                if (!names.Add(child.Name))
                    throw new InvalidDataException("Duplicate archive filename: " + child.Name);
                if (child.IsDirectory)
                {
                    AddDirectory(nodes, child, index, false, visited, depth + 1);
                }
                else
                {
                    BuildNode fileNode = new BuildNode();
                    fileNode.Entry = child;
                    fileNode.Type = 0;
                    fileNode.ParentIndex = index;
                    nodes.Add(fileNode);
                }
            }

            dirNode.Size = (uint)nodes.Count; // end index, exclusive
            return index;
        }

        private static int Align(int value, int alignment)
        {
            int mask = alignment - 1;
            return checked((value + mask) & ~mask);
        }
    }
}
