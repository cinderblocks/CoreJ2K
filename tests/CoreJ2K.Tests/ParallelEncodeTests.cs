// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using System.Threading.Tasks;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The forward wavelet transform splits its row and column passes across threads. That must be invisible: the encoded bytes are
    /// identical for every thread count, for every kind of stream. These tests lower the size threshold so small images really run
    /// their passes on several threads.
    /// </summary>
    public class ParallelEncodeTests
    {
        private static int[][] MakeComponents(int width, int height, int components, int seed = 1)
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
            return comps;
        }

        // An image source is closed by the encoder, so every encode builds a fresh one over the same samples.
        private static InterleavedImageSource Source(int[][] comps, int width, int height)
            => new InterleavedImageSource(width, height, comps.Length, 8, new bool[comps.Length], comps);

        private static byte[] Encode(int[][] comps, int width, int height, int threads, Action<ParameterList> configure, AtkMarkerSegment? atk = null)
        {
            var previous = ForwWTFull.MinParallelSamplesForCurrentThread;
            ForwWTFull.MinParallelSamplesForCurrentThread = 0; // this thread only: tiny images must still split their passes
            try
            {
                var pl = J2kImage.GetDefaultEncoderParameterList();
                pl["file_format"] = "off";
                pl["threads"] = threads.ToString();
                configure(pl);
                return J2kImage.ToBytes(Source(comps, width, height), null, pl, null, null, null, atk)!;
            }
            finally
            {
                ForwWTFull.MinParallelSamplesForCurrentThread = previous;
            }
        }

        private static void AssertSameBytes(int[][] comps, int width, int height, Action<ParameterList> configure, string what, AtkMarkerSegment? atk = null)
        {
            var sequential = Encode(comps, width, height, 1, configure, atk);
            foreach (var threads in new[] { 2, 8 })
            {
                var parallel = Encode(comps, width, height, threads, configure, atk);
                Assert.True(sequential.AsSpan().SequenceEqual(parallel), $"{what}: encoded bytes differ with {threads} threads ({sequential.Length} vs {parallel.Length} bytes)");
            }
        }

        public static TheoryData<string> Variants() => new TheoryData<string>
        {
            "lossless", "lossy-9x7", "lossy-low-rate", "tiled-aligned", "tiled-unaligned-origin", "layers", "levels-0", "levels-1", "levels-5",
            "precincts-rpcl", "roi", "mct-off", "small-codeblocks",
        };

        private static void Configure(string variant, ParameterList pl)
        {
            switch (variant)
            {
                case "lossless": pl["lossless"] = "on"; break;
                case "lossy-9x7": pl["lossless"] = "off"; pl["rate"] = "2.0"; break;
                case "lossy-low-rate": pl["lossless"] = "off"; pl["rate"] = "0.4"; break;
                case "tiled-aligned": pl["lossless"] = "on"; pl["tiles"] = "64 64"; break;
                case "tiled-unaligned-origin": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["tiles"] = "60 60"; break;
                case "layers": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Alayers"] = "0.2 +4 2.0"; break;
                case "levels-0": pl["lossless"] = "on"; pl["Wlev"] = "0"; break;
                case "levels-1": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Wlev"] = "1"; break;
                case "levels-5": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Wlev"] = "5"; break;
                case "precincts-rpcl": pl["lossless"] = "on"; pl["Cpp"] = "64 64"; pl["Aptype"] = "res-pos"; pl["tiles"] = "100 70"; break;
                case "roi": pl["lossless"] = "on"; pl["Rroi"] = "R 20 20 60 50"; break;
                case "mct-off": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Mct"] = "off"; break;
                case "small-codeblocks": pl["lossless"] = "on"; pl["Cblksiz"] = "16 16"; break;
            }
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void ParallelEncode_IsByteIdenticalToSequential(string variant)
        {
            AssertSameBytes(MakeComponents(200, 160, 3), 200, 160, pl => Configure(variant, pl), variant);
        }

        public static TheoryData<int, int> Shapes() => new TheoryData<int, int>
        {
            { 16, 16 }, { 17, 33 }, { 15, 100 }, { 100, 15 }, { 1, 50 }, { 50, 1 }, { 2, 2 }, { 3, 77 }, { 203, 157 }, { 257, 5 }, { 5, 257 },
            { 31, 32 }, { 64, 3 }, { 33, 17 }, { 16, 17 }, { 17, 16 },
        };

        [Theory]
        [MemberData(nameof(Shapes))]
        public void ParallelEncode_IsByteIdentical_AroundTheColumnBlockBoundary(int width, int height)
        {
            var comps = MakeComponents(width, height, 1);
            AssertSameBytes(comps, width, height, pl => { pl["lossless"] = "on"; pl["Wlev"] = "3"; }, $"{width}x{height} lossless");

            // A rate-limited codestream needs room for its own headers, so very small images are only tested lossless.
            if (width * height >= 256)
                AssertSameBytes(comps, width, height, pl => { pl["lossless"] = "off"; pl["rate"] = "64.0"; pl["Wlev"] = "3"; }, $"{width}x{height} lossy");
        }

        [Fact]
        public void ParallelEncode_WithCustomAtkKernels_IsByteIdentical()
        {
            // The arbitrary-kernel analysis filters take the generic path through the same pass runner.
            var comps = MakeComponents(200, 150, 1);
            AssertSameBytes(comps, 200, 150, pl => pl["lossless"] = "on", "ATK 5/3", AtkMarkerSegment.CreateW5x3Equivalent(2));
            AssertSameBytes(comps, 200, 150, pl => { pl["lossless"] = "off"; pl["rate"] = "3.0"; }, "ATK 9/7", AtkMarkerSegment.CreateW9x7Equivalent(3));
        }

        [Fact]
        public void ConcurrentEncodes_EachUsingParallelPasses_AllMatch()
        {
            var comps = MakeComponents(300, 220, 3);
            var expected = Encode(comps, 300, 220, 1, pl => Configure("tiled-unaligned-origin", pl));

            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 6 }, i =>
            {
                var actual = Encode(comps, 300, 220, 4, pl => Configure("tiled-unaligned-origin", pl));
                if (!expected.AsSpan().SequenceEqual(actual)) failures.Add($"encode {i}");
            });

            Assert.Empty(failures);
        }

        [Fact]
        public void ConfigurationAndBuilder_ControlTheDegree()
        {
            Assert.Equal(3, J2kImage.ResolveDegreeOfParallelism(new J2KEncoderConfiguration().WithMaxDegreeOfParallelism(3).ToParameterList()));
            Assert.Equal(J2kImage.DefaultMaxDegreeOfParallelism, J2kImage.ResolveDegreeOfParallelism(new J2KEncoderConfiguration().ToParameterList()));

            var comps = MakeComponents(200, 160, 3);

            var previous = ForwWTFull.MinParallelSamplesForCurrentThread;
            ForwWTFull.MinParallelSamplesForCurrentThread = 0;
            try
            {
                // A configuration sets its own defaults, so compare it with itself at different thread counts.
                byte[] ViaConfig(int threads) => J2kImage.ToBytes(Source(comps, 200, 160),
                    new J2KEncoderConfiguration().WithLossless().WithFileFormat(false).WithMaxDegreeOfParallelism(threads));
                byte[] ViaBuilder(int threads) => new CompleteEncoderConfigurationBuilder().ForLossless().WithFileFormat(false)
                    .WithMaxDegreeOfParallelism(threads).Encode(Source(comps, 200, 160));

                Assert.True(ViaConfig(1).AsSpan().SequenceEqual(ViaConfig(4)), "a configuration with 4 threads encoded different bytes than with 1");
                Assert.True(ViaBuilder(1).AsSpan().SequenceEqual(ViaBuilder(4)), "a builder with 4 threads encoded different bytes than with 1");
            }
            finally
            {
                ForwWTFull.MinParallelSamplesForCurrentThread = previous;
            }
        }
    }
}
