using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NSPersonalCloud.FileSharing;
using NSPersonalCloud.FileSharing.Aliyun;
using NSPersonalCloud.Interfaces.Apps;
using NSPersonalCloud.RootFS;

namespace NSPersonalCloud
{

    public class PersonalCloud : IDisposable
    {
        readonly ILogger logger;

        internal PersonalCloud(ILogger l, IPCService pcsrv, List<StorageProviderInfo> storageProviderInfos)
        {
            CachedNodes = new List<NodeInfoForPC>();
            RootFS = new RootFileSystem(pcsrv);
            StorageProviderInstances = new List<StorageProviderInstance>();

            if (storageProviderInfos != null)
            {
                foreach (var info in storageProviderInfos)
                {
                    try
                    {
                        if (info.Type == StorageProviderInstance.TypeAliYun)
                        {
                            StorageProviderInstances.Add(new StorageProviderInstance_AliyunOSS(info));
                        }
                        else if (info.Type == StorageProviderInstance.TypeAzure)
                        {
                            StorageProviderInstances.Add(new StorageProviderInstance_AzureBlob(info));
                        }
                    }
                    catch
                    {
                        // Ignore any errors
                    }
                }
            }
            logger = l;
            Apps = new List<AppLauncher>();
            httpClient = new Lazy<HttpClient>(() => new HttpClient());
        }

        // display name for human beings
        public string DisplayName { get; set; }
        public string NodeDisplayName { get; set; }

        // Cloud identifier
        public string Id { get; set; }
        //utc
        public long UpdateTimeStamp { get; set; }

        internal List<NodeInfoForPC> CachedNodes { get; }//node guid,url
        internal List<StorageProviderInstance> StorageProviderInstances { get; }

        #region Apps

        public List<AppLauncher> Apps { get; internal set; }

        internal void AddApp(AppLauncher appl)
        {
            lock (Apps)
            {
                Apps.Add(appl);
            }
        }

        public Uri GetWebAppUri(AppLauncher launcher)
        {
            if (launcher is null) throw new ArgumentNullException(nameof(launcher));

            var node = CachedNodes.FirstOrDefault(x => x.NodeGuid == launcher.NodeId);
            if (node is null) return null;

            var nu = node.Url[node.Url.Length - 1] == '/' ? node.Url.Substring(0, node.Url.Length - 1) : node.Url;
            return new Uri($"{nu}{launcher.WebAddress}?AccKey={launcher.AccessKey}");
        }

        #endregion

        //Cloud password
#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] MasterKey { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        public event EventHandler OnNodeChangedEvent;

        internal string CurrentShareCode;
        internal string GenerateShareCode()
        {
            var ran = new Random();
            var code = ran.Next(1000, 10000);
            CurrentShareCode = code.ToString(CultureInfo.InvariantCulture);
            return CurrentShareCode;
        }

        public RootFileSystem RootFS { get; private set; }

        public bool AddStorageProvider(string nodeName, OssConfig ossConfig, StorageProviderVisibility visibility)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) throw new ArgumentException("The node name is empty", nameof(nodeName));
            if (ossConfig == null) throw new ArgumentNullException(nameof(ossConfig));

