// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.entropy.encoder;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.io;
using CoreJ2K.j2k.roi.encoder;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using System;

namespace CoreJ2K.j2k.codestream.writer
{

    /// <summary> This class writes almost of the markers and marker segments in main header
    /// and in tile-part headers. It is created by the run() method of the Encoder
    /// instance.
    /// 
    /// A marker segment includes a marker and eventually marker segment
    /// parameters. It is designed by the three letter code of the marker
    /// associated with the marker segment. JPEG 2000 part I defines 6 types of
    /// markers:
    /// <ul> 
    /// <li>Delimiting : SOC,SOT,SOD,EOC (written in FileCodestreamWriter).</li>
    /// <li>Fixed information: SIZ.</li> 
    /// <li>Functional: COD,COC,RGN,QCD,QCC,POC.</li>
    /// <li> In bit-stream: SOP,EPH.</li>
    /// <li> Pointer: TLM,PLM,PLT,PPM,PPT.</li> 
    /// <li> Informational: CRG,COM.</li>
    /// </ul>
    /// 
    /// Main Header is written when Encoder instance calls encodeMainHeader
    /// whereas tile-part headers are written when the EBCOTRateAllocator instance
    /// calls encodeTilePartHeader.
    /// 
    /// </summary>
    /// <seealso cref="Encoder" />
    /// <seealso cref="Markers" />
    /// <seealso cref="EBCOTRateAllocator" />
    public class HeaderEncoder
    {
        /// <summary> Returns the parameters that are used in this class and implementing
        /// classes. It returns a 2D String array. Each of the 1D arrays is for a
        /// different option, and they have 3 elements. The first element is the
        /// option name, the second one is the synopsis, the third one is a long
        /// description of what the parameter is and the fourth is its default
        /// value. The synopsis or description may be 'null', in which case it is
        /// assumed that there is no synopsis or description of the option,
        /// respectively. Null may be returned if no options are supported.
        /// 
        /// </summary>
        /// <returns> the options name, their synopsis and their explanation, or null
        /// if no options are supported.
        /// 
        /// </returns>
        public static string[][] ParameterInfo => pinfo;

        /// <summary> Returns the byte-buffer used to store the codestream header.
        /// 
        /// </summary>
        /// <returns> A byte array countaining codestream header
        /// 
        /// </returns>
        protected internal virtual byte[] Buffer => baos.ToArray();

        /// <summary> Returns the length of the header.
        /// 
        /// </summary>
        /// <returns> The length of the header in bytes
        /// 
        /// </returns>
        public virtual int Length => (int)hbuf.BaseStream.Length;

        /// <summary> Returns the number of bytes used in the codestream header's buffer.
        /// 
        /// </summary>
        /// <returns> Header length in buffer (without any header overhead)
        /// 
        /// </returns>
        protected internal virtual int BufferLength => (int)baos.Length;

        /// <summary>The prefix for the header encoder options: 'H' </summary>
        public const char OPT_PREFIX = 'H';

        /// <summary>The list of parameters that are accepted for the header encoder
        /// module. Options for this modules start with 'H'. 
        /// </summary>
        private static readonly string[][] pinfo = { new string[] { "Hjj2000_COM", null, "Writes or not the JJ2000 COM marker in the " + "codestream", "off" }, new string[] { "HCOM", "<Comment 1>[#<Comment 2>[#<Comment3...>]]", "Adds COM marker segments in the codestream. Comments must be " + "separated with '#' and are written into distinct maker segments.", null } };

        /// <summary>Nominal range bit of the component defining default values in QCD for
        /// main header 
        /// </summary>
        private int defimgn;

        /// <summary>Nominal range bit of the component defining default values in QCD for
        /// tile headers 
        /// </summary>
        private int deftilenr;

        /// <summary>The number of components in the image </summary>
        private readonly int nComp;

        /// <summary>Whether or not to write the JJ2000 COM marker segment </summary>
        private readonly bool enJJ2KMarkSeg = true;

        /// <summary>Other COM marker segments specified in the command line </summary>
        private readonly string otherCOMMarkSeg = null;

        /// <summary>The ByteArrayOutputStream to store header data. This handler is kept
        /// in order to use methods not accessible from a general
        /// DataOutputStream. For the other methods, it's better to use variable
        /// hbuf.
        /// 
        /// </summary>
        /// <seealso cref="hbuf">
        /// </seealso>
        protected internal System.IO.MemoryStream baos;

