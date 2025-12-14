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
    /// Comprehensive tests for UUID Info Box and URL Box per ISO/IEC 15444-1 Section I.7.1.
    /// Tests reading, writing, and round-trip functionality.
    /// </summary>
    public class UuidInfoUrlBoxTests
    {
        #region UUID Info Box Writing Tests

        [Fact]
        public void WriteUuidInfoBox_WithSingleUuid_WritesCorrectly()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();
            
            metadata.SetUuidInfo(new System.Collections.Generic.List<Guid> { uuid1 });

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Single(reader.Metadata.UuidInfo.UuidList);
                Assert.Equal(uuid1, reader.Metadata.UuidInfo.UuidList[0]);
            }
        }

        [Fact]
        public void WriteUuidInfoBox_WithMultipleUuids_WritesCorrectly()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();
            var uuid2 = Guid.NewGuid();
            var uuid3 = Guid.NewGuid();
            
            metadata.SetUuidInfo(new System.Collections.Generic.List<Guid> { uuid1, uuid2, uuid3 });

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Equal(3, reader.Metadata.UuidInfo.UuidList.Count);
                Assert.Contains(uuid1, reader.Metadata.UuidInfo.UuidList);
                Assert.Contains(uuid2, reader.Metadata.UuidInfo.UuidList);
                Assert.Contains(uuid3, reader.Metadata.UuidInfo.UuidList);
            }
        }

        [Fact]
        public void WriteUuidInfoBox_WithUrl_WritesCorrectly()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();
            var testUrl = "https://example.com/uuid-info";
            
            metadata.SetUuidInfo(
                new System.Collections.Generic.List<Guid> { uuid1 },
                url: testUrl,
                urlVersion: 1,
                urlFlags: 1); // Absolute URL

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Equal(testUrl, reader.Metadata.UuidInfo.Url);
                Assert.Equal(1, reader.Metadata.UuidInfo.UrlVersion);
                Assert.Equal(1, reader.Metadata.UuidInfo.UrlFlags);
            }
        }

        [Fact]
        public void WriteUuidInfoBox_WithoutUrl_WritesCorrectly()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();
            
            metadata.SetUuidInfo(new System.Collections.Generic.List<Guid> { uuid1 });

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Single(reader.Metadata.UuidInfo.UuidList);
                Assert.Null(reader.Metadata.UuidInfo.Url);
            }
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void AddUuidToInfo_CreatesUuidInfoIfNull()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();

            Assert.Null(metadata.UuidInfo);

            metadata.AddUuidToInfo(uuid1);

            Assert.NotNull(metadata.UuidInfo);
            Assert.Single(metadata.UuidInfo.UuidList);
            Assert.Equal(uuid1, metadata.UuidInfo.UuidList[0]);
        }

        [Fact]
        public void AddUuidToInfo_AddsDuplicateUuids()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();

            metadata.AddUuidToInfo(uuid1);
            metadata.AddUuidToInfo(uuid1); // Add same UUID again

            // Should not add duplicate
            Assert.Single(metadata.UuidInfo.UuidList);
        }

        [Fact]
        public void SetUuidInfoUrl_SetsAbsoluteUrl()
        {
            var metadata = new J2KMetadata();
            var url = "https://example.com/info";

            metadata.SetUuidInfoUrl(url, isAbsolute: true, version: 1);

            Assert.NotNull(metadata.UuidInfo);
            Assert.Equal(url, metadata.UuidInfo.Url);
            Assert.Equal(1, metadata.UuidInfo.UrlVersion);
            Assert.Equal(1, metadata.UuidInfo.UrlFlags); // Absolute
        }

        [Fact]
        public void SetUuidInfoUrl_SetsRelativeUrl()
        {
            var metadata = new J2KMetadata();
            var url = "/relative/path/info";

            metadata.SetUuidInfoUrl(url, isAbsolute: false, version: 0);

            Assert.NotNull(metadata.UuidInfo);
            Assert.Equal(url, metadata.UuidInfo.Url);
            Assert.Equal(0, metadata.UuidInfo.UrlVersion);
            Assert.Equal(0, metadata.UuidInfo.UrlFlags); // Relative
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void RoundTrip_UuidInfoWithMultipleUuidsAndUrl_PreservesAllData()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();
            var uuid2 = Guid.NewGuid();
            var uuid3 = Guid.NewGuid();
            var testUrl = "https://example.com/uuid-documentation";

            metadata.SetUuidInfo(
                new System.Collections.Generic.List<Guid> { uuid1, uuid2, uuid3 },
                url: testUrl,
                urlVersion: 2,
                urlFlags: 1);

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
                reader.readFileFormat();

                // Verify all data preserved
                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Equal(3, reader.Metadata.UuidInfo.UuidList.Count);
                Assert.Contains(uuid1, reader.Metadata.UuidInfo.UuidList);
                Assert.Contains(uuid2, reader.Metadata.UuidInfo.UuidList);
                Assert.Contains(uuid3, reader.Metadata.UuidInfo.UuidList);
                Assert.Equal(testUrl, reader.Metadata.UuidInfo.Url);
                Assert.Equal(2, reader.Metadata.UuidInfo.UrlVersion);
                Assert.Equal(1, reader.Metadata.UuidInfo.UrlFlags);
            }
        }

        [Fact]
        public void RoundTrip_MultipleMetadataTypes_AllPreserved()
        {
            var metadata = new J2KMetadata();
            
            // Add UUID Info
            var uuid1 = Guid.NewGuid();
            metadata.SetUuidInfo(
                new System.Collections.Generic.List<Guid> { uuid1 },
                url: "https://example.com");

            // Add UUID boxes
            metadata.AddUuid(Guid.NewGuid(), new byte[] { 1, 2, 3, 4 });

            // Add XML
            metadata.AddXml("<?xml version=\"1.0\"?><test/>");

            // Add Label
            metadata.AddLabel("Test Image");

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
                reader.readFileFormat();

                // Verify all metadata types preserved
                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Single(reader.Metadata.UuidInfo.UuidList);
                Assert.Single(reader.Metadata.UuidBoxes);
                Assert.Single(reader.Metadata.XmlBoxes);
                Assert.Single(reader.Metadata.Labels);
            }
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void WriteUuidInfoBox_EmptyUuidList_DoesNotWrite()
        {
            var metadata = new J2KMetadata();
            metadata.UuidInfo = new UuidInfoBox(); // Empty list

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                var bytesWritten = writer.writeFileFormat();

                // UUID Info box should not be written if UUID list is empty
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                // UuidInfo should be null since nothing was written
                Assert.Null(reader.Metadata.UuidInfo);
            }
        }

        [Fact]
        public void WriteUuidInfoBox_ManyUuids_HandlesCorrectly()
        {
            var metadata = new J2KMetadata();
            var uuids = new System.Collections.Generic.List<Guid>();

            // Add 50 UUIDs
            for (int i = 0; i < 50; i++)
            {
                uuids.Add(Guid.NewGuid());
            }

            metadata.SetUuidInfo(uuids, url: "https://example.com/many-uuids");

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Equal(50, reader.Metadata.UuidInfo.UuidList.Count);
                
                // Verify all UUIDs are preserved
                foreach (var uuid in uuids)
                {
                    Assert.Contains(uuid, reader.Metadata.UuidInfo.UuidList);
                }
            }
        }

        [Fact]
        public void WriteUuidInfoBox_LongUrl_HandlesCorrectly()
        {
            var metadata = new J2KMetadata();
            var longUrl = "https://example.com/" + new string('a', 500) + "/uuid-info";
            var uuid1 = Guid.NewGuid();

            metadata.SetUuidInfo(
                new System.Collections.Generic.List<Guid> { uuid1 },
                url: longUrl);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Equal(longUrl, reader.Metadata.UuidInfo.Url);
            }
        }

        [Fact]
        public void WriteUuidInfoBox_SpecialCharactersInUrl_HandlesCorrectly()
        {
            var metadata = new J2KMetadata();
            var specialUrl = "https://example.com/path?param=value&other=123#fragment";
            var uuid1 = Guid.NewGuid();

            metadata.SetUuidInfo(
                new System.Collections.Generic.List<Guid> { uuid1 },
                url: specialUrl);

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read back
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.readFileFormat();

                Assert.NotNull(reader.Metadata.UuidInfo);
                Assert.Equal(specialUrl, reader.Metadata.UuidInfo.Url);
            }
        }

        #endregion

        #region URL Box Tests

        [Fact]
        public void UrlBox_AbsoluteFlag_SetCorrectly()
        {
            var metadata = new J2KMetadata();
            metadata.SetUuidInfoUrl("https://example.com", isAbsolute: true);

            Assert.Equal(1, metadata.UuidInfo.UrlFlags);
        }

        [Fact]
        public void UrlBox_RelativeFlag_SetCorrectly()
        {
            var metadata = new J2KMetadata();
            metadata.SetUuidInfoUrl("/relative/path", isAbsolute: false);

            Assert.Equal(0, metadata.UuidInfo.UrlFlags);
        }

        [Fact]
        public void UrlBox_VersionField_SetCorrectly()
        {
            var metadata = new J2KMetadata();
            
            for (byte version = 0; version < 5; version++)
            {
                metadata.SetUuidInfoUrl($"https://example.com/v{version}", version: version);
                Assert.Equal(version, metadata.UuidInfo.UrlVersion);
            }
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void UuidInfoBox_ValidStructure_PassesValidation()
        {
            var metadata = new J2KMetadata();
            var uuid1 = Guid.NewGuid();
            
            metadata.SetUuidInfo(
                new System.Collections.Generic.List<Guid> { uuid1 },
                url: "https://example.com");

            var codestream = CreateMinimalCodestream();

            using (var ms = new MemoryStream())
            {
                ms.Write(codestream, 0, codestream.Length);
                ms.Seek(0, SeekOrigin.Begin);

                var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length);
                writer.Metadata = metadata;
                writer.writeFileFormat();

                // Read and validate
                ms.Seek(0, SeekOrigin.Begin);
                var reader = new FileFormatReader(new ISRandomAccessIO(ms));
                reader.StrictValidation = false; // Don't throw on warnings
                reader.readFileFormat();

                // Should pass validation
                Assert.False(reader.Validator.HasErrors);
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

        #endregion
    }
}
