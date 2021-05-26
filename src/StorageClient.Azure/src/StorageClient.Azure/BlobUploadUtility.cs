using Azure;
using Azure.Storage;
using Azure.Storage.Blobs.Models;

using System;
using System.IO;
using System.Threading;

namespace NSPersonalCloud.StorageClient.Azure
{
    public static class BlobUploadUtility
    {
        public static Response<BlobContentInfo> Upload
            (
            IAzureBlobClientBuilder clientBuilder,
            string blobName,
            string objectName,
            Stream streamToUpload,
            StorageTransferOptions transferOptions = default(StorageTransferOptions),
            EventHandler<AzureTransferProgressEventArgs> transferProgress = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            if (clientBuilder == null) throw new ArgumentNullException(nameof(clientBuilder));
            if (streamToUpload is null) throw new ArgumentNullException(nameof(streamToUpload));

            var client = clientBuilder.GetBlobContainerClient(blobName).GetBlobClient(objectName);

            long streamLength = 0;
            if (streamToUpload.CanSeek)
            {
                streamLength = streamToUpload.Length;
            }

            return client.Upload(streamToUpload,
                transferOptions: transferOptions,
                progressHandler: new AzureTransferProgressAdapter(streamLength, transferProgress),
                cancellationToken: cancellationToken);
        }
    }

    internal class AzureTransferProgressAdapter : IProgress<long>
    {
        private readonly long _StreamLength;
        private readonly EventHandler<AzureTransferProgressEventArgs> _EventHandler;

        public AzureTransferProgressAdapter
            (
            long streamLength,
            EventHandler<AzureTransferProgressEventArgs> eventHandler
            )
        {
            _StreamLength = streamLength;
            _EventHandler = eventHandler;
        }

        public void Report(long value)
        {
            _EventHandler.Invoke(this, new AzureTransferProgressEventArgs(value, _StreamLength));
        }
    }
}
