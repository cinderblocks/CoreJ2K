// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Text;
using CoreJ2K.j2k.fileformat;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Comprehensive tests for FileFormatWriter to ensure all metadata boxes
    /// are correctly written and can be read back.
    /// </summary>
    public class FileFormatWriterTests
    {
        [Fact]
        public void TestWriteBasicJP2File()
        {
            // Arrange
            var metadata = new J2KMetadata();
            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert - Read it back
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.True(reader.JP2FFUsed);
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestWriteWithXMLBox()
        {
            // Arrange
            var metadata = new J2KMetadata();
            var xmlContent = "<?xml version=\"1.0\"?><test><data>Sample XML</data></test>";
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = xmlContent });

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert - Read it back
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.Single(reader.Metadata.XmlBoxes);
                Assert.Equal(xmlContent, reader.Metadata.XmlBoxes[0].XmlContent);
            }
        }

        [Fact]
        public void TestWriteWithMultipleXMLBoxes()
        {
            // Arrange
            var metadata = new J2KMetadata();
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = "<?xml version=\"1.0\"?><test1/>" });
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = "<?xml version=\"1.0\"?><test2/>" });
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = "<?xml version=\"1.0\"?><test3/>" });

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.Equal(3, reader.Metadata.XmlBoxes.Count);
            }
        }

        [Fact]
        public void TestWriteWithUUIDBox()
        {
            // Arrange
            var metadata = new J2KMetadata();
            var uuid = Guid.NewGuid();
            var data = Encoding.UTF8.GetBytes("Custom UUID data");
            metadata.UuidBoxes.Add(new UuidBox { Uuid = uuid, Data = data });

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.Single(reader.Metadata.UuidBoxes);
                Assert.Equal(uuid, reader.Metadata.UuidBoxes[0].Uuid);
                Assert.Equal(data, reader.Metadata.UuidBoxes[0].Data);
            }
        }

        [Fact]
        public void TestWriteWithJPRBox()
        {
            // Arrange
            var metadata = new J2KMetadata();
            var copyright = "Copyright © 2025 Test Company";
            metadata.IntellectualPropertyRights.Add(new JprBox { Text = copyright });

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.Single(reader.Metadata.IntellectualPropertyRights);
                Assert.Equal(copyright, reader.Metadata.IntellectualPropertyRights[0].Text);
            }
        }

        [Fact]
        public void TestWriteWithLabelBox()
        {
            // Arrange
            var metadata = new J2KMetadata();
            var label = "Test Image Label";
            metadata.Labels.Add(new LabelBox { Label = label });

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.Single(reader.Metadata.Labels);
                Assert.Equal(label, reader.Metadata.Labels[0].Label);
            }
        }

        [Fact]
        public void TestWriteWithPaletteAndComponentMapping()
        {
            // Arrange
            var metadata = new J2KMetadata();

            // Create a simple 3-entry RGB palette
            var paletteData = new int[3][];
            paletteData[0] = new[] { 255, 0, 0 };     // Red
            paletteData[1] = new[] { 0, 255, 0 };     // Green
            paletteData[2] = new[] { 0, 0, 255 };     // Blue

            metadata.SetPalette(3, 3, new short[] { 7, 7, 7 }, paletteData);

            // Map indexed component to RGB channels
            metadata.AddComponentMapping(0, 1, 0); // R from palette column 0
            metadata.AddComponentMapping(0, 1, 1); // G from palette column 1
            metadata.AddComponentMapping(0, 1, 2); // B from palette column 2

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.Palette);
                Assert.Equal(3, reader.Metadata.Palette.NumEntries);
                Assert.Equal(3, reader.Metadata.Palette.NumColumns);

                Assert.NotNull(reader.Metadata.ComponentMapping);
                Assert.Equal(3, reader.Metadata.ComponentMapping.NumChannels);
                Assert.True(reader.Metadata.ComponentMapping.UsesPalette);
            }
        }

        [Fact]
        public void TestWriteWithChannelDefinitions()
        {
            // Arrange
            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Color, 1);        // R
            metadata.ChannelDefinitions.AddChannel(1, ChannelType.Color, 2);        // G
            metadata.ChannelDefinitions.AddChannel(2, ChannelType.Color, 3);        // B
            metadata.ChannelDefinitions.AddChannel(3, ChannelType.Opacity, 0);      // Alpha

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 4, new[] { 8, 8, 8, 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.Equal(4, reader.Metadata.ChannelDefinitions.Channels.Count);
                Assert.True(reader.Metadata.ChannelDefinitions.HasAlphaChannel);
            }
        }

        [Fact]
        public void TestWriteWithResolution()
        {
            // Arrange
            var metadata = new J2KMetadata();
            metadata.Resolution = new ResolutionData();
            metadata.Resolution.SetCaptureResolution(2835.0, 2835.0); // ~72 DPI
            metadata.Resolution.SetDisplayResolution(3543.3, 3543.3); // ~90 DPI

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.Resolution);
                Assert.True(reader.Metadata.Resolution.HasCaptureResolution);
                Assert.True(reader.Metadata.Resolution.HasDisplayResolution);

                // Allow small tolerance for floating-point comparison
                Assert.InRange(reader.Metadata.Resolution.HorizontalCaptureResolution.Value, 2800, 2900);
                Assert.InRange(reader.Metadata.Resolution.VerticalCaptureResolution.Value, 2800, 2900);
            }
        }

        [Fact]
        public void TestWriteWithICCProfile()
        {
            // Arrange
            var metadata = new J2KMetadata();
            
            // Create a minimal ICC profile (just header for testing)
            var iccProfile = CreateMinimalICCProfile();
            metadata.SetIccProfile(iccProfile);

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 8, 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.IccProfile);
                Assert.True(reader.Metadata.IccProfile.IsValid);
                Assert.Equal(iccProfile.Length, reader.Metadata.IccProfile.ProfileSize);
            }
        }

        [Fact]
        public void TestWriteWithAllMetadata()
        {
            // Arrange - Create metadata with everything
            var metadata = new J2KMetadata();

            // XML boxes
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = "<?xml version=\"1.0\"?><metadata/>" });

            // UUID box
            metadata.UuidBoxes.Add(new UuidBox 
            { 
                Uuid = Guid.NewGuid(), 
                Data = Encoding.UTF8.GetBytes("Custom data") 
            });

            // JPR box
            metadata.IntellectualPropertyRights.Add(new JprBox { Text = "Copyright 2025" });

            // Label box
            metadata.Labels.Add(new LabelBox { Label = "Test Image" });

            // Palette
            var paletteData = new int[2][];
            paletteData[0] = new[] { 0, 0, 0 };
            paletteData[1] = new[] { 255, 255, 255 };
            metadata.SetPalette(2, 3, new short[] { 7, 7, 7 }, paletteData);

            // Component mapping
            metadata.AddComponentMapping(0, 1, 0);
            metadata.AddComponentMapping(0, 1, 1);
            metadata.AddComponentMapping(0, 1, 2);

            // Channel definitions
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Color, 1);
            metadata.ChannelDefinitions.AddChannel(1, ChannelType.Color, 2);
            metadata.ChannelDefinitions.AddChannel(2, ChannelType.Color, 3);

            // Resolution
            metadata.Resolution = new ResolutionData();
            metadata.Resolution.SetCaptureResolution(2835.0, 2835.0);

            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert - Read it all back
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Validate everything
                Assert.Single(reader.Metadata.XmlBoxes);
                Assert.Single(reader.Metadata.UuidBoxes);
                Assert.Single(reader.Metadata.IntellectualPropertyRights);
                Assert.Single(reader.Metadata.Labels);
                Assert.NotNull(reader.Metadata.Palette);
                Assert.NotNull(reader.Metadata.ComponentMapping);
                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.NotNull(reader.Metadata.Resolution);

                // Validate file structure
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestWriteWithBitsPerComponentBox()
        {
            // Arrange - Different bit depths per component
            var metadata = new J2KMetadata();
            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                // RGB with different bit depths (8, 10, 12)
                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 10, 12 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.True(reader.FileStructure.HasBitsPerComponentBox);
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestWrittenFilePassesValidation()
        {
            // Arrange
            var metadata = new J2KMetadata();
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = "<?xml version=\"1.0\"?><test/>" });
            
            var codestream = CreateMinimalCodestream();

            // Act
            byte[] jp2Data;
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                jp2Data = ms.ToArray();
            }

            // Assert - Run strict validation
            using (var ms = new MemoryStream(jp2Data))
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = true;

                // Should not throw
                reader.readFileFormat();

                Assert.False(reader.Validator.HasErrors);
                Assert.True(reader.FileStructure.HasSignatureBox);
                Assert.True(reader.FileStructure.HasFileTypeBox);
                Assert.True(reader.FileStructure.HasJP2HeaderBox);
                Assert.True(reader.FileStructure.HasContiguousCodestreamBox);
            }
        }

        /// <summary>
        /// Creates a minimal valid JPEG 2000 codestream for testing.
        /// </summary>
        private byte[] CreateMinimalCodestream()
        {
            // SOC (Start of Codestream) marker
            var soc = new byte[] { 0xFF, 0x4F };
            
            // SIZ (Image and tile size) marker segment - minimal version
            var siz = new byte[]
            {
                0xFF, 0x51, // SIZ marker
                0x00, 0x29, // Lsiz = 41 bytes
                0x00, 0x00, // Rsiz = 0 (baseline)
                0x00, 0x00, 0x00, 0x10, // Xsiz = 16
                0x00, 0x00, 0x00, 0x10, // Ysiz = 16
                0x00, 0x00, 0x00, 0x00, // XOsiz = 0
                0x00, 0x00, 0x00, 0x00, // YOsiz = 0
                0x00, 0x00, 0x00, 0x10, // XTsiz = 16
                0x00, 0x00, 0x00, 0x10, // YTsiz = 16
                0x00, 0x00, 0x00, 0x00, // XTOsiz = 0
                0x00, 0x00, 0x00, 0x00, // YTOsiz = 0
                0x00, 0x01, // Csiz = 1 component
                0x07, // Ssiz = 8 bits unsigned
                0x01, // XRsiz = 1
                0x01  // YRsiz = 1
            };

            // EOC (End of Codestream) marker
            var eoc = new byte[] { 0xFF, 0xD9 };

            // Combine
            var result = new byte[soc.Length + siz.Length + eoc.Length];
            soc.CopyTo(result, 0);
            siz.CopyTo(result, soc.Length);
            eoc.CopyTo(result, soc.Length + siz.Length);

            return result;
        }

        /// <summary>
        /// Creates a minimal ICC profile header for testing (128 bytes).
        /// This is just enough to be recognized as an ICC profile.
        /// </summary>
        private byte[] CreateMinimalICCProfile()
        {
            var profile = new byte[128];
            
            // Profile size (bytes 0-3, big-endian)
            profile[0] = 0x00;
            profile[1] = 0x00;
            profile[2] = 0x00;
            profile[3] = 0x80; // 128 bytes
            
            // CMM Type (bytes 4-7)
            profile[4] = (byte)'a';
            profile[5] = (byte)'c';
            profile[6] = (byte)'s';
            profile[7] = (byte)'p';
            
            // Profile version (bytes 8-11)
            profile[8] = 0x02;  // Major version 2
            profile[9] = 0x00;  // Minor version 0
            
            // Profile class (bytes 12-15) - 'scnr' for scanner
            profile[12] = (byte)'s';
            profile[13] = (byte)'c';
            profile[14] = (byte)'n';
            profile[15] = (byte)'r';
            
            // Color space (bytes 16-19) - 'RGB '
            profile[16] = (byte)'R';
            profile[17] = (byte)'G';
            profile[18] = (byte)'B';
            profile[19] = (byte)' ';
            
            // PCS (bytes 20-23) - 'XYZ '
            profile[20] = (byte)'X';
            profile[21] = (byte)'Y';
            profile[22] = (byte)'Z';
            profile[23] = (byte)' ';
            
            return profile;
        }
    }
}
