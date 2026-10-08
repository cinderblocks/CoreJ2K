// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.roi;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// <see cref="CompleteEncoderConfigurationBuilder.ForPortrait"/> bundles the settings of a small portrait under a hard size
    /// limit: one tile, one layer, ICT with 9/7, five levels, 64×64 code-blocks, luma weight 1.25, optional Maxshift face region.
    /// </summary>
    public class PortraitPresetTests
    {
        private const int Width = 240;
        private const int Height = 320;
        private const int Limit = 6000;

        private static InterleavedImageSource MakeImage(int seed = 5)
        {
            var rnd = new Random(seed);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[Width * Height];
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < Width; x++)
                    {
                        var v = 128 + (int)(60 * Math.Sin(x / (5.0 + c)) * Math.Cos(y / 6.0)) + rnd.Next(-20, 21);
                        comps[c][y * Width + x] = Math.Clamp(v, 0, 255) - 128;
                    }
            }
            return new InterleavedImageSource(Width, Height, 3, 8, new bool[3], comps);
        }

        private static ROIMask Face() => ROIMask.FromEllipse(Width, Height, 120, 130, 60, 80);

        private static byte[] Encode(CompleteEncoderConfigurationBuilder builder)
        {
            var pl = builder.Build().ToParameterList();
            pl["threads"] = "1";
            return J2kImage.ToBytes(MakeImage(), null, pl)!;
        }

        /// <summary>The settings FaceOFFx's vendored encoder writes, as a raw parameter list.</summary>
        private static byte[] EncodeRaw(int limit, ROIMask? face)
        {
            var pl = new ParameterList(J2kImage.GetDefaultEncoderParameterList())
            {
                ["file_format"] = "on",
                ["lossless"] = "off",
                ["Wlev"] = "5",
                ["Cblksiz"] = "64 64",
                ["Aptype"] = "layer",
                ["max_bytes"] = limit.ToString(),
                ["Dweights"] = "1.25:c0",
                ["threads"] = "1",
            };
            if (face != null)
            {
                pl["Rstart_level"] = "4";
                pl["Ralign"] = "off";
                pl["Rroi"] = "M 0";
                pl.RoiMasks.Add(face);
            }
            return J2kImage.ToBytes(MakeImage(), null, pl)!;
        }

        private static int IndexOfMarker(byte[] data, byte second, int from = 0)
        {
            for (var i = from; i < data.Length - 1; i++)
                if (data[i] == 0xFF && data[i + 1] == second) return i;
            return -1;
        }

        private static int CodestreamStart(byte[] data)
        {
            for (var i = 0; i < data.Length - 3; i++)
                if (data[i] == 0xFF && data[i + 1] == 0x4F && data[i + 2] == 0xFF && data[i + 3] == 0x51) return i;
            throw new InvalidOperationException("No codestream.");
        }

        [Fact]
        public void Preset_MatchesTheRawRecipeByteForByte()
        {
            var preset = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit));
            Assert.Equal(EncodeRaw(Limit, null), preset);
        }

        [Fact]
        public void PresetWithFace_MatchesTheRawRecipeByteForByte()
        {
            var preset = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit, Face()));
            Assert.Equal(EncodeRaw(Limit, Face()), preset);
        }

        [Fact]
        public void Output_FitsTheLimit_AsOneTileAndOneLayer()
        {
            foreach (var limit in new[] { 3000, 4500, 7000 })
            {
                var data = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(limit, Face()));
                Assert.True(data.Length <= limit, $"{data.Length} bytes for a limit of {limit}.");
                Assert.True(data.Length >= limit * 0.9, $"Only {data.Length} of {limit} bytes used.");

                var start = CodestreamStart(data);

                // SIZ: marker, Lsiz, Rsiz, Xsiz, Ysiz, XOsiz, YOsiz, XTsiz, YTsiz
                var siz = IndexOfMarker(data, 0x51, start);
                Assert.Equal(Width, ReadInt(data, siz + 6));
                Assert.Equal(Height, ReadInt(data, siz + 10));
                Assert.Equal(Width, ReadInt(data, siz + 22));
                Assert.Equal(Height, ReadInt(data, siz + 26));

                // COD: marker, Lcod, Scod, progression, layers (2), mct, levels
                var cod = IndexOfMarker(data, 0x52, start);
                Assert.Equal(1, (data[cod + 6] << 8) | data[cod + 7]);
                Assert.Equal(1, data[cod + 8]);
                Assert.Equal(5, data[cod + 9]);
                Assert.Equal(4, data[cod + 10]); // 64 = 2^(4+2)
                Assert.Equal(4, data[cod + 11]);
            }
        }

        [Fact]
        public void Face_ProducesAnRgnMarker_AndNoFaceDoesNot()
        {
            var withFace = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit, Face()));
            var without = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit));
            Assert.True(IndexOfMarker(withFace, 0x5E) >= 0);
            Assert.True(IndexOfMarker(without, 0x5E) < 0);
        }

        [Fact]
        public void Output_Decodes()
        {
            var data = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit, Face()));
            using var image = J2kImage.FromBytes(data);
            Assert.Equal(Width, image.Width);
            Assert.Equal(Height, image.Height);
            Assert.Equal(3, image.NumberOfComponents);
        }

        [Fact]
        public void LumaWeight_ChangesTheOutput()
        {
            var weighted = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit));
            var builder = new CompleteEncoderConfigurationBuilder().ForPortrait(Limit);
            builder.WithDistortionWeights(new CoreJ2K.j2k.encoder.DistortionWeights());
            Assert.NotEqual(weighted, Encode(builder));
        }

        [Fact]
        public void WithMaxBytes_AfterThePreset_ChangesTheLimit()
        {
            var builder = new CompleteEncoderConfigurationBuilder().ForPortrait(Limit).WithMaxBytes(4000);
            var data = Encode(builder);
            Assert.True(data.Length <= 4000);
            Assert.True(data.Length > 3500);
        }

        [Fact]
        public void Preset_UndoesEarlierPresetsThatWouldOverrideIt()
        {
            var clean = Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit));

            foreach (var earlier in new Action<CompleteEncoderConfigurationBuilder>[]
            {
                b => b.ForLossless(),
                b => b.ForWeb(),
                b => b.ForThumbnail(),
                b => b.ForHighQuality(),
                b => b.ForArchival(),
                b => b.ForMedical(),
            })
            {
                var builder = new CompleteEncoderConfigurationBuilder();
                earlier(builder);
                builder.ForPortrait(Limit);
                Assert.Equal(clean, Encode(builder));
            }
        }

        [Fact]
        public void Preset_KeepsARegionSetEarlier_WhenNoFaceIsGiven()
        {
            var builder = new CompleteEncoderConfigurationBuilder()
                .WithROI(r => r.AddRectangle(-1, 60, 80, 120, 160).SetStartLevel(2))
                .ForPortrait(Limit);
            Assert.True(IndexOfMarker(Encode(builder), 0x5E) >= 0);
        }

        [Fact]
        public void PresetAccessor_IsTheSameAsTheBuilderMethod()
        {
            Assert.Equal(
                Encode(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit, Face())),
                Encode(CompleteConfigurationPresets.Portrait(Limit, Face())));
        }

        [Fact]
        public void Preset_RejectsANonPositiveLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CompleteEncoderConfigurationBuilder().ForPortrait(0));
        }

        [Fact]
        public void Preset_ConfigurationValidates()
        {
            Assert.Empty(new CompleteEncoderConfigurationBuilder().ForPortrait(Limit, Face()).Build().Validate());
        }

        private static int ReadInt(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
    }
}
