using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud
{
#pragma warning disable CA1303

    public class VirtualFileSystem : ILocalFileSystem
    {
        public static IReadOnlyList<char> InvalidCharacters { get; } = new char[] { '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007', '\b', '\t', '\n', '\u000B', '\f', '\r', '\u000E', '\u000F', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F', '\u0022', '*', '/', ':', '\u003C', '\u003E', '?', '\\', '|' };

        private readonly EnumerationOptions DefaultEnumerationOptions = new EnumerationOptions {
            IgnoreInaccessible = false,
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        private string rootPath;
        public string RootPath
        {
            get => rootPath;
            set {
                rootPath = value;
                if (rootPath is null) return;
                if (!rootPath.EndsWith(Path.DirectorySeparatorChar)) rootPath += Path.DirectorySeparatorChar;
                if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
            }
        }

        public VirtualFileSystem(string path)
        {
            RootPath = path;
        }

        #region Utility

        public bool IsAbsolutePathValid(string path, out bool isDirectory, out FileSystemInfo info)
        {
            if (Directory.Exists(path) || string.IsNullOrEmpty(Path.GetFileName(path)))
            {
                var dirInfo = new DirectoryInfo(path);
                isDirectory = true;
                info = dirInfo;
                return dirInfo.FullName.StartsWith(RootPath, StringComparison.InvariantCulture);
            }
            else
            {
                var fileInfo = new FileInfo(path);
                isDirectory = false;
                info = fileInfo;
                return fileInfo.FullName.StartsWith(RootPath, StringComparison.InvariantCulture);
            }
        }

        public bool IsRelativePathValid(string relativePath, out bool isDirectory, out FileSystemInfo info)
        {
            if (RootPath is null)
            {
                isDirectory = false;
                info = null;
                return false;
            }
            var absolutePath = Path.Combine(RootPath, relativePath?.TrimStart(Path.AltDirectorySeparatorChar));
            return IsAbsolutePathValid(absolutePath, out isDirectory, out info);
        }

        #endregion Utility

        #region IReadOnlyFileSystem

        public virtual ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation = default)
        {
            var dinfo = DriveInfo.GetDrives().Single(drive => string.Equals(drive.RootDirectory.Name, Path.GetPathRoot(RootPath), StringComparison.OrdinalIgnoreCase));

            return new ValueTask<FreeSpaceInformation>(new FreeSpaceInformation {
                FreeBytesAvailable = dinfo.TotalFreeSpace,
                TotalNumberOfBytes = dinfo.TotalSize,
                TotalNumberOfFreeBytes = dinfo.AvailableFreeSpace
            });
        }

        public virtual ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string relativePath, CancellationToken cancellation = default)
        {
            return EnumerateChildrenAsync(relativePath, "*", -1, -1, cancellation);
        }

        public virtual async ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string relativePath, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation = default)
        {
            if (RootPath is null) return new List<FileSystemEntry>(0);

            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (!string.IsNullOrEmpty(Path.GetFileName(relativePath))) relativePath += Path.AltDirectorySeparatorChar;

            if (IsSpecialPathValid(relativePath))
            {
                var virtualChildren = new List<FileSystemEntry>();
                await EnumerateChildrenExtension(relativePath, virtualChildren, cancellation).ConfigureAwait(false);
                return virtualChildren;
            }

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot enumerate a folder that is not shared.");
            }

            if (!isDirectory || !info.Exists)
            {
                throw new DirectoryNotFoundException("Cannot enumerate a folder that does not exist.");
            }

            var dirInfo = (DirectoryInfo) info;
            var children = new List<FileSystemEntry>();
            var items = dirInfo.EnumerateFileSystemInfos(searchPattern, DefaultEnumerationOptions);
            if (pageSize > 0 && pageIndex >= 0)
            {
                items = items.Skip(pageSize * pageIndex).Take(pageSize);
            }
            foreach (var entry in items) children.Add(new FileSystemEntry(entry));

            await EnumerateChildrenExtension(relativePath, children, cancellation).ConfigureAwait(false);
            return children;
        }

        public virtual ValueTask<FileSystemEntry> ReadMetadataAsync(string relativePath, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) return ReadMetadataExtension(relativePath, cancellation);

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a resource that is not shared.");
            }

            if (isDirectory)
            {
                if (!info.Exists) throw new DirectoryNotFoundException("The folder does not exist.");
                return new ValueTask<FileSystemEntry>(new FileSystemEntry((DirectoryInfo) info));
            }
            else
            {
                if (!info.Exists) throw new FileNotFoundException("The file does not exist.");
                return new ValueTask<FileSystemEntry>(new FileSystemEntry((FileInfo) info));
            }
        }

        public virtual ValueTask<Stream> ReadFileAsync(string relativePath, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) return ReadFileExtension(relativePath, cancellation);

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot read a file that is not shared.");
            }

            if (isDirectory) throw new InvalidOperationException("The path does not point to a file.");
            if (!info.Exists) throw new FileNotFoundException("The file does not exist.");

            return new ValueTask<Stream>(((FileInfo) info).Open(FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public virtual ValueTask<Stream> ReadPartialFileAsync(string relativePath, long fromPosition, long toPosition, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) return ReadPartialFileExtension(relativePath, fromPosition, toPosition, cancellation);

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot read a file that is not shared.");
            }

            if (isDirectory) throw new InvalidOperationException("The path does not point to a file.");
            if (!info.Exists) throw new FileNotFoundException("The file does not exist.");

            var fileInfo = (FileInfo) info;
            toPosition = Math.Min(fileInfo.Length - 1, toPosition);
            if (toPosition < fromPosition || fromPosition < 0 || toPosition < 0)
            {
                throw new InvalidOperationException("Read range for this file is unsatisfiable.");
            }
            var strm = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            strm.Seek(fromPosition, SeekOrigin.Begin);
            return new ValueTask<Stream>(strm);
        }

        #endregion IReadOnlyFileSystem

        #region IWritableFileSystem

        public virtual async ValueTask WriteFileAsync(string relativePath, Stream fileContent, CancellationToken cancellationToken = default)
        {
            if (fileContent is null) throw new ArgumentNullException(nameof(fileContent));

            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) throw new NotSupportedException("Write to a virtual file is not supported.");

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a folder that is not shared.");
            }


            if (isDirectory )
            {
                throw new InvalidOperationException("The path does not point to a file.");
            }

            var fileInfo = (FileInfo) info;
            Directory.CreateDirectory(fileInfo.DirectoryName);

            if (info.Exists)
            {
                try
                {
                    if (fileContent.Length == fileContent.Position)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                }
                using var target = fileInfo.OpenWrite();
                await fileContent.CopyToAsync(target).ConfigureAwait(false);
                //throw new InvalidOperationException("The file already exists.");
            }
            else
            {
                using var target = fileInfo.Create();
                await fileContent.CopyToAsync(target).ConfigureAwait(false);
            }

        }

        public virtual async ValueTask WritePartialFileAsync(string relativePath, long offset, long length, Stream partialContent, CancellationToken cancellation = default)
        {
            if (partialContent is null) throw new ArgumentNullException(nameof(partialContent));

            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) throw new NotSupportedException("Write to a virtual file is not supported.");

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot write a file that is not shared.");
            }

            if (isDirectory) throw new InvalidOperationException("The path does not point to a file.");
            if (!info.Exists) throw new FileNotFoundException("The file does not exist.");

            if (offset < 0 || length <= 0) throw new InvalidOperationException("Write range for this file is unsatisfiable.");

            using var file = new FileStream(info.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            file.Seek(offset, SeekOrigin.Begin);
            await partialContent.CopyToAsync(file).ConfigureAwait(false);
        }

        public virtual ValueTask CreateDirectoryAsync(string relativePath, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (!string.IsNullOrEmpty(Path.GetFileName(relativePath))) relativePath += Path.AltDirectorySeparatorChar;

            if (IsSpecialPathValid(relativePath)) throw new NotSupportedException("Creation of a virtual folder is not supported.");

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a folder that is not shared.");
            }

            if (!isDirectory || info.Exists)
            {
                throw new InvalidOperationException("The path does not point to a folder, or the folder already exists.");
            }

           ((DirectoryInfo) info).Create();
            return default;
        }

        public virtual ValueTask RenameAsync(string relativePath, string name, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) throw new NotSupportedException("Renaming a virtual folder is not supported.");

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a resource that is not shared.");
            }

            if (info.Attributes != (FileAttributes) (-1) && info.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                throw new InvalidOperationException("Cannot rename read-only resource.");
            }

            if (isDirectory)
            {
                var oldInfo = (DirectoryInfo) info;
                if (!oldInfo.Exists) throw new DirectoryNotFoundException("The original folder no longer exists.");

                if (oldInfo.FullName == RootPath) throw new InvalidOperationException("Cannot rename shared container.");

                var newPath = Path.Combine(Path.IsPathRooted(name) ? RootPath : oldInfo.Parent.FullName, name?.TrimStart(Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrEmpty(Path.GetFileName(newPath))) newPath += Path.AltDirectorySeparatorChar;
                if (!IsAbsolutePathValid(newPath, out var newPathIsDirectory, out var newInfo) || !newPathIsDirectory || newInfo.Exists)
                {
                    throw new InvalidOperationException("The new folder name is invalid.");
                }

                oldInfo.MoveTo(newInfo.FullName);
            }
            else
            {
                var oldInfo = (FileInfo) info;
                if (!oldInfo.Exists) throw new FileNotFoundException("The original file no longer exists.");

                var newPath = Path.Combine(Path.IsPathRooted(name) ? RootPath : oldInfo.Directory.FullName, name?.TrimStart(Path.AltDirectorySeparatorChar));
                if (!IsAbsolutePathValid(newPath, out var newPathIsDirectory, out var newInfo) || newPathIsDirectory || newInfo.Exists)
                {
                    throw new InvalidOperationException("The new file name is invalid.");
                }

                oldInfo.MoveTo(newInfo.FullName);
            }

            return default;
        }

        public virtual ValueTask DeleteAsync(string relativePath, bool safeDelete = false, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (IsSpecialPathValid(relativePath)) throw new NotSupportedException("Deleting a virtual folder is not supported.");

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a resource that is not shared.");
            }

            if (info.Attributes != (FileAttributes) (-1) && info.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                throw new InvalidOperationException("Cannot delete read-only resource.");
            }

            if (isDirectory)
            {
                var dirInfo = (DirectoryInfo) info;
                if (!dirInfo.Exists) throw new DirectoryNotFoundException("The original folder no longer exists.");

                if (dirInfo.FullName == RootPath) throw new InvalidOperationException("Cannot delete shared container.");

                dirInfo.Delete(true);
            }
            else
            {
                var fileInfo = (FileInfo) info;
                if (!safeDelete && !fileInfo.Exists) throw new FileNotFoundException("The original file no longer exists.");

                fileInfo.Delete();
            }

            return default;
        }

        public virtual async ValueTask SetFileLengthAsync(string relativePath, long length, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a resource that is not shared.");
            }

            if (info.Attributes != (FileAttributes) (-1) && info.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                throw new InvalidOperationException("Cannot set length on read-only resource.");
            }

            if (isDirectory) throw new InvalidOperationException("Cannot set length on a folder.");

            var fileInfo = (FileInfo) info;
            if (!fileInfo.Exists) throw new FileNotFoundException("The original file no longer exists.");

            using var stream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await stream.FlushAsync().ConfigureAwait(false);
            stream.SetLength(length);
        }

        public virtual ValueTask SetFileAttributesAsync(string relativePath, FileAttributes attributes, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a resource that is not shared.");
            }

            if (isDirectory)
            {
                var dirInfo = (DirectoryInfo) info;
                if (!dirInfo.Exists) throw new DirectoryNotFoundException("The original folder no longer exists.");

                dirInfo.Attributes = attributes;
            }
            else
            {
                var fileInfo = (FileInfo) info;
                if (!fileInfo.Exists) throw new FileNotFoundException("The original file no longer exists.");

                fileInfo.Attributes = attributes;
            }

            return default;
        }

        public virtual ValueTask SetFileTimeAsync(string relativePath, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = string.Empty;

            if (!IsRelativePathValid(relativePath, out var isDirectory, out var info))
            {
                throw new UnauthorizedAccessException("Cannot access a resource that is not shared.");
            }

            if (isDirectory)
            {
                var dirInfo = (DirectoryInfo) info;
                if (!dirInfo.Exists) throw new FileNotFoundException("The original folder no longer exists.");

                if (creationTime.HasValue) dirInfo.CreationTimeUtc = creationTime.Value;
                if (lastAccessTime.HasValue) dirInfo.LastAccessTimeUtc = lastAccessTime.Value;
                if (lastWriteTime.HasValue) dirInfo.LastWriteTimeUtc = lastWriteTime.Value;
            }
            else
            {
                var fileInfo = (FileInfo) info;
                if (!fileInfo.Exists) throw new FileNotFoundException("The original file no longer exists.");

                if (creationTime.HasValue) fileInfo.CreationTimeUtc = creationTime.Value;
                if (lastAccessTime.HasValue) fileInfo.LastAccessTimeUtc = lastAccessTime.Value;
                if (lastWriteTime.HasValue) fileInfo.LastWriteTimeUtc = lastWriteTime.Value;
            }

            return default;
        }

        #endregion IWritableFileSystem

        #region Extensions

        protected virtual bool IsSpecialPathValid(string path) => false;

        protected virtual ValueTask EnumerateChildrenExtension(string relativePath, List<FileSystemEntry> children, CancellationToken cancellation) => default;

        protected virtual ValueTask<FileSystemEntry> ReadMetadataExtension(string relativePath, CancellationToken cancellation) 
            => throw new NotSupportedException("This operation is currently not supported on a virtual file.");

        protected virtual ValueTask<Stream> ReadFileExtension(string relativePath, CancellationToken cancellation) 
            => throw new NotSupportedException("This operation is currently not supported on a virtual file.");

        protected virtual ValueTask<Stream> ReadPartialFileExtension(string relativePath, long fromPosition, long toPosition, CancellationToken cancellation)
            => throw new NotSupportedException("This operation is currently not supported on a virtual file.");

        #endregion Extensions
    }

#pragma warning restore CA1303
}
