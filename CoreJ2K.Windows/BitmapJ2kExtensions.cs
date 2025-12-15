// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.Configuration;
using System;
using System.Drawing;
using System.IO;

namespace CoreJ2K.Windows
{
    /// <summary>
    /// Extension methods for System.Drawing integration with CoreJ2K modern configuration API.
    /// </summary>
    public static class BitmapJ2kExtensions
    {
        #region Encoding Extensions

        /// <summary>
        /// Encodes a Bitmap to JPEG 2000 using the modern configuration API.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <param name="config">The encoder configuration.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2K(this Bitmap bitmap, J2KEncoderConfiguration config)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (config == null) throw new ArgumentNullException(nameof(config));

            return J2kImage.ToBytes(bitmap, config);
        }

        /// <summary>
        /// Encodes a Bitmap to JPEG 2000 using a complete configuration builder.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <param name="builder">The complete encoder configuration builder.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2K(this Bitmap bitmap, CompleteEncoderConfigurationBuilder builder)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var config = builder.Build();
            return J2kImage.ToBytes(bitmap, config);
        }

        /// <summary>
        /// Encodes a Bitmap to JPEG 2000 with a preset configuration.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <param name="preset">The preset to use (e.g., CompleteConfigurationPresets.Web).</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2K(this Bitmap bitmap, Func<CompleteEncoderConfigurationBuilder> preset)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            return bitmap.EncodeToJ2K(preset());
        }

        /// <summary>
        /// Encodes a Bitmap to JPEG 2000 using lossless compression.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2KLossless(this Bitmap bitmap)
        {
            return bitmap.EncodeToJ2K(CompleteConfigurationPresets.Medical);
        }

        /// <summary>
        /// Encodes a Bitmap to JPEG 2000 using high quality settings.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <param name="copyright">Optional copyright text.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2KHighQuality(this Bitmap bitmap, string copyright = null)
        {
            var builder = CompleteConfigurationPresets.Photography;
            if (!string.IsNullOrEmpty(copyright))
            {
                builder.WithCopyright(copyright);
            }
            return bitmap.EncodeToJ2K(builder);
        }

        /// <summary>
        /// Encodes a Bitmap to JPEG 2000 optimized for web delivery.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <param name="copyright">Optional copyright text.</param>
        /// <returns>JPEG 2000 encoded data.</returns>
        public static byte[] EncodeToJ2KWeb(this Bitmap bitmap, string copyright = null)
        {
            var builder = CompleteConfigurationPresets.Web;
            if (!string.IsNullOrEmpty(copyright))
            {
                builder.WithCopyright(copyright);
            }
            return bitmap.EncodeToJ2K(builder);
        }

        /// <summary>
        /// Saves a Bitmap as a JPEG 2000 file using the modern configuration API.
        /// </summary>
        /// <param name="bitmap">The bitmap to save.</param>
        /// <param name="path">The file path.</param>
        /// <param name="config">The encoder configuration.</param>
        public static void SaveAsJ2K(this Bitmap bitmap, string path, J2KEncoderConfiguration config)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var data = bitmap.EncodeToJ2K(config);
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Saves a Bitmap as a JPEG 2000 file using a complete configuration builder.
        /// </summary>
        /// <param name="bitmap">The bitmap to save.</param>
        /// <param name="path">The file path.</param>
        /// <param name="builder">The complete encoder configuration builder.</param>
        public static void SaveAsJ2K(this Bitmap bitmap, string path, CompleteEncoderConfigurationBuilder builder)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var data = bitmap.EncodeToJ2K(builder);
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Saves a Bitmap as a lossless JPEG 2000 file.
        /// </summary>
        /// <param name="bitmap">The bitmap to save.</param>
        /// <param name="path">The file path.</param>
        public static void SaveAsJ2KLossless(this Bitmap bitmap, string path)
        {
            bitmap.SaveAsJ2K(path, CompleteConfigurationPresets.Medical);
        }

        /// <summary>
        /// Saves a Bitmap as a high-quality JPEG 2000 file.
        /// </summary>
        /// <param name="bitmap">The bitmap to save.</param>
        /// <param name="path">The file path.</param>
        /// <param name="copyright">Optional copyright text.</param>
        public static void SaveAsJ2KHighQuality(this Bitmap bitmap, string path, string copyright = null)
        {
            var builder = CompleteConfigurationPresets.Photography;
            if (!string.IsNullOrEmpty(copyright))
            {
                builder.WithCopyright(copyright);
            }
            bitmap.SaveAsJ2K(path, builder);
        }

        #endregion

        #region Decoding Extensions

        /// <summary>
        /// Decodes a JPEG 2000 file to a Bitmap using the modern configuration API.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="config">Optional decoder configuration.</param>
        /// <returns>Decoded Bitmap.</returns>
        public static Bitmap FromJ2KFile(string path, J2KDecoderConfiguration config = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var image = config != null
                ? J2kImage.FromFile(path, config)
                : J2kImage.FromFile(path);

            return image.As<Bitmap>();
        }

        /// <summary>
        /// Decodes JPEG 2000 data to a Bitmap using the modern configuration API.
        /// </summary>
        /// <param name="data">The JPEG 2000 data.</param>
        /// <param name="config">Optional decoder configuration.</param>
        /// <returns>Decoded Bitmap.</returns>
        public static Bitmap FromJ2KBytes(byte[] data, J2KDecoderConfiguration config = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var image = config != null
                ? J2kImage.FromBytes(data, config)
                : J2kImage.FromBytes(data);

            return image.As<Bitmap>();
        }

        /// <summary>
        /// Decodes a JPEG 2000 stream to a Bitmap using the modern configuration API.
        /// </summary>
        /// <param name="stream">The JPEG 2000 stream.</param>
        /// <param name="config">Optional decoder configuration.</param>
        /// <returns>Decoded Bitmap.</returns>
        public static Bitmap FromJ2KStream(Stream stream, J2KDecoderConfiguration config = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var image = config != null
                ? J2kImage.FromStream(stream, config)
                : J2kImage.FromStream(stream);

            return image.As<Bitmap>();
        }

        #endregion

        #region Fluent Configuration Helpers

        /// <summary>
        /// Creates a new complete encoder configuration builder for Bitmap encoding.
        /// </summary>
        /// <returns>A new CompleteEncoderConfigurationBuilder.</returns>
        public static CompleteEncoderConfigurationBuilder CreateJ2KEncoder()
        {
            return new CompleteEncoderConfigurationBuilder();
        }

        /// <summary>
        /// Creates a new decoder configuration for Bitmap decoding.
        /// </summary>
        /// <returns>A new J2KDecoderConfiguration.</returns>
        public static J2KDecoderConfiguration CreateJ2KDecoder()
        {
            return new J2KDecoderConfiguration();
        }

        #endregion
    }
}
