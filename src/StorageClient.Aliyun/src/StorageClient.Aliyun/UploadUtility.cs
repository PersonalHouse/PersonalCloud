using Aliyun.OSS;
using System;
using System.IO;
using System.Threading;
using static NSPersonalCloud.StorageClient.Aliyun.SimpleRetryManager;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public static class UploadUtility
    {
        public static PutObjectResult SimpleUpload
            (
            IOssClientBuilder clientBuilder, // Ensure OssClient is created with MD5 check option.
            string bucketName,
            string objectName,
            Stream streamToUpload,
            EventHandler<StreamTransferProgressArgs> streamTransferProgress = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            if (clientBuilder == null) throw new ArgumentNullException(nameof(clientBuilder));
            var client = clientBuilder.Build(o => o.EnalbeMD5Check = true);
            return Retry((_) =>
                client.PutObject(new PutObjectRequest(bucketName, objectName, streamToUpload)
                {
                    StreamTransferProgress = (s, e) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        streamTransferProgress?.Invoke(s, e);
                    }
                })
            );
        }

        public static CompleteMultipartUploadResult MultipartUpload
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
            return MultipartUploadUtility.UploadMultipart(clientBuilder, bucketName, objectName, streamToUpload,
                options: options,
                streamTransferProgress: streamTransferProgress,
                cancellationToken: cancellationToken);
        }

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
            return AppendUploadUtility.AppendUpload(clientBuilder, bucketName, objectName, streamToUpload,
                options: options,
                streamTransferProgress: streamTransferProgress,
                cancellationToken: cancellationToken);
        }
    }
}
