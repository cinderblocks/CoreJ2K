// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.encoder;
using CoreJ2K.j2k.quantization.quantizer;
using CoreJ2K.j2k.wavelet.analysis;
using System;
using System.IO;

namespace CoreJ2K.j2k.codestream.writer.markers
{
    /// <summary>
    /// Writes QCD (Quantization Default) marker segments.
    /// </summary>
    internal class QCDMarkerWriter
    {
        private readonly EncoderSpecs encSpec;
        private readonly ForwardWT dwt;

        public QCDMarkerWriter(EncoderSpecs encSpec, ForwardWT dwt)
        {
            this.encSpec = encSpec;
            this.dwt = dwt;
        }

        public int WriteMain(BinaryWriter writer)
        {
            var qType = (string)encSpec.qts.getDefault();
            var baseStep = (float)encSpec.qsss.getDefault();
            var gb = ((int)encSpec.gbs.getDefault());

            var isDerived = qType.Equals("derived");
            var isReversible = qType.Equals("reversible");

            int mrl = ((int)encSpec.dls.getDefault());

            // Find representative tile/component
            var tcIdx = FindRepresentativeTileComponent(mrl, qType);
            SubbandAn sbRoot = dwt.getAnSubbandTree(tcIdx[0], tcIdx[1]);
            int defimgn = dwt.getNomRangeBits(tcIdx[1]);

            // Get quantization style
            int qstyle = GetQuantizationStyle(isReversible, isDerived);

            // QCD marker
            writer.Write(Markers.QCD);

            // Compute number of steps
            int nqcd = ComputeNumberOfSteps(qstyle, sbRoot, mrl);

            // Lqcd (marker segment length)
            var markSegLen = 3 + ((isReversible) ? nqcd : 2 * nqcd);
            writer.Write((short)markSegLen);

            // Sqcd
            writer.Write((byte)(qstyle + (gb << Markers.SQCX_GB_SHIFT)));

            // SPqcd
            WriteQuantizationSteps(writer, qstyle, sbRoot, mrl, defimgn, baseStep);

            return defimgn;
        }

        public int WriteTile(BinaryWriter writer, int tileIdx, int deftilenr)
        {
            var qType = (string)encSpec.qts.getTileDef(tileIdx);
            var baseStep = (float)encSpec.qsss.getTileDef(tileIdx);
            int mrl = ((int)encSpec.dls.getTileDef(tileIdx));

            var compIdx = FindRepresentativeComponent(tileIdx, mrl, qType);
            SubbandAn sbRoot = dwt.getAnSubbandTree(tileIdx, compIdx);
            deftilenr = dwt.getNomRangeBits(compIdx);
            var gb = ((int)encSpec.gbs.getTileDef(tileIdx));

            var isDerived = qType.Equals("derived");
            var isReversible = qType.Equals("reversible");

            int qstyle = GetQuantizationStyle(isReversible, isDerived);

            // QCD marker
            writer.Write(Markers.QCD);

            // Compute number of steps
            int nqcd = ComputeNumberOfSteps(qstyle, sbRoot, mrl);

            // Lqcd
            var markSegLen = 3 + ((isReversible) ? nqcd : 2 * nqcd);
            writer.Write((short)markSegLen);

            // Sqcd
            writer.Write((byte)(qstyle + (gb << Markers.SQCX_GB_SHIFT)));

            // SPqcd
            WriteQuantizationSteps(writer, qstyle, sbRoot, mrl, deftilenr, baseStep);
            
            return deftilenr;
        }

        private int[] FindRepresentativeTileComponent(int mrl, string qType)
        {
            var nt = dwt.getNumTiles();
            var nc = dwt.NumComps;
            var tcIdx = new int[2];

            for (var t = 0; t < nt; t++)
            {
                for (var c = 0; c < nc; c++)
                {
                    int tmpI = ((int)encSpec.dls.getTileCompVal(t, c));
                    string tmpStr = ((string)encSpec.qts.getTileCompVal(t, c));
                    if (tmpI == mrl && tmpStr.Equals(qType))
                    {
                        tcIdx[0] = t; 
                        tcIdx[1] = c;
                        return tcIdx;
                    }
                }
            }

            throw new InvalidOperationException(
                "Default representative for quantization type and number of decomposition levels not found " +
                "in main QCD marker segment. You have found a JJ2000 bug.");
        }

