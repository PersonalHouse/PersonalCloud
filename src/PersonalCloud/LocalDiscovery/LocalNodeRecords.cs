using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using System.Threading.Tasks.Dataflow;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Net.Http;
using System.Reactive.Concurrency;
using NSPersonalCloud.Interfaces.Errors;

namespace NSPersonalCloud.LocalDiscovery
{

    public enum NodeDiscoveryState
    {
        Listening,
        Created,
    }

    class FetchQueueItem
    {
        public NodeInfoInNet Node;
        public LocalNodeInfo LocalNode;
        public TaskCompletionSource<int> FinishTask;
    }

    public class LocalNodeUpdateEventArgs 
    {
        internal LocalNodeInfo nodeInfo;
        internal List<SSDPPCInfo> PCinfos;
    }


    public class LocalNodeRecords : IDisposable
    {
        public int BindPort;
        public int[] TargetPort;
        public int RepublicTime = 2 * 60 * 1000;

        public NodeDiscoveryState State { get; internal set; }
        public List<LocalNodeInfo> LocalNodes;
        public readonly List<NodeShareInfo> sharedPCs;

        public event EventHandler<ErrorCode> OnError;
        public event EventHandler<LocalNodeUpdateEventArgs> OnNodeAdded;//node guid,url

        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;
        System.Threading.Timer _BroadcastingTimer;

        LocalNodesNetwork _Network;


        int WebServerPort;
        string ThisNodeID;


        ActionBlock<FetchQueueItem> fetchCloudInfo;
        HttpClient httpclient;
        Subject<NodeInfoInNet> ReceivedNodeInfosSubject;
        Subject<Unit> rxduration;
        private int durationCount;


        public LocalNodeRecords(ILoggerFactory logfac)
        {
            LocalNodes = new List<LocalNodeInfo>();
            sharedPCs = new List<NodeShareInfo>();

            BindPort = Definition.MulticastPort;
            TargetPort = new[] { Definition.MulticastPort };
            loggerFactory = logfac;
            logger = loggerFactory.CreateLogger<LocalNodeRecords>();
            _BroadcastingTimer = new System.Threading.Timer(OnBroadcastingTimer, null, Timeout.Infinite, Timeout.Infinite);
            durationCount = 0;

            _Network = new LocalNodesNetwork(loggerFactory);
            _Network.OnReceiveNodeInfo = OnReceiveNodeInfo;
            _Network.OnError = OnNetlayerError;

            State = NodeDiscoveryState.Created;

            SetupNodeInfoQueue();
        }




        #region fetch cloud info

        private void SetupNodeInfoQueue()
        {
            httpclient = new HttpClient();
            httpclient.Timeout = TimeSpan.FromSeconds(15);
            fetchCloudInfo = new ActionBlock<FetchQueueItem>(GetNodeClodeInfo, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 3 });
            rxduration = new Subject<Unit>();
            ReceivedNodeInfosSubject = new Subject<NodeInfoInNet>();

            ReceivedNodeInfosSubject.DistinctUntilChanged().GroupByUntil(x => x.NodeId, x => rxduration.AsObservable()).Subscribe(
                g => {
                    g.Distinct().ObserveOn(TaskPoolScheduler.Default).Subscribe(
                        NodeInfoForOneNode,
                        ex => {
                            logger.LogError("ReceivedNodeInfosSubjectGroupBy.Subscribe OnError {0}", ex.Message);
                        },
                        () => {
                            lock (LocalNodes)
                            {
                                var nod = LocalNodes.FirstOrDefault(y => y.NodeId == g.Key);
                                if (nod != null)
                                {
                                    Interlocked.Exchange(ref nod.MissCount, 0);
                                }
                            }
                        }
                        );
                },
                ex => {
                    logger.LogError("ReceivedNodeInfosSubjectGroupBy OnError {0}", ex.Message);
                },
                () => {
                    //logger.LogInformation("ReceivedNodeInfosSubjectGroupBy OnError {0}", ex.Message);
                });
        }

