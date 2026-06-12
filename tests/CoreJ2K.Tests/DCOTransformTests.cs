// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Xunit;
using CoreJ2K;
using CoreJ2K.Util;
using CoreJ2K.j2k.codestream;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Round-trip and serialization tests for the DCO (Variable DC Offset) marker segment
    /// (ISO/IEC 15444-2, marker 0xFF70).
    /// </summary>
    public class DCOTransformTests
    {
        // Helper: build an InterleavedImageSource with a fixed ramp pattern in signed-centred domain.
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
                    orig[c][i] = (i * (7 + c * 11)) & 0xFF;
                    comps[c][i] = orig[c][i] - 128; // signed-centred domain
                }
            }
            return (new InterleavedImageSource(w, h, nc, 8, new bool[nc], comps), orig);
        }

        private static InterleavedImage EncodeDecode(
            InterleavedImageSource src, DCOMarkerSegment dco, string tiles = "64 64")
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            pl["tiles"] = tiles;
            var bytes = J2kImage.ToBytes(src, null, pl, null, null, dco);
            return J2kImage.FromBytes(bytes!);
        }

        [Fact]
        public void DCO_SingleComponent_RoundTripsExactly()
        {
            var (src, orig) = MakeImage(64, 64, 1);
            var dco = new DCOMarkerSegment { Offsets = new[] { 10 } };

            var decoded = EncodeDecode(src, dco);

            Assert.Equal(64, decoded.Width);
            Assert.Equal(64, decoded.Height);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.True(orig[0][i] == comp[i], $"sample {i}: expected {orig[0][i]}, got {comp[i]}");
        }

        [Fact]
        public void DCO_MultiComponent_PerChannelOffsets_RoundTripsExactly()
        {
            var (src, orig) = MakeImage(32, 32, 3);
            // Different offsets per channel including negative
            var dco = new DCOMarkerSegment { Offsets = new[] { 5, -3, 20 } };

            var decoded = EncodeDecode(src, dco);

            Assert.Equal(3, decoded.NumberOfComponents);
            for (var c = 0; c < 3; c++)
            {
                var comp = decoded.GetComponent(c);
                for (var i = 0; i < orig[c].Length; i++)
                    Assert.True(orig[c][i] == comp[i], $"comp {c} sample {i}: expected {orig[c][i]}, got {comp[i]}");
            }
        }

        [Fact]
        public void DCO_ZeroOffsets_PassesThrough()
        {
            var (src, orig) = MakeImage(48, 48, 1);
            var dco = new DCOMarkerSegment { Offsets = new[] { 0 } };

            var decoded = EncodeDecode(src, dco);

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.True(orig[0][i] == comp[i], $"sample {i}: expected {orig[0][i]}, got {comp[i]}");
        }

        [Fact]
        public void DCO_LargeOffset_FitsInShort_RoundTripsExactly()
        {
            var (src, orig) = MakeImage(32, 32, 1);
            // Offset large enough to require 2-byte encoding
            var dco = new DCOMarkerSegment { Offsets = new[] { 200 } };

            var decoded = EncodeDecode(src, dco);

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.True(orig[0][i] == comp[i], $"sample {i}: expected {orig[0][i]}, got {comp[i]}");
        }

        [Fact]
        public void DCOMarkerSegment_SerializesAndDeserializes()
        {
            var orig = new DCOMarkerSegment { Offsets = new[] { 10, -5, 127 } };
            var bytes = orig.ToBytes();
            var roundTripped = DCOMarkerSegment.FromBytes(bytes);

            Assert.Equal(orig.Offsets.Length, roundTripped.Offsets.Length);
            for (var i = 0; i < orig.Offsets.Length; i++)
                Assert.Equal(orig.Offsets[i], roundTripped.Offsets[i]);
        }

        [Fact]
        public void DCOMarkerSegment_LargeOffsets_ChoosesCorrectWidth()
        {
            // Values in [128..255] need 2 bytes (sbyte max = 127)
            var seg = new DCOMarkerSegment { Offsets = new[] { 200 } };
            var bytes = seg.ToBytes();
            var rt = DCOMarkerSegment.FromBytes(bytes);
            Assert.Equal(200, rt.Offsets[0]);

            // Values in [32768..] need 4 bytes
            var seg2 = new DCOMarkerSegment { Offsets = new[] { 40000 } };
            var bytes2 = seg2.ToBytes();
            var rt2 = DCOMarkerSegment.FromBytes(bytes2);
            Assert.Equal(40000, rt2.Offsets[0]);
        }

        [Fact]
        public void DCO_WithMultiTile_RoundTripsExactly()
        {
            var (src, orig) = MakeImage(128, 128, 1);
            var dco = new DCOMarkerSegment { Offsets = new[] { 7 } };

            var decoded = EncodeDecode(src, dco, tiles: "64 64");

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.True(orig[0][i] == comp[i], $"sample {i}: expected {orig[0][i]}, got {comp[i]}");
        }
    }
}
