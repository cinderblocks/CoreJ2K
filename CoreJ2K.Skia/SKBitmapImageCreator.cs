// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using JetBrains.Annotations;
using SkiaSharp;

namespace CoreJ2K.Util
{
    [UsedImplicitly]
    public sealed class SKBitmapImageCreator : ImageCreator<SKBitmap>
    {
        public override IImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            return new SKBitmapImage(width, height, numComponents, bytes);
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            return new ImgReaderSkia((SKBitmap)imageObject);
        }
    }

    [UsedImplicitly]
    public sealed class SKPixmapImageCreator : ImageCreator<SKPixmap>
    {
        public override IImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            return new SKBitmapImage(width, height, numComponents, bytes);
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            return new ImgReaderSkia((SKPixmap)imageObject);
        }
    }
}