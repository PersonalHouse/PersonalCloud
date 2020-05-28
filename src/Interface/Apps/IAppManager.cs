using System.Collections.Generic;
using System.Threading.Tasks;

using EmbedIO;

namespace NSPersonalCloud.Interfaces.Apps
{
    public interface IAppManager
    {
        string GetAppId();

        Task InstallWebStatiFiles(string webstaticpath);

        List<AppLauncher> Config(string configjsons);

        WebServer ConfigWebController(string id, string path, WebServer webServer);
    }
}