        /// <summary>The DataOutputStream to store header data. This kind of object is
        /// useful to write short, int, .... It's constructor takes baos as
        /// parameter.
        /// 
        /// </summary>
        /// <seealso cref="baos" />
        //UPGRADE_TODO: Class 'java.io.DataOutputStream' was converted to 'System.IO.BinaryWriter' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaioDataOutputStream'"
        protected internal System.IO.BinaryWriter hbuf;

        /// <summary>The image data reader. Source of original data info </summary>
        protected internal ImgData origSrc;

        /// <summary>An array specifying, for each component,if the data was signed or not
        /// 
        /// </summary>
        protected internal bool[] isOrigSig;

        /// <summary>Reference to the rate allocator </summary>
        protected internal PostCompRateAllocator ralloc;

        /// <summary>Reference to the DWT module </summary>
        protected internal ForwardWT dwt;

        /// <summary>Reference to the tiler module </summary>
        protected internal Tiler tiler;

        /// <summary>Reference to the ROI module </summary>
        protected internal ROIScaler roiSc;

        /// <summary>The encoder specifications </summary>
        protected internal EncoderSpecs encSpec;

        // Marker writers
        private readonly markers.CODMarkerWriter codWriter;
        private readonly markers.COCMarkerWriter cocWriter;
        private readonly markers.QCDMarkerWriter qcdWriter;
        private readonly markers.QCCMarkerWriter qccWriter;
        private readonly markers.SIZMarkerWriter sizWriter;
        private readonly markers.POCMarkerWriter pocWriter;
        private readonly markers.RGNMarkerWriter rgnWriter;
        private readonly markers.COMMarkerWriter comWriter;
        private readonly markers.SOTMarkerWriter sotWriter;

        // Section managers
        private readonly MainHeaderWriter mainHeaderWriter;
        private readonly TileHeaderWriter tileHeaderWriter;

        /// <summary> Initializes the header writer with the references to the coding chain.
        /// 
        /// </summary>
        /// <param name="origsrc">The original image data (before any component mixing,
        /// tiling, etc.)
        /// 
        /// </param>
        /// <param name="isorigsig">An array specifying for each component if it was
        /// originally signed or not.
        /// 
        /// </param>
        /// <param name="dwt">The discrete wavelet transform module.
        /// 
        /// </param>
        /// <param name="tiler">The tiler module.
        /// 
        /// </param>
        /// <param name="encSpec">The encoder specifications
        /// 
        /// </param>
        /// <param name="roiSc">The ROI scaler module.
        /// 
        /// </param>
        /// <param name="ralloc">The post compression rate allocator.
        /// 
        /// </param>
        /// <param name="pl">ParameterList instance.
        /// 
        /// </param>
        public HeaderEncoder(ImgData origsrc, bool[] isorigsig, ForwardWT dwt, Tiler tiler, EncoderSpecs encSpec, ROIScaler roiSc, PostCompRateAllocator ralloc, ParameterList pl)
        {
            pl.checkList(OPT_PREFIX, ParameterList.toNameArray(pinfo));
            if (origsrc.NumComps != isorigsig.Length)
            {
                throw new ArgumentException();
            }
            origSrc = origsrc;
            isOrigSig = isorigsig;
            this.dwt = dwt;
            this.tiler = tiler;
            this.encSpec = encSpec;
            this.roiSc = roiSc;
            this.ralloc = ralloc;

            baos = new System.IO.MemoryStream();
            //UPGRADE_TODO: Class 'java.io.DataOutputStream' was converted to 'System.IO.BinaryWriter' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaioDataOutputStream'"
            hbuf = new Util.EndianBinaryWriter(baos, true);
            nComp = origsrc.NumComps;
            enJJ2KMarkSeg = pl.getBooleanParameter("Hjj2000_COM");
            otherCOMMarkSeg = pl.getParameter("HCOM");

            // Initialize marker writers
            codWriter = new markers.CODMarkerWriter(encSpec, dwt, ralloc);
            cocWriter = new markers.COCMarkerWriter(encSpec, dwt, nComp);
            qcdWriter = new markers.QCDMarkerWriter(encSpec, dwt);
            qccWriter = new markers.QCCMarkerWriter(encSpec, dwt, nComp);
            sizWriter = new markers.SIZMarkerWriter(origSrc, isOrigSig, tiler, nComp);
            pocWriter = new markers.POCMarkerWriter(encSpec, nComp);
            rgnWriter = new markers.RGNMarkerWriter(encSpec, nComp);
            comWriter = new markers.COMMarkerWriter(enJJ2KMarkSeg, otherCOMMarkSeg);
            sotWriter = new markers.SOTMarkerWriter();

            // Initialize section managers
            mainHeaderWriter = new MainHeaderWriter(encSpec, dwt, nComp);
            tileHeaderWriter = new TileHeaderWriter(encSpec, dwt, nComp);
        }

