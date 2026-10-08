// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.entropy.encoder;
using CoreJ2K.j2k.roi;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// <see cref="J2KEncoderConfiguration.WithTelemetry"/> reports what the encode kept. The totals are measured where the bytes are
    /// written, so these tests check them against the bytes of the output itself.
    /// </summary>
    public class EncodeTelemetryTests
    {
        private const int Width = 240;
        private const int Height = 320;

        private static InterleavedImageSource MakeImage(int seed = 5)
        {
            var rnd = new Random(seed);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[Width * Height];
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < Width; x++)
                    {
                        var v = 128 + (int)(60 * Math.Sin(x / (5.0 + c)) * Math.Cos(y / 6.0)) + rnd.Next(-20, 21);
                        comps[c][y * Width + x] = Math.Clamp(v, 0, 255) - 128;
                    }
            }
            return new InterleavedImageSource(Width, Height, 3, 8, new bool[3], comps);
        }

        private static (byte[] Data, EncodeTelemetry? Telemetry) Encode(Action<J2KEncoderConfiguration> configure, int threads = 1)
        {
            EncodeTelemetry? seen = null;
            var config = new J2KEncoderConfiguration();
            configure(config);
            config.WithTelemetry(t => seen = t);
            var pl = config.ToParameterList();
            pl["threads"] = threads.ToString();
            var data = J2kImage.ToBytes(MakeImage(), null, pl)!;
            return (data, seen);
        }

        private static byte[] EncodeWithoutTelemetry(Action<J2KEncoderConfiguration> configure, int threads = 1)
        {
            var config = new J2KEncoderConfiguration();
            configure(config);
            var pl = config.ToParameterList();
            pl["threads"] = threads.ToString();
            return J2kImage.ToBytes(MakeImage(), null, pl)!;
        }

        private static ROIMask Face() => ROIMask.FromEllipse(Width, Height, 120, 130, 60, 80);

        private static int Be16(byte[] d, int i) => (d[i] << 8) | d[i + 1];

        private static int Be32(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

        /// <summary>Parses the codestream for its start, the bytes of main and tile-part headers, and the sum of the tile-part lengths.</summary>
        private static (int Start, int CodestreamLength, int HeaderBytes) ParseCodestream(byte[] d)
        {
            var start = -1;
            for (var i = 0; i < d.Length - 3 && start < 0; i++)
                if (d[i] == 0xFF && d[i + 1] == 0x4F && d[i + 2] == 0xFF && d[i + 3] == 0x51) start = i;
            Assert.True(start >= 0);

            var pos = start + 2;
            while (!(d[pos] == 0xFF && d[pos + 1] == 0x90)) pos += 2 + Be16(d, pos + 2);
            var headers = pos - start;

            // Tile-parts: SOT (Psot is the length from the SOT marker), then markers up to and including SOD
            while (d[pos] == 0xFF && d[pos + 1] == 0x90)
            {
                var psot = Be32(d, pos + 6);
                var p = pos + 12;
                while (!(d[p] == 0xFF && d[p + 1] == 0x93)) p += 2 + Be16(d, p + 2);
                headers += p + 2 - pos;
                pos += psot;
            }

            Assert.Equal(0xFF, d[pos]);
            Assert.Equal(0xD9, d[pos + 1]);
            return (start, pos + 2 - start, headers);
        }

        private static string Fingerprint(EncodeTelemetry t) =>
            string.Join("|", t.CodeBlocks.Select(b =>
                $"{b.Tile},{b.Component},{b.ResolutionLevel},{b.Subband},{b.Layer},{b.BlockColumn},{b.BlockRow},{b.X},{b.Y},{b.Width},{b.Height},"
                + $"{b.MissingMsbs},{b.PassesIncluded},{b.BytesInLayer},{b.DistortionReduction:R},{b.RoiCoefficients},{b.RoiPasses}"))
            + $"#{t.PacketCount},{t.HeaderBytes},{t.PacketHeaderBytes},{t.PacketBodyBytes}";

        [Fact]
        public void Telemetry_DoesNotChangeTheOutput()
        {
            foreach (var configure in new Action<J2KEncoderConfiguration>[]
            {
                c => c.WithBitrate(1.0f),
                c => c.WithMaxBytes(6000),
                c => c.WithMaxBytes(7000).WithROI(new ROIConfiguration().AddMask(-1, Face()).SetStartLevel(3)),
            })
            {
                Assert.Equal(EncodeWithoutTelemetry(configure), Encode(configure).Data);
            }
        }

        [Fact]
        public void Totals_AddUpToTheOutput_ByParsingIt()
        {
            foreach (var configure in new Action<J2KEncoderConfiguration>[]
            {
                c => c.WithMaxBytes(6000),
                c => c.WithBitrate(1.5f),
                c => c.WithBitrate(1.5f).WithFileFormat(false),
                c => c.WithMaxBytes(8000).WithTiles(t => t.SetSize(128, 128)),
            })
            {
                var (data, t) = Encode(configure);
                Assert.NotNull(t);

                var (start, length, headers) = ParseCodestream(data);

                // Measured as written...
                Assert.Equal(t!.CodestreamBytes, t.HeaderBytes + t.PacketHeaderBytes + t.PacketBodyBytes + t.EndOfCodestreamBytes);
                Assert.Equal(data.Length, t.TotalBytes);

                // ...and against the bytes themselves
                Assert.Equal(length, t.CodestreamBytes);
                Assert.Equal(data.Length - length, t.ContainerBytes);
                Assert.Equal(start > 0, t.ContainerBytes > 0);
                Assert.Equal(headers, t.HeaderBytes);
                Assert.Equal(2, t.EndOfCodestreamBytes);
            }
        }

        [Fact]
        public void PacketBodyBytes_AreTheSumOfTheBlocks()
        {
            var (_, t) = Encode(c => c.WithMaxBytes(6000));
            Assert.NotNull(t);
            Assert.Equal(t!.PacketBodyBytes, t.CodeBlocks.Sum(b => (long)b.BytesInLayer));
            Assert.True(t.PacketHeaderBytes > 0);
            Assert.True(t.PacketCount > 0);
            Assert.Equal(1, t.LayerCount);
            Assert.All(t.CodeBlocks, b => Assert.Equal(0, b.Layer));
        }

        [Fact]
        public void EachBlock_IsConsistentWithItself()
        {
            var (_, t) = Encode(c => c.WithMaxBytes(6000));
            foreach (var b in t!.CodeBlocks)
            {
                Assert.Equal(b.PassesIncluded, b.PassEndBytes.Length);
                Assert.Equal(b.PassesInLayer, b.PassesIncluded);
                Assert.Equal(b.BytesInLayer, b.BytesIncluded);
                Assert.True(b.PassesIncluded >= 1 && b.PassesIncluded <= b.TotalPasses);
                Assert.True(b.BytesInLayer > 0 || b.PassesIncluded > 0);
                Assert.True(b.DistortionReduction >= 0);
                Assert.True(b.MissingMsbs >= 0 && b.MissingMsbs < b.MagnitudeBits);
                Assert.True(b.X >= 0 && b.Y >= 0 && b.X + b.Width <= b.SubbandWidth && b.Y + b.Height <= b.SubbandHeight,
                    $"{b.X},{b.Y} {b.Width}x{b.Height} in {b.SubbandWidth}x{b.SubbandHeight}");
                for (var i = 1; i < b.PassEndBytes.Length; i++)
                    Assert.True(b.PassEndBytes[i] >= b.PassEndBytes[i - 1]);
            }
        }

        [Fact]
        public void Blocks_TileTheirSubbands_WhenEverythingIsKept()
        {
            // No limit: every block with any data is kept, and a noisy image has data in all of them.
            var (_, t) = Encode(c => c.WithBitrate(-1f));
            var groups = t!.CodeBlocks.GroupBy(b => (b.Tile, b.Component, b.ResolutionLevel, b.Subband));
            Assert.NotEmpty(groups);
            foreach (var g in groups)
            {
                var first = g.First();
                var area = g.Select(b => (b.BlockColumn, b.BlockRow, Area: (long)b.Width * b.Height)).Distinct().Sum(b => b.Area);
                Assert.Equal((long)first.SubbandWidth * first.SubbandHeight, area);

                // Blocks of one column share an X, blocks of one row a Y, and they run in order
                foreach (var column in g.GroupBy(b => b.BlockColumn)) Assert.Single(column.Select(b => b.X).Distinct());
                foreach (var row in g.GroupBy(b => b.BlockRow)) Assert.Single(row.Select(b => b.Y).Distinct());
                var xs = g.GroupBy(b => b.BlockColumn).OrderBy(c => c.Key).Select(c => c.First().X).ToList();
                Assert.Equal(xs.OrderBy(x => x), xs);
                Assert.Equal(0, xs[0]);
            }
        }

        [Fact]
        public void Layers_AreReportedOnePerBlockPerLayer()
        {
            var (data, t) = Encode(c => c.WithBitrate(2.0f).WithProgression(p => p.WithQualityLayers(0.25f, 0.5f, 1.0f, 2.0f)));
            Assert.NotNull(t);

            // The layer count in the COD marker, which is written from the allocator's own layers
            var cod = Array.FindIndex(data, 0, data.Length - 1, i => false);
            for (var i = 0; i < data.Length - 1 && cod < 0; i++)
                if (data[i] == 0xFF && data[i + 1] == 0x52) cod = i;
            Assert.Equal(Be16(data, cod + 6), t!.LayerCount);
            Assert.True(t.LayerCount >= 4);
            Assert.True(t.CodeBlocks.Max(b => b.Layer) >= 2);
            Assert.Equal(t.PacketBodyBytes, t.CodeBlocks.Sum(b => (long)b.BytesInLayer));

            foreach (var block in t.CodeBlocks.GroupBy(b => (b.Tile, b.Component, b.ResolutionLevel, b.Subband, b.BlockColumn, b.BlockRow)))
            {
                var ordered = block.OrderBy(b => b.Layer).ToList();
                Assert.Equal(ordered.Count, ordered.Select(b => b.Layer).Distinct().Count());
                var passes = 0;
                var bytes = 0;
                foreach (var b in ordered)
                {
                    passes += b.PassesInLayer;
                    bytes += b.BytesInLayer;
                    Assert.Equal(passes, b.PassesIncluded);
                    Assert.Equal(bytes, b.BytesIncluded);
                }
            }
        }

        [Fact]
        public void ParallelPacketBuilding_ReportsTheSameAsSerial()
        {
            Action<J2KEncoderConfiguration> configure = c =>
                c.WithBitrate(1.0f).WithProgression(p => p.WithQualityLayers(0.5f, 1.0f)).WithTiles(tl => tl.SetSize(96, 96));

            var serial = Encode(configure, threads: 1);

            EBCOTRateAllocator.MinParallelBlocksForCurrentThread = 0;
            try
            {
                var parallel = Encode(configure, threads: 4);
                Assert.Equal(serial.Data, parallel.Data);
                Assert.Equal(Fingerprint(serial.Telemetry!), Fingerprint(parallel.Telemetry!));
            }
            finally
            {
                EBCOTRateAllocator.MinParallelBlocksForCurrentThread = null;
            }
        }

        [Fact]
        public void RegionOfInterest_IsReportedPerBlock()
        {
            var (_, roi) = Encode(c => c.WithMaxBytes(7000).WithROI(new ROIConfiguration().AddMask(-1, Face()).SetStartLevel(3)));
            var (_, plain) = Encode(c => c.WithMaxBytes(7000));

            Assert.All(plain!.CodeBlocks, b => { Assert.Equal(0, b.RoiCoefficients); Assert.Equal(0, b.RoiPasses); });

            var withRegion = roi!.CodeBlocks.Where(b => b.RoiCoefficients > 0).ToList();
            Assert.NotEmpty(withRegion);
            Assert.All(withRegion, b =>
            {
                Assert.True(b.RoiCoefficients <= b.Width * b.Height);
                Assert.Equal(1, b.RoiPasses % 3);                  // a cleanup pass, then whole planes of three
                Assert.True(b.DistortionScale >= 1);
            });
            // Blocks of the levels coded wholly as ROI (start level 3) carry the Maxshift distortion scale; mixed blocks above them do not
            Assert.Contains(withRegion, b => b.DistortionScale > 1);
            Assert.Contains(withRegion, b => b.DistortionScale == 1 && b.RoiCoefficients < b.Width * b.Height);
            Assert.All(roi.CodeBlocks.Where(b => b.RoiCoefficients == 0), b => Assert.Equal(1.0, b.DistortionScale));

            // The expression the encoder guide uses for the bytes of the leading region passes
            var faceBytes = roi.CodeBlocks
                .Where(b => b.RoiCoefficients > 0 && b.RoiPasses > 0)
                .Sum(b => b.PassEndBytes[Math.Min(b.RoiPasses, b.PassesIncluded) - 1]);
            Assert.InRange(faceBytes, 1, roi.PacketBodyBytes);
        }

        [Fact]
        public void DistortionWeights_AreReportedAsTheScale_AndCanBeDividedOut()
        {
            // A limit far above the image's size keeps every pass, in one layer, with or without weights
            Action<J2KEncoderConfiguration> plain = c => c.WithMaxBytes(10_000_000);
            var weights = new DistortionWeights().ForComponent(0, 1.25).Add(0.5, component: 2);

            var (_, none) = Encode(plain);
            var (_, weighted) = Encode(c => { plain(c); c.WithDistortionWeights(weights); });

            Assert.All(none!.CodeBlocks, b => Assert.Equal(1.0, b.DistortionScale));
            Assert.All(weighted!.CodeBlocks, b =>
                Assert.Equal(b.Component == 0 ? 1.25 : b.Component == 2 ? 0.5 : 1.0, b.DistortionScale, 6));

            var reference = none.CodeBlocks.ToDictionary(b => (b.Component, b.ResolutionLevel, b.Subband, b.BlockColumn, b.BlockRow));
            foreach (var b in weighted.CodeBlocks)
            {
                var r = reference[(b.Component, b.ResolutionLevel, b.Subband, b.BlockColumn, b.BlockRow)];
                Assert.Equal(r.DistortionReduction, b.UnscaledDistortionReduction, 3);
                Assert.Equal(r.DistortionReduction * b.DistortionScale, b.DistortionReduction, 3);
            }
        }

        [Fact]
        public void FailedEncode_DoesNotReport()
        {
            var called = false;
            var config = new J2KEncoderConfiguration().WithMaxBytes(50).WithTelemetry(_ => called = true);
            Assert.ThrowsAny<Exception>(() => J2kImage.ToBytes(MakeImage(), null, config.ToParameterList()));
            Assert.False(called);
        }

        [Fact]
        public void TileParts_AreRefused()
        {
            var config = new J2KEncoderConfiguration().WithBitrate(1.0f)
                .WithTiles(t => t.SetSize(128, 128).WithPacketsPerTilePart(4))
                .WithTelemetry(_ => { });
            Assert.Throws<ArgumentException>(() => J2kImage.ToBytes(MakeImage(), null, config.ToParameterList()));
        }

        [Fact]
        public void Builder_PassesTheCallbackOn()
        {
            EncodeTelemetry? seen = null;
            var builder = new CompleteEncoderConfigurationBuilder().ForPortrait(6000).WithTelemetry(t => seen = t);
            var pl = builder.Build().ToParameterList();
            pl["threads"] = "1";
            var data = J2kImage.ToBytes(MakeImage(), null, pl)!;
            Assert.NotNull(seen);
            Assert.Equal(data.Length, seen!.TotalBytes);
        }

        [Fact]
        public void NoCallback_LeavesTheParameterListWithout()
        {
            Assert.Null(new J2KEncoderConfiguration().ToParameterList().TelemetryCallback);
        }
    }
}
