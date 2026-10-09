// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Some encoders write the index of the last tile-part of a tile (counting from 0) in the TNsot field, which holds the number of
    /// them: every tile then has one more tile-part than it announces (OpenJPEG warns "Non conformant codestream TPsot==TNsot" and reads
    /// them all). The reader stopped at the announced count and left out the last tile-part of every tile.
    /// </summary>
    public class TilePartCountTests
    {
        private static CodingScenario Scenario() =>
            CodingScenario.FromSeed(1).With(s =>
            {
                s.OffsetX = 0; s.OffsetY = 0; s.TileOffsetX = 0; s.TileOffsetY = 0; s.Precincts = Array.Empty<int>();
                s.Bypass = false; s.Terminate = false; s.ResetContexts = false; s.VerticallyCausal = false; s.SegmentationSymbols = false;
                s.Sop = false; s.Eph = false; s.Plt = false; s.Tlm = false; s.PackedHeadersInMain = false; s.PackedHeadersInTile = false;
                s.Signed = false; s.Components = 1; s.ColourTransform = false; s.Bits = 8; s.Width = 90; s.Height = 70; s.Levels = 3;
                s.TileWidth = 48; s.TileHeight = 40; s.BlockWidth = 32; s.BlockHeight = 32; s.Progression = "res"; s.Roi = false;
                s.FileFormat = false; s.Layers = 0; s.Lossless = true; s.Threads = 1; s.GuardBits = 2;
                s.PacketsPerTilePart = 3;
            });

        [Fact]
        public void TilePartsBeyondTheAnnouncedCount_AreRead()
        {
            var scenario = Scenario();
            var image = scenario.MakeImage();
            var data = ScenarioRunner.Encode(scenario, image);

            // every tile has several tile-parts, and announces them all
            var patched = (byte[])data.Clone();
            var announced = 0;
            for (var i = 0; i + 11 < patched.Length; i++)
            {
                if (patched[i] != 0xFF || patched[i + 1] != 0x90 || patched[i + 2] != 0 || patched[i + 3] != 10) continue;
                if (patched[i + 11] > 1) { patched[i + 11]--; announced++; }
            }
            Assert.True(announced > scenario.TileCount, "The test needs tiles with several tile-parts.");

            var (width, height, samples) = ScenarioRunner.Decode(patched);
            Assert.Equal((scenario.Width, scenario.Height), (width, height));
            var diff = ScenarioRunner.Compare(image.OffsetBinary(), samples, width, scenario.Bits);
            Assert.False(diff.Any, diff.ToString());
        }
    }
}
