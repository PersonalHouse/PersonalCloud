using System;
using System.Collections.Generic;
using System.Text;

namespace NSPersonalCloud.Interfaces.Apps
{
    public class AppLauncher
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        public string Name;
        public AppType AppType;
        public string NodeId;
        public string AppId;
        public string WebAddress;
        public string AccessKey;
#pragma warning restore CA1051 // Do not declare visible instance fields
    }
}
