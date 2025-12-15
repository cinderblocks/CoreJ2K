// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the wavelet configuration builder API.
    /// </summary>
    public class WaveletConfigurationBuilderTests
    {
        [Fact]
        public void Constructor_CreatesDefaultConfiguration()
        {
            var config = new WaveletConfigurationBuilder();
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.UseDefaultFilters);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseReversible_5_3_SetsReversibleFilter()
        {
            var config = new WaveletConfigurationBuilder()
                .UseReversible_5_3();
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseIrreversible_9_7_SetsIrreversibleFilter()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7();
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseReversible_Alias_SetsReversibleFilter()
        {
            var config = new WaveletConfigurationBuilder()
                .UseReversible();
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
        }
        
        [Fact]
        public void UseIrreversible_Alias_SetsIrreversibleFilter()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible();
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
        }
        
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(32)]
        public void WithDecompositionLevels_ValidLevels_Succeeds(int levels)
        {
            var config = new WaveletConfigurationBuilder()
                .WithDecompositionLevels(levels);
            
            Assert.Equal(levels, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(33)]
        [InlineData(100)]
        public void WithDecompositionLevels_InvalidLevels_ThrowsException(int levels)
        {
            var config = new WaveletConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithDecompositionLevels(levels));
        }
        
        [Fact]
        public void WithComponentFilter_SetsCustomFilter()
        {
            var config = new WaveletConfigurationBuilder()
                .WithComponentFilter(0, WaveletFilter.Irreversible97);
            
            Assert.False(config.UseDefaultFilters);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void WithComponentFilter_NegativeComponent_ThrowsException()
        {
            var config = new WaveletConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => 
                config.WithComponentFilter(-1, WaveletFilter.Reversible53));
        }
        
        [Fact]
        public void WithComponentFilter_MultipleComponents_Succeeds()
        {
            var config = new WaveletConfigurationBuilder()
                .WithComponentFilter(0, WaveletFilter.Irreversible97)
                .WithComponentFilter(1, WaveletFilter.Reversible53)
                .WithComponentFilter(2, WaveletFilter.Reversible53);
            
            Assert.False(config.UseDefaultFilters);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseDefaultComponentFilters_ClearsCustomFilters()
        {
            var config = new WaveletConfigurationBuilder()
                .WithComponentFilter(0, WaveletFilter.Irreversible97)
                .WithComponentFilter(1, WaveletFilter.Reversible53)
                .UseDefaultComponentFilters();
            
            Assert.True(config.UseDefaultFilters);
        }
        
        [Fact]
        public void ForLossless_ConfiguresForLossless()
        {
            var config = new WaveletConfigurationBuilder()
                .ForLossless();
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForHighQuality_ConfiguresForHighQuality()
        {
            var config = new WaveletConfigurationBuilder()
                .ForHighQuality();
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(6, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForBalanced_ConfiguresForBalanced()
        {
            var config = new WaveletConfigurationBuilder()
                .ForBalanced();
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForFast_ConfiguresForFast()
        {
            var config = new WaveletConfigurationBuilder()
                .ForFast();
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(3, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(6)
                .WithComponentFilter(0, WaveletFilter.Reversible53);
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(6, config.DecompositionLevels);
            Assert.False(config.UseDefaultFilters);
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(5);
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidDecompositionLevels_ReturnsError()
        {
            var config = new WaveletConfigurationBuilder();
            config.DecompositionLevels = 0; // Invalid
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("Decomposition levels"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(6)
                .WithComponentFilter(0, WaveletFilter.Reversible53);
            
            var clone = original.Clone();
            
            // Modify clone
            clone.WithDecompositionLevels(3);
            clone.UseReversible_5_3();
            
            // Original should be unchanged
            Assert.Equal(WaveletFilter.Irreversible97, original.Filter);
            Assert.Equal(6, original.DecompositionLevels);
            
            // Clone should have new values
            Assert.Equal(WaveletFilter.Reversible53, clone.Filter);
            Assert.Equal(3, clone.DecompositionLevels);
        }
        
        [Fact]
        public void ToString_ReturnsDescriptiveString()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(5);
            
            var str = config.ToString();
            
            Assert.Contains("9/7", str);
            Assert.Contains("irreversible", str.ToLower());
            Assert.Contains("5", str);
        }
        
        [Fact]
        public void ToString_Reversible_IndicatesReversible()
        {
            var config = new WaveletConfigurationBuilder()
                .UseReversible_5_3();
            
            var str = config.ToString();
            
            Assert.Contains("5/3", str);
            Assert.Contains("reversible", str.ToLower());
        }
        
        // Preset tests
        
        [Fact]
        public void Presets_Lossless_IsReversible()
        {
            var config = WaveletPresets.Lossless;
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_HighQuality_ConfiguredCorrectly()
        {
            var config = WaveletPresets.HighQuality;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(6, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Balanced_ConfiguredCorrectly()
        {
            var config = WaveletPresets.Balanced;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Fast_HasFewerLevels()
        {
            var config = WaveletPresets.Fast;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(3, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Medical_IsLossless()
        {
            var config = WaveletPresets.Medical;
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Archival_HighQuality()
        {
            var config = WaveletPresets.Archival;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(6, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Web_Balanced()
        {
            var config = WaveletPresets.Web;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(5, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Thumbnail_FewerLevels()
        {
            var config = WaveletPresets.Thumbnail;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(3, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_MaximumCompression_MinimalLevels()
        {
            var config = WaveletPresets.MaximumCompression;
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(2, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_AllAreValid()
        {
            Assert.True(WaveletPresets.Lossless.IsValid);
            Assert.True(WaveletPresets.HighQuality.IsValid);
            Assert.True(WaveletPresets.Balanced.IsValid);
            Assert.True(WaveletPresets.Fast.IsValid);
            Assert.True(WaveletPresets.Medical.IsValid);
            Assert.True(WaveletPresets.Archival.IsValid);
            Assert.True(WaveletPresets.Web.IsValid);
            Assert.True(WaveletPresets.Thumbnail.IsValid);
            Assert.True(WaveletPresets.MaximumCompression.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_MedicalImaging()
        {
            var config = new WaveletConfigurationBuilder()
                .UseReversible_5_3()
                .WithDecompositionLevels(5);
            
            Assert.Equal(WaveletFilter.Reversible53, config.Filter);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_HighQualityPhotography()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(6);
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.Equal(6, config.DecompositionLevels);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_FastWebDelivery()
        {
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(3);
            
            Assert.True(config.IsValid);
            Assert.True(config.DecompositionLevels < 5); // Fewer levels = faster
        }
        
        [Fact]
        public void RealWorldScenario_MixedComponentFilters()
        {
            // Y component with 9/7, CbCr with 5/3
            var config = new WaveletConfigurationBuilder()
                .UseIrreversible_9_7()
                .WithDecompositionLevels(5)
                .WithComponentFilter(1, WaveletFilter.Reversible53)
                .WithComponentFilter(2, WaveletFilter.Reversible53);
            
            Assert.Equal(WaveletFilter.Irreversible97, config.Filter);
            Assert.False(config.UseDefaultFilters);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void DecompositionLevels_Effect_OnImageSize()
        {
            // More levels = more resolution scaling options
            var lowLevels = new WaveletConfigurationBuilder()
                .WithDecompositionLevels(2);
            
            var highLevels = new WaveletConfigurationBuilder()
                .WithDecompositionLevels(8);
            
            Assert.True(lowLevels.DecompositionLevels < highLevels.DecompositionLevels);
            Assert.True(lowLevels.IsValid);
            Assert.True(highLevels.IsValid);
        }
    }
}
