using System;

namespace NSPersonalCloud
{
    public class StorageProviderInfo
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public StorageProviderVisibility Visibility { get; set; }
        public string Settings { get; set; }
    }
}
