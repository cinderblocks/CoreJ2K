// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// <c>WithMaxLayers(n)</c> asks for the first n quality layers. The limit was applied to layer numbers counted from 1 as if they
    /// counted from 0, so n layers gave n - 1: one layer gave a blank image, and all of them could not be decoded by naming their count.
    /// </summary>
    public class LayerLimitTests
    {
        private const int Width = 96, Height = 80;

        private static byte[] Encode(out int[] source)
        {
            var rnd = new Random(11);
            source = new int[Width * Height];
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    source[y * Width + x] = Math.Clamp((int)(128 + 90 * Math.Sin(x / 9.0) * Math.Cos(y / 7.0)) + rnd.Next(-40, 41), 0, 255);

            var image = new InterleavedImageSource(Width, Height, 1, 8, new[] { false }, new[] { source.Select(v => v - 128).ToArray() });
            var parameters = new ParameterList(J2kImage.GetDefaultEncoderParameterList());
            parameters["lossless"] = "on";
            parameters["verbose"] = "off";
            parameters["Alayers"] = "0.5 1.0 2.0 8.0";
            return J2kImage.ToBytes(image, null, parameters);
        }

        private static int LayersInHeader(byte[] file)
        {
            var d = ScenarioRunner.Codestream(file);
            for (var i = 2; i + 3 < d.Length && d[i + 1] != 0x90; i += 2 + ((d[i + 2] << 8) | d[i + 3]))
                if (d[i + 1] == 0x52) return (d[i + 6] << 8) | d[i + 7];
            throw new InvalidOperationException("No COD marker segment.");
        }

        private static int[] Decode(byte[] data, int maxLayers)
        {
            var configuration = new J2KDecoderConfiguration().WithQuitConditions(q => q.WithMaxLayers(maxLayers));
            return J2kImage.FromBytes(data, configuration).GetComponent(0);
        }

        [Fact]
        public void MaxLayers_DecodesExactlyThatManyLayers()
        {
            var data = Encode(out var source);
            var layers = LayersInHeader(data);
            Assert.True(layers >= 3, $"The test needs several layers; the file has {layers}.");

            var psnr = Enumerable.Range(1, layers)
                .Select(n => ScenarioRunner.Compare(new[] { source }, new[] { Decode(data, n) }, Width, 8).Psnr)
                .ToArray();

            // a blank image is about 13 dB from this source
            Assert.True(psnr[0] > 18, $"One layer gave {psnr[0]:F1} dB: the image is blank.");
            for (var n = 1; n < layers; n++)
                Assert.True(psnr[n] >= psnr[n - 1], $"{n + 1} layers ({psnr[n]:F1} dB) must be at least as close to the source as {n} ({psnr[n - 1]:F1} dB).");
            Assert.True(psnr[layers - 2] > psnr[0], "Later layers must add detail.");
            Assert.True(double.IsPositiveInfinity(psnr[layers - 1]), "All the layers of a lossless file give the source back.");
            Assert.Equal(J2kImage.FromBytes(data).GetComponent(0), Decode(data, layers));
        }

        [Fact]
        public void MaxLayers_BeyondTheLayerCount_DecodesEverything()
        {
            var data = Encode(out var source);
            Assert.Equal(source, Decode(data, LayersInHeader(data) + 5));
        }
    }
}
