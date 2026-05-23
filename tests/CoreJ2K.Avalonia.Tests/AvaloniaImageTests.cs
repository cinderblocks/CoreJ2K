// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Avalonia.Tests
{
    public class AvaloniaImageTests
    {
        public AvaloniaImageTests()
        {
            HeadlessAvalonia.EnsureStarted();
        }

        private static WriteableBitmap CreateBitmap(int w, int h, int n, byte[] bytes)
            => HeadlessAvalonia.Invoke(() => new AvaloniaImage(w, h, n, bytes).As<WriteableBitmap>());

        [Fact]
        public void Create_RGB_FromBytes_ShouldProduceBgra8888Bitmap()
        {
            const int width = 8;
            const int height = 4;
            const int numComponents = 3;
            var bytes = new byte[width * height * numComponents];
            for (var i = 0; i < bytes.Length; i += 3)
            {
                bytes[i + 0] = 0x10;
                bytes[i + 1] = 0x20;
                bytes[i + 2] = 0x30;
            }

            var bmp = CreateBitmap(width, height, numComponents, bytes);

            Assert.NotNull(bmp);
            Assert.Equal(width, bmp.PixelSize.Width);
            Assert.Equal(height, bmp.PixelSize.Height);
            Assert.Equal(PixelFormat.Bgra8888, bmp.Format);

            using var fb = bmp.Lock();
            unsafe
            {
                var p = (byte*)fb.Address.ToPointer();
                Assert.Equal(0x30, p[0]);
                Assert.Equal(0x20, p[1]);
                Assert.Equal(0x10, p[2]);
                Assert.Equal(0xFF, p[3]);
            }
        }

        [Fact]
        public void Create_RGBA_FromBytes_ShouldPreserveAlpha()
        {
            const int width = 2;
            const int height = 2;
            const int numComponents = 4;
            var bytes = new byte[width * height * numComponents];
            for (var i = 0; i < bytes.Length; i += 4)
            {
                bytes[i + 0] = 0xAA;
                bytes[i + 1] = 0xBB;
                bytes[i + 2] = 0xCC;
                bytes[i + 3] = 0x80;
            }

            var bmp = CreateBitmap(width, height, numComponents, bytes);

            Assert.NotNull(bmp);
            using var fb = bmp.Lock();
            unsafe
            {
                var p = (byte*)fb.Address.ToPointer();
                Assert.Equal(0xCC, p[0]);
                Assert.Equal(0xBB, p[1]);
                Assert.Equal(0xAA, p[2]);
                Assert.Equal(0x80, p[3]);
            }
        }

        [Fact]
        public void Create_Grayscale_FromBytes_ShouldReplicateChannels()
        {
            const int width = 4;
            const int height = 4;
            const int numComponents = 1;
            var bytes = new byte[width * height];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = 0x55;

            var bmp = CreateBitmap(width, height, numComponents, bytes);

            Assert.NotNull(bmp);
            using var fb = bmp.Lock();
            unsafe
            {
                var p = (byte*)fb.Address.ToPointer();
                Assert.Equal(0x55, p[0]);
                Assert.Equal(0x55, p[1]);
                Assert.Equal(0x55, p[2]);
                Assert.Equal(0xFF, p[3]);
            }
        }

        [Fact]
        public void Create_FiveComponents_ShouldUseFirstFourAsRGBA()
        {
            const int width = 2;
            const int height = 2;
            const int numComponents = 5;
            var bytes = new byte[width * height * 5];
            for (var i = 0; i < width * height; i++)
            {
                bytes[i * 5 + 0] = 0x01;
                bytes[i * 5 + 1] = 0x02;
                bytes[i * 5 + 2] = 0x03;
                bytes[i * 5 + 3] = 0x04;
                bytes[i * 5 + 4] = 0x00;
            }

            var bmp = CreateBitmap(width, height, numComponents, bytes);

            Assert.NotNull(bmp);
            using var fb = bmp.Lock();
            unsafe
            {
                var p = (byte*)fb.Address.ToPointer();
                Assert.Equal(0x03, p[0]);
                Assert.Equal(0x02, p[1]);
                Assert.Equal(0x01, p[2]);
                Assert.Equal(0x04, p[3]);
            }
        }

        [Fact]
        public void Create_UnsupportedComponentCount_Throws()
        {
            var ex = Assert.ThrowsAny<Exception>(() =>
                CreateBitmap(4, 4, 7, new byte[4 * 4 * 7]));
            while (ex.InnerException != null) ex = ex.InnerException;
            Assert.IsType<NotImplementedException>(ex);
        }
    }
}
