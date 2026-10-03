// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.entropy.encoder;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The forward wavelet transform splits its row and column passes across threads, and the entropy coder codes batches of
    /// code-blocks on several threads. That must be invisible: the encoded bytes are identical for every thread count, for every kind
    /// of stream. These tests lower the size thresholds so small images really run on several threads.
    /// </summary>
    public class ParallelEncodeTests
    {
        private static int[][] MakeComponents(int width, int height, int components, int seed = 1)
        {
            var rnd = new Random(seed);
            var comps = new int[components][];
            for (var c = 0; c < components; c++)
            {
                comps[c] = new int[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var v = 128 + (int)(70 * Math.Sin(x / (7.0 + c)) * Math.Cos(y / 9.0)) + rnd.Next(-12, 13)
                                + ((x / 40 + y / 30) % 2 == 0 ? 30 : -30);
                        comps[c][y * width + x] = Math.Clamp(v, 0, 255) - 128;
                    }
            }
            return comps;
        }

        // An image source is closed by the encoder, so every encode builds a fresh one over the same samples.
        private static InterleavedImageSource Source(int[][] comps, int width, int height)
            => new InterleavedImageSource(width, height, comps.Length, 8, new bool[comps.Length], comps);

        private static byte[] Encode(int[][] comps, int width, int height, int threads, Action<ParameterList> configure, AtkMarkerSegment? atk = null,
            int packetPassMinBlocks = 0)
        {
            var previousSamples = ForwWTFull.MinParallelSamplesForCurrentThread;
            var previousBlocks = StdEntropyCoder.MinParallelBlocksForCurrentThread;
            var previousPipeline = StdEntropyCoder.MinPipelineBlocksForCurrentThread;
            var previousPackets = EBCOTRateAllocator.MinParallelBlocksForCurrentThread;
            // This thread only: tiny images must still split their wavelet passes, code-block batches and packet passes across
            // threads, and pull their blocks on the producer thread.
            ForwWTFull.MinParallelSamplesForCurrentThread = 0;
            StdEntropyCoder.MinParallelBlocksForCurrentThread = 0;
            StdEntropyCoder.MinPipelineBlocksForCurrentThread = 0;
            EBCOTRateAllocator.MinParallelBlocksForCurrentThread = packetPassMinBlocks;
            try
            {
                var pl = J2kImage.GetDefaultEncoderParameterList();
                pl["file_format"] = "off";
                pl["threads"] = threads.ToString();
                configure(pl);
                return J2kImage.ToBytes(Source(comps, width, height), null, pl, null, null, null, atk)!;
            }
            finally
            {
                ForwWTFull.MinParallelSamplesForCurrentThread = previousSamples;
                StdEntropyCoder.MinParallelBlocksForCurrentThread = previousBlocks;
                StdEntropyCoder.MinPipelineBlocksForCurrentThread = previousPipeline;
                EBCOTRateAllocator.MinParallelBlocksForCurrentThread = previousPackets;
            }
        }

        private static void AssertSameBytes(int[][] comps, int width, int height, Action<ParameterList> configure, string what, AtkMarkerSegment? atk = null)
        {
            var sequential = Encode(comps, width, height, 1, configure, atk);
            foreach (var threads in new[] { 2, 8 })
            {
                var parallel = Encode(comps, width, height, threads, configure, atk);
                Assert.True(sequential.AsSpan().SequenceEqual(parallel), $"{what}: encoded bytes differ with {threads} threads ({sequential.Length} vs {parallel.Length} bytes)");
            }
        }

        public static TheoryData<string> Variants() => new TheoryData<string>
        {
            "lossless", "lossy-9x7", "lossy-low-rate", "tiled-aligned", "tiled-unaligned-origin", "layers", "levels-0", "levels-1", "levels-5",
            "precincts-rpcl", "roi", "mct-off", "small-codeblocks",
            // Code-block coding modes: each changes how a block's passes are coded, and so what a worker must reproduce.
            "bypass", "term-each-pass", "reset-mq", "segmentation-symbols", "causal", "sop-eph", "predictable-termination", "predictable-termination-bypass", "all-modes",
            // Packet building and writing: what is recorded per packet (lengths, markers), and the order packets are written in.
            "plt-tlm", "sop-eph-layers", "packed-headers-main", "packed-headers-tile", "poc", "roi-layers-tiles", "many-layers-precincts",
            "precincts-layers-layer", "precincts-layers-res", "precincts-layers-res-pos", "precincts-layers-pos-comp", "precincts-layers-comp-pos",
        };

        private static void Configure(string variant, ParameterList pl)
        {
            switch (variant)
            {
                case "lossless": pl["lossless"] = "on"; break;
                case "lossy-9x7": pl["lossless"] = "off"; pl["rate"] = "2.0"; break;
                case "lossy-low-rate": pl["lossless"] = "off"; pl["rate"] = "0.4"; break;
                case "tiled-aligned": pl["lossless"] = "on"; pl["tiles"] = "64 64"; break;
                case "tiled-unaligned-origin": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["tiles"] = "60 60"; break;
                case "layers": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Alayers"] = "0.2 +4 2.0"; break;
                case "levels-0": pl["lossless"] = "on"; pl["Wlev"] = "0"; break;
                case "levels-1": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Wlev"] = "1"; break;
                case "levels-5": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Wlev"] = "5"; break;
                case "precincts-rpcl": pl["lossless"] = "on"; pl["Cpp"] = "64 64"; pl["Aptype"] = "res-pos"; pl["tiles"] = "100 70"; break;
                case "roi": pl["lossless"] = "on"; pl["Rroi"] = "R 20 20 60 50"; break;
                case "mct-off": pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Mct"] = "off"; break;
                case "small-codeblocks": pl["lossless"] = "on"; pl["Cblksiz"] = "16 16"; break;
                case "bypass": pl["lossless"] = "on"; pl["Cbypass"] = "on"; break;
                case "term-each-pass": pl["lossless"] = "on"; pl["Cterminate"] = "on"; break;
                case "reset-mq": pl["lossless"] = "on"; pl["CresetMQ"] = "on"; break;
                case "segmentation-symbols": pl["lossless"] = "on"; pl["Cseg_symbol"] = "on"; break;
                case "causal": pl["lossless"] = "on"; pl["Ccausal"] = "on"; break;
                case "sop-eph": pl["lossless"] = "on"; pl["Psop"] = "on"; pl["Peph"] = "on"; break;
                case "predictable-termination": pl["lossless"] = "on"; pl["Cterm_type"] = "predict"; pl["Cterminate"] = "on"; break;
                // 'predict' enables the predictable-termination option; it only reaches the raw (bypass) coding passes in bypass mode.
                case "predictable-termination-bypass": pl["lossless"] = "on"; pl["Cterm_type"] = "predict"; pl["Cbypass"] = "on"; pl["Cterminate"] = "on"; break;
                case "plt-tlm": pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["tiles"] = "100 100"; pl["Hplt"] = "on"; pl["Htlm"] = "on"; break;
                case "sop-eph-layers": pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["tiles"] = "100 100"; pl["Psop"] = "on"; pl["Peph"] = "on"; break;
                case "packed-headers-main": pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["Hppm"] = "on"; break;
                case "packed-headers-tile": pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["tiles"] = "100 100"; pl["Hppt"] = "on"; break;
                case "poc":
                    pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["Cpp"] = "64 64"; pl["tiles"] = "120 100";
                    pl["Aptype"] = "res 0 0 2 6 3 res-pos 0 0 5 6 3 pos-comp";
                    break;
                case "roi-layers-tiles": pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["tiles"] = "100 80"; pl["Rroi"] = "R 20 20 60 50"; break;
                case "many-layers-precincts": pl["lossless"] = "off"; pl["rate"] = "4.0"; pl["Cpp"] = "32 32"; pl["Alayers"] = "0.02 +12 0.5 +10 4.0"; break;
                case "precincts-layers-layer":
                case "precincts-layers-res":
                case "precincts-layers-res-pos":
                case "precincts-layers-pos-comp":
                case "precincts-layers-comp-pos":
                    // The progression is the end of the variant's name: every order visits a precinct's layers, in order, in a different way.
                    pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["Cpp"] = "32 32"; pl["tiles"] = "120 100";
                    pl["Aptype"] = variant.Substring("precincts-layers-".Length);
                    break;
                case "all-modes":
                    pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["Cbypass"] = "on"; pl["Cterminate"] = "on"; pl["CresetMQ"] = "on";
                    pl["Cseg_symbol"] = "on"; pl["Ccausal"] = "on"; pl["tiles"] = "64 64";
                    break;
            }
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void ParallelEncode_IsByteIdenticalToSequential(string variant)
        {
            AssertSameBytes(MakeComponents(200, 160, 3), 200, 160, pl => Configure(variant, pl), variant);
        }

        public static TheoryData<int, int> Shapes() => new TheoryData<int, int>
        {
            { 16, 16 }, { 17, 33 }, { 15, 100 }, { 100, 15 }, { 1, 50 }, { 50, 1 }, { 2, 2 }, { 3, 77 }, { 203, 157 }, { 257, 5 }, { 5, 257 },
            { 31, 32 }, { 64, 3 }, { 33, 17 }, { 16, 17 }, { 17, 16 },
        };

        [Theory]
        [MemberData(nameof(Shapes))]
        public void ParallelEncode_IsByteIdentical_AroundTheColumnBlockBoundary(int width, int height)
        {
            var comps = MakeComponents(width, height, 1);
            AssertSameBytes(comps, width, height, pl => { pl["lossless"] = "on"; pl["Wlev"] = "3"; }, $"{width}x{height} lossless");

            // A rate-limited codestream needs room for its own headers, so very small images are only tested lossless.
            if (width * height >= 256)
                AssertSameBytes(comps, width, height, pl => { pl["lossless"] = "off"; pl["rate"] = "64.0"; pl["Wlev"] = "3"; }, $"{width}x{height} lossy");
        }

        [Fact]
        public void ParallelEncode_IsByteIdentical_WhenManyBatchesAreNeeded()
        {
            // A batch ends at 4096 blocks or about 2M samples. 4x4 code-blocks in a 700x700 component are about 30,000 blocks, so the
            // coder pulls, codes and drains several batches per component, and the batch boundaries must not change the output.
            var comps = MakeComponents(700, 700, 1);
            AssertSameBytes(comps, 700, 700, pl => { pl["lossless"] = "on"; pl["Cblksiz"] = "4 4"; pl["Wlev"] = "2"; }, "4x4 code-blocks");
        }

        [Fact]
        public void ParallelEncode_IsByteIdentical_AcrossManyTilesAndComponents()
        {
            // The coder's per-component queues and "source exhausted" flags must reset at every tile.
            var comps = MakeComponents(480, 330, 3);
            AssertSameBytes(comps, 480, 330, pl => { pl["lossless"] = "off"; pl["rate"] = "2.0"; pl["tiles"] = "40 40"; }, "many small tiles");
        }

        [Fact]
        public void ParallelEncode_WithCustomAtkKernels_IsByteIdentical()
        {
            // The arbitrary-kernel analysis filters take the generic path through the same pass runner.
            var comps = MakeComponents(200, 150, 1);
            AssertSameBytes(comps, 200, 150, pl => pl["lossless"] = "on", "ATK 5/3", AtkMarkerSegment.CreateW5x3Equivalent(2));
            AssertSameBytes(comps, 200, 150, pl => { pl["lossless"] = "off"; pl["rate"] = "3.0"; }, "ATK 9/7", AtkMarkerSegment.CreateW9x7Equivalent(3));
        }

        [Fact]
        public void ParallelEncode_IsByteIdentical_WhetherOrNotThePacketPassesAreParallel()
        {
            // Rate allocation builds each layer's packets on several threads above a size threshold, and writes them from a queue.
            // With the threshold out of reach the same encode runs one packet at a time on the calling thread.
            var comps = MakeComponents(300, 200, 3);
            foreach (var variant in new[] { "layers", "plt-tlm", "precincts-layers-res-pos", "tiled-aligned" })
            {
                var serialPackets = Encode(comps, 300, 200, 4, pl => Configure(variant, pl), packetPassMinBlocks: int.MaxValue);
                var parallelPackets = Encode(comps, 300, 200, 4, pl => Configure(variant, pl));
                Assert.True(serialPackets.AsSpan().SequenceEqual(parallelPackets), $"{variant}: parallel packet passes changed the bytes");
            }
        }

        /// <summary>
        /// Reads each tile-part of a codestream: the packet lengths its PLT markers declare, and the lengths the packets really have,
        /// found from the SOP marker in front of each. Independent of the encoder code that produced either.
        /// </summary>
        private static System.Collections.Generic.List<(int[] Declared, int[] Actual, int Psot, int Length)> ReadTileParts(byte[] data)
        {
            int U16(int at) => (data[at] << 8) | data[at + 1];
            int U32(int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

            var result = new System.Collections.Generic.List<(int[], int[], int, int)>();
            var pos = 2; // after SOC
            while (U16(pos) != 0xFF90) pos += 2 + U16(pos + 2); // main header marker segments, up to the first SOT

            while (pos + 12 <= data.Length && U16(pos) == 0xFF90)
            {
                // The tile-part ends where the next SOT marker starts (or at the EOC marker); 0xFF90 cannot occur inside packet data.
                var start = pos;
                var psot = U32(pos + 6);
                var end = data.Length - 2;
                for (var at = start + 12; at + 3 < data.Length; at++)
                    if (data[at] == 0xFF && data[at + 1] == 0x90 && data[at + 2] == 0 && data[at + 3] == 10) { end = at; break; }
                pos += 12;

                var declared = new System.Collections.Generic.List<int>();
                while (U16(pos) != 0xFF93) // until SOD
                {
                    var segmentEnd = pos + 2 + U16(pos + 2);
                    if (U16(pos) == 0xFF58)
                    {
                        // Zplt, then 7-bit values whose high bit means "more follows"
                        for (int at = pos + 5, value = 0; at < segmentEnd; at++)
                        {
                            value = (value << 7) | (data[at] & 0x7F);
                            if ((data[at] & 0x80) == 0) { declared.Add(value); value = 0; }
                        }
                    }
                    pos = segmentEnd;
                }
                pos += 2;

                // A body byte after 0xFF is always below 0x90, so 0xFF91 only ever starts a packet's SOP marker.
                var starts = new System.Collections.Generic.List<int>();
                for (var at = pos; at + 3 < end; at++)
                    if (data[at] == 0xFF && data[at + 1] == 0x91 && data[at + 2] == 0 && data[at + 3] == 4) starts.Add(at);
                var actual = starts.Select((first, i) => (i + 1 < starts.Count ? starts[i + 1] : end) - first).ToArray();

                result.Add((declared.ToArray(), actual, psot, end - start));
                pos = end;
            }
            return result;
        }

        public static TheoryData<string, int, int> PacketOrders() => TheoryDataFor(
            new[] { "layer", "res", "res-pos", "pos-comp", "comp-pos", "res 0 0 2 6 3 res-pos 0 0 5 6 3 pos-comp" }, new[] { 1, 3 }, new[] { 1, 4 });

        private static TheoryData<string, int, int> TheoryDataFor(string[] progressions, int[] components, int[] threads)
        {
            var data = new TheoryData<string, int, int>();
            foreach (var progression in progressions)
                foreach (var componentCount in components)
                    foreach (var threadCount in threads)
                        data.Add(progression, componentCount, threadCount);
            return data;
        }

        [Theory]
        [MemberData(nameof(PacketOrders))]
        public void PacketLengthMarkers_ListThePacketsInTheOrderTheyAreWritten(string progression, int components, int threads)
        {
            // The markers are in front of the packets they describe, so their lengths are worked out before anything is written; they
            // still have to come in the order the tile's progression writes the packets (which differs from the order rate allocation
            // simulates them in), so that each entry equals the length of the packet it describes, however many threads built them.
            var comps = MakeComponents(260, 200, components);
            var data = Encode(comps, 260, 200, threads, pl =>
            {
                pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["tiles"] = "100 90"; pl["Cpp"] = "64 64";
                pl["Hplt"] = "on"; pl["Psop"] = "on"; pl["Peph"] = "on"; pl["Aptype"] = progression;
            });

            var tileParts = ReadTileParts(data);
            Assert.Equal(9, tileParts.Count);
            for (var i = 0; i < tileParts.Count; i++)
            {
                Assert.NotEmpty(tileParts[i].Actual);
                Assert.True(tileParts[i].Declared.SequenceEqual(tileParts[i].Actual),
                    $"tile-part {i}, '{progression}', {components} component(s), {threads} thread(s): PLT lengths differ from the real packet lengths");
                Assert.Equal(tileParts[i].Length, tileParts[i].Psot);
            }
        }

        private static int U16(byte[] data, int at) => (data[at] << 8) | data[at + 1];

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void PacketLengthMarkers_ListEveryPacketOfATileWithMoreThanOneMarkerHolds(int threads)
        {
            // 8x8 precincts and 4x4 code-blocks give one 640x640 tile tens of thousands of packets per layer. Their lengths take well
            // over the 65532 bytes one PLT marker can hold, so they must be spread over several markers, none of them cut short.
            var comps = MakeComponents(640, 640, 1);
            byte[] Lossy(bool plt) => Encode(comps, 640, 640, threads, pl =>
            {
                pl["lossless"] = "off"; pl["rate"] = "6.0"; pl["Alayers"] = "0.5 +6 6.0"; pl["Cblksiz"] = "4 4"; pl["Cpp"] = "8 8";
                pl["Aptype"] = "layer"; pl["Psop"] = "on";
                if (plt) pl["Hplt"] = "on";
            });
            var data = Lossy(true);

            var tilePart = Assert.Single(ReadTileParts(data));
            Assert.True(tilePart.Actual.Length > 40000, $"only {tilePart.Actual.Length} packets: not enough to need a second marker");
            Assert.True(tilePart.Declared.SequenceEqual(tilePart.Actual), "the PLT lengths are not the lengths of the packets");
            Assert.Equal(tilePart.Length, tilePart.Psot);

            // The lengths are spread over consecutive markers, numbered from 0, each within 16 bits.
            var zplts = new List<int>();
            for (var pos = 2; ; pos += 2 + U16(data, pos + 2))
            {
                if (U16(data, pos) == 0xFF93) break; // SOD
                if (U16(data, pos) != 0xFF58) continue;
                zplts.Add(data[pos + 4]);
                Assert.True(U16(data, pos + 2) <= 65535);
            }
            Assert.True(zplts.Count >= 2, "the lengths fit in one PLT marker");
            Assert.Equal(Enumerable.Range(0, zplts.Count), zplts);

            // The decoder collects the lengths of every marker, not only the last one.
            using (var stream = new System.IO.MemoryStream(data))
            {
                var raf = new CoreJ2K.j2k.util.ISRandomAccessIO(stream);
                var info = new CoreJ2K.j2k.codestream.HeaderInfo();
                var decoderParameters = new ParameterList(J2kImage.GetDefaultDecoderParameterList());
                var headerDecoder = new CoreJ2K.j2k.codestream.reader.HeaderDecoder(raf, decoderParameters, info);
                var reader = CoreJ2K.j2k.codestream.reader.BitstreamReaderAgent.createInstance(raf, headerDecoder, decoderParameters, headerDecoder.DecoderSpecs, false, info);
                reader.SetTile(0, 0);

                var collected = headerDecoder.GetPLTData().GetPacketEntries(0).Select(entry => entry.PacketLength).ToArray();
                Assert.True(tilePart.Declared.SequenceEqual(collected), $"the decoder collected {collected.Length} packet lengths, the stream lists {tilePart.Declared.Length}");
            }

            // The markers are an index only: the image is the one a stream without them holds.
            var withPlt = J2kImage.FromBytes(data);
            var without = J2kImage.FromBytes(Lossy(false));
            Assert.True(withPlt.GetComponent(0).SequenceEqual(without.GetComponent(0)), "a stream with PLT markers decodes differently from one without");
        }

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(3, 1, true)]
        [InlineData(3, 4, true)]
        [InlineData(3, 4, false)]
        public void TilePartLength_CountsEveryByteOfTheTilePart(int components, int threads, bool plt)
        {
            // Psot (and the TLM entry, which is the same number) is the length of the whole tile-part, header included. The PLT
            // marker is part of that header, but it can only be sized once the packets are built.
            var comps = MakeComponents(260, 200, components);
            var data = Encode(comps, 260, 200, threads, pl =>
            {
                pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; pl["tiles"] = "100 90"; pl["Cpp"] = "64 64";
                pl["Psop"] = "on";
                if (plt) pl["Hplt"] = "on";
            });

            var tileParts = ReadTileParts(data);
            Assert.Equal(9, tileParts.Count);
            for (var i = 0; i < tileParts.Count; i++)
            {
                Assert.True(tileParts[i].Psot == tileParts[i].Length, $"tile-part {i}: Psot is {tileParts[i].Psot} but the tile-part is {tileParts[i].Length} bytes long");
                if (plt) Assert.NotEmpty(tileParts[i].Declared);
            }
        }

        [Fact]
        public void ParallelEncode_IsByteIdentical_WhenThePacketQueueFlushesMidProgression()
        {
            // The queue of packets waiting to be built holds at most 16384 packets. 4x4 code-blocks with 16x16 precincts and several
            // layers make far more packets than that in one progression, so it must write what it has and carry on mid-stream.
            var comps = MakeComponents(640, 640, 1);
            void Configure(ParameterList pl)
            {
                pl["lossless"] = "off"; pl["rate"] = "6.0"; pl["Cblksiz"] = "4 4"; pl["Cpp"] = "16 16"; pl["Alayers"] = "0.5 +6 6.0"; pl["Aptype"] = "res-pos";
            }
            AssertSameBytes(comps, 640, 640, Configure, "packet queue flush");

            // One tile with one progression is one write pass unless the queue filled up part-way.
            var writePasses = 0;
            var previous = EBCOTRateAllocator.BeforePacketPassForCurrentThread;
            EBCOTRateAllocator.BeforePacketPassForCurrentThread = name => { if (name == "write") writePasses++; };
            try
            {
                Encode(comps, 640, 640, 4, Configure);
            }
            finally
            {
                EBCOTRateAllocator.BeforePacketPassForCurrentThread = previous;
            }
            Assert.True(writePasses >= 2, $"the packet queue never flushed part-way through the progression ({writePasses} write pass)");
        }

        [Fact]
        public void ParallelEncode_IsByteIdentical_WhetherOrNotTheSourceIsPulledAhead()
        {
            // Below the pipeline threshold a tile is pulled and coded on the calling thread; above it a producer thread pulls ahead
            // (even into the next component). Both must produce the same bytes as each other and as one thread.
            var comps = MakeComponents(300, 200, 3);
            var sequential = Encode(comps, 300, 200, 1, pl => pl["lossless"] = "on");

            var previous = StdEntropyCoder.MinPipelineBlocks;
            try
            {
                StdEntropyCoder.MinPipelineBlocks = int.MaxValue; // never pipeline
                var inline = Encode(comps, 300, 200, 4, pl => pl["lossless"] = "on");
                Assert.True(sequential.AsSpan().SequenceEqual(inline), "non-pipelined parallel encode differs from the single-threaded encode");
            }
            finally
            {
                StdEntropyCoder.MinPipelineBlocks = previous;
            }

            var pipelined = Encode(comps, 300, 200, 4, pl => pl["lossless"] = "on"); // Encode() forces the pipeline for this thread
            Assert.True(sequential.AsSpan().SequenceEqual(pipelined), "pipelined encode differs from the single-threaded encode");
        }

        [Fact]
        public void ASourceFailure_OnTheProducerThread_SurfacesWithItsOwnType()
        {
            var comps = MakeComponents(512, 512, 3);

            foreach (var threads in new[] { 1, 8 })
            {
                var previous = StdEntropyCoder.MinPipelineBlocksForCurrentThread;
                StdEntropyCoder.MinPipelineBlocksForCurrentThread = 0;
                try
                {
                    var pl = J2kImage.GetDefaultEncoderParameterList();
                    pl["file_format"] = "off";
                    pl["lossless"] = "on";
                    pl["threads"] = threads.ToString();

                    // The source starts failing part-way through reading the first component, which the producer thread is doing
                    // when more than one thread is allowed.
                    var failing = new FailingImageSource(Source(comps, 512, 512), failAfterCalls: 200);
                    var exception = Assert.Throws<InvalidOperationException>(() => J2kImage.ToBytes(failing, null, pl));
                    Assert.Contains("source failed", exception.Message);
                }
                finally
                {
                    StdEntropyCoder.MinPipelineBlocksForCurrentThread = previous;
                }
            }
        }

        [Fact]
        public void ConcurrentEncodes_EachUsingParallelPasses_AllMatch()
        {
            var comps = MakeComponents(300, 220, 3);
            var expected = Encode(comps, 300, 220, 1, pl => Configure("tiled-unaligned-origin", pl));

            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 6 }, i =>
            {
                var actual = Encode(comps, 300, 220, 4, pl => Configure("tiled-unaligned-origin", pl));
                if (!expected.AsSpan().SequenceEqual(actual)) failures.Add($"encode {i}");
            });

            Assert.Empty(failures);
        }

        [Fact]
        public void ConfigurationAndBuilder_ControlTheDegree()
        {
            Assert.Equal(3, J2kImage.ResolveDegreeOfParallelism(new J2KEncoderConfiguration().WithMaxDegreeOfParallelism(3).ToParameterList()));
            Assert.Equal(J2kImage.DefaultMaxDegreeOfParallelism, J2kImage.ResolveDegreeOfParallelism(new J2KEncoderConfiguration().ToParameterList()));

            var comps = MakeComponents(200, 160, 3);

            var previous = ForwWTFull.MinParallelSamplesForCurrentThread;
            ForwWTFull.MinParallelSamplesForCurrentThread = 0;
            try
            {
                // A configuration sets its own defaults, so compare it with itself at different thread counts.
                byte[] ViaConfig(int threads) => J2kImage.ToBytes(Source(comps, 200, 160),
                    new J2KEncoderConfiguration().WithLossless().WithFileFormat(false).WithMaxDegreeOfParallelism(threads));
                byte[] ViaBuilder(int threads) => new CompleteEncoderConfigurationBuilder().ForLossless().WithFileFormat(false)
                    .WithMaxDegreeOfParallelism(threads).Encode(Source(comps, 200, 160));

                Assert.True(ViaConfig(1).AsSpan().SequenceEqual(ViaConfig(4)), "a configuration with 4 threads encoded different bytes than with 1");
                Assert.True(ViaBuilder(1).AsSpan().SequenceEqual(ViaBuilder(4)), "a builder with 4 threads encoded different bytes than with 1");
            }
            finally
            {
                ForwWTFull.MinParallelSamplesForCurrentThread = previous;
            }
        }
    }
    /// <summary>An image source that reads normally for a while and then throws, to exercise the encoder's failure paths.</summary>
    internal sealed class FailingImageSource : ImgDataAdapter, BlkImgDataSrc
    {
        private readonly BlkImgDataSrc _inner;
        private readonly int _failAfterCalls;
        private int _calls;

        public FailingImageSource(BlkImgDataSrc inner, int failAfterCalls) : base(inner)
        {
            _inner = inner;
            _failAfterCalls = failAfterCalls;
        }

        private void MaybeFail()
        {
            if (System.Threading.Interlocked.Increment(ref _calls) > _failAfterCalls)
                throw new InvalidOperationException("source failed");
        }

        public int GetFixedPoint(int compIndex) => _inner.GetFixedPoint(compIndex);

        public DataBlk GetInternCompData(DataBlk blk, int compIndex)
        {
            MaybeFail();
            return _inner.GetInternCompData(blk, compIndex);
        }

        public DataBlk GetCompData(DataBlk blk, int c)
        {
            MaybeFail();
            return _inner.GetCompData(blk, c);
        }

        public void Close() => _inner.Close();

        public bool IsOrigSigned(int compIndex) => _inner.IsOrigSigned(compIndex);
    }
}
