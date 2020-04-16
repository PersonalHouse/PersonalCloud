using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
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
using NSPersonalCloud.Interfaces.Errors;

using Standart.Hash.xxHash;

namespace NSPersonalCloud
{

    class NodeShareInfo
    {
        public string NodeId;
        public string Url;
        public string PCVersion;

        public ulong Hash;
        public string PCId;
    }
    class FetchQueueItem
    {
        public NodeInfo Node;
        public bool IsFetching;
    }


    public class PCLocalService : IDisposable, IPCService
    {
        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;

        private IConfigStorage ConfigStorage { get; }
        private VirtualFileSystem FileSystem { get; }

        NodeDiscovery nodeDiscovery;
        HttpClient httpclient;
        readonly List<NodeShareInfo> sharedPCs;
        readonly Dictionary<string, NodeInfo> KnownNodes;

        readonly ActionBlock<NodeInfo> fetchCloudInfo;
        readonly List<FetchQueueItem> fetchQueue;

        public int ServerPort { get; private set; }
        public string NodeGuid { get; private set; }
        public IReadOnlyList<PersonalCloud> PersonalClouds => _PersonalClouds.AsReadOnly();

        public event EventHandler<ServiceErrorEventArgs> OnError;

        private List<PersonalCloud> _PersonalClouds;
        WebServer WebServer;

        private SSDPServiceController CreateSSDPServiceController() => new SSDPServiceController(this);
        public ShareController CreateShareController() => new ShareController(FileSystem,this);

