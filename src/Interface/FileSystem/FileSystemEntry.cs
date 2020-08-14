using System;
using System.IO;

namespace NSPersonalCloud.Interfaces.FileSystem
{
    /// <summary>
    /// A virtual file system entry.
    /// </summary>
    public class FileSystemEntry
    {
        /// <summary>
        /// The name of this resource, including its extension.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// (Optional) This field may be provided to better identify file content.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// (Optional) The size of this resource in bytes.
        /// For files, this is the file length; for directories, this is the size of all children.
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// (Optional) If the resource is a directory or otherwise enumerable container, this is the count of elements contained.
        /// </summary>
        public int? ChildCount { get; set; }

        /// <summary>
        /// The creation date (must be of <see cref="DateTimeKind.Utc"/>) of the resource.
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// (Optional) The last modification date (must be of <see cref="DateTimeKind.Utc"/>) of the resource.
        /// </summary>
        public DateTime? ModificationDate { get; set; }

        /// <summary>
        /// File attributes (hidden, etc.)
        /// </summary>
        public FileAttributes Attributes { get; set; }

        #region Utility

        /// <summary>
        /// On most operating systems, unlike Windows, a read-only resource cannot be renamed or deleted.
        /// </summary>
        public bool IsReadOnly => Attributes != (FileAttributes) (-1) && Attributes.HasFlag(FileAttributes.ReadOnly);

        public bool IsHidden => Attributes != (FileAttributes) (-1) && Attributes.HasFlag(FileAttributes.Hidden);

        public bool IsDirectory => Attributes != (FileAttributes) (-1) && Attributes.HasFlag(FileAttributes.Directory);

        public override int GetHashCode() => HashCode.Combine(Name.GetHashCode(StringComparison.Ordinal), Size.GetHashCode(), CreationDate.GetHashCode());

        #endregion Utility

        #region Interop

        public FileSystemEntry() { }

        public FileSystemEntry(FileInfo file)
        {
            if (file is null) throw new ArgumentNullException(nameof(file));

            file.Refresh();
            Name = file.Name;
            Size = file.Length;
            CreationDate = file.CreationTimeUtc;
            ModificationDate = file.LastWriteTimeUtc;
            Attributes = file.Attributes;
        }

        public FileSystemEntry(DirectoryInfo dir)
        {
            if (dir is null) throw new ArgumentNullException(nameof(dir));

            dir.Refresh();
            Name = dir.Name;
            CreationDate = dir.CreationTimeUtc;
            ModificationDate = dir.LastWriteTimeUtc;
            Attributes = dir.Attributes;
        }

        public FileSystemEntry(FileSystemInfo entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            entry.Refresh();
            Name = entry.Name;
            Size = (entry as FileInfo)?.Length;
            CreationDate = entry.CreationTimeUtc;
            ModificationDate = entry.LastWriteTimeUtc;
            Attributes = entry.Attributes;
        }

        public FileSystemEntry(Zio.FileSystemEntry file)
        {
            if (file is null) throw new ArgumentNullException(nameof(file));

            Name = file.Name;
            CreationDate = file.CreationTime.ToUniversalTime();
            ModificationDate = file.LastWriteTime.ToUniversalTime();
            Attributes = file.Attributes;

            if (file.Attributes.HasFlag(FileAttributes.Directory))
            {
            }
            else
            {
                Size = file.FileSystem.GetFileLength(file.FullName);
            }
        }

        /// <summary>
        /// Convert this <see cref="FileSystemEntry"/> reference to a <see cref="DirectoryInfo"/>.
        /// Create this directory if non-exist; set appropriate metadata along the way.
        /// </summary>
        public DirectoryInfo ToDirectory(string parent)
        {
            var dir = new DirectoryInfo(Path.Combine(parent, Name));
            dir.Create();
            dir.CreationTimeUtc = CreationDate;
            if (ModificationDate.HasValue) dir.LastWriteTimeUtc = ModificationDate.Value;
            dir.Attributes = Attributes;
            return dir;
        }

        /// <summary>
        /// Convert this <see cref="FileSystemEntry"/> reference to a <see cref="FileInfo"/>.
        /// Create file if non-exist; set appropriate metadata along the way.
        /// </summary>
        public FileInfo ToFile(string parent)
        {
            var file = new FileInfo(Path.Combine(parent, Name));
            file.Create().Dispose();
            file.CreationTimeUtc = CreationDate;
            if (ModificationDate.HasValue) file.LastWriteTimeUtc = ModificationDate.Value;
            file.Attributes = Attributes;
            return file;
        }

        #endregion Interop
    }
}
