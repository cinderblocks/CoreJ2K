using System;
using System.Reflection;
using Xunit;
using CoreJ2K.Util;

namespace CoreJ2K.Skia.Tests
{
    public class ConversionTests
    {
        [Fact]
        public void ConvertRGB888toRGB888x_ProducesExpectedOutput()
        {
            var width = 2;
            var height = 1;
            var totalPixels = width * height;
            var input = new byte[totalPixels * 3];
            // pixel0 R,G,B = 1,2,3; pixel1 = 4,5,6
            input[0] = 1; input[1] = 2; input[2] = 3;
            input[3] = 4; input[4] = 5; input[5] = 6;

            var type = typeof(SKBitmapImage);
            var mi = type.GetMethod("ConvertRGB888toRGB888x", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            var result = (byte[])mi.Invoke(null, new object[] { width, height, input });

            Assert.Equal(totalPixels * 4, result.Length);
            // Check first pixel: R,G,B,Alpha
            Assert.Equal((byte)1, result[0]);
            Assert.Equal((byte)2, result[1]);
            Assert.Equal((byte)3, result[2]);
            Assert.Equal((byte)0xFF, result[3]);
            // Second pixel
            Assert.Equal((byte)4, result[4]);
            Assert.Equal((byte)5, result[5]);
            Assert.Equal((byte)6, result[6]);
            Assert.Equal((byte)0xFF, result[7]);
        }

        [Fact]
        public void ConvertRGBHM88888toRGBA8888_ProducesExpectedOutput()
        {
            var width = 2;
            var height = 1;
            var totalPixels = width * height;
            var input = new byte[totalPixels * 5];
            // pixel0 R,G,B,H,res = 1,2,3,4,0 ; pixel1 = 5,6,7,8,0
            input[0] = 1; input[1] = 2; input[2] = 3; input[3] = 4; input[4] = 0;
            input[5] = 5; input[6] = 6; input[7] = 7; input[8] = 8; input[9] = 0;

            var type = typeof(SKBitmapImage);
            var mi = type.GetMethod("ConvertRGBHM88888toRGBA8888", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            var result = (byte[])mi.Invoke(null, new object[] { width, height, input });

            Assert.Equal(totalPixels * 4, result.Length);
            // First pixel => R,G,B,H
            Assert.Equal((byte)1, result[0]);
            Assert.Equal((byte)2, result[1]);
            Assert.Equal((byte)3, result[2]);
            Assert.Equal((byte)4, result[3]);
            // Second pixel
            Assert.Equal((byte)5, result[4]);
            Assert.Equal((byte)6, result[5]);
            Assert.Equal((byte)7, result[6]);
            Assert.Equal((byte)8, result[7]);
        }
    }
}
