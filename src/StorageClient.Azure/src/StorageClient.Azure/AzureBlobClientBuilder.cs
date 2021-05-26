using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NSPersonalCloud.StorageClient.Azure
{
    public class AzureBlobClientBuilder : IAzureBlobClientBuilder
    {
        private readonly string _ConnectionString;

        public AzureBlobClientBuilder(string connectionString)
        {
            _ConnectionString = connectionString;
        }

        public BlobContainerClient GetBlobContainerClient(string containerName)
        {
            return new BlobContainerClient(_ConnectionString, containerName);
        }

        public BlockBlobClient GetBlockBlobClient(string containerName, string objectName)
        {
            return new BlockBlobClient(_ConnectionString, containerName, objectName);
        }
    }
}
