using Aliyun.OSS;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public class AppendUploadResult
    {
        public string ETag { get; set; }

        public string Crc64 { get; set; }

        public long ContentLength { get; set; }

        public AppendObjectResult AppendObjectResult { get; set; }
    }
}
