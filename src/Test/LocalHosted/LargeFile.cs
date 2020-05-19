using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NSPersonalCloud;
using NSPersonalCloud.FileSharing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace LocalHosted
{

    class TestStream : Stream
    {
        long length;
        long read;
        public TestStream(long l)
        {
            length = l;
            read = 0;
        }
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => length;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var cnt = (length - read) > count ? count : (length - read);
            for (int i = 0; i < cnt; i++)
            {
                buffer[offset + i] = (byte) read;
                read++;
            }
            return (int) cnt;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }

    public class LargeFile
    {
        static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
        }

        [Test]
        public async Task LargeFileTest()
        {
            long filesize = 1L*1024 *  1024 * 1024;
            var testRoot = "I:\\Personal Cloud Test\\";
            int parts = 128;
            var partsize = filesize / parts;


            if (((filesize/ parts) % 256)!=0)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Assert.Fail("filesize/parts must be a multiple of 256");//otherwise you have to rewrite TestStream
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            Directory.CreateDirectory(testRoot);

            using var server = new HttpProvider(100, new VirtualFileSystem(testRoot));
            server.Start();

            using var client = new TopFolderClient($"http://localhost:100", new byte[32], "");

            //if(false)
            {
                try
                {
                    await client.DeleteAsync("test.txt").ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
                {
                }
                using var teststrm = new TestStream(filesize);
                await client.WriteFileAsync("test.txt", teststrm).ConfigureAwait(false);
                await TestRead("test.txt", filesize, parts, client).ConfigureAwait(false);
                await Task.Delay(1000).ConfigureAwait(false);
                try
                {
                    await client.DeleteAsync("test.txt").ConfigureAwait(false);
                }
                catch 
                {
                }
            }

            //if (false)
            {
                try
                {
                    await client.DeleteAsync("test1.txt").ConfigureAwait(false);
                }
                catch 
                {
                }
                var origpart = parts;
                if ((filesize % parts) != 0)
                {
                    parts++;
                }
                {
                    using var teststrm = new TestStream(partsize);
                    await client.WriteFileAsync("test1.txt", teststrm).ConfigureAwait(false);
                }
                for (int i = 1; i < origpart; i++)
                {
                    using var teststrm = new TestStream(partsize);
                    await client.WritePartialFileAsync("test1.txt", partsize*i, partsize, teststrm).ConfigureAwait(false);
                }
                if (origpart< parts)
                {
                    partsize = filesize % origpart;
                    using var teststrm = new TestStream(partsize);
                    await client.WritePartialFileAsync("test1.txt", partsize * origpart, partsize, teststrm).ConfigureAwait(false);
                }
                await TestRead("test1.txt", filesize, parts, client).ConfigureAwait(false);
                await Task.Delay(1000).ConfigureAwait(false);
                try
                {
                    await client.DeleteAsync("test1.txt").ConfigureAwait(false);
                }
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                }
            }

        }

        static private async Task TestRead(string filename, long filesize, int parts, TopFolderClient client)
        {
            var reflen = 1024 * 1024;
            var bufref = new byte[reflen];
            var buf = new byte[reflen];
            for (int i = 0; i < reflen; i++)
            {
                bufref[i] = (byte) i;
            }

            //if (false)
            {
                using var target = await client.ReadFileAsync(filename).ConfigureAwait(false);
                Assert.AreEqual(target.Length, filesize);
                for (long i = 0; i < filesize; i++)
                {
                    var offset = i % reflen;
                    var read = target.Read(buf, 0, reflen - (int) offset);
                    if (read == 0)
                    {
                        Assert.Fail();
                    }
                    if (!buf.AsSpan(0, read).SequenceEqual(bufref.AsSpan((int) offset, read)))
                    {
                        Assert.Fail();
                    }
                    i += read - 1;
                }
                var lastread = target.Read(buf, 0, 1);
                if (lastread != 0)
                {
                    Assert.Fail();
                }
            }
            //if (false)
            {
                var partsize = filesize / parts;
                var origpart = parts;
                if ((filesize % parts) != 0)
                {
                    parts++;
                }

                for (int j = 0; j < parts; j++)
                {
                    
                    using var target = await client.ReadPartialFileAsync(filename, j * partsize, (j + 1) * partsize - 1).ConfigureAwait(false);
                    if (j!=(parts-1))
                    {
                        Assert.AreEqual(target.Length, partsize);
                    }else
                    {
                        if (origpart== parts)
                        {
                            Assert.AreEqual(target.Length, partsize);
                        }
                        else
                        {
                            Assert.AreEqual(target.Length, filesize % origpart);
                        }
                    }
                    for (long i = 0; i < target.Length; i++)
                    {
                        var bufoffset = (i + j * partsize) % reflen;
                        var read = target.Read(buf, 0, reflen - (int) bufoffset);
                        if (read == 0)
                        {
                            Assert.Fail();
                        }
                        if (!buf.AsSpan(0, read).SequenceEqual(bufref.AsSpan((int) bufoffset, read)))
                        {
                            Assert.Fail();
                        }
                        i += read - 1;
                    }
                    var lastread = target.Read(buf, 0, 1);
                    if (lastread != 0)
                    {
                        Assert.Fail();
                    }
                }

            }

            //if(false)
            {
                using var target = await client.ReadPartialFileAsync(filename, 0, filesize-1).ConfigureAwait(false);
                Assert.AreEqual(target.Length, filesize);
                for (long i = 0; i < filesize; i++)
                {
                    var offset = i % reflen;
                    var read = target.Read(buf, 0, reflen - (int) offset);
                    if (read == 0)
                    {
                        Assert.Fail();
                    }
                    if (!buf.AsSpan(0, read).SequenceEqual(bufref.AsSpan((int) offset, read)))
                    {
                        Assert.Fail();
                    }
                    i += read - 1;
                }
                var lastread = target.Read(buf, 0, 1);
                if (lastread != 0)
                {
                    Assert.Fail();
                }

            }
        }
    }
}
