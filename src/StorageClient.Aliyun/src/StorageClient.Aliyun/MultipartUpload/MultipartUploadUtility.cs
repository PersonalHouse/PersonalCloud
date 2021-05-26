using Aliyun.OSS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using static NSPersonalCloud.StorageClient.Aliyun.SimpleRetryManager;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public static class MultipartUploadUtility
    {
        internal const int DEFAULT_PART_SIZE = 256 * 1024;

#if DEBUG
        public static bool OutputDebugInfo { get; set; } = false;
#endif

        public static CompleteMultipartUploadResult UploadMultipart
            (
            IOssClientBuilder clientBuilder,
            string bucketName,
            string objectName,
            Stream streamToUpload,
            Action<MultipartUploadOptions> options = null,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            CompleteMultipartUploadResult retVal = null;

            if (clientBuilder == null) throw new ArgumentNullException(nameof(clientBuilder));
            if (streamToUpload == null) throw new ArgumentNullException(nameof(streamToUpload));

            MultipartUploadOptions uploadOptions = new MultipartUploadOptions();
            options?.Invoke(uploadOptions);
            int partSize = Math.Max(DEFAULT_PART_SIZE, uploadOptions.PartSize);
            var streamMode = uploadOptions.StreamMode;

            long fileSize = streamToUpload.Length;
            long progressUpdateInterval = (fileSize / 100 + 4096) / 4096 * 4096;

            if (streamMode == MultipartStreamMode.Auto || streamMode == MultipartStreamMode.PartialFileStream)
            {
                if (streamToUpload is FileStream && streamToUpload.CanSeek && streamToUpload.CanRead)
                {
                    streamMode = MultipartStreamMode.PartialFileStream;
                }
                else if (streamToUpload.CanRead)
                {
                    streamMode = MultipartStreamMode.SequenceInputStream;
                }
            }
            if (streamMode == MultipartStreamMode.PartialFileStream)
            {
                var client = clientBuilder.Build(o =>
                {
                    o.ProgressUpdateInterval = progressUpdateInterval;
                });

                var uploadId = InitiateMultipartUpload(client, bucketName, objectName);
                using (var cleaner = new MultipartCleaner(client, bucketName, objectName, uploadId))
                {
                    var partETags = UploadParts(client, bucketName, objectName, streamToUpload, uploadId, partSize,
                        (s, e) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            streamTransferProgress?.Invoke(s, e);
                        });
                    retVal = CompleteUploadPart(client, bucketName, objectName, uploadId, partETags);
                    cleaner.Complete();
                }
            }
            else if (streamMode == MultipartStreamMode.SequenceInputStream)
            {
                var client = clientBuilder.Build(o =>
                {
                    o.ProgressUpdateInterval = progressUpdateInterval;
                });

                var uploadId = InitiateMultipartUpload(client, bucketName, objectName);
                using (var cleaner = new MultipartCleaner(client, bucketName, objectName, uploadId))
                {
                    var partETags = UploadPartsWithCache(client, bucketName, objectName, streamToUpload, uploadId, partSize,
                        (s, e) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            streamTransferProgress?.Invoke(s, e);
                        });
                    retVal = CompleteUploadPart(client, bucketName, objectName, uploadId, partETags);
                    cleaner.Complete();
                }
            }
            else
            {
                throw new ArgumentException("Stream Type is not supported", nameof(streamToUpload));
            }
#if DEBUG
            if (OutputDebugInfo)
            {
                Console.WriteLine("Multipart put object:{0} succeeded", objectName);
            }
