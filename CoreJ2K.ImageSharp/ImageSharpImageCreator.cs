// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using CoreJ2K.Util;
using JetBrains.Annotations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.CompilerServices;

namespace CoreJ2K.ImageSharp
{
    [UsedImplicitly]
    public sealed class ImageSharpImageCreator : ImageCreator<Image>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            // Map to a widely-supported ImageSharp pixel type
            switch (numComponents)
            {
                case 1:
                    return new ImageSharpImage<L8>(width, height, numComponents, bytes);
                case 3:
                    return new ImageSharpImage<Rgb24>(width, height, numComponents, bytes);
                case 4:
                case 5:
                    return new ImageSharpImage<Rgba32>(width, height, 4, bytes);
                default:
                    throw new NotImplementedException($"Image with {numComponents} components is not supported at this time.");
            }
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is Image img) return new ImgReaderImageSharp(img);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException($"Expected {nameof(Image)} but got {imageObject.GetType()}", nameof(imageObject));
        }
    }

    internal sealed class ImageSharpImage<TPixel> : ImageBase<Image>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        internal ImageSharpImage(int width, int height, int numComponents, byte[] bytes)
            : base(width, height, numComponents, bytes)
        { }

        protected override object GetImageObject()
        {
            // Create Image<TPixel> from raw planar/interleaved bytes depending on component count
            // Our Bytes array is interleaved per pixel and 8-bit per component
            var pixelCount = Width * Height;
            Image<TPixel> img = new Image<TPixel>(Width, Height);

            if (typeof(TPixel) == typeof(L8) && NumComponents == 1)
            {
                for (int y = 0, p=0; y < Height; ++y)
                {
                    for (int x = 0; x < Width; ++x)
                    {
                        var v = Bytes[p++];
                        var px = new L8(v);
                        img[x,y] = (TPixel)(object)px;
                    }
                }
            }
            else if (typeof(TPixel) == typeof(Rgb24) && NumComponents >= 3)
            {
                for (int y = 0, p=0; y < Height; ++y)
                {
                    for (int x = 0; x < Width; ++x)
                    {
                        var r = Bytes[p++]; var g = Bytes[p++]; var b = Bytes[p++];
                        if (NumComponents > 3) p++; // skip extra component if present
                        var px = new Rgb24(r,g,b);
                        img[x,y] = (TPixel)(object)px;
                    }
                }
            }
            else if (typeof(TPixel) == typeof(Rgba32))
            {
                for (int y = 0, p=0; y < Height; ++y)
                {
                    for (int x = 0; x < Width; ++x)
                    {
                        byte r,g,b,a;
                        if (NumComponents == 3)
                        { r = Bytes[p++]; g = Bytes[p++]; b = Bytes[p++]; a = 255; }
                        else
                        { r = Bytes[p++]; g = Bytes[p++]; b = Bytes[p++]; a = Bytes[p++]; if (NumComponents>4) p++; }
                        var px = new Rgba32(r,g,b,a);
                        img[x,y] = (TPixel)(object)px;
                    }
                }
            }
            else
            {
                throw new NotSupportedException($"Pixel type {typeof(TPixel).Name} with {NumComponents} components not supported");
            }

            return (Image)img;
        }
    }
}
