// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using Xunit;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for JPEG 2000 Part 1 boxes: UUID Info and Reader Requirements boxes.
    /// </summary>
    public class UuidInfoReaderReqTests
    {
        #region UUID Info Box Tests

        [Fact]
        public void UuidInfoBox_DefaultConstructor_EmptyCollections()
        {
            var uuidInfo = new UuidInfoBox();

            Assert.Empty(uuidInfo.UuidList);
            Assert.Null(uuidInfo.Url);
            Assert.Equal(0, uuidInfo.UrlVersion);
            Assert.Equal(0, uuidInfo.UrlFlags);
        }

        [Fact]
        public void UuidInfoBox_AddUuids_StoresCorrectly()
        {
            var uuidInfo = new UuidInfoBox();
            var uuid1 = Guid.NewGuid();
            var uuid2 = Guid.NewGuid();

            uuidInfo.UuidList.Add(uuid1);
            uuidInfo.UuidList.Add(uuid2);

            Assert.Equal(2, uuidInfo.UuidList.Count);
            Assert.Contains(uuid1, uuidInfo.UuidList);
            Assert.Contains(uuid2, uuidInfo.UuidList);
        }

        [Fact]
        public void UuidInfoBox_SetUrl_StoresCorrectly()
        {
            var uuidInfo = new UuidInfoBox
            {
                Url = "https://example.com/uuid-info",
                UrlVersion = 1,
                UrlFlags = 1 // Absolute URL
            };

            Assert.Equal("https://example.com/uuid-info", uuidInfo.Url);
            Assert.Equal(1, uuidInfo.UrlVersion);
            Assert.Equal(1, uuidInfo.UrlFlags);
        }

        [Fact]
        public void UuidInfoBox_ToString_WithUuidsOnly()
        {
            var uuidInfo = new UuidInfoBox();
            uuidInfo.UuidList.Add(Guid.NewGuid());
            uuidInfo.UuidList.Add(Guid.NewGuid());
            uuidInfo.UuidList.Add(Guid.NewGuid());

            var str = uuidInfo.ToString();

            Assert.Contains("UUID Info Box", str);
            Assert.Contains("3 UUID(s)", str);
        }

        [Fact]
        public void UuidInfoBox_ToString_WithUuidsAndUrl()
        {
            var uuidInfo = new UuidInfoBox
            {
                Url = "https://example.com/info"
            };
            uuidInfo.UuidList.Add(Guid.NewGuid());

            var str = uuidInfo.ToString();

            Assert.Contains("UUID Info Box", str);
            Assert.Contains("1 UUID(s)", str);
            Assert.Contains("https://example.com/info", str);
        }

        [Fact]
        public void UuidInfoBox_EmptyList_ToString()
        {
            var uuidInfo = new UuidInfoBox();

            var str = uuidInfo.ToString();

            Assert.Contains("0 UUID(s)", str);
        }

        #endregion

        #region Reader Requirements Box Tests

        [Fact]
        public void ReaderRequirementsBox_DefaultConstructor_EmptyCollections()
        {
            var readerReq = new ReaderRequirementsBox();

            Assert.Empty(readerReq.StandardFeatures);
            Assert.Empty(readerReq.VendorFeatures);
            Assert.False(readerReq.IsJp2Compatible);
        }

        [Fact]
        public void ReaderRequirementsBox_AddStandardFeatures_StoresCorrectly()
        {
            var readerReq = new ReaderRequirementsBox();

            readerReq.StandardFeatures.Add(1); // Feature ID 1
            readerReq.StandardFeatures.Add(5); // Feature ID 5
            readerReq.StandardFeatures.Add(10); // Feature ID 10

            Assert.Equal(3, readerReq.StandardFeatures.Count);
            Assert.Contains((ushort)1, readerReq.StandardFeatures);
            Assert.Contains((ushort)5, readerReq.StandardFeatures);
            Assert.Contains((ushort)10, readerReq.StandardFeatures);
        }

        [Fact]
        public void ReaderRequirementsBox_AddVendorFeatures_StoresCorrectly()
        {
            var readerReq = new ReaderRequirementsBox();
            var vendorUuid1 = Guid.NewGuid();
            var vendorUuid2 = Guid.NewGuid();

            readerReq.VendorFeatures.Add(vendorUuid1);
            readerReq.VendorFeatures.Add(vendorUuid2);

            Assert.Equal(2, readerReq.VendorFeatures.Count);
            Assert.Contains(vendorUuid1, readerReq.VendorFeatures);
            Assert.Contains(vendorUuid2, readerReq.VendorFeatures);
        }

        [Fact]
        public void ReaderRequirementsBox_RequiresFeature_ReturnsCorrectly()
        {
            var readerReq = new ReaderRequirementsBox();
            readerReq.StandardFeatures.Add(1);
            readerReq.StandardFeatures.Add(5);

            Assert.True(readerReq.RequiresFeature(1));
            Assert.True(readerReq.RequiresFeature(5));
            Assert.False(readerReq.RequiresFeature(10));
        }

        [Fact]
        public void ReaderRequirementsBox_RequiresVendorFeature_ReturnsCorrectly()
        {
            var readerReq = new ReaderRequirementsBox();
            var uuid1 = Guid.NewGuid();
            var uuid2 = Guid.NewGuid();
            var uuid3 = Guid.NewGuid();

            readerReq.VendorFeatures.Add(uuid1);
            readerReq.VendorFeatures.Add(uuid2);

            Assert.True(readerReq.RequiresVendorFeature(uuid1));
            Assert.True(readerReq.RequiresVendorFeature(uuid2));
            Assert.False(readerReq.RequiresVendorFeature(uuid3));
        }

        [Fact]
        public void ReaderRequirementsBox_Jp2Compatible_CanBeSet()
        {
            var readerReq = new ReaderRequirementsBox
            {
                IsJp2Compatible = true
            };

            Assert.True(readerReq.IsJp2Compatible);
        }

        [Fact]
        public void ReaderRequirementsBox_ToString_WithStandardFeaturesOnly()
        {
            var readerReq = new ReaderRequirementsBox();
            readerReq.StandardFeatures.Add(1);
            readerReq.StandardFeatures.Add(2);
            readerReq.StandardFeatures.Add(3);

            var str = readerReq.ToString();

            Assert.Contains("Reader Requirements Box", str);
            Assert.Contains("3 standard feature(s)", str);
            Assert.Contains("0 vendor feature(s)", str);
        }

        [Fact]
        public void ReaderRequirementsBox_ToString_WithVendorFeaturesOnly()
        {
            var readerReq = new ReaderRequirementsBox();
            readerReq.VendorFeatures.Add(Guid.NewGuid());
            readerReq.VendorFeatures.Add(Guid.NewGuid());

            var str = readerReq.ToString();

            Assert.Contains("Reader Requirements Box", str);
            Assert.Contains("0 standard feature(s)", str);
            Assert.Contains("2 vendor feature(s)", str);
        }

        [Fact]
        public void ReaderRequirementsBox_ToString_WithCompatibilityFlag()
        {
            var readerReq = new ReaderRequirementsBox
            {
                IsJp2Compatible = true
            };
            readerReq.StandardFeatures.Add(1);

            var str = readerReq.ToString();

            Assert.Contains("(JP2 compatible)", str);
        }

        [Fact]
        public void ReaderRequirementsBox_ToString_WithMixedFeatures()
        {
            var readerReq = new ReaderRequirementsBox();
            readerReq.StandardFeatures.Add(1);
            readerReq.StandardFeatures.Add(2);
            readerReq.VendorFeatures.Add(Guid.NewGuid());

            var str = readerReq.ToString();

            Assert.Contains("2 standard feature(s)", str);
            Assert.Contains("1 vendor feature(s)", str);
        }

        #endregion

        #region Metadata Integration Tests

        [Fact]
        public void J2KMetadata_UuidInfo_CanBeSet()
        {
            var metadata = new J2KMetadata();
            var uuidInfo = new UuidInfoBox();
            uuidInfo.UuidList.Add(Guid.NewGuid());

            metadata.UuidInfo = uuidInfo;

            Assert.NotNull(metadata.UuidInfo);
            Assert.Single(metadata.UuidInfo.UuidList);
        }

        [Fact]
        public void J2KMetadata_ReaderRequirements_CanBeSet()
        {
            var metadata = new J2KMetadata();
            var readerReq = new ReaderRequirementsBox();
            readerReq.StandardFeatures.Add(1);

            metadata.ReaderRequirements = readerReq;

            Assert.NotNull(metadata.ReaderRequirements);
            Assert.Single(metadata.ReaderRequirements.StandardFeatures);
        }

        [Fact]
        public void J2KMetadata_BothBoxes_CanCoexist()
        {
            var metadata = new J2KMetadata();

            metadata.UuidInfo = new UuidInfoBox();
            metadata.UuidInfo.UuidList.Add(Guid.NewGuid());
            metadata.UuidInfo.Url = "https://example.com";

            metadata.ReaderRequirements = new ReaderRequirementsBox();
            metadata.ReaderRequirements.StandardFeatures.Add(1);
            metadata.ReaderRequirements.IsJp2Compatible = true;

            Assert.NotNull(metadata.UuidInfo);
            Assert.NotNull(metadata.ReaderRequirements);
            Assert.Single(metadata.UuidInfo.UuidList);
            Assert.Single(metadata.ReaderRequirements.StandardFeatures);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void UuidInfoBox_ManyUuids_HandlesCorrectly()
        {
            var uuidInfo = new UuidInfoBox();

            // Add 100 UUIDs
            for (int i = 0; i < 100; i++)
            {
                uuidInfo.UuidList.Add(Guid.NewGuid());
            }

            Assert.Equal(100, uuidInfo.UuidList.Count);
            Assert.All(uuidInfo.UuidList, uuid => Assert.NotEqual(Guid.Empty, uuid));
        }

        [Fact]
        public void ReaderRequirementsBox_ManyFeatures_HandlesCorrectly()
        {
            var readerReq = new ReaderRequirementsBox();

            // Add many standard features
            for (ushort i = 1; i <= 50; i++)
            {
                readerReq.StandardFeatures.Add(i);
            }

            // Add many vendor features
            for (int i = 0; i < 20; i++)
            {
                readerReq.VendorFeatures.Add(Guid.NewGuid());
            }

            Assert.Equal(50, readerReq.StandardFeatures.Count);
            Assert.Equal(20, readerReq.VendorFeatures.Count);
        }

        [Fact]
        public void UuidInfoBox_NullUrl_HandlesCorrectly()
        {
            var uuidInfo = new UuidInfoBox
            {
                Url = null
            };
            uuidInfo.UuidList.Add(Guid.NewGuid());

            var str = uuidInfo.ToString();

            Assert.Contains("1 UUID(s)", str);
            Assert.DoesNotContain("URL:", str);
        }

        [Fact]
        public void UuidInfoBox_EmptyUrl_HandlesCorrectly()
        {
            var uuidInfo = new UuidInfoBox
            {
                Url = ""
            };
            uuidInfo.UuidList.Add(Guid.NewGuid());

            var str = uuidInfo.ToString();

            Assert.Contains("1 UUID(s)", str);
            Assert.DoesNotContain("URL:", str);
        }

        [Fact]
        public void ReaderRequirementsBox_DuplicateFeatures_Allowed()
        {
            var readerReq = new ReaderRequirementsBox();

            readerReq.StandardFeatures.Add(1);
            readerReq.StandardFeatures.Add(1); // Duplicate

            Assert.Equal(2, readerReq.StandardFeatures.Count);
        }

        [Fact]
        public void UuidInfoBox_LongUrl_HandlesCorrectly()
        {
            var longUrl = "https://example.com/" + new string('a', 500);
            var uuidInfo = new UuidInfoBox
            {
                Url = longUrl
            };

            Assert.Equal(longUrl, uuidInfo.Url);
            var str = uuidInfo.ToString();
            Assert.Contains("URL:", str);
        }

        #endregion
    }
}
