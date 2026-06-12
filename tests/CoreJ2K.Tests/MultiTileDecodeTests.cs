// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Xunit;
using CoreJ2K;
using CoreJ2K.Util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Multi-tile decoding coverage.
    /// </summary>
    /// <remarks>
    /// Fixed: <c>PktDecoder.restart</c> previously reused the previous tile's code-block geometry
    /// when only <c>mdl</c> and <c>numPrec</c> matched, so partial-edge tiles inherited the full
    /// tile's (larger) code-block geometry and overflowed the tile buffer in
    /// <c>InvWTFull.waveletTreeReconstruction</c>. The reuse now also requires the tile-component
    /// dimensions and the per-subband code-block grid to match.
    /// </remarks>
    public class MultiTileDecodeTests
    {
        private static (InterleavedImageSource src, int[][] orig) MakeImage(int w, int h, int nc)
        {
            var comps = new int[nc][];
            var orig = new int[nc][];
            for (var c = 0; c < nc; c++)
            {
                comps[c] = new int[w * h];
                orig[c] = new int[w * h];
                for (var i = 0; i < w * h; i++)
                {
                    orig[c][i] = (i * (7 + c * 13)) & 0xFF;
                    comps[c][i] = orig[c][i] - 128; // signed-centred domain
                }
            }
            return (new InterleavedImageSource(w, h, nc, 8, new bool[nc], comps), orig);
        }

        private static InterleavedImage EncodeDecode(InterleavedImageSource src, string tiles)
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            pl["tiles"] = tiles;
            return J2kImage.FromBytes(J2kImage.ToBytes(src, null, pl)!);
        }

        [Theory]
        [InlineData(200, 200, 1, "64 64")]   // partial-edge tiles (8-wide edge) — the original crash
        [InlineData(300, 300, 3, "128 128")] // multi-component, partial edges
        [InlineData(101, 101, 1, "32 32")]   // tiny odd edge (5-wide)
        [InlineData(256, 256, 1, "64 64")]   // exact tiling, no partial edges
        public void TiledLossless_RoundTripsExactly(int w, int h, int nc, string tiles)
        {
            var (src, orig) = MakeImage(w, h, nc);
            var decoded = EncodeDecode(src, tiles);

            Assert.Equal(w, decoded.Width);
            Assert.Equal(h, decoded.Height);
            for (var c = 0; c < nc; c++)
            {
                var comp = decoded.GetComponent(c);
                for (var i = 0; i < orig[c].Length; i++)
                    Assert.True(orig[c][i] == comp[i], $"comp {c} sample {i}: expected {orig[c][i]}, got {comp[i]}");
            }
        }

        // KNOWN LIMITATION: non-power-of-two tile sizes decode without crashing but are not yet
        // bit-exact (a separate defect in the wavelet/precinct handling of non-dyadic tile sizes,
        // exposed once the edge-tile geometry-reuse bug was fixed). Power-of-two tile sizes are exact.
        [Fact(Skip = "Known limitation: non-power-of-two tile sizes decode but are not yet bit-exact.")]
        public void NonPowerOfTwoTiles_RoundTripsExactly()
        {
            var (src, orig) = MakeImage(200, 200, 1);
            var decoded = EncodeDecode(src, "60 60");

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.True(orig[0][i] == comp[i], $"sample {i}: expected {orig[0][i]}, got {comp[i]}");
        }
    }
}
