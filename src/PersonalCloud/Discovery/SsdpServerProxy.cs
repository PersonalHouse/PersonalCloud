using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NSPersonalCloud.Interfaces.Errors;

namespace NSPersonalCloud
{

    static class NetworkPacketOperations
    {
        public const string Search = "Search";
        public const string Announce = "Announce";
        public const string Response = "Response";
    }
    class SsdpServerProxy :IDisposable
    {
        readonly IPAddress address;
        readonly int interfaceIndex;
        readonly int BindPort;
        readonly int[] TargetPort;

        readonly UdpMulticastServer udpMulticastServer;

        Socket sendsocket;
        Socket listensocket;
        //readonly Socket _Sendsocket;
        readonly ILogger logger;
        readonly byte[] header;
        readonly Dictionary<string, long> responseCache;
        readonly Dictionary<string, long> searchCache;


        public string AnnounceString { get; set; }



        public SsdpServerProxy(IPAddress addr, int infindx, UdpMulticastServer svr,ILogger l,int bport,int[] tport)
        {
            BindPort = bport;
            TargetPort = tport;

            address = addr;
            interfaceIndex = infindx;
            udpMulticastServer = svr;
            logger = l;
            AnnounceString = "";
            header = new byte[] { 44, 59, 48, (byte)Definition.CloudVersion, 0, 0, 0, };
            responseCache = new Dictionary<string, long>();
            searchCache = new Dictionary<string, long>();

            //_Sendsocket = udpMulticastServer.CreateSocket(address, interfaceIndex, 0);
            sendsocket = udpMulticastServer.CreateSocket(address, interfaceIndex, BindPort, address);
            listensocket = udpMulticastServer.CreateSocket(address, interfaceIndex, BindPort,
                address.AddressFamily==AddressFamily.InterNetwork?IPAddress.Any:IPAddress.IPv6Any);
            BeginListeningForBroadcasts();
        }


        public int UdpSendCount { get => 3; set => throw new NotImplementedException(); }
        public TimeSpan UdpSendDelay { get=> TimeSpan.FromMilliseconds(100); set => throw new NotImplementedException(); }

        public event EventHandler<string> ResponseReceived;
        public event EventHandler<ErrorCode> OnError;

        void BeginListeningForBroadcasts()
        {
            udpMulticastServer.Listen(sendsocket, SocketListernCallback, OnSendError);//OnSendError has not be used yet
            udpMulticastServer.Listen(listensocket, SocketListernCallback, OnSendError);
        }

        private Task<bool> SocketListernCallback(byte[] buffer, int datasize, IPEndPoint endPoint)
        {
            try
            {
                var version = BitConverter.ToInt16(buffer, 3);
                if (version > Definition.CloudVersion)
                {
                    OnError?.Invoke(this, ErrorCode.NeedUpdate);
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
                            if (IsInCache(ts, endPoint.ToString(),searchCache))
                            {
                                break;
                            }
                            if (!endPoint.Address.Equals( address))
                            {
                                logger.LogInformation($"Search request from {endPoint}, local address is: {address}");
                            }
                            SendSearchResponse(ts, endPoint);
                        }
                        break;
                        case NetworkPacketOperations.Response:
                        case NetworkPacketOperations.Announce:
                        {
                            var ts = long.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                            if (IsInCache(ts, endPoint.ToString(), responseCache))
                            {
                                break;
                            }
                            if ((opstr == NetworkPacketOperations.Response)&&(endPoint.Address != address))
                            {
                                logger.LogInformation($"Search response from {endPoint}, local address is: {address}");
                            }
                            var strcontent = sr.ReadLine();
                            ResponseReceived?.Invoke(this, strcontent);
                        }
                        break;
                        default:
                            break;
                    }
                }


