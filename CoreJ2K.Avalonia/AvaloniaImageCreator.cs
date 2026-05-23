// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Avalonia.Media.Imaging;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using JetBrains.Annotations;
using System;
using System.Runtime.CompilerServices;

namespace CoreJ2K.Util
{
    /// <summary>
    /// Image creator that materializes CoreJ2K decoded images as Avalonia
    /// <see cref="WriteableBitmap"/> instances. Discovered automatically by
    /// <c>J2kSetup.FindCodecs&lt;IImageCreator&gt;</c>.
    /// </summary>
    [UsedImplicitly]
    public sealed class AvaloniaImageCreator : ImageCreator<WriteableBitmap>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IImage Create(int width, int height, int numComponents, byte[] bytes) =>
            new AvaloniaImage(width, height, numComponents,
                bytes ?? throw new ArgumentNullException(nameof(bytes)));

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is WriteableBitmap wb) return new ImgReaderAvalonia(wb);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException(
                $"Expected {nameof(WriteableBitmap)} but got {imageObject.GetType()}", nameof(imageObject));
        }
    }
}
