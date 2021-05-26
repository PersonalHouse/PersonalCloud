using Aliyun.OSS;
using System;
using System.Collections.Generic;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public interface IStreamProgressMonitor
    {
        event EventHandler<StreamTransferProgressArgs> StreamTransferProgress;
    }

    public class MultipartStreamProgressMonitor : IStreamProgressMonitor
    {
        public long TotalBytes { get; set; }

        public event EventHandler<StreamTransferProgressArgs> StreamTransferProgress;

        private Dictionary<int, long> _TransferredBytes = new Dictionary<int, long>();

#if DEBUG
        public bool ShowProgressOnConsole { get; set; }
#endif

        public void PartStreamTransferProgress(int partIndex, StreamTransferProgressArgs args)
        {
            if (args == null) return;

            long nTransferredBytes = 0;
            lock (_TransferredBytes)
            {
                _TransferredBytes[partIndex] = args.TransferredBytes;
                foreach (var item in _TransferredBytes)
                {
                    nTransferredBytes += item.Value;
                }
            }
            if (TotalBytes != 0)
            {
#if DEBUG
                if (ShowProgressOnConsole)
                {
                    if (this.TotalBytes > 0)
                    {
                        Console.WriteLine($"[{partIndex}] +{args.IncrementTransferred} --> {nTransferredBytes} / {this.TotalBytes}, {Math.Truncate((double)nTransferredBytes / (double)this.TotalBytes * 1000.0) / 1000.0:P1}");
                    }
                    else
                    {
                        Console.WriteLine($"[{partIndex}] +{args.IncrementTransferred} --> {nTransferredBytes}");
                    }
                }
#endif
                StreamTransferProgress?.Invoke(this, new StreamTransferProgressArgs(args.IncrementTransferred, nTransferredBytes, this.TotalBytes));
            }
        }
    }
}