                return Task.FromResult(true);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception in SsdpServerProxy.");
                return Task.FromResult(true);
            }
        }

        public void SendAnnounce()
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true))
                {
                    sw.WriteLine(NetworkPacketOperations.Announce);
                    sw.WriteLine(DateTime.UtcNow.ToFileTime().ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine(AnnounceString);
                    sw.Flush();

                    var arr = ms.ToArray();
                    foreach (var port in TargetPort)
                    {
                        IPEndPoint endp;
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            endp = new IPEndPoint(IPAddress.Parse("239.255.255.250"), port);
                        }
                        else
                        {
                            endp = new IPEndPoint(IPAddress.Parse("FF02::C"), port);
                        }
                        SendMessage(arr, endp);
                    }
                }
            }
        }


        private void SendSearchResponse(long ts, IPEndPoint endPoint)
        {
            //logger.LogDebug("SendSearchResponse");
            if (string.IsNullOrWhiteSpace(AnnounceString))
            {
                return;
            }
            if (IsInCache(ts, endPoint.ToString(),responseCache))
            {
                return;
            }
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true))
                {
                    sw.WriteLine(NetworkPacketOperations.Response);
                    sw.WriteLine(DateTime.UtcNow.ToFileTime().ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine(AnnounceString);
                    sw.Flush();
                    SendMessage(ms.ToArray(), endPoint);
                }
            }
        }

        private bool IsInCache(long ts, string key, Dictionary<string, long> cache)
        {
            CleanCache(cache);

            lock (cache)
            {
                if (cache.TryGetValue(key,out var pre))
                {
                    if (ts>pre)
                    {
                        return false;
                    }else
                    {
                        return true;
                    }
                }else
                {
                    cache.Add(key, ts);
                    return false;
                }
            }
        }

        private void CleanCache(Dictionary<string, long> cache)
        {
            var cur = DateTime.UtcNow.ToFileTime();
            lock (cache)
            {
                var keysToRemove = cache.Where(x =>(cur- x.Value)>((long)UdpSendDelay.TotalMilliseconds*10000*UdpSendCount*10))
                           .Select(kvp => kvp.Key)
                           .ToArray();
                foreach (var key in keysToRemove)
                {
                    cache.Remove(key);
                }
            }
        }

        public void Search()
        {
            logger.LogInformation($"Searching, {address}");
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true))
                {
                    sw.WriteLine(NetworkPacketOperations.Search);
                    sw.WriteLine(DateTime.UtcNow.ToFileTime().ToString(CultureInfo.InvariantCulture));
                    sw.Flush();
                    var arr = ms.ToArray();
                    foreach (var port in TargetPort)
                    {
                        IPEndPoint endp;
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            endp = new IPEndPoint(IPAddress.Parse("239.255.255.250"), port);
                        }
                        else
                        {
                            endp = new IPEndPoint(IPAddress.Parse("FF02::C"), port);
                        }
                        SendMessage(arr, endp);
                    }
                }
            }
        }
         static byte[] Combine(byte[] array1, byte[] array2)
        {
            byte[] rv = new byte[array1.Length+array2.Length];
            System.Buffer.BlockCopy(array1, 0, rv, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, rv, array1.Length, array2.Length);
            return rv;
        }
        void ReInitSocket()
        {
            lock (this)
            {
                sendsocket?.Dispose();
                listensocket?.Dispose();

                sendsocket = udpMulticastServer.CreateSocket(address, interfaceIndex, BindPort, address);
                listensocket = udpMulticastServer.CreateSocket(address, interfaceIndex, BindPort,
                    address.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any);

            }
            BeginListeningForBroadcasts();
        }
        public void SendMessage(byte[] messageData, IPEndPoint endp)
        {

            var msg = Combine(header, messageData);
            Socket so;

            lock (this)
            {
                so = sendsocket;
            }
            udpMulticastServer.SendTo(so, endp, msg, 0, msg.Length,
                UdpSendCount, (int) UdpSendDelay.TotalMilliseconds, OnSendError);
        }

        private void OnSendError(SocketError obj)
        {
            try
            {
                if (!disposedValue)
                {
                    ReInitSocket();
                }
            }
            catch
            {
            }
        }



        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!disposedValue)
                {
                    if (sendsocket != null)
                    {
                        udpMulticastServer.CloseSocket(sendsocket);
                        sendsocket = null;
                    }
                    if (listensocket != null)
                    {
                        udpMulticastServer.CloseSocket(listensocket);
                        listensocket = null;
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
