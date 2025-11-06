// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using JetBrains.Annotations;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace CoreJ2K.Util
{
    [UsedImplicitly]
    public sealed class SKBitmapImageCreator : ImageCreator<SKBitmap>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IImage Create(int width, int height, int numComponents, byte[] bytes) =>
            new SKBitmapImage(width, height, numComponents, bytes ?? throw new ArgumentNullException(nameof(bytes)));

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is SKBitmap bmp) return new ImgReaderSkia(bmp);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException($"Expected {nameof(SKBitmap)} but got {imageObject.GetType()}", nameof(imageObject));
        }
    }

    [UsedImplicitly]
    public sealed class SKPixmapImageCreator : ImageCreator<SKPixmap>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IImage Create(int width, int height, int numComponents, byte[] bytes) =>
            new SKBitmapImage(width, height, numComponents, bytes ?? throw new ArgumentNullException(nameof(bytes)));

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is SKPixmap pm) return new ImgReaderSkia(pm);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException($"Expected {nameof(SKPixmap)} but got {imageObject.GetType()}", nameof(imageObject));
        }
    }
}