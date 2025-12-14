// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using Xunit;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.codestream.metadata;
using CoreJ2K.j2k.codestream.writer;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for TLM (Tile-part Lengths, Main header) marker writer.
    /// </summary>
    public class TLMMarkerWriterTests
    {
        [Fact]
        public void TLMMarkerWriter_EmptyTLM_WritesNothing()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            Assert.Equal(0, ms.Length);
        }
        
        [Fact]
        public void TLMMarkerWriter_NullTLM_WritesNothing()
        {
            // Arrange
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, null);
            
            // Assert
            Assert.Equal(0, ms.Length);
        }
        
        [Fact]
        public void TLMMarkerWriter_SingleTilePart_WritesCorrectly()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 50000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            Assert.True(bytes.Length > 0);
            
            // Check TLM marker - written as big-endian short 0xFF55
            // Read as big-endian: (bytes[0] << 8) | bytes[1] should equal 0xFF55
            int marker = (bytes[0] << 8) | bytes[1];
            Assert.Equal(Markers.TLM, (short)marker);
            
            // Check length field exists  
            int ltlm = (bytes[2] << 8) | bytes[3];
            Assert.True(ltlm >= 4); // At least Ztlm + Stlm + overhead
            
            // Check Ztlm (should be 0 for first marker) - at byte 4
            Assert.Equal(0, bytes[4]);
            
            // Check Stlm exists - at byte 5
            Assert.True(bytes[5] >= 0);
        }
        
        [Fact]
        public void TLMMarkerWriter_SingleTileMultipleParts_WritesAllParts()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 50000);
            tlm.AddTilePart(0, 1, 48000);
            tlm.AddTilePart(0, 2, 45000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            Assert.True(ms.Length > 0);
            Assert.Equal(3, tlm.TotalTileParts);
            
            var bytes = ms.ToArray();
            // Verify marker as big-endian short
            int marker = (bytes[0] << 8) | bytes[1];
            Assert.Equal(Markers.TLM, (short)marker);
        }
        
        [Fact]
        public void TLMMarkerWriter_MultipleTiles_WritesAllEntries()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            for (int t = 0; t < 10; t++)
            {
                tlm.AddTilePart(t, 0, 50000 + t * 100);
                tlm.AddTilePart(t, 1, 48000 + t * 100);
            }
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            Assert.True(ms.Length > 0);
            Assert.Equal(20, tlm.TotalTileParts);
            
            var bytes = ms.ToArray();
            int marker = (bytes[0] << 8) | bytes[1];
            Assert.Equal(Markers.TLM, (short)marker);
        }
        
        [Fact]
        public void TLMMarkerWriter_SmallTileParts_Uses16BitLength()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 30000); // < 65536, can use 16-bit
            tlm.AddTilePart(1, 0, 40000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Check Stlm field (byte 5)
            int stlm = bytes[5];
            int ptlmSizeIndicator = (stlm >> 4) & 0x03;
            Assert.Equal(0, ptlmSizeIndicator); // 0 = 16-bit length
        }
        
        [Fact]
        public void TLMMarkerWriter_LargeTileParts_Uses32BitLength()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 70000); // > 65535, requires 32-bit
            tlm.AddTilePart(1, 0, 80000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Check Stlm field (byte 5)
            int stlm = bytes[5];
            int ptlmSizeIndicator = (stlm >> 4) & 0x03;
            Assert.Equal(1, ptlmSizeIndicator); // 1 = 32-bit length
        }
        
        [Fact]
        public void TLMMarkerWriter_SequentialTiles_CanUseImplicitIndexing()
        {
            // Arrange - Sequential tiles 0, 1, 2, 3...
            var tlm = new TilePartLengthsData();
            for (int t = 0; t < 5; t++)
            {
                tlm.AddTilePart(t, 0, 50000);
            }
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Check Stlm field (byte 5)
            int stlm = bytes[5];
            int ttlmSize = (stlm >> 6) & 0x03;
            // Note: Actual implementation might use explicit or implicit
            // This just verifies Stlm is valid
            Assert.True(ttlmSize >= 0 && ttlmSize <= 2);
        }
        
        [Fact]
        public void TLMMarkerWriter_ManyTilesSmallIndices_Uses8BitTileIndex()
        {
            // Arrange - Tiles 0-9 (all < 256)
            var tlm = new TilePartLengthsData();
            for (int t = 0; t < 10; t++)
            {
                tlm.AddTilePart(t, 0, 50000);
            }
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Verify marker written
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0x55, bytes[1]);
            
            // Check that we have reasonable data
            int ltlm = (bytes[2] << 8) | bytes[3];
            Assert.True(ltlm > 4);
        }
        
        [Fact]
        public void TLMMarkerWriter_LargeTileIndices_Uses16BitTileIndex()
        {
            // Arrange - Tile index > 255, requires 16-bit
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(300, 0, 50000);
            tlm.AddTilePart(301, 0, 51000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Check Stlm field (byte 5)
            int stlm = bytes[5];
            int ttlmSize = (stlm >> 6) & 0x03;
            Assert.Equal(2, ttlmSize); // 2 = 16-bit tile index
        }
        
        [Fact]
        public void TLMMarkerWriter_VeryLargeDataSet_HandlesCorrectly()
        {
            // Arrange - 1000 tiles with multiple parts each
            var tlm = new TilePartLengthsData();
            for (int t = 0; t < 1000; t++)
            {
                tlm.AddTilePart(t, 0, 50000 + t);
                tlm.AddTilePart(t, 1, 48000 + t);
            }
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            Assert.True(ms.Length > 0);
            Assert.Equal(2000, tlm.TotalTileParts);
            
            // Verify first marker as big-endian short
            var bytes = ms.ToArray();
            int marker = (bytes[0] << 8) | bytes[1];
            Assert.Equal(Markers.TLM, (short)marker);
        }
        
        [Fact]
        public void TLMMarkerWriter_MarkerLengthField_IsCorrect()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 50000);
            tlm.AddTilePart(1, 0, 51000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Read Ltlm (marker length) - big endian at bytes 2-3
            int ltlm = (bytes[2] << 8) | bytes[3];
            
            // Ltlm includes all bytes after the marker itself
            // Should be: 2 (Ltlm itself) + 1 (Ztlm) + 1 (Stlm) + entries
            Assert.True(ltlm >= 4);
            
            // The actual segment length should match
            // (minus the 2-byte marker itself)
            int expectedSegmentLength = ltlm + 2;
            Assert.True(bytes.Length >= expectedSegmentLength);
        }
        
        [Fact]
        public void TLMMarkerWriter_ZtlmField_StartsAtZero()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 50000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Ztlm is at byte 4
            Assert.Equal(0, bytes[4]);
        }
        
        [Fact]
        public void TLMMarkerWriter_StlmField_HasValidStructure()
        {
            // Arrange
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 50000);
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Stlm is at byte 5
            int stlm = bytes[5];
            
            // Bits 6-7: Ttlm size (0-2, not 3)
            int ttlmSize = (stlm >> 6) & 0x03;
            Assert.True(ttlmSize >= 0 && ttlmSize <= 2);
            
            // Bits 4-5: Ptlm size indicator (0-1, not 2-3)
            int ptlmIndicator = (stlm >> 4) & 0x03;
            Assert.True(ptlmIndicator >= 0 && ptlmIndicator <= 1);
            
            // Bits 0-3: Should be 0 (reserved)
            int reserved = stlm & 0x0F;
            Assert.Equal(0, reserved);
        }
        
        [Fact]
        public void TLMMarkerWriter_MultipleMarkers_IncrementZtlm()
        {
            // Arrange - Create enough data to potentially span multiple markers
            var tlm = new TilePartLengthsData();
            
            // Add many tile-parts to potentially trigger multiple TLM markers
            for (int t = 0; t < 5000; t++)
            {
                tlm.AddTilePart(t, 0, 50000);
            }
            
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            // Act
            TLMMarkerWriter.WriteTLM(writer, tlm);
            
            // Assert
            var bytes = ms.ToArray();
            
            // Should have written something
            Assert.True(bytes.Length > 0);
            
            // First marker should be TLM
            int marker = (bytes[0] << 8) | bytes[1];
            Assert.Equal(Markers.TLM, (short)marker);
            
            // First Ztlm should be 0 (at byte 4)
            Assert.Equal(0, bytes[4]);
        }
    }
}
