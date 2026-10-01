// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Globalization;
using CoreJ2K.j2k.util;

namespace CoreJ2K
{
    /// <summary>
    /// Thrown when a codestream asks the decoder for more resources than the active
    /// <see cref="DecoderLimits"/> allow, or exceeds a maximum defined by ISO/IEC 15444-1 itself.
    /// </summary>
    /// <remarks>
    /// It derives from <see cref="InvalidOperationException"/>, like the decoder's other
    /// malformed-input errors, so existing handlers keep working. It is raised from the header
    /// information alone, before the decoder allocates any image-sized buffer.
    /// </remarks>
    public sealed class DecoderLimitException : InvalidOperationException
    {
        /// <summary>Gets the name of the limit that was exceeded, e.g. <c>MaxPixels</c>.</summary>
        public string Limit { get; }

        /// <summary>Gets the value the codestream asked for.</summary>
        public long Requested { get; }

        /// <summary>Gets the maximum allowed value.</summary>
        public long Allowed { get; }

        /// <summary>Creates a new exception.</summary>
        public DecoderLimitException(string limit, long requested, long allowed, string message)
            : base(message)
        {
            Limit = limit;
            Requested = requested;
            Allowed = allowed;
        }
    }

    /// <summary>
    /// Resource limits applied to a decode before the decoder allocates image-sized buffers, so that a
    /// small hostile or corrupt file cannot make the decoder allocate gigabytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Limits apply to the image <em>as it will be decoded</em>: a reduced-resolution decode of a very
    /// large image is measured at the reduced size, so previews of huge images keep working.
    /// </para>
    /// <para>
    /// Independently of these settings, the decoder always rejects tile and component counts above the
    /// maxima defined by ISO/IEC 15444-1 (65535 tiles, 16384 components). Those cannot be relaxed.
    /// </para>
    /// <para>
    /// Instances are immutable; use the <c>With*</c> methods to derive modified copies. Set
    /// <see cref="Default"/> once at start-up to change the limits for every decode that does not
    /// specify its own.
    /// </para>
    /// </remarks>
    public sealed class DecoderLimits
    {
        /// <summary>Maximum number of tiles the JPEG 2000 codestream syntax can address (16-bit tile index).</summary>
        public const int SpecMaxTiles = 65535;

        /// <summary>Maximum number of components the JPEG 2000 codestream syntax allows.</summary>
        public const int SpecMaxComponents = 16384;

        // Estimated bytes of working memory per sample of the largest tile (reconstruction buffer).
        private const long WorkingBytesPerTileSample = 4;

        private static volatile DecoderLimits _default = new DecoderLimits(
            maxPixels: 1L << 30, maxMemoryBytes: 2L << 30, maxTileComponents: 1L << 22);

        private DecoderLimits(long maxPixels, long maxMemoryBytes, long maxTileComponents)
        {
            MaxPixels = maxPixels;
            MaxMemoryBytes = maxMemoryBytes;
            MaxTileComponents = maxTileComponents;
        }

        /// <summary>
        /// Gets or sets the limits used by every decode that does not specify its own. The initial value is
        /// 1 Gpixel, 2 GiB estimated memory and 4 Mi tile-components; see <see cref="None"/> to disable.
        /// </summary>
        public static DecoderLimits Default
        {
            get => _default;
            set => _default = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// A stricter preset for untrusted input: 64 Mpixel, 512 MiB estimated memory and 256 Ki
        /// tile-components.
        /// </summary>
        public static DecoderLimits Strict { get; } = new DecoderLimits(
            maxPixels: 64L << 20, maxMemoryBytes: 512L << 20, maxTileComponents: 1L << 18);

        /// <summary>
        /// No configurable limits (the ISO/IEC 15444-1 maxima still apply). Restores the behavior of
        /// versions before decode limits existed.
        /// </summary>
        public static DecoderLimits None { get; } = new DecoderLimits(long.MaxValue, long.MaxValue, long.MaxValue);

        /// <summary>Gets the maximum number of pixels (width × height) of the decoded image at the requested resolution.</summary>
        public long MaxPixels { get; }

        /// <summary>
        /// Gets the maximum estimated memory, in bytes, the decode may need: the decoded image plus the
        /// working buffers for the largest tile. This is an approximate estimate, not a hard cap on the process.
        /// </summary>
        public long MaxMemoryBytes { get; }

        /// <summary>Gets the maximum number of tiles × components, which sizes the decoder's per-tile tables.</summary>
        public long MaxTileComponents { get; }

        /// <summary>Returns a copy with a different <see cref="MaxPixels"/>.</summary>
        public DecoderLimits WithMaxPixels(long maxPixels)
            => new DecoderLimits(Positive(maxPixels, nameof(maxPixels)), MaxMemoryBytes, MaxTileComponents);

        /// <summary>Returns a copy with a different <see cref="MaxMemoryBytes"/>.</summary>
        public DecoderLimits WithMaxMemoryBytes(long maxMemoryBytes)
            => new DecoderLimits(MaxPixels, Positive(maxMemoryBytes, nameof(maxMemoryBytes)), MaxTileComponents);

        /// <summary>Returns a copy with a different <see cref="MaxTileComponents"/>.</summary>
        public DecoderLimits WithMaxTileComponents(long maxTileComponents)
            => new DecoderLimits(MaxPixels, MaxMemoryBytes, Positive(maxTileComponents, nameof(maxTileComponents)));

        private static long Positive(long value, string name)
            => value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, "Limit must be greater than zero.");

