using System;
using System.Collections.Generic;

namespace NSPersonalCloud.LocalDiscovery
{
    class NodeInfoInNet : IEquatable<NodeInfoInNet>
    {
        public string NodeGuid;
        public string PCVersion;
        public long TimeStamp;//sender StatusTimeStamp, for versioning
        public string Url;

        public override int GetHashCode()
        {
            return NodeGuid.GetHashCode() & TimeStamp.GetHashCode() & Url.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NodeInfoInNet);
        }


        public bool Equals(NodeInfoInNet other)
        {
            if (NodeGuid != other.NodeGuid)
            {
                return false;
            }
            if (TimeStamp != other.TimeStamp)
            {
                return false;
            }
            if (Url != other.Url)
            {
                return false;
            }
            return true;
        }
    }

}


