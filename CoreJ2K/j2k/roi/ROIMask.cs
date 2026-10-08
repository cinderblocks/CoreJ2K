// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Drawing;

namespace CoreJ2K.j2k.roi
{
    /// <summary>
    /// A region of interest of any shape, held in memory as one bit per pixel of the image. Add it to an
    /// <see cref="ROIConfiguration"/> with <see cref="ROIConfiguration.AddMask"/>.
    /// </summary>
    /// <remarks>
    /// The mask is as large as the image it is used with. Pixel centres decide membership: a pixel belongs to a polygon or
    /// ellipse when the point half a pixel right of and below its corner lies inside. A mask never changes after it is made.
    /// </remarks>
    public sealed class ROIMask
    {
        private readonly byte[] _bits;

        /// <summary>Gets the width of the mask, in pixels.</summary>
        public int Width { get; }

        /// <summary>Gets the height of the mask, in pixels.</summary>
        public int Height { get; }

        /// <summary>Gets the number of pixels in the region.</summary>
        public long PixelCount { get; }

        private ROIMask(int width, int height, byte[] bits)
        {
            Width = width;
            Height = height;
            _bits = bits;
            long count = 0;
            foreach (var b in bits)
            {
                var v = b;
                while (v != 0) { v &= (byte)(v - 1); count++; }
            }
            PixelCount = count;
        }

        /// <summary>Returns whether the pixel at (<paramref name="x"/>, <paramref name="y"/>) is in the region.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The pixel is outside the mask.</exception>
        public bool Contains(int x, int y)
        {
            if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
            if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
            return IsSet(x, y);
        }

        internal bool IsSet(int x, int y)
        {
            var index = (long)y * Width + x;
            return (_bits[index >> 3] & (1 << (int)(index & 7))) != 0;
        }

        /// <summary>
        /// Makes a mask from one byte per pixel, row by row, from the top left. Any non-zero byte puts the pixel in the region.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">Exactly <paramref name="width"/> × <paramref name="height"/> bytes.</param>
        public static ROIMask FromBytes(int width, int height, ReadOnlySpan<byte> pixels)
        {
            var builder = new Builder(width, height);
            if (pixels.Length != builder.PixelTotal)
                throw new ArgumentException("The mask must contain exactly width times height bytes.", nameof(pixels));
            for (long i = 0; i < pixels.Length; i++)
            {
                if (pixels[(int)i] != 0) builder.Set(i);
            }
            return builder.Build();
        }

        /// <summary>Makes a mask from a test applied to every pixel.</summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="contains">Returns true for the pixels (x, y) in the region.</param>
        public static ROIMask FromPredicate(int width, int height, Func<int, int, bool> contains)
        {
            if (contains == null) throw new ArgumentNullException(nameof(contains));
            var builder = new Builder(width, height);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    if (contains(x, y)) builder.Set((long)y * width + x);
            return builder.Build();
        }

        /// <summary>Makes an elliptical mask with its axes along the image axes.</summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="centerX">Where the centre is, in pixels from the left edge.</param>
        /// <param name="centerY">Where the centre is, in pixels from the top edge.</param>
        /// <param name="radiusX">Half the width of the ellipse; positive.</param>
        /// <param name="radiusY">Half the height of the ellipse; positive.</param>
        public static ROIMask FromEllipse(int width, int height, double centerX, double centerY, double radiusX, double radiusY)
        {
            RequireFinite(centerX, nameof(centerX));
            RequireFinite(centerY, nameof(centerY));
            if (!(radiusX > 0) || double.IsInfinity(radiusX)) throw new ArgumentOutOfRangeException(nameof(radiusX), "The radius must be positive and finite.");
            if (!(radiusY > 0) || double.IsInfinity(radiusY)) throw new ArgumentOutOfRangeException(nameof(radiusY), "The radius must be positive and finite.");

            return FromPredicate(width, height, (x, y) =>
            {
                var nx = (x + 0.5 - centerX) / radiusX;
                var ny = (y + 0.5 - centerY) / radiusY;
                return nx * nx + ny * ny <= 1;
            });
        }

        /// <summary>
        /// Makes a mask from a polygon. The outline closes itself; where it crosses itself, the even-odd rule decides what is inside.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="vertices">At least three corners, in pixels from the top left of the image. Parts outside the image are cut off.</param>
        public static ROIMask FromPolygon(int width, int height, IReadOnlyList<PointF> vertices)
            => FromPolygon(width, height, ToTuples(vertices, nameof(vertices)));

