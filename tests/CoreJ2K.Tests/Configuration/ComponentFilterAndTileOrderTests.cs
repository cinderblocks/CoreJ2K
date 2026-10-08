// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// Wavelet filters per component and progression orders per tile reach the encoder, and the files they make decode.
    /// </summary>
    public class ComponentFilterAndTileOrderTests
    {
        private static int[][] Samples(int w, int h, int components)
        {
            var rnd = new Random(11);
            var samples = new int[components][];
            for (var c = 0; c < components; c++)
            {
                samples[c] = new int[w * h];
                for (var i = 0; i < w * h; i++)
                    samples[c][i] = Math.Clamp(128 + (int)(60 * Math.Sin(i % w / (6.0 + c))) + rnd.Next(-10, 11), 0, 255);
            }
            return samples;
        }

        private static InterleavedImageSource Source(int[][] samples, int w, int h) =>
            new InterleavedImageSource(w, h, samples.Length, 8, new bool[samples.Length],
                samples.Select(c => c.Select(v => v - 128).ToArray()).ToArray());

        private static byte[] Encode(CompleteEncoderConfigurationBuilder builder, int[][] samples, int w, int h)
        {
            var pl = builder.Build().ToParameterList();
            pl["threads"] = "1";
            return J2kImage.ToBytes(Source(samples, w, h), null, pl)!;
        }

        private static List<int> MarkerOffsets(byte[] data, byte second)
        {
            var found = new List<int>();
            for (var i = 0; i < data.Length - 1; i++)
                if (data[i] == 0xFF && data[i + 1] == second) found.Add(i);
            return found;
        }

        [Fact]
        public void ComponentFilter_NineSevenWithFiveThreeOnTheSecondComponent_WritesAndDecodes()
        {
            const int w = 64, h = 64;
            var samples = Samples(w, h, 2);
            var builder = new CompleteEncoderConfigurationBuilder()
                .WithWavelet(wv => wv.UseIrreversible_9_7().WithComponentFilter(1, WaveletFilter.Reversible53));
            var data = Encode(builder, samples, w, h);

            // component 1 has its own coding style (COC) and quantization (QCC)
            Assert.Contains(MarkerOffsets(data, 0x53), o => data[o + 4] == 1);
            Assert.Contains(MarkerOffsets(data, 0x5D), o => data[o + 4] == 1);

            var decoded = J2kImage.FromBytes(data);
            Assert.Equal(samples[1], decoded.GetComponent(1)); // 5/3 with reversible quantization is exact
            var worst = samples[0].Zip(decoded.GetComponent(0), (a, b) => Math.Abs(a - b)).Max();
            Assert.InRange(worst, 0, 6);
        }

        [Fact]
        public void ComponentFilter_FiveThreeDefaultWithNineSevenOnOneComponent_Decodes()
        {
            const int w = 64, h = 64;
            var samples = Samples(w, h, 2);
            var builder = new CompleteEncoderConfigurationBuilder()
                .WithEncoder(e => e.WithWavelet(wv => wv.UseReversible53()).WithQuantization(q => q.UseReversible()))
                .WithWavelet(wv => wv.UseReversible_5_3().WithComponentFilter(0, WaveletFilter.Irreversible97));
            var data = Encode(builder, samples, w, h);

            var decoded = J2kImage.FromBytes(data);
            Assert.Equal(samples[1], decoded.GetComponent(1));
            Assert.InRange(samples[0].Zip(decoded.GetComponent(0), (a, b) => Math.Abs(a - b)).Max(), 0, 6);
        }

        [Fact]
        public void ComponentFilter_ChangesTheOutput()
        {
            const int w = 64, h = 64;
            var samples = Samples(w, h, 2);
            var plain = Encode(new CompleteEncoderConfigurationBuilder().WithWavelet(wv => wv.UseIrreversible_9_7()), samples, w, h);
            var mixed = Encode(new CompleteEncoderConfigurationBuilder()
                .WithWavelet(wv => wv.UseIrreversible_9_7().WithComponentFilter(1, WaveletFilter.Reversible53)), samples, w, h);
            Assert.NotEqual(plain, mixed);
        }

        [Fact]
        public void ComponentFilter_ParameterValues()
        {
            var pl = new CompleteEncoderConfigurationBuilder()
                .WithWavelet(wv => wv.UseIrreversible_9_7().WithComponentFilter(2, WaveletFilter.Reversible53).WithComponentFilter(1, WaveletFilter.Reversible53))
                .Build().ToParameterList();
            Assert.Equal("w9x7 c1 w5x3 c2 w5x3", pl["Ffilters"]);
            Assert.Equal("expounded c1 reversible c2 reversible", pl["Qtype"]);

            var standalone = new ParameterList();
            new WaveletConfigurationBuilder().UseIrreversible_9_7().WithComponentFilter(1, WaveletFilter.Reversible53).ApplyTo(standalone);
            Assert.Equal("w9x7 c1 w5x3", standalone["Ffilters"]);
        }

        [Fact]
        public void ComponentFilter_Cleared_LeavesTheDefaults()
        {
            var pl = new CompleteEncoderConfigurationBuilder()
                .WithWavelet(wv => wv.UseIrreversible_9_7().WithComponentFilter(1, WaveletFilter.Reversible53).UseDefaultComponentFilters())
                .Build().ToParameterList();
            Assert.Equal("w9x7", pl["Ffilters"]);
            Assert.Equal("expounded", pl["Qtype"]);
        }

        [Fact]
        public void ComponentFilter_NineSevenWithLossless_IsReportedByValidate()
        {
            var config = new J2KEncoderConfiguration().WithLossless()
                .WithWavelet(wv => wv.WithComponentFilter(1, WaveletFilter.Irreversible97));
            Assert.Contains(config.Validate(), e => e.Contains("lossless"));
        }

        [Fact]
        public void TileOrder_OneTileWithItsOwnOrder_WritesItsOwnCod_AndDecodes()
        {
            const int w = 128, h = 64;
            var samples = Samples(w, h, 1);
            var builder = new CompleteEncoderConfigurationBuilder()
                .WithEncoder(e => e.WithLossless().WithTiles(t => t.SetSize(64, 64)))
                .WithProgression(p => p.UseLRCP().WithTileOrder(1, ProgressionOrder.RLCP));
            var data = Encode(builder, samples, w, h);

            var cods = MarkerOffsets(data, 0x52);
            Assert.Equal(2, cods.Count);                   // the main header's, and tile 1's
            Assert.Equal(0, data[cods[0] + 5]);            // LRCP
            Assert.Equal(1, data[cods[1] + 5]);            // RLCP
            Assert.True(cods[1] > MarkerOffsets(data, 0x90)[1]);

            Assert.Equal(samples[0], J2kImage.FromBytes(data).GetComponent(0));
        }

        [Fact]
        public void TileOrder_ParameterValues()
        {
            var pl = new CompleteEncoderConfigurationBuilder()
                .WithProgression(p => p.UseRPCL().WithTileOrder(3, ProgressionOrder.CPRL).WithTileOrder(1, ProgressionOrder.RLCP))
                .Build().ToParameterList();
            Assert.Equal("res-pos t1 res t3 comp-pos", pl["Aptype"]);

            var standalone = new ParameterList();
            new ProgressionConfigurationBuilder().UseLRCP().WithTileOrder(1, ProgressionOrder.PCRL).ApplyTo(standalone);
            Assert.Equal("layer t1 pos-comp", standalone["Aptype"]);
        }

        [Fact]
        public void TileOrder_Cleared_LeavesTheDefault()
        {
            var pl = new CompleteEncoderConfigurationBuilder()
                .WithProgression(p => p.UseRLCP().WithTileOrder(1, ProgressionOrder.LRCP).UseDefaultTileOrders())
                .Build().ToParameterList();
            Assert.Equal("res", pl["Aptype"]);
        }

        [Fact]
        public void TileOrder_NegativeTile_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new J2KEncoderConfiguration().WithProgression(p => p.WithTileOrder(-1, ProgressionOrder.LRCP)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new J2KEncoderConfiguration().WithWavelet(wv => wv.WithComponentFilter(-1, WaveletFilter.Reversible53)));
        }
    }
}
