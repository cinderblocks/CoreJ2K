// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Runtime.CompilerServices;
using CoreJ2K.j2k.wavelet.analysis;
using CoreJ2K.j2k.wavelet.synthesis;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Parallel code-block and wavelet decoding is skipped for tile-components with only a few blocks or samples, which is almost every image the
    /// test suite uses. Setting <c>COREJ2K_TEST_FORCE_PARALLEL=1</c> makes the whole suite take the parallel path regardless
    /// of size, so it can be run both ways: <c>dotnet test</c> and <c>COREJ2K_TEST_FORCE_PARALLEL=1 dotnet test</c>.
    /// </summary>
    internal static class ParallelTestSetup
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            if (Environment.GetEnvironmentVariable("COREJ2K_TEST_FORCE_PARALLEL") == "1")
            {
                InvWTFull.MinParallelBlocks = 0;
                InvWTFull.MinParallelWaveletSamples = 0;
                ForwWTFull.MinParallelSamples = 0;
            }
        }
    }
}
