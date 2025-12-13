// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.codestream.metadata;
using CoreJ2K.j2k.codestream.reader;
using CoreJ2K.j2k.codestream.writer;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for PLT marker reading functionality.
    /// Tests decoding, validation, and round-trip encoding/decoding.
    /// </summary>
    public class PLTMarkerReaderTests
    {
        [Fact]
        public void ReadPLT_WithSinglePacket_ReadsCorrectly()
        {
            // Arrange: Create a PLT marker with one packet of length 100
            var stream = new MemoryStream();
            var pltDataWrite = new PacketLengthsData();
            pltDataWrite.AddPacket(0, 100);
            PLTMarkerWriter.WritePLT(stream, pltDataWrite, 0, 0);
            
            // Rewind past marker
            stream.Position = 2; // Skip marker bytes
            
            // Act: Read it back
            var pltDataRead = new PacketLengthsData();
            var bytesRead = PLTMarkerReader.ReadPLT(stream, pltDataRead, 0);
            
            // Assert
            Assert.Equal(1, pltDataRead.GetPacketCount(0));
            Assert.Equal(100, pltDataRead.GetPacketEntries(0).First().PacketLength);
            Assert.True(bytesRead > 0);
        }

        [Fact]
        public void ReadPLT_WithMultiplePackets_ReadsAllCorrectly()
        {
            // Arrange
            var stream = new MemoryStream();
            var pltDataWrite = new PacketLengthsData();
            pltDataWrite.AddPacket(0, 100);
            pltDataWrite.AddPacket(0, 200);
            pltDataWrite.AddPacket(0, 300);
            PLTMarkerWriter.WritePLT(stream, pltDataWrite, 0, 0);
            
            stream.Position = 2;
            
            // Act
            var pltDataRead = new PacketLengthsData();
            PLTMarkerReader.ReadPLT(stream, pltDataRead, 0);
            
            // Assert
            Assert.Equal(3, pltDataRead.GetPacketCount(0));
            var packets = pltDataRead.GetPacketEntries(0).ToList();
            Assert.Equal(100, packets[0].PacketLength);
            Assert.Equal(200, packets[1].PacketLength);
            Assert.Equal(300, packets[2].PacketLength);
        }

        [Fact]
        public void ReadPLT_RoundTrip_ProducesSameData()
        {
            // Arrange: Create various packet lengths
            var originalData = new PacketLengthsData();
            var testLengths = new[] { 1, 127, 128, 255, 256, 16383, 16384, 65535 };
            
            foreach (var length in testLengths)
            {
                originalData.AddPacket(0, length);
            }
            
            // Act: Write then read
            var stream = new MemoryStream();
            PLTMarkerWriter.WritePLT(stream, originalData, 0, 0);
            stream.Position = 2; // Skip marker
            
            var readData = new PacketLengthsData();
            PLTMarkerReader.ReadPLT(stream, readData, 0);
            
            // Assert
            Assert.Equal(originalData.GetPacketCount(0), readData.GetPacketCount(0));
            var readPackets = readData.GetPacketEntries(0).ToList();
            for (var i = 0; i < testLengths.Length; i++)
            {
                Assert.Equal(testLengths[i], readPackets[i].PacketLength);
            }
        }

        [Fact]
        public void DecodeVariableLengthInt_WithZero_ReturnsZero()
        {
            // Arrange
            var stream = new MemoryStream(new byte[] { 0x00 });
            
            // Act
            var result = PLTMarkerReader.DecodeVariableLengthInt(stream);
            
            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void DecodeVariableLengthInt_WithSingleByte_DecodesCorrectly()
        {
            // Arrange: 127 = 0x7F
            var stream = new MemoryStream(new byte[] { 0x7F });
            
            // Act
            var result = PLTMarkerReader.DecodeVariableLengthInt(stream);
            
            // Assert
            Assert.Equal(127, result);
        }

        [Fact]
        public void DecodeVariableLengthInt_WithTwoBytes_DecodesCorrectly()
        {
            // Arrange: 128 = 0x81 0x00 (10000001 00000000 in variable-length encoding)
            var stream = new MemoryStream(new byte[] { 0x81, 0x00 });
            
            // Act
            var result = PLTMarkerReader.DecodeVariableLengthInt(stream);
            
            // Assert
            Assert.Equal(128, result);
        }

        [Fact]
        public void DecodeVariableLengthInt_WithLargeValue_DecodesCorrectly()
        {
            // Arrange: 16383 = 0xFF 0x7F
            var stream = new MemoryStream(new byte[] { 0xFF, 0x7F });
            
            // Act
            var result = PLTMarkerReader.DecodeVariableLengthInt(stream);
            
            // Assert
            Assert.Equal(16383, result);
        }

        [Fact]
        public void DecodeVariableLengthInt_WithThreeBytes_DecodesCorrectly()
        {
            // Arrange: 16384 = 0x81 0x80 0x00
            var stream = new MemoryStream(new byte[] { 0x81, 0x80, 0x00 });
            
            // Act
            var result = PLTMarkerReader.DecodeVariableLengthInt(stream);
            
            // Assert
            Assert.Equal(16384, result);
        }

        [Fact]
        public void DecodeVariableLengthInt_WithEndOfStream_ThrowsException()
        {
            // Arrange: Stream with incomplete variable-length int
            var stream = new MemoryStream(new byte[] { 0x81 }); // Continuation bit set but no next byte
            
            // Act & Assert
            Assert.Throws<EndOfStreamException>(() => 
                PLTMarkerReader.DecodeVariableLengthInt(stream));
        }

        [Fact]
        public void DecodeVariableLengthInt_MatchesEncode_ForAllTestValues()
        {
            // Arrange
            var testValues = new[] { 0, 1, 127, 128, 255, 256, 16383, 16384, 32767, 65535 };
            
            foreach (var value in testValues)
            {
                // Act: Encode then decode
                var encoded = PLTMarkerWriter.EncodeVariableLengthInt(value);
                var stream = new MemoryStream(encoded);
                var decoded = PLTMarkerReader.DecodeVariableLengthInt(stream);
                
                // Assert
                Assert.Equal(value, decoded);
            }
        }

        [Fact]
        public void ReadPLT_WithNullStream_ThrowsArgumentNullException()
        {
            // Arrange
            var pltData = new PacketLengthsData();
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                PLTMarkerReader.ReadPLT(null, pltData, 0));
        }

        [Fact]
        public void ReadPLT_WithNullPLTData_ThrowsArgumentNullException()
        {
            // Arrange
            var stream = new MemoryStream();
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                PLTMarkerReader.ReadPLT(stream, null, 0));
        }

        [Fact]
        public void ReadPLT_WithMultipleTiles_StoresInCorrectTile()
        {
            // Arrange
            var pltData = new PacketLengthsData();
            
            // Write packets for tile 0
            var stream0 = new MemoryStream();
            var temp0 = new PacketLengthsData();
            temp0.AddPacket(0, 100);
            temp0.AddPacket(0, 200);
            PLTMarkerWriter.WritePLT(stream0, temp0, 0, 0);
            
            // Write packets for tile 1
            var stream1 = new MemoryStream();
            var temp1 = new PacketLengthsData();
            temp1.AddPacket(1, 300);
            temp1.AddPacket(1, 400);
            PLTMarkerWriter.WritePLT(stream1, temp1, 1, 0);
            
            // Act: Read both
            stream0.Position = 2;
            stream1.Position = 2;
            PLTMarkerReader.ReadPLT(stream0, pltData, 0);
            PLTMarkerReader.ReadPLT(stream1, pltData, 1);
            
            // Assert
            Assert.Equal(2, pltData.GetPacketCount(0));
            Assert.Equal(2, pltData.GetPacketCount(1));
            var packets0 = pltData.GetPacketEntries(0).ToList();
            var packets1 = pltData.GetPacketEntries(1).ToList();
            Assert.Equal(100, packets0[0].PacketLength);
            Assert.Equal(200, packets0[1].PacketLength);
            Assert.Equal(300, packets1[0].PacketLength);
            Assert.Equal(400, packets1[1].PacketLength);
        }

        [Fact]
        public void ValidatePacketLengths_WithValidData_ReturnsTrue()
        {
            // Arrange
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100);
            pltData.AddPacket(0, 200);
            
            // Act
            var isValid = PLTMarkerReader.ValidatePacketLengths(pltData, 0);
            
            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidatePacketLengths_WithEmptyData_ReturnsFalse()
        {
            // Arrange
            var pltData = new PacketLengthsData();
            
            // Act
            var isValid = PLTMarkerReader.ValidatePacketLengths(pltData, 0);
            
            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidatePacketLengths_WithNullData_ReturnsFalse()
        {
            // Act
            var isValid = PLTMarkerReader.ValidatePacketLengths(null, 0);
            
            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ReadPLT_WithLargeNumberOfPackets_HandlesCorrectly()
        {
            // Arrange: Create 1000 packets
            var originalData = new PacketLengthsData();
            for (var i = 0; i < 1000; i++)
            {
                originalData.AddPacket(0, 100 + i);
            }
            
            // Act: Write and read
            var stream = new MemoryStream();
            PLTMarkerWriter.WritePLT(stream, originalData, 0, 0);
            stream.Position = 2;
            
            var readData = new PacketLengthsData();
            PLTMarkerReader.ReadPLT(stream, readData, 0);
            
            // Assert
            Assert.Equal(1000, readData.GetPacketCount(0));
            var packets = readData.GetPacketEntries(0).ToList();
            for (var i = 0; i < 1000; i++)
            {
                Assert.Equal(100 + i, packets[i].PacketLength);
            }
        }

        [Fact]
        public void ReadPLT_ReturnsCorrectByteCount()
        {
            // Arrange
            var stream = new MemoryStream();
            var pltDataWrite = new PacketLengthsData();
            pltDataWrite.AddPacket(0, 128); // Requires 2 bytes in variable-length encoding
            
            var totalWritten = PLTMarkerWriter.WritePLT(stream, pltDataWrite, 0, 0);
            stream.Position = 2; // Skip marker
            
            // Act
            var pltDataRead = new PacketLengthsData();
            var bytesRead = PLTMarkerReader.ReadPLT(stream, pltDataRead, 0);
            
            // Assert
            // totalWritten includes marker (2 bytes), bytesRead doesn't
            Assert.Equal(totalWritten - 2, bytesRead);
        }

        [Fact]
        public void ReadPLT_WithEdgeCaseValues_HandlesCorrectly()
        {
            // Arrange: Test boundary values
            var edgeCases = new[] 
            { 
                0,           // Minimum
                1,           // Smallest non-zero
                127,         // Max single byte (0x7F)
                128,         // Min two bytes (0x81 0x00)
                16383,       // Max two bytes (0xFF 0x7F)
                16384,       // Min three bytes
                65535        // Max ushort
            };
            
            var originalData = new PacketLengthsData();
            foreach (var value in edgeCases)
            {
                originalData.AddPacket(0, value);
            }
            
            // Act
            var stream = new MemoryStream();
            PLTMarkerWriter.WritePLT(stream, originalData, 0, 0);
            stream.Position = 2;
            
            var readData = new PacketLengthsData();
            PLTMarkerReader.ReadPLT(stream, readData, 0);
            
            // Assert
            Assert.Equal(edgeCases.Length, readData.GetPacketCount(0));
            var packets = readData.GetPacketEntries(0).ToList();
            for (var i = 0; i < edgeCases.Length; i++)
            {
                Assert.Equal(edgeCases[i], packets[i].PacketLength);
            }
        }
    }
}
