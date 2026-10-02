// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Buffers;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreJ2K.j2k.wavelet
{
    /// <summary>
    /// Runs one pass of a 2D wavelet transform (all rows, or all columns) either inline or split across threads. Shared by the
    /// forward and inverse transforms.
    /// </summary>
    internal static class WaveletPass
    {
        /// <summary>
        /// Runs a pass made of <paramref name="count"/> independent items (rows, or columns), each of <paramref name="itemLength"/>
        /// samples. <paramref name="body"/> processes items [start, end) using a scratch array of at least
        /// <paramref name="scratchLength"/> elements.
        /// </summary>
        /// <param name="sequentialScratch">Grow-only scratch reused when the pass runs on the calling thread.</param>
        /// <param name="degree">Maximum number of threads; 1 or less keeps the pass on the calling thread.</param>
        /// <param name="minSamples">Passes with fewer samples than this stay on the calling thread: splitting them costs more than it saves.</param>
        /// <remarks>
        /// Each chunk gets its own scratch array and the 1D filters hold no mutable state, so chunks do not interact. Chunks write
        /// disjoint rows or columns of the shared sample buffer.
        /// </remarks>
        internal static void Run<T>(int count, int itemLength, int scratchLength, ref T[]? sequentialScratch, int degree, int minSamples,
            CancellationToken cancellationToken, Action<int, int, T[]> body)
        {
            if (degree <= 1 || count < 2 || (long)count * itemLength < minSamples)
            {
                if (sequentialScratch == null || sequentialScratch.Length < scratchLength)
                    sequentialScratch = new T[scratchLength];
                body(0, count, sequentialScratch);
                return;
            }

            // A few chunks per thread keeps all threads busy when chunks take unequal time.
            var chunkCount = Math.Min(count, degree * 4);
            var chunkSize = (count + chunkCount - 1) / chunkCount;
            chunkCount = (count + chunkSize - 1) / chunkSize;
            var options = new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken };

            try
            {
                Parallel.For(0, chunkCount, options,
                    () => ArrayPool<T>.Shared.Rent(scratchLength),
                    (chunk, _, scratch) =>
                    {
                        var start = chunk * chunkSize;
                        body(start, Math.Min(count, start + chunkSize), scratch);
                        return scratch;
                    },
                    scratch => ArrayPool<T>.Shared.Return(scratch));
            }
            catch (AggregateException e)
            {
                // Surface the original exception (e.g. cancellation) with its type intact.
                ExceptionDispatchInfo.Capture(e.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
        }
    }
}
