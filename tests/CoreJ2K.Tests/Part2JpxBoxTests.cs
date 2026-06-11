// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.IO;
using System.Text;
using Xunit;
using CoreJ2K.j2k.fileformat;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.fileformat.writer;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for the JPEG 2000 Part 2 (JPX) extended file-format boxes:
    /// Association (asoc), Number List (nlst), Data Reference (dtbl), Fragment Table (ftbl),
    /// Fragment List (flst), Cross Reference (cref), Codestream Header (jpch), and
    /// Compositing Layer Header (jplh), plus the 'jpx ' File Type brand.
    /// </summary>
    public class Part2JpxBoxTests
    {
        #region Model-level round-trip tests

        [Fact]
        public void NumberListBox_RoundTrip_PreservesEntries()
        {
            var nlst = new NumberListBox()
                .AddRenderedResult()
                .AddCodestream(2)
                .AddCompositingLayer(5);

            var parsed = NumberListBox.Parse(nlst.GetContentBytes());

            Assert.Equal(3, parsed.Entries.Count);
            Assert.Equal(NumberListBox.RenderedResult, parsed.Entries[0]);
            Assert.True(NumberListBox.IsCodestream(parsed.Entries[1]));
            Assert.Equal(2u, NumberListBox.IndexOf(parsed.Entries[1]));
            Assert.True(NumberListBox.IsCompositingLayer(parsed.Entries[2]));
            Assert.Equal(5u, NumberListBox.IndexOf(parsed.Entries[2]));
        }

        [Fact]
        public void DataReferenceBox_RoundTrip_PreservesUrls()
        {
            var dtbl = new DataReferenceBox()
                .AddUrl("https://example.com/data1.j2c")
                .AddUrl("file:///local/data2.j2c");

            var parsed = DataReferenceBox.Parse(dtbl.GetContentBytes());

            Assert.Equal(2, parsed.Entries.Count);
            Assert.Equal("https://example.com/data1.j2c", parsed.Entries[0].Url);
            Assert.Equal("file:///local/data2.j2c", parsed.Entries[1].Url);
        }

        [Fact]
        public void FragmentListBox_RoundTrip_PreservesFragments()
        {
            var flst = new FragmentListBox()
                .AddFragment(0x1_0000_0000UL, 4096, 0)   // 64-bit offset exercises OFF width
                .AddFragment(12345, 678, 2);

            var parsed = FragmentListBox.Parse(flst.GetContentBytes());

            Assert.Equal(2, parsed.Fragments.Count);
            Assert.Equal(0x1_0000_0000UL, parsed.Fragments[0].Offset);
            Assert.Equal(4096u, parsed.Fragments[0].Length);
            Assert.Equal(0, parsed.Fragments[0].DataReference);
            Assert.Equal(12345UL, parsed.Fragments[1].Offset);
            Assert.Equal(678u, parsed.Fragments[1].Length);
            Assert.Equal(2, parsed.Fragments[1].DataReference);
        }

        [Fact]
        public void AssociationBox_RoundTrip_PreservesNumberListAndChildren()
        {
            var asoc = new AssociationBox { NumberList = new NumberListBox().AddCodestream(0) };
            asoc.Children.Add(new Jp2BoxData
            {
                BoxType = FileFormatBoxes.LBL_BOX,
                Content = Encoding.UTF8.GetBytes("Frame 0")
            });

            var parsed = AssociationBox.Parse(asoc.GetContentBytes());

            Assert.NotNull(parsed.NumberList);
            Assert.Single(parsed.NumberList!.Entries);
            Assert.True(NumberListBox.IsCodestream(parsed.NumberList.Entries[0]));
            Assert.Single(parsed.Children);
            Assert.Equal(FileFormatBoxes.LBL_BOX, parsed.Children[0].BoxType);
            Assert.Equal("Frame 0", Encoding.UTF8.GetString(parsed.Children[0].Content));
        }

        [Fact]
        public void CrossReferenceBox_RoundTrip_PreservesFragmentList()
        {
            var cref = new CrossReferenceBox
            {
                FragmentList = new FragmentListBox().AddFragment(100, 200, 1)
            };

            var parsed = CrossReferenceBox.Parse(cref.GetContentBytes());

            Assert.NotNull(parsed.FragmentList);
            Assert.Single(parsed.FragmentList!.Fragments);
            Assert.Equal(100UL, parsed.FragmentList.Fragments[0].Offset);
            Assert.Equal(1, parsed.FragmentList.Fragments[0].DataReference);
        }

        [Fact]
        public void SuperBox_RoundTrip_PreservesRawChildren()
        {
            var jpch = new CodestreamHeaderBox();
            jpch.Children.Add(new Jp2BoxData
            {
                BoxType = FileFormatBoxes.IMAGE_HEADER_BOX,
                Content = new byte[] { 1, 2, 3, 4 }
            });
            jpch.Children.Add(new Jp2BoxData
            {
                BoxType = FileFormatBoxes.COLOUR_SPECIFICATION_BOX,
                Content = new byte[] { 9, 8, 7 }
            });

            var parsed = CodestreamHeaderBox.Parse(jpch.GetContentBytes());

            Assert.Equal(2, parsed.Children.Count);
            Assert.Equal(FileFormatBoxes.IMAGE_HEADER_BOX, parsed.Children[0].BoxType);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, parsed.Children[0].Content);
            Assert.Equal(FileFormatBoxes.COLOUR_SPECIFICATION_BOX, parsed.Children[1].BoxType);
            Assert.Equal(new byte[] { 9, 8, 7 }, parsed.Children[1].Content);
        }

        #endregion

        #region File-level round-trip tests

        [Fact]
        public void FileRoundTrip_AssociationWithLabel_IsPreserved()
        {
            var metadata = new J2KMetadata();
            metadata.AddAssociation(new NumberListBox().AddCodestream(0), "Region of interest");

            var reader = WriteThenRead(metadata);

            Assert.Single(reader.Metadata.Associations);
            var asoc = reader.Metadata.Associations[0];
            Assert.NotNull(asoc.NumberList);
            Assert.True(NumberListBox.IsCodestream(asoc.NumberList!.Entries[0]));
            Assert.Single(asoc.Children);
            Assert.Equal("Region of interest", Encoding.UTF8.GetString(asoc.Children[0].Content));
        }

        [Fact]
        public void FileRoundTrip_DataReference_IsPreserved()
        {
            var metadata = new J2KMetadata
            {
                DataReference = new DataReferenceBox().AddUrl("https://example.com/external.j2c")
            };

            var reader = WriteThenRead(metadata);

            Assert.NotNull(reader.Metadata.DataReference);
            Assert.Single(reader.Metadata.DataReference!.Entries);
            Assert.Equal("https://example.com/external.j2c", reader.Metadata.DataReference.Entries[0].Url);
        }

        [Fact]
        public void FileRoundTrip_FragmentTableAndCrossReference_ArePreserved()
        {
            var metadata = new J2KMetadata();
            metadata.FragmentTables.Add(new FragmentTableBox
            {
                FragmentList = new FragmentListBox().AddFragment(2048, 1024, 1)
            });
            metadata.CrossReferences.Add(new CrossReferenceBox
            {
                FragmentList = new FragmentListBox().AddFragment(64, 32, 0)
            });

            var reader = WriteThenRead(metadata);

            Assert.Single(reader.Metadata.FragmentTables);
            Assert.Equal(2048UL, reader.Metadata.FragmentTables[0].FragmentList!.Fragments[0].Offset);

            Assert.Single(reader.Metadata.CrossReferences);
            Assert.Equal(32u, reader.Metadata.CrossReferences[0].FragmentList!.Fragments[0].Length);
        }

        [Fact]
        public void FileRoundTrip_HeaderSuperBoxes_ArePreserved()
        {
            var metadata = new J2KMetadata();

            var jpch = new CodestreamHeaderBox();
            jpch.Children.Add(new Jp2BoxData { BoxType = FileFormatBoxes.IMAGE_HEADER_BOX, Content = new byte[] { 1, 2 } });
            metadata.CodestreamHeaders.Add(jpch);

            var jplh = new CompositingLayerHeaderBox();
            jplh.Children.Add(new Jp2BoxData { BoxType = FileFormatBoxes.CHANNEL_DEFINITION_BOX, Content = new byte[] { 3, 4, 5 } });
            metadata.CompositingLayerHeaders.Add(jplh);

            var reader = WriteThenRead(metadata);

            Assert.Single(reader.Metadata.CodestreamHeaders);
            Assert.Equal(FileFormatBoxes.IMAGE_HEADER_BOX, reader.Metadata.CodestreamHeaders[0].Children[0].BoxType);

            Assert.Single(reader.Metadata.CompositingLayerHeaders);
            Assert.Equal(new byte[] { 3, 4, 5 }, reader.Metadata.CompositingLayerHeaders[0].Children[0].Content);
        }

        #endregion

        #region Brand tests

        [Fact]
        public void HasJpxBoxes_TrueWhenJpxBoxPresent_FalseOtherwise()
        {
            var plain = new J2KMetadata();
            Assert.False(plain.HasJpxBoxes);

            plain.AddLabel("a Part-1 label is not a JPX box");
            Assert.False(plain.HasJpxBoxes);

            plain.AddAssociation(new NumberListBox().AddRenderedResult());
            Assert.True(plain.HasJpxBoxes);
        }

        [Fact]
        public void FileTypeBox_UsesJpxBrand_WhenJpxBoxesPresent()
        {
            var metadata = new J2KMetadata();
            metadata.AddAssociation(new NumberListBox().AddRenderedResult(), "label");

            var bytes = WriteFile(metadata);

            // Signature box is 12 bytes; File Type box: LBox(4)+TBox(4)+BR(4) -> BR at offset 20.
            Assert.Equal(FileFormatBoxes.FT_BR_JPX, ReadInt(bytes, 20));
        }

        [Fact]
        public void FileTypeBox_UsesJp2Brand_WhenNoJpxBoxes()
        {
            var metadata = new J2KMetadata();
            metadata.AddLabel("just a label");

            var bytes = WriteFile(metadata);

            Assert.Equal(FileFormatBoxes.FT_BR, ReadInt(bytes, 20));
        }

        [Fact]
        public void UseJpxBrand_ForcesJpxBrand_WithoutJpxBoxes()
        {
            var metadata = new J2KMetadata { UseJpxBrand = true };

            var bytes = WriteFile(metadata);

            Assert.Equal(FileFormatBoxes.FT_BR_JPX, ReadInt(bytes, 20));
        }

        #endregion

        #region Helpers

        private static byte[] WriteFile(J2KMetadata metadata)
        {
            var codestream = CreateMinimalCodestream();
            using var ms = new MemoryStream();
            ms.Write(codestream, 0, codestream.Length);
            ms.Seek(0, SeekOrigin.Begin);

            var writer = new FileFormatWriter(ms, 16, 16, 1, new[] { 8 }, codestream.Length)
            {
                Metadata = metadata
            };
            writer.writeFileFormat();

            return ms.ToArray();
        }

        private static FileFormatReader WriteThenRead(J2KMetadata metadata)
        {
            var bytes = WriteFile(metadata);
            var readStream = new MemoryStream(bytes);
            var reader = new FileFormatReader(new ISRandomAccessIO(readStream));
            reader.readFileFormat();
            return reader;
        }

        private static int ReadInt(byte[] b, int o) =>
            (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

        private static byte[] CreateMinimalCodestream()
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
