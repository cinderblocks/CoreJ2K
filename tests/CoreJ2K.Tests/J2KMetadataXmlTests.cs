// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.j2k.fileformat.metadata;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for the JPEG 2000 Part 14 (JPXML) XML representation of <see cref="J2KMetadata"/>.
    /// </summary>
    public class J2KMetadataXmlTests
    {
        [Fact]
        public void RoundTrip_BasicTextMetadata_Preserved()
        {
            var md = new J2KMetadata();
            md.AddComment("Hello world", "en");
            md.AddComment("Bonjour", "fr");
            md.AddXml("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"></x:xmpmeta>");
            md.AddIntellectualPropertyRights("(c) 2025 ACME Corp.");
            md.AddLabel("Front cover");
            md.IntellectualPropertyBoxes.Add(new IntellectualPropertyBox { Text = "Patent pending" });

            var xml = J2KMetadataXml.ToXml(md);
            Assert.Contains("J2KMetadata", xml);

            var rt = J2KMetadataXml.FromXml(xml);
            Assert.Equal(2, rt.Comments.Count);
            Assert.Equal("Hello world", rt.Comments[0].Text);
            Assert.Equal("fr", rt.Comments[1].Language);
            Assert.Single(rt.XmlBoxes);
            Assert.Contains("xmpmeta", rt.XmlBoxes[0].XmlContent);
            Assert.Single(rt.IntellectualPropertyRights);
            Assert.Equal("(c) 2025 ACME Corp.", rt.IntellectualPropertyRights[0].GetText());
            Assert.Single(rt.Labels);
            Assert.Equal("Front cover", rt.Labels[0].GetLabel());
            Assert.Single(rt.IntellectualPropertyBoxes);
            Assert.Equal("Patent pending", rt.IntellectualPropertyBoxes[0].GetText());
        }

        [Fact]
        public void RoundTrip_BinaryUuidAndIpr_Preserved()
        {
            var md = new J2KMetadata();
            var guid = Guid.NewGuid();
            md.AddUuid(guid, new byte[] { 1, 2, 3, 4, 5 });
            md.IntellectualPropertyRights.Add(new JprBox { RawData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF } });

            var xml = J2KMetadataXml.ToXml(md);
            var rt = J2KMetadataXml.FromXml(xml);

            Assert.Single(rt.UuidBoxes);
            Assert.Equal(guid, rt.UuidBoxes[0].Uuid);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, rt.UuidBoxes[0].Data);

            Assert.Single(rt.IntellectualPropertyRights);
            Assert.True(rt.IntellectualPropertyRights[0].IsBinary);
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, rt.IntellectualPropertyRights[0].RawData);
        }

        [Fact]
        public void ToXml_ResolutionAndUuidInfo_Emitted()
        {
            var md = new J2KMetadata();
            md.SetResolutionDpi(300.0, 300.0, isCapture: true);
            md.SetUuidInfo(new System.Collections.Generic.List<Guid> { Guid.NewGuid() },
                "https://example.com", urlVersion: 0, urlFlags: 1);

            var xml = J2KMetadataXml.ToXml(md);
            Assert.Contains("Resolution", xml);
            Assert.Contains("captureHorizontalDpi", xml);
            Assert.Contains("UuidInfo", xml);
            Assert.Contains("https://example.com", xml);

            var rt = J2KMetadataXml.FromXml(xml);
            Assert.NotNull(rt.Resolution);
            Assert.True(rt.Resolution!.HasCaptureResolution);
            Assert.NotNull(rt.UuidInfo);
            Assert.Equal("https://example.com", rt.UuidInfo!.Url);
            Assert.Single(rt.UuidInfo.UuidList);
        }

        [Fact]
        public void FromXml_RejectsInvalidRoot()
        {
            Assert.Throws<System.IO.InvalidDataException>(() =>
                J2KMetadataXml.FromXml("<NotJ2K xmlns=\"urn:iso:std:iso-iec:15444:-14\"/>"));
        }

        [Fact]
        public void FixedConstant_IntellectualPropertyBoxIsJp2i()
        {
            // ISO/IEC 15444-1 §I.7.3: Intellectual Property box type = 'jp2i' (0x6A703269).
            Assert.Equal(0x6a703269, CoreJ2K.j2k.fileformat.FileFormatBoxes.INTELLECTUAL_PROPERTY_BOX);
        }
    }
}
