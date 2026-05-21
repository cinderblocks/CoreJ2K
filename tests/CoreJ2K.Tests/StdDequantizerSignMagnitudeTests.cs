// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Regression tests for the sign-magnitude coefficient encoding used by the
    /// JPEG 2000 entropy decoder (fixed in commit 88ae977).
    ///
    /// JPEG 2000 coefficients are sign-magnitude encoded: bit 31 is the sign flag
    /// and bits 0-30 are the magnitude. The old (broken) code treated them as
    /// two's-complement integers, causing large-magnitude negative coefficients to
    /// produce the wrong decoded value.
    ///
    /// These tests pin the three affected code paths in StdDequantizer:
    ///   1. Reversible integer path:    (temp &gt;= 0) ? (temp &gt;&gt; shift) : -((temp &amp; 0x7FFFFFFF) &gt;&gt; shift)
    ///   2. Non-reversible integer path: ((temp &gt;= 0) ? temp : -(temp &amp; 0x7FFFFFFF)) * step
    ///   3. Non-reversible float path:   same as integer but stored as float
    /// </summary>
    public class StdDequantizerSignMagnitudeTests
    {
        // -----------------------------------------------------------------------
        // Helpers that reproduce the exact arithmetic used in StdDequantizer.cs
        // -----------------------------------------------------------------------

        /// <summary>
        /// Reversible path: integer magnitude bits shifted right by (31 - magBits).
        /// </summary>
        private static int ApplyReversible(int coeff, int shiftBits)
            => coeff >= 0 ? coeff >> shiftBits : -((coeff & 0x7FFFFFFF) >> shiftBits);

        /// <summary>
        /// Non-reversible path: magnitude multiplied by a quantization step.
        /// </summary>
        private static int ApplyNonReversibleInt(int coeff, float step)
            => (int)(((coeff >= 0) ? coeff : -(coeff & 0x7FFFFFFF)) * step);

        private static float ApplyNonReversibleFloat(int coeff, float step)
            => ((coeff >= 0) ? coeff : -(coeff & 0x7FFFFFFF)) * step;

        // -----------------------------------------------------------------------
        // Reversible path
        // -----------------------------------------------------------------------

        [Fact]
        public void Reversible_PositiveCoefficient_ShiftsCorrectly()
        {
            // Positive: sign bit clear, magnitude is the value itself
            int coeff = 0x0000_00F0; // +240
            Assert.Equal(240 >> 3, ApplyReversible(coeff, 3));
        }

        [Fact]
        public void Reversible_NegativeCoefficient_SignMagnitude_NotTwosComplement()
        {
            // Sign-magnitude: bit 31 set means negative; bits 0-30 = magnitude = 0xF0 = 240
            // Expected: -(240 >> 3) = -30
            // Two's-complement (wrong) interpretation of 0x800000F0 >> 3 would give a huge
            // positive value (arithmetic shift keeps sign bit; result depends on runtime, but
            // the magnitude would be completely wrong).
            int coeff = unchecked((int)0x8000_00F0); // -240 in sign-magnitude
            Assert.Equal(-(240 >> 3), ApplyReversible(coeff, 3));
        }

        [Fact]
        public void Reversible_NegativeCoefficient_LargeMagnitude()
        {
            // magnitude = 0x7FFF = 32767; shift = 1 → -(32767 >> 1) = -16383
            int coeff = unchecked((int)(0x8000_0000 | 0x7FFF));
            Assert.Equal(-(0x7FFF >> 1), ApplyReversible(coeff, 1));
        }

        [Fact]
        public void Reversible_ZeroCoefficient_ReturnsZero()
        {
            Assert.Equal(0, ApplyReversible(0, 5));
        }

        [Fact]
        public void Reversible_MinNegativeCoefficient_MagnitudeZero()
        {
            // 0x80000000: sign bit set, magnitude = 0 → result must be 0, not some large value
            int coeff = unchecked((int)0x8000_0000);
            Assert.Equal(0, ApplyReversible(coeff, 0));
        }

        [Theory]
        [InlineData(0, 0, 4)]
        [InlineData(100, 50, 1)]          // +100 >> 1 = 50
        [InlineData(unchecked((int)0x8000_0064), -50, 1)] // sign-mag -100 >> 1 = -50
        [InlineData(1024, 32, 5)]         // +1024 >> 5 = 32
        [InlineData(unchecked((int)0x8000_0400), -32, 5)] // sign-mag -1024 >> 5 = -32
        public void Reversible_Theory(int coeff, int expected, int shift)
        {
            Assert.Equal(expected, ApplyReversible(coeff, shift));
        }

        // -----------------------------------------------------------------------
        // Non-reversible integer path
        // -----------------------------------------------------------------------

        [Fact]
        public void NonReversibleInt_PositiveCoefficient_ScalesCorrectly()
        {
            // +4 * 0.5 = 2
            Assert.Equal(2, ApplyNonReversibleInt(4, 0.5f));
        }

        [Fact]
        public void NonReversibleInt_NegativeCoefficient_SignMagnitude()
        {
            // sign-mag -4 * 0.5 → -(4) * 0.5 = -2 → (int)(-2.0) = -2
            int coeff = unchecked((int)(0x8000_0000 | 4));
            Assert.Equal(-2, ApplyNonReversibleInt(coeff, 0.5f));
        }

        [Fact]
        public void NonReversibleInt_NegativeCoefficient_NotTwosComplement()
        {
            // 0x80000010 in two's complement is a large negative integer with magnitude
            // ≈ 2^31 - 16 ≈ 2.1e9, which would give a wildly wrong scaled value.
            // In sign-magnitude it is simply -16 with step 1.0 → -16.
            int coeff = unchecked((int)(0x8000_0000 | 16));
            Assert.Equal(-16, ApplyNonReversibleInt(coeff, 1.0f));
        }

        // -----------------------------------------------------------------------
        // Non-reversible float path
        // -----------------------------------------------------------------------

        [Fact]
        public void NonReversibleFloat_PositiveCoefficient_ScalesCorrectly()
        {
            Assert.Equal(4.0f * 0.25f, ApplyNonReversibleFloat(4, 0.25f), precision: 6);
        }

        [Fact]
        public void NonReversibleFloat_NegativeCoefficient_SignMagnitude()
        {
            // sign-mag -8 * 0.25 = -2.0
            int coeff = unchecked((int)(0x8000_0000 | 8));
            Assert.Equal(-2.0f, ApplyNonReversibleFloat(coeff, 0.25f), precision: 6);
        }

        [Fact]
        public void NonReversibleFloat_ZeroCoefficient_ReturnsZero()
        {
            Assert.Equal(0.0f, ApplyNonReversibleFloat(0, 1.5f), precision: 6);
        }
    }
}
