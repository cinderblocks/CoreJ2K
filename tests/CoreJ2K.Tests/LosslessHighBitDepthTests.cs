// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// A lossless encode must reproduce every sample. The last coding passes of a code-block carry a distortion estimate that can fall
    /// below an earlier pass's, and the final layer must still keep them; high bit-depth images and tiles with a short last row
    /// exercise this.
    /// </summary>
    public class LosslessHighBitDepthTests
    {
        private static int[][] Samples(int width, int height, int components, int bits, bool noise)
        {
            var rnd = new Random(11);
            var max = (1 << bits) - 1;
            var half = 1 << (bits - 1);
            var comps = new int[components][];
            for (var c = 0; c < components; c++)
            {
                comps[c] = new int[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var v = noise
                            ? rnd.Next(0, max + 1)
                            : half + (int)(half * 0.5 * Math.Sin(x / (5.0 + c)) * Math.Cos(y / 6.0))
                              + (x > width / 2 && y > height / 2 ? half / 3 : 0) + rnd.Next(-(half / 10), half / 10 + 1);
                        comps[c][y * width + x] = Math.Clamp(v, 0, max);
                    }
            }
            return comps;
        }

        private static int Differences(int width, int height, int components, int bits, bool noise, Action<J2KEncoderConfiguration> configure,
            Action<j2k.util.ParameterList>? tweak = null)
        {
            var samples = Samples(width, height, components, bits, noise);
            var half = 1 << (bits - 1);
            var source = new InterleavedImageSource(width, height, components, bits, new bool[components],
                samples.Select(c => c.Select(v => v - half).ToArray()).ToArray());

            var config = new J2KEncoderConfiguration().WithLossless();
            configure(config);
            var pl = config.ToParameterList();
            pl["threads"] = "1";
            tweak?.Invoke(pl);

            var decoded = J2kImage.FromBytes(J2kImage.ToBytes(source, null, pl)!);
            return Enumerable.Range(0, components).Sum(c => decoded.GetComponent(c).Zip(samples[c]).Count(p => p.First != p.Second));
        }

        [Fact]
        public void SixteenBitNoise_InOneSmallTile_IsExact()
        {
            // 5 levels on 20 rows leaves a 2x1 HH block whose later passes have a negative slope
            Assert.Equal(0, Differences(50, 20, 1, 16, true, _ => { }));
        }

        [Theory]
        [InlineData(12, 3)]
        [InlineData(13, 3)]
        [InlineData(16, 1)]
        public void HighBitDepth_WithShortLastTiles_IsExact(int bits, int components)
        {
            Assert.Equal(0, Differences(200, 160, components, bits, false, c => c.WithTiles(t => t.SetSize(50, 70))));
        }

        [Fact]
        public void HighBitDepth_WithLayers_IsExact()
        {
            Assert.Equal(0, Differences(200, 160, 3, 12, false,
                c => c.WithTiles(t => t.SetSize(50, 70)).WithProgression(p => p.WithQualityLayers(0.5f, 1f, 2f))));
        }

        [Fact]
        public void HighBitDepth_WithWeightsAndRoi_IsExact()
        {
            Assert.Equal(0, Differences(200, 160, 3, 12, false,
                c => c.WithTiles(t => t.SetSize(50, 70))
                    .WithDistortionWeights(new j2k.encoder.DistortionWeights().ForComponent(0, 2))
                    .WithROI(new j2k.roi.ROIConfiguration().AddRectangle(-1, 40, 30, 100, 80).SetStartLevel(2))));
        }

        [Fact]
        public void EveryBlockKeepsAllItsPasses_InALosslessEncode()
        {
            var samples = Samples(50, 20, 1, 16, true);
            var source = new InterleavedImageSource(50, 20, 1, 16, new bool[1], new[] { samples[0].Select(v => v - 32768).ToArray() });
            EncodeTelemetry? seen = null;
            var pl = new J2KEncoderConfiguration().WithLossless().WithTelemetry(t => seen = t).ToParameterList();
            pl["threads"] = "1";
            J2kImage.ToBytes(source, null, pl);

            Assert.NotNull(seen);
            var last = seen!.CodeBlocks.GroupBy(b => (b.Component, b.ResolutionLevel, b.Subband, b.BlockColumn, b.BlockRow))
                .Select(g => g.OrderBy(b => b.Layer).Last());
            Assert.All(last, b => Assert.Equal(b.TotalPasses, b.PassesIncluded));
        }
    }
}
