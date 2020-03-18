using Microsoft.Extensions.Logging;

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

        //public ConcurrentDictionary<string, string> CachedNodes { get; set; }//node guid,url

        public PersonalCloud ToPersonalCloud(ILogger l, IPCService pcsrv)
        {
            return new PersonalCloud(l, pcsrv) {
                Id = Id,
                DisplayName = DisplayName,
                NodeDisplayName = NodeDisplayName,
                MasterKey = MasterKey,
                UpdateTimeStamp = TimeStamp
            };          

        }

        internal static PersonalCloudInfo FromPersonalCloud(PersonalCloud pc)
        {
            return new PersonalCloudInfo {
                Id = pc.Id,
                DisplayName = pc.DisplayName,
                NodeDisplayName = pc.NodeDisplayName,
                MasterKey = pc.MasterKey,
                TimeStamp = pc.UpdateTimeStamp
            };

        }
    }
}
