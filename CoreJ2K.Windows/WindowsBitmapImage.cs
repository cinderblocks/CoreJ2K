// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

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
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            var ptr = dstdata.Scan0;
            switch (NumComponents)
            {
                case 5:
                    var pix = ConvertRGBHM88888toRGBA8888(Width, Height, Bytes);
                    System.Runtime.InteropServices.Marshal.Copy(pix, 0, ptr, Bytes.Length);
                    break;
                default:
                    System.Runtime.InteropServices.Marshal.Copy(Bytes, 0, ptr, Bytes.Length);
                    break;
            }

            bitmap.UnlockBits(dstdata);

            return bitmap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe byte[] ConvertRGBHM88888toRGBA8888(int width, int height, byte[] input)
        {
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
                        ++srcPos;
                    }
                }
            }

            return ret;
        }

        #endregion
    }
}
