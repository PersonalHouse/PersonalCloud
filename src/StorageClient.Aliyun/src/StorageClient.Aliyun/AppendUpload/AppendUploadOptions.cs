namespace NSPersonalCloud.StorageClient.Aliyun
{
    public class AppendUploadOptions
    {
        public int InitialBlockSize { get; set; } = AppendUploadUtility.DEFAULT_BLOCK_SIZE;

        public bool AllowOverwrite { get; set; } = false;

        public bool AllowResume { get; set; } = false;

        public bool VerifyBeforeResume { get; set; } = true;
    }
}
