// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.io;
using CoreJ2K.j2k.util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for Palette (pclr) and Component Mapping (cmap) box support in JPEG 2000.
    /// These boxes are required for palettized/indexed color images per ISO/IEC 15444-1.
    /// </summary>
    public class PaletteTests
    {
        [Fact]
        public void TestPaletteBoxWriteAndRead()
        {
            // Arrange: Create a simple RGB palette with 4 entries
            var metadata = new J2KMetadata();
            
            // Create a 4-color palette: Red, Green, Blue, White
            var numEntries = 4;
            var numColumns = 3; // RGB
            var bitDepths = new short[] { 7, 7, 7 }; // 8-bit unsigned (depth-1, no sign bit)
            var entries = new int[][]
            {
                new int[] { 255, 0, 0 },     // Red
                new int[] { 0, 255, 0 },     // Green
                new int[] { 0, 0, 255 },     // Blue
                new int[] { 255, 255, 255 }  // White
            };

            metadata.SetPalette(numEntries, numColumns, bitDepths, entries);

            // Create component mapping: map single indexed component to 3 RGB channels via palette
            metadata.AddComponentMapping(0, 1, 0); // R: component 0, palette mapping, column 0
            metadata.AddComponentMapping(0, 1, 1); // G: component 0, palette mapping, column 1
            metadata.AddComponentMapping(0, 1, 2); // B: component 0, palette mapping, column 2

            // Create a simple codestream (just a minimal header)
            var codestream = CreateMinimalCodestream();

            // Act: Write JP2 file with palette
            using (var ms = new MemoryStream())
            {
                // First write the codestream to the stream
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read it back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Assert: Verify palette was read correctly
                Assert.NotNull(reader.Metadata.Palette);
                Assert.Equal(numEntries, reader.Metadata.Palette.NumEntries);
                Assert.Equal(numColumns, reader.Metadata.Palette.NumColumns);

                // Verify palette entries
                for (int i = 0; i < numEntries; i++)
                {
                    for (int j = 0; j < numColumns; j++)
                    {
                        Assert.Equal(entries[i][j], reader.Metadata.Palette.GetEntry(i, j));
                    }
                }

                // Verify component mapping
                Assert.NotNull(reader.Metadata.ComponentMapping);
                Assert.Equal(3, reader.Metadata.ComponentMapping.NumChannels);
                Assert.True(reader.Metadata.ComponentMapping.UsesPalette);

                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(0, reader.Metadata.ComponentMapping.GetComponentIndex(i));
                    Assert.Equal(1, reader.Metadata.ComponentMapping.GetMappingType(i));
                    Assert.Equal(i, reader.Metadata.ComponentMapping.GetPaletteColumn(i));
                }
            }
        }

        [Fact]
        public void TestPaletteBoxWithSignedValues()
        {
            // Arrange: Create a palette with signed values
            var metadata = new J2KMetadata();
            
            var numEntries = 2;
            var numColumns = 1; // Grayscale
            var bitDepths = new short[] { 0x87 }; // 8-bit signed (bit 7 set)
            var entries = new int[][]
            {
                new int[] { -128 },
                new int[] { 127 }
            };

            metadata.SetPalette(numEntries, numColumns, bitDepths, entries);

            var codestream = CreateMinimalCodestream();

            // Act: Write and read
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 2, 2, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Assert: Verify signed values
                Assert.NotNull(reader.Metadata.Palette);
                Assert.True(reader.Metadata.Palette.IsSigned(0));
                Assert.Equal(-128, reader.Metadata.Palette.GetEntry(0, 0));
                Assert.Equal(127, reader.Metadata.Palette.GetEntry(1, 0));
            }
        }

        [Fact]
        public void TestComponentMappingDirectMode()
        {
            // Arrange: Create component mapping with direct mapping (no palette)
            var metadata = new J2KMetadata();
            
            // Direct mapping: component 0->R, component 1->G, component 2->B
            metadata.AddComponentMapping(0, 0, 0); // Component 0, direct mapping
            metadata.AddComponentMapping(1, 0, 0); // Component 1, direct mapping
            metadata.AddComponentMapping(2, 0, 0); // Component 2, direct mapping

            var codestream = CreateMinimalCodestream();

            // Act: Write and read
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

                // Assert: Verify direct mapping
                Assert.NotNull(reader.Metadata.ComponentMapping);
                Assert.Equal(3, reader.Metadata.ComponentMapping.NumChannels);
                Assert.False(reader.Metadata.ComponentMapping.UsesPalette);

                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(i, reader.Metadata.ComponentMapping.GetComponentIndex(i));
                    Assert.Equal(0, reader.Metadata.ComponentMapping.GetMappingType(i)); // Direct
                }
            }
        }

        [Fact]
        public void TestPaletteBox16Bit()
        {
            // Arrange: Create a palette with 16-bit entries
            var metadata = new J2KMetadata();
            
            var numEntries = 3;
            var numColumns = 3; // RGB
            var bitDepths = new short[] { 15, 15, 15 }; // 16-bit unsigned (depth-1=15)
            var entries = new int[][]
            {
                new int[] { 65535, 0, 0 },        // Full red
                new int[] { 0, 65535, 0 },        // Full green
                new int[] { 32768, 32768, 32768 } // Mid gray
            };

            metadata.SetPalette(numEntries, numColumns, bitDepths, entries);

            var codestream = CreateMinimalCodestream();

            // Act: Write and read
            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 8, 8, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Assert: Verify 16-bit palette
                Assert.NotNull(reader.Metadata.Palette);
                Assert.Equal(16, reader.Metadata.Palette.GetBitDepth(0));
                Assert.Equal(65535, reader.Metadata.Palette.GetEntry(0, 0));
                Assert.Equal(65535, reader.Metadata.Palette.GetEntry(1, 1));
                Assert.Equal(32768, reader.Metadata.Palette.GetEntry(2, 0));
            }
        }

        [Fact]
        public void TestPaletteBoxOrdering()
        {
            // This test verifies that palette box comes before component mapping box
            // per ISO/IEC 15444-1 specification
            var metadata = new J2KMetadata();
            
            metadata.SetPalette(2, 1, new short[] { 7 }, new int[][] { new int[] { 0 }, new int[] { 255 } });
            metadata.AddComponentMapping(0, 1, 0);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 4, 4, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read raw bytes to verify order
                ms.Seek(0, SeekOrigin.Begin);
                var bytes = ms.ToArray();
                
                // Find palette box (0x70636c72)
                int palettePos = FindBoxPosition(bytes, 0x70636c72);
                
                // Find component mapping box (0x636d6170)
                int cmapPos = FindBoxPosition(bytes, 0x636d6170);
                
                // Assert: Palette must come before component mapping
                Assert.True(palettePos > 0, "Palette box not found");
                Assert.True(cmapPos > 0, "Component mapping box not found");
                Assert.True(palettePos < cmapPos, "Palette box must come before component mapping box");
            }
        }

        /// <summary>
        /// Finds the position of a box type in the byte array.
        /// </summary>
        private int FindBoxPosition(byte[] data, int boxType)
        {
            var typeBytes = new byte[]
            {
                (byte)((boxType >> 24) & 0xFF),
                (byte)((boxType >> 16) & 0xFF),
                (byte)((boxType >> 8) & 0xFF),
                (byte)(boxType & 0xFF)
            };

            for (int i = 0; i < data.Length - 4; i++)
            {
                if (data[i] == typeBytes[0] && data[i + 1] == typeBytes[1] &&
                    data[i + 2] == typeBytes[2] && data[i + 3] == typeBytes[3])
                {
                    return i;
                }
            }

            return -1;
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
    }
}
