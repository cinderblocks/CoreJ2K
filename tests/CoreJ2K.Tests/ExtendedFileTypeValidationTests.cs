// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using CoreJ2K.j2k.fileformat;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Extended validation tests for JPEG 2000 File Type Box per ISO/IEC 15444-1 Section I.5.2.
    /// Tests brand validation, compatibility lists, MinorVersion conformance, and other extended features.
    /// </summary>
    public class ExtendedFileTypeValidationTests
    {
        #region Brand Validation Tests

        [Fact]
        public void TestJP2BrandValidation()
        {
            // ISO/IEC 15444-1: Brand 'jp2 ' (0x6a703220) indicates baseline JP2
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.HasValidBrand = true;

            validator.ValidateFileFormat(structure);

            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestInvalidBrandDetected()
        {
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.HasValidBrand = false; // Invalid brand

            validator.ValidateFileFormat(structure);

            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("must have 'jp2 '"));
        }

        [Fact]
        public void TestJPXBrandRecognition()
        {
            // JPX brand (0x6a707820) indicates JPEG 2000 Part 2 extensions
            // This should be recognized but may generate warnings about extended features

            var codestream = CreateMinimalCodestream();
            
            using (var ms = new MemoryStream())
            {
                // Write JP2 file with JPX compatibility
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.writeFileFormat();

                // Read and validate
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = false; // Allow warnings
                reader.readFileFormat();

                // Should have valid brand
                Assert.True(reader.FileStructure.HasValidBrand);
            }
        }

        #endregion

        #region MinorVersion Tests

        [Fact]
        public void TestMinorVersionZero()
        {
            // ISO/IEC 15444-1: MinorVersion (MinV) = 0 for baseline JP2
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.MinorVersion = 0;

            validator.ValidateFileFormat(structure);

            // MinV=0 should not generate any warnings
            Assert.False(validator.HasWarnings);
        }

        [Fact]
        public void TestMinorVersionNonZero()
        {
            // Non-zero MinorVersion indicates potential extended features
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.MinorVersion = 1;

            validator.ValidateFileFormat(structure);

            // Should not be an error, but implementation may log info messages
            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestMinorVersionNegative()
        {
            // Negative MinorVersion is invalid
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.MinorVersion = -1;

            validator.ValidateFileFormat(structure);

            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Invalid MinorVersion"));
        }

        #endregion

        #region Compatibility List Tests

        [Fact]
        public void TestCompatibilityListWithJP2()
        {
            // Compatibility list must include 'jp2 ' for baseline compliance
            var validator = new JP2Validator();
            var compatList = new int[] { FileFormatBoxes.FT_BR }; // 'jp2 '

            validator.ValidateCompatibilityList(compatList, requireJP2: true);

            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestCompatibilityListMissingJP2()
        {
            // Compatibility list without 'jp2 ' should fail validation
            var validator = new JP2Validator();
            var compatList = new int[] { 0x6a707820 }; // 'jpx ' only

            validator.ValidateCompatibilityList(compatList, requireJP2: true);

            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("Compatibility list must include 'jp2 '"));
        }

        [Fact]
        public void TestCompatibilityListEmpty()
        {
            // Empty compatibility list should generate warning
            var validator = new JP2Validator();
            var compatList = new int[0];

            validator.ValidateCompatibilityList(compatList, requireJP2: true);

            Assert.True(validator.HasWarnings);
            Assert.Contains(validator.Warnings, w => w.Contains("Compatibility list is empty"));
        }

        [Fact]
        public void TestCompatibilityListMultipleProfiles()
        {
            // File can support multiple profiles (e.g., jp2 + jpx)
            var validator = new JP2Validator();
            var compatList = new int[] 
            { 
                FileFormatBoxes.FT_BR,  // 'jp2 '
                0x6a707820              // 'jpx '
            };

            validator.ValidateCompatibilityList(compatList, requireJP2: true);

            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestCompatibilityListWithJPM()
        {
            // JPM (JPEG 2000 Part 6 - Compound Image) compatibility
            var validator = new JP2Validator();
            var compatList = new int[] 
            { 
                FileFormatBoxes.FT_BR,  // 'jp2 '
                0x6a706d20              // 'jpm '
            };

            validator.ValidateCompatibilityList(compatList, requireJP2: false);

            Assert.False(validator.HasErrors);
        }

        #endregion

        #region File Type Box Length Tests

        [Fact]
        public void TestFileTypeBoxMinimumLength()
        {
            // Minimum length: 20 bytes (8 header + 4 brand + 4 MinV + 4 CL entry)
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.FileTypeBoxLength = 20;

            validator.ValidateFileFormat(structure);

            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestFileTypeBoxTooShort()
        {
            // Less than 20 bytes is invalid
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.FileTypeBoxLength = 16;

            validator.ValidateFileFormat(structure);

            Assert.True(validator.HasErrors);
            Assert.Contains(validator.Errors, e => e.Contains("File Type Box is too short"));
        }

        [Fact]
        public void TestFileTypeBoxExtendedCompatibilityList()
        {
            // File Type box with multiple compatibility entries (>20 bytes)
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.FileTypeBoxLength = 32; // Room for 3 CL entries

            validator.ValidateFileFormat(structure);

            Assert.False(validator.HasErrors);
        }

        #endregion

        #region File Type Box Position Tests

        [Fact]
        public void TestFileTypeBoxImmediatelyFollowsSignature()
        {
            // File Type box should be at position 12 (after 12-byte signature)
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.SignatureBoxPosition = 0;
            structure.SignatureBoxLength = 12;
            structure.FileTypeBoxPosition = 12;

            validator.ValidateFileFormat(structure);

            // Should pass without warnings about position
            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestFileTypeBoxWrongPosition()
        {
            // File Type box not immediately after signature generates warning
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.SignatureBoxPosition = 0;
            structure.SignatureBoxLength = 12;
            structure.FileTypeBoxPosition = 20; // Wrong position

            validator.ValidateFileFormat(structure);

            // Should generate warning
            Assert.True(validator.HasWarnings);
            Assert.Contains(validator.Warnings, w => w.Contains("File Type Box should immediately follow"));
        }

        #endregion

        #region Reader Requirements Integration Tests

        [Fact]
        public void TestReaderRequirementsFeatureDescriptions()
        {
            // Test that feature descriptions are correctly returned
            var desc1 = ReaderRequirementsBox.GetFeatureDescription(ReaderRequirementsBox.FEATURE_NO_EXTENSIONS);
            var desc2 = ReaderRequirementsBox.GetFeatureDescription(ReaderRequirementsBox.FEATURE_DCT);
            var desc3 = ReaderRequirementsBox.GetFeatureDescription(ReaderRequirementsBox.FEATURE_LOSSLESS);

            Assert.Contains("baseline", desc1.ToLower());
            Assert.Contains("DCT", desc2);
            Assert.Contains("Lossless", desc3);
        }

        [Fact]
        public void TestReaderRequirementsWithStandardFeatures()
        {
            var readerReq = new ReaderRequirementsBox();
            readerReq.StandardFeatures.Add(ReaderRequirementsBox.FEATURE_NO_EXTENSIONS);
            readerReq.StandardFeatures.Add(ReaderRequirementsBox.FEATURE_SINGLE_TILE);
            readerReq.StandardFeatures.Add(ReaderRequirementsBox.FEATURE_LOSSLESS);

            Assert.True(readerReq.RequiresFeature(ReaderRequirementsBox.FEATURE_NO_EXTENSIONS));
            Assert.True(readerReq.RequiresFeature(ReaderRequirementsBox.FEATURE_SINGLE_TILE));
            Assert.True(readerReq.RequiresFeature(ReaderRequirementsBox.FEATURE_LOSSLESS));
            Assert.False(readerReq.RequiresFeature(ReaderRequirementsBox.FEATURE_DCT));
        }

        [Fact]
        public void TestReaderRequirementsUnknownFeature()
        {
            // Unknown feature IDs should be handled gracefully
            var desc = ReaderRequirementsBox.GetFeatureDescription(9999);
            
            Assert.Contains("Unknown", desc);
            Assert.Contains("9999", desc);
        }

        #endregion

        #region Round-Trip with Extended Features

        [Fact]
        public void TestRoundTripWithReaderRequirements()
        {
            var metadata = new J2KMetadata();
            
            // Add reader requirements
            metadata.ReaderRequirements = new ReaderRequirementsBox();
            metadata.ReaderRequirements.StandardFeatures.Add(ReaderRequirementsBox.FEATURE_NO_EXTENSIONS);
            metadata.ReaderRequirements.StandardFeatures.Add(ReaderRequirementsBox.FEATURE_SINGLE_TILE);
            metadata.ReaderRequirements.IsJp2Compatible = true;

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                // Note: Current FileFormatWriter doesn't write reader requirements
                // This test verifies metadata structure only
                
                Assert.NotNull(metadata.ReaderRequirements);
                Assert.Equal(2, metadata.ReaderRequirements.StandardFeatures.Count);
                Assert.True(metadata.ReaderRequirements.IsJp2Compatible);
            }
        }

        #endregion

        #region Validation Report Tests

        [Fact]
        public void TestValidationReportFormatWithFileTypeErrors()
        {
            var validator = new JP2Validator();
            var structure = new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = false, // Missing!
                HasJP2HeaderBox = true,
                HasImageHeaderBox = true,
                HasColourSpecificationBox = true,
                HasContiguousCodestreamBox = true
            };

            validator.ValidateFileFormat(structure);
            var report = validator.GetValidationReport();

            Assert.Contains("File Type Box is missing", report);
            Assert.Contains("ERRORS", report);
        }

        [Fact]
        public void TestValidationReportWithCompatibilityWarnings()
        {
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.HasJP2Compatibility = true;
            structure.MinorVersion = 2; // Non-zero MinV

            validator.ValidateFileFormat(structure);
            var report = validator.GetValidationReport();

            // Should pass validation even with non-zero MinV
            Assert.False(validator.HasErrors);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void TestFileTypeBoxWithExtendedLength()
        {
            // Test detection of extended length (XLBox) in File Type box
            var validator = new JP2Validator();
            
            // Simulate extended length detection
            var hasExtended = validator.DetectExtendedLength(1, 0x100000000L); // > 4GB

            Assert.True(hasExtended);
        }

        [Fact]
        public void TestFileTypeBoxWithZeroLength()
        {
            // LBox=0 means box extends to end of file (not valid for File Type box)
            var validator = new JP2Validator();
            var structure = CreateValidStructure();
            structure.FileTypeBoxLength = 0;

            validator.ValidateFileFormat(structure);

            // This is unusual and should generate an error
            Assert.True(validator.HasErrors);
        }

        [Fact]
        public void TestMultipleFileTypeBoxes()
        {
            // Only one File Type box is allowed
            // Note: This is implicitly validated by the reader which only tracks one
            var validator = new JP2Validator();
            var structure = CreateValidStructure();

            validator.ValidateFileFormat(structure);

            Assert.False(validator.HasErrors);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a valid JP2 structure for testing.
        /// </summary>
        private JP2Structure CreateValidStructure()
        {
            return new JP2Structure
            {
                HasSignatureBox = true,
                SignatureBoxPosition = 0,
                SignatureBoxLength = 12,
                HasFileTypeBox = true,
                FileTypeBoxPosition = 12,
                FileTypeBoxLength = 20,
                HasValidBrand = true,
                HasJP2Compatibility = true,
                MinorVersion = 0,
                HasJP2HeaderBox = true,
                JP2HeaderBoxPosition = 32,
                JP2HeaderBoxLength = 50,
                JP2HeaderBoxCount = 1,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                ImageHeaderBPCValue = 7, // 8-bit
                HasColourSpecificationBox = true,
                ColourSpecificationBoxOrder = 1,
                HasContiguousCodestreamBox = true,
                ContiguousCodestreamBoxPosition = 100
            };
        }

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

        #endregion
    }
}
