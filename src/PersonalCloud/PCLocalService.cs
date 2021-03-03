using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using EmbedIO;
using EmbedIO.WebApi;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NSPersonalCloud.Config;
using NSPersonalCloud.FileSharing;
using NSPersonalCloud.FileSharing.Aliyun;
using NSPersonalCloud.Interfaces.Apps;
using NSPersonalCloud.Interfaces.Errors;

using Standart.Hash.xxHash;

namespace NSPersonalCloud
{


    public class PCLocalService : IDisposable, IPCService
    {
        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;

        private IConfigStorage ConfigStorage { get; }
        string ExtraWebPath;

        LocalDiscovery.LocalNodeRecords _LocalNodes;

        public int ServerPort { get; private set; }

        public string NodeId { get; private set; }//guid
        public IReadOnlyList<PersonalCloud> PersonalClouds => _PersonalClouds.AsReadOnly();



        public event EventHandler<ServiceErrorEventArgs> OnError;


        /// <summary>
        /// used in lock(_PersonalClouds){}
        /// </summary>
        private List<PersonalCloud> _PersonalClouds;
        WebServer WebServer;

        private SSDPServiceController CreateSSDPServiceController() => new SSDPServiceController(this);
        public ShareController CreateShareController() => new ShareController(_FileSystem, this);
        Zio.IFileSystem _FileSystem;
        public Zio.IFileSystem FileSystem
        {
            get=> _FileSystem;
            set {
                if (_FileSystem != null)
                {
                    try
                    {
                        _FileSystem.Dispose();
                    }
                    catch{}
                    _FileSystem = null;
                }
                _FileSystem = value;
                InitWebServer();
            }
        }

        public PCLocalService(IConfigStorage configStorage, ILoggerFactory logfac, Zio.IFileSystem fileSystem, string extraWebPath)
        {
            ExtraWebPath = extraWebPath;

            ConfigStorage = configStorage;
            _FileSystem = fileSystem;
            loggerFactory = logfac;
#pragma warning disable CA1062 // Validate arguments of public methods
            logger = loggerFactory.CreateLogger("PCLocalService");
#pragma warning restore CA1062 // Validate arguments of public methods


            _LocalNodes = new LocalDiscovery.LocalNodeRecords(logfac);
            _LocalNodes.OnNodeUpdate += LocalNet_OnNodeUpdate;
            _LocalNodes.OnError += NodeDiscovery_OnError;

            var cfg = LoadConfiguration();
            ServerPort = cfg.Port;
#pragma warning disable CA1305 // Specify IFormatProvider
            NodeId = cfg.Id.ToString("N");
#pragma warning restore CA1305 // Specify IFormatProvider

            LoadPCList();
            foreach (var item in _PersonalClouds)
            {
                item.ResyncClientList();
            }
            InitWebServer();

        }

        private void LocalNet_OnNodeUpdate(object sender, LocalDiscovery.LocalNodeUpdateEventArgs e)
        {
            var pcs = PersonalClouds;
            foreach (var pc in pcs)
            {
                _ = pc.OnLocalNodeUpdate(e.nodeInfo, e.PCinfos).ConfigureAwait(false);
            }
        }

        private void NodeDiscovery_OnError(object sender, ErrorCode e)
        {
            switch (e)
            {
                case ErrorCode.NeedUpdate:
                    OnError?.Invoke(this, new ServiceErrorEventArgs(e));
                    break;
                default:
                    break;
            }
        }

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        async Task EmbedIOResponseSerializerCallback(IHttpContext context, object? data)
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        {
            context.Response.ContentType = MimeType.Json;
            using var text = context.OpenResponseText(new UTF8Encoding(false));
            //             await text.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data,
            //                 new System.Text.Json.JsonSerializerOptions() { IgnoreNullValues = true })).ConfigureAwait(false);
            await text.WriteAsync(
                JsonConvert.SerializeObject(data,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })).ConfigureAwait(false);
        }

        void InitWebServer()
        {
            if (WebServer != null)
            {
                WebServer.Dispose();
                WebServer = null;
            }
            var curpath = Path.GetDirectoryName(typeof(PCLocalService).Assembly.Location);
            WebServer = new WebServer(ServerPort);
            WebServer = WebServer
                .WithModule(new PCWebServerAuth("/api/share", this))
                .WithWebApi("SSDP", "/clouds", EmbedIOResponseSerializerCallback, module => module.WithController(CreateSSDPServiceController))
                .WithWebApi("Share", "/api/share", EmbedIOResponseSerializerCallback, module => module.WithController(CreateShareController));
            if (!string.IsNullOrWhiteSpace(ExtraWebPath))
            {
                WebServer = WebServer.WithStaticFolder("/AppsStatic", ExtraWebPath, false);
            }
            var apps = GetAppMgrs();
            foreach (var appmgr in apps)
            {
                var id = appmgr.GetAppId();
                WebServer = appmgr.ConfigWebController(id, $"/api/Apps/{id}", WebServer);

            }
            WebServer.Start();

        }

