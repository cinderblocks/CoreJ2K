/*
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
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.codestream.writer;
using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreJ2K.j2k.entropy.encoder
{

    /// <summary> This implements the EBCOT post compression rate allocation algorithm. This
    /// algorithm finds the most suitable truncation points for the set of
    /// code-blocks, for each layer target bitrate. It works by first collecting
    /// the rate distortion info from all code-blocks, in all tiles and all
    /// components, and then running the rate-allocation on the whole image at
    /// once, for each layer.
    /// 
    /// This implementation also provides some timing features. They can be
    /// enabled by setting the 'DO_TIMING' constant of this class to true and
    /// recompiling. The timing uses the 'System.currentTimeMillis()' Java API
    /// call, which returns wall clock time, not the actual CPU time used. The
    /// timing results will be printed on the message output. Since the times
    /// reported are wall clock times and not CPU usage times they can not be added
    /// to find the total used time (i.e. some time might be counted in several
    /// places). When timing is disabled ('DO_TIMING' is false) there is no penalty
    /// if the compiler performs some basic optimizations. Even if not the penalty
    /// should be negligeable.
    /// 
    /// </summary>
    /// <seealso cref="PostCompRateAllocator" />
    /// <seealso cref="CodedCBlkDataSrcEnc" />
    /// <seealso cref="j2k.codestream.writer.CodestreamWriter" />
    public sealed class EBCOTRateAllocator : PostCompRateAllocator
    {

#if DO_TIMING
		/// <summary>The wall time for the initialization. </summary>
		//private long initTime;
		
		/// <summary>The wall time for the building of layers. </summary>
		//private long buildTime;
		
		/// <summary>The wall time for the writing of layers. </summary>
		//private long writeTime;
#endif

        /// <summary> 5D Array containing all the coded code-blocks:
        /// 
        /// <ul>
        /// <li>1st index: tile index</li>
        /// <li>2nd index: component index</li>
        /// <li>3rd index: resolution level index</li>
        /// <li>4th index: subband index</li>
        /// <li>5th index: code-block index</li>
        /// </ul>
        /// 
        /// </summary>
        private readonly CBlkRateDistStats[][][][][] cblks;

        /// <summary> 6D Array containing the indices of the truncation points. It actually
        /// contains the index of the element in CBlkRateDistStats.truncIdxs that
        /// gives the real truncation point index.
        /// 
        /// <ul>
        /// <li>1st index: tile index</li>
        /// <li>2nd index: layer index</li>
        /// <li>3rd index: component index</li>
        /// <li>4th index: resolution level index</li>
        /// <li>5th index: subband index</li>
        /// <li>6th index: code-block index</li>
        /// </ul>
        /// 
        /// </summary>
        private readonly int[][][][][][] truncIdxs;

        /// <summary> Number of precincts in each resolution level:
        /// 
        /// <ul>
        /// <li>1st dim: tile index.</li>
        /// <li>2nd dim: component index.</li>
        /// <li>3nd dim: resolution level index.</li>
        /// </ul>
        /// 
        /// </summary>
        private readonly Coord[][][] numPrec;

        /// <summary>Array containing the layers information. </summary>
        private EBCOTLayer[] layers = null!;

        /// <summary>The log of 2, natural base </summary>
        private static readonly double LOG2 = Math.Log(2);

        /// <summary>The normalization offset for the R-D summary table </summary>
        private const int RD_SUMMARY_OFF = 24;

        /// <summary>The size of the summary table </summary>
        private const int RD_SUMMARY_SIZE = 64;

        /// <summary>The relative precision for float data. This is the relative tolerance
        /// up to which the layer slope thresholds are calculated. 
        /// </summary>
        private const float FLOAT_REL_PRECISION = 1e-4f;

        /// <summary>The precision for float data type, in an absolute sense. Two float
        /// numbers are considered "equal" if they are within this precision. 
        /// </summary>
        private const float FLOAT_ABS_PRECISION = 1e-10f;

        /// <summary>Minimum average size of a packet. If layer has less bytes than the
        /// this constant multiplied by number of packets in the layer, then the
        /// layer is skipped. 
        /// </summary>
        private const int MIN_AVG_PACKET_SZ = 32;

        /// <summary>The R-D summary information collected from the coding of all
        /// code-blocks. For each entry it contains the accumulated length of all
        /// truncation points that have a slope not less than
        /// '2*(k-RD_SUMMARY_OFF)', where 'k' is the entry index.
        /// 
        /// Therefore, the length at entry 'k' is the total number of bytes of
        /// code-block data that would be obtained if the truncation slope was
        /// chosen as '2*(k-RD_SUMMARY_OFF)', without counting the overhead
        /// associated with the packet heads.
        /// 
        /// This summary is used to estimate the relation of the R-D slope to
        /// coded length, and to obtain absolute minimums on the slope given a
        /// length.  
        /// </summary>
        private readonly int[] RDSlopesRates;

        /// <summary>Packet encoder. </summary>
        private readonly PktEncoder pktEnc;

        /// <summary>The layer specifications </summary>
        private readonly LayersInfo lyrSpec;

        /// <summary>The maximum slope accross all code-blocks and truncation points. </summary>
        private float maxSlope;

        /// <summary>The minimum slope accross all code-blocks and truncation points. </summary>
        private float minSlope;

        /// <summary> Initializes the EBCOT rate allocator of entropy coded data. The layout
        /// of layers, and their bitrate constraints, is specified by the 'lyrs'
        /// parameter.
        /// 
        /// </summary>
        /// <param name="src">The source of entropy coded data.
        /// 
        /// </param>
        /// <param name="lyrs">The layers layout specification.
        /// 
        /// </param>
        /// <param name="writer">The bit stream writer.
        /// 
        /// </param>
        /// <seealso cref="ProgressionType" />
        public EBCOTRateAllocator(CodedCBlkDataSrcEnc src, LayersInfo lyrs, CodestreamWriter writer, EncoderSpecs encSpec, ParameterList pl) : base(src, lyrs.TotNumLayers, writer, encSpec)
        {

            int minsbi, maxsbi;
            int i;
            SubbandAn? sb, sb2;
            Coord? ncblks = null;

            // If we do timing create necessary structures
#if DO_TIMING
			// If we are timing make sure that 'finalize' gets called.
			// CONVERSION PROBLEM?
			// The System.runFinalizersOnExit() method is deprecated in Java
			// 1.2 since it can cause a deadlock in some cases. However, here
			// we use it only for profiling purposes and is disabled in
			// production code.
			initTime = 0L;
			buildTime = 0L;
			writeTime = 0L;
#endif

            // Save the layer specs
            lyrSpec = lyrs;

            //Initialize the size of the RD slope rates array
            RDSlopesRates = new int[RD_SUMMARY_SIZE];

            //Get number of tiles, components
            var nt = src.GetNumTiles();
            var nc = NumComps;

            //Allocate the coded code-blocks and truncation points indexes arrays
            cblks = new CBlkRateDistStats[nt][][][][];
            for (var i2 = 0; i2 < nt; i2++)
            {
                cblks[i2] = new CBlkRateDistStats[nc][][][];
            }
            truncIdxs = new int[nt][][][][][];
            for (var i3 = 0; i3 < nt; i3++)
            {
                truncIdxs[i3] = new int[num_Layers][][][][];
                for (var i4 = 0; i4 < num_Layers; i4++)
                {
                    truncIdxs[i3][i4] = new int[nc][][][];
                }
            }

            int cblkPerSubband; // Number of code-blocks per subband
            int mrl; // Number of resolution levels
            int l; // layer index
            int s; //subband index

            // Used to compute the maximum number of precincts for each resolution
            // level
            int tx0, ty0, tx1, ty1; // Current tile position in the reference grid
            int tcx0, tcy0, tcx1, tcy1; // Current tile position in the domain of
                                        // the image component
            int trx0, try0, trx1, try1; // Current tile position in the reduced
                                        // resolution image domain
            int xrsiz, yrsiz; // Component sub-sampling factors
            Coord? tileI = null;
            Coord? nTiles = null;
            int xsiz, ysiz, x0siz, y0siz;
            int xt0siz, yt0siz;
            int xtsiz, ytsiz;

            var cb0x = src.CbULX;
            var cb0y = src.CbULY;

            src.SetTile(0, 0);
            for (var t = 0; t < nt; t++)
            {
                // Loop on tiles
                nTiles = src.GetNumTiles(nTiles);
                tileI = src.GetTile(tileI);
                x0siz = ImgULX;
                y0siz = ImgULY;
                xsiz = x0siz + ImgWidth;
                ysiz = y0siz + ImgHeight;
                xt0siz = src.TilePartULX;
                yt0siz = src.TilePartULY;
                xtsiz = src.NomTileWidth;
                ytsiz = src.NomTileHeight;

                // Tile's coordinates on the reference grid
                tx0 = (tileI.x == 0) ? x0siz : xt0siz + tileI.x * xtsiz;
                ty0 = (tileI.y == 0) ? y0siz : yt0siz + tileI.y * ytsiz;
                tx1 = (tileI.x != nTiles.x - 1) ? xt0siz + (tileI.x + 1) * xtsiz : xsiz;
                ty1 = (tileI.y != nTiles.y - 1) ? yt0siz + (tileI.y + 1) * ytsiz : ysiz;

                for (var c = 0; c < nc; c++)
                {
                    // loop on components

                    //Get the number of resolution levels
                    sb = src.GetAnSubbandTree(t, c);
                    mrl = sb.resLvl + 1;

                    // Initialize maximum number of precincts per resolution array
                    if (numPrec == null)
                    {
                        var tmpArray = new Coord[nt][][];
                        for (var i5 = 0; i5 < nt; i5++)
                        {
                            tmpArray[i5] = new Coord[nc][];
                        }
                        numPrec = tmpArray;
                    }
                    if (numPrec[t][c] == null)
                    {
                        numPrec[t][c] = new Coord[mrl];
                    }

                    // Subsampling factors
                    xrsiz = src.GetCompSubsX(c);
                    yrsiz = src.GetCompSubsY(c);

                    // Tile's coordinates in the image component domain
                    tcx0 = (int)Math.Ceiling(tx0 / (double)(xrsiz));
                    tcy0 = (int)Math.Ceiling(ty0 / (double)(yrsiz));
                    tcx1 = (int)Math.Ceiling(tx1 / (double)(xrsiz));
                    tcy1 = (int)Math.Ceiling(ty1 / (double)(yrsiz));

                    cblks[t][c] = new CBlkRateDistStats[mrl][][];

                    for (l = 0; l < num_Layers; l++)
                    {
                        truncIdxs[t][l][c] = new int[mrl][][];
                    }

                    for (var r = 0; r < mrl; r++)
                    {
                        // loop on resolution levels

                        // Tile's coordinates in the reduced resolution image
                        // domain
                        trx0 = (int)Math.Ceiling(tcx0 / (double)(1 << (mrl - 1 - r)));
                        try0 = (int)Math.Ceiling(tcy0 / (double)(1 << (mrl - 1 - r)));
                        trx1 = (int)Math.Ceiling(tcx1 / (double)(1 << (mrl - 1 - r)));
                        try1 = (int)Math.Ceiling(tcy1 / (double)(1 << (mrl - 1 - r)));

                        // Calculate the maximum number of precincts for each
                        // resolution level taking into account tile specific
                        // options.
                        double twoppx = encSpec.pss.GetPPX(t, c, r);
                        double twoppy = encSpec.pss.GetPPY(t, c, r);
                        numPrec[t][c][r] = new Coord();
                        if (trx1 > trx0)
                        {
                            numPrec[t][c][r].x = (int)Math.Ceiling((trx1 - cb0x) / twoppx) - (int)Math.Floor((trx0 - cb0x) / twoppx);
                        }
                        else
                        {
                            numPrec[t][c][r].x = 0;
                        }
                        if (try1 > try0)
                        {
                            numPrec[t][c][r].y = (int)Math.Ceiling((try1 - cb0y) / twoppy) - (int)Math.Floor((try0 - cb0y) / twoppy);
                        }
                        else
                        {
                            numPrec[t][c][r].y = 0;
                        }

                        minsbi = (r == 0) ? 0 : 1;
                        maxsbi = (r == 0) ? 1 : 4;

                        cblks[t][c][r] = new CBlkRateDistStats[maxsbi][];
                        for (l = 0; l < num_Layers; l++)
                        {
                            truncIdxs[t][l][c][r] = new int[maxsbi][];
                        }

                        for (s = minsbi; s < maxsbi; s++)
                        {
                            // loop on subbands
                            //Get the number of blocks in the current subband
                            sb2 = (SubbandAn)sb.GetSubbandByIdx(r, s);
                            ncblks = sb2.numCb;
                            cblkPerSubband = ncblks.x * ncblks.y;
                            cblks[t][c][r][s] = new CBlkRateDistStats[cblkPerSubband];

                            for (l = 0; l < num_Layers; l++)
                            {
                                truncIdxs[t][l][c][r][s] = new int[cblkPerSubband];
                                for (i = 0; i < cblkPerSubband; i++)
                                {
                                    truncIdxs[t][l][c][r][s][i] = -1;
                                }
                            }
                        } // End loop on subbands
                    } // End lopp on resolution levels
                } // End loop on components
                if (t != nt - 1)
                {
                    src.NextTile();
                }
            } // End loop on tiles

            //Initialize the packet encoder
            pktEnc = new PktEncoder(src, encSpec, numPrec, pl);

            // The layers array has to be initialized after the constructor since
            // it is needed that the bit stream header has been entirely written
        }

#if DO_TIMING
		/// <summary> Prints the timing information, if collected, and calls 'finalize' on
		/// the super class.
		/// 
		/// </summary>
        ~EBCOTRateAllocator()
        {

            System.Text.StringBuilder sb;

            sb = new System.Text.StringBuilder("EBCOTRateAllocator wall clock times:\n");
            sb.Append("  initialization: ");
            sb.Append(initTime);
            sb.Append(" ms\n");
            sb.Append("  layer building: ");
            sb.Append(buildTime);
            sb.Append(" ms\n");
            sb.Append("  final writing:  ");
            sb.Append(writeTime);
            sb.Append(" ms");
            FacilityManager.GetMsgLogger().printmsg(CoreJ2K.j2k.util.MsgLogger_Fields.INFO, sb.ToString());
        }
#endif
        /// <summary> Runs the rate allocation algorithm and writes the data to the bit
        /// stream writer object provided to the constructor.
        /// 
        /// </summary>
        public override void runAndWrite()
        {
            //Now, run the rate allocation
            WriteLayers();
        }

        // ---- Parallel packet passes ---------------------------------------------------------------------------------------

        /// <summary>The fewest code-blocks a packet pass must cover to be spread over several threads.</summary>
        /// <remarks>Parallel set-up is wasted on a handful of blocks. Tests lower this to 0 to exercise the parallel path on small images.</remarks>
        internal static int MinParallelBlocks = 512;

        /// <summary>Overrides <see cref="MinParallelBlocks"/> for encodes started on the current thread (tests only).</summary>
        [ThreadStatic]
        internal static int? MinParallelBlocksForCurrentThread;

        /// <summary>
        /// Called on the calling thread as each pass over the packets starts, with "simulate" or "write", for the current thread only
        /// (tests cancel or fail an encode at a known point here).
        /// </summary>
        [ThreadStatic]
        internal static Action<string>? BeforePacketPassForCurrentThread;

        private int parallelDegree = 1;

        private int? codestreamByteLimit;

        /// <summary>Collects what the final write keeps; null (and nothing is allocated) unless the caller asked for telemetry.</summary>
        internal sealed class TelemetryCollector
        {
            internal readonly List<CodeBlockTelemetry> Blocks = new List<CodeBlockTelemetry>();
            internal int Packets;
            internal int Layers;
        }

        private TelemetryCollector? telemetry;

        /// <summary>Records the code-blocks and packets of the final write in <paramref name="collector"/>.</summary>
        internal void SetTelemetry(TelemetryCollector collector) => telemetry = collector;

        private const int EocMarkerBytes = 2;

        /// <summary>
        /// Sets the exact number of bytes the whole codestream may take, in place of the one the overall bitrate works out to.
        /// </summary>
        /// <param name="bytes">The limit, headers and EOC marker included.</param>
        internal void SetCodestreamByteLimit(int bytes) => codestreamByteLimit = bytes;

        /// <summary>
        /// Allows packets to be built on up to <paramref name="maxDegreeOfParallelism"/> threads. The packets, and so the codestream,
        /// are identical whatever the degree.
        /// </summary>
        internal void SetMaxDegreeOfParallelism(int maxDegreeOfParallelism) => parallelDegree = Math.Max(1, maxDegreeOfParallelism);

        /// <summary>One packet position of a layer: a precinct of a resolution level of a tile-component.</summary>
        private sealed class PacketJob
        {
            public int Tile, Comp, Res, Precinct;

            /// <summary>The tile-component's subband tree, read once because asking the source for it is not thread-safe.</summary>
            public SubbandAn Root = null!;

            /// <summary>The LL subband of the resolution level, parent of every subband in the packet.</summary>
            public SubbandAn Ll = null!;

            public bool Sop, Eph;

            /// <summary>The number of code-blocks in the precinct, used to start the largest packets first.</summary>
            public int Blocks;
        }

        private PacketJob[]? packetJobs;

        /// <summary>Index in the jobs of precinct 0 of each tile, component and resolution level.</summary>
        private int[][][]? packetJobBase;
        private int[]? packetJobOrder;
        private long packetJobBlocks;

        /// <summary>
        /// Lists every packet position of a layer, in the order the layer is simulated: tile, component, resolution level, precinct.
        /// Asking the source for subband trees is done here, on the calling thread, so the passes never have to.
        /// </summary>
        private PacketJob[] GetPacketJobs()
        {
            if (packetJobs != null) return packetJobs;

            var jobs = new List<PacketJob>();
            var nt = src.GetNumTiles();
            var nc = src.NumComps;
            var jobBase = new int[nt][][];
            for (var t = 0; t < nt; t++)
            {
                jobBase[t] = new int[nc][];
                var sop = string.Equals((string)encSpec.sops.GetTileDef(t), "on", StringComparison.OrdinalIgnoreCase);
                var eph = string.Equals((string)encSpec.ephs.GetTileDef(t), "on", StringComparison.OrdinalIgnoreCase);
                for (var c = 0; c < nc; c++)
                {
                    var root = src.GetAnSubbandTree(t, c);
                    var ll = root;
                    while (ll.subb_LL != null) ll = ll.subb_LL;

                    jobBase[t][c] = new int[root.resLvl + 1];
                    for (var r = 0; r <= root.resLvl; r++)
                    {
                        jobBase[t][c][r] = jobs.Count;
                        var nPrec = numPrec[t][c][r].x * numPrec[t][c][r].y;
                        for (var p = 0; p < nPrec; p++)
                        {
                            var prec = pktEnc.GetPrecInfo(t, c, r, p);
                            var blocks = 0;
                            for (var s = r == 0 ? 0 : 1; s < (r == 0 ? 1 : 4); s++) blocks += prec.nblk[s];

                            jobs.Add(new PacketJob
                            {
                                Tile = t, Comp = c, Res = r, Precinct = p, Root = root, Ll = ll, Sop = sop, Eph = eph, Blocks = blocks,
                            });
                            packetJobBlocks += blocks;
                        }
                        ll = ll.parentband!;
                    }
                }
            }

            // The largest packets first, so that one of them is not left until last. Results are filed by job index, not by completion.
            var order = Enumerable.Range(0, jobs.Count).OrderByDescending(i => jobs[i].Blocks).ToArray();
            packetJobOrder = order;
            packetJobBase = jobBase;
            return packetJobs = jobs.ToArray();
        }

        /// <summary>Sizes marker for a position where there is no packet to write.</summary>
        private const int NoPacket = -1;

        /// <summary>
        /// Builds the packets of one layer for every precinct, with the code-blocks' truncation points chosen by
        /// <paramref name="threshold"/>, and adds up their sizes. The packet encoder's state moves on to the next layer, as it does
        /// when the packets are written; callers that only probe a threshold restore it afterwards. Nothing is written.
        /// </summary>
        /// <param name="sizes">If given, receives each job's packet size in bytes, or <see cref="NoPacket"/>, indexed like the jobs.</param>
        /// <returns>The total size of the layer's packets.</returns>
        private int SimulateLayer(int layerIdx, float threshold, int[]? sizes)
        {
            var jobs = GetPacketJobs();
            var order = packetJobOrder!;
            BeforePacketPassForCurrentThread?.Invoke("simulate");

            int Simulate(int index, PktEncoder.PacketBuffers buffers)
            {
                var job = jobs[index];
                findTruncIndices(layerIdx, job.Comp, job.Res, job.Tile, job.Ll, threshold, job.Precinct);
                CancellationToken.ThrowIfCancellationRequested();

                pktEnc.EncodePacket(buffers, job.Root, layerIdx + 1, job.Comp, job.Res, job.Tile, cblks[job.Tile][job.Comp][job.Res],
                    truncIdxs[job.Tile][layerIdx][job.Comp][job.Res], job.Precinct, simulate: true);
                var size = NoPacket;
                if (buffers.Writable)
                {
                    size = bsWriter.writePacketHead(buffers.Head!.Buffer, buffers.Head.Length, true, job.Sop, job.Eph)
                           + bsWriter.writePacketBody(buffers.Body ?? Array.Empty<byte>(), buffers.BodyLength, true, buffers.RoiInPacket, buffers.RoiLength);
                }
                if (sizes != null) sizes[index] = size;
                return size;
            }

            var total = 0;
            if (!ShouldBuildPacketsInParallel())
            {
                var buffers = new PktEncoder.PacketBuffers();
                foreach (var index in order)
                {
                    var size = Simulate(index, buffers);
                    if (size != NoPacket) total += size;
                }
                return total;
            }

            try
            {
                Parallel.For(0, order.Length,
                    new ParallelOptions { MaxDegreeOfParallelism = parallelDegree, CancellationToken = CancellationToken },
                    () => (Buffers: new PktEncoder.PacketBuffers(), Bytes: 0),
                    (i, _, local) =>
                    {
                        var size = Simulate(order[i], local.Buffers);
                        return size == NoPacket ? local : (local.Buffers, local.Bytes + size);
                    },
                    local => Interlocked.Add(ref total, local.Bytes));
            }
            catch (AggregateException e)
            {
                // Surface the original exception (a configuration error, or cancellation) with its type intact.
                ExceptionDispatchInfo.Capture(e.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
            return total;
        }

        // ---- Writing packets --------------------------------------------------------------------------------------------

        private sealed class PendingPacket
        {
            public int Layer, Comp, Res, Tile, Precinct, Job;
            public bool Sop, Eph;
        }

        /// <summary>A packet built ahead of being written.</summary>
        private sealed class BuiltPacket
        {
            public byte[] Head = Array.Empty<byte>();
            public byte[] Body = Array.Empty<byte>();
            public int BodyLength;
            public bool Writable, RoiInPacket;
            public int RoiLength;
            public List<CodeBlockTelemetry>? Blocks;
        }

        /// <summary>Packets waiting to be built in parallel and written, in writing order; null when packets are written one by one.</summary>
        private List<PendingPacket>? pendingPackets;

        private long pendingBytes;

        /// <summary>Most estimated packet bytes held back at once; bounds the memory the packets built ahead take.</summary>
        private const long MaxPendingBytes = 64L << 20;

        /// <summary>Most packets held back at once.</summary>
        private const int MaxPendingPackets = 1 << 14;

        /// <summary>The size of each packet as simulated, per layer then job; tells how many packets fit in the bytes held back.</summary>
        private int[][]? layerPacketSizes;

        private PktEncoder.PacketBuffers? serialBuffers;

        /// <summary>Whether <see cref="EmitPacket"/> only records the packets' lengths in the PLT data, in the order they will be written.</summary>
        private bool recordingPacketOrder;

        /// <summary>Whether a pass over every packet is worth spreading over several threads.</summary>
        private bool ShouldBuildPacketsInParallel()
            => parallelDegree >= 2 && GetPacketJobs().Length >= 2 && packetJobBlocks >= (MinParallelBlocksForCurrentThread ?? MinParallelBlocks);

        /// <summary>The number of threads to save and restore the packet encoder's state on.</summary>
        private int SaveRestoreDegree() => ShouldBuildPacketsInParallel() ? parallelDegree : 1;

        /// <summary>Called by the progression writers for each packet, in writing order. Either writes it, or queues it.</summary>
        private void EmitPacket(int l, int c, int r, int t, int p, bool sop, bool eph)
        {
            if (recordingPacketOrder)
            {
                // Nothing is written: the packet's simulated length goes into the PLT data, at the place in the tile-part the
                // progression puts the packet. A position that has no packet is not written later either.
                if (p < numPrec[t][c][r].x * numPrec[t][c][r].y)
                {
                    var size = layerPacketSizes![l][packetJobBase![t][c][r] + p];
                    if (size != NoPacket) pltData!.AddPacket(t, size);
                }
                return;
            }

            if (pendingPackets != null)
            {
                var first = packetJobBase![t][c][r];
                if (p < numPrec[t][c][r].x * numPrec[t][c][r].y)
                {
                    var job = first + p;
                    pendingPackets.Add(new PendingPacket { Layer = l, Comp = c, Res = r, Tile = t, Precinct = p, Job = job, Sop = sop, Eph = eph });
                    pendingBytes += Math.Max(0, layerPacketSizes![l][job]);
                    if (pendingBytes >= MaxPendingBytes || pendingPackets.Count >= MaxPendingPackets)
                    {
                        WritePendingPackets();
                    }
                    return;
                }

                // Not a packet the simulation knew about: write what is queued, then fail or succeed exactly as when writing one by one.
                WritePendingPackets();
            }

            WritePacketNow(l, c, r, t, p, sop, eph);
        }

        private void WritePacketNow(int l, int c, int r, int t, int p, bool sop, bool eph)
        {
            var sb = src.GetAnSubbandTree(t, c);
            for (var i = sb.resLvl; i > r; i--)
            {
                sb = sb.subb_LL!;
            }

            findTruncIndices(l, c, r, t, sb, layers[l].rdThreshold, p);

            CancellationToken.ThrowIfCancellationRequested();

            var buffers = serialBuffers ??= new PktEncoder.PacketBuffers();
            buffers.Blocks = telemetry != null ? new List<CodeBlockTelemetry>() : null;
            pktEnc.EncodePacket(buffers, src.GetAnSubbandTree(t, c), l + 1, c, r, t, cblks[t][c][r], truncIdxs[t][l][c][r], p, simulate: false);
            if (buffers.Writable)
            {
                if (telemetry != null)
                {
                    telemetry.Packets++;
                    telemetry.Blocks.AddRange(buffers.Blocks!);
                }
                bsWriter.writePacketHead(buffers.Head!.Buffer, buffers.Head.Length, false, sop, eph);
                bsWriter.writePacketBody(buffers.Body!, buffers.BodyLength, false, buffers.RoiInPacket, buffers.RoiLength);
            }
        }

        /// <summary>
        /// Builds the queued packets on several threads and writes them, in the order they were queued. The packets of one precinct
        /// are built in layer order by one thread, since they share tag-tree state; different precincts are independent.
        /// </summary>
        private void WritePendingPackets()
        {
            var queue = pendingPackets!;
            if (queue.Count == 0) return;
            BeforePacketPassForCurrentThread?.Invoke("write");

            var built = new BuiltPacket[queue.Count];
            var chains = new Dictionary<int, List<int>>();
            for (var i = 0; i < queue.Count; i++)
            {
                if (!chains.TryGetValue(queue[i].Job, out var chain))
                {
                    chains[queue[i].Job] = chain = new List<int>();
                }
                chain.Add(i);
            }

            var jobs = GetPacketJobs();
            var work = chains.OrderByDescending(chain => jobs[chain.Key].Blocks * (long)chain.Value.Count).Select(chain => chain.Value).ToArray();

            void Build(List<int> chain, PktEncoder.PacketBuffers buffers)
            {
                foreach (var index in chain)
                {
                    var packet = queue[index];
                    var job = jobs[packet.Job];
                    findTruncIndices(packet.Layer, packet.Comp, packet.Res, packet.Tile, job.Ll, layers[packet.Layer].rdThreshold, packet.Precinct);
                    CancellationToken.ThrowIfCancellationRequested();

                    buffers.Body = null; // a body is not reused: it is kept until it has been written
                    buffers.Blocks = telemetry != null ? new List<CodeBlockTelemetry>() : null;
                    pktEnc.EncodePacket(buffers, job.Root, packet.Layer + 1, packet.Comp, packet.Res, packet.Tile,
                        cblks[packet.Tile][packet.Comp][packet.Res], truncIdxs[packet.Tile][packet.Layer][packet.Comp][packet.Res],
                        packet.Precinct, simulate: false);

                    var result = new BuiltPacket { Writable = buffers.Writable };
                    if (buffers.Writable)
                    {
                        result.Head = new byte[buffers.Head!.Length];
                        Buffer.BlockCopy(buffers.Head.Buffer, 0, result.Head, 0, result.Head.Length);
                        result.Body = buffers.Body ?? Array.Empty<byte>();
                        result.BodyLength = buffers.BodyLength;
                        result.RoiInPacket = buffers.RoiInPacket;
                        result.RoiLength = buffers.RoiLength;
                        result.Blocks = buffers.Blocks;
                    }
                    built[index] = result;
                }
            }

            try
            {
                Parallel.For(0, work.Length,
                    new ParallelOptions { MaxDegreeOfParallelism = parallelDegree, CancellationToken = CancellationToken },
                    () => new PktEncoder.PacketBuffers(),
                    (i, _, buffers) =>
                    {
                        Build(work[i], buffers);
                        return buffers;
                    },
                    _ => { });
            }
            catch (AggregateException e)
            {
                // Surface the original exception (a configuration error, or cancellation) with its type intact.
                ExceptionDispatchInfo.Capture(e.Flatten().InnerExceptions[0]).Throw();
                throw;
            }

            for (var i = 0; i < queue.Count; i++)
            {
                var packet = built[i];
                if (!packet.Writable) continue;

                // Recorded here, on the calling thread and in writing order, not where the packet was built.
                if (telemetry != null)
                {
                    telemetry.Packets++;
                    telemetry.Blocks.AddRange(packet.Blocks!);
                }
                bsWriter.writePacketHead(packet.Head, packet.Head.Length, false, queue[i].Sop, queue[i].Eph);
                bsWriter.writePacketBody(packet.Body, packet.BodyLength, false, packet.RoiInPacket, packet.RoiLength);
                built[i] = null!; // let the body go as soon as it has been written
            }

            queue.Clear();
            pendingBytes = 0;
        }

        /// <summary> Initializes the layers array. This must be called after the main header
        /// has been entirely written or simulated, so as to take its overhead into
        /// account. This method will get all the code-blocks and then initialize
        /// the target bitrates for each layer, according to the specifications.
        /// 
        /// </summary>
        public override void initialize()
        {
            int n, i, l;
            int ho; // The header overhead (in bytes)
            float np; // The number of pixels divided by the number of bits per byte
            double ls; // Step for log-scale
            double basebytes;
            int lastbytes, newbytes, nextbytes;
            int loopnlyrs;
            int minlsz; // The minimum allowable number of bytes in a layer
            int totenclength;
            int maxpkt;
            var numTiles = src.GetNumTiles();
            var numComps = src.NumComps;
            int numLvls;
            int avgPktLen;
#if DO_TIMING
			long stime = 0L;
#endif
            // Start by getting all the code-blocks, we need this in order to have 
            // an idea of the total encoded bitrate.
            GetAllCodeBlocks();

#if DO_TIMING
				stime = (System.DateTime.Now.Ticks - 621355968000000000) / 10000;
#endif

            // Now get the total encoded length
            totenclength = RDSlopesRates[0]; // all the encoded data
                                             // Make a rough estimation of the packet head overhead, as 2 bytes per
                                             // packet in average (plus EPH / SOP) , and add that to the total
                                             // encoded length
            for (var t = 0; t < numTiles; t++)
            {
                avgPktLen = 2;
                // Add SOP length if set
                if (string.Equals((string)encSpec.sops.GetTileDef(t), "on", StringComparison.OrdinalIgnoreCase))
                {
                    avgPktLen += Markers.SOP_LENGTH;
                }
                // Add EPH length if set
                if (string.Equals((string)encSpec.ephs.GetTileDef(t), "on", StringComparison.OrdinalIgnoreCase))
                {
                    avgPktLen += Markers.EPH_LENGTH;
                }

                for (var c = 0; c < numComps; c++)
                {
                    numLvls = src.GetAnSubbandTree(t, c).resLvl + 1;
                    if (!src.precinctPartitionUsed(c, t))
                    {
                        // Precinct partition is not used so there is only
                        // one packet per resolution level/layer
                        totenclength += num_Layers * avgPktLen * numLvls;
                    }
                    else
                    {
                        // Precinct partition is used so for each
                        // component/tile/resolution level, we get the maximum
                        // number of packets
                        for (var rl = 0; rl < numLvls; rl++)
                        {
                            maxpkt = numPrec[t][c][rl].x * numPrec[t][c][rl].y;
                            totenclength += num_Layers * avgPktLen * maxpkt;
                        }
                    }
                } // End loop on components
            } // End loop on tiles

            // If any layer specifies more than 'totenclength' as its target
            // length then 'totenclength' is used. This is to prevent that
            // estimated layers get excessively large target lengths due to an
            // excessively large target bitrate. At the end the last layer is set
            // to the target length corresponding to the overall target
            // bitrate. Thus, 'totenclength' can not limit the total amount of
            // encoded data, as intended.

            ho = headEnc!.Length;
            np = src.ImgWidth * src.ImgHeight / 8f;

            // SOT marker must be taken into account
            for (var t = 0; t < numTiles; t++)
            {
                headEnc.reset();
                headEnc.encodeTilePartHeader(0, t);
                ho += headEnc.Length;
            }

            layers = new EBCOTLayer[num_Layers];
            for (n = num_Layers - 1; n >= 0; n--)
            {
                layers[n] = new EBCOTLayer();
            }

            minlsz = 0; // To keep compiler happy
            for (var t = 0; t < numTiles; t++)
            {
                for (var c = 0; c < numComps; c++)
                {
                    numLvls = src.GetAnSubbandTree(t, c).resLvl + 1;

                    if (!src.precinctPartitionUsed(c, t))
                    {
                        // Precinct partition is not used
                        minlsz += MIN_AVG_PACKET_SZ * numLvls;
                    }
                    else
                    {
                        // Precinct partition is used
                        for (var rl = 0; rl < numLvls; rl++)
                        {
                            maxpkt = numPrec[t][c][rl].x * numPrec[t][c][rl].y;
                            minlsz += MIN_AVG_PACKET_SZ * maxpkt;
                        }
                    }
                } // End loop on components
            } // End loop on tiles

            // Initialize layers
            n = 0;
            i = 0;
            lastbytes = 0;

            while (n < num_Layers - 1)
            {
                // At an optimized layer
                basebytes = Math.Floor(lyrSpec.GetTargetBitrate(i) * np);
                if (i < lyrSpec.NOptPoints - 1)
                {
                    nextbytes = (int)(lyrSpec.GetTargetBitrate(i + 1) * np);
                    // Limit target length to 'totenclength'
                    if (nextbytes > totenclength)
                        nextbytes = totenclength;
                }
                else
                {
                    nextbytes = 1;
                }
                loopnlyrs = lyrSpec.GetExtraLayers(i) + 1;
                ls = Math.Exp(Math.Log(nextbytes / basebytes) / loopnlyrs);
                layers[n].optimize = true;
                for (l = 0; l < loopnlyrs; l++)
                {
                    newbytes = (int)basebytes - lastbytes - ho;
                    if (newbytes < minlsz)
                    {
                        // Skip layer (too small)
                        basebytes *= ls;
                        num_Layers--;
                        continue;
                    }
                    lastbytes = (int)basebytes - ho;
                    layers[n].maxBytes = lastbytes;
                    basebytes *= ls;
                    n++;
                }
                i++; // Goto next optimization point
            }

            // Ensure minimum size of last layer (this one determines overall
            // bitrate)
            n = num_Layers - 2;
            // A byte limit is exact; a bitrate is rounded to whole bytes and can only be that precise.
            // The EOC marker that ends the codestream comes after the last packet, and is not part of 'ho'.
            nextbytes = (codestreamByteLimit.HasValue ? codestreamByteLimit.Value - EocMarkerBytes : (int)(lyrSpec.TotBitrate * np)) - ho;
            newbytes = nextbytes - ((n >= 0) ? layers[n].maxBytes : 0);
            while (newbytes < minlsz)
            {
                if (num_Layers == 1)
                {
                    if (newbytes <= 0)
                    {
                        throw new ArgumentException(codestreamByteLimit.HasValue
                            ? "The byte limit is too low, given the current bit stream header overhead"
                            : "Overall target bitrate too low, given the current bit stream header overhead");
                    }
                    break;
                }
                // Delete last layer
                num_Layers--;
                n--;
                newbytes = nextbytes - ((n >= 0) ? layers[n].maxBytes : 0);
            }
            // Set last layer to the overall target bitrate
            n++;
            layers[n].maxBytes = nextbytes;
            layers[n].optimize = true;

            // Re-initialize progression order changes if needed Default values
            Progression[] prog1; // prog2 removed
            prog1 = (Progression[])encSpec.pocs.GetDefault();
            var nValidProg = prog1.Length;
            foreach (var prg in prog1)
            {
                if (prg.lye > num_Layers)
                {
                    prg.lye = num_Layers;
                }
            }
            if (nValidProg == 0)
            {
                throw new InvalidOperationException("Unable to initialize rate allocator: No " + "default progression type has been defined.");
            }

            // Tile specific values
            for (var t = 0; t < numTiles; t++)
            {
                if (encSpec.pocs.IsTileSpecified(t))
                {
                    prog1 = (Progression[])encSpec.pocs.GetTileDef(t);
                    nValidProg = prog1.Length;
                    foreach (var prg in prog1)
                    {
                        if (prg.lye > num_Layers)
                        {
                            prg.lye = num_Layers;
                        }
                    }
                    if (nValidProg == 0)
                    {
                        throw new InvalidOperationException(
                            $"Unable to initialize rate allocator: No default progression type has been defined for tile {t}");
                    }
                }
            } // End loop on tiles

            // The layers are built, and the length of every tile-part found, here and not when the packets are written, because the
            // codestream's main header (TLM) and the tile-part headers (SOT) have to be known before anything is written.
            BuildLayers();

#if DO_TIMING
			initTime += (System.DateTime.Now.Ticks - 621355968000000000) / 10000 - stime;
#endif
        }

        /// <summary> This method gets all the coded code-blocks from the EBCOT entropy coder
        /// for every component and every tile. Each coded code-block is stored in
        /// a 5D array according to the component, the resolution level, the tile,
        /// the subband it belongs and its position in the subband.
        /// 
        ///  For each code-block, the valid slopes are computed and converted
        /// into the mantissa-exponent representation.
        /// 
        /// </summary>
        private void GetAllCodeBlocks()
        {

            int numComps, numTiles; // numBytes removed
            int c, r, t, s, sidx, k;
            //int slope;
            SubbandAn? subb;
            CBlkRateDistStats? ccb = null;
            Coord? ncblks = null;
            int last_sidx;
            float fslope;
#if DO_TIMING
			long stime = 0L;
#endif
            maxSlope = 0f;
            minSlope = float.MaxValue;

            //Get the number of components and tiles
            numComps = src.NumComps;
            numTiles = src.GetNumTiles();

            SubbandAn root, sb;
            var cblkToEncode = 0;

            //Get all coded code-blocks Goto first tile
            src.SetTile(0, 0);
            for (t = 0; t < numTiles; t++)
            {
                //loop on tiles
                cblkToEncode = 0;
                for (c = 0; c < numComps; c++)
                {
                    root = src.GetAnSubbandTree(t, c);
                    for (r = 0; r <= root.resLvl; r++)
                    {
                        if (r == 0)
                        {
                            sb = (SubbandAn)root.GetSubbandByIdx(0, 0);
                            if (sb != null)
                                cblkToEncode += sb.numCb.x * sb.numCb.y;
                        }
                        else
                        {
                            sb = (SubbandAn)root.GetSubbandByIdx(r, 1);
                            if (sb != null)
                                cblkToEncode += sb.numCb.x * sb.numCb.y;
                            sb = (SubbandAn)root.GetSubbandByIdx(r, 2);
                            if (sb != null)
                                cblkToEncode += sb.numCb.x * sb.numCb.y;
                            sb = (SubbandAn)root.GetSubbandByIdx(r, 3);
                            if (sb != null)
                                cblkToEncode += sb.numCb.x * sb.numCb.y;
                        }
                    }
                }

                for (c = 0; c < numComps; c++)
                {
                    //loop on components

                    //Get next coded code-block coordinates
                    while ((ccb = src.GetNextCodeBlock(c, ccb)) != null)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
#if DO_TIMING
						stime = (System.DateTime.Now.Ticks - 621355968000000000) / 10000;
#endif

                        subb = ccb.sb;

                        //Get the coded code-block resolution level index
                        r = subb!.resLvl;

                        //Get the coded code-block subband index
                        s = subb.sbandIdx;

                        //Get the number of blocks in the current subband
                        ncblks = subb.numCb;

                        // Add code-block contribution to summary R-D table
                        // RDSlopesRates
                        last_sidx = -1;
                        for (k = ccb.nVldTrunc - 1; k >= 0; k--)
                        {
                            fslope = ccb.truncSlopes![k];
                            if (fslope > maxSlope)
                                maxSlope = fslope;
                            if (fslope < minSlope)
                                minSlope = fslope;
                            sidx = GetLimitedSIndexFromSlope(fslope);
                            for (; sidx > last_sidx; sidx--)
                            {
                                RDSlopesRates[sidx] += ccb.truncRates![ccb.truncIdxs![k]];
                            }
                            last_sidx = GetLimitedSIndexFromSlope(fslope);
                        }

                        //Fills code-blocks array
                        cblks[t][c][r][s][(ccb.m * ncblks!.x) + ccb.n] = ccb;
                        ccb = null;

#if DO_TIMING
						initTime += (System.DateTime.Now.Ticks - 621355968000000000) / 10000 - stime;
#endif
                    }
                }

                CancellationToken.ThrowIfCancellationRequested();
            //Goto next tile
                if (t < numTiles - 1)
                    //not at last tile
                    src.NextTile();
            }
        }

        /// <summary> This method builds all the bit stream layers and then writes them to
        /// the output bit stream. Firstly it builds all the layers by computing
        /// the threshold according to the layer target bit-rate, and then it
        /// writes the layer bit streams according to the progressive type.
        /// 
        /// </summary>
        private void BuildLayers()
        {
            int maxBytes, actualBytes;
            float rdThreshold;
            var nc = src.NumComps;
            var nt = src.GetNumTiles();
#if DO_TIMING
			long stime = 0L;
			stime = (System.DateTime.Now.Ticks - 621355968000000000) / 10000;
#endif

            // Start with the maximum slope
            rdThreshold = maxSlope;

            // TLM SUPPORT: Initialize TLM data collection if enabled
            codestream.metadata.TilePartLengthsData? tlmData = null;
            if (headEnc!.IsTLMEnabled)
            {
                tlmData = new codestream.metadata.TilePartLengthsData();
            }

            // PLT SUPPORT: Initialize PLT data collection if enabled
            pltData = null;
            if (headEnc!.IsPLTEnabled)
            {
                pltData = new codestream.metadata.PacketLengthsData();
            }

            tileLengths = new int[nt];
            layerPacketSizes = new int[num_Layers][];
            actualBytes = 0;

            // +------------------------------+
            // |  First we build the layers   |
            // +------------------------------+
            // Bitstream is simulated to know tile length
            for (var l = 0; l < num_Layers; l++)
            {
                //loop on layers
                CancellationToken.ThrowIfCancellationRequested();

                maxBytes = layers[l].maxBytes;
                if (layers[l].optimize)
                {
                    rdThreshold = optimizeBitstreamLayer(l, rdThreshold, maxBytes, actualBytes);
                }
                else
                {
                    if (l <= 0 || l >= num_Layers - 1)
                    {
                        throw new ArgumentException("The first and the" + " last layer " + "thresholds" + " must be optimized");
                    }
                    rdThreshold = estimateLayerThreshold(maxBytes, layers[l - 1]);
                }

                if (l == 0)
                {
                    for (var t = 0; t < nt; t++)
                    {
                        // Tile header
                        headEnc.reset();
                        headEnc.encodeTilePartHeader(0, t);
                        tileLengths[t] += headEnc.Length;
                    }
                }

                // The layer's packets are built in parallel, but their sizes are added up in the order the layer is simulated.
                var jobs = GetPacketJobs();
                var sizes = new int[jobs.Length];
                SimulateLayer(l, rdThreshold, sizes);
                layerPacketSizes[l] = sizes;
                for (var j = 0; j < jobs.Length; j++)
                {
                    if (sizes[j] == NoPacket) continue;

                    actualBytes += sizes[j];
                    tileLengths[jobs[j].Tile] += sizes[j];
                }
                layers[l].rdThreshold = rdThreshold;
                layers[l].actualBytes = actualBytes;
            } // end loop on layers

            // PLT SUPPORT: The PLT markers list the packets of a tile-part in the order they are written, which is the order of the
            // tile's progression and not the one the layers were simulated in. Walk the progression without writing, to record them.
            if (pltData != null)
            {
                recordingPacketOrder = true;
                try
                {
                    // The position-based progressions read the geometry of the current tile, which writing a tile-part header sets.
                    var numTiles = GetNumTiles(null);
                    for (var t = 0; t < nt; t++)
                    {
                        SetTile(t % numTiles.x, t / numTiles.x);
                        WriteTileProgressions(t);
                    }
                }
                finally
                {
                    recordingPacketOrder = false;
                }

                // The tile-part headers measured while building the layers had no PLT marker yet, but the ones written do, and the
                // tile-part length in SOT (and TLM) must count every byte of the tile-part.
                for (var t = 0; t < nt; t++)
                {
                    tileLengths[t] += HeaderEncoder.GetPLTLength(pltData, t);
                }
            }

            if (tlmData != null)
            {
                for (var t = 0; t < nt; t++)
                {
                    // tileLengths[t] includes the complete tile-part length
                    tlmData.AddTilePart(t, 0, tileLengths[t]);
                }

                // TLM SUPPORT: Pass collected TLM data to header encoder, which writes it in the main header
                headEnc.SetTLMData(tlmData);
            }

#if DO_TIMING
			buildTime += (System.DateTime.Now.Ticks - 621355968000000000) / 10000 - stime;
#endif
        }

        /// <summary>The length of the tile-part of each tile, found by <see cref="BuildLayers"/>.</summary>
        private int[]? tileLengths;

        /// <summary>The lengths of the packets of each tile, found by <see cref="BuildLayers"/> when PLT markers are written.</summary>
        private codestream.metadata.PacketLengthsData? pltData;

        /// <summary>
        /// Walks the packets of a tile in the order of its progression(s), handing each to <see cref="EmitPacket"/>: written, queued to be
        /// built in parallel and written, or only recorded, according to the state the allocator is in.
        /// </summary>
        private void WriteTileProgressions(int t)
        {
            var nc = src.NumComps;
            Progression[] prog; // Progression(s) in each tile
            int cs, ce, rs, re, lye;

            var mrlc = new int[nc];
            //int[][] lysA; // layer index start for each component and
            // resolution level
            var lys = new int[nc][];
            for (var c = 0; c < nc; c++)
            {
                mrlc[c] = src.GetAnSubbandTree(t, c).resLvl;
                lys[c] = new int[mrlc[c] + 1];
            }

            prog = (Progression[])encSpec.pocs.GetTileDef(t);

            foreach (var p in prog)
            {
                // Loop on progression
                lye = p.lye;
                cs = p.cs;
                ce = p.ce;
                rs = p.rs;
                re = p.re;

                switch (p.type)
                {

                    case ProgressionType.RES_LY_COMP_POS_PROG:
                        writeResLyCompPos(t, rs, re, cs, ce, lys, lye);
                        break;

                    case ProgressionType.LY_RES_COMP_POS_PROG:
                        writeLyResCompPos(t, rs, re, cs, ce, lys, lye);
                        break;

                    case ProgressionType.POS_COMP_RES_LY_PROG:
                        writePosCompResLy(t, rs, re, cs, ce, lys, lye);
                        break;

                    case ProgressionType.COMP_POS_RES_LY_PROG:
                        writeCompPosResLy(t, rs, re, cs, ce, lys, lye);
                        break;

                    case ProgressionType.RES_POS_COMP_LY_PROG:
                        writeResPosCompLy(t, rs, re, cs, ce, lys, lye);
                        break;

                    default:
                        throw new InvalidOperationException("Unsupported bit stream progression type");

                } // switch on progression

                // Packets of the progression that were queued to be built in parallel must be written before its layer indices
                // move on, and before the next tile's header.
                if (pendingPackets != null)
                {
                    WritePendingPackets();
                }

                // Update next first layer index 
                for (var c = cs; c < ce; c++)
                    for (var r = rs; r < re; r++)
                    {
                        if (r > mrlc[c])
                            continue;
                        lys[c][r] = lye;
                    }
            }
        }

        /// <summary> Writes the tiles' tile-part headers and packets to the bit stream, according to their progression orders. The main
        /// header must have been written already.
        /// 
        /// </summary>
        private void WriteLayers()
        {
            var nc = src.NumComps;
            var nt = src.GetNumTiles();
            var tileLengths = this.tileLengths!;
#if DO_TIMING
			long stime = 0L;
			stime = (System.DateTime.Now.Ticks - 621355968000000000) / 10000;
#endif
            // PLT SUPPORT: Pass collected PLT data to header encoder. It goes in the tile-part headers; if it was set earlier, the
            // main header would carry it too.
            if (pltData != null)
            {
                headEnc!.SetPLTData(pltData);
            }

            // +--------------------------------------------------+
            // | Write tiles according to their Progression order |
            // +--------------------------------------------------+
            // Reset the packet encoder before writing all packets
            pktEnc.reset();
            if (telemetry != null) telemetry.Layers = num_Layers; // empty trailing layers have been dropped from this count
            pendingPackets = ShouldBuildPacketsInParallel() ? new List<PendingPacket>() : null;
            for (var t = 0; t < nt; t++)
            {
                // Tile header
                headEnc.reset();
                headEnc.encodeTilePartHeader(tileLengths[t], t);
                bsWriter.commitBitstreamHeader(headEnc);

                WriteTileProgressions(t);
            } // End loop on tiles

#if DO_TIMING
			writeTime += (System.DateTime.Now.Ticks - 621355968000000000) / 10000 - stime;
#endif
            pendingPackets = null;
            layerPacketSizes = null;
            this.tileLengths = null;
            pltData = null;

        }

        /// <summary> Write a piece of bit stream according to the
        /// RES_LY_COMP_POS_PROG progression mode and between given bounds
        /// 
        /// </summary>
        /// <param name="t">Tile index.
        /// 
        /// </param>
        /// <param name="rs">First resolution level index.
        /// 
        /// </param>
        /// <param name="re">Last resolution level index.
        /// 
        /// </param>
        /// <param name="cs">First component index.
        /// 
        /// </param>
        /// <param name="ce">Last component index.
        /// 
        /// </param>
        /// <param name="lys">First layer index for each component and resolution.
        /// 
        /// </param>
        /// <param name="lye">Index of the last layer.
        /// 
        /// </param>
        public void writeResLyCompPos(int t, int rs, int re, int cs, int ce, int[][] lys, int lye)
        {

            bool sopUsed; // Should SOP markers be used ?
            bool ephUsed; // Should EPH markers be used ?
            var nc = src.NumComps;
            var mrl = new int[nc];
            var nPrec = 0;

            // Max number of resolution levels in the tile
            var maxResLvl = 0;
            for (var c = 0; c < nc; c++)
            {
                mrl[c] = src.GetAnSubbandTree(t, c).resLvl;
                if (mrl[c] > maxResLvl)
                    maxResLvl = mrl[c];
            }

            int minlys; // minimum layer start index of each component

            for (var r = rs; r < re; r++)
            {
                //loop on resolution levels
                if (r > maxResLvl)
                    continue;

                minlys = 100000;
                for (var c = cs; c < ce; c++)
                {
                    if (r < lys[c].Length && lys[c][r] < minlys)
                    {
                        minlys = lys[c][r];
                    }
                }

                for (var l = minlys; l < lye; l++)
                {
                    //loop on layers
                    for (var c = cs; c < ce; c++)
                    {
                        //loop on components
                        if (r >= lys[c].Length)
                            continue;
                        if (l < lys[c][r])
                            continue;

                        // If no more decomposition levels for this component
                        if (r > mrl[c])
                            continue;

                        nPrec = numPrec[t][c][r].x * numPrec[t][c][r].y;
                        for (var p = 0; p < nPrec; p++)
                        {
                            // loop on precincts

                            // set boolean sopUsed here (SOP markers)
                            sopUsed = ((string)encSpec.sops.GetTileDef(t)).Equals("on");
                            // set boolean ephUsed here (EPH markers)
                            ephUsed = ((string)encSpec.ephs.GetTileDef(t)).Equals("on");

                            EmitPacket(l, c, r, t, p, sopUsed, ephUsed);
                        } // End loop on precincts
                    } // End loop on components
                } // End loop on layers
            } // End loop on resolution levels
        }

        /// <summary> Write a piece of bit stream according to the
        /// LY_RES_COMP_POS_PROG progression mode and between given bounds
        /// 
        /// </summary>
        /// <param name="t">Tile index.
        /// 
        /// </param>
        /// <param name="rs">First resolution level index.
        /// 
        /// </param>
        /// <param name="re">Last resolution level index.
        /// 
        /// </param>
        /// <param name="cs">First component index.
        /// 
        /// </param>
        /// <param name="ce">Last component index.
        /// 
        /// </param>
        /// <param name="lys">First layer index for each component and resolution.
        /// 
        /// </param>
        /// <param name="lye">Index of the last layer.
        /// 
        /// </param>
        public void writeLyResCompPos(int t, int rs, int re, int cs, int ce, int[][] lys, int lye)
        {

            bool sopUsed; // Should SOP markers be used ?
            bool ephUsed; // Should EPH markers be used ?
            var nc = src.NumComps;
            int mrl;
            var nPrec = 0;

            var minlys = 100000; // minimum layer start index of each component
            for (var c = cs; c < ce; c++)
            {
                for (var r = 0; r < lys.Length; r++)
                {
                    if (lys[c] != null && r < lys[c].Length && lys[c][r] < minlys)
                    {
                        minlys = lys[c][r];
                    }
                }
            }

            for (var l = minlys; l < lye; l++)
            {
                // loop on layers
                for (var r = rs; r < re; r++)
                {
                    // loop on resolution level
                    for (var c = cs; c < ce; c++)
                    {
                        // loop on components
                        mrl = src.GetAnSubbandTree(t, c).resLvl;
                        if (r > mrl)
                            continue;
                        if (r >= lys[c].Length)
                            continue;
                        if (l < lys[c][r])
                            continue;

                        nPrec = numPrec[t][c][r].x * numPrec[t][c][r].y;
                        for (var p = 0; p < nPrec; p++)
                        {
                            // loop on precincts

                            // set boolean sopUsed here (SOP markers)
                            sopUsed = ((string)encSpec.sops.GetTileDef(t)).Equals("on");
                            // set boolean ephUsed here (EPH markers)
                            ephUsed = ((string)encSpec.ephs.GetTileDef(t)).Equals("on");

                            EmitPacket(l, c, r, t, p, sopUsed, ephUsed);
                        } // end loop on precincts
                    } // end loop on components
                } // end loop on resolution levels
            } // end loop on layers
        }

        /// <summary> Write a piece of bit stream according to the
        /// COMP_POS_RES_LY_PROG progression mode and between given bounds
        /// 
        /// </summary>
        /// <param name="t">Tile index.
        /// 
        /// </param>
        /// <param name="rs">First resolution level index.
        /// 
        /// </param>
        /// <param name="re">Last resolution level index.
        /// 
        /// </param>
        /// <param name="cs">First component index.
        /// 
        /// </param>
        /// <param name="ce">Last component index.
        /// 
        /// </param>
        /// <param name="lys">First layer index for each component and resolution.
        /// 
        /// </param>
        /// <param name="lye">Index of the last layer.
        /// 
        /// </param>
        public void writePosCompResLy(int t, int rs, int re, int cs, int ce, int[][] lys, int lye)
        {

            bool sopUsed; // Should SOP markers be used ?
            bool ephUsed; // Should EPH markers be used ?
            var nc = src.NumComps;
            int mrl;

            // Computes current tile offset in the reference grid
            var nTiles = src.GetNumTiles(null);
            var tileI = src.GetTile(null);
            var x0siz = src.ImgULX;
            var y0siz = src.ImgULY;
            var xsiz = x0siz + src.ImgWidth;
            var ysiz = y0siz + src.ImgHeight;
            var xt0siz = src.TilePartULX;
            var yt0siz = src.TilePartULY;
            var xtsiz = src.NomTileWidth;
            var ytsiz = src.NomTileHeight;
            var tx0 = (tileI.x == 0) ? x0siz : xt0siz + tileI.x * xtsiz;
            var ty0 = (tileI.y == 0) ? y0siz : yt0siz + tileI.y * ytsiz;
            var tx1 = (tileI.x != nTiles.x - 1) ? xt0siz + (tileI.x + 1) * xtsiz : xsiz;
            var ty1 = (tileI.y != nTiles.y - 1) ? yt0siz + (tileI.y + 1) * ytsiz : ysiz;

            // Get precinct information (number,distance between two consecutive
            // precincts in the reference grid) in each component and resolution
            // level
            PrecInfo prec; // temporary variable
            int p; // Current precinct index
            var gcd_x = 0; // Horiz. distance between 2 precincts in the ref. grid
            var gcd_y = 0; // Vert. distance between 2 precincts in the ref. grid
            var nPrec = 0; // Total number of found precincts
            var nextPrec = new int[ce][]; // Next precinct index in each
                                          // component and resolution level
            var minlys = 100000; // minimum layer start index of each component
            var minx = tx1; // Horiz. offset of the second precinct in the
                            // reference grid
            var miny = ty1; // Vert. offset of the second precinct in the
                            // reference grid. 
            var maxx = tx0; // Max. horiz. offset of precincts in the ref. grid
            var maxy = ty0; // Max. vert. offset of precincts in the ref. grid
            for (var c = cs; c < ce; c++)
            {
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                nextPrec[c] = new int[mrl + 1];
                for (var r = rs; r < re; r++)
                {
                    if (r > mrl)
                        continue;
                    if (r < lys[c].Length && lys[c][r] < minlys)
                    {
                        minlys = lys[c][r];
                    }
                    p = numPrec[t][c][r].y * numPrec[t][c][r].x - 1;
                    for (; p >= 0; p--)
                    {
                        prec = pktEnc.GetPrecInfo(t, c, r, p);
                        if (prec.rgulx != tx0)
                        {
                            if (prec.rgulx < minx)
                                minx = prec.rgulx;
                            if (prec.rgulx > maxx)
                                maxx = prec.rgulx;
                        }
                        if (prec.rguly != ty0)
                        {
                            if (prec.rguly < miny)
                                miny = prec.rguly;
                            if (prec.rguly > maxy)
                                maxy = prec.rguly;
                        }

                        if (nPrec == 0)
                        {
                            gcd_x = prec.rgw;
                            gcd_y = prec.rgh;
                        }
                        else
                        {
                            gcd_x = MathUtil.gcd(gcd_x, prec.rgw);
                            gcd_y = MathUtil.gcd(gcd_y, prec.rgh);
                        }
                        nPrec++;
                    } // precincts
                } // resolution levels
            } // components

            if (nPrec == 0)
            {
                throw new InvalidOperationException("Image cannot have no precinct");
            }

            var pyend = (maxy - miny) / gcd_y + 1;
            var pxend = (maxx - minx) / gcd_x + 1;
            var y = ty0;
            var x = tx0;
            for (var py = 0; py <= pyend; py++)
            {
                // Vertical precincts
                for (var px = 0; px <= pxend; px++)
                {
                    // Horiz. precincts
                    for (var c = cs; c < ce; c++)
                    {
                        // Components
                        mrl = src.GetAnSubbandTree(t, c).resLvl;
                        for (var r = rs; r < re; r++)
                        {
                            // Resolution levels
                            if (r > mrl)
                                continue;
                            if (nextPrec[c][r] >= numPrec[t][c][r].x * numPrec[t][c][r].y)
                            {
                                continue;
                            }
                            prec = pktEnc.GetPrecInfo(t, c, r, nextPrec[c][r]);
                            if ((prec.rgulx != x) || (prec.rguly != y))
                            {
                                continue;
                            }
                            for (var l = minlys; l < lye; l++)
                            {
                                // Layers
                                if (r >= lys[c].Length)
                                    continue;
                                if (l < lys[c][r])
                                    continue;

                                // set boolean sopUsed here (SOP markers)
                                sopUsed = ((string)encSpec.sops.GetTileDef(t)).Equals("on");
                                // set boolean ephUsed here (EPH markers)
                                ephUsed = ((string)encSpec.ephs.GetTileDef(t)).Equals("on");

                                EmitPacket(l, c, r, t, nextPrec[c][r], sopUsed, ephUsed);
                            } // layers
                            nextPrec[c][r]++;
                        } // Resolution levels
                    } // Components
                    if (px != pxend)
                    {
                        x = minx + px * gcd_x;
                    }
                    else
                    {
                        x = tx0;
                    }
                } // Horizontal precincts
                if (py != pyend)
                {
                    y = miny + py * gcd_y;
                }
                else
                {
                    y = ty0;
                }
            } // Vertical precincts

            // Check that all precincts have been written
            for (var c = cs; c < ce; c++)
            {
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                for (var r = rs; r < re; r++)
                {
                    if (r > mrl)
                        continue;
                    if (nextPrec[c][r] < numPrec[t][c][r].x * numPrec[t][c][r].y - 1)
                    {
                        throw new InvalidOperationException(
                            $"JJ2000 bug: One precinct at least has not been written for resolution level {r} of component {c} in tile {t}.");
                    }
                }
            }
        }

        /// <summary> Write a piece of bit stream according to the
        /// COMP_POS_RES_LY_PROG progression mode and between given bounds
        /// 
        /// </summary>
        /// <param name="t">Tile index.
        /// 
        /// </param>
        /// <param name="rs">First resolution level index.
        /// 
        /// </param>
        /// <param name="re">Last resolution level index.
        /// 
        /// </param>
        /// <param name="cs">First component index.
        /// 
        /// </param>
        /// <param name="ce">Last component index.
        /// 
        /// </param>
        /// <param name="lys">First layer index for each component and resolution.
        /// 
        /// </param>
        /// <param name="lye">Index of the last layer.
        /// 
        /// </param>
        public void writeCompPosResLy(int t, int rs, int re, int cs, int ce, int[][] lys, int lye)
        {

            bool sopUsed; // Should SOP markers be used ?
            bool ephUsed; // Should EPH markers be used ?
            var nc = src.NumComps;
            int mrl;

            // Computes current tile offset in the reference grid
            var nTiles = src.GetNumTiles(null);
            var tileI = src.GetTile(null);
            var x0siz = src.ImgULX;
            var y0siz = src.ImgULY;
            var xsiz = x0siz + src.ImgWidth;
            var ysiz = y0siz + src.ImgHeight;
            var xt0siz = src.TilePartULX;
            var yt0siz = src.TilePartULY;
            var xtsiz = src.NomTileWidth;
            var ytsiz = src.NomTileHeight;
            var tx0 = (tileI.x == 0) ? x0siz : xt0siz + tileI.x * xtsiz;
            var ty0 = (tileI.y == 0) ? y0siz : yt0siz + tileI.y * ytsiz;
            var tx1 = (tileI.x != nTiles.x - 1) ? xt0siz + (tileI.x + 1) * xtsiz : xsiz;
            var ty1 = (tileI.y != nTiles.y - 1) ? yt0siz + (tileI.y + 1) * ytsiz : ysiz;

            // Get precinct information (number,distance between two consecutive
            // precincts in the reference grid) in each component and resolution
            // level
            PrecInfo prec; // temporary variable
            int p; // Current precinct index
            var gcd_x = 0; // Horiz. distance between 2 precincts in the ref. grid
            var gcd_y = 0; // Vert. distance between 2 precincts in the ref. grid
            var nPrec = 0; // Total number of found precincts
            var nextPrec = new int[ce][]; // Next precinct index in each
                                          // component and resolution level
            var minlys = 100000; // minimum layer start index of each component
            var minx = tx1; // Horiz. offset of the second precinct in the
                            // reference grid
            var miny = ty1; // Vert. offset of the second precinct in the
                            // reference grid. 
            var maxx = tx0; // Max. horiz. offset of precincts in the ref. grid
            var maxy = ty0; // Max. vert. offset of precincts in the ref. grid
            for (var c = cs; c < ce; c++)
            {
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                for (var r = rs; r < re; r++)
                {
                    if (r > mrl)
                        continue;
                    nextPrec[c] = new int[mrl + 1];
                    if (r < lys[c].Length && lys[c][r] < minlys)
                    {
                        minlys = lys[c][r];
                    }
                    p = numPrec[t][c][r].y * numPrec[t][c][r].x - 1;
                    for (; p >= 0; p--)
                    {
                        prec = pktEnc.GetPrecInfo(t, c, r, p);
                        if (prec.rgulx != tx0)
                        {
                            if (prec.rgulx < minx)
                                minx = prec.rgulx;
                            if (prec.rgulx > maxx)
                                maxx = prec.rgulx;
                        }
                        if (prec.rguly != ty0)
                        {
                            if (prec.rguly < miny)
                                miny = prec.rguly;
                            if (prec.rguly > maxy)
                                maxy = prec.rguly;
                        }

                        if (nPrec == 0)
                        {
                            gcd_x = prec.rgw;
                            gcd_y = prec.rgh;
                        }
                        else
                        {
                            gcd_x = MathUtil.gcd(gcd_x, prec.rgw);
                            gcd_y = MathUtil.gcd(gcd_y, prec.rgh);
                        }
                        nPrec++;
                    } // precincts
                } // resolution levels
            } // components

            if (nPrec == 0)
            {
                throw new InvalidOperationException("Image cannot have no precinct");
            }

            var pyend = (maxy - miny) / gcd_y + 1;
            var pxend = (maxx - minx) / gcd_x + 1;
            int y;
            int x;
            for (var c = cs; c < ce; c++)
            {
                // Loop on components
                y = ty0;
                x = tx0;
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                for (var py = 0; py <= pyend; py++)
                {
                    // Vertical precincts
                    for (var px = 0; px <= pxend; px++)
                    {
                        // Horiz. precincts
                        for (var r = rs; r < re; r++)
                        {
                            // Resolution levels
                            if (r > mrl)
                                continue;
                            if (nextPrec[c][r] >= numPrec[t][c][r].x * numPrec[t][c][r].y)
                            {
                                continue;
                            }
                            prec = pktEnc.GetPrecInfo(t, c, r, nextPrec[c][r]);
                            if ((prec.rgulx != x) || (prec.rguly != y))
                            {
                                continue;
                            }

                            for (var l = minlys; l < lye; l++)
                            {
                                // Layers
                                if (r >= lys[c].Length)
                                    continue;
                                if (l < lys[c][r])
                                    continue;

                                // set boolean sopUsed here (SOP markers)
                                sopUsed = ((string)encSpec.sops.GetTileDef(t)).Equals("on");
                                // set boolean ephUsed here (EPH markers)
                                ephUsed = ((string)encSpec.ephs.GetTileDef(t)).Equals("on");

                                EmitPacket(l, c, r, t, nextPrec[c][r], sopUsed, ephUsed);
                            } // Layers
                            nextPrec[c][r]++;
                        } // Resolution levels                    
                        if (px != pxend)
                        {
                            x = minx + px * gcd_x;
                        }
                        else
                        {
                            x = tx0;
                        }
                    } // Horizontal precincts
                    if (py != pyend)
                    {
                        y = miny + py * gcd_y;
                    }
                    else
                    {
                        y = ty0;
                    }
                } // Vertical precincts
            } // components

            // Check that all precincts have been written
            for (var c = cs; c < ce; c++)
            {
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                for (var r = rs; r < re; r++)
                {
                    if (r > mrl)
                        continue;
                    if (nextPrec[c][r] < numPrec[t][c][r].x * numPrec[t][c][r].y - 1)
                    {
                        throw new InvalidOperationException(
                            $"JJ2000 bug: One precinct at least has not been written for resolution level {r} of component {c} in tile {t}.");
                    }
                }
            }
        }

        /// <summary> Write a piece of bit stream according to the
        /// RES_POS_COMP_LY_PROG progression mode and between given bounds
        /// 
        /// </summary>
        /// <param name="t">Tile index.
        /// 
        /// </param>
        /// <param name="rs">First resolution level index.
        /// 
        /// </param>
        /// <param name="re">Last resolution level index.
        /// 
        /// </param>
        /// <param name="cs">First component index.
        /// 
        /// </param>
        /// <param name="ce">Last component index.
        /// 
        /// </param>
        /// <param name="lys">First layer index for each component and resolution.
        /// 
        /// </param>
        /// <param name="lye">Last layer index.
        /// 
        /// </param>
        public void writeResPosCompLy(int t, int rs, int re, int cs, int ce, int[][] lys, int lye)
        {

            bool sopUsed; // Should SOP markers be used ?
            bool ephUsed; // Should EPH markers be used ?
            var nc = src.NumComps;
            int mrl;

            // Computes current tile offset in the reference grid
            var nTiles = src.GetNumTiles(null);
            var tileI = src.GetTile(null);
            var x0siz = src.ImgULX;
            var y0siz = src.ImgULY;
            var xsiz = x0siz + src.ImgWidth;
            var ysiz = y0siz + src.ImgHeight;
            var xt0siz = src.TilePartULX;
            var yt0siz = src.TilePartULY;
            var xtsiz = src.NomTileWidth;
            var ytsiz = src.NomTileHeight;
            var tx0 = (tileI.x == 0) ? x0siz : xt0siz + tileI.x * xtsiz;
            var ty0 = (tileI.y == 0) ? y0siz : yt0siz + tileI.y * ytsiz;
            var tx1 = (tileI.x != nTiles.x - 1) ? xt0siz + (tileI.x + 1) * xtsiz : xsiz;
            var ty1 = (tileI.y != nTiles.y - 1) ? yt0siz + (tileI.y + 1) * ytsiz : ysiz;

            // Get precinct information (number,distance between two consecutive
            // precincts in the reference grid) in each component and resolution
            // level
            PrecInfo prec; // temporary variable
            int p; // Current precinct index
            var gcd_x = 0; // Horiz. distance between 2 precincts in the ref. grid
            var gcd_y = 0; // Vert. distance between 2 precincts in the ref. grid
            var nPrec = 0; // Total number of found precincts
            var nextPrec = new int[ce][]; // Next precinct index in each
                                          // component and resolution level
            var minlys = 100000; // minimum layer start index of each component
            var minx = tx1; // Horiz. offset of the second precinct in the
                            // reference grid
            var miny = ty1; // Vert. offset of the second precinct in the
                            // reference grid. 
            var maxx = tx0; // Max. horiz. offset of precincts in the ref. grid
            var maxy = ty0; // Max. vert. offset of precincts in the ref. grid
            for (var c = cs; c < ce; c++)
            {
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                nextPrec[c] = new int[mrl + 1];
                for (var r = rs; r < re; r++)
                {
                    if (r > mrl)
                        continue;
                    if (r < lys[c].Length && lys[c][r] < minlys)
                    {
                        minlys = lys[c][r];
                    }
                    p = numPrec[t][c][r].y * numPrec[t][c][r].x - 1;
                    for (; p >= 0; p--)
                    {
                        prec = pktEnc.GetPrecInfo(t, c, r, p);
                        if (prec.rgulx != tx0)
                        {
                            if (prec.rgulx < minx)
                                minx = prec.rgulx;
                            if (prec.rgulx > maxx)
                                maxx = prec.rgulx;
                        }
                        if (prec.rguly != ty0)
                        {
                            if (prec.rguly < miny)
                                miny = prec.rguly;
                            if (prec.rguly > maxy)
                                maxy = prec.rguly;
                        }

                        if (nPrec == 0)
                        {
                            gcd_x = prec.rgw;
                            gcd_y = prec.rgh;
                        }
                        else
                        {
                            gcd_x = MathUtil.gcd(gcd_x, prec.rgw);
                            gcd_y = MathUtil.gcd(gcd_y, prec.rgh);
                        }
                        nPrec++;
                    } // precincts
                } // resolution levels
            } // components

            if (nPrec == 0)
            {
                throw new InvalidOperationException("Image cannot have no precinct");
            }

            var pyend = (maxy - miny) / gcd_y + 1;
            var pxend = (maxx - minx) / gcd_x + 1;
            int x, y;
            for (var r = rs; r < re; r++)
            {
                // Resolution levels
                y = ty0;
                x = tx0;
                for (var py = 0; py <= pyend; py++)
                {
                    // Vertical precincts
                    for (var px = 0; px <= pxend; px++)
                    {
                        // Horiz. precincts
                        for (var c = cs; c < ce; c++)
                        {
                            // Components
                            mrl = src.GetAnSubbandTree(t, c).resLvl;
                            if (r > mrl)
                                continue;
                            if (nextPrec[c][r] >= numPrec[t][c][r].x * numPrec[t][c][r].y)
                            {
                                continue;
                            }
                            prec = pktEnc.GetPrecInfo(t, c, r, nextPrec[c][r]);
                            if ((prec.rgulx != x) || (prec.rguly != y))
                            {
                                continue;
                            }
                            for (var l = minlys; l < lye; l++)
                            {
                                if (r >= lys[c].Length)
                                    continue;
                                if (l < lys[c][r])
                                    continue;

                                // set boolean sopUsed here (SOP markers)
                                sopUsed = ((string)encSpec.sops.GetTileDef(t)).Equals("on");
                                // set boolean ephUsed here (EPH markers)
                                ephUsed = ((string)encSpec.ephs.GetTileDef(t)).Equals("on");

                                EmitPacket(l, c, r, t, nextPrec[c][r], sopUsed, ephUsed);
                            } // layers
                            nextPrec[c][r]++;
                        } // Components
                        if (px != pxend)
                        {
                            x = minx + px * gcd_x;
                        }
                        else
                        {
                            x = tx0;
                        }
                    } // Horizontal precincts
                    if (py != pyend)
                    {
                        y = miny + py * gcd_y;
                    }
                    else
                    {
                        y = ty0;
                    }
                } // Vertical precincts
            } // Resolution levels

            // Check that all precincts have been written
            for (var c = cs; c < ce; c++)
            {
                mrl = src.GetAnSubbandTree(t, c).resLvl;
                for (var r = rs; r < re; r++)
                {
                    if (r > mrl)
                        continue;
                    if (nextPrec[c][r] < numPrec[t][c][r].x * numPrec[t][c][r].y - 1)
                    {
                        throw new InvalidOperationException(
                            $"JJ2000 bug: One precinct at least has not been written for resolution level {r} of component {c} in tile {t}.");
                    }
                }
            }
        }

        /// <summary> This function implements the rate-distortion optimization algorithm.
        /// It saves the state of any previously generated bit-stream layers and
        /// then simulate the formation of a new layer in the bit stream as often
        /// as necessary to find the smallest rate-distortion threshold such that
        /// the total number of bytes required to represent the layer does not
        /// exceed `maxBytes' minus `prevBytes'.  It then restores the state of any
        /// previously generated bit-stream layers and returns the threshold.
        /// 
        /// </summary>
        /// <param name="layerIdx">The index of the current layer
        /// 
        /// </param>
        /// <param name="fmaxt">The maximum admissible slope value. Normally the threshold
        /// slope of the previous layer.
        /// 
        /// </param>
        /// <param name="maxBytes">The maximum number of bytes that can be written. It
        /// includes the length of the current layer bistream length and all the
        /// previous layers bit streams.
        /// 
        /// </param>
        /// <param name="prevBytes">The number of bytes of all the previous layers.
        /// 
        /// </param>
        /// <returns> The value of the slope threshold.
        /// 
        /// </returns>
        private float optimizeBitstreamLayer(int layerIdx, float fmaxt, int maxBytes, int prevBytes)
        {

            int actualBytes; // Actual number of bytes for a layer
            float fmint; // Minimum of the current threshold interval
            float ft; // Current threshold
            int sidx; // The index in the summary table

            pktEnc.Save(SaveRestoreDegree());

            // Estimate the minimum slope to start with from the summary
            // information in 'RDSlopesRates'. This is a real minimum since it
            // does not include the packet head overhead, which is always
            // non-zero.

            // Look for the summary entry that gives 'maxBytes' or more data
            for (sidx = RD_SUMMARY_SIZE - 1; sidx > 0; sidx--)
            {
                if (RDSlopesRates[sidx] >= maxBytes)
                {
                    break;
                }
            }
            // Get the corresponding minimum slope
            fmint = GetSlopeFromSIndex(sidx);
            // Ensure that it is smaller the maximum slope
            if (fmint >= fmaxt)
            {
                sidx--;
                fmint = GetSlopeFromSIndex(sidx);
            }
            // If we are using the last entry of the summary, then that
            // corresponds to all the data, Thus, set the minimum slope to 0.
            if (sidx <= 0)
                fmint = 0;

            // We look for the best threshold 'ft', which is the lowest threshold
            // that generates no more than 'maxBytes' code bytes.

            // The search is done iteratively using a binary split algorithm. We
            // start with 'fmaxt' as the maximum possible threshold, and 'fmint'
            // as the minimum threshold. The threshold 'ft' is calculated as the
            // middle point of 'fmaxt'-'fmint' interval. The 'fmaxt' or 'fmint'
            // bounds are moved according to the number of bytes obtained from a
            // simulation, where 'ft' is used as the threshold.

            // We stop whenever the interval is sufficiently small, and thus
            // enough precision is achieved.

            // Initialize threshold as the middle point of the interval.
            ft = (fmaxt + fmint) / 2f;
            // If 'ft' reaches 'fmint' it means that 'fmaxt' and 'fmint' are so
            // close that the average is 'fmint', due to rounding. Force it to
            // 'fmaxt' instead, since 'fmint' is normally an exclusive lower
            // bound.
            if (ft <= fmint)
                ft = fmaxt;

            do
            {
                // Get the number of bytes used by this layer, if 'ft' is the
                // threshold, by simulation.
                src.SetTile(0, 0);
                actualBytes = prevBytes + SimulateLayer(layerIdx, ft, null);

                // Move the interval bounds according to simulation result
                if (actualBytes > maxBytes)
                {
                    // 'ft' is too low and generates too many bytes, make it the
                    // new minimum.
                    fmint = ft;
                }
                else
                {
                    // 'ft' is too high and does not generate as many bytes as we
                    // are allowed too, make it the new maximum.
                    fmaxt = ft;
                }

                // Update 'ft' for the new iteration as the middle point of the
                // new interval.
                ft = (fmaxt + fmint) / 2f;
                // If 'ft' reaches 'fmint' it means that 'fmaxt' and 'fmint' are
                // so close that the average is 'fmint', due to rounding. Force it
                // to 'fmaxt' instead, since 'fmint' is normally an exclusive
                // lower bound.
                if (ft <= fmint)
                    ft = fmaxt;

                pktEnc.Restore(SaveRestoreDegree());

                // We continue to iterate, until the threshold reaches the upper
                // limit of the interval, within a FLOAT_REL_PRECISION relative
                // tolerance, or a FLOAT_ABS_PRECISION absolute tolerance. This is
                // the sign that the interval is sufficiently small.
            }
            while (ft < fmaxt * (1f - FLOAT_REL_PRECISION) && ft < (fmaxt - FLOAT_ABS_PRECISION));

            // If we have a threshold which is close to 0, set it to 0 so that
            // everything is taken into the layer. This is to avoid not sending
            // some least significant bit-planes in the lossless case. We use the
            // FLOAT_ABS_PRECISION value as a measure of "close" to 0.
            ft = ft <= FLOAT_ABS_PRECISION ? 0f :
                // Otherwise make the threshold 'fmaxt', just to be sure that we
                // will not send more bytes than allowed.
                fmaxt;
            return ft;
        }

        /// <summary> This function attempts to estimate a rate-distortion slope threshold
        /// which will achieve a target number of code bytes close the
        /// `targetBytes' value.
        /// 
        /// </summary>
        /// <param name="targetBytes">The target number of bytes for the current layer
        /// 
        /// </param>
        /// <param name="lastLayer">The previous layer information.
        /// 
        /// </param>
        /// <returns> The value of the slope threshold for the estimated layer
        /// 
        /// </returns>
        private float estimateLayerThreshold(int targetBytes, EBCOTLayer lastLayer)
        {
            float log_sl1; // The log of the first slope used for interpolation
            float log_sl2; // The log of the second slope used for interpolation
            float log_len1; // The log of the first length used for interpolation
            float log_len2; // The log of the second length used for interpolation
            float log_isl; // The log of the interpolated slope
            float log_ilen; // Log of the interpolated length
            float log_ab; // Log of actual bytes in last layer
            int sidx; // Index into the summary R-D info array
            float log_off; // The log of the offset proportion
            int tlen; // The corrected target layer length
            float lthresh; // The threshold of the last layer
            float eth; // The estimated threshold

            // In order to estimate the threshold we base ourselves in the summary
            // R-D info in RDSlopesRates. In order to use it we must compensate
            // for the overhead of the packet heads. The proportion of overhead is
            // estimated using the last layer simulation results.

            // NOTE: the model used in this method is that the slope varies
            // linearly with the log of the rate (i.e. length).

            // NOTE: the model used in this method is that the distortion is
            // proprotional to a power of the rate. Thus, the slope is also
            // proportional to another power of the rate. This translates as the
            // log of the slope varies linearly with the log of the rate, which is
            // what we use.

            // 1) Find the offset of the length predicted from the summary R-D
            // information, to the actual length by using the last layer.

            // We ensure that the threshold we use for estimation actually
            // includes some data.
            lthresh = lastLayer.rdThreshold;
            if (lthresh > maxSlope)
                lthresh = maxSlope;
            // If the slope of the last layer is too small then we just include
            // all the rest (not possible to do better).
            if (lthresh < FLOAT_ABS_PRECISION)
                return 0f;
            sidx = GetLimitedSIndexFromSlope(lthresh);
            // If the index is outside of the summary info array use the last two, 
            // or first two, indexes, as appropriate
            if (sidx >= RD_SUMMARY_SIZE - 1)
                sidx = RD_SUMMARY_SIZE - 2;

            // Get the logs of the lengths and the slopes

            if (RDSlopesRates[sidx + 1] == 0)
            {
                // Pathological case, we can not use log of 0. Add
                // RDSlopesRates[sidx]+1 bytes to the rates (just a crude simple
                // solution to this rare case)
                log_len1 = (float)Math.Log((RDSlopesRates[sidx] << 1) + 1);
                log_len2 = (float)Math.Log(RDSlopesRates[sidx] + 1);
                log_ab = (float)Math.Log(lastLayer.actualBytes + RDSlopesRates[sidx] + 1);
            }
            else
            {
                log_len1 = (float)Math.Log(RDSlopesRates[sidx]);
                log_len2 = (float)Math.Log(RDSlopesRates[sidx + 1]);
                log_ab = (float)Math.Log(lastLayer.actualBytes);
            }

            log_sl1 = (float)Math.Log(GetSlopeFromSIndex(sidx));
            log_sl2 = (float)Math.Log(GetSlopeFromSIndex(sidx + 1));

            log_isl = (float)Math.Log(lthresh);

            log_ilen = log_len1 + (log_isl - log_sl1) * (log_len1 - log_len2) / (log_sl1 - log_sl2);

            log_off = log_ab - log_ilen;

            // Do not use negative offsets (i.e. offset proportion larger than 1)
            // since that is probably a sign that our model is off. To be
            // conservative use an offset of 0 (i.e. offset proportiojn 1).
            if (log_off < 0)
                log_off = 0f;

            // 2) Correct the target layer length by the offset.

            tlen = (int)(targetBytes / (float)Math.Exp(log_off));

            // 3) Find, from the summary R-D info, the thresholds that generate
            // lengths just above and below our corrected target layer length.

            // Look for the index in the summary info array that gives the largest 
            // length smaller than the target length
            for (sidx = RD_SUMMARY_SIZE - 1; sidx >= 0; sidx--)
            {
                if (RDSlopesRates[sidx] >= tlen)
                    break;
            }
            sidx++;
            // Correct if out of the array
            if (sidx >= RD_SUMMARY_SIZE)
                sidx = RD_SUMMARY_SIZE - 1;
            if (sidx <= 0)
                sidx = 1;

            // Get the log of the lengths and the slopes that are just above and
            // below the target length.

            if (RDSlopesRates[sidx] == 0)
            {
                // Pathological case, we can not use log of 0. Add
                // RDSlopesRates[sidx-1]+1 bytes to the rates (just a crude simple 
                // solution to this rare case)
                log_len1 = (float)Math.Log(RDSlopesRates[sidx - 1] + 1);
                log_len2 = (float)Math.Log((RDSlopesRates[sidx - 1] << 1) + 1);
                log_ilen = (float)Math.Log(tlen + RDSlopesRates[sidx - 1] + 1);
            }
            else
            {
                // Normal case, we can safely take the logs.
                log_len1 = (float)Math.Log(RDSlopesRates[sidx]);
                log_len2 = (float)Math.Log(RDSlopesRates[sidx - 1]);
                log_ilen = (float)Math.Log(tlen);
            }

            log_sl1 = (float)Math.Log(GetSlopeFromSIndex(sidx));
            log_sl2 = (float)Math.Log(GetSlopeFromSIndex(sidx - 1));

            // 4) Interpolate the two thresholds to find the target threshold.

            log_isl = log_sl1 + (log_ilen - log_len1) * (log_sl1 - log_sl2) / (log_len1 - log_len2);

            eth = (float)Math.Exp(log_isl);

            // Correct out of bounds results
            if (eth > lthresh)
                eth = lthresh;
            if (eth < FLOAT_ABS_PRECISION)
                eth = 0f;

            // Return the estimated threshold
            return eth;
        }

        /// <summary> This function finds the new truncation points indices for a packet. It
        /// does so by including the data from the code-blocks in the component,
        /// resolution level and tile, associated with a R-D slope which is larger
        /// than or equal to 'fthresh'.
        /// 
        /// </summary>
        /// <param name="layerIdx">The index of the current layer
        /// 
        /// </param>
        /// <param name="compIdx">The index of the current component
        /// 
        /// </param>
        /// <param name="lvlIdx">The index of the current resolution level
        /// 
        /// </param>
        /// <param name="tileIdx">The index of the current tile
        /// 
        /// </param>
        /// <param name="subb">The LL subband in the resolution level lvlIdx, which is
        /// parent of all the subbands in the packet. Except for resolution level 0
        /// this subband is always a node.
        /// 
        /// </param>
        /// <param name="fthresh">The value of the rate-distortion threshold
        /// 
        /// </param>
        private void findTruncIndices(int layerIdx, int compIdx, int lvlIdx, int tileIdx, SubbandAn? subb, float fthresh, int precinctIdx)
        {
            CancellationToken.ThrowIfCancellationRequested();
            int minsbi, maxsbi, b, n; // bIdx removed
                                      //Coord ncblks = null;
            SubbandAn? sb;
            CBlkRateDistStats cur_cblk;
            var prec = pktEnc.GetPrecInfo(tileIdx, compIdx, lvlIdx, precinctIdx);
            Coord cbCoord;

            sb = subb;
            while (sb!.subb_HH != null)
            {
                sb = sb!.subb_HH;
            }
            minsbi = (lvlIdx == 0) ? 0 : 1;
            maxsbi = (lvlIdx == 0) ? 1 : 4;

            int yend, xend;

            sb = (SubbandAn)subb!.GetSubbandByIdx(lvlIdx, minsbi);
            for (var s = minsbi; s < maxsbi; s++)
            {
                //loop on subbands
                yend = (prec.cblk[s] != null) ? prec.cblk[s].Length : 0;
                for (var y = 0; y < yend; y++)
                {
                    xend = (prec.cblk[s][y] != null) ? prec.cblk[s][y].Length : 0;
                    for (var x = 0; x < xend; x++)
                    {
                        cbCoord = prec.cblk[s][y][x].idx;
                        b = cbCoord.x + cbCoord.y * sb.numCb.x;

                        //Get the current code-block
                        cur_cblk = cblks[tileIdx][compIdx][lvlIdx][s][b];
                        for (n = 0; n < cur_cblk.nVldTrunc; n++)
                        {
                            // A threshold of 0 takes everything into the layer (the search sets it so for the layer that has all the
                            // data, to send every bit-plane of a lossless encode). The last point of a lossless block is kept valid
                            // whatever its slope, and the distortion estimate can make that slope negative, so it must not be
                            // compared with the threshold then.
                            if (fthresh > 0f && cur_cblk.truncSlopes[n] < fthresh)
                            {
                                break;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        // Store the index in the code-block truncIdxs that gives
                        // the real truncation index.
                        truncIdxs[tileIdx][layerIdx][compIdx][lvlIdx][s][b] = n - 1;
                    } // End loop on horizontal code-blocks
                } // End loop on vertical code-blocks
                sb = (SubbandAn)sb.nextSubband();
            } // End loop on subbands
        }

        /// <summary> Returns the index of a slope for the summary table, limiting to the
        /// admissible values. The index is calculated as RD_SUMMARY_OFF plus the
        /// maximum exponent, base 2, that yields a value not larger than the slope
        /// itself.
        /// 
        /// If the value to return is lower than 0, 0 is returned. If it is
        /// larger than the maximum table index, then the maximum is returned.
        /// 
        /// </summary>
        /// <param name="slope">The slope value
        /// 
        /// </param>
        /// <returns> The index for the summary table of the slope.
        /// 
        /// </returns>
        private static int GetLimitedSIndexFromSlope(float slope)
        {
            int idx;

            idx = (int)Math.Floor(Math.Log(slope) / LOG2) + RD_SUMMARY_OFF;

            if (idx < 0)
            {
                return 0;
            }
            else if (idx >= RD_SUMMARY_SIZE)
            {
                return RD_SUMMARY_SIZE - 1;
            }
            else
            {
                return idx;
            }
        }

        /// <summary> Returns the minimum slope value associated with a summary table
        /// index. This minimum slope is just 2^(index-RD_SUMMARY_OFF).
        /// 
        /// </summary>
        /// <param name="index">The summary index value.
        /// 
        /// </param>
        /// <returns> The minimum slope value associated with a summary table index.
        /// 
        /// </returns>
        private static float GetSlopeFromSIndex(int index)
        {
            return (float)Math.Pow(2, (index - RD_SUMMARY_OFF));
        }
    }
}