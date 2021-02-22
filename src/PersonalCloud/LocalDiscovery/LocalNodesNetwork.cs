using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace NSPersonalCloud.LocalDiscovery
{

    static class NetworkPacketOperations
    {
        public const string Search = "Search";
        public const string Announce = "Announce";
        public const string Response = "Response";
        public const string Bye = "Bye";
    }

    class LocalNodesNetwork : IDisposable
    {
        private readonly ILogger logger;
        private bool disposedValue;
        int _BindPort;
        int _WebServerPort;
        int[] _TargetPort;
        string _ThisNodeID;
        long _StatusTimeStamp;
        ImmutableList<Tuple<IPAddress, int, Socket>> _ListenSockets;

        readonly LocalNodesSocket _SockertLayer;
        readonly Dictionary<BigInteger, long> searchCache;
        readonly Dictionary<BigInteger, long> responseCache;

        readonly byte[] header;
        public static int UdpSendCount { get => 3; }
        public static TimeSpan UdpSendDelay { get => TimeSpan.FromMilliseconds(100); }


        public Action<string> OnReceiveNodeInfo { get; internal set; }
        public Action<Interfaces.Errors.ErrorCode> OnError { get; internal set; }


        public LocalNodesNetwork(ILoggerFactory lf)
        {

            logger = lf.CreateLogger<LocalNodesNetwork>();
            _SockertLayer = new LocalNodesSocket(lf.CreateLogger<LocalNodesSocket>());
            header = new byte[] { 44, 59, 48, (byte) Definition.CloudVersion, 0, 0, 0, };
            searchCache = new Dictionary<BigInteger, long>();
            responseCache = new Dictionary<BigInteger, long>();


            _StatusTimeStamp = DateTime.UtcNow.ToFileTime();
        }

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
            ips.Add(Tuple.Create(IPAddress.Any, 0));
            ips.Add(Tuple.Create(IPAddress.IPv6Any, 0));
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

        #region local network commands

        static byte[] Combine(byte[] array1, byte[] array2)
        {
            byte[] rv = new byte[array1.Length + array2.Length];
            System.Buffer.BlockCopy(array1, 0, rv, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, rv, array1.Length, array2.Length);
            return rv;
        }
        void SendMessage(byte[] messageData, int port)
        {
            var msg = Combine(header, messageData);

            var solis = _ListenSockets;
            foreach (var soinf in solis)
            {
                IPEndPoint endp;
                if (soinf.Item1.AddressFamily == AddressFamily.InterNetwork)
                {
                    endp = new IPEndPoint(IPAddress.Parse("239.255.255.250"), port);
                }
                else
                {
                    endp = new IPEndPoint(IPAddress.Parse("FF02::C"), port);
                }
                _ = _SockertLayer.SendTo(soinf.Item3, endp, msg, 0, msg.Length, UdpSendCount, (int) UdpSendDelay.TotalMilliseconds);
            }
        }
        void SendMessage(Tuple<IPAddress, int, Socket> tuple, byte[] messageData, int port)
        {
            var msg = Combine(header, messageData);

            //var sso = _SockertLayer.CreateClientSocket(tuple.Item1, tuple.Item2);
            IPEndPoint endp;
            if (tuple.Item1.AddressFamily == AddressFamily.InterNetwork)
            {
                endp = new IPEndPoint(IPAddress.Parse("239.255.255.250"), port);
            }
            else
            {
                endp = new IPEndPoint(IPAddress.Parse("FF02::C"), port);
            }
            _ = _SockertLayer.SendTo(tuple.Item3, endp, msg, 0, msg.Length, UdpSendCount, (int) UdpSendDelay.TotalMilliseconds);
        }


        internal void SendAnnounce(bool forceupdateinfo)
        {
            if (forceupdateinfo)
            {
                ++_StatusTimeStamp;
            }
            SendAnnounceOrSearchResponse(NetworkPacketOperations.Announce);
        }



        void SendAnnounceOrSearchResponse(string Ops)
        {
            var lis = _ListenSockets;
            foreach(var ip in lis)
            {
                string url = null;
                if (ip.Item1.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    url = $"http://[{ip.Item1}]:{_WebServerPort}/";
                }
                else
                {
                    url = $"http://{ip.Item1}:{_WebServerPort}/";
                }

                var node = new NodeInfoInNet {
                    NodeId = _ThisNodeID,
                    PCVersion = Definition.CloudVersion.ToString(CultureInfo.InvariantCulture),
                    TimeStamp = _StatusTimeStamp,
                    Url = url,
                };
                var AnnounceString = JsonConvert.SerializeObject(node);

                using var ms = new MemoryStream();
                using var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);
                sw.WriteLine(Ops);
                sw.WriteLine(DateTime.UtcNow.ToFileTime().ToString(CultureInfo.InvariantCulture));
                sw.WriteLine(AnnounceString);
                sw.Flush();

                var arr = ms.ToArray();
                foreach (var port in _TargetPort)
                {
                    SendMessage(ip, arr, port);
                }
            }
        }

        internal void SendSearch(int[] targetPort)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true))
                {
                    sw.WriteLine(NetworkPacketOperations.Search);
                    sw.WriteLine(DateTime.UtcNow.ToFileTime().ToString(CultureInfo.InvariantCulture));
                    sw.Flush();
                    var arr = ms.ToArray();
                    foreach (var port in targetPort)
                    {
                        SendMessage(arr, port);
                    }
                }
            }
        }


        #endregion

        internal void Start(int bindPort, int[] targetPort, int webport, string thisnodeid)
        {
            _BindPort = bindPort;
            _WebServerPort = webport;
            _TargetPort = targetPort;
            _ThisNodeID = thisnodeid;

            InitListenSockets();
        }

        internal void EnsureListenSocketFine()
        {
            var newips = GetLocalIPAddress();
            bool changed = false;
            var nowlis = _ListenSockets;
            var newlis = new List<Tuple<IPAddress, int, Socket>>();
            try
            {
                foreach (var item in nowlis)
                {
                    var founditem = newips.FirstOrDefault(x => x.Item1.Equals(item.Item1) && (x.Item2 == item.Item2));
                    if (founditem == null)//not found
                    {
                        item.Item3.Dispose();
                        changed = true;
                    }
                    else
                    {
                        newlis.Add(item);
                    }
                }
                foreach (var item in newips)
                {
                    if (nowlis.FirstOrDefault(x => x.Item1.Equals(item.Item1) && (x.Item2 == item.Item2)) == null)//not found
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        var so = GetOneSocket(item);
#pragma warning restore CA2000 // Dispose objects before losing scope
                        if (so==null)
                        {
                            continue;
                        }
                        newlis.Add(Tuple.Create(item.Item1, item.Item2, so));
                        changed = true;
                    }
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception in EnsureListenSocketFine");
                foreach (var item in newlis)
                {
                    item.Item3?.Dispose();
                }
                throw;
            }
            if (changed)
            {
                _ListenSockets = newlis.ToImmutableList();
                SendSearch(_TargetPort);
                SendAnnounce(false);
            }
        }

        void CleanListenSockets()
        {
            if (_ListenSockets?.Count > 0)
            {
                var lis = _ListenSockets;
                foreach (var item in lis)
                {
                    item.Item3.Dispose();
                }
            }
            _ListenSockets = null;
        }
        void InitListenSockets()
        {
            var ips = GetLocalIPAddress();
            CleanListenSockets();
            _ListenSockets = ips.Select(x => {
                try
                {
                    var so = GetOneSocket(x);
                    return Tuple.Create(x.Item1, x.Item2, so);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Exception in InitListenSockets");
                }
                return null;
            }).Where(x => x.Item3 != null).ToImmutableList();
        }

        private Socket GetOneSocket(Tuple<IPAddress, int> item)
        {
            Socket so = null;
            try
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                so = _SockertLayer.CreateListenSocket(item.Item1, item.Item2, _BindPort);
#pragma warning restore CA2000 // Dispose objects before losing scope
                _SockertLayer.StartListen(so, item.Item1, SocketListernCallback, OnSocketError);
            }
            catch (Exception e)
            {
                so?.Dispose();
                logger.LogError(e, "Exception in InitListenSockets");
                return null;
            }

            return so;
        }

        private void OnSocketError(Socket so)
        {
            if (disposedValue)
            {
                return;
            }
            try
            {
                lock (_ListenSockets)
                {
                    _ListenSockets.RemoveAll(x => x.Item3 == so);
                }
                so?.Dispose();
            }
            catch
            {
            }
            try
            {
                EnsureListenSocketFine();
            }
            catch
            {
            }
        }

        #region process respones



        private static void CleanCache(Dictionary<BigInteger, long> cache)
        {
            var cur = DateTime.UtcNow.ToFileTime();
            lock (cache)
            {
                var keysToRemove = cache.Where(x => (cur - x.Value) > ((long) UdpSendDelay.TotalMilliseconds * 10000 * UdpSendCount * 10))
                           .Select(kvp => kvp.Key)
                           .ToArray();
                foreach (var key in keysToRemove)
                {
                    cache.Remove(key);
                }
            }
        }

        private static bool IsInCache(long ts, BigInteger key, Dictionary<BigInteger, long> cache)
        {
            CleanCache(cache);

            lock (cache)
            {
                if (cache.TryGetValue(key, out var pre))
                {
                    if (ts > pre)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    cache.Add(key, ts);
                    return false;
                }
            }
        }

        private Task<bool> SocketListernCallback(byte[] buffer, int datasize, IPEndPoint endPoint)
        {
            try
            {
                if (buffer[0]!=44 ||buffer[1]!=59|| buffer[2]!=48 )
                {
                    return Task.FromResult(true);
                }
                var version = BitConverter.ToInt16(buffer, 3);
                if (version > Definition.CloudVersion)
                {
                    OnError?.Invoke(Interfaces.Errors.ErrorCode.NeedUpdate);
                    return Task.FromResult(true);
                }
                var str = UTF8Encoding.UTF8.GetString(buffer, header.Length, datasize - header.Length);
                using (var sr = new StringReader(str))
                {
                    var opstr = sr.ReadLine();
                    switch (opstr)
                    {
                        case NetworkPacketOperations.Search:
                        {
                            var ts = long.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                            if (IsInCache(ts, new BigInteger(endPoint.Address.GetAddressBytes()), searchCache))
                            {
                                return Task.FromResult(true);
                            }
                            OnSearch(endPoint);
                        }
                        break;
                        case NetworkPacketOperations.Response:
                        case NetworkPacketOperations.Announce:
                        {
                            var ts = long.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                            if (IsInCache(ts, new BigInteger(endPoint.Address.GetAddressBytes()), responseCache))
                            {
                                return Task.FromResult(true);
                            }
                            var strcontent = sr.ReadLine();
                            OnReceiveNodeInfo?.Invoke(strcontent);
                        }
                        break;
                        case NetworkPacketOperations.Bye:               
                            break;
                        default:
                            break;
                    }
                }


                return Task.FromResult(true);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception in LocalNodesNetwork.");
                return Task.FromResult(true);
            }
        }


        private void OnSearch(IPEndPoint remoteip)
        {
            if (_ListenSockets.FirstOrDefault(x => x.Item1.Equals(remoteip.Address)) != null)
            {//this node
                return;
            }
            SendAnnounceOrSearchResponse(NetworkPacketOperations.Response);
        }
        #endregion


        #region disposable


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;

                if (disposing)
                {
                    CleanListenSockets();
                }

            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~LocalNodesNetwork()
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
