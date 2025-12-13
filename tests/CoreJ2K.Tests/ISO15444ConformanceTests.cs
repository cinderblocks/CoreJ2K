// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.fileformat;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Conformance tests for ISO/IEC 15444-1 Part 1 compliance.
    /// These tests verify that CoreJ2K correctly implements the JP2 file format specification.
    /// </summary>
    public class ISO15444ConformanceTests
    {
        #region File Format Structure Tests

        [Fact]
        public void TestSignatureBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.1: JP2 Signature box
            // The signature box shall be the first box in a JP2 file
            // Length: 12 bytes, Type: 'jP  ' (0x6a502020), Data: 0x0d0a870a

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var bytes = ms.ToArray();

                // Verify signature box is first (position 0)
                Assert.Equal(0x00, bytes[0]);
                Assert.Equal(0x00, bytes[1]);
                Assert.Equal(0x00, bytes[2]);
                Assert.Equal(0x0c, bytes[3]); // Length = 12

                // Verify box type
                Assert.Equal(0x6a, bytes[4]); // 'j'
                Assert.Equal(0x50, bytes[5]); // 'P'
                Assert.Equal(0x20, bytes[6]); // ' '
                Assert.Equal(0x20, bytes[7]); // ' '

                // Verify signature data
                Assert.Equal(0x0d, bytes[8]);
                Assert.Equal(0x0a, bytes[9]);
                Assert.Equal(0x87, bytes[10]);
                Assert.Equal(0x0a, bytes[11]);
            }
        }

        [Fact]
        public void TestFileTypeBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.2: File Type box
            // Shall immediately follow the JP2 Signature box
            // Brand field: 'jp2 ' (0x6a703220)
            // MinV: 0
            // CL: shall include at least 'jp2 '

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var bytes = ms.ToArray();

                // File Type box should be at position 12 (after signature)
                Assert.Equal(0x00, bytes[12]);
                Assert.Equal(0x00, bytes[13]);
                Assert.Equal(0x00, bytes[14]);
                Assert.Equal(0x14, bytes[15]); // Length = 20

                // Verify box type 'ftyp'
                Assert.Equal(0x66, bytes[16]); // 'f'
                Assert.Equal(0x74, bytes[17]); // 't'
                Assert.Equal(0x79, bytes[18]); // 'y'
                Assert.Equal(0x70, bytes[19]); // 'p'

                // Verify Brand 'jp2 '
                Assert.Equal(0x6a, bytes[20]); // 'j'
                Assert.Equal(0x70, bytes[21]); // 'p'
                Assert.Equal(0x32, bytes[22]); // '2'
                Assert.Equal(0x20, bytes[23]); // ' '

                // Verify MinV = 0
                Assert.Equal(0x00, bytes[24]);
                Assert.Equal(0x00, bytes[25]);
                Assert.Equal(0x00, bytes[26]);
                Assert.Equal(0x00, bytes[27]);

                // Verify compatibility list includes 'jp2 '
                Assert.Equal(0x6a, bytes[28]);
                Assert.Equal(0x70, bytes[29]);
                Assert.Equal(0x32, bytes[30]);
                Assert.Equal(0x20, bytes[31]);
            }
        }

        [Fact]
        public void TestJP2HeaderBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3: JP2 Header box
            // Type: 'jp2h' (0x6a703268)
            // Shall contain Image Header and Colour Specification boxes (required)
            // Image Header must be first sub-box

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Verify JP2 Header box structure
                Assert.True(reader.FileStructure.HasJP2HeaderBox);
                Assert.True(reader.FileStructure.HasImageHeaderBox);
                Assert.True(reader.FileStructure.HasColourSpecificationBox);

                // Image Header must be first in JP2 Header
                Assert.Equal(0, reader.FileStructure.ImageHeaderBoxOrder);
            }
        }

        [Fact]
        public void TestImageHeaderBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.1: Image Header box
            // Type: 'ihdr' (0x69686472)
            // Must be first box in JP2 Header
            // Contains: HEIGHT, WIDTH, NC, BPC, C, UnkC, IPR

            var codestream = CreateMinimalCodestream();
            var height = 1024;
            var width = 768;
            var nc = 3;

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, height, width, nc, new[] { 8, 8, 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Verify Image Header is first
                Assert.Equal(0, reader.FileStructure.ImageHeaderBoxOrder);

                // Verify no validation errors
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestBitsPerComponentBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.2: Bits Per Component box
            // Type: 'bpcc' (0x62706363)
            // Required when BPC field in Image Header is 0xFF
            // Contains array of BPC values for each component

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                // Different bit depths per component requires bpcc box
                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 10, 12 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Should have bpcc box
                Assert.True(reader.FileStructure.HasBitsPerComponentBox);

                // Should pass validation
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestColourSpecificationBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.3: Colour Specification box
            // Type: 'colr' (0x636f6c72)
            // METH: 1 (enumerated) or 2 (ICC profile)
            // For METH=1: EnumCS shall be valid value (16=sRGB, 17=Greyscale, etc.)

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 8, 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Must have colour specification box
                Assert.True(reader.FileStructure.HasColourSpecificationBox);
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestPaletteBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.4: Palette box
            // Type: 'pclr' (0x70636c72)
            // Format: NE(2) + NPC(1) + B[NPC] + C[NE][NPC]
            // Must come before Component Mapping box if both present

            var metadata = new J2KMetadata();
            var paletteData = new int[4][];
            paletteData[0] = new[] { 255, 0, 0 };
            paletteData[1] = new[] { 0, 255, 0 };
            paletteData[2] = new[] { 0, 0, 255 };
            paletteData[3] = new[] { 255, 255, 255 };

            metadata.SetPalette(4, 3, new short[] { 7, 7, 7 }, paletteData);
            metadata.AddComponentMapping(0, 1, 0);
            metadata.AddComponentMapping(0, 1, 1);
            metadata.AddComponentMapping(0, 1, 2);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Verify palette box ordering
                Assert.True(reader.FileStructure.HasPaletteBox);
                Assert.True(reader.FileStructure.HasComponentMappingBox);
                Assert.True(reader.FileStructure.PaletteBoxOrder < reader.FileStructure.ComponentMappingBoxOrder);
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestComponentMappingBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.5: Component Mapping box
            // Type: 'cmap' (0x636d6170)
            // Format: Array of {CMP(2) + MTYP(1) + PCOL(1)}
            // MTYP: 0=direct, 1=palette mapping

            var metadata = new J2KMetadata();
            
            // Direct mapping
            metadata.AddComponentMapping(0, 0, 0); // R
            metadata.AddComponentMapping(1, 0, 0); // G
            metadata.AddComponentMapping(2, 0, 0); // B

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 8, 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.True(reader.FileStructure.HasComponentMappingBox);
                Assert.Equal(3, reader.Metadata.ComponentMapping.NumChannels);
                Assert.False(reader.Metadata.ComponentMapping.UsesPalette);
            }
        }

        [Fact]
        public void TestChannelDefinitionBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.6: Channel Definition box
            // Type: 'cdef' (0x63646566)
            // Format: N(2) + Array of {Cn(2) + Typ(2) + Asoc(2)}
            // Typ: 0=color, 1=opacity, 2=premultiplied opacity, 65535=unspecified

            var metadata = new J2KMetadata();
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Color, 1);
            metadata.ChannelDefinitions.AddChannel(1, ChannelType.Color, 2);
            metadata.ChannelDefinitions.AddChannel(2, ChannelType.Color, 3);
            metadata.ChannelDefinitions.AddChannel(3, ChannelType.Opacity, 0);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 4, new[] { 8, 8, 8, 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.True(reader.FileStructure.HasChannelDefinitionBox);
                Assert.Equal(4, reader.Metadata.ChannelDefinitions.Channels.Count);
                Assert.True(reader.Metadata.ChannelDefinitions.HasAlphaChannel);
            }
        }

        [Fact]
        public void TestResolutionBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.7: Resolution box
            // Type: 'res ' (0x72657320) - superbox
            // Contains Capture Resolution (resc) and/or Display Resolution (resd)
            // Format: VR_N(2) + VR_D(2) + HR_N(2) + HR_D(2) + VR_E(1) + HR_E(1)

            var metadata = new J2KMetadata();
            metadata.Resolution = new ResolutionData();
            metadata.Resolution.SetCaptureResolution(2835.0, 2835.0); // ~72 DPI
            metadata.Resolution.SetDisplayResolution(3543.0, 3543.0); // ~90 DPI

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.True(reader.FileStructure.HasResolutionBox);
                Assert.NotNull(reader.Metadata.Resolution);
                Assert.True(reader.Metadata.Resolution.HasCaptureResolution);
                Assert.True(reader.Metadata.Resolution.HasDisplayResolution);
            }
        }

        [Fact]
        public void TestContiguousCodestreamBoxConformance()
        {
            // ISO/IEC 15444-1 Section I.5.4: Contiguous Codestream box
            // Type: 'jp2c' (0x6a703263)
            // Must be present (required)
            // Contains the JPEG 2000 codestream
            // Should be last box in file (best practice)

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.True(reader.FileStructure.HasContiguousCodestreamBox);
                Assert.True(reader.FirstCodeStreamPos > 0);
                Assert.Equal(codestream.Length, reader.FirstCodeStreamLength - 8); // -8 for box header
            }
        }

        #endregion

        #region Box Ordering Tests

        [Fact]
        public void TestBoxOrderingConformance()
        {
            // ISO/IEC 15444-1: Box ordering requirements
            // 1. Signature box (first)
            // 2. File Type box (second)
            // 3. JP2 Header box
            // 4. Contiguous Codestream box (typically last)

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                var boxOrder = reader.FileStructure.GetTopLevelBoxOrder();

                Assert.Equal("Signature", boxOrder[0]);
                Assert.Equal("FileType", boxOrder[1]);
                Assert.Equal("JP2Header", boxOrder[2]);
                Assert.Equal("Codestream", boxOrder[3]);
            }
        }

        [Fact]
        public void TestJP2HeaderSubBoxOrdering()
        {
            // ISO/IEC 15444-1 Section I.5.3: JP2 Header sub-box ordering
            // Image Header must be first
            // Palette must come before Component Mapping

            var metadata = new J2KMetadata();
            
            // Add palette and component mapping
            metadata.SetPalette(2, 3, new short[] { 7, 7, 7 }, 
                new int[][] { new int[] { 0, 0, 0 }, new int[] { 255, 255, 255 } });
            metadata.AddComponentMapping(0, 1, 0);
            metadata.AddComponentMapping(0, 1, 1);
            metadata.AddComponentMapping(0, 1, 2);

            // Add channel definitions
            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Color, 1);
            metadata.ChannelDefinitions.AddChannel(1, ChannelType.Color, 2);
            metadata.ChannelDefinitions.AddChannel(2, ChannelType.Color, 3);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Image Header must be first
                Assert.Equal(0, reader.FileStructure.ImageHeaderBoxOrder);

                // Palette must come before Component Mapping
                Assert.True(reader.FileStructure.PaletteBoxOrder < reader.FileStructure.ComponentMappingBoxOrder);

                // No validation errors
                Assert.False(reader.Validator.HasErrors);
            }
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void TestRoundTripConformance()
        {
            // Test that files written by CoreJ2K can be read back correctly
            // This verifies both writer and reader conformance

            var metadata = new J2KMetadata();
            
            // Add comprehensive metadata
            metadata.XmlBoxes.Add(new XmlBox { XmlContent = "<?xml version=\"1.0\"?><test/>" });
            metadata.UuidBoxes.Add(new UuidBox { Uuid = Guid.NewGuid(), Data = new byte[] { 1, 2, 3 } });
            
            var paletteData = new int[3][];
            paletteData[0] = new[] { 255, 0, 0 };
            paletteData[1] = new[] { 0, 255, 0 };
            paletteData[2] = new[] { 0, 0, 255 };
            metadata.SetPalette(3, 3, new short[] { 7, 7, 7 }, paletteData);
            metadata.AddComponentMapping(0, 1, 0);
            metadata.AddComponentMapping(0, 1, 1);
            metadata.AddComponentMapping(0, 1, 2);

            metadata.ChannelDefinitions = new ChannelDefinitionData();
            metadata.ChannelDefinitions.AddChannel(0, ChannelType.Color, 1);
            metadata.ChannelDefinitions.AddChannel(1, ChannelType.Color, 2);
            metadata.ChannelDefinitions.AddChannel(2, ChannelType.Color, 3);

            metadata.Resolution = new ResolutionData();
            metadata.Resolution.SetCaptureResolution(2835.0, 2835.0);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                // Write
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = true; // Use strict mode for conformance
                reader.readFileFormat();

                // Verify all metadata preserved
                Assert.Single(reader.Metadata.XmlBoxes);
                Assert.Single(reader.Metadata.UuidBoxes);
                Assert.NotNull(reader.Metadata.Palette);
                Assert.Equal(3, reader.Metadata.Palette.NumEntries);
                Assert.NotNull(reader.Metadata.ComponentMapping);
                Assert.Equal(3, reader.Metadata.ComponentMapping.NumChannels);
                Assert.NotNull(reader.Metadata.ChannelDefinitions);
                Assert.Equal(3, reader.Metadata.ChannelDefinitions.Channels.Count);
                Assert.NotNull(reader.Metadata.Resolution);

                // Verify no validation errors
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestMultipleRoundTripsConformance()
        {
            // Test that files can be read and written multiple times
            // Each iteration should produce identical results

            var codestream = CreateMinimalCodestream();
            byte[] firstWrite;
            byte[] secondWrite;

            // First write
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                firstWrite = ms.ToArray();
            }

            // Read and write again
            using (var ms1 = new MemoryStream(firstWrite))
            using (var ms2 = new MemoryStream())
            {
                var reader = new FileFormatReader(new ISRandomAccessIO(ms1));
                reader.readFileFormat();

                ms2.Write(codestream, 0, codestream.Length);
                ms2.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms2, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = reader.Metadata;
                writer.writeFileFormat();

                secondWrite = ms2.ToArray();
            }

            // The two files should be identical (or at least structurally equivalent)
            Assert.Equal(firstWrite.Length, secondWrite.Length);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void TestStrictValidationConformance()
        {
            // All conformant files should pass strict validation

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 8, 8 }, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = true;

                // Should not throw in strict mode if conformant
                reader.readFileFormat();

                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestInvalidFileDetection()
        {
            // Validation should detect non-conformant files

            // Create a file missing required JP2 Header
            using (var ms = new MemoryStream())
            {
                // Write only signature and file type
                ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A }, 0, 12);
                ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 
                                     0x6A, 0x70, 0x32, 0x20, 0x00, 0x00, 0x00, 0x00, 
                                     0x6A, 0x70, 0x32, 0x20 }, 0, 20);

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));

                Assert.Throws<InvalidOperationException>(() => reader.readFileFormat());
            }
        }

        #endregion

        #region Metadata Conformance Tests

        [Fact]
        public void TestICCProfileConformance()
        {
            // ISO/IEC 15444-1 Section I.5.3.3: ICC profile method
            // METH=2 for ICC profile
            // Profile should be valid ICC format

            var metadata = new J2KMetadata();
            var iccProfile = CreateMinimalICCProfile();
            metadata.SetIccProfile(iccProfile);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, new[] { 8, 8, 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.IccProfile);
                Assert.True(reader.Metadata.IccProfile.IsValid);
                Assert.Equal(iccProfile.Length, reader.Metadata.IccProfile.ProfileSize);
            }
        }

        [Fact]
        public void TestPaletteValueRangeConformance()
        {
            // Palette values should respect the bit depth specified
            // For 8-bit unsigned: 0-255
            // For 16-bit unsigned: 0-65535

            var metadata = new J2KMetadata();
            
            // Test with maximum values for 8-bit
            var paletteData = new int[2][];
            paletteData[0] = new[] { 0, 0, 0 };
            paletteData[1] = new[] { 255, 255, 255 };

            metadata.SetPalette(2, 3, new short[] { 7, 7, 7 }, paletteData);
            metadata.AddComponentMapping(0, 1, 0);
            metadata.AddComponentMapping(0, 1, 1);
            metadata.AddComponentMapping(0, 1, 2);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Verify values preserved
                Assert.Equal(0, reader.Metadata.Palette.GetEntry(0, 0));
                Assert.Equal(255, reader.Metadata.Palette.GetEntry(1, 0));
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a minimal valid JPEG 2000 codestream for testing.
        /// </summary>
        private byte[] CreateMinimalCodestream()
        {
            var soc = new byte[] { 0xFF, 0x4F };
            var siz = new byte[]
            {
                0xFF, 0x51, 0x00, 0x29, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x01, 0x07, 0x01, 0x01
            };
            var eoc = new byte[] { 0xFF, 0xD9 };

            var result = new byte[soc.Length + siz.Length + eoc.Length];
            soc.CopyTo(result, 0);
            siz.CopyTo(result, soc.Length);
            eoc.CopyTo(result, soc.Length + siz.Length);

            return result;
        }

        /// <summary>
        /// Creates a minimal ICC profile for testing.
        /// </summary>
        private byte[] CreateMinimalICCProfile()
        {
            var profile = new byte[128];
            profile[0] = 0x00;
            profile[1] = 0x00;
            profile[2] = 0x00;
            profile[3] = 0x80;
            profile[4] = (byte)'a';
            profile[5] = (byte)'c';
            profile[6] = (byte)'s';
            profile[7] = (byte)'p';
            profile[8] = 0x02;
            profile[9] = 0x00;
            profile[12] = (byte)'s';
            profile[13] = (byte)'c';
            profile[14] = (byte)'n';
            profile[15] = (byte)'r';
            profile[16] = (byte)'R';
            profile[17] = (byte)'G';
            profile[18] = (byte)'B';
            profile[19] = (byte)' ';
            profile[20] = (byte)'X';
            profile[21] = (byte)'Y';
            profile[22] = (byte)'Z';
            profile[23] = (byte)' ';
            return profile;
        }

        #endregion
    }
}
