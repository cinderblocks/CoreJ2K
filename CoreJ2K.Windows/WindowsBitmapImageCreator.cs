// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image;
using JetBrains.Annotations;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace CoreJ2K.Util
{
    [UsedImplicitly]
    public sealed class WindowsBitmapImageCreator : ImageCreator<Image>
    {

        #region METHODS

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            return new WindowsBitmapImage(width, height, numComponents, bytes ?? throw new ArgumentNullException(nameof(bytes)));
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is Bitmap bmp) return WindowsBitmapImageSource.Create(bmp);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException($"Expected {nameof(Bitmap)} but got {imageObject.GetType()}", nameof(imageObject));
        }

        #endregion
    }
}
