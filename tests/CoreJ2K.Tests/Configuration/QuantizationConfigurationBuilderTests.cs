// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the quantization configuration builder API.
    /// </summary>
    public class QuantizationConfigurationBuilderTests
    {
        [Fact]
        public void Constructor_CreatesDefaultConfiguration()
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.0078125f, config.BaseStepSize);
            Assert.Equal(1, config.GuardBits);
            Assert.True(config.UseDefaultSteps);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseReversible_SetsReversibleType()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseReversible();
            
            Assert.Equal(QuantizationType.Reversible, config.Type);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseDerived_SetsDerivedType()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseDerived();
            
            Assert.Equal(QuantizationType.Derived, config.Type);
            Assert.True(config.UseDefaultSteps);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseExpounded_SetsExpoundedType()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded();
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.True(config.IsValid);
        }
        
        [Theory]
        [InlineData(0.001f)]
        [InlineData(0.0078125f)]
        [InlineData(0.02f)]
        [InlineData(0.1f)]
        public void WithBaseStepSize_SetsStepSize(float stepSize)
        {
            var config = new QuantizationConfigurationBuilder()
                .WithBaseStepSize(stepSize);
            
            Assert.Equal(stepSize, config.BaseStepSize);
        }
        
        [Fact]
        public void WithBaseStepSize_ZeroOrNegative_ThrowsException()
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithBaseStepSize(0));
            Assert.Throws<ArgumentException>(() => config.WithBaseStepSize(-0.01f));
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        public void WithGuardBits_SetsGuardBits(int bits)
        {
            var config = new QuantizationConfigurationBuilder()
                .WithGuardBits(bits);
            
            Assert.Equal(bits, config.GuardBits);
        }
        
        [Theory]
        [InlineData(-1)]
        [InlineData(8)]
        [InlineData(10)]
        public void WithGuardBits_OutOfRange_ThrowsException(int bits)
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithGuardBits(bits));
        }
        
        [Fact]
        public void WithSubbandStep_SetsCustomStep()
        {
            var config = new QuantizationConfigurationBuilder()
                .WithSubbandStep(0, "LL", 0.01f);
            
            Assert.False(config.UseDefaultSteps);
            Assert.True(config.IsValid);
        }
        
        [Theory]
        [InlineData("LL")]
        [InlineData("HL")]
        [InlineData("LH")]
        [InlineData("HH")]
        [InlineData("ll")]
        [InlineData("hh")]
        public void WithSubbandStep_ValidSubbands_Succeeds(string subband)
        {
            var config = new QuantizationConfigurationBuilder()
                .WithSubbandStep(0, subband, 0.01f);
            
            Assert.False(config.UseDefaultSteps);
        }
        
        [Theory]
        [InlineData("XX")]
        [InlineData("AB")]
        public void WithSubbandStep_InvalidSubband_ThrowsException(string subband)
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithSubbandStep(0, subband, 0.01f));
        }
        
        [Fact]
        public void WithSubbandStep_NullOrEmptySubband_ThrowsArgumentNullException()
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Throws<ArgumentNullException>(() => config.WithSubbandStep(0, null, 0.01f));
            Assert.Throws<ArgumentNullException>(() => config.WithSubbandStep(0, "", 0.01f));
        }
        
        [Fact]
        public void WithSubbandStep_NegativeLevel_ThrowsException()
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithSubbandStep(-1, "LL", 0.01f));
        }
        
        [Fact]
        public void WithSubbandStep_ZeroStepSize_ThrowsException()
        {
            var config = new QuantizationConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithSubbandStep(0, "LL", 0));
        }
        
        [Fact]
        public void WithResolutionSteps_SetsAllSubbands()
        {
            var config = new QuantizationConfigurationBuilder()
                .WithResolutionSteps(0, 0.01f, 0.012f, 0.013f, 0.015f);
            
            Assert.False(config.UseDefaultSteps);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseDefaultSubbandSteps_ClearsCustomSteps()
        {
            var config = new QuantizationConfigurationBuilder()
                .WithSubbandStep(0, "LL", 0.01f)
                .WithSubbandStep(1, "HL", 0.02f)
                .UseDefaultSubbandSteps();
            
            Assert.True(config.UseDefaultSteps);
        }
        
        [Fact]
        public void ForHighQuality_ConfiguresHighQuality()
        {
            var config = new QuantizationConfigurationBuilder()
                .ForHighQuality();
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.002f, config.BaseStepSize);
            Assert.Equal(2, config.GuardBits);
        }
        
        [Fact]
        public void ForBalanced_ConfiguresBalanced()
        {
            var config = new QuantizationConfigurationBuilder()
                .ForBalanced();
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.0078125f, config.BaseStepSize);
            Assert.Equal(1, config.GuardBits);
        }
        
        [Fact]
        public void ForHighCompression_ConfiguresHighCompression()
        {
            var config = new QuantizationConfigurationBuilder()
                .ForHighCompression();
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.02f, config.BaseStepSize);
            Assert.Equal(1, config.GuardBits);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.01f)
                .WithGuardBits(2)
                .WithSubbandStep(0, "LL", 0.008f)
                .WithSubbandStep(1, "HH", 0.015f);
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.01f, config.BaseStepSize);
            Assert.Equal(2, config.GuardBits);
            Assert.False(config.UseDefaultSteps);
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.01f)
                .WithGuardBits(2);
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Validate_ReversibleWithCustomSteps_ReturnsError()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseReversible()
                .WithSubbandStep(0, "LL", 0.01f);
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("reversible"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_DerivedWithCustomSteps_ReturnsError()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseDerived()
                .WithSubbandStep(0, "LL", 0.01f);
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("derived"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.01f)
                .WithGuardBits(2)
                .WithSubbandStep(0, "LL", 0.008f);
            
            var clone = original.Clone();
            
            // Modify clone
            clone.WithBaseStepSize(0.02f);
            clone.WithGuardBits(1);
            
            // Original should be unchanged
            Assert.Equal(0.01f, original.BaseStepSize);
            Assert.Equal(2, original.GuardBits);
            
            // Clone should have new values
            Assert.Equal(0.02f, clone.BaseStepSize);
            Assert.Equal(1, clone.GuardBits);
        }
        
        [Fact]
        public void ToString_ReturnsDescriptiveString()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.01f)
                .WithGuardBits(2);
            
            var str = config.ToString();
            
            Assert.Contains("Expounded", str);
            Assert.Contains("0.01", str);
            Assert.Contains("2", str);
        }
        
        [Fact]
        public void ToString_Reversible_IndicatesLossless()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseReversible();
            
            var str = config.ToString();
            
            Assert.Contains("Reversible", str);
            Assert.Contains("lossless", str.ToLower());
        }
        
        // Preset tests
        
        [Fact]
        public void Presets_Lossless_IsReversible()
        {
            var config = QuantizationPresets.Lossless;
            
            Assert.Equal(QuantizationType.Reversible, config.Type);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_NearLossless_HasSmallStepSize()
        {
            var config = QuantizationPresets.NearLossless;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.001f, config.BaseStepSize);
            Assert.Equal(2, config.GuardBits);
        }
        
        [Fact]
        public void Presets_HighQuality_ConfiguredCorrectly()
        {
            var config = QuantizationPresets.HighQuality;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.002f, config.BaseStepSize);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Balanced_ConfiguredCorrectly()
        {
            var config = QuantizationPresets.Balanced;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.0078125f, config.BaseStepSize);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_HighCompression_HasLargerStepSize()
        {
            var config = QuantizationPresets.HighCompression;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.02f, config.BaseStepSize);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_MaximumCompression_HasLargestStepSize()
        {
            var config = QuantizationPresets.MaximumCompression;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.05f, config.BaseStepSize);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Medical_IsLossless()
        {
            var config = QuantizationPresets.Medical;
            
            Assert.Equal(QuantizationType.Reversible, config.Type);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Archival_HighQuality()
        {
            var config = QuantizationPresets.Archival;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.True(config.BaseStepSize < 0.002f);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Web_Balanced()
        {
            var config = QuantizationPresets.Web;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.Equal(0.01f, config.BaseStepSize);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Thumbnail_HigherCompression()
        {
            var config = QuantizationPresets.Thumbnail;
            
            Assert.Equal(QuantizationType.Expounded, config.Type);
            Assert.True(config.BaseStepSize > QuantizationPresets.Web.BaseStepSize);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_AllAreValid()
        {
            Assert.True(QuantizationPresets.Lossless.IsValid);
            Assert.True(QuantizationPresets.NearLossless.IsValid);
            Assert.True(QuantizationPresets.HighQuality.IsValid);
            Assert.True(QuantizationPresets.Balanced.IsValid);
            Assert.True(QuantizationPresets.HighCompression.IsValid);
            Assert.True(QuantizationPresets.MaximumCompression.IsValid);
            Assert.True(QuantizationPresets.Medical.IsValid);
            Assert.True(QuantizationPresets.Archival.IsValid);
            Assert.True(QuantizationPresets.Web.IsValid);
            Assert.True(QuantizationPresets.Thumbnail.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_MedicalImaging()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseReversible(); // Lossless for medical
            
            Assert.Equal(QuantizationType.Reversible, config.Type);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_ArchivalStorage()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.0015f)
                .WithGuardBits(2);
            
            Assert.True(config.IsValid);
            Assert.True(config.BaseStepSize < 0.002f); // Very high quality
        }
        
        [Fact]
        public void RealWorldScenario_WebDelivery()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.01f)
                .WithGuardBits(1);
            
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_CustomSubbandWeighting()
        {
            var config = new QuantizationConfigurationBuilder()
                .UseExpounded()
                .WithBaseStepSize(0.01f)
                .WithResolutionSteps(0, 0.008f, 0.012f, 0.012f, 0.015f) // Fine-tune subbands
                .WithResolutionSteps(1, 0.010f, 0.015f, 0.015f, 0.020f);
            
            Assert.True(config.IsValid);
            Assert.False(config.UseDefaultSteps);
        }
    }
}
