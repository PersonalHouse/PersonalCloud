namespace NSPersonalCloud.Interfaces.Errors
{
    public enum ErrorCode
    {
        NeedUpdate = 1,//client software needs update.
        NetworkLayer = 2,//network error. Sockets have to be reinited. todo: rewrite SSDPServiceController and UdpMulticastServer
    }
}
