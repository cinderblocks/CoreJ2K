// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Buffers;

namespace CoreJ2K.j2k.image.input
{
    /// <summary>
    /// Image reader that exposes an Avalonia <see cref="WriteableBitmap"/> as a
    /// component-wise <see cref="BlkImgDataSrc"/> for the JPEG-2000 encoder.
    /// </summary>
    public sealed class ImgReaderAvalonia : ImgReader
    {
        private const int DC_OFFSET = 128;

        private int[][] barr;
        private readonly DataBlkInt dbi = new DataBlkInt();
        private DataBlkInt intBlk;

        private WriteableBitmap image;
        private ILockedFramebuffer framebuffer;
        private readonly int bytesPerPixel;
        private readonly bool isBgr; // true => memory order B,G,R(,A); false => R,G,B(,A)

        public ImgReaderAvalonia(WriteableBitmap bitmap)
        {
            if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
            image = bitmap;
            w = bitmap.PixelSize.Width;
            h = bitmap.PixelSize.Height;
            var fmt = bitmap.Format ?? PixelFormat.Bgra8888;
            if (fmt != PixelFormat.Bgra8888 && fmt != PixelFormat.Rgba8888)
            {
                throw new NotSupportedException(
                    $"Pixel format {fmt} is not supported by ImgReaderAvalonia. " +
                    "Convert the bitmap to Bgra8888 or Rgba8888 first.");
            }
            nc = GetNumberOfComponents(fmt, bitmap.AlphaFormat);
            bytesPerPixel = 4;
            isBgr = fmt == PixelFormat.Bgra8888;
            framebuffer = bitmap.Lock();
        }

        public override void Close()
        {
            if (barr != null)
            {
                for (var i = 0; i < barr.Length; i++)
                {
                    var a = barr[i];
                    if (a != null)
                    {
                        try { ArrayPool<int>.Shared.Return(a, clearArray: false); } catch { }
                        barr[i] = null;
                    }
                }
                barr = null;
            }

            framebuffer?.Dispose();
            framebuffer = null;
            image = null;
        }

        public override int GetNomRangeBits(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");
            return 8;
        }

        public override int GetFixedPoint(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");
            return 0;
        }

        public override bool IsOrigSigned(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");
            return false;
        }

