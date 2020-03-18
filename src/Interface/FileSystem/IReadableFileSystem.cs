using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NSPersonalCloud.Interfaces.FileSystem
{
    /// <summary>
    /// A read-only (for this interface) file system that supports reading files and directories (folders).
    /// </summary>
    public interface IReadableFileSystem
    {
        ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation = default);

        ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, CancellationToken cancellation = default);

        ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation = default);

        ValueTask<FileSystemEntry> ReadMetadataAsync(string path, CancellationToken cancellation = default);

        ValueTask<Stream> ReadFileAsync(string path, CancellationToken cancellation = default);

        ValueTask<Stream> ReadPartialFileAsync(string path, long fromPosition, long toPosition, CancellationToken cancellation = default);
    }
}
