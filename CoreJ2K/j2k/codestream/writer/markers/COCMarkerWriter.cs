// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.entropy;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using System.IO;

namespace CoreJ2K.j2k.codestream.writer.markers
{
    /// <summary>
    /// Writes COC (Coding style Component) marker segments.
    /// </summary>
    internal class COCMarkerWriter
    {
        private readonly EncoderSpecs encSpec;
        private readonly ForwardWT dwt;
        private readonly int nComp;

        public COCMarkerWriter(EncoderSpecs encSpec, ForwardWT dwt, int nComp)
        {
            this.encSpec = encSpec;
            this.dwt = dwt;
            this.nComp = nComp;
        }

        public void Write(BinaryWriter writer, bool isMainHeader, int tileIdx, int compIdx)
        {
            AnWTFilter[][] filt;
            bool precinctPartitionUsed;
            int tmp;
            int mrl = 0, a = 0;
            int ppx = 0, ppy = 0;

            if (isMainHeader)
            {
                mrl = ((int)encSpec.dls.getCompDef(compIdx));
                ppx = encSpec.pss.getPPX(-1, compIdx, mrl);
                ppy = encSpec.pss.getPPY(-1, compIdx, mrl);
            }
            else
            {
                mrl = ((int)encSpec.dls.getTileCompVal(tileIdx, compIdx));
                ppx = encSpec.pss.getPPX(tileIdx, compIdx, mrl);
                ppy = encSpec.pss.getPPY(tileIdx, compIdx, mrl);
            }

            precinctPartitionUsed = (ppx != Markers.PRECINCT_PARTITION_DEF_SIZE || 
                                    ppy != Markers.PRECINCT_PARTITION_DEF_SIZE);

            if (precinctPartitionUsed)
            {
                a = mrl + 1;
            }

            // COC marker
            writer.Write(Markers.COC);

            // Lcoc (marker segment length)
            var markSegLen = 8 + ((nComp < 257) ? 1 : 2) + a;
            writer.Write((short)markSegLen);

            // Ccoc (component index)
            if (nComp < 257)
            {
                writer.Write((byte)compIdx);
            }
            else
            {
                writer.Write((short)compIdx);
            }

            // Scod (coding style parameter)
            tmp = 0;
            if (precinctPartitionUsed)
            {
                tmp = Markers.SCOX_PRECINCT_PARTITION;
            }
            writer.Write((byte)tmp);

            // SPcoc - Number of decomposition levels
            writer.Write((byte)mrl);

            // Code-block width and height
            if (isMainHeader)
            {
                tmp = encSpec.cblks.getCBlkWidth(ModuleSpec.SPEC_COMP_DEF, -1, compIdx);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
                tmp = encSpec.cblks.getCBlkHeight(ModuleSpec.SPEC_COMP_DEF, -1, compIdx);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
            }
            else
            {
                tmp = encSpec.cblks.getCBlkWidth(ModuleSpec.SPEC_TILE_COMP, tileIdx, compIdx);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
                tmp = encSpec.cblks.getCBlkHeight(ModuleSpec.SPEC_TILE_COMP, tileIdx, compIdx);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
            }

            // Entropy coding mode options
            tmp = GetCodeBlockStyle(isMainHeader, tileIdx, compIdx);
            writer.Write((byte)tmp);

            // Wavelet filter
            if (isMainHeader)
            {
                filt = ((AnWTFilter[][])encSpec.wfs.getCompDef(compIdx));
                writer.Write((byte)filt[0][0].FilterType);
            }
            else
            {
                filt = ((AnWTFilter[][])encSpec.wfs.getTileCompVal(tileIdx, compIdx));
                writer.Write((byte)filt[0][0].FilterType);
            }

            // Precinct partition
            if (precinctPartitionUsed)
            {
                WritePrecinctPartition(writer, isMainHeader, tileIdx, compIdx, mrl);
            }
        }

        private int GetCodeBlockStyle(bool isMainHeader, int tileIdx, int compIdx)
        {
            int tmp = 0;

            if (isMainHeader)
            {
                if (((string)encSpec.bms.getCompDef(compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_BYPASS;
                if (((string)encSpec.mqrs.getCompDef(compIdx)).ToUpper().Equals("ON"))
                    tmp |= StdEntropyCoderOptions.OPT_RESET_MQ;
                if (((string)encSpec.rts.getCompDef(compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_TERM_PASS;
                if (((string)encSpec.css.getCompDef(compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_VERT_STR_CAUSAL;
                if (((string)encSpec.tts.getCompDef(compIdx)).Equals("predict"))
                    tmp |= StdEntropyCoderOptions.OPT_PRED_TERM;
                if (((string)encSpec.sss.getCompDef(compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_SEG_SYMBOLS;
            }
            else
            {
                if (((string)encSpec.bms.getTileCompVal(tileIdx, compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_BYPASS;
                if (((string)encSpec.mqrs.getTileCompVal(tileIdx, compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_RESET_MQ;
                if (((string)encSpec.rts.getTileCompVal(tileIdx, compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_TERM_PASS;
                if (((string)encSpec.css.getTileCompVal(tileIdx, compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_VERT_STR_CAUSAL;
                if (((string)encSpec.tts.getTileCompVal(tileIdx, compIdx)).Equals("predict"))
                    tmp |= StdEntropyCoderOptions.OPT_PRED_TERM;
                if (((string)encSpec.sss.getTileCompVal(tileIdx, compIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_SEG_SYMBOLS;
            }

            return tmp;
        }

        private void WritePrecinctPartition(BinaryWriter writer, bool isMainHeader, int tileIdx, int compIdx, int mrl)
        {
            System.Collections.Generic.List<int>[] v = isMainHeader ?
                (System.Collections.Generic.List<int>[])encSpec.pss.getCompDef(compIdx) :
                (System.Collections.Generic.List<int>[])encSpec.pss.getTileCompVal(tileIdx, compIdx);

            for (var r = mrl; r >= 0; r--)
            {
                int tmp = r >= v[1].Count ? v[1][v[1].Count - 1] : v[1][r];
                var yExp = (MathUtil.log2(tmp) << 4) & 0x00F0;

                tmp = r >= v[0].Count ? v[0][v[0].Count - 1] : v[0][r];
                var xExp = MathUtil.log2(tmp) & 0x000F;
                writer.Write((byte)(yExp | xExp));
            }
        }
    }
}
