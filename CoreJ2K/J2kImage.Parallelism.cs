// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Globalization;
using System.Threading;
using CoreJ2K.j2k.codestream.reader;
using CoreJ2K.j2k.decoder;
using CoreJ2K.j2k.quantization.dequantizer;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.synthesis;

namespace CoreJ2K
{
    public partial class J2kImage
    {
        private static int _defaultMaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount);

        /// <summary>
        /// Gets or sets the maximum number of threads a single decode (code-blocks and the inverse wavelet transform) or encode
        /// (code-block coding and the forward wavelet transform) uses when the caller does not say otherwise. The initial value is
        /// <see cref="Environment.ProcessorCount"/>.
        /// </summary>
        /// <remarks>
        /// Output is identical for every value. Applications that already decode or encode many images concurrently
        /// can set this to 1 once at start-up to avoid oversubscribing the machine; a single call can also override it
        /// with the <c>threads</c> parameter, <c>J2KDecoderConfiguration.MaxDegreeOfParallelism</c> or
        /// <c>J2KEncoderConfiguration.MaxDegreeOfParallelism</c>.
        /// </remarks>
        public static int DefaultMaxDegreeOfParallelism
        {
            get => _defaultMaxDegreeOfParallelism;
            set => _defaultMaxDegreeOfParallelism = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be at least 1.");
        }

        /// <summary>Resolves the degree of parallelism for a decode from its parameter list.</summary>
        internal static int ResolveDegreeOfParallelism(ParameterList? pl)
        {
            var text = pl?.GetParameter("threads");
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threads))
                    throw new ArgumentException($"Parameter 'threads' must be an integer, got '{text}'.");
                if (threads >= 1) return threads;
            }
            return _defaultMaxDegreeOfParallelism;
        }

        /// <summary>
        /// Prepares the inverse wavelet transform for decoding: makes it observe <paramref name="cancellationToken"/> and, when
        /// several threads are allowed, lets it decode code-blocks in parallel, giving each worker its own chain of
        /// entropy-decoder, ROI and dequantiser stages over the shared bitstream reader.
        /// </summary>
        private static void ConfigureInverseTransform(InverseWT invWT, SharedCodedBlockSource? sharedSource, int degree,
            HeaderDecoder hd, ParameterList pl, DecoderSpecs decSpec, int[] depth, CancellationToken cancellationToken)
        {
            if (!(invWT is InvWTFull full)) return;

            full.SetCancellationToken(cancellationToken);
            if (sharedSource == null) return;

            full.EnableParallelDecoding(degree, () =>
            {
                var source = sharedSource.CreateFollower();
                var entropy = hd.createEntropyDecoder(source, pl);
                var roi = hd.createROIDeScaler(entropy, pl, decSpec);
                return HeaderDecoder.createDequantizer(roi, depth, decSpec);
            });
        }
    }
}
