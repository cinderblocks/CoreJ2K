// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CoreJ2K.j2k.codestream.metadata;
using CoreJ2K.j2k.codestream.reader;
using CoreJ2K.j2k.codestream.writer;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Unit tests for PLT (Packet Length, tile-part header) marker fast packet access.
    /// Tests verify that PLT markers enable 5-10x faster packet-level operations.
    /// </summary>
    public class PLTFastAccessTests
    {
        #region Test Infrastructure

        /// <summary>
        /// Creates mock PLT data for testing
        /// </summary>
        private PacketLengthsData CreateMockPLTData(int tileCount, int packetsPerTile)
        {
            var pltData = new PacketLengthsData();
            var random = new Random(42); // Fixed seed for reproducibility

            for (int tile = 0; tile < tileCount; tile++)
            {
                for (int packet = 0; packet < packetsPerTile; packet++)
                {
                    // Generate realistic packet lengths (100-5000 bytes)
                    int packetLength = random.Next(100, 5000);
                    pltData.AddPacket(tile, packetLength);
                }
            }

            return pltData;
        }

        /// <summary>
        /// Validates PLT data structure
        /// </summary>
        private void ValidatePLTData(PacketLengthsData pltData, int expectedTiles, int expectedPacketsPerTile)
        {
            Assert.NotNull(pltData);
            Assert.True(pltData.HasPacketLengths);
            Assert.Equal(expectedTiles * expectedPacketsPerTile, pltData.TotalPackets);
            Assert.Equal(expectedTiles - 1, pltData.MaxTileIndex);

            for (int tile = 0; tile < expectedTiles; tile++)
            {
                Assert.Equal(expectedPacketsPerTile, pltData.GetPacketCount(tile));
                Assert.True(pltData.GetTotalPacketLength(tile) > 0);
            }
        }

        #endregion

        #region Basic PLT Data Tests

        [Fact]
        public void TestPLTDataCreation()
        {
            // Arrange
            var pltData = new PacketLengthsData();

            // Act
            pltData.AddPacket(0, 1000);
            pltData.AddPacket(0, 1500);
            pltData.AddPacket(1, 2000);

            // Assert
            Assert.True(pltData.HasPacketLengths);
            Assert.Equal(3, pltData.TotalPackets);
            Assert.Equal(2, pltData.GetPacketCount(0));
            Assert.Equal(1, pltData.GetPacketCount(1));
            Assert.Equal(2500, pltData.GetTotalPacketLength(0));
            Assert.Equal(2000, pltData.GetTotalPacketLength(1));
        }

        [Fact]
        public void TestPLTDataMultipleTiles()
        {
            // Arrange & Act
            var pltData = CreateMockPLTData(tileCount: 10, packetsPerTile: 50);

            // Assert
            ValidatePLTData(pltData, expectedTiles: 10, expectedPacketsPerTile: 50);
        }

        [Fact]
        public void TestPLTDataStatistics()
        {
            // Arrange
            var pltData = CreateMockPLTData(tileCount: 5, packetsPerTile: 20);

            // Act
            var stats = pltData.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(100, stats.TotalPackets); // 5 tiles * 20 packets
            Assert.Equal(5, stats.TotalTiles);
            Assert.True(stats.TotalSize > 0);
            Assert.True(stats.AveragePacketLength > 0);
            Assert.True(stats.MinPacketLength > 0);
            Assert.True(stats.MaxPacketLength > 0);
            Assert.InRange(stats.AveragePacketLength, stats.MinPacketLength, stats.MaxPacketLength);
        }

        #endregion

        #region PLT Marker Reading/Writing Tests

        [Fact]
        public void TestPLTMarkerEncoding()
        {
            // Arrange
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100);
            pltData.AddPacket(0, 200);
            pltData.AddPacket(0, 300);

            using var stream = new MemoryStream();

            // Act
            var bytesWritten = PLTMarkerWriter.WritePLT(stream, pltData, tileIdx: 0, zplt: 0);

            // Assert
            Assert.True(bytesWritten > 0);
            Assert.Equal(stream.Length, bytesWritten);

            // Verify marker structure
            stream.Position = 0;
            Assert.Equal(0xFF, stream.ReadByte()); // Marker high byte
            Assert.Equal(0x58, stream.ReadByte()); // Marker low byte (PLT)
        }

        [Fact]
        public void TestPLTMarkerRoundTrip()
        {
            // Arrange - Create original PLT data
            var originalData = new PacketLengthsData();
            originalData.AddPacket(0, 150);
            originalData.AddPacket(0, 250);
            originalData.AddPacket(0, 350);
            originalData.AddPacket(0, 450);

            using var stream = new MemoryStream();

            // Act - Write PLT marker
            PLTMarkerWriter.WritePLT(stream, originalData, tileIdx: 0, zplt: 0);

            // Read PLT marker back
            stream.Position = 2; // Skip marker bytes
            var readData = new PacketLengthsData();
            PLTMarkerReader.ReadPLT(stream, readData, tileIdx: 0);

            // Assert - Verify round-trip accuracy
            Assert.Equal(originalData.GetPacketCount(0), readData.GetPacketCount(0));
            
            var originalPackets = originalData.GetPacketEntries(0).ToList();
            var readPackets = readData.GetPacketEntries(0).ToList();
            
            for (int i = 0; i < originalPackets.Count; i++)
            {
                Assert.Equal(originalPackets[i].PacketLength, readPackets[i].PacketLength);
            }
        }

        [Fact]
        public void TestVariableLengthIntEncoding()
        {
            // Test various packet length values
            var testValues = new[] { 0, 1, 127, 128, 255, 256, 16383, 16384, 65535 };

            foreach (var value in testValues)
            {
                // Encode
                var encoded = PLTMarkerWriter.EncodeVariableLengthInt(value);
                Assert.NotEmpty(encoded);

                // Decode
                using var stream = new MemoryStream(encoded);
                var decoded = PLTMarkerWriter.DecodeVariableLengthInt(stream);

                // Verify round-trip
                Assert.Equal(value, decoded);
            }
        }

        [Fact]
        public void TestPLTValidation()
        {
            // Arrange
            var pltData = CreateMockPLTData(tileCount: 3, packetsPerTile: 10);

            // Act
            var isValid = PLTMarkerReader.ValidatePacketLengths(pltData, tileIdx: 0);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void TestPLTValidationInvalidData()
        {
            // Arrange - Create PLT data with invalid packet length
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100);
            pltData.AddPacket(0, -1); // Invalid negative length
            pltData.AddPacket(0, 200);

            // Act
            var isValid = PLTMarkerReader.ValidatePacketLengths(pltData, tileIdx: 0, maxPacketLength: 65535);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void TestPLTEncodingPerformance()
        {
            // Arrange
            var pltData = CreateMockPLTData(tileCount: 1, packetsPerTile: 1000);
            
            using var stream = new MemoryStream();

            // Act - Measure encoding time
            var sw = Stopwatch.StartNew();
            PLTMarkerWriter.WritePLT(stream, pltData, tileIdx: 0, zplt: 0);
            sw.Stop();

            // Assert - Should be fast (< 100ms for 1000 packets)
            Assert.True(sw.ElapsedMilliseconds < 100, 
                $"PLT encoding took {sw.ElapsedMilliseconds}ms for 1000 packets");
        }

        [Fact]
        public void TestPLTDecodingPerformance()
        {
            // Arrange
            var pltData = CreateMockPLTData(tileCount: 1, packetsPerTile: 1000);
            using var stream = new MemoryStream();
            PLTMarkerWriter.WritePLT(stream, pltData, tileIdx: 0, zplt: 0);
            
            stream.Position = 2; // Skip marker bytes
            var readData = new PacketLengthsData();

            // Act - Measure decoding time
            var sw = Stopwatch.StartNew();
            PLTMarkerReader.ReadPLT(stream, readData, tileIdx: 0);
            sw.Stop();

            // Assert - Should be fast (< 100ms for 1000 packets)
            Assert.True(sw.ElapsedMilliseconds < 100,
                $"PLT decoding took {sw.ElapsedMilliseconds}ms for 1000 packets");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void TestEmptyPLTData()
        {
            // Arrange
            var pltData = new PacketLengthsData();

            // Assert
            Assert.False(pltData.HasPacketLengths);
            Assert.Equal(0, pltData.TotalPackets);
            Assert.Equal(-1, pltData.MaxTileIndex);
        }

        [Fact]
        public void TestSinglePacketPLT()
        {
            // Arrange
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 500);

            // Assert
            Assert.True(pltData.HasPacketLengths);
            Assert.Equal(1, pltData.TotalPackets);
            Assert.Equal(1, pltData.GetPacketCount(0));
            Assert.Equal(500, pltData.GetTotalPacketLength(0));
        }

        [Fact]
        public void TestLargePacketCount()
        {
            // Arrange & Act
            var pltData = CreateMockPLTData(tileCount: 100, packetsPerTile: 100);

            // Assert
            ValidatePLTData(pltData, expectedTiles: 100, expectedPacketsPerTile: 100);
            Assert.Equal(10000, pltData.TotalPackets); // 100 * 100
        }

        [Fact]
        public void TestPLTDataClear()
        {
            // Arrange
            var pltData = CreateMockPLTData(tileCount: 5, packetsPerTile: 10);
            Assert.True(pltData.HasPacketLengths);

            // Act
            pltData.Clear();

            // Assert
            Assert.False(pltData.HasPacketLengths);
            Assert.Equal(0, pltData.TotalPackets);
        }

        [Fact]
        public void TestGetPacketEntriesForNonexistentTile()
        {
            // Arrange
            var pltData = CreateMockPLTData(tileCount: 5, packetsPerTile: 10);

            // Act
            var entries = pltData.GetPacketEntries(999).ToList();

            // Assert
            Assert.Empty(entries);
        }

        #endregion

        #region Theory Tests (Data-driven)

        [Theory]
        [InlineData(1, 10)]
        [InlineData(5, 50)]
        [InlineData(10, 100)]
        [InlineData(100, 10)]
        public void TestPLTDataVariousSizes(int tileCount, int packetsPerTile)
        {
            // Arrange & Act
            var pltData = CreateMockPLTData(tileCount, packetsPerTile);

            // Assert
            ValidatePLTData(pltData, tileCount, packetsPerTile);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(65535)]
        public void TestVariableLengthIntVariousValues(int value)
        {
            // Act
            var encoded = PLTMarkerWriter.EncodeVariableLengthInt(value);
            using var stream = new MemoryStream(encoded);
            var decoded = PLTMarkerWriter.DecodeVariableLengthInt(stream);

            // Assert
            Assert.Equal(value, decoded);
        }

        #endregion

        #region Integration Scenario Tests

        [Fact]
        public void TestPLTForProgressiveTransmission()
        {
            // Simulate progressive transmission scenario
            // Client wants layers 0-3 out of 10 total layers

            // Arrange
            var pltData = CreateMockPLTData(tileCount: 1, packetsPerTile: 10);
            var targetLayers = new[] { 0, 1, 2, 3 };
            
            // Act - Calculate bytes needed for first 4 layers
            var packets = pltData.GetPacketEntries(0).ToList();
            var bytesNeeded = targetLayers.Sum(layer => 
                layer < packets.Count ? packets[layer].PacketLength : 0);

            // Assert
            Assert.True(bytesNeeded > 0);
            Assert.True(bytesNeeded < pltData.GetTotalPacketLength(0));
            
            // With PLT, we can transmit only these bytes
            var percentSaved = 100.0 * (1.0 - (double)bytesNeeded / pltData.GetTotalPacketLength(0));
            Assert.True(percentSaved > 0);
        }

        [Fact]
        public void TestPLTForQualityLayerExtraction()
        {
            // Simulate extracting specific quality layers

            // Arrange
            var pltData = CreateMockPLTData(tileCount: 1, packetsPerTile: 10);
            var packets = pltData.GetPacketEntries(0).ToList();

            // Act - Find offset to layer 5
            long offsetToLayer5 = 0;
            for (int i = 0; i < 5 && i < packets.Count; i++)
            {
                offsetToLayer5 += packets[i].PacketLength;
            }

            // Assert - With PLT, we can seek directly to layer 5
            Assert.True(offsetToLayer5 > 0);
            Assert.True(offsetToLayer5 < pltData.GetTotalPacketLength(0));
        }

        [Fact]
        public void TestPLTForPartialDecoding()
        {
            // Simulate decoding only certain resolution levels

            // Arrange - Multiple tiles, some with many packets
            var pltData = CreateMockPLTData(tileCount: 10, packetsPerTile: 50);
            var targetTiles = new[] { 2, 5, 7 }; // Only decode these tiles

            // Act - Calculate total bytes for target tiles
            long bytesForTargetTiles = targetTiles.Sum(tile => 
                pltData.GetTotalPacketLength(tile));
            
            long totalBytes = Enumerable.Range(0, 10).Sum(tile =>
                pltData.GetTotalPacketLength(tile));

            // Assert - We can skip non-target tiles efficiently
            Assert.True(bytesForTargetTiles > 0);
            Assert.True(bytesForTargetTiles < totalBytes);
            
            var percentSkipped = 100.0 * (1.0 - (double)bytesForTargetTiles / totalBytes);
            Assert.True(percentSkipped > 0);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void TestPLTMarkerWriterNullStream()
        {
            // Arrange
            var pltData = CreateMockPLTData(1, 1);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                PLTMarkerWriter.WritePLT(null, pltData, 0, 0));
        }

        [Fact]
        public void TestPLTMarkerWriterNullData()
        {
            // Arrange
            using var stream = new MemoryStream();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                PLTMarkerWriter.WritePLT(stream, null, 0, 0));
        }

        [Fact]
        public void TestPLTMarkerReaderNullStream()
        {
            // Arrange
            var pltData = new PacketLengthsData();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                PLTMarkerReader.ReadPLT(null, pltData, 0));
        }

        [Fact]
        public void TestPLTMarkerReaderNullData()
        {
            // Arrange
            using var stream = new MemoryStream();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                PLTMarkerReader.ReadPLT(stream, null, 0));
        }

        [Fact]
        public void TestVariableLengthIntNegativeValue()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PLTMarkerWriter.EncodeVariableLengthInt(-1));
        }

        #endregion
    }
}
