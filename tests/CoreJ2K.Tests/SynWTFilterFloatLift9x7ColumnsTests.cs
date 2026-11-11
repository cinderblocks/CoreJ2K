// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.wavelet.synthesis;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// <see cref="SynWTFilterFloatLift9x7.SynthetizeColumns"/> synthesises several columns at once, along rows. It must give,
    /// to the bit, what the one-column methods give for each column, for every length (the boundaries differ for 1, 2, odd and even
    /// lengths), for both subband orders, and for widths on either side of a SIMD vector.
    /// </summary>
    public class SynWTFilterFloatLift9x7ColumnsTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MatchesTheOneColumnFilter_BitForBit(bool lowFirst)
        {
            var filter = new SynWTFilterFloatLift9x7();
            var rnd = new Random(lowFirst ? 1 : 2);

            for (var h = 1; h <= 41; h++)
            {
                foreach (var n in new[] { 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 32, 33 })
                {
                    const int stride = 40, outBase = 3;
                    var input = new float[h * n]; // row by row, in subband order
                    for (var i = 0; i < input.Length; i++) input[i] = (float)((rnd.NextDouble() - 0.5) * 4096);

                    // expected: each column through the one-column filter
                    var expected = new float[outBase + h * stride];
                    var firstLen = lowFirst ? (h + 1) / 2 : h / 2;
                    for (var x = 0; x < n; x++)
                    {
                        var column = new float[h];
                        for (var i = 0; i < h; i++) column[i] = input[i * n + x];
                        var output = new float[h];
                        if (lowFirst)
                            filter.synthetize_lpf(column, 0, firstLen, 1, column, firstLen, h - firstLen, 1, output, 0, 1);
                        else
                            filter.synthetize_hpf(column, 0, firstLen, 1, column, firstLen, h - firstLen, 1, output, 0, 1);
                        for (var r = 0; r < h; r++) expected[outBase + r * stride + x] = output[r];
                    }

                    var actual = new float[outBase + h * stride];
                    SynWTFilterFloatLift9x7.SynthetizeColumns(lowFirst, (float[])input.Clone(), n, h, actual, outBase, stride);

                    for (var i = 0; i < expected.Length; i++)
                    {
                        if (BitConverter.SingleToInt32Bits(expected[i]) != BitConverter.SingleToInt32Bits(actual[i]))
                            Assert.Fail($"h={h} n={n}: sample {i} is {actual[i]:R}, the one-column filter gives {expected[i]:R}");
                    }
                }
            }
        }
    }
}
