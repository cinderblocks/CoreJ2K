// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.synthesis;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Parallel code-block decoding must be invisible: the output is bit-identical to a single-threaded decode for every
    /// codestream, however the stream was produced. These tests compare 1 thread against 8 across codec features, using
    /// small images with the parallel threshold lowered so the parallel path really runs.
    /// </summary>
    public class ParallelDecodeTests
    {
        private static readonly string OpjDir = Path.Combine(AppContext.BaseDirectory, "TestFiles", "opj");

        /// <summary>A reproducible multi-component test image with smooth areas, edges and noise.</summary>
        private static InterleavedImageSource MakeImage(int width, int height, int components, int seed = 1)
        {
            var rnd = new Random(seed);
            var comps = new int[components][];
            for (var c = 0; c < components; c++)
            {
                comps[c] = new int[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var v = 128 + (int)(70 * Math.Sin(x / (7.0 + c)) * Math.Cos(y / 9.0)) + rnd.Next(-12, 13)
                                + ((x / 40 + y / 30) % 2 == 0 ? 30 : -30);
                        comps[c][y * width + x] = Math.Clamp(v, 0, 255) - 128;
                    }
            }
            return new InterleavedImageSource(width, height, components, 8, new bool[components], comps);
        }

        private static byte[] Encode(InterleavedImageSource src, Action<ParameterList> configure)
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            configure(pl);
            return J2kImage.ToBytes(src, null, pl)!;
        }

        /// <summary>
        /// Runs <paramref name="decode"/> with the parallel threshold lowered to zero for this thread only, so small test images
        /// really take the parallel path without affecting other tests running at the same time.
        /// </summary>
        private static T WithForcedParallelism<T>(Func<T> decode)
        {
            var previous = InvWTFull.MinParallelBlocksForCurrentThread;
            InvWTFull.MinParallelBlocksForCurrentThread = 0;
            try { return decode(); }
            finally { InvWTFull.MinParallelBlocksForCurrentThread = previous; }
        }

        private static ParameterList DecoderParameters(int threads, string? res = null)
        {
            var pl = new ParameterList(J2kImage.GetDefaultDecoderParameterList());
            pl["threads"] = threads.ToString();
            if (res != null) pl["res"] = res;
            return pl;
        }

        private static int[][] Decode(byte[] data, int threads, string? res = null) => WithForcedParallelism(() =>
        {
            using var image = J2kImage.FromBytes(data, DecoderParameters(threads, res));
            return Enumerable.Range(0, image.NumberOfComponents).Select(c => (int[])image.GetComponent(c).Clone()).ToArray();
        });

        // Captures the byte buffer the 8-bit fast path hands to the image factory.
        private sealed class FastPathBytes { public byte[] Bytes = Array.Empty<byte>(); }

        private sealed class FastPathBytesImage : IImage
        {
            private readonly byte[] _bytes;
            public FastPathBytesImage(byte[] bytes) { _bytes = bytes; }
            public T As<T>() => (T)(object)new FastPathBytes { Bytes = _bytes };
        }

        private sealed class FastPathBytesCreator : ImageCreator<FastPathBytes>
        {
            public override IImage Create(int width, int height, int numComponents, byte[] bytes) => new FastPathBytesImage(bytes);

            public override BlkImgDataSrc ToPortableImageSource(object imageObject) => throw new NotSupportedException();
        }

        private static void AssertParallelMatchesSequential(byte[] data, string what, string? res = null)
        {
            var sequential = Decode(data, 1, res);
            foreach (var threads in new[] { 2, 8 })
            {
                var parallel = Decode(data, threads, res);
                Assert.Equal(sequential.Length, parallel.Length);
                for (var c = 0; c < sequential.Length; c++)
                    Assert.True(sequential[c].AsSpan().SequenceEqual(parallel[c]), $"{what}: component {c} differs with {threads} threads");
            }
        }

        public static TheoryData<string> Variants() => new TheoryData<string>
        {
            "lossless", "lossy-9x7", "lossy-9x7-rate", "tiled-aligned", "tiled-unaligned-origin", "layers", "bypass", "term-pass",
            "reset-and-segsym", "causal", "sop-eph", "small-codeblocks", "precincts-rpcl", "roi", "mct-off", "tile-specific",
        };

        private static void Configure(string variant, ParameterList pl)
        {
            switch (variant)
            {
                case "lossless": pl["lossless"] = "on"; break;
                case "lossy-9x7": pl["lossless"] = "off"; pl["rate"] = "2.0"; break;
                case "lossy-9x7-rate": pl["lossless"] = "off"; pl["rate"] = "0.4"; break;
                case "tiled-aligned": pl["lossless"] = "on"; pl["tiles"] = "64 64"; break;
                case "tiled-unaligned-origin": pl["lossless"] = "on"; pl["tiles"] = "60 60"; break; // origins not multiples of 2^levels
                case "layers": pl["lossless"] = "on"; pl["Alayers"] = "0.2 +4 1.0"; break;
                case "bypass": pl["lossless"] = "on"; pl["Cbypass"] = "on"; break;
                case "term-pass": pl["lossless"] = "on"; pl["Cterminate"] = "on"; break;
                case "reset-and-segsym": pl["lossless"] = "on"; pl["CresetMQ"] = "on"; pl["Cseg_symbol"] = "on"; break;
                case "causal": pl["lossless"] = "on"; pl["Ccausal"] = "on"; break;
                case "sop-eph": pl["lossless"] = "on"; pl["Psop"] = "on"; pl["Peph"] = "on"; break;
                case "small-codeblocks": pl["lossless"] = "on"; pl["Cblksiz"] = "16 16"; break;
                case "precincts-rpcl": pl["lossless"] = "on"; pl["Cpp"] = "64 64"; pl["Aptype"] = "res-pos"; pl["tiles"] = "100 70"; break;
                case "roi": pl["lossless"] = "on"; pl["Rroi"] = "R 20 20 60 50"; break;
                case "mct-off": pl["lossless"] = "on"; pl["Mct"] = "off"; break;
                case "tile-specific":
                    // Different decomposition depth and code-block size per tile: a worker chain that kept the wrong tile's
                    // per-tile options would decode these blocks with the wrong parameters.
                    pl["lossless"] = "on"; pl["tiles"] = "64 64"; pl["Wlev"] = "5 t1 2 t2 3"; pl["Cblksiz"] = "64 64 t1 16 16";
                    break;
            }
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void ParallelDecode_IsBitIdenticalToSequential(string variant)
        {
            var data = Encode(MakeImage(200, 160, 3), pl => Configure(variant, pl));
            AssertParallelMatchesSequential(data, variant);
        }

        [Theory]
        [InlineData("lossless", "2")]
        [InlineData("lossy-9x7", "3")]
        [InlineData("tiled-unaligned-origin", "3")]
        [InlineData("tiled-unaligned-origin", "1")]
        public void ParallelReducedResolution_IsBitIdenticalToSequential(string variant, string res)
        {
            var data = Encode(MakeImage(200, 160, 3), pl => Configure(variant, pl));
            AssertParallelMatchesSequential(data, $"{variant} @res {res}", res);
        }

        [Theory]
        [InlineData(0.3)]
        [InlineData(0.6)]
        [InlineData(0.9)]
        public void ParallelTruncatedStream_IsBitIdenticalToSequential(double fraction)
        {
            var full = Encode(MakeImage(200, 160, 3), pl => Configure("tiled-unaligned-origin", pl));
            var truncated = full.Take((int)(full.Length * fraction)).ToArray();
            AssertParallelMatchesSequential(truncated, $"truncated to {fraction:P0}");
        }

        // A truncated stream is a legitimate partial decode, never an error. Sweeping every cut point finds the packet/layer
        // boundaries where the rate accounting used to index a packet that was never read.
        [Theory]
        [InlineData("tiled-unaligned-origin")]
        [InlineData("layers")]
        [InlineData("precincts-rpcl")]
        [InlineData("tile-specific")]
        [InlineData("lossy-9x7")]
        public void TruncationAtEveryPercent_NeverThrows_AndParallelAgrees(string variant)
        {
            var full = Encode(MakeImage(200, 160, 3), pl => Configure(variant, pl));
            for (var percent = 5; percent <= 100; percent++)
            {
                var data = full.Take(full.Length * percent / 100).ToArray();
                int[][] sequential;
                try { sequential = Decode(data, 1); }
                catch (Exception e) { throw new Exception($"{variant}: sequential decode of {percent}% threw {e.GetType().Name}: {e.Message}", e); }

                var parallel = Decode(data, 8);
                for (var c = 0; c < sequential.Length; c++)
                    Assert.True(sequential[c].AsSpan().SequenceEqual(parallel[c]), $"{variant} at {percent}%: component {c} differs");
            }
        }

        [Theory]
        [InlineData("gray200_tile100_6res.j2k")]
        [InlineData("gray250x170_tile100x70_rpcl_prec64.j2k")]
        [InlineData("rgb160x140_tile60_cprl_prec32.j2k")]
        [InlineData("gray250x170_tile96_7res_origin.j2k")]
        public void ParallelDecode_OfOpenJpegStreams_IsBitIdenticalToSequential(string file)
        {
            AssertParallelMatchesSequential(File.ReadAllBytes(Path.Combine(OpjDir, file)), file);
        }

        // The README recommends DecodeToImage<T>, which has its own pipeline builder and output loop.
        [Theory]
        [InlineData("lossless")]
        [InlineData("lossy-9x7")]
        [InlineData("tiled-unaligned-origin")]
        [InlineData("tile-specific")]
        public void FastPath_ParallelDecode_IsBitIdenticalToSequential(string variant)
        {
            ImageFactory.Register(new FastPathBytesCreator());
            var data = Encode(MakeImage(200, 160, 3), pl => Configure(variant, pl));

            var sequential = WithForcedParallelism(() => J2kImage.DecodeToImage<FastPathBytes>(data, DecoderParameters(1))).Bytes;
            var parallel = WithForcedParallelism(() => J2kImage.DecodeToImage<FastPathBytes>(data, DecoderParameters(8))).Bytes;

            Assert.Equal(200 * 160 * 3, sequential.Length);
            Assert.True(sequential.AsSpan().SequenceEqual(parallel), $"{variant}: fast-path output differs with 8 threads");
        }

        [Fact]
        public void ParallelDecode_IsBitIdenticalForSingleLargeTile()
        {
            // One tile, many code-blocks: the case parallelism exists for.
            var data = Encode(MakeImage(512, 512, 3, seed: 7), pl => pl["lossless"] = "on");
            AssertParallelMatchesSequential(data, "single 512x512 tile");
        }

        [Fact]
        public void ConcurrentDecodes_EachUsingInternalParallelism_AllMatch()
        {
            var data = Encode(MakeImage(200, 160, 3), pl => Configure("tiled-unaligned-origin", pl));
            var expected = Decode(data, 1);

            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 16, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var actual = Decode(data, 4);
                for (var c = 0; c < expected.Length; c++)
                    if (!expected[c].AsSpan().SequenceEqual(actual[c])) failures.Add($"decode {i}, component {c}");
            });

            Assert.Empty(failures);
        }

        [Fact]
        public void ConfigurationAndProcessWideDefault_ControlTheDegree()
        {
            var data = Encode(MakeImage(200, 160, 1), pl => pl["lossless"] = "on");
            var expected = Decode(data, 1);

            using (var viaConfig = WithForcedParallelism(() => J2kImage.FromBytes(data, new J2KDecoderConfiguration().WithMaxDegreeOfParallelism(3))))
                Assert.True(expected[0].AsSpan().SequenceEqual(viaConfig.GetComponent(0)));

            Assert.Equal(3, J2kImage.ResolveDegreeOfParallelism(new J2KDecoderConfiguration().WithMaxDegreeOfParallelism(3).ToParameterList()));
            Assert.Equal(J2kImage.DefaultMaxDegreeOfParallelism, J2kImage.ResolveDegreeOfParallelism(new J2KDecoderConfiguration().ToParameterList()));
            Assert.Throws<ArgumentOutOfRangeException>(() => J2kImage.DefaultMaxDegreeOfParallelism = 0);
        }

        [Fact]
        public void CorruptStreams_FailTheSameWayRegardlessOfThreads()
        {
            // With corrupted tile data a decode may recover, produce garbage or throw. Parallel and sequential must agree on
            // whether it throws, and on the output when it does not. (Which exception wins when several blocks are corrupt is
            // allowed to differ, so only the throw/no-throw outcome and the exception family are compared.)
            var good = Encode(MakeImage(200, 160, 3), pl => Configure("tiled-aligned", pl));
            var rnd = new Random(1234);
            var disagreements = 0;

            for (var trial = 0; trial < 60; trial++)
            {
                var data = (byte[])good.Clone();
                for (var k = 0; k < 1 + trial % 4; k++)
                    data[rnd.Next(data.Length / 8, data.Length)] ^= (byte)(1 << rnd.Next(8));

                var (sequential, sequentialError) = Try(data, 1);
                var (parallel, parallelError) = Try(data, 8);

                if ((sequentialError == null) != (parallelError == null)) disagreements++;
                else if (sequentialError == null && !sequential!.Zip(parallel!, (a, b) => a.AsSpan().SequenceEqual(b)).All(same => same)) disagreements++;
            }

            Assert.Equal(0, disagreements);

            static (int[][]? samples, Exception? error) Try(byte[] data, int threads)
            {
                try { return (Decode(data, threads), null); }
                catch (Exception e) { return (null, e); }
            }
        }
    }
}
