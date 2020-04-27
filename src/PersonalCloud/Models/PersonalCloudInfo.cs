using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using NSPersonalCloud.Interfaces.Apps;

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
        public List<AppLauncher> Apps { get; internal set; }

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
                UpdateTimeStamp = TimeStamp,
                Apps = Apps
            };          
        }

        internal static PersonalCloudInfo FromPersonalCloud(PersonalCloud pc)
        {

            return new PersonalCloudInfo(pc.StorageProviderInstances.Select(x => x.ProviderInfo).ToList()) {
                Id = pc.Id,
                DisplayName = pc.DisplayName,
                NodeDisplayName = pc.NodeDisplayName,
                MasterKey = pc.MasterKey,
                TimeStamp = pc.UpdateTimeStamp,
                Apps = pc.Apps
            };

        }
    }
}
