// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.wavelet.synthesis;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Regression tests for the 9x7 inverse wavelet synthesis fast path (fixed in commit 669c098).
    ///
    /// Commit 65285391 introduced a fast path in <see cref="SynWTFilterFloatLift9x7"/> that
    /// activates when lowStep == highStep == outStep == 1. A subsequent bug in that path
    /// left the last sample(s) of even-length signals unprocessed in each of the four
    /// lifting phases, causing a right-edge smear/blur artifact on decoded images.
    ///
    /// Strategy: call <c>synthetize_lpf</c> / <c>synthetize_hpf</c> twice with the same
    /// subband data — once via the fast path (outStep=1) and once via the generic path
    /// (outStep=2 on a sparsely-packed buffer, then unpacked). The two results must agree
    /// to floating-point precision for every output length from 2 to 16.
    ///
    /// Even lengths exercise the previously-missing right-boundary writes; odd lengths
    /// ensure we haven't broken the existing corner cases.
    /// </summary>
    public class SynWTFilterFloatLift9x7BoundaryTests
    {
        private static readonly SynWTFilterFloatLift9x7 Filter = new SynWTFilterFloatLift9x7();
        private const float Tolerance = 1e-4f; // identical floating-point ops → exact; allow 1 ULP

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Build a reproducible non-trivial subband pair for a given total output length.
        /// Uses a simple deterministic sequence so all samples are non-zero.
        /// </summary>
        private static (float[] low, float[] high) MakeSubbands(int outLen)
        {
            int lowLen = (outLen + 1) / 2;   // ceiling
            int highLen = outLen / 2;          // floor

            var low = new float[lowLen];
            var high = new float[highLen];

            for (int i = 0; i < lowLen; i++)
                low[i] = (float)Math.Sin(i + 1) * 100f;
            for (int i = 0; i < highLen; i++)
                high[i] = (float)Math.Cos(i * 1.3 + 0.5) * 50f;

            return (low, high);
        }

        /// <summary>
        /// Run the fast path (outStep=1) and return the compact output array.
        /// Always uses fresh copies of low/high so in-place normalization (synthetize_hpf)
        /// does not affect the subsequent generic-path call.
        /// </summary>
        private static float[] RunFastPath(
            Func<float[], int, int, int, float[], int, int, int, float[], int, int, float[]> invoke,
            float[] low, float[] high, int outLen)
        {
            var outFast = new float[outLen];
            invoke((float[])low.Clone(), 0, low.Length, 1,
                   (float[])high.Clone(), 0, high.Length, 1,
                   outFast, 0, 1);
            return outFast;
        }

        /// <summary>
        /// Run the generic (stride-2 in output) path using outStep=2, then extract the
        /// compact results from even positions.  Using outStep=2 forces the code into
        /// the generic branch (lowStep=1, highStep=1, outStep=2 → not the fast path).
        /// Always uses fresh copies of low/high for the same reason as RunFastPath.
        /// </summary>
        private static float[] RunGenericPath(
            Func<float[], int, int, int, float[], int, int, int, float[], int, int, float[]> invoke,
            float[] low, float[] high, int outLen)
        {
            // outSig must hold samples at indices 0, 2, 4, ... (outStep=2)
            var outSparse = new float[outLen * 2 + 2];
            invoke((float[])low.Clone(), 0, low.Length, 1,
                   (float[])high.Clone(), 0, high.Length, 1,
                   outSparse, 0, 2);

            var result = new float[outLen];
            for (int i = 0; i < outLen; i++)
                result[i] = outSparse[i * 2];
            return result;
        }

        // -----------------------------------------------------------------------
        // Invoke wrappers so both lpf and hpf tests share the same harness.
        // -----------------------------------------------------------------------

        private float[] InvokeLpf(float[] low, int lowOff, int lowLen, int lowStep,
                                   float[] high, int highOff, int highLen, int highStep,
                                   float[] outSig, int outOff, int outStep)
        {
            Filter.synthetize_lpf(low, lowOff, lowLen, lowStep, high, highOff, highLen, highStep,
                                  outSig, outOff, outStep);
            return outSig;
        }

        private float[] InvokeHpf(float[] low, int lowOff, int lowLen, int lowStep,
                                   float[] high, int highOff, int highLen, int highStep,
                                   float[] outSig, int outOff, int outStep)
        {
            Filter.synthetize_hpf(low, lowOff, lowLen, lowStep, high, highOff, highLen, highStep,
                                  outSig, outOff, outStep);
            return outSig;
        }

        // -----------------------------------------------------------------------
        // lpf tests: fast path matches generic path for lengths 2..16
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(14)]
        [InlineData(15)]
        [InlineData(16)]
        public void SynthetizeLpf_FastPathMatchesGenericPath(int outLen)
        {
            var (low, high) = MakeSubbands(outLen);

            var fast = RunFastPath(InvokeLpf, low, high, outLen);
            var generic = RunGenericPath(InvokeLpf, low, high, outLen);

            Assert.Equal(outLen, fast.Length);
            Assert.Equal(outLen, generic.Length);

            for (int i = 0; i < outLen; i++)
            {
                Assert.True(
                    Math.Abs(fast[i] - generic[i]) <= Tolerance,
                    $"synthetize_lpf outLen={outLen}: mismatch at output[{i}]: fast={fast[i]:G9}, generic={generic[i]:G9}");
            }
        }

        // -----------------------------------------------------------------------
        // hpf tests: fast path matches generic path for lengths 2..16
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(14)]
        [InlineData(15)]
        [InlineData(16)]
        public void SynthetizeHpf_FastPathMatchesGenericPath(int outLen)
        {
            // hpf: high-pass subband is the larger one (lowLen = floor, highLen = ceil)
            int highLen = (outLen + 1) / 2;
            int lowLen  = outLen / 2;

            var low  = new float[Math.Max(lowLen, 1)];
            var high = new float[highLen];

            for (int i = 0; i < low.Length;  i++) low[i]  = (float)Math.Sin(i * 0.7 + 0.3) * 80f;
            for (int i = 0; i < high.Length; i++) high[i] = (float)Math.Cos(i * 1.1 + 0.9) * 60f;

            var fast    = RunFastPath(InvokeHpf, low, high, outLen);
            var generic = RunGenericPath(InvokeHpf, low, high, outLen);

            for (int i = 0; i < outLen; i++)
            {
                Assert.True(
                    Math.Abs(fast[i] - generic[i]) <= Tolerance,
                    $"synthetize_hpf outLen={outLen}: mismatch at output[{i}]: fast={fast[i]:G9}, generic={generic[i]:G9}");
            }
        }

        // -----------------------------------------------------------------------
        // Edge-length boundary tests (the specific lengths that triggered the bug:
        // even outLen where last even/odd sample was skipped).
        // -----------------------------------------------------------------------

        [Fact]
        public void SynthetizeLpf_EvenLength_LastSampleCorrect()
        {
            // outLen=6: lowLen=3, highLen=3.
            // The last even sample (index 4) and last odd sample (index 5) were the ones
            // left unupdated by the broken fast path.
            const int outLen = 6;
            var (low, high) = MakeSubbands(outLen);

            var fast    = RunFastPath(InvokeLpf, low, high, outLen);
            var generic = RunGenericPath(InvokeLpf, low, high, outLen);

            Assert.True(Math.Abs(fast[outLen - 2] - generic[outLen - 2]) <= Tolerance,
                $"Last even sample wrong: fast={fast[outLen-2]:G9}, generic={generic[outLen-2]:G9}");
            Assert.True(Math.Abs(fast[outLen - 1] - generic[outLen - 1]) <= Tolerance,
                $"Last odd sample wrong: fast={fast[outLen-1]:G9}, generic={generic[outLen-1]:G9}");
        }

        [Fact]
        public void SynthetizeLpf_OddLength_AllSamplesCorrect()
        {
            // outLen=7: lowLen=4, highLen=3.
            const int outLen = 7;
            var (low, high) = MakeSubbands(outLen);

            var fast    = RunFastPath(InvokeLpf, low, high, outLen);
            var generic = RunGenericPath(InvokeLpf, low, high, outLen);

            for (int i = 0; i < outLen; i++)
                Assert.True(Math.Abs(fast[i] - generic[i]) <= Tolerance,
                    $"Mismatch at [{i}]: fast={fast[i]:G9}, generic={generic[i]:G9}");
        }

        /// <summary>
        /// Larger even length that exercises the AVX fast path (>=8 elements) followed
        /// by the scalar tail handler. Both paths must converge at the right edge.
        /// </summary>
        [Fact]
        public void SynthetizeLpf_LargeEvenLength_RightEdgeCorrect()
        {
            const int outLen = 64;
            var (low, high) = MakeSubbands(outLen);

            var fast    = RunFastPath(InvokeLpf, low, high, outLen);
            var generic = RunGenericPath(InvokeLpf, low, high, outLen);

            // Specifically assert the last several samples — these are the ones the
            // edge-stopping bug would corrupt.
            for (int i = outLen - 4; i < outLen; i++)
                Assert.True(Math.Abs(fast[i] - generic[i]) <= Tolerance,
                    $"Right-edge sample [{i}]: fast={fast[i]:G9}, generic={generic[i]:G9}");
        }

        [Fact]
        public void SynthetizeLpf_LargeOddLength_RightEdgeCorrect()
        {
            const int outLen = 65;
            var (low, high) = MakeSubbands(outLen);

            var fast    = RunFastPath(InvokeLpf, low, high, outLen);
            var generic = RunGenericPath(InvokeLpf, low, high, outLen);

            for (int i = outLen - 4; i < outLen; i++)
                Assert.True(Math.Abs(fast[i] - generic[i]) <= Tolerance,
                    $"Right-edge sample [{i}]: fast={fast[i]:G9}, generic={generic[i]:G9}");
        }
    }
}
