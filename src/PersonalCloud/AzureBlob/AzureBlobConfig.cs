using System;
using System.Linq;
using Azure.Storage.Blobs;

namespace NSPersonalCloud.FileSharing.Aliyun
{
    public class AzureBlobConfig
    {
        public string ConnectionString { get; set; }

        public string BlobName { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ConnectionString)
                && !string.IsNullOrWhiteSpace(BlobName);
        }
    }

    public static class AzureBlobConfigExtensions
    {
        public static bool Verify(this AzureBlobConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            try
            {
                var client = new BlobContainerClient(config.ConnectionString, config.BlobName);
                var info = client.GetBlobsByHierarchy(prefix: "", delimiter: "/").AsPages(pageSizeHint: 1).FirstOrDefault();
                return info != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