        #region Config

        private ServiceConfiguration LoadConfiguration()
        {
            return ConfigStorage.LoadConfiguration() ?? NewLocalServiceConfig();
        }

        private ServiceConfiguration NewLocalServiceConfig()
        {
            var ran = new Random();
            var cfg = new ServiceConfiguration() {
                Id = Guid.NewGuid()
            };
            for (var i = 0; i < 100; i++)
            {

                cfg.Port = ran.Next(10000, 60000);
                TcpListener tcpListener = null;
                try
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    tcpListener = new TcpListener(cfg.Port);
#pragma warning restore CS0618 // Type or member is obsolete
                    tcpListener.Start();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    continue;
                }
                finally
                {
                    tcpListener?.Stop();
                }

                ConfigStorage.SaveConfiguration(cfg);

                return cfg;
            }
            throw new PortsInUseException();
        }
        private void SavePCList()
        {
            lock (_PersonalClouds)
            {
                ConfigStorage.SaveCloud(_PersonalClouds.Select(x => PersonalCloudInfo.FromPersonalCloud(x)));
            }
        }

        private void LoadPCList()
        {
            var saved = ConfigStorage.LoadCloud();
            if (saved == null)
            {
                _PersonalClouds = new List<PersonalCloud>();
                return;
            }
            var l = loggerFactory.CreateLogger("PersonalCloud");
            _PersonalClouds = saved.Select(x => x.ToPersonalCloud(l, this)).ToList();
        }

        #endregion

        #region storage providers


        public List<StorageProviderInstance> GetStorageProviderInstances(string cloudId)
        {
            PersonalCloud personalCloud = null;

            lock (_PersonalClouds)
            {
                personalCloud = _PersonalClouds.Where(x => x.Id == cloudId).FirstOrDefault();
            }

            if (personalCloud != null)
            {
                return personalCloud.StorageProviderInstances;
            }
            else
            {
                throw new NoSuchCloudException();
            }
        }

        public bool AddStorageProvider(string cloudId, Guid nodeId, string nodeName, OssConfig ossConfig, StorageProviderVisibility visibility, bool saveChanges = true)
        {
            PersonalCloud personalCloud = null;

            lock (_PersonalClouds)
            {
                personalCloud = _PersonalClouds.FirstOrDefault(x => x.Id == cloudId);
            }

            if (personalCloud != null)
            {
                bool haveChanges = personalCloud.AddStorageProvider(nodeId, nodeName, ossConfig, visibility);
                if (haveChanges && saveChanges)
                {
                    SavePCList();
                }
                return haveChanges;
            }
            else
            {
                throw new NoSuchCloudException();
            }
        }

        public bool AddStorageProvider(string cloudId, Guid nodeId, string nodeName, AzureBlobConfig azureConfig, StorageProviderVisibility visibility, bool saveChanges = true)
        {
            PersonalCloud personalCloud = null;

            lock (_PersonalClouds)
            {
                personalCloud = _PersonalClouds.FirstOrDefault(x => x.Id == cloudId);
            }

            if (personalCloud != null)
            {
                var haveChanges = personalCloud.AddStorageProvider(nodeId, nodeName, azureConfig, visibility);
                if (haveChanges && saveChanges)
                {
                    SavePCList();
                }
                return haveChanges;
            }
            else
            {
                throw new NoSuchCloudException();
            }
        }

        public bool RemoveStorageProvider(string cloudId, string nodeName, bool saveChanges = true)
        {
            PersonalCloud personalCloud = null;

            lock (_PersonalClouds)
            {
                personalCloud = _PersonalClouds.FirstOrDefault(x => x.Id == cloudId);
            }

            if (personalCloud != null)
            {
                bool haveChanges = personalCloud.RemoveStorageProvider(nodeName);
                if (haveChanges && saveChanges)
                {
                    SavePCList();
                }
                return haveChanges;
            }
            else
            {
                throw new NoSuchCloudException();
            }
        }

        #endregion

        #region Apps
        List<IAppManager> AppMgrs;
        List<IAppManager> GetAppMgrs()
        {
            try
            {
                if (AppMgrs == null)
                {

                    var mgrs = new List<IAppManager> {  };

                    var curpath = Path.GetDirectoryName(typeof(PCLocalService).Assembly.Location);
                    var pathapps = Directory.GetDirectories(Path.Combine(curpath, Definition.AppsFolder));
                    foreach (var item in pathapps)
                    {
                        var assemblyPath = Path.Combine(item, Definition.AppDllName);
                        if (!File.Exists(assemblyPath))
                        {
                            continue;
                        }
                        var appAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                        foreach (var ti in appAssembly.DefinedTypes)
                        {
                            if (ti.ImplementedInterfaces.Contains(typeof(IAppManager)))
                            {
                                var m = (IAppManager) Activator.CreateInstance(ti);
                                mgrs.Add(m);
                            }
                        }
                    }


                    AppMgrs = mgrs;
                }
                return AppMgrs;
            }
            catch (Exception e)
            {
                logger.LogError(e, "GetAppMgrs");
                return new List<IAppManager>();
            }
        }
