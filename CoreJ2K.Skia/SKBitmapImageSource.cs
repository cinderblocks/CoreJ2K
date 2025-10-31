// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image.input;
using SkiaSharp;
using System;
using System.Linq;

namespace CoreJ2K.Util
{
    public class SKBitmapImageSource : PortableImageSource
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
            var red = barr[0];
            var green = nc > 1 ? barr[1] : null;
            var blue = nc > 2 ? barr[2] : null;
            var alpha = nc > 3 ? barr[3] : null;

            unsafe
            {
                var ptr = (byte*)safePtr.ToPointer();
                switch (image.ColorType)
                {
                    case SKColorType.Bgra8888:
                    case SKColorType.Rgba8888:
                    case SKColorType.Rgb888x:
                    case SKColorType.Alpha8:
                    case SKColorType.Gray8:
                    case SKColorType.Rg88:
                        {
                            var k = 0;
                            for (var j = 0; j < w * h; ++j)
                            {
                                red[k] = (*(ptr + 0) & 0xFF) - DC_OFFSET;
                                if (green != null) { green[k] = (*(ptr + 1) & 0xFF) - DC_OFFSET; }
                                if (blue != null) { blue[k] = (*(ptr + 2) & 0xFF) - DC_OFFSET; }
                                if (alpha != null) { alpha[k] = (*(ptr + 3) & 0xFF) - DC_OFFSET; }

                                ++k;
                                ptr += image.BytesPerPixel;
                            }
                        }
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Colortype {nameof(image.ColorType)} not currently supported.");
                }
            }

            return barr;
        }

        private static bool[] GetSignedArray(SKBitmap bitmap)
        {
            return Enumerable.Repeat(false, ImgReaderSkia.GetNumberOfComponents(bitmap.Info)).ToArray();
        }

        #endregion
    }
}