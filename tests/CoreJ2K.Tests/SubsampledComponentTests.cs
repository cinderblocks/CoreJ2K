// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.Configuration;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Components that are subsampled have fewer samples than the image has pixels. The decoder assumed they had as many, so a codestream
    /// with subsampled components threw, or gave an image mostly made of empty rows; an image whose components are all subsampled alike
    /// came out at the size of the reference grid rather than the size of the components. The components are now put on the grid of
    /// the finest one: it keeps its samples, and the others repeat theirs.
    /// </summary>
    public class SubsampledComponentTests
    {
        private static string TestFile(params string[] path) =>
            Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles" }.Concat(path).ToArray());

        /// <summary>What the subsampled streams of <c>TestFiles/subsampled</c> hold: component c at its own sample (x, y).</summary>
        private static int Sample(int c, int x, int y) => (x * 7 + y * 13 + c * 50 + x * y) & 255;

        [Theory]
        [InlineData("mixed-2x2-chroma.j2k", new[] { 1, 2, 2 }, new[] { 1, 2, 2 })]
        [InlineData("mixed-2x1-and-1x2.j2k", new[] { 1, 2, 1 }, new[] { 1, 1, 2 })]
        [InlineData("mixed-3x3-and-4x4.j2k", new[] { 1, 3, 4 }, new[] { 1, 3, 4 })]
        public void CoarserComponents_RepeatTheirSamplesOnTheGridOfTheFinestOne(string file, int[] factorsX, int[] factorsY)
        {
            var image = J2kImage.FromBytes(File.ReadAllBytes(TestFile("subsampled", file)));

            Assert.Equal((36, 24, 3), (image.Width, image.Height, image.NumberOfComponents));
            for (var c = 0; c < 3; c++)
            {
                var samples = image.GetComponent(c);
                for (var y = 0; y < 24; y++)
                    for (var x = 0; x < 36; x++)
                        Assert.True(samples[y * 36 + x] == Sample(c, x / factorsX[c], y / factorsY[c]),
                            $"component {c} at ({x},{y}) is {samples[y * 36 + x]}, expected the sample ({x / factorsX[c]},{y / factorsY[c]}) of the component");
            }
        }

        [Fact]
        public void ComponentsAllSubsampledAlike_GiveAnImageAsLargeAsTheComponents()
        {
            var image = J2kImage.FromBytes(File.ReadAllBytes(TestFile("subsampled", "all-2x2.j2k")));

            // 36 x 24 on the reference grid, 18 x 12 samples in each component
            Assert.Equal((18, 12, 3), (image.Width, image.Height, image.NumberOfComponents));
            for (var c = 0; c < 3; c++)
            {
                var samples = image.GetComponent(c);
                for (var y = 0; y < 12; y++)
                    for (var x = 0; x < 18; x++)
                        Assert.Equal(Sample(c, x, y), samples[y * 18 + x]);
            }
        }
    }

    /// <summary>
    /// A few of the streams of the ISO/IEC 15444-4 conformance suite (see <c>TestFiles/iso15444-4/ATTRIBUTION.md</c>) that CoreJ2K
    /// could not decode, with the reference images the suite gives for them.
    /// </summary>
    public class Iso15444PartFourStreamTests
    {
        private static string File(string name) =>
            Path.Combine(AppContext.BaseDirectory, "TestFiles", "iso15444-4", name);

        private static CoreJ2K.Util.InterleavedImage Decode(string stream, J2KDecoderConfiguration? configuration = null) =>
            configuration == null
                ? J2kImage.FromBytes(System.IO.File.ReadAllBytes(File(stream)))
                : J2kImage.FromBytes(System.IO.File.ReadAllBytes(File(stream)), configuration);

        /// <summary>
        /// Reads a PGX reference image as unsigned values. The headers of the suite's files are not all alike (<c>PG ML +8 128 128</c>,
        /// <c>PG ML 8 17 37</c>, <c>PG ML -4 256 256</c>), so the sign, when there is one, is taken from the front of the depth.
        /// </summary>
        private static (int Width, int Height, int[] Samples) ReadReference(string name)
        {
            var bytes = System.IO.File.ReadAllBytes(File(name));
            var eol = Array.IndexOf(bytes, (byte)'\n');
            var fields = System.Text.Encoding.ASCII.GetString(bytes, 0, eol).Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToList();
            var signed = false;
            if (fields[0][0] == '+' || fields[0][0] == '-')
            {
                signed = fields[0][0] == '-';
                fields[0] = fields[0].Substring(1);
                if (fields[0].Length == 0) fields.RemoveAt(0);
            }
            var bits = int.Parse(fields[0]);
            var width = int.Parse(fields[1]);
            var height = int.Parse(fields[2]);
            var samples = new int[width * height];
            for (var i = 0; i < samples.Length; i++)
            {
                var value = bits > 8 ? (bytes[eol + 1 + 2 * i] << 8) | bytes[eol + 2 + 2 * i] : bytes[eol + 1 + i];
                if (signed)
                {
                    var shift = 32 - bits;
                    value = ((value << shift) >> shift) + (1 << (bits - 1));
                }
                samples[i] = value;
            }
            return (width, height, samples);
        }

        private static void AssertMatches(CoreJ2K.Util.InterleavedImage image, int component, string reference)
        {
            var (width, height, expected) = ReadReference(reference);
            Assert.Equal((width, height), (image.Width, image.Height));
            var samples = image.GetComponent(component);
            for (var i = 0; i < expected.Length; i++)
                if (samples[i] != expected[i])
                    Assert.Fail($"{reference}: sample {i} (x={i % width}, y={i / width}) is {samples[i]}, the reference has {expected[i]}");
        }

        [Fact]
        public void P0_02_ASingleComponentSubsampledByTwo_IsDecodedAtTheSizeOfTheComponent()
        {
            var image = Decode("p0_02.j2k");
            Assert.Equal(1, image.NumberOfComponents);
            AssertMatches(image, 0, "c1p0_02_0.pgx");
        }

        [Fact]
        public void P1_01_ASubsampledComponentWithAnOriginThatIsNotAMultipleOfTheFactor()
        {
            var image = Decode("p1_01.j2k");
            AssertMatches(image, 0, "c1p1_01_0.pgx");
        }

        [Fact]
        public void P0_10_TilesAndTileParts_ThatDoNotSayHowManyThereAre_AndAnEmptyOne()
        {
            var image = Decode("p0_10.j2k");
            Assert.Equal(3, image.NumberOfComponents);
            for (var c = 0; c < 3; c++) AssertMatches(image, c, $"c1p0_10_{c}.pgx");
        }

        [Fact]
        public void P0_03_TiledImage_AtOneResolutionLevelDown()
        {
            // 256 x 256 in two resolutions: level 0 is the lower one, 128 x 128
            var image = Decode("p0_03.j2k", new J2KDecoderConfiguration().WithResolutionLevel(0));
            AssertMatches(image, 0, "c0p0_03r1.pgx");
        }

        [Fact]
        public void P1_07_PositionProgressionOfComponentsOfDifferentSampling_WithAnImageOrigin()
        {
            var image = Decode("p1_07.j2k");

            // component 1 is sampled at every point of the grid, component 0 at every fourth column
            Assert.Equal((8, 12, 2), (image.Width, image.Height, image.NumberOfComponents));
            var full = ReadReference("c1p1_07_1.pgx").Samples;
            Assert.Equal(full, image.GetComponent(1));

            // the image begins at column 4 of the grid, so the samples of component 0 are those of columns 4 and 8
            var narrow = ReadReference("c1p1_07_0.pgx").Samples;
            var repeated = image.GetComponent(0);
            for (var y = 0; y < 12; y++)
                for (var x = 0; x < 8; x++)
                    Assert.Equal(narrow[y * 2 + x / 4], repeated[y * 8 + x]);
        }
    }
}
