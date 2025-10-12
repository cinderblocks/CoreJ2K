// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CoreJ2K.Util
{
    using j2k.image;

    public static class ImageFactory
    {
        #region FIELDS

        private static readonly List<IImageCreator> _creators = new List<IImageCreator>();

        #endregion

        #region CONSTRUCTORS

        static ImageFactory()
        {
            foreach (var creator in J2kSetup.FindCodecs<IImageCreator>())
            {
                _creators.Add(Activator.CreateInstance(creator) as IImageCreator);
            }
        }

        #endregion

        #region METHODS

        internal static IImage New<T>(int width, int height, int numComponents, byte[] bytes)
        {
            try
            {
                var creator = _creators.Single(c => c.ImageType.IsAssignableFrom(typeof(T)));
                return creator.Create(width, height, numComponents, bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            try
            {
                var creator = _creators.Single(c => c.ImageType.IsAssignableFrom(imageObject.GetType()));
                return creator.ToPortableImageSource(imageObject);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
