// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreJ2K.j2k.codestream;
using CoreJ2K.Util;

namespace CoreJ2K.Configuration
{
    /// <summary>
    /// Complete JPEG 2000 encoder configuration builder that integrates all configuration aspects.
    /// Provides a unified, fluent API for comprehensive JPEG 2000 encoding configuration.
    /// </summary>
    public class CompleteEncoderConfigurationBuilder
    {
        private readonly J2KEncoderConfiguration _encoderConfig = new J2KEncoderConfiguration();
        private QuantizationConfigurationBuilder? _quantization = null;
        private WaveletConfigurationBuilder? _wavelet = null;
        private ProgressionConfigurationBuilder? _progression = null;
        private MetadataConfigurationBuilder? _metadata = null;
        private DCOMarkerSegment? _dco = null;
        private List<NLTMarkerSegment>? _nlts = null;
        private List<MctEncodeSpec>? _mcts = null;
        private AtkMarkerSegment? _atk = null;
        
        /// <summary>
        /// Gets the underlying encoder configuration.
        /// </summary>
        public J2KEncoderConfiguration EncoderConfiguration => _encoderConfig;
        
        /// <summary>
        /// Gets the quantization configuration.
        /// </summary>
        public QuantizationConfigurationBuilder? Quantization => _quantization;
        
        /// <summary>
        /// Gets the wavelet configuration.
        /// </summary>
        public WaveletConfigurationBuilder? Wavelet => _wavelet;
        
        /// <summary>
        /// Gets the progression configuration.
        /// </summary>
        public ProgressionConfigurationBuilder? Progression => _progression;
        
        /// <summary>
        /// Gets the metadata configuration.
        /// </summary>
        public MetadataConfigurationBuilder? Metadata => _metadata;

        /// <summary>
        /// Gets the configured DC offset segment, or null if DCO is not active.
        /// </summary>
        public DCOMarkerSegment? Dco => _dco;

        /// <summary>
        /// Gets the configured NLT segments, or null if NLT is not active.
        /// </summary>
        public IReadOnlyList<NLTMarkerSegment>? Nlts => _nlts?.AsReadOnly();

        /// <summary>
        /// Gets the configured MCT encode specs, or null if MCT is not active.
        /// </summary>
        public IReadOnlyList<MctEncodeSpec>? Mcts => _mcts?.AsReadOnly();

        /// <summary>The configured Arbitrary Transformation Kernel (ATK) segment, if any.</summary>
        public AtkMarkerSegment? Atk => _atk;
        
        #region Quality Presets
        
        /// <summary>
        /// Configures for lossless compression.
        /// Uses reversible quantization and wavelet filter.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForLossless()
        {
            _encoderConfig.WithLossless();
            _quantization = QuantizationPresets.Lossless;
            _wavelet = WaveletPresets.Lossless;
            return this;
        }
        
        /// <summary>
        /// Configures for near-lossless compression.
        /// Very high quality with minimal artifacts.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForNearLossless()
        {
            _encoderConfig.WithQuality(0.99);
            _quantization = QuantizationPresets.NearLossless;
            _wavelet = WaveletPresets.HighQuality;
            return this;
        }
        
        /// <summary>
        /// Configures for high quality lossy compression.
        /// Excellent visual quality with good compression.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForHighQuality()
        {
            _encoderConfig.WithQuality(0.9);
            _quantization = QuantizationPresets.HighQuality;
            _wavelet = WaveletPresets.HighQuality;
            _progression = ProgressionPresets.QualityProgressive;
            return this;
        }
        
        /// <summary>
        /// Configures for balanced quality and compression.
        /// Good visual quality with moderate file size.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForBalanced()
        {
            _encoderConfig.WithQuality(0.75);
            _quantization = QuantizationPresets.Balanced;
            _wavelet = WaveletPresets.Balanced;
            _progression = ProgressionPresets.QualityProgressive;
            return this;
        }
        
