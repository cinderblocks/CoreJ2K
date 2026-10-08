// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Globalization;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.roi;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// <see cref="DistortionWeights"/> steer the rate allocator towards the code-blocks they favour, without changing what the
    /// codestream looks like.
    /// </summary>
    public class DistortionWeightsTests
    {
        private const int Width = 240;
        private const int Height = 320;

        private static InterleavedImageSource MakeImage()
        {
            var rnd = new Random(5);
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

        private static byte[] Encode(J2KEncoderConfiguration config)
        {
            var pl = config.ToParameterList();
            pl["threads"] = "1";
            return J2kImage.ToBytes(MakeImage(), null, pl)!;
        }

        private static J2KEncoderConfiguration Limited(DistortionWeights? weights = null)
        {
            var config = new J2KEncoderConfiguration().WithFileFormat(false).WithMaxBytes(6000);
            return weights == null ? config : config.WithDistortionWeights(weights);
        }

        /// <summary>Mean absolute error of the luma and of the two chroma channels, of a decode against the source.</summary>
        private static (double Luma, double Chroma) DecodeError(byte[] data)
        {
            using var decoded = J2kImage.FromBytes(data);
            var block = new j2k.image.DataBlkInt(0, 0, Width, Height);
            var original = MakeImage();
            var got = Enumerable.Range(0, 3).Select(c => decoded.GetComponent(c)).ToArray();
            var want = Enumerable.Range(0, 3)
                .Select(c => ((j2k.image.DataBlkInt)original.GetInternCompData(block, c)).DataInt.Select(v => v + 128).ToArray())
                .ToArray();

            double luma = 0, chroma = 0;
            for (var i = 0; i < Width * Height; i++)
            {
                double Y(int[] r, int[] g, int[] b, int k) => 0.299 * r[k] + 0.587 * g[k] + 0.114 * b[k];
                double Cb(int[] r, int[] g, int[] b, int k) => -0.168736 * r[k] - 0.331264 * g[k] + 0.5 * b[k];
                double Cr(int[] r, int[] g, int[] b, int k) => 0.5 * r[k] - 0.418688 * g[k] - 0.081312 * b[k];
                luma += Math.Abs(Y(got[0], got[1], got[2], i) - Y(want[0], want[1], want[2], i));
                chroma += Math.Abs(Cb(got[0], got[1], got[2], i) - Cb(want[0], want[1], want[2], i))
                          + Math.Abs(Cr(got[0], got[1], got[2], i) - Cr(want[0], want[1], want[2], i));
            }
            return (luma / (Width * Height), chroma / (2.0 * Width * Height));
        }

        [Fact]
        public void GetWeight_IsTheProductOfTheMatchingRules()
        {
            var weights = new DistortionWeights()
                .ForComponent(0, 2)
                .ForResolution(3, 0.5)
                .ForSubband(WaveletSubband.HH, 4)
                .Add(1.5, component: 1, resolution: 2, subband: WaveletSubband.LH);

            Assert.Equal(1, weights.GetWeight(2, 0, WaveletSubband.LL));
            Assert.Equal(2, weights.GetWeight(0, 0, WaveletSubband.LL));
            Assert.Equal(2 * 0.5, weights.GetWeight(0, 3, WaveletSubband.HL));
            Assert.Equal(2 * 0.5 * 4, weights.GetWeight(0, 3, WaveletSubband.HH));
            Assert.Equal(1.5, weights.GetWeight(1, 2, WaveletSubband.LH));
            Assert.Equal(1, weights.GetWeight(1, 2, WaveletSubband.HL));
            Assert.Equal(1, new DistortionWeights().GetWeight(0, 0, WaveletSubband.LL));
        }

        [Fact]
        public void Validate_FindsBadRules()
        {
            Assert.Empty(new DistortionWeights().ForComponent(0, 1.25).Validate());
            Assert.NotEmpty(new DistortionWeights().ForComponent(0, 100).Validate());
            Assert.NotEmpty(new DistortionWeights().ForComponent(0, 0.01).Validate());
            Assert.NotEmpty(new DistortionWeights().ForComponent(0, double.NaN).Validate());
            Assert.NotEmpty(new DistortionWeights().ForComponent(-2, 1).Validate());
            Assert.NotEmpty(new DistortionWeights().Add(1, resolution: 2, subband: WaveletSubband.LL).Validate());
            Assert.NotEmpty(new DistortionWeights().Add(1, resolution: 0, subband: WaveletSubband.HH).Validate());
            Assert.Empty(new DistortionWeights().Add(1.1, resolution: 0, subband: WaveletSubband.LL).Validate());
        }

        [Fact]
        public void TheOptionRoundTrips()
        {
            var weights = new DistortionWeights()
                .ForComponent(0, 1.25)
                .Add(0.8, component: 1, resolution: 2, subband: WaveletSubband.HL)
                .ForSubband(WaveletSubband.LL, 0.0625);
            var pl = new ParameterList();
            weights.ApplyTo(pl);

            Assert.Equal("1.25:c0 0.8:c1:r2:HL 0.0625:LL", pl["Dweights"]);

            var read = DistortionWeights.FromParameterList(pl)!;
            Assert.Equal(3, read.Rules.Count);
            Assert.Equal(1.25, read.Rules[0].Weight);
            Assert.Equal(0, read.Rules[0].Component);
            Assert.Equal(-1, read.Rules[0].Resolution);
            Assert.Null(read.Rules[0].Subband);
            Assert.Equal(WaveletSubband.HL, read.Rules[1].Subband);
            Assert.Equal(2, read.Rules[1].Resolution);
            Assert.Equal(WaveletSubband.LL, read.Rules[2].Subband);
        }

        [Fact]
        public void TheOptionDoesNotDependOnTheCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var pl = new ParameterList();
                new DistortionWeights().ForComponent(0, 1.25).ApplyTo(pl);
                Assert.Equal("1.25:c0", pl["Dweights"]);
                Assert.Equal(1.25, DistortionWeights.FromParameterList(pl)!.Rules[0].Weight);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Fact]
        public void NoOption_MeansNoWeights()
        {
            Assert.Null(DistortionWeights.FromParameterList(new ParameterList()));
            var pl = new ParameterList();
            new DistortionWeights().ApplyTo(pl);
            Assert.Null(pl.GetParameter("Dweights"));
        }

        [Theory]
        [InlineData("heavy")]
        [InlineData("2:x9")]
        [InlineData("2:c")]
        [InlineData("2:ll")]
        [InlineData("100")]
        [InlineData("2:r1:LL")]
        [InlineData("2:c-1")]
        public void MalformedOption_IsRejected(string text)
        {
            var pl = new J2KEncoderConfiguration().WithMaxBytes(6000).ToParameterList();
            pl["Dweights"] = text;
            var ex = Assert.Throws<ArgumentException>(() => J2kImage.ToBytes(MakeImage(), null, pl));
            Assert.Contains("Dweights", ex.Message);
        }

        [Fact]
        public void RulesNamingAnAbsentComponentOrLevel_AreRejected()
        {
            var component = Assert.ThrowsAny<Exception>(() => Encode(Limited(new DistortionWeights().ForComponent(3, 2))));
            Assert.Contains("component 3", component.Message);

            var level = Assert.ThrowsAny<Exception>(() => Encode(Limited(new DistortionWeights().ForResolution(9, 2))));
            Assert.Contains("resolution level 9", level.Message);
        }

        [Fact]
        public void NeutralWeights_ChangeNothing()
        {
            var plain = Encode(Limited());
            var neutral = Encode(Limited(new DistortionWeights().ForComponent(0, 1).ForSubband(WaveletSubband.HH, 1)));
            Assert.True(plain.AsSpan().SequenceEqual(neutral));
        }

        [Fact]
        public void LumaWeight_MovesBytesFromChromaToLuma()
        {
            var plain = DecodeError(Encode(Limited()));
            var favoured = DecodeError(Encode(Limited(new DistortionWeights().ForComponent(0, 8))));
            var disfavoured = DecodeError(Encode(Limited(new DistortionWeights().ForComponent(0, 0.125))));

            Assert.True(favoured.Chroma > plain.Chroma, $"chroma error {favoured.Chroma:F3} should exceed {plain.Chroma:F3}");
            Assert.True(favoured.Luma <= plain.Luma + 1e-9, $"luma error {favoured.Luma:F3} should not exceed {plain.Luma:F3}");
            Assert.True(disfavoured.Chroma < plain.Chroma, $"chroma error {disfavoured.Chroma:F3} should be below {plain.Chroma:F3}");
            Assert.True(disfavoured.Luma >= plain.Luma - 1e-9, $"luma error {disfavoured.Luma:F3} should not be below {plain.Luma:F3}");
        }

        [Fact]
        public void Weights_KeepToTheByteLimit_AndWorkWithRoi()
        {
            var weights = new DistortionWeights().ForComponent(0, 1.25).ForSubband(WaveletSubband.LL, 1.25);
            var roi = new ROIConfiguration().AddRectangle(-1, 60, 80, 120, 160).SetStartLevel(2);

            var data = Encode(Limited(weights).WithROI(roi));

            Assert.True(data.Length <= 6000, $"{data.Length} bytes");
            Assert.True(HasRgn(data));
        }

        [Fact]
        public void Weights_DoNotChangeASingleLayerLosslessEncode()
        {
            // With several layers the weights change how the passes are divided between them, but not which passes there are.
            byte[] Lossless(DistortionWeights? weights)
            {
                var config = new J2KEncoderConfiguration().WithLossless().WithFileFormat(false);
                if (weights != null) config.WithDistortionWeights(weights);
                var pl = config.ToParameterList();
                pl["Alayers"] = "sl";
                pl["threads"] = "1";
                return J2kImage.ToBytes(MakeImage(), null, pl)!;
            }

            Assert.True(Lossless(null).AsSpan().SequenceEqual(Lossless(new DistortionWeights().ForComponent(0, 4))));
        }

        [Fact]
        public void Configuration_CarriesTheWeights()
        {
            var weights = new DistortionWeights().ForComponent(0, 1.25);
            var config = new J2KEncoderConfiguration().WithDistortionWeights(weights);
            Assert.Same(weights, config.DistortionWeights);
            Assert.Equal("1.25:c0", config.ToParameterList()["Dweights"]);
            Assert.True(config.IsValid);

            Assert.False(new J2KEncoderConfiguration().WithDistortionWeights(new DistortionWeights().ForComponent(0, 50)).IsValid);
            Assert.Same(weights, new CompleteEncoderConfigurationBuilder().WithDistortionWeights(weights).Build().DistortionWeights);
        }

        private static bool HasRgn(byte[] data)
        {
            for (var i = 0; i < data.Length - 1; i++)
                if (data[i] == 0xFF && data[i + 1] == 0x5E) return true;
            return false;
        }
    }
}