            lock (StorageProviderInstances)
            {
                if (StorageProviderInstances.Any(x => string.Equals(x.ProviderInfo.Name, nodeName, StringComparison.InvariantCultureIgnoreCase))) return false;
                var instance = new StorageProviderInstance_AliyunOSS(new StorageProviderInfo {
                    Type = StorageProviderInstance.TypeAliYun,
                    Name = nodeName,
                    Visibility = visibility,
                    Settings = JsonConvert.SerializeObject(ossConfig)
                });
                StorageProviderInstances.Add(instance);
                ResyncClientListToStorageProviderInstances();
                return true;
            }
        }

        public bool AddStorageProvider(string nodeName, AzureBlobConfig azureBlobConfig, StorageProviderVisibility visibility)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) throw new ArgumentException("The node name is empty", nameof(nodeName));
            if (azureBlobConfig == null) throw new ArgumentNullException(nameof(azureBlobConfig));

            lock (StorageProviderInstances)
            {
                if (StorageProviderInstances.Any(x => string.Equals(x.ProviderInfo.Name, nodeName, StringComparison.InvariantCultureIgnoreCase))) return false;
                var instance = new StorageProviderInstance_AzureBlob(new StorageProviderInfo {
                    Type = StorageProviderInstance.TypeAzure,
                    Name = nodeName,
                    Visibility = visibility,
                    Settings = JsonConvert.SerializeObject(azureBlobConfig)
                });
                StorageProviderInstances.Add(instance);
                ResyncClientListToStorageProviderInstances();
                return true;
            }
        }

        public bool RemoveStorageProvider(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) throw new ArgumentException("The node name is empty", nameof(nodeName));

            lock (StorageProviderInstances)
            {
                var item = StorageProviderInstances
                    .Where(x => string.Equals(x.ProviderInfo.Name, nodeName, StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault();
                if (item != null)
                {
                    StorageProviderInstances.Remove(item);
                    ResyncClientListToStorageProviderInstances();
                    return true;
                }
                return false;
            }
        }

        public void ResyncClientList()
        {
            lock (StorageProviderInstances)
            {
                ResyncClientListToStorageProviderInstances();
            }
        }

        private void ResyncClientListToStorageProviderInstances()
        {
            List<Guid> skips = new List<Guid>();
            List<string> removes = new List<string>();
            foreach (var client in RootFS.ClientList)
            {
                if (client.Value is AliyunOSSFileSystemClient aliyunOssInstance)
                {
                    if (StorageProviderInstances.Any(x => x.RuntimeId == aliyunOssInstance.RuntimeId))
                    {
                        skips.Add(aliyunOssInstance.RuntimeId);
                    }
                    else
                    {
                        removes.Add(client.Key);
                    }
                }
                else if (client.Value is AzureBlobFileSystemClient azureBlobInstance)
                {
                    if (StorageProviderInstances.Any(x => x.RuntimeId == azureBlobInstance.RuntimeId))
                    {
                        skips.Add(azureBlobInstance.RuntimeId);
                    }
                    else
                    {
                        removes.Add(client.Key);
                    }
                }
            }
            foreach (var item in removes)
            {
                RootFS.ClientList.TryRemove(item, out _);
            }
            foreach (var item in StorageProviderInstances)
            {
                if (skips.Contains(item.RuntimeId)) continue;
                if (item.ProviderInfo.Type == StorageProviderInstance.TypeAliYun && item is StorageProviderInstance_AliyunOSS aliyunOssInstance)
                {
                    InsertRootFS(item.ProviderInfo.Name, new AliyunOSSFileSystemClient(item.RuntimeId, aliyunOssInstance.OssConfig));
                }
                else if (item.ProviderInfo.Type == StorageProviderInstance.TypeAzure && item is StorageProviderInstance_AzureBlob azureBlobInstance)
                {
                    InsertRootFS(item.ProviderInfo.Name, new AzureBlobFileSystemClient(item.RuntimeId, azureBlobInstance.AzureBlobConfig));
                }
            }
        }

        internal async Task OnNodeUpdate(NodeInfo ninfo, List<SSDPPCInfo> ssdpinfo)
        {
            bool updated = false;
            bool deleted = false;
            lock (CachedNodes)
            {
                var ssdpin = ssdpinfo.FirstOrDefault(x => x.Id == Id);
                if (ssdpin != null)
                {
                    var name = DecryptName(ssdpin.EN);
                    if (name == null)
                    {
                        logger.LogError($"Invalid node from {ninfo.Url}");
                        return;
                    }

                    NodeInfoForPC info = CachedNodes.FirstOrDefault(x => x.NodeGuid == ninfo.NodeGuid);
                    if (info != null)
                    {
                        if (info.PCTimeStamp < ssdpin.TimeStamp)
                        {
                            CachedNodes.Remove(info);
                        }
                        else
                        {
                            return;
                        }
                    }
                    var newinf = new NodeInfoForPC {
                        NodeGuid = ninfo.NodeGuid,
                        PCVersion = ninfo.PCVersion,
                        PCTimeStamp = ssdpin.TimeStamp,
                        Url = ninfo.Url,
                        Name = name
                    };
                    CachedNodes.Add(newinf);
                    OnCachedNodesChange();
                    updated = true;
                }
                else
                {
                    int nremoved = 0;
                    if (CachedNodes.FirstOrDefault(x => x.NodeGuid == ninfo.NodeGuid) != null)
                    {
                        nremoved = CachedNodes.RemoveAll(x => x.NodeGuid == ninfo.NodeGuid);
                    }
                    if (nremoved > 0)
                    {
                        OnCachedNodesChange();
                        deleted = true;
                    }
                }
            }
            if (updated || deleted)
            {
                if (!deleted)
                {
                    var pci = await GetPeerPCInfo(this, ninfo).ConfigureAwait(false);
                    if ((pci != null) && (pci.Apps != null))
                    {
                        foreach (var ai in pci.Apps)
                        {
                            NodeInfoForPC n = null;
                            lock (CachedNodes)
                            {
                                n = CachedNodes.FirstOrDefault(x => x.NodeGuid == ai.NodeId);
                            }
                            if (n == null)
                            {
                                return;
                            }
                            var a = Apps.FirstOrDefault(x => (x.NodeId == ai.NodeId) && (x.AppId == ai.AppId) && (x.AccessKey == ai.AccessKey));
                            if (a == null)
                            {
                                lock (Apps)
                                {
                                    Apps.Add(ai);
                                }
                            }
                        }
                    }
                }
                _ = Task.Run(() => {
                    try
                    {
                        OnNodeChangedEvent?.Invoke(this, EventArgs.Empty);
                    }
                    catch
                    {
                    }
                });
            }
        }

        Lazy<HttpClient> httpClient;
        private async Task<PersonalCloudInfo> GetPeerPCInfo(PersonalCloud pc, NodeInfo ninfo)
        {
            try
            {
                var url = new Uri(new Uri(ninfo.Url), "/api/share/cloud");
                var s = await TopFolderClient.GetCloudInfo(httpClient.Value, url, pc.Id, pc.MasterKey).ConfigureAwait(false);
                var cfg = JsonConvert.DeserializeObject<PersonalCloudInfo>(s);
                return cfg;
            }
            catch (Exception e)
            {
                logger.LogError("Exception in GetPeerPCInfo", e);
                return null;
            }
        }

        public void InsertRootFS(string nodeName, AliyunOSSFileSystemClient client)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) throw new ArgumentException("The node name is empty", nameof(nodeName));
            if (client == null) throw new ArgumentNullException(nameof(client));

            string nm = nodeName;
            string key = nm;
            if (RootFS.ClientList.ContainsKey(nm))
            {
                //user input duplicated name
                for (int i = 2; i < int.MaxValue; i++)
                {
                    key = $"{nm}({i})";
                    if (RootFS.ClientList.ContainsKey(key))
                    {
                        continue;
                    }
                    else
                    {
                        RootFS.ClientList.TryAdd(key, client);
                        break;
                    }
                }
            }
            else
            {
                RootFS.ClientList.TryAdd(key, client);
            }
        }

        public void InsertRootFS(string nodeName, AzureBlobFileSystemClient client)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) throw new ArgumentException("The node name is empty", nameof(nodeName));
            if (client == null) throw new ArgumentNullException(nameof(client));

            string nm = nodeName;
            string key = nm;
            if (RootFS.ClientList.ContainsKey(nm))
            {
                //user input duplicated name
                for (int i = 2; i < int.MaxValue; i++)
                {
                    key = $"{nm}({i})";
                    if (RootFS.ClientList.ContainsKey(key))
                    {
                        continue;
                    }
                    else
                    {
                        RootFS.ClientList.TryAdd(key, client);
                        break;
                    }
                }
            }
            else
            {
                RootFS.ClientList.TryAdd(key, client);
            }
        }

        private void InsertRootFS(NodeInfoForPC node, TopFolderClient cli = null)
        {
            string nm = node.Name;
            string key = nm;
            var loccli = cli;
            if (RootFS.ClientList.ContainsKey(nm))
            {
                //user input duplicated name
                for (int i = 2; i < int.MaxValue; i++)
                {
                    key = $"{nm}({i})";
                    if (RootFS.ClientList.ContainsKey(key))
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (loccli == null)
                {
#pragma warning disable CA2000 // Collection elements are disposed elsewhere.
                    loccli = new TopFolderClient(node.Url, MasterKey, Id) {
                        Name = node.Name,
                        NodeId = node.NodeGuid,
                        TimeStamp = node.PCTimeStamp
                    };
#pragma warning restore CA2000
                }
                var ret = RootFS.ClientList.TryAdd(key, loccli);
                if ((!ret) && (cli == null))
                {
                    loccli.Dispose();
                }

                loccli = null;
            }
            else
            {
                if (loccli == null)
                {
#pragma warning disable CA2000 // Collection elements are disposed elsewhere.
                    loccli = new TopFolderClient(node.Url, MasterKey, Id) {
                        Name = node.Name,
                        NodeId = node.NodeGuid,
                        TimeStamp = node.PCTimeStamp
                    };
#pragma warning restore CA2000
                }
                var ret = RootFS.ClientList.TryAdd(key, loccli);
                if ((!ret) && (cli == null))
                {
                    loccli.Dispose();
                }

                loccli = null;
            }
        }

        void OnCachedNodesChange()
        {
            lock (CachedNodes)
            {
                NodeInfoForPC[] nodes;
                nodes = CachedNodes.ToArray();
                foreach (var node in nodes)
                {
                    var cur = RootFS.ClientList.FirstOrDefault(x => {//find by node id
                        if (x.Value is TopFolderClient tpc)
                        {
                            if (tpc.NodeId == node.NodeGuid)
                            {
                                return true;
                            }
                        }
                        return false;
                    });
                    if (cur.Key == null)//not found
                    {
                        InsertRootFS(node);

                    }
                    else
                    {
                        if (cur.Value is TopFolderClient tpc)
                        {
                            if (tpc.TimeStamp >= node.PCTimeStamp)
                            {
                                continue;
                            }
                            var origname = tpc.Name;
                            tpc.hostUri = new Uri(node.Url);
                            tpc.Name = node.Name;
                            tpc.NodeId = node.NodeGuid;
                            tpc.TimeStamp = node.PCTimeStamp;
                            if (origname != node.Name)
                            {
                                InsertRootFS(node, tpc);
                                RootFS.ClientList.TryRemove(origname, out _);
                            }
                            else
                            {// do nothing

                            }
                        }
                    }
                }

                var lis = RootFS.ClientList.Where(x => {
                    if (x.Value is TopFolderClient tpc)
                    {
                        lock (CachedNodes)
                        {
                            return CachedNodes.FirstOrDefault(y => y.NodeGuid == tpc.NodeId) == null;
                        }
                    }
                    return false;
                }).ToList();
                foreach (var item in lis)
                {
                    RootFS.ClientList.TryRemove(item.Key, out _);
                }
            }
        }

        internal void OnNodeAdded(NodeShareInfo ninfo, string nodename, long pctimeStamp)
        {
            var newinf = new NodeInfoForPC {
                NodeGuid = ninfo.NodeId,
                PCVersion = ninfo.PCVersion,
                PCTimeStamp = pctimeStamp,
                Url = ninfo.Url,
                Name = nodename
            };
            lock (CachedNodes)
            {
                CachedNodes.Add(newinf);
            }
            OnCachedNodesChange();
        }


        #region encryptedName

        string DecryptName(byte[] en)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = MasterKey;
                aes.IV = new byte[aes.BlockSize / 8];
                aes.Mode = CipherMode.CBC;
                using (var from = new MemoryStream(en))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(from, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {

                        var namebuf = new byte[1024];
                        var readcnt = csDecrypt.Read(namebuf, 0, 4);
                        readcnt = csDecrypt.Read(namebuf, 0, 1);
                        if ((readcnt != 1) && (namebuf[0] != 1))
                        {
                            return null;
                        }

                        readcnt = csDecrypt.Read(namebuf, 0, namebuf.Length);
                        return Encoding.Unicode.GetString(namebuf, 0, readcnt);

                    }
                }
            }
        }
        byte[] encryptedName;
