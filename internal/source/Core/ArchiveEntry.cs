using System;
using System.Collections.Generic;

namespace murumsWiiModStudio
{
    public sealed class ArchiveEntry
    {
        public string Name { get; set; }
        public bool IsDirectory { get; private set; }
        public byte[] Data { get; set; }
        public List<ArchiveEntry> Children { get; private set; }
        public ArchiveEntry Parent { get; set; }

        public ArchiveEntry(string name, bool isDirectory)
        {
            Name = name ?? string.Empty;
            IsDirectory = isDirectory;
            Data = new byte[0];
            Children = new List<ArchiveEntry>();
        }

        public ArchiveEntry FindChild(string name)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (string.Equals(Children[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return Children[i];
            }

            return null;
        }

        public void AddChild(ArchiveEntry child)
        {
            if (!IsDirectory)
                throw new InvalidOperationException("Files cannot contain child entries.");
            child.Parent = this;
            Children.Add(child);
        }
    }
}
