using System;
using Aliyun.OSS;

namespace NSPersonalCloud.FileSharing.Aliyun
{
    public class OssConfig
    {
        public string OssEndpoint { get; set; }

        public string AccessKeyId { get; set; }

        public string AccessKeySecret { get; set; }

        public string BucketName { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(OssEndpoint)
                && !string.IsNullOrWhiteSpace(AccessKeyId)
                && !string.IsNullOrWhiteSpace(AccessKeySecret)
                && !string.IsNullOrWhiteSpace(BucketName);
        }
    }

    public static class OssConfigExtensions
    {
        public static bool Verify(this OssConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            try
            {
                var client = new OssClient(config.OssEndpoint, config.AccessKeyId, config.AccessKeySecret);
                var info = client.GetBucketInfo(config.BucketName);
                return info != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
