// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.image.input;
using Pfim;

namespace CoreJ2K.j2k.image.input
{
    /// <summary>
    /// Image reader for Pfim.IImage.
    /// Supports common 8/16-bit formats: Rgb24, Rgba32 and generic Pfim images.
    /// Channels are level shifted as required by JPEG2000 pipeline.
    /// </summary>
    public sealed class ImgReaderPfim : ImgReader
    {
        private readonly int dcOffset;

        private readonly IImage image;
        private readonly int componentCount;
        private readonly int bitsPerComponent;
        private readonly int bytesPerPixel;
        private readonly bool isBgrOrder;   // true if memory order is B,G,R,(A)
        private readonly bool hasAlpha;

        private int[][] barr;               // cached component buffers for current block
        private readonly DataBlkInt dbi = new DataBlkInt();
        private DataBlkInt intBlk;

        public ImgReaderPfim(IImage img)
        {
            image = img ?? throw new ArgumentNullException(nameof(img));
            w = img.Width;
            h = img.Height;

            // Prefer explicit known formats, otherwise fall back to bytes-per-pixel
            if (image.Format == ImageFormat.Rgb24)
            {
                componentCount = 3; bitsPerComponent = 8; bytesPerPixel = 3; isBgrOrder = false; hasAlpha = false;
            }
            else if (image.Format == ImageFormat.Rgba32)
            {
                componentCount = 4; bitsPerComponent = 8; bytesPerPixel = 4; isBgrOrder = false; hasAlpha = true;
            }
            else
            {
                // Generic fallback: derive components from bytes-per-pixel
                bytesPerPixel = Math.Max(1, image.BitsPerPixel);
                componentCount = Math.Max(1, bytesPerPixel);
                // Derive per-component bit depth from total bits per pixel when available
                var totalBits = image.BitsPerPixel > 0 ? image.BitsPerPixel : (bytesPerPixel * 8);
                bitsPerComponent = Math.Max(8, totalBits / componentCount);
                isBgrOrder = false;
                hasAlpha = componentCount > 3;
            }

            nc = componentCount;
            dcOffset = 1 << (bitsPerComponent - 1);
        }

        public override void Close()
        {
            (image as IDisposable)?.Dispose();
            barr = null;
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

                // De-interleave from Pfim raw buffer
                var src = image.Data;
                var stride = image.Stride;
                var startY = blk.uly;
                var endY = blk.uly + blk.h;
                var startX = blk.ulx;
                var endX = blk.ulx + blk.w;

                var rBuf = barr[0];
                var gBuf = nc > 1 ? barr[1] : null;
                var bBuf = nc > 2 ? barr[2] : null;
                var aBuf = nc > 3 ? barr[3] : null;

                int idx = 0;
                var sampleBytes = Math.Max(1, bitsPerComponent / 8);
                for (int y = startY; y < endY; y++)
                {
                    var row = y * stride;
                    var pixBase = row + startX * bytesPerPixel;

                    for (int x = startX; x < endX; x++)
                    {
                        var p = pixBase;
                        if (bitsPerComponent == 8)
                        {
                            if (componentCount == 1)
                            {
                                var v = src[p];
                                rBuf[idx] = v - dcOffset;
                            }
                            else
                            {
                                if (isBgrOrder)
                                {
                                    if (bBuf != null) bBuf[idx] = src[p + 0] - dcOffset;
                                    if (gBuf != null) gBuf[idx] = src[p + 1] - dcOffset;
                                    if (rBuf != null) rBuf[idx] = src[p + 2] - dcOffset;
                                    if (hasAlpha && aBuf != null) aBuf[idx] = src[p + 3] - dcOffset;
                                }
                                else
                                {
                                    if (rBuf != null) rBuf[idx] = src[p + 0] - dcOffset;
                                    if (gBuf != null) gBuf[idx] = src[p + 1] - dcOffset;
                                    if (bBuf != null) bBuf[idx] = src[p + 2] - dcOffset;
                                    if (hasAlpha && aBuf != null) aBuf[idx] = src[p + 3] - dcOffset;
                                }
                            }
                        }
                        else
                        {
                            // multi-byte sample (e.g., 16-bit)
                            if (componentCount == 1)
                            {
                                // read little-endian sampleBytes
                                int val = 0;
                                for (int sb = 0; sb < sampleBytes; ++sb)
                                {
                                    val |= src[p + sb] << (8 * sb);
                                }
                                rBuf[idx] = val - dcOffset;
                            }
                            else
                            {
                                int compOff = 0;
                                if (isBgrOrder)
                                {
                                    if (bBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        bBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                    if (gBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        gBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                    if (rBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        rBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                    if (hasAlpha && aBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        aBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                }
                                else
                                {
                                    if (rBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        rBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                    if (gBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        gBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                    if (bBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        bBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                    if (hasAlpha && aBuf != null)
                                    {
                                        int val = 0; for (int sb = 0; sb < sampleBytes; ++sb) val |= src[p + compOff + sb] << (8 * sb);
                                        aBuf[idx] = val - dcOffset; compOff += sampleBytes;
                                    }
                                }
                            }
                        }

                        idx++;
                        pixBase += bytesPerPixel;
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

        public override string ToString() => $"ImgReaderPfim: WxH={w}x{h}, Components={nc}, Format={image.Format}";
    }
}
