// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the progression configuration builder API.
    /// </summary>
    public class ProgressionConfigurationBuilderTests
    {
        [Fact]
        public void Constructor_CreatesDefaultConfiguration()
        {
            var config = new ProgressionConfigurationBuilder();
            
            Assert.Equal(ProgressionOrder.LRCP, config.DefaultOrder);
            Assert.True(config.UseDefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseLRCP_SetsLRCPOrder()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseLRCP();
            
            Assert.Equal(ProgressionOrder.LRCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseRLCP_SetsRLCPOrder()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseRLCP();
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseRPCL_SetsRPCLOrder()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseRPCL();
            
            Assert.Equal(ProgressionOrder.RPCL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UsePCRL_SetsPCRLOrder()
        {
            var config = new ProgressionConfigurationBuilder()
                .UsePCRL();
            
            Assert.Equal(ProgressionOrder.PCRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseCPRL_SetsCPRLOrder()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseCPRL();
            
            Assert.Equal(ProgressionOrder.CPRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void WithTileOrder_SetsCustomTileOrder()
        {
            var config = new ProgressionConfigurationBuilder()
                .WithTileOrder(0, ProgressionOrder.RLCP);
            
            Assert.False(config.UseDefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void WithTileOrder_NegativeTile_ThrowsException()
        {
            var config = new ProgressionConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => 
                config.WithTileOrder(-1, ProgressionOrder.LRCP));
        }
        
        [Fact]
        public void WithTileOrder_MultipleTiles_Succeeds()
        {
            var config = new ProgressionConfigurationBuilder()
                .WithTileOrder(0, ProgressionOrder.LRCP)
                .WithTileOrder(1, ProgressionOrder.RLCP)
                .WithTileOrder(2, ProgressionOrder.RPCL);
            
            Assert.False(config.UseDefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void UseDefaultTileOrders_ClearsCustomOrders()
        {
            var config = new ProgressionConfigurationBuilder()
                .WithTileOrder(0, ProgressionOrder.RLCP)
                .WithTileOrder(1, ProgressionOrder.RPCL)
                .UseDefaultTileOrders();
            
            Assert.True(config.UseDefaultOrder);
        }
        
        [Fact]
        public void ForQualityProgressive_ConfiguresLRCP()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForQualityProgressive();
            
            Assert.Equal(ProgressionOrder.LRCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForResolutionProgressive_ConfiguresRLCP()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForResolutionProgressive();
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForSpatialBrowsing_ConfiguresRPCL()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForSpatialBrowsing();
            
            Assert.Equal(ProgressionOrder.RPCL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForTileAccess_ConfiguresPCRL()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForTileAccess();
            
            Assert.Equal(ProgressionOrder.PCRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void ForComponentAccess_ConfiguresCPRL()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForComponentAccess();
            
            Assert.Equal(ProgressionOrder.CPRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseRLCP()
                .WithTileOrder(0, ProgressionOrder.LRCP)
                .WithTileOrder(1, ProgressionOrder.RPCL);
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.False(config.UseDefaultOrder);
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseRLCP()
                .WithTileOrder(0, ProgressionOrder.LRCP);
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new ProgressionConfigurationBuilder()
                .UseRLCP()
                .WithTileOrder(0, ProgressionOrder.LRCP);
            
            var clone = original.Clone();
            
            // Modify clone
            clone.UsePCRL();
            
            // Original should be unchanged
            Assert.Equal(ProgressionOrder.RLCP, original.DefaultOrder);
            
            // Clone should have new values
            Assert.Equal(ProgressionOrder.PCRL, clone.DefaultOrder);
        }
        
        [Fact]
        public void ToString_ReturnsDescriptiveString()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseRLCP();
            
            var str = config.ToString();
            
            Assert.Contains("RLCP", str);
            Assert.Contains("Progression", str);
        }
        
        [Fact]
        public void ToString_WithCustomTiles_IndicatesCustomTiles()
        {
            var config = new ProgressionConfigurationBuilder()
                .UseRLCP()
                .WithTileOrder(0, ProgressionOrder.LRCP)
                .WithTileOrder(1, ProgressionOrder.RPCL);
            
            var str = config.ToString();
            
            Assert.Contains("2 custom", str);
        }
        
        // Preset tests
        
        [Fact]
        public void Presets_QualityProgressive_IsLRCP()
        {
            var config = ProgressionPresets.QualityProgressive;
            
            Assert.Equal(ProgressionOrder.LRCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_ResolutionProgressive_IsRLCP()
        {
            var config = ProgressionPresets.ResolutionProgressive;
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_SpatialBrowsing_IsRPCL()
        {
            var config = ProgressionPresets.SpatialBrowsing;
            
            Assert.Equal(ProgressionOrder.RPCL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_TileAccess_IsPCRL()
        {
            var config = ProgressionPresets.TileAccess;
            
            Assert.Equal(ProgressionOrder.PCRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_ComponentAccess_IsCPRL()
        {
            var config = ProgressionPresets.ComponentAccess;
            
            Assert.Equal(ProgressionOrder.CPRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_WebStreaming_IsLRCP()
        {
            var config = ProgressionPresets.WebStreaming;
            
            Assert.Equal(ProgressionOrder.LRCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Medical_IsRLCP()
        {
            var config = ProgressionPresets.Medical;
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_Geospatial_IsRPCL()
        {
            var config = ProgressionPresets.Geospatial;
            
            Assert.Equal(ProgressionOrder.RPCL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_AllAreValid()
        {
            Assert.True(ProgressionPresets.QualityProgressive.IsValid);
            Assert.True(ProgressionPresets.ResolutionProgressive.IsValid);
            Assert.True(ProgressionPresets.SpatialBrowsing.IsValid);
            Assert.True(ProgressionPresets.TileAccess.IsValid);
            Assert.True(ProgressionPresets.ComponentAccess.IsValid);
            Assert.True(ProgressionPresets.WebStreaming.IsValid);
            Assert.True(ProgressionPresets.Medical.IsValid);
            Assert.True(ProgressionPresets.Geospatial.IsValid);
        }
        
        // Extension method tests
        
        [Fact]
        public void ToParameterString_ReturnsCorrectString()
        {
            Assert.Equal("LRCP", ProgressionOrder.LRCP.ToParameterString());
            Assert.Equal("RLCP", ProgressionOrder.RLCP.ToParameterString());
            Assert.Equal("RPCL", ProgressionOrder.RPCL.ToParameterString());
            Assert.Equal("PCRL", ProgressionOrder.PCRL.ToParameterString());
            Assert.Equal("CPRL", ProgressionOrder.CPRL.ToParameterString());
        }
        
        [Fact]
        public void GetDescription_ReturnsDescription()
        {
            var desc = ProgressionOrder.LRCP.GetDescription();
            
            Assert.Contains("LRCP", desc);
            Assert.Contains("Quality", desc);
        }
        
        [Fact]
        public void GetBestUseCase_ReturnsUseCase()
        {
            var useCase = ProgressionOrder.RLCP.GetBestUseCase();
            
            Assert.Contains("resolution", useCase.ToLower());
        }
        
        // Real-world scenario tests
        
        [Fact]
        public void RealWorldScenario_WebStreaming()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForQualityProgressive();
            
            Assert.Equal(ProgressionOrder.LRCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_ImageBrowser()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForResolutionProgressive();
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_GeospatialMapping()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForSpatialBrowsing();
            
            Assert.Equal(ProgressionOrder.RPCL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_TileServer()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForTileAccess();
            
            Assert.Equal(ProgressionOrder.PCRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_MultiSpectralImaging()
        {
            var config = new ProgressionConfigurationBuilder()
                .ForComponentAccess();
            
            Assert.Equal(ProgressionOrder.CPRL, config.DefaultOrder);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_MixedTileOrders()
        {
            // Different orders for different tiles (e.g., important tiles first)
            var config = new ProgressionConfigurationBuilder()
                .UseRLCP()  // Default for most tiles
                .WithTileOrder(0, ProgressionOrder.LRCP)  // Quality progressive for center tile
                .WithTileOrder(1, ProgressionOrder.LRCP);
            
            Assert.Equal(ProgressionOrder.RLCP, config.DefaultOrder);
            Assert.False(config.UseDefaultOrder);
            Assert.True(config.IsValid);
        }
    }
}
