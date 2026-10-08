// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// A file whose components differ in wavelet filter (5/3 with reversible quantization on some, 9/7 on others) decodes: the
    /// components then come back as different data types.
    /// </summary>
    public class MixedComponentDecodeTests
    {
        [Theory]
        [InlineData("w9x7 c1 w5x3", "expounded c1 reversible", "02")]
        [InlineData("w5x3 c0 w9x7", "reversible c0 expounded", "0")]
        [InlineData("w9x7 c1-2 w5x3", "expounded c1-2 reversible", "0")]
        public void MixedFilters_Decode(string filters, string types, string lossyComponents)
        {
            const int w = 64, h = 64;
            var rnd = new Random(3);
            var samples = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                samples[c] = new int[w * h];
                for (var i = 0; i < w * h; i++)
                    samples[c][i] = Math.Clamp(128 + (int)(60 * Math.Sin(i % w / (6.0 + c))) + rnd.Next(-10, 11), 0, 255);
            }

            var pl = new ParameterList(J2kImage.GetDefaultEncoderParameterList())
            {
                ["Mct"] = "off",
                ["Ffilters"] = filters,
                ["Qtype"] = types,
                ["rate"] = "-1",
                ["threads"] = "1",
            };
            var source = new InterleavedImageSource(w, h, 3, 8, new bool[3], samples.Select(c => c.Select(v => v - 128).ToArray()).ToArray());
            var decoded = J2kImage.FromBytes(J2kImage.ToBytes(source, null, pl)!);

            for (var c = 0; c < 3; c++)
            {
                var worst = samples[c].Zip(decoded.GetComponent(c), (a, b) => Math.Abs(a - b)).Max();
                if (lossyComponents.Contains(c.ToString()))
                    Assert.InRange(worst, 0, 8); // 9/7 with the default step
                else
                    Assert.Equal(0, worst);      // 5/3 with reversible quantization is exact
            }
        }
    }
}
