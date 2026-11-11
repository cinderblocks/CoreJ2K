// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// A <see cref="ParameterList"/> made with <c>new ParameterList()</c> has no defaults of its own. The encoder and the decoder must
    /// supply theirs for whatever the caller left out, as the examples in the README and the guides rely on.
    /// </summary>
    public class BareParameterListTests
    {
        private const int Width = 48, Height = 40, Components = 3;

        private static int[][] Samples()
        {
            var rnd = new Random(5);
            return Enumerable.Range(0, Components)
                .Select(c => Enumerable.Range(0, Width * Height).Select(i => (i * (c + 3) + rnd.Next(0, 9)) & 0xFF).ToArray())
                .ToArray();
        }

        private static InterleavedImageSource Source(int[][] samples) =>
            new InterleavedImageSource(Width, Height, Components, 8, new bool[Components],
                samples.Select(c => c.Select(v => v - 128).ToArray()).ToArray());

        private static void AssertSame(InterleavedImage expected, InterleavedImage decoded) =>
            AssertSame(Enumerable.Range(0, Components).Select(expected.GetComponent).ToArray(), decoded);

        private static void AssertSame(int[][] expected, InterleavedImage decoded)
        {
            for (var c = 0; c < Components; c++)
                Assert.Equal(expected[c], decoded.GetComponent(c));
        }

        [Fact]
        public void Encode_Lossless_AsInTheReadme()
        {
            var samples = Samples();
            var p = new ParameterList();
            p["lossless"] = "on";
            p["file_format"] = "on";

            var data = J2kImage.ToBytes(Source(samples), p);

            AssertSame(samples, J2kImage.FromBytes(data));
        }

        [Fact]
        public void Encode_Lossy_AsInTheReadme()
        {
            var p = new ParameterList();
            p["rate"] = "2";
            p["file_format"] = "on";
            p["tiles"] = "32 32";
            p["Wlev"] = "3";

            var decoded = J2kImage.FromBytes(J2kImage.ToBytes(Source(Samples()), p));

            Assert.Equal(Width, decoded.Width);
            Assert.Equal(Height, decoded.Height);
        }

        [Fact]
        public void Encode_Layers_AsInTheReadme()
        {
            var p = new ParameterList();
            p["rate"] = "2";
            p["Alayers"] = "0.5 +2 2.0 +1";
            p["Aptype"] = "res";

            var decoded = J2kImage.FromBytes(J2kImage.ToBytes(Source(Samples()), p));

            Assert.Equal(Components, decoded.NumberOfComponents);
        }

        [Fact]
        public void Encode_EmptyList_IsTheSameAsNoList()
        {
            var samples = Samples();

            Assert.Equal(J2kImage.ToBytes(Source(samples), (ParameterList?)null), J2kImage.ToBytes(Source(samples), new ParameterList()));
        }

        [Fact]
        public void Encode_DoesNotChangeTheCallersList()
        {
            var p = new ParameterList();
            p["lossless"] = "on";

            J2kImage.ToBytes(Source(Samples()), p);

            Assert.Equal(new[] { "lossless" }, p.Keys);
            Assert.Null(p.DefaultParameterList);
        }

        [Fact]
        public void Decode_WithTheDefaultList()
        {
            // GetDefaultDecoderParameterList() holds the defaults as its own entries, and has no defaults behind it either
            var data = J2kImage.ToBytes(Source(Samples()), J2kImage.GetDefaultEncoderParameterList());

            AssertSame(J2kImage.FromBytes(data), J2kImage.FromBytes(data, J2kImage.GetDefaultDecoderParameterList()));
        }

        [Fact]
        public void Decode_WithABareList()
        {
            var samples = Samples();
            var lossless = new ParameterList();
            lossless["lossless"] = "on";
            var data = J2kImage.ToBytes(Source(samples), lossless);

            AssertSame(samples, J2kImage.FromBytes(data, new ParameterList()));

            var reduced = new ParameterList();
            reduced["res"] = "1";
            using var stream = new MemoryStream(data);
            var small = J2kImage.FromStream(stream, reduced);
            Assert.True(small.Width < Width);
        }
    }
}
