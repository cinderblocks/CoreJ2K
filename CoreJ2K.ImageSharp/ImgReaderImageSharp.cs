// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Buffers;
using CoreJ2K.j2k.image.input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;

namespace CoreJ2K.j2k.image.input
{
    /// <summary>
    /// Image reader for SixLabors.ImageSharp images.
    /// Provides pixel component data as level-shifted integers for JPEG2000 pipeline.
    /// Supports common 8/16-bit pixel formats: L8, L16, Rgb24, Bgr24, Bgra32, Argb32, Rgba32, Rgb48, Rgba64.
    /// </summary>
    public sealed class ImgReaderImageSharp : ImgReader
    {
        private const int DC_OFFSET = 128;

        private int[][] barr;               // cached component buffers for current block
        private readonly DataBlkInt dbi = new DataBlkInt(); // tracks cached block rectangle
        private DataBlkInt intBlk;          // reusable int block wrapper

        private Image image;                // reference to base Image
        private object pixelSpanAccessor;   // typed pixel accessor cached for performance
        private int bytesPerPixel;
        private int bitsPerComponent;
        private int componentCount;
        private Func<int,int,int,int,int,int[]> blockLoader; // loader delegate (x,y,w,h,comps) -> interleaved comps

        public ImgReaderImageSharp(Image img)
        {
            if (img is null) throw new ArgumentNullException(nameof(img));
            image = img;
            w = img.Width;
            h = img.Height;
            InitializeFormat(img);
            nc = componentCount;
        }

        private void InitializeFormat(Image img)
        {
            // Attempt to bind to known pixel formats via reflection (avoid generic constraints spilling outside this assembly)
            var pfType = img.GetType();
            if (pfType.IsGenericType && pfType.GetGenericTypeDefinition() == typeof(Image<>))
            {
                var arg = pfType.GetGenericArguments()[0];
                if (arg == typeof(L8))
                {
                    componentCount = 1; bitsPerComponent = 8; bytesPerPixel = 1;
                    blockLoader = (x,y,w,h,comps) => LoadBlockL8(x,y,w,h,comps);
                }
                else if (arg == typeof(L16))
                {
                    componentCount = 1; bitsPerComponent = 16; bytesPerPixel = 2;
                    blockLoader = (x,y,w,h,comps) => LoadBlockL16(x,y,w,h,comps);
                }
                else if (arg == typeof(Rgb24))
                {
                    componentCount = 3; bitsPerComponent = 8; bytesPerPixel = 3;
                    blockLoader = (x,y,w,h,comps) => LoadBlockRgb24(x,y,w,h,comps);
                }
                else if (arg == typeof(Bgr24))
                {
                    componentCount = 3; bitsPerComponent = 8; bytesPerPixel = 3;
                    blockLoader = (x,y,w,h,comps) => LoadBlockBgr24(x,y,w,h,comps);
                }
                else if (arg == typeof(Rgba32))
                {
                    componentCount = 4; bitsPerComponent = 8; bytesPerPixel = 4;
                    blockLoader = (x,y,w,h,comps) => LoadBlockRgba32(x,y,w,h,comps);
                }
                else if (arg == typeof(Bgra32))
                {
                    componentCount = 4; bitsPerComponent = 8; bytesPerPixel = 4;
                    blockLoader = (x,y,w,h,comps) => LoadBlockBgra32(x,y,w,h,comps);
                }
                else if (arg == typeof(Argb32))
                {
                    componentCount = 4; bitsPerComponent = 8; bytesPerPixel = 4;
                    blockLoader = (x,y,w,h,comps) => LoadBlockArgb32(x,y,w,h,comps);
                }
                else if (arg == typeof(Rgb48))
                {
                    componentCount = 3; bitsPerComponent = 16; bytesPerPixel = 6;
                    blockLoader = (x,y,w,h,comps) => LoadBlockRgb48(x,y,w,h,comps);
                }
                else if (arg == typeof(Rgba64))
                {
                    componentCount = 4; bitsPerComponent = 16; bytesPerPixel = 8;
                    blockLoader = (x,y,w,h,comps) => LoadBlockRgba64(x,y,w,h,comps);
                }
                else
                {
                    // Fallback to dynamic clone-based loader
                    componentCount = 4;
                    bitsPerComponent = 8;
                    bytesPerPixel = 4;
                    blockLoader = (x,y,w,h,comps) => LoadBlockDynamic(x,y,w,h,comps);
                }
            }
            else
            {
                // Fallback: use dynamic loader (clone to Rgba32 at read time)
                componentCount = 4;
                bitsPerComponent = 8;
                bytesPerPixel = 4;
                blockLoader = LoadBlockDynamic;
            }
        }

