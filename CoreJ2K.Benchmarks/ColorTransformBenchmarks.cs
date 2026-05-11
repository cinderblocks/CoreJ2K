// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.
//
// Microbenchmarks for the SIMD-accelerated color transform kernels.
//   dotnet run -c Release --project CoreJ2K.Benchmarks
//
// The "Scalar*" variants reproduce the legacy per-pixel loops verbatim, so
// numbers measure the *delta* attributable to the SIMD rewrite.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CoreJ2K.j2k.image.Simd;

namespace CoreJ2K.Benchmarks
{
    [MemoryDiagnoser]
    public class ColorTransformBenchmarks
    {
        // Sized to be representative of a JP2 tile row buffer.
        // 1024 = typical tile width; 4096 stresses cache locality.
        [Params(64, 256, 1024, 4096)]
        public int N;

        private int[] _y = Array.Empty<int>();
        private int[] _cb = Array.Empty<int>();
        private int[] _cr = Array.Empty<int>();
        private int[] _r = Array.Empty<int>();
        private int[] _g = Array.Empty<int>();
        private int[] _b = Array.Empty<int>();

        private float[] _yf = Array.Empty<float>();
        private float[] _cbf = Array.Empty<float>();
        private float[] _crf = Array.Empty<float>();

        [GlobalSetup]
        public void Setup()
        {
            var rng = new Random(1234);
            _y = new int[N]; _cb = new int[N]; _cr = new int[N];
            _r = new int[N]; _g = new int[N]; _b = new int[N];
            _yf = new float[N]; _cbf = new float[N]; _crf = new float[N];
            for (int i = 0; i < N; i++)
            {
                _y[i]  = rng.Next(-2048, 2048);
                _cb[i] = rng.Next(-2048, 2048);
                _cr[i] = rng.Next(-2048, 2048);
                _yf[i]  = (float)(rng.NextDouble() * 4096.0 - 2048.0);
                _cbf[i] = (float)(rng.NextDouble() * 4096.0 - 2048.0);
                _crf[i] = (float)(rng.NextDouble() * 4096.0 - 2048.0);
            }
        }

        // ---------- Inverse RCT ----------

        [Benchmark(Baseline = true, Description = "InvRCT scalar")]
        public void InvRct_Scalar()
        {
            int n = _y.Length;
            for (int i = 0; i < n; i++)
            {
                int gv = _y[i] - ((_cb[i] + _cr[i]) >> 2);
                _g[i] = gv;
                _r[i] = _cr[i] + gv;
                _b[i] = _cb[i] + gv;
            }
        }

        [Benchmark(Description = "InvRCT SIMD")]
        public void InvRct_Simd()
            => SimdColorTransform.InvRctRow(_y, _cb, _cr, _r, _g, _b);

        // ---------- Inverse ICT ----------

        [Benchmark(Description = "InvICT scalar")]
        public void InvIct_Scalar()
        {
            int n = _yf.Length;
            for (int i = 0; i < n; i++)
            {
                _r[i] = (int)(_yf[i] + 1.402f * _crf[i] + 0.5f);
                _g[i] = (int)(_yf[i] - 0.34413f * _cbf[i] - 0.71414f * _crf[i] + 0.5f);
                _b[i] = (int)(_yf[i] + 1.772f * _cbf[i] + 0.5f);
            }
        }

        [Benchmark(Description = "InvICT SIMD")]
        public void InvIct_Simd()
            => SimdColorTransform.InvIctRow(_yf, _cbf, _crf, _r, _g, _b);

        // ---------- Forward RCT ----------

        [Benchmark(Description = "ForwRCT scalar")]
        public void ForwRct_Scalar()
        {
            int n = _r.Length;
            for (int i = 0; i < n; i++)
            {
                _y[i]  = (_r[i] + 2 * _g[i] + _b[i]) >> 2;
                _cb[i] = _b[i] - _g[i];
                _cr[i] = _r[i] - _g[i];
            }
        }

        [Benchmark(Description = "ForwRCT SIMD")]
        public void ForwRct_Simd()
            => SimdColorTransform.ForwRctRow(_r, _g, _b, _y, _cb, _cr);

        // ---------- Forward ICT ----------

        [Benchmark(Description = "ForwICT scalar")]
        public void ForwIct_Scalar()
        {
            int n = _r.Length;
            for (int i = 0; i < n; i++)
            {
                _yf[i]  =   0.299f   * _r[i] + 0.587f   * _g[i] + 0.114f   * _b[i];
                _cbf[i] = (-0.16875f) * _r[i] - 0.33126f * _g[i] + 0.5f     * _b[i];
                _crf[i] =   0.5f     * _r[i] - 0.41869f * _g[i] - 0.08131f * _b[i];
            }
        }

        [Benchmark(Description = "ForwICT SIMD")]
        public void ForwIct_Simd()
            => SimdColorTransform.ForwIctRow(_r, _g, _b, _yf, _cbf, _crf);
    }

    public static class Program
    {
        public static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(typeof(ColorTransformBenchmarks).Assembly).Run(args);
    }
}
