using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSPersonalCloud.Interfaces.Apps;
using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud.FileSharing
{
    public class AppInFs : IFileSystem
    {
        public Func<List<AppLauncher>> GetApps { get; set; }
        public Func<AppLauncher, string> GetUrl { get; set; }


        static string[] GetFolderSegments(string path)
        {
            return  path.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("Creation of a virtual folder is not supported.");
        }

        public ValueTask DeleteAsync(string path, bool safeDelete = false, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("Deleting is not supported.");
        }

        public ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException("Enum directory is not supported.");
            }
            var ps = GetFolderSegments(path);
            if (ps?.Length>0)
            {
                throw new NotSupportedException("Enum directory is not supported.");
            }
            var apps = GetApps();
            return new ValueTask<List<FileSystemEntry>>(
                apps.Select(x => new FileSystemEntry() {
                    Name=$"{x.Name}.htm",
                }).ToList());
        }

        public ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException("Enum directory is not supported.");
            }
            var ps = GetFolderSegments(path);
            if (ps?.Length > 0)
            {
                throw new NotSupportedException("Enum directory is not supported.");
            }
            var apps = GetApps();
            return new ValueTask<List<FileSystemEntry>>(
                apps.Select(x => new FileSystemEntry() {
                    Name = $"{x.Name}.htm",
                }).Skip(pageSize* pageIndex).Take(pageSize).ToList());
        }

        public ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation = default)
        {
            return new ValueTask<FreeSpaceInformation>(new FreeSpaceInformation());
        }

        Stream GetContent(AppLauncher app, int start=-1, int count=-1)
        {
            var url = GetUrl?.Invoke(app);
            string s = null;
            if (url==null)
            {
                s= "<!DOCTYPE html><html lang=\"en\"><body>There is a problem. Please update Personal Cloud to the most recently version. </body></html> ";
            }else
            {
                s = "<!DOCTYPE html><html lang=\"en\"><body><noscript>You need to enable JavaScript to run this app.</noscript><script>" +
                    $"location.href = \'{url}\' </script></body></html>";
            }
            if (start == -1)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(s));
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(s), start, count);
        }

        public ValueTask<FileSystemEntry> ReadMetadataAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException($"ReadMetadataAsync on path {path} is not supported.");
            }
            var ps = GetFolderSegments(path);
            if (ps?.Length != 1)
            {
                throw new NotSupportedException($"ReadMetadataAsync on path {path} is not supported.");
            }
            var appname = Path.GetFileNameWithoutExtension(ps[0]);
            var apps = GetApps();
            var al = apps.FirstOrDefault(x => string.Compare(x.Name, appname, true, CultureInfo.InvariantCulture) == 0);
            if (al==null)
            {
                throw new NotSupportedException($"ReadMetadataAsync couldn't find path {path}.");
            }
            var ac = GetContent(al);

            return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                Name = $"{al.Name}.htm",
                CreationDate = DateTime.Now,
                ModificationDate = DateTime.Now,
                Size = ac.Length
            });
        }


        public ValueTask<Stream> ReadFileAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException($"ReadMetadataAsync on path {path} is not supported.");
            }
            var ps = GetFolderSegments(path);
            if (ps?.Length != 1)
            {
                throw new NotSupportedException($"ReadMetadataAsync on path {path} is not supported.");
            }
            var appname = Path.GetFileNameWithoutExtension(ps[0]);
            var apps = GetApps();
            var al = apps.FirstOrDefault(x => string.Compare(x.Name, appname, true, CultureInfo.InvariantCulture) == 0);
            if (al == null)
            {
                throw new NotSupportedException($"ReadMetadataAsync couldn't find path {path}.");
            }
            var ac = GetContent(al);
            return new ValueTask<Stream>(ac);
        }

        public ValueTask<Stream> ReadPartialFileAsync(string path, long fromPosition, long toPosition, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException($"ReadMetadataAsync on path {path} is not supported.");
            }
            var ps = GetFolderSegments(path);
            if (ps?.Length != 1)
            {
                throw new NotSupportedException($"ReadMetadataAsync on path {path} is not supported.");
            }
            var appname = Path.GetFileNameWithoutExtension(ps[0]);
            var apps = GetApps();
            var al = apps.FirstOrDefault(x => string.Compare(x.Name, appname, true, CultureInfo.InvariantCulture) == 0);
            if (al == null)
            {
                throw new NotSupportedException($"ReadMetadataAsync couldn't find path {path}.");
            }
            var ac = GetContent(al,(int) fromPosition, (int) (toPosition - fromPosition+1));
            return new ValueTask<Stream>(ac);

        }

        public ValueTask RenameAsync(string path, string name, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("Rename is not supported.");
        }

        public ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("SetFileAttributesAsync is not supported.");
        }

        public ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("SetFileLengthAsync is not supported.");
        }

        public ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("SetFileTimeAsync is not supported.");
        }

        public ValueTask WriteFileAsync(string path, Stream data, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("WriteFileAsync is not supported.");
        }

        public ValueTask WritePartialFileAsync(string path, long offset, long dataLength, Stream data, CancellationToken cancellation = default)
        {
            throw new NotSupportedException("WritePartialFileAsync is not supported.");
        }
    }
}
