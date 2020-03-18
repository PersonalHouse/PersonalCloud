namespace NSPersonalCloud.Interfaces.FileSystem
{
    public class FreeSpaceInformation
    {
        public long FreeBytesAvailable { get; set; }
        public long TotalNumberOfBytes { get; set; }
        public long TotalNumberOfFreeBytes { get; set; }
    }
}
