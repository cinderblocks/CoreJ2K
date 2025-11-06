// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace CoreJ2K.Util
{
    internal class WindowsBitmapImageSource : PortableImageSource
    {
        #region CONSTRUCTORS

        private WindowsBitmapImageSource(Bitmap bitmap)
            : base(
                bitmap.Width,
                bitmap.Height,
                GetNumberOfComponents(bitmap.PixelFormat),
                GetRangeBits(bitmap.PixelFormat),
                GetSignedArray(bitmap.PixelFormat),
                GetComponents(bitmap))
        {
        }

        #endregion

        #region METHODS


        internal static BlkImgDataSrc Create(object imageObject)
        {
            return !(imageObject is Bitmap bitmap) ? null : new WindowsBitmapImageSource(bitmap);
        }

        private static int GetNumberOfComponents(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format16bppGrayScale:
                case PixelFormat.Format1bppIndexed:
                case PixelFormat.Format4bppIndexed:
                case PixelFormat.Format8bppIndexed:
                    return 1;
                case PixelFormat.Format24bppRgb:
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return 3;
                case PixelFormat.Format16bppRgb555:
                case PixelFormat.Format16bppRgb565:
                case PixelFormat.Format16bppArgb1555:
                case PixelFormat.Format48bppRgb:
                case PixelFormat.Format64bppPArgb:
                case PixelFormat.Format64bppArgb:
                default:
                    throw new ArgumentOutOfRangeException(nameof(pixelFormat));
            }
        }

        private static int GetRangeBits(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format16bppGrayScale:
                    return 16;
                case PixelFormat.Format1bppIndexed:
                    return 1;
                case PixelFormat.Format4bppIndexed:
                    return 4;
                case PixelFormat.Format8bppIndexed:
                case PixelFormat.Format24bppRgb:
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return 8;
                case PixelFormat.Format16bppRgb555:
                case PixelFormat.Format16bppRgb565:
                case PixelFormat.Format16bppArgb1555:
                case PixelFormat.Format48bppRgb:
                case PixelFormat.Format64bppPArgb:
                case PixelFormat.Format64bppArgb:
                default:
                    throw new ArgumentOutOfRangeException(nameof(pixelFormat));
            }
        }

        private static bool[] GetSignedArray(PixelFormat pixelFormat)
        {
            return Enumerable.Repeat(false, GetNumberOfComponents(pixelFormat)).ToArray();
        }

        private static int[][] GetComponents(Bitmap bitmap)
        {
            var w = bitmap.Width;
            var h = bitmap.Height;
            var nc = GetNumberOfComponents(bitmap.PixelFormat);

            var comps = new int[nc][];
            for (var c = 0; c < nc; ++c) comps[c] = new int[w * h];

            // Fast paths for common formats using LockBits (avoids slow GetPixel calls)
            switch (bitmap.PixelFormat)
            {
                case PixelFormat.Format24bppRgb:
                    {
                        var rect = new Rectangle(0, 0, w, h);
                        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                        try
                        {
                            unsafe
                            {
                                var ptr = (byte*)data.Scan0.ToPointer();
                                var stride = data.Stride;
                                for (var y = 0; y < h; ++y)
                                {
                                    var row = ptr + y * stride;
                                    var baseIdx = y * w;
                                    for (var x = 0; x < w; ++x)
                                    {
                                        var b = row[x * 3 + 0];
                                        var g = row[x * 3 + 1];
                                        var r = row[x * 3 + 2];

                                        var idx = baseIdx + x;
                                        comps[0][idx] = r;
                                        comps[1][idx] = g;
                                        comps[2][idx] = b;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            bitmap.UnlockBits(data);
                        }
                    }
                    break;

                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    {
                        var rect = new Rectangle(0, 0, w, h);
                        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                        try
                        {
                            unsafe
                            {
                                var ptr = (byte*)data.Scan0.ToPointer();
                                var stride = data.Stride;
                                for (var y = 0; y < h; ++y)
                                {
                                    var row = ptr + y * stride;
                                    var baseIdx = y * w;
                                    for (var x = 0; x < w; ++x)
                                    {
                                        var b = row[x * 4 + 0];
                                        var g = row[x * 4 + 1];
                                        var r = row[x * 4 + 2];

                                        var idx = baseIdx + x;
                                        comps[0][idx] = r;
                                        comps[1][idx] = g;
                                        comps[2][idx] = b;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            bitmap.UnlockBits(data);
                        }
                    }
                    break;

                default:
                    // Fall back to GetPixel for indexed or uncommon formats
                    for (int y = 0, xy = 0; y < h; ++y)
                    {
                        for (var x = 0; x < w; ++x, ++xy)
                        {
                            var color = bitmap.GetPixel(x, y);
                            for (var c = 0; c < nc; ++c)
                            {
                                comps[c][xy] = c == 0 ? color.R : c == 1 ? color.G : color.B;
                            }
                        }
                    }
                    break;
            }

            return comps;
        }

        #endregion
    }
}