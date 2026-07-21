using System;
using Xunit;
using CoreJ2K.Util;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

namespace CoreJ2K.Windows.Tests
{
    public class WindowsBitmapImageTests
    {
        [Fact]
        public void Create_24bppBitmap_FromBytes_ShouldProduceBitmap()
        {
            var width = 16;
            var height = 8;
            var numComponents = 3;
            var bytes = new byte[width * height * numComponents];
            for (int i = 0; i < bytes.Length; i += 3)
            {
                bytes[i + 0] = 0x10;
                bytes[i + 1] = 0x20;
                bytes[i + 2] = 0x30;
            }

            // Create internal WindowsBitmapImage via reflection
            var asm = Assembly.Load("CoreJ2K.Windows");
            var type = asm.GetType("CoreJ2K.Util.WindowsBitmapImage", throwOnError: true);
            var instance = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { width, height, numComponents, bytes }, null);

            // Call As<Bitmap>() method
            var asMethod = type.GetMethod("As").MakeGenericMethod(typeof(Bitmap));
            var bmp = asMethod.Invoke(instance, null) as Bitmap;

            Assert.NotNull(bmp);
            Assert.Equal(width, bmp.Width);
            Assert.Equal(height, bmp.Height);
            Assert.Equal(PixelFormat.Format24bppRgb, bmp.PixelFormat);

            // Source bytes are R,G,B order; verify channels were not swapped
            var px = bmp.GetPixel(0, 0);
            Assert.Equal(0x10, px.R);
            Assert.Equal(0x20, px.G);
            Assert.Equal(0x30, px.B);
        }

        [Fact]
        public void Create_32bppBitmap_WithAlpha()
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
                bytes[i + 3] = 0x80;
            }

            var asm = Assembly.Load("CoreJ2K.Windows");
            var type = asm.GetType("CoreJ2K.Util.WindowsBitmapImage", throwOnError: true);
            var instance = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { width, height, numComponents, bytes }, null);

            var asMethod = type.GetMethod("As").MakeGenericMethod(typeof(Bitmap));
            var bmp = asMethod.Invoke(instance, null) as Bitmap;

            Assert.NotNull(bmp);
            Assert.Equal(width, bmp.Width);
            Assert.Equal(height, bmp.Height);
            Assert.Equal(PixelFormat.Format32bppArgb, bmp.PixelFormat);

            // Source bytes are R,G,B,A order; verify channels were not swapped
            var px = bmp.GetPixel(0, 0);
            Assert.Equal(0xFF, px.R);
            Assert.Equal(0x00, px.G);
            Assert.Equal(0x00, px.B);
            Assert.Equal(0x80, px.A);
        }

        [Fact]
        public void Create_8bppGrayscaleBitmap_FromBytes_ShouldProduceIndexedBitmapWithGrayRamp()
        {
            var width = 16;
            var height = 8;
            var numComponents = 1;
            var bytes = new byte[width * height];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 256);
            }

            var asm = Assembly.Load("CoreJ2K.Windows");
            var type = asm.GetType("CoreJ2K.Util.WindowsBitmapImage", throwOnError: true);
            var instance = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { width, height, numComponents, bytes }, null);

            var asMethod = type.GetMethod("As").MakeGenericMethod(typeof(Bitmap));
            var bmp = asMethod.Invoke(instance, null) as Bitmap;

            Assert.NotNull(bmp);
            Assert.Equal(width, bmp.Width);
            Assert.Equal(height, bmp.Height);
            Assert.Equal(PixelFormat.Format8bppIndexed, bmp.PixelFormat);

            // Palette must be a grayscale ramp so index i renders as gray level i
            Assert.Equal(256, bmp.Palette.Entries.Length);
            for (int i = 0; i < 256; i++)
            {
                var entry = bmp.Palette.Entries[i];
                Assert.Equal(i, entry.R);
                Assert.Equal(i, entry.G);
                Assert.Equal(i, entry.B);
            }

            // Pixel indices must match the source luminance bytes
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
            try
            {
                var row = new byte[width];
                for (int y = 0; y < height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, width);
                    for (int x = 0; x < width; x++)
                    {
                        Assert.Equal(bytes[y * width + x], row[x]);
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        [Fact]
        public void WindowsBitmapImageSource_Reads_24bppBitmap()
        {
            var width = 8;
            var height = 4;
            using (var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(System.Drawing.Color.FromArgb(0x11, 0x22, 0x33));

                // Call internal static Create via reflection
                var asm = Assembly.Load("CoreJ2K.Windows");
                var type = asm.GetType("CoreJ2K.Util.WindowsBitmapImageSource", throwOnError: true);
                var mi = type.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static);
                var src = mi.Invoke(null, new object[] { bmp });
                Assert.NotNull(src);
            }
        }
    }
}
