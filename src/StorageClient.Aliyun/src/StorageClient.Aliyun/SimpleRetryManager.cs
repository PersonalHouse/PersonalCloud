using System;
using System.Threading;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    internal static class SimpleRetryManager
    {
        public static void Retry(Action<int> action, int maxRetryCount = 3, int waitTimeBeforeRetry = 300, CancellationToken cancellationToken = default(CancellationToken))
        {
            int retryCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    action(retryCount);
                    break;
                }
                catch
                {
                    ++retryCount;
                    if (retryCount > maxRetryCount)
                    {
                        throw;
                    }
                    Thread.Sleep(waitTimeBeforeRetry);
                }
            }
        }

        public static T Retry<T>(Func<int, T> action, int maxRetryCount = 3, int waitTimeBeforeRetry = 300, CancellationToken cancellationToken = default(CancellationToken))
        {
            int retryCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return action(retryCount);
                }
                catch
                {
                    ++retryCount;
                    if (retryCount > maxRetryCount)
                    {
                        throw;
                    }
                    Thread.Sleep(waitTimeBeforeRetry);
                }
            }
        }
    }
}
