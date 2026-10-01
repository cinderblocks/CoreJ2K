// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The forward wavelet transform analyses columns in blocks of 16 for cache efficiency. These round trips check that blocking
    /// changes no sample: lossless coding is only exact if every column of the 5/3 analysis is right, and lossy 9/7 coding at a very
    /// high rate reconstructs closely only if the 9/7 analysis is right. The shapes sit on both sides of the block boundary.
    /// </summary>
    public class ForwardTransformTests
    {
        private static int[] Samples(int width, int height, int seed)
        {
            var rnd = new Random(seed);
            var samples = new int[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    samples[y * width + x] = Math.Clamp(128 + (int)(70 * Math.Sin(x / 5.0) * Math.Cos(y / 7.0)) + rnd.Next(-25, 26), 0, 255) - 128;
            return samples;
        }

        private static int[] RoundTrip(int[] samples, int width, int height, Action<ParameterList> configure)
        {
            var source = new InterleavedImageSource(width, height, 1, 8, new[] { false }, new[] { (int[])samples.Clone() });
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            configure(pl);
            using var decoded = J2kImage.FromBytes(J2kImage.ToBytes(source, null, pl)!);
            return (int[])decoded.GetComponent(0).Clone();
        }

        public static TheoryData<int, int> Shapes()
        {
            var data = new TheoryData<int, int>();
            // Every width from 1 to 40 against heights either side of the 16-row/column block, odd and even.
            foreach (var width in new[] { 1, 2, 3, 7, 8, 15, 16, 17, 18, 31, 32, 33, 34, 40 })
                foreach (var height in new[] { 1, 2, 15, 16, 17, 33 })
                    data.Add(width, height);
            // Larger, odd-sized and not multiples of the block.
            data.Add(100, 15);
            data.Add(203, 157);
            data.Add(257, 5);
            data.Add(5, 257);
            return data;
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void LosslessRoundTrip_IsExact(int width, int height)
        {
            var samples = Samples(width, height, 11);
            foreach (var levels in new[] { 1, 3, 5 })
            {
                var decoded = RoundTrip(samples, width, height, pl => { pl["lossless"] = "on"; pl["Wlev"] = levels.ToString(); });
                for (var i = 0; i < samples.Length; i++)
                    Assert.True(decoded[i] == samples[i] + 128, $"{width}x{height}, {levels} level(s): sample {i} is {decoded[i]}, expected {samples[i] + 128}");
            }
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void LossyRoundTrip_AtAVeryHighRate_ReconstructsClosely(int width, int height)
        {
            // A rate-limited codestream needs room for its own headers, so very small images are only tested lossless.
            if (width * height < 256) return;

            var samples = Samples(width, height, 12);
            var decoded = RoundTrip(samples, width, height, pl => { pl["lossless"] = "off"; pl["rate"] = "64.0"; pl["Wlev"] = "3"; });

            double squaredError = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var difference = decoded[i] - (samples[i] + 128);
                squaredError += (double)difference * difference;
            }
            var mse = squaredError / samples.Length;
            var psnr = mse == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255.0 / mse);
            Assert.True(psnr > 45, $"{width}x{height}: PSNR {psnr:F1} dB is too low for a 64 bpp 9/7 round trip");
        }
    }
}
