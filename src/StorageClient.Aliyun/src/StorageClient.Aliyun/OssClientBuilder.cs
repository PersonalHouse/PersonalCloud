using Aliyun.OSS;
using Aliyun.OSS.Common;
using System;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    public class OssClientBuilder : IOssClientBuilder
    {
        private readonly string _OssEndpoint;
        private readonly string _AccessKeyId;
        private readonly string _AccessKeySecret;

        public ClientConfiguration ClientConfiguration { get; } = new ClientConfiguration();

        public OssClientBuilder(string ossEndpoint, string accessKeyId, string accessKeySecret)
        {
            _OssEndpoint = ossEndpoint;
            _AccessKeyId = accessKeyId;
            _AccessKeySecret = accessKeySecret;
        }

        public OssClient Build()
        {
            ClientConfiguration clientConfiguration = (ClientConfiguration)this.ClientConfiguration.Clone();
            return new OssClient(_OssEndpoint, _AccessKeyId, _AccessKeySecret, clientConfiguration);
        }

        public OssClient Build(Action<ClientConfiguration> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            ClientConfiguration clientConfiguration = (ClientConfiguration)this.ClientConfiguration.Clone();
            options(clientConfiguration);
            return new OssClient(_OssEndpoint, _AccessKeyId, _AccessKeySecret, clientConfiguration);
        }
    }
}
