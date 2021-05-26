using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NSPersonalCloud.StorageClient.Azure
{
    public interface IAzureBlobClientBuilder
    {
        BlobContainerClient GetBlobContainerClient(string containerName);

        BlockBlobClient GetBlockBlobClient(string containerName, string objectName);
    }
}
