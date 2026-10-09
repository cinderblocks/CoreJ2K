// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// Some encoders set the multiple-component-transform flag of the COD marker segment on an image with fewer than three components,
    /// where there is nothing to transform. The decoder refused such a file ("wavelet transformation and component transformation
    /// not coherent"); the transform is not applied instead, which is what other decoders do.
    /// </summary>
    public class ColourTransformFlagTests
    {
        private static byte[] SetTransformFlag(byte[] file)
        {
            var copy = (byte[])file.Clone();
            int start;
            for (start = 0; start + 3 < copy.Length; start++)
                if (copy[start] == 0xFF && copy[start + 1] == 0x4F && copy[start + 2] == 0xFF && copy[start + 3] == 0x51) break;
            for (var i = start + 2; i + 3 < copy.Length && copy[i + 1] != 0x90; i += 2 + ((copy[i + 2] << 8) | copy[i + 3]))
                if (copy[i + 1] == 0x52)
                {
                    copy[i + 8] = 1; // marker (2), length (2), Scod (1), progression order (1), layers (2), then the transform
                    return copy;
                }
            throw new InvalidOperationException("No COD marker segment.");
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(2, false)]
        public void TransformFlagOnFewerThanThreeComponents_IsIgnored(int components, bool lossless)
        {
            var rnd = new Random(4);
            var planes = Enumerable.Range(0, components).Select(_ => Enumerable.Range(0, 48 * 40).Select(i => rnd.Next(256)).ToArray()).ToArray();
            var source = new InterleavedImageSource(48, 40, components, 8, new bool[components], planes.Select(p => p.Select(v => v - 128).ToArray()).ToArray());
            var configuration = new J2KEncoderConfiguration();
            if (lossless) configuration = configuration.WithLossless();
            var data = J2kImage.ToBytes(source, configuration.ToParameterList());

            var decoded = J2kImage.FromBytes(SetTransformFlag(data));

            Assert.Equal(components, decoded.NumberOfComponents);
            for (var c = 0; c < components; c++)
            {
                var diff = decoded.GetComponent(c).Zip(planes[c], (a, b) => Math.Abs(a - b)).Max();
                Assert.True(diff <= (lossless ? 0 : 4), $"component {c} differs by {diff}");
            }
        }
    }
}
