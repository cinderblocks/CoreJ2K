// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.roi;
using CoreJ2K.j2k.util;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.ROI
{
    /// <summary>
    /// <see cref="ROIMask"/> holds a region of any shape in memory, without the PGM file that arbitrary shapes needed.
    /// </summary>
    public class ROIMaskTests
    {
        private const int Width = 240;
        private const int Height = 320;

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

        private static byte[] Encode(ROIConfiguration roi, Action<ParameterList>? configure = null)
        {
            var pl = new J2KEncoderConfiguration().WithBitrate(0.4f).WithFileFormat(false).WithROI(roi).ToParameterList();
            pl["Alayers"] = "sl";
            pl["threads"] = "1";
            configure?.Invoke(pl);
            return J2kImage.ToBytes(MakeImage(), null, pl)!;
        }

        private static ROIMask Rectangle(int x, int y, int w, int h) =>
            ROIMask.FromPredicate(Width, Height, (px, py) => px >= x && px < x + w && py >= y && py < y + h);

        [Fact]
        public void FromBytes_NonZeroIsInside()
        {
            var mask = ROIMask.FromBytes(3, 2, new byte[] { 0, 1, 0, 255, 0, 7 });

            Assert.Equal(3, mask.Width);
            Assert.Equal(2, mask.Height);
            Assert.Equal(3, mask.PixelCount);
            Assert.False(mask.Contains(0, 0));
            Assert.True(mask.Contains(1, 0));
            Assert.True(mask.Contains(0, 1));
            Assert.True(mask.Contains(2, 1));
        }

        [Fact]
        public void FromBytes_RejectsTheWrongLength()
        {
            Assert.Throws<ArgumentException>(() => ROIMask.FromBytes(3, 2, new byte[5]));
            Assert.Throws<ArgumentOutOfRangeException>(() => ROIMask.FromBytes(0, 2, ReadOnlySpan<byte>.Empty));
        }

        [Fact]
        public void Contains_RejectsPixelsOutsideTheMask()
        {
            var mask = ROIMask.FromBytes(2, 2, new byte[4]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mask.Contains(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => mask.Contains(0, -1));
        }

        [Fact]
        public void FromPolygon_Rectangle_CoversExactlyThePixelsWhoseCentresAreInside()
        {
            var mask = ROIMask.FromPolygon(Width, Height, new[] { new PointF(10, 20), new PointF(30, 20), new PointF(30, 50), new PointF(10, 50) });

            Assert.Equal(20 * 30, mask.PixelCount);
            Assert.True(mask.Contains(10, 20));
            Assert.True(mask.Contains(29, 49));
            Assert.False(mask.Contains(30, 49));
            Assert.False(mask.Contains(29, 50));
            Assert.False(mask.Contains(9, 20));
        }

        [Fact]
        public void FromPolygon_Triangle_HasAboutTheRightArea()
        {
            var mask = ROIMask.FromPolygon(Width, Height, new[] { new PointF(20, 20), new PointF(120, 20), new PointF(20, 120) });
            Assert.InRange(mask.PixelCount, 4900, 5100);   // area 5000
            Assert.True(mask.Contains(30, 30));
            Assert.False(mask.Contains(100, 100));
        }

        [Fact]
        public void FromPolygon_CutsOffWhatIsOutsideTheImage()
        {
            var mask = ROIMask.FromPolygon(Width, Height, new[] { new PointF(-50, -50), new PointF(1000, -50), new PointF(1000, 1000), new PointF(-50, 1000) });
            Assert.Equal((long)Width * Height, mask.PixelCount);
        }

        [Fact]
        public void FromPolygon_RejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => ROIMask.FromPolygon(10, 10, new[] { new PointF(0, 0), new PointF(5, 5) }));
            Assert.Throws<ArgumentException>(() => ROIMask.FromPolygon(10, 10, new[] { new PointF(0, 0), new PointF(5, float.NaN), new PointF(9, 9) }));
        }

        [Fact]
        public void FromConvexHull_IgnoresPointsInsideTheHull()
        {
            var corners = new[] { new PointF(10, 20), new PointF(30, 20), new PointF(30, 50), new PointF(10, 50) };
            var withInterior = corners.Concat(new[] { new PointF(15, 30), new PointF(20, 40), new PointF(25, 25) }).ToArray();

            var hull = ROIMask.FromConvexHull(Width, Height, withInterior);
            var polygon = ROIMask.FromPolygon(Width, Height, corners);

            Assert.Equal(polygon.PixelCount, hull.PixelCount);
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    Assert.Equal(polygon.Contains(x, y), hull.Contains(x, y));
        }

        [Fact]
        public void FromConvexHull_SpansAFaceShapedPointCloud()
        {
            // An oval of 68 points, like a landmark set: the hull should hold all of them.
            var points = Enumerable.Range(0, 68)
                .Select(i => new PointF(120 + 70f * (float)Math.Cos(i * 0.0925), 160 + 100f * (float)Math.Sin(i * 0.0925)))
                .ToArray();
            var mask = ROIMask.FromConvexHull(Width, Height, points);

            Assert.InRange(mask.PixelCount, 20000, 22500);   // an ellipse of 70 x 100: about 22000
            Assert.True(mask.Contains(120, 160));
            Assert.False(mask.Contains(5, 5));
        }

        [Fact]
        public void TupleOverloads_MatchThePointOnes()
        {
            var corners = new[] { new PointF(10.25f, 20.5f), new PointF(130, 25), new PointF(90, 150), new PointF(5, 100) };
            var tuples = corners.Select(p => ((double)p.X, (double)p.Y)).ToArray();

            var polygon = ROIMask.FromPolygon(Width, Height, corners);
            var hull = ROIMask.FromConvexHull(Width, Height, corners);
            Assert.Equal(polygon.PixelCount, ROIMask.FromPolygon(Width, Height, tuples).PixelCount);
            Assert.Equal(hull.PixelCount, ROIMask.FromConvexHull(Width, Height, tuples).PixelCount);
            Assert.Equal(polygon.PixelCount, hull.PixelCount);
        }

        [Fact]
        public void FromConvexHull_RejectsPointsOnALine()
        {
            Assert.Throws<ArgumentException>(() =>
                ROIMask.FromConvexHull(Width, Height, new[] { new PointF(1, 1), new PointF(2, 2), new PointF(3, 3), new PointF(4, 4) }));
        }

        [Fact]
        public void FromEllipse_HasAboutTheRightArea()
        {
            var mask = ROIMask.FromEllipse(Width, Height, 120, 160, 60, 90);
            Assert.InRange(mask.PixelCount, (long)(Math.PI * 60 * 90 * 0.99), (long)(Math.PI * 60 * 90 * 1.01));
            Assert.True(mask.Contains(120, 160));
            Assert.False(mask.Contains(120 + 61, 160));
        }

        [Fact]
        public void AddMask_PassesTheMaskInTheParameterList()
        {
            var mask = Rectangle(10, 10, 50, 50);
            var other = Rectangle(100, 100, 20, 20);
            var pl = new ParameterList();
            new ROIConfiguration()
                .AddMask(-1, mask)
                .AddRectangle(-1, 1, 2, 3, 4)
                .AddMask(1, mask)
                .AddMask(1, other)
                .ApplyTo(pl);

            Assert.Equal("M 0 R 1 2 3 4 c1 M 0 M 1", pl["Rroi"]);
            Assert.Equal(2, pl.RoiMasks.Count);
            Assert.Same(mask, pl.RoiMasks[0]);
            Assert.Same(other, pl.RoiMasks[1]);
        }

        [Fact]
        public void AddMask_ValidatesTheMask()
        {
            Assert.Throws<ArgumentNullException>(() => new ROIConfiguration().AddMask(-1, null!));
            var empty = ROIMask.FromBytes(2, 2, new byte[4]);
            Assert.Contains(new ROIConfiguration().AddMask(-1, empty).Validate(), e => e.Contains("at least one pixel"));
        }

        [Fact]
        public void RectangularMask_EncodesLikeTheRectangle()
        {
            var viaMask = Encode(new ROIConfiguration().AddMask(-1, Rectangle(60, 80, 120, 160)).SetStartLevel(2));
            var viaRectangle = Encode(new ROIConfiguration().AddRectangle(-1, 60, 80, 120, 160).SetStartLevel(2).ForceGenericMask());

            Assert.True(viaMask.AsSpan().SequenceEqual(viaRectangle));
        }

        [Fact]
        public void RectangularMask_EncodesLikeTheRectangle_WithSeveralTiles()
        {
            // Tiles that start part-way across the mask read it from an offset.
            void Tiles(ParameterList pl) => pl["tiles"] = "100 128";

            var viaMask = Encode(new ROIConfiguration().AddMask(-1, Rectangle(60, 80, 120, 160)).SetStartLevel(2), Tiles);
            var viaRectangle = Encode(new ROIConfiguration().AddRectangle(-1, 60, 80, 120, 160).SetStartLevel(2).ForceGenericMask(), Tiles);

            Assert.True(viaMask.AsSpan().SequenceEqual(viaRectangle));
        }

        [Fact]
        public void Mask_ForOneComponent_EncodesLikeTheRectangleForThatComponent()
        {
            var viaMask = Encode(new ROIConfiguration().AddMask(0, Rectangle(60, 80, 120, 160)));
            var viaRectangle = Encode(new ROIConfiguration().AddRectangle(0, 60, 80, 120, 160).ForceGenericMask());
            var allComponents = Encode(new ROIConfiguration().AddMask(-1, Rectangle(60, 80, 120, 160)));

            Assert.True(viaMask.AsSpan().SequenceEqual(viaRectangle));
            Assert.False(viaMask.AsSpan().SequenceEqual(allComponents));
        }

        [Fact]
        public void EllipticalMask_CodesTheRegionBetterThanTheBackground()
        {
            var mask = ROIMask.FromEllipse(Width, Height, 120, 160, 60, 80);
            var data = Encode(new ROIConfiguration().AddMask(-1, mask).SetStartLevel(2));
            using var decoded = J2kImage.FromBytes(data);

            double inside = 0, outside = 0;
            long nInside = 0, nOutside = 0;
            var block = new j2k.image.DataBlkInt(0, 0, Width, Height);
            var original = MakeImage();
            for (var c = 0; c < 3; c++)
            {
                var got = decoded.GetComponent(c);
                var want = ((j2k.image.DataBlkInt)original.GetInternCompData(block, c)).DataInt;
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < Width; x++)
                    {
                        var error = Math.Abs(got[y * Width + x] - (want[y * Width + x] + 128));
                        if (mask.Contains(x, y)) { inside += error; nInside++; } else { outside += error; nOutside++; }
                    }
            }

            Assert.True(inside / nInside < outside / nOutside, $"inside {inside / nInside:F2}, outside {outside / nOutside:F2}");
        }

        [Fact]
        public void MaskOfTheWrongSize_IsRejected()
        {
            var small = ROIMask.FromPredicate(Width - 1, Height, (x, y) => true);
            var ex = Assert.Throws<InvalidOperationException>(() => Encode(new ROIConfiguration().AddMask(-1, small)));
            Assert.Contains("same size", ex.Message);
        }

        [Fact]
        public void RroiMaskReference_WithoutTheMask_IsRejected()
        {
            var pl = new J2KEncoderConfiguration().WithBitrate(0.4f).WithFileFormat(false).ToParameterList();
            pl["Rroi"] = "M 0";
            var ex = Assert.Throws<InvalidOperationException>(() => J2kImage.ToBytes(MakeImage(), null, pl));
            Assert.Contains("RoiMasks", ex.Message);
        }
    }
}