        /// <summary> Resets the contents of this HeaderEncoder to its initial state. It
        /// erases all the data in the header buffer and reactualizes the
        /// headerLength field of the bit stream writer.
        /// 
        /// </summary>
        public virtual void reset()
        {
            //UPGRADE_ISSUE: Method 'java.io.ByteArrayOutputStream.reset' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaioByteArrayOutputStreamreset'"
            // CONVERSION PROBLEM?
            //baos.reset();
            baos.SetLength(0);
            //UPGRADE_TODO: Class 'java.io.DataOutputStream' was converted to 'System.IO.BinaryWriter' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaioDataOutputStream'"
            hbuf = new Util.EndianBinaryWriter(baos, true); //new System.IO.BinaryWriter(baos);
        }

        /// <summary> Writes the header to the specified BinaryDataOutput.
        /// 
        /// </summary>
        /// <param name="out">Where to write the header.
        /// 
        /// </param>
        public virtual void writeTo(BinaryDataOutput out_Renamed)
        {
            int i, len;
            byte[] buf;

            buf = Buffer;
            len = Length;

            for (i = 0; i < len; i++)
            {
                out_Renamed.writeByte(buf[i]);
            }
        }

        /// <summary> Writes the header to the specified OutputStream.
        /// 
        /// </summary>
        /// <param name="out">Where to write the header.
        /// 
        /// </param>
        public virtual void writeTo(System.IO.Stream out_Renamed)
        {
            out_Renamed.Write(Buffer, 0, BufferLength);
        }

        /// <summary> Start Of Codestream marker (SOC) signalling the beginning of a
        /// codestream.
        /// 
        /// </summary>
        private void writeSOC()
        {
            hbuf.Write(Markers.SOC);
        }

        /// <summary> Writes TLM marker segment(s) in the main header.
        /// TLM markers contain tile-part lengths for fast random tile access.
        /// 
        /// Multiple TLM markers may be written if there are many tiles.
        /// 
        /// </summary>
        /// <param name="tlm">The TLM data to write
        /// 
        /// </param>
        protected internal virtual void writeTLM(CoreJ2K.j2k.codestream.metadata.TilePartLengthsData tlm)
        {
            TLMMarkerWriter.WriteTLM(hbuf, tlm);
        }

        /// <summary> Writes PLM marker segment(s) in the main header.
        /// PLM markers contain packet lengths for all tiles, allowing decoders to
        /// quickly locate packets without parsing packet headers.
        /// 
        /// Multiple PLM markers may be written if there are many packets.
        /// 
        /// </summary>
        /// <param name="plm">The PLM data to write
        /// 
        /// </param>
        protected internal virtual void writePLM(CoreJ2K.j2k.codestream.metadata.PacketLengthsData plm)
        {
            PLMMarkerWriter.WritePLM(hbuf, plm);
        }

        /// <summary> Write main header. JJ2000 main header corresponds to the following
        /// sequence of marker segments:
        /// 
        /// <ol>
        /// <li>SOC</li>
        /// <li>SIZ</li>
        /// <li>COD</li>
        /// <li>COC (if needed)</li>
        /// <li>QCD</li>
        /// <li>QCC (if needed)</li>
        /// <li>POC (if needed)</li>
        /// </ol>
        /// 
        /// </summary>
        public virtual void encodeMainHeader()
        {
            // +---------------------------------+
            // |    SOC marker segment           |
            // +---------------------------------+
            writeSOC();

            // +---------------------------------+
            // |    Image and tile SIZe (SIZ)    |
            // +---------------------------------+
            sizWriter.Write(hbuf);

            // +-------------------------------+
            // |   COding style Default (COD)  |
            // +-------------------------------+
            var isEresUsed = ((string)encSpec.tts.getDefault()).Equals("predict");
            codWriter.Write(hbuf, true, 0, nComp);

            // +---------------------------------+
            // |   COding style Component (COC)  |
            // +---------------------------------+
            for (int i = 0; i < nComp; i++)
            {
                if (mainHeaderWriter.ShouldWriteCOC(i, isEresUsed))
                {
                    cocWriter.Write(hbuf, true, 0, i);
                }
            }

            // +-------------------------------+
            // |   Quantization Default (QCD)  |
            // +-------------------------------+
            defimgn = qcdWriter.WriteMain(hbuf);

            // +-------------------------------+
            // | Quantization Component (QCC)  |
            // +-------------------------------+
            for (int i = 0; i < nComp; i++)
            {
                if (mainHeaderWriter.ShouldWriteQCC(i, defimgn))
                {
                    qccWriter.WriteMain(hbuf, i);
                }
            }

            // +--------------------------+
            // |    POC maker segment     |
            // +--------------------------+
            if (mainHeaderWriter.ShouldWritePOC())
            {
                pocWriter.Write(hbuf, true, 0);
            }

            // +---------------------------+
            // |      Comments (COM)       |
            // +---------------------------+
            comWriter.Write(hbuf);
        }

