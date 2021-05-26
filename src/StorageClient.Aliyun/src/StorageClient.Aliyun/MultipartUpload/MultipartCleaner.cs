using Aliyun.OSS;
using System;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public class MultipartCleaner : IDisposable
    {
        private OssClient _Client;
        private AbortMultipartUploadRequest _Request = null;

        public MultipartCleaner(OssClient client, string bucketName, string objectName, string uploadId)
        {
            _Client = client;
            _Request = new AbortMultipartUploadRequest(bucketName, objectName, uploadId);
        }

        public void Complete()
        {
            _Request = null; // No need to cleanup Multipart files
        }

        #region IDisposable Support

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    if (_Request != null)
                    {
                        try
                        {
                            _Client.AbortMultipartUpload(_Request);
                            _Request = null;
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            // No Permission, Leave the files there.
                        }
                    }
                }

                disposedValue = true;
            }
        }

        #endregion
    }
}
