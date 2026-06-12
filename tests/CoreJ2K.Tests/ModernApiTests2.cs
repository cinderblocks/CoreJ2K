// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using CoreJ2K;
using CoreJ2K.Util;
using CoreJ2K.Configuration;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for the second batch of modern API additions:
    /// WithFileFormat shortcut, Encode(object), async encode/decode,
    /// J2KDecoderConfiguration.Decode* instance methods, and ToBytes non-nullable.
    /// </summary>
    public class ModernApiTests2
    {
        private static (InterleavedImageSource src, int[][] orig) MakeImage(int w, int h)
        {
            var data = new int[1][];
            var orig = new int[1][];
            data[0] = new int[w * h];
            orig[0] = new int[w * h];
            for (var i = 0; i < w * h; i++)
            {
                orig[0][i] = (i * 13) & 0xFF;
                data[0][i] = orig[0][i] - 128;
            }
            return (new InterleavedImageSource(w, h, 1, 8, new[] { false }, data), orig);
        }

        // ------------------------------------------------------------------
        // WithFileFormat shortcut
        // ------------------------------------------------------------------

        [Fact]
        public void WithFileFormat_ProducesJp2Output()
        {
            var (src, orig) = MakeImage(32, 32);

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithFileFormat()            // ← new shortcut
                .Encode(src);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);

            // Should decode back to same pixels
            var decoded = J2kImage.FromBytes(bytes);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void WithFileFormat_False_ProducesBareCodestream()
        {
            var (src, _) = MakeImage(16, 16);

            var bytes = new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithFileFormat(false)
                .Encode(src);

            // Bare codestream starts with SOC marker 0xFF4F
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0x4F, bytes[1]);
        }

        // ------------------------------------------------------------------
        // J2KDecoderConfiguration instance Decode methods
        // ------------------------------------------------------------------

        [Fact]
        public void DecoderConfig_Decode_ByteArray_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl);

            var config = new J2KDecoderConfiguration().WithHighestResolution();
            var result = config.Decode(bytes);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void DecoderConfig_Decode_ReadOnlyMemory_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl);

            ReadOnlyMemory<byte> mem = bytes;
            var config = new J2KDecoderConfiguration();
            var result = config.Decode(mem);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void DecoderConfig_Decode_Stream_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl);

            var config = new J2KDecoderConfiguration();
            using var stream = new MemoryStream(bytes);
            var result = config.Decode(stream);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        // ------------------------------------------------------------------
        // ToBytes non-nullable (throws instead of returning null)
        // ------------------------------------------------------------------

        [Fact]
        public void ToBytes_NullImgsrc_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                J2kImage.ToBytes((j2k.image.BlkImgDataSrc)null!));
        }

        [Fact]
        public void ToBytes_ReturnsNonNullable_NoExclamationNeeded()
        {
            var (src, _) = MakeImage(16, 16);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["lossless"] = "on";

            // Verify return type is byte[] (not byte[]?) — compile-time guarantee,
            // runtime test just confirms encoding completes without throwing.
            byte[] data = J2kImage.ToBytes(src, null, pl);
            Assert.True(data.Length > 0);
        }

        // ------------------------------------------------------------------
        // Async encode/decode
        // ------------------------------------------------------------------

        [Fact]
        public async Task ToBytesAsync_ProducesDecodableOutput()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            var bytes = await J2kImage.ToBytesAsync(src, pl);

            Assert.True(bytes.Length > 0);
            var decoded = J2kImage.FromBytes(bytes);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public async Task DecodeBytesAsync_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl);

            var result = await J2kImage.DecodeBytesAsync(bytes);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public async Task DecodeStreamAsync_WithConfig_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl);

            var config = new J2KDecoderConfiguration().WithHighestResolution();
            using var stream = new MemoryStream(bytes);
            var result = await J2kImage.DecodeStreamAsync(stream, config);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public async Task WriteToAsync_ProducesDecodableOutput()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            using var output = new MemoryStream();
            await J2kImage.WriteToAsync(output, src, pl);

            output.Position = 0;
            var decoded = J2kImage.FromStream(output);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public async Task Builder_EncodeAsync_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);

            var bytes = await new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithFileFormat()
                .EncodeAsync(src);

            var decoded = J2kImage.FromBytes(bytes);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public async Task Builder_WriteToAsync_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);

            using var output = new MemoryStream();
            await new CompleteEncoderConfigurationBuilder()
                .ForLossless()
                .WithFileFormat()
                .WriteToAsync(src, output);

            output.Position = 0;
            var decoded = J2kImage.FromStream(output);
            var comp = decoded.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public async Task DecoderConfig_DecodeAsync_ByteArray_RoundTrips()
        {
            var (src, orig) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl);

            var config = new J2KDecoderConfiguration();
            var result = await config.DecodeAsync(bytes);

            var comp = result.Image.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }
    }
}