        /// <summary>
        /// Configures for high compression with acceptable quality.
        /// Smaller files with visible compression artifacts.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForHighCompression()
        {
            _encoderConfig.WithBitrate(1.0f);
            _quantization = QuantizationPresets.HighCompression;
            _wavelet = WaveletPresets.Fast;
            _progression = ProgressionPresets.ResolutionProgressive;
            return this;
        }
        
        #endregion
        
        #region Use Case Presets
        
        /// <summary>
        /// Configures for medical imaging.
        /// Lossless compression with high quality requirements.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForMedical()
        {
            ForLossless();
            _progression = ProgressionPresets.Medical;
            _encoderConfig.WithTiles(t => t.SetSize(512, 512));
            return this;
        }
        
        /// <summary>
        /// Configures for archival storage.
        /// Very high quality for long-term preservation.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForArchival()
        {
            _encoderConfig.WithQuality(0.98);
            _quantization = QuantizationPresets.Archival;
            _wavelet = WaveletPresets.Archival;
            _encoderConfig.WithErrorResilience(er => er.EnableAll());
            _encoderConfig.WithTiles(t => t.SetSize(1024, 1024));
            return this;
        }
        
        /// <summary>
        /// Configures for web delivery.
        /// Progressive transmission with balanced quality.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForWeb()
        {
            ForBalanced();
            _quantization = QuantizationPresets.Web;
            _wavelet = WaveletPresets.Web;
            _progression = ProgressionPresets.WebStreaming;
            _encoderConfig.WithTiles(t => t.SetSize(512, 512));
            return this;
        }
        
        /// <summary>
        /// Configures for thumbnail generation.
        /// Fast encoding with higher compression.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForThumbnail()
        {
            _encoderConfig.WithBitrate(0.5f);
            _quantization = QuantizationPresets.Thumbnail;
            _wavelet = WaveletPresets.Thumbnail;
            _encoderConfig.WithWavelet(w => w.WithDecompositionLevels(3));
            _encoderConfig.WithTiles(t => t.SetSize(256, 256));
            return this;
        }
        
        /// <summary>
        /// Configures for geospatial/GIS applications.
        /// Spatial browsing with tiled organization.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForGeospatial()
        {
            ForBalanced();
            _progression = ProgressionPresets.Geospatial;
            _encoderConfig.WithTiles(t => t.SetSize(256, 256));
            return this;
        }
        
        /// <summary>
        /// Configures for streaming/progressive delivery.
        /// Quality-progressive for bandwidth-limited scenarios.
        /// </summary>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForStreaming()
        {
            ForBalanced();
            _progression = ProgressionPresets.QualityProgressive;
            _encoderConfig.WithTiles(t => t.SetSize(512, 512));
            return this;
        }
        
