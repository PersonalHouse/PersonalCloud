namespace NSPersonalCloud.StorageClient.Aliyun
{
    public class MultipartUploadOptions
    {
        public int PartSize { get; set; } = MultipartUploadUtility.DEFAULT_PART_SIZE;

        public MultipartStreamMode StreamMode { get; set; } = MultipartStreamMode.Auto;
    }
}
