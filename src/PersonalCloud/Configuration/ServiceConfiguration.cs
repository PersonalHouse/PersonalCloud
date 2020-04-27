using System;
using System.Collections.Generic;

namespace NSPersonalCloud.Config
{
#pragma warning disable CA1051 // Do not declare visible instance fields
    public class AppServiceConfiguration
    {
        public string PcId;
        public string JsonConfig;
    }
    public class ServiceConfiguration
    {
        public Guid Id;
        public int Port;

        public List<AppServiceConfiguration> Apps;
    }
#pragma warning restore CA1051 // Do not declare visible instance fields
}