        /// <summary>
        /// Configures for a small portrait that must fit a hard size limit, such as a face image on an identity card.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The recipe is a JP2 file with one tile and one quality layer, the irreversible colour transform and 9/7 wavelet,
        /// five decomposition levels, 64×64 code-blocks, and a luma distortion weight of 1.25. The encoder keeps as much of the
        /// image as fits in <paramref name="maxBytes"/> and never writes more, JP2 boxes included. Quantization is expounded
        /// with a base step of 1/128 and two guard bits, the progression order is layer-resolution-component-position, and
        /// everything else keeps the codec default.
        /// </para>
        /// <para>
        /// With <paramref name="face"/>, that region is coded as Maxshift ROI on every component, with the lowest five resolution
        /// levels (<c>start level</c> 4) coded entirely as ROI. The mask must be the size of the image. A face that
        /// needs more than 15 quantized magnitude bits is refused, which this recipe's step size does not reach for 8-bit input.
        /// </para>
        /// <para>
        /// The preset replaces the quantization, wavelet, progression, tile, code-block, entropy-coding, error-resilience and
        /// distortion-weight settings, the rate, and the file-format choice, so call it before adjusting any of those. It leaves a region of interest set
        /// earlier alone unless <paramref name="face"/> is given. Calling <see cref="WithMaxBytes"/> afterwards changes the limit.
        /// </para>
        /// </remarks>
        /// <param name="maxBytes">The most bytes the complete output may take.</param>
        /// <param name="face">The region to prioritise, or null for no ROI.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder ForPortrait(int maxBytes, j2k.roi.ROIMask? face = null)
        {
            // These builder fields overwrite the configuration when Build() runs, so drop any left by an earlier preset.
            _quantization = null;
            _wavelet = null;
            _progression = null;

            _encoderConfig.WithBitrate(-1f); // not lossless; the byte limit replaces the rate
            _encoderConfig.WithFileFormat(true);
            _encoderConfig.WithMaxBytes(maxBytes);
            _encoderConfig.WithTiles(t => t.SetSize(0, 0));
            _encoderConfig.WithWavelet(w => w.UseIrreversible97().WithDecompositionLevels(5));
            _encoderConfig.WithQuantization(q => q.UseExpounded().WithBaseStepSize(0.0078125f).WithGuardBits(2));
            _encoderConfig.WithProgression(p =>
            {
                p.WithOrder(ProgressionOrder.LRCP);
                p.QualityLayers.Clear();
            });
            _encoderConfig.WithCodeBlocks(c => c.SetSize(64, 64));
            _encoderConfig.WithEntropyCoding(e =>
            {
                e.LengthCalculation = LengthCalculation.NearOptimal;
                e.Termination = TerminationType.NearOptimal;
                e.SegmentationSymbol = false;
                e.CausalMode = false;
                e.ResetMQ = false;
                e.BypassMode = false;
                e.RegularTermination = false;
            });
            _encoderConfig.WithErrorResilience(r =>
            {
                r.SOPMarkers = false;
                r.EPHMarkers = false;
            });
            _encoderConfig.WithDistortionWeights(new j2k.encoder.DistortionWeights().ForComponent(0, 1.25));

            if (face != null)
                _encoderConfig.WithROI(new j2k.roi.ROIConfiguration().AddMask(-1, face).SetStartLevel(4));

            return this;
        }

        #endregion

        #region Configuration Methods
        
        /// <summary>
        /// Configures quantization settings.
        /// </summary>
        /// <param name="configurator">Action to configure quantization.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithQuantization(Action<QuantizationConfigurationBuilder> configurator)
        {
            if (_quantization == null)
                _quantization = new QuantizationConfigurationBuilder();
            
            configurator?.Invoke(_quantization);
            return this;
        }
        
        /// <summary>
        /// Configures wavelet transform settings.
        /// </summary>
        /// <param name="configurator">Action to configure wavelet.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithWavelet(Action<WaveletConfigurationBuilder> configurator)
        {
            if (_wavelet == null)
                _wavelet = new WaveletConfigurationBuilder();
            
            configurator?.Invoke(_wavelet);
            return this;
        }
        
        /// <summary>
        /// Configures progression order settings.
        /// </summary>
        /// <param name="configurator">Action to configure progression.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithProgression(Action<ProgressionConfigurationBuilder> configurator)
        {
            if (_progression == null)
                _progression = new ProgressionConfigurationBuilder();
            
            configurator?.Invoke(_progression);
            return this;
        }
        
        /// <summary>
        /// Configures metadata settings.
        /// </summary>
        /// <param name="configurator">Action to configure metadata.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithMetadata(Action<MetadataConfigurationBuilder> configurator)
        {
            if (_metadata == null)
                _metadata = new MetadataConfigurationBuilder();
            
            configurator?.Invoke(_metadata);
            return this;
        }
        
        /// <summary>
        /// Configures encoder-specific settings.
        /// </summary>
        /// <param name="configurator">Action to configure encoder.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithEncoder(Action<J2KEncoderConfiguration> configurator)
        {
            configurator?.Invoke(_encoderConfig);
            return this;
        }
        
        /// <summary>
        /// Sets quality level (0.0 to 1.0).
        /// </summary>
        /// <param name="quality">Quality level.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithQuality(double quality)
        {
            _encoderConfig.WithQuality(quality);
            return this;
        }
        
