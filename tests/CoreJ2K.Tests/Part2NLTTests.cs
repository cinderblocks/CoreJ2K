// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using Xunit;
using CoreJ2K;
using CoreJ2K.j2k.codestream;
using CoreJ2K.Util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for the JPEG 2000 Part 2 (ISO/IEC 15444-2) Non-linearity point transformation
    /// (NLT): marker serialization, the point-transform math, and a full encode/decode
    /// round-trip through the codec pipeline.
    /// </summary>
    public class Part2NLTTests
    {
        #region Marker serialization

        [Fact]
        public void NltMarker_Gamma_RoundTrips()
        {
            var seg = new NLTMarkerSegment
            {
                ComponentIndex = 1,
                BitDepth = 12,
                Signed = true,
                Type = NLTType.Gamma,
                GammaExponent = 2.2
            };

            var parsed = NLTMarkerSegment.FromBytes(seg.ToBytes());

            Assert.Equal(1, parsed.ComponentIndex);
            Assert.Equal(12, parsed.BitDepth);
            Assert.True(parsed.Signed);
            Assert.Equal(NLTType.Gamma, parsed.Type);
            Assert.Equal(2.2, parsed.GammaExponent, 3);
        }

        [Fact]
        public void NltMarker_Lut_RoundTrips()
        {
            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = 255 - i;

            var seg = new NLTMarkerSegment
            {
                ComponentIndex = NLTMarkerSegment.AllComponents,
                BitDepth = 8,
                Signed = false,
                Type = NLTType.LookupTable,
                Lut = lut
            };

            var parsed = NLTMarkerSegment.FromBytes(seg.ToBytes());

            Assert.Equal(NLTMarkerSegment.AllComponents, parsed.ComponentIndex);
            Assert.Equal(8, parsed.BitDepth);
            Assert.Equal(NLTType.LookupTable, parsed.Type);
            Assert.Equal(lut, parsed.Lut);
        }

        #endregion

        #region Transform math

        [Fact]
        public void Lut_ForwardThenInverse_IsExactForPermutation()
        {
            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = (i * 7 + 13) & 0xFF; // bijection on [0,255]

            var seg = new NLTMarkerSegment { BitDepth = 8, Signed = false, Type = NLTType.LookupTable, Lut = lut };

            for (var v = 0; v < 256; v++)
                Assert.Equal(v, seg.InverseSample(seg.ForwardSample(v)));
        }

        [Fact]
        public void Lut_IsIdentityOutsideDomain()
        {
            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = 255 - i;
            var seg = new NLTMarkerSegment { BitDepth = 8, Signed = false, Type = NLTType.LookupTable, Lut = lut };

            Assert.Equal(-50, seg.InverseSample(-50));
            Assert.Equal(1000, seg.ForwardSample(1000));
        }

        [Fact]
        public void Gamma_ForwardThenInverse_IsNearLossless()
        {
            var seg = new NLTMarkerSegment { BitDepth = 8, Signed = false, Type = NLTType.Gamma, GammaExponent = 2.2 };

            var maxErr = 0;
            for (var v = 0; v < 256; v++)
                maxErr = Math.Max(maxErr, Math.Abs(v - seg.InverseSample(seg.ForwardSample(v))));

            // Rounding through the gamma curve should recover values within a small tolerance.
            Assert.True(maxErr <= 1, $"max round-trip error {maxErr} exceeded tolerance");
        }

        #endregion

        #region Full pipeline round-trip

        [Fact]
        public void Pipeline_LosslessBaseline_RoundTripsExactly()
        {
            // The coding pipeline works in a DC level-shifted (signed-centred) domain, so a
            // source must supply signed-centred samples (value - 2^(B-1)); the decoder adds
            // the centre back. This baseline confirms lossless behaviour without NLT.
            const int w = 32, h = 32;
            var pixels = Gradient(w, h);
            var source = new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { Centre(pixels) });

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, null);
            var decoded = J2kImage.FromBytes(encoded);

            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        [Fact]
        public void Pipeline_WithLutNlt_RoundTripsExactly()
        {
            const int w = 32, h = 32;
            var pixels = Gradient(w, h);
            var source = new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { Centre(pixels) });

            // A non-trivial bijection of the signed-centred domain [-128, 127]; if the inverse
            // NLT were not applied on decode, the reconstructed samples would be the
            // forward-transformed values rather than the originals.
            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = ((i * 7 + 13) & 0xFF) - 128;

            var seg = new NLTMarkerSegment
            {
                ComponentIndex = NLTMarkerSegment.AllComponents,
                BitDepth = 8,
                Signed = true, // segment domain matches the signed-centred pipeline domain
                Type = NLTType.LookupTable,
                Lut = lut
            };

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, new List<NLTMarkerSegment> { seg });
            var decoded = J2kImage.FromBytes(encoded);

            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        [Fact]
        public void Diagnostic_ForwThenInv_OverSource_IsIdentity()
        {
            const int w = 4, h = 1;
            var signed = new[] { -128, -64, 0, 63 };
            var source = new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { signed });

            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = ((i * 7 + 13) & 0xFF) - 128;
            var seg = new NLTMarkerSegment
            {
                ComponentIndex = NLTMarkerSegment.AllComponents,
                BitDepth = 8,
                Signed = true,
                Type = NLTType.LookupTable,
                Lut = lut
            };
            var segs = new List<NLTMarkerSegment> { seg };

            var fwd = new CoreJ2K.j2k.image.nlt.ForwNLT(source, segs);
            var fwdBlk = (CoreJ2K.j2k.image.DataBlkInt)fwd.GetCompData(
                new CoreJ2K.j2k.image.DataBlkInt(0, 0, w, h), 0);

            // Forward output must equal the model's ForwardSample for each input.
            for (var i = 0; i < w; i++)
                Assert.Equal(seg.ForwardSample(signed[i]), fwdBlk.DataInt[fwdBlk.offset + i]);

            var inv = new CoreJ2K.j2k.image.nlt.InvNLT(fwd, segs);
            var invBlk = (CoreJ2K.j2k.image.DataBlkInt)inv.GetCompData(
                new CoreJ2K.j2k.image.DataBlkInt(0, 0, w, h), 0);

            for (var i = 0; i < w; i++)
                Assert.Equal(signed[i], invBlk.DataInt[invBlk.offset + i]);
        }

        [Fact]
        public void Diagnostic_SignedLutMarker_RoundTrips()
        {
            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = ((i * 7 + 13) & 0xFF) - 128;

            var seg = new NLTMarkerSegment
            {
                ComponentIndex = NLTMarkerSegment.AllComponents,
                BitDepth = 8,
                Signed = true,
                Type = NLTType.LookupTable,
                Lut = lut
            };

            var parsed = NLTMarkerSegment.FromBytes(seg.ToBytes());

            Assert.True(parsed.Signed);
            Assert.Equal(lut, parsed.Lut);
            // The decode-side inverse must undo the encode-side forward.
            Assert.Equal(-128, parsed.InverseSample(seg.ForwardSample(-128)));
        }

        [Fact]
        public void Diagnostic_EncodedCodestream_ContainsNltAndCapMarkers()
        {
            const int w = 16, h = 16;
            var pixels = Gradient(w, h);
            var source = new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { Centre(pixels) });

            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = ((i * 7 + 13) & 0xFF) - 128;
            var seg = new NLTMarkerSegment
            {
                ComponentIndex = NLTMarkerSegment.AllComponents,
                BitDepth = 8,
                Signed = true,
                Type = NLTType.LookupTable,
                Lut = lut
            };

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, new List<NLTMarkerSegment> { seg });

            Assert.True(ContainsMarker(encoded, 0xFF76), "encoded codestream is missing the NLT marker");
            Assert.True(ContainsMarker(encoded, 0xFF50), "encoded codestream is missing the CAP marker");
        }

        [Fact]
        public void Diagnostic_IdentityNlt_Pipeline_RoundTrips()
        {
            const int w = 16, h = 16;
            var pixels = Gradient(w, h);
            var source = new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { Centre(pixels) });

            // Identity LUT in the signed-centred domain: transformed value == original value.
            var lut = new int[256];
            for (var i = 0; i < 256; i++) lut[i] = i - 128;
            var seg = new NLTMarkerSegment
            {
                ComponentIndex = NLTMarkerSegment.AllComponents,
                BitDepth = 8,
                Signed = true,
                Type = NLTType.LookupTable,
                Lut = lut
            };

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, new List<NLTMarkerSegment> { seg });
            var decoded = J2kImage.FromBytes(encoded);

            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        private static bool ContainsMarker(byte[] data, int marker)
        {
            var hi = (byte)(marker >> 8);
            var lo = (byte)(marker & 0xFF);
            for (var i = 0; i + 1 < data.Length; i++)
                if (data[i] == hi && data[i + 1] == lo) return true;
            return false;
        }

        #endregion

        #region Helpers

        private static int[] Gradient(int w, int h)
        {
            var c = new int[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    c[y * w + x] = (x * 8 + y * 3) & 0xFF;
            return c;
        }

        // Converts unsigned 8-bit samples to the signed-centred domain expected by the encoder.
        private static int[] Centre(int[] unsigned)
        {
            var c = new int[unsigned.Length];
            for (var i = 0; i < unsigned.Length; i++) c[i] = unsigned[i] - 128;
            return c;
        }

        private static void AssertComponentMatches(int[] expected, int[] actual, int w, int h)
        {
            Assert.Equal(w * h, actual.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.True(expected[i] == actual[i],
                    $"sample {i} mismatch: expected {expected[i]}, got {actual[i]}");
        }

        #endregion
    }
}
