// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.
//
// Decodes lossless tiled codestreams produced by OpenJPEG 2.5.4 (an encoder CoreJ2K did not
// write) and checks every sample against the synthetic source image. The fixtures were generated
// with opj_compress from the image formula in Pixel() below; see TestFiles/opj/README.md.
//
// These guard two decoder defects that only show up when tile origins are not multiples of
// 2^(decomposition levels), which CoreJ2K's own power-of-two tiled round trips never hit:
//  * PktDecoder reused code-block position/size from the previous tile even though identically
//    sized tiles have different subband extents at deeper resolution levels.
//  * PktDecoder's "same geometry" test compared the precinct counts with themselves, so tiles
//    whose precinct counts differ (custom precinct sizes) reused a scaffold of the wrong shape.
//
// The truncated / reduced-resolution tests guard two further defects found while validating those:
//  * InvWTFull rented its reconstruction buffers from ArrayPool without clearing them, so any area
//    that no code-block wrote (truncated stream, skipped blocks) leaked a previous decode's samples.
//  * FileBitstreamReaderAgent indexed the packet-head-length table with a packet whose head was cut
//    short by truncation, throwing IndexOutOfRangeException.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.j2k.util;
using Xunit;

namespace CoreJ2K.Tests
{
    public class OpenJpegTiledInteropTests
    {
        private static readonly string FixtureDir =
            Path.Combine(AppContext.BaseDirectory, "TestFiles", "opj");

        // Must match the generator used to create the fixtures.
        private static int Pixel(int x, int y, int c) =>
            (x * 3 + y * 5 + ((x * y) >> 4) + c * 40 + ((x * 7 + y * 11 + c) % 13)) & 255;

        [Theory]
        [InlineData("gray200_tile100_6res.j2k", 200, 200, 1)]               // tile origin 100: LL3 is 13 wide in tile 0, 12 in tile 1
        [InlineData("gray250x170_tile100x70_rpcl_prec64.j2k", 250, 170, 1)] // custom precincts, RPCL, partial edge tiles
        [InlineData("rgb160x140_tile60_cprl_prec32.j2k", 160, 140, 3)]      // 3 components, CPRL, 32x32 precincts, RCT
        [InlineData("gray250x170_tile96_7res_origin.j2k", 250, 170, 1)]     // 6 decomposition levels + image/tile-grid offsets
        public void OpenJpegTiledLossless_DecodesExactly(string file, int width, int height, int components)
        {
            var image = J2kImage.FromBytes(File.ReadAllBytes(Path.Combine(FixtureDir, file)));

            Assert.Equal(width, image.Width);
            Assert.Equal(height, image.Height);
            Assert.Equal(components, image.NumberOfComponents);

            for (var c = 0; c < components; c++)
            {
                var comp = image.GetComponent(c);
                var mismatches = 0;
                var first = string.Empty;
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var got = comp[y * width + x];
                        var expected = Pixel(x, y, c);
                        if (got == expected) continue;
                        if (mismatches++ == 0)
                            first = $"first at ({x},{y}) comp {c}: expected {expected}, got {got}";
                    }
                }
                Assert.True(mismatches == 0, $"{file}: {mismatches} mismatching samples in component {c}; {first}");
            }
        }

        private static int[][] Decode(byte[] data, string? res = null)
        {
            ParameterList? pl = null;
            if (res != null)
            {
                pl = new ParameterList(J2kImage.GetDefaultDecoderParameterList());
                pl["res"] = res;
            }
            var image = J2kImage.FromBytes(data, pl);
            return Enumerable.Range(0, image.NumberOfComponents).Select(c => (int[])image.GetComponent(c).Clone()).ToArray();
        }

        private static byte[] Truncated(string file, double fraction)
        {
            var bytes = File.ReadAllBytes(Path.Combine(FixtureDir, file));
            return bytes.Take((int)(bytes.Length * fraction)).ToArray();
        }

        private static void AssertSame(int[][] expected, int[][] actual, string what)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (var c = 0; c < expected.Length; c++)
                Assert.True(expected[c].AsSpan().SequenceEqual(actual[c]), $"{what}: component {c} differs between identical decodes");
        }

        public static TheoryData<string, double> TruncationCases()
        {
            var data = new TheoryData<string, double>();
            foreach (var file in new[]
                     {
                         "gray200_tile100_6res.j2k", "gray250x170_tile100x70_rpcl_prec64.j2k",
                         "rgb160x140_tile60_cprl_prec32.j2k", "gray250x170_tile96_7res_origin.j2k"
                     })
                foreach (var fraction in new[] { 0.15, 0.3, 0.45, 0.6, 0.75, 0.9, 0.97 })
                    data.Add(file, fraction);
            return data;
        }

        // Truncated tiled streams with custom precincts used to throw IndexOutOfRangeException when the
        // cut fell inside a packet head. A truncated decode is a legitimate partial decode, not an error.
        [Theory]
        [MemberData(nameof(TruncationCases))]
        public void TruncatedStream_DecodesWithoutThrowing(string file, double fraction)
        {
            var exception = Record.Exception(() => Decode(Truncated(file, fraction)));
            Assert.Null(exception);
        }

        // Output must not depend on what was decoded earlier in the process: decode the truncated stream,
        // then a complete stream (leaving the pooled buffers full of other samples), then the truncated
        // stream again, both at full and at reduced resolution.
        [Theory]
        [InlineData("gray200_tile100_6res.j2k", null)]
        [InlineData("gray200_tile100_6res.j2k", "2")]
        [InlineData("gray250x170_tile100x70_rpcl_prec64.j2k", null)]
        [InlineData("rgb160x140_tile60_cprl_prec32.j2k", "3")]
        [InlineData("gray250x170_tile96_7res_origin.j2k", "1")]
        public void TruncatedOrReducedDecode_IsIndependentOfEarlierDecodes(string file, string? res)
        {
            var truncated = Truncated(file, 0.6);
            var complete = File.ReadAllBytes(Path.Combine(FixtureDir, file));

            var first = Decode(truncated, res);
            Decode(complete);
            var second = Decode(truncated, res);

            AssertSame(first, second, $"{file} (res {res ?? "full"})");
        }
    }
}
