// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.image.Simd;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Validates that the SIMD-accelerated multi-component transform kernels
    /// produce results identical (bit-exact for integer paths, exact-int for
    /// truncation-based float-to-int paths) to the scalar reference defined
    /// by the JPEG 2000 Part-1 specification.
    /// </summary>
    public class SimdColorTransformTests
    {
        private static readonly int[] Widths =
        {
            // small / unaligned / aligned mixtures: exercise both vector body
            // and scalar tail across SSE2/AVX2/NEON typical lane counts.
            1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 23, 31, 32, 33, 63, 64, 65, 127, 256, 257
        };

        // ---- Scalar references (copied verbatim from legacy code) ----

        private static void ScalarInvRct(int[] y, int[] cb, int[] cr, int[] r, int[] g, int[] b)
        {
            for (int i = 0; i < y.Length; i++)
            {
                int gv = y[i] - ((cb[i] + cr[i]) >> 2);
                g[i] = gv;
                r[i] = cr[i] + gv;
                b[i] = cb[i] + gv;
            }
        }

        private static void ScalarInvIct(float[] y, float[] cb, float[] cr, int[] r, int[] g, int[] b)
        {
            for (int i = 0; i < y.Length; i++)
            {
                r[i] = (int)(y[i] + 1.402f * cr[i] + 0.5f);
                g[i] = (int)(y[i] - 0.34413f * cb[i] - 0.71414f * cr[i] + 0.5f);
                b[i] = (int)(y[i] + 1.772f * cb[i] + 0.5f);
            }
        }

        private static void ScalarForwRct(int[] r, int[] g, int[] b, int[] y, int[] u, int[] v)
        {
            for (int i = 0; i < r.Length; i++)
            {
                y[i] = (r[i] + 2 * g[i] + b[i]) >> 2;
                u[i] = b[i] - g[i];
                v[i] = r[i] - g[i];
            }
        }

        private static void ScalarForwIct(int[] r, int[] g, int[] b, float[] y, float[] cb, float[] cr)
        {
            for (int i = 0; i < r.Length; i++)
            {
                y[i]  =   0.299f   * r[i] + 0.587f   * g[i] + 0.114f   * b[i];
                cb[i] = (-0.16875f) * r[i] - 0.33126f * g[i] + 0.5f     * b[i];
                cr[i] =   0.5f     * r[i] - 0.41869f * g[i] - 0.08131f * b[i];
            }
        }

        [Fact]
        public void InvRct_MatchesScalar_AcrossWidths()
        {
            var rng = new Random(0xC0FFEE);
            foreach (var n in Widths)
            {
                int[] y = new int[n], cb = new int[n], cr = new int[n];
                for (int i = 0; i < n; i++)
                {
                    // Cover full signed range including negatives.
                    y[i]  = rng.Next(-2048, 2048);
                    cb[i] = rng.Next(-2048, 2048);
                    cr[i] = rng.Next(-2048, 2048);
                }
                int[] rRef = new int[n], gRef = new int[n], bRef = new int[n];
                int[] rSimd = new int[n], gSimd = new int[n], bSimd = new int[n];

                ScalarInvRct(y, cb, cr, rRef, gRef, bRef);
                SimdColorTransform.InvRctRow(y, cb, cr, rSimd, gSimd, bSimd);

                Assert.Equal(rRef, rSimd);
                Assert.Equal(gRef, gSimd);
                Assert.Equal(bRef, bSimd);
            }
        }

        [Fact]
        public void InvIct_MatchesScalar_AcrossWidths()
        {
            var rng = new Random(0xBADF00D);
            foreach (var n in Widths)
            {
                float[] y = new float[n], cb = new float[n], cr = new float[n];
                for (int i = 0; i < n; i++)
                {
                    y[i]  = (float)(rng.NextDouble() * 4096.0 - 2048.0);
                    cb[i] = (float)(rng.NextDouble() * 4096.0 - 2048.0);
                    cr[i] = (float)(rng.NextDouble() * 4096.0 - 2048.0);
                }
                int[] rRef = new int[n], gRef = new int[n], bRef = new int[n];
                int[] rSimd = new int[n], gSimd = new int[n], bSimd = new int[n];

                ScalarInvIct(y, cb, cr, rRef, gRef, bRef);
                SimdColorTransform.InvIctRow(y, cb, cr, rSimd, gSimd, bSimd);

                // ConvertToInt32 truncates toward zero, identical to the
                // (int) cast used by the scalar reference. Result MUST be
                // bit-exact.
                Assert.Equal(rRef, rSimd);
                Assert.Equal(gRef, gSimd);
                Assert.Equal(bRef, bSimd);
            }
        }

        [Fact]
        public void ForwRct_MatchesScalar_AcrossWidths()
        {
            var rng = new Random(unchecked((int)0xFEEDFACE));
            foreach (var n in Widths)
            {
                int[] r = new int[n], g = new int[n], b = new int[n];
                for (int i = 0; i < n; i++)
                {
                    r[i] = rng.Next(0, 256) - 128;
                    g[i] = rng.Next(0, 256) - 128;
                    b[i] = rng.Next(0, 256) - 128;
                }
                int[] yRef = new int[n], uRef = new int[n], vRef = new int[n];
                int[] ySimd = new int[n], uSimd = new int[n], vSimd = new int[n];

                ScalarForwRct(r, g, b, yRef, uRef, vRef);
                SimdColorTransform.ForwRctRow(r, g, b, ySimd, uSimd, vSimd);

                Assert.Equal(yRef, ySimd);
                Assert.Equal(uRef, uSimd);
                Assert.Equal(vRef, vSimd);
            }
        }

        [Fact]
        public void ForwIct_MatchesScalar_Closely_AcrossWidths()
        {
            // Forward ICT outputs floats; FP order of operations differs only
            // in associativity (a*b+c == FMA vs. mul-then-add). We allow a
            // tiny epsilon relative to the input magnitude.
            var rng = new Random(unchecked((int)0xDEADBEEF));
            foreach (var n in Widths)
            {
                int[] r = new int[n], g = new int[n], b = new int[n];
                for (int i = 0; i < n; i++)
                {
                    r[i] = rng.Next(0, 256) - 128;
                    g[i] = rng.Next(0, 256) - 128;
                    b[i] = rng.Next(0, 256) - 128;
                }
                float[] yRef = new float[n], cbRef = new float[n], crRef = new float[n];
                float[] ySimd = new float[n], cbSimd = new float[n], crSimd = new float[n];

                ScalarForwIct(r, g, b, yRef, cbRef, crRef);
                SimdColorTransform.ForwIctRow(r, g, b, ySimd, cbSimd, crSimd);

                for (int i = 0; i < n; i++)
                {
                    Assert.True(Math.Abs(yRef[i]  - ySimd[i])  <= 1e-3f, $"Y mismatch at {i}: {yRef[i]} vs {ySimd[i]}");
                    Assert.True(Math.Abs(cbRef[i] - cbSimd[i]) <= 1e-3f, $"Cb mismatch at {i}: {cbRef[i]} vs {cbSimd[i]}");
                    Assert.True(Math.Abs(crRef[i] - crSimd[i]) <= 1e-3f, $"Cr mismatch at {i}: {crRef[i]} vs {crSimd[i]}");
                }
            }
        }

        [Fact]
        public void InvRct_FullRoundTrip_WithForwRct()
        {
            // Reversible: forward then inverse must yield original samples.
            var rng = new Random(42);
            const int n = 137; // deliberately misaligned to Vector<int>.Count
            int[] r = new int[n], g = new int[n], b = new int[n];
            for (int i = 0; i < n; i++)
            {
                r[i] = rng.Next(0, 256);
                g[i] = rng.Next(0, 256);
                b[i] = rng.Next(0, 256);
            }
            int[] y = new int[n], u = new int[n], v = new int[n];
            int[] r2 = new int[n], g2 = new int[n], b2 = new int[n];

            SimdColorTransform.ForwRctRow(r, g, b, y, u, v);
            // Inverse expects (Y, Cb, Cr) where the encoder produced
            // (Yr, Ur, Vr) = (Y, B-G, R-G). The inverse uses Cb=Ur, Cr=Vr.
            SimdColorTransform.InvRctRow(y, u, v, r2, g2, b2);

            Assert.Equal(r, r2);
            Assert.Equal(g, g2);
            Assert.Equal(b, b2);
        }
    }
}
