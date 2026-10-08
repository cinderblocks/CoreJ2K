// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CoreJ2K.ImageSharp.Tests
{
    /// <summary>The telemetry callback on the configuration is reached by an ImageSharp encode.</summary>
    public class EncodeTelemetryTests
    {
        [Fact]
        public void EncodeToJ2K_ReportsWhatWasKept()
        {
            var rnd = new Random(3);
            using var image = new Image<Rgb24>(160, 200);
            for (var y = 0; y < 200; y++)
                for (var x = 0; x < 160; x++)
                    image[x, y] = new Rgb24((byte)rnd.Next(60, 200), (byte)(x + y), (byte)(x * 2 % 255));

            EncodeTelemetry? seen = null;
            var data = image.EncodeToJ2K(new J2KEncoderConfiguration().WithMaxBytes(5000).WithTelemetry(t => seen = t));

            Assert.NotNull(seen);
            Assert.Equal(data.Length, seen!.TotalBytes);
            Assert.True(data.Length <= 5000);
            Assert.Equal(seen.PacketBodyBytes, seen.CodeBlocks.Sum(b => (long)b.BytesInLayer));
        }
    }
}
