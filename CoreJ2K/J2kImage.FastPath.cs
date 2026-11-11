// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

namespace CoreJ2K
{
    using CoreJ2K.Util;
    using j2k.codestream;
    using j2k.codestream.reader;
    using j2k.entropy.decoder;
    using j2k.fileformat.reader;
    using j2k.image;
    using j2k.image.dco;
    using j2k.image.invcomptransf;
    using j2k.io;
    using j2k.quantization.dequantizer;
    using j2k.roi;
    using j2k.util;
    using j2k.wavelet.synthesis;
    using System.Threading;
    using Color;
    using System;
    using System.IO;

    public partial class J2kImage
    {
        #region Public fast-path API

        /// <summary>
        /// Decodes a JPEG 2000 codestream directly into a backend image of type
        /// <typeparamref name="T"/> using an 8-bit fast path when all components have
        /// nominal bit depth ≤ 8. The fast path avoids the full-size <see cref="InterleavedImage"/>
        /// <c>int[]</c> allocation entirely, writing scaled bytes directly into a single byte
        /// buffer that is handed to the registered <see cref="IImageCreator"/>.
        /// For components wider than 8 bits the method falls back to
        /// <see cref="FromStream(Stream, ParameterList?)"/> followed by
        /// <see cref="InterleavedImage.As{T}"/>.
        /// </summary>
        /// <typeparam name="T">Backend image type registered with <see cref="ImageFactory"/>.</typeparam>
        /// <param name="stream">Stream containing JPEG 2000 data.</param>
        /// <param name="parameters">Optional decoder parameters.</param>
        /// <returns>The decoded image converted to <typeparamref name="T"/>.</returns>
        public static T DecodeToImage<T>(Stream stream, ParameterList? parameters = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return DecodeToImageCore<T>(stream, parameters, CancellationToken.None);
        }

        /// <summary>
        /// Decodes JPEG 2000 data from a byte array directly into a backend image of type
        /// <typeparamref name="T"/> using the 8-bit fast path when possible.
        /// </summary>
        /// <typeparam name="T">Backend image type registered with <see cref="ImageFactory"/>.</typeparam>
        /// <param name="j2kdata">The JPEG 2000 compressed data.</param>
        /// <param name="parameters">Optional decoder parameters.</param>
        /// <returns>The decoded image converted to <typeparamref name="T"/>.</returns>
        public static T DecodeToImage<T>(byte[] j2kdata, ParameterList? parameters = null)
        {
            if (j2kdata == null) throw new ArgumentNullException(nameof(j2kdata));
            using (var ms = new MemoryStream(j2kdata))
            {
                return DecodeToImage<T>(ms, parameters);
            }
        }

        /// <summary>
        /// Decodes a JPEG 2000 file directly into a backend image of type
        /// <typeparamref name="T"/> using the 8-bit fast path when possible.
        /// </summary>
        /// <typeparam name="T">Backend image type registered with <see cref="ImageFactory"/>.</typeparam>
        /// <param name="filename">Path to the JPEG 2000 file.</param>
        /// <param name="parameters">Optional decoder parameters.</param>
        /// <returns>The decoded image converted to <typeparamref name="T"/>.</returns>
        public static T DecodeFileToImage<T>(string filename, ParameterList? parameters = null)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            using (var stream = FileStreamFactory.New(filename, "r"))
            {
                return DecodeToImage<T>(stream, parameters);
            }
        }

