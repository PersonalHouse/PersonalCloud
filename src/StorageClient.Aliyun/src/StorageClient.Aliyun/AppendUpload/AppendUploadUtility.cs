using Aliyun.OSS;
using Aliyun.OSS.Common;
using Aliyun.OSS.Common.Internal;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using static NSPersonalCloud.StorageClient.Aliyun.SimpleRetryManager;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public static class AppendUploadUtility
    {
        internal const int MIN_BLOCK_SIZE = 256 * 1024;
        internal const int DEFAULT_BLOCK_SIZE = 1024 * 1024;

        public static AppendUploadResult AppendUpload
            (
            IOssClientBuilder clientBuilder,
            string bucketName,
            string objectName,
            Stream streamToUpload,
            Action<AppendUploadOptions> options = null,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            if (clientBuilder == null) throw new ArgumentNullException(nameof(clientBuilder));
            if (streamToUpload == null) throw new ArgumentNullException(nameof(streamToUpload));

            AppendUploadOptions uploadOptions = new AppendUploadOptions();
            options?.Invoke(uploadOptions);
            int blockSize = Math.Max(MIN_BLOCK_SIZE, uploadOptions.InitialBlockSize);

            var client = clientBuilder.Build(o => o.EnableCrcCheck = true);
            long position = 0;
            ObjectMetadata metadata = null;
            try
            {
                metadata = client.GetObjectMetadata(bucketName, objectName);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // No such object
            }

            if (metadata != null)
            {
                if (string.Compare(metadata.ObjectType, "Appendable", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    if (metadata.ContentLength > 0)
                    {
                        if (metadata.ContentLength <= streamToUpload.Length)
                        {
                            if (uploadOptions.AllowResume)
                            {
                                position = metadata.ContentLength;
                                if (uploadOptions.VerifyBeforeResume)
                                {
                                    using (var stream = new PartialStream(streamToUpload, 0, position))
                                    using (var crc64 = new Crc64HashAlgorithm())
                                    {
                                        crc64.ComputeHash(stream);
                                        string localHash = BitConverter.ToUInt64(crc64.Hash, 0).ToString(CultureInfo.InvariantCulture);
                                        if (string.Compare(metadata.Crc64, localHash, StringComparison.InvariantCultureIgnoreCase) != 0)
                                        {
                                            if (uploadOptions.AllowOverwrite)
                                            {
                                                client.DeleteObject(bucketName, objectName);
                                                position = 0;
                                            }
                                            else
                                            {
                                                throw new OssException("Hash mismatched, could not resume file");
                                            }
                                        }
                                    }
                                }
                            }
                            else if (uploadOptions.AllowOverwrite)
                            {
                                client.DeleteObject(bucketName, objectName);
                            }
                            else
                            {
                                throw new OssException("Could not resume or overwrite file");
                            }
                        }
                        else
                        {
                            if (uploadOptions.AllowOverwrite)
                            {
                                client.DeleteObject(bucketName, objectName);
                            }
                            else
                            {
                                throw new OssException("Could not resume, the length of remote object is longer than local object.");
                            }
                        }
                    }
                }
                else if (uploadOptions.AllowOverwrite)
                {
                    client.DeleteObject(bucketName, objectName);
                }
                else
                {
                    throw new OssException("The object is not appendable");
                }
            }

            if (streamToUpload.Length == position)
            {
                return new AppendUploadResult { ETag = metadata.ETag, Crc64 = metadata.Crc64, ContentLength = metadata.ContentLength };
            }

            while (true)
            {
                var result = Retry((retryCount) =>
                {
                    if (retryCount > 0)
                    {
                        blockSize = Math.Max(MIN_BLOCK_SIZE, blockSize >> 1);
                    }
                    var length = Math.Min(blockSize, streamToUpload.Length - position);
                    var request = new AppendObjectRequest(bucketName, objectName)
                    {
                        Content = new PartialStream(streamToUpload, position, length),
                        Position = position,
                        StreamTransferProgress = (s, e) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (streamTransferProgress != null)
                            {
                                streamTransferProgress.Invoke(s, new StreamTransferProgressArgs(e.IncrementTransferred, position + e.TransferredBytes, streamToUpload.Length));
                            }
                        }
                    };
                    return client.AppendObject(request);
                },
                cancellationToken: cancellationToken);
                position = result.NextAppendPosition;
                if (position >= streamToUpload.Length)
                {
                    return new AppendUploadResult
                    {
                        ETag = result.ETag,
                        Crc64 = result.HashCrc64Ecma.ToString(CultureInfo.InvariantCulture),
                        ContentLength = result.NextAppendPosition,
                        AppendObjectResult = result
                    };
                }
            }
        }
    }
}
