// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Text;
using CoreJ2K;
using CoreJ2K.j2k.fileformat.metadata;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for JPEG2000 metadata reading and writing (XML boxes, UUID boxes, comments).
    /// </summary>
    public class MetadataTests
    {
        [Fact]
        public void Metadata_CreateAndAccessProperties()
        {
            var metadata = new J2KMetadata();
            
            // Add a comment
            metadata.AddComment("This is a test image", "en");
            
            // Add XML metadata
            var xmpXml = @"<?xml version=""1.0""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about="""" xmlns:dc=""http://purl.org/dc/elements/1.1/"">
      <dc:title>Test Image</dc:title>
      <dc:creator>Unit Test</dc:creator>
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>";
            metadata.AddXml(xmpXml);
            
            // Add a UUID box
            var customUuid = Guid.NewGuid();
            var customData = Encoding.UTF8.GetBytes("Custom vendor data");
            metadata.AddUuid(customUuid, customData);
            
            // Verify
            Assert.Single(metadata.Comments);
            Assert.Equal("This is a test image", metadata.Comments[0].Text);
            Assert.Equal("en", metadata.Comments[0].Language);
            
            Assert.Single(metadata.XmlBoxes);
            Assert.True(metadata.XmlBoxes[0].IsXMP);
            
            Assert.Single(metadata.UuidBoxes);
            Assert.Equal(customUuid, metadata.UuidBoxes[0].Uuid);
        }

        [Fact]
        public void Metadata_XmpDetection()
        {
            var xmlBox = new XmlBox
            {
                XmlContent = @"<x:xmpmeta xmlns:x=""adobe:ns:meta/""><rdf:RDF/></x:xmpmeta>"
            };
            
            Assert.True(xmlBox.IsXMP);
            Assert.False(xmlBox.IsIPTC);
        }

        [Fact]
        public void Metadata_IptcDetection()
        {
            var xmlBox = new XmlBox
            {
                XmlContent = @"<iptc:IptcCore xmlns:iptc=""http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/""/>"
            };
            
            Assert.True(xmlBox.IsIPTC);
            Assert.False(xmlBox.IsXMP);
        }

        [Fact]
        public void UuidBox_KnownUuids()
        {
            var xmpBox = new UuidBox
            {
                Uuid = UuidBox.XmpUuid,
                Data = Encoding.UTF8.GetBytes("<xmpmeta/>")
            };
            
            Assert.True(xmpBox.IsXmp);
            Assert.False(xmpBox.IsExif);
            
            var exifBox = new UuidBox
            {
                Uuid = UuidBox.ExifUuid,
                Data = new byte[] { 0x49, 0x49, 0x2A, 0x00 } // TIFF header
            };
            
            Assert.True(exifBox.IsExif);
            Assert.False(exifBox.IsXmp);
        }

        [Fact]
        public void UuidBox_GetTextData()
        {
            var testText = "Hello, JPEG2000 Metadata!";
            var uuidBox = new UuidBox
            {
                Uuid = Guid.NewGuid(),
                Data = Encoding.UTF8.GetBytes(testText)
            };
            
            var retrievedText = uuidBox.GetTextData();
            Assert.Equal(testText, retrievedText);
        }

        [Fact]
        public void CommentBox_ToString()
        {
            var comment = new CommentBox
            {
                Text = "This is a very long comment that should be truncated when displayed",
                Language = "en"
            };
            
            var str = comment.ToString();
            Assert.Contains("Comment[en]", str);
            Assert.Contains("This is a very long comment", str);
        }

        [Fact]
        public void Metadata_GetXmpAndIptc()
        {
            var metadata = new J2KMetadata();
            
            // Add regular XML
            metadata.AddXml("<regular>xml</regular>");
            
            // Add XMP
            metadata.AddXml(@"<x:xmpmeta><rdf:RDF/></x:xmpmeta>");
            
            // Add IPTC
            metadata.AddXml(@"<iptc:IptcCore/>");
            
            var xmp = metadata.GetXmp();
            var iptc = metadata.GetIptc();
            
            Assert.NotNull(xmp);
            Assert.True(xmp.IsXMP);
            
            Assert.NotNull(iptc);
            Assert.True(iptc.IsIPTC);
        }

        /// <summary>
        /// Example showing how metadata would be used in a real application.
        /// This is a documentation test showing the API usage pattern.
        /// </summary>
        [Fact]
        public void Metadata_UsageExample()
        {
            // This demonstrates the intended usage pattern for metadata
            
            // Creating metadata for encoding
            var metadata = new J2KMetadata();
            
            // Add a simple comment
            metadata.AddComment("Encoded by CoreJ2K Unit Tests", "en");
            
            // Add XMP metadata
            var xmp = @"<?xml version=""1.0""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about="""" 
                     xmlns:dc=""http://purl.org/dc/elements/1.1/""
                     xmlns:xmp=""http://ns.adobe.com/xap/1.0/"">
      <dc:title>Sample Image with Metadata</dc:title>
      <dc:creator>CoreJ2K</dc:creator>
      <dc:description>This demonstrates JPEG2000 metadata support</dc:description>
      <xmp:CreateDate>2025-01-01T00:00:00</xmp:CreateDate>
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>";
            metadata.AddXml(xmp);
            
            // Add a custom UUID box with application-specific data
            var appUuid = new Guid("12345678-1234-5678-1234-567812345678");
            var appData = Encoding.UTF8.GetBytes("Application version 1.0");
            metadata.AddUuid(appUuid, appData);
            
            // Verify the metadata structure
            Assert.Single(metadata.Comments);
            Assert.Single(metadata.XmlBoxes);
            Assert.Single(metadata.UuidBoxes);
            
            // Retrieve XMP
            var retrievedXmp = metadata.GetXmp();
            Assert.NotNull(retrievedXmp);
            Assert.Contains("Sample Image with Metadata", retrievedXmp.XmlContent);
            
            // In actual usage, this metadata would be passed to J2kImage.ToBytes():
            // var encoded = J2kImage.ToBytes(imageSource, metadata);
            
            // When decoding, metadata would be extracted:
            // var decoded = J2kImage.FromStream(stream, out J2KMetadata extractedMetadata);
            // var comments = extractedMetadata.Comments;
            // var xmpData = extractedMetadata.GetXmp();
        }
    }
}