#endif
            return retVal;
        }

        public static CompleteMultipartUploadResult UploadMultipart
            (
            IOssClientBuilder clientBuilder,
            string bucketName,
            string objectName,
            string fileToUpload,
            int partSize = DEFAULT_PART_SIZE,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            CompleteMultipartUploadResult retVal = null;

            if (clientBuilder == null) throw new ArgumentNullException(nameof(clientBuilder));

            long fileSize = new FileInfo(fileToUpload).Length;
            long progressUpdateInterval = (fileSize / 100 + 4096) / 4096 * 4096;

            var client = clientBuilder.Build(o =>
            {
                o.ProgressUpdateInterval = progressUpdateInterval;
            });

            var uploadId = InitiateMultipartUpload(client, bucketName, objectName);
            using (var cleaner = new MultipartCleaner(client, bucketName, objectName, uploadId))
            {
                var partETags = UploadParts(client, bucketName, objectName, fileToUpload, uploadId, partSize,
                    (s, e) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        streamTransferProgress?.Invoke(s, e);
                    });
                retVal = CompleteUploadPart(client, bucketName, objectName, uploadId, partETags);
                cleaner.Complete();
            }
#if DEBUG
            if (OutputDebugInfo)
            {
                Console.WriteLine("Multipart put object:{0} succeeded", objectName);
            }