        /// <summary>
        /// Sets target bitrate in bits per pixel.
        /// </summary>
        /// <param name="bitrate">Bitrate in bpp.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithBitrate(float bitrate)
        {
            _encoderConfig.WithBitrate(bitrate);
            return this;
        }
        
        /// <summary>
        /// Configures tile settings.
        /// </summary>
        /// <param name="configurator">Action to configure tiles.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithTiles(Action<TileConfiguration> configurator)
        {
            _encoderConfig.WithTiles(configurator);
            return this;
        }
        
        /// <summary>
        /// Enables or disables JP2/JPX file-format output (wraps the codestream in the file container).
        /// Equivalent to <c>.WithEncoder(e => e.WithFileFormat(value))</c>.
        /// </summary>
        public CompleteEncoderConfigurationBuilder WithFileFormat(bool value = true)
        {
            _encoderConfig.WithFileFormat(value);
            return this;
        }

        /// <summary>
        /// Adds a comment to the metadata.
        /// </summary>
        /// <param name="comment">The comment text.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithComment(string comment)
        {
            if (_metadata == null)
                _metadata = new MetadataConfigurationBuilder();
            
            _metadata.WithComment(comment);
            return this;
        }
        
        /// <summary>
        /// Adds copyright information to the metadata.
        /// </summary>
        /// <param name="copyright">The copyright text.</param>
        /// <returns>This configuration instance for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithCopyright(string copyright)
        {
            if (_metadata == null)
                _metadata = new MetadataConfigurationBuilder();
            
            _metadata.WithCopyright(copyright);
            return this;
        }
        
        #endregion

        #region Part 2 Transforms

        /// <summary>
        /// Applies a Variable DC Offset (DCO, ISO/IEC 15444-2 §A.3) per-component integer
        /// offset subtracted before wavelet encoding and restored after decoding.
        /// Produces a JPX codestream; <see cref="J2KEncoderConfiguration.UseFileFormat"/> is
        /// implied when file-format output is requested.
        /// </summary>
        /// <param name="offsets">One signed integer offset per component, in component order.</param>
        public CompleteEncoderConfigurationBuilder WithDco(params int[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
                throw new ArgumentException("At least one offset is required.", nameof(offsets));
            _dco = new DCOMarkerSegment { Offsets = (int[])offsets.Clone() };
            return this;
        }

        /// <summary>
        /// Applies a Variable DC Offset (DCO) using a pre-built segment.
        /// </summary>
        public CompleteEncoderConfigurationBuilder WithDco(DCOMarkerSegment segment)
        {
            _dco = segment ?? throw new ArgumentNullException(nameof(segment));
            return this;
        }

        /// <summary>
        /// Adds a Non-Linear point Transform (NLT, ISO/IEC 15444-2 §A.4) segment.
        /// Call once per component (or once globally with ComponentIndex == AllComponents).
        /// Produces a JPX codestream.
        /// </summary>
        public CompleteEncoderConfigurationBuilder AddNlt(NLTMarkerSegment segment)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            _nlts ??= new List<NLTMarkerSegment>();
            _nlts.Add(segment);
            return this;
        }

        /// <summary>
        /// Configures and adds a Non-Linear point Transform (NLT) segment via an inline action.
        /// </summary>
        public CompleteEncoderConfigurationBuilder AddNlt(Action<NLTMarkerSegment> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var seg = new NLTMarkerSegment();
            configure(seg);
            return AddNlt(seg);
        }

        /// <summary>
        /// Adds a Multiple Component Transform (MCT, ISO/IEC 15444-2 §A.5) encode spec.
        /// Call once per transform stage. Produces a JPX codestream.
        /// </summary>
        public CompleteEncoderConfigurationBuilder AddMct(MctEncodeSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            _mcts ??= new List<MctEncodeSpec>();
            _mcts.Add(spec);
            return this;
        }

