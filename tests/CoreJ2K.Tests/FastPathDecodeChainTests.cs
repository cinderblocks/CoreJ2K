// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// <c>DecodeToImage</c> and <c>FromStream</c> run the same decoding chain and differ only in the buffer they fill, so they must
    /// agree on every sample, whatever the tiling, the image origin and the bit depth.
    /// </summary>
    public class FastPathDecodeChainTests
    {
        private sealed class Pixels
        {
            public int Width, Height, Components;
            public byte[] Bytes = Array.Empty<byte>();
        }

        private sealed class PixelsImage : IImage
        {
            public Pixels Value = new Pixels();
            public T As<T>() => (T)(object)Value;
        }

        private sealed class PixelsCreator : ImageCreator<Pixels>
        {
            public override IImage Create(int width, int height, int numComponents, byte[] bytes) =>
                new PixelsImage { Value = new Pixels { Width = width, Height = height, Components = numComponents, Bytes = bytes } };

            public override BlkImgDataSrc ToPortableImageSource(object imageObject) => throw new NotSupportedException();
        }

        /// <summary>A stream that can only be read forwards, like a network stream.</summary>
        private sealed class ForwardOnlyStream : Stream
        {
            private readonly Stream _inner;
            public ForwardOnlyStream(Stream inner) => _inner = inner;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static byte[] Encode(int width, int height, int components, int bits, Action<ParameterList> options)
        {
            var rnd = new Random(width * 31 + bits);
            var half = 1 << (bits - 1);
            var samples = Enumerable.Range(0, components)
                .Select(c => Enumerable.Range(0, width * height)
                    .Select(i => (((i % width) * (c + 2) + (i / width) * 3) << Math.Max(0, bits - 8)) + rnd.Next(0, 5) - half)
                    .Select(v => Math.Clamp(v, -half, half - 1)).ToArray())
                .ToArray();
            var pl = new ParameterList();
            pl["lossless"] = "on";
            options(pl);
            return J2kImage.ToBytes(new InterleavedImageSource(width, height, components, bits, new bool[components], samples), pl);
        }

        [Theory]
        [InlineData("off")]
        [InlineData("on")]
        public void EightBit_TiledWithAnOrigin_MatchesFromBytes(string fileFormat)
        {
            ImageFactory.Register(new PixelsCreator());
            var data = Encode(75, 53, 3, 8, pl =>
            {
                pl["tiles"] = "32 24";
                pl["ref"] = "5 3";
                pl["file_format"] = fileFormat;
            });

            using var expected = J2kImage.FromBytes(data);
            var actual = J2kImage.DecodeToImage<Pixels>(data);

            Assert.Equal((75, 53, 3), (actual.Width, actual.Height, actual.Components));
            var interleaved = new byte[75 * 53 * 3];
            for (var c = 0; c < 3; c++)
            {
                var comp = expected.GetComponent(c);
                for (var i = 0; i < comp.Length; i++) interleaved[i * 3 + c] = (byte)comp[i];
            }
            Assert.Equal(interleaved, actual.Bytes);
        }

        [Fact]
        public void FromStream_ReadsAForwardOnlyStream_LongerThanOneReadAhead()
        {
            // noise does not compress, so the file is longer than the 256 kB the reader takes from such a stream at a time
            var rnd = new Random(3);
            var samples = Enumerable.Range(0, 3).Select(_ => Enumerable.Range(0, 400 * 300).Select(__ => rnd.Next(-128, 128)).ToArray()).ToArray();
            var pl = new ParameterList();
            pl["lossless"] = "on";
            var data = J2kImage.ToBytes(new InterleavedImageSource(400, 300, 3, 8, new bool[3], samples), pl);
            Assert.True(data.Length > 300_000);

            using var stream = new ForwardOnlyStream(new MemoryStream(data));
            using var decoded = J2kImage.FromStream(stream);

            for (var c = 0; c < 3; c++)
                Assert.Equal(samples[c].Select(v => v + 128), decoded.GetComponent(c));
        }

        [Fact]
        public void TwelveBit_FromAForwardOnlyStream_MatchesFromBytes()
        {
            ImageFactory.Register(new PixelsCreator());
            var data = Encode(40, 33, 1, 12, pl => pl["tiles"] = "16 16");

            using var decoded = J2kImage.FromBytes(data);
            var expected = decoded.As<Pixels>();
            using var stream = new ForwardOnlyStream(new MemoryStream(data));
            var actual = J2kImage.DecodeToImage<Pixels>(stream);

            Assert.Equal((40, 33, 1), (actual.Width, actual.Height, actual.Components));
            Assert.Equal(expected.Bytes, actual.Bytes);
            Assert.Contains(actual.Bytes, b => b != actual.Bytes[0]);
        }
    }
}
