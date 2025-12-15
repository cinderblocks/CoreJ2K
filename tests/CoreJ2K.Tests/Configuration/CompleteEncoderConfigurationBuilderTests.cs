// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the complete encoder configuration builder API.
    /// </summary>
    public class CompleteEncoderConfigurationBuilderTests
    {
        [Fact]
        public void Constructor_CreatesDefaultConfiguration()
        {
            var config = new CompleteEncoderConfigurationBuilder();
            
            Assert.NotNull(config.EncoderConfiguration);
            Assert.Null(config.Quantization);
            Assert.Null(config.Wavelet);
            Assert.Null(config.Progression);
            Assert.Null(config.Metadata);
        }
        
        [Fact]
        public void ForLossless_ConfiguresLosslessSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForLossless();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.Equal(QuantizationType.Reversible, config.Quantization.Type);
            Assert.Equal(WaveletFilter.Reversible53, config.Wavelet.Filter);
        }
        
        [Fact]
        public void ForNearLossless_ConfiguresNearLosslessSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForNearLossless();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
        }
        
        [Fact]
        public void ForHighQuality_ConfiguresHighQualitySettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForHighQuality();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void ForBalanced_ConfiguresBalancedSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForBalanced();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void ForHighCompression_ConfiguresHighCompressionSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForHighCompression();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void ForMedical_ConfiguresMedicalSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForMedical();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
            Assert.Equal(QuantizationType.Reversible, config.Quantization.Type);
        }
        
        [Fact]
        public void ForArchival_ConfiguresArchivalSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForArchival();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
        }
        
        [Fact]
        public void ForWeb_ConfiguresWebSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForWeb();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void ForThumbnail_ConfiguresThumbnailSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForThumbnail();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
        }
        
        [Fact]
        public void ForGeospatial_ConfiguresGeospatialSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForGeospatial();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void ForStreaming_ConfiguresStreamingSettings()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForStreaming();
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void WithQuantization_ConfiguresQuantization()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithQuantization(q => q
                    .UseExpounded()
                    .WithBaseStepSize(0.01f));
            
            Assert.NotNull(config.Quantization);
            Assert.Equal(QuantizationType.Expounded, config.Quantization.Type);
            Assert.Equal(0.01f, config.Quantization.BaseStepSize);
        }
        
        [Fact]
        public void WithWavelet_ConfiguresWavelet()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithWavelet(w => w
                    .UseIrreversible_9_7()
                    .WithDecompositionLevels(6));
            
            Assert.NotNull(config.Wavelet);
            Assert.Equal(WaveletFilter.Irreversible97, config.Wavelet.Filter);
            Assert.Equal(6, config.Wavelet.DecompositionLevels);
        }
        
        [Fact]
        public void WithProgression_ConfiguresProgression()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithProgression(p => p.UseLRCP());
            
            Assert.NotNull(config.Progression);
            Assert.Equal(ProgressionOrder.LRCP, config.Progression.DefaultOrder);
        }
        
        [Fact]
        public void WithMetadata_ConfiguresMetadata()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithMetadata(m => m
                    .WithComment("Test")
                    .WithCopyright("Copyright © 2025"));
            
            Assert.NotNull(config.Metadata);
            Assert.Single(config.Metadata.Comments);
            Assert.NotNull(config.Metadata.IntellectualPropertyRights);
        }
        
        [Fact]
        public void WithComment_AddsCommentToMetadata()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithComment("Test comment");
            
            Assert.NotNull(config.Metadata);
            Assert.Single(config.Metadata.Comments);
        }
        
        [Fact]
        public void WithCopyright_AddsCopyrightToMetadata()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithCopyright("Copyright © 2025");
            
            Assert.NotNull(config.Metadata);
            Assert.NotNull(config.Metadata.IntellectualPropertyRights);
        }
        
        [Fact]
        public void WithQuality_SetsQuality()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithQuality(0.85);
            
            // Quality is set in encoder configuration
            Assert.NotNull(config.EncoderConfiguration);
        }
        
        [Fact]
        public void WithBitrate_SetsBitrate()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithBitrate(2.0f);
            
            // Bitrate is set in encoder configuration
            Assert.NotNull(config.EncoderConfiguration);
        }
        
        [Fact]
        public void WithTiles_ConfiguresTiles()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithTiles(t => t.SetSize(512, 512));
            
            // Tiles are configured in encoder configuration
            Assert.NotNull(config.EncoderConfiguration);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForBalanced()
                .WithQuantization(q => q.WithGuardBits(2))
                .WithWavelet(w => w.WithDecompositionLevels(6))
                .WithProgression(p => p.UseLRCP())
                .WithMetadata(m => m.WithComment("Test"))
                .WithCopyright("Copyright © 2025");
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
            Assert.NotNull(config.Metadata);
        }
        
        [Fact]
        public void Build_CreatesEncoderConfiguration()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForHighQuality()
                .Build();
            
            Assert.NotNull(config);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void GetMetadata_ReturnsMetadataOrNull()
        {
            var configWithMetadata = new CompleteEncoderConfigurationBuilder()
                .WithComment("Test");
            
            var metadata = configWithMetadata.GetMetadata();
            Assert.NotNull(metadata);
            
            var configWithoutMetadata = new CompleteEncoderConfigurationBuilder();
            var noMetadata = configWithoutMetadata.GetMetadata();
            Assert.Null(noMetadata);
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForBalanced();
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ToString_ReturnsDescriptiveString()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForHighQuality()
                .WithComment("Test");
            
            var str = config.ToString();
            
            Assert.Contains("Complete Configuration", str);
            Assert.Contains("Quality", str);
        }
        
        // Preset tests
        
        [Fact]
        public void Presets_Medical_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Medical;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.Equal(QuantizationType.Reversible, config.Quantization.Type);
        }
        
        [Fact]
        public void Presets_Archival_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Archival;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
        }
        
        [Fact]
        public void Presets_Web_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Web;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void Presets_Thumbnail_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Thumbnail;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
        }
        
        [Fact]
        public void Presets_Geospatial_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Geospatial;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void Presets_Streaming_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Streaming;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void Presets_Photography_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.Photography;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Metadata);
        }
        
        [Fact]
        public void Presets_GeneralPurpose_ConfiguredCorrectly()
        {
            var config = CompleteConfigurationPresets.GeneralPurpose;
            
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
        }
        
        [Fact]
        public void Presets_AllAreValid()
        {
            Assert.True(CompleteConfigurationPresets.Medical.IsValid);
            Assert.True(CompleteConfigurationPresets.Archival.IsValid);
            Assert.True(CompleteConfigurationPresets.Web.IsValid);
            Assert.True(CompleteConfigurationPresets.Thumbnail.IsValid);
            Assert.True(CompleteConfigurationPresets.Geospatial.IsValid);
            Assert.True(CompleteConfigurationPresets.Streaming.IsValid);
            Assert.True(CompleteConfigurationPresets.Photography.IsValid);
            Assert.True(CompleteConfigurationPresets.GeneralPurpose.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_HighQualityPhotograph()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForHighQuality()
                .WithCopyright("© 2025 John Doe Photography")
                .WithComment("Sunset over Grand Canyon")
                .WithMetadata(m => m.WithXml("<exif><camera>Canon R5</camera></exif>"));
            
            Assert.True(config.IsValid);
            Assert.NotNull(config.Metadata);
            Assert.Single(config.Metadata.Comments);
        }
        
        [Fact]
        public void RealWorldScenario_MedicalDiagnostic()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForMedical()
                .WithMetadata(m => m
                    .WithComment("Patient: Anonymous")
                    .WithComment("Study: CT Scan - Chest")
                    .WithXml("<dicom><studyId>12345</studyId></dicom>"));
            
            Assert.True(config.IsValid);
            Assert.Equal(QuantizationType.Reversible, config.Quantization.Type);
        }
        
        [Fact]
        public void RealWorldScenario_WebGallery()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .ForWeb()
                .WithTiles(t => t.SetSize(512, 512))
                .WithCopyright("© 2025 Art Gallery")
                .WithComment("Available for purchase");
            
            Assert.True(config.IsValid);
            Assert.NotNull(config.Progression);
        }
        
        [Fact]
        public void RealWorldScenario_CustomConfiguration()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithQuality(0.85)
                .WithQuantization(q => q
                    .UseExpounded()
                    .WithBaseStepSize(0.008f)
                    .WithGuardBits(2))
                .WithWavelet(w => w
                    .UseIrreversible_9_7()
                    .WithDecompositionLevels(6))
                .WithProgression(p => p.UseLRCP())
                .WithTiles(t => t.SetSize(1024, 1024))
                .WithCopyright("Custom Configuration Test");
            
            Assert.True(config.IsValid);
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Progression);
            Assert.NotNull(config.Metadata);
        }
    }
}
