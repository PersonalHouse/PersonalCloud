namespace NSPersonalCloud.Interfaces.FileSystem
{
    public class EnumerateChildrenRequest
    {
        public string SearchPattern { get; set; }
        public int PageSize { get; set; }
        public int PageIndex { get; set; }
    }
}
