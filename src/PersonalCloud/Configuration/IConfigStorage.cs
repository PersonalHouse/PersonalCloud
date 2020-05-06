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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appid"></param>
        /// <returns>list of pcid,json string</returns>
        List<Tuple<string,string>> GetApp(string appid);
    }
}
