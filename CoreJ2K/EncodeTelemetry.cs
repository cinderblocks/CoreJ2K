// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using CoreJ2K.j2k.encoder;

namespace CoreJ2K
{
    /// <summary>
    /// What an encode kept, reported to the callback given to <see cref="Configuration.J2KEncoderConfiguration.WithTelemetry"/> once
    /// the output is final: one record per code-block per quality layer, and the bytes of each part of the output, each counted where
    /// it was written.
    /// </summary>
    /// <remarks>
    /// The numbers are the raw ones the rate allocator worked with, for accounting outside the codec (for example how much of the
    /// output went to a face region). Nothing is derived by subtraction:
    /// <c>HeaderBytes + PacketHeaderBytes + PacketBodyBytes + EndOfCodestreamBytes == CodestreamBytes</c> and
    /// <c>CodestreamBytes + ContainerBytes == TotalBytes</c> are checks, not definitions.
    /// </remarks>
    public sealed class EncodeTelemetry
    {
        internal EncodeTelemetry(IReadOnlyList<CodeBlockTelemetry> codeBlocks, int layerCount, int packetCount, long headerBytes,
            long packetHeaderBytes, long packetBodyBytes, int endOfCodestreamBytes, int codestreamBytes, int containerBytes)
        {
            CodeBlocks = codeBlocks;
            LayerCount = layerCount;
            PacketCount = packetCount;
            HeaderBytes = headerBytes;
            PacketHeaderBytes = packetHeaderBytes;
            PacketBodyBytes = packetBodyBytes;
            EndOfCodestreamBytes = endOfCodestreamBytes;
            CodestreamBytes = codestreamBytes;
            ContainerBytes = containerBytes;
        }

        /// <summary>The code-blocks that contributed bytes to a layer, in the order their packets were written.</summary>
        public IReadOnlyList<CodeBlockTelemetry> CodeBlocks { get; }

        /// <summary>The number of quality layers.</summary>
        public int LayerCount { get; }

        /// <summary>The number of packets written (empty ones included).</summary>
        public int PacketCount { get; }

        /// <summary>The codestream's main header and tile-part headers, markers included.</summary>
        public long HeaderBytes { get; }

        /// <summary>The packet headers, including any SOP and EPH markers.</summary>
        public long PacketHeaderBytes { get; }

        /// <summary>The packet bodies: the coded data of the code-blocks. The sum of <see cref="CodeBlockTelemetry.BytesInLayer"/>.</summary>
        public long PacketBodyBytes { get; }

        /// <summary>The end-of-codestream marker.</summary>
        public int EndOfCodestreamBytes { get; }

        /// <summary>The whole codestream.</summary>
        public int CodestreamBytes { get; }

        /// <summary>The JP2 boxes around the codestream, metadata included; 0 for a bare codestream.</summary>
        public int ContainerBytes { get; }

        /// <summary>The whole output.</summary>
        public long TotalBytes => (long)CodestreamBytes + ContainerBytes;
    }

    /// <summary>
    /// One code-block's contribution to one quality layer. With a single layer (the usual case under a byte limit) the "in layer" and
    /// cumulative figures are the same.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distortion is in the allocator's units: the squared error the passes remove, in the quantizer's step-normalised domain, times
    /// <see cref="DistortionScale"/>. The scale is 1 unless a distortion weight or a region of interest applies to the block.
    /// <see cref="UnscaledDistortionReduction"/> divides it out.
    /// </para>
    /// <para>
    /// Passes are numbered from 0 in coding order. Pass <c>i</c> ends at <c>PassEndBytes[i]</c> bytes into the block's data. To place
    /// a pass in a bit-plane: the first pass is the cleanup of bit-plane <c>MagnitudeBits - MissingMsbs - 1</c>, and every later group
    /// of three passes (significance, refinement, cleanup) takes the next lower bit-plane.
    /// </para>
    /// </remarks>
    public sealed class CodeBlockTelemetry
    {
        internal CodeBlockTelemetry(int tile, int component, int resolutionLevel, WaveletSubband subband, int layer,
            int blockColumn, int blockRow, int x, int y, int width, int height, int subbandWidth, int subbandHeight,
            int magnitudeBits, int missingMsbs, int totalPasses, int passesIncluded, int passesInLayer, int bytesInLayer,
            int[] passEndBytes, double distortionReduction, double distortionScale, int roiCoefficients, int roiPasses)
        {
            Tile = tile;
            Component = component;
            ResolutionLevel = resolutionLevel;
            Subband = subband;
            Layer = layer;
            BlockColumn = blockColumn;
            BlockRow = blockRow;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            SubbandWidth = subbandWidth;
            SubbandHeight = subbandHeight;
            MagnitudeBits = magnitudeBits;
            MissingMsbs = missingMsbs;
            TotalPasses = totalPasses;
            PassesIncluded = passesIncluded;
            PassesInLayer = passesInLayer;
            BytesInLayer = bytesInLayer;
            PassEndBytes = passEndBytes;
            DistortionReduction = distortionReduction;
            DistortionScale = distortionScale;
            RoiCoefficients = roiCoefficients;
            RoiPasses = roiPasses;
        }

