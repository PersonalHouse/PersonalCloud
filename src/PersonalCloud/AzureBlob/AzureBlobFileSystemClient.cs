using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.StorageClient.Azure;

namespace NSPersonalCloud.FileSharing.Aliyun
{
    public class AzureBlobFileSystemClient : IFileSystem
    {
        private readonly IAzureBlobClientBuilder _ClientBuilder;
        private readonly string _ContainerName;

        public Guid RuntimeId { get; }

        public AzureBlobFileSystemClient(Guid runtimeId, AzureBlobConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            RuntimeId = runtimeId;
            _ClientBuilder = new AzureBlobClientBuilder(config.ConnectionString);
            _ContainerName = config.BlobName;
        }

        #region IReadableFileSystem

        public async ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string filePath, CancellationToken cancellation = default)
        {
            filePath = filePath?.Replace('\\', '/').Trim('/') ?? "";
            if (filePath.Length > 0)
            {
                filePath += "/";
            }
            var client = _ClientBuilder.GetBlobContainerClient(_ContainerName);
            var pages = client.GetBlobsByHierarchyAsync(prefix: filePath, delimiter: "/", cancellationToken: cancellation).AsPages().ConfigureAwait(false);
            var files = new List<FileSystemEntry>();
            await foreach (var page in pages)
            {
                foreach (var blob in page.Values)
                {
                    if (!blob.IsBlob)
                    {
                        files.Add(new FileSystemEntry {
                            Attributes = FileAttributes.Normal | FileAttributes.Directory,
                            Size = 0,
                            Name = blob.Prefix.Substring(filePath.Length).TrimEnd('/')
                        });
                    }
                    else
                    {
                        if (blob.Blob.Name.EndsWith('/'))
                        {
                            continue;
                        }

                        files.Add(new FileSystemEntry {
                            Attributes = FileAttributes.Normal,
                            ModificationDate = blob.Blob.Properties.LastModified?.DateTime,
                            Size = blob.Blob.Properties.ContentLength ?? 0,
                            Name = blob.Blob.Name.Substring(filePath.Length)
                        });
                    }
                }
            }
            return files;
        }

        public ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<FileSystemEntry> ReadMetadataAsync(string filePath, CancellationToken cancellation = default)
        {
            BlobProperties fileMetadata = null;

            filePath = filePath?.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(filePath))
            {
                // Root Folder
                return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                    Attributes = FileAttributes.Normal | FileAttributes.Directory,
                    Size = 0,
                    Name = string.Empty
                });
            }
            var client = _ClientBuilder.GetBlobContainerClient(_ContainerName);

            if (filePath.EndsWith('/'))
            {
                if (_IsDirectory(filePath, client))
                {
                    return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                        Attributes = FileAttributes.Normal | FileAttributes.Directory,
                        Size = 0,
                        Name = GetRelativeName(filePath)
                    });
                }
            }
            else
            {
                try
                {
                    fileMetadata = client.GetBlobClient(filePath).GetProperties(cancellationToken: cancellation);
                    return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                        Attributes = FileAttributes.Normal,
                        ModificationDate = fileMetadata.LastModified.DateTime,
                        Size = fileMetadata.ContentLength,
                        Name = GetRelativeName(filePath)
                    });
                }
                catch
                {
                    // No such file
                }

                if (fileMetadata == null)
                {
                    if (_IsDirectory(filePath, client))
                    {
                        return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                            Attributes = FileAttributes.Normal | FileAttributes.Directory,
                            Size = 0,
                            Name = GetRelativeName(filePath)
                        });
                    }
                }
            }

            return default;
        }

        private static bool _IsDirectory(string filePath, BlobContainerClient client)
        {
            if (!filePath.EndsWith('/')) filePath += "/";

            var pages = client.GetBlobsByHierarchy(prefix: filePath, delimiter: "/").AsPages(pageSizeHint: 1);

            return pages.FirstOrDefault()?.Values?.Count > 0;
        }

        private static string GetRelativeName(string path)
        {
            path = path?.Replace('\\', '/').Trim('/');
            if (!string.IsNullOrEmpty(path))
            {
                int nPos = path.LastIndexOf('/');
                if (nPos >= 0)
                {
                    return path.Substring(nPos + 1);
                }
                else
                {
                    return path;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public ValueTask<Stream> ReadFileAsync(string fileName, CancellationToken cancellation = default)
        {
            fileName = fileName?.Replace('\\', '/').Trim('/');
            var client = _ClientBuilder.GetBlobContainerClient(_ContainerName);
            return new ValueTask<Stream>(client.GetBlobClient(fileName).Download(cancellationToken: cancellation).Value.Content);
        }

        public ValueTask<Stream> ReadPartialFileAsync(string path, long fromPosition, long includeToPosition, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        #endregion IReadableFileSystem

        #region IWritableFileSystem

        public ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var client = _ClientBuilder.GetBlobContainerClient(_ContainerName);
            using (var fs = new MemoryStream())
            {
                client.UploadBlob(path.Trim('/') + "/", fs, cancellationToken: cancellation);
            }
            return default;
        }

        public async ValueTask WriteFileAsync(string path, Stream data, CancellationToken cancellation = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            path = path.TrimStart('/');
            await Task.Run(() => BlobUploadUtility.Upload(_ClientBuilder, _ContainerName, path, data, cancellationToken: cancellation)).ConfigureAwait(false);
        }

        public ValueTask WritePartialFileAsync(string path, long offset, long dataLength, Stream data, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask RenameAsync(string path, string name, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DeleteAsync(string path, bool safeDelete = false, CancellationToken cancellation = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            path = path.Trim('/');
            if (path.Length == 0)
            {
                return default;
            }

            var client = _ClientBuilder.GetBlobContainerClient(_ContainerName);
            try
            {
                var blobClient = client.GetBlobClient(path);
                blobClient.Delete(cancellationToken: cancellation);
                return default;
            }
            catch
            {
                // Not a file
            }

            var pages = client.GetBlobsByHierarchy(prefix: path + "/", delimiter: "/",cancellationToken: cancellation).AsPages(pageSizeHint: 3);
            var firstPage = pages.FirstOrDefault();

            if (firstPage != null)
            {
                bool deleteFolder = false;
                foreach (var item in firstPage.Values)
                {
                    if (!item.IsBlob || !item.Blob.Name.EndsWith('/'))
                    {
                        throw new IOException("The directory is not empty.");
                    }
                    if (item.IsBlob && item.Blob.Name.EndsWith('/'))
                    {
                        deleteFolder = true;
                    }
                }
                if (deleteFolder)
                {
                    client.GetBlobClient(path + "/").Delete(cancellationToken: cancellation);
                }
            }

            return default;
        }

        public ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        #endregion IWritableFileSystem
    }
}
