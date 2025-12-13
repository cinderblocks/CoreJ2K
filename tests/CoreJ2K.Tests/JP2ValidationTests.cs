// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for comprehensive JP2 file format validation per ISO/IEC 15444-1.
    /// Tests validation of box ordering, required boxes, and proper structure.
    /// </summary>
    public class JP2ValidationTests
    {
        [Fact]
        public void TestValidJP2FilePassesValidation()
        {
            // Arrange: Create a valid JP2 file
            var metadata = new J2KMetadata();
            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Act: Read and validate
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = false; // Don't throw on warnings
                reader.readFileFormat();

                // Assert: Should have no errors
                Assert.False(reader.Validator.HasErrors, 
                    $"Valid JP2 file should not have errors:\n{reader.Validator.GetValidationReport()}");
                Assert.NotNull(reader.FileStructure);
                Assert.True(reader.FileStructure.HasSignatureBox);
                Assert.True(reader.FileStructure.HasFileTypeBox);
                Assert.True(reader.FileStructure.HasJP2HeaderBox);
                Assert.True(reader.FileStructure.HasContiguousCodestreamBox);
            }
        }

        [Fact]
        public void TestMissingSignatureBoxDetected()
        {
            // Arrange: Create a validator and structure without signature box
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = false,
                HasFileTypeBox = true,
                HasJP2HeaderBox = true,
                HasImageHeaderBox = true,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains("JP2 Signature Box is missing", validator.Errors[0]);
        }

        [Fact]
        public void TestMissingFileTypeBoxDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = false,
                HasJP2HeaderBox = true,
                HasImageHeaderBox = true,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("File Type Box is missing"));
        }

        [Fact]
        public void TestMissingJP2HeaderBoxDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = false,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("JP2 Header Box is missing"));
        }

        [Fact]
        public void TestMissingImageHeaderBoxDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                HasImageHeaderBox = false, // Missing!
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Image Header Box is missing"));
        }

        [Fact]
        public void TestMissingColourSpecificationBoxDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                HasColourSpecificationBox = false, // Missing!
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Colour Specification Box is missing"));
        }

        [Fact]
        public void TestImageHeaderBoxNotFirstDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 1, // Not first!
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Image Header Box must be first"));
        }

        [Fact]
        public void TestPaletteBoxMustComeBeforeComponentMapping()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                HasColourSpecificationBox = true,
                ColourSpecificationBoxOrder = 1,
                HasPaletteBox = true,
                PaletteBoxOrder = 3,
                HasComponentMappingBox = true,
                ComponentMappingBoxOrder = 2, // Before palette!
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Palette Box must appear before Component Mapping Box"));
        }

        [Fact]
        public void TestBitsPerComponentBoxRequiredWhenBPCIs0xFF()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                ImageHeaderBPCValue = 0xFF, // Indicates BPC box required
                HasColourSpecificationBox = true,
                HasBitsPerComponentBox = false, // Missing!
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Bits Per Component Box is required"));
        }

        [Fact]
        public void TestMultipleJP2HeaderBoxesDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                JP2HeaderBoxCount = 2, // Multiple!
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Multiple JP2 Header Boxes found"));
        }

        [Fact]
        public void TestInvalidBrandDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                FileTypeBoxLength = 20,
                HasValidBrand = false, // Invalid brand!
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                HasImageHeaderBox = true,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("must have 'jp2 '"));
        }

        [Fact]
        public void TestMissingJP2CompatibilityDetected()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                FileTypeBoxLength = 20,
                HasValidBrand = true,
                HasJP2Compatibility = false, // Missing!
                HasJP2HeaderBox = true,
                HasImageHeaderBox = true,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("compatibility list must include"));
        }

        [Fact]
        public void TestValidationReportFormat()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = false,
                HasFileTypeBox = true,
                HasJP2HeaderBox = true,
                HasImageHeaderBox = true,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);
            var report = validator.GetValidationReport();

            // Assert
            Assert.Contains("=== JP2 File Format Validation Report ===", report);
            Assert.Contains("ERRORS", report);
            Assert.Contains("?", report);
        }

        [Fact]
        public void TestStrictValidationThrowsOnErrors()
        {
            // Arrange: Create an invalid JP2 file (missing codestream)
            using (var ms = new MemoryStream())
            {
                // Write only signature and file type boxes
                ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A }, 0, 12);
                ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x6A, 0x70, 0x32, 0x20, 0x00, 0x00, 0x00, 0x00, 0x6A, 0x70, 0x32, 0x20 }, 0, 20);

                ms.Seek(0, SeekOrigin.Begin);

                // Act & Assert: Should throw in strict mode
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = true;

                Assert.Throws<InvalidOperationException>(() => reader.readFileFormat());
            }
        }

        [Fact]
        public void TestComponentMappingWithoutPaletteWarning()
        {
            // Arrange
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                HasColourSpecificationBox = true,
                HasPaletteBox = false, // No palette
                HasComponentMappingBox = true, // But has component mapping
                HasContiguousCodestreamBox = true
            };

            // Act
            validator.ValidateFileFormat(structure);

            // Assert
            Assert.True(validator.HasWarnings);
            Assert.Contains(validator.Warnings, w => w.Contains("Component Mapping Box present but no Palette Box found"));
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
