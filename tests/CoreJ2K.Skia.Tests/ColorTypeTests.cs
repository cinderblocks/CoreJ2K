// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CoreJ2K.Configuration;
using SkiaSharp;
using Xunit;

namespace CoreJ2K.Skia.Tests
{
    /// <summary>
    /// The loader reads one byte per component, so packed and 16-bit colour types are converted to the 8-bit type with the same
    /// components first.
    /// </summary>
    public class ColorTypeTests
    {
        private static SKBitmap RandomBitmap(SKColorType colorType, SKAlphaType alphaType)
        {
            var bitmap = new SKBitmap(new SKImageInfo(24, 16, colorType, alphaType));
            var bytes = new byte[bitmap.ByteCount];
            new Random(5).NextBytes(bytes);
            Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
            return bitmap;
        }

        [Theory]
        [InlineData(SKColorType.Rgba16161616, SKAlphaType.Unpremul, 4)]
        [InlineData(SKColorType.Rgba1010102, SKAlphaType.Unpremul, 4)]
        [InlineData(SKColorType.Rgb565, SKAlphaType.Opaque, 3)]
        [InlineData(SKColorType.Rgb101010x, SKAlphaType.Opaque, 3)]
        public void PackedAndWideColorTypes_EncodeTheirPixels(SKColorType colorType, SKAlphaType alphaType, int components)
        {
            using var bitmap = RandomBitmap(colorType, alphaType);
            var data = bitmap.EncodeToJ2K(new J2KEncoderConfiguration().WithLossless());
            var decoded = J2kImage.FromBytes(data);

            Assert.Equal(components, decoded.NumberOfComponents);
            Assert.All(decoded.BitDepths, d => Assert.Equal(8, d));

            // What Skia itself makes of the pixels at 8 bits
            using var expected = new SKBitmap(new SKImageInfo(24, 16, SKColorType.Rgba8888, alphaType));
            Assert.True(bitmap.PeekPixels().ReadPixels(expected.Info, expected.GetPixels(), expected.RowBytes));
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 24; x++)
                {
                    var px = expected.GetPixel(x, y);
                    var got = decoded.GetPixel(x, y);
                    Assert.Equal(new[] { (int)px.Red, px.Green, px.Blue }, got.Take(3));
                }
        }

        [Fact]
        public void SixteenBitAlpha_AndTwoChannelTypes_AreConverted()
        {
            foreach (var colorType in new[] { SKColorType.Alpha16, SKColorType.Rg1616 })
            {
                using var bitmap = RandomBitmap(colorType, SKAlphaType.Premul);
                var decoded = J2kImage.FromBytes(bitmap.EncodeToJ2K(new J2KEncoderConfiguration().WithLossless()));
                Assert.All(decoded.BitDepths, d => Assert.Equal(8, d));
                // random 16-bit data spreads over the range
                Assert.True(decoded.GetComponent(0).Max() - decoded.GetComponent(0).Min() > 100);
            }
        }

        /// <summary>The builder's metadata lives outside the J2KEncoderConfiguration, so the helper that takes a builder must write it from the builder.</summary>
        [Fact]
        public void BuilderMetadata_ReachesTheOutput()
        {
            using var bitmap = RandomBitmap(SKColorType.Rgba8888, SKAlphaType.Opaque);
            var data = bitmap.EncodeToJ2K(new CompleteEncoderConfigurationBuilder().ForBalanced().WithCopyright("(c) probe"));

            using var stream = new MemoryStream(data);
            J2kImage.FromStream(stream, out var metadata);
            Assert.Single(metadata.IntellectualPropertyRights);
        }
    }
}
