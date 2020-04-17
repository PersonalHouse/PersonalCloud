using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSPersonalCloud.Interfaces.Apps;

namespace NSPersonalCloud.Apps.Album
{
    public class AlbumManager : IAppManager
    {
        Task<List<IApp>> IAppManager.GetApps()
        {
            throw new NotImplementedException();
        }

        Task<List<Tuple<string, global::EmbedIO.WebApi.WebApiController>>> IAppManager.GetWebControllers()
        {
            throw new NotImplementedException();
        }

        Task IAppManager.Init(string json)
        {
            throw new NotImplementedException();
        }

        Task IAppManager.InstallWebStatiFiles(string webstaticpath)
        {
            throw new NotImplementedException();
        }
    }
}