        /// <summary>The tile.</summary>
        public int Tile { get; }

        /// <summary>The component, numbered after the colour transform (0 is luma).</summary>
        public int Component { get; }

        /// <summary>The resolution level; 0 is the lowest and holds the LL band.</summary>
        public int ResolutionLevel { get; }

        /// <summary>The subband.</summary>
        public WaveletSubband Subband { get; }

        /// <summary>The quality layer, from 0.</summary>
        public int Layer { get; }

        /// <summary>The column of the code-block in the subband's grid of code-blocks.</summary>
        public int BlockColumn { get; }

        /// <summary>The row of the code-block in the subband's grid of code-blocks.</summary>
        public int BlockRow { get; }

        /// <summary>The offset of the code-block's left edge in the subband, in coefficients.</summary>
        public int X { get; }

        /// <summary>The offset of the code-block's top edge in the subband, in coefficients.</summary>
        public int Y { get; }

        /// <summary>The width of the code-block in coefficients.</summary>
        public int Width { get; }

        /// <summary>The height of the code-block in coefficients.</summary>
        public int Height { get; }

        /// <summary>The width of the subband in coefficients.</summary>
        public int SubbandWidth { get; }

        /// <summary>The height of the subband in coefficients.</summary>
        public int SubbandHeight { get; }

        /// <summary>The magnitude bit-planes the block was coded with, a Maxshift region's shift included.</summary>
        public int MagnitudeBits { get; }

        /// <summary>The most significant bit-planes that were all zero and so not coded.</summary>
        public int MissingMsbs { get; }

        /// <summary>The coding passes the block has in all.</summary>
        public int TotalPasses { get; }

        /// <summary>The passes kept up to and including this layer.</summary>
        public int PassesIncluded { get; }

        /// <summary>The passes this layer adds.</summary>
        public int PassesInLayer { get; }

        /// <summary>The bytes of coded data this layer adds.</summary>
        public int BytesInLayer { get; }

        /// <summary>The bytes of coded data kept up to and including this layer.</summary>
        public int BytesIncluded => PassEndBytes.Length == 0 ? 0 : PassEndBytes[PassEndBytes.Length - 1];

        /// <summary>For each pass kept up to and including this layer, the bytes of data up to the end of the pass.</summary>
        public int[] PassEndBytes { get; }

        /// <summary>The distortion the kept passes remove, in the allocator's units (scaled by <see cref="DistortionScale"/>).</summary>
        public double DistortionReduction { get; }

        /// <summary>The factor the allocator multiplied the block's distortion by: its distortion weights and any region-of-interest scaling.</summary>
        public double DistortionScale { get; }

        /// <summary>The distortion the kept passes remove, without <see cref="DistortionScale"/>.</summary>
        public double UnscaledDistortionReduction => DistortionScale == 0 ? 0 : DistortionReduction / DistortionScale;

        /// <summary>The coefficients of the block inside a Maxshift region of interest; 0 when there is none.</summary>
        public int RoiCoefficients { get; }

        /// <summary>The leading passes that code only region-of-interest bit-planes; 0 for a block with no region coefficients.</summary>
        public int RoiPasses { get; }
    }
}