        /// <summary>
        /// Resolves the limits for a decode: the process-wide <see cref="Default"/>, overridden by any of the
        /// <c>max_pixels</c>, <c>max_memory</c> and <c>max_tile_components</c> parameters present.
        /// </summary>
        internal static DecoderLimits FromParameters(ParameterList? pl)
        {
            var limits = Default;
            if (pl == null) return limits;

            if (TryGetLong(pl, "max_pixels", out var maxPixels)) limits = limits.WithMaxPixels(maxPixels);
            if (TryGetLong(pl, "max_memory", out var maxMemory)) limits = limits.WithMaxMemoryBytes(maxMemory);
            if (TryGetLong(pl, "max_tile_components", out var maxTc)) limits = limits.WithMaxTileComponents(maxTc);
            return limits;
        }

        private static bool TryGetLong(ParameterList pl, string name, out long value)
        {
            value = 0;
            var text = pl.GetParameter(name);
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
                throw new ArgumentException($"Parameter '{name}' must be a positive integer, got '{text}'.");
            return true;
        }

        /// <summary>
        /// Checks the tile and component counts read from the SIZ marker segment. Called before any
        /// per-tile table is allocated. The ISO/IEC 15444-1 maxima are enforced even for <see cref="None"/>.
        /// </summary>
        internal void ValidateStructure(long tiles, int components)
        {
            if (tiles > SpecMaxTiles)
                throw new DecoderLimitException("SpecMaxTiles", tiles, SpecMaxTiles,
                    $"The codestream declares {tiles} tiles; ISO/IEC 15444-1 allows at most {SpecMaxTiles}.");

            if (components > SpecMaxComponents)
                throw new DecoderLimitException("SpecMaxComponents", components, SpecMaxComponents,
                    $"The codestream declares {components} components; ISO/IEC 15444-1 allows at most {SpecMaxComponents}.");

            var tileComponents = tiles * components;
            if (tileComponents > MaxTileComponents)
                throw new DecoderLimitException(nameof(MaxTileComponents), tileComponents, MaxTileComponents,
                    $"The codestream declares {tiles} tiles x {components} components = {tileComponents} tile-components, " +
                    $"above the limit of {MaxTileComponents}.");
        }

        /// <summary>
        /// Checks the size of the image that will actually be produced (after any resolution reduction) and the
        /// memory the decode is estimated to need. Called before the decoded image or any tile buffer is allocated.
        /// </summary>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="components">Number of output components (may exceed the codestream's, e.g. after a palette).</param>
        /// <param name="tileWidth">Width of the largest tile at the output resolution.</param>
        /// <param name="tileHeight">Height of the largest tile at the output resolution.</param>
        /// <param name="workingComponents">Number of codestream components, which have a working buffer per tile.</param>
        /// <param name="bytesPerOutputSample">Bytes the output keeps per sample: 4 for <c>InterleavedImage</c>, 1 for the 8-bit fast path.</param>
        internal void ValidateOutput(long width, long height, int components, long tileWidth, long tileHeight,
            int workingComponents, int bytesPerOutputSample)
        {
            var pixels = width * height;
            if (pixels > MaxPixels)
                throw new DecoderLimitException(nameof(MaxPixels), pixels, MaxPixels,
                    $"The decoded image would be {width} x {height} = {pixels} pixels, above the limit of {MaxPixels}. " +
                    "Decode at a lower resolution level, or raise DecoderLimits.MaxPixels if the input is trusted.");

            var estimate = EstimateMemory(width, height, components, tileWidth, tileHeight, workingComponents, bytesPerOutputSample);
            if (estimate > MaxMemoryBytes)
                throw new DecoderLimitException(nameof(MaxMemoryBytes), estimate, MaxMemoryBytes,
                    $"Decoding a {width} x {height} image with {components} component(s) is estimated to need {estimate} bytes, " +
                    $"above the limit of {MaxMemoryBytes}. Decode at a lower resolution level, or raise " +
                    "DecoderLimits.MaxMemoryBytes if the input is trusted.");
        }

        internal static long EstimateMemory(long width, long height, int components, long tileWidth, long tileHeight,
            int workingComponents, int bytesPerOutputSample)
        {
            try
            {
                checked
                {
                    return width * height * components * bytesPerOutputSample
                           + tileWidth * tileHeight * workingComponents * WorkingBytesPerTileSample;
                }
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }
    }
}
