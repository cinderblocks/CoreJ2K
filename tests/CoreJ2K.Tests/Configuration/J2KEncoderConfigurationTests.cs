// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the modern encoder configuration API.
    /// </summary>
    public class J2KEncoderConfigurationTests
    {
        [Fact]
        public void Constructor_CreatesDefaultConfiguration()
        {
            var config = new J2KEncoderConfiguration();
            
            Assert.Equal(-1f, config.TargetBitrate);
            Assert.False(config.Lossless);
            Assert.True(config.UseFileFormat);
            Assert.NotNull(config.Tiles);
            Assert.NotNull(config.Wavelet);
            Assert.NotNull(config.Quantization);
            Assert.NotNull(config.Progression);
            Assert.NotNull(config.CodeBlocks);
            Assert.NotNull(config.EntropyCoding);
            Assert.NotNull(config.ErrorResilience);
        }
        
        [Fact]
        public void WithBitrate_SetsBitrate()
        {
            var config = new J2KEncoderConfiguration()
                .WithBitrate(2.5f);
            
            Assert.Equal(2.5f, config.TargetBitrate);
            Assert.False(config.Lossless);
        }
        
        [Theory]
        [InlineData(-2)]
        [InlineData(-10)]
        public void WithBitrate_InvalidValue_ThrowsException(float bitrate)
        {
            var config = new J2KEncoderConfiguration();
            
            Assert.Throws<ArgumentException>(() => config.WithBitrate(bitrate));
        }
        
        [Theory]
        [InlineData(0.1, 0.05)]
        [InlineData(0.5, 1.25)]
        [InlineData(1.0, 5.0)]
        public void WithQuality_ConvertsToBitrate(double quality, double expectedBitrate)
        {
            var config = new J2KEncoderConfiguration()
                .WithQuality(quality);
            
            Assert.Equal((float)expectedBitrate, config.TargetBitrate, 2);
            Assert.False(config.Lossless);
        }
        
        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public void WithQuality_InvalidValue_ThrowsException(double quality)
        {
            var config = new J2KEncoderConfiguration();
            
            Assert.Throws<ArgumentException>(() => config.WithQuality(quality));
        }
        
        [Fact]
        public void WithLossless_ConfiguresLosslessMode()
        {
            var config = new J2KEncoderConfiguration()
                .WithLossless();
            
            Assert.True(config.Lossless);
            Assert.Equal(-1f, config.TargetBitrate);
            Assert.Equal(WaveletFilter.Reversible53, config.Wavelet.Filter);
            Assert.Equal(QuantizationType.Reversible, config.Quantization.Type);
        }
        
        [Fact]
        public void WithFileFormat_SetsFileFormat()
        {
            var config = new J2KEncoderConfiguration()
                .WithFileFormat(false);
            
            Assert.False(config.UseFileFormat);
            
            config.WithFileFormat(true);
            Assert.True(config.UseFileFormat);
        }
        
        [Fact]
        public void WithTiles_ConfiguresTiles()
        {
            var config = new J2KEncoderConfiguration()
                .WithTiles(tiles => tiles
                    .SetSize(1024, 1024)
                    .WithPacketsPerTilePart(100));
            
            Assert.Equal(1024, config.Tiles.Width);
            Assert.Equal(1024, config.Tiles.Height);
            Assert.Equal(100, config.Tiles.PacketsPerTilePart);
        }
        
        [Fact]
        public void WithWavelet_ConfiguresWavelet()
        {
            var config = new J2KEncoderConfiguration()
                .WithWavelet(wavelet => wavelet
                    .UseIrreversible97()
                    .WithDecompositionLevels(6));
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Wavelet.Filter);
            Assert.Equal(6, config.Wavelet.DecompositionLevels);
        }
        
        [Fact]
        public void WithQuantization_ConfiguresQuantization()
        {
            var config = new J2KEncoderConfiguration()
                .WithQuantization(quant => quant
                    .UseExpounded()
                    .WithBaseStepSize(0.01f)
                    .WithGuardBits(2));
            
            Assert.Equal(QuantizationType.Expounded, config.Quantization.Type);
            Assert.Equal(0.01f, config.Quantization.BaseStepSize);
            Assert.Equal(2, config.Quantization.GuardBits);
        }
        
        [Fact]
        public void WithProgression_ConfiguresProgression()
        {
            var config = new J2KEncoderConfiguration()
                .WithProgression(prog => prog
                    .WithOrder(ProgressionOrder.RPCL)
                    .WithQualityLayers(0.1f, 0.5f, 1.0f));
            
            Assert.Equal(ProgressionOrder.RPCL, config.Progression.Order);
            Assert.Equal(3, config.Progression.QualityLayers.Count);
            Assert.Equal(0.1f, config.Progression.QualityLayers[0]);
        }
        
        [Fact]
        public void WithCodeBlocks_ConfiguresCodeBlocks()
        {
            var config = new J2KEncoderConfiguration()
                .WithCodeBlocks(cb => cb.SetSize(32, 32));
            
            Assert.Equal(32, config.CodeBlocks.Width);
            Assert.Equal(32, config.CodeBlocks.Height);
        }
        
        [Fact]
        public void WithEntropyCoding_ConfiguresEntropyCoding()
        {
            var config = new J2KEncoderConfiguration()
                .WithEntropyCoding(entropy => 
                {
                    entropy.SegmentationSymbol = true;
                    entropy.CausalMode = true;
                });
            
            Assert.True(config.EntropyCoding.SegmentationSymbol);
            Assert.True(config.EntropyCoding.CausalMode);
        }
        
        [Fact]
        public void WithErrorResilience_ConfiguresErrorResilience()
        {
            var config = new J2KEncoderConfiguration()
                .WithErrorResilience(resilience => resilience
                    .EnableSOPMarkers()
                    .EnableEPHMarkers());
            
            Assert.True(config.ErrorResilience.SOPMarkers);
            Assert.True(config.ErrorResilience.EPHMarkers);
        }
        
        [Fact]
        public void WithROI_ConfiguresROI()
        {
            var roiConfig = new CoreJ2K.j2k.roi.ROIConfiguration()
                .AddRectangle(0, 100, 100, 200, 200);
            
            var config = new J2KEncoderConfiguration()
                .WithROI(roiConfig);
            
            Assert.NotNull(config.ROI);
            Assert.Single(config.ROI.Regions);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new J2KEncoderConfiguration()
                .WithQuality(0.8)
                .WithFileFormat(true)
                .WithTiles(t => t.SetSize(512, 512))
                .WithWavelet(w => w.UseIrreversible97())
                .WithQuantization(q => q.UseExpounded())
                .WithProgression(p => p.WithOrder(ProgressionOrder.LRCP))
                .WithCodeBlocks(cb => cb.SetSize(64, 64))
                .WithErrorResilience(er => er.EnableAll());
            
            Assert.NotEqual(-1f, config.TargetBitrate);
            Assert.Equal(512, config.Tiles.Width);
            Assert.Equal(WaveletFilter.Irreversible97, config.Wavelet.Filter);
            Assert.True(config.ErrorResilience.SOPMarkers);
        }
        
        [Fact]
        public void ToParameterList_CreatesValidParameterList()
        {
            var config = new J2KEncoderConfiguration()
                .WithBitrate(2.0f)
                .WithTiles(t => t.SetSize(1024, 1024));
            
            var pl = config.ToParameterList();
            
            Assert.NotNull(pl);
            Assert.Equal("on", pl.getParameter("file_format"));
            Assert.Equal("2", pl.getParameter("rate"));
            Assert.Equal("1024 1024", pl.getParameter("tiles"));
        }
        
        [Fact]
        public void ToParameterList_Lossless_SetsCorrectParameters()
        {
            var config = new J2KEncoderConfiguration()
                .WithLossless()
                .WithTiles(t => t.SetSize(512, 512));
            
            var pl = config.ToParameterList();
            
            Assert.Equal("on", pl.getParameter("lossless"));
            Assert.Equal("-1", pl.getParameter("rate"));
            Assert.Equal("w5x3", pl.getParameter("Ffilters"));
            Assert.Equal("reversible", pl.getParameter("Qtype"));
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new J2KEncoderConfiguration()
                .WithBitrate(1.0f)
                .WithTiles(t => t.SetSize(512, 512));
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Validate_LosslessWithBitrate_ReturnsError()
        {
            var config = new J2KEncoderConfiguration()
                .WithLossless();
            config.TargetBitrate = 2.0f; // Manually set conflicting value
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("lossless"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidTileSize_ReturnsError()
        {
            var config = new J2KEncoderConfiguration()
                .WithBitrate(1.0f)
                .WithTiles(t => t.SetSize(-100, 512));
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidCodeBlockSize_ReturnsError()
        {
            var config = new J2KEncoderConfiguration()
                .WithBitrate(1.0f)
                .WithTiles(t => t.SetSize(512, 512))
                .WithCodeBlocks(cb => cb.SetSize(7, 64)); // 7 is not power of 2
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("power of 2"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void WaveletConfiguration_UseReversible53_SetsFilter()
        {
            var wavelet = new WaveletConfiguration()
                .UseReversible53();
            
            Assert.Equal(WaveletFilter.Reversible53, wavelet.Filter);
        }
        
        [Fact]
        public void WaveletConfiguration_UseIrreversible97_SetsFilter()
        {
            var wavelet = new WaveletConfiguration()
                .UseIrreversible97();
            
            Assert.Equal(WaveletFilter.Irreversible97, wavelet.Filter);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(10)]
        public void WaveletConfiguration_WithDecompositionLevels_SetsLevels(int levels)
        {
            var wavelet = new WaveletConfiguration()
                .WithDecompositionLevels(levels);
            
            Assert.Equal(levels, wavelet.DecompositionLevels);
        }
        
        [Fact]
        public void QuantizationConfiguration_UseReversible_SetsType()
        {
            var quant = new QuantizationConfiguration()
                .UseReversible();
            
            Assert.Equal(QuantizationType.Reversible, quant.Type);
        }
        
        [Fact]
        public void QuantizationConfiguration_UseDerived_SetsType()
        {
            var quant = new QuantizationConfiguration()
                .UseDerived();
            
            Assert.Equal(QuantizationType.Derived, quant.Type);
        }
        
        [Fact]
        public void QuantizationConfiguration_UseExpounded_SetsType()
        {
            var quant = new QuantizationConfiguration()
                .UseExpounded();
            
            Assert.Equal(QuantizationType.Expounded, quant.Type);
        }
        
        [Theory]
        [InlineData(ProgressionOrder.LRCP)]
        [InlineData(ProgressionOrder.RLCP)]
        [InlineData(ProgressionOrder.RPCL)]
        [InlineData(ProgressionOrder.PCRL)]
        [InlineData(ProgressionOrder.CPRL)]
        public void ProgressionConfiguration_WithOrder_SetsOrder(ProgressionOrder order)
        {
            var prog = new ProgressionConfiguration()
                .WithOrder(order);
            
            Assert.Equal(order, prog.Order);
        }
        
        [Fact]
        public void TileConfiguration_SetSize_SetsSize()
        {
            var tiles = new TileConfiguration()
                .SetSize(1024, 1024);
            
            Assert.Equal(1024, tiles.Width);
            Assert.Equal(1024, tiles.Height);
        }
        
        [Fact]
        public void TileConfiguration_WithImageReference_SetsReference()
        {
            var tiles = new TileConfiguration()
                .WithImageReference(100, 200);
            
            Assert.Equal(100, tiles.ReferenceX);
            Assert.Equal(200, tiles.ReferenceY);
        }
        
        [Fact]
        public void CodeBlockConfiguration_SetSize_SetsSize()
        {
            var cb = new CodeBlockConfiguration()
                .SetSize(32, 64);
            
            Assert.Equal(32, cb.Width);
            Assert.Equal(64, cb.Height);
        }
        
        [Fact]
        public void ErrorResilienceConfiguration_EnableAll_EnablesAllMarkers()
        {
            var resilience = new ErrorResilienceConfiguration()
                .EnableAll();
            
            Assert.True(resilience.SOPMarkers);
            Assert.True(resilience.EPHMarkers);
        }
    }
}
