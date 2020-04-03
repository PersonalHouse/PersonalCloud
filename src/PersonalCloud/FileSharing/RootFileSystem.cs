using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud.RootFS
{
    public class RootFileSystem : IFileSystem, IDisposable
    {
        public ConcurrentDictionary<string, IFileSystem> ClientList { get; private set; }

        readonly IPCService pCService;
        public RootFileSystem(IPCService pcsrv)
        {
            pCService = pcsrv;
            ClientList = new ConcurrentDictionary<string, IFileSystem>();
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ClientList != null)
                {
                    foreach (var item in ClientList)
                    {
                        if (item.Value is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    ClientList.Clear();
                    ClientList = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable

        private string[] SplitPath(string path) => path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        #region IReadableFileSystem

        public ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation = default)
        {
            throw new InvalidOperationException();
        }

        public ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.EnumerateChildrenAsync(deviceRelativePath, cancellation);
                }
                else throw new DeviceNotFoundException();
            }
            pCService?.CleanExpiredNodes();
            return new ValueTask<List<FileSystemEntry>>(ClientList.Select(x => new FileSystemEntry {
                Name = x.Key,
                Attributes = FileAttributes.Directory | FileAttributes.Device
            }).ToList());
        }

        public ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<FileSystemEntry> ReadMetadataAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            if (path == "/" || path == "/*")
            {
                return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                    Name = "Personal Cloud",
                    Attributes = FileAttributes.Directory
                });
            }

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.ReadMetadataAsync(deviceRelativePath, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask<Stream> ReadFileAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.ReadFileAsync(deviceRelativePath, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask<Stream> ReadPartialFileAsync(string path, long from, long to, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.ReadPartialFileAsync(deviceRelativePath, from, to, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        #endregion IReadableFileSystem

        #region IWritableFileSystem

        public ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.CreateDirectoryAsync(deviceRelativePath, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask WriteFileAsync(string path, Stream data, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (data is null) throw new ArgumentNullException(nameof(data));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.WriteFileAsync(deviceRelativePath, data, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask RenameAsync(string path, string name, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            if (ClientList == null) throw new NoDeviceResponseException();

            var sourceSegments = SplitPath(path);

            string targetDevice = null;
            if (Path.IsPathRooted(name))
            {
                var targetSegments = SplitPath(name);
                if (targetSegments.Length > 0) targetDevice = targetSegments[0];
                name = Path.AltDirectorySeparatorChar + string.Join(Path.AltDirectorySeparatorChar, targetSegments.TakeLast(targetSegments.Length - 1));
            }

            if (sourceSegments.Length > 0)
            {
                if (!string.IsNullOrEmpty(targetDevice) && sourceSegments[0] != targetDevice) throw new InvalidOperationException();

                if (ClientList.TryGetValue(sourceSegments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, sourceSegments.TakeLast(sourceSegments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.RenameAsync(deviceRelativePath, name, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask DeleteAsync(string path, bool safeDelete = false, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.DeleteAsync(deviceRelativePath, safeDelete, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask WritePartialFileAsync(string path, long position, long length, Stream data, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (data is null) throw new ArgumentNullException(nameof(data));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.WritePartialFileAsync(deviceRelativePath, position, length, data, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.SetFileLengthAsync(deviceRelativePath, length, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.SetFileAttributesAsync(deviceRelativePath, attributes, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        public ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (ClientList == null) throw new NoDeviceResponseException();

            var segments = SplitPath(path);
            if (segments.Length > 0)
            {
                if (ClientList.TryGetValue(segments[0], out var device))
                {
                    var deviceRelativePath = string.Join(Path.AltDirectorySeparatorChar, segments.TakeLast(segments.Length - 1));
                    deviceRelativePath = Path.AltDirectorySeparatorChar + deviceRelativePath;
                    return device.SetFileTimeAsync(deviceRelativePath, creationTime, lastAccessTime, lastWriteTime, cancellation);
                }
                else throw new DeviceNotFoundException();
            }

            throw new InvalidOperationException();
        }

        #endregion IWritableFileSystem
    }
}
