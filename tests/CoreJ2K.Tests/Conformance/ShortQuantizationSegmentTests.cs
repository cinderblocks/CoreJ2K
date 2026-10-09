// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// A QCD marker segment with fewer step sizes than the decomposition has subbands. A 34x34 image with 32 decomposition levels in the
    /// OpenJPEG test suite carries 18 of about 97. The reader asked the segment for entries it did not have and failed with an
    /// end-of-stream exception; the subbands without an entry now get a zero exponent, as other decoders give them.
    /// </summary>
    public class ShortQuantizationSegmentTests
    {
        /// <summary>Cuts the step sizes of the first QCD marker segment down to <paramref name="keep"/> entries.</summary>
        private static byte[] ShortenQcd(byte[] codestream, int keep, int bytesPerEntry = 1)
        {
            for (var i = 2; i + 4 < codestream.Length && codestream[i + 1] != 0x90; i += 2 + ((codestream[i + 2] << 8) | codestream[i + 3]))
            {
                if (codestream[i + 1] != 0x5C) continue;
                var length = (codestream[i + 2] << 8) | codestream[i + 3];
                Assert.Equal(bytesPerEntry == 1 ? 0 : 2, codestream[i + 4] & 0x1F); // no quantization: one byte per subband; expounded: two
                var newLength = 3 + keep * bytesPerEntry;
                var result = new List<byte>(codestream.Take(i + 2));
                result.Add((byte)(newLength >> 8));
                result.Add((byte)newLength);
                result.Add(codestream[i + 4]);
                result.AddRange(codestream.Skip(i + 5).Take(keep * bytesPerEntry));
                result.AddRange(codestream.Skip(i + 2 + length));
                return result.ToArray();
            }
            throw new InvalidOperationException("No QCD marker segment.");
        }

        [Fact]
        public void QcdWithFewerStepSizesThanSubbands_StillDecodes()
        {
            const int width = 16, height = 16, levels = 10;
            var rnd = new Random(8);
            var plane = Enumerable.Range(0, width * height).Select(_ => rnd.Next(256)).ToArray();
            var source = new InterleavedImageSource(width, height, 1, 8, new[] { false }, new[] { plane.Select(v => v - 128).ToArray() });
            var parameters = new ParameterList(J2kImage.GetDefaultEncoderParameterList());
            parameters["lossless"] = "on";
            parameters["verbose"] = "off";
            parameters["file_format"] = "off";
            parameters["Wlev"] = levels.ToString();
            var data = J2kImage.ToBytes(source, null, parameters);
            Assert.Equal(plane, J2kImage.FromBytes(data).GetComponent(0));

            // keep the step sizes of the four coarsest resolutions only; what the finer subbands then decode to is up to the decoder
            var decoded = J2kImage.FromBytes(ShortenQcd(data, 1 + 3 * 4));
            Assert.Equal((width, height, 1), (decoded.Width, decoded.Height, decoded.NumberOfComponents));
        }

        [Fact]
        public void ExpoundedQcdWithFewerStepSizesThanSubbands_StillDecodes()
        {
            const int width = 16, height = 16, levels = 10;
            var rnd = new Random(9);
            var plane = Enumerable.Range(0, width * height).Select(_ => rnd.Next(256)).ToArray();
            var source = new InterleavedImageSource(width, height, 1, 8, new[] { false }, new[] { plane.Select(v => v - 128).ToArray() });
            var parameters = new ParameterList(J2kImage.GetDefaultEncoderParameterList());
            parameters["verbose"] = "off";
            parameters["file_format"] = "off";
            parameters["Wlev"] = levels.ToString();
            parameters["Qtype"] = "expounded";
            var data = J2kImage.ToBytes(source, null, parameters);

            var decoded = J2kImage.FromBytes(ShortenQcd(data, 1 + 3 * 4, bytesPerEntry: 2));
            Assert.Equal((width, height, 1), (decoded.Width, decoded.Height, decoded.NumberOfComponents));
        }
    }
}
