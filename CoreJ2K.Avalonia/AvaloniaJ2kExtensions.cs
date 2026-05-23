// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Avalonia.Media.Imaging;
using CoreJ2K.Configuration;
using System;
using System.IO;

namespace CoreJ2K.Avalonia
{
    /// <summary>
    /// Extension methods for Avalonia integration with the CoreJ2K modern configuration API.
    /// </summary>
    public static class AvaloniaJ2kExtensions
    {
        #region Encoding

        public static byte[] EncodeToJ2K(this WriteableBitmap bitmap, J2KEncoderConfiguration config)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (config == null) throw new ArgumentNullException(nameof(config));
            return J2kImage.ToBytes(bitmap, config);
        }

        public static byte[] EncodeToJ2K(this WriteableBitmap bitmap, CompleteEncoderConfigurationBuilder builder)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return J2kImage.ToBytes(bitmap, builder.Build());
        }

        public static byte[] EncodeToJ2KLossless(this WriteableBitmap bitmap) =>
            bitmap.EncodeToJ2K(CompleteConfigurationPresets.Medical);

        public static byte[] EncodeToJ2KHighQuality(this WriteableBitmap bitmap, string copyright = null)
        {
            var builder = CompleteConfigurationPresets.Photography;
            if (!string.IsNullOrEmpty(copyright)) builder.WithCopyright(copyright);
            return bitmap.EncodeToJ2K(builder);
        }

        public static byte[] EncodeToJ2KWeb(this WriteableBitmap bitmap, string copyright = null)
        {
            var builder = CompleteConfigurationPresets.Web;
            if (!string.IsNullOrEmpty(copyright)) builder.WithCopyright(copyright);
            return bitmap.EncodeToJ2K(builder);
        }

        public static void SaveAsJ2K(this WriteableBitmap bitmap, string path, J2KEncoderConfiguration config)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            File.WriteAllBytes(path, bitmap.EncodeToJ2K(config));
        }

        public static void SaveAsJ2K(this WriteableBitmap bitmap, string path, CompleteEncoderConfigurationBuilder builder)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            File.WriteAllBytes(path, bitmap.EncodeToJ2K(builder));
        }

        public static void SaveAsJ2KLossless(this WriteableBitmap bitmap, string path) =>
            bitmap.SaveAsJ2K(path, CompleteConfigurationPresets.Medical);

        #endregion

        #region Decoding

        public static WriteableBitmap FromJ2KFile(string path, J2KDecoderConfiguration config = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            var image = config != null ? J2kImage.FromFile(path, config) : J2kImage.FromFile(path);
            return image.As<WriteableBitmap>();
        }

        public static WriteableBitmap FromJ2KBytes(byte[] data, J2KDecoderConfiguration config = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var image = config != null ? J2kImage.FromBytes(data, config) : J2kImage.FromBytes(data);
            return image.As<WriteableBitmap>();
        }

        public static WriteableBitmap FromJ2KStream(Stream stream, J2KDecoderConfiguration config = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var image = config != null ? J2kImage.FromStream(stream, config) : J2kImage.FromStream(stream);
            return image.As<WriteableBitmap>();
        }

        #endregion
    }
}
