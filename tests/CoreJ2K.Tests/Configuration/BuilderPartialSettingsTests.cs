// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>
    /// The complete builder passes on a quantization or wavelet setting only when the caller chose it, so changing one setting
    /// (say the number of levels) leaves the others as the encoder configuration has them.
    /// </summary>
    public class BuilderPartialSettingsTests
    {
        private static InterleavedImageSource Image(out int[][] samples)
        {
            const int w = 96, h = 80;
            var rnd = new Random(2);
            samples = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                samples[c] = new int[w * h];
                for (var i = 0; i < w * h; i++)
                    samples[c][i] = Math.Clamp(128 + (int)(50 * Math.Sin(i % w / 7.0 + c)) + rnd.Next(-15, 16), 0, 255);
            }
            var copy = samples;
            return new InterleavedImageSource(w, h, 3, 8, new bool[3], copy.Select(c => c.Select(v => v - 128).ToArray()).ToArray());
        }

        [Fact]
        public void LossyBuilder_WithOnlyTheLevelsChanged_StillEncodes()
        {
            var builder = new CompleteEncoderConfigurationBuilder().WithQuality(0.5).WithWavelet(w => w.WithDecompositionLevels(3));
            var pl = builder.Build().ToParameterList();

            Assert.Equal("w9x7", pl["Ffilters"]);
            Assert.Equal("3", pl["Wlev"]);
            Assert.NotEmpty(J2kImage.ToBytes(Image(out _), null, pl)!);
        }

        [Fact]
        public void LosslessConfiguration_WithTheLevelsOrGuardBitsChanged_StaysLossless()
        {
            var builder = new CompleteEncoderConfigurationBuilder()
                .WithEncoder(e => e.WithLossless())
                .WithWavelet(w => w.WithDecompositionLevels(3))
                .WithQuantization(q => q.WithGuardBits(3));
            var pl = builder.Build().ToParameterList();
            pl["threads"] = "1";

            var source = Image(out var samples);
            var decoded = J2kImage.FromBytes(J2kImage.ToBytes(source, null, pl)!);
            for (var c = 0; c < 3; c++)
                Assert.Equal(samples[c], decoded.GetComponent(c));
            Assert.Equal("reversible", pl["Qtype"]);
            Assert.Equal("w5x3", pl["Ffilters"]);
            Assert.Equal("3", pl["Qguard_bits"]);
        }

        [Fact]
        public void ChangingTheStepSize_AfterAPreset_KeepsThePresetsGuardBits()
        {
            var pl = new CompleteEncoderConfigurationBuilder().ForPortrait(6000)
                .WithQuantization(q => q.WithBaseStepSize(0.02f)).Build().ToParameterList();

            Assert.Equal("2", pl["Qguard_bits"]);
            Assert.Equal("0.02", pl["Qstep"]);
            Assert.Equal("expounded", pl["Qtype"]);
        }

        [Fact]
        public void ChoosingEverything_StillWinsOverTheEncoderConfiguration()
        {
            var pl = new CompleteEncoderConfigurationBuilder()
                .WithEncoder(e => e.WithBitrate(1f))
                .WithWavelet(w => w.UseReversible().WithDecompositionLevels(2))
                .WithQuantization(q => q.UseReversible().WithGuardBits(4))
                .Build().ToParameterList();

            Assert.Equal("w5x3", pl["Ffilters"]);
            Assert.Equal("2", pl["Wlev"]);
            Assert.Equal("reversible", pl["Qtype"]);
            Assert.Equal("4", pl["Qguard_bits"]);
        }

        [Fact]
        public void Presets_StillSetWhatTheyDid()
        {
            var lossless = new CompleteEncoderConfigurationBuilder().ForLossless().Build().ToParameterList();
            Assert.Equal("reversible", lossless["Qtype"]);
            Assert.Equal("w5x3", lossless["Ffilters"]);

            var balanced = new CompleteEncoderConfigurationBuilder().ForBalanced().Build().ToParameterList();
            Assert.Equal("expounded", balanced["Qtype"]);
            Assert.Equal("w9x7", balanced["Ffilters"]);
        }
    }
}