        public override DataBlk GetInternCompData(DataBlk blk, int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");

            if (blk.DataType != DataBlk.TYPE_INT)
            {
                if (intBlk == null)
                {
                    intBlk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
                }
                else
                {
                    intBlk.ulx = blk.ulx; intBlk.uly = blk.uly;
                    intBlk.w = blk.w; intBlk.h = blk.h;
                }
                blk = intBlk;
            }

            if (barr == null || dbi.ulx > blk.ulx || dbi.uly > blk.uly
                || dbi.ulx + dbi.w < blk.ulx + blk.w || dbi.uly + dbi.h < blk.uly + blk.h)
            {
                if (barr == null || barr.Length != nc)
                {
                    if (barr != null)
                    {
                        for (var i = 0; i < barr.Length; i++)
                        {
                            var a = barr[i];
                            if (a != null)
                            {
                                try { ArrayPool<int>.Shared.Return(a, clearArray: false); } catch { }
                            }
                        }
                    }
                    barr = new int[nc][];
                }

                var needed = blk.w * blk.h;
                for (var cc = 0; cc < nc; ++cc)
                {
                    var cur = barr[cc];
                    if (cur == null || cur.Length < needed)
                    {
                        var newBuf = ArrayPool<int>.Shared.Rent(needed);
                        if (cur != null)
                        {
                            try { ArrayPool<int>.Shared.Return(cur, clearArray: false); } catch { }
                        }
                        barr[cc] = newBuf;
                    }
                }

                dbi.ulx = blk.ulx; dbi.uly = blk.uly;
                dbi.w = blk.w; dbi.h = blk.h; dbi.scanw = dbi.w;

                var red = barr[0];
                var green = nc > 1 ? barr[1] : null;
                var blue = nc > 2 ? barr[2] : null;
                var alpha = nc > 3 ? barr[3] : null;

                unsafe
                {
                    var basePtr = (byte*)framebuffer.Address.ToPointer();
                    var stride = framebuffer.RowBytes;
                    var idx = 0;
                    for (var y = blk.uly; y < blk.uly + blk.h; ++y)
                    {
                        var row = basePtr + y * stride + blk.ulx * bytesPerPixel;
                        if (nc == 1)
                        {
                            for (var x = 0; x < blk.w; ++x)
                            {
                                red[idx++] = row[0] - DC_OFFSET;
                                row += bytesPerPixel;
                            }
                        }
                        else if (nc == 3)
                        {
                            if (isBgr)
                            {
                                for (var x = 0; x < blk.w; ++x)
                                {
                                    red[idx] = row[2] - DC_OFFSET;
                                    green[idx] = row[1] - DC_OFFSET;
                                    blue[idx] = row[0] - DC_OFFSET;
                                    idx++;
                                    row += bytesPerPixel;
                                }
                            }
                            else
                            {
                                for (var x = 0; x < blk.w; ++x)
                                {
                                    red[idx] = row[0] - DC_OFFSET;
                                    green[idx] = row[1] - DC_OFFSET;
                                    blue[idx] = row[2] - DC_OFFSET;
                                    idx++;
                                    row += bytesPerPixel;
                                }
                            }
                        }
                        else // nc == 4
                        {
                            if (isBgr)
                            {
                                for (var x = 0; x < blk.w; ++x)
                                {
                                    red[idx] = row[2] - DC_OFFSET;
                                    green[idx] = row[1] - DC_OFFSET;
                                    blue[idx] = row[0] - DC_OFFSET;
                                    alpha[idx] = row[3] - DC_OFFSET;
                                    idx++;
                                    row += bytesPerPixel;
                                }
                            }
                            else
                            {
                                for (var x = 0; x < blk.w; ++x)
                                {
                                    red[idx] = row[0] - DC_OFFSET;
                                    green[idx] = row[1] - DC_OFFSET;
                                    blue[idx] = row[2] - DC_OFFSET;
                                    alpha[idx] = row[3] - DC_OFFSET;
                                    idx++;
                                    row += bytesPerPixel;
                                }
                            }
                        }
                    }
                }

                if (blk is DataBlkInt dbiBlk)
                {
                    dbiBlk.DataInt = barr[compIndex];
                    dbiBlk.offset = 0;
                    dbiBlk.scanw = dbiBlk.w;
                }
                else
                {
                    blk.Data = barr[compIndex];
                    blk.offset = 0;
                    blk.scanw = blk.w;
                }
            }
            else
            {
                if (blk is DataBlkInt dbiBlk)
                {
                    dbiBlk.DataInt = barr[compIndex];
                    dbiBlk.offset = (blk.ulx - dbi.ulx) + (blk.uly - dbi.uly) * dbi.scanw;
                    dbiBlk.scanw = dbi.scanw;
                }
                else
                {
                    blk.Data = barr[compIndex];
                    blk.offset = (blk.ulx - dbi.ulx) + (blk.uly - dbi.uly) * dbi.scanw;
                    blk.scanw = dbi.scanw;
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
            var width = blk.w;
            var height = blk.h;
            blk.Data = null;
            GetInternCompData(blk, c);
            if (bak == null) bak = new int[width * height];

            if (blk.offset == 0 && blk.scanw == width)
            {
                Array.Copy((Array)blk.Data, 0, bak, 0, width * height);
            }
            else
            {
                for (var i = height - 1; i >= 0; i--)
                {
                    Array.Copy((Array)blk.Data, blk.offset + i * blk.scanw, bak, i * width, width);
                }
            }
            blk.Data = bak;
            blk.offset = 0;
            blk.scanw = blk.w;
            return blk;
        }

        public static int GetNumberOfComponents(PixelFormat fmt, AlphaFormat? alphaFormat)
        {
            if (fmt == PixelFormat.Bgra8888 || fmt == PixelFormat.Rgba8888)
            {
                return alphaFormat.HasValue && alphaFormat.Value != AlphaFormat.Opaque ? 4 : 3;
            }
            throw new NotSupportedException($"Pixel format {fmt} is not supported.");
        }

        public override string ToString() => $"ImgReaderAvalonia: WxH={w}x{h}, Components={nc}";
    }
}
