// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Feeds the decoder damaged files: valid ones cut short, with bytes changed, and with stretches overwritten. Whatever it makes of
    /// them, it must not hang, and the exception it throws must say the file is bad — an exception that comes from a bug in the
    /// decoder (a null reference, an index out of range, a bad cast, an arithmetic overflow, a division by zero) is a failure.
    /// <c>COREJ2K_SWEEP_COUNT</c> and <c>COREJ2K_SWEEP_START</c> choose the files, <c>COREJ2K_FUZZ_MUTATIONS</c> how many damaged copies of each, <c>COREJ2K_FUZZ_DEADLINE</c> the seconds a decode may take, and
    /// <c>COREJ2K_FUZZ_DUMP</c> a directory to keep the damaged files that fail in.
    /// </summary>
    public class MalformedStreamTests
    {
        private readonly ITestOutputHelper _output;

        public MalformedStreamTests(ITestOutputHelper output) => _output = output;

        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("COREJ2K_FUZZ_DEADLINE"), out var d) ? d : 20);

        /// <summary>Exceptions that mean the decoder broke rather than the file.</summary>
        private static bool IsDecoderBug(Exception e)
        {
            // an exception that reaches the caller as one of these is the decoder's own slip; the decoder turns the ones that a damaged
            // file causes into InvalidOperationException (keeping the original as the inner exception)
            return e is NullReferenceException || e is IndexOutOfRangeException || e is InvalidCastException
                   || e is DivideByZeroException || e is OverflowException || e is OutOfMemoryException
                   || e is KeyNotFoundException || e is ArrayTypeMismatchException || e is NotImplementedException
                   || e is InsufficientExecutionStackException;
        }

        [Fact]
        public void DamagedFiles_AreRefusedCleanly()
        {
            var log = new ScenarioRunner.FailureLog();
            var files = ScenarioRunner.SweepCount(40);
            var start = ScenarioRunner.SweepStart();
            var mutationsPerFile = int.TryParse(Environment.GetEnvironmentVariable("COREJ2K_FUZZ_MUTATIONS"), out var m) ? m : 12;
            for (var seed = start; seed < start + files; seed++)
            {
                var scenario = CodingScenario.FromSeed(seed);
                if (scenario.Width * scenario.Height * scenario.Components > 20000) continue; // keep each decode quick
                byte[] data;
                try { data = ScenarioRunner.Encode(scenario, scenario.MakeImage()); }
                catch (Exception) { continue; }

                var rnd = new Random(seed * 31 + 7);
                for (var k = 0; k < mutationsPerFile; k++)
                {
                    var (damaged, how) = Damage(data, rnd);
                    var cause = Decode(damaged);
                    if (cause != null)
                    {
                        log.Add($"{cause} [{how.Split(' ')[0]}]", scenario);
                        var dump = Environment.GetEnvironmentVariable("COREJ2K_FUZZ_DUMP");
                        if (!string.IsNullOrEmpty(dump))
                        {
                            Directory.CreateDirectory(dump);
                            File.WriteAllBytes(Path.Combine(dump, $"seed{seed}_m{k}_{cause.Split(':')[0].Replace(' ', '_')}.bin"), damaged);
                        }
                    }
                }
            }

            var path = Environment.GetEnvironmentVariable("COREJ2K_SWEEP_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, log.Report(int.MaxValue));
            Assert.True(log.Count == 0, log.Report());
        }

        private static (byte[] Data, string How) Damage(byte[] data, Random rnd)
        {
            var copy = (byte[])data.Clone();
            switch (rnd.Next(5))
            {
                case 0: // cut short
                    var length = rnd.Next(1, Math.Max(2, copy.Length));
                    return (copy.AsSpan(0, length).ToArray(), $"truncated at {length}");
                case 1: // one byte changed
                {
                    var at = rnd.Next(copy.Length);
                    copy[at] = (byte)rnd.Next(256);
                    return (copy, $"byte {at}");
                }
                case 2: // a few bytes changed in the headers
                {
                    var span = Math.Min(copy.Length, 200);
                    for (var i = 0; i < 3; i++) copy[rnd.Next(span)] = (byte)rnd.Next(256);
                    return (copy, "header bytes");
                }
                case 3: // a stretch overwritten
                {
                    var at = rnd.Next(copy.Length);
                    var n = Math.Min(copy.Length - at, rnd.Next(1, 40));
                    var fill = (byte)(rnd.Next(2) == 0 ? 0x00 : 0xFF);
                    for (var i = 0; i < n; i++) copy[at + i] = fill;
                    return (copy, $"filled {n} at {at}");
                }
                default: // a bit flipped
                {
                    var at = rnd.Next(copy.Length);
                    copy[at] ^= (byte)(1 << rnd.Next(8));
                    return (copy, $"bit {at}");
                }
            }
        }

        /// <summary>Decodes; returns null if the outcome is acceptable, otherwise what was wrong.</summary>
        private static string? Decode(byte[] data)
        {
            using var cts = new CancellationTokenSource(Deadline);
            Exception? failure = null;
            var done = new ManualResetEventSlim();

            // a thread of its own: a decode queued behind a busy thread pool would look like a hang
            var thread = new Thread(() =>
            {
                try
                {
                    // a damaged header can claim any size; refuse the absurd ones the way a caller of untrusted data would
                    var pl = J2kImage.GetDefaultDecoderParameterList();
                    pl["max_pixels"] = "4000000";
                    using var stream = new MemoryStream(data);
                    using var image = J2kImage.FromStream(stream, pl, cts.Token);
                    for (var c = 0; c < image.NumberOfComponents && c < 8; c++) image.GetComponent(c);
                }
                catch (Exception e)
                {
                    failure = e;
                }
                finally
                {
                    done.Set();
                }
            }) { IsBackground = true };
            thread.Start();
            if (!done.Wait(Deadline + TimeSpan.FromSeconds(10)))
                return "decode hangs";
            if (failure == null || failure is OperationCanceledException) return failure == null ? null : "decode cancelled by the deadline";
            return IsDecoderBug(failure) ? "decoder bug: " + ScenarioRunner.Normalise(ScenarioRunner.ShortMessage(failure)) + " at " + FirstFrame(failure) : null;
        }

        private static string FirstFrame(Exception e)
        {
            var inner = e;
            while (inner.InnerException != null) inner = inner.InnerException;
            var frame = (inner.StackTrace ?? string.Empty).Split('\n').FirstOrDefault(l => l.Contains("CoreJ2K")) ?? string.Empty;
            frame = frame.Trim();
            var cut = frame.IndexOf(" in ", StringComparison.Ordinal);
            return cut > 0 ? frame.Substring(0, cut) : frame;
        }
    }
}
