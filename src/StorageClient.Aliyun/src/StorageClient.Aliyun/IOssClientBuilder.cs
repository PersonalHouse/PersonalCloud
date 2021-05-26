using Aliyun.OSS;
using Aliyun.OSS.Common;
using System;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public interface IOssClientBuilder
    {
        ClientConfiguration ClientConfiguration { get; }

        OssClient Build();

        OssClient Build(Action<ClientConfiguration> options);
    }
}