        /// <summary> Writes tile-part header. JJ2000 tile-part header corresponds to the
        /// following sequence of marker segments:
        /// 
        /// <ol> 
        /// <li>SOT</li> 
        /// <li>COD (if needed)</li> 
        /// <li>COC (if needed)</li> 
        /// <li>QCD (if needed)</li> 
        /// <li>QCC (if needed)</li> 
        /// <li>RGN (if needed)</li> 
        /// <li>POC (if needed)</li>
        /// <li>SOD</li> 
        /// </ol>
        /// 
        /// </summary>
        /// <param name="length">The length of the current tile-part.
        /// 
        /// </param>
        /// <param name="tileIdx">Index of the tile to write
        /// 
        /// </param>
        public virtual void encodeTilePartHeader(int tileLength, int tileIdx)
        {
            var numTiles = ralloc.getNumTiles(null);
            ralloc.setTile(tileIdx % numTiles.x, tileIdx / numTiles.x);

            // +--------------------------+
            // |    SOT maker segment     |
            // +--------------------------+
            sotWriter.Write(hbuf, tileIdx, tileLength);

            // +--------------------------+
            // |    COD maker segment     |
            // +--------------------------+
            var isEresUsed = ((string)encSpec.tts.getDefault()).Equals("predict");
            var tileCODwritten = false;
            
            if (tileHeaderWriter.ShouldWriteCOD(tileIdx, isEresUsed))
            {
                codWriter.Write(hbuf, false, tileIdx, nComp);
                tileCODwritten = true;
            }

            // +--------------------------+
            // |    COC maker segment     |
            // +--------------------------+
            for (var c = 0; c < nComp; c++)
            {
                if (tileHeaderWriter.ShouldWriteCOC(tileIdx, c, isEresUsed, tileCODwritten))
                {
                    cocWriter.Write(hbuf, false, tileIdx, c);
                }
            }

            // +--------------------------+
            // |    QCD maker segment     |
            // +--------------------------+
            var tileQCDwritten = false;
            if (tileHeaderWriter.ShouldWriteQCD(tileIdx))
            {
                deftilenr = qcdWriter.WriteTile(hbuf, tileIdx, deftilenr);
                tileQCDwritten = true;
            }
            else
            {
                deftilenr = defimgn;
            }

            // +--------------------------+
            // |    QCC maker segment     |
            // +--------------------------+
            for (var c = 0; c < nComp; c++)
            {
                if (tileHeaderWriter.ShouldWriteQCC(tileIdx, c, deftilenr, tileQCDwritten))
                {
                    qccWriter.WriteTile(hbuf, tileIdx, c);
                }
            }

            // +--------------------------+
            // |    RGN maker segment     |
            // +--------------------------+
            if (roiSc.useRoi() && (!roiSc.BlockAligned))
            {
                rgnWriter.Write(hbuf, tileIdx);
            }

            // +--------------------------+
            // |    POC maker segment     |
            // +--------------------------+
            if (tileHeaderWriter.ShouldWritePOC(tileIdx))
            {
                pocWriter.Write(hbuf, false, tileIdx);
            }

            // +--------------------------+
            // |         SOD marker        |
            // +--------------------------+
            hbuf.Write((byte)SupportClass.URShift(Markers.SOD, 8));
            hbuf.Write((byte)(Markers.SOD & 0x00FF));
        }
    }
}