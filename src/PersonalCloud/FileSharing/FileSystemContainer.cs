using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud.FileSharing
{
    public class FileSystemContainer : IFileSystem
    {
        Dictionary<string,IFileSystem> _SubFss;

        public FileSystemContainer(Dictionary<string,IFileSystem> subfss)//case sensitive
        {
            _SubFss = subfss;
        }
        static string GetRootFolder(string path, out string subpath)
        {
            string[] items = path.Split(new char[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries);
            if (items?.Length>0)
            {
                subpath = string.Join('/', items, 1, items.Length - 1);
                return items[0];
            }
            subpath = null;
            return null;
        }
        IFileSystem GetSubFs(string path, out string subpath)
        {
            var cpath = GetRootFolder(path,out subpath);
            if (cpath!=null)
            {
                lock (_SubFss)
                {
                    if (_SubFss.ContainsKey(cpath))
                    {
                        return _SubFss[cpath];
                    }
                }
                throw new UnauthorizedAccessException($"Unknow folder: {path}.");
            }
            return null;
        }
        public virtual ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation)
        {
            var fs = GetSubFs(path,out var subpath);
            if (fs==null)
            {
                throw new NotSupportedException("Creation of a virtual folder is not supported.");
            }else
            {
                return fs.CreateDirectoryAsync(subpath, cancellation);
            }
        }

        public virtual ValueTask DeleteAsync(string path, bool safeDelete, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new NotSupportedException("Delete of a virtual folder is not supported.");
            }
            else
            {
                return fs.DeleteAsync(subpath, safeDelete,cancellation);
            }
        }

        public virtual ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, CancellationToken cancellation)
        {
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
        //todo: implement searchPattern
        public virtual ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {

                lock (_SubFss)
                {
                    return new ValueTask<List<FileSystemEntry>>(
                        _SubFss
                        .Skip(pageSize* pageIndex)
                        .Take(pageSize)
                        .Select(x => new FileSystemEntry {
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

        public virtual ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation)
        {
            return new ValueTask<FreeSpaceInformation>(new FreeSpaceInformation());
        }

        public virtual ValueTask<Stream> ReadFileAsync(string path, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.ReadFileAsync(subpath, cancellation);
            }
        }

        public virtual ValueTask<FileSystemEntry> ReadMetadataAsync(string path, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                lock (_SubFss)
                {
                    return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                        ChildCount = _SubFss.Count,
                        Attributes=FileAttributes.Directory,
                    });
                }
            }
            else
            {
                return fs.ReadMetadataAsync(subpath, cancellation);
            }
        }

        public virtual ValueTask<Stream> ReadPartialFileAsync(string path, long fromPosition, long toPosition, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.ReadPartialFileAsync(subpath, fromPosition, toPosition, cancellation);
            }
        }

        public virtual ValueTask RenameAsync(string path, string name, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            var fs2 = GetSubFs(name, out var subpath2);
            if ((fs == null)|| (fs2!= fs))
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.RenameAsync(subpath, subpath2, cancellation);
            }
        }

        public virtual ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.SetFileAttributesAsync(subpath, attributes, cancellation);
            }
        }

        public virtual ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.SetFileLengthAsync(subpath, length, cancellation);
            }
        }

        public virtual ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.SetFileTimeAsync(subpath, creationTime, lastAccessTime, lastWriteTime, cancellation);
            }
        }

        public virtual ValueTask WriteFileAsync(string path, Stream data, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.WriteFileAsync(subpath, data, cancellation);
            }
        }

        public virtual ValueTask WritePartialFileAsync(string path, long offset, long dataLength, Stream data, CancellationToken cancellation)
        {
            var fs = GetSubFs(path, out var subpath);
            if (fs == null)
            {
                throw new UnauthorizedAccessException($"Unknow path: {path}.");
            }
            else
            {
                return fs.WritePartialFileAsync(subpath, offset, dataLength, data, cancellation);
            }
        }
    }
}
