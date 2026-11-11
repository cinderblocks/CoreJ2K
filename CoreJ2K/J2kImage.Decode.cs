// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

namespace CoreJ2K
{
    using Color;
    using j2k.codestream;
    using j2k.codestream.reader;
    using j2k.entropy.decoder;
    using j2k.fileformat.metadata;
    using j2k.fileformat.reader;
    using j2k.image;
    using j2k.image.dco;
    using j2k.image.invcomptransf;
    using j2k.image.nlt;
    using j2k.quantization.dequantizer;
    using j2k.roi;
    using j2k.util;
    using j2k.wavelet.synthesis;
    using System;
    using System.IO;
    using System.Threading;
    using Util;

    public partial class J2kImage
    {
        /// <summary>
        /// The decoding chain of one image, from the file to the stage whose samples are the output. Every way of decoding
        /// (<see cref="InterleavedImage"/>, with or without the metadata, and straight to a platform image) builds this one chain
        /// and differs only in what it makes of <see cref="Image"/>.
        /// </summary>
        private sealed class DecodeChain
        {
            /// <summary>The caller's parameters over the decoder's defaults.</summary>
            public ParameterList Parameters = null!;

            /// <summary>What the main header said.</summary>
            public HeaderInfo Header = null!;

            /// <summary>The inverse wavelet transform, which owns the stages before it. Set as soon as it exists, so that a
            /// chain that fails half-built is still closed.</summary>
            public InverseWT? InverseTransform;

            /// <summary>The last stage: the image after the component transforms and the colour-space mapping.</summary>
            public BlkImgDataSrc Image = null!;

            /// <summary>The metadata of the JP2 file (empty for a raw codestream).</summary>
            public J2KMetadata Metadata = null!;
        }

        /// <summary>
        /// Builds the decoding chain for <paramref name="stream"/>, lets <paramref name="produce"/> make the result from it, and
        /// closes it. A file that makes the decoder run into a structure that is not there is reported as a damaged file.
        /// </summary>
        private static TResult Decode<TResult>(Stream stream, ParameterList? parameters, CancellationToken cancellationToken,
            Func<DecodeChain, TResult> produce)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chain = new DecodeChain();

            try
            {
                BuildDecodeChain(chain, stream, parameters, cancellationToken);
                return produce(chain);
            }
            catch (Exception e) when (IsDamagedFileSymptom(e))
            {
                // The decoder trusts the structure it reads; a file that breaks that trust shows as one of these. Report the file, not the
                // line of the decoder that was the first to notice (it stays in InnerException).
                throw new InvalidOperationException($"The data is damaged or is not a JPEG 2000 file it can decode: {e.Message}", e);
            }
            finally
            {
                // InverseWT owns the decoding chain and cleans up all upstream resources
                if (chain.InverseTransform != null)
                {
                    try
                    {
                        chain.InverseTransform.Close();
                    }
                    catch
                    {
                        // Suppress exceptions during cleanup to avoid masking the original exception
                    }
                }
            }
        }

