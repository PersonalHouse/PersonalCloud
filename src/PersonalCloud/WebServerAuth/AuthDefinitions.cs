using System;
using System.Collections.Generic;
using System.Text;

namespace NSPersonalCloud.FileSharing
{
    public static class AuthDefinitions
    {
        public const string AuthenticationVersion = "PCAuthVer";
        public const string AuthenticationTimeStamp = "AuthTS";
        public const string AuthenticationHash = "AuthHash";
        public const string AuthenticationPCId = "PcId";
        public const string AuthenticationType = "PCIntranetRaw";
        public const int CurAuthVersion = 2;

        public const string HttpFileLength = "FileLength";
    }
}
