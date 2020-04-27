using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NSPersonalCloud.Interfaces.Apps
{
    public interface IAppManager
    {
        public string GetAppId();
        public  Task InstallWebStatiFiles(string webstaticpath);
        public List<AppLauncher> Config(string configjsons);

        public EmbedIO.WebServer ConfigWebController(string id, string path, EmbedIO.WebServer webServer);


    }
}
