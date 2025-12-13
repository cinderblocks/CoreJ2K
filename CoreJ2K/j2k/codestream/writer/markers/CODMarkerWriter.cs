// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.entropy;
using CoreJ2K.j2k.entropy.encoder;
using CoreJ2K.j2k.util;
using CoreJ2K.j2k.wavelet.analysis;
using System.IO;

namespace CoreJ2K.j2k.codestream.writer.markers
{
    /// <summary>
    /// Writes COD (Coding style Default) marker segments.
    /// </summary>
    internal class CODMarkerWriter
    {
        private readonly EncoderSpecs encSpec;
        private readonly ForwardWT dwt;
        private readonly PostCompRateAllocator ralloc;

        public CODMarkerWriter(EncoderSpecs encSpec, ForwardWT dwt, PostCompRateAllocator ralloc)
        {
            this.encSpec = encSpec;
            this.dwt = dwt;
            this.ralloc = ralloc;
        }

        public void Write(BinaryWriter writer, bool isMainHeader, int tileIdx, int nComp)
        {
            AnWTFilter[][] filt;
            bool precinctPartitionUsed;
            int tmp;
            int mrl = 0, a = 0;
            int ppx = 0, ppy = 0;
            Progression[] prog;

            if (isMainHeader)
            {
                mrl = ((int)encSpec.dls.getDefault());
                ppx = encSpec.pss.getPPX(-1, -1, mrl);
                ppy = encSpec.pss.getPPY(-1, -1, mrl);
                prog = (Progression[])(encSpec.pocs.getDefault());
            }
            else
            {
                mrl = ((int)encSpec.dls.getTileDef(tileIdx));
                ppx = encSpec.pss.getPPX(tileIdx, -1, mrl);
                ppy = encSpec.pss.getPPY(tileIdx, -1, mrl);
                prog = (Progression[])(encSpec.pocs.getTileDef(tileIdx));
            }

            precinctPartitionUsed = (ppx != Markers.PRECINCT_PARTITION_DEF_SIZE || 
                                    ppy != Markers.PRECINCT_PARTITION_DEF_SIZE);

            if (precinctPartitionUsed)
            {
                a = mrl + 1;
            }

            // Write COD marker
            writer.Write(Markers.COD);

            // Lcod (marker segment length)
            var markSegLen = 12 + a;
            writer.Write((short)markSegLen);

            // Scod (coding style parameter)
            tmp = 0;
            if (precinctPartitionUsed)
            {
                tmp = Markers.SCOX_PRECINCT_PARTITION;
            }

            // SOP markers
            if (isMainHeader)
            {
                if (encSpec.sops.getDefault().ToString().ToUpper().Equals("ON"))
                {
                    tmp |= Markers.SCOX_USE_SOP;
                }
            }
            else
            {
                if (encSpec.sops.getTileDef(tileIdx).ToString().ToUpper().Equals("ON"))
                {
                    tmp |= Markers.SCOX_USE_SOP;
                }
            }

            // EPH markers
            if (isMainHeader)
            {
                if (encSpec.ephs.getDefault().ToString().ToUpper().Equals("ON"))
                {
                    tmp |= Markers.SCOX_USE_EPH;
                }
            }
            else
            {
                if (encSpec.ephs.getTileDef(tileIdx).ToString().ToUpper().Equals("ON"))
                {
                    tmp |= Markers.SCOX_USE_EPH;
                }
            }

            if (dwt.CbULX != 0)
                tmp |= Markers.SCOX_HOR_CB_PART;
            if (dwt.CbULY != 0)
                tmp |= Markers.SCOX_VER_CB_PART;
            
            writer.Write((byte)tmp);

            // SGcod - Progression order
            writer.Write((byte)prog[0].type);

            // Number of layers
            writer.Write((short)ralloc.NumLayers);

            // Multiple component transform
            string str = isMainHeader ? 
                ((string)encSpec.cts.getDefault()) : 
                ((string)encSpec.cts.getTileDef(tileIdx));

            writer.Write((byte)(str.Equals("none") ? 0 : 1));

            // SPcod - Number of decomposition levels
            writer.Write((byte)mrl);

            // Code-block width and height
            if (isMainHeader)
            {
                tmp = encSpec.cblks.getCBlkWidth(ModuleSpec.SPEC_DEF, -1, -1);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
                tmp = encSpec.cblks.getCBlkHeight(ModuleSpec.SPEC_DEF, -1, -1);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
            }
            else
            {
                tmp = encSpec.cblks.getCBlkWidth(ModuleSpec.SPEC_TILE_DEF, tileIdx, -1);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
                tmp = encSpec.cblks.getCBlkHeight(ModuleSpec.SPEC_TILE_DEF, tileIdx, -1);
                writer.Write((byte)(MathUtil.log2(tmp) - 2));
            }

            // Style of code-block coding passes
            tmp = GetCodeBlockStyle(isMainHeader, tileIdx);
            writer.Write((byte)tmp);

            // Wavelet filter
            if (isMainHeader)
            {
                filt = ((AnWTFilter[][])encSpec.wfs.getDefault());
                writer.Write((byte)filt[0][0].FilterType);
            }
            else
            {
                filt = ((AnWTFilter[][])encSpec.wfs.getTileDef(tileIdx));
                writer.Write((byte)filt[0][0].FilterType);
            }

            // Precinct partition
            if (precinctPartitionUsed)
            {
                WritePrecinctPartition(writer, isMainHeader, tileIdx, mrl);
            }
        }

        private int GetCodeBlockStyle(bool isMainHeader, int tileIdx)
        {
            int tmp = 0;

            if (isMainHeader)
            {
                if (((string)encSpec.bms.getDefault()).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_BYPASS;
                if (((string)encSpec.mqrs.getDefault()).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_RESET_MQ;
                if (((string)encSpec.rts.getDefault()).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_TERM_PASS;
                if (((string)encSpec.css.getDefault()).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_VERT_STR_CAUSAL;
                if (((string)encSpec.tts.getDefault()).Equals("predict"))
                    tmp |= StdEntropyCoderOptions.OPT_PRED_TERM;
                if (((string)encSpec.sss.getDefault()).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_SEG_SYMBOLS;
            }
            else
            {
                if (((string)encSpec.bms.getTileDef(tileIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_BYPASS;
                if (((string)encSpec.mqrs.getTileDef(tileIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_RESET_MQ;
                if (((string)encSpec.rts.getTileDef(tileIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_TERM_PASS;
                if (((string)encSpec.css.getTileDef(tileIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_VERT_STR_CAUSAL;
                if (((string)encSpec.tts.getTileDef(tileIdx)).Equals("predict"))
                    tmp |= StdEntropyCoderOptions.OPT_PRED_TERM;
                if (((string)encSpec.sss.getTileDef(tileIdx)).Equals("on"))
                    tmp |= StdEntropyCoderOptions.OPT_SEG_SYMBOLS;
            }

            return tmp;
        }

        private void WritePrecinctPartition(BinaryWriter writer, bool isMainHeader, int tileIdx, int mrl)
        {
            System.Collections.Generic.List<int>[] v = isMainHeader ?
                (System.Collections.Generic.List<int>[])encSpec.pss.getDefault() :
                (System.Collections.Generic.List<int>[])encSpec.pss.getTileDef(tileIdx);

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
