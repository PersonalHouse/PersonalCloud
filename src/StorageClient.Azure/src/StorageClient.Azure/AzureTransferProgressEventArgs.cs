using System;

namespace NSPersonalCloud.StorageClient.Azure
{
    public class AzureTransferProgressEventArgs : EventArgs
    {
        public AzureTransferProgressEventArgs(long transferred, long total)
        {
            TransferredBytes = transferred;
            TotalBytes = total;
        }

        public long TransferredBytes { get; }

        public long TotalBytes { get; }
    }
}
