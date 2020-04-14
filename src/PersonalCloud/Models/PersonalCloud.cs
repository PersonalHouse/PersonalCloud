using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using NSPersonalCloud.Interfaces.Cloud;
using NSPersonalCloud.RootFS;
using NSPersonalCloud.FileSharing;
using System.Globalization;
using System.Threading.Tasks;

namespace NSPersonalCloud
{

    public class PersonalCloud : ICloud, IDisposable
    {
        readonly ILogger logger;

        internal PersonalCloud(ILogger l, IPCService pcsrv)
        {
            CachedNodes = new List<NodeInfoForPC>();
            RootFS = new RootFileSystem(pcsrv);
            logger = l;
        }

        // display name for human beings
        public string DisplayName { get; set; }
        public string NodeDisplayName { get; set; }

        // Cloud identifier
        public string Id { get; set; }
        //utc
        public long UpdateTimeStamp { get; set; }

        internal List<NodeInfoForPC> CachedNodes { get; }//node guid,url

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

        internal void OnNodeUpdate(NodeInfo ninfo, List<SSDPPCInfo> ssdpinfo)
        {
            bool updated = false;
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
                        updated = true;
                    }
                }
            }
            if (updated)
            {
                Task.Run(() => {
                    try
                    {
                        OnNodeChangedEvent?.Invoke(this, new EventArgs());
                    }
                    catch
                    {
                    }
                });
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
