// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>Values of the SIZ marker segment that are legal but unusual.</summary>
    public class SizMarkerTests
    {
        /// <summary>
        /// The tile size is an unsigned 32-bit value, and a tile larger than the image is one tile. A size of 2^31 or more was read as a
        /// negative number and refused.
        /// </summary>
        [Fact]
        public void TileSizeOf2ToThe32Minus1_IsOneTileOverTheImage()
        {
            var rnd = new Random(5);
            var plane = Enumerable.Range(0, 40 * 32).Select(_ => rnd.Next(256)).ToArray();
            var source = new InterleavedImageSource(40, 32, 1, 8, new[] { false }, new[] { plane.Select(v => v - 128).ToArray() });
            var data = J2kImage.ToBytes(source, new J2KEncoderConfiguration().WithLossless().ToParameterList());

            var patched = (byte[])data.Clone();
            int start;
            for (start = 0; start + 3 < patched.Length; start++)
                if (patched[start] == 0xFF && patched[start + 1] == 0x4F && patched[start + 2] == 0xFF && patched[start + 3] == 0x51) break;
            var siz = start + 2;
            for (var i = 0; i < 8; i++) patched[siz + 22 + i] = 0xFF; // XTsiz and YTsiz follow Rsiz, Xsiz, Ysiz, XOsiz and YOsiz

            Assert.Equal(plane, J2kImage.FromBytes(patched).GetComponent(0));
        }
    }
}
