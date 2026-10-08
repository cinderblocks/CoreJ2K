// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Explains one scenario in detail: encodes it with CoreJ2K and with OpenJPEG, decodes each file with both decoders, and prints how
    /// every pairing compares with the source image. Run with <c>COREJ2K_TRIAGE_SEED</c> set to a seed from a sweep report (and
    /// <c>COREJ2K_TRIAGE_DIR</c> to keep the files); otherwise it does nothing.
    /// </summary>
    public class ScenarioTriage
    {
        private readonly ITestOutputHelper _output;

        public ScenarioTriage(ITestOutputHelper output) => _output = output;

        [Fact]
        public void Triage()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("COREJ2K_TRIAGE_SEED"), out var seed))
                return;

            var scenario = CodingScenario.FromSeed(seed);
            var image = scenario.MakeImage();
            var expected = image.OffsetBinary();
            var tol = Math.Max(1, 1 << Math.Max(0, scenario.Bits - 8));
            var keep = Environment.GetEnvironmentVariable("COREJ2K_TRIAGE_DIR");
            var ext = scenario.FileFormat ? "jp2" : "j2k";
            _output.WriteLine("SCENARIO " + scenario.Describe());
            _output.WriteLine($"SCENARIO exact={scenario.ExpectExact} tolerance-between-decoders={tol}");

            byte[]? ours = null;
            try
            {
                ours = ScenarioRunner.Encode(scenario, image);
                _output.WriteLine($"SCENARIO CoreJ2K encode: {ours.Length} bytes");
                if (keep != null) { Directory.CreateDirectory(keep); File.WriteAllBytes(Path.Combine(keep, $"seed_{seed}_core.{ext}"), ours); }
                var structure = ScenarioRunner.ValidateStructure(ours);
                if (structure != null) _output.WriteLine("SCENARIO structure: " + structure.Replace("\n", " | "));
            }
            catch (Exception e)
            {
                _output.WriteLine("SCENARIO CoreJ2K encode failed: " + e.GetType().Name + ": " + e.Message);
            }

            if (ours != null)
            {
                Report("CoreJ2K file, CoreJ2K decode", expected, () => ScenarioRunner.Decode(ours).Samples, scenario, tol);
                if (OpenJpegTools.Available)
                {
                    Report("CoreJ2K file, opj decode", expected, () => Throw(OpenJpegCodec.Decode(ours, ext)), scenario, tol);
                    Pair("CoreJ2K file: CoreJ2K vs opj decode", () => ScenarioRunner.Decode(ours).Samples, () => Throw(OpenJpegCodec.Decode(ours, ext)), scenario);
                }
            }

            if (OpenJpegTools.Available)
            {
                var (theirs, error) = OpenJpegCodec.Encode(scenario, image, ext);
                if (theirs == null)
                {
                    _output.WriteLine("SCENARIO opj encode failed: " + error);
                }
                else
                {
                    _output.WriteLine($"SCENARIO opj encode: {theirs.Length} bytes");
                    if (keep != null) File.WriteAllBytes(Path.Combine(keep, $"seed_{seed}_opj.{ext}"), theirs);
                    Report("opj file, CoreJ2K decode", expected, () => ScenarioRunner.Decode(theirs).Samples, scenario, tol);
                    Report("opj file, opj decode", expected, () => Throw(OpenJpegCodec.Decode(theirs, ext)), scenario, tol);
                    Pair("opj file: CoreJ2K vs opj decode", () => ScenarioRunner.Decode(theirs).Samples, () => Throw(OpenJpegCodec.Decode(theirs, ext)), scenario);
                }
            }
        }

        private void Pair(string label, Func<int[][]> a, Func<int[][]> b, CodingScenario s)
        {
            try
            {
                var x = a();
                var y = b();
                var d = ScenarioRunner.Compare(x, y, s.Width, s.Bits);
                var histogram = new int[6];
                for (var c = 0; c < x.Length; c++)
                    for (var i = 0; i < x[c].Length; i++)
                        histogram[Math.Min(5, Math.Abs(x[c][i] - y[c][i]))]++;
                _output.WriteLine($"SCENARIO {label}: {d}; |diff| histogram 0..4,5+: {string.Join(" ", histogram)}");
            }
            catch (Exception e)
            {
                _output.WriteLine($"SCENARIO {label}: FAILED {e.GetType().Name}");
            }
        }

        private static int[][] Throw(OpenJpegCodec.DecodeResult r) =>
            r.Error != null ? throw new InvalidOperationException(r.Error) : r.Samples;

        private void Report(string label, int[][] expected, Func<int[][]> decode, CodingScenario s, int tol)
        {
            try
            {
                var got = decode();
                if (got.Length != expected.Length) { _output.WriteLine($"SCENARIO {label}: {got.Length} components, expected {expected.Length}"); return; }
                var exact = ScenarioRunner.Compare(expected, got, s.Width, s.Bits);
                var perComponent = string.Join(" ", Enumerable.Range(0, got.Length).Select(c =>
                {
                    var d = ScenarioRunner.Compare(new[] { expected[c] }, new[] { got[c] }, s.Width, s.Bits);
                    return $"c{c}:max{d.MaxAbsolute}";
                }));
                var bias = string.Join(" ", Enumerable.Range(0, got.Length).Select(c =>
                    $"c{c}:{Enumerable.Range(0, got[c].Length).Average(i => (double)(got[c][i] - expected[c][i])):+0.00;-0.00}"));
                _output.WriteLine($"SCENARIO {label}: {exact} [{perComponent}] mean error [{bias}]");
            }
            catch (Exception e)
            {
                _output.WriteLine($"SCENARIO {label}: FAILED {e.GetType().Name}: {e.Message.Split('\n')[0]}");
            }
        }
    }
}
