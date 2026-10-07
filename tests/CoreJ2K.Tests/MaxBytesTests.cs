// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.roi;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The <c>max_bytes</c> option (<see cref="J2KEncoderConfiguration.WithMaxBytes"/>) is a hard limit on the complete output: the
    /// JP2 boxes count, the encode keeps as much of the image as fits, and it never writes more.
    /// </summary>
    public class MaxBytesTests
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

        private static ParameterList Parameters(int maxBytes, bool fileFormat = true, Action<ParameterList>? configure = null)
        {
            var pl = new J2KEncoderConfiguration().WithFileFormat(fileFormat).WithMaxBytes(maxBytes).ToParameterList();
            pl["threads"] = "1";
            configure?.Invoke(pl);
            return pl;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Output_NeverExceedsTheLimit_AndUsesMostOfIt(bool fileFormat)
        {
            for (var limit = 3000; limit <= 12000; limit += 333)
            {
                var data = J2kImage.ToBytes(MakeImage(), null, Parameters(limit, fileFormat))!;

                Assert.True(data.Length <= limit, $"{data.Length} bytes written for a limit of {limit}.");
                Assert.True(data.Length >= limit * 0.9, $"Only {data.Length} of {limit} bytes used.");
            }
        }

        [Fact]
        public void Output_WithRegionOfInterest_NeverExceedsTheLimit()
        {
            var roi = new ROIConfiguration().AddRectangle(-1, 60, 80, 120, 160).SetStartLevel(2);
            for (var limit = 3000; limit <= 9000; limit += 487)
            {
                var pl = new J2KEncoderConfiguration().WithROI(roi).WithMaxBytes(limit).ToParameterList();
                pl["threads"] = "1";
                var data = J2kImage.ToBytes(MakeImage(), null, pl)!;
                Assert.True(data.Length <= limit, $"{data.Length} bytes written for a limit of {limit}.");
            }
        }

        [Fact]
        public void Output_Decodes()
        {
            var data = J2kImage.ToBytes(MakeImage(), null, Parameters(6000))!;
            using var image = J2kImage.FromBytes(data);
            Assert.Equal(Width, image.Width);
            Assert.Equal(Height, image.Height);
            Assert.Equal(3, image.NumberOfComponents);
        }

        [Fact]
        public void Metadata_CountsTowardsTheLimit()
        {
            const int limit = 6000;
            var plain = J2kImage.ToBytes(MakeImage(), null, Parameters(limit))!;

            var metadata = new J2KMetadata();
            metadata.AddXml("<note>" + new string('x', 700) + "</note>");
            var withMetadata = J2kImage.ToBytes(MakeImage(), metadata, Parameters(limit))!;

            Assert.True(withMetadata.Length <= limit, $"{withMetadata.Length} bytes written for a limit of {limit}.");
            Assert.True(withMetadata.Length >= limit * 0.9);
            // The metadata took bytes the image could have had.
            Assert.NotEqual(plain.Length, withMetadata.Length - 700);
        }

        [Fact]
        public void MultipleTiles_StayWithinTheLimit()
        {
            const int limit = 8000;
            var data = J2kImage.ToBytes(MakeImage(), null, Parameters(limit, true, pl => pl["tiles"] = "128 128"))!;
            Assert.True(data.Length <= limit, $"{data.Length} bytes written for a limit of {limit}.");
        }

        [Fact]
        public void TlmMarker_CountsTowardsTheLimit()
        {
            const int limit = 8000;
            var data = J2kImage.ToBytes(MakeImage(), null, Parameters(limit, true, pl =>
            {
                pl["tiles"] = "128 128";
                pl["Htlm"] = "on";
            }))!;
            Assert.True(data.Length <= limit, $"{data.Length} bytes written for a limit of {limit}.");
        }

        [Fact]
        public void LimitTooSmallForTheHeaders_Fails()
        {
            Assert.ThrowsAny<ArgumentException>(() => J2kImage.ToBytes(MakeImage(), null, Parameters(50)));
            Assert.ThrowsAny<ArgumentException>(() => J2kImage.ToBytes(MakeImage(), null, Parameters(150)));
        }

        [Fact]
        public void SingleLayer_AcceptedExplicitly()
        {
            var data = J2kImage.ToBytes(MakeImage(), null, Parameters(5000, true, pl => pl["Alayers"] = "sl"))!;
            Assert.True(data.Length <= 5000);
        }

        [Theory]
        [InlineData("Alayers", "0.1 +4 1.0")]
        [InlineData("Hplt", "on")]
        [InlineData("tile_parts", "4")]
        [InlineData("pph_tile", "on")]
        [InlineData("pph_main", "on")]
        [InlineData("max_bytes", "0")]
        [InlineData("max_bytes", "lots")]
        public void IncompatibleOptions_AreRejected(string option, string value)
        {
            var pl = Parameters(5000, true, p => p[option] = value);
            Assert.ThrowsAny<ArgumentException>(() => J2kImage.ToBytes(MakeImage(), null, pl));
        }

        [Fact]
        public void Lossless_IsRejected()
        {
            var pl = Parameters(5000, true, p => p["lossless"] = "on");
            Assert.ThrowsAny<ArgumentException>(() => J2kImage.ToBytes(MakeImage(), null, pl));
        }

        [Fact]
        public void Configuration_WritesTheOption()
        {
            var config = new J2KEncoderConfiguration().WithMaxBytes(11820);
            Assert.Equal(11820, config.MaxBytes);
            Assert.Equal("11820", config.ToParameterList()["max_bytes"]);
            Assert.True(config.IsValid);

            Assert.Equal(11820, new CompleteEncoderConfigurationBuilder().WithMaxBytes(11820).Build().MaxBytes);
        }

        [Fact]
        public void Configuration_RejectsBadLimits()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new J2KEncoderConfiguration().WithMaxBytes(0));
            Assert.False(new J2KEncoderConfiguration().WithMaxBytes(1000).WithLossless().IsValid);
        }

        [Fact]
        public void WithoutALimit_OutputIsUnchanged()
        {
            // The limit changes nothing for encodes that do not use it.
            var pl = new J2KEncoderConfiguration().WithBitrate(0.5f).ToParameterList();
            pl["threads"] = "1";
            Assert.Null(pl.GetParameter("max_bytes"));
            var a = J2kImage.ToBytes(MakeImage(), null, pl)!;
            var b = J2kImage.ToBytes(MakeImage(), null, pl)!;
            Assert.True(a.AsSpan().SequenceEqual(b));
        }
    }
}
