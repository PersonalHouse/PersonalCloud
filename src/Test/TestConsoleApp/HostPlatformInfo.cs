using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NSPersonalCloud;
using NSPersonalCloud.Config;
using NSPersonalCloud.Interfaces;

namespace TestConsoleApp
{
    class HostPlatformInfo : IConfigStorage
    {
        string sub;
        public HostPlatformInfo()
        {
            sub = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        }
        public string GetConfigFolder()
        {
            return Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TestConsoleApp", sub);
        }

        public IEnumerable<PersonalCloudInfo> LoadCloud()
        {
            var fileInfo = new FileInfo(Path.Combine(GetConfigFolder(), "clouds.txt"));
            if (fileInfo.Exists)
            {
                try
                {
                    var c = File.ReadAllText(fileInfo.FullName);
                    return JsonConvert.DeserializeObject<List<PersonalCloudInfo>>(c);
                }
                catch
                {
                    // Ignore
                }
            }
            return null;
        }

        public void SaveCloud(IEnumerable<PersonalCloudInfo> cloud)
        {
            var c = JsonConvert.SerializeObject(cloud);
            File.WriteAllText(Path.Combine(GetConfigFolder(), "clouds.txt"),c);
        }

        public ServiceConfiguration LoadConfiguration()
        {
            var fileInfo = new FileInfo(Path.Combine(GetConfigFolder(), "service.txt"));
            if (fileInfo.Exists)
            {
                try
                {
                    var c = File.ReadAllText(fileInfo.FullName);
                    return JsonConvert.DeserializeObject<ServiceConfiguration>(c);
                }
                catch
                {
                    // Ignore
                }
            }
            return null;
        }


        public void SaveConfiguration(ServiceConfiguration config)
        {
            var c = JsonConvert.SerializeObject(config);
            File.WriteAllText(Path.Combine(GetConfigFolder(), "service.txt"), c);
        }
    }
}
