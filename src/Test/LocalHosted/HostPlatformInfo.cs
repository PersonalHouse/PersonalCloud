using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NSPersonalCloud;
using NSPersonalCloud.Config;
using NSPersonalCloud.Interfaces;

namespace LocalHosted
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
            var d = GetConfigFolder();
            Directory.CreateDirectory(d);
            if (File.Exists(Path.Combine(d, "clouds.txt")))
            {
                var c = File.ReadAllText(Path.Combine(d, "clouds.txt"));
                return JsonConvert.DeserializeObject<List<PersonalCloudInfo>>(c);
            }
            else
            {
                return null;
            }
        }
        public void SaveCloud(IEnumerable<PersonalCloudInfo> cloud)
        {
            var c = JsonConvert.SerializeObject(cloud);
            File.WriteAllText(Path.Combine(GetConfigFolder(), "clouds.txt"),c);
        }

        public ServiceConfiguration LoadConfiguration()
        {
            var d = GetConfigFolder();
            Directory.CreateDirectory(d);
            if (File.Exists(Path.Combine(d, "service.txt")))
            {
                var c = File.ReadAllText(Path.Combine(d, "service.txt"));
                return JsonConvert.DeserializeObject<ServiceConfiguration>(c);
            }
            else
            {
                return null;
            }

        }


        public void SaveConfiguration(ServiceConfiguration config)
        {
            var c = JsonConvert.SerializeObject(config);
            File.WriteAllText(Path.Combine(GetConfigFolder(), "service.txt"), c);
        }
    }
}
