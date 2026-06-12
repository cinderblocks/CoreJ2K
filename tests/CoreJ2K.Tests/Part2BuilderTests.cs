// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.IO;
using Xunit;
using CoreJ2K;
using CoreJ2K.Util;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Verifies that Part 2 transforms (DCO, NLT, MCT) are accessible through
    /// CompleteEncoderConfigurationBuilder and produce correct output via Encode().
    /// </summary>
    public class Part2BuilderTests
    {
        private static (InterleavedImageSource src, int[][] orig) MakeImage(int w, int h, int comps = 1)
        {
            var data = new int[comps][];
            var orig = new int[comps][];
            for (var c = 0; c < comps; c++)
            {
                data[c] = new int[w * h];
                orig[c] = new int[w * h];
                for (var i = 0; i < w * h; i++)
                {
                    orig[c][i] = ((i * 7 + c * 31) & 0xFF);
                    data[c][i] = orig[c][i] - 128; // signed-centred
                }
            }
            var signed = new bool[comps];
            return (new InterleavedImageSource(w, h, comps, 8, signed, data), orig);
        }

        private static FileFormatReader ReadBoxes(byte[] data)
        {
            var reader = new FileFormatReader(new ISRandomAccessIO(new MemoryStream(data)));
            reader.readFileFormat();
            return reader;
        }

        // ------------------------------------------------------------------
        // DCO via builder
        // ------------------------------------------------------------------

        [Fact]
        public void WithDco_OffsetArray_RoundTripsPixelsExact()
        {
            var (src, orig) = MakeImage(32, 32);

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .WithDco(10)
                .Encode(src);

            var decoded = J2kImage.FromBytes(bytes);
            Assert.NotNull(decoded);
            var comp = decoded!.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void WithDco_WritesRreqWithDcoFeature()
        {
            var (src, _) = MakeImage(16, 16);

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .WithDco(5)
                .Encode(src);

            var reader = ReadBoxes(bytes);
            Assert.NotNull(reader.Metadata.ReaderRequirements);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_DCO,
                reader.Metadata.ReaderRequirements!.StandardFeatures);
        }

        [Fact]
        public void WithDco_PrebuiltSegment_RoundTrips()
        {
            var (src, orig) = MakeImage(24, 24);
            var seg = new DCOMarkerSegment { Offsets = new[] { 7 } };

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .WithDco(seg)
                .Encode(src);

            var decoded = J2kImage.FromBytes(bytes);
            Assert.NotNull(decoded);
            var comp = decoded!.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        // ------------------------------------------------------------------
        // NLT via builder
        // ------------------------------------------------------------------

        [Fact]
        public void AddNlt_GammaSegment_WritesRreqWithNltFeature()
        {
            var (src, _) = MakeImage(16, 16);

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .AddNlt(new NLTMarkerSegment { Type = NLTType.None })
                .Encode(src);

            var reader = ReadBoxes(bytes);
            Assert.NotNull(reader.Metadata.ReaderRequirements);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_NLT,
                reader.Metadata.ReaderRequirements!.StandardFeatures);
        }

        [Fact]
        public void AddNlt_ActionOverload_AddsSegment()
        {
            var (src, _) = MakeImage(16, 16);

            var builder = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .AddNlt(s => { s.Type = NLTType.None; s.BitDepth = 8; });

            Assert.NotNull(builder.Nlts);
            Assert.Single(builder.Nlts!);
        }

        // ------------------------------------------------------------------
        // MCT via builder
        // ------------------------------------------------------------------

        [Fact]
        public void AddMct_StoresSpec()
        {
            var spec = new MctEncodeSpec
            {
                TransformType = MctTransformType.Matrix,
                Components = new[] { 0, 1, 2 },
                ForwardMatrix = new double[,]
                {
                    { 1, 0, 0 },
                    { 0, 1, 0 },
                    { 0, 0, 1 }
                },
                Irreversible = false
            };

            var builder = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .AddMct(spec);

            Assert.NotNull(builder.Mcts);
            Assert.Single(builder.Mcts!);
            Assert.Equal(MctTransformType.Matrix, builder.Mcts![0].TransformType);
        }

        // ------------------------------------------------------------------
        // Property accessors and Validate()
        // ------------------------------------------------------------------

        [Fact]
        public void Dco_Property_ReturnsSegment()
        {
            var builder = new CompleteEncoderConfigurationBuilder()
                .WithDco(1, 2, 3);

            Assert.NotNull(builder.Dco);
            Assert.Equal(new[] { 1, 2, 3 }, builder.Dco!.Offsets);
        }

        [Fact]
        public void Validate_EmptyDcoOffsets_ReturnsError()
        {
            var seg = new DCOMarkerSegment { Offsets = System.Array.Empty<int>() };
            var builder = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithDco(seg);

            var errors = builder.Validate();
            Assert.Contains(errors, e => e.Contains("DCO"));
        }

        [Fact]
        public void ToString_IncludesPart2Info()
        {
            var builder = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithDco(5)
                .AddNlt(new NLTMarkerSegment { Type = NLTType.None });

            var str = builder.ToString();
            Assert.Contains("DCO", str);
            Assert.Contains("NLT", str);
        }

        // ------------------------------------------------------------------
        // No Part 2 — builder still routes through full overload correctly
        // ------------------------------------------------------------------

        [Fact]
        public void Encode_NoPart2_ProducesValidJp2()
        {
            var (src, orig) = MakeImage(32, 32);

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .Encode(src);

            var decoded = J2kImage.FromBytes(bytes);
            Assert.NotNull(decoded);
            var comp = decoded!.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }
    }
}