        /// <summary>
        /// Decodes a JPEG 2000 stream straight into the platform image <typeparamref name="T"/>, stopping if
        /// <paramref name="cancellationToken"/> is cancelled. See <see cref="DecodeToImage{T}(Stream, ParameterList?)"/>.
        /// </summary>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public static T DecodeToImage<T>(Stream stream, ParameterList? parameters, CancellationToken cancellationToken)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return DecodeToImageCore<T>(stream, parameters, cancellationToken);
        }

        /// <summary>
        /// Decodes JPEG 2000 data straight into the platform image <typeparamref name="T"/>, stopping if
        /// <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public static T DecodeToImage<T>(byte[] j2kdata, ParameterList? parameters, CancellationToken cancellationToken)
        {
            if (j2kdata == null) throw new ArgumentNullException(nameof(j2kdata));
            using (var ms = new MemoryStream(j2kdata))
            {
                return DecodeToImageCore<T>(ms, parameters, cancellationToken);
            }
        }

        /// <summary>
        /// Decodes a JPEG 2000 file straight into the platform image <typeparamref name="T"/>, stopping if
        /// <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public static T DecodeFileToImage<T>(string filename, ParameterList? parameters, CancellationToken cancellationToken)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            using (var stream = FileStreamFactory.New(filename, "r"))
            {
                return DecodeToImageCore<T>(stream, parameters, cancellationToken);
            }
        }

        #endregion

        #region Core decode

        /// <summary>
        /// Applies the <see cref="DecoderLimits"/> to the image that is about to be produced, using its size at the requested
        /// resolution. Runs before the output buffer is allocated; building the decoding chain beforehand allocates nothing
        /// image-sized.
        /// </summary>
        /// <param name="invWT">The inverse wavelet transform, whose tile-components are the working buffers.</param>
        /// <param name="decodedImage">The final image of the chain (after colour-space mapping), whose size and component count
        /// are what the output buffer is sized from. A palette or multiple-component transform can give it more components than
        /// the codestream has.</param>
        /// <param name="bytesPerOutputSample">Bytes per sample the caller keeps for the output (4 for <see cref="InterleavedImage"/>, 1 for the fast path).</param>
        private static void EnforceDecoderLimits(ParameterList pl, HeaderInfo hi, InverseWT invWT, BlkImgDataSrc decodedImage,
            int bytesPerOutputSample)
        {
            var limits = DecoderLimits.FromParameters(pl);

            long width = decodedImage.ImgWidth, height = decodedImage.ImgHeight;
            var siz = hi.sizValue;
            double fullWidth = Math.Max(1, (long)siz.xsiz - siz.x0siz), fullHeight = Math.Max(1, (long)siz.ysiz - siz.y0siz);

            // Largest tile at the output resolution, scaled from the full-resolution tile size. The working buffers are the
            // codestream's tile-components, so they use the codestream's component count rather than the output's.
            var tileWidth = (long)Math.Ceiling(Math.Min(siz.xtsiz, fullWidth) * invWT.ImgWidth / fullWidth);
            var tileHeight = (long)Math.Ceiling(Math.Min(siz.ytsiz, fullHeight) * invWT.ImgHeight / fullHeight);

            limits.ValidateOutput(width, height, decodedImage.NumComps, tileWidth, tileHeight, invWT.NumComps, bytesPerOutputSample);
        }

        /// <summary>
        /// Core implementation of the 8-bit fast-path decode.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When all components of the decoded image have a nominal bit depth of 8 or fewer bits,
        /// this method writes interleaved, scaled byte samples directly into a
        /// <c>byte[Width × Height × NumComponents]</c> array and forwards that array to
        /// <see cref="ImageFactory.New{T}"/>.  This avoids the full-size
        /// <c>int[Width × Height × NumComponents]</c> allocation that
        /// <see cref="InterleavedImage"/> normally carries, cutting peak transient memory
        /// from roughly <b>5 bytes/sample</b> to <b>1 byte/sample</b> for the common
        /// 8-bit decode-and-display path.
        /// </para>
        /// <para>
        /// For images with any component wider than 8 bits the same decoding chain produces an
        /// <see cref="InterleavedImage"/>, which is converted with <see cref="InterleavedImage.As{T}"/>,
        /// so that HDR/scientific data is never silently truncated.
        /// </para>
        /// </remarks>
        private static T DecodeToImageCore<T>(Stream stream, ParameterList? parameters, CancellationToken cancellationToken)
        {
            // The platform image is made after the chain is closed: an exception from an image creator is not the file's fault.
            int width = 0, height = 0, components = 0;
            InterleavedImage? wide = null;
            var bytes = Decode(stream, parameters, cancellationToken, chain =>
            {
                var image = chain.Image;
                for (var i = 0; i < image.NumComps; i++)
                {
                    if (image.GetNomRangeBits(i) <= 8) continue;
                    wide = ToInterleavedImage(chain, cancellationToken);
                    return null;
                }

                width = image.ImgWidth;
                height = image.ImgHeight;
                components = image.NumComps;
                return ToInterleavedBytes(chain, cancellationToken);
            });

            if (wide != null)
            {
                using (wide)
                {
                    return wide.As<T>();
                }
            }

            var iimg = ImageFactory.New<T>(width, height, components, bytes!);
            if (iimg == null)
            {
                throw new InvalidOperationException(
                    $"No image creator registered for target type {typeof(T).FullName}. " +
                    $"Registered creators: {ImageFactory.DescribeRegistered()}.");
            }
            return iimg.As<T>();
        }

        #endregion
    }
}
