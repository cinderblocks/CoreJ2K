// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using SkiaSharp;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("CoreJ2K.Skia.Tests")]
namespace CoreJ2K.Util
{
    internal class SKBitmapImage : ImageBase<SKBitmap>
    {
        internal SKBitmapImage(int width, int height, int numComponents, byte[] bytes)
            : base(width, height, numComponents, bytes)
        { }

        protected override object GetImageObject()
        {
            var bitmap = new SKBitmap();

            SKColorType colorType;
            // TODO: Right now just supporting 8-bit colortypes. Extend in the future.
            switch (NumComponents)
            {
                case 1: colorType = SKColorType.Gray8; break;
                case 2: colorType = SKColorType.Rg88; break;
                case 3: colorType = SKColorType.Rgb888x; break;
                case 4: case 5: colorType = SKColorType.Rgba8888; break;
                default:
                    throw new NotImplementedException(
                        $"Image with {NumComponents} components is not supported at this time.");
            }

            GCHandle gcHandle;
            var info = new SKImageInfo(Width, Height, colorType, SKAlphaType.Unpremul);


            switch (NumComponents)
            {
                // SkiaSharp doesn't play well with 24-bit images, upgrade to 32-bit.
                case 3:
                    {
                        var pix = ConvertRGB888toRGB888x(Width, Height, Bytes);
                        gcHandle = GCHandle.Alloc(pix, GCHandleType.Pinned);
                    }
                    break;
                // Attribute layers aren't available in SkiaSharp,
                // so we will only handle the first four components.
                case 5:
                    {
                        var pix = ConvertRGBHM88888toRGBA8888(Width, Height, Bytes);
                        gcHandle = GCHandle.Alloc(pix, GCHandleType.Pinned);
                    }
                    break;
                default:
                    {
                        gcHandle = GCHandle.Alloc(Bytes, GCHandleType.Pinned);
                    }
                    break;
            }
            bitmap.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes,
                delegate { gcHandle.Free(); }, null);

            return bitmap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe byte[] ConvertRGB888toRGB888x(int width, int height, byte[] input)
        {
            var totalPixels = width * height;
            var ret = new byte[totalPixels * 4];

            fixed (byte* srcPtr = input)
            fixed (byte* dstPtr = ret)
            {
                var s = srcPtr;
                var d = dstPtr;
                for (var i = 0; i < totalPixels; ++i)
                {
                    // copy R,G,B then set alpha to 0xFF
                    d[0] = s[0];
                    d[1] = s[1];
                    d[2] = s[2];
                    d[3] = 0xFF;

                    s += 3;
                    d += 4;
                }
            }

            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe byte[] ConvertRGBHM88888toRGBA8888(int width, int height, byte[] input)
        {
            var totalPixels = width * height;
            var ret = new byte[totalPixels * 4];

            fixed (byte* srcPtr = input)
            fixed (byte* dstPtr = ret)
            {
                var s = srcPtr;
                var d = dstPtr;
                for (var i = 0; i < totalPixels; ++i)
                {
                    // copy first four channels (R,G,B,H) then skip reserved/unused channel
                    d[0] = s[0];
                    d[1] = s[1];
                    d[2] = s[2];
                    d[3] = s[3];

                    s += 5; // skip the 5th component in source
                    d += 4;
                }
            }

            return ret;
        }
    }
}