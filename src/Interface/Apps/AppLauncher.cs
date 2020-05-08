namespace NSPersonalCloud.Interfaces.Apps
{
    public class AppLauncher
    {
        public string Name { get; set; }
        public AppType AppType { get; set; }
        public string NodeId { get; set; }
        public string AppId { get; set; }
        public string WebAddress { get; set; }
        public string AccessKey { get; set; }
    }
}