        private static void BuildDecodeChain(DecodeChain chain, Stream stream, ParameterList? parameters, CancellationToken cancellationToken)
        {
            var in_stream = new ISRandomAccessIO(stream);

            // Create parameter list using defaults
            var pl = chain.Parameters = WithDefaults(parameters, GetDefaultDecoderParameterList(decoder_pinfo));

            // **** File Format ****
            // If the codestream is wrapped in the jp2 file format, Read the
            // file format wrapper
            var ff = new FileFormatReader(in_stream);
            ff.readFileFormat();
            chain.Metadata = ff.Metadata;
            if (ff.JP2FFUsed)
            {
                in_stream.seek(ff.FirstCodeStreamPos);
            }

            // +----------------------------+
            // | Instantiate decoding chain |
            // +----------------------------+

            // **** Header decoder ****
            // Instantiate header decoder and read main header
            var hi = chain.Header = new HeaderInfo();
            HeaderDecoder hd;
            try
            {
                hd = new HeaderDecoder(in_stream, pl, hi);
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidOperationException("Codestream too short or bad header, unable to decode.", e);
            }

            var nCompCod = hd.NumComps;
            var decSpec = hd.DecoderSpecs;

            // Get demixed bitdepths
            var depth = new int[nCompCod];
            for (var i = 0; i < nCompCod; i++)
            {
                depth[i] = hd.GetOriginalBitDepth(i);
            }

            // **** Bit stream reader ****
            BitstreamReaderAgent breader;
            try
            {
                breader = BitstreamReaderAgent.createInstance(in_stream, hd, pl, decSpec, false, hi);
            }
            catch (IOException e)
            {
                throw new InvalidOperationException("Error while reading bit stream header or parsing packets.", e);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate bit stream reader.", e);
            }

            // **** Entropy decoder ****
            (breader as FileBitstreamReaderAgent)?.SetCancellationToken(cancellationToken);
            // When several threads decode code-blocks they share one bitstream reader through a locking wrapper.
            var parallelDegree = ResolveDegreeOfParallelism(pl);
            var sharedSource = parallelDegree > 1 ? new SharedCodedBlockSource(breader) : null;
            CodedCBlkDataSrcDec blockSource = sharedSource ?? (CodedCBlkDataSrcDec)breader;
            EntropyDecoder entdec;
            try
            {
                entdec = hd.createEntropyDecoder(blockSource, pl);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate entropy decoder.", e);
            }

            // **** ROI de-scaler ****
            ROIDeScaler roids;
            try
            {
                roids = hd.createROIDeScaler(entdec, pl, decSpec);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate roi de-scaler.", e);
            }

            // **** Dequantizer ****
            Dequantizer deq;
            try
            {
                deq = HeaderDecoder.createDequantizer(roids, depth, decSpec);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate dequantizer.", e);
            }

            // **** Inverse wavelet transform ***
            InverseWT invWT;
            try
            {
                // full page inverse wavelet transform
                invWT = chain.InverseTransform = InverseWT.createInstance(deq, decSpec);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate inverse wavelet transform.", e);
            }

            invWT.ImgResLevel = breader.ImgRes;
            ConfigureInverseTransform(invWT, sharedSource, parallelDegree, hd, pl, decSpec, depth, cancellationToken);

            // **** Data converter **** (after inverse transform module)
            var converter = new ImgDataConverter(invWT, 0);

            // **** Inverse component transformation ****
            var ictransf = new InvCompTransf(converter, decSpec, depth, pl);

            // **** Inverse multiple component transform (MCT, ISO/IEC 15444-2) ****
            BlkImgDataSrc afterCt = ictransf;
            var mctStages = MctTransform.AssembleDecodeStages(hd.MctArrays, hd.MccSegments, hd.McoSegment);
            if (mctStages.Count > 0)
            {
                afterCt = j2k.image.mct.ComponentTransform.BuildChain(ictransf, mctStages, inverse: true);
            }

            // **** Inverse non-linearity point transform (NLT, ISO/IEC 15444-2) ****
            var postCt = afterCt;
            if (hd.NLTSegments != null && hd.NLTSegments.Count > 0)
            {
                postCt = new InvNLT(afterCt, hd.NLTSegments);
            }

            // **** Inverse variable DC offset (DCO, ISO/IEC 15444-2) ****
            if (hd.DcoSegment != null)
            {
                postCt = new InvDCO(postCt, hd.DcoSegment);
            }

            // **** Component grid ****
            // Components may be subsampled, and so differ in size; put them on one grid (the image as large as the components
            // when they are all sampled alike) before anything is made of them
            postCt = ComponentGridUpsampler.IfNeeded(postCt);

            // **** Color space mapping ****
            if (ff.JP2FFUsed && pl.GetParameter("nocolorspace").Equals("off"))
            {
                try
                {
                    var csMap = new ColorSpace(in_stream, hd, pl);
                    // the channel definitions name the channels the palette makes, so they come after it
                    var resampled = hd.createResampler(postCt, csMap);
                    var palettized = hd.createPalettizedColorSpaceMapper(resampled, csMap);
                    var channels = hd.createChannelDefinitionMapper(palettized, csMap);
                    chain.Image = ConvertColors(hd, channels, csMap) ?? postCt;
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException("Could not instantiate ICC profiler.", e);
                }
                catch (ColorSpaceException e)
                {
                    throw new InvalidOperationException("Error processing ColorSpace information.", e);
                }
            }
            else
            {
                // Skip colorspace mapping
                chain.Image = postCt;
            }
        }