        /// <summary>
        /// Uses a custom Arbitrary Transformation Kernel (ATK, ISO/IEC 15444-2 §A.6) as
        /// the wavelet filter for all tile-components. The kernel replaces the Part 1
        /// 5/3 or 9/7 filter; a reversible kernel requires reversible (lossless)
        /// quantization and an irreversible one requires lossy quantization. The Part 1
        /// component transform (RCT/ICT) is disabled. Produces a JPX codestream.
        /// </summary>
        public CompleteEncoderConfigurationBuilder WithAtk(AtkMarkerSegment kernel)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            kernel.Validate();
            _atk = kernel;
            return this;
        }

        /// <summary>
        /// Configures a custom Arbitrary Transformation Kernel (ATK) via an inline action.
        /// </summary>
        public CompleteEncoderConfigurationBuilder WithAtk(Action<AtkMarkerSegment> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var kernel = new AtkMarkerSegment();
            configure(kernel);
            return WithAtk(kernel);
        }

        #endregion

        #region Build Methods
        
        /// <summary>
        /// Builds the complete encoder configuration.
        /// Integrates all sub-configurations into a final J2KEncoderConfiguration.
        /// </summary>
        /// <returns>A configured J2KEncoderConfiguration instance.</returns>
        public J2KEncoderConfiguration Build()
        {
            _quantization?.ThrowIfSubbandStepsAreSet();

            // Apply quantization if configured
            if (_quantization != null)
            {
                _encoderConfig.WithQuantization(q =>
                {
                    // Only what was chosen: the rest keeps what the encoder configuration already has (for example a lossless
                    // setting, or a preset's guard bits)
                    if (_quantization.TypeIsSet) q.Type = _quantization.Type;
                    if (_quantization.BaseStepSizeIsSet) q.BaseStepSize = _quantization.BaseStepSize;
                    if (_quantization.GuardBitsAreSet) q.GuardBits = _quantization.GuardBits;
                });
            }
            
            // Apply wavelet if configured
            if (_wavelet != null)
            {
                _encoderConfig.WithWavelet(w =>
                {
                    if (_wavelet.FilterIsSet) w.Filter = _wavelet.Filter;
                    if (_wavelet.DecompositionLevelsAreSet) w.DecompositionLevels = _wavelet.DecompositionLevels;
                    if (!_wavelet.UseDefaultFilters)
                    {
                        foreach (var cf in _wavelet.ComponentFilters)
                            w.ComponentFilters[cf.Key] = cf.Value;
                    }
                });
            }
            
            // Apply progression if configured
            if (_progression != null)
            {
                _encoderConfig.WithProgression(p =>
                {
                    p.Order = _progression.DefaultOrder;
                    if (!_progression.UseDefaultOrder)
                    {
                        foreach (var to in _progression.TileOrders)
                            p.TileOrders[to.Key] = to.Value;
                    }
                });
            }
            
            return _encoderConfig;
        }
        
        /// <summary>
        /// Gets the metadata configuration as a J2KMetadata object.
        /// </summary>
        /// <returns>J2KMetadata object or null if no metadata configured.</returns>
        public j2k.fileformat.metadata.J2KMetadata? GetMetadata()
        {
            return _metadata?.ToJ2KMetadata();
        }
        
        /// <summary>
        /// Encodes an image using this configuration and writes the result to <paramref name="output"/>.
        /// </summary>
        /// <param name="imgsrc">The image source to encode.</param>
        /// <param name="output">The stream to write the encoded data to.</param>
        public void WriteTo(j2k.image.BlkImgDataSrc imgsrc, System.IO.Stream output)
            => WriteTo(imgsrc, output, CancellationToken.None);

