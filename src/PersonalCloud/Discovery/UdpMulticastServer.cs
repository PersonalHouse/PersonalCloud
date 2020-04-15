using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NSPersonalCloud
{
    class UdpMulticastServer
    {
        readonly ILogger logger;
        readonly List<SocketAsyncEventArgs> socketAsyncEventArgs;

        public UdpMulticastServer(ILogger l)
        {
            logger = l;
            socketAsyncEventArgs = new List<SocketAsyncEventArgs>();
        }
        internal Socket CreateSocket(IPAddress address, int interfaceIndex,int port, IPAddress bindtoaddress)
        {
            try
            {
                var so = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                so.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                so.ExclusiveAddressUse = false;
                so.EnableBroadcast = true;
                so.Ttl = 5;
                switch (address.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        //so.Bind(new IPEndPoint(IPAddress.Any, port));
                        so.Bind(new IPEndPoint(bindtoaddress, port));
                        so.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                            new MulticastOption(IPAddress.Parse("239.255.255.250"), address));
                        //so.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,IPAddress.HostToNetworkOrder( interfaceIndex));
                        break;

                    case AddressFamily.InterNetworkV6:
                        //so.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                        so.Bind(new IPEndPoint(bindtoaddress, port));
                        so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, interfaceIndex);

                        if (interfaceIndex >= 0)
                            so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                                new IPv6MulticastOption(IPAddress.Parse("FF02::C"), interfaceIndex));
                        else
                            so.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                                new IPv6MulticastOption(IPAddress.Parse("FF02::C")));
                        break;
                    default:
                        throw new InvalidOperationException($"address is {address.AddressFamily}");
                }
                return so;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error creating Socket.");
                throw;
            }
        }

        class SocketRecvContext
        {
            public  byte[] Buffer;//todo:change it to list to support udp fragment
            public int Offset;
            public int Count;
            public Socket So;
            public Func<byte[], int,IPEndPoint, Task<bool>> Callback;
            public Action<SocketError> ErrorCallback;
        }
        internal void Listen(Socket so, Func<byte[], int, IPEndPoint, Task<bool>> socketListernCallback, Action<SocketError> errorcb)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var se = GetSocketAsyncEventArgs();
