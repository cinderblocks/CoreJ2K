// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.image;
using JetBrains.Annotations;
using System.Drawing;

namespace CoreJ2K.Util
{
    [UsedImplicitly]
    public sealed class WindowsBitmapImageCreator : ImageCreator<Image>
    {

        #region METHODS

        public override IImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            return new WindowsBitmapImage(width, height, numComponents, bytes);
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            return WindowsBitmapImageSource.Create(imageObject);
        }

        #endregion
    }
}
