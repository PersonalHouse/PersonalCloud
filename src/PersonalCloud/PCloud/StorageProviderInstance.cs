using System;

using Newtonsoft.Json;

using NSPersonalCloud.FileSharing.Aliyun;

namespace NSPersonalCloud
{
    public abstract class StorageProviderInstance
    {
        public const string TypeAliYun = "Aliyun-OSS";
        public const string TypeAzure = "Azure-Blob";

        public Guid RuntimeId { get; }
        public StorageProviderInfo ProviderInfo { get; }

        public StorageProviderInstance(StorageProviderInfo providerInfo)
        {
            if (providerInfo == null) throw new ArgumentNullException(nameof(providerInfo));
            RuntimeId = providerInfo.Id;
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

    internal class StorageProviderInstance_AzureBlob : StorageProviderInstance
    {
        public StorageProviderInstance_AzureBlob(StorageProviderInfo providerInfo) : base(providerInfo)
        {
            AzureBlobConfig = JsonConvert.DeserializeObject<AzureBlobConfig>(ProviderInfo.Settings);
            if (AzureBlobConfig == null || !AzureBlobConfig.IsValid())
            {
                throw new Exception("Invalid config for Azure Blob");
            }
        }

        public AzureBlobConfig AzureBlobConfig { get; }
    }
}