#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] EncryptedName
#pragma warning restore CA1819 // Properties should not return arrays
        {
            get {
                try
                {
                    if (encryptedName == null)
                    {
                        using (var aes = Aes.Create())
                        {
                            aes.KeySize = 256;
                            aes.Key = MasterKey;
                            aes.Padding = PaddingMode.PKCS7;
                            aes.IV = new byte[aes.BlockSize / 8];
                            aes.Mode = CipherMode.CBC;
                            using (var to = new MemoryStream())
                            {
                                using (CryptoStream writer = new CryptoStream(to, aes.CreateEncryptor(), CryptoStreamMode.Write))
                                {
                                    var ran = new Random();
                                    Int32 rani = ran.Next(0, int.MaxValue);
                                    var i = BitConverter.GetBytes(rani);
                                    writer.Write(i, 0, 4);
                                    writer.WriteByte(1);

                                    using (var txtwriter = new StreamWriter(writer, new UnicodeEncoding(false, false), 1024, true))
                                    {
                                        txtwriter.Write(NodeDisplayName);
                                    }
                                    writer.FlushFinalBlock();
                                    encryptedName = to.ToArray();
                                }
                            }
                        }
                    }
                    return encryptedName;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Exception getting EncryptedName.");
                    throw;
                }
            }
        }
        #endregion

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RootFS?.Dispose();
                RootFS = null;
            }

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}
