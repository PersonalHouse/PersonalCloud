using System;
using System.IO;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    class PartialStream : Stream
    {
        private Stream _Stream;
        private long _StartPosition;
        private long _Length;

        public PartialStream(Stream stream, long startPosition, long length)
        {
            _Stream = stream;
            _Stream.Seek(startPosition, SeekOrigin.Begin);
            _StartPosition = startPosition;
            _Length = length;
            ReadTillPosition = startPosition + length;
        }

        public long ReadTillPosition { get; set; }

        public override bool CanRead => _Stream.CanRead;

        public override bool CanSeek => _Stream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _Length;

        public override long Position
        {
            get
            {
                return _Stream.Position - _StartPosition;
            }
            set
            {
                long position = Math.Max(0, value - _StartPosition);
                _Stream.Seek(position, SeekOrigin.Begin);
            }
        }

        public override int Read(byte[] array, int offset, int count)
        {
            if (_Stream.Position >= this.ReadTillPosition)
                return 0;

            if (_Stream.Position + count > this.ReadTillPosition)
                count = (int)(this.ReadTillPosition - _Stream.Position);

            return _Stream.Read(array, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (SeekOrigin.Begin == origin)
            {
                return _Stream.Seek(_StartPosition + offset, SeekOrigin.Begin);
            }
            else if (SeekOrigin.End == origin)
            {
                return _Stream.Seek(_StartPosition + _Length - offset, SeekOrigin.Begin);
            }
            else
            {
                return _Stream.Seek(offset, origin);
            }
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }
    }
}