        /// <inheritdoc cref="FromPolygon(int,int,IReadOnlyList{PointF})"/>
        /// <remarks>Takes plain coordinates, which suits callers whose point type is not <see cref="PointF"/>, such as ImageSharp's.</remarks>
        public static ROIMask FromPolygon(int width, int height, IReadOnlyList<(double X, double Y)> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (vertices.Count < 3) throw new ArgumentException("A polygon needs at least three vertices.", nameof(vertices));
            foreach (var v in vertices)
            {
                RequireFinite(v.X, nameof(vertices));
                RequireFinite(v.Y, nameof(vertices));
            }

            var builder = new Builder(width, height);
            var crossings = new List<double>();
            for (var y = 0; y < height; y++)
            {
                var yc = y + 0.5;
                crossings.Clear();
                for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
                {
                    double yi = vertices[i].Y, yj = vertices[j].Y;
                    if ((yi <= yc) == (yj <= yc)) continue;
                    double xi = vertices[i].X, xj = vertices[j].X;
                    crossings.Add(xi + (yc - yi) * (xj - xi) / (yj - yi));
                }
                crossings.Sort();
                for (var k = 0; k + 1 < crossings.Count; k += 2)
                {
                    // The pixels whose centres lie in [crossings[k], crossings[k + 1]).
                    var first = Math.Min(width, Math.Max(0.0, Math.Ceiling(crossings[k] - 0.5)));
                    var last = Math.Min(width - 1.0, Math.Ceiling(crossings[k + 1] - 0.5) - 1);
                    for (var x = (int)first; x <= last; x++) builder.Set((long)y * width + x);
                }
            }
            return builder.Build();
        }

        /// <summary>
        /// Makes a mask from the convex hull of a set of points, such as facial landmarks. Points inside the hull do not change it.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="points">Points in pixels from the top left of the image; they must not all lie on one line.</param>
        public static ROIMask FromConvexHull(int width, int height, IReadOnlyList<PointF> points)
            => FromConvexHull(width, height, ToTuples(points, nameof(points)));

        /// <inheritdoc cref="FromConvexHull(int,int,IReadOnlyList{PointF})"/>
        /// <remarks>Takes plain coordinates, which suits callers whose point type is not <see cref="PointF"/>, such as ImageSharp's.</remarks>
        public static ROIMask FromConvexHull(int width, int height, IReadOnlyList<(double X, double Y)> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var hull = ConvexHull(points);
            if (hull.Count < 3) throw new ArgumentException("The points do not enclose an area.", nameof(points));
            return FromPolygon(width, height, hull);
        }

        private static List<(double X, double Y)> ToTuples(IReadOnlyList<PointF> points, string name)
        {
            if (points == null) throw new ArgumentNullException(name);
            var list = new List<(double X, double Y)>(points.Count);
            foreach (var p in points) list.Add((p.X, p.Y));
            return list;
        }

        /// <summary>The corners of the convex hull of <paramref name="points"/>, in order (Andrew's monotone chain).</summary>
        internal static List<(double X, double Y)> ConvexHull(IReadOnlyList<(double X, double Y)> points)
        {
            var sorted = new List<(double X, double Y)>(points.Count);
            foreach (var p in points)
            {
                RequireFinite(p.X, nameof(points));
                RequireFinite(p.Y, nameof(points));
                sorted.Add(p);
            }
            sorted.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            if (sorted.Count < 3) return sorted;

            double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
                (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

            var hull = new List<(double X, double Y)>(2 * sorted.Count);
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            var lower = hull.Count + 1;
            for (var i = sorted.Count - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("Coordinates must be finite.", name);
        }

        private sealed class Builder
        {
            private readonly int _width;
            private readonly int _height;
            private readonly byte[] _bits;

            public long PixelTotal { get; }

            public Builder(int width, int height)
            {
                if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
                if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
                PixelTotal = (long)width * height;
                if ((PixelTotal + 7) / 8 > int.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(width), "The mask is too large.");
                _width = width;
                _height = height;
                _bits = new byte[(PixelTotal + 7) / 8];
            }

            public void Set(long index) => _bits[index >> 3] |= (byte)(1 << (int)(index & 7));

            public ROIMask Build() => new ROIMask(_width, _height, _bits);
        }
    }
}