        /// <summary>
        /// Encodes an image using this configuration and writes the result to <paramref name="output"/>, stopping if
        /// <paramref name="cancellationToken"/> or the configuration's own token is cancelled.
        /// </summary>
        /// <param name="imgsrc">The image source to encode.</param>
        /// <param name="output">The stream to write the encoded data to.</param>
        /// <param name="cancellationToken">Cancels the encode.</param>
        /// <exception cref="OperationCanceledException">A token was cancelled; nothing has been written to <paramref name="output"/>.</exception>
        public void WriteTo(j2k.image.BlkImgDataSrc imgsrc, System.IO.Stream output, CancellationToken cancellationToken)
        {
            if (output == null) throw new System.ArgumentNullException(nameof(output));
            using var linked = J2kImage.LinkTokens(_encoderConfig.CancellationToken, cancellationToken, out var token);
            var data = EncodeCore(imgsrc, token);
            token.ThrowIfCancellationRequested();
            output.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Encodes an image using this configuration.
        /// Note: This requires the image to be converted to BlkImgDataSrc first.
        /// </summary>
        /// <param name="imgsrc">The image source to encode.</param>
        /// <returns>The encoded JPEG 2000 data.</returns>
        public byte[] Encode(j2k.image.BlkImgDataSrc imgsrc)
            => Encode(imgsrc, CancellationToken.None);

        /// <summary>
        /// Encodes an image using this configuration, stopping if <paramref name="cancellationToken"/> or the configuration's own
        /// token is cancelled.
        /// </summary>
        /// <param name="imgsrc">The image source to encode.</param>
        /// <param name="cancellationToken">Cancels the encode.</param>
        /// <returns>The encoded JPEG 2000 data.</returns>
        /// <exception cref="OperationCanceledException">A token was cancelled.</exception>
        public byte[] Encode(j2k.image.BlkImgDataSrc imgsrc, CancellationToken cancellationToken)
        {
            using var linked = J2kImage.LinkTokens(_encoderConfig.CancellationToken, cancellationToken, out var token);
            return EncodeCore(imgsrc, token);
        }

        /// <summary>
        /// Weights the distortion of code-blocks by component, resolution level and subband. See <see cref="J2KEncoderConfiguration.WithDistortionWeights"/>.
        /// </summary>
        /// <param name="weights">The weights.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithDistortionWeights(j2k.encoder.DistortionWeights weights)
        {
            _encoderConfig.WithDistortionWeights(weights);
            return this;
        }

        /// <summary>
        /// Reports what the encode kept to <paramref name="callback"/> when it is done. See <see cref="J2KEncoderConfiguration.WithTelemetry"/>.
        /// </summary>
        /// <param name="callback">Receives the report, or null to turn it off.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithTelemetry(Action<EncodeTelemetry>? callback)
        {
            _encoderConfig.WithTelemetry(callback);
            return this;
        }

        /// <summary>
        /// Sets a hard limit on the size of the complete output, JP2 boxes included. See <see cref="J2KEncoderConfiguration.WithMaxBytes"/>.
        /// </summary>
        /// <param name="maxBytes">The most bytes the output may take.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithMaxBytes(int maxBytes)
        {
            _encoderConfig.WithMaxBytes(maxBytes);
            return this;
        }

        /// <summary>
        /// Encodes the given regions of interest with higher priority than the rest of the image.
        /// </summary>
        /// <param name="roiConfig">The ROI configuration.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithROI(j2k.roi.ROIConfiguration roiConfig)
        {
            _encoderConfig.WithROI(roiConfig);
            return this;
        }

        /// <summary>
        /// Encodes the regions of interest set up by <paramref name="configurator"/> with higher priority than the rest of the image.
        /// </summary>
        /// <param name="configurator">Adds regions to a new ROI configuration.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithROI(Action<j2k.roi.ROIConfiguration> configurator)
        {
            var roiConfig = new j2k.roi.ROIConfiguration();
            configurator?.Invoke(roiConfig);
            return WithROI(roiConfig);
        }

        /// <summary>
        /// Sets the maximum number of threads used to encode.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">1 for single-threaded; 0 or negative for the process-wide default.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithMaxDegreeOfParallelism(int maxDegreeOfParallelism)
        {
            _encoderConfig.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            return this;
        }

        /// <summary>
        /// Sets a token that cancels encodes started with this builder.
        /// </summary>
        /// <param name="cancellationToken">The token to observe.</param>
        /// <returns>This builder for method chaining.</returns>
        public CompleteEncoderConfigurationBuilder WithCancellationToken(CancellationToken cancellationToken)
        {
            _encoderConfig.CancellationToken = cancellationToken;
            return this;
        }

        private byte[] EncodeCore(j2k.image.BlkImgDataSrc imgsrc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var config = Build();
            var metadata = GetMetadata();
            var pl = config.ToParameterList();
            RemoveFilterOptionsForAtk(pl);

            return J2kImage.ToBytes(imgsrc, metadata, pl,
                _nlts is { Count: > 0 } ? _nlts : null,
                _mcts is { Count: > 0 } ? _mcts : null,
                _dco,
                _atk,
                cancellationToken)!;
        }
        
        // The ATK kernel replaces the wavelet filter, so any Ffilters value emitted by
        // the wavelet sub-configuration's defaults must not reach the encoder.
        private void RemoveFilterOptionsForAtk(j2k.util.ParameterList pl)
        {
            if (_atk == null) return;
            pl.Remove("Ffilters");
        }

        /// <summary>
        /// Encodes an image object (SKBitmap, Bitmap, etc.) using this configuration.
        /// The object is converted to an encodable source via <see cref="ImageFactory"/>.
        /// </summary>
        public byte[] Encode(object imageObject)
        {
            var imgsrc = ImageFactory.ToPortableImageSource(imageObject);
            return Encode(imgsrc);
        }

        /// <summary>Encodes an image object, stopping if <paramref name="cancellationToken"/> or the configuration's own token is cancelled.</summary>
        /// <exception cref="OperationCanceledException">A token was cancelled.</exception>
        public byte[] Encode(object imageObject, CancellationToken cancellationToken)
            => Encode(ImageFactory.ToPortableImageSource(imageObject), cancellationToken);

        /// <summary>
        /// Encodes an image and writes the result to <paramref name="output"/>.
        /// The object is converted to an encodable source via <see cref="ImageFactory"/>.
        /// </summary>
        public void WriteTo(object imageObject, Stream output)
        {
            var imgsrc = ImageFactory.ToPortableImageSource(imageObject);
            WriteTo(imgsrc, output);
        }

        /// <summary>Encodes an image asynchronously and returns the encoded bytes.</summary>
        public async Task<byte[]> EncodeAsync(j2k.image.BlkImgDataSrc imgsrc,
            CancellationToken cancellationToken = default)
        {
            // Link once, so a cancelled encode ends the task in the Canceled state rather than Faulted.
            using var linked = J2kImage.LinkTokens(_encoderConfig.CancellationToken, cancellationToken, out var token);
            return await Task.Run(() => EncodeCore(imgsrc, token), token).ConfigureAwait(false);
        }

        /// <summary>Encodes an image object asynchronously and returns the encoded bytes.</summary>
        public Task<byte[]> EncodeAsync(object imageObject,
            CancellationToken cancellationToken = default)
            => EncodeAsync(ImageFactory.ToPortableImageSource(imageObject), cancellationToken);

        /// <summary>Encodes an image asynchronously and writes the result to <paramref name="output"/>.</summary>
        public async Task WriteToAsync(j2k.image.BlkImgDataSrc imgsrc, Stream output,
            CancellationToken cancellationToken = default)
        {
            if (output == null) throw new System.ArgumentNullException(nameof(output));
            using var linked = J2kImage.LinkTokens(_encoderConfig.CancellationToken, cancellationToken, out var token);
            await Task.Run(() =>
            {
                var data = EncodeCore(imgsrc, token);
                token.ThrowIfCancellationRequested();
                output.Write(data, 0, data.Length);
            }, token).ConfigureAwait(false);
        }

        #endregion

        #region Validation
        
        /// <summary>
        /// Validates the complete configuration.
        /// </summary>
        /// <returns>List of validation errors, empty if valid.</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();
            
            // Validate encoder config
            errors.AddRange(_encoderConfig.Validate());
            
            // Validate quantization
            if (_quantization != null)
                errors.AddRange(_quantization.Validate());
            
            // Validate wavelet
            if (_wavelet != null)
                errors.AddRange(_wavelet.Validate());
            
            // Validate progression
            if (_progression != null)
                errors.AddRange(_progression.Validate());
            
            // Validate metadata
            if (_metadata != null)
                errors.AddRange(_metadata.Validate());

            // Validate Part 2 segments
            if (_dco != null && _dco.Offsets.Length == 0)
                errors.Add("DCO segment has no offsets.");

            if (_nlts != null)
            {
                foreach (var nlt in _nlts)
                {
                    if (nlt.BitDepth < 1 || nlt.BitDepth > 38)
                        errors.Add($"NLT segment has invalid bit depth {nlt.BitDepth}.");
                }
            }

            if (_mcts != null)
            {
                foreach (var mct in _mcts)
                {
                    if (mct.Components == null || mct.Components.Length < 2)
                        errors.Add("MCT spec must reference at least 2 components.");
                }
            }

            return errors;
        }
        
