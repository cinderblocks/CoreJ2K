// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Locates the OpenJPEG command-line tools, an independent implementation of the standard to check CoreJ2K against.
    /// Looked for in <c>OPJ_BIN</c> (a directory), then on the PATH, then in the usual install directories. Tests that need them
    /// are skipped when they are not found.
    /// </summary>
    internal static class OpenJpegTools
    {
        private static readonly string[] Directories =
        {
            "/opt/local/bin", "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin",
            @"C:\Program Files\OpenJPEG\bin", @"C:\Program Files (x86)\OpenJPEG\bin",
        };

        public static string? Compress { get; } = Find("opj_compress");

        public static string? Decompress { get; } = Find("opj_decompress");

        public static string? Dump { get; } = Find("opj_dump");

        public static bool Available => Compress != null && Decompress != null;

        public const string SkipReason = "OpenJPEG (opj_compress and opj_decompress) was not found; install it or set OPJ_BIN to its directory.";

        private static string? Find(string name)
        {
            var exe = OperatingSystem.IsWindows() ? name + ".exe" : name;
            var candidates = new List<string>();
            var env = Environment.GetEnvironmentVariable("OPJ_BIN");
            if (!string.IsNullOrEmpty(env)) candidates.Add(env);
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            candidates.AddRange(Directories);
            return candidates.Select(d => Path.Combine(d, exe)).FirstOrDefault(File.Exists);
        }

        /// <summary>Runs a tool and returns its exit code and combined output; kills it after <paramref name="timeoutMs"/>.</summary>
        public static (int ExitCode, string Output) Run(string exe, IEnumerable<string> args, int timeoutMs = 60000)
        {
            var info = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args) info.ArgumentList.Add(a);

            using var process = new Process { StartInfo = info };
            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(true); } catch (InvalidOperationException) { }
                return (-1, "timed out\n" + output);
            }
            process.WaitForExit();
            return (process.ExitCode, output.ToString());
        }
    }

    /// <summary>A <see cref="FactAttribute"/> that is skipped when the OpenJPEG tools are not installed.</summary>
    internal sealed class OpenJpegFactAttribute : FactAttribute
    {
        public OpenJpegFactAttribute()
        {
            if (!OpenJpegTools.Available) Skip = OpenJpegTools.SkipReason;
        }
    }

    /// <summary>A <see cref="TheoryAttribute"/> that is skipped when the OpenJPEG tools are not installed.</summary>
    internal sealed class OpenJpegTheoryAttribute : TheoryAttribute
    {
        public OpenJpegTheoryAttribute()
        {
            if (!OpenJpegTools.Available) Skip = OpenJpegTools.SkipReason;
        }
    }
}