        private int FindRepresentativeComponent(int tileIdx, int mrl, string qType)
        {
            var nc = dwt.NumComps;

            for (var c = 0; c < nc; c++)
            {
                int tmpI = ((int)encSpec.dls.getTileCompVal(tileIdx, c));
                string tmpStr = ((string)encSpec.qts.getTileCompVal(tileIdx, c));
                if (tmpI == mrl && tmpStr.Equals(qType))
                {
                    return c;
                }
            }

            throw new InvalidOperationException(
                $"Default representative for quantization type and number of decomposition levels not found " +
                $"in tile QCD (t={tileIdx}) marker segment. You have found a JJ2000 bug.");
        }

        private int GetQuantizationStyle(bool isReversible, bool isDerived)
        {
            if (isReversible)
                return Markers.SQCX_NO_QUANTIZATION;
            if (isDerived)
                return Markers.SQCX_SCALAR_DERIVED;
            return Markers.SQCX_SCALAR_EXPOUNDED;
        }

        private int ComputeNumberOfSteps(int qstyle, SubbandAn sbRoot, int mrl)
        {
            switch (qstyle)
            {
                case Markers.SQCX_SCALAR_DERIVED:
                    return 1;

                case Markers.SQCX_NO_QUANTIZATION:
                case Markers.SQCX_SCALAR_EXPOUNDED:
                    int nqcd = 0;
                    SubbandAn sb = (SubbandAn)sbRoot.getSubbandByIdx(0, 0);

                    for (var j = 0; j <= mrl; j++)
                    {
                        SubbandAn csb = sb;
                        while (csb != null)
                        {
                            nqcd++;
                            csb = (SubbandAn)csb.nextSubband();
                        }
                        sb = (SubbandAn)sb.NextResLevel;
                    }
                    return nqcd;

                default:
                    throw new InvalidOperationException("Internal JJ2000 error");
            }
        }

        private void WriteQuantizationSteps(BinaryWriter writer, int qstyle, SubbandAn sbRoot, 
                                           int mrl, int nomRangeBits, float baseStep)
        {
            SubbandAn sb = (SubbandAn)sbRoot.getSubbandByIdx(0, 0);

            switch (qstyle)
            {
                case Markers.SQCX_NO_QUANTIZATION:
                    for (var j = 0; j <= mrl; j++)
                    {
                        SubbandAn csb = sb;
                        while (csb != null)
                        {
                            var tmp = (nomRangeBits + csb.anGainExp);
                            writer.Write((byte)(tmp << Markers.SQCX_EXP_SHIFT));
                            csb = (SubbandAn)csb.nextSubband();
                        }
                        sb = (SubbandAn)sb.NextResLevel;
                    }
                    break;

                case Markers.SQCX_SCALAR_DERIVED:
                    float step = baseStep / (1 << sb.level);
                    writer.Write((short)StdQuantizer.convertToExpMantissa(step));
                    break;

                case Markers.SQCX_SCALAR_EXPOUNDED:
                    for (var j = 0; j <= mrl; j++)
                    {
                        SubbandAn csb = sb;
                        while (csb != null)
                        {
                            float subbandStep = baseStep / (csb.l2Norm * (1 << csb.anGainExp));
                            writer.Write((short)StdQuantizer.convertToExpMantissa(subbandStep));
                            csb = (SubbandAn)csb.nextSubband();
                        }
                        sb = (SubbandAn)sb.NextResLevel;
                    }
                    break;

                default:
                    throw new InvalidOperationException("Internal JJ2000 error");
            }
        }
    }
}
