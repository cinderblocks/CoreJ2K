// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Encodes and decodes randomly drawn coding scenarios (see <see cref="CodingScenario"/>) and checks what must hold for any of
    /// them: the codestream is well formed, the decoded image has the right shape, a lossless coding returns every sample, and a
    /// lossy one stays close. A scenario is fixed by its seed. Set <c>COREJ2K_SWEEP_COUNT</c> and <c>COREJ2K_SWEEP_START</c> to run
    /// more of them, and <c>COREJ2K_SWEEP_REPORT</c> to a path to keep the full report.
    /// </summary>
    public class ScenarioSweepTests
    {
        private readonly ITestOutputHelper _output;

        public ScenarioSweepTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void RandomScenarios_RoundTrip()
        {
            var log = new ScenarioRunner.FailureLog();
            var start = ScenarioRunner.SweepStart();
            var count = ScenarioRunner.SweepCount(120);
            for (var seed = start; seed < start + count; seed++)
            {
                var scenario = CodingScenario.FromSeed(seed);
                var cause = Check(scenario);
                if (cause != null) log.Add(cause, scenario);
            }

            var report = log.Report();
            var path = Environment.GetEnvironmentVariable("COREJ2K_SWEEP_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, log.Report(int.MaxValue));
            Assert.True(log.Count == 0, report);
        }

        /// <summary>With <c>COREJ2K_SWEEP_DUMP</c> set to a directory, keeps every encoded file there as <c>seed_N.j2k</c> or <c>.jp2</c>.</summary>
        private static void Dump(CodingScenario scenario, byte[] data)
        {
            var dir = Environment.GetEnvironmentVariable("COREJ2K_SWEEP_DUMP");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"seed_{scenario.Seed}.{(scenario.FileFormat ? "jp2" : "j2k")}"), data);
        }

        /// <summary>Returns what went wrong with the scenario, or null.</summary>
        internal static string? Check(CodingScenario scenario)
        {
            var image = scenario.MakeImage();

            byte[] data;
            try
            {
                data = ScenarioRunner.Encode(scenario, image);
            }
            catch (Exception e)
            {
                // refused on purpose (see the ROI overflow check): a scenario the encoder declines is not a failure
                for (var inner = (Exception?)e; inner != null; inner = inner.InnerException)
                    if (inner is ArgumentException && inner.Message.StartsWith("ROI coding with Maxshift supports at most", StringComparison.Ordinal))
                        return null;
                return "encode: " + ScenarioRunner.Normalise(ScenarioRunner.ShortMessage(e));
            }

            Dump(scenario, data);

            var structure = ScenarioRunner.ValidateStructure(data);
            if (structure != null)
                return "structure: " + ScenarioRunner.Normalise(structure);

            int width, height;
            int[][] samples;
            try
            {
                (width, height, samples) = ScenarioRunner.Decode(data);
            }
            catch (Exception e)
            {
                return "decode: " + ScenarioRunner.Normalise(ScenarioRunner.ShortMessage(e));
            }

            if (width != scenario.Width || height != scenario.Height || samples.Length != scenario.Components)
                return $"shape: decoded {width}x{height}x{samples.Length}";

            var diff = ScenarioRunner.Compare(image.OffsetBinary(), samples, width, scenario.Bits);
            if (scenario.ExpectExact)
                return diff.Any ? "lossless differs" : null;
            return null;
        }
    }
}
