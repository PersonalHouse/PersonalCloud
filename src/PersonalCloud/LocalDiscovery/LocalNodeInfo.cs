using System.Collections.Generic;

namespace NSPersonalCloud.LocalDiscovery
{
    public class LocalNodeInfo
    {
        public string NodeId;
        public string PCVersion;
        public long StatusTimeStamp;//sender StatusTimeStamp, for versioning
        public int MissCount;//How many times did this node miss the local node echo?
        public bool Fetched;
        public string Url;//The url could reach the device
        public List<string> PcIds;//list of personal cloud's ids.
    }
}
