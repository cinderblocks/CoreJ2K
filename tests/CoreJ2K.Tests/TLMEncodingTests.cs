// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.codestream.reader;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The <c>Htlm</c> option writes a TLM (tile-part lengths) marker in the main header. It can only be written once every tile-part's
    /// length is known, which is after the layers have been built, so these tests read the codestream back with a parser of their own
    /// (ISO/IEC 15444-1, A.7.1 and A.4.2) and compare what TLM says with what the tile-parts really are.
    /// </summary>
    public class TLMEncodingTests
    {
        private const int Width = 260;
        private const int Height = 200;

        private static int[][] MakeComponents(int width, int height, int components, bool noise = false)
        {
            var rnd = new Random(3);
            var comps = new int[components][];
            for (var c = 0; c < components; c++)
            {
                comps[c] = new int[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var v = 128 + (int)(70 * Math.Sin(x / (7.0 + c)) * Math.Cos(y / 9.0)) + rnd.Next(-12, 13);
                        comps[c][y * width + x] = noise ? rnd.Next(0, 256) - 128 : Math.Clamp(v, 0, 255) - 128;
                    }
            }
            return comps;
        }

        private static byte[] Encode(int[][] comps, int width, int height, Action<ParameterList> configure, int threads = 1)
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["threads"] = threads.ToString();
            configure(pl);
            var source = new InterleavedImageSource(width, height, comps.Length, 8, new bool[comps.Length], comps);
            return J2kImage.ToBytes(source, null, pl)!;
        }

        private static int U16(byte[] data, int at) => (data[at] << 8) | data[at + 1];

        private static int U32(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

        /// <summary>Reads the TLM entries of the main header (every TLM marker, in order), or null if there is no TLM marker.</summary>
        private static (List<(int Tile, int Length)> Entries, int Stlm)? ReadTlm(byte[] data)
        {
            var entries = new List<(int, int)>();
            var stlm = -1;
            for (var pos = 2; U16(data, pos) != 0xFF90; pos += 2 + U16(data, pos + 2))
            {
                if (U16(data, pos) != 0xFF55) continue;

                stlm = data[pos + 5];
                var tileBytes = (stlm >> 4) & 3;            // ST
                var lengthBytes = ((stlm >> 6) & 1) == 0 ? 2 : 4; // SP
                var end = pos + 2 + U16(data, pos + 2);
                for (var at = pos + 6; at + tileBytes + lengthBytes <= end; at += tileBytes + lengthBytes)
                {
                    var tile = tileBytes == 0 ? entries.Count : tileBytes == 1 ? data[at] : U16(data, at);
                    var length = lengthBytes == 2 ? U16(data, at + tileBytes) : U32(data, at + tileBytes);
                    entries.Add((tile, length));
                }
            }
            return stlm < 0 ? ((List<(int, int)>, int)?)null : (entries, stlm);
        }

        /// <summary>Follows the tile-parts from SOT to SOT by their Psot, and checks that the last one ends at the EOC marker.</summary>
        private static List<(int Tile, int Length)> ReadTileParts(byte[] data)
        {
            var pos = 2;
            while (U16(data, pos) != 0xFF90) pos += 2 + U16(data, pos + 2);

            var parts = new List<(int, int)>();
            while (U16(data, pos) == 0xFF90)
            {
                var length = U32(data, pos + 6);
                parts.Add((U16(data, pos + 4), length));
                pos += length;
            }
            Assert.True(pos == data.Length - 2 && U16(data, pos) == 0xFFD9, "the tile-part lengths do not lead from one tile-part to the next up to EOC");
            return parts;
        }

        public static TheoryData<int, int, bool, bool> Cases() => new TheoryData<int, int, bool, bool>
        {
            // components, threads, PLT markers, layers and precincts
            { 1, 1, false, false },
            { 3, 1, false, false },
            { 3, 4, false, true },
            { 1, 1, true, true },
            { 3, 4, true, true },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void Htlm_WritesTheLengthOfEveryTilePart(int components, int threads, bool plt, bool layers)
        {
            var comps = MakeComponents(Width, Height, components);
            var data = Encode(comps, Width, Height, pl =>
            {
                pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["tiles"] = "100 90"; pl["Htlm"] = "on";
                if (plt) pl["Hplt"] = "on";
                if (layers) { pl["Alayers"] = "0.1 +4 2.5"; pl["Cpp"] = "64 64"; }
            }, threads);

            var tlm = ReadTlm(data);
            Assert.NotNull(tlm);
            var parts = ReadTileParts(data);
            Assert.Equal(9, parts.Count);
            Assert.Equal(parts, tlm!.Value.Entries);
        }

        [Fact]
        public void Htlm_UsesWideLengthsOnlyWhenATilePartNeedsThem()
        {
            // Ptlm is 16 bits unless some tile-part is longer than 65535 bytes, when every entry becomes 32 bits (Stlm bit 6).
            var small = Encode(MakeComponents(Width, Height, 1), Width, Height, pl => { pl["lossless"] = "on"; pl["tiles"] = "100 90"; pl["Htlm"] = "on"; });
            var big = Encode(MakeComponents(300, 300, 1, noise: true), 300, 300, pl => { pl["lossless"] = "on"; pl["Htlm"] = "on"; });

            var smallTlm = ReadTlm(small)!.Value;
            var bigTlm = ReadTlm(big)!.Value;

            Assert.True(smallTlm.Entries.All(entry => entry.Length <= 65535));
            Assert.Equal(0, (smallTlm.Stlm >> 6) & 1);
            Assert.Equal(ReadTileParts(small), smallTlm.Entries);

            Assert.True(bigTlm.Entries.Single().Length > 65535);
            Assert.Equal(1, (bigTlm.Stlm >> 6) & 1);
            Assert.Equal(ReadTileParts(big), bigTlm.Entries);

            // Reserved bits are zero, and ST says one byte per tile index (fewer than 256 tiles).
            Assert.Equal(0, smallTlm.Stlm & 0x8F);
            Assert.Equal(1, (smallTlm.Stlm >> 4) & 3);
        }

        [Fact]
        public void Htlm_UsesTwoByteTileIndices_ForMoreThan256Tiles()
        {
            var comps = MakeComponents(400, 400, 1);
            var data = Encode(comps, 400, 400, pl => { pl["lossless"] = "on"; pl["tiles"] = "20 20"; pl["Htlm"] = "on"; }); // 400 tiles

            var tlm = ReadTlm(data)!.Value;
            Assert.Equal(2, (tlm.Stlm >> 4) & 3);
            Assert.Equal(400, tlm.Entries.Count);
            Assert.Equal(ReadTileParts(data), tlm.Entries);
        }

        public static TheoryData<string> Configurations() => new TheoryData<string>
        {
            "lossy-layers", "precincts-rpcl", "precincts-comp-pos", "poc", "sop-eph", "roi", "plt", "lossless-small-blocks",
        };

        private static void Configure(string name, ParameterList pl)
        {
            pl["tiles"] = "100 90";
            switch (name)
            {
                case "lossy-layers": pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["Alayers"] = "0.1 +4 2.5"; break;
                case "precincts-rpcl": pl["lossless"] = "on"; pl["Cpp"] = "64 64"; pl["Aptype"] = "res-pos"; break;
                case "precincts-comp-pos": pl["lossless"] = "on"; pl["Cpp"] = "32 32"; pl["Aptype"] = "comp-pos"; break;
                case "poc": pl["lossless"] = "on"; pl["Cpp"] = "64 64"; pl["Aptype"] = "res 0 0 1 6 3 res-pos"; break;
                case "sop-eph": pl["lossless"] = "on"; pl["Psop"] = "on"; pl["Peph"] = "on"; break;
                case "roi": pl["lossless"] = "on"; pl["Rroi"] = "R 20 20 60 50"; break;
                case "plt": pl["lossless"] = "on"; pl["Hplt"] = "on"; break;
                case "lossless-small-blocks": pl["lossless"] = "on"; pl["Cblksiz"] = "16 16"; break;
            }
        }

        [Theory]
        [MemberData(nameof(Configurations))]
        public void Htlm_ChangesNeitherTheImageNorTheTlmAccuracy_WhateverTheStreamIsLike(string name)
        {
            var comps = MakeComponents(Width, Height, 3);
            var with = Encode(comps, Width, Height, pl => { Configure(name, pl); pl["Htlm"] = "on"; }, threads: 4);
            var without = Encode(comps, Width, Height, pl => Configure(name, pl), threads: 4);

            Assert.Equal(ReadTileParts(with), ReadTlm(with)!.Value.Entries);

            var a = J2kImage.FromBytes(with);
            var b = J2kImage.FromBytes(without);

            // A rate-limited stream with TLM has a longer header and so a little less room for packets: its image is not the same one
            // (see TheTlmMarkerIsCountedInTheRateTarget). Lossless streams are exact, so those must match.
            if (name == "lossy-layers") return;
            for (var c = 0; c < 3; c++)
            {
                Assert.True(a.GetComponent(c).SequenceEqual(b.GetComponent(c)), $"{name}: component {c} decodes differently with a TLM marker");
            }
        }

        [Fact]
        public void TheDecoder_ReadsTheTlmThatWasWritten()
        {
            // The decoder's own parser, over every combination of field sizes the encoder writes: 16- and 32-bit lengths, with one-
            // and two-byte tile indices.
            var cases = new (int[][] Comps, int W, int H, string Tiles)[]
            {
                (MakeComponents(Width, Height, 1), Width, Height, "100 90"),                // Ttlm 1 byte, Ptlm 2 bytes
                (MakeComponents(300, 300, 1, noise: true), 300, 300, "300 300"),            // Ttlm 1 byte, Ptlm 4 bytes
                (MakeComponents(400, 400, 1), 400, 400, "20 20"),                           // Ttlm 2 bytes, Ptlm 2 bytes
            };

            foreach (var (comps, w, h, tiles) in cases)
            {
                var data = Encode(comps, w, h, pl => { pl["lossless"] = "on"; pl["tiles"] = tiles; pl["Htlm"] = "on"; });

                using var stream = new MemoryStream(data);
                var decoder = new HeaderDecoder(new ISRandomAccessIO(stream), J2kImage.GetDefaultDecoderParameterList(), new HeaderInfo());
                var tlm = decoder.GetTLMData();

                Assert.NotNull(tlm);
                var read = tlm!.TilePartEntries.Select(entry => (entry.TileIndex, entry.TilePartLength)).ToList();
                Assert.True(ReadTileParts(data).SequenceEqual(read), $"{w}x{h} tiles {tiles}: the decoder read different tile-part lengths than the encoder wrote");
            }
        }

        public static TheoryData<int, int, string, bool> DecodeCases() => new TheoryData<int, int, string, bool>
        {
            // width, height, tile size, noise: each has a different Ttlm/Ptlm width in the TLM marker
            { Width, Height, "100 90", false },   // 9 tiles, one-byte tile indices, 16-bit lengths
            { 400, 400, "20 20", false },         // 400 tiles, two-byte tile indices, 16-bit lengths
            { 600, 300, "300 300", true },        // 2 tiles of over 65535 bytes each, one-byte tile indices, 32-bit lengths
        };

        [Theory]
        [MemberData(nameof(DecodeCases))]
        public void ATlmStream_DecodesToTheEncodedImage_WhateverFieldSizesItUses(int width, int height, string tiles, bool noise)
        {
            // The decoder moves from tile to tile by the TLM lengths when there is a TLM marker; a wrong or misread length makes the
            // decode fail, so each combination of field sizes has to decode the image it was made from.
            var comps = MakeComponents(width, height, 1, noise);
            var data = Encode(comps, width, height, pl => { pl["lossless"] = "on"; pl["tiles"] = tiles; pl["Htlm"] = "on"; });

            Assert.Equal(ReadTileParts(data), ReadTlm(data)!.Value.Entries);
            var image = J2kImage.FromBytes(data);
            Assert.True(image.GetComponent(0).Select(v => v - 128).SequenceEqual(comps[0]), $"{width}x{height} tiles {tiles}: the decoded image is not the encoded one");
        }

        [Fact]
        public void WithoutHtlm_NoTlmIsWritten()
        {
            var data = Encode(MakeComponents(Width, Height, 1), Width, Height, pl => { pl["lossless"] = "on"; pl["tiles"] = "100 90"; });
            Assert.Null(ReadTlm(data));
        }

        [Fact]
        public void AStreamWithTlm_DecodesToTheSameImage()
        {
            // The decoder finds each tile from the TLM lengths when it has them, so a wrong length would stop the decode working.
            var comps = MakeComponents(Width, Height, 3);
            byte[] Lossless(bool tlm) => Encode(comps, Width, Height, pl =>
            {
                pl["lossless"] = "on"; pl["tiles"] = "100 90"; pl["Hplt"] = "on";
                if (tlm) pl["Htlm"] = "on";
            });

            var with = J2kImage.FromBytes(Lossless(true));
            var without = J2kImage.FromBytes(Lossless(false));

            for (var c = 0; c < 3; c++)
            {
                Assert.True(with.GetComponent(c).SequenceEqual(without.GetComponent(c)), $"component {c} decodes differently with a TLM marker");
                Assert.True(with.GetComponent(c).Select(v => v - 128).SequenceEqual(comps[c]), $"component {c} is not the encoded image");
            }
        }

        [Fact]
        public void TheTlmMarkerIsCountedInTheRateTarget()
        {
            // The marker is part of the main header, so a rate-limited stream with it has less room for packets instead of coming out
            // longer than one without it. The header used to measure the overhead can be a couple of bytes per tile longer than the final
            // one (it assumes 32-bit lengths), but not shorter.
            var comps = MakeComponents(Width, Height, 3);
            byte[] Lossy(bool tlm) => Encode(comps, Width, Height, pl =>
            {
                pl["lossless"] = "off"; pl["rate"] = "2.5"; pl["tiles"] = "100 90";
                if (tlm) pl["Htlm"] = "on";
            });

            var with = Lossy(true);
            var without = Lossy(false);
            Assert.True(with.Length <= without.Length, $"the stream with TLM is {with.Length} bytes, longer than {without.Length} without");
            Assert.True(without.Length - with.Length <= 2 * 9 + 16, $"the stream with TLM is {with.Length} bytes, {without.Length - with.Length} fewer than without");
        }

        [Fact]
        public void Htlm_WithTileParts_IsLeftOutRatherThanWrittenWrong()
        {
            // Splitting tile-parts rewrites the codestream after it is written, which would leave the TLM lengths wrong.
            var comps = MakeComponents(Width, Height, 1);
            var data = Encode(comps, Width, Height, pl => { pl["lossless"] = "on"; pl["tiles"] = "100 90"; pl["Htlm"] = "on"; pl["tile_parts"] = "4"; });

            Assert.Null(ReadTlm(data));
            var image = J2kImage.FromBytes(data);
            Assert.True(image.GetComponent(0).Select(v => v - 128).SequenceEqual(comps[0]), "a stream with tile-parts and Htlm does not decode to the encoded image");
        }
    }
}
