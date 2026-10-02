// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.Collections.Generic;
using System.Linq;
using CoreJ2K.j2k.codestream.metadata;
using CoreJ2K.j2k.codestream.reader;
using CoreJ2K.j2k.codestream.writer;
using System;
using System.IO;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for PLT (Packet Length, tile-part header) marker writing and encoding.
    /// </summary>
    public class PLTMarkerTests
    {
        [Fact]
        public void TestVariableLengthEncoding_SmallValues()
        {
            // Values that fit in 7 bits (0-127)
            Assert.Equal(new byte[] { 0x00 }, PLTMarkerWriter.EncodeVariableLengthInt(0));
            Assert.Equal(new byte[] { 0x01 }, PLTMarkerWriter.EncodeVariableLengthInt(1));
            Assert.Equal(new byte[] { 0x7F }, PLTMarkerWriter.EncodeVariableLengthInt(127));
        }

        [Fact]
        public void TestVariableLengthEncoding_MediumValues()
        {
            // Values that need 2 bytes (128-16383)
            Assert.Equal(new byte[] { 0x81, 0x00 }, PLTMarkerWriter.EncodeVariableLengthInt(128));
            Assert.Equal(new byte[] { 0x81, 0x7F }, PLTMarkerWriter.EncodeVariableLengthInt(255));
            Assert.Equal(new byte[] { 0x82, 0x00 }, PLTMarkerWriter.EncodeVariableLengthInt(256));
            Assert.Equal(new byte[] { 0xFF, 0x7F }, PLTMarkerWriter.EncodeVariableLengthInt(16383));
        }

        [Fact]
        public void TestVariableLengthEncoding_LargeValues()
        {
            // Values that need 3 bytes (16384-2097151)
            Assert.Equal(new byte[] { 0x81, 0x80, 0x00 }, PLTMarkerWriter.EncodeVariableLengthInt(16384));
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0x7F }, PLTMarkerWriter.EncodeVariableLengthInt(2097151));
        }

        [Fact]
        public void TestVariableLengthEncoding_VeryLargeValues()
        {
            // Values that need 4 bytes (2097152-268435455)
            Assert.Equal(new byte[] { 0x81, 0x80, 0x80, 0x00 }, PLTMarkerWriter.EncodeVariableLengthInt(2097152));
        }

        [Fact]
        public void TestVariableLengthEncoding_NegativeValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PLTMarkerWriter.EncodeVariableLengthInt(-1));
        }

        [Fact]
        public void TestVariableLengthDecoding_SmallValues()
        {
            var ms = new MemoryStream(new byte[] { 0x00 });
            Assert.Equal(0, PLTMarkerWriter.DecodeVariableLengthInt(ms));

            ms = new MemoryStream(new byte[] { 0x01 });
            Assert.Equal(1, PLTMarkerWriter.DecodeVariableLengthInt(ms));

            ms = new MemoryStream(new byte[] { 0x7F });
            Assert.Equal(127, PLTMarkerWriter.DecodeVariableLengthInt(ms));
        }

        [Fact]
        public void TestVariableLengthDecoding_MediumValues()
        {
            var ms = new MemoryStream(new byte[] { 0x81, 0x00 });
            Assert.Equal(128, PLTMarkerWriter.DecodeVariableLengthInt(ms));

            ms = new MemoryStream(new byte[] { 0x81, 0x7F });
            Assert.Equal(255, PLTMarkerWriter.DecodeVariableLengthInt(ms));

            ms = new MemoryStream(new byte[] { 0xFF, 0x7F });
            Assert.Equal(16383, PLTMarkerWriter.DecodeVariableLengthInt(ms));
        }

        [Fact]
        public void TestVariableLengthDecoding_LargeValues()
        {
            var ms = new MemoryStream(new byte[] { 0x81, 0x80, 0x00 });
            Assert.Equal(16384, PLTMarkerWriter.DecodeVariableLengthInt(ms));

            ms = new MemoryStream(new byte[] { 0xFF, 0xFF, 0x7F });
            Assert.Equal(2097151, PLTMarkerWriter.DecodeVariableLengthInt(ms));
        }

        [Fact]
        public void TestVariableLengthRoundTrip()
        {
            // Test various values for round-trip encoding/decoding
            var testValues = new[] { 0, 1, 127, 128, 255, 256, 16383, 16384, 65535, 1000000 };

            foreach (var value in testValues)
            {
                var encoded = PLTMarkerWriter.EncodeVariableLengthInt(value);
                var ms = new MemoryStream(encoded);
                var decoded = PLTMarkerWriter.DecodeVariableLengthInt(ms);

                Assert.Equal(value, decoded);
            }
        }

        [Fact]
        public void TestGetEncodedSize()
        {
            Assert.Equal(1, PLTMarkerWriter.GetEncodedSize(0));
            Assert.Equal(1, PLTMarkerWriter.GetEncodedSize(127));
            Assert.Equal(2, PLTMarkerWriter.GetEncodedSize(128));
            Assert.Equal(2, PLTMarkerWriter.GetEncodedSize(16383));
            Assert.Equal(3, PLTMarkerWriter.GetEncodedSize(16384));
            Assert.Equal(3, PLTMarkerWriter.GetEncodedSize(2097151));
            Assert.Equal(4, PLTMarkerWriter.GetEncodedSize(2097152));
        }

        [Fact]
        public void TestWritePLT_SinglePacket()
        {
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 1000);

            using (var ms = new MemoryStream())
            {
                var bytesWritten = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);

                Assert.True(bytesWritten > 0);

                var buffer = ms.ToArray();

                // Verify marker (0xFF58)
                Assert.Equal(0xFF, buffer[0]);
                Assert.Equal(0x58, buffer[1]);

                // Verify Zplt
                Assert.Equal(0, buffer[4]);
            }
        }

        [Fact]
        public void TestWritePLT_MultiplePackets()
        {
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100);
            pltData.AddPacket(0, 200);
            pltData.AddPacket(0, 300);

            using (var ms = new MemoryStream())
            {
                var bytesWritten = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);

                Assert.True(bytesWritten > 0);

                var buffer = ms.ToArray();

                // Verify marker
                Assert.Equal(0xFF, buffer[0]);
                Assert.Equal(0x58, buffer[1]);

                // Calculate expected Lplt
                var ipltSize = PLTMarkerWriter.GetEncodedSize(100) +
                              PLTMarkerWriter.GetEncodedSize(200) +
                              PLTMarkerWriter.GetEncodedSize(300);
                var expectedLplt = ipltSize + 3; // +3 for Lplt itself and Zplt

                var actualLplt = (buffer[2] << 8) | buffer[3];
                Assert.Equal(expectedLplt, actualLplt);
            }
        }

        [Fact]
        public void TestWritePLT_NoPackets()
        {
            var pltData = new PacketLengthsData();

            using (var ms = new MemoryStream())
            {
                var bytesWritten = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);

                Assert.Equal(0, bytesWritten);
                Assert.Equal(0, ms.Length);
            }
        }

        [Fact]
        public void TestWritePLT_DifferentTiles()
        {
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100);
            pltData.AddPacket(1, 200);
            pltData.AddPacket(2, 300);

            using (var ms = new MemoryStream())
            {
                // Write PLT for tile 1
                var bytesWritten = PLTMarkerWriter.WritePLT(ms, pltData, 1, 0);

                Assert.True(bytesWritten > 0);

                // Verify only tile 1's packet is written
                var buffer = ms.ToArray();
                var ipltSize = bytesWritten - 5; // Remove marker(2) + Lplt(2) + Zplt(1)

                // Should be size of encoding 200
                Assert.Equal(PLTMarkerWriter.GetEncodedSize(200), ipltSize);
            }
        }

        [Fact]
        public void TestCalculatePLTSize()
        {
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100);
            pltData.AddPacket(0, 200);
            pltData.AddPacket(0, 300);

            var size = PLTMarkerWriter.CalculatePLTSize(pltData, 0);

            // Expected: marker(2) + Lplt(2) + Zplt(1) + encoded packet lengths
            var ipltSize = PLTMarkerWriter.GetEncodedSize(100) +
                          PLTMarkerWriter.GetEncodedSize(200) +
                          PLTMarkerWriter.GetEncodedSize(300);
            var expectedSize = 5 + ipltSize;

            Assert.Equal(expectedSize, size);
        }

        [Fact]
        public void TestCalculatePLTSize_NoPackets()
        {
            var pltData = new PacketLengthsData();

            var size = PLTMarkerWriter.CalculatePLTSize(pltData, 0);

            Assert.Equal(0, size);
        }

        [Fact]
        public void TestWritePLT_NullArguments()
        {
            var pltData = new PacketLengthsData();
            using (var ms = new MemoryStream())
            {
                Assert.Throws<ArgumentNullException>(() =>
                    PLTMarkerWriter.WritePLT(null, pltData, 0, 0));

                Assert.Throws<ArgumentNullException>(() =>
                    PLTMarkerWriter.WritePLT(ms, null, 0, 0));
            }
        }

        [Fact]
        public void TestVariableLengthDecoding_UnexpectedEndOfStream()
        {
            // Stream with continuation bit set but no more bytes
            var ms = new MemoryStream(new byte[] { 0x81 });

            Assert.Throws<EndOfStreamException>(() =>
                PLTMarkerWriter.DecodeVariableLengthInt(ms));
        }

        [Fact]
        public void TestPLTMarkerFormat()
        {
            // Test that the PLT marker format is correct according to JPEG 2000 spec
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 500);

            using (var ms = new MemoryStream())
            {
                PLTMarkerWriter.WritePLT(ms, pltData, 0, 5);

                var buffer = ms.ToArray();

                // Marker: 0xFF58
                Assert.Equal(0xFF, buffer[0]);
                Assert.Equal(0x58, buffer[1]);

                // Lplt: 2 bytes (big-endian)
                var lplt = (buffer[2] << 8) | buffer[3];
                Assert.True(lplt > 3); // Must include at least Zplt + some Iplt

                // Zplt: 1 byte
                Assert.Equal(5, buffer[4]);

                // Iplt: variable-length encoded packet length
                // Should start at buffer[5]
            }
        }

        [Fact]
        public void TestLargePacketLength()
        {
            // Test with a large packet length that requires multiple bytes
            var pltData = new PacketLengthsData();
            pltData.AddPacket(0, 100000);

            using (var ms = new MemoryStream())
            {
                var bytesWritten = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);

                Assert.True(bytesWritten > 0);

                // Read back and verify
                ms.Seek(0, SeekOrigin.Begin);

                // Skip marker, Lplt, and Zplt
                ms.Seek(5, SeekOrigin.Begin);

                // Decode the packet length
                var decodedLength = PLTMarkerWriter.DecodeVariableLengthInt(ms);

                Assert.Equal(100000, decodedLength);
            }
        }

        /// <summary>Splits written PLT marker segments into (Lplt, Zplt, packet lengths); fails if the bytes are anything but PLT segments.</summary>
        private static List<(int Lplt, int Zplt, List<int> Lengths)> ReadSegments(byte[] buffer)
        {
            var segments = new List<(int, int, List<int>)>();
            var pos = 0;
            while (pos < buffer.Length)
            {
                Assert.Equal(0xFF, buffer[pos]);
                Assert.Equal(0x58, buffer[pos + 1]);
                var lplt = (buffer[pos + 2] << 8) | buffer[pos + 3];
                var end = pos + 2 + lplt;
                Assert.True(end <= buffer.Length, "a PLT segment runs past the end of what was written");

                // Each segment's lengths must end exactly with the segment: one split between two segments would not.
                var lengths = new List<int>();
                for (int at = pos + 5, value = 0; at < end; at++)
                {
                    value = (value << 7) | (buffer[at] & 0x7F);
                    if ((buffer[at] & 0x80) == 0) { lengths.Add(value); value = 0; }
                    else Assert.True(at + 1 < end, "a packet length is split across two PLT segments");
                }
                segments.Add((lplt, buffer[pos + 4], lengths));
                pos = end;
            }
            return segments;
        }

        [Fact]
        public void TestWritePLT_ASegmentFilledExactly_IsNotSplit()
        {
            // 65532 one-byte lengths fill Lplt's 16 bits exactly (65535 = 3 + 65532).
            var pltData = new PacketLengthsData();
            for (var i = 0; i < 65532; i++) pltData.AddPacket(0, i % 100);

            using var ms = new MemoryStream();
            var written = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);

            var segments = ReadSegments(ms.ToArray());
            var segment = Assert.Single(segments);
            Assert.Equal(65535, segment.Lplt);
            Assert.Equal(65532, segment.Lengths.Count);
            Assert.Equal(65532 + 5, written);
        }

        [Fact]
        public void TestWritePLT_OneLengthOverTheLimit_StartsASecondSegment()
        {
            var pltData = new PacketLengthsData();
            for (var i = 0; i < 65533; i++) pltData.AddPacket(0, 7);

            using var ms = new MemoryStream();
            var written = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);

            var segments = ReadSegments(ms.ToArray());
            Assert.Equal(2, segments.Count);
            Assert.Equal(new[] { 0, 1 }, segments.Select(segment => segment.Zplt).ToArray());
            Assert.Equal(65532, segments[0].Lengths.Count);
            Assert.Single(segments[1].Lengths);
            Assert.Equal(written, ms.Length);
        }

        [Fact]
        public void TestWritePLT_LengthsOfEverySize_AreNeverSplitAndReadBackInOrder()
        {
            // Lengths of one to five bytes, in a mix that makes a segment end part-way through a multi-byte length.
            var rnd = new Random(4);
            var expected = new List<int>();
            var pltData = new PacketLengthsData();
            for (var i = 0; i < 120000; i++)
            {
                var value = rnd.Next(4) switch { 0 => rnd.Next(0, 128), 1 => rnd.Next(128, 16384), 2 => rnd.Next(16384, 2097152), _ => rnd.Next(2097152, 268435456) };
                expected.Add(value);
                pltData.AddPacket(0, value);
            }
            pltData.AddPacket(1, 999); // another tile's lengths are not part of tile 0's markers

            using var ms = new MemoryStream();
            var written = PLTMarkerWriter.WritePLT(ms, pltData, 0, 0);
            var buffer = ms.ToArray();

            var segments = ReadSegments(buffer);
            Assert.True(segments.Count > 4);
            Assert.True(segments.All(segment => segment.Lplt <= 65535));
            Assert.Equal(Enumerable.Range(0, segments.Count), segments.Select(segment => segment.Zplt));
            Assert.Equal(expected, segments.SelectMany(segment => segment.Lengths).ToList());

            // The reader puts them back together, and the size calculation agrees with what was written.
            var read = new PacketLengthsData();
            PLTMarkerReader.ReadAllPLTMarkers(new MemoryStream(buffer), read, 0);
            Assert.Equal(expected, read.GetPacketEntries(0).Select(entry => entry.PacketLength).ToList());
            Assert.Equal(written, buffer.Length);
            Assert.Equal(written, PLTMarkerWriter.CalculatePLTSize(pltData, 0));
        }

        [Fact]
        public void TestWritePLT_NumbersSegmentsFromTheGivenIndex_AndRefusesToPassZplt255()
        {
            var pltData = new PacketLengthsData();
            for (var i = 0; i < 70000; i++) pltData.AddPacket(0, 1); // two segments

            using (var ms = new MemoryStream())
            {
                PLTMarkerWriter.WritePLT(ms, pltData, 0, 254);
                Assert.Equal(new[] { 254, 255 }, ReadSegments(ms.ToArray()).Select(segment => segment.Zplt).ToArray());
            }

            using (var ms = new MemoryStream())
            {
                // Zplt is one byte: a third index would be 256.
                Assert.Throws<InvalidOperationException>(() => PLTMarkerWriter.WritePLT(ms, pltData, 0, 255));
            }
        }
    }
}
