// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2024-2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreJ2K.Util
{
    using j2k.image;

    public static class ImageFactory
    {
        #region FIELDS

        private static readonly List<IImageCreator> _creators = new List<IImageCreator>();
        private static readonly object _lock = new object();

        #endregion

        #region CONSTRUCTORS

        static ImageFactory()
        {
#if !NET8_0_OR_GREATER
            foreach (var creator in J2kSetup.FindCodecInstances<IImageCreator>())
            {
                _creators.Add(creator);
            }
#endif
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Registers an image creator. Call this from a <c>[ModuleInitializer]</c> or application startup
        /// on platforms where automatic plugin discovery is unavailable (e.g. NativeAOT).
        /// </summary>
        public static void Register(IImageCreator creator)
        {
            if (creator == null) throw new ArgumentNullException(nameof(creator));
            lock (_lock)
            {
                _creators.Add(creator);
            }
        }

        internal static IImage? New<T>(int width, int height, int numComponents, byte[] bytes)
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

        internal static BlkImgDataSrc? ToPortableImageSource(object imageObject)
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
