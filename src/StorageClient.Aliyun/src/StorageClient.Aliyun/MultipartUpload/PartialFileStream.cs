using System.IO;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    class PartialFileStream : FileStream
    {
        public PartialFileStream(string path, FileMode mode, FileAccess access, FileShare share, long startPosition, long length)
            : base(path, mode, access, share)
        {
            base.Seek(startPosition, SeekOrigin.Begin);
            ReadTillPosition = startPosition + length;
        }

        public long ReadTillPosition { get; set; }

        public override int Read(byte[] array, int offset, int count)
        {
            if (base.Position >= this.ReadTillPosition)
                return 0;

            if (base.Position + count > this.ReadTillPosition)
                count = (int)(this.ReadTillPosition - base.Position);

            return base.Read(array, offset, count);
        }
    }
}
