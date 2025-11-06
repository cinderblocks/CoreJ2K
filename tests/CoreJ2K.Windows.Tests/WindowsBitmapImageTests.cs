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
