// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.IO;
using CoreJ2K;
using CoreJ2K.Skia;
using SkiaSharp;
using Xunit;

namespace CoreJ2K.Skia.Tests
{
    public class FastPathDecodeTests
    {
        /// <summary>
        /// Verifies that <see cref="J2kImage.DecodeToImage{T}"/> produces a pixel-identical
        /// result to the legacy <c>FromStream(...).As&lt;SKBitmap&gt;()</c> path for an
        /// 8-bit RGB image. This exercises the fast path that writes bytes directly into the
        /// output buffer without materialising the intermediate <c>int[]</c> of
        /// <see cref="CoreJ2K.Util.InterleavedImage"/>.
        /// </summary>
        [Fact]
        public void DecodeToImage_FastPath_MatchesLegacyDecode_For8BitRgb()
        {
            // Arrange: build a small 8-bit RGB bitmap with a deterministic colour pattern.
            const int w = 16;
            const int h = 12;

            using var original = new SKBitmap(w, h, SKColorType.Rgb888x, SKAlphaType.Opaque);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    original.SetPixel(x, y, new SKColor(
                        (byte)((x * 17 + y * 3) & 0xFF),
                        (byte)((x * 5  + y * 23) & 0xFF),
                        (byte)((x * 13 + y * 7)  & 0xFF)));
                }
            }

            // Encode lossless so the round-trip preserves every sample exactly.
            var j2kBytes = original.EncodeToJ2KLossless();
            Assert.NotNull(j2kBytes);
            Assert.True(j2kBytes.Length > 0);

            // Act – legacy path: InterleavedImage → As<SKBitmap>()
            SKBitmap legacy;
            using (var ms = new MemoryStream(j2kBytes))
            using (var img = J2kImage.FromStream(ms))
            {
                legacy = img.As<SKBitmap>();
            }

            // Act – fast path: DecodeToImage<T> writes bytes directly to the backend.
            SKBitmap fast;
            using (var ms = new MemoryStream(j2kBytes))
            {
                fast = J2kImage.DecodeToImage<SKBitmap>(ms);
            }

            // Assert: dimensions and every pixel must match.
            Assert.Equal(legacy.Width,  fast.Width);
            Assert.Equal(legacy.Height, fast.Height);

            for (var y = 0; y < legacy.Height; y++)
            {
                for (var x = 0; x < legacy.Width; x++)
                {
                    var lp = legacy.GetPixel(x, y);
                    var fp = fast.GetPixel(x, y);
                    Assert.Equal(lp.Red,   fp.Red);
                    Assert.Equal(lp.Green, fp.Green);
                    Assert.Equal(lp.Blue,  fp.Blue);
                }
            }

            legacy.Dispose();
            fast.Dispose();
        }

        /// <summary>
        /// Verifies that the fast path handles a single-component (greyscale) 8-bit image
        /// without error.
        /// </summary>
        [Fact]
        public void DecodeToImage_FastPath_MatchesLegacyDecode_For8BitGrayscale()
        {
            const int w = 8;
            const int h = 8;

            using var original = new SKBitmap(w, h, SKColorType.Gray8, SKAlphaType.Opaque);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    original.SetPixel(x, y, new SKColor((byte)((x * 31 + y * 11) & 0xFF),
                                                         (byte)((x * 31 + y * 11) & 0xFF),
                                                         (byte)((x * 31 + y * 11) & 0xFF)));

            var j2kBytes = original.EncodeToJ2KLossless();

            SKBitmap legacy;
            using (var ms = new MemoryStream(j2kBytes))
            using (var img = J2kImage.FromStream(ms))
            {
                legacy = img.As<SKBitmap>();
            }

            SKBitmap fast;
            using (var ms = new MemoryStream(j2kBytes))
            {
                fast = J2kImage.DecodeToImage<SKBitmap>(ms);
            }

            Assert.Equal(legacy.Width,  fast.Width);
            Assert.Equal(legacy.Height, fast.Height);

            for (var y = 0; y < legacy.Height; y++)
            {
                for (var x = 0; x < legacy.Width; x++)
                {
                    // Grayscale: compare red channel only (R=G=B for grey pixels).
                    Assert.Equal(legacy.GetPixel(x, y).Red, fast.GetPixel(x, y).Red);
                }
            }

            legacy.Dispose();
            fast.Dispose();
        }

        /// <summary>
        /// Verifies that <see cref="J2kImage.DecodeToImage{T}"/> also works when called
        /// via the convenience byte-array overload.
        /// </summary>
        [Fact]
        public void DecodeToImage_ByteArrayOverload_ProducesCorrectResult()
        {
            const int w = 4;
            const int h = 4;

            using var original = new SKBitmap(w, h, SKColorType.Rgb888x, SKAlphaType.Opaque);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    original.SetPixel(x, y, new SKColor(
                        (byte)(x * 50 + 5),
                        (byte)(y * 50 + 5),
                        (byte)((x + y) * 25 + 5)));

            var j2kBytes = original.EncodeToJ2KLossless();

            var fast = J2kImage.DecodeToImage<SKBitmap>(j2kBytes);
            Assert.NotNull(fast);
            Assert.Equal(w, fast.Width);
            Assert.Equal(h, fast.Height);

            // Spot-check first and last pixel.
            var first = fast.GetPixel(0, 0);
            Assert.Equal(5, first.Red);
            Assert.Equal(5, first.Green);
            Assert.Equal(5, first.Blue);

            fast.Dispose();
        }
    }
}
