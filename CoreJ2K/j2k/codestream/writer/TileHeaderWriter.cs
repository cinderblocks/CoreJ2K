// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.wavelet.analysis;
using CoreJ2K.j2k.entropy;

namespace CoreJ2K.j2k.codestream.writer
{
    /// <summary>
    /// Manages writing of tile header sections.
    /// </summary>
    internal class TileHeaderWriter
    {
        private readonly EncoderSpecs encSpec;
        private readonly ForwardWT dwt;
        private readonly int nComp;

        public TileHeaderWriter(EncoderSpecs encSpec, ForwardWT dwt, int nComp)
        {
            this.encSpec = encSpec;
            this.dwt = dwt;
            this.nComp = nComp;
        }

        public bool ShouldWriteCOD(int tileIdx, bool isEresUsed)
        {
            var isEresUsedInTile = ((string)encSpec.tts.getTileDef(tileIdx)).Equals("predict");
            
            return encSpec.wfs.isTileSpecified(tileIdx) ||
                   encSpec.cts.isTileSpecified(tileIdx) ||
                   encSpec.dls.isTileSpecified(tileIdx) ||
                   encSpec.bms.isTileSpecified(tileIdx) ||
                   encSpec.mqrs.isTileSpecified(tileIdx) ||
                   encSpec.rts.isTileSpecified(tileIdx) ||
                   encSpec.css.isTileSpecified(tileIdx) ||
                   encSpec.pss.isTileSpecified(tileIdx) ||
                   encSpec.sops.isTileSpecified(tileIdx) ||
                   encSpec.sss.isTileSpecified(tileIdx) ||
                   encSpec.pocs.isTileSpecified(tileIdx) ||
                   encSpec.ephs.isTileSpecified(tileIdx) ||
                   encSpec.cblks.isTileSpecified(tileIdx) ||
                   (isEresUsed != isEresUsedInTile);
        }

        public bool ShouldWriteCOC(int tileIdx, int compIdx, bool isEresUsed, bool tileCODwritten)
        {
            var isEresUsedInTileComp = ((string)encSpec.tts.getTileCompVal(tileIdx, compIdx)).Equals("predict");

            if (encSpec.wfs.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.dls.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.bms.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.mqrs.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.rts.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.css.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.pss.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.sss.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.cblks.isTileCompSpecified(tileIdx, compIdx) ||
                (isEresUsedInTileComp != isEresUsed))
            {
                return true;
            }
            
            if (tileCODwritten)
            {
                return encSpec.wfs.isCompSpecified(compIdx) ||
                       encSpec.dls.isCompSpecified(compIdx) ||
                       encSpec.bms.isCompSpecified(compIdx) ||
                       encSpec.mqrs.isCompSpecified(compIdx) ||
                       encSpec.rts.isCompSpecified(compIdx) ||
                       encSpec.sss.isCompSpecified(compIdx) ||
                       encSpec.css.isCompSpecified(compIdx) ||
                       encSpec.pss.isCompSpecified(compIdx) ||
                       encSpec.cblks.isCompSpecified(compIdx) ||
                       (encSpec.tts.isCompSpecified(compIdx) && 
                        ((string)encSpec.tts.getCompDef(compIdx)).Equals("predict"));
            }

            return false;
        }

        public bool ShouldWriteQCD(int tileIdx)
        {
            return encSpec.qts.isTileSpecified(tileIdx) ||
                   encSpec.qsss.isTileSpecified(tileIdx) ||
                   encSpec.dls.isTileSpecified(tileIdx) ||
                   encSpec.gbs.isTileSpecified(tileIdx);
        }

        public bool ShouldWriteQCC(int tileIdx, int compIdx, int deftilenr, bool tileQCDwritten)
        {
            if (dwt.getNomRangeBits(compIdx) != deftilenr ||
                encSpec.qts.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.qsss.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.dls.isTileCompSpecified(tileIdx, compIdx) ||
                encSpec.gbs.isTileCompSpecified(tileIdx, compIdx))
            {
                return true;
            }
            
            if (tileQCDwritten)
            {
                return encSpec.qts.isCompSpecified(compIdx) ||
                       encSpec.qsss.isCompSpecified(compIdx) ||
                       encSpec.dls.isCompSpecified(compIdx) ||
                       encSpec.gbs.isCompSpecified(compIdx);
            }

            return false;
        }

        public bool ShouldWritePOC(int tileIdx)
        {
            if (encSpec.pocs.isTileSpecified(tileIdx))
            {
                var prog = (Progression[])(encSpec.pocs.getTileDef(tileIdx));
                return prog.Length > 1;
            }
            return false;
        }
    }
}
