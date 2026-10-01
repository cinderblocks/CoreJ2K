// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Decoding observes a <see cref="CancellationToken"/>: a cancelled decode throws <see cref="OperationCanceledException"/>
    /// within about one code-block, whether it runs on one thread or many, and leaves nothing behind that could affect the next decode.
    /// </summary>
    /// <summary>Timing-sensitive tests must not compete with other test classes for the CPU, so they run alone.</summary>
    [CollectionDefinition("Cancellation timing", DisableParallelization = true)]
    public class CancellationTimingCollection { }

    [Collection("Cancellation timing")]
    public class CancellationTests
    {
        private const int Size = 1280;

        // A large-ish lossless RGB stream that takes a few hundred milliseconds to decode even on all cores, shared by the tests.
        private static readonly Lazy<byte[]> LargeStream = new Lazy<byte[]>(() =>
        {
            var rnd = new Random(5);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[Size * Size];
                for (var y = 0; y < Size; y++)
                    for (var x = 0; x < Size; x++)
                        comps[c][y * Size + x] = Math.Clamp(128 + (int)(70 * Math.Sin(x / (9.0 + c)) * Math.Cos(y / 11.0)) + rnd.Next(-15, 16), 0, 255) - 128;
            }
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            return J2kImage.ToBytes(new InterleavedImageSource(Size, Size, 3, 8, new bool[3], comps), null, pl)!;
        });

        // A lossy stream at a low rate: entropy decoding is cheap, so the inverse wavelet transform dominates the decode.
        private static readonly Lazy<byte[]> LossyStream = new Lazy<byte[]>(() =>
        {
            const int size = 1536;
            var rnd = new Random(6);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[size * size];
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                        comps[c][y * size + x] = Math.Clamp(128 + (int)(70 * Math.Sin(x / (13.0 + c)) * Math.Cos(y / 17.0)) + rnd.Next(-9, 10), 0, 255) - 128;
            }
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "off";
            pl["rate"] = "1.0";
            return J2kImage.ToBytes(new InterleavedImageSource(size, size, 3, 8, new bool[3], comps), null, pl)!;
        });

        private static ParameterList Parameters(int threads)
        {
            var pl = new ParameterList(J2kImage.GetDefaultDecoderParameterList());
            pl["threads"] = threads.ToString();
            return pl;
        }

        private static int[][] Samples(InterleavedImage image)
            => Enumerable.Range(0, image.NumberOfComponents).Select(c => (int[])image.GetComponent(c).Clone()).ToArray();

        /// <summary>
        /// Cancels from a dedicated thread. <c>CancelAfter</c> would run its callback on the thread pool, which the parallel
        /// decoder keeps busy, and the delay would then be part of what is being measured.
        /// </summary>
        private static void CancelOnThread(CancellationTokenSource source, TimeSpan delay)
        {
            var thread = new Thread(() =>
            {
                Thread.Sleep(delay);
                source.Cancel();
            }) { IsBackground = true };
            thread.Start();
        }

        /// <summary>
        /// Times an uncancelled decode, then cancels another after an eighth of that time and checks it ends in well under half
        /// the full time. Cancelling in the first eighth means the decode is still somewhere in its main work.
        /// </summary>
        private static void AssertCancelsPromptly(Action<CancellationToken> decode, string what)
        {
            decode(CancellationToken.None); // warm up
            var stopwatch = Stopwatch.StartNew();
            decode(CancellationToken.None);
            var full = stopwatch.Elapsed;

            using var source = new CancellationTokenSource();
            CancelOnThread(source, TimeSpan.FromTicks(full.Ticks / 8));
            stopwatch.Restart();
            Assert.ThrowsAny<OperationCanceledException>(() => decode(source.Token));
            var cancelled = stopwatch.Elapsed;

            Assert.True(cancelled < full / 2, $"{what}: cancelled after {full / 8} but ran {cancelled}; an uncancelled decode takes {full}");
        }

        private static CancellationToken Cancelled()
        {
            var source = new CancellationTokenSource();
            source.Cancel();
            return source.Token;
        }

        // Private marker type and creator so the shared ImageFactory registry is not affected for other fixtures.
        private sealed class CancelMarker { }

        private sealed class CancelImage : IImage
        {
            public T As<T>() => (T)(object)new CancelMarker();
        }

        private sealed class CancelCreator : ImageCreator<CancelMarker>
        {
            public override IImage Create(int width, int height, int numComponents, byte[] bytes) => new CancelImage();

            public override BlkImgDataSrc ToPortableImageSource(object imageObject) => throw new NotSupportedException();
        }

        [Fact]
        public void AlreadyCancelledToken_StopsEveryEntryPointBeforeReadingAnything()
        {
            // Not a JPEG 2000 stream: an uncancelled decode would throw a format error, so seeing OperationCanceledException
            // proves the token was observed first.
            var garbage = new byte[64];
            var token = Cancelled();
            ImageFactory.Register(new CancelCreator());

            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.FromBytes(garbage, null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.FromBytes(new ReadOnlyMemory<byte>(garbage), null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.FromStream(new MemoryStream(garbage), null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.FromStream(new MemoryStream(garbage), out _, null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.DecodeBytes(garbage, null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.DecodeStream(new MemoryStream(garbage), null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.DecodeToImage<CancelMarker>(garbage, null, token));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.DecodeBytes(garbage, new J2KDecoderConfiguration().WithCancellationToken(token)));
            Assert.ThrowsAny<OperationCanceledException>(() => J2kImage.FromBytes(garbage, new J2KDecoderConfiguration().WithCancellationToken(token)));
        }

        [Fact]
        public async Task AlreadyCancelledToken_CancelsAsyncTasks()
        {
            var token = Cancelled();
            var data = new byte[64];

            var byteArray = J2kImage.DecodeBytesAsync(data, (ParameterList?)null, token);
            var configured = J2kImage.DecodeBytesAsync(data, new J2KDecoderConfiguration(), token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => byteArray);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => configured);
            Assert.True(byteArray.IsCanceled);
            Assert.True(configured.IsCanceled);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        public void CancellingMidDecode_StopsPromptly(int threads)
        {
            var data = LargeStream.Value;
            var pl = Parameters(threads);
            AssertCancelsPromptly(token => J2kImage.FromBytes(data, pl, token).Dispose(), $"{threads} thread(s)");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        public void CancellingMidDecode_InTheWaveletTransform_StopsPromptly(int threads)
        {
            var data = LossyStream.Value;
            var pl = Parameters(threads);
            AssertCancelsPromptly(token => J2kImage.FromBytes(data, pl, token).Dispose(), $"lossy, {threads} thread(s)");
        }

        [Fact]
        public void ConfigurationToken_CancelsMidDecode()
        {
            var data = LargeStream.Value;
            AssertCancelsPromptlyViaConfiguration(data);

            static void AssertCancelsPromptlyViaConfiguration(byte[] bytes)
            {
                // The token is baked into the configuration, so each attempt builds its own.
                AssertCancelsPromptly(token => J2kImage.DecodeBytes(bytes, new J2KDecoderConfiguration().WithCancellationToken(token)), "configuration token");
            }
        }

        [Fact]
        public void FastPath_DecodeToImage_CancelsMidDecode()
        {
            ImageFactory.Register(new CancelCreator());
            var data = LargeStream.Value;
            AssertCancelsPromptly(token => J2kImage.DecodeToImage<CancelMarker>(data, Parameters(1), token), "DecodeToImage fast path");
        }

        [Fact]
        public void Jp2WithColourMapping_CancelsMidDecode()
        {
            // A JP2 goes through the colour-space mapping chain after the codestream stages.
            var raw = LargeStream.Value;
            var image = J2kImage.FromBytes(raw);
            var source = new InterleavedImageSource(Size, Size, 3, 8, new bool[3], Enumerable.Range(0, 3).Select(c => image.GetComponent(c).Select(v => v - 128).ToArray()).ToArray());
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var jp2 = J2kImage.ToBytes(source, null, pl)!;
            Assert.Equal(0x6A, jp2[4]); // 'j' of the JP2 signature box: a real JP2 file, not a bare codestream

            AssertCancelsPromptly(token => J2kImage.FromBytes(jp2, Parameters(1), token).Dispose(), "JP2 decode");
        }

        [Fact]
        public void AfterACancelledDecode_TheNextDecodeIsUnaffected()
        {
            var data = LargeStream.Value;
            var expected = Samples(J2kImage.FromBytes(data, Parameters(1)));

            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var source = new CancellationTokenSource();
                CancelOnThread(source, TimeSpan.FromMilliseconds(15 + 25 * attempt));
                try { J2kImage.FromBytes(data, Parameters(8), source.Token).Dispose(); }
                catch (OperationCanceledException) { }
            }

            var after = Samples(J2kImage.FromBytes(data, Parameters(8)));
            for (var c = 0; c < expected.Length; c++)
                Assert.True(expected[c].AsSpan().SequenceEqual(after[c]), $"component {c} differs after cancelled decodes");
        }

        [Fact]
        public void UncancelledToken_ChangesNothing()
        {
            var data = LargeStream.Value;
            var expected = Samples(J2kImage.FromBytes(data, Parameters(8)));

            using var source = new CancellationTokenSource();
            var actual = Samples(J2kImage.FromBytes(data, Parameters(8), source.Token));
            var viaConfig = Samples(J2kImage.FromBytes(data, new J2KDecoderConfiguration().WithCancellationToken(source.Token)));

            for (var c = 0; c < expected.Length; c++)
            {
                Assert.True(expected[c].AsSpan().SequenceEqual(actual[c]), $"component {c} differs with a live token");
                Assert.True(expected[c].AsSpan().SequenceEqual(viaConfig[c]), $"component {c} differs with a configured token");
            }
        }

        [Theory]
        [InlineData("async token only")]
        [InlineData("configuration token only")]
        [InlineData("same token in both places")]
        [InlineData("different tokens, async one cancelled")]
        public async Task AsyncDecode_CancelledMidDecode_EndsInTheCanceledState(string scenario)
        {
            var data = LargeStream.Value;
            using var asyncSource = new CancellationTokenSource();
            using var configSource = new CancellationTokenSource();

            var config = new J2KDecoderConfiguration().WithMaxDegreeOfParallelism(1);
            CancellationTokenSource toCancel;
            CancellationToken asyncToken;
            switch (scenario)
            {
                case "async token only": toCancel = asyncSource; asyncToken = asyncSource.Token; break;
                case "configuration token only": config.CancellationToken = configSource.Token; toCancel = configSource; asyncToken = CancellationToken.None; break;
                case "same token in both places": config.CancellationToken = asyncSource.Token; toCancel = asyncSource; asyncToken = asyncSource.Token; break;
                default: config.CancellationToken = configSource.Token; toCancel = asyncSource; asyncToken = asyncSource.Token; break;
            }

            var task = J2kImage.DecodeBytesAsync(data, config, asyncToken);
            CancelOnThread(toCancel, TimeSpan.FromMilliseconds(40));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsCanceled, $"{scenario}: task ended {task.Status}");
        }

        [Fact]
        public async Task AsyncDecode_WithParameterList_IsCancelledMidDecode()
        {
            using var source = new CancellationTokenSource();
            var task = J2kImage.DecodeBytesAsync(LargeStream.Value, Parameters(1), source.Token);
            CancelOnThread(source, TimeSpan.FromMilliseconds(40));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsCanceled, $"task ended {task.Status}");
        }

        [Fact]
        public void TiledImage_CancelsMidDecode()
        {
            // 1024 tiles of 64x64: the decode is a long run of small tiles, so the per-tile checks take part.
            var rnd = new Random(3);
            var samples = new int[2048 * 2048];
            for (var i = 0; i < samples.Length; i++) samples[i] = rnd.Next(-100, 100);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            pl["tiles"] = "64 64";
            var tiled = J2kImage.ToBytes(new InterleavedImageSource(2048, 2048, 1, 8, new[] { false }, new[] { samples }), null, pl)!;

            AssertCancelsPromptly(token => J2kImage.FromBytes(tiled, Parameters(1), token).Dispose(), "tiled decode");
        }
    }
}
