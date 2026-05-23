// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CoreJ2K.Avalonia.Tests")]
namespace CoreJ2K.Util
{
    /// <summary>
    /// Image implementation backed by an Avalonia <see cref="WriteableBitmap"/>.
    /// Produces a BGRA8888 / Unpremul bitmap because that's the most widely supported
    /// pixel layout across Avalonia rendering backends.
    /// </summary>
    internal sealed class AvaloniaImage : ImageBase<WriteableBitmap>
    {
        internal AvaloniaImage(int width, int height, int numComponents, byte[] bytes)
            : base(width, height, numComponents, bytes)
        { }

        protected override object GetImageObject()
        {
            var pixelSize = new PixelSize(Width, Height);
            var dpi = new Vector(96, 96);
            var bmp = new WriteableBitmap(pixelSize, dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            using (var fb = bmp.Lock())
            {
                unsafe
                {
                    var basePtr = (byte*)fb.Address.ToPointer();
                    var stride = fb.RowBytes;
                    var src = Bytes;

                    switch (NumComponents)
                    {
                        case 1:
                            FillFromGray8(basePtr, stride, src);
                            break;
                        case 3:
                            FillFromRgb888(basePtr, stride, src);
                            break;
                        case 4:
                            FillFromRgba8888(basePtr, stride, src, srcPixelStride: 4);
                            break;
                        case 5:
                            // R,G,B,H,reserved → take first four channels as RGBA
                            FillFromRgba8888(basePtr, stride, src, srcPixelStride: 5);
                            break;
                        default:
                            throw new NotImplementedException(
                                $"Image with {NumComponents} components is not supported at this time.");
                    }
                }
            }

            return bmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void FillFromGray8(byte* basePtr, int stride, byte[] src)
        {
            var p = 0;
            for (var y = 0; y < Height; ++y)
            {
                var row = basePtr + y * stride;
                for (var x = 0; x < Width; ++x)
                {
                    var v = src[p++];
                    var d = row + x * 4;
                    d[0] = v;   // B
                    d[1] = v;   // G
                    d[2] = v;   // R
                    d[3] = 0xFF; // A
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void FillFromRgb888(byte* basePtr, int stride, byte[] src)
        {
            var p = 0;
            for (var y = 0; y < Height; ++y)
            {
                var row = basePtr + y * stride;
                for (var x = 0; x < Width; ++x)
                {
                    var r = src[p++];
                    var g = src[p++];
                    var b = src[p++];
                    var d = row + x * 4;
                    d[0] = b;
                    d[1] = g;
                    d[2] = r;
                    d[3] = 0xFF;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void FillFromRgba8888(byte* basePtr, int stride, byte[] src, int srcPixelStride)
        {
            var p = 0;
            for (var y = 0; y < Height; ++y)
            {
                var row = basePtr + y * stride;
                for (var x = 0; x < Width; ++x)
                {
                    var r = src[p];
                    var g = src[p + 1];
                    var b = src[p + 2];
                    var a = src[p + 3];
                    p += srcPixelStride;
                    var d = row + x * 4;
                    d[0] = b;
                    d[1] = g;
                    d[2] = r;
                    d[3] = a;
                }
            }
        }
    }
}
