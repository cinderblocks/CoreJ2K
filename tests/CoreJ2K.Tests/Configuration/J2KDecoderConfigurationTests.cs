// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the modern decoder configuration API.
    /// </summary>
    public class J2KDecoderConfigurationTests
    {
        [Fact]
        public void Constructor_CreatesDefaultConfiguration()
        {
            var config = new J2KDecoderConfiguration();
            
            Assert.Equal(-1, config.ResolutionLevel);
            Assert.Equal(-1f, config.DecodingRate);
            Assert.Equal(-1, config.DecodingBytes);
            Assert.True(config.UseColorSpace);
            Assert.True(config.ParsingMode);
            Assert.True(config.Verbose);
            Assert.NotNull(config.QuitConditions);
            Assert.NotNull(config.ComponentTransform);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void WithResolutionLevel_SetsLevel(int level)
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(level);
            
            Assert.Equal(level, config.ResolutionLevel);
        }
        
        [Fact]
        public void WithResolutionLevel_NegativeOtherThanMinusOne_ThrowsException()
        {
            var config = new J2KDecoderConfiguration();
            
            Assert.Throws<ArgumentException>(() => config.WithResolutionLevel(-2));
        }
        
        [Fact]
        public void WithHighestResolution_SetsToMinusOne()
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(3)
                .WithHighestResolution();
            
            Assert.Equal(-1, config.ResolutionLevel);
        }
        
        [Theory]
        [InlineData(0.5f)]
        [InlineData(1.0f)]
        [InlineData(2.5f)]
        public void WithDecodingRate_SetsRate(float rate)
        {
            var config = new J2KDecoderConfiguration()
                .WithDecodingRate(rate);
            
            Assert.Equal(rate, config.DecodingRate);
            Assert.Equal(-1, config.DecodingBytes); // Should clear bytes
        }
        
        [Fact]
        public void WithDecodingRate_InvalidValue_ThrowsException()
        {
            var config = new J2KDecoderConfiguration();
            
            Assert.Throws<ArgumentException>(() => config.WithDecodingRate(-2));
        }
        
        [Theory]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        public void WithDecodingBytes_SetsBytes(int bytes)
        {
            var config = new J2KDecoderConfiguration()
                .WithDecodingBytes(bytes);
            
            Assert.Equal(bytes, config.DecodingBytes);
            Assert.Equal(-1, config.DecodingRate); // Should clear rate
        }
        
        [Fact]
        public void WithDecodingBytes_InvalidValue_ThrowsException()
        {
            var config = new J2KDecoderConfiguration();
            
            Assert.Throws<ArgumentException>(() => config.WithDecodingBytes(-2));
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithColorSpace_SetsColorSpace(bool useColorSpace)
        {
            var config = new J2KDecoderConfiguration()
                .WithColorSpace(useColorSpace);
            
            Assert.Equal(useColorSpace, config.UseColorSpace);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithParsingMode_SetsParsingMode(bool parsingMode)
        {
            var config = new J2KDecoderConfiguration()
                .WithParsingMode(parsingMode);
            
            Assert.Equal(parsingMode, config.ParsingMode);
        }
        
        [Fact]
        public void WithProgressiveDecoding_EnablesParsingMode()
        {
            var config = new J2KDecoderConfiguration()
                .WithParsingMode(false)
                .WithProgressiveDecoding();
            
            Assert.True(config.ParsingMode);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithVerbose_SetsVerbose(bool verbose)
        {
            var config = new J2KDecoderConfiguration()
                .WithVerbose(verbose);
            
            Assert.Equal(verbose, config.Verbose);
        }
        
        [Fact]
        public void WithQuitConditions_ConfiguresQuitConditions()
        {
            var config = new J2KDecoderConfiguration()
                .WithQuitConditions(quit => quit
                    .WithMaxCodeBlocks(100)
                    .WithMaxLayers(5)
                    .WithMaxBitPlanes(8));
            
            Assert.Equal(100, config.QuitConditions.MaxCodeBlocks);
            Assert.Equal(5, config.QuitConditions.MaxLayers);
            Assert.Equal(8, config.QuitConditions.MaxBitPlanes);
        }
        
        [Fact]
        public void WithComponentTransform_ConfiguresComponentTransform()
        {
            var config = new J2KDecoderConfiguration()
                .WithComponentTransform(ct => ct.Disable());
            
            Assert.False(config.ComponentTransform.UseComponentTransform);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(2)
                .WithDecodingRate(1.0f)
                .WithColorSpace(true)
                .WithParsingMode(true)
                .WithVerbose(false)
                .WithQuitConditions(q => q.WithMaxLayers(10))
                .WithComponentTransform(ct => ct.Enable());
            
            Assert.Equal(2, config.ResolutionLevel);
            Assert.Equal(1.0f, config.DecodingRate);
            Assert.True(config.UseColorSpace);
            Assert.True(config.ParsingMode);
            Assert.False(config.Verbose);
            Assert.Equal(10, config.QuitConditions.MaxLayers);
            Assert.True(config.ComponentTransform.UseComponentTransform);
        }
        
        [Fact]
        public void ToParameterList_CreatesValidParameterList()
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(2)
                .WithDecodingRate(1.0f)
                .WithColorSpace(false);
            
            var pl = config.ToParameterList();
            
            Assert.NotNull(pl);
            Assert.Equal("2", pl.getParameter("res"));
            Assert.Equal("1", pl.getParameter("rate"));
            Assert.Equal("on", pl.getParameter("nocolorspace")); // inverted
        }
        
        [Fact]
        public void ToParameterList_DefaultConfiguration_CreatesValidParameterList()
        {
            var config = new J2KDecoderConfiguration();
            
            var pl = config.ToParameterList();
            
            Assert.NotNull(pl);
            Assert.Equal("off", pl.getParameter("nocolorspace")); // colorspace enabled
            Assert.Equal("on", pl.getParameter("parsing"));
            Assert.Equal("on", pl.getParameter("verbose"));
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(2)
                .WithDecodingRate(1.0f);
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Validate_BothRateAndBytes_ReturnsError()
        {
            var config = new J2KDecoderConfiguration()
                .WithDecodingRate(1.0f);
            config.DecodingBytes = 10000; // Manually set conflicting value
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("both decoding rate and decoding bytes"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidResolutionLevel_ReturnsError()
        {
            var config = new J2KDecoderConfiguration();
            config.ResolutionLevel = -5; // Invalid
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void QuitConditions_WithMaxCodeBlocks_SetsValue()
        {
            var quit = new QuitConditions()
                .WithMaxCodeBlocks(50);
            
            Assert.Equal(50, quit.MaxCodeBlocks);
        }
        
        [Fact]
        public void QuitConditions_WithMaxLayers_SetsValue()
        {
            var quit = new QuitConditions()
                .WithMaxLayers(10);
            
            Assert.Equal(10, quit.MaxLayers);
        }
        
        [Fact]
        public void QuitConditions_WithMaxBitPlanes_SetsValue()
        {
            var quit = new QuitConditions()
                .WithMaxBitPlanes(8);
            
            Assert.Equal(8, quit.MaxBitPlanes);
        }
        
        [Fact]
        public void QuitConditions_QuitAfterFirstProgression_SetsFlag()
        {
            var quit = new QuitConditions()
                .QuitAfterFirstProgression();
            
            Assert.True(quit.QuitAfterFirstProgressionOrder);
        }
        
        [Fact]
        public void QuitConditions_DecodeOnlyFirstTilePart_SetsFlag()
        {
            var quit = new QuitConditions()
                .DecodeOnlyFirstTilePart();
            
            Assert.True(quit.OnlyFirstTilePart);
        }
        
        [Fact]
        public void QuitConditions_Validate_ValidConditions_ReturnsNoErrors()
        {
            var quit = new QuitConditions
            {
                MaxCodeBlocks = 100,
                MaxLayers = 5,
                MaxBitPlanes = 8
            };
            
            var errors = quit.Validate();
            
            Assert.Empty(errors);
        }
        
        [Fact]
        public void QuitConditions_Validate_InvalidValues_ReturnsErrors()
        {
            var quit = new QuitConditions
            {
                MaxCodeBlocks = -5,
                MaxLayers = -10,
                MaxBitPlanes = -3
            };
            
            var errors = quit.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Equal(3, errors.Count);
        }
        
        [Fact]
        public void ComponentTransformSettings_Enable_EnablesTransform()
        {
            var ct = new ComponentTransformSettings()
                .Enable();
            
            Assert.True(ct.UseComponentTransform);
        }
        
        [Fact]
        public void ComponentTransformSettings_Disable_DisablesTransform()
        {
            var ct = new ComponentTransformSettings()
                .Disable();
            
            Assert.False(ct.UseComponentTransform);
        }
        
        [Fact]
        public void RealWorldScenario_LowResolutionPreview()
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(0)  // Lowest resolution
                .WithColorSpace(true)
                .WithVerbose(false);
            
            Assert.True(config.IsValid);
            Assert.Equal(0, config.ResolutionLevel);
        }
        
        [Fact]
        public void RealWorldScenario_ProgressiveLimitedBandwidth()
        {
            var config = new J2KDecoderConfiguration()
                .WithDecodingRate(0.5f)  // Low bitrate
                .WithProgressiveDecoding()
                .WithColorSpace(true);
            
            Assert.True(config.IsValid);
            Assert.Equal(0.5f, config.DecodingRate);
            Assert.True(config.ParsingMode);
        }
        
        [Fact]
        public void RealWorldScenario_FastDecodeFirstTileOnly()
        {
            var config = new J2KDecoderConfiguration()
                .WithResolutionLevel(1)
                .WithQuitConditions(q => q
                    .DecodeOnlyFirstTilePart()
                    .WithMaxLayers(3))
                .WithVerbose(false);
            
            Assert.True(config.IsValid);
            Assert.True(config.QuitConditions.OnlyFirstTilePart);
            Assert.Equal(3, config.QuitConditions.MaxLayers);
        }
        
        [Fact]
        public void RealWorldScenario_HighQualityFullDecode()
        {
            var config = new J2KDecoderConfiguration()
                .WithHighestResolution()
                .WithColorSpace(true)
                .WithComponentTransform(ct => ct.Enable())
                .WithParsingMode(true);
            
            Assert.True(config.IsValid);
            Assert.Equal(-1, config.ResolutionLevel);
            Assert.True(config.UseColorSpace);
        }
    }
}
