// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CoreJ2K.j2k.codestream;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>The samples of two images that differ, and where the first difference is.</summary>
    internal sealed class SampleDifference
    {
        public long Count { get; init; }
        public long Total { get; init; }
        public int MaxAbsolute { get; init; }
        public string First { get; init; } = string.Empty;
        public double Psnr { get; init; }

        public bool Any => Count > 0;

        public override string ToString() =>
            Count == 0 ? "identical" : $"{Count}/{Total} samples differ, max {MaxAbsolute}, PSNR {Psnr:F1} dB, first at {First}";
    }

    internal static class ScenarioRunner
    {
        /// <summary>The environment variable that sets how many random scenarios the sweep tests draw.</summary>
        public const string CountVariable = "COREJ2K_SWEEP_COUNT";

        /// <summary>The environment variable that sets the first seed of a sweep.</summary>
        public const string StartVariable = "COREJ2K_SWEEP_START";

        public static int SweepCount(int defaultCount) =>
            int.TryParse(Environment.GetEnvironmentVariable(CountVariable), out var n) && n > 0 ? n : defaultCount;

        public static int SweepStart() =>
            int.TryParse(Environment.GetEnvironmentVariable(StartVariable), out var n) ? n : 0;

        public static byte[] Encode(CodingScenario scenario, SampleImage image)
        {
            var pl = scenario.ToParameterList();
            return J2kImage.ToBytes(image.ToSource(), null, pl)
                   ?? throw new InvalidOperationException("The encoder returned no data.");
        }

        /// <summary>Decodes with CoreJ2K; the samples are unsigned values, row by row.</summary>
        public static (int Width, int Height, int[][] Samples) Decode(byte[] data)
        {
            using var image = J2kImage.FromBytes(data);
            var samples = new int[image.NumberOfComponents][];
            for (var c = 0; c < samples.Length; c++) samples[c] = image.GetComponent(c);
            return (image.Width, image.Height, samples);
        }

        public static SampleDifference Compare(int[][] expected, int[][] actual, int width, int bits, int tolerance = 0)
        {
            long count = 0, total = 0;
            var max = 0;
            double squared = 0;
            var first = string.Empty;
            for (var c = 0; c < expected.Length; c++)
            {
                var e = expected[c];
                var a = actual[c];
                for (var i = 0; i < e.Length; i++)
                {
                    total++;
                    var d = Math.Abs(e[i] - a[i]);
                    squared += (double)d * d;
                    if (d > tolerance)
                    {
                        if (count++ == 0) first = $"component {c} x={i % width} y={i / width} (expected {e[i]}, got {a[i]})";
                    }
                    if (d > max) max = d;
                }
            }
            var peak = (double)((1L << bits) - 1);
            var mse = total == 0 ? 0 : squared / total;
            return new SampleDifference
            {
                Count = count,
                Total = total,
                MaxAbsolute = max,
                First = first,
                Psnr = mse == 0 ? double.PositiveInfinity : 10 * Math.Log10(peak * peak / mse),
            };
        }

        /// <summary>The bytes of the codestream inside a JP2 file (or the data itself when it is a bare codestream).</summary>
        public static byte[] Codestream(byte[] data)
        {
            for (var i = 0; i + 3 < data.Length; i++)
                if (data[i] == 0xFF && data[i + 1] == 0x4F && data[i + 2] == 0xFF && data[i + 3] == 0x51)
                    return i == 0 ? data : data.AsSpan(i).ToArray();
            return data;
        }

        /// <summary>Whether some tile-part has nothing after its SOD marker (all its packets are empty and their headers are packed elsewhere).</summary>
        public static bool HasTilePartWithoutData(byte[] data)
        {
            var d = Codestream(data);
            var i = 2;
            while (i + 3 < d.Length && !(d[i] == 0xFF && d[i + 1] == 0x90))
                i += 2 + ((d[i + 2] << 8) | d[i + 3]);
            while (i + 11 < d.Length && d[i] == 0xFF && d[i + 1] == 0x90)
            {
                var psot = (int)(((uint)d[i + 6] << 24) | ((uint)d[i + 7] << 16) | ((uint)d[i + 8] << 8) | d[i + 9]);
                if (psot < 14) return false;
                var j = i + 12;
                while (j + 1 < i + psot && !(d[j] == 0xFF && d[j + 1] == 0x93))
                    j += 2 + ((d[j + 2] << 8) | d[j + 3]);
                if (j + 2 >= i + psot) return true;
                i += psot;
            }
            return false;
        }

        /// <summary>Checks the codestream's structure with the independent validator; returns the errors, if any.</summary>
        public static string? ValidateStructure(byte[] data)
        {
            var validator = new CodestreamValidator();
            validator.ValidateCodestream(Codestream(data));
            return validator.HasErrors ? validator.GetValidationReport() : null;
        }

        /// <summary>Replaces numbers so that failures differing only in sizes and offsets group together.</summary>
        public static string Normalise(string message)
        {
            // for a validation report, the first error is the cause
            var lines = message.Split('\n');
            var errors = Array.FindIndex(lines, l => l.StartsWith("ERRORS", StringComparison.Ordinal));
            if (errors >= 0 && errors + 1 < lines.Length)
                message = lines[errors + 1].Trim();
            return System.Text.RegularExpressions.Regex.Replace(message, @"\d+", "N");
        }

        public static string ShortMessage(Exception e)
        {
            var inner = e;
            while (inner.InnerException != null) inner = inner.InnerException;
            var message = inner.Message.Split('\n')[0].Trim();
            return $"{inner.GetType().Name}: {message}";
        }

        /// <summary>A scratch directory removed on disposal.</summary>
        public sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "corej2k-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public string File(string name) => System.IO.Path.Combine(Path, name);

            public void Dispose()
            {
                try { Directory.Delete(Path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>Collects failures by cause so a sweep reports each distinct problem once, with the seeds that show it.</summary>
        public sealed class FailureLog
        {
            private readonly SortedDictionary<string, List<(int Seed, string Description)>> _byCause = new SortedDictionary<string, List<(int, string)>>();

            public int Count { get; private set; }

            public void Add(string cause, CodingScenario scenario)
            {
                Count++;
                if (!_byCause.TryGetValue(cause, out var list)) _byCause[cause] = list = new List<(int, string)>();
                list.Add((scenario.Seed, scenario.Describe()));
            }

            public string Report(int examplesPerCause = 3)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{Count} failure(s) of {_byCause.Count} kind(s):");
                foreach (var kv in _byCause.OrderByDescending(k => k.Value.Count))
                {
                    sb.AppendLine($"* {kv.Value.Count}x {kv.Key}");
                    foreach (var (seed, description) in kv.Value.Take(examplesPerCause))
                        sb.AppendLine($"    {description}");
                }
                return sb.ToString();
            }
        }
    }
}
