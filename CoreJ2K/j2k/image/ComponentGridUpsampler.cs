// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;

namespace CoreJ2K.j2k.image
{
    /// <summary>
    /// Puts the components of an image on one sampling grid. The components of a JPEG 2000 image may be subsampled (each by its own
    /// factors, XRsiz and YRsiz), and so differ in size; a picture needs them all the same size. The grid chosen is the finest of
    /// the components: one that has the finest sampling in a direction keeps its samples, and the others are repeated to fill it.
    /// When every component has the same factors, nothing is repeated and the image is as large as the components are.
    /// </summary>
    /// <remarks>
    /// The sample at position <c>X</c> of the grid lies at <c>X * gridX</c> on the reference grid. A component with factor <c>f</c> has a
    /// sample at every multiple of <c>f</c> there, and the one at or before <c>X * gridX</c> is used, or the first of the tile when
    /// that falls before the tile begins. Everything is worked out from the geometry that the source reports for its components, so it
    /// holds for tiles and image origins that are not multiples of the factors, and for decodes at a reduced resolution.
    /// </remarks>
    public sealed class ComponentGridUpsampler : ImgDataAdapter, BlkImgDataSrc
    {
        private readonly BlkImgDataSrc _src;
        private readonly int[] _subsX;
        private readonly int[] _subsY;
        private readonly int _gridX;
        private readonly int _gridY;
        private readonly int _masterX; // a component sampled at gridX
        private readonly int _masterY;
        private readonly DataBlkInt?[] _intRequests;
        private readonly DataBlkFloat?[] _floatRequests;

        private ComponentGridUpsampler(BlkImgDataSrc src) : base(src)
        {
            _src = src;
            var n = src.NumComps;
            _subsX = new int[n];
            _subsY = new int[n];
            for (var c = 0; c < n; c++)
            {
                _subsX[c] = src.GetCompSubsX(c);
                _subsY[c] = src.GetCompSubsY(c);
                if (c == 0 || _subsX[c] < _subsX[_masterX]) _masterX = c;
                if (c == 0 || _subsY[c] < _subsY[_masterY]) _masterY = c;
            }
            _gridX = _subsX[_masterX];
            _gridY = _subsY[_masterY];
            _intRequests = new DataBlkInt?[n];
            _floatRequests = new DataBlkFloat?[n];
        }

        /// <summary>
        /// The source as it is when all its components are sampled alike at every position of the reference grid; otherwise the source
        /// with its components put on a common grid.
        /// </summary>
        public static BlkImgDataSrc IfNeeded(BlkImgDataSrc src)
        {
            for (var c = 0; c < src.NumComps; c++)
                if (src.GetCompSubsX(c) != 1 || src.GetCompSubsY(c) != 1)
                    return new ComponentGridUpsampler(src);
            return src;
        }

        private static int CeilDiv(int a, int b) => (int)(((long)a + b - 1) / b);

        // ---- geometry: the common grid is the new reference grid, so every component has factors of 1 on it ----

        public override int GetCompSubsX(int c) => 1;

        public override int GetCompSubsY(int c) => 1;

        public override int ImgULX => CeilDiv(_src.ImgULX, _gridX);

        public override int ImgULY => CeilDiv(_src.ImgULY, _gridY);

        public override int ImgWidth => CeilDiv(_src.ImgULX + _src.ImgWidth, _gridX) - CeilDiv(_src.ImgULX, _gridX);

        public override int ImgHeight => CeilDiv(_src.ImgULY + _src.ImgHeight, _gridY) - CeilDiv(_src.ImgULY, _gridY);

        public override int TileWidth => _src.GetTileCompWidth(_src.TileIdx, _masterX);

        public override int TileHeight => _src.GetTileCompHeight(_src.TileIdx, _masterY);

        public override int GetCompImgWidth(int c) => ImgWidth;

        public override int GetCompImgHeight(int c) => ImgHeight;

        public override int GetTileCompWidth(int t, int c) => _src.GetTileCompWidth(t, _masterX);

        public override int GetTileCompHeight(int t, int c) => _src.GetTileCompHeight(t, _masterY);

        public override int GetCompULX(int c) => _src.GetCompULX(_masterX);

        public override int GetCompULY(int c) => _src.GetCompULY(_masterY);