        public override void Close()
        {
            image?.Dispose();
            image = null;
            barr = null;
            pixelSpanAccessor = null;
        }

        public override int getNomRangeBits(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));
            return bitsPerComponent;
        }

        public override int GetFixedPoint(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));
            return 0;
        }

        public override bool IsOrigSigned(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));
            return false;
        }

        public override DataBlk GetInternCompData(DataBlk blk, int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));

            if (blk.DataType != DataBlk.TYPE_INT)
            {
                if (intBlk == null) intBlk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
                else { intBlk.ulx = blk.ulx; intBlk.uly = blk.uly; intBlk.w = blk.w; intBlk.h = blk.h; }
                blk = intBlk;
            }

            // need new load?
            if ((barr == null) || (dbi.ulx > blk.ulx) || (dbi.uly > blk.uly) || (dbi.ulx + dbi.w < blk.ulx + blk.w) || (dbi.uly + dbi.h < blk.uly + blk.h))
            {
                if (barr == null || barr.Length != nc) barr = new int[nc][];
                var needed = blk.w * blk.h;
                for (var c = 0; c < nc; ++c)
                {
                    if (barr[c] == null || barr[c].Length < needed) barr[c] = new int[needed];
                }

                dbi.ulx = blk.ulx; dbi.uly = blk.uly; dbi.w = blk.w; dbi.h = blk.h; dbi.scanw = blk.w;

                // load block as interleaved then de-interleave into barr[]
                var interleaved = blockLoader(blk.ulx, blk.uly, blk.w, blk.h, nc);
                if (interleaved == null || interleaved.Length < needed * nc) throw new InvalidOperationException("Interleaved loader size mismatch.");

                try
                {
                    for (var c = 0; c < nc; ++c)
                    {
                        var dest = barr[c];
                        var compOffset = c;
                        for (int i = 0, p = 0; i < needed; ++i, p+=nc)
                        {
                            dest[i] = interleaved[p + compOffset] - DC_OFFSET;
                        }
                    }
                }
                finally
                {
                    // If the loader rented the array from the pool, return it
                    if (interleaved.Length >= 0)
                    {
                        // We only return arrays that came from the pool by convention.
                        // Our loader implementations rent from ArrayPool; safe to return.
                        ArrayPool<int>.Shared.Return(interleaved);
                    }
                }

                if (blk is DataBlkInt dbiBlk)
                {
                    dbiBlk.DataInt = barr[compIndex]; dbiBlk.offset = 0; dbiBlk.scanw = dbiBlk.w;
                }
                else
                {
                    blk.Data = barr[compIndex]; blk.offset = 0; blk.scanw = blk.w;
                }
            }
            else
            {
                if (blk is DataBlkInt dbiBlk)
                {
                    dbiBlk.DataInt = barr[compIndex]; dbiBlk.offset = (blk.ulx - dbi.ulx) + (blk.uly - dbi.uly) * dbi.scanw; dbiBlk.scanw = dbi.scanw;
                }
                else
                {
                    blk.Data = barr[compIndex]; blk.offset = (blk.ulx - dbi.ulx) + (blk.uly - dbi.uly) * dbi.scanw; blk.scanw = dbi.scanw;
                }
            }
            blk.progressive = false;
            return blk;
        }

        public override DataBlk GetCompData(DataBlk blk, int c)
        {
            if (blk.DataType != DataBlk.TYPE_INT)
            {
                blk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
            }
            var bak = (int[])blk.Data;
            var width = blk.w; var height = blk.h;
            blk.Data = null;
            GetInternCompData(blk, c);
            if (bak == null) bak = new int[width * height];
            if (blk.offset == 0 && blk.scanw == width)
            {
                Array.Copy((int[])blk.Data, 0, bak, 0, width * height);
            }
            else
            {
                for (var row = 0; row < height; ++row)
                {
                    Array.Copy((int[])blk.Data, blk.offset + row * blk.scanw, bak, row * width, width);
                }
            }
            blk.Data = bak; blk.offset = 0; blk.scanw = blk.w; return blk;
        }

        // Loader implementations
        private int[] LoadBlockL8(int x, int y, int width, int height, int comps)
        {
            var img = (Image<L8>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    ret[idx++] = img[col,row].PackedValue;
                }
            }
            return ret;
        }
        private int[] LoadBlockL16(int x, int y, int width, int height, int comps)
        {
            var img = (Image<L16>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    ret[idx++] = img[col,row].PackedValue >> 8; // reduce to 8-bit nominal range
                }
            }
            return ret;
        }
        private int[] LoadBlockRgb24(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Rgb24>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    ret[idx++] = px.R;
                    ret[idx++] = px.G;
                    ret[idx++] = px.B;
                }
            }
            return ret;
        }
        private int[] LoadBlockBgr24(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Bgr24>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    // Pixel struct exposes R,G,B; map to RGB order
                    ret[idx++] = px.R;
                    ret[idx++] = px.G;
                    ret[idx++] = px.B;
                }
            }
            return ret;
        }
        private int[] LoadBlockRgba32(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Rgba32>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    ret[idx++] = px.R;
                    ret[idx++] = px.G;
                    ret[idx++] = px.B;
                    ret[idx++] = px.A;
                }
            }
            return ret;
        }
        private int[] LoadBlockBgra32(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Bgra32>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    ret[idx++] = px.R; // reorder to RGB(A)
                    ret[idx++] = px.G;
                    ret[idx++] = px.B;
                    ret[idx++] = px.A;
                }
            }
            return ret;
        }
        private int[] LoadBlockArgb32(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Argb32>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    // Argb32 provides A,R,G,B; convert to R,G,B,A order
                    ret[idx++] = px.R;
                    ret[idx++] = px.G;
                    ret[idx++] = px.B;
                    ret[idx++] = px.A;
                }
            }
            return ret;
        }
        private int[] LoadBlockRgb48(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Rgb48>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    // 16-bit channels -> reduce to 8-bit nominal by shifting
                    ret[idx++] = px.R >> 8;
                    ret[idx++] = px.G >> 8;
                    ret[idx++] = px.B >> 8;
                }
            }
            return ret;
        }
        private int[] LoadBlockRgba64(int x, int y, int width, int height, int comps)
        {
            var img = (Image<Rgba64>)image;
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = img[col,row];
                    ret[idx++] = px.R >> 8;
                    ret[idx++] = px.G >> 8;
                    ret[idx++] = px.B >> 8;
                    ret[idx++] = px.A >> 8;
                }
            }
            return ret;
        }
        private int[] LoadBlockDynamic(int x,int y,int width,int height,int comps)
        {
            // Fallback dynamic loader through cloning to Rgba32
            using var clone = image.CloneAs<Rgba32>();
            var needed = width * height * comps;
            var pool = ArrayPool<int>.Shared;
            var ret = pool.Rent(needed);
            var idx = 0;
            for (var row=y; row< y+height; ++row)
            {
                for (var col=x; col< x+width; ++col)
                {
                    var px = clone[col,row];
                    ret[idx++] = px.R;
                    if (comps>1) ret[idx++] = px.G;
                    if (comps>2) ret[idx++] = px.B;
                    if (comps>3) ret[idx++] = px.A;
                }
            }
            return ret;
        }

        public override string ToString() => $"ImgReaderImageSharp: WxH={w}x{h}, Components={nc}";
    }
}
