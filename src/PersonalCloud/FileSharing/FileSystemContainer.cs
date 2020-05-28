using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NSPersonalCloud.Interfaces.FileSystem;


namespace NSPersonalCloud.FileSharing
{
    public class FileSystemContainer : IFileSystem
    {
        private readonly Dictionary<string, IFileSystem> _SubFss;
        private readonly ILogger logger;

        public FileSystemContainer(Dictionary<string, IFileSystem> subfss, ILogger l) //case sensitive
        {
            logger = l;
            _SubFss = subfss;
        }
        public static string GetRootFolder(string path, out string subpath)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));

            string[] items = path.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (items?.Length > 0)
            {
                if (items.Length > 1)
                {
                    subpath = '/' + string.Join('/', items, 1, items.Length - 1);
                }
                else
                {
                    subpath = "/";
                }
                return items[0];
            }
            subpath = null;
            return null;
        }

        private IFileSystem GetSubFs(string path, out string subpath)
        {
            var cpath = GetRootFolder(path, out subpath);
            if (cpath != null)
            {
                lock (_SubFss)
                {
                    if (_SubFss.ContainsKey(cpath))
                    {
                        return _SubFss[cpath];
                    }
                }
                throw new FileNotFoundException($"Unknown folder: {path}.");
            }
            return null;
        }
        public virtual ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation)
        {
            //logger.LogTrace("CreateDirectoryAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new NotSupportedException("Creation of a virtual folder is not supported.");
            }
            else
            {
                return fs.CreateDirectoryAsync(subpath, cancellation);
            }
        }

        public virtual ValueTask DeleteAsync(string path, bool safeDelete, CancellationToken cancellation)
        {
            //logger.LogTrace("DeleteAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new NotSupportedException("Delete of a virtual folder is not supported.");
            }
            else
            {
                return fs.DeleteAsync(subpath, safeDelete, cancellation);
            }
        }

        public virtual ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, CancellationToken cancellation)
        {
            try
            {
                //logger.LogTrace("EnumerateChildrenAsync called");
                var fs = GetSubFs(path, out var subpath);
                if (fs == null)
                {
                    lock (_SubFss)
                    {
                        return new ValueTask<List<FileSystemEntry>>(_SubFss.Select(x => new FileSystemEntry {
                            Name = x.Key,
                            Attributes = FileAttributes.Normal | FileAttributes.Directory,
                            Size = 0,
                        }).ToList());
                    }
                }
                else
                {
                    return fs.EnumerateChildrenAsync(subpath, cancellation);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "EnumerateChildrenAsync Exception");
                throw;
            }
        }
        // If you want to implement both "*" and "?"
        private static string WildCardToRegular(string value)
        {
            return "^" + Regex.Escape(value).Replace("\\?", ".", StringComparison.Ordinal).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        }


        public virtual ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation)
        {
            //logger.LogTrace("EnumerateChildrenAsync with search called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                List<FileSystemEntry> ret = null;
                lock (_SubFss)
                {
                    ret = _SubFss.Where(x => Regex.IsMatch(x.Key, WildCardToRegular(searchPattern), RegexOptions.IgnoreCase))
                        .Skip(pageSize * pageIndex)
                        .Take(pageSize)
                        .Select(x => new FileSystemEntry {
                            Name = x.Key,
                            Attributes = FileAttributes.Normal | FileAttributes.Directory,
                            Size = 0,
                        }).ToList();
                }

                return new ValueTask<List<FileSystemEntry>>(ret);
            }
            else
            {
                return fs.EnumerateChildrenAsync(subpath, cancellation);
            }
        }

        public virtual async ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation)
        {
            //logger.LogTrace("GetFreeSpaceAsync called");
            var TotalFreeSpace = 0L;
            var TotalSize = 0L;
            var AvailableFreeSpace = 0L;
            foreach (var item in _SubFss)
            {
                try
                {
                    //logger.LogTrace($"GetFreeSpaceAsync {item.Key}");
                    var fs = await item.Value.GetFreeSpaceAsync().ConfigureAwait(false);
                    TotalFreeSpace += fs.FreeBytesAvailable;
                    TotalSize += fs.TotalNumberOfBytes;
                    AvailableFreeSpace += fs.TotalNumberOfFreeBytes;
                }
                catch
                {
                }
            }

            return new FreeSpaceInformation {
                FreeBytesAvailable = TotalFreeSpace,
                TotalNumberOfBytes = TotalSize,
                TotalNumberOfFreeBytes = AvailableFreeSpace
            };
        }

        public virtual ValueTask<Stream> ReadFileAsync(string path, CancellationToken cancellation)
        {
            //logger.LogTrace("ReadFileAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknown path: {path}.");
            }
            else
            {
                return fs.ReadFileAsync(subpath, cancellation);
            }
        }

        public virtual ValueTask<FileSystemEntry> ReadMetadataAsync(string path, CancellationToken cancellation)
        {

            try
            {
                //logger.LogTrace($"ReadMetadataAsync {path} called");

                var fs = GetSubFs(path, out var subpath);
                if (fs == null)
                {
                    lock (_SubFss)
                    {
                        return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                            ChildCount = _SubFss.Count,
                            Attributes = FileAttributes.Directory,
                        });
                    }
                }
                else
                {
                    return fs.ReadMetadataAsync(subpath, cancellation);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $"ReadMetadataAsync {path}");
                throw;
            }
        }

        public virtual ValueTask<Stream> ReadPartialFileAsync(string path, long fromPosition, long toPosition, CancellationToken cancellation)
        {
            //logger.LogTrace($"ReadPartialFileAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknown path: {path}.");
            }
            else
            {
                return fs.ReadPartialFileAsync(subpath, fromPosition, toPosition, cancellation);
            }
        }

        public virtual ValueTask RenameAsync(string path, string name, CancellationToken cancellation)
        {
            //logger.LogTrace($"RenameAsync called");
            var fs = GetSubFs(path, out var subpath);
            var fs2 = GetSubFs(name, out var subpath2);
            if ((fs == null) || (fs2 != fs))
            {
                throw new UnauthorizedAccessException($"Unknown path: {path}.");
            }
            else
            {
                return fs.RenameAsync(subpath, subpath2, cancellation);
            }
        }

        public virtual ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation)
        {
            //logger.LogTrace($"SetFileAttributesAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknown path: {path}.");
            }
            else
            {
                return fs.SetFileAttributesAsync(subpath, attributes, cancellation);
            }
        }

        public virtual ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation)
        {
            //logger.LogTrace($"SetFileLengthAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknown path: {path}.");
            }
            else
            {
                return fs.SetFileLengthAsync(subpath, length, cancellation);
            }
        }

        public virtual ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation)
        {
            //logger.LogTrace($"SetFileTimeAsync called");
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknown path: {path}.");
            }
            else
            {
                return fs.SetFileTimeAsync(subpath, creationTime, lastAccessTime, lastWriteTime, cancellation);
            }
        }

        public virtual ValueTask WriteFileAsync(string path, Stream data, CancellationToken cancellation)
        {
            try
            {
                //logger.LogTrace($"WriteFileAsync {path}");
                var fs = GetSubFs(path, out var subpath);
                if (fs == null)
                {
                    throw new UnauthorizedAccessException($"Unknown path: {path}.");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(subpath))
                    {
                        return fs.WriteFileAsync(subpath, data, cancellation);
                    }
                    else
                    {
                        throw new InvalidOperationException("The path does not point to a file, or the file already exists.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogTrace($"WriteFileAsync ex {ex.Message}");
                throw;
            }
        }

        public virtual ValueTask WritePartialFileAsync(string path, long offset, long dataLength, Stream data, CancellationToken cancellation)
        {
            try
            {
                //logger.LogTrace($"WritePartialFileAsync {path}");
                var fs = GetSubFs(path, out var subpath);
                if (fs == null)
                {
                    throw new UnauthorizedAccessException($"Unknown path: {path}.");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(subpath))
                    {
                        logger.LogTrace($"WritePartialFileAsync to sub {subpath}");
                        return fs.WritePartialFileAsync(subpath, offset, dataLength, data, cancellation);
                    }
                    else
                    {
                        throw new InvalidOperationException("The path does not point to a file, or the file already exists.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogTrace($"WriteFileAsync ex {ex.Message}");
                throw;
            }
        }
    }
}
