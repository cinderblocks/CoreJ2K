// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using Xunit;
using CoreJ2K.j2k.codestream.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for Tile-part Lengths (TLM) marker support.
    /// </summary>
    public class TilePartLengthsTests
    {
        [Fact]
        public void TilePartLengthsData_DefaultConstructor_HasNoData()
        {
            var tlm = new TilePartLengthsData();

            Assert.False(tlm.HasTilePartLengths);
            Assert.Equal(0, tlm.TotalTileParts);
            Assert.Empty(tlm.TilePartEntries);
            Assert.Equal(-1, tlm.MaxTileIndex);
            Assert.Equal(0, tlm.TotalSize);
        }

        [Fact]
        public void TilePartLengthsData_AddTilePart_AddsCorrectly()
        {
            var tlm = new TilePartLengthsData();

            tlm.AddTilePart(0, 0, 1024);

            Assert.True(tlm.HasTilePartLengths);
            Assert.Single(tlm.TilePartEntries);
            Assert.Equal(0, tlm.TilePartEntries[0].TileIndex);
            Assert.Equal(0, tlm.TilePartEntries[0].TilePartIndex);
            Assert.Equal(1024, tlm.TilePartEntries[0].TilePartLength);
        }

        [Fact]
        public void TilePartLengthsData_AddMultipleTileParts_TracksAll()
        {
            var tlm = new TilePartLengthsData();

            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(1, 0, 2000);

            Assert.Equal(3, tlm.TotalTileParts);
            Assert.Equal(4500, tlm.TotalSize);
            Assert.Equal(1, tlm.MaxTileIndex);
        }

        [Fact]
        public void TilePartLengthsData_GetTilePartEntries_ReturnsCorrectTile()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(1, 0, 2000);
            tlm.AddTilePart(1, 1, 2500);

            var tile0Entries = tlm.GetTilePartEntries(0).ToList();
            var tile1Entries = tlm.GetTilePartEntries(1).ToList();

            Assert.Equal(2, tile0Entries.Count);
            Assert.Equal(2, tile1Entries.Count);
            Assert.All(tile0Entries, e => Assert.Equal(0, e.TileIndex));
            Assert.All(tile1Entries, e => Assert.Equal(1, e.TileIndex));
        }

        [Fact]
        public void TilePartLengthsData_GetTotalTileLength_CalculatesCorrectly()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(1, 0, 2000);

            var tile0Length = tlm.GetTotalTileLength(0);
            var tile1Length = tlm.GetTotalTileLength(1);

            Assert.Equal(2500, tile0Length);
            Assert.Equal(2000, tile1Length);
        }

        [Fact]
        public void TilePartLengthsData_GetTilePartCount_ReturnsCorrectCount()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(0, 2, 1200);
            tlm.AddTilePart(1, 0, 2000);

            Assert.Equal(3, tlm.GetTilePartCount(0));
            Assert.Equal(1, tlm.GetTilePartCount(1));
            Assert.Equal(0, tlm.GetTilePartCount(2));
        }

        [Fact]
        public void TilePartLengthsData_Clear_RemovesAllEntries()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(1, 0, 2000);

            tlm.Clear();

            Assert.False(tlm.HasTilePartLengths);
            Assert.Equal(0, tlm.TotalTileParts);
            Assert.Empty(tlm.TilePartEntries);
        }

        [Fact]
        public void TilePartLengthsData_ToString_FormatsCorrectly()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(1, 0, 2000);

            var str = tlm.ToString();

            Assert.Contains("3 tile-parts", str);
            Assert.Contains("2 tiles", str);
            Assert.Contains("4,500 bytes", str);
        }

        [Fact]
        public void TilePartLengthsData_ToString_EmptyData()
        {
            var tlm = new TilePartLengthsData();

            var str = tlm.ToString();

            Assert.Contains("No TLM data", str);
        }

        [Fact]
        public void TilePartEntry_ToString_FormatsCorrectly()
        {
            var entry = new TilePartEntry
            {
                TileIndex = 5,
                TilePartIndex = 2,
                TilePartLength = 12345
            };

            var str = entry.ToString();

            Assert.Contains("Tile 5", str);
            Assert.Contains("Part 2", str);
            Assert.Contains("12,345 bytes", str);
        }

        [Fact]
        public void TilePartLengthsData_GetStatistics_ReturnsNull_WhenEmpty()
        {
            var tlm = new TilePartLengthsData();

            var stats = tlm.GetStatistics();

            Assert.Null(stats);
        }

        [Fact]
        public void TilePartLengthsData_GetStatistics_CalculatesCorrectly()
        {
            var tlm = new TilePartLengthsData();
            // Tile 0: 3 parts, 4500 bytes total
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(0, 2, 2000);
            // Tile 1: 2 parts, 3000 bytes total
            tlm.AddTilePart(1, 0, 1000);
            tlm.AddTilePart(1, 1, 2000);

            var stats = tlm.GetStatistics();

            Assert.NotNull(stats);
            Assert.Equal(5, stats.TotalTileParts);
            Assert.Equal(2, stats.TotalTiles);
            Assert.Equal(7500, stats.TotalSize);
            
            // Tile lengths
            Assert.Equal(4500, stats.TileLengths[0]);
            Assert.Equal(3000, stats.TileLengths[1]);
            Assert.Equal(3750, stats.AverageTileLength);
            Assert.Equal(3000, stats.MinTileLength);
            Assert.Equal(4500, stats.MaxTileLength);
            
            // Tile-part counts
            Assert.Equal(3, stats.TilePartCounts[0]);
            Assert.Equal(2, stats.TilePartCounts[1]);
            Assert.Equal(3, stats.AverageTilePartCount); // Rounded from 2.5
            Assert.Equal(2, stats.MinTilePartCount);
            Assert.Equal(3, stats.MaxTilePartCount);
        }

        [Fact]
        public void TilePartStatistics_ToString_FormatsCorrectly()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1500);
            tlm.AddTilePart(1, 0, 2000);

            var stats = tlm.GetStatistics();
            var str = stats.ToString();

            Assert.Contains("Tiles: 2", str);
            Assert.Contains("Tile-parts: 3", str);
            Assert.Contains("Avg tile size:", str);
            Assert.Contains("Avg parts/tile:", str);
        }

        [Fact]
        public void TilePartLengthsData_LargeTileSet_HandlesCorrectly()
        {
            var tlm = new TilePartLengthsData();

            // Simulate 100 tiles with varying tile-parts
            for (var tileIdx = 0; tileIdx < 100; tileIdx++)
            {
                var numParts = (tileIdx % 5) + 1; // 1-5 parts per tile
                for (var partIdx = 0; partIdx < numParts; partIdx++)
                {
                    tlm.AddTilePart(tileIdx, partIdx, 1000 + tileIdx * 10 + partIdx);
                }
            }

            Assert.Equal(300, tlm.TotalTileParts); // Sum of 1+2+3+4+5 repeated 20 times
            Assert.Equal(99, tlm.MaxTileIndex);
            
            var stats = tlm.GetStatistics();
            Assert.NotNull(stats);
            Assert.Equal(100, stats.TotalTiles);
        }

        [Fact]
        public void TilePartLengthsData_GetTilePartEntries_EmptyTile_ReturnsEmpty()
        {
            var tlm = new TilePartLengthsData();
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(2, 0, 2000);

            var tile1Entries = tlm.GetTilePartEntries(1).ToList();

            Assert.Empty(tile1Entries);
        }

        [Fact]
        public void TilePartLengthsData_MultiplePartsPerTile_OrderPreserved()
        {
            var tlm = new TilePartLengthsData();
            
            tlm.AddTilePart(0, 0, 1000);
            tlm.AddTilePart(0, 1, 1100);
            tlm.AddTilePart(0, 2, 1200);

            var entries = tlm.GetTilePartEntries(0).ToList();

            Assert.Equal(3, entries.Count);
            Assert.Equal(0, entries[0].TilePartIndex);
            Assert.Equal(1, entries[1].TilePartIndex);
            Assert.Equal(2, entries[2].TilePartIndex);
        }

        [Fact]
        public void TilePartLengthsData_TotalSize_LargeValues_NoOverflow()
        {
            var tlm = new TilePartLengthsData();

            // Add tile-parts that sum to more than int.MaxValue
            for (var i = 0; i < 3; i++)
            {
                tlm.AddTilePart(i, 0, int.MaxValue / 2);
            }

            // TotalSize is long, so it should handle this
            Assert.True(tlm.TotalSize > int.MaxValue);
            Assert.Equal((long)int.MaxValue / 2 * 3, tlm.TotalSize);
        }
    }
}
