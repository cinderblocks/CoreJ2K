// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using Xunit;
using CoreJ2K;
using CoreJ2K.Util;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for the modern API additions: J2kDecodeResult, ReadOnlyMemory decode,
    /// and WriteTo stream encoding.
    /// </summary>
    public class ModernApiTests
    {
        private static (InterleavedImageSource src, int[][] orig) MakeImage(int w, int h)
        {
            var data = new int[1][];
            var orig = new int[1][];
            data[0] = new int[w * h];
            orig[0] = new int[w * h];
            for (var i = 0; i < w * h; i++)
            {
                orig[0][i] = (i * 7) & 0xFF;
                data[0][i] = orig[0][i] - 128;
            }
            return (new InterleavedImageSource(w, h, 1, 8, new[] { false }, data), orig);
        }

        private static byte[] EncodeJp2(InterleavedImageSource src)
        {
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            return J2kImage.ToBytes(src, null, pl)!;
        }

        // ------------------------------------------------------------------
        // J2kDecodeResult
        // ------------------------------------------------------------------

        [Fact]
        public void DecodeStream_ReturnsResult_WithImageAndMetadata()
        {
            var (src, orig) = MakeImage(32, 32);
            var bytes = EncodeJp2(src);

            using var stream = new MemoryStream(bytes);
            var result = J2kImage.DecodeStream(stream);

            Assert.NotNull(result);
            Assert.NotNull(result.Image);
            Assert.NotNull(result.Metadata);
        }

        [Fact]
        public void DecodeBytes_ByteArray_RoundTripsPixels()
        {
            var (src, orig) = MakeImage(32, 32);
            var bytes = EncodeJp2(src);

            var result = J2kImage.DecodeBytes(bytes);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void DecodeBytes_ByteArray_WithConfiguration_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var bytes = EncodeJp2(src);

            var config = new J2KDecoderConfiguration().WithHighestResolution();
            var result = J2kImage.DecodeBytes(bytes, config);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void DecodeStream_WithConfiguration_ReturnsMetadata()
        {
            var (src, _) = MakeImage(16, 16);
            var bytes = EncodeJp2(src);

            var config = new J2KDecoderConfiguration();
            using var stream = new MemoryStream(bytes);
            var result = J2kImage.DecodeStream(stream, config);

            Assert.NotNull(result.Metadata);
        }

        [Fact]
        public void DecodeResult_Deconstruct_Works()
        {
            var (src, orig) = MakeImage(16, 16);
            var bytes = EncodeJp2(src);

            var (image, metadata) = J2kImage.DecodeBytes(bytes);

            Assert.NotNull(image);
            Assert.NotNull(metadata);
            Assert.Equal(orig[0].Length, image.GetComponent(0).Length);
        }

        [Fact]
        public void DecodeResult_WithDco_IncludesMetadata()
        {
            var (src, orig) = MakeImage(24, 24);
            var dco = new DCOMarkerSegment { Offsets = new[] { 8 } };
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl, null, null, dco)!;

            var result = J2kImage.DecodeBytes(bytes);

            Assert.NotNull(result.Metadata.ReaderRequirements);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_DCO,
                result.Metadata.ReaderRequirements!.StandardFeatures);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        // ------------------------------------------------------------------
        // ReadOnlyMemory<byte> decode
        // ------------------------------------------------------------------

        [Fact]
        public void FromBytes_ReadOnlyMemory_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var bytes = EncodeJp2(src);

            ReadOnlyMemory<byte> mem = bytes;
            var decoded = J2kImage.FromBytes(mem);

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void FromBytes_ReadOnlyMemory_WithConfiguration_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var bytes = EncodeJp2(src);

            ReadOnlyMemory<byte> mem = bytes;
            var config = new J2KDecoderConfiguration().WithHighestResolution();
            var decoded = J2kImage.FromBytes(mem, config);

            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void DecodeBytes_ReadOnlyMemory_ReturnsResult()
        {
            var (src, orig) = MakeImage(16, 16);
            var bytes = EncodeJp2(src);

            ReadOnlyMemory<byte> mem = bytes;
            var result = J2kImage.DecodeBytes(mem);

            Assert.NotNull(result.Image);
            Assert.NotNull(result.Metadata);
            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void DecodeBytes_ReadOnlyMemory_SlicedSegment_Works()
        {
            var (src, orig) = MakeImage(16, 16);
            var bytes = EncodeJp2(src);

            // Embed in a larger buffer with an offset — tests TryGetArray path
            var padded = new byte[10 + bytes.Length + 5];
            Array.Copy(bytes, 0, padded, 10, bytes.Length);
            ReadOnlyMemory<byte> mem = new ReadOnlyMemory<byte>(padded, 10, bytes.Length);

            var result = J2kImage.DecodeBytes(mem);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        // ------------------------------------------------------------------
        // WriteTo(Stream)
        // ------------------------------------------------------------------

        [Fact]
        public void WriteTo_Stream_ProducesDecodableOutput()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            using var output = new MemoryStream();
            J2kImage.WriteTo(output, src, null, pl);

            output.Position = 0;
            var decoded = J2kImage.FromStream(output);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void WriteTo_Stream_WithConfiguration_ProducesDecodableOutput()
        {
            var (src, orig) = MakeImage(32, 32);
            var config = new J2KEncoderConfiguration()
                .WithLossless()
                .WithFileFormat(true);

            using var output = new MemoryStream();
            J2kImage.WriteTo(output, src, config);

            output.Position = 0;
            var decoded = J2kImage.FromStream(output);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void Builder_WriteTo_Stream_ProducesDecodableOutput()
        {
            var (src, orig) = MakeImage(32, 32);

            using var output = new MemoryStream();
            new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .WriteTo(src, output);

            output.Position = 0;
            var decoded = J2kImage.FromStream(output);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void Builder_WriteTo_Stream_WithDco_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);

            using var output = new MemoryStream();
            new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithEncoder(e => e.WithFileFormat(true))
                .WithDco(5)
                .WriteTo(src, output);

            output.Position = 0;
            var decoded = J2kImage.FromStream(output);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }
    }
}
