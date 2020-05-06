using System;
using System.Collections.Generic;

using NSPersonalCloud.Config;

namespace NSPersonalCloud
{
    public interface IConfigStorage
    {
        ServiceConfiguration LoadConfiguration();
        void SaveConfiguration(ServiceConfiguration config);

        IEnumerable<PersonalCloudInfo> LoadCloud();
        void SaveCloud(IEnumerable<PersonalCloudInfo> cloud);

        void SaveApp(string appid, string pcid,string jsonconfigs);

        List<(string,string)> GetApp(string appid);
    }
}