        #region Output

        /// <summary>One row of one tile, clipped to the image, as the stages of the chain hand it over.</summary>
        private sealed class OutputRow
        {
            /// <summary>The row of each component. A sample of column <c>SourceX + i</c> is at <c>offset + SourceX + i</c>.</summary>
            public DataBlkInt[] Samples = null!;

            /// <summary>Fractional bits of each component's samples (they differ by tile when the wavelet does).</summary>
            public int[] FixedPoint = null!;

            /// <summary>What to add to a sample of each component to undo the level shift.</summary>
            public int[] LevelShift = null!;

            /// <summary>The largest value a sample of each component can have.</summary>
            public int[] MaxValue = null!;

            /// <summary>The first column of the tile row that lies inside the image.</summary>
            public int SourceX;

            /// <summary>The line and the column of the image that sample belongs to.</summary>
            public int Line, X;

            /// <summary>How many pixels of the row lie inside the image.</summary>
            public int Count;
        }

        /// <summary>
        /// Pulls the image through the chain, tile by tile and row by row, and hands every row to <paramref name="write"/>.
        /// </summary>
        private static void DecodeRows(BlkImgDataSrc image, CancellationToken cancellationToken, Action<OutputRow> write)
        {
            var numComps = image.NumComps;
            var imgWidth = image.ImgWidth;
            var imgHeight = image.ImgHeight;
            var numTiles = image.GetNumTiles(null);

            var row = new OutputRow
            {
                Samples = new DataBlkInt[numComps],
                FixedPoint = new int[numComps],
                LevelShift = new int[numComps],
                MaxValue = new int[numComps],
            };
            var db = row.Samples;

            var tIdx = 0;
            for (var y = 0; y < numTiles.y; y++)
            {
                for (var x = 0; x < numTiles.x; x++, tIdx++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    image.SetTile(x, y);

                    var height = image.GetTileCompHeight(tIdx, 0);
                    var width = image.GetTileCompWidth(tIdx, 0);

                    var tOffx = image.GetCompULX(0) - (int)Math.Ceiling(image.ImgULX / (double)image.GetCompSubsX(0));
                    var tOffy = image.GetCompULY(0) - (int)Math.Ceiling(image.ImgULY / (double)image.GetCompSubsY(0));

                    for (var i = 0; i < numComps; i++)
                    {
                        db[i] = new DataBlkInt();
                        // Use per-component nominal bits and fixed point
                        var bits = image.GetNomRangeBits(i);
                        row.LevelShift[i] = 1 << (bits - 1);
                        row.MaxValue[i] = (1 << bits) - 1;
                        row.FixedPoint[i] = image.GetFixedPoint(i);
                    }

                    // The part of a tile row that lies inside the image (a tile can start left of it)
                    row.X = Math.Max(tOffx, 0);
                    row.SourceX = row.X - tOffx;
                    row.Count = Math.Min(imgWidth - row.X, width - row.SourceX);

                    for (var l = 0; l < height; l++)
                    {
                        if ((l & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
                        // Map tile-local row 'l' to destination image line
                        var destLine = tOffy + l;
                        // Skip rows that end up above the destination image
                        if (destLine < 0) continue;
                        // Stop processing when we've reached the bottom of destination image
                        if (destLine >= imgHeight) break;

                        // Load each component line into its DataBlk
                        for (var i = 0; i < numComps; i++)
                        {
                            db[i].ulx = 0;
                            db[i].uly = l;
                            db[i].w = width;
                            db[i].h = 1;
                            db[i] = (DataBlkInt)image.GetInternCompData(db[i], i);
                        }

                        if (row.Count <= 0) continue;
                        row.Line = destLine;
                        write(row);
                    }
                }
            }
        }

        /// <summary>Decodes the image of the chain into an <see cref="InterleavedImage"/>.</summary>
        private static InterleavedImage ToInterleavedImage(DecodeChain chain, CancellationToken cancellationToken)
        {
            var image = chain.Image;
            var numComps = image.NumComps;
            var imgWidth = image.ImgWidth;

            var bitsUsed = new int[numComps];
            for (var j = 0; j < numComps; ++j) bitsUsed[j] = image.GetNomRangeBits(j);

            // Refuse an image that would exceed the decoder limits before allocating the output buffer. This is checked
            // on the final image: a palette or multiple-component transform can output more components than the codestream has.
            EnforceDecoderLimits(chain.Parameters, chain.Header, chain.InverseTransform!, image, 4);

            var dst = new InterleavedImage(imgWidth, image.ImgHeight, numComps, bitsUsed);
            try
            {
                var data = dst.Data;
                DecodeRows(image, cancellationToken, row =>
                {
                    var first = (row.Line * imgWidth + row.X) * numComps;
                    var count = row.Count;
                    for (var c = 0; c < numComps; c++)
                    {
                        var blk = row.Samples[c];
                        var src = new ReadOnlySpan<int>(blk.data_array, blk.offset + row.SourceX, count);
                        var fb = row.FixedPoint[c];
                        var ls = row.LevelShift[c];
                        var mv = row.MaxValue[c];
                        var at = first + c;
                        for (var i = 0; i < src.Length; i++, at += numComps)
                        {
                            var v = (src[i] >> fb) + ls;
                            if (v < 0) v = 0;
                            else if (v > mv) v = mv;
                            data[at] = v;
                        }
                    }
                });
                return dst;
            }
            catch
            {
                dst.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Decodes the image of the chain into interleaved bytes, one per sample, scaling components of fewer than 8 bits to
        /// 0..255. The caller has checked that no component has more.
        /// </summary>
        private static byte[] ToInterleavedBytes(DecodeChain chain, CancellationToken cancellationToken)
        {
            var image = chain.Image;
            var numComps = image.NumComps;
            var imgWidth = image.ImgWidth;

            // Checked on the final image (a palette or MCT can output more components than the codestream has).
            EnforceDecoderLimits(chain.Parameters, chain.Header, chain.InverseTransform!, image, 1);
            var pixelBytes = new byte[checked(imgWidth * image.ImgHeight * numComps)];

            DecodeRows(image, cancellationToken, row =>
            {
                var first = (row.Line * imgWidth + row.X) * numComps;
                var count = row.Count;
                for (var c = 0; c < numComps; c++)
                {
                    var blk = row.Samples[c];
                    var src = new ReadOnlySpan<int>(blk.data_array, blk.offset + row.SourceX, count);
                    var fb = row.FixedPoint[c];
                    var ls = row.LevelShift[c];
                    var mv = row.MaxValue[c];
                    var at = first + c;
                    if (mv == 255)
                    {
                        for (var i = 0; i < src.Length; i++, at += numComps)
                        {
                            var v = (src[i] >> fb) + ls;
                            if (v < 0) v = 0;
                            else if (v > 255) v = 255;
                            pixelBytes[at] = (byte)v;
                        }
                    }
                    else
                    {
                        var scale = 255.0 / mv;
                        for (var i = 0; i < src.Length; i++, at += numComps)
                        {
                            var v = (src[i] >> fb) + ls;
                            if (v < 0) v = 0;
                            else if (v > mv) v = mv;
                            pixelBytes[at] = (byte)Math.Round(scale * v);
                        }
                    }
                }
            });
            return pixelBytes;
        }

        #endregion
    }
}
