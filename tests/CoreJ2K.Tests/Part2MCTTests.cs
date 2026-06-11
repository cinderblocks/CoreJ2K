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
    /// Tests for the JPEG 2000 Part 2 (ISO/IEC 15444-2) array-based Multiple Component
    /// Transform (MCT/MCC/MCO/CBD): marker serialization, matrix maths, and a full
    /// encode/decode round-trip of a cross-component matrix transform.
    /// </summary>
    public class Part2MCTTests
    {
        #region Marker serialization

        [Fact]
        public void MctArrayMarker_RoundTrips()
        {
            var seg = new MctArrayMarkerSegment
            {
                Index = 3,
                ArrayType = MctArrayType.Decorrelation,
                ElementType = MctElementType.Float64,
                Values = new[] { 1.0, -0.5, 0.25, 2.0 }
            };

            var parsed = RoundTrip(seg);

            Assert.Equal(3, parsed.Index);
            Assert.Equal(MctArrayType.Decorrelation, parsed.ArrayType);
            Assert.Equal(MctElementType.Float64, parsed.ElementType);
            Assert.Equal(seg.Values, parsed.Values);
        }

        [Fact]
        public void MccMarker_RoundTrips()
        {
            var seg = new MccMarkerSegment
            {
                Index = 1,
                Irreversible = true,
                DecorrelationArrayIndex = 0,
                OffsetArrayIndex = MccMarkerSegment.NoOffset,
                Components = new[] { 0, 1, 2 }
            };

            var parsed = RoundTrip(seg);

            Assert.Equal(1, parsed.Index);
            Assert.True(parsed.Irreversible);
            Assert.Equal(0, parsed.DecorrelationArrayIndex);
            Assert.Equal(MccMarkerSegment.NoOffset, parsed.OffsetArrayIndex);
            Assert.Equal(new[] { 0, 1, 2 }, parsed.Components);
        }

        [Fact]
        public void MccMarker_TransformType_RoundTrips()
        {
            foreach (var type in new[] { MctTransformType.Matrix, MctTransformType.Dependency, MctTransformType.Wavelet })
            {
                var seg = new MccMarkerSegment
                {
                    Index = 0,
                    TransformType = type,
                    DecorrelationArrayIndex = MccMarkerSegment.NoOffset,
                    Components = new[] { 0, 1, 2 }
                };
                var parsed = RoundTrip(seg);
                Assert.Equal(type, parsed.TransformType);
                Assert.Equal(new[] { 0, 1, 2 }, parsed.Components);
            }
        }

        [Fact]
        public void McoAndCbdMarkers_RoundTrip()
        {
            var mco = new McoMarkerSegment { Stages = new[] { 0, 1 } };
            var parsedMco = RoundTripMco(mco);
            Assert.Equal(new[] { 0, 1 }, parsedMco.Stages);

            var cbd = CbdMarkerSegment.FromComponents(3, 8, signed: false);
            var parsedCbd = RoundTripCbd(cbd);
            Assert.Equal(3, parsedCbd.ComponentDepths.Length);
            Assert.Equal(8, parsedCbd.GetBitDepth(0));
            Assert.False(parsedCbd.IsSigned(0));
        }

        #endregion

        #region Matrix maths

        [Fact]
        public void Invert_ProducesIdentityWhenMultiplied()
        {
            var a = new double[,] { { 2, 1, 0 }, { 1, 3, 1 }, { 0, 1, 2 } };
            var inv = MctTransform.Invert(a);

            for (var i = 0; i < 3; i++)
                for (var j = 0; j < 3; j++)
                {
                    var sum = 0.0;
                    for (var k = 0; k < 3; k++) sum += a[i, k] * inv[k, j];
                    Assert.Equal(i == j ? 1.0 : 0.0, sum, 9);
                }
        }

        #endregion

        #region Full pipeline round-trip

        [Fact]
        public void Pipeline_PermutationMct_RoundTripsExactly()
        {
            const int w = 24, h = 24, nc = 3;
            var pixels = new[] { Gradient(w, h, 8, 3), Gradient(w, h, 3, 8), Gradient(w, h, 5, 5) };
            var comps = new[] { Centre(pixels[0]), Centre(pixels[1]), Centre(pixels[2]) };
            var source = new InterleavedImageSource(w, h, nc, 8, new[] { false, false, false }, comps);

            // Component permutation: coded0=in1, coded1=in2, coded2=in0 (stays in range, exact).
            var forward = new double[,] { { 0, 1, 0 }, { 0, 0, 1 }, { 1, 0, 0 } };
            var spec = new MctEncodeSpec
            {
                Components = new[] { 0, 1, 2 },
                ForwardMatrix = forward,
                Irreversible = false,
                ElementType = MctElementType.Float64
            };

            var decoded = EncodeDecode(source, new List<MctEncodeSpec> { spec });

            for (var c = 0; c < nc; c++)
                AssertComponentMatches(pixels[c], decoded.GetComponent(c), w, h);
        }

        [Fact]
        public void Pipeline_DecorrelationMct_RoundTripsLosslessly()
        {
            // A reversible integer decorrelation (det = 1, integer inverse). The difference
            // channels exceed the nominal 8-bit range, but reversible coding preserves them
            // and the inverse transform restores the originals exactly.
            const int w = 24, h = 24, nc = 3;
            var pixels = new[] { Gradient(w, h, 6, 1), Gradient(w, h, 1, 6), Gradient(w, h, 4, 4) };
            var comps = new[] { Centre(pixels[0]), Centre(pixels[1]), Centre(pixels[2]) };
            var source = new InterleavedImageSource(w, h, nc, 8, new[] { false, false, false }, comps);

            // Unit lower-triangular (unimodular) -> exact integer inverse.
            var forward = new double[,] { { 1, 0, 0 }, { -1, 1, 0 }, { 0, -1, 1 } };
            var spec = new MctEncodeSpec
            {
                Components = new[] { 0, 1, 2 },
                ForwardMatrix = forward,
                Irreversible = false,
                ElementType = MctElementType.Int32
            };

            var decoded = EncodeDecode(source, new List<MctEncodeSpec> { spec });

            for (var c = 0; c < nc; c++)
                AssertComponentMatches(pixels[c], decoded.GetComponent(c), w, h);
        }

        [Fact]
        public void Pipeline_DependencyMct_RoundTripsLosslessly()
        {
            // Dependency lifting with a fractional, strictly-lower-triangular prediction matrix.
            // The rounded prediction uses the originals in both directions, so it is exactly
            // reversible despite the fractional coefficients.
            const int w = 24, h = 24, nc = 3;
            var pixels = new[] { Gradient(w, h, 7, 2), Gradient(w, h, 2, 7), Gradient(w, h, 4, 4) };
            var comps = new[] { Centre(pixels[0]), Centre(pixels[1]), Centre(pixels[2]) };
            var source = new InterleavedImageSource(w, h, nc, 8, new[] { false, false, false }, comps);

            var p = new double[,] { { 0, 0, 0 }, { 0.5, 0, 0 }, { 0.25, 0.5, 0 } };
            var spec = new MctEncodeSpec
            {
                TransformType = MctTransformType.Dependency,
                Components = new[] { 0, 1, 2 },
                ForwardMatrix = p,
                Irreversible = false,
                ElementType = MctElementType.Float64
            };

            var decoded = EncodeDecode(source, new List<MctEncodeSpec> { spec });

            for (var c = 0; c < nc; c++)
                AssertComponentMatches(pixels[c], decoded.GetComponent(c), w, h);
        }

        [Fact]
        public void Pipeline_WaveletMct_RoundTripsLosslessly()
        {
            // Reversible 5/3 wavelet applied across the components.
            const int w = 24, h = 24, nc = 3;
            var pixels = new[] { Gradient(w, h, 5, 2), Gradient(w, h, 2, 5), Gradient(w, h, 3, 6) };
            var comps = new[] { Centre(pixels[0]), Centre(pixels[1]), Centre(pixels[2]) };
            var source = new InterleavedImageSource(w, h, nc, 8, new[] { false, false, false }, comps);

            var spec = new MctEncodeSpec
            {
                TransformType = MctTransformType.Wavelet,
                Components = new[] { 0, 1, 2 },
                Irreversible = false
            };

            var decoded = EncodeDecode(source, new List<MctEncodeSpec> { spec });

            for (var c = 0; c < nc; c++)
                AssertComponentMatches(pixels[c], decoded.GetComponent(c), w, h);
        }

        #endregion

        #region Helpers

        private static InterleavedImage EncodeDecode(InterleavedImageSource source, IList<MctEncodeSpec> specs)
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";
            pl["Mct"] = "off"; // disable the Part 1 component transform; MCT is the cross-component transform

            var encoded = J2kImage.ToBytes(source, null, pl, null, specs);
            return J2kImage.FromBytes(encoded);
        }

        private static int[] Gradient(int w, int h, int sx, int sy)
        {
            var c = new int[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    c[y * w + x] = (x * sx + y * sy) & 0xFF;
            return c;
        }

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

        private static MctArrayMarkerSegment RoundTrip(MctArrayMarkerSegment seg)
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new EndianBinaryWriter(ms, true)) { seg.Write(w); w.Flush(); }
            return ReadAfterMarker(ms.ToArray(), r => MctArrayMarkerSegment.Read(r));
        }

        private static MccMarkerSegment RoundTrip(MccMarkerSegment seg)
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new EndianBinaryWriter(ms, true)) { seg.Write(w); w.Flush(); }
            return ReadAfterMarker(ms.ToArray(), r => MccMarkerSegment.Read(r));
        }

        private static McoMarkerSegment RoundTripMco(McoMarkerSegment seg)
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new EndianBinaryWriter(ms, true)) { seg.Write(w); w.Flush(); }
            return ReadAfterMarker(ms.ToArray(), r => McoMarkerSegment.Read(r));
        }

        private static CbdMarkerSegment RoundTripCbd(CbdMarkerSegment seg)
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new EndianBinaryWriter(ms, true)) { seg.Write(w); w.Flush(); }
            return ReadAfterMarker(ms.ToArray(), r => CbdMarkerSegment.Read(r));
        }

        // The marker writers emit the 2-byte marker code first; the readers expect it consumed.
        private static T ReadAfterMarker<T>(byte[] bytes, Func<System.IO.BinaryReader, T> read)
        {
            using var ms = new System.IO.MemoryStream(bytes);
            using var r = new EndianBinaryReader(ms, true);
            r.ReadInt16(); // marker
            return read(r);
        }

        #endregion
    }
}
