// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace CoreJ2K.ImageSharp
{
    /// <summary>
    /// Extension methods for ImageSharp integration with CoreJ2K modern configuration API.
    /// </summary>
    public static class ImageSharpJ2kExtensions
    {
        #region Encoding Extensions

        /// <summary>
        /// Encodes an Image to JPEG 2000 using the modern configuration API.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <param name="config">The encoder configuration.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2K<TPixel>(this Image<TPixel> image, J2KEncoderConfiguration config)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (config == null) throw new ArgumentNullException(nameof(config));

            return J2kImage.ToBytes(image, config);
        }

        /// <summary>
        /// Encodes an Image to JPEG 2000 using a complete configuration builder.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <param name="builder">The complete encoder configuration builder.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2K<TPixel>(this Image<TPixel> image, CompleteEncoderConfigurationBuilder builder)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var config = builder.Build();
            return J2kImage.ToBytes(image, config);
        }

        /// <summary>
        /// Encodes an Image to JPEG 2000 with a preset configuration.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <param name="preset">The preset to use (e.g., CompleteConfigurationPresets.Web).</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2K<TPixel>(this Image<TPixel> image, Func<CompleteEncoderConfigurationBuilder> preset)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            return image.EncodeToJ2K(preset());
        }

        /// <summary>
        /// Encodes an Image to JPEG 2000 using lossless compression.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2KLossless<TPixel>(this Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return image.EncodeToJ2K(CompleteConfigurationPresets.Medical);
        }

        /// <summary>
        /// Encodes an Image to JPEG 2000 using high quality settings.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <param name="copyright">Optional copyright text.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2KHighQuality<TPixel>(this Image<TPixel> image, string copyright = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var builder = CompleteConfigurationPresets.Photography;
            if (!string.IsNullOrEmpty(copyright))
            {
                builder.WithCopyright(copyright);
            }
            return image.EncodeToJ2K(builder);
        }

        /// <summary>
        /// Encodes an Image to JPEG 2000 optimized for web delivery.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <param name="copyright">Optional copyright text.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2KWeb<TPixel>(this Image<TPixel> image, string copyright = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var builder = CompleteConfigurationPresets.Web;
            if (!string.IsNullOrEmpty(copyright))
            {
                builder.WithCopyright(copyright);
            }
            return image.EncodeToJ2K(builder);
        }

        /// <summary>
        /// Saves an Image as a JPEG 2000 file using the modern configuration API.
        /// </summary>
        /// <param name="image">The image to save.</param>
        /// <param name="path">The file path.</param>
        /// <param name="config">The encoder configuration.</param>
        public static void SaveAsJ2K<TPixel>(this Image<TPixel> image, string path, J2KEncoderConfiguration config)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var data = image.EncodeToJ2K(config);
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Saves an Image as a JPEG 2000 file using a complete configuration builder.
        /// </summary>
        /// <param name="image">The image to save.</param>
        /// <param name="path">The file path.</param>
        /// <param name="builder">The complete encoder configuration builder.</param>
        public static void SaveAsJ2K<TPixel>(this Image<TPixel> image, string path, CompleteEncoderConfigurationBuilder builder)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var data = image.EncodeToJ2K(builder);
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Saves an Image as a lossless JPEG 2000 file.
        /// </summary>
        /// <param name="image">The image to save.</param>
        /// <param name="path">The file path.</param>
        public static void SaveAsJ2KLossless<TPixel>(this Image<TPixel> image, string path)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            image.SaveAsJ2K(path, CompleteConfigurationPresets.Medical);
        }

        /// <summary>
        /// Saves an Image as a high-quality JPEG 2000 file.
        /// </summary>
        /// <param name="image">The image to save.</param>
        /// <param name="path">The file path.</param>
        /// <param name="copyright">Optional copyright text.</param>
        public static void SaveAsJ2KHighQuality<TPixel>(this Image<TPixel> image, string path, string copyright = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var builder = CompleteConfigurationPresets.Photography;
            if (!string.IsNullOrEmpty(copyright))
            {
                builder.WithCopyright(copyright);
            }
            image.SaveAsJ2K(path, builder);
        }

        #endregion

        #region Decoding Extensions

        /// <summary>
        /// Decodes a JPEG 2000 file to an Image using the modern configuration API.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="config">Optional decoder configuration.</param>
        /// <returns>Decoded Image.</returns>
        public static Image<Rgba32> FromJ2KFile(string path, J2KDecoderConfiguration config = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var image = config != null
                ? J2kImage.FromFile(path, config)
                : J2kImage.FromFile(path);

            return image.As<Image<Rgba32>>();
        }

        /// <summary>
        /// Decodes JPEG 2000 data to an Image using the modern configuration API.
        /// </summary>
        /// <param name="data">The JPEG 2000 data.</param>
        /// <param name="config">Optional decoder configuration.</param>
        /// <returns>Decoded Image.</returns>
        public static Image<Rgba32> FromJ2KBytes(byte[] data, J2KDecoderConfiguration config = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var image = config != null
                ? J2kImage.FromBytes(data, config)
                : J2kImage.FromBytes(data);

            return image.As<Image<Rgba32>>();
        }

        /// <summary>
        /// Decodes a JPEG 2000 stream to an Image using the modern configuration API.
        /// </summary>
        /// <param name="stream">The JPEG 2000 stream.</param>
        /// <param name="config">Optional decoder configuration.</param>
        /// <returns>Decoded Image.</returns>
        public static Image<Rgba32> FromJ2KStream(Stream stream, J2KDecoderConfiguration config = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var image = config != null
                ? J2kImage.FromStream(stream, config)
                : J2kImage.FromStream(stream);

            return image.As<Image<Rgba32>>();
        }

        #endregion

        #region Fluent Configuration Helpers

        /// <summary>
        /// Creates a new complete encoder configuration builder for Image encoding.
        /// </summary>
        /// <returns>A new CompleteEncoderConfigurationBuilder.</returns>
        public static CompleteEncoderConfigurationBuilder CreateJ2KEncoder()
        {
            return new CompleteEncoderConfigurationBuilder();
        }

        /// <summary>
        /// Creates a new decoder configuration for Image decoding.
        /// </summary>
        /// <returns>A new J2KDecoderConfiguration.</returns>
        public static J2KDecoderConfiguration CreateJ2KDecoder()
        {
            return new J2KDecoderConfiguration();
        }

        #endregion
    }
}
