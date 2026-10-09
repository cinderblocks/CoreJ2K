// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Runtime.CompilerServices;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The codec reports through a process-wide logger, and the tests provoke most of what it has to say: tiles larger than
    /// the image, damaged files, malformed boxes. Printed, those lines bury the test results, so the test assemblies log
    /// nothing unless COREJ2K_TEST_LOG is set.
    /// </summary>
    internal static class QuietLogging
    {
        [ModuleInitializer]
        internal static void Install()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COREJ2K_TEST_LOG")))
                FacilityManager.DefaultMsgLogger = new SilentLogger();
        }

        private sealed class SilentLogger : IMsgLogger
        {
            public void printmsg(int sev, string msg) { }
            public void println(string str, int flind, int ind) { }
            public void flush() { }
        }
    }
}