        public int GetFixedPoint(int compIndex) => _src.GetFixedPoint(compIndex);

        public bool IsOrigSigned(int compIndex) => _src.IsOrigSigned(compIndex);

        public void Close() => _src.Close();

        // ---- data ----

        public DataBlk GetCompData(DataBlk blk, int c) => GetInternCompData(blk, c);

        public DataBlk GetInternCompData(DataBlk blk, int c)
        {
            if (_subsX[c] == _gridX && _subsY[c] == _gridY)
                return _src.GetInternCompData(blk, c);

            var t = _src.TileIdx;
            var width = blk.w;
            var height = blk.h;

            // where the tile begins on the grid, and where the component's own samples of the tile begin and end
            var gridLeft = _src.GetCompULX(_masterX) + blk.ulx;
            var gridTop = _src.GetCompULY(_masterY) + blk.uly;
            var left = _src.GetCompULX(c);
            var top = _src.GetCompULY(c);
            var compWidth = _src.GetTileCompWidth(t, c);
            var compHeight = _src.GetTileCompHeight(t, c);

            var xmap = new int[width];
            for (var x = 0; x < width; x++)
                xmap[x] = SampleOf((long)(gridLeft + x) * _gridX, _subsX[c], left, compWidth);
            var ymap = new int[height];
            for (var y = 0; y < height; y++)
                ymap[y] = SampleOf((long)(gridTop + y) * _gridY, _subsY[c], top, compHeight);

            // the part of the component the block draws on (the maps rise, so it is a rectangle between their ends)
            var x0 = xmap[0];
            var x1 = xmap[width - 1];
            var y0 = ymap[0];
            var y1 = ymap[height - 1];

            var isFloat = blk.DataType == DataBlk.TYPE_FLOAT;
            DataBlk request;
            if (isFloat) request = _floatRequests[c] ??= new DataBlkFloat();
            else request = _intRequests[c] ??= new DataBlkInt();
            request.ulx = x0;
            request.uly = y0;
            request.w = x1 - x0 + 1;
            request.h = y1 - y0 + 1;
            var got = _src.GetInternCompData(request, c);

            var length = checked(width * height);
            if (isFloat)
            {
                var output = blk.Data as float[];
                if (output == null || output.Length < length) { output = new float[length]; blk.Data = output; }
                if (got.Data is float[] samples)
                {
                    for (var y = 0; y < height; y++)
                    {
                        var row = got.offset + (ymap[y] - y0) * got.scanw - x0;
                        var o = y * width;
                        for (var x = 0; x < width; x++) output[o + x] = samples[row + xmap[x]];
                    }
                }
                else
                {
                    var ints = (int[])got.Data!;
                    for (var y = 0; y < height; y++)
                    {
                        var row = got.offset + (ymap[y] - y0) * got.scanw - x0;
                        var o = y * width;
                        for (var x = 0; x < width; x++) output[o + x] = ints[row + xmap[x]];
                    }
                }
            }
            else
            {
                var output = blk.Data as int[];
                if (output == null || output.Length < length) { output = new int[length]; blk.Data = output; }
                if (got.Data is int[] samples)
                {
                    for (var y = 0; y < height; y++)
                    {
                        var row = got.offset + (ymap[y] - y0) * got.scanw - x0;
                        var o = y * width;
                        for (var x = 0; x < width; x++) output[o + x] = samples[row + xmap[x]];
                    }
                }
                else
                {
                    var floats = (float[])got.Data!;
                    for (var y = 0; y < height; y++)
                    {
                        var row = got.offset + (ymap[y] - y0) * got.scanw - x0;
                        var o = y * width;
                        for (var x = 0; x < width; x++) output[o + x] = (int)Math.Round(floats[row + xmap[x]]);
                    }
                }
            }

            blk.offset = 0;
            blk.scanw = width;
            blk.progressive = got.progressive;
            return blk;
        }

        /// <summary>
        /// The index, from the first sample of the component in the tile, of the sample that stands for the point <paramref name="position"/>
        /// of the reference grid: the one at or before it, or the first when none is.
        /// </summary>
        private static int SampleOf(long position, int factor, int first, int count)
        {
            var absolute = position / factor;
            var index = absolute - first;
            if (index < 0) return 0;
            return index >= count ? count - 1 : (int)index;
        }
    }
}
