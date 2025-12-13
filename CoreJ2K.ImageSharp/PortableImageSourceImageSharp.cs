// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using SixLabors.ImageSharp;

namespace CoreJ2K.ImageSharp
{
    /// <summary>
    /// Provides an image source implementation backed by an ImageSharp Image.
    /// This is currently unused directly (ImgReaderImageSharp covers reading) but left for parity with other backends.
    /// </summary>
    internal static class PortableImageSourceImageSharp
    {
        public static BlkImgDataSrc Create(Image image)
        {
            if (image is null) throw new ArgumentNullException(nameof(image));
            return new ImgReaderImageSharp(image);
        }
    }
}
