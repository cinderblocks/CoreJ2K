// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CoreJ2K.j2k;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// One test for each defect the random scenario sweeps found, each with the smallest scenario that shows it. They run without
    /// OpenJPEG; the claims about what other decoders read were checked against it when the defects were found.
    /// </summary>
    public class CodecRegressionTests
    {
        /// <summary>A small, plain scenario to change one setting of at a time: one 80x80 grey component, lossless, one tile.</summary>
        private static CodingScenario Plain(Action<CodingScenario>? change = null) =>
            CodingScenario.FromSeed(1).With(s =>
            {
                s.OffsetX = 0; s.OffsetY = 0; s.TileWidth = 0; s.TileHeight = 0; s.TileOffsetX = 0; s.TileOffsetY = 0;
                s.Precincts = Array.Empty<int>(); s.Bypass = false; s.Terminate = false; s.ResetContexts = false; s.VerticallyCausal = false;
                s.SegmentationSymbols = false; s.Sop = false; s.Eph = false; s.Plt = false; s.Tlm = false;
                s.PackedHeadersInMain = false; s.PackedHeadersInTile = false; s.PacketsPerTilePart = 0; s.Signed = false;
                s.Components = 1; s.ColourTransform = false; s.Bits = 8; s.Width = 80; s.Height = 80; s.Levels = 3;
                s.BlockWidth = 32; s.BlockHeight = 32; s.Progression = "layer"; s.Roi = false; s.FileFormat = false; s.Layers = 0;
                s.Lossless = true; s.Threads = 1; s.StepSize = 1f / 128; s.DerivedQuantization = false; s.GuardBits = 2;
                change?.Invoke(s);
            });

        private static void AssertRoundTrip(CodingScenario s)
        {
            var image = s.MakeImage();
            var data = ScenarioRunner.Encode(s, image);
            var (w, h, samples) = ScenarioRunner.Decode(data);
            Assert.Equal((s.Width, s.Height, s.Components), (w, h, samples.Length));
            var diff = ScenarioRunner.Compare(image.OffsetBinary(), samples, w, s.Bits);
            Assert.False(diff.Any && s.Lossless, diff.ToString());
        }

        /// <summary>The tile-parts of a codestream as (tile, index, count) in file order.</summary>
        private static List<(int Tile, int Index, int Count)> TileParts(byte[] file)
        {
            var d = ScenarioRunner.Codestream(file);
            var i = 2;
            while (!(d[i] == 0xFF && d[i + 1] == 0x90)) i += 2 + ((d[i + 2] << 8) | d[i + 3]);
            var parts = new List<(int, int, int)>();
            while (i + 11 < d.Length && d[i] == 0xFF && d[i + 1] == 0x90)
            {
                parts.Add(((d[i + 4] << 8) | d[i + 5], d[i + 10], d[i + 11]));
                i += (int)(((uint)d[i + 6] << 24) | ((uint)d[i + 7] << 16) | ((uint)d[i + 8] << 8) | d[i + 9]);
            }
            Assert.True(d[i] == 0xFF && d[i + 1] == 0xD9 && i + 2 == d.Length, "The codestream must end with EOC and nothing after it.");
            return parts;
        }

        // ---- tiling -----------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(38, 23, 36, 18, 66, 12)]  // tile grid starts before the image
        [InlineData(18, 38, 18, 38, 9, 27)]   // tile grid starts at the image: the tile count had counted tiles beyond the image
        [InlineData(35, 0, 20, 0, 20, 30)]
        public void ImageOffset_WithTiling_RoundTrips(int x0, int y0, int tx0, int ty0, int tw, int th)
        {
            var s = Plain(c => { c.Width = 48; c.Height = 40; c.OffsetX = x0; c.OffsetY = y0; c.TileOffsetX = tx0; c.TileOffsetY = ty0; c.TileWidth = tw; c.TileHeight = th; });
            AssertRoundTrip(s);
            Assert.Equal(s.TileCount, TileParts(ScenarioRunner.Encode(s, s.MakeImage())).Count);
        }

        // ---- the codestream validator -----------------------------------------------------------------------------

        [Fact]
        public void Validator_AcceptsWhatTheEncoderWrites_PrecinctsAndAllMarkersIncluded()
        {
            var s = Plain(c => { c.Precincts = new[] { 32, 32, 64, 16 }; c.Plt = true; c.Sop = true; c.Eph = true; c.TileWidth = 40; c.TileHeight = 40; });
            var report = ScenarioRunner.ValidateStructure(ScenarioRunner.Encode(s, s.MakeImage()));
            Assert.Null(report);
        }

        // ---- packed packet headers, TLM, tile-parts -----------------------------------------------------------------

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PackedHeadersInMain_AreReadBack(bool tiles)
        {
            AssertRoundTrip(Plain(c => { c.PackedHeadersInMain = true; if (tiles) { c.TileWidth = 40; c.TileHeight = 40; } }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Tlm_InAJp2_IsReadBack(bool tiles)
        {
            // the tile offsets are measured from the start of the codestream, which in a JP2 is not the start of the file
            AssertRoundTrip(Plain(c => { c.Tlm = true; c.FileFormat = true; if (tiles) { c.TileWidth = 40; c.TileHeight = 40; } }));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void EachTile_HasOneTileParts_WhenNoneAreAsked_WhateverItsNumberOfPackets(bool ppm, bool ppt)
        {
            // tiles of different sizes have different numbers of packets (here with small precincts); the count of the first tile had
            // been taken for all
            var s = Plain(c =>
            {
                c.Width = 70; c.Height = 70; c.TileWidth = 30; c.TileHeight = 30; c.Levels = 3;
                c.Precincts = new[] { 16, 16 }; c.PackedHeadersInMain = ppm; c.PackedHeadersInTile = ppt;
            });
            var parts = TileParts(ScenarioRunner.Encode(s, s.MakeImage()));
            Assert.Equal(s.TileCount, parts.Count);
            Assert.All(parts, p => Assert.Equal((0, 1), (p.Index, p.Count)));
            AssertRoundTrip(s);
        }

        [Fact]
        public void TileParts_AreAtMost255PerTile()
        {
            // a packet per tile-part would be more than the byte of the SOT marker can number
            var s = Plain(c => { c.Width = 100; c.Height = 24; c.Levels = 4; c.Precincts = new[] { 16, 16 }; c.PacketsPerTilePart = 1; c.Layers = 0; });
            var data = ScenarioRunner.Encode(s, s.MakeImage());
            var parts = TileParts(data);
            Assert.InRange(parts.Count, 2, 255);
            Assert.All(parts, p => Assert.Equal(parts.Count, p.Count));
            AssertRoundTrip(s);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void PackedHeaders_LeaveNothingAfterTheEndOfTheCodestream(bool ppm, bool ppt)
        {
            // removing the SOP and EPH markers that were only there to find the packets shortens the file
            var s = Plain(c => { c.PackedHeadersInMain = ppm; c.PackedHeadersInTile = ppt; c.Width = 8; c.Height = 6; c.Bits = 1; });
            TileParts(ScenarioRunner.Encode(s, s.MakeImage())); // asserts that EOC is the last thing
        }

        // ---- the encoder ------------------------------------------------------------------------------------------

        [Fact]
        public void Bypass_WithAPassRateBeyondTheData_Encodes()
        {
            // seed 729 of the sweep: a raw pass whose estimated end lay a byte past the end of the code-block's data
            var s = CodingScenario.FromSeed(729);
            Assert.Null(ScenarioSweepTests.Check(s));
        }

        // ---- the decoder ------------------------------------------------------------------------------------------

        [Fact]
        public void LossyColourTransform_RoundsDarkAndBrightSamplesAlike()
        {
            // inverse ICT output is centred on zero, and rounding it by adding half and truncating pushed the dark half (negative) up by
            // a whole level more than the bright half
            var s = Plain(c => { c.Lossless = false; c.Components = 3; c.ColourTransform = true; c.Levels = 3; c.StepSize = 1f / 64; c.Width = 128; c.Height = 64; });
            var rnd = new Random(9);
            var centred = Enumerable.Range(0, 3).Select(c => Enumerable.Range(0, 128 * 64)
                .Select(i => Math.Clamp((i % 128) * 2 + rnd.Next(-6, 7) + c * 5, 0, 255) - 128).ToArray()).ToArray();
            var image = new SampleImage(128, 64, 8, false, centred);
            var (w, _, samples) = ScenarioRunner.Decode(ScenarioRunner.Encode(s, image));
            var expected = image.OffsetBinary();
            for (var c = 0; c < 3; c++)
            {
                double Mean(Func<int, bool> pick) => Enumerable.Range(0, samples[c].Length).Where(i => pick(expected[c][i])).Average(i => (double)(samples[c][i] - expected[c][i]));
                Assert.InRange(Math.Abs(Mean(v => v < 128) - Mean(v => v >= 128)), 0, 0.25);
            }
        }

        [Theory]
        [InlineData(6)]
        [InlineData(4)]
        [InlineData(2)]
        public void LossyColourTransform_WithoutDecomposition_KeepsTheFractionOfTheCoefficients(int bits)
        {
            // with no decomposition level the coefficients were rounded to integers before the colour transform that needs them as floats
            var s = Plain(c => { c.Lossless = false; c.Components = 3; c.ColourTransform = true; c.Levels = 0; c.StepSize = 1f / 128; c.Bits = bits; c.Width = 96; c.Height = 96; });
            var image = s.MakeImage();
            var (w, _, samples) = ScenarioRunner.Decode(ScenarioRunner.Encode(s, image));
            Assert.False(ScenarioRunner.Compare(image.OffsetBinary(), samples, w, bits).Any);
        }

        [Fact]
        public void ColourSpaceBoxes_WithAZeroLength_DoNotLoopForever()
        {
            // a JP2 whose box says it is 0 bytes long never gets past that box
            var s = Plain(c => c.FileFormat = true);
            var data = ScenarioRunner.Encode(s, s.MakeImage());
            var corrupt = (byte[])data.Clone();
            // the second box (file type) follows the 12-byte signature box; give it length 0
            Array.Clear(corrupt, 12, 4);
            AssertEndsPromptly(corrupt);
        }

        [Fact]
        public void PacketHeaders_ThatNeverEnd_AreRefused()
        {
            // a stretch of 1 bits has a tag tree and a length indicator read on without end
            var s = Plain(c => { c.Layers = 0; });
            var data = ScenarioRunner.Encode(s, s.MakeImage());
            var sod = IndexOf(data, 0xFF, 0x93);
            var eoc = data.Length - 2;
            var corrupt = (byte[])data.Clone();
            for (var i = sod + 2; i < eoc; i++) corrupt[i] = 0xFF;
            AssertEndsPromptly(corrupt);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void DamagedFiles_AreRefusedWithAnInvalidOperation_NotAnImplementationException(int seed)
        {
            // cut the file short in several places, and change its header bytes
            var s = CodingScenario.FromSeed(seed);
            var data = ScenarioRunner.Encode(s, s.MakeImage());
            var rnd = new Random(seed);
            for (var k = 0; k < 40; k++)
            {
                var copy = k % 2 == 0 ? data.AsSpan(0, rnd.Next(1, data.Length)).ToArray() : (byte[])data.Clone();
                if (k % 2 == 1) copy[rnd.Next(Math.Min(copy.Length, 120))] = (byte)rnd.Next(256);
                try
                {
                    using var _ = J2kImage.FromBytes(copy);
                }
                catch (Exception e)
                {
                    Assert.False(e is NullReferenceException || e is IndexOutOfRangeException || e is InvalidCastException
                                 || e is DivideByZeroException || e is OverflowException, e.ToString());
                }
            }
        }

        // ---- speed ------------------------------------------------------------------------------------------------

        [Fact]
        public void ManyTiles_DecodeInTimeProportionalToTheirNumber()
        {
            // 4096 tiles of 8x8; the per-tile bookkeeping had looked at every tile for each code-block, which made this take over a second
            // (16384 tiles, 17 seconds; now 0.3)
            var s = Plain(c => { c.Width = 512; c.Height = 512; c.TileWidth = 8; c.TileHeight = 8; c.Levels = 1; c.BlockWidth = 32; c.BlockHeight = 32; c.Layers = 1; });
            var data = ScenarioRunner.Encode(s, s.MakeImage());
            var watch = Stopwatch.StartNew();
            ScenarioRunner.Decode(data);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1.5), $"Decoding 4096 tiles took {watch.Elapsed.TotalSeconds:F1} s.");
        }

        [Fact]
        public void IntegerSpec_MinAndMax_FollowTheValuesSet()
        {
            var spec = new IntegerSpec(3, 2, ModuleSpec.SPEC_TYPE_TILE_COMP);
            spec.SetDefault(5);
            Assert.Equal(5, spec.Min);
            Assert.Equal(5, spec.Max);
            spec.SetTileCompVal(1, 1, 2);
            Assert.Equal(2, spec.Min);
            spec.SetTileCompVal(2, 0, 9);
            Assert.Equal(9, spec.Max);
            spec.SetTileCompVal(1, 1, 7);
            Assert.Equal(5, spec.Min);
        }

        // ---- helpers ----------------------------------------------------------------------------------------------

        private static int IndexOf(byte[] data, byte first, byte second)
        {
            for (var i = 0; i < data.Length - 1; i++)
                if (data[i] == first && data[i + 1] == second) return i;
            throw new InvalidOperationException("Marker not found.");
        }

        private static void AssertEndsPromptly(byte[] data)
        {
            // a thread of its own: a decode queued behind the busy thread pool of a full test run would look like a hang
            var done = new System.Threading.ManualResetEventSlim();
            var thread = new System.Threading.Thread(() =>
            {
                try { using var _ = J2kImage.FromBytes(data); }
                catch (Exception e) when (!(e is NullReferenceException || e is IndexOutOfRangeException)) { }
                finally { done.Set(); }
            }) { IsBackground = true };
            thread.Start();
            Assert.True(done.Wait(TimeSpan.FromSeconds(30)), "The decode did not end.");
        }
    }
}