//         public Task SetAlbumConfig(string pcid, List<Apps.Album.AlbumConfig> albcongs)
//         {
//             return SetAppMgrConfig("Album", pcid, JsonConvert.SerializeObject(albcongs));
//         }

//         public List<Apps.Album.AlbumConfig> GetAlbumConfig(string pcid)
//         {
// 
//             var json = GetAppConfig(pcid,"Album");
//             if (json != null)
//             {
//                 return JsonConvert.DeserializeObject<List<Apps.Album.AlbumConfig>>(json);
//             }
//             return new List<Apps.Album.AlbumConfig>();
//         }
        public string GetAppConfig(string pcid,string appid)
        {
            var lis = GetAppMgrs();
            var s = ConfigStorage.GetApp(appid);
            var (pcidloc, json) = s.FirstOrDefault(x => x.Item1 == pcid);
            return json;
        }

        public Task SetAppMgrConfig(string appid, string pcid, string jsonconfig)
        {
            try
            {
                var lis = GetAppMgrs();
                var appmgr = lis.FirstOrDefault(x => x.GetAppId() == appid);
                if (appmgr != null)
                {
                    var updatenet = false;
                    List<AppLauncher> appls = null;
                    try
                    {
                        appls = appmgr.Config(jsonconfig);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"Config error for {appmgr.GetAppId()}");
                        return Task.CompletedTask;
                    }
                    ConfigStorage.SaveApp(appid, pcid, jsonconfig);
                    PersonalCloud pc = null;
                    lock (_PersonalClouds)
                    {
                        pc = _PersonalClouds.FirstOrDefault(x => x.Id == pcid);
                    }
                    if (pc != null)
                    {
                        pc.CleanApps();
                        foreach (var appl in appls)
                        {
                            if (appl==null)
                            {
                                continue;
                            }
                            appl.NodeId = NodeId;
                            appl.AppId = appid;
                            pc.AddApp(appl);
                            updatenet = true;
                        }
                    }
                    if (updatenet)
                    {
                        _LocalNodes.BroadcastingIveChanged();
                    }
                }

                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                logger.LogError(e, "SetAppMgrConfig");
                throw;
            }
        }
        /// <summary>
        /// install apps. may be called multiple times
        /// </summary>
        /// <param name="webstaticpath"></param>
        /// <returns></returns>
        public async Task InstallApps()
        {
            if (string.IsNullOrWhiteSpace(ExtraWebPath))
            {
                throw new InvalidProgramException("extraWebPath must be provide in construtor");
            }
            var lis = GetAppMgrs();
            foreach (var item in lis)
            {
                try
                {
                    await item.InstallWebStatiFiles(ExtraWebPath).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Fail to InstallWebStatiFiles for {item?.GetAppId()}");
                }
            }
        }


        private void LoadApps()
        {
            var lis = GetAppMgrs();
            foreach (var item in lis)
            {
                var s = ConfigStorage.GetApp(item.GetAppId());
                foreach (var pcc in s)
                {

                    List<AppLauncher> appls = null;
                    try
                    {
                        appls = item.Config(pcc.Item2);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"Config error for {item.GetAppId()}");
                        return;
                    }
                    foreach (var appl in appls)
                    {
                        PersonalCloud pc = null;
                        lock (_PersonalClouds)
                        {
                            pc = _PersonalClouds.FirstOrDefault(x => x.Id == pcc.Item1);
                        }
                        if (pc != null)
                        {
                            appl.NodeId = NodeId;
                            pc.AddApp(appl);
                        }
                    }
                }
            }
        }


        #endregion



        private void EnsureWebServerStarted()
        {
            if (WebServer.State != WebServerState.Listening)
            {
                InitWebServer();
            } 
            else
            {
                using var cts = new CancellationTokenSource(500);
                try
                {
                    using var resp = _LocalNodes.Httpclient.GetAsync($"http://localhost:{ServerPort}/", HttpCompletionOption.ResponseHeadersRead, cts.Token).Result;
                }
                catch
                {
                    InitWebServer();
                    _LocalNodes.LocalNetworkMayChanged(true);
                }
            }
        }

        private void EnsureLocalNetStarted()
        {
            if (_LocalNodes.State != NSPersonalCloud.LocalDiscovery.NodeDiscoveryState.Listening)
            {
                _LocalNodes.Start(ServerPort, NodeId);
            }else
            {
                _LocalNodes.LocalNetworkMayChanged(false);
            }
        }
        #region Local cloud operations

        public PersonalCloud CreatePersonalCloud(string displayName, string nodedisplaryname)
        {
            var l = loggerFactory.CreateLogger("PersonalCloud");
            var pc = new PersonalCloud(l, this, null) {
                Id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                UpdateTimeStamp = DateTime.UtcNow.ToFileTime(),
                NodeDisplayName = nodedisplaryname,
                DisplayName = displayName,
            };
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                pc.MasterKey = aes.Key;
            }
            lock (_PersonalClouds)
            {
                _PersonalClouds.Add(pc);
            }
            SavePCList();

            EnsureWebServerStarted();
            EnsureLocalNetStarted();
            _LocalNodes.BroadcastingIveChanged();

            return pc;
        }
        public string SharePersonalCloud(PersonalCloud pc)
        {
            if (pc == null)
            {
                throw new InvalidDataException("pc couldn't be null");
            }
            var str = pc.GenerateShareCode();
            _LocalNodes.BroadcastingIveChanged();
            return str;
        }

        public void StopSharePersonalCloud(PersonalCloud pc)
        {
            if (pc == null)
            {
                throw new InvalidDataException("pc couldn't be null");
            }
            pc.CurrentShareCode = null;
            _LocalNodes.BroadcastingIveChanged();
            return;
        }

        public async Task<PersonalCloud> JoinPersonalCloud(int code, string nodedisplayname)
        {
            byte[] data = Encoding.UTF8.GetBytes(code.ToString(CultureInfo.InvariantCulture));
            ulong hcode = xxHash64.ComputeHash(data, data.Length);
            LocalDiscovery.NodeShareInfo spc = null;
            lock (_LocalNodes.sharedPCs)
            {
                spc =_LocalNodes.sharedPCs.FirstOrDefault(x => x.Hash == hcode);
            }

            if (spc != null)
            {
                var ts = DateTime.UtcNow.ToFileTime();
                data = BitConverter.GetBytes(ts + code);
                var newhcode = xxHash64.ComputeHash(data, data.Length);
                var pcresp = await _LocalNodes.GetNodeWebResp(new Uri(new Uri(spc.Url), $"clouds/{spc.PCId}?ts={ts}&hash={newhcode}")).ConfigureAwait(false);
                if (pcresp.IsSuccessStatusCode)
                {
                    var str = await pcresp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    //todo: add response check(hash check).
                    var pci = JsonConvert.DeserializeObject<PersonalCloudInfo>(str);
                    if ((pci == null) || (pci.DisplayName == null))
                    {
                        throw new InviteNotAcceptedException();
                    }
                    var l = loggerFactory.CreateLogger("PersonalCloud");
                    var pc = pci.ToPersonalCloud(l, this);
                    pc.NodeDisplayName = nodedisplayname;
                    pc.OnNodeAdded(spc, pci.NodeDisplayName, pci.TimeStamp);
                    lock (_PersonalClouds)
                    {
                        _PersonalClouds.Add(pc);
                    }
                    SavePCList();
                    _LocalNodes.BroadcastingIveChanged();
                    _LocalNodes.SyncPCNodes(pc.Id);
                    return pc;
                }
                throw new InvalidDeviceResponseException();
            }
            throw new NoDeviceResponseException();
        }

        public void ExitFromCloud(PersonalCloud pc)
        {
            lock (_PersonalClouds)
            {
                _PersonalClouds.Remove(pc);
            }
            SavePCList();
            _LocalNodes.BroadcastingIveChanged();
        }

        #endregion

        public void StartService()
        {
            logger.LogInformation("StartService");
            EnsureWebServerStarted();
            EnsureLocalNetStarted();

            LoadApps();
        }

        public void NetworkMayChanged(bool besure)
        {
            EnsureWebServerStarted();
            _LocalNodes.LocalNetworkMayChanged(besure);
        }

        public void BroadcastingIveChanged()
        {
            _LocalNodes.BroadcastingIveChanged();
        }




        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                WebServer?.Dispose();
                WebServer = null;
                _LocalNodes?.Dispose();
                _LocalNodes = null;
                _FileSystem?.Dispose();
                _FileSystem = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region  for debug only

#if DEBUG
        public void TestSetReannounceTime(int time)
        {
            _LocalNodes.RepublicTime = time;
        }
        public void TestStopWebServer()
        {
            WebServer.Dispose();
        }

        //Seting multicast ports.for internal use only
        public void TestSetUdpPort(int bport, int[] tport)
        {
            _LocalNodes.TargetPort = tport;
            _LocalNodes.BindPort = bport;
        }
#endif//DEBUG
        #endregion
    }
}
