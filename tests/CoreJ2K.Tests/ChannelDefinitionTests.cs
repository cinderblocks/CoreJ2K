// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using Xunit;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.util;

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

        #region File I/O Integration Tests

        [Fact]
        public void ChannelDefinitionBox_WriteAndRead_Rgba()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = ChannelDefinitionData.CreateRgba();

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                // Write codestream first
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 4,
                    bpc: new[] { 8, 8, 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.True(reader.Metadata.ChannelDefinitions.HasDefinitions);
                Assert.Equal(4, reader.Metadata.ChannelDefinitions.Channels.Count);
                Assert.True(reader.Metadata.ChannelDefinitions.HasAlphaChannel);

                var ch0 = reader.Metadata.ChannelDefinitions.GetChannel(0);
                Assert.Equal(0, ch0.ChannelIndex);
                Assert.Equal(ChannelType.Color, ch0.ChannelType);
                Assert.Equal(1, ch0.Association);

                var ch3 = reader.Metadata.ChannelDefinitions.GetChannel(3);
                Assert.Equal(3, ch3.ChannelIndex);
                Assert.Equal(ChannelType.Opacity, ch3.ChannelType);
                Assert.Equal(0, ch3.Association);
            }
        }

        [Fact]
        public void ChannelDefinitionBox_WriteAndRead_GrayscaleAlpha()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = ChannelDefinitionData.CreateGrayscaleAlpha();

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 2,
                    bpc: new[] { 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.Equal(2, reader.Metadata.ChannelDefinitions.Channels.Count);
                Assert.True(reader.Metadata.ChannelDefinitions.HasAlphaChannel);

                var colorChannels = reader.Metadata.ChannelDefinitions.GetColorChannels().ToList();
                Assert.Single(colorChannels);
                Assert.Equal(1, colorChannels[0].Association);

                var opacityChannels = reader.Metadata.ChannelDefinitions.GetOpacityChannels().ToList();
                Assert.Single(opacityChannels);
            }
        }

        [Fact]
        public void ChannelDefinitionBox_WriteAndRead_RgbWithoutAlpha()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = ChannelDefinitionData.CreateRgb();

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 3,
                    bpc: new[] { 8, 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.Equal(3, reader.Metadata.ChannelDefinitions.Channels.Count);
                Assert.False(reader.Metadata.ChannelDefinitions.HasAlphaChannel);
            }
        }

        [Fact]
        public void ChannelDefinitionBox_NotWritten_WhenNoDefinitions()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            // Don't set channel definitions

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 3,
                    bpc: new[] { 8, 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.True(reader.Metadata.ChannelDefinitions == null || 
                           !reader.Metadata.ChannelDefinitions.HasDefinitions);
            }
        }

        [Fact]
        public void ChannelDefinitionBox_WriteAndRead_PremultipliedAlpha()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddColorChannel(0, 1);
            metadata.ChannelDefinitions.AddColorChannel(1, 2);
            metadata.ChannelDefinitions.AddColorChannel(2, 3);
            metadata.ChannelDefinitions.AddPremultipliedOpacityChannel(3, 0);

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 4,
                    bpc: new[] { 8, 8, 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.True(reader.Metadata.ChannelDefinitions.HasAlphaChannel);

                var alphaCh = reader.Metadata.ChannelDefinitions.GetChannel(3);
                Assert.Equal(ChannelType.PremultipliedOpacity, alphaCh.ChannelType);
            }
        }

        [Fact]
        public void ChannelDefinitionBox_WriteAndRead_BgrOrder()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Color, 3); // Blue
            metadata.ChannelDefinitions.AddChannel(1, ChannelType.Color, 2); // Green
            metadata.ChannelDefinitions.AddChannel(2, ChannelType.Color, 1); // Red

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 3,
                    bpc: new[] { 8, 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.Equal(3, reader.Metadata.ChannelDefinitions.Channels.Count);

                var ch0 = reader.Metadata.ChannelDefinitions.GetChannel(0);
                Assert.Equal(3, ch0.Association); // Blue

                var ch2 = reader.Metadata.ChannelDefinitions.GetChannel(2);
                Assert.Equal(1, ch2.Association); // Red
            }
        }

        [Fact]
        public void ChannelDefinitionBox_WithResolution_BothWritten()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = ChannelDefinitionData.CreateRgba();
            metadata.SetResolutionDpi(300, 300, isCapture: true);

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 4,
                    bpc: new[] { 8, 8, 8, 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.NotNull(reader.Metadata.Resolution);
                Assert.True(reader.Metadata.ChannelDefinitions.HasDefinitions);
                Assert.True(reader.Metadata.Resolution.HasCaptureResolution);
            }
        }

        [Fact]
        public void ChannelDefinitionBox_UnspecifiedType_RoundTrip()
        {
            var codestream = CreateMinimalCodestream();

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Unspecified, 65535);

            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(
                    ms,
                    height: 64,
                    width: 64,
                    nc: 1,
                    bpc: new[] { 8 },
                    clength: codestream.Length
                )
                {
                    Metadata = metadata
                };

                writer.writeFileFormat();
                jp2Data = ms.ToArray();
            }

            using (var ms = new MemoryStream(jp2Data))
            {
                var raf = new ISRandomAccessIO(ms);
                var reader = new FileFormatReader(raf);
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                var ch = reader.Metadata.ChannelDefinitions.GetChannel(0);
                Assert.Equal(ChannelType.Unspecified, ch.ChannelType);
                Assert.Equal(65535, ch.Association);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a minimal valid JPEG2000 codestream for testing.
        /// </summary>
        private byte[] CreateMinimalCodestream()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // SOC (Start of Codestream) - 0xFF4F
                writer.Write((byte)0xFF);
                writer.Write((byte)0x4F);

                // SIZ (Image and tile size) marker - 0xFF51
                writer.Write((byte)0xFF);
                writer.Write((byte)0x51);
                writer.Write((short)47); // Lsiz

                writer.Write((short)0); // Rsiz
                writer.Write((int)64); // Xsiz
                writer.Write((int)64); // Ysiz
                writer.Write((int)0); // XOsiz
                writer.Write((int)0); // YOsiz
                writer.Write((int)64); // XTsiz
                writer.Write((int)64); // YTsiz
                writer.Write((int)0); // XTOsiz
                writer.Write((int)0); // YTOsiz
                writer.Write((short)3); // Csiz (3 components)

                // Component 0
                writer.Write((byte)7); // Ssiz (8-bit unsigned)
                writer.Write((byte)1); // XRsiz
                writer.Write((byte)1); // YRsiz

                // Component 1
                writer.Write((byte)7);
                writer.Write((byte)1);
                writer.Write((byte)1);

                // Component 2
                writer.Write((byte)7);
                writer.Write((byte)1);
                writer.Write((byte)1);

                // COD (Coding style default) - 0xFF52
                writer.Write((byte)0xFF);
                writer.Write((byte)0x52);
                writer.Write((short)12); // Lcod
                writer.Write((byte)0); // Scod
                writer.Write((byte)0); // SGcod - Progression order
                writer.Write((short)1); // Number of layers
                writer.Write((byte)0); // Multiple component transform
                writer.Write((byte)5); // Number of decomposition levels
                writer.Write((byte)2); // Code-block width
                writer.Write((byte)2); // Code-block height
                writer.Write((byte)0); // Code-block style
                writer.Write((byte)0); // Wavelet transformation

                // QCD (Quantization default) - 0xFF5C
                writer.Write((byte)0xFF);
                writer.Write((byte)0x5C);
                writer.Write((short)4); // Lqcd
                writer.Write((byte)0); // Sqcd
                writer.Write((byte)8); // SPqcd

                // EOC (End of Codestream) - 0xFFD9
                writer.Write((byte)0xFF);
                writer.Write((byte)0xD9);

                return ms.ToArray();
            }
        }

        #endregion
    }
}
