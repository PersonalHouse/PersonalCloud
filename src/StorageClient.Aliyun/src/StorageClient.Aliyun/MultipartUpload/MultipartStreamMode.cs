namespace NSPersonalCloud.StorageClient.Aliyun
{
    public enum MultipartStreamMode
    {
        /// <summary>
        /// 根据 Stream 类型自动选择
        /// </summary>
        Auto = 0,

        /// <summary>
        /// 打开多个 PartialFileStream, 多线程分片上传
        /// </summary>
        PartialFileStream = 1,

        /// <summary>
        /// 顺序读取 Stream 内容, 单线程分片上传
        /// </summary>
        SequenceInputStream = 2
    }
}
