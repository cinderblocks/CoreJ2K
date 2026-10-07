// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.roi;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.ROI
{
    /// <summary>
    /// <see cref="J2KEncoderConfiguration.WithROI"/> must reach the encoder, and Maxshift must refuse images whose
    /// coefficients are too wide for it rather than write a codestream that decodes to garbage.
    /// </summary>
    public class ROIEncodingTests
    {
        private const int Width = 240;
        private const int Height = 320;

        // The region of interest used throughout: x 60..179, y 80..239.
        private const int RoiX = 60, RoiY = 80, RoiW = 120, RoiH = 160;

        private static InterleavedImageSource MakeImage()
        {
            var rnd = new Random(5);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[Width * Height];
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < Width; x++)
                    {
                        var v = 128 + (int)(60 * Math.Sin(x / (5.0 + c)) * Math.Cos(y / 6.0)) + rnd.Next(-20, 21);
                        comps[c][y * Width + x] = Math.Clamp(v, 0, 255) - 128;
                    }
            }
            return new InterleavedImageSource(Width, Height, 3, 8, new bool[3], comps);
        }

        private static ParameterList Parameters(J2KEncoderConfiguration config)
        {
            var pl = config.ToParameterList();
            pl["file_format"] = "off";
            pl["Alayers"] = "sl";
            pl["threads"] = "1";
            return pl;
        }

        private static J2KEncoderConfiguration Configuration(ROIConfiguration? roi = null)
        {
            var config = new J2KEncoderConfiguration().WithBitrate(0.4f).WithFileFormat(false);
            return roi == null ? config : config.WithROI(roi);
        }

        private static ROIConfiguration FaceRoi() =>
            new ROIConfiguration().AddRectangle(-1, RoiX, RoiY, RoiW, RoiH).SetStartLevel(2);

        private static bool HasRgnMarker(byte[] data)
        {
            for (var i = 0; i < data.Length - 1; i++)
                if (data[i] == 0xFF && data[i + 1] == 0x5E) return true;
            return false;
        }

        /// <summary>Mean absolute error of the decoded image inside and outside the ROI, over all components.</summary>
        private static (double Inside, double Outside) DecodeError(byte[] data, InterleavedImageSource original)
        {
            using var decoded = J2kImage.FromBytes(data);
            double inside = 0, outside = 0;
            long nInside = 0, nOutside = 0;
            var block = new j2k.image.DataBlkInt(0, 0, Width, Height);
            for (var c = 0; c < 3; c++)
            {
                var got = decoded.GetComponent(c);
                var want = ((j2k.image.DataBlkInt)original.GetInternCompData(block, c)).DataInt;
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < Width; x++)
                    {
                        var error = Math.Abs(got[y * Width + x] - (want[y * Width + x] + 128));
                        if (x >= RoiX && x < RoiX + RoiW && y >= RoiY && y < RoiY + RoiH) { inside += error; nInside++; }
                        else { outside += error; nOutside++; }
                    }
            }
            return (inside / nInside, outside / nOutside);
        }

        [Fact]
        public void ApplyTo_WritesTheRoiOptions()
        {
            var pl = new ParameterList();
            new ROIConfiguration()
                .AddRectangle(-1, 10, 20, 30, 40)
                .AddCircle(-1, 50, 60, 70)
                .SetStartLevel(4)
                .SetBlockAlignment(true)
                .ForceGenericMask()
                .ApplyTo(pl);

            Assert.Equal("R 10 20 30 40 C 50 60 70", pl["Rroi"]);
            Assert.Equal("4", pl["Rstart_level"]);
            Assert.Equal("on", pl["Ralign"]);
            Assert.Equal("on", pl["Rno_rect"]);
        }

        [Fact]
        public void ApplyTo_PutsAllComponentRegionsBeforeTheComponentSpecificOnes()
        {
            // A component prefix applies to every region after it, so a region for all components must not follow one.
            var pl = new ParameterList();
            new ROIConfiguration()
                .AddRectangle(1, 1, 2, 3, 4)
                .AddRectangle(-1, 5, 6, 7, 8)
                .AddCircle(0, 9, 10, 11)
                .AddRectangle(1, 12, 13, 14, 15)
                .ApplyTo(pl);

            Assert.Equal("R 5 6 7 8 c1 R 1 2 3 4 R 12 13 14 15 c0 C 9 10 11", pl["Rroi"]);
        }

        [Fact]
        public void ApplyTo_WritesNothingWithoutRegions()
        {
            var pl = new ParameterList();
            new ROIConfiguration().SetStartLevel(3).ApplyTo(pl);
            Assert.Null(pl.GetParameter("Rroi"));
            Assert.Null(pl.GetParameter("Rstart_level"));
        }

        [Fact]
        public void Validate_RejectsAComponentBelowMinusOneAndMaskPathsWithWhitespace()
        {
            Assert.Contains(new ROIConfiguration().AddRectangle(-2, 0, 0, 10, 10).Validate(), e => e.Contains("component"));

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "roi mask " + Guid.NewGuid().ToString("N") + ".pgm");
            System.IO.File.WriteAllText(path, "P2\n1 1\n255\n255\n");
            try
            {
                Assert.Contains(new ROIConfiguration().AddArbitraryShape(-1, path).Validate(), e => e.Contains("whitespace"));
            }
            finally { System.IO.File.Delete(path); }
        }

        [Fact]
        public void WithROI_ReachesTheEncoder()
        {
            var plain = J2kImage.ToBytes(MakeImage(), null, Parameters(Configuration()))!;
            var withRoi = J2kImage.ToBytes(MakeImage(), null, Parameters(Configuration(FaceRoi())))!;

            Assert.False(HasRgnMarker(plain));
            Assert.True(HasRgnMarker(withRoi), "The ROI configuration did not produce an RGN marker.");
            Assert.False(plain.AsSpan().SequenceEqual(withRoi));
        }

        [Fact]
        public void WithROI_MatchesTheRroiParameter()
        {
            var viaConfiguration = J2kImage.ToBytes(MakeImage(), null, Parameters(Configuration(FaceRoi())))!;

            var pl = Parameters(Configuration());
            pl["Rroi"] = $"R {RoiX} {RoiY} {RoiW} {RoiH}";
            pl["Rstart_level"] = "2";
            var viaParameter = J2kImage.ToBytes(MakeImage(), null, pl)!;

            Assert.True(viaConfiguration.AsSpan().SequenceEqual(viaParameter));
        }

        [Fact]
        public void WithROI_OnTheBuilder_SetsTheRoi()
        {
            var config = new CompleteEncoderConfigurationBuilder()
                .WithBitrate(0.4f).WithFileFormat(false)
                .WithROI(roi => roi.AddRectangle(-1, RoiX, RoiY, RoiW, RoiH).SetStartLevel(2))
                .Build();

            Assert.NotNull(config.ROI);
            Assert.Single(config.ROI!.Regions);
            Assert.Equal("R 60 80 120 160", config.ToParameterList()["Rroi"]);
        }

        [Fact]
        public void WithROI_CodesTheRegionBetterThanTheBackground()
        {
            var original = MakeImage();
            var plain = DecodeError(J2kImage.ToBytes(MakeImage(), null, Parameters(Configuration()))!, original);
            var roi = DecodeError(J2kImage.ToBytes(MakeImage(), null, Parameters(Configuration(FaceRoi())))!, original);

            Assert.True(roi.Inside < roi.Outside, $"ROI error {roi.Inside:F2} should be below background error {roi.Outside:F2}.");
            Assert.True(roi.Inside < plain.Inside, $"ROI error {roi.Inside:F2} should beat the unprioritised {plain.Inside:F2}.");
        }

        [Fact]
        public void Maxshift_AtTheMagnitudeLimit_StillDecodes()
        {
            var original = MakeImage();
            var pl = Parameters(Configuration(FaceRoi()));
            pl["Qstep"] = "0.002"; // 15 magnitude bits
            var error = DecodeError(J2kImage.ToBytes(MakeImage(), null, pl)!, original);

            Assert.True(error.Inside < error.Outside, $"ROI error {error.Inside:F2}, background {error.Outside:F2}.");
        }

        [Fact]
        public void Maxshift_BeyondTheMagnitudeLimit_IsRejected()
        {
            var pl = Parameters(Configuration(FaceRoi()));
            pl["Qstep"] = "0.001"; // 16 magnitude bits: the shifted coefficients would not fit in 31
            var ex = Assert.Throws<InvalidOperationException>(() => J2kImage.ToBytes(MakeImage(), null, pl));
            Assert.Contains("magnitude bits", ex.Message);
        }

        [Fact]
        public void BlockAlignedRoi_IsNotLimitedByTheMagnitudeBits()
        {
            // Block-aligned ROI scales the distortion estimates, not the coefficients, so nothing overflows.
            var roi = FaceRoi().SetBlockAlignment(true);
            var pl = Parameters(Configuration(roi));
            pl["Qstep"] = "0.001";
            var data = J2kImage.ToBytes(MakeImage(), null, pl)!;
            Assert.True(data.Length > 0);
        }
    }
}
