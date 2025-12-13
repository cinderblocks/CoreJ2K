// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using CoreJ2K.Util;
using JetBrains.Annotations;
using Pfim;
using System.Runtime.CompilerServices;
using J2KIImage = CoreJ2K.Util.IImage;
using PfIImage = Pfim.IImage;

namespace CoreJ2K.Pfim
{
    [UsedImplicitly]
    public sealed class PfimImageCreator : ImageCreator<PfIImage>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override J2KIImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Invalid dimensions");
            ImageFormat format;
            int bpp;
            switch (numComponents)
            {
                case 3: format = ImageFormat.Rgb24; bpp = 24; break;
                case 4: format = ImageFormat.Rgba32; bpp = 32; break;
                default:
                    throw new NotSupportedException($"Pfim decode target does not support {numComponents} components.");
            }
            var expected = width * height * (bpp / 8);
            // For interop with InterleavedImage (which produces 8-bit per component interleaved bytes), allow expected == width*height*numComponents when bpp==8
            if (bpp == 8)
            {
                expected = width * height * numComponents;
            }
            if (bytes.Length < expected)
                throw new ArgumentException("Byte buffer too small for decoded image dimensions.");
            return new PfimPortableImage(width, height, format, bytes, bpp);
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is PfIImage pfimImage) return new ImgReaderPfim(pfimImage);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException($"Expected {nameof(PfIImage)} but got {imageObject.GetType()}", nameof(imageObject));
        }
    }
}
