// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>Encodes and decodes with the OpenJPEG command-line tools, exchanging samples through raw and PGX files.</summary>
    internal static class OpenJpegCodec
    {
        /// <summary>The result of an OpenJPEG decode: unsigned sample values per component, or why it failed.</summary>
        internal sealed class DecodeResult
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public int[][] Samples { get; init; } = Array.Empty<int[]>();
            public string? Error { get; init; }
        }

        /// <summary>Decodes a JP2 or codestream with <c>opj_decompress</c>; signed components are returned offset to unsigned.</summary>
        public static DecodeResult Decode(byte[] data, string extension, string[]? extraArguments = null)
        {
            using var dir = new ScenarioRunner.TempDirectory();
            var input = dir.File("in." + extension);
            File.WriteAllBytes(input, data);
            var args = new List<string> { "-i", input, "-o", dir.File("out.pgx") };
            if (extraArguments != null) args.AddRange(extraArguments);
            var (exit, output) = OpenJpegTools.Run(OpenJpegTools.Decompress!, args);
            var files = Directory.GetFiles(dir.Path, "out*.pgx")
                .OrderBy(f => ComponentIndex(f)).ToArray();
            if (exit != 0 || files.Length == 0)
                return new DecodeResult { Error = $"opj_decompress exit {exit}: {FirstLines(output)}" };

            var comps = files.Select(OpenJpegSamples).ToArray();
            return new DecodeResult { Width = comps[0].Width, Height = comps[0].Height, Samples = comps.Select(c => c.OffsetBinary).ToArray() };
        }

        private static (int Width, int Height, int[] OffsetBinary) OpenJpegSamples(string path)
        {
            var (w, h, _, _, samples) = SampleImage.ReadPgx(path);
            return (w, h, samples);
        }

        // out.pgx (one component) or out_0.pgx, out_1.pgx ... (several)
        private static int ComponentIndex(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var u = name.LastIndexOf('_');
            return u >= 0 && int.TryParse(name.Substring(u + 1), out var n) ? n : 0;
        }

        /// <summary>Encodes the image with <c>opj_compress</c> using the scenario's settings; returns the file or the failure.</summary>
        public static (byte[]? Data, string? Error) Encode(CodingScenario scenario, SampleImage image, string extension = "j2k")
        {
            using var dir = new ScenarioRunner.TempDirectory();
            var raw = dir.File("in.raw");
            var output = dir.File("out." + extension);
            File.WriteAllBytes(raw, image.ToRaw());
            var (exit, text) = OpenJpegTools.Run(OpenJpegTools.Compress!, scenario.ToOpjArguments(raw, output));
            if (exit != 0 || !File.Exists(output))
                return (null, $"opj_compress exit {exit}: {FirstLines(text)}");
            return (File.ReadAllBytes(output), null);
        }

        private static string FirstLines(string text) =>
            string.Join(" | ", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("[INFO]", StringComparison.Ordinal)).Take(3));
    }
}
