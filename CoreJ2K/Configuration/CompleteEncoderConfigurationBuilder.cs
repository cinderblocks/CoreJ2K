// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreJ2K.Configuration
{
    /// <summary>
    /// Complete JPEG 2000 encoder configuration builder that integrates all configuration aspects.
    /// Provides a unified, fluent API for comprehensive JPEG 2000 encoding configuration.
    /// </summary>
    public class CompleteEncoderConfigurationBuilder
    {
        private readonly J2KEncoderConfiguration _encoderConfig = new J2KEncoderConfiguration();
        private QuantizationConfigurationBuilder _quantization = null;
        private WaveletConfigurationBuilder _wavelet = null;
        private ProgressionConfigurationBuilder _progression = null;
        private MetadataConfigurationBuilder _metadata = null;
        
        /// <summary>
        /// Gets the underlying encoder configuration.
        /// </summary>
        public J2KEncoderConfiguration EncoderConfiguration => _encoderConfig;
        
        /// <summary>
        /// Gets the quantization configuration.
        /// </summary>
        public QuantizationConfigurationBuilder Quantization => _quantization;
        
        /// <summary>
        /// Gets the wavelet configuration.
        /// </summary>
        public WaveletConfigurationBuilder Wavelet => _wavelet;
        
        /// <summary>
        /// Gets the progression configuration.
        /// </summary>
        public ProgressionConfigurationBuilder Progression => _progression;
        
        /// <summary>
        /// Gets the metadata configuration.
        /// </summary>
        public MetadataConfigurationBuilder Metadata => _metadata;
        
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
        
        #region Build Methods
        
        /// <summary>
        /// Builds the complete encoder configuration.
        /// Integrates all sub-configurations into a final J2KEncoderConfiguration.
        /// </summary>
        /// <returns>A configured J2KEncoderConfiguration instance.</returns>
        public J2KEncoderConfiguration Build()
        {
            // Apply quantization if configured
            if (_quantization != null)
            {
                _encoderConfig.WithQuantization(q =>
                {
                    q.Type = _quantization.Type;
                    q.BaseStepSize = _quantization.BaseStepSize;
                    q.GuardBits = _quantization.GuardBits;
                });
            }
            
            // Apply wavelet if configured
            if (_wavelet != null)
            {
                _encoderConfig.WithWavelet(w =>
                {
                    w.Filter = _wavelet.Filter;
                    w.DecompositionLevels = _wavelet.DecompositionLevels;
                });
            }
            
            // Apply progression if configured
            if (_progression != null)
            {
                _encoderConfig.WithProgression(p =>
                {
                    p.Order = _progression.DefaultOrder;
                });
            }
            
            return _encoderConfig;
        }
        
        /// <summary>
        /// Gets the metadata configuration as a J2KMetadata object.
        /// </summary>
        /// <returns>J2KMetadata object or null if no metadata configured.</returns>
        public j2k.fileformat.metadata.J2KMetadata GetMetadata()
        {
            return _metadata?.ToJ2KMetadata();
        }
        
        /// <summary>
        /// Encodes an image using this configuration.
        /// Note: This requires the image to be converted to BlkImgDataSrc first.
        /// </summary>
        /// <param name="imgsrc">The image source to encode.</param>
        /// <returns>The encoded JPEG 2000 data.</returns>
        public byte[] Encode(j2k.image.BlkImgDataSrc imgsrc)
        {
            var config = Build();
            var metadata = GetMetadata();
            
            if (metadata != null)
            {
                return J2kImage.ToBytes(imgsrc, metadata, config);
            }
            else
            {
                return J2kImage.ToBytes(imgsrc, config);
            }
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
        /// Balanced general purpose preset.
        /// </summary>
        public static CompleteEncoderConfigurationBuilder GeneralPurpose =>
            new CompleteEncoderConfigurationBuilder()
                .ForBalanced();
    }
}
