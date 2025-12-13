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
    /// Tests for Bits Per Component (BPC) Box per ISO/IEC 15444-1 Section I.5.3.2.
    /// Ensures proper reading, writing, and validation of varying bit depths.
    /// </summary>
    public class BitsPerComponentTests
    {
        [Fact]
        public void TestBitsPerComponentDataCreation()
        {
            var bitDepths = new[] { 8, 10, 12 };
            var isSigned = new[] { false, false, false };

            var bpcData = BitsPerComponentData.FromBitDepths(bitDepths, isSigned);

            Assert.NotNull(bpcData);
            Assert.Equal(3, bpcData.NumComponents);
            Assert.Equal(8, bpcData.GetBitDepth(0));
            Assert.Equal(10, bpcData.GetBitDepth(1));
            Assert.Equal(12, bpcData.GetBitDepth(2));
            Assert.False(bpcData.IsSigned(0));
            Assert.False(bpcData.IsSigned(1));
            Assert.False(bpcData.IsSigned(2));
        }

        [Fact]
        public void TestBitsPerComponentWithSignedValues()
        {
            var bitDepths = new[] { 8, 16, 12 };
            var isSigned = new[] { true, false, true };

            var bpcData = BitsPerComponentData.FromBitDepths(bitDepths, isSigned);

            Assert.True(bpcData.IsSigned(0));
            Assert.False(bpcData.IsSigned(1));
            Assert.True(bpcData.IsSigned(2));
        }

        [Fact]
        public void TestBitsPerComponentUniformCheck()
        {
            // Uniform bit depths
            var uniformData = BitsPerComponentData.FromBitDepths(new[] { 8, 8, 8 }, new[] { false, false, false });
            Assert.True(uniformData.AreComponentsUniform());
            Assert.False(uniformData.IsBoxNeeded());

            // Varying bit depths
            var varyingData = BitsPerComponentData.FromBitDepths(new[] { 8, 10, 12 }, new[] { false, false, false });
            Assert.False(varyingData.AreComponentsUniform());
            Assert.True(varyingData.IsBoxNeeded());

            // Varying signedness
            var varyingSign = BitsPerComponentData.FromBitDepths(new[] { 8, 8, 8 }, new[] { true, false, false });
            Assert.False(varyingSign.AreComponentsUniform());
            Assert.True(varyingSign.IsBoxNeeded());
        }

        [Fact]
        public void TestBitsPerComponentSetBitDepth()
        {
            var bpcData = new BitsPerComponentData
            {
                ComponentBitDepths = new byte[3]
            };

            bpcData.SetBitDepth(0, 8, false);
            bpcData.SetBitDepth(1, 16, true);
            bpcData.SetBitDepth(2, 12, false);

            Assert.Equal(8, bpcData.GetBitDepth(0));
            Assert.False(bpcData.IsSigned(0));

            Assert.Equal(16, bpcData.GetBitDepth(1));
            Assert.True(bpcData.IsSigned(1));

            Assert.Equal(12, bpcData.GetBitDepth(2));
            Assert.False(bpcData.IsSigned(2));
        }

        [Fact]
        public void TestBitsPerComponentInvalidBitDepth()
        {
            var bpcData = new BitsPerComponentData
            {
                ComponentBitDepths = new byte[1]
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => bpcData.SetBitDepth(0, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => bpcData.SetBitDepth(0, 39, false));
        }

        [Fact]
        public void TestBitsPerComponentToString()
        {
            var bpcData = BitsPerComponentData.FromBitDepths(
                new[] { 8, 10, 12 },
                new[] { false, false, true });

            var str = bpcData.ToString();

            Assert.Contains("Bits Per Component Box", str);
            Assert.Contains("3 components", str);
            Assert.Contains("C0:8U", str);  // Component 0: 8-bit unsigned
            Assert.Contains("C1:10U", str); // Component 1: 10-bit unsigned
            Assert.Contains("C2:12S", str); // Component 2: 12-bit signed
        }

        [Fact]
        public void TestBPCBoxWriteAndRead()
        {
            // Create a JP2 file with varying bit depths
            var codestream = CreateMinimalCodestream();
            var bitDepths = new[] { 8, 10, 12 };
            var isSigned = new[] { false, false, false };

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                // Write JP2 file with BPC box
                var writer = new FileFormatWriter(ms, 16, 16, 3, bitDepths, codestream.Length);
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // Verify BPC box was read
                Assert.True(reader.FileStructure.HasBitsPerComponentBox);
                Assert.NotNull(reader.Metadata.BitsPerComponent);

                var bpcData = reader.Metadata.BitsPerComponent;
                Assert.Equal(3, bpcData.NumComponents);
                Assert.Equal(8, bpcData.GetBitDepth(0));
                Assert.Equal(10, bpcData.GetBitDepth(1));
                Assert.Equal(12, bpcData.GetBitDepth(2));
            }
        }

        [Fact]
        public void TestBPCBoxRequiredWhenVarying()
        {
            // Create file with varying bit depths
            var codestream = CreateMinimalCodestream();
            var bitDepths = new[] { 8, 10, 12 };

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, bitDepths, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // BPC box should be present and required
                Assert.True(reader.FileStructure.HasBitsPerComponentBox);
                Assert.Equal((byte)0xFF, reader.FileStructure.ImageHeaderBPCValue);

                // Validator should not report errors
                Assert.False(reader.Validator.HasErrors);
            }
        }

        [Fact]
        public void TestBPCBoxNotNeededWhenUniform()
        {
            // Create file with uniform bit depths
            var codestream = CreateMinimalCodestream();
            var bitDepths = new[] { 8, 8, 8 };

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 3, bitDepths, codestream.Length);
                writer.writeFileFormat();

                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // BPC box should NOT be present for uniform bit depths
                Assert.False(reader.FileStructure.HasBitsPerComponentBox);
                Assert.NotEqual((byte)0xFF, reader.FileStructure.ImageHeaderBPCValue);
            }
        }

        [Fact]
        public void TestBPCBoxWithMaxComponents()
        {
            // Test with many components
            var numComponents = 10;
            var bitDepths = new int[numComponents];
            var isSigned = new bool[numComponents];

            for (int i = 0; i < numComponents; i++)
            {
                bitDepths[i] = 8 + i; // Varying bit depths
                isSigned[i] = (i % 2 == 0); // Alternate signed/unsigned
            }

            var bpcData = BitsPerComponentData.FromBitDepths(bitDepths, isSigned);

            Assert.Equal(numComponents, bpcData.NumComponents);
            for (int i = 0; i < numComponents; i++)
            {
                Assert.Equal(8 + i, bpcData.GetBitDepth(i));
                Assert.Equal(i % 2 == 0, bpcData.IsSigned(i));
            }
        }

        [Fact]
        public void TestBPCBoxValidation()
        {
            var structure = new JP2Structure
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
                JP2HeaderBoxLength = 80,
                JP2HeaderBoxCount = 1,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                ImageHeaderBPCValue = 0xFF, // Requires BPC box
                HasColourSpecificationBox = true,
                ColourSpecificationBoxOrder = 1,
                HasBitsPerComponentBox = true, // BPC box present
                BitsPerComponentBoxOrder = 2,
                HasContiguousCodestreamBox = true,
                ContiguousCodestreamBoxPosition = 112
            };

            var validator = new JP2Validator();
            validator.ValidateFileFormat(structure);

            // Should not have errors
            Assert.False(validator.HasErrors);
        }

        [Fact]
        public void TestBPCBoxMissingError()
        {
            var structure = new JP2Structure
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
                JP2HeaderBoxLength = 80,
                JP2HeaderBoxCount = 1,
                HasImageHeaderBox = true,
                ImageHeaderBoxOrder = 0,
                ImageHeaderBPCValue = 0xFF, // Requires BPC box
                HasColourSpecificationBox = true,
                ColourSpecificationBoxOrder = 1,
                HasBitsPerComponentBox = false, // BPC box MISSING!
                HasContiguousCodestreamBox = true,
                ContiguousCodestreamBoxPosition = 112
            };

            var validator = new JP2Validator();
            validator.ValidateFileFormat(structure);

            // Should have error about missing BPC box
            Assert.True(validator.HasErrors);
            Assert.Contains("Bits Per Component", validator.GetValidationReport());
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
            Array.Copy(soc, 0, result, 0, soc.Length);
            Array.Copy(siz, 0, result, soc.Length, siz.Length);
            Array.Copy(eoc, 0, result, soc.Length + siz.Length, eoc.Length);

            return result;
        }
    }
}
