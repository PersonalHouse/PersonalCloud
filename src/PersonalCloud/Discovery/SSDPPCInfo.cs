namespace NSPersonalCloud
{
    //ssdp personal cloud info
    public class SSDPPCInfo
    {
        public string Id { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] EN { get; set; }//encrypted node name
#pragma warning restore CA1819 // Properties should not return arrays

        public long TimeStamp { get; set; }
        //Join code hash
        public string CodeHash { get; set; }
    }
}
