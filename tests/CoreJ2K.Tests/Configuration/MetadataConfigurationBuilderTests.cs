// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Tests for the metadata configuration builder API.
    /// </summary>
    public class MetadataConfigurationBuilderTests
    {
        [Fact]
        public void Constructor_CreatesEmptyConfiguration()
        {
            var config = new MetadataConfigurationBuilder();
            
            Assert.Empty(config.Comments);
            Assert.Empty(config.XmlData);
            Assert.Empty(config.Uuids);
            Assert.Null(config.IntellectualPropertyRights);
            Assert.False(config.HasMetadata);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void WithComment_AddsComment()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Test comment");
            
            Assert.Single(config.Comments);
            Assert.Equal("Test comment", config.Comments[0]);
            Assert.True(config.HasMetadata);
        }
        
        [Fact]
        public void WithComment_NullOrEmpty_ThrowsException()
        {
            var config = new MetadataConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithComment(null));
            Assert.Throws<ArgumentException>(() => config.WithComment(""));
        }
        
        [Fact]
        public void WithComments_AddsMultipleComments()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComments("Comment 1", "Comment 2", "Comment 3");
            
            Assert.Equal(3, config.Comments.Count);
            Assert.Equal("Comment 1", config.Comments[0]);
            Assert.Equal("Comment 2", config.Comments[1]);
            Assert.Equal("Comment 3", config.Comments[2]);
        }
        
        [Fact]
        public void WithXml_AddsXmlData()
        {
            var xml = "<metadata><title>Test</title></metadata>";
            var config = new MetadataConfigurationBuilder()
                .WithXml(xml);
            
            Assert.Single(config.XmlData);
            Assert.Equal(xml, config.XmlData[0]);
            Assert.True(config.HasMetadata);
        }
        
        [Fact]
        public void WithXml_NullOrEmpty_ThrowsException()
        {
            var config = new MetadataConfigurationBuilder();
            
            Assert.Throws<ArgumentException>(() => config.WithXml(null));
            Assert.Throws<ArgumentException>(() => config.WithXml(""));
        }
        
        [Fact]
        public void WithXmlData_AddsMultipleXmlBlocks()
        {
            var xml1 = "<metadata1/>";
            var xml2 = "<metadata2/>";
            
            var config = new MetadataConfigurationBuilder()
                .WithXmlData(xml1, xml2);
            
            Assert.Equal(2, config.XmlData.Count);
        }
        
        [Fact]
        public void WithUuid_ByteArray_AddsUuidData()
        {
            var uuid = Guid.NewGuid();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            
            var config = new MetadataConfigurationBuilder()
                .WithUuid(uuid, data);
            
            Assert.Single(config.Uuids);
            Assert.Equal(uuid, config.Uuids[0].Uuid);
            Assert.Equal(data, config.Uuids[0].Data);
            Assert.True(config.HasMetadata);
        }
        
        [Fact]
        public void WithUuid_String_AddsUuidData()
        {
            var uuid = Guid.NewGuid();
            var data = "Test UUID data";
            
            var config = new MetadataConfigurationBuilder()
                .WithUuid(uuid, data);
            
            Assert.Single(config.Uuids);
            Assert.Equal(uuid, config.Uuids[0].Uuid);
        }
        
        [Fact]
        public void WithUuid_NullData_ThrowsException()
        {
            var config = new MetadataConfigurationBuilder();
            var uuid = Guid.NewGuid();
            
            Assert.Throws<ArgumentNullException>(() => config.WithUuid(uuid, (byte[])null));
            Assert.Throws<ArgumentException>(() => config.WithUuid(uuid, (string)null));
        }
        
        [Fact]
        public void WithIntellectualPropertyRights_SetsIPR()
        {
            var ipr = "Copyright © 2025 Test Company";
            var config = new MetadataConfigurationBuilder()
                .WithIntellectualPropertyRights(ipr);
            
            Assert.Equal(ipr, config.IntellectualPropertyRights);
            Assert.True(config.HasMetadata);
        }
        
        [Fact]
        public void WithCopyright_SetsIPR()
        {
            var copyright = "Copyright © 2025 Test Company";
            var config = new MetadataConfigurationBuilder()
                .WithCopyright(copyright);
            
            Assert.Equal(copyright, config.IntellectualPropertyRights);
        }
        
        [Fact]
        public void ClearComments_RemovesAllComments()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComments("Comment 1", "Comment 2")
                .ClearComments();
            
            Assert.Empty(config.Comments);
        }
        
        [Fact]
        public void ClearXml_RemovesAllXml()
        {
            var config = new MetadataConfigurationBuilder()
                .WithXmlData("<xml1/>", "<xml2/>")
                .ClearXml();
            
            Assert.Empty(config.XmlData);
        }
        
        [Fact]
        public void ClearUuids_RemovesAllUuids()
        {
            var config = new MetadataConfigurationBuilder()
                .WithUuid(Guid.NewGuid(), new byte[] { 1 })
                .WithUuid(Guid.NewGuid(), new byte[] { 2 })
                .ClearUuids();
            
            Assert.Empty(config.Uuids);
        }
        
        [Fact]
        public void ClearAll_RemovesEverything()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Test")
                .WithXml("<test/>")
                .WithUuid(Guid.NewGuid(), new byte[] { 1 })
                .WithCopyright("Copyright")
                .ClearAll();
            
            Assert.Empty(config.Comments);
            Assert.Empty(config.XmlData);
            Assert.Empty(config.Uuids);
            Assert.Null(config.IntellectualPropertyRights);
            Assert.False(config.HasMetadata);
        }
        
        [Fact]
        public void FluentAPI_ChainsAllMethods()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Comment 1")
                .WithComment("Comment 2")
                .WithXml("<metadata/>")
                .WithUuid(Guid.NewGuid(), "UUID data")
                .WithCopyright("Copyright © 2025");
            
            Assert.Equal(2, config.Comments.Count);
            Assert.Single(config.XmlData);
            Assert.Single(config.Uuids);
            Assert.NotNull(config.IntellectualPropertyRights);
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Test")
                .WithXml("<test/>")
                .WithUuid(Guid.NewGuid(), new byte[] { 1 });
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new MetadataConfigurationBuilder()
                .WithComment("Comment")
                .WithXml("<xml/>")
                .WithUuid(Guid.NewGuid(), new byte[] { 1 })
                .WithCopyright("Copyright");
            
            var clone = original.Clone();
            
            // Modify clone
            clone.WithComment("New comment");
            
            // Original should be unchanged
            Assert.Single(original.Comments);
            
            // Clone should have new value
            Assert.Equal(2, clone.Comments.Count);
        }
        
        [Fact]
        public void ToString_EmptyMetadata_ReturnsNone()
        {
            var config = new MetadataConfigurationBuilder();
            
            var str = config.ToString();
            
            Assert.Contains("none", str.ToLower());
        }
        
        [Fact]
        public void ToString_WithMetadata_DescribesContent()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Test")
                .WithXml("<test/>")
                .WithCopyright("Copyright");
            
            var str = config.ToString();
            
            Assert.Contains("1 comment", str);
            Assert.Contains("1 XML", str);
            Assert.Contains("IPR", str);
        }
        
        // Preset tests
        
        [Fact]
        public void Presets_WithCopyright_CreatesCopyrightMetadata()
        {
            var copyright = "Copyright © 2025";
            var config = MetadataPresets.WithCopyright(copyright);
            
            Assert.Equal(copyright, config.IntellectualPropertyRights);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Presets_WithTitleAndDescription_CreatesComments()
        {
            var title = "Test Image";
            var description = "Test Description";
            
            var config = MetadataPresets.WithTitleAndDescription(title, description);
            
            Assert.Equal(2, config.Comments.Count);
            Assert.Contains("Title:", config.Comments[0]);
            Assert.Contains("Description:", config.Comments[1]);
        }
        
        [Fact]
        public void Presets_WithExif_CreatesXmlMetadata()
        {
            var exif = "<exif><make>Canon</make></exif>";
            var config = MetadataPresets.WithExif(exif);
            
            Assert.Single(config.XmlData);
            Assert.Equal(exif, config.XmlData[0]);
        }
        
        [Fact]
        public void RealWorldScenario_PhotographMetadata()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Sunset over mountains")
                .WithCopyright("Copyright © 2025 Photographer Name")
                .WithXml("<exif><camera>Canon EOS R5</camera><lens>RF 24-105mm</lens></exif>");
            
            Assert.True(config.HasMetadata);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_MedicalImaging()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Patient: Anonymous")
                .WithComment("Study: CT Scan")
                .WithComment("Date: 2025-01-15")
                .WithXml("<dicom><studyId>12345</studyId></dicom>")
                .WithUuid(Guid.NewGuid(), "Institution-specific data");
            
            Assert.True(config.HasMetadata);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void RealWorldScenario_GeospatialData()
        {
            var config = new MetadataConfigurationBuilder()
                .WithComment("Satellite imagery - Region: North America")
                .WithXml("<gml:metadata><bounds>-180,90,180,-90</bounds></gml:metadata>")
                .WithCopyright("© 2025 Satellite Provider");
            
            Assert.True(config.HasMetadata);
            Assert.True(config.IsValid);
        }
    }
}
