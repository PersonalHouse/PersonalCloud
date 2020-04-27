using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

using Standart.Hash.xxHash;

namespace NSPersonalCloud
{
    internal class SSDPServiceController: WebApiController
    {
        private PCLocalService LocalService { get; }

        public SSDPServiceController(PCLocalService service)
        {
            LocalService = service;
        }

        [Route(HttpVerbs.Get, "/")]
        public List<SSDPPCInfo> ListCloud()
        {
            return LocalService.PersonalClouds.Select(x => {
                string codehash = null;
                if (x.CurrentShareCode!=null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(x.CurrentShareCode);
                    ulong h64_1 = xxHash64.ComputeHash(data, data.Length);
                    codehash = h64_1.ToString(CultureInfo.InvariantCulture);
                }
                var info = new SSDPPCInfo {
                    Id = x.Id,
                    CodeHash = codehash,
                    EN = x.EncryptedName,
                    TimeStamp = x.UpdateTimeStamp
                };
                return info;
            }).ToList();
        }


        [Route(HttpVerbs.Get, "/{id?}")]
        public PersonalCloudInfo GetCloudInfo(string id, [QueryField("ts", false)] long? ts, [QueryField("hash", false)] ulong? hash)
        {
            if (ts.HasValue && hash.HasValue)
            {
                var pc = LocalService.PersonalClouds.First(x => x.Id == id);
                if (pc == null)
                {
                    return null;
                }
                var data = BitConverter.GetBytes(ts.Value + int.Parse(pc.CurrentShareCode, CultureInfo.InvariantCulture));
                var newhcode = xxHash64.ComputeHash(data, data.Length);
                if (newhcode== hash.Value)
                {
                    var pci = PersonalCloudInfo.FromPersonalCloud(pc);
                    pci.NodeGuid = LocalService.NodeId;
                    return pci;
                }
            }
            return null;
        }
    }
}
