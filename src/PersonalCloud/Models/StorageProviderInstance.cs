using System;

using Newtonsoft.Json;

using NSPersonalCloud.FileSharing.Aliyun;

namespace NSPersonalCloud
{
    public class StorageProviderInstance
    {
        public const string TypeAliYun = "Aliyun-OSS";

        public Guid RuntimeId { get; }
        public StorageProviderInfo ProviderInfo { get; }

        public StorageProviderInstance(StorageProviderInfo providerInfo)
        {
            RuntimeId = Guid.NewGuid();
            ProviderInfo = providerInfo ?? throw new ArgumentNullException(nameof(providerInfo));
        }
    }

    internal class StorageProviderInstance_AliyunOSS : StorageProviderInstance
    {
        public StorageProviderInstance_AliyunOSS(StorageProviderInfo providerInfo) : base(providerInfo)
        {
            OssConfig = JsonConvert.DeserializeObject<OssConfig>(ProviderInfo.Settings);
            if (OssConfig == null || !OssConfig.IsValid())
            {
                throw new Exception("Invalid config for Aliyun OSS");
            }
        }

        public OssConfig OssConfig { get; }
    }
}
