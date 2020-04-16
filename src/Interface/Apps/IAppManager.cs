using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NSPersonalCloud.Interfaces.Apps
{
    public interface IAppManager
    {
        public  Task InstallWebStatiFiles(string webstaticpath);
        public  Task Init(string json);
        public Task<List<Tuple<string, EmbedIO.WebApi.WebApiController>>> GetWebControllers();

        public Task<List<IApp>> GetApps();

    }
}
