using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NSPersonalCloud.FileSharing
{
    internal static class StreamExt
    {
        public static async Task FastCopyStreamAsync(this Stream input, Stream output, long bytes, CancellationToken cancellationToken)
        {
            const int buflen = 32768;

            var ba = new byte[][] { new byte[buflen], new byte[buflen] };
            var bra = new int[2];
            var ta = new Task[2];

            bra[0] = await input.ReadAsync(ba[0].AsMemory(0, (int) Math.Min((long) buflen, bytes)), cancellationToken).ConfigureAwait(false);
            bytes -= bra[0];
            if (bra[0]>0)
            {
                int i = 0;
                while (bytes > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ta[1] = output.WriteAsync(ba[i % 2], 0, bra[i % 2], cancellationToken);
                    ta[0] = input.ReadAsync(ba[(i + 1) % 2], 0, (int) Math.Min((long) buflen, bytes), cancellationToken);
                    await Task.WhenAll(ta).ConfigureAwait(false);
                    bra[(i + 1) % 2] = ((Task<int>) (ta[0])).Result;

                    if (bra[(i + 1) % 2]==0)
                    {
                        throw new InvalidDataException();
                    }
                    bytes -= bra[(i + 1) % 2];
                    ++i;
                }
                await output.WriteAsync(ba[i % 2].AsMemory(0, bra[i % 2]), cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        public static async Task FastCopyStreamAsync(this Stream input, Stream output, long bytes)
        {
            const int buflen = 32768;

            var ba = new byte[][] { new byte[buflen], new byte[buflen] };
            var bra = new int[2];
            var ta = new Task[2];

            bra[0] = await input.ReadAsync(ba[0].AsMemory(0, (int) Math.Min((long) buflen, bytes))).ConfigureAwait(false);
            bytes -= bra[0];
            if (bra[0] > 0)
            {
                int i = 0;
                while (bytes > 0)
                {
                    ta[1] = output.WriteAsync(ba[i % 2], 0, bra[i % 2]);
                    ta[0] = input.ReadAsync(ba[(i + 1) % 2], 0, (int) Math.Min((long) buflen, bytes));
                    await Task.WhenAll(ta).ConfigureAwait(false);
                    bra[(i + 1) % 2] = ((Task<int>) (ta[0])).Result;

                    if (bra[(i + 1) % 2] == 0)
                    {
                        throw new InvalidDataException();
                    }
                    bytes -= bra[(i + 1) % 2];
                    ++i;
                }
                await output.WriteAsync(ba[i % 2].AsMemory(0, bra[i % 2])).ConfigureAwait(false);
            }
            return;

        }
    }
}
