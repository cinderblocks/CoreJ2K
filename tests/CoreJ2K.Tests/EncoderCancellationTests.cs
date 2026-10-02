// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.entropy.encoder;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Encoding observes a <see cref="CancellationToken"/>: a cancelled encode throws <see cref="OperationCanceledException"/>
    /// within about one code-block, packet or few rows of the wavelet transform, and leaves nothing behind that could affect the
    /// next encode.
    /// </summary>
    [Collection("Cancellation timing")]
    public class EncoderCancellationTests
    {
        private const int Size = 1024;

        // Shared sample data: an image source is closed by the encoder, so each encode builds a fresh source over the same arrays.
        private static readonly Lazy<int[][]> Samples = new Lazy<int[][]>(() =>
        {
            var rnd = new Random(8);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[Size * Size];
                for (var y = 0; y < Size; y++)
                    for (var x = 0; x < Size; x++)
                        comps[c][y * Size + x] = Math.Clamp(128 + (int)(70 * Math.Sin(x / (9.0 + c)) * Math.Cos(y / 11.0)) + rnd.Next(-15, 16), 0, 255) - 128;
            }
            return comps;
        });

        private static InterleavedImageSource NewSource() => new InterleavedImageSource(Size, Size, 3, 8, new bool[3], Samples.Value);

        private static InterleavedImageSource SmallBlocksSource()
            => new InterleavedImageSource(512, 512, 1, 8, new[] { false }, new[] { Samples.Value[0].Take(512 * 512).ToArray() });

        private static ParameterList Lossless(string? tiles = null)
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            if (tiles != null) pl["tiles"] = tiles;
            return pl;
        }

        private static ParameterList LossyLayers()
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "off";
            pl["rate"] = "1.0";
            pl["Alayers"] = "0.05 +10 0.5 +5 1.0";
            return pl;
        }

        private static void CancelOnThread(CancellationTokenSource source, TimeSpan delay)
        {
            var thread = new Thread(() =>
            {
                Thread.Sleep(delay);
                try
                {
                    source.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The operation finished first and its test disposed the source; nothing left to cancel.
                }
            }) { IsBackground = true };
            thread.Start();
        }

        /// <summary>
        /// Times an uncancelled encode, then cancels another after an eighth of that time and checks it ends in well under half the
        /// full time.
        /// </summary>
        private static void AssertCancelsPromptly(Action<CancellationToken> encode, string what)
        {
            encode(CancellationToken.None); // warm up
            var stopwatch = Stopwatch.StartNew();
            encode(CancellationToken.None);
            var full = stopwatch.Elapsed;

            using var source = new CancellationTokenSource();
            CancelOnThread(source, TimeSpan.FromTicks(full.Ticks / 8));
            stopwatch.Restart();
            Assert.ThrowsAny<OperationCanceledException>(() => encode(source.Token));
            var cancelled = stopwatch.Elapsed;

            Assert.True(cancelled < full / 2, $"{what}: cancelled after {full / 8} but ran {cancelled}; an uncancelled encode takes {full}");
        }

        private static CancellationToken Cancelled()
        {
            var source = new CancellationTokenSource();
            source.Cancel();
            return source.Token;
        }

        [Fact]
        public void AlreadyCancelledToken_StopsEveryEntryPointBeforeDoingAnyWork()
        {
            var token = Cancelled();
            var output = new MemoryStream();
            var config = new J2KEncoderConfiguration().WithLossless().WithCancellationToken(token);

            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.ToBytes(NewSource(), Lossless(), token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.ToBytes(NewSource(), null, Lossless(), token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.ToBytes(NewSource(), null, Lossless(), null, null, null, null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.WriteTo(output, NewSource(), Lossless(), token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.WriteTo(output, NewSource(), null, Lossless(), token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.ToBytes(NewSource(), config));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.WriteTo(output, NewSource(), config));
            Assert.Equal(0, output.Length);

            var builder = new CompleteEncoderConfigurationBuilder().ForLossless();
            Assert.ThrowsAny<OperationCanceledException>(() => builder.Encode(NewSource(), token));
            Assert.ThrowsAny<OperationCanceledException>(() => builder.WriteTo(NewSource(), output, token));
            Assert.ThrowsAny<OperationCanceledException>(() => new CompleteEncoderConfigurationBuilder().ForLossless().WithCancellationToken(token).Encode(NewSource()));
            Assert.Equal(0, output.Length);
        }

        [Fact]
        public async Task AlreadyCancelledToken_CancelsAsyncTasks()
        {
            var token = Cancelled();
            var output = new MemoryStream();

            var plain = J2kImage.ToBytesAsync(NewSource(), Lossless(), token);
            var configured = J2kImage.ToBytesAsync(NewSource(), new J2KEncoderConfiguration().WithLossless(), token);
            var written = J2kImage.WriteToAsync(output, NewSource(), new J2KEncoderConfiguration().WithLossless(), token);
            var built = new CompleteEncoderConfigurationBuilder().ForLossless().EncodeAsync(NewSource(), token);

            foreach (var task in new Task[] { plain, configured, written, built })
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
                Assert.True(task.IsCanceled, $"task ended {task.Status}");
            }
            Assert.Equal(0, output.Length);
        }

        [Theory]
        [InlineData("lossless, single tile")]
        [InlineData("lossless, 64x64 tiles")]
        [InlineData("lossy with layers")]
        public void CancellingMidEncode_StopsPromptly(string scenario)
        {
            Func<ParameterList> parameters = scenario switch
            {
                "lossless, single tile" => () => Lossless(),
                "lossless, 64x64 tiles" => () => Lossless("64 64"),
                _ => LossyLayers,
            };
            AssertCancelsPromptly(token => J2kImage.ToBytes(NewSource(), parameters(), token), scenario);
        }

        [Fact]
        public void ConfigurationToken_CancelsMidEncode()
        {
            AssertCancelsPromptly(token => J2kImage.ToBytes(NewSource(), new J2KEncoderConfiguration().WithLossless().WithCancellationToken(token)), "configuration token");
        }

        [Fact]
        public void BuilderToken_CancelsMidEncode()
        {
            AssertCancelsPromptly(token => new CompleteEncoderConfigurationBuilder().ForLossless().Encode(NewSource(), token), "builder token");
        }

        [Fact]
        public void AfterACancelledEncode_TheNextEncodeIsIdentical()
        {
            var expected = J2kImage.ToBytes(NewSource(), Lossless());

            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var source = new CancellationTokenSource();
                CancelOnThread(source, TimeSpan.FromMilliseconds(30 + 60 * attempt));
                try { J2kImage.ToBytes(NewSource(), Lossless(), source.Token); }
                catch (OperationCanceledException) { }
            }

            var after = J2kImage.ToBytes(NewSource(), Lossless());
            Assert.True(expected.AsSpan().SequenceEqual(after), "an encode after cancelled encodes produced different bytes");
        }

        [Fact]
        public void UncancelledToken_ChangesNothing()
        {
            var expected = J2kImage.ToBytes(NewSource(), Lossless());

            using var source = new CancellationTokenSource();
            var withToken = J2kImage.ToBytes(NewSource(), Lossless(), source.Token);
            var viaConfig = J2kImage.ToBytes(NewSource(), new J2KEncoderConfiguration().WithLossless().WithCancellationToken(source.Token));
            var viaBuilder = new CompleteEncoderConfigurationBuilder().ForLossless().Encode(NewSource(), source.Token);

            Assert.True(expected.AsSpan().SequenceEqual(withToken), "a live token changed the output");
            Assert.True(viaConfig.Length > 0 && viaBuilder.Length > 0);
        }

        [Fact]
        public void AbandonedEncodes_LeaveNoProducerThreadsBehind()
        {
            // With several threads the encoder pulls blocks ahead on a dedicated producer thread. Encodes that are cancelled or fail
            // part-way must release it: a leaked producer would sit blocked forever, one thread per abandoned encode.
            Process Current() => Process.GetCurrentProcess();
            void Settle() { GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(300); }

            // One of each first, so lazily created thread-pool and runtime threads are already counted in the baseline.
            RunAbandonedEncodes(1);
            Settle();
            var before = Current().Threads.Count;

            RunAbandonedEncodes(12);
            Settle();
            var after = Current().Threads.Count;

            Assert.True(after - before <= 6, $"thread count grew from {before} to {after} over 36 abandoned encodes");

            void RunAbandonedEncodes(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    using var source = new CancellationTokenSource();
                    CancelOnThread(source, TimeSpan.FromMilliseconds(20 + 5 * (i % 6)));
                    try { J2kImage.ToBytes(NewSource(), Lossless(), source.Token); }
                    catch (OperationCanceledException) { }

                    try { J2kImage.ToBytes(new FailingImageSource(NewSource(), failAfterCalls: 40 + 10 * (i % 5)), Lossless()); }
                    catch (InvalidOperationException) { }

                    // The case that actually needs the encoder's cleanup: the coding side fails while the producer is healthy and
                    // blocked on a full queue. 4x4 code-blocks give a component more batches than the queue holds.
                    var previous = StdEntropyCoder.BeforeCodeBatchForCurrentThread;
                    StdEntropyCoder.BeforeCodeBatchForCurrentThread = _ => throw new InvalidOperationException("coding failed");
                    try
                    {
                        var pl = Lossless();
                        pl["Cblksiz"] = "4 4";
                        Assert.Throws<InvalidOperationException>(() => J2kImage.ToBytes(SmallBlocksSource(), pl));
                    }
                    finally
                    {
                        StdEntropyCoder.BeforeCodeBatchForCurrentThread = previous;
                    }
                }
            }
        }

        [Fact]
        public void CancelledEncode_WritesNothingToTheOutputStream()
        {
            using var source = new CancellationTokenSource();
            var output = new MemoryStream();
            CancelOnThread(source, TimeSpan.FromMilliseconds(50));

            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.WriteTo(output, NewSource(), Lossless(), source.Token));
            Assert.Equal(0, output.Length);
        }

        [Theory]
        [InlineData("async token only")]
        [InlineData("configuration token only")]
        [InlineData("same token in both places")]
        [InlineData("different tokens, async one cancelled")]
        public async Task AsyncEncode_CancelledMidEncode_EndsInTheCanceledState(string scenario)
        {
            using var asyncSource = new CancellationTokenSource();
            using var configSource = new CancellationTokenSource();

            var config = new J2KEncoderConfiguration().WithLossless();
            CancellationTokenSource toCancel;
            CancellationToken asyncToken;
            switch (scenario)
            {
                case "async token only": toCancel = asyncSource; asyncToken = asyncSource.Token; break;
                case "configuration token only": config.CancellationToken = configSource.Token; toCancel = configSource; asyncToken = CancellationToken.None; break;
                case "same token in both places": config.CancellationToken = asyncSource.Token; toCancel = asyncSource; asyncToken = asyncSource.Token; break;
                default: config.CancellationToken = configSource.Token; toCancel = asyncSource; asyncToken = asyncSource.Token; break;
            }

            var task = J2kImage.ToBytesAsync(NewSource(), config, asyncToken);
            CancelOnThread(toCancel, TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsCanceled, $"{scenario}: task ended {task.Status}");
        }

        [Fact]
        public async Task AsyncEncode_WithParameterList_AndWithBuilder_AreCancelledMidEncode()
        {
            using (var source = new CancellationTokenSource())
            {
                var task = J2kImage.ToBytesAsync(NewSource(), Lossless(), source.Token);
                CancelOnThread(source, TimeSpan.FromMilliseconds(50));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
                Assert.True(task.IsCanceled, $"task ended {task.Status}");
            }

            using (var source = new CancellationTokenSource())
            {
                var task = new CompleteEncoderConfigurationBuilder().ForLossless().EncodeAsync(NewSource(), source.Token);
                CancelOnThread(source, TimeSpan.FromMilliseconds(50));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
                Assert.True(task.IsCanceled, $"task ended {task.Status}");
            }
        }
    }
}
