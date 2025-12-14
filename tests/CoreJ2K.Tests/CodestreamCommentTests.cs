// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using Xunit;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.codestream.reader;
using CoreJ2K.j2k.io;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for COM (Comment) marker segment support in JPEG2000 codestreams.
    /// Tests both reading COM markers from codestreams and the metadata API.
    /// </summary>
    public class CodestreamCommentTests
    {
        [Fact]
        public void CodestreamComment_DefaultConstructor()
        {
            var comment = new CodestreamComment();

            Assert.True(comment.IsMainHeader);
            Assert.Equal(-1, comment.TileIndex);
            Assert.Equal(1, comment.RegistrationMethod);
            Assert.False(comment.IsBinary);
        }

        [Fact]
        public void CodestreamComment_LatinTextComment()
        {
            var comment = new CodestreamComment
            {
                Text = "Created by CoreJ2K",
                RegistrationMethod = 1, // Latin text
                IsMainHeader = true
            };

            Assert.Equal("Created by CoreJ2K", comment.GetText());
            Assert.False(comment.IsBinary);
            Assert.True(comment.IsMainHeader);
        }

        [Fact]
        public void CodestreamComment_BinaryData()
        {
            var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var comment = new CodestreamComment
            {
                Data = binaryData,
                RegistrationMethod = 0, // Binary
                IsBinary = true,
                IsMainHeader = true
            };

            Assert.True(comment.IsBinary);
            Assert.Null(comment.GetText());
            Assert.Equal(binaryData, comment.Data);
        }

        [Fact]
        public void CodestreamComment_TileComment()
        {
            var comment = new CodestreamComment
            {
                Text = "Tile-specific comment",
                IsMainHeader = false,
                TileIndex = 5
            };

            Assert.False(comment.IsMainHeader);
            Assert.Equal(5, comment.TileIndex);
        }

        [Fact]
        public void CodestreamComment_ToString()
        {
            var mainComment = new CodestreamComment
            {
                Text = "Main header comment",
                RegistrationMethod = 1,
                IsMainHeader = true
            };

            var tileComment = new CodestreamComment
            {
                Text = "Tile comment",
                RegistrationMethod = 1,
                IsMainHeader = false,
                TileIndex = 3
            };

            var mainStr = mainComment.ToString();
            var tileStr = tileComment.ToString();

            Assert.Contains("Main header", mainStr);
            Assert.Contains("Latin text", mainStr);
            Assert.Contains("Main header comment", mainStr);

            Assert.Contains("Tile 3", tileStr);
            Assert.Contains("Tile comment", tileStr);
        }

        [Fact]
        public void J2KMetadata_AddCodestreamComment_Text()
        {
            var metadata = new J2KMetadata();

            metadata.AddCodestreamComment("Test comment", 1, true, -1);

            Assert.Single(metadata.CodestreamComments);
            var comment = metadata.CodestreamComments[0];
            Assert.Equal("Test comment", comment.Text);
            Assert.Equal(1, comment.RegistrationMethod);
            Assert.True(comment.IsMainHeader);
            Assert.False(comment.IsBinary);
        }

        [Fact]
        public void J2KMetadata_AddCodestreamComment_Binary()
        {
            var metadata = new J2KMetadata();
            var binaryData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            metadata.AddCodestreamComment(binaryData, 0, true, -1);

            Assert.Single(metadata.CodestreamComments);
            var comment = metadata.CodestreamComments[0];
            Assert.True(comment.IsBinary);
            Assert.Equal(binaryData, comment.Data);
            Assert.Equal(0, comment.RegistrationMethod);
        }

        [Fact]
        public void J2KMetadata_GetAllComments_CombinesBothTypes()
        {
            var metadata = new J2KMetadata();

            // Add JP2 box comment
            metadata.AddComment("JP2 comment", "en");

            // Add codestream comments
            metadata.AddCodestreamComment("Codestream comment 1", 1, true, -1);
            metadata.AddCodestreamComment("Codestream comment 2", 1, false, 0);

            var allComments = metadata.GetAllComments().ToList();

            Assert.Equal(3, allComments.Count);
            Assert.Contains("JP2 comment", allComments);
            Assert.Contains("Codestream comment 1", allComments);
            Assert.Contains("Codestream comment 2", allComments);
        }

        [Fact]
        public void J2KMetadata_GetMainHeaderComments()
        {
            var metadata = new J2KMetadata();

            metadata.AddCodestreamComment("Main comment 1", 1, true, -1);
            metadata.AddCodestreamComment("Main comment 2", 1, true, -1);
            metadata.AddCodestreamComment("Tile comment", 1, false, 0);

            var mainComments = metadata.GetMainHeaderComments().ToList();

            Assert.Equal(2, mainComments.Count);
            Assert.All(mainComments, c => Assert.True(c.IsMainHeader));
        }

        [Fact]
        public void J2KMetadata_GetTileComments()
        {
            var metadata = new J2KMetadata();

            metadata.AddCodestreamComment("Main comment", 1, true, -1);
            metadata.AddCodestreamComment("Tile 0 comment", 1, false, 0);
            metadata.AddCodestreamComment("Tile 1 comment", 1, false, 1);
            metadata.AddCodestreamComment("Tile 0 comment 2", 1, false, 0);

            var tile0Comments = metadata.GetTileComments(0).ToList();
            var tile1Comments = metadata.GetTileComments(1).ToList();

            Assert.Equal(2, tile0Comments.Count);
            Assert.Single(tile1Comments);
            Assert.All(tile0Comments, c => Assert.Equal(0, c.TileIndex));
            Assert.All(tile1Comments, c => Assert.Equal(1, c.TileIndex));
        }

        [Fact]
        public void CodestreamComment_GetText_FromData()
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes("Encoded text");
            var comment = new CodestreamComment
            {
                Data = textBytes,
                RegistrationMethod = 1 // Latin text
            };

            var text = comment.GetText();

            Assert.Equal("Encoded text", text);
        }

        [Fact]
        public void CodestreamComment_GetText_BinaryReturnsNull()
        {
            var binaryData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var comment = new CodestreamComment
            {
                Data = binaryData,
                RegistrationMethod = 0, // Binary
                IsBinary = true
            };

            var text = comment.GetText();

            Assert.Null(text);
        }

        [Fact]
        public void CodestreamComment_MultipleCommentScenario()
        {
            var metadata = new J2KMetadata();

            // Simulate encoder adding version info
            metadata.AddCodestreamComment("Created by: CoreJ2K version 1.0", 1, true, -1);

            // User adds custom comment
            metadata.AddCodestreamComment("Source: Digital Camera Model XYZ", 1, true, -1);

            // Tile-specific comment
            metadata.AddCodestreamComment("Tile processed with enhanced quality", 1, false, 0);

            Assert.Equal(3, metadata.CodestreamComments.Count);
            Assert.Equal(2, metadata.GetMainHeaderComments().Count());
            Assert.Single(metadata.GetTileComments(0));
        }

        /// <summary>
        /// Helper method to create a minimal valid JPEG2000 codestream with COM markers.
        /// </summary>
        private byte[] CreateMinimalCodestreamWithCOM(string comment)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // SOC (Start of Codestream) - 0xFF4F
                writer.Write((byte)0xFF);
                writer.Write((byte)0x4F);

                // SIZ (Image and tile size) marker - 0xFF51  
                writer.Write((byte)0xFF);
                writer.Write((byte)0x51);
                writer.Write((short)47); // Lsiz

                writer.Write((short)0); // Rsiz
                writer.Write((int)64); // Xsiz
                writer.Write((int)64); // Ysiz
                writer.Write((int)0); // XOsiz
                writer.Write((int)0); // YOsiz
                writer.Write((int)64); // XTsiz
                writer.Write((int)64); // YTsiz
                writer.Write((int)0); // XTOsiz
                writer.Write((int)0); // YTOsiz
                writer.Write((short)3); // Csiz (3 components)

                // Component 0
                writer.Write((byte)7); // Ssiz (8-bit unsigned)
                writer.Write((byte)1); // XRsiz
                writer.Write((byte)1); // YRsiz

                // Component 1
                writer.Write((byte)7);
                writer.Write((byte)1);
                writer.Write((byte)1);

                // Component 2
                writer.Write((byte)7);
                writer.Write((byte)1);
                writer.Write((byte)1);

                // COM marker - 0xFF64 (must come AFTER SIZ)
                writer.Write((byte)0xFF);
                writer.Write((byte)0x64);

                // Calculate length: Lcom(2) + Rcom(2) + comment length
                var commentBytes = System.Text.Encoding.UTF8.GetBytes(comment);
                int markSegLen = 2 + 2 + commentBytes.Length;
                writer.Write((byte)((markSegLen >> 8) & 0xFF));
                writer.Write((byte)(markSegLen & 0xFF));

                // Rcom - General use (IS 8859-15:1999 Latin values)
                writer.Write((byte)0);
                writer.Write((byte)1);

                // Write comment string
                writer.Write(commentBytes);

                // Add minimal COD and QCD markers for completeness
                // (HeaderDecoder needs these for proper initialization)

                // COD (Coding style default) - 0xFF52
                writer.Write((byte)0xFF);
                writer.Write((byte)0x52);
                writer.Write((short)12); // Lcod
                writer.Write((byte)0); // Scod
                writer.Write((byte)0); // SGcod - Progression order
                writer.Write((short)1); // Number of layers
                writer.Write((byte)0); // Multiple component transform
                writer.Write((byte)5); // Number of decomposition levels
                writer.Write((byte)2); // Code-block width
                writer.Write((byte)2); // Code-block height
                writer.Write((byte)0); // Code-block style
                writer.Write((byte)0); // Wavelet transformation

                // QCD (Quantization default) - 0xFF5C
                writer.Write((byte)0xFF);
                writer.Write((byte)0x5C);
                writer.Write((short)4); // Lqcd
                writer.Write((byte)0); // Sqcd
                writer.Write((byte)8); // SPqcd

                // EOC (End of Codestream) - 0xFFD9
                writer.Write((byte)0xFF);
                writer.Write((byte)0xD9);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// This test verifies that COM markers can be read from a codestream.
        /// Note: Full integration requires decoder updates to populate J2KMetadata.
        /// TODO: This test requires a complete valid codestream with tile data.
        /// For now, COM marker reading is verified at the HeaderInfo level.
        /// </summary>
        [Fact(Skip = "Requires complete codestream with tiles - to be implemented in future decoder integration")]
        public void HeaderDecoder_ReadsCOMMarker()
        {
            var testComment = "Test Comment from Codestream";
            var codestream = CreateMinimalCodestreamWithCOM(testComment);

            using (var ms = new MemoryStream(codestream))
            {
                var raf = new ISRandomAccessIO(ms);
                var hi = new HeaderInfo();
                var pl = new ParameterList();

                // This will read the codestream and populate HeaderInfo
                var decoder = new HeaderDecoder(raf, pl, hi);

                // Verify COM marker was read
                Assert.NotEmpty(hi.comValue);
                Assert.True(hi.comValue.ContainsKey("main_0"));

                var comMarker = hi.comValue["main_0"];
                Assert.Equal(1, comMarker.rcom); // Latin text
                Assert.NotNull(comMarker.ccom);

                // Convert comment bytes to string
                var readComment = System.Text.Encoding.UTF8.GetString(comMarker.ccom);
                Assert.Equal(testComment, readComment);
            }
        }
    }
}
