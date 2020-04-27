using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using NSPersonalCloud.Config;

namespace NSPersonalCloud
{
    public class SimpleConfigStorage : IConfigStorage
    {
        public const string CloudListFileName = "PCListFile.dat";
        public const string ServiceConfigFileName = "LocalService.dat";

        public string RootPath { get; }

        public SimpleConfigStorage(string rootDirectory)
        {
            RootPath = rootDirectory;
            if (!Directory.Exists(RootPath)) Directory.CreateDirectory(RootPath);
        }

        public ServiceConfiguration LoadConfiguration()
        {
            var path = Path.Combine(RootPath, ServiceConfigFileName);
            if (!File.Exists(path)) return null;

            var content = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(content)) return null;

            try
            {
                return JsonConvert.DeserializeObject<ServiceConfiguration>(content);
            }
            catch
            {
                return null;
            }
        }

        public void SaveConfiguration(ServiceConfiguration config)
        {
            var path = Path.Combine(RootPath, ServiceConfigFileName);
            var text = JsonConvert.SerializeObject(config);
            File.WriteAllText(path, text);
        }

        public IEnumerable<PersonalCloudInfo> LoadCloud()
        {
            var path = Path.Combine(RootPath, CloudListFileName);
            if (!File.Exists(path))
            {
                return new List<PersonalCloudInfo>();
            }

            var text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<IEnumerable<PersonalCloudInfo>>(text);
        }

        public void SaveCloud(IEnumerable<PersonalCloudInfo> cloud)
        {
            var path = Path.Combine(RootPath, CloudListFileName);
            var text = JsonConvert.SerializeObject(cloud);
            File.WriteAllText(path, text);
        }

        public void SaveApp(string appid, string pcid,  string jsonconfigs)
        {
            var path = Path.Combine(RootPath, appid, $"{pcid}.pc");
            Directory.CreateDirectory(Path.Combine(RootPath, appid));
            File.WriteAllText(path, jsonconfigs);
        }

        public List<Tuple<string, string>> GetApp(string appid)
        {
            var ret = new List<Tuple<string, string>>();
            var path = Path.Combine(RootPath, appid);
            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                return ret;
            }
            foreach (var item in dir.GetFiles("*.pc"))
            {
                var c = File.ReadAllText(item.FullName);
                var pcid = item.Name.Replace(".pc", "",true, CultureInfo.InvariantCulture);
                ret.Add(Tuple.Create(pcid, c));
            }
            return ret;
        }
    }
}
