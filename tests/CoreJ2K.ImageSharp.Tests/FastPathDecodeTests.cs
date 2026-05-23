// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.IO;
using CoreJ2K.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CoreJ2K.ImageSharp.Tests
{
    public class FastPathDecodeTests
    {
        [Fact]
        public void DecodeToImage_FastPath_MatchesLegacyDecode_For8BitRgb()
        {
            // Build a small synthetic 8-bit RGBA image with a deterministic pattern
            const int w = 16;
            const int h = 12;
            using var original = new Image<Rgba32>(w, h);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    original[x, y] = new Rgba32(
                        (byte)((x * 17 + y * 3) & 0xFF),
                        (byte)((x * 5 + y * 23) & 0xFF),
                        (byte)((x * 13 + y * 7) & 0xFF),
                        (byte)0xFF);
                }
            }

            // Encode lossless so a round-trip preserves samples exactly
            var j2kBytes = original.EncodeToJ2KLossless();
            Assert.NotNull(j2kBytes);
            Assert.True(j2kBytes.Length > 0);

            // Legacy path: InterleavedImage -> As<Image<Rgba32>>()
            Image<Rgba32> legacy;
            using (var ms = new MemoryStream(j2kBytes))
            using (var img = J2kImage.FromStream(ms))
            {
                legacy = img.As<Image<Rgba32>>();
            }

            // Fast path: DecodeToImage<T> goes straight to bytes
            Image<Rgba32> fast;
            using (var ms = new MemoryStream(j2kBytes))
            {
                fast = J2kImage.DecodeToImage<Image<Rgba32>>(ms);
            }

            Assert.Equal(legacy.Width, fast.Width);
            Assert.Equal(legacy.Height, fast.Height);

            for (var y = 0; y < legacy.Height; y++)
            {
                for (var x = 0; x < legacy.Width; x++)
                {
                    Assert.Equal(legacy[x, y], fast[x, y]);
                }
            }

            legacy.Dispose();
            fast.Dispose();
        }
    }
}
