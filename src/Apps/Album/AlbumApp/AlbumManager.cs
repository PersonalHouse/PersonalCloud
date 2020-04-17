using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSPersonalCloud.Interfaces.Apps;

namespace NSPersonalCloud.Apps.Album
{
    public class AlbumManager : IAppManager
    {
        async Task<List<IApp>> IAppManager.GetApps()
        {
            return null;
        }

        async Task<List<Tuple<string, EmbedIO.WebApi.WebApiController>>> IAppManager.GetWebControllers()
        {
            return null;
        }

        Task IAppManager.Init(string _)
        {
            return Task.CompletedTask;
        }

        Task IAppManager.InstallWebStatiFiles(string _)
        {
            return Task.CompletedTask;
        }
    }
}
