// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using Xunit;
using CoreJ2K;
using CoreJ2K.Util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Encoder option handling: the segmentation-symbol error-resilience feature works, and an
    /// unknown/misspelled encoder option fails with a clear error rather than silently returning
    /// null (which previously surfaced as a cryptic ArgumentNullException downstream).
    /// </summary>
    public class EncoderOptionValidationTests
    {
        private static (InterleavedImageSource src, int[] pixels) MakeGray(int w, int h)
        {
            var pixels = new int[w * h];
            var centred = new int[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (i * 37) & 0xFF;
                centred[i] = pixels[i] - 128;
            }
            return (new InterleavedImageSource(w, h, 1, 8, new[] { false }, new[] { centred }), pixels);
        }

        [Fact]
        public void SegmentationSymbols_RoundTripsExactly()
        {
            const int w = 64, h = 64;
            var (src, pixels) = MakeGray(w, h);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            pl["Cseg_symbol"] = "on"; // the correct option name for segmentation symbols

            var encoded = J2kImage.ToBytes(src, null, pl);
            var decoded = J2kImage.FromBytes(encoded!);

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < pixels.Length; i++)
                Assert.True(pixels[i] == comp[i], $"sample {i}: expected {pixels[i]}, got {comp[i]}");
        }

        [Fact]
        public void UnknownOption_ThrowsClearError_NotNull()
        {
            const int w = 64, h = 64;
            var (src, _) = MakeGray(w, h);

            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            pl["CsegSymbol"] = "on"; // misspelled (real option is 'Cseg_symbol')

            var ex = Assert.Throws<InvalidOperationException>(() => J2kImage.ToBytes(src, null, pl));

            // The clear "not a valid" message must surface (here, from the entropy coder option check).
            var full = ex.Message + " " + (ex.InnerException?.Message ?? "");
            Assert.Contains("CsegSymbol", full);
            Assert.Contains("not a valid", full);
        }
    }
}
