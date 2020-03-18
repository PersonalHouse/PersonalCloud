using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NSPersonalCloud.Interfaces.FileSystem
{
    /// <summary>
    /// A writable file system that supports adding and removing files and directories (folders).
    /// </summary>
    public interface IWritableFileSystem
    {
        ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation = default);

        ValueTask WriteFileAsync(string path, Stream data, CancellationToken cancellation = default);

        ValueTask WritePartialFileAsync(string path, long offset, long dataLength, Stream data, CancellationToken cancellation = default);

        ValueTask RenameAsync(string path, string name, CancellationToken cancellation = default);

        ValueTask DeleteAsync(string path, bool safeDelete = false, CancellationToken cancellation = default);

        ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation = default);

        ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation = default);

        ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation = default);
    }
}
