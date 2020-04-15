using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSPersonalCloud.FileSharing.Aliyun;

namespace NSPersonalCloud
{
    public class PersonalCloudInfo
    {
        //display name for human beings
        public string DisplayName { get; set; }
        public string NodeDisplayName { get; set; }
        public string NodeGuid{ get;set ;}
        public string Id { get; set; }  //PersonalCloud identifier
                                        //Cloud password
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[] MasterKey { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        public long TimeStamp { get; set; }

        public List<StorageProviderInfo> StorageProviders { get; }

        //public ConcurrentDictionary<string, string> CachedNodes { get; set; }//node guid,url

        public PersonalCloudInfo(List<StorageProviderInfo> storageProviderInfos)
        {
            StorageProviders = storageProviderInfos ?? new List<StorageProviderInfo>();
        }

        public PersonalCloud ToPersonalCloud(ILogger l, IPCService pcsrv)
        {
            return new PersonalCloud(l, pcsrv, StorageProviders) {
                Id = Id,
                DisplayName = DisplayName,
                NodeDisplayName = NodeDisplayName,
                MasterKey = MasterKey,
                UpdateTimeStamp = TimeStamp
            };          
        }

        internal static PersonalCloudInfo FromPersonalCloud(PersonalCloud pc)
        {
            
            return new PersonalCloudInfo(pc.StorageProviderInstances.Select(x => x.ProviderInfo).ToList()) {
                Id = pc.Id,
                DisplayName = pc.DisplayName,
                NodeDisplayName = pc.NodeDisplayName,
                MasterKey = pc.MasterKey,
                TimeStamp = pc.UpdateTimeStamp
            };

        }
    }

    public class StorageProviderInfo
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public StorageProviderVisibility Visibility { get; set; }
        public string Settings { get; set; }
    }

    public enum StorageProviderVisibility
    {
        Private = 0,
        Public = 1
    }

    public class StorageProviderInstance
    {
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
        public StorageProviderInstance_AliyunOSS(StorageProviderInfo providerInfo)
            : base(providerInfo)
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
