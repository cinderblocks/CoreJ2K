using System;
using Xunit;
using CoreJ2K.Util;
using SkiaSharp;

namespace CoreJ2K.Skia.Tests
{
    public class SKBitmapImageTests
    {
        [Fact]
        public void Create_RGBBitmap_FromBytes_ShouldProduceSKBitmap()
        {
            var width = 16;
            var height = 8;
            var numComponents = 3;
            var bytes = new byte[width * height * numComponents];
            // Fill with a simple gradient
            for (int i = 0; i < bytes.Length; i += 3)
            {
                bytes[i + 0] = 0x10; // R
                bytes[i + 1] = 0x20; // G
                bytes[i + 2] = 0x30; // B
            }

            var image = new SKBitmapImage(width, height, numComponents, bytes);
            var sk = image.As<SKBitmap>();

            Assert.NotNull(sk);
            Assert.Equal(width, sk.Width);
            Assert.Equal(height, sk.Height);
            Assert.Equal(SKColorType.Rgb888x, sk.Info.ColorType);
        }

        [Fact]
        public void Create_RGBA_FromBytes_ShouldProduceSKBitmapWithAlpha()
        {
            var width = 4;
            var height = 4;
            var numComponents = 4;
            var bytes = new byte[width * height * numComponents];
            for (int i = 0; i < bytes.Length; i += 4)
            {
                bytes[i + 0] = 0xFF;
                bytes[i + 1] = 0x00;
                bytes[i + 2] = 0x00;
                bytes[i + 3] = 0x80; // alpha
            }

            var image = new SKBitmapImage(width, height, numComponents, bytes);
            var sk = image.As<SKBitmap>();

            Assert.NotNull(sk);
            Assert.Equal(width, sk.Width);
            Assert.Equal(height, sk.Height);
            Assert.Equal(SKColorType.Rgba8888, sk.Info.ColorType);
        }

        [Fact]
        public void Create_RGBHM5Component_ShouldConvertToRGBA()
        {
            var width = 2;
            var height = 2;
            var numComponents = 5;
            var bytes = new byte[width * height * 5];
            // Fill R,G,B,H, reserved
            for (int i = 0, p = 0; i < width * height; ++i)
            {
                bytes[p++] = 0x01;
                bytes[p++] = 0x02;
                bytes[p++] = 0x03;
                bytes[p++] = 0x04;
                bytes[p++] = 0x00; // reserved
            }

            var image = new SKBitmapImage(width, height, numComponents, bytes);
            var sk = image.As<SKBitmap>();

            Assert.NotNull(sk);
            Assert.Equal(width, sk.Width);
            Assert.Equal(height, sk.Height);
            Assert.Equal(SKColorType.Rgba8888, sk.Info.ColorType);
        }
    }
}
