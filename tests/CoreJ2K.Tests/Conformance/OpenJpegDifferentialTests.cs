// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Checks CoreJ2K against OpenJPEG, an independent implementation of the standard, over the random scenarios of
    /// <see cref="CodingScenario"/>: what CoreJ2K writes must decode the same in OpenJPEG as in CoreJ2K, and what OpenJPEG writes
    /// must decode the same in CoreJ2K. Skipped when the OpenJPEG tools are not installed (see <see cref="OpenJpegTools"/>).
    /// </summary>
    public class OpenJpegDifferentialTests
    {
        private readonly ITestOutputHelper _output;

        public OpenJpegDifferentialTests(ITestOutputHelper output) => _output = output;

        [OpenJpegFact]
        public void CoreJ2KEncodes_DecodeAlikeInOpenJpeg()
        {
            var log = new ScenarioRunner.FailureLog();
            var start = ScenarioRunner.SweepStart();
            var count = ScenarioRunner.SweepCount(60);
            for (var seed = start; seed < start + count; seed++)
            {
                var scenario = CodingScenario.FromSeed(seed);
                var cause = CheckOurEncode(scenario);
                if (cause != null) log.Add(cause, scenario);
            }
            Report(log, "opj-decode");
        }

        [OpenJpegFact]
        public void OpenJpegEncodes_DecodeAlikeInCoreJ2K()
        {
            var log = new ScenarioRunner.FailureLog();
            var start = ScenarioRunner.SweepStart();
            var count = ScenarioRunner.SweepCount(60);
            for (var seed = start; seed < start + count; seed++)
            {
                var scenario = CodingScenario.FromSeed(seed);
                var cause = CheckTheirEncode(scenario);
                if (cause != null) log.Add(cause, scenario);
            }
            Report(log, "opj-encode");
        }

        private static void Report(ScenarioRunner.FailureLog log, string name)
        {
            var report = log.Report();
            var path = Environment.GetEnvironmentVariable("COREJ2K_SWEEP_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path + "." + name, log.Report(int.MaxValue));
            Assert.True(log.Count == 0, report);
        }

        /// <summary>The largest difference two decoders may have on a lossy file (9/7 arithmetic is not specified to the last bit, and how the shifted coefficients of a region of interest are reconstructed is up to the decoder).</summary>
        private static int LossyTolerance(CodingScenario s) => Math.Max(1, (1 << Math.Max(0, s.Bits - 8))) * (s.Roi ? 3 : 1) ; 

        /// <summary>
        /// OpenJPEG decides which passes of a code-block are raw (selective bypass) without the Maxshift shift, so it misreads files that
        /// combine the two; CoreJ2K and ffmpeg's decoder read them correctly. Scenarios with both are left out of the comparison.
        /// </summary>
        internal static bool OpenJpegMisreadsBypassWithRoi(CodingScenario s) => s.Roi && s.Bypass;

        /// <summary>
        /// CoreJ2K numbers the PPT marker segments of each tile-part header from 0 (the index is "relative to all other PPT marker
        /// segments present in the current header"); OpenJPEG numbers them across all the tile-parts of a tile and refuses a file that
        /// starts over ("Zppt already read"). Which is meant is not settled here, so such files are left out of the comparison.
        /// </summary>
        internal static bool PptIndexAcrossTileParts(CodingScenario s) => s.PackedHeadersInTile && s.PacketsPerTilePart > 0;

        /// <summary>
        /// With tile-parts, CoreJ2K writes the first tile-part of every tile, then the second of every tile, and so on, and the packed
        /// packet headers of a PPM marker follow that order. OpenJPEG takes the packet headers of a tile from the PPM data one after the
        /// other, so it reads the headers of another tile's tile-part and fails. CoreJ2K reads these files; the standard allows the layout.
        /// </summary>
        internal static bool PpmWithInterleavedTileParts(CodingScenario s) => s.PackedHeadersInMain && s.PacketsPerTilePart > 0 && s.TileCount > 1;

        /// <summary>Whether OpenJPEG is known to misread (or refuse) files of the scenario, so that what it decodes says nothing about CoreJ2K.</summary>
        internal static bool OpenJpegMisreads(CodingScenario s) =>
            OpenJpegMisreadsBypassWithRoi(s) || PptIndexAcrossTileParts(s) || PpmWithInterleavedTileParts(s);

        /// <summary>CoreJ2K encodes, OpenJPEG decodes.</summary>
        internal static string? CheckOurEncode(CodingScenario scenario)
        {
            if (OpenJpegMisreads(scenario)) return null;
            var image = scenario.MakeImage();
            byte[] data;
            try
            {
                data = ScenarioRunner.Encode(scenario, image);
            }
            catch (Exception e)
            {
                for (var inner = (Exception?)e; inner != null; inner = inner.InnerException)
                    if (inner is ArgumentException && inner.Message.StartsWith("ROI coding with Maxshift supports at most", StringComparison.Ordinal))
                        return null;
                return null; // encode failures are the round-trip sweep's business
            }

            var theirs = OpenJpegCodec.Decode(data, scenario.FileFormat ? "jp2" : "j2k");
            if (theirs.Error != null)
            {
                // OpenJPEG fails on a tile-part with packed packet headers and no data after SOD (every packet in it is empty)
                if ((scenario.PackedHeadersInMain || scenario.PackedHeadersInTile) && ScenarioRunner.HasTilePartWithoutData(data))
                    return null;
                return "opj cannot decode: " + ScenarioRunner.Normalise(theirs.Error);
            }
            if (theirs.Width != scenario.Width || theirs.Height != scenario.Height || theirs.Samples.Length != scenario.Components)
                return $"opj shape {theirs.Width}x{theirs.Height}x{theirs.Samples.Length}";

            if (scenario.ExpectExact)
            {
                var diff = ScenarioRunner.Compare(image.OffsetBinary(), theirs.Samples, scenario.Width, scenario.Bits);
                return diff.Any ? "opj decode of lossless differs from the source" : null;
            }

            int[][] ours;
            try
            {
                ours = ScenarioRunner.Decode(data).Samples;
            }
            catch (Exception)
            {
                return null; // the round-trip sweep reports decode failures
            }
            var lossy = ScenarioRunner.Compare(ours, theirs.Samples, scenario.Width, scenario.Bits, LossyTolerance(scenario));
            if (lossy.Any && scenario.Roi)
            {
                // a decoder reconstructs the shifted coefficients of a region as it sees fit; what is asked is that each is about as
                // close to the source as the other
                var mine = ScenarioRunner.Compare(image.OffsetBinary(), ours, scenario.Width, scenario.Bits).Psnr;
                var other = ScenarioRunner.Compare(image.OffsetBinary(), theirs.Samples, scenario.Width, scenario.Bits).Psnr;
                if (Math.Abs(mine - other) <= 3) return null;
            }
            return lossy.Any ? "opj and CoreJ2K decode a lossy file differently" : null;
        }

        /// <summary>
        /// <c>opj_compress</c> writes files that neither it nor CoreJ2K decodes back to the source when a tile (at the edge of the image)
        /// is narrower than the wavelet decomposition is deep (or only a few samples wide), so such scenarios are left out when OpenJPEG is the encoder.
        /// </summary>
        private static bool OpenJpegEncodesNarrowTilesWrongly(CodingScenario s) =>
            s.SmallestTileDimension < 4 || (s.Tiled && s.SmallestTileDimension < (1 << s.Levels));


        /// <summary>
        /// OpenJPEG encodes, CoreJ2K decodes. The file is also decoded by OpenJPEG, and the two decodes must agree (exactly for a
        /// lossless file): a mistake in OpenJPEG's encoder shows in both and is not held against CoreJ2K's decoder.
        /// </summary>
        internal static string? CheckTheirEncode(CodingScenario scenario)
        {
            if (OpenJpegEncodesNarrowTilesWrongly(scenario)) return null;
            var image = scenario.MakeImage();
            var (data, error) = OpenJpegCodec.Encode(scenario, image);
            if (data == null)
                return null; // OpenJPEG declined the scenario (its limits differ from CoreJ2K's)

            var theirs = OpenJpegCodec.Decode(data, "j2k");
            if (theirs.Error != null)
                return null; // not a valid file, whatever the cause

            // OpenJPEG's own decode must give the source back (lossless) or come close (lossy) before its file says anything about
            // CoreJ2K: on some data its encoder overflows, and a stream that damaged decodes differently in every decoder
            var vsSource = ScenarioRunner.Compare(image.OffsetBinary(), theirs.Samples, scenario.Width, scenario.Bits);
            if (scenario.ExpectExact ? vsSource.Any : vsSource.Psnr < 30)
                return null;

            int[][] ours;
            int width, height;
            try
            {
                (width, height, ours) = ScenarioRunner.Decode(data);
            }
            catch (Exception e)
            {
                return "CoreJ2K cannot decode an opj file: " + ScenarioRunner.Normalise(ScenarioRunner.ShortMessage(e));
            }

            if (width != scenario.Width || height != scenario.Height || ours.Length != scenario.Components)
                return $"CoreJ2K shape {width}x{height}x{ours.Length}";

            var diff = ScenarioRunner.Compare(theirs.Samples, ours, width, scenario.Bits, scenario.ExpectExact ? 0 : LossyTolerance(scenario));
            return diff.Any ? (scenario.ExpectExact ? "CoreJ2K and opj decode an opj lossless file differently" : "CoreJ2K and opj decode an opj lossy file differently") : null;
        }
    }
}
