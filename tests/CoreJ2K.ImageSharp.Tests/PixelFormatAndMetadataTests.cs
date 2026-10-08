// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CoreJ2K.ImageSharp.Tests
{
    public class PixelFormatAndMetadataTests
    {
        /// <summary>16-bit pixel types are read as full 16-bit samples, matching the bit depth the reader declares.</summary>
        [Fact]
        public void L16_KeepsItsFullRange_Losslessly()
        {
            ushort[] values = { 0, 1, 255, 256, 1000, 32768, 40000, 65535 };
            using var image = new Image<L16>(values.Length, 3);
            for (var y = 0; y < 3; y++)
                for (var x = 0; x < values.Length; x++)
                    image[x, y] = new L16(values[x]);

            var data = image.EncodeToJ2K(new J2KEncoderConfiguration().WithLossless());
            var decoded = J2kImage.FromBytes(data);

            Assert.Equal(16, decoded.GetBitDepth(0));
            Assert.Equal(values.Select(v => (int)v), decoded.GetComponent(0).Take(values.Length));

            using var eightBit = ImageSharpJ2kExtensions.FromJ2KBytes(data);
            Assert.Equal(values.Select(v => (byte)((v + 128) / 257)), Enumerable.Range(0, values.Length).Select(x => eightBit[x, 0].R));
        }

        [Fact]
        public void Rgba64_KeepsItsFullRange_Losslessly()
        {
            using var image = new Image<Rgba64>(4, 2);
            for (var y = 0; y < 2; y++)
                for (var x = 0; x < 4; x++)
                    image[x, y] = new Rgba64((ushort)(x * 20000), (ushort)(65535 - x * 15000), (ushort)(y * 30000 + x), (ushort)(65535 - y * 40000));

            var decoded = J2kImage.FromBytes(image.EncodeToJ2K(new J2KEncoderConfiguration().WithLossless()));

            Assert.Equal(4, decoded.NumberOfComponents);
            for (var y = 0; y < 2; y++)
                for (var x = 0; x < 4; x++)
                {
                    var px = image[x, y];
                    Assert.Equal(new[] { (int)px.R, px.G, px.B, px.A }, decoded.GetPixel(x, y));
                }
        }

        /// <summary>The builder's metadata lives outside the J2KEncoderConfiguration, so the helpers that take a builder must write it from the builder.</summary>
        [Fact]
        public void BuilderMetadata_ReachesTheOutput()
        {
            using var image = new Image<Rgb24>(32, 24);
            for (var y = 0; y < 24; y++)
                for (var x = 0; x < 32; x++)
                    image[x, y] = new Rgb24((byte)(x * 7), (byte)(y * 9), (byte)(x ^ y));

            foreach (var data in new[]
            {
                image.EncodeToJ2KHighQuality("(c) probe"),
                image.EncodeToJ2KWeb("(c) probe"),
                image.EncodeToJ2K(new CompleteEncoderConfigurationBuilder().ForBalanced().WithCopyright("(c) probe")),
            })
            {
                using var stream = new MemoryStream(data);
                J2kImage.FromStream(stream, out var metadata);
                Assert.Single(metadata.IntellectualPropertyRights);
            }
        }
    }
}