        /// <summary>
        /// Checks if the complete configuration is valid.
        /// </summary>
        public bool IsValid => Validate().Count == 0;
        
        #endregion
        
        /// <summary>
        /// Gets a string representation of this configuration.
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>();
            
            parts.Add("Complete Configuration");
            
            if (_quantization != null)
                parts.Add(_quantization.ToString());
            
            if (_wavelet != null)
                parts.Add(_wavelet.ToString());
            
            if (_progression != null)
                parts.Add(_progression.ToString());
            
            if (_metadata != null && _metadata.HasMetadata)
                parts.Add(_metadata.ToString());

            if (_dco != null)
                parts.Add($"DCO ({_dco.Offsets.Length} component(s))");

            if (_nlts is { Count: > 0 })
                parts.Add($"NLT ({_nlts.Count} segment(s))");

            if (_mcts is { Count: > 0 })
                parts.Add($"MCT ({_mcts.Count} stage(s))");

            return string.Join("; ", parts);
        }
    }
    
    /// <summary>
    /// Preset complete configurations for common scenarios.
    /// </summary>
    public static class CompleteConfigurationPresets
    {
        /// <summary>
        /// Lossless medical imaging preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Medical =>
            new CompleteEncoderConfigurationBuilder()
                .ForMedical();
        
        /// <summary>
        /// High-quality archival preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Archival =>
            new CompleteEncoderConfigurationBuilder()
                .ForArchival();
        
        /// <summary>
        /// Web delivery preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Web =>
            new CompleteEncoderConfigurationBuilder()
                .ForWeb();
        
        /// <summary>
        /// Thumbnail generation preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Thumbnail =>
            new CompleteEncoderConfigurationBuilder()
                .ForThumbnail();
        
        /// <summary>
        /// Geospatial/GIS preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Geospatial =>
            new CompleteEncoderConfigurationBuilder()
                .ForGeospatial();
        
        /// <summary>
        /// Streaming delivery preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Streaming =>
            new CompleteEncoderConfigurationBuilder()
                .ForStreaming();
        
        /// <summary>
        /// High quality photography preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder Photography =>
            new CompleteEncoderConfigurationBuilder()
                .ForHighQuality()
                .WithComment("High-quality photograph");
        
        /// <summary>
        /// Portrait preset for a hard size limit; see <see cref="CompleteEncoderConfigurationBuilder.ForPortrait"/>.
        /// </summary>
        /// <param name="maxBytes">The most bytes the complete output may take.</param>
        /// <param name="face">The region to prioritise, or null for no ROI.</param>
        public static CompleteEncoderConfigurationBuilder Portrait(int maxBytes, j2k.roi.ROIMask? face = null) =>
            new CompleteEncoderConfigurationBuilder()
                .ForPortrait(maxBytes, face);

        /// <summary>
        /// Balanced general purpose preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder GeneralPurpose =>
            new CompleteEncoderConfigurationBuilder()
                .ForBalanced();
    }
}