        private void NodeInfoForOneNode(NodeInfoInNet x)
        {
            if (x.NodeId == ThisNodeID)
            {
                return;
            }
            LocalNodeInfo nod = null;
            lock (LocalNodes)
            {
                nod = LocalNodes.FirstOrDefault(y => y.NodeId == x.NodeId);
                if (nod == null)
                {
                    nod = new LocalNodeInfo {
                        NodeId = x.NodeId,
                        Fetched = false,
                        PCVersion = x.PCVersion,
                        StatusTimeStamp = x.TimeStamp,
                    };
                    LocalNodes.Add(nod);
                }
                else
                {
                    if (nod.StatusTimeStamp > x.TimeStamp)
                    {
                        return;
                    }
                    if (nod.StatusTimeStamp == x.TimeStamp)
                    {
                        if (nod.Fetched)
                        {
                            return;
                        }
                    }
                    else
                    {
                        nod.Fetched = false;
                        nod.StatusTimeStamp = x.TimeStamp;
                    }
                    //do not set LocalNodeInfo.Url in this block
                }
            }

            var t = new FetchQueueItem {
                Node = x,
                LocalNode = nod,
                FinishTask = new TaskCompletionSource<int>()
            };
            fetchCloudInfo.Post(t);
            t.FinishTask.Task.Wait();
        }

