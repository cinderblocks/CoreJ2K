// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Decoding at a lower resolution: the image is the lowest resolutions of every tile, each placed where the full-size tile would
    /// be, scaled down. The tile and the image origin of a reduced decode were taken at full size, so every tile but the first was
    /// put off the image, and an image with an origin was shifted.
    /// </summary>
    public class ReducedResolutionTests
    {
        // one value per tile: a constant stays itself at every resolution of the 5/3 wavelet, so a reduced decode must give back the
        // value of the tile that every pixel belongs to
        private static int TileValue(int tile, int x, int y) => 20 + 37 * ((x / tile) + 5 * (y / tile)) % 230;

        private static byte[] EncodeTiles(int width, int height, int tile, int offsetX, int offsetY, int levels)
        {
            var plane = new int[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    plane[y * width + x] = TileValue(tile, x + offsetX, y + offsetY);

            var image = new InterleavedImageSource(width, height, 1, 8, new[] { false }, new[] { plane.Select(v => v - 128).ToArray() });
            var parameters = new ParameterList(J2kImage.GetDefaultEncoderParameterList());
            parameters["lossless"] = "on";
            parameters["verbose"] = "off";
            parameters["Mct"] = "off";
            parameters["Wlev"] = levels.ToString();
            parameters["tiles"] = $"{tile} {tile}";
            parameters["ref"] = $"{offsetX} {offsetY}";
            return J2kImage.ToBytes(image, null, parameters);
        }

        [Theory]
        [InlineData(128, 96, 64, 0, 0, 3, 1)]
        [InlineData(128, 96, 64, 0, 0, 3, 2)]
        [InlineData(100, 70, 32, 0, 0, 3, 1)]
        [InlineData(100, 70, 32, 5, 3, 3, 1)]
        [InlineData(100, 70, 32, 9, 13, 3, 2)]
        [InlineData(90, 60, 1000, 7, 11, 3, 1)] // one tile, an image origin
        public void ReducedDecode_PlacesEveryTile(int width, int height, int tile, int offsetX, int offsetY, int levels, int dropped)
        {
            var data = EncodeTiles(width, height, tile, offsetX, offsetY, levels);
            var step = 1 << dropped;
            var decoded = J2kImage.FromBytes(data, new J2KDecoderConfiguration().WithResolutionLevel(levels - dropped));

            // the reduced image covers the reduced grid from ceil(origin / step) to ceil(end / step)
            var firstX = (offsetX + step - 1) / step;
            var firstY = (offsetY + step - 1) / step;
            Assert.Equal(((offsetX + width + step - 1) / step - firstX, (offsetY + height + step - 1) / step - firstY), (decoded.Width, decoded.Height));
            var samples = decoded.GetComponent(0);
            for (var y = 0; y < decoded.Height; y++)
                for (var x = 0; x < decoded.Width; x++)
                {
                    var expected = TileValue(tile, (firstX + x) * step, (firstY + y) * step);
                    Assert.True(samples[y * decoded.Width + x] == expected, $"({x},{y}) is {samples[y * decoded.Width + x]}, the tile there is {expected}");
                }
        }

        /// <summary>The number of decomposition levels written in the first COD marker segment of a codestream.</summary>
        private static int DecompositionLevels(byte[] file)
        {
            var d = ScenarioRunner.Codestream(file);
            for (var i = 2; i + 3 < d.Length && d[i + 1] != 0x90; i += 2 + ((d[i + 2] << 8) | d[i + 3]))
                if (d[i + 1] == 0x52) return d[i + 9];
            throw new InvalidOperationException("No COD marker segment.");
        }

        [OpenJpegFact]
        public void ReducedResolution_DecodesAlikeInOpenJpeg()
        {
            var log = new ScenarioRunner.FailureLog();
            var start = ScenarioRunner.SweepStart();
            var count = ScenarioRunner.SweepCount(60);
            for (var seed = start; seed < start + count; seed++)
            {
                var scenario = CodingScenario.FromSeed(seed);
                var cause = Check(scenario);
                if (cause != null) log.Add(cause, scenario);
            }

            var path = Environment.GetEnvironmentVariable("COREJ2K_SWEEP_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path + ".reduced", log.Report(int.MaxValue));
            Assert.True(log.Count == 0, log.Report());
        }

        /// <summary>CoreJ2K encodes; CoreJ2K and OpenJPEG decode at each lower resolution and must agree.</summary>
        internal static string? Check(CodingScenario scenario)
        {
            if (OpenJpegDifferentialTests.OpenJpegMisreads(scenario)) return null;
            byte[] data;
            try
            {
                data = ScenarioRunner.Encode(scenario, scenario.MakeImage());
            }
            catch (Exception)
            {
                return null; // encode failures are the round-trip sweep's business
            }

            var levels = DecompositionLevels(data);
            var extension = scenario.FileFormat ? "jp2" : "j2k";
            for (var dropped = 1; dropped <= Math.Min(levels, 2); dropped++)
            {
                var theirs = OpenJpegCodec.Decode(data, extension, new[] { "-r", dropped.ToString() });
                if (theirs.Error != null) continue; // OpenJPEG refuses some small reduced images

                int width, height;
                int[][] ours;
                try
                {
                    using var image = J2kImage.FromBytes(data, new J2KDecoderConfiguration().WithResolutionLevel(levels - dropped));
                    width = image.Width;
                    height = image.Height;
                    ours = Enumerable.Range(0, image.NumberOfComponents).Select(image.GetComponent).ToArray();
                }
                catch (Exception e)
                {
                    return $"CoreJ2K cannot decode {dropped} level(s) down: " + ScenarioRunner.Normalise(ScenarioRunner.ShortMessage(e));
                }

                if (width != theirs.Width || height != theirs.Height || ours.Length != theirs.Samples.Length)
                    return $"reduced shape {width}x{height}x{ours.Length} against opj {theirs.Width}x{theirs.Height}x{theirs.Samples.Length}";

                var tolerance = scenario.ExpectExact ? 0 : Math.Max(1, 1 << Math.Max(0, scenario.Bits - 8)) * (scenario.Roi ? 3 : 1);
                var diff = ScenarioRunner.Compare(theirs.Samples, ours, width, scenario.Bits, tolerance);
                if (diff.Any) return (scenario.ExpectExact ? "reduced lossless decode differs from opj" : "reduced lossy decode differs from opj");
            }
            return null;
        }
    }
}
