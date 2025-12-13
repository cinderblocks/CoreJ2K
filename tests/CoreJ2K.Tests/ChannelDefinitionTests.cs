// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using Xunit;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for channel definition metadata support.
    /// </summary>
    public class ChannelDefinitionTests
    {
        [Fact]
        public void ChannelDefinitionData_DefaultConstructor_HasNoDefinitions()
        {
            var def = new ChannelDefinitionData();

            Assert.False(def.HasDefinitions);
            Assert.False(def.HasAlphaChannel);
            Assert.Empty(def.Channels);
        }

        [Fact]
        public void ChannelDefinitionData_AddColorChannel_AddsCorrectly()
        {
            var def = new ChannelDefinitionData();
            
            def.AddColorChannel(0, 1); // Red

            Assert.True(def.HasDefinitions);
            Assert.Single(def.Channels);
            
            var ch = def.Channels[0];
            Assert.Equal(0, ch.ChannelIndex);
            Assert.Equal(ChannelType.Color, ch.ChannelType);
            Assert.Equal(1, ch.Association);
        }

        [Fact]
        public void ChannelDefinitionData_AddOpacityChannel_SetsHasAlpha()
        {
            var def = new ChannelDefinitionData();
            
            def.AddOpacityChannel(3, 0);

            Assert.True(def.HasAlphaChannel);
            
            var ch = def.Channels[0];
            Assert.Equal(ChannelType.Opacity, ch.ChannelType);
        }

        [Fact]
        public void ChannelDefinitionData_CreateRgb_CreatesCorrectDefinition()
        {
            var def = ChannelDefinitionData.CreateRgb();

            Assert.Equal(3, def.Channels.Count);
            Assert.False(def.HasAlphaChannel);
            
            // Check Red channel
            var red = def.GetChannel(0);
            Assert.Equal(ChannelType.Color, red.ChannelType);
            Assert.Equal(1, red.Association);
            
            // Check Green channel
            var green = def.GetChannel(1);
            Assert.Equal(2, green.Association);
            
            // Check Blue channel
            var blue = def.GetChannel(2);
            Assert.Equal(3, blue.Association);
        }

        [Fact]
        public void ChannelDefinitionData_CreateRgba_IncludesAlpha()
        {
            var def = ChannelDefinitionData.CreateRgba();

            Assert.Equal(4, def.Channels.Count);
            Assert.True(def.HasAlphaChannel);
            
            var alpha = def.GetChannel(3);
            Assert.Equal(ChannelType.Opacity, alpha.ChannelType);
            Assert.Equal(0, alpha.Association); // Whole image
        }

        [Fact]
        public void ChannelDefinitionData_CreateGrayscale_SingleChannel()
        {
            var def = ChannelDefinitionData.CreateGrayscale();

            Assert.Single(def.Channels);
            Assert.False(def.HasAlphaChannel);
            
            var gray = def.GetChannel(0);
            Assert.Equal(ChannelType.Color, gray.ChannelType);
            Assert.Equal(1, gray.Association);
        }

        [Fact]
        public void ChannelDefinitionData_CreateGrayscaleAlpha_HasAlpha()
        {
            var def = ChannelDefinitionData.CreateGrayscaleAlpha();

            Assert.Equal(2, def.Channels.Count);
            Assert.True(def.HasAlphaChannel);
            
            var gray = def.GetChannel(0);
            Assert.Equal(ChannelType.Color, gray.ChannelType);
            
            var alpha = def.GetChannel(1);
            Assert.Equal(ChannelType.Opacity, alpha.ChannelType);
        }

        [Fact]
        public void ChannelDefinitionData_AddPremultipliedOpacity_SetsCorrectType()
        {
            var def = new ChannelDefinitionData();
            
            def.AddPremultipliedOpacityChannel(3, 0);

            Assert.True(def.HasAlphaChannel);
            
            var ch = def.Channels[0];
            Assert.Equal(ChannelType.PremultipliedOpacity, ch.ChannelType);
        }

        [Fact]
        public void ChannelDefinitionData_GetColorChannels_ReturnsOnlyColor()
        {
            var def = ChannelDefinitionData.CreateRgba();

            var colorChannels = def.GetColorChannels().ToList();

            Assert.Equal(3, colorChannels.Count);
            Assert.All(colorChannels, ch => Assert.Equal(ChannelType.Color, ch.ChannelType));
        }

        [Fact]
        public void ChannelDefinitionData_GetOpacityChannels_ReturnsOnlyOpacity()
        {
            var def = new ChannelDefinitionData();
            def.AddColorChannel(0, 1);
            def.AddColorChannel(1, 2);
            def.AddOpacityChannel(2, 0);
            def.AddPremultipliedOpacityChannel(3, 0);

            var opacityChannels = def.GetOpacityChannels().ToList();

            Assert.Equal(2, opacityChannels.Count);
        }

        [Fact]
        public void ChannelDefinitionData_GetChannel_ReturnsCorrectChannel()
        {
            var def = ChannelDefinitionData.CreateRgb();

            var ch1 = def.GetChannel(1);

            Assert.NotNull(ch1);
            Assert.Equal(1, ch1.ChannelIndex);
        }

        [Fact]
        public void ChannelDefinitionData_GetChannel_ReturnsNullIfNotFound()
        {
            var def = ChannelDefinitionData.CreateRgb();

            var ch99 = def.GetChannel(99);

            Assert.Null(ch99);
        }

        [Fact]
        public void ChannelDefinitionData_ToString_FormatsCorrectly()
        {
            var def = new ChannelDefinitionData();
            def.AddColorChannel(0, 1);
            def.AddOpacityChannel(1, 0);

            var str = def.ToString();

            Assert.Contains("Ch0", str);
            Assert.Contains("Color", str);
            Assert.Contains("Ch1", str);
            Assert.Contains("Opacity", str);
        }

        [Fact]
        public void ChannelDefinitionData_ToString_NoDefinitions()
        {
            var def = new ChannelDefinitionData();

            var str = def.ToString();

            Assert.Contains("No channel", str);
        }

        [Fact]
        public void ChannelDefinition_ToString_FormatsCorrectly()
        {
            var ch = new ChannelDefinition
            {
                ChannelIndex = 2,
                ChannelType = ChannelType.Opacity,
                Association = 0
            };

            var str = ch.ToString();

            Assert.Contains("Ch2", str);
            Assert.Contains("Opacity", str);
            Assert.Contains("Assoc=0", str);
        }

        [Fact]
        public void ChannelType_HasCorrectValues()
        {
            Assert.Equal((ushort)0, (ushort)ChannelType.Color);
            Assert.Equal((ushort)1, (ushort)ChannelType.Opacity);
            Assert.Equal((ushort)2, (ushort)ChannelType.PremultipliedOpacity);
            Assert.Equal((ushort)65535, (ushort)ChannelType.Unspecified);
        }

        [Fact]
        public void ChannelDefinitionData_CustomChannelMapping_Works()
        {
            var def = new ChannelDefinitionData();
            
            // BGR order instead of RGB
            def.AddChannel(0, ChannelType.Color, 3); // Blue
            def.AddChannel(1, ChannelType.Color, 2); // Green
            def.AddChannel(2, ChannelType.Color, 1); // Red

            Assert.Equal(3, def.Channels.Count);
            Assert.Equal(3, def.Channels[0].Association); // Blue
            Assert.Equal(1, def.Channels[2].Association); // Red
        }

        [Fact]
        public void J2KMetadata_ChannelDefinitions_Integration()
        {
            var metadata = new J2KMetadata();
            
            metadata.ChannelDefinitions = ChannelDefinitionData.CreateRgba();

            Assert.NotNull(metadata.ChannelDefinitions);
            Assert.True(metadata.ChannelDefinitions.HasAlphaChannel);
        }
    }
}
