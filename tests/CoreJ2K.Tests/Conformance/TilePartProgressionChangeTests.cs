// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// A POC marker segment may sit in a tile-part header, where it changes the progression of that tile alone. The official
    /// conformance streams p0_07 and e1_colr carry one; the reader looked up the tile's earlier POC by indexing a dictionary that
    /// had no entry yet, so they threw a <see cref="KeyNotFoundException"/>.
    /// </summary>
    public class TilePartProgressionChangeTests
    {
        private const int Width = 64, Height = 48, Components = 3;

        private static byte[] Encode(string aptype, out int[][] samples)
        {
            var rnd = new Random(7);
            var planes = Enumerable.Range(0, Components).Select(_ => Enumerable.Range(0, Width * Height).Select(i => rnd.Next(256)).ToArray()).ToArray();
            samples = planes;
            var source = new InterleavedImageSource(Width, Height, Components, 8, new bool[Components],
                planes.Select(c => c.Select(v => v - 128).ToArray()).ToArray());

            var parameters = new J2KEncoderConfiguration().WithLossless().WithTiles(t => t.SetSize(32, 32)).ToParameterList();
            parameters["Aptype"] = aptype;
            parameters["Alayers"] = "0.1 +2";
            return J2kImage.ToBytes(source, null, parameters);
        }

        /// <summary>The tiles whose tile-part header holds a POC marker segment, and whether the main header has one.</summary>
        private static (bool Main, HashSet<int> Tiles) WhereIsPoc(byte[] file)
        {
            var d = ScenarioRunner.Codestream(file);
            var i = 2;
            var main = false;
            while (!(d[i] == 0xFF && d[i + 1] == 0x90))
            {
                main |= d[i + 1] == 0x5F;
                i += 2 + ((d[i + 2] << 8) | d[i + 3]);
            }

            var tiles = new HashSet<int>();
            while (i + 11 < d.Length && d[i] == 0xFF && d[i + 1] == 0x90)
            {
                var tile = (d[i + 4] << 8) | d[i + 5];
                var next = i + (int)(((uint)d[i + 6] << 24) | ((uint)d[i + 7] << 16) | ((uint)d[i + 8] << 8) | d[i + 9]);
                for (var j = i + 12; !(d[j] == 0xFF && d[j + 1] == 0x93); j += 2 + ((d[j + 2] << 8) | d[j + 3]))
                    if (d[j + 1] == 0x5F) tiles.Add(tile);
                i = next;
            }
            return (main, tiles);
        }

        [Fact]
        public void PocInATilePartHeader_Decodes()
        {
            var data = Encode("t1 layer 0 0 2 3 3 res", out var samples);

            var (main, tiles) = WhereIsPoc(data);
            Assert.False(main);
            Assert.Equal(new[] { 1 }, tiles.OrderBy(t => t));

            var decoded = J2kImage.FromBytes(data);
            for (var c = 0; c < Components; c++)
                Assert.Equal(samples[c], decoded.GetComponent(c));
        }

        [Fact]
        public void PocInTheMainHeader_StillDecodes()
        {
            var data = Encode("layer 0 0 2 3 3 res", out var samples);

            var (main, tiles) = WhereIsPoc(data);
            Assert.True(main);
            Assert.Empty(tiles);

            var decoded = J2kImage.FromBytes(data);
            for (var c = 0; c < Components; c++)
                Assert.Equal(samples[c], decoded.GetComponent(c));
        }
    }
}
