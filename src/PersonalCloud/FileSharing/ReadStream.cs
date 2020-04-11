using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using DamienG.Security.Cryptography;

namespace NSPersonalCloud.FileSharing
{
    class ReadStream : Stream
    {
        long FileLength;
        Stream stream;
        long ReadBytes;
        Crc64Iso crc64;
        bool Append;
        bool CloseStream;
        int TimeOut;
        public CancellationTokenSource TokenSource;
        public ReadStream(Stream strm, bool Appendhash, long filelen = 0, int cancellreadafterms = 0, bool closestrm = true)
        {
            CloseStream = closestrm;
            FileLength = filelen > 0 ? filelen : strm.Length;
            crc64 = new Crc64Iso();
            stream = strm;
            ReadBytes = 0;
            Append = Appendhash;
            if (cancellreadafterms > 0)
            {
                TimeOut = cancellreadafterms;
                TokenSource = new CancellationTokenSource(TimeOut);
            }
        }
        public override bool CanRead => true;

        // This method SHOULD return false because the current implementation doesn't support Seek.
        // On certain iOS versions, this property MUST return true.
        // A C# Stream that doesn't support Seek becomes an incomplete (abstract) Objective-C class;
        // which causes runtime error and crash.
        // (This error cannot be caught, as an abstract Objective-C class cannot be instantiated in code,
        // the Objective-C runtime gets confused when an abstract object is passed around.)
        // On the contrary, on some Android devices, a Stream that doesn't support Seek MUST report false;
        // otherwise the system tests Seek() method even when no seeking is performed in code.
        // A workaround is used here to check for "certain iOS versions",
        // this is not documented nor fully tested on all affected devices.
#if DEBUG
        public override bool CanSeek => Environment.MachineName == "bogon";
#else
        public override bool CanSeek => Environment.UserName == "mobile";
#endif

        public override bool CanWrite => false;// stream.CanWrite;

        public override long Length
        {
            get {
                if (Append)
                {
                    return FileLength + 8;
                }
                else
                {
                    return FileLength - 8;
                }
            }
        }

        public override long Position
        {
            get {
                if (Append)
                {
                    return ReadBytes;
                }
                else
                {
                    if (ReadBytes > FileLength)
                    {
                        return FileLength;
                    }
                    return ReadBytes;
                }
            }
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (FileLength == 0)
                {
                    return 0;
                }
                if (TimeOut > 0)
                {
                    TokenSource?.CancelAfter(TimeOut);
                }
                if (Append)
                {
                    if (ReadBytes >= FileLength)
                    {
                        var nct = count;
                        if ((ReadBytes + count) > (8 + FileLength))
                        {
                            nct = (int) (FileLength + 8 - ReadBytes);
                        }
                        Buffer.BlockCopy(crc64.Hash, (int) (ReadBytes - FileLength), buffer, offset, nct);
                        ReadBytes += nct;
                        return nct;
                    }
                    else
                    {
                        var n = stream.Read(buffer, offset, count);
                        crc64.TransformBlock(buffer, offset, n, buffer, offset);
                        ReadBytes += n;
                        if (ReadBytes == FileLength)
                        {
                            var b = new byte[1];
                            crc64.TransformFinalBlock(b, 0, 0);
                        }
                        return n;
                    }
                }
                else
                {
                    if (FileLength <= 8)
                    {
                        return 0;
                    }

                    var n = stream.Read(buffer, offset, count);
                    if (n == 0)
                    {
                        return n;
                    }
                    ReadBytes += n;
                    var de = FileLength - ReadBytes;
                    if (de <= 8)
                    {
                        var exceed = 8 - (int) de;
                        var buflen = new byte[8];
                        var realdataread = n - exceed;
                        Buffer.BlockCopy(buffer, offset + realdataread, buflen, 0, exceed);
                        var readcnt = stream.Read(buflen, exceed, (int) de);
                        if (readcnt != de)
                        {
                            throw new InvalidDataException();
                        }

                        crc64.TransformFinalBlock(buffer, offset, n - exceed);
                        for (int i = 0; i < 8; i++)
                        {
                            if (crc64.Hash[i] != buflen[i])
                            {
                                throw new InvalidDataException();
                            }
                        }

                        return realdataread;
                    }
                    crc64.TransformBlock(buffer, offset, n, buffer, offset);
                    return n;
                }

            }
            catch (Exception e)
            {
                _ = e.Message;
                throw;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
            //return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
            //stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
            //stream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                crc64?.Dispose();
                crc64 = null;
                if (CloseStream)
                {
                    stream?.Dispose();
                }
                stream = null;
                TokenSource?.Dispose();
                TokenSource = null;
            }

            base.Dispose(disposing);

        }
    }

    class WriteStream : Stream
    {
        Stream stream;
        long FileLength;
        Crc64Iso crc64;
        public WriteStream(Stream strm,long filelen)
        {
            crc64 = new Crc64Iso();
            stream = strm;
            FileLength = filelen;
        }
        public override bool CanRead => false;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => true;

        public override long Length => FileLength + 8;

        public override long Position
        {
            get => stream.Position;
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
            //return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
            //stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
            _ = stream.FlushAsync();
            crc64.TransformBlock(buffer, offset, count, buffer, offset);
            return;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    var b = new byte[1];
                    crc64.TransformFinalBlock(b, 0, 0);

                    stream.Write(crc64.Hash, 0, 8);
                    stream?.Dispose();
                    stream = null;
                    crc64?.Dispose();
                    crc64 = null;
                }

                base.Dispose(disposing);
            }
            catch 
            {
            }

        }
    }
}
