// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.Configuration;
using CoreJ2K.ImageSharp;
using CoreJ2K.j2k.roi;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CoreJ2K.ImageSharp.Tests
{
    /// <summary>A region of interest set on the configuration reaches an ImageSharp encode, and is cut to a byte limit.</summary>
    public class RegionOfInterestTests
    {
        private const int Width = 200;
        private const int Height = 260;

        private static Image<Rgb24> MakeImage()
        {
            var rnd = new Random(7);
            var image = new Image<Rgb24>(Width, Height);
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    image[x, y] = new Rgb24(
                        (byte)Math.Clamp(120 + 60 * Math.Sin(x / 6.0) + rnd.Next(-20, 21), 0, 255),
                        (byte)Math.Clamp(120 + 60 * Math.Cos(y / 7.0) + rnd.Next(-20, 21), 0, 255),
                        (byte)Math.Clamp(110 + (x ^ y) % 50 + rnd.Next(-20, 21), 0, 255));
            return image;
        }

        private static bool HasRgnMarker(byte[] data)
        {
            for (var i = 0; i < data.Length - 1; i++)
                if (data[i] == 0xFF && data[i + 1] == 0x5E) return true;
            return false;
        }

        [Fact]
        public void EncodeToJ2K_WithRoi_WritesTheRoi()
        {
            using var image = MakeImage();
            var roi = new ROIConfiguration().AddRectangle(-1, 50, 60, 100, 120).SetStartLevel(2);

            var plain = image.EncodeToJ2K(new J2KEncoderConfiguration().WithBitrate(0.4f));
            var withRoi = image.EncodeToJ2K(new J2KEncoderConfiguration().WithBitrate(0.4f).WithROI(roi));

            Assert.False(HasRgnMarker(plain));
            Assert.True(HasRgnMarker(withRoi));
        }

        [Fact]
        public void EncodeToJ2K_WithAnInMemoryMask_WritesTheRoi()
        {
            using var image = MakeImage();
            // ImageSharp's PointF is not System.Drawing's, so landmarks go in as plain coordinates.
            var landmarks = new PointF[] { new(50, 80), new(150, 70), new(160, 160), new(100, 220), new(40, 160) };
            var mask = ROIMask.FromConvexHull(Width, Height, Array.ConvertAll(landmarks, p => ((double)p.X, (double)p.Y)));

            var plain = image.EncodeToJ2K(new J2KEncoderConfiguration().WithBitrate(0.4f));
            var withMask = image.EncodeToJ2K(new J2KEncoderConfiguration().WithBitrate(0.4f)
                .WithROI(new ROIConfiguration().AddMask(-1, mask).SetStartLevel(2)));

            Assert.True(HasRgnMarker(withMask));
            Assert.False(plain.AsSpan().SequenceEqual(withMask));

            // Read it back.
            var decoded = ImageSharpJ2kExtensions.FromJ2KBytes(withMask);
            Assert.Equal(Width, decoded.Width);
            Assert.Equal(Height, decoded.Height);
        }

        [Fact]
        public void EncodeToJ2K_WithAByteLimit_StaysWithinIt()
        {
            using var image = MakeImage();
            var roi = new ROIConfiguration().AddRectangle(-1, 50, 60, 100, 120).SetStartLevel(2);

            foreach (var limit in new[] { 4000, 6500, 9000 })
            {
                var data = image.EncodeToJ2K(new J2KEncoderConfiguration().WithMaxBytes(limit).WithROI(roi));
                Assert.True(data.Length <= limit, $"{data.Length} bytes for a limit of {limit}.");
                Assert.True(data.Length >= limit * 0.9);
            }
        }

        [Fact]
        public void FromJ2K_ReadsRgbAndGreyStreams_AsRgba32()
        {
            using var rgb = MakeImage();
            var rgbData = rgb.EncodeToJ2KLossless();

            using var fromBytes = ImageSharpJ2kExtensions.FromJ2KBytes(rgbData);
            using var fromStream = ImageSharpJ2kExtensions.FromJ2KStream(new System.IO.MemoryStream(rgbData));
            Assert.Equal(new Rgba32(rgb[3, 4].R, rgb[3, 4].G, rgb[3, 4].B, 255), fromBytes[3, 4]);
            Assert.Equal(fromBytes[10, 20], fromStream[10, 20]);

            using var grey = new Image<L8>(16, 16);
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    grey[x, y] = new L8((byte)(x * 16 + y));
            using var fromGrey = ImageSharpJ2kExtensions.FromJ2KBytes(grey.EncodeToJ2KLossless());
            Assert.Equal(new Rgba32(5 * 16 + 7, 5 * 16 + 7, 5 * 16 + 7, 255), fromGrey[5, 7]);
        }
    }
}
