// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoreJ2K.Util
{
    internal class WindowsBitmapImage : ImageBase<Image>
    {
        #region CONSTRUCTORS

        internal WindowsBitmapImage(int width, int height, int numComponents, byte[] bytes)
            : base(width, height, numComponents, bytes)
        {
        }

        #endregion

        #region METHODS

        protected override object GetImageObject()
        {
            PixelFormat pixelFormat;
            // TODO: Right now just supporting 8-bit colortypes. Extend in the future.
            switch (NumComponents)
            {
                case 3: pixelFormat = PixelFormat.Format24bppRgb; break;
                case 4: case 5: pixelFormat = PixelFormat.Format32bppArgb; break;
                default:
                    throw new NotImplementedException(
                        $"Image with {NumComponents} components is not supported at this time.");
            }

            var bitmap = new Bitmap(Width, Height, pixelFormat);

            var dstdata = bitmap.LockBits(
                new Rectangle(0, 0, Width, Height),
                ImageLockMode.WriteOnly,
                bitmap.PixelFormat);

            try
            {
                var dstScan0 = dstdata.Scan0;
                var dstStride = dstdata.Stride;

                int bytesPerPixel = (NumComponents == 3) ? 3 : 4;
                var src = Bytes;

                if (NumComponents == 5)
                {
                    // Convert first (5 bytes per pixel -> 4 bytes per pixel)
                    src = ConvertRGBHM88888toRGBA8888(Width, Height, Bytes);
                }

                var srcRowBytes = Width * bytesPerPixel;
                var expectedSrcLen = srcRowBytes * Height;
                if (src == null || src.Length < expectedSrcLen)
                    throw new ArgumentException("Source pixel buffer is too small for the image dimensions.");

                for (var y = 0; y < Height; ++y)
                {
                    var srcOffset = y * srcRowBytes;
                    var destPtr = IntPtr.Add(dstScan0, y * dstStride);
                    Marshal.Copy(src, srcOffset, destPtr, srcRowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(dstdata);
            }

            return bitmap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe byte[] ConvertRGBHM88888toRGBA8888(int width, int height, byte[] input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            // Input is expected to be width * height * 5
            if (input.Length < width * height * 5)
                throw new ArgumentException("Input buffer is too small for RGBHM88888 data.", nameof(input));

            var ret = new byte[width * height * 4];
            var destPos = 0;
            var srcPos = 0;
            fixed (byte* srcPtr = input)
            {
                for (var y = 0; y < height; ++y)
                {
                    for (var x = 0; x < width; ++x)
                    {
                        ret[destPos++] = srcPtr[srcPos++];
                        ret[destPos++] = srcPtr[srcPos++];
                        ret[destPos++] = srcPtr[srcPos++];
                        ret[destPos++] = srcPtr[srcPos++];
                        ++srcPos; // skip the extra channel
                    }
                }
            }

            return ret;
        }

        #endregion
    }
}