        private async Task<HttpResponseMessage> GetNodeWebResp(Uri turi)
        {
            const int retrycnt = 2;//Too many retries may block the queue.
            for (int i = 0; i < retrycnt; i++)
            {
                try
                {
                    return await httpclient.GetAsync(turi,HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if (i < (retrycnt - 1))
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }

            }
            return null;
        }


        async Task GetNodeClodeInfo(FetchQueueItem qinfo)
        {
            var node = qinfo.Node;
            try
            {
                var turi = new Uri(new Uri(node.Url), "clouds");
                var resp = await GetNodeWebResp(turi).ConfigureAwait(false);
                if ((resp != null) && resp.IsSuccessStatusCode)
                {
                    logger.LogTrace($"Discovered {turi}");
                    qinfo.LocalNode.Url = qinfo.Node.Url;
                    qinfo.LocalNode.Fetched = true;

                    await OnNewNodeDiscovered(qinfo.LocalNode, resp).ConfigureAwait(false);

                    qinfo.FinishTask.SetResult(0);
                }
                else
                {//do nothing

                }
            }
            catch (Exception exception)
            {
                if (exception.HResult != -2147467259)
                {
                    logger.LogError(exception, $"Error getting info for node: {node.Url}");
                }
            }
        }


        private async Task OnNewNodeDiscovered(LocalNodeInfo node, HttpResponseMessage req)
        {
            var res = JsonConvert.DeserializeObject<List<SSDPPCInfo>>(await req.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (res == null)
            {
                res = new List<SSDPPCInfo>();
            }
            try
            {
                var info = new LocalNodeUpdateEventArgs() { nodeInfo = node, PCinfos = res };
                OnNodeAdded?.Invoke(this, info);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Error getting info for node: {node.Url}");
            }

            SyncSharePCs(node, res);

        }

        private void SyncSharePCs(LocalNodeInfo node, List<SSDPPCInfo> res)
        {
            //remove
            lock (sharedPCs)
            {
                sharedPCs.RemoveAll(x =>
                    (x.NodeId == node.NodeId) &&
                    (
                        res.FirstOrDefault(y =>
                        (y.Id == x.PCId) &&
                        (!string.IsNullOrWhiteSpace(y.CodeHash))) == null
                    )
                );
            }

            //add
            lock (sharedPCs)
            {
                foreach (var item in res)
                {
                    if (!string.IsNullOrWhiteSpace(item.CodeHash))
                    {
                        var prev = sharedPCs.FirstOrDefault(x => ((x.NodeId == node.NodeId) && (x.PCId == item.Id)));
                        if (prev == null)
                        {
                            var hash = ulong.Parse(item.CodeHash, CultureInfo.InvariantCulture);
                            var snode = new NodeShareInfo {
                                Hash = hash,
                                NodeId = node.NodeId,
                                Url = node.Url,
                                PCId = item.Id,
                                PCVersion = node.PCVersion,
                            };
                            sharedPCs.Add(snode);
                        }
                        logger.LogTrace($"{node.Url}:A PC is sharing");
                    }
                }
            }
        }

        private void CleanExpiredLocalNodes()
        {
            lock (LocalNodes)
            {
                for (int i = 0; i < LocalNodes.Count; )
                {
                    var node = LocalNodes[i];
                    if (node.MissCount >= 15)//to compatible with old version. Could change to 5 after 2021.12.31
                    {
                        LocalNodes.RemoveAt(i);//todo: ping the node and then remove it
                        continue;
                    }
                    Interlocked.Increment(ref node.MissCount);
                    i++;
                }
            }
            if (durationCount > 3)
            {
                rxduration.OnNext(Unit.Default);
                durationCount = 0;
            }
            ++durationCount;
        }


        #endregion

        #region events

        private void OnBroadcastingTimer(object state)
        {
            logger.LogInformation("OnBroadcastingTimer");
            try
            {
                CleanExpiredLocalNodes();
                _Network.EnsureListenSocketFine();
                _Network.SendAnnounce(false);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "OnBroadcastingTimer callback finished with error.");
            }
        }

        private void OnNetlayerError(ErrorCode obj)
        {
            OnError?.Invoke(this, obj);
        }


        private void OnReceiveNodeInfo(string nodeinfostr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nodeinfostr))
                {
                    logger.LogError("LocalNodeRecords:Receive null string.");
                    return;
                }
                var nodinfo = JsonConvert.DeserializeObject<NodeInfoInNet>(nodeinfostr);
                if (string.IsNullOrWhiteSpace(nodinfo.NodeId))
                {
                    logger.LogError("LocalNodeRecords:Receive malformed nodeinfo.");
                    return;
                }
                ReceivedNodeInfosSubject.OnNext(nodinfo);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception in LocalNodeRecords.");
            }
        }

        #endregion



        #region public methods


        public void Start(int webport, string thisnodeid)
        {
            try
            {
                if (State == NodeDiscoveryState.Listening)
                {
                    _Network.EnsureListenSocketFine();
                    return;
                }

                WebServerPort = webport;
                ThisNodeID = thisnodeid;

                _Network.Start(BindPort, TargetPort, WebServerPort, ThisNodeID);
                _BroadcastingTimer.Change(0, RepublicTime);
                _Network.SendSearch(TargetPort);

                State = NodeDiscoveryState.Listening;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception in LocalNodeRecords:Start");
                throw;
            }
        }

        public void SendCloudUpdateEvent()
        {
            _Network.SendAnnounce(true);
        }

        internal void LocalNetworkMayChanged(bool besure)
        {
            if(besure)
            {

            }else
            {
                _Network.EnsureListenSocketFine();
            }
        }



        #endregion


        #region List local ips

        private static List<Tuple<IPAddress, int>> GetLocalIPAddress()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var ips = new List<Tuple<IPAddress, int>>();
            foreach (var networkInterface in networkInterfaces)
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (!networkInterface.SupportsMulticast)
                    continue;
                if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    continue;

                AddUnicastAddress(networkInterface, ips);
            }
            return ips;
        }


        private static void AddUnicastAddress(NetworkInterface networkInterface, List<Tuple<IPAddress, int>> ips)
        {
            var interfaceProperties = networkInterface.GetIPProperties();
            var unicastAddresses = interfaceProperties.UnicastAddresses;

            int index = 0;
            try
            {
                var props = interfaceProperties.GetIPv4Properties();
                index = props.Index;
            }
            catch
            {
                try
                {
                    var props = interfaceProperties.GetIPv6Properties();
                    index = props.Index;
                }
                catch { }
            }

            foreach (var ipAddressInfo in unicastAddresses)
            {
                var ip = ipAddressInfo.Address;
                try
                {
                    if (ipAddressInfo.SuffixOrigin == System.Net.NetworkInformation.SuffixOrigin.Random)
                    {
                        //continue;
                    }
                    if (ipAddressInfo.SuffixOrigin == System.Net.NetworkInformation.SuffixOrigin.WellKnown)
                    {
                        continue;
                    }
//                     if (ipAddressInfo.PrefixOrigin == PrefixOrigin.Other )
//                     {
//                         continue;
//                     }
                }
                catch (NotImplementedException)
                {
                    try
                    {
                        if (IPAddress.IsLoopback(ip))
                        {
                            continue;
                        }
                    }
                    catch
                    {
                    }
                }
                if (((ip.AddressFamily == AddressFamily.InterNetwork) || (ip.AddressFamily == AddressFamily.InterNetworkV6)))
                {
                    ips.Add(Tuple.Create(ip, index));
                    continue;
                }
            }

        }
        #endregion

        #region disposable

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _Network?.Dispose();
                    _Network = null;

                    _BroadcastingTimer?.Dispose();
                    _BroadcastingTimer = null;
                    ReceivedNodeInfosSubject?.Dispose();
                    ReceivedNodeInfosSubject = null;
                    rxduration?.Dispose();
                    rxduration = null;
                    httpclient?.Dispose();
                    httpclient = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~LocalNodeRecords()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        #endregion
    }
}