#endif
            return retVal;
        }

        private static string InitiateMultipartUpload(OssClient client, string bucketName, string objectName)
        {
            return Retry((_) =>
            {
                var request = new InitiateMultipartUploadRequest(bucketName, objectName);
                var result = client.InitiateMultipartUpload(request);
                return result.UploadId;
            });
        }

        private static List<PartETag> UploadParts
            (
            OssClient client,
            string bucketName,
            string objectName,
            Stream streamToUpload,
            string uploadId,
            int partSize,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            long fileSize = streamToUpload.Length;

            MultipartStreamProgressMonitor progressMonitor = null;
            if (streamTransferProgress != null)
            {
                progressMonitor = new MultipartStreamProgressMonitor();
                progressMonitor.StreamTransferProgress += streamTransferProgress;
                progressMonitor.TotalBytes = fileSize;
#if DEBUG
                progressMonitor.ShowProgressOnConsole = OutputDebugInfo;
#endif
            }

            var partCount = fileSize / partSize;
            if (fileSize % partSize != 0)
            {
                partCount++;
            }

            var partETags = new List<PartETag>();
            for (var i = 0; i < partCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var skipBytes = (long)partSize * i;
                var size = (partSize < fileSize - skipBytes) ? partSize : (fileSize - skipBytes);

                string md5 = HashUtility.CalulateHash_MD5(streamToUpload, skipBytes, size);

                Retry((_) =>
                {
                    streamToUpload.Seek(skipBytes, 0);
                    var request = new UploadPartRequest(bucketName, objectName, uploadId)
                    {
                        InputStream = streamToUpload,
                        Md5Digest = md5,
                        PartSize = size,
                        PartNumber = i + 1
                    };
                    if (progressMonitor != null)
                    {
                        request.StreamTransferProgress += (s, e) => progressMonitor.PartStreamTransferProgress(i, e);
                    }
                    var result = client.UploadPart(request);
                    partETags.Add(result.PartETag);
                },
                cancellationToken: cancellationToken);
#if DEBUG
                if (OutputDebugInfo)
                {
                    Console.WriteLine("finish {0}/{1}", partETags.Count, partCount);
                }
#endif
            }
            return partETags;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "<Pending>")]
        private static List<PartETag> UploadParts
            (
            OssClient client,
            string bucketName,
            string objectName,
            string fileToUpload,
            string uploadId,
            int partSize,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            var fileSize = new FileInfo(fileToUpload).Length;
            long progressUpdateInterval = (fileSize / 100 + 4096) / 4096 * 4096;

            MultipartStreamProgressMonitor progressMonitor = null;
            if (streamTransferProgress != null)
            {
                progressMonitor = new MultipartStreamProgressMonitor();
                progressMonitor.StreamTransferProgress += streamTransferProgress;
                progressMonitor.TotalBytes = fileSize;
#if DEBUG
                progressMonitor.ShowProgressOnConsole = OutputDebugInfo;
#endif
            }

            var partCount = fileSize / partSize;
            if (fileSize % partSize != 0)
            {
                partCount++;
            }

            var partETags = new List<PartETag>();
            using (var fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                for (var i = 0; i < partCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var skipBytes = (long)partSize * i;
                    var size = (partSize < fileSize - skipBytes) ? partSize : (fileSize - skipBytes);

                    string md5 = null;
                    using (MD5 md5Hash = MD5.Create())
                    using (var md5stream = new PartialFileStream(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read, skipBytes, size))
                    {
                        md5 = Convert.ToBase64String(md5Hash.ComputeHash(md5stream));
                    }

                    Retry((_) =>
                    {
                        fs.Seek(skipBytes, 0);
                        var request = new UploadPartRequest(bucketName, objectName, uploadId)
                        {
                            InputStream = fs,
                            Md5Digest = md5,
                            PartSize = size,
                            PartNumber = i + 1
                        };
                        if (progressMonitor != null)
                        {
                            request.StreamTransferProgress += (s, e) => progressMonitor.PartStreamTransferProgress(i, e);
                        }
                        var result = client.UploadPart(request);
                        partETags.Add(result.PartETag);
                    },
                    cancellationToken: cancellationToken);
#if DEBUG
                    if (OutputDebugInfo)
                    {
                        Console.WriteLine("finish {0}/{1}", partETags.Count, partCount);
                    }
#endif
                }
            }
            return partETags;
        }

        private static List<PartETag> UploadPartsWithCache
            (
            OssClient client,
            string bucketName,
            string objectName,
            Stream streamToUpload,
            string uploadId,
            int partSize,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            long fileSize = streamToUpload.Length;

            MultipartStreamProgressMonitor progressMonitor = null;
            if (streamTransferProgress != null)
            {
                progressMonitor = new MultipartStreamProgressMonitor();
                progressMonitor.StreamTransferProgress += streamTransferProgress;
                progressMonitor.TotalBytes = fileSize;
#if DEBUG
                progressMonitor.ShowProgressOnConsole = OutputDebugInfo;
#endif
            }

            byte[] buffer = new byte[partSize];
            var partETags = new List<PartETag>();
            for (int i = 0; /* part count is unknown */ ; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var ms = new MemoryStream())
                {
                    int readSize = streamToUpload.Read(buffer, 0, partSize);
                    if (readSize == 0)
                    {
                        break;
                    }
                    string md5 = HashUtility.CalulateHash_MD5(buffer, 0, readSize);

                    Retry((_) =>
                    {
                        var request = new UploadPartRequest(bucketName, objectName, uploadId)
                        {
                            InputStream = new MemoryStream(buffer, 0, readSize),
                            Md5Digest = md5,
                            PartSize = readSize,
                            PartNumber = i + 1
                        };
                        if (progressMonitor != null)
                        {
                            request.StreamTransferProgress += (s, e) => progressMonitor.PartStreamTransferProgress(i, e);
                        }
                        var result = client.UploadPart(request);
                        partETags.Add(result.PartETag);
                    },
                    cancellationToken: cancellationToken);

                    if (readSize < partSize)
                    {
                        break;
                    }
                }
#if DEBUG
                if (OutputDebugInfo)
                {
                    Console.WriteLine("finish {0} parts", partETags.Count);
                }
#endif
            }
            return partETags;
        }

        private static CompleteMultipartUploadResult CompleteUploadPart(OssClient client, string bucketName, string objectName,
            string uploadId, List<PartETag> partETags)
        {
            return Retry((_) =>
            {
                var completeMultipartUploadRequest =
                    new CompleteMultipartUploadRequest(bucketName, objectName, uploadId);
                foreach (var partETag in partETags)
                {
                    completeMultipartUploadRequest.PartETags.Add(partETag);
                }
                return client.CompleteMultipartUpload(completeMultipartUploadRequest);
            });
        }
    }
}
