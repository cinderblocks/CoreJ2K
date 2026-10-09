// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Decoding a JP2 file whose channel definition box names an opacity channel, or puts the channels in another order. The decoder
    /// looked the channels up by the colour they stand for, which an opacity channel (colour 0, "the whole image") has none of, so it
    /// read past the last channel: no file with an alpha channel could be decoded, not even one CoreJ2K had written.
    /// </summary>
    public class ChannelDefinitionDecodeTests
    {
        private const int Width = 40, Height = 30;

        private static int[][] Planes(int components, int seed = 2)
        {
            var rnd = new Random(seed);
            return Enumerable.Range(0, components).Select(_ => Enumerable.Range(0, Width * Height).Select(i => rnd.Next(256)).ToArray()).ToArray();
        }

        private static byte[] Encode(int[][] planes, ChannelDefinitionData definitions, bool lossless = true)
        {
            var source = new InterleavedImageSource(Width, Height, planes.Length, 8, new bool[planes.Length],
                planes.Select(c => c.Select(v => v - 128).ToArray()).ToArray());
            var configuration = new J2KEncoderConfiguration().WithFileFormat(true);
            if (lossless) configuration = configuration.WithLossless();
            var metadata = new J2KMetadata { ChannelDefinitions = definitions };
            return J2kImage.ToBytes(source, metadata, configuration.ToParameterList());
        }

        [Fact]
        public void Rgba_RoundTrips()
        {
            var planes = Planes(4);
            var decoded = J2kImage.FromBytes(Encode(planes, ChannelDefinitionData.CreateRgba()));

            Assert.Equal(4, decoded.NumberOfComponents);
            for (var c = 0; c < 4; c++) Assert.Equal(planes[c], decoded.GetComponent(c));
        }

        [Fact]
        public void GrayscaleAlpha_RoundTrips()
        {
            var planes = Planes(2);
            var decoded = J2kImage.FromBytes(Encode(planes, ChannelDefinitionData.CreateGrayscaleAlpha()));

            Assert.Equal(2, decoded.NumberOfComponents);
            for (var c = 0; c < 2; c++) Assert.Equal(planes[c], decoded.GetComponent(c));
        }

        [Fact]
        public void ColourChannels_ComeOutInTheOrderOfTheColours()
        {
            // channel 0 is blue (colour 3), channel 2 is red (colour 1)
            var definitions = new ChannelDefinitionData();
            definitions.AddColorChannel(0, 3);
            definitions.AddColorChannel(1, 2);
            definitions.AddColorChannel(2, 1);
            var planes = Planes(3);
            var decoded = J2kImage.FromBytes(Encode(planes, definitions));

            Assert.Equal(planes[2], decoded.GetComponent(0));
            Assert.Equal(planes[1], decoded.GetComponent(1));
            Assert.Equal(planes[0], decoded.GetComponent(2));
        }

        [Fact]
        public void OpacityChannelFirst_ComesOutLast()
        {
            var definitions = new ChannelDefinitionData();
            definitions.AddOpacityChannel(0);
            definitions.AddColorChannel(1, 1);
            definitions.AddColorChannel(2, 2);
            definitions.AddColorChannel(3, 3);
            var planes = Planes(4);
            var decoded = J2kImage.FromBytes(Encode(planes, definitions));

            Assert.Equal(planes[1], decoded.GetComponent(0));
            Assert.Equal(planes[2], decoded.GetComponent(1));
            Assert.Equal(planes[3], decoded.GetComponent(2));
            Assert.Equal(planes[0], decoded.GetComponent(3));
        }

        [Fact]
        public void ChannelsTheBoxDoesNotMention_AreKept()
        {
            var definitions = new ChannelDefinitionData();
            definitions.AddColorChannel(1, 1);
            var planes = Planes(3);
            var decoded = J2kImage.FromBytes(Encode(planes, definitions));

            Assert.Equal(3, decoded.NumberOfComponents);
            Assert.Equal(planes[1], decoded.GetComponent(0));
            Assert.Equal(planes[0], decoded.GetComponent(1));
            Assert.Equal(planes[2], decoded.GetComponent(2));
        }

        /// <summary>
        /// The inverse colour transform only touches the first three components. Asked first for a later one, it asked the wavelet
        /// stage for floats, was handed a block of another type, and read the empty block it had passed in.
        /// </summary>
        [Fact]
        public void ComponentBeyondTheColourTransform_RequestedFirst_Decodes()
        {
            // output channel 0 is component 4, which the lossy colour transform leaves alone
            var definitions = new ChannelDefinitionData();
            definitions.AddColorChannel(4, 1);
            definitions.AddColorChannel(0, 2);
            definitions.AddColorChannel(1, 3);
            definitions.AddOpacityChannel(2);
            definitions.AddOpacityChannel(3);
            var planes = Planes(5);
            var decoded = J2kImage.FromBytes(Encode(planes, definitions, lossless: false));

            Assert.Equal(5, decoded.NumberOfComponents);
            var first = decoded.GetComponent(0);
            Assert.True(first.Zip(planes[4], (a, b) => Math.Abs(a - b)).Max() <= 3, "Output channel 0 is the fifth component.");
        }
    }
}
