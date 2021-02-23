using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace NSPersonalCloud.LocalDiscovery
{
    class LocalNodesSocket
    {
        readonly ILogger logger;

        public LocalNodesSocket(ILogger l)
        {
            logger = l;
        }
        internal Socket CreateListenSocket(IPAddress localaddress, int interfaceIndex, int port)
        {
            Socket so = null;
            try
            {
                so = new Socket(localaddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                so.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                so.ExclusiveAddressUse = false;
                so.EnableBroadcast = true;
                so.MulticastLoopback = false;
                switch (localaddress.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        so.Bind(new IPEndPoint(IPAddress.Any, port));
                        so.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                            new MulticastOption(IPAddress.Parse("239.255.255.250"), localaddress));
                        break;

                    case AddressFamily.InterNetworkV6:
                        so.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                        so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, interfaceIndex);

                        if (interfaceIndex >= 0)
                            so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                                new IPv6MulticastOption(IPAddress.Parse("FF02::C"), interfaceIndex));
                        else
                            so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                                new IPv6MulticastOption(IPAddress.Parse("FF02::C")));
                        break;
                    default:
                        throw new InvalidOperationException($"address is {localaddress.AddressFamily}");
                }
                so.Ttl = 5;
                return so;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error when creating Socket.");
                so?.Dispose();
                return null;
            }
        }

        internal Socket CreateClientSocket(IPAddress localaddress, int interfaceIndex)
        {
            Socket so = null;
            try
            {
                so = new Socket(localaddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                so.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                so.ExclusiveAddressUse = false;
                so.EnableBroadcast = true;
                switch (localaddress.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        so.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                            new MulticastOption(IPAddress.Parse("239.255.255.250"), localaddress));
                        break;

                    case AddressFamily.InterNetworkV6:
                        so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, interfaceIndex);

                        if (interfaceIndex >= 0)
                            so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                                new IPv6MulticastOption(IPAddress.Parse("FF02::C"), interfaceIndex));
                        else
                            so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                                new IPv6MulticastOption(IPAddress.Parse("FF02::C")));
                        break;
                    default:
                        throw new InvalidOperationException($"address is {localaddress.AddressFamily}");
                }
                so.Ttl = 5;
                return so;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error when creating Socket.");
                so?.Dispose();
                return null;
            }
        }



        internal void StartListen(Socket so, AddressFamily addressFamily  , Func<byte[], int, IPEndPoint, 
            Task<bool>> socketListernCallback, Action<Socket> errorcb)
        {
            Task.Run(async () => {
                var buffer = new byte[4096];
                EndPoint endp = null;
                switch (addressFamily)
                {
                    case AddressFamily.InterNetwork:
                        endp = (EndPoint) new IPEndPoint(IPAddress.Any, Definition.MulticastPort);
                        break;

                    case AddressFamily.InterNetworkV6:
                        endp = (EndPoint) new IPEndPoint(IPAddress.IPv6Any, Definition.MulticastPort);
                        break;
                    default:
                        logger.LogError($"StartListen:address {addressFamily} {so.RemoteEndPoint}");
                        return;
                }

                bool shouldexit = true;
                while (shouldexit)
                {
                    try
                    {
#pragma warning disable PC001, PC002 // API not supported on all platforms ,API not available in .NET Framework 4.6.1
                        var res = await so.ReceiveFromAsync(new ArraySegment<byte>(buffer, 0, 4096), SocketFlags.None, endp);
#pragma warning restore PC001, PC002 // API not available in .NET Framework 4.6.1

                        shouldexit = await socketListernCallback?.Invoke(buffer, res.ReceivedBytes, res.RemoteEndPoint as IPEndPoint);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception in StartListen");
                        errorcb?.Invoke(so);
                        return;
                    }
                }
            });
        }



        public async Task SendTo(Socket so, IPEndPoint endp, byte[] data, int off, int count, int repeatcnt, int repeatmillisecondsDelay)
        {
            for (int i = 0; i < repeatcnt; i++)
            {
                try
                {
                    await so.SendToAsync(new ArraySegment<byte>(data, off, count), SocketFlags.None, endp);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Exception in so.SendToAsync");
                }
                await Task.Delay(repeatmillisecondsDelay);
            }

        }
    }
}