        public PCLocalService(IConfigStorage configStorage, ILoggerFactory logfac, VirtualFileSystem fileSystem)
        {
            ConfigStorage = configStorage;
            FileSystem = fileSystem;
            loggerFactory = logfac;
#pragma warning disable CA1062 // Validate arguments of public methods
            logger = loggerFactory.CreateLogger("PCLocalService");
#pragma warning restore CA1062 // Validate arguments of public methods

            KnownNodes = new Dictionary<string, NodeInfo>();
            sharedPCs = new List<NodeShareInfo>();
            httpclient = new HttpClient();
            httpclient.Timeout = TimeSpan.FromSeconds(10);
            nodeDiscovery = new NodeDiscovery(logfac);
            nodeDiscovery.OnNodeAdded += NodeDiscovery_OnNodeAdded;
            nodeDiscovery.OnError += (o, e) => OnError?.Invoke(this, new ServiceErrorEventArgs(e));
            fetchQueue = new List<FetchQueueItem>();

            var cfg = LoadConfiguration();
            ServerPort = cfg.Port;
#pragma warning disable CA1305 // Specify IFormatProvider
            NodeGuid = cfg.Id.ToString("N");
#pragma warning restore CA1305 // Specify IFormatProvider

            LoadPCList();
            foreach (var item in _PersonalClouds)
            {
                item.ResyncClientList();
            }
            fetchCloudInfo = new ActionBlock<NodeInfo>(GetNodeClodeInfo, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 3 });
            InitWebServer();


        }

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        async Task EmbedIOResponseSerializerCallback(IHttpContext context, object? data)
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        {
            context.Response.ContentType = MimeType.Json;
            using var text = context.OpenResponseText(new UTF8Encoding(false));
            await text.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions() { IgnoreNullValues = true })).ConfigureAwait(false);
        }

        void InitWebServer()
        {
            if (WebServer!=null)
            {
                WebServer.Dispose();
                WebServer = null;
            }
            var curpath = Path.GetDirectoryName(typeof(PCLocalService).Assembly.Location);
            WebServer = new WebServer(ServerPort);
            WebServer = WebServer
                .WithModule(new PCWebServerAuth("/api/share", this))
                .WithWebApi("SSDP", "/clouds", module => module.WithController(CreateSSDPServiceController))
                .WithWebApi("Share", "/api/share", module => module.WithController(CreateShareController))
                .WithWebApi("Album", "/api/Apps/album", EmbedIOResponseSerializerCallback, module => module.WithController(typeof(Plugins.Album.AlbumWebController)))
                // .WithStaticFolder("/AppsStatic",Path.Combine(curpath, "Apps","Static"),false)
                ;
            WebServer.Start();

        }
        async Task GetNodeClodeInfo(NodeInfo _)
        {
            NodeInfo node = null;
            FetchQueueItem curnodeinfo = null;
            try
            {
                lock (fetchQueue)
                {
                    curnodeinfo = fetchQueue.FirstOrDefault(x => x.IsFetching != true);
                    if (curnodeinfo == null)
                    {
                        return;
                    }
                    node = curnodeinfo.Node;
                    curnodeinfo.IsFetching = true;
                }

                lock (KnownNodes)
                {
                    if (KnownNodes.TryGetValue(node.NodeGuid, out var lnode))
                    {
                        if (lnode.TimeStamp >= node.TimeStamp)
                        {
                            lnode.LastSeenTime = DateTime.UtcNow.ToFileTime();
                            return;
                        }
                    }
                }
                var turi = new Uri(new Uri(node.Url), "clouds");
                var req = await httpclient.GetAsync(turi).ConfigureAwait(false);
                if (req.IsSuccessStatusCode)
                {
                    logger.LogTrace($"Discovered {turi}");
                    await OnNewNodeDiscovered(node, req).ConfigureAwait(false);
                    lock (KnownNodes)
                    {
                        if (KnownNodes.ContainsKey(node.NodeGuid))
                        {
                            KnownNodes[node.NodeGuid] = node;
                        }
                        else
                        {
                            KnownNodes.Add(node.NodeGuid, node);
                        }
                    }
                }
                else
                {

                }
            }
            catch (Exception exception)
            {
                if (exception.HResult != -2147467259)
                {
                    logger.LogError(exception, $"Error getting info for node: {node.Url}");
                }
            }
            finally
            {
                if (curnodeinfo!=null)
                {
                    lock (fetchQueue)
                    {
                        fetchQueue.Remove(curnodeinfo);
                    }
                }
            }
        }

        private async Task OnNewNodeDiscovered(NodeInfo node, HttpResponseMessage req)
        {
            var res = JsonConvert.DeserializeObject<List<SSDPPCInfo>>(await req.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (res == null)
            {
                res = new List<SSDPPCInfo>();
            }
            var pcs = PersonalClouds;
            foreach (var pc in pcs)
            {
                pc.OnNodeUpdate(node, res);
                //logger.LogTrace($"{ServerPort}:OnNodeUpdate {pc.NodeDisplayName}");
            }
            if (res?.Count > 0)
            {
                foreach (var item in res)
                {
                    var pc = pcs.Where(x => x.Id == item.Id).FirstOrDefault();
                    if ((pc == null) && (!string.IsNullOrWhiteSpace(item.CodeHash)))
                    {
                        var hash = ulong.Parse(item.CodeHash, CultureInfo.InvariantCulture);
                        var snode = new NodeShareInfo {
                            Hash = hash,
                            NodeId = node.NodeGuid,
                            Url = node.Url,
                            PCId = item.Id,
                            PCVersion = node.PCVersion,
                        };
                        AddShareNode(snode);
                        logger.LogTrace($"{ServerPort}:A PC is sharing");
                    }
                    else
                    {
                        RemoveSharedNode(node, item.Id);
                    }
                }
            }
            else
            {
                RemoveSharedNode(node);
            }
        }

        private void AddShareNode(NodeShareInfo snode)
        {
            lock (sharedPCs)
            {
                if (sharedPCs.FirstOrDefault(x => ((x.NodeId == snode.NodeId) && (x.PCId == snode.PCId))) != null)
                {//already in sharedPCs
                    return;
                }
                else
                {
                    sharedPCs.Add(snode);
                }
            }
        }

        private void RemoveSharedNode(NodeInfo node, string id)
        {
            lock (sharedPCs)
            {
                sharedPCs.RemoveAll(x => ((x.NodeId == node.NodeGuid)&& (x.PCId==id)));
            }
        }

        private void RemoveSharedNode(NodeInfo node)
        {
            lock (sharedPCs)
            {
                sharedPCs.RemoveAll(x => x.NodeId == node.NodeGuid);
            }
        }

        private void NodeDiscovery_OnNodeAdded(NodeInfo nodeinfo)
        {
            try
            {
                nodeinfo.LastSeenTime = DateTime.UtcNow.ToFileTime();
                //logger.LogDebug($"NodeDiscovery_OnNodeAdded {ServerPort}: {nodeinfo.Url} {nodeinfo.TimeStamp}");
                lock (fetchQueue)
                {
                    if (fetchQueue.FirstOrDefault(x =>
                    (x.Node.NodeGuid == nodeinfo.NodeGuid) && (x.Node.Url == nodeinfo.Url) && (x.Node.TimeStamp >= nodeinfo.TimeStamp)) != null)
                    {
                        return;
                    }
                    else
                    {
                        fetchQueue.Add(new FetchQueueItem { Node = nodeinfo, IsFetching = false });
                    }
                }
                fetchCloudInfo.Post(null);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error adding node from discovery.");
            }
        }
        public void CleanExpiredNodes()
        {
            Task.Run(() => {
                var cur = DateTime.UtcNow.ToFileTime();

                List<KeyValuePair<string, NodeInfo>> lis = null;
                lock (KnownNodes)
                {
                    lis = KnownNodes.Where(x => {
                        if ((cur - x.Value.LastSeenTime) > (nodeDiscovery.RepublicTime * 10000L * 15 / 10))//1.5 *RepublicTime
                        {
                            return true;
                        }
                        return false;
                    }).ToList();
                    foreach (var item in lis)
                    {
                        KnownNodes.Remove(item.Key);
                    }
                }
                var ssdp = new List<SSDPPCInfo>();
                foreach (var pc in PersonalClouds)
                {
                    foreach (var item in lis)
                    {
                        pc.OnNodeUpdate(item.Value, ssdp);
                    }
                }
            });
        }

        public void CleanKnownNodes()
        {
            List<KeyValuePair<string, NodeInfo>> lis = null;
            lock (KnownNodes)
            {
                lis = KnownNodes.ToList();
                foreach (var item in lis)
                {
                    KnownNodes.Remove(item.Key);
                }
            }
            var ssdp = new List<SSDPPCInfo>();
            foreach (var pc in PersonalClouds)
            {
                foreach (var item in lis)
                {
                    pc.OnNodeUpdate(item.Value, ssdp);
                }
            }
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

                cfg.Port = ran.Next(20000, 30000);
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
            ConfigStorage.SaveCloud(_PersonalClouds.Select(x => PersonalCloudInfo.FromPersonalCloud(x)));
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
            _PersonalClouds = saved.Select(x => x.ToPersonalCloud(l,this)).ToList();
        }

        public List<StorageProviderInstance> GetStorageProviderInstances(string cloudId)
        {
            var personalCloud = _PersonalClouds.Where(x => x.Id == cloudId).FirstOrDefault();

            if (personalCloud != null)
            {
                return personalCloud.StorageProviderInstances;
            }
            else
            {
                throw new NoSuchCloudException();
            }
        }

        public bool AddStorageProvider(string cloudId, string nodeName, OssConfig ossConfig, StorageProviderVisibility visibility, bool saveChanges = true)
        {
            var personalCloud = _PersonalClouds.FirstOrDefault(x => x.Id == cloudId);

            if (personalCloud != null)
            {
                bool haveChanges = personalCloud.AddStorageProvider(nodeName, ossConfig, visibility);
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
            var personalCloud = _PersonalClouds.FirstOrDefault(x => x.Id == cloudId);

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

        public async Task<PersonalCloud> CreatePersonalCloud(string displayName, string nodedisplaryname)
        {
            var l = loggerFactory.CreateLogger("PersonalCloud");
            var pc = new PersonalCloud(l,this, null) {
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
            await EnsureSSDPStarted().ConfigureAwait(false);
            await nodeDiscovery.RePublish(NodeGuid, ServerPort).ConfigureAwait(false);

            return pc;


        }


        private void EnsureWebServerStarted()
        {
            if (WebServer.State != WebServerState.Listening)
            {
                WebServer.Start();
            }
        }

        private async Task EnsureSSDPStarted()
        {
            if (nodeDiscovery.State != NodeDiscoveryState.Listening)
            {
                nodeDiscovery.StartMonitoring();
                int n = 0;
                lock (_PersonalClouds)
                {
                    n = _PersonalClouds.Count;
                }
                if (n > 0)
                {
                    await nodeDiscovery.RePublish(NodeGuid, ServerPort).ConfigureAwait(false);
                }
            }
        }

        public async Task<string> SharePersonalCloud(PersonalCloud pc)
        {
            if (pc == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new InvalidDataException("pc couldn't be null");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            var str = pc.GenerateShareCode();
            await nodeDiscovery.RePublish(NodeGuid, ServerPort).ConfigureAwait(false);
            return str;
        }

        public async Task StopSharePersonalCloud(PersonalCloud pc)
        {
            if (pc == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new InvalidDataException("pc couldn't be null");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            pc.CurrentShareCode = null;
            await nodeDiscovery.RePublish(NodeGuid, ServerPort).ConfigureAwait(false);
            return;
        }

        public async Task<PersonalCloud> JoinPersonalCloud(int code, string nodedisplayname)
        {
            byte[] data = Encoding.UTF8.GetBytes(code.ToString(CultureInfo.InvariantCulture));
            ulong hcode = xxHash64.ComputeHash(data, data.Length);
            NodeShareInfo spc = null;
            lock (sharedPCs)
            {
                spc = sharedPCs.FirstOrDefault(x => x.Hash == hcode);
            }

            if (spc!=null)
            {
                var ts = DateTime.UtcNow.ToFileTime();
                data = BitConverter.GetBytes(ts + code);
                var newhcode = xxHash64.ComputeHash(data, data.Length);
                var pcresp = await httpclient.GetAsync(new Uri(new Uri(spc.Url), $"clouds/{spc.PCId}?ts={ts}&hash={newhcode}")).ConfigureAwait(false);
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
                    var pc = pci.ToPersonalCloud(l,this);
                    pc.NodeDisplayName = nodedisplayname;
                    pc.OnNodeAdded(spc,pci.NodeDisplayName,pci.TimeStamp);
                    lock (_PersonalClouds)
                    {
                        _PersonalClouds.Add(pc);
                    }
                    SavePCList();
                    lock (KnownNodes)
                    {
                        KnownNodes.Clear();
                    }                    
                    await nodeDiscovery.RePublish(NodeGuid, ServerPort).ConfigureAwait(false);
                    nodeDiscovery.StartSearch();
                    logger.LogDebug($"Join {pc.DisplayName}");
                    return pc;
                }
                throw new InvalidDeviceResponseException();
            }
            throw new NoDeviceResponseException();
        }

        public void StartService()
        {
            logger.LogInformation("StartService");
            EnsureWebServerStarted();
            _ = EnsureSSDPStarted();
        }


        //call StartNetwork after network changed
        //forcerestart will make current http request failed.
        public void StartNetwork(bool forcerestart)
        {
            if (forcerestart || (WebServer==null)|| (WebServer.State != WebServerState.Listening)|| (nodeDiscovery.State != NodeDiscoveryState.Listening))
            {
                InitWebServer();
                nodeDiscovery.StopNetwork();
                CleanKnownNodes();
            }
            else
            {
                using var cts = new CancellationTokenSource(500);
                try
                {
                    using var resp = httpclient.GetAsync($"http://127.0.0.1:{ServerPort}/", HttpCompletionOption.ResponseHeadersRead, cts.Token).Result;
                }
                catch
                {
                    InitWebServer();
                    nodeDiscovery.StopNetwork();
                    CleanKnownNodes();
                }
            }

            nodeDiscovery.StartMonitoring();
            _ = nodeDiscovery.RePublish(NodeGuid, ServerPort);
            nodeDiscovery.StartSearch();
        }

        //republish cloud info to network
        public void NetworkRefeshNodes()
        {
            //nodeDiscovery.ForceNetworkRefesh();
            _ = nodeDiscovery.RePublish(NodeGuid, ServerPort);
            nodeDiscovery.StartSearch();
        }

        public void StopNetwork()
        {
            nodeDiscovery.StopNetwork();
            WebServer?.Listener?.Stop();
            WebServer?.Dispose();
            WebServer = null;
            CleanKnownNodes();
        }


        public void ExitFromCloud(PersonalCloud pc)
        {
            lock (_PersonalClouds)
            {
                _PersonalClouds.Remove(pc);
            }
            SavePCList();
            _ = nodeDiscovery.RePublish(NodeGuid, ServerPort);
        }

        //Seting multicast ports.for internal use only
        public void SetUdpPort(int bport,int[] tport)
        {
            nodeDiscovery.TargetPort = tport;
            nodeDiscovery.BindPort = bport;
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                WebServer?.Dispose();
                WebServer = null;
                nodeDiscovery?.Dispose();
                nodeDiscovery = null;
                httpclient?.Dispose();
                httpclient = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

#if DEBUG
        public void TestSetReannounceTime(int time)
        {
            nodeDiscovery.RepublicTime = time;
        }
        public void TestStopWebServer()
        {
            WebServer.Dispose();
        }
#endif//DEBUG
    }
}
