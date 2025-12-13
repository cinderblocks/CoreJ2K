// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image.input;
using SkiaSharp;
using System;
using System.Linq;

namespace CoreJ2K.Util
{
    public class SKBitmapImageSource : InterleavedImageSource
    {
        /// <summary>DC offset value used when reading image </summary>
        private const int DC_OFFSET = 128;

        #region CONSTRUCTORS

        private SKBitmapImageSource(SKBitmap bitmap)
            : base(bitmap.Width, bitmap.Height
            , ImgReaderSkia.GetNumberOfComponents(bitmap.Info)
            , bitmap.Info.BytesPerPixel
            , GetSignedArray(bitmap)
            , GetComponents(bitmap))
        { }

        #endregion

        #region METHODS

        public static int[][] GetComponents(SKBitmap image)
        {
            var w = image.Width;
            var h = image.Height;
            var nc = ImgReaderSkia.GetNumberOfComponents(image.Info);
            var safePtr = image.GetPixels();

            var barr = new int[nc][];
            for (var c = 0; c < nc; ++c) { barr[c] = new int[w * h]; }

            var total = w * h;

            // Determine whether the pixel layout needs red/blue swizzling
            var swizzle = image.ColorType == SKColorType.Bgra8888
                          || image.ColorType == SKColorType.Bgra1010102
                          || image.ColorType == SKColorType.Bgr101010x;

            unsafe
            {
                var ptr = (byte*)safePtr.ToPointer();
                var bpp = image.BytesPerPixel;

                if (nc == 1)
                {
                    var comp0 = barr[0];
                    for (var k = 0; k < total; ++k)
                    {
                        comp0[k] = ptr[0] - DC_OFFSET;
                        ptr += bpp;
                    }
                }
                else if (nc == 2)
                {
                    var comp0 = barr[0];
                    var comp1 = barr[1];
                    for (var k = 0; k < total; ++k)
                    {
                        comp0[k] = ptr[0] - DC_OFFSET;
                        comp1[k] = ptr[1] - DC_OFFSET;
                        ptr += bpp;
                    }
                }
                else if (nc == 3)
                {
                    var red = barr[0];
                    var green = barr[1];
                    var blue = barr[2];

                    if (swizzle)
                    {
                        // BGRA / BGR order in memory
                        for (var k = 0; k < total; ++k)
                        {
                            red[k] = ptr[2] - DC_OFFSET;
                            green[k] = ptr[1] - DC_OFFSET;
                            blue[k] = ptr[0] - DC_OFFSET;
                            ptr += bpp;
                        }
                    }
                    else
                    {
                        for (var k = 0; k < total; ++k)
                        {
                            red[k] = ptr[0] - DC_OFFSET;
                            green[k] = ptr[1] - DC_OFFSET;
                            blue[k] = ptr[2] - DC_OFFSET;
                            ptr += bpp;
                        }
                    }
                }
                else // nc >= 4
                {
                    var red = barr[0];
                    var green = barr[1];
                    var blue = barr[2];
                    var alpha = barr[3];

                    if (swizzle)
                    {
                        for (var k = 0; k < total; ++k)
                        {
                            red[k] = ptr[2] - DC_OFFSET;
                            green[k] = ptr[1] - DC_OFFSET;
                            blue[k] = ptr[0] - DC_OFFSET;
                            alpha[k] = ptr[3] - DC_OFFSET;
                            ptr += bpp;
                        }
                    }
                    else
                    {
                        for (var k = 0; k < total; ++k)
                        {
                            red[k] = ptr[0] - DC_OFFSET;
                            green[k] = ptr[1] - DC_OFFSET;
                            blue[k] = ptr[2] - DC_OFFSET;
                            alpha[k] = ptr[3] - DC_OFFSET;
                            ptr += bpp;
                        }
                    }
                }
            }

            return barr;
        }

        private static bool[] GetSignedArray(SKBitmap bitmap)
        {
            // all components are unsigned for SKBitmap-backed images; allocate directly
            var nc = ImgReaderSkia.GetNumberOfComponents(bitmap.Info);
            return new bool[nc];
        }

        #endregion
    }
}