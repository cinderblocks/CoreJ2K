// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// A JP2 file with any number of components decodes: without a palette the palette stage passes the components through.
    /// </summary>
    public class ComponentCountTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(8)]
        public void Jp2_RoundTripsAnyNumberOfComponents(int components)
        {
            var rnd = new Random(1);
            var samples = Enumerable.Range(0, components).Select(_ => Enumerable.Range(0, 32 * 24).Select(i => rnd.Next(256)).ToArray()).ToArray();
            var source = new InterleavedImageSource(32, 24, components, 8, new bool[components],
                samples.Select(c => c.Select(v => v - 128).ToArray()).ToArray());

            var data = J2kImage.ToBytes(source, null, new J2KEncoderConfiguration().WithLossless().WithFileFormat(true).ToParameterList());
            var decoded = J2kImage.FromBytes(data);

            Assert.Equal(components, decoded.NumberOfComponents);
            for (var c = 0; c < components; c++)
                Assert.Equal(samples[c], decoded.GetComponent(c));
        }
    }
}
