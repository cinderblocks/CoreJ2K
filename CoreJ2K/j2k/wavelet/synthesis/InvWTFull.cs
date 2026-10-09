/* 
* 
*                          the InvWTFullInt and InvWTFullFloat
*                          classes by Bertrand Berthelot, Apr-19-1999
* 
* 
* COPYRIGHT:
* 
* This software module was originally developed by Rapha�l Grosbois and
* Diego Santa Cruz (Swiss Federal Institute of Technology-EPFL); Joel
* Askel�f (Ericsson Radio Systems AB); and Bertrand Berthelot, David
* Bouchard, F�lix Henry, Gerard Mozelle and Patrice Onno (Canon Research
* Centre France S.A) in the course of development of the JPEG2000
* standard as specified by ISO/IEC 15444 (JPEG 2000 Standard). This
* software module is an implementation of a part of the JPEG 2000
* Standard. Swiss Federal Institute of Technology-EPFL, Ericsson Radio
* Systems AB and Canon Research Centre France S.A (collectively JJ2000
* Partners) agree not to assert against ISO/IEC and users of the JPEG
* 2000 Standard (Users) any of their rights under the copyright, not
* including other intellectual property rights, for this software module
* with respect to the usage by ISO/IEC and Users of this software module
* or modifications thereof for use in hardware or software products
* claiming conformance to the JPEG 2000 Standard. Those intending to use
* this software module in hardware or software products are advised that
* their use may infringe existing patents. The original developers of
* this software module, JJ2000 Partners and ISO/IEC assume no liability
* for use of this software module or modifications thereof. No license
* or right to this software module is granted for non JPEG 2000 Standard
* conforming products. JJ2000 Partners have full right to use this
* software module for his/her own purpose, assign or donate this
* software module to any third party and to inhibit third parties from
* using this software module for non JPEG 2000 Standard conforming
* products. This copyright notice must be included in all copies or
* derivative works of this software module.
* 
* Copyright (c) 1999/2000 JJ2000 Partners.
* */
using CoreJ2K.j2k.decoder;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.wavelet;
using System;
using System.Collections.Generic;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreJ2K.j2k.wavelet.synthesis
{

    /// <summary> This class implements the InverseWT with the full-page approach for int and
    /// float data.
    /// 
    /// The image can be reconstructed at different (image) resolution levels
    /// indexed from the lowest resolution available for each tile-component. This
    /// is controlled by the setImgResLevel() method.
    /// 
    /// Note: Image resolution level indexes may differ from tile-component
    /// resolution index. They are indeed indexed starting from the lowest number
    /// of decomposition levels of each component of each tile.
    /// 
    /// Example: For an image (1 tile) with 2 components (component 0 having 2
    /// decomposition levels and component 1 having 3 decomposition levels), the
    /// first (tile-) component has 3 resolution levels and the second one has 4
    /// resolution levels, whereas the image has only 3 resolution levels
    /// available.
    /// 
    /// This implementation does not support progressive data: Data is
    /// considered to be non-progressive (i.e. "final" data) and the 'progressive'
    /// attribute of the 'DataBlk' class is always set to false, see the 'DataBlk'
    /// class.
    /// 
    /// </summary>
    /// <seealso cref="DataBlk" />
    public class InvWTFull : InverseWT
    {

        /// <summary>The total number of code-blocks to decode </summary>
        private int cblkToDecode = 0;

        /// <summary>the code-block buffer's source i.e. the quantizer </summary>
        private readonly CBlkWTDataSrcDec src;

        /// <summary>Current data type </summary>
        private int dtype;

        /// <summary>Block storing the reconstructed image for each component </summary>
        private readonly DataBlk[] reconstructedComps;

        /// <summary>Number of decomposition levels in each component </summary>
        private readonly int[] ndl;

        // Rented buffers for reconstructed components to avoid LOH allocations
        private float[]?[] rentedFloatBuffers;
        private int[]?[] rentedIntBuffers;

        // Cached DataBlk wrappers for waveletTreeReconstruction leaf nodes – avoids per-subband allocation
        private DataBlkInt? _subbDataInt;
        private DataBlkFloat? _subbDataFloat;

        // Grow-only scratch line buffers for wavelet2DReconstruction.
        // Sized to max(w,h) of the largest subband seen so far; never returned to a pool,
        // so every subband reconstruction reuses the same allocation with zero rent/return overhead.
        private int[]?   _waveletScratchInt;
        private float[]? _waveletScratchFloat;

        /// <summary>
        /// Configures whether ArrayPool buffers should be cleared when returned.
        /// Setting this to true improves security by preventing sensitive image data 
        /// from remaining in memory, but has a small performance cost.
        /// 
        /// Default: false (prioritizes performance)
        /// Recommended: true for sensitive images (medical, personal photos, etc.)
        /// </summary>
        public static bool ClearArrayPoolBuffersOnReturn { get; set; } = false;

        // ---- Parallel code-block decoding -------------------------------------------------------------------
        // The code-block stage (bitstream parse, entropy decode, dequantisation) dominates decode time and is
        // independent per code-block, so the blocks of a tile-component are decoded together by a team of workers,
        // each with its own entropy-decoder/ROI/dequantiser chain, and written straight into their own rectangle of
        // the tile-component buffer. The wavelet recursion then runs unchanged on the filled buffer.

        /// <summary>Tile-components with fewer code-blocks than this are decoded sequentially.</summary>
        /// <remarks>Parallel set-up is wasted on thumbnails. Tests lower this to 0 to exercise the parallel path on small images.</remarks>
        internal static int MinParallelBlocks = 16;

        /// <summary>Overrides <see cref="MinParallelBlocks"/> for decodes started on the current thread (tests only).</summary>
        [ThreadStatic]
        internal static int? MinParallelBlocksForCurrentThread;

        private sealed class BlockWorker
        {
            public CBlkWTDataSrcDec Chain = null!;
            public DataBlk? Block;
            public int Tile = -1;
        }

        private struct BlockJob
        {
            public SubbandSyn Subband;
            public int M, N;
        }

        private int parallelDegree = 1;

        // Observed between code-blocks and between wavelet levels so a cancelled decode stops within about one code-block.
        private CancellationToken cancellationToken;

        /// <summary>
        /// Makes the reconstruction cooperative: once <paramref name="token"/> is cancelled, it throws
        /// <see cref="OperationCanceledException"/> before decoding the next code-block or wavelet level.
        /// </summary>
        internal void SetCancellationToken(CancellationToken token) => cancellationToken = token;
        private Func<CBlkWTDataSrcDec>? workerChainFactory;
        private readonly ConcurrentBag<BlockWorker> idleWorkers = new ConcurrentBag<BlockWorker>();

        /// <summary>
        /// Allows this transform to decode the code-blocks of a tile-component on several threads.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">Maximum number of threads; 1 or less keeps decoding sequential.</param>
        /// <param name="chainFactory">
        /// Creates an additional chain of code-block decoding stages that follows this transform's source. Each
        /// worker thread uses its own chain, so the stages themselves need not be thread-safe.
        /// </param>
        internal void EnableParallelDecoding(int maxDegreeOfParallelism, Func<CBlkWTDataSrcDec> chainFactory)
        {
            parallelDegree = Math.Max(1, maxDegreeOfParallelism);
            workerChainFactory = parallelDegree > 1 ? chainFactory : null;
        }

        /// <summary> The reversible flag for each component in each tile. The first index is
        /// the tile index, the second one is the component index. The
        /// reversibility of the components for each tile are calculated on a as
        /// needed basis.
        /// 
        /// </summary>
        private readonly Dictionary<int, bool[]> reversible = new Dictionary<int, bool[]>();
        //private bool[][] reversible;

        /// <summary> Initializes this object with the given source of wavelet
        /// coefficients. It initializes the resolution level for full resolutioin
        /// reconstruction.
        /// 
        /// </summary>
        /// <param name="src">from where the wavelet coefficinets should be obtained.
        /// 
        /// </param>
        /// <param name="decSpec">The decoder specifications
        /// 
        /// </param>
        public InvWTFull(CBlkWTDataSrcDec src, DecoderSpecs decSpec) : base(src, decSpec)
        {
            this.src = src;
            var nc = src.NumComps;
            reconstructedComps = new DataBlk[nc];
            ndl = new int[nc];
            rentedFloatBuffers = new float[nc][];
            rentedIntBuffers = new int[nc][];
        }

        /// <summary> Returns the reversibility of the current subband. It computes
        /// iteratively the reversibility of the child subbands. For each subband
        /// it tests the reversibility of the horizontal and vertical synthesis
        /// filters used to reconstruct this subband.
        /// 
        /// </summary>
        /// <param name="subband">The current subband.
        /// 
        /// </param>
        /// <returns> true if all the  filters used to reconstruct the current 
        /// subband are reversible
        /// 
        /// </returns>
        private bool IsSubbandReversible(Subband? subband)
        {
            if (subband == null) return true;
            if (subband.isNode)
            {
                // It's reversible if the filters to obtain the 4 subbands are
                // reversible and the ones for this one are reversible too.
                return IsSubbandReversible(subband.LL) && IsSubbandReversible(subband.HL) && IsSubbandReversible(subband.LH) && IsSubbandReversible(subband.HH) && ((SubbandSyn)subband).hFilter!.Reversible && ((SubbandSyn)subband).vFilter!.Reversible;
            }
            else
            {
                // Leaf subband. Reversibility of data depends on source, so say
                // it's true
                return true;
            }
        }

        /// <summary> Returns the reversibility of the wavelet transform for the specified
        /// component, in the current tile. A wavelet transform is reversible when
        /// it is suitable for lossless and lossy-to-lossless compression.
        /// 
        /// </summary>
        /// <param name="t">The index of the tile.
        /// 
        /// </param>
        /// <param name="c">The index of the component.
        /// 
        /// </param>
        /// <returns> true is the wavelet transform is reversible, false if not.
        /// 
        /// </returns>
        public override bool IsReversible(int t, int c)
        {
            if (reversible[t] == null)
            {
                // Reversibility not yet calculated for this tile
                reversible[t] = new bool[NumComps];
                for (var i = reversible[t].Length - 1; i >= 0; i--)
                {
                    reversible[t][i] = IsSubbandReversible(src.GetSynSubbandTree(t, i));
                }
            }
            return reversible[t][c];
        }

        /// <summary> Returns the number of bits, referred to as the "range bits",
        /// corresponding to the nominal range of the data in the specified
        /// component.
        /// 
        /// The returned value corresponds to the nominal dynamic range of the
        /// reconstructed image data, as long as the GetNomRangeBits() method of
        /// the source returns a value corresponding to the nominal dynamic range
        /// of the image data and not not of the wavelet coefficients.
        /// 
        /// If this number is <i>b</b> then for unsigned data the nominal range
        /// is between 0 and 2^b-1, and for signed data it is between -2^(b-1) and
        /// 2^(b-1)-1.
        /// 
        /// </summary>
        /// <param name="compIndex">The index of the component.
        /// 
        /// </param>
        /// <returns> The number of bits corresponding to the nominal range of the
        /// data.
        /// 
        /// </returns>
        public override int GetNomRangeBits(int compIndex)
        {
            return src.GetNomRangeBits(compIndex);
        }

        /// <summary> Returns the position of the fixed point in the specified
        /// component. This is the position of the least significant integral
        /// (i.e. non-fractional) bit, which is equivalent to the number of
        /// fractional bits. For instance, for fixed-point values with 2 fractional
        /// bits, 2 is returned. For floating-point data this value does not apply
        /// and 0 should be returned. Position 0 is the position of the least
        /// significant bit in the data.
        /// 
        /// This default implementation assumes that the wavelet transform does
        /// not modify the fixed point. If that were the case this method should be
        /// overriden.
        /// 
        /// </summary>
        /// <param name="compIndex">The index of the component.
        /// 
        /// </param>
        /// <returns> The position of the fixed-point, which is the same as the
        /// number of fractional bits. For floating-point data 0 is returned.
        /// 
        /// </returns>
        public override int GetFixedPoint(int compIndex)
        {
            return src.GetFixedPoint(compIndex);
        }

        /// <summary> Returns a block of image data containing the specifed rectangular area,
        /// in the specified component, as a reference to the internal buffer (see
        /// below). The rectangular area is specified by the coordinates and
        /// dimensions of the 'blk' object.
        /// 
        /// The area to return is specified by the 'ulx', 'uly', 'w' and 'h'
        /// members of the 'blk' argument. These members are not modified by this
        /// method.
        /// 
        /// The data returned by this method can be the data in the internal
        /// buffer of this object, if any, and thus can not be modified by the
        /// caller. The 'offset' and 'scanw' of the returned data can be
        /// arbitrary. See the 'DataBlk' class.
        /// 
        /// The returned data has its 'progressive' attribute unset
        /// (i.e. false).
        /// 
        /// </summary>
        /// <param name="blk">Its coordinates and dimensions specify the area to return.
        /// 
        /// </param>
        /// <param name="compIndex">The index of the component from which to get the data.
        /// 
        /// </param>
        /// <returns> The requested DataBlk
        /// 
        /// </returns>
        /// <seealso cref="GetInternCompData" />
        public override DataBlk GetInternCompData(DataBlk blk, int compIndex)
        {
            var tIdx = TileIdx;

            //If the source image has not been decomposed (or was invalidated by a tile change)
            if (reconstructedComps[compIndex] == null || reconstructedComps[compIndex].Data == null)
            {
                // Call GetSynSubbandTree exactly once on the slow path; reuse for both
                // dtype determination and waveletTreeReconstruction — avoids the extra
                // virtual dispatch that was occurring on every call in the original code.
                var synTree = src.GetSynSubbandTree(tIdx, compIndex);
                // Without a decomposition there is no filter to say which type the data has; it is the dequantizer's: integers if the
                // quantization is reversible, otherwise floats (integers would drop the fraction of every coefficient, which the
                // component transform that follows needs)
                dtype = synTree.HorWFilter! != null
                    ? synTree.HorWFilter!.DataType
                    : (decSpec.qts.IsReversible(tIdx, compIndex) ? DataBlk.TYPE_INT : DataBlk.TYPE_FLOAT);

                //Allocate component data buffer
                switch (dtype)
                {

                    case DataBlk.TYPE_FLOAT:
                        var fwidth = GetFullTileCompWidth(tIdx, compIndex);
                        var fheight = GetFullTileCompHeight(tIdx, compIndex);

                        // Validate dimensions to prevent integer overflow
                        long fBufferSize = (long)fwidth * fheight;
                        if (fBufferSize > int.MaxValue)
                        {
                            throw new InvalidOperationException(
                                $"Tile component too large for float reconstruction: " +
                                $"w={fwidth}, h={fheight}. " +
                                $"Buffer size {fBufferSize} exceeds maximum array size.");
                        }

                        // Reuse the existing wrapper if it has the right type; otherwise create a
                        // new one using the no-arg ctor so no internal float[] is allocated.
                        if (reconstructedComps[compIndex] is DataBlkFloat existingFloat)
                        {
                            existingFloat.ulx = 0; existingFloat.uly = 0;
                            existingFloat.w = fwidth; existingFloat.h = fheight;
                            existingFloat.offset = 0; existingFloat.scanw = fwidth;
                        }
                        else
                        {
                            var dbf = new DataBlkFloat();
                            dbf.ulx = 0; dbf.uly = 0;
                            dbf.w = fwidth; dbf.h = fheight;
                            dbf.offset = 0; dbf.scanw = fwidth;
                            reconstructedComps[compIndex] = dbf;
                        }
                        try
                        {
                            var rent = ArrayPool<float>.Shared.Rent((int)fBufferSize);
                            // Rent() may hand back a buffer holding a previous decode's samples, and code-blocks
                            // that are skipped or truncated never write their area, so start from zero.
                            Array.Clear(rent, 0, (int)fBufferSize);
                            reconstructedComps[compIndex].Data = rent;
                            rentedFloatBuffers[compIndex] = rent;
                        }
                        catch
                        {
                            // fallback to default allocation if renting fails
                            reconstructedComps[compIndex].Data = new float[(int)fBufferSize];
                        }
                        break;

                    case DataBlk.TYPE_INT:
                        var iwidth = GetFullTileCompWidth(tIdx, compIndex);
                        var iheight = GetFullTileCompHeight(tIdx, compIndex);

                        // Validate dimensions to prevent integer overflow
                        long iBufferSize = (long)iwidth * iheight;
                        if (iBufferSize > int.MaxValue)
                        {
                            throw new InvalidOperationException(
                                $"Tile component too large for int reconstruction: " +
                                $"w={iwidth}, h={iheight}. " +
                                $"Buffer size {iBufferSize} exceeds maximum array size.");
                        }

                        // Reuse the existing wrapper if it has the right type; otherwise create a
                        // new one using the no-arg ctor so no internal int[] is allocated.
                        if (reconstructedComps[compIndex] is DataBlkInt existingInt)
                        {
                            existingInt.ulx = 0; existingInt.uly = 0;
                            existingInt.w = iwidth; existingInt.h = iheight;
                            existingInt.offset = 0; existingInt.scanw = iwidth;
                        }
                        else
                        {
                            var dbi = new DataBlkInt();
                            dbi.ulx = 0; dbi.uly = 0;
                            dbi.w = iwidth; dbi.h = iheight;
                            dbi.offset = 0; dbi.scanw = iwidth;
                            reconstructedComps[compIndex] = dbi;
                        }
                        try
                        {
                            var irent = ArrayPool<int>.Shared.Rent((int)iBufferSize);
                            Array.Clear(irent, 0, (int)iBufferSize);
                            reconstructedComps[compIndex].Data = irent;
                            rentedIntBuffers[compIndex] = irent;
                        }
                        catch
                        {
                            // fallback to default allocation
                            reconstructedComps[compIndex].Data = new int[(int)iBufferSize];
                        }
                        break;
                }
                //Reconstruct source image — reuse the synTree reference already fetched above
                ReconstructComponent(reconstructedComps[compIndex], synTree, compIndex);
            }
            else
            {
                // Fast path: reconstruction already cached. Derive dtype from the concrete
                // type of the cached block — zero virtual calls to GetSynSubbandTree.
                dtype = reconstructedComps[compIndex] is DataBlkInt ? DataBlk.TYPE_INT : DataBlk.TYPE_FLOAT;
            }

            if (blk.DataType != dtype)
            {
                if (dtype == DataBlk.TYPE_INT)
                {
                    blk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
                }
                else
                {
                    blk = new DataBlkFloat(blk.ulx, blk.uly, blk.w, blk.h);
                }
            }
            // Set the reference to the internal buffer
            blk.Data = reconstructedComps[compIndex].Data;
            blk.offset = reconstructedComps[compIndex].w * blk.uly + blk.ulx;
            blk.scanw = reconstructedComps[compIndex].w;
            blk.progressive = false;
            return blk;
        }

        /// <summary> Returns a block of image data containing the specifed rectangular area,
        /// in the specified component, as a copy (see below). The rectangular area
        /// is specified by the coordinates and dimensions of the 'blk' object.
        /// 
        /// The area to return is specified by the 'ulx', 'uly', 'w' and 'h'
        /// members of the 'blk' argument. These members are not modified by this
        /// method.
        /// 
        /// The data returned by this method is always a copy of the internal
        /// data of this object, if any, and it can be modified "in place" without
        /// any problems after being returned. The 'offset' of the returned data is
        /// 0, and the 'scanw' is the same as the block's width. See the 'DataBlk'
        /// class.
        /// 
        /// If the data array in 'blk' is <tt>null</tt>, then a new one is
        /// created. If the data array is not <tt>null</tt> then it must be big
        /// enough to contain the requested area.
        /// 
        /// The returned data always has its 'progressive' attribute unset (i.e
        /// false)
        /// 
        /// </summary>
        /// <param name="blk">Its coordinates and dimensions specify the area to
        /// return. If it contains a non-null data array, then it must be large
        /// enough. If it contains a null data array a new one is created. The
        /// fields in this object are modified to return the data.
        /// 
        /// </param>
        /// <param name="c">The index of the component from which to get the data.
        /// 
        /// </param>
        /// <returns> The requested DataBlk
        /// 
        /// </returns>
        /// <seealso cref="GetCompData" />
        public override DataBlk GetCompData(DataBlk blk, int c)
        {
            //int j;
            // dst_data declared below
            int[]? dst_data_int; // src_data_int removed
            float[]? dst_data_float; // src_data_float removed

            // To keep compiler happy
            object? dst_data = null;

            // Ensure output buffer
            switch (blk.DataType)
            {

                case DataBlk.TYPE_INT:
                    // Validate dimensions to prevent integer overflow
                    long intBufferSize = (long)blk.w * blk.h;
                    if (intBufferSize > int.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"Block dimensions too large for int buffer: " +
                            $"w={blk.w}, h={blk.h}. " +
                            $"Buffer size {intBufferSize} exceeds maximum array size.");
                    }
                    
                    dst_data_int = (int[]?)blk.Data;
                    if (dst_data_int == null || dst_data_int.Length < intBufferSize)
                    {
                        dst_data_int = new int[(int)intBufferSize];
                    }
                    dst_data = dst_data_int;
                    break;

                case DataBlk.TYPE_FLOAT:
                    // Validate dimensions to prevent integer overflow
                    long floatBufferSize = (long)blk.w * blk.h;
                    if (floatBufferSize > int.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"Block dimensions too large for float buffer: " +
                            $"w={blk.w}, h={blk.h}. " +
                            $"Buffer size {floatBufferSize} exceeds maximum array size.");
                    }
                    
                    dst_data_float = (float[]?)blk.Data;
                    if (dst_data_float == null || dst_data_float.Length < floatBufferSize)
                    {
                        dst_data_float = new float[(int)floatBufferSize];
                    }
                    dst_data = dst_data_float;
                    break;
            }

            // Use getInternCompData() to get the data, since getInternCompData()
            // returns reference to internal buffer, we must copy it.
            blk = GetInternCompData(blk, c);

            // Copy the data
            blk.Data = dst_data;
            blk.offset = 0;
            blk.scanw = blk.w;
            return blk;
        }

        /// <summary>
        /// Tile-component passes with fewer samples than this run on the calling thread; splitting them costs more than it saves.
        /// </summary>
        internal static int MinParallelWaveletSamples = 1 << 17;

        /// <summary>Number of adjacent columns the vertical passes process together (16 four-byte samples fill a 64-byte cache line).</summary>
        private const int ColumnBlock = 16;

        /// <summary>Overrides <see cref="MinParallelWaveletSamples"/> for decodes started on the current thread (tests only).</summary>
        [ThreadStatic]
        internal static int? MinParallelWaveletSamplesForCurrentThread;

        /// <summary>
        /// Runs one pass of the 2D inverse transform (all rows, or all columns), inline or split across threads; see
        /// <see cref="WaveletPass.Run{T}"/>.
        /// </summary>
        private void RunPass<T>(int count, int itemLength, int scratchLength, ref T[]? sequentialScratch, Action<int, int, T[]> body)
            => WaveletPass.Run(count, itemLength, scratchLength, ref sequentialScratch, parallelDegree,
                MinParallelWaveletSamplesForCurrentThread ?? MinParallelWaveletSamples, cancellationToken, body);

        /// <summary>Horizontal 5x3 synthesis of rows [rowStart, rowEnd) of a subband whose first row starts at <paramref name="baseOffset"/>.</summary>
        private void HorizontalPass5x3(int[] data, int[] buf, SynWTFilterIntLift5x3 filter, bool evenStart, int baseOffset, int stride,
            int w, int rowStart, int rowEnd)
        {
            int wHalf = w / 2, wHalfCeil = (w + 1) / 2;
            for (var i = rowStart; i < rowEnd; i++)
            {
                if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                var offset = baseOffset + i * stride;
                new ReadOnlySpan<int>(data, offset, w).CopyTo(buf);
                if (evenStart)
                    filter.synthetize_lpf(buf, 0, wHalfCeil, 1, buf, wHalfCeil, wHalf, 1, data, offset, 1);
                else
                    filter.synthetize_hpf(buf, 0, wHalf, 1, buf, wHalf, wHalfCeil, 1, data, offset, 1);
            }
        }

        /// <summary>
        /// Vertical 5x3 synthesis of columns [colStart, colEnd) of a subband whose first column starts at <paramref name="baseOffset"/>.
        /// Columns are processed <see cref="ColumnBlock"/> at a time: gathering one column touches a cache line per sample, so
        /// reading and writing a block of adjacent columns together uses each cache line fully. <paramref name="buf"/> must hold
        /// 2 * <see cref="ColumnBlock"/> * <paramref name="h"/> samples (the gathered columns, then the synthesised ones).
        /// </summary>
        private void VerticalPass5x3(int[] data, int[] buf, SynWTFilterIntLift5x3 filter, bool evenStart, int baseOffset, int stride,
            int h, int colStart, int colEnd)
        {
            int hHalf = h / 2, hHalfCeil = (h + 1) / 2;
            var outBase = ColumnBlock * h;
            for (var blockStart = colStart; blockStart < colEnd; blockStart += ColumnBlock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var width = Math.Min(ColumnBlock, colEnd - blockStart);
                var rowBase = baseOffset + blockStart;

                // Gather: one pass down the rows, reading 'width' adjacent samples (one cache line) per row.
                for (var i = 0; i < h; i++)
                {
                    var src = rowBase + i * stride;
                    for (var cc = 0; cc < width; cc++)
                        buf[cc * h + i] = data[src + cc];
                }

                for (var cc = 0; cc < width; cc++)
                {
                    var inOff = cc * h;
                    if (evenStart)
                        filter.synthetize_lpf(buf, inOff, hHalfCeil, 1, buf, inOff + hHalfCeil, hHalf, 1, buf, outBase + inOff, 1);
                    else
                        filter.synthetize_hpf(buf, inOff, hHalf, 1, buf, inOff + hHalf, hHalfCeil, 1, buf, outBase + inOff, 1);
                }

                // Scatter the synthesised columns back, again a cache line per row.
                for (var i = 0; i < h; i++)
                {
                    var dst = rowBase + i * stride;
                    for (var cc = 0; cc < width; cc++)
                        data[dst + cc] = buf[outBase + cc * h + i];
                }
            }
        }

        /// <summary>Horizontal 9x7 synthesis of rows [rowStart, rowEnd) of a subband whose first row starts at <paramref name="baseOffset"/>.</summary>
        private void HorizontalPass9x7(float[] data, float[] buf, SynWTFilterFloatLift9x7 filter, bool evenStart, int baseOffset, int stride,
            int w, int rowStart, int rowEnd)
        {
            int wHalf = w / 2, wHalfCeil = (w + 1) / 2;
            for (var i = rowStart; i < rowEnd; i++)
            {
                if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                var offset = baseOffset + i * stride;
                new ReadOnlySpan<float>(data, offset, w).CopyTo(buf);
                if (evenStart)
                    filter.synthetize_lpf(buf, 0, wHalfCeil, 1, buf, wHalfCeil, wHalf, 1, data, offset, 1);
                else
                    filter.synthetize_hpf(buf, 0, wHalf, 1, buf, wHalf, wHalfCeil, 1, data, offset, 1);
            }
        }

        /// <summary>
        /// Vertical 9x7 synthesis of columns [colStart, colEnd) of a subband whose first column starts at <paramref name="baseOffset"/>.
        /// Columns are processed <see cref="ColumnBlock"/> at a time: gathering one column touches a cache line per sample, so
        /// reading and writing a block of adjacent columns together uses each cache line fully. <paramref name="buf"/> must hold
        /// 2 * <see cref="ColumnBlock"/> * <paramref name="h"/> samples (the gathered columns, then the synthesised ones).
        /// </summary>
        private void VerticalPass9x7(float[] data, float[] buf, SynWTFilterFloatLift9x7 filter, bool evenStart, int baseOffset, int stride,
            int h, int colStart, int colEnd)
        {
            int hHalf = h / 2, hHalfCeil = (h + 1) / 2;
            var outBase = ColumnBlock * h;
            for (var blockStart = colStart; blockStart < colEnd; blockStart += ColumnBlock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var width = Math.Min(ColumnBlock, colEnd - blockStart);
                var rowBase = baseOffset + blockStart;

                // Gather: one pass down the rows, reading 'width' adjacent samples (one cache line) per row.
                for (var i = 0; i < h; i++)
                {
                    var src = rowBase + i * stride;
                    for (var cc = 0; cc < width; cc++)
                        buf[cc * h + i] = data[src + cc];
                }

                for (var cc = 0; cc < width; cc++)
                {
                    var inOff = cc * h;
                    if (evenStart)
                        filter.synthetize_lpf(buf, inOff, hHalfCeil, 1, buf, inOff + hHalfCeil, hHalf, 1, buf, outBase + inOff, 1);
                    else
                        filter.synthetize_hpf(buf, inOff, hHalf, 1, buf, inOff + hHalf, hHalfCeil, 1, buf, outBase + inOff, 1);
                }

                // Scatter the synthesised columns back, again a cache line per row.
                for (var i = 0; i < h; i++)
                {
                    var dst = rowBase + i * stride;
                    for (var cc = 0; cc < width; cc++)
                        data[dst + cc] = buf[outBase + cc * h + i];
                }
            }
        }

        /// <summary> Performs the 2D inverse wavelet transform on a subband of the image, on
        /// the specified component. This method will successively perform 1D
        /// filtering steps on all columns and then all lines of the subband.
        /// 
        /// </summary>
        /// <param name="db">the buffer for the image/wavelet data.
        /// 
        /// </param>
        /// <param name="sb">The subband to reconstruct.
        /// 
        /// </param>
        /// <param name="c">The index of the component to reconstruct 
        /// 
        /// </param>
        private void wavelet2DReconstruction(DataBlk db, SubbandSyn sb, int c)
        {
            object? data;
            object? buf;
            int ulx, uly, w, h;
            int i, j, k;
            int offset;

            // If subband is empty (i.e. zero size) nothing to do
            if (sb.w == 0 || sb.h == 0)
            {
                return;
            }

            data = db.Data;

            ulx = sb.ulx;
            uly = sb.uly;
            w = sb.w;
            h = sb.h;

            buf = null; // To keep compiler happy

            // Fast paths: both filters are the same concrete type (5x3 int or 9x7 float) – call the typed methods
            // directly to eliminate the object-typed virtual dispatch chain and the slower Array.Copy(Array,...)
            // overload used in the generic fallback. Rows (then columns) are independent, so each pass may be split
            // across threads; see RunPass.
            var baseOffset = (uly - db.uly) * db.w + ulx - db.ulx;
            var stride = db.w;
            var evenStartX = sb.ulcx % 2 == 0;
            var evenStartY = sb.ulcy % 2 == 0;
            var need = (w >= h) ? w : h;

            if (sb.hFilter is SynWTFilterIntLift5x3 hf5x3 && sb.vFilter is SynWTFilterIntLift5x3 vf5x3)
            {
                var intData = (int[])data!;
                RunPass(h, w, need, ref _waveletScratchInt,
                    (rowStart, rowEnd, scratch) => HorizontalPass5x3(intData, scratch, hf5x3, evenStartX, baseOffset, stride, w, rowStart, rowEnd));
                RunPass(w, h, Math.Max(need, 2 * ColumnBlock * h), ref _waveletScratchInt,
                    (colStart, colEnd, scratch) => VerticalPass5x3(intData, scratch, vf5x3, evenStartY, baseOffset, stride, h, colStart, colEnd));
                return;
            }

            if (sb.hFilter is SynWTFilterFloatLift9x7 hf9x7 && sb.vFilter is SynWTFilterFloatLift9x7 vf9x7)
            {
                var floatData = (float[])data!;
                RunPass(h, w, need, ref _waveletScratchFloat,
                    (rowStart, rowEnd, scratch) => HorizontalPass9x7(floatData, scratch, hf9x7, evenStartX, baseOffset, stride, w, rowStart, rowEnd));
                RunPass(w, h, Math.Max(need, 2 * ColumnBlock * h), ref _waveletScratchFloat,
                    (colStart, colEnd, scratch) => VerticalPass9x7(floatData, scratch, vf9x7, evenStartY, baseOffset, stride, h, colStart, colEnd));
                return;
            }

            int needGen = (w >= h) ? w : h;
            switch (sb.HorWFilter!.DataType)
            {

                case DataBlk.TYPE_INT:
                    if (_waveletScratchInt == null || _waveletScratchInt.Length < needGen)
                        _waveletScratchInt = new int[needGen];
                    buf = _waveletScratchInt;
                    break;

                case DataBlk.TYPE_FLOAT:
                    if (_waveletScratchFloat == null || _waveletScratchFloat.Length < needGen)
                        _waveletScratchFloat = new float[needGen];
                    buf = _waveletScratchFloat;
                    break;
            }

            try
            {
                int wHalfG = w / 2, wHalfCeilG = (w + 1) / 2;
                int hHalfG = h / 2, hHalfCeilG = (h + 1) / 2;

                //Perform the horizontal reconstruction
                offset = (uly - db.uly) * db.w + ulx - db.ulx;
                if (sb.ulcx % 2 == 0)
                {
                    // start index is even => use LPF
                    for (i = 0; i < h; i++, offset += db.w)
                    {
                        if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                        Array.Copy((Array)data!, offset, (Array)buf!, 0, w);
                        sb.hFilter!.synthetize_lpf(buf, 0, wHalfCeilG, 1, buf, wHalfCeilG, wHalfG, 1, data, offset, 1);
                    }
                }
                else
                {
                    // start index is odd => use HPF
                    for (i = 0; i < h; i++, offset += db.w)
                    {
                        if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                        Array.Copy((Array)data!, offset, (Array)buf!, 0, w);
                        sb.hFilter!.synthetize_hpf(buf, 0, wHalfG, 1, buf, wHalfG, wHalfCeilG, 1, data, offset, 1);
                    }
                }

                //Perform the vertical reconstruction 
                offset = (uly - db.uly) * db.w + ulx - db.ulx;
                switch (sb.VerWFilter!.DataType)
                {

                    case DataBlk.TYPE_INT:
                        int[] data_int, buf_int;
                        data_int = (int[])data!;
                        buf_int = (int[])buf!;
                        if (sb.ulcy % 2 == 0)
                        {
                            // start index is even => use LPF
                            for (j = 0; j < w; j++, offset++)
                            {
                                if ((j & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                                for (i = 0, k = offset; i < h; i++, k += db.w)
                                    buf_int[i] = data_int[k];
                                sb.vFilter!.synthetize_lpf(buf, 0, hHalfCeilG, 1, buf, hHalfCeilG, hHalfG, 1, data, offset, db.w);
                            }
                        }
                        else
                        {
                            // start index is odd => use HPF
                            for (j = 0; j < w; j++, offset++)
                            {
                                if ((j & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                                for (i = 0, k = offset; i < h; i++, k += db.w)
                                    buf_int[i] = data_int[k];
                                sb.vFilter!.synthetize_hpf(buf, 0, hHalfG, 1, buf, hHalfG, hHalfCeilG, 1, data, offset, db.w);
                            }
                        }
                        break;

                    case DataBlk.TYPE_FLOAT:
                        float[] data_float2, buf_float2;
                        data_float2 = (float[])data!;
                        buf_float2 = (float[])buf!;
                        if (sb.ulcy % 2 == 0)
                        {
                            // start index is even => use LPF
                            for (j = 0; j < w; j++, offset++)
                            {
                                if ((j & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                                for (i = 0, k = offset; i < h; i++, k += db.w)
                                    buf_float2[i] = data_float2[k];
                                sb.vFilter!.synthetize_lpf(buf, 0, hHalfCeilG, 1, buf, hHalfCeilG, hHalfG, 1, data, offset, db.w);
                            }
                        }
                        else
                        {
                            // start index is odd => use HPF
                            for (j = 0; j < w; j++, offset++)
                            {
                                if ((j & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                                for (i = 0, k = offset; i < h; i++, k += db.w)
                                    buf_float2[i] = data_float2[k];
                                sb.vFilter!.synthetize_hpf(buf, 0, hHalfG, 1, buf, hHalfG, hHalfCeilG, 1, data, offset, db.w);
                            }
                        }
                        break;
                }
            }
            finally
            {
                // buf points to an instance-level scratch field; nothing to return.
            }
        }

        /// <summary> Performs the inverse wavelet transform on the whole component. It
        /// iteratively reconstructs the subbands from leaves up to the root
        /// node. This method is recursive, the first call to it the 'sb' must be
        /// the root of the subband tree. The method will then process the entire
        /// subband tree by calling itslef recursively.
        /// 
        /// </summary>
        /// <param name="img">The buffer for the image/wavelet data.
        /// 
        /// </param>
        /// <param name="sb">The subband to reconstruct.
        /// 
        /// </param>
        /// <param name="c">The index of the component to reconstruct 
        /// 
        /// </param>
        /// <summary>
        /// Fills the tile-component buffer from the code-blocks and performs the inverse wavelet transform on it.
        /// </summary>
        private void ReconstructComponent(DataBlk img, SubbandSyn root, int c)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workerChainFactory != null)
            {
                var jobs = new List<BlockJob>();
                CollectCodeBlocks(root, c, jobs);
                if (jobs.Count >= (MinParallelBlocksForCurrentThread ?? MinParallelBlocks))
                {
                    DecodeCodeBlocksInParallel(img, jobs, c);
                    waveletTreeReconstruction(img, root, c, loadCodeBlocks: false);
                    return;
                }
            }

            waveletTreeReconstruction(img, root, c, loadCodeBlocks: true);
        }

        /// <summary>Lists the code-blocks <see cref="waveletTreeReconstruction"/> would read, in the same subband order.</summary>
        private void CollectCodeBlocks(SubbandSyn sb, int c, List<BlockJob> jobs)
        {
            if (!sb.isNode)
            {
                if (sb.w == 0 || sb.h == 0) return;
                var ncblks = sb.numCb;
                for (var m = 0; m < ncblks.y; m++)
                    for (var n = 0; n < ncblks.x; n++)
                        jobs.Add(new BlockJob { Subband = sb, M = m, N = n });
                return;
            }

            CollectCodeBlocks((SubbandSyn)sb.LL!, c, jobs);
            if (sb.resLvl <= reslvl - maxImgRes + ndl[c])
            {
                CollectCodeBlocks((SubbandSyn)sb.HL, c, jobs);
                CollectCodeBlocks((SubbandSyn)sb.LH, c, jobs);
                CollectCodeBlocks((SubbandSyn)sb.HH, c, jobs);
            }
        }

        private BlockWorker RentWorker(int tile)
        {
            if (!idleWorkers.TryTake(out var worker))
            {
                worker = new BlockWorker { Chain = workerChainFactory!() };
            }

            if (worker.Tile != tile)
            {
                // The primary chain has already moved the shared reader to this tile; bring this chain's view along.
                var numTiles = src.GetNumTiles(null);
                worker.Chain.SetTile(tile % numTiles.x, tile / numTiles.x);
                worker.Tile = tile;
            }

            if (worker.Block == null || worker.Block.DataType != dtype)
            {
                worker.Block = dtype == DataBlk.TYPE_INT ? (DataBlk)new DataBlkInt() : new DataBlkFloat();
            }
            return worker;
        }

        private void DecodeCodeBlocksInParallel(DataBlk img, List<BlockJob> jobs, int c)
        {
            var tile = src.TileIdx;
            var options = new ParallelOptions { MaxDegreeOfParallelism = parallelDegree, CancellationToken = cancellationToken };

            try
            {
                Parallel.For(0, jobs.Count, options,
                    () => RentWorker(tile),
                    (i, _, worker) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var job = jobs[i];
                        var blk = worker.Chain.GetInternCodeBlock(c, job.M, job.N, job.Subband, worker.Block!);
                        worker.Block = blk;
                        CopyCodeBlock(blk, img);
                        return worker;
                    },
                    worker => idleWorkers.Add(worker));
            }
            catch (AggregateException e)
            {
                // Surface the original exception (e.g. a decoder limit or corrupt-codestream error) with its type intact.
                ExceptionDispatchInfo.Capture(e.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
        }

        /// <summary>Copies a decoded code-block into its (disjoint) rectangle of the tile-component buffer.</summary>
        private static void CopyCodeBlock(DataBlk blk, DataBlk img)
        {
            var dstBase = blk.uly * img.w + blk.ulx;
            if (blk.DataType == DataBlk.TYPE_INT)
            {
                var srcArr = (int[])blk.Data!;
                var dstArr = (int[])img.Data!;
                for (var i = blk.h - 1; i >= 0; i--)
                    new ReadOnlySpan<int>(srcArr, blk.offset + i * blk.scanw, blk.w)
                        .CopyTo(dstArr.AsSpan(dstBase + i * img.w, blk.w));
            }
            else
            {
                var srcArr = (float[])blk.Data!;
                var dstArr = (float[])img.Data!;
                for (var i = blk.h - 1; i >= 0; i--)
                    new ReadOnlySpan<float>(srcArr, blk.offset + i * blk.scanw, blk.w)
                        .CopyTo(dstArr.AsSpan(dstBase + i * img.w, blk.w));
            }
        }

        private void waveletTreeReconstruction(DataBlk img, SubbandSyn sb, int c, bool loadCodeBlocks = true)
        {

            DataBlk subbData;

            // If the current subband is a leaf then get the data from the source
            if (!sb.isNode)
            {
                if (!loadCodeBlocks) return; // Already filled by DecodeCodeBlocksInParallel

                int i, m, n;
                Coord ncblks;

                if (sb.w == 0 || sb.h == 0)
                {
                    return; // If empty subband do nothing
                }

                // Get all code-blocks in subband
                if (dtype == DataBlk.TYPE_INT)
                {
                    if (_subbDataInt == null) _subbDataInt = new DataBlkInt();
                    subbData = _subbDataInt;
                }
                else
                {
                    if (_subbDataFloat == null) _subbDataFloat = new DataBlkFloat();
                    subbData = _subbDataFloat;
                }
                ncblks = sb.numCb;
                if (dtype == DataBlk.TYPE_INT)
                {
                    int[] dstArr = (int[])img.Data!;
                    for (m = 0; m < ncblks.y; m++)
                    {
                        for (n = 0; n < ncblks.x; n++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            subbData = src.GetInternCodeBlock(c, m, n, sb, subbData);
                            int[] srcArr = (int[])subbData.Data!;
                            int dstBase = subbData.uly * img.w + subbData.ulx;
                            for (i = subbData.h - 1; i >= 0; i--)
                            {
                                new ReadOnlySpan<int>(srcArr, subbData.offset + i * subbData.scanw, subbData.w)
                                    .CopyTo(dstArr.AsSpan(dstBase + i * img.w, subbData.w));
                            }
                        }
                    }
                }
                else
                {
                    float[] dstArr = (float[])img.Data!;
                    for (m = 0; m < ncblks.y; m++)
                    {
                        for (n = 0; n < ncblks.x; n++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            subbData = src.GetInternCodeBlock(c, m, n, sb, subbData);
                            float[] srcArr = (float[])subbData.Data!;
                            int dstBase = subbData.uly * img.w + subbData.ulx;
                            for (i = subbData.h - 1; i >= 0; i--)
                            {
                                new ReadOnlySpan<float>(srcArr, subbData.offset + i * subbData.scanw, subbData.w)
                                    .CopyTo(dstArr.AsSpan(dstBase + i * img.w, subbData.w));
                            }
                        }
                    }
                }
            }
            else if (sb.isNode)
            {
                // Reconstruct the lower resolution levels if the current subbands
                // is a node

                //Perform the reconstruction of the LL subband
                waveletTreeReconstruction(img, (SubbandSyn)sb.LL!, c, loadCodeBlocks);

                if (sb.resLvl <= reslvl - maxImgRes + ndl[c])
                {
                    //Reconstruct the other subbands
                    waveletTreeReconstruction(img, (SubbandSyn)sb.HL, c, loadCodeBlocks);
                    waveletTreeReconstruction(img, (SubbandSyn)sb.LH, c, loadCodeBlocks);
                    waveletTreeReconstruction(img, (SubbandSyn)sb.HH, c, loadCodeBlocks);

                    //Perform the 2D wavelet decomposition of the current subband
                    cancellationToken.ThrowIfCancellationRequested();
                    wavelet2DReconstruction(img, sb, c);
                }
            }
        }

        /// <summary> Returns the implementation type of this wavelet transform, WT_IMPL_FULL
        /// (full-page based transform). All components return the same.
        /// 
        /// </summary>
        /// <param name="c">The index of the component.
        /// 
        /// </param>
        /// <returns> WT_IMPL_FULL
        /// 
        /// </returns>
        /// <seealso cref="WaveletTransform.WT_IMPL_FULL" />
        public override int GetImplementationType(int c)
        {
            return WaveletTransform_Fields.WT_IMPL_FULL;
        }

        /// <summary> Changes the current tile, given the new indexes. An
        /// IllegalArgumentException is thrown if the indexes do not correspond to
        /// a valid tile.
        /// 
        /// </summary>
        /// <param name="x">The horizontal index of the tile.
        /// 
        /// </param>
        /// <param name="y">The vertical index of the new tile.
        /// 
        /// </param>
        public override void SetTile(int x, int y)
        {
            int i;

            // Change tile
            base.SetTile(x, y);

            var nc = src.NumComps;
            var tIdx = src.TileIdx;
            for (var c = 0; c < nc; c++)
            {
                ndl[c] = src.GetSynSubbandTree(tIdx, c).resLvl;
            }

            // Ensure rented buffers are large enough for new tile; do not return them so they are reused
            for (i = 0; i < nc; i++)
            {
                var newWidth = GetFullTileCompWidth(tIdx, i);
                var newHeight = GetFullTileCompHeight(tIdx, i);
                
                // Validate dimensions to prevent integer overflow
                long needed = (long)newWidth * newHeight;
                if (needed > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Tile component {i} too large in SetTile: " +
                        $"w={newWidth}, h={newHeight}. " +
                        $"Buffer size {needed} exceeds maximum array size.");
                }
                int neededInt = (int)needed;

                if (rentedFloatBuffers != null && rentedFloatBuffers.Length > i && rentedFloatBuffers[i] != null)
                {
                    if (rentedFloatBuffers[i]!.Length < neededInt)
                    {
                        // Rent a larger buffer and replace the old one
                        var old = rentedFloatBuffers[i];
                        var rent = ArrayPool<float>.Shared.Rent(neededInt);
                        rentedFloatBuffers[i] = rent;
                        try { ArrayPool<float>.Shared.Return(old!, clearArray: false); } catch { }
                    }
                }
                if (rentedIntBuffers != null && rentedIntBuffers.Length > i && rentedIntBuffers[i] != null)
                {
                    if (rentedIntBuffers[i]!.Length < neededInt)
                    {
                        var old = rentedIntBuffers[i];
                        var rent = ArrayPool<int>.Shared.Rent(neededInt);
                        rentedIntBuffers[i] = rent;
                        try { ArrayPool<int>.Shared.Return(old!, clearArray: false); } catch { }
                    }
                }

                // The wavelet data must be reconstructed since we've switched to a different tile.
                // Null only the backing Data array so the wrapper object itself can be reused,
                // avoiding a fresh DataBlkFloat/DataBlkInt allocation per tile per component.
                if (reconstructedComps != null && i < reconstructedComps.Length && reconstructedComps[i] != null)
                {
                    reconstructedComps[i].Data = null;
                }
            }

            cblkToDecode = 0;
            SubbandSyn root, sb;
            for (var c = 0; c < nc; c++)
            {
                root = src.GetSynSubbandTree(tIdx, c);
                for (var r = 0; r <= reslvl - maxImgRes + root.resLvl; r++)
                {
                    if (r == 0)
                    {
                        sb = (SubbandSyn)root.GetSubbandByIdx(0, 0);
                        if (sb != null)
                            cblkToDecode += sb.numCb.x * sb.numCb.y;
                    }
                    else
                    {
                        sb = (SubbandSyn)root.GetSubbandByIdx(r, 1);
                        if (sb != null)
                            cblkToDecode += sb.numCb.x * sb.numCb.y;
                        sb = (SubbandSyn)root.GetSubbandByIdx(r, 2);
                        if (sb != null)
                            cblkToDecode += sb.numCb.x * sb.numCb.y;
                        sb = (SubbandSyn)root.GetSubbandByIdx(r, 3);
                        if (sb != null)
                            cblkToDecode += sb.numCb.x * sb.numCb.y;
                    }
                } // Loop on resolution levels
            } // Loop on components
        }

        public override void NextTile()
        {
            int i;

            // Change tile
            base.NextTile();

            var nc = src.NumComps;
            var tIdx = src.TileIdx;
            for (var c = 0; c < nc; c++)
            {
                ndl[c] = src.GetSynSubbandTree(tIdx, c).resLvl;
            }

            // Ensure rented buffers are large enough for new tile; keep them for reuse
            for (i = 0; i < nc; i++)
            {
                var newWidth = GetFullTileCompWidth(tIdx, i);
                var newHeight = GetFullTileCompHeight(tIdx, i);
                
                // Validate dimensions to prevent integer overflow
                long needed = (long)newWidth * newHeight;
                if (needed > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Tile component {i} too large in NextTile: " +
                        $"w={newWidth}, h={newHeight}. " +
                        $"Buffer size {needed} exceeds maximum array size.");
                }
                int neededInt = (int)needed;

                if (rentedFloatBuffers != null && rentedFloatBuffers.Length > i && rentedFloatBuffers[i] != null)
                {
                    if (rentedFloatBuffers[i]!.Length < neededInt)
                    {
                        var old = rentedFloatBuffers[i];
                        var rent = ArrayPool<float>.Shared.Rent(neededInt);
                        rentedFloatBuffers[i] = rent;
                        try { ArrayPool<float>.Shared.Return(old!, clearArray: false); } catch { }
                    }
                }
                if (rentedIntBuffers != null && rentedIntBuffers.Length > i && rentedIntBuffers[i] != null)
                {
                    if (rentedIntBuffers[i]!.Length < neededInt)
                    {
                        var old = rentedIntBuffers[i];
                        var rent = ArrayPool<int>.Shared.Rent(neededInt);
                        rentedIntBuffers[i] = rent;
                        try { ArrayPool<int>.Shared.Return(old!, clearArray: false); } catch { }
                    }
                }

                // The wavelet data must be reconstructed since we've switched to a different tile.
                // Null only the backing Data array so the wrapper object itself can be reused,
                // avoiding a fresh DataBlkFloat/DataBlkInt allocation per tile per component.
                if (reconstructedComps != null && i < reconstructedComps.Length && reconstructedComps[i] != null)
                {
                    reconstructedComps[i].Data = null;
                }
            }
        }

        /// <summary> Closes this object, releasing any system resources it may be using.
        /// This should be the last method called on an object of this class.
        /// 
        /// </summary>
        public new void Close()
        {
            // Return any rented buffers
            // Use configurable clearing for security vs performance trade-off
            if (rentedFloatBuffers != null)
            {
                for (var i = 0; i < rentedFloatBuffers.Length; i++)
                {
                    var buf = rentedFloatBuffers[i];
                    if (buf != null)
                    {
                        try { ArrayPool<float>.Shared.Return(buf!, clearArray: ClearArrayPoolBuffersOnReturn); } catch { }
                        rentedFloatBuffers[i] = null;
                    }
                }
            }
            if (rentedIntBuffers != null)
            {
                for (var i = 0; i < rentedIntBuffers.Length; i++)
                {
                    var ibuf = rentedIntBuffers[i];
                    if (ibuf != null)
                    {
                        try { ArrayPool<int>.Shared.Return(ibuf!, clearArray: ClearArrayPoolBuffersOnReturn); } catch { }
                        rentedIntBuffers[i] = null;
                    }
                }
            }

            // Call base Close (does nothing, but keep behavior consistent)
            base.Close();
        }
    }
}