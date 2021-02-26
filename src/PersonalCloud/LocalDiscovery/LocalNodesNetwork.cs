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
using System.Threading;

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

        Socket _ListenSocketV4;
        Socket _ListenSocketV6;
        List<int> ifindices;

        readonly LocalNodesSocket _SockertLayer;
        readonly Dictionary<string, long> searchNodeCache;
        readonly Dictionary<string, long> responseNodeCache;
        readonly Dictionary<BigInteger, long> searchCache;
        readonly Dictionary<BigInteger, long> responseCache;

        readonly byte[] header;
        public static int UdpSendCount { get => 3; }


        public Action<string> OnReceiveNodeInfo { get; internal set; }
        public Action<Interfaces.Errors.ErrorCode> OnError { get; internal set; }


        public LocalNodesNetwork(ILoggerFactory lf)
        {

            logger = lf.CreateLogger<LocalNodesNetwork>();
            _SockertLayer = new LocalNodesSocket(lf.CreateLogger<LocalNodesSocket>());
            header = new byte[] { 44, 59, 48, (byte) Definition.CloudVersion, 0, 0, 0, };
            searchCache = new Dictionary<BigInteger, long>();
            searchNodeCache = new Dictionary<string, long>();
            responseCache = new Dictionary<BigInteger, long>();
            responseNodeCache = new Dictionary<string, long>();


            _StatusTimeStamp = DateTime.UtcNow.ToFileTime();
        }

        #region List local ips

        private static List<int> GetLocalIfIndices()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var ips = new List<int>();
            foreach (var networkInterface in networkInterfaces)
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (!networkInterface.SupportsMulticast)
                    continue;
                if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    continue;

                var idx = GetAdapterIndex(networkInterface);
                if (idx != 0)
                {
                    ips.Add(idx);
                }
            }
            return ips;
        }

        private static int GetAdapterIndex(NetworkInterface networkInterface)
        {
            try
            {
                var interfaceProperties = networkInterface.GetIPProperties();
                try
                {
                    var props = interfaceProperties.GetIPv4Properties();
                    return props.Index;
                }
                catch
                {
                    try
                    {
                        var props = interfaceProperties.GetIPv6Properties();
                        return props.Index;
                    }
                    catch { }
                }
            }
            catch
            {
            }
            return 0;
        }

        private static List<(IPAddress, int)> GetLocalIPAddress()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var ips = new List<(IPAddress, int)>();
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


        private static void AddUnicastAddress(NetworkInterface networkInterface, List<(IPAddress, int)> ips)
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
                    ips.Add((ip, index));
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

        void SendMessage(byte[] messageData, int[] ports)
        {
            var msg = Combine(header, messageData);
            var ips = GetLocalIPAddress();
            var tosend = ips.Select(x => {
                var endps = ports.Select(port => {
                    if (x.Item1.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return new IPEndPoint(IPAddress.Parse("239.255.255.250"), port);
                    }
                    else
                    {
                        return new IPEndPoint(IPAddress.Parse("FF02::C"), port);
                    }
                });
                return (_SockertLayer.CreateClientSocket(x.Item1, x.Item2), endps);
            }).Where(x=>x.Item1!=null);
            for (int i = 0; i < UdpSendCount; i++)
            {
                foreach(var s in tosend)
                {
                    foreach (var p in s.Item2)
                    {
                        try
                        {
                            _ = _SockertLayer.SendTo(s.Item1,p, msg, 0, msg.Length);
                        }
                        finally
                        {
                        }
                    }
                }
            }
            foreach (var so in tosend)
            {
                try
                {
                    so.Item1.Dispose();
                }
                finally
                {
                }
            }
        }
        void SendMessage(Socket so, byte[] messageData, IPEndPoint endp)
        {

            var msg = Combine(header, messageData);

            _ = _SockertLayer.SendTo(so, endp, msg, 0, msg.Length);
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
            var ips = GetLocalIPAddress();
            var ts = DateTime.UtcNow.ToFileTime().ToString(CultureInfo.InvariantCulture);

            var tosend = ips.Select(ip => {
                try
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
                        NodeGuid = _ThisNodeID,
                        PCVersion = Definition.CloudVersion.ToString(CultureInfo.InvariantCulture),
                        TimeStamp = _StatusTimeStamp,
                        Url = url,
                    };
                    var AnnounceString = JsonConvert.SerializeObject(node);

                    using var ms = new MemoryStream();
                    using var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);
                    sw.WriteLine(Ops);
                    sw.WriteLine(ts);
                    sw.WriteLine(AnnounceString);
                    sw.WriteLine(_ThisNodeID);
                    sw.Flush();

                    var arr = ms.ToArray();


                    var endps = _TargetPort.Select(port => {
                        if (ip.Item1.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return new IPEndPoint(IPAddress.Parse("239.255.255.250"), port);
                        }
                        else
                        {
                            return new IPEndPoint(IPAddress.Parse("FF02::C"), port);
                        }
                    });

                    return (_SockertLayer.CreateClientSocket(ip.Item1, ip.Item2), arr, endps);
                }
                finally
                {
                }

            }).Where(x => x.Item1 != null);


            for (int i = 0; i < UdpSendCount; i++)
            {
                foreach (var s in tosend)
                {
                    try
                    {
                        foreach (var endp in s.Item3)
                        {
                            SendMessage(s.Item1,s.Item2, endp);
                        }
                    }
                    finally
                    {
                    }
                }
                foreach (var so in tosend)
                {
                    try
                    {
                        so.Item1.Dispose();
                    }
                    finally
                    {
                    }
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
                    sw.WriteLine(_ThisNodeID);
                    sw.Flush();
                    var arr = ms.ToArray();
                    SendMessage(arr, targetPort);
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
        internal void Restart()
        {
            logger.LogInformation($"Restarting local udp socks. {_ThisNodeID}");
            InitListenSockets();
        }

        internal void EnsureListenSocketFine()
        {
            var newifs = GetLocalIfIndices();
            var nowlis = ifindices;
            var ifsTobeAdded = newifs.Where(x => !nowlis.Contains(x)).ToList();
            if (ifsTobeAdded.Count > 0)
            {
                try
                {
                    _SockertLayer.JoinGroups(_ListenSocketV4, AddressFamily.InterNetwork, ifsTobeAdded);

                }
                catch (Exception e)
                {
                    if (!disposedValue)
                    {
                        logger.LogError(e, "Exception in InitListenSockets");
                        InitListenSockets();
                    }
                }

                try
                {
                    _SockertLayer.JoinGroups(_ListenSocketV6, AddressFamily.InterNetworkV6, ifsTobeAdded);

                }
                catch (Exception e)
                {
                    if (!disposedValue)
                    {
                        logger.LogError(e, "Exception in InitListenSockets");
                        InitListenSockets();
                    }
                }
                SendSearch(_TargetPort);
                SendAnnounce(false);
            }
            else
            {//send ping

            }
        }

        void CleanListenSockets()
        {
            var so = _ListenSocketV4;
            _ListenSocketV4 = null;//to prevent reinitializing
            so?.Dispose();
            so = _ListenSocketV6;
            _ListenSocketV6 = null;
            so?.Dispose();
        }
        void InitListenSockets()
        {
            var idxes = GetLocalIfIndices();
            CleanListenSockets();
            var so = GetOneSocket(IPAddress.Any, idxes);
            if (so != null)
            {
                _ListenSocketV4 = so;
            }
            so = GetOneSocket(IPAddress.IPv6Any, idxes);
            if (so != null)
            {
                _ListenSocketV6 = so;
            }
            ifindices = idxes;
        }

        private Socket GetOneSocket(IPAddress addrany, List<int> idxes)
        {
            Socket so = null;
            try
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                so = _SockertLayer.CreateListenSocket(addrany, _BindPort);
#pragma warning restore CA2000 // Dispose objects before losing scope
                if (so == null)
                {
                    return so;
                }
                _SockertLayer.JoinGroups(so, addrany.AddressFamily, idxes);
                _SockertLayer.StartListen(so, addrany.AddressFamily, SocketListernCallback, OnSocketError);
            }
            catch (Exception e)
            {
                so?.Dispose();
                logger.LogError(e, "Exception in InitListenSockets");
                return null;
            }

            return so;
        }

        private void OnSocketError(Socket so, Exception e)
        {
            if (disposedValue)
            {
                return;
            }
            if ((so!=_ListenSocketV4)||(so != _ListenSocketV6))//socket has been reinitialized.
            {
                return;
            }
            try
            {
                logger.LogError(e, "Exception when receiving data");
                InitListenSockets();
            }
            catch
            {
            }
        }

        #region process respones



        private static void CleanCache<T>(Dictionary<T, long> cache)
        {
            var cur = DateTime.UtcNow.ToFileTime();
            lock (cache)
            {
                var keysToRemove = cache.Where(x => (cur - x.Value) > ( 10000000L * 60*60))//1 hour
                           .Select(kvp => kvp.Key)
                           .ToArray();
                foreach (var key in keysToRemove)
                {
                    cache.Remove(key);
                }
            }
        }

        private static bool IsInCache<T>(long ts, T key, Dictionary<T, long> cache)
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
                if (buffer[0] != 44 || buffer[1] != 59 || buffer[2] != 48)
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
                            var nodeid = sr.ReadLine();
                            if (!string.IsNullOrWhiteSpace(nodeid))
                            {
                                if (IsInCache(ts, nodeid, searchNodeCache))
                                {
                                    return Task.FromResult(true);
                                }
                            }
                            else
                            {//for compatible
                                if (IsInCache(ts, new BigInteger(endPoint.Address.GetAddressBytes()), searchCache))
                                {
                                    return Task.FromResult(true);
                                }
                            }

                            SendAnnounceOrSearchResponse(NetworkPacketOperations.Response);
                        }
                        break;
                        case NetworkPacketOperations.Response:
                        case NetworkPacketOperations.Announce:
                        {
                            var ts = long.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                            var strcontent = sr.ReadLine();
                            var nodeid = sr.ReadLine();

                            if (!string.IsNullOrWhiteSpace(nodeid))
                            {
                                if (IsInCache(ts, nodeid, responseNodeCache))
                                {
                                    return Task.FromResult(true);
                                }
                            }
                            else
                            {//for compatible
                                if (IsInCache(ts, new BigInteger(endPoint.Address.GetAddressBytes()), responseCache))
                                {
                                    return Task.FromResult(true);
                                }
                            }
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
