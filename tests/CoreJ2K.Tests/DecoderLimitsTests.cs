// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Threading.Tasks;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// A few-KB file can claim an enormous image. These tests patch the SIZ marker segment of a small valid codestream
    /// to do exactly that and check the decoder refuses it up front, with a typed exception and without allocating.
    /// </summary>
    public class DecoderLimitsTests
    {
        // Private marker type + creator so the shared ImageFactory registry is not affected for other fixtures.
        private sealed class FastPathMarker { }

        private sealed class FastPathImage : IImage
        {
            public int Length { get; init; }
            public T As<T>() => (T)(object)new FastPathMarker();
        }

        private sealed class FastPathCreator : ImageCreator<FastPathMarker>
        {
            public override IImage Create(int width, int height, int numComponents, byte[] bytes) => new FastPathImage { Length = bytes.Length };

            public override BlkImgDataSrc ToPortableImageSource(object imageObject) => throw new NotSupportedException();
        }

        // Offsets into a raw codestream: SOC(2) SIZ marker(2) Lsiz(2) Rsiz(2) then Xsiz, Ysiz, XOsiz, YOsiz, XTsiz, YTsiz.
        private const int XsizOffset = 8, YsizOffset = 12, XTsizOffset = 24, YTsizOffset = 28;

        private static byte[] SmallStream(int size = 256)
        {
            var samples = new int[size * size];
            for (var i = 0; i < samples.Length; i++) samples[i] = ((i % size) * 3 + (i / size) * 5) % 200 - 100;
            var src = new InterleavedImageSource(size, size, 1, 8, new[] { false }, new[] { samples });
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "off";
            pl["lossless"] = "on";
            return J2kImage.ToBytes(src, null, pl)!;
        }

        private static void Put32(byte[] b, int offset, long value)
        {
            for (var i = 0; i < 4; i++) b[offset + i] = (byte)(value >> (24 - 8 * i));
        }

        /// <summary>A copy of <paramref name="stream"/> that claims a different image and tile size.</summary>
        private static byte[] Claiming(byte[] stream, long width, long height, long tileWidth, long tileHeight)
        {
            var b = (byte[])stream.Clone();
            Put32(b, XsizOffset, width);
            Put32(b, YsizOffset, height);
            Put32(b, XTsizOffset, tileWidth);
            Put32(b, YTsizOffset, tileHeight);
            return b;
        }

        [Fact]
        public void HugeImageClaim_IsRejectedBeforeAllocating()
        {
            var bomb = Claiming(SmallStream(), 20000, 20000, 20000, 20000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var ex = Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(bomb));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsAssignableFrom<InvalidOperationException>(ex);
            Assert.Equal(nameof(DecoderLimits.MaxMemoryBytes), ex.Limit);
            Assert.True(ex.Requested > ex.Allowed);
            Assert.True(allocated < 64L * 1024 * 1024, $"decoder allocated {allocated} bytes before rejecting the image");
        }

        [Fact]
        public void HugeImageClaim_IsRejectedFromEveryEntryPoint()
        {
            var bomb = Claiming(SmallStream(), 20000, 20000, 20000, 20000);

            Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(bomb));
            Assert.Throws<DecoderLimitException>(() => J2kImage.DecodeBytes(bomb));
            Assert.Throws<DecoderLimitException>(() => J2kImage.DecodeBytes(bomb, new J2KDecoderConfiguration()));
            Assert.Throws<DecoderLimitException>(() => J2kImage.FromStream(new System.IO.MemoryStream(bomb), out _));
        }

        [Fact]
        public void HugeImageClaim_IsRejectedByTheFastPathToo()
        {
            ImageFactory.Register(new FastPathCreator());
            var stream = SmallStream();
            Assert.NotNull(J2kImage.DecodeToImage<FastPathMarker>(stream)); // sanity: the fast path works on a valid stream

            // Grayscale: 20000x20000 would estimate just under the 2 GiB default, so claim 30000x30000.
            var bomb = Claiming(stream, 30000, 30000, 30000, 30000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Throws<DecoderLimitException>(() => J2kImage.DecodeToImage<FastPathMarker>(bomb));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 64L * 1024 * 1024);
        }

        // A JP2 palette turns the one codestream component into several output components, so the output buffer is bigger than
        // the codestream alone suggests. The limit must be checked against the output: this claims 12000x12000 (144 Mpx), which
        // is under the 2 GiB default as one component (~1.2 GB) but over it as the three palette columns (~2.3 GB).
        [Fact]
        public void PaletteExpandedOutput_IsCountedAgainstTheLimits()
        {
            var metadata = new J2KMetadata();
            metadata.SetPalette(4, 3, new short[] { 7, 7, 7 },
                new[] { new[] { 255, 0, 0 }, new[] { 0, 255, 0 }, new[] { 0, 0, 255 }, new[] { 255, 255, 255 } });
            metadata.AddComponentMapping(0, 1, 0);
            metadata.AddComponentMapping(0, 1, 1);
            metadata.AddComponentMapping(0, 1, 2);

            var samples = new int[64 * 64];
            for (var i = 0; i < samples.Length; i++) samples[i] = (i % 4) * 40 - 60;
            var source = new InterleavedImageSource(64, 64, 1, 8, new[] { false }, new[] { samples });
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var jp2 = J2kImage.ToBytes(source, metadata, pl)!;

            // Sanity: the unpatched file really decodes to three output components.
            using (var small = J2kImage.FromBytes(jp2))
                Assert.Equal(3, small.NumberOfComponents);

            // Patch the SIZ marker segment inside the JP2 (it follows the SOC marker 0xFF4F) to claim a huge image.
            var soc = FindCodestreamStart(jp2);
            var bomb = (byte[])jp2.Clone();
            Put32(bomb, soc + XsizOffset, 12000);
            Put32(bomb, soc + YsizOffset, 12000);
            Put32(bomb, soc + XTsizOffset, 12000);
            Put32(bomb, soc + YTsizOffset, 12000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var ex = Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(bomb));
            Assert.Equal(nameof(DecoderLimits.MaxMemoryBytes), ex.Limit);
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 64L * 1024 * 1024);
        }

        private static int FindCodestreamStart(byte[] jp2)
        {
            for (var i = 0; i < jp2.Length - 3; i++)
                if (jp2[i] == 0xFF && jp2[i + 1] == 0x4F && jp2[i + 2] == 0xFF && jp2[i + 3] == 0x51) return i;
            throw new InvalidOperationException("No codestream found in the JP2 file.");
        }

        [Fact]
        public async Task HugeImageClaim_ReachesAsyncCallersUnwrapped()
        {
            var bomb = Claiming(SmallStream(), 20000, 20000, 20000, 20000);
            await Assert.ThrowsAsync<DecoderLimitException>(() => J2kImage.DecodeBytesAsync(bomb));
        }

        [Fact]
        public void MaxPixels_RejectsFullResolutionButNotAReducedPreview()
        {
            var stream = SmallStream(256);

            // 256x256 = 65536 px. A 100 Kpx cap allows it; a 10 Kpx cap does not.
            Assert.NotNull(J2kImage.FromBytes(stream, new J2KDecoderConfiguration().WithLimits(DecoderLimits.Default.WithMaxPixels(100_000))));

            var tight = new J2KDecoderConfiguration().WithLimits(DecoderLimits.Default.WithMaxPixels(10_000));
            var ex = Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(stream, tight));
            Assert.Equal(nameof(DecoderLimits.MaxPixels), ex.Limit);

            // The same cap passes once the decode is reduced to 64x64 (resolution level 3 of 5): previews of huge
            // images must keep working under a limit that rejects the full-size decode.
            var preview = new J2KDecoderConfiguration { ResolutionLevel = 3 }.WithLimits(DecoderLimits.Default.WithMaxPixels(10_000));
            using var image = J2kImage.FromBytes(stream, preview);
            Assert.True(image.Width * image.Height <= 10_000, $"preview was {image.Width}x{image.Height}");
            Assert.True(image.Width < 256);
        }

        [Fact]
        public void TooManyTiles_IsRejectedEvenWhenLimitsAreDisabled()
        {
            // 4096x4096 in 1x1 tiles is 16 Mi tiles; ISO/IEC 15444-1 addresses at most 65535.
            var bomb = Claiming(SmallStream(), 4096, 4096, 1, 1);

            var ex = Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(bomb, new J2KDecoderConfiguration().WithLimits(DecoderLimits.None)));
            Assert.Equal("SpecMaxTiles", ex.Limit);
        }

        [Fact]
        public void TileCountThatOverflowsInt_IsRejectedNotMisreported()
        {
            // 65535x65535 in 1x1 tiles: the tile count overflows an int, which used to surface as "negative tiles".
            var bomb = Claiming(SmallStream(), 65535, 65535, 1, 1);

            var ex = Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(bomb));
            Assert.Equal("SpecMaxTiles", ex.Limit);
            Assert.Equal(65535L * 65535L, ex.Requested);
        }

        [Fact]
        public void NoneDisablesTheConfigurableLimits()
        {
            var stream = SmallStream(256);
            var config = new J2KDecoderConfiguration().WithLimits(DecoderLimits.None);

            using var image = J2kImage.FromBytes(stream, config);
            Assert.Equal(256, image.Width);
        }

        [Fact]
        public void LimitsCanBeSetThroughTheParameterList()
        {
            var pl = new ParameterList(J2kImage.GetDefaultDecoderParameterList());
            pl["max_pixels"] = "1000";

            var ex = Assert.Throws<DecoderLimitException>(() => J2kImage.FromBytes(SmallStream(256), pl));
            Assert.Equal(nameof(DecoderLimits.MaxPixels), ex.Limit);
            Assert.Equal(1000, ex.Allowed);
        }

        [Fact]
        public void InvalidParameterValue_IsAnArgumentError()
        {
            var pl = new ParameterList(J2kImage.GetDefaultDecoderParameterList());
            pl["max_pixels"] = "lots";
            Assert.Throws<ArgumentException>(() => J2kImage.FromBytes(SmallStream(64), pl));
        }

        [Fact]
        public void Presets_AreOrderedAndImmutable()
        {
            Assert.True(DecoderLimits.Strict.MaxPixels < DecoderLimits.Default.MaxPixels);
            Assert.True(DecoderLimits.Strict.MaxMemoryBytes < DecoderLimits.Default.MaxMemoryBytes);
            Assert.Equal(long.MaxValue, DecoderLimits.None.MaxPixels);

            var derived = DecoderLimits.Strict.WithMaxPixels(5);
            Assert.Equal(5, derived.MaxPixels);
            Assert.NotEqual(5, DecoderLimits.Strict.MaxPixels);
            Assert.Throws<ArgumentOutOfRangeException>(() => DecoderLimits.Strict.WithMaxPixels(0));
        }

        [Fact]
        public void DefaultLimits_AcceptTypicalLargeImages()
        {
            // 8192x8192 RGB at 4 bytes/sample plus a single tile's working buffer must fit the default 2 GiB estimate.
            var estimate = DecoderLimits.EstimateMemory(8192, 8192, 3, 8192, 8192, 3, 4);
            Assert.True(estimate <= DecoderLimits.Default.MaxMemoryBytes, $"estimate {estimate}");
        }
    }
}