#pragma warning restore CA2000 // Dispose objects before losing scope
            var ctx = new SocketRecvContext() {
                So = so,
                Buffer =  new byte[4096],
                Offset = 0,
                Count = 4096,
                Callback = socketListernCallback,
                ErrorCallback=errorcb
            };
            se.UserToken = ctx;
            se.SetBuffer(ctx.Buffer, ctx.Offset,ctx.Count- ctx.Offset);
            if (so.AddressFamily==AddressFamily.InterNetwork)
            {
                se.RemoteEndPoint = new IPEndPoint(IPAddress.Any, Definition.MulticastPort);
            }else
            {
                se.RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Any, Definition.MulticastPort);
            }
            if (!so.ReceiveFromAsync(se))
            {
                Task.Run(() => {
                    Socket_Completed(null, se);
                });                    
            }
        }
        internal void CloseSocket(Socket so)
        {
            so?.Dispose();
        }

        void ReceivMore(SocketRecvContext ctx, SocketAsyncEventArgs e)
        {
            try
            {
                e.UserToken = ctx;
                //ctx.Buffer  = new byte[4096];
                e.SetBuffer(ctx.Buffer, 0, ctx.Buffer.Length);
                if (ctx.So.AddressFamily == AddressFamily.InterNetwork)
                {
                    e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, Definition.MulticastPort);
                }
                else
                {
                    e.RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Any, Definition.MulticastPort);
                }
                if (!ctx.So.ReceiveFromAsync(e))
                {
                    Task.Run(() => {
                        Socket_Completed(null, e);
                    });
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception in UdpMulticastServer.");
            }
        }

        SocketAsyncEventArgs GetSocketAsyncEventArgs()
        {
            lock (socketAsyncEventArgs)
            {
                if (socketAsyncEventArgs.Count>0)
                {
                    var e = socketAsyncEventArgs[0];
                    socketAsyncEventArgs.RemoveAt(0);
                    return e;
                }
                else
                {
                    var e = new SocketAsyncEventArgs();
                    e.Completed += Socket_Completed;
                    return e;
                }
            }
        }
        void  StoreSocketAsyncEventArgs(SocketAsyncEventArgs e)
        {
            lock (socketAsyncEventArgs)
            {
                if (socketAsyncEventArgs.Count > 10)//don't cache too much
                {
                    return;
                }
                else
                {
                    socketAsyncEventArgs.Add(e);
                }
            }
        }

        private void Socket_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {

                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.ReceiveFrom:

                        if (e.SocketError != SocketError.Success)
                        {
                            if (e.SocketError!= SocketError.OperationAborted)
                            {
                                logger.LogError($"Socket Receive error: {e.SocketError}.");

                            }
                            return;
                        }
                        
                        Task.Run(async () => {
                            var ctx = (SocketRecvContext) e.UserToken;
                            var remotep = e.RemoteEndPoint as IPEndPoint;
                            //logger.LogTrace($"ReceiveFrom  {remotep.Port} local port {ctx.So.LocalEndPoint}");

                            var bmore = await ctx.Callback(ctx.Buffer, e.BytesTransferred, remotep).ConfigureAwait(false);
                            if (bmore)
                            {
                                ReceivMore(ctx, e);
                            }
                        });

                        break;
                    case SocketAsyncOperation.SendTo:
                        var ctx = (SocketSendContext) e.UserToken;
                        if (e.SocketError != SocketError.Success)
                        {
                           logger.LogError($"Socket Send error {e.SocketError}. From {ctx.So.LocalEndPoint} to {e.RemoteEndPoint}.");
                        }
                        if (ctx != null)
                        {
                            --ctx.RepeatCount;
                        }
                        //logger.LogInformation($"Socket Send error {e.RemoteEndPoint} {ctx.So.LocalEndPoint}");
                        if (ctx.RepeatCount > 0)
                        {
                            Task.Run(async () => {
                                await Task.Delay(ctx.RepeatDelay).ConfigureAwait(false);
                                e.SetBuffer(ctx.Data, ctx.Offset, ctx.Count);
                                ctx.So.SendToAsync(e);
                            });
                        }else
                        {
                            var err = e.SocketError;
                            StoreSocketAsyncEventArgs(e);
                            ctx.ErrorCallback?.Invoke(err);
                        }
                        break;
                    case SocketAsyncOperation.Disconnect:
                        break;
                    default:
                        break;
                }
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception in UdpMulticastServer.");
            }
        }

        class SocketSendContext
        {
            public int RepeatCount;
            public int RepeatDelay;
            public byte[] Data;
            public int Offset;
            public int Count;
            public Socket So;
            public Action<SocketError> ErrorCallback;
        }
        public void SendTo(Socket so, IPEndPoint endp, byte[] data,int off, int count, int repeatcnt, int repeatdelay, Action<SocketError> cb)
        {
//             if (endp.AddressFamily==AddressFamily.InterNetwork)
//             {
//                 if (! endp.Address.Equals(IPAddress.Parse("239.255.255.250")))
//                 {
//                     logger.LogInformation($"send to :{endp.Address}");
//                 }
//             }
//             if (endp.AddressFamily == AddressFamily.InterNetworkV6)
//             {
//                 if (!endp.Address.Equals(IPAddress.Parse("FF02::C")))
//                 {
//                     logger.LogInformation($"send to :{endp.Address}");
//                 }
//             }
#pragma warning disable CA2000 // Dispose objects before losing scope
            var se = GetSocketAsyncEventArgs();
#pragma warning restore CA2000 // Dispose objects before losing scope
            se.UserToken = new SocketSendContext() {
                RepeatCount = repeatcnt,
                RepeatDelay = repeatdelay,
                Data=data,
                Offset=off,
                Count=count,
                So=so,
                ErrorCallback=cb,
            };
            se.RemoteEndPoint = endp;
            se.SetBuffer(data, off, count);
            if (!so.SendToAsync(se))
            {
                Task.Run(() => {
                    Socket_Completed(null, se);
                });
            }
        }
    }
}
