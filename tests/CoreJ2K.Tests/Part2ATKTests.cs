// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using CoreJ2K;
using CoreJ2K.Util;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using CoreJ2K.j2k.wavelet.synthesis;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for the JPEG 2000 Part 2 (ISO/IEC 15444-2) Arbitrary Transformation Kernel
    /// (ATK): marker serialization, the generic lifting engine (verified bit-exactly
    /// against the Part 1 5/3 filter and numerically against the 9/7 filter), perfect
    /// reconstruction of custom kernels, and full encode/decode round-trips through the
    /// codec pipeline including capability signaling.
    /// </summary>
    public class Part2ATKTests
    {
        #region Marker serialization

        [Fact]
        public void AtkMarker_Reversible_RoundTrips()
        {
            var seg = new AtkMarkerSegment
            {
                Index = 5,
                Reversible = true,
                FirstStepUpdatesOdd = true,
                Steps = new List<AtkLiftingStep>
                {
                    new AtkLiftingStep { Coefficients = new double[] { -1, -1 }, Epsilon = 1, Beta = 1, Offset = 0 },
                    new AtkLiftingStep { Coefficients = new double[] { 3, 3 }, Epsilon = 4, Beta = 8, Offset = 1 }
                }
            };

            var parsed = AtkMarkerSegment.FromBytes(seg.ToBytes());

            Assert.True(seg.StructurallyEquals(parsed));
            Assert.Equal(5, parsed.Index);
            Assert.True(parsed.Reversible);
            Assert.True(parsed.FirstStepUpdatesOdd);
            Assert.Equal(2, parsed.Steps.Count);
            Assert.Equal(new double[] { -1, -1 }, parsed.Steps[0].Coefficients);
            Assert.Equal(1, parsed.Steps[0].Epsilon);
            Assert.Equal(1, parsed.Steps[0].Beta);
            Assert.Equal(1, parsed.Steps[1].Offset);
        }

        [Fact]
        public void AtkMarker_Irreversible_RoundTrips()
        {
            var seg = AtkMarkerSegment.CreateW9x7Equivalent(9);

            var parsed = AtkMarkerSegment.FromBytes(seg.ToBytes());

            Assert.True(seg.StructurallyEquals(parsed));
            Assert.Equal(9, parsed.Index);
            Assert.False(parsed.Reversible);
            Assert.Equal(0.8128930655, parsed.LowGain, 12);
            Assert.Equal(1.230174106, parsed.HighGain, 12);
            Assert.Equal(4, parsed.Steps.Count);
            Assert.Equal(-1.586134342, parsed.Steps[0].Coefficients[0], 12);
        }

        [Fact]
        public void AtkMarker_Validate_RejectsBadSegments()
        {
            // Index outside the SPcod-referencable range.
            Assert.Throws<ArgumentException>(() => AtkMarkerSegment.CreateW5x3Equivalent(1).Validate());
            Assert.Throws<ArgumentException>(() => AtkMarkerSegment.CreateW5x3Equivalent(128).Validate());

            // No lifting steps.
            Assert.Throws<ArgumentException>(() => new AtkMarkerSegment { Index = 3 }.Validate());

            // Reversible kernels require integer coefficients.
            var frac = new AtkMarkerSegment
            {
                Index = 3,
                Reversible = true,
                Steps = new List<AtkLiftingStep> { new AtkLiftingStep { Coefficients = new[] { -0.5, -0.5 }, Epsilon = 0 } }
            };
            Assert.Throws<ArgumentException>(() => frac.Validate());

            // Shift out of range.
            var badShift = AtkMarkerSegment.CreateW5x3Equivalent(3);
            badShift.Steps[0].Epsilon = 32;
            Assert.Throws<ArgumentException>(() => badShift.Validate());
        }

        #endregion

        #region Lifting engine vs Part 1 filters

        [Fact]
        public void AtkAnalysis_W5x3Kernel_MatchesBuiltin5x3Exactly()
        {
            var atk = new AnWTFilterIntArbitrary(AtkMarkerSegment.CreateW5x3Equivalent(3));
            var builtin = new AnWTFilterIntLift5x3();
            var rnd = new Random(42);

            for (var n = 1; n <= 17; n++)
            {
                var input = RandomInts(rnd, n);
                foreach (var lpf in new[] { true, false })
                {
                    var (lowLen, highLen) = SubbandLengths(n, lpf);
                    int[] lowA = new int[Math.Max(1, lowLen)], highA = new int[Math.Max(1, highLen)];
                    int[] lowB = new int[Math.Max(1, lowLen)], highB = new int[Math.Max(1, highLen)];

                    if (lpf)
                    {
                        atk.analyze_lpf(input, 0, n, 1, lowA, 0, 1, highA, 0, 1);
                        builtin.analyze_lpf(input, 0, n, 1, lowB, 0, 1, highB, 0, 1);
                    }
                    else
                    {
                        atk.analyze_hpf(input, 0, n, 1, lowA, 0, 1, highA, 0, 1);
                        builtin.analyze_hpf(input, 0, n, 1, lowB, 0, 1, highB, 0, 1);
                    }

                    Assert.True(SequencesEqual(lowB, lowA),
                        $"low-pass mismatch at n={n}, {(lpf ? "lpf" : "hpf")}");
                    Assert.True(SequencesEqual(highB, highA),
                        $"high-pass mismatch at n={n}, {(lpf ? "lpf" : "hpf")}");
                }
            }
        }

        [Fact]
        public void AtkSynthesis_W5x3Kernel_MatchesBuiltin5x3Exactly()
        {
            var atk = new SynWTFilterIntArbitrary(AtkMarkerSegment.CreateW5x3Equivalent(3));
            var builtin = new SynWTFilterIntLift5x3();
            var rnd = new Random(43);

            for (var n = 1; n <= 17; n++)
            {
                foreach (var lpf in new[] { true, false })
                {
                    var (lowLen, highLen) = SubbandLengths(n, lpf);
                    var low = RandomInts(rnd, Math.Max(1, lowLen));
                    var high = RandomInts(rnd, Math.Max(1, highLen));
                    int[] outA = new int[n], outB = new int[n];

                    if (lpf)
                    {
                        atk.synthetize_lpf(low, 0, lowLen, 1, high, 0, highLen, 1, outA, 0, 1);
                        builtin.synthetize_lpf(low, 0, lowLen, 1, high, 0, highLen, 1, outB, 0, 1);
                    }
                    else
                    {
                        atk.synthetize_hpf(low, 0, lowLen, 1, high, 0, highLen, 1, outA, 0, 1);
                        builtin.synthetize_hpf(low, 0, lowLen, 1, high, 0, highLen, 1, outB, 0, 1);
                    }

                    Assert.True(SequencesEqual(outB, outA),
                        $"synthesis mismatch at n={n}, {(lpf ? "lpf" : "hpf")}");
                }
            }
        }

        [Fact]
        public void AtkAnalysis_W9x7Kernel_MatchesBuiltin9x7Numerically()
        {
            var atk = new AnWTFilterFloatArbitrary(AtkMarkerSegment.CreateW9x7Equivalent(4));
            var builtin = new AnWTFilterFloatLift9x7();
            var rnd = new Random(44);

            for (var n = 2; n <= 17; n++)
            {
                var input = RandomFloats(rnd, n);
                foreach (var lpf in new[] { true, false })
                {
                    var (lowLen, highLen) = SubbandLengths(n, lpf);
                    float[] lowA = new float[Math.Max(1, lowLen)], highA = new float[Math.Max(1, highLen)];
                    float[] lowB = new float[Math.Max(1, lowLen)], highB = new float[Math.Max(1, highLen)];

                    if (lpf)
                    {
                        atk.analyze_lpf(input, 0, n, 1, lowA, 0, 1, highA, 0, 1);
                        builtin.analyze_lpf(input, 0, n, 1, lowB, 0, 1, highB, 0, 1);
                    }
                    else
                    {
                        atk.analyze_hpf(input, 0, n, 1, lowA, 0, 1, highA, 0, 1);
                        builtin.analyze_hpf(input, 0, n, 1, lowB, 0, 1, highB, 0, 1);
                    }

                    AssertClose(lowB, lowA, 1e-2f, $"low-pass n={n} {(lpf ? "lpf" : "hpf")}");
                    AssertClose(highB, highA, 1e-2f, $"high-pass n={n} {(lpf ? "lpf" : "hpf")}");
                }
            }
        }

        [Fact]
        public void AtkWaveforms_W5x3Kernel_MatchPart1SynthesisWaveforms()
        {
            var atk = new AnWTFilterIntArbitrary(AtkMarkerSegment.CreateW5x3Equivalent(3));

            var lp = atk.GetLPSynthesisFilter();
            var hp = atk.GetHPSynthesisFilter();

            AssertClose(new[] { 0.5f, 1f, 0.5f }, lp, 1e-6f, "LP synthesis waveform");
            AssertClose(new[] { -0.125f, -0.25f, 0.75f, -0.25f, -0.125f }, hp, 1e-6f, "HP synthesis waveform");
        }

        #endregion

        #region Perfect reconstruction of custom kernels

        [Fact]
        public void CustomReversibleKernel_AnalysisSynthesis_IsPerfectlyReconstructing()
        {
            // A genuinely custom kernel (not the 5/3): a weaker update step and a wider
            // predict step. Any lifting ladder is perfectly reconstructing by
            // construction; this verifies the engine honours that end to end.
            var kernel = new AtkMarkerSegment
            {
                Index = 7,
                Reversible = true,
                Steps = new List<AtkLiftingStep>
                {
                    new AtkLiftingStep { Coefficients = new double[] { -3, -3 }, Epsilon = 3, Beta = 4 },
                    new AtkLiftingStep { Coefficients = new double[] { 1, 1 }, Epsilon = 3, Beta = 4 }
                }
            };
            var an = new AnWTFilterIntArbitrary(kernel);
            var syn = new SynWTFilterIntArbitrary(kernel);
            var rnd = new Random(45);

            for (var n = 1; n <= 33; n++)
            {
                var input = RandomInts(rnd, n);
                foreach (var lpf in new[] { true, false })
                {
                    var (lowLen, highLen) = SubbandLengths(n, lpf);
                    int[] low = new int[Math.Max(1, lowLen)], high = new int[Math.Max(1, highLen)];
                    var output = new int[n];

                    if (lpf)
                    {
                        an.analyze_lpf(input, 0, n, 1, low, 0, 1, high, 0, 1);
                        syn.synthetize_lpf(low, 0, lowLen, 1, high, 0, highLen, 1, output, 0, 1);
                    }
                    else
                    {
                        an.analyze_hpf(input, 0, n, 1, low, 0, 1, high, 0, 1);
                        syn.synthetize_hpf(low, 0, lowLen, 1, high, 0, highLen, 1, output, 0, 1);
                    }

                    Assert.True(SequencesEqual(input, output),
                        $"reconstruction mismatch at n={n}, {(lpf ? "lpf" : "hpf")}");
                }
            }
        }

        [Fact]
        public void CustomIrreversibleKernel_AnalysisSynthesis_ReconstructsNumerically()
        {
            var kernel = new AtkMarkerSegment
            {
                Index = 8,
                Reversible = false,
                LowGain = 0.9,
                HighGain = 1.1,
                Steps = new List<AtkLiftingStep>
                {
                    new AtkLiftingStep { Coefficients = new[] { -0.4, -0.4 } },
                    new AtkLiftingStep { Coefficients = new[] { 0.2, 0.2 } }
                }
            };
            var an = new AnWTFilterFloatArbitrary(kernel);
            var syn = new SynWTFilterFloatArbitrary(kernel);
            var rnd = new Random(46);

            for (var n = 1; n <= 33; n++)
            {
                var input = RandomFloats(rnd, n);
                foreach (var lpf in new[] { true, false })
                {
                    var (lowLen, highLen) = SubbandLengths(n, lpf);
                    float[] low = new float[Math.Max(1, lowLen)], high = new float[Math.Max(1, highLen)];
                    var output = new float[n];

                    if (lpf)
                    {
                        an.analyze_lpf(input, 0, n, 1, low, 0, 1, high, 0, 1);
                        syn.synthetize_lpf(low, 0, lowLen, 1, high, 0, highLen, 1, output, 0, 1);
                    }
                    else
                    {
                        an.analyze_hpf(input, 0, n, 1, low, 0, 1, high, 0, 1);
                        syn.synthetize_hpf(low, 0, lowLen, 1, high, 0, highLen, 1, output, 0, 1);
                    }

                    AssertClose(input, output, 1e-3f, $"reconstruction n={n} {(lpf ? "lpf" : "hpf")}");
                }
            }
        }

        #endregion

        #region Full pipeline round-trips

        [Fact]
        public void Pipeline_W5x3EquivalentAtk_LosslessRoundTripsExactly()
        {
            const int w = 32, h = 32;
            var pixels = Gradient(w, h);
            var source = MakeSource(w, h, pixels);
            var atk = AtkMarkerSegment.CreateW5x3Equivalent(3);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, null, null, null, atk);
            Assert.True(ContainsMarker(encoded, 0xFF79), "encoded codestream is missing the ATK marker");
            Assert.True(ContainsMarker(encoded, 0xFF50), "encoded codestream is missing the CAP marker");

            var decoded = J2kImage.FromBytes(encoded);
            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        [Fact]
        public void Pipeline_CustomReversibleAtk_LosslessRoundTripsExactly()
        {
            const int w = 40, h = 24;
            var pixels = Gradient(w, h);
            var source = MakeSource(w, h, pixels);
            var atk = CustomReversibleKernel(index: 11);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, null, null, null, atk);
            var decoded = J2kImage.FromBytes(encoded);

            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        [Fact]
        public void Pipeline_CustomReversibleAtk_OddDimensions_RoundTripsExactly()
        {
            // Odd dimensions exercise the boundary handling of every decomposition level.
            const int w = 33, h = 17;
            var pixels = Gradient(w, h);
            var source = MakeSource(w, h, pixels);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(5));
            var decoded = J2kImage.FromBytes(encoded);

            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        [Fact]
        public void Pipeline_CustomReversibleAtk_MultiComponent_RoundTripsExactly()
        {
            const int w = 32, h = 32;
            var comps = new int[3][];
            var pixels = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                pixels[c] = new int[w * h];
                comps[c] = new int[w * h];
                for (var i = 0; i < w * h; i++)
                {
                    pixels[c][i] = (i * (c + 3) + 17 * c) & 0xFF;
                    comps[c][i] = pixels[c][i] - 128;
                }
            }
            var source = new InterleavedImageSource(w, h, 3, 8, new[] { false, false, false }, comps);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(6));
            var decoded = J2kImage.FromBytes(encoded);

            for (var c = 0; c < 3; c++)
                AssertComponentMatches(pixels[c], decoded.GetComponent(c), w, h);
        }

        [Fact]
        public void Pipeline_CustomReversibleAtk_Tiled_RoundTripsExactly()
        {
            const int w = 48, h = 48;
            var pixels = Gradient(w, h);
            var source = MakeSource(w, h, pixels);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";
            pl["tiles"] = "16 16";

            var encoded = J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(4));
            var decoded = J2kImage.FromBytes(encoded);

            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        [Fact]
        public void Pipeline_W9x7EquivalentAtk_MatchesBuiltin9x7Decode()
        {
            const int w = 32, h = 32;
            var pixels = Gradient(w, h);

            // Encode once with the built-in 9/7 and once with the ATK 9/7-equivalent.
            var plRef = J2kImage.GetDefaultEncoderParameterList();
            var encodedRef = J2kImage.ToBytes(MakeSource(w, h, pixels), null, plRef);
            var decodedRef = J2kImage.FromBytes(encodedRef).GetComponent(0);

            var plAtk = J2kImage.GetDefaultEncoderParameterList();
            var encodedAtk = J2kImage.ToBytes(MakeSource(w, h, pixels), null, plAtk, null, null, null,
                AtkMarkerSegment.CreateW9x7Equivalent(4));
            var decodedAtk = J2kImage.FromBytes(encodedAtk).GetComponent(0);

            // Both are lossy; each must stay close to the source, and to each other.
            var maxVsSource = 0;
            var maxVsRef = 0;
            for (var i = 0; i < w * h; i++)
            {
                maxVsSource = Math.Max(maxVsSource, Math.Abs(decodedAtk[i] - pixels[i]));
                maxVsRef = Math.Max(maxVsRef, Math.Abs(decodedAtk[i] - decodedRef[i]));
            }
            Assert.True(maxVsSource <= 4, $"ATK 9/7 decode strays {maxVsSource} from the source");
            Assert.True(maxVsRef <= 2, $"ATK 9/7 decode differs from built-in 9/7 by {maxVsRef}");
        }

        [Fact]
        public void Pipeline_AtkJpx_WritesRreqWithAtkFeature()
        {
            const int w = 32, h = 32;
            var source = MakeSource(w, h, Gradient(w, h));

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";
            pl["file_format"] = "on";

            var bytes = J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(3));

            var reader = new FileFormatReader(new ISRandomAccessIO(new MemoryStream(bytes)));
            reader.readFileFormat();
            Assert.NotNull(reader.Metadata.ReaderRequirements);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_ATK,
                reader.Metadata.ReaderRequirements!.StandardFeatures);
        }

        [Fact]
        public void Builder_WithAtk_RoundTripsExactly()
        {
            const int w = 32, h = 32;
            var pixels = Gradient(w, h);
            var source = MakeSource(w, h, pixels);

            var encoded = new global::CoreJ2K.Configuration.CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithAtk(CustomReversibleKernel(10))
                .Encode(source);

            var decoded = J2kImage.FromBytes(encoded);
            AssertComponentMatches(pixels, decoded.GetComponent(0), w, h);
        }

        #endregion

        #region Error handling

        [Fact]
        public void Encode_AtkWithExplicitFfilters_Throws()
        {
            var source = MakeSource(16, 16, Gradient(16, 16));
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";
            pl["Ffilters"] = "w5x3";

            Assert.Throws<ArgumentException>(() =>
                J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(3)));
        }

        [Fact]
        public void Encode_ReversibleAtkWithoutLossless_Throws()
        {
            var source = MakeSource(16, 16, Gradient(16, 16));
            var pl = J2kImage.GetDefaultEncoderParameterList();

            Assert.Throws<ArgumentException>(() =>
                J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(3)));
        }

        [Fact]
        public void Encode_IrreversibleAtkWithLossless_Throws()
        {
            var source = MakeSource(16, 16, Gradient(16, 16));
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            Assert.Throws<ArgumentException>(() =>
                J2kImage.ToBytes(source, null, pl, null, null, null, AtkMarkerSegment.CreateW9x7Equivalent(4)));
        }

        [Fact]
        public void Encode_AtkWithPart1ComponentTransform_Throws()
        {
            const int w = 16, h = 16;
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[w * h];
                for (var i = 0; i < w * h; i++) comps[c][i] = ((i * 5) & 0xFF) - 128;
            }
            var source = new InterleavedImageSource(w, h, 3, 8, new[] { false, false, false }, comps);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";
            pl["Mct"] = "on";

            Assert.Throws<ArgumentException>(() =>
                J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(3)));
        }

        [Fact]
        public void Decode_MissingAtkSegment_Throws()
        {
            const int w = 16, h = 16;
            var source = MakeSource(w, h, Gradient(w, h));
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            var encoded = J2kImage.ToBytes(source, null, pl, null, null, null, CustomReversibleKernel(3));
            var stripped = StripMarkerSegment(encoded, 0xFF79);
            Assert.True(stripped.Length < encoded.Length, "test setup failed to strip the ATK segment");

            // The COD transformation byte now references a kernel that no longer exists.
            Assert.ThrowsAny<Exception>(() => J2kImage.FromBytes(stripped));
        }

        #endregion

        #region Helpers

        /// <summary>A custom reversible lifting kernel that is intentionally not the 5/3.</summary>
        private static AtkMarkerSegment CustomReversibleKernel(int index)
        {
            return new AtkMarkerSegment
            {
                Index = index,
                Reversible = true,
                Steps = new List<AtkLiftingStep>
                {
                    new AtkLiftingStep { Coefficients = new double[] { -3, -3 }, Epsilon = 3, Beta = 4 },
                    new AtkLiftingStep { Coefficients = new double[] { 1, 1 }, Epsilon = 3, Beta = 4 }
                }
            };
        }

        private static (int lowLen, int highLen) SubbandLengths(int n, bool lowFirst)
        {
            var first = (n + 1) / 2;
            var second = n / 2;
            return lowFirst ? (first, second) : (second, first);
        }

        private static int[] RandomInts(Random rnd, int n)
        {
            var a = new int[n];
            for (var i = 0; i < n; i++) a[i] = rnd.Next(-128, 128);
            return a;
        }

        private static float[] RandomFloats(Random rnd, int n)
        {
            var a = new float[n];
            for (var i = 0; i < n; i++) a[i] = rnd.Next(-128, 128);
            return a;
        }

        private static bool SequencesEqual(int[] expected, int[] actual)
        {
            if (expected.Length != actual.Length) return false;
            for (var i = 0; i < expected.Length; i++)
                if (expected[i] != actual[i]) return false;
            return true;
        }

        private static void AssertClose(float[] expected, float[] actual, float tol, string what)
        {
            Assert.True(expected.Length == actual.Length, $"{what}: length {actual.Length} != {expected.Length}");
            for (var i = 0; i < expected.Length; i++)
                Assert.True(Math.Abs(expected[i] - actual[i]) <= tol,
                    $"{what}: sample {i} expected {expected[i]}, got {actual[i]}");
        }

        private static int[] Gradient(int w, int h)
        {
            var c = new int[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    c[y * w + x] = (x * 8 + y * 3) & 0xFF;
            return c;
        }

        private static InterleavedImageSource MakeSource(int w, int h, int[] pixels)
        {
            var centred = new int[pixels.Length];
            for (var i = 0; i < pixels.Length; i++) centred[i] = pixels[i] - 128;
            return new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { centred });
        }

        private static void AssertComponentMatches(int[] expected, int[] actual, int w, int h)
        {
            Assert.Equal(w * h, actual.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.True(expected[i] == actual[i],
                    $"sample {i} mismatch: expected {expected[i]}, got {actual[i]}");
        }

        private static bool ContainsMarker(byte[] data, int marker)
        {
            var hi = (byte)(marker >> 8);
            var lo = (byte)(marker & 0xFF);
            for (var i = 0; i + 1 < data.Length; i++)
                if (data[i] == hi && data[i + 1] == lo) return true;
            return false;
        }

        /// <summary>Removes the first occurrence of the given marker segment (marker + Lseg bytes).</summary>
        private static byte[] StripMarkerSegment(byte[] data, int marker)
        {
            var hi = (byte)(marker >> 8);
            var lo = (byte)(marker & 0xFF);
            for (var i = 0; i + 3 < data.Length; i++)
            {
                if (data[i] != hi || data[i + 1] != lo) continue;
                var len = (data[i + 2] << 8) | data[i + 3];
                var total = 2 + len;
                if (i + total > data.Length) break;
                var outBytes = new byte[data.Length - total];
                Array.Copy(data, 0, outBytes, 0, i);
                Array.Copy(data, i + total, outBytes, i, data.Length - i - total);
                return outBytes;
            }
            return data;
        }

        #endregion
    }
}
