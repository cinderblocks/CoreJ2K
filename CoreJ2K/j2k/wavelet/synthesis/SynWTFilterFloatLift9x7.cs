/*
* CVS identifier:
*
* $Id: SynWTFilterFloatLift9x7.java,v 1.15 2002/05/22 15:01:56 grosbois Exp $
*
* Class:                   SynWTFilterFloatLift9x7
*
* Description:             A synthetizing wavelet filter implementing the
*                          lifting 9x7 transform.
*
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
*  */

using System.Runtime.CompilerServices;

namespace CoreJ2K.j2k.wavelet.synthesis
{

    /// <summary> This class inherits from the synthesis wavelet filter definition for int
    /// data. It implements the inverse wavelet transform specifically for the 9x7
    /// filter. The implementation is based on the lifting scheme.
    /// 
    /// See the SynWTFilter class for details such as normalization, how to
    /// split odd-length signals, etc. In particular, this method assumes that the
    /// low-pass coefficient is computed first.
    /// 
    /// </summary>
    /// <seealso cref="SynWTFilter" />
    /// <seealso cref="SynWTFilterFloat" />
    public class SynWTFilterFloatLift9x7 : SynWTFilterFloat
    {
        /// <summary> Returns the negative support of the low-pass analysis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// </summary>
        /// <returns> 2
        /// 
        /// </returns>
        public override int AnLowNegSupport => 4;

        /// <summary> Returns the positive support of the low-pass analysis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// </summary>
        /// <returns> The number of taps of the low-pass analysis filter in the
        /// positive direction
        /// 
        /// </returns>
        public override int AnLowPosSupport => 4;

        /// <summary> Returns the negative support of the high-pass analysis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// </summary>
        /// <returns> The number of taps of the high-pass analysis filter in
        /// the negative direction
        /// 
        /// </returns>
        public override int AnHighNegSupport => 3;

        /// <summary> Returns the positive support of the high-pass analysis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// </summary>
        /// <returns> The number of taps of the high-pass analysis filter in the
        /// positive direction
        /// 
        /// </returns>
        public override int AnHighPosSupport => 3;

        /// <summary> Returns the negative support of the low-pass synthesis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// A MORE PRECISE DEFINITION IS NEEDED
        /// 
        /// </summary>
        /// <returns> The number of taps of the low-pass synthesis filter in the
        /// negative direction
        /// 
        /// </returns>
        public override int SynLowNegSupport => 3;

        /// <summary> Returns the positive support of the low-pass synthesis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// A MORE PRECISE DEFINITION IS NEEDED
        /// 
        /// </summary>
        /// <returns> The number of taps of the low-pass synthesis filter in the
        /// positive direction
        /// 
        /// </returns>
        public override int SynLowPosSupport => 3;

        /// <summary> Returns the negative support of the high-pass synthesis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// A MORE PRECISE DEFINITION IS NEEDED
        /// 
        /// </summary>
        /// <returns> The number of taps of the high-pass synthesis filter in the
        /// negative direction
        /// 
        /// </returns>
        public override int SynHighNegSupport => 4;

        /// <summary> Returns the positive support of the high-pass synthesis filter. That is
        /// the number of taps of the filter in the negative direction.
        /// 
        /// A MORE PRECISE DEFINITION IS NEEDED
        /// 
        /// </summary>
        /// <returns> The number of taps of the high-pass synthesis filter in the
        /// positive direction
        /// 
        /// </returns>
        public override int SynHighPosSupport => 4;

        /// <summary> Returns the implementation type of this filter, as defined in this
        /// class, such as WT_FILTER_INT_LIFT, WT_FILTER_FLOAT_LIFT,
        /// WT_FILTER_FLOAT_CONVOL.
        /// 
        /// </summary>
        /// <returns> WT_FILTER_INT_LIFT.
        /// 
        /// </returns>
        public override int ImplType => WaveletFilter_Fields.WT_FILTER_FLOAT_LIFT;

        /// <summary> Returns the reversibility of the filter. A filter is considered
        /// reversible if it is suitable for lossless coding.
        /// 
        /// </summary>
        /// <returns> true since the 9x7 is reversible, provided the appropriate
        /// rounding is performed.
        /// 
        /// </returns>
        public override bool Reversible => false;

        /// <summary>The value of the first lifting step coefficient </summary>
        public const float ALPHA = -1.586134342f;

        /// <summary>The value of the second lifting step coefficient </summary>
        public const float BETA = -0.05298011854f;

        /// <summary>The value of the third lifting step coefficient </summary>
        public const float GAMMA = 0.8829110762f;

        /// <summary>The value of the fourth lifting step coefficient </summary>
        public const float DELTA = 0.4435068522f;

        /// <summary>The value of the low-pass subband normalization factor </summary>
        public const float KL = 0.8128930655f;

        /// <summary>The value of the high-pass subband normalization factor </summary>
        public const float KH = 1.230174106f;

        // Precomputed reciprocals and combined coefficients used in synthetize_lpf/hpf
        // to replace runtime divisions with multiplications.
        private const float INV_KL = 1.0f / KL;
        private const float INV_KH = 1.0f / KH;
        private const float DELTA_OVER_KH = DELTA * INV_KH;
        private const float TWO_DELTA_OVER_KH = 2.0f * DELTA * INV_KH;
        private const float TWO_DELTA = 2.0f * DELTA;
        private const float TWO_BETA = 2.0f * BETA;
        private const float TWO_GAMMA = 2.0f * GAMMA;
        private const float TWO_ALPHA = 2.0f * ALPHA;

        /// <summary> An implementation of the synthetize_lpf() method that works on int
        /// data, for the inverse 9x7 wavelet transform using the lifting
        /// scheme. See the general description of the synthetize_lpf() method in
        /// the SynWTFilter class for more details.
        /// 
        /// The low-pass and high-pass subbands are normalized by respectively a
        /// factor of 1/KL and a factor of 1/KH
        /// 
        /// The coefficients of the first lifting step are [-DELTA 1 -DELTA]. 
        /// 
        /// The coefficients of the second lifting step are [-GAMMA 1 -GAMMA].
        /// 
        /// The coefficients of the third lifting step are [-BETA 1 -BETA]. 
        /// 
        /// The coefficients of the fourth lifting step are [-ALPHA 1 -ALPHA].
        /// 
        /// </summary>
        /// <param name="lowSig">This is the array that contains the low-pass input
        /// signal.
        /// 
        /// </param>
        /// <param name="lowOff">This is the index in lowSig of the first sample to
        /// filter.
        /// 
        /// </param>
        /// <param name="lowLen">This is the number of samples in the low-pass input
        /// signal to filter.
        /// 
        /// </param>
        /// <param name="lowStep">This is the step, or interleave factor, of the low-pass
        /// input signal samples in the lowSig array.
        /// 
        /// </param>
        /// <param name="highSig">This is the array that contains the high-pass input
        /// signal.
        /// 
        /// </param>
        /// <param name="highOff">This is the index in highSig of the first sample to
        /// filter.
        /// 
        /// </param>
        /// <param name="highLen">This is the number of samples in the high-pass input
        /// signal to filter.
        /// 
        /// </param>
        /// <param name="highStep">This is the step, or interleave factor, of the
        /// high-pass input signal samples in the highSig array.
        /// 
        /// </param>
        /// <param name="outSig">This is the array where the output signal is placed. It
        /// should be long enough to contain the output signal.
        /// 
        /// </param>
        /// <param name="outOff">This is the index in outSig of the element where to put
        /// the first output sample.
        /// 
        /// </param>
        /// <param name="outStep">This is the step, or interleave factor, of the output
        /// samples in the outSig array.
        /// 
        /// </param>
        /// <seealso cref="SynWTFilter.synthetize_lpf" />
        public sealed override void synthetize_lpf(float[] lowSig, int lowOff, int lowLen, int lowStep, float[] highSig, int highOff, int highLen, int highStep, float[] outSig, int outOff, int outStep)
        {
            // Fast path for unit strides (the common case from InvWTFull).
            // Sequential access lets the JIT auto-vectorize and eliminate index arithmetic.
            if (lowStep == 1 && highStep == 1 && outStep == 1)
            {
                Synthetize_lpf_step1(lowSig, lowOff, lowLen, highSig, highOff, highLen, outSig, outOff);
                return;
            }

            int i;
            var outLen = lowLen + highLen;
            var iStep = 2 * outStep;
            int ik, lk, hk;

            // Generate intermediate low frequency subband
            lk = lowOff; hk = highOff; ik = outOff;
            if (outLen > 1)
            {
                outSig[ik] = lowSig[lk] * INV_KL - TWO_DELTA_OVER_KH * highSig[hk];
            }
            else
            {
                outSig[ik] = lowSig[lk];
            }
            lk += lowStep; hk += highStep; ik += iStep;
            for (i = 2; i < outLen - 1; i += 2, ik += iStep, lk += lowStep, hk += highStep)
            {
                outSig[ik] = lowSig[lk] * INV_KL - DELTA_OVER_KH * (highSig[hk - highStep] + highSig[hk]);
            }
            if (outLen % 2 == 1 && outLen > 2)
            {
                outSig[ik] = lowSig[lk] * INV_KL - TWO_DELTA_OVER_KH * highSig[hk - highStep];
            }

            // Generate intermediate high frequency subband
            lk = lowOff; hk = highOff; ik = outOff + outStep;
            for (i = 1; i < outLen - 1; i += 2, ik += iStep, hk += highStep, lk += lowStep)
            {
                outSig[ik] = highSig[hk] * INV_KH - GAMMA * (outSig[ik - outStep] + outSig[ik + outStep]);
            }
            if (outLen % 2 == 0)
            {
                outSig[ik] = highSig[hk] * INV_KH - TWO_GAMMA * outSig[ik - outStep];
            }

            // Generate even samples (inverse low-pass filter)
            ik = outOff;
            if (outLen > 1)
            {
                outSig[ik] -= TWO_BETA * outSig[ik + outStep];
            }
            ik += iStep;
            for (i = 2; i < outLen - 1; i += 2, ik += iStep)
            {
                outSig[ik] -= BETA * (outSig[ik - outStep] + outSig[ik + outStep]);
            }
            if (outLen % 2 == 1 && outLen > 2)
            {
                outSig[ik] -= TWO_BETA * outSig[ik - outStep];
            }

            // Generate odd samples (inverse high pass-filter)
            ik = outOff + outStep;
            for (i = 1; i < outLen - 1; i += 2, ik += iStep)
            {
                outSig[ik] -= ALPHA * (outSig[ik - outStep] + outSig[ik + outStep]);
            }
            if (outLen % 2 == 0)
            {
                outSig[ik] -= TWO_ALPHA * outSig[ik - outStep];
            }
        }

        /// <summary>
        /// Optimized synthetize_lpf for the common case where lowStep=highStep=outStep=1.
        /// Sequential (stride-1) access enables JIT auto-vectorization and removes index arithmetic.
        /// </summary>
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static void Synthetize_lpf_step1(
            float[] lowSig, int lowOff, int lowLen,
            float[] highSig, int highOff, int highLen,
            float[] outSig, int outOff)
        {
            int outLen = lowLen + highLen;

            // ---- Phase 1: Generate intermediate low-frequency subband (even positions) ----
            // Even output positions (0, 2, 4, ...) come from lowSig.
            // The interleaved output step is 2, so even[n] = outSig[outOff + 2*n].
            int lk = lowOff;
            int hk = highOff;
            int ik = outOff; // current even output index (stride=2 in output)

            if (outLen > 1)
            {
                // tail boundary: only right neighbour high sample exists
                outSig[ik] = lowSig[lk] * INV_KL - TWO_DELTA_OVER_KH * highSig[hk];
            }
            else
            {
                outSig[ik] = lowSig[lk];
            }
            lk++; hk++; ik += 2;

            int evenEnd = outOff + 2 * (lowLen - 1); // last even output index
            while (ik < evenEnd)
            {
                outSig[ik] = lowSig[lk] * INV_KL - DELTA_OVER_KH * (highSig[hk - 1] + highSig[hk]);
                lk++; hk++; ik += 2;
            }
            // head boundary (only when lowLen > highLen, i.e. odd outLen > 2)
            if (outLen % 2 == 1 && outLen > 2)
            {
                outSig[ik] = lowSig[lk] * INV_KL - TWO_DELTA_OVER_KH * highSig[hk - 1];
            }

            // ---- Phase 2: Generate intermediate high-frequency subband (odd positions) ----
            hk = highOff;
            ik = outOff + 1; // first odd output index

            int oddEnd = outOff + 2 * highLen - 1; // last odd output index (before possible boundary)
            while (ik < oddEnd)
            {
                outSig[ik] = highSig[hk] * INV_KH - GAMMA * (outSig[ik - 1] + outSig[ik + 1]);
                hk++; ik += 2;
            }
            // head boundary: no right neighbour when outLen is even
            if (outLen % 2 == 0)
            {
                outSig[ik] = highSig[hk] * INV_KH - TWO_GAMMA * outSig[ik - 1];
            }

            // ---- Phase 3: Even samples — inverse low-pass (BETA update) ----
            ik = outOff;
            if (outLen > 1)
            {
                outSig[ik] -= TWO_BETA * outSig[ik + 1];
            }
            ik += 2;

            int evenBetaEnd = outOff + 2 * (lowLen - 1);
            while (ik < evenBetaEnd)
            {
                outSig[ik] -= BETA * (outSig[ik - 1] + outSig[ik + 1]);
                ik += 2;
            }
            if (outLen % 2 == 1 && outLen > 2)
            {
                outSig[ik] -= TWO_BETA * outSig[ik - 1];
            }

            // ---- Phase 4: Odd samples — inverse high-pass (ALPHA update) ----
            ik = outOff + 1;
            int oddAlphaEnd = outOff + 2 * highLen - 1;
            while (ik < oddAlphaEnd)
            {
                outSig[ik] -= ALPHA * (outSig[ik - 1] + outSig[ik + 1]);
                ik += 2;
            }
            if (outLen % 2 == 0)
            {
                outSig[ik] -= TWO_ALPHA * outSig[ik - 1];
            }
        }

        /// <summary> An implementation of the synthetize_hpf() method that works on int
        /// data, for the inverse 9x7 wavelet transform using the lifting
        /// scheme. See the general description of the synthetize_hpf() method in
        /// the SynWTFilter class for more details.
        /// 
        /// The low-pass and high-pass subbands are normalized by respectively
        /// a factor of 1/KL and a factor of 1/KH   
        /// 
        /// The coefficients of the first lifting step are [-DELTA 1 -DELTA]. 
        /// 
        /// The coefficients of the second lifting step are [-GAMMA 1 -GAMMA].
        /// 
        /// The coefficients of the third lifting step are [-BETA 1 -BETA]. 
        /// 
        /// The coefficients of the fourth lifting step are [-ALPHA 1 -ALPHA].
        /// 
        /// </summary>
        /// <param name="lowSig">This is the array that contains the low-pass
        /// input signal.
        /// 
        /// </param>
        /// <param name="lowOff">This is the index in lowSig of the first sample to
        /// filter.
        /// 
        /// </param>
        /// <param name="lowLen">This is the number of samples in the low-pass input
        /// signal to filter.
        /// 
        /// </param>
        /// <param name="lowStep">This is the step, or interleave factor, of the low-pass
        /// input signal samples in the lowSig array.
        /// 
        /// </param>
        /// <param name="highSig">This is the array that contains the high-pass input
        /// signal.
        /// 
        /// </param>
        /// <param name="highOff">This is the index in highSig of the first sample to
        /// filter.
        /// 
        /// </param>
        /// <param name="highLen">This is the number of samples in the high-pass input
        /// signal to filter.
        /// 
        /// </param>
        /// <param name="highStep">This is the step, or interleave factor, of the
        /// high-pass input signal samples in the highSig array.
        /// 
        /// </param>
        /// <param name="outSig">This is the array where the output signal is placed. It
        /// should be long enough to contain the output signal.
        /// 
        /// </param>
        /// <param name="outOff">This is the index in outSig of the element where to put
        /// the first output sample.
        /// 
        /// </param>
        /// <param name="outStep">This is the step, or interleave factor, of the output
        /// samples in the outSig array.
        /// 
        /// </param>
        /// <seealso cref="SynWTFilter.synthetize_hpf" />
        public sealed override void synthetize_hpf(float[] lowSig, int lowOff, int lowLen, int lowStep, float[] highSig, int highOff, int highLen, int highStep, float[] outSig, int outOff, int outStep)
        {

            int i;
            var outLen = lowLen + highLen; //Length of the output signal
            var iStep = 2 * outStep; //Upsampling in outSig
            int ik; //Indexing outSig
            int lk; //Indexing lowSig
            int hk; //Indexing highSig

            // Initialize counters
            lk = lowOff;
            hk = highOff;

            if (outLen != 1)
            {
                var outLen2 = outLen >> 1;
                // "Inverse normalize" each sample
                for (i = 0; i < outLen2; i++)
                {
                    lowSig[lk] *= INV_KL;
                    highSig[hk] *= INV_KH;
                    lk += lowStep;
                    hk += highStep;
                }
                // "Inverse normalise" last high pass coefficient
                if (outLen % 2 == 1)
                {
                    highSig[hk] *= INV_KH;
                }
            }
            else
            {
                // Normalize for Nyquist gain
                highSig[highOff] /= 2;
            }

            // Generate intermediate low frequency subband

            //Initialize counters
            lk = lowOff;
            hk = highOff;
            ik = outOff + outStep;

            //Apply lifting step to each "inner" sample
            for (i = 1; i < outLen - 1; i += 2)
            {
                outSig[ik] = lowSig[lk] - DELTA * (highSig[hk] + highSig[hk + highStep]);
                ik += iStep;
                lk += lowStep;
                hk += highStep;
            }

            if (outLen % 2 == 0 && outLen > 1)
            {
                //Use symmetric extension
                outSig[ik] = lowSig[lk] - TWO_DELTA * highSig[hk];
            }

            // Generate intermediate high frequency subband

            //Initialize counters
            hk = highOff;
            ik = outOff;

            if (outLen > 1)
            {
                outSig[ik] = highSig[hk] - TWO_GAMMA * outSig[ik + outStep];
            }
            else
            {
                outSig[ik] = highSig[hk];
            }

            ik += iStep;
            hk += highStep;

            //Apply lifting step to each "inner" sample
            for (i = 2; i < outLen - 1; i += 2)
            {
                outSig[ik] = highSig[hk] - GAMMA * (outSig[ik - outStep] + outSig[ik + outStep]);
                ik += iStep;
                hk += highStep;
            }

            //Handle head boundary effect if output signal has even length
            if (outLen % 2 == 1 && outLen > 1)
            {
                //Use symmetric extension
                outSig[ik] = highSig[hk] - TWO_GAMMA * outSig[ik - outStep];
            }

            // Generate even samples (inverse low-pass filter)

            //Initialize counters
            ik = outOff + outStep;

            //Apply lifting step to each "inner" sample
            for (i = 1; i < outLen - 1; i += 2)
            {
                outSig[ik] -= BETA * (outSig[ik - outStep] + outSig[ik + outStep]);
                ik += iStep;
            }

            if (outLen % 2 == 0 && outLen > 1)
            {
                // symmetric extension.
                outSig[ik] -= TWO_BETA * outSig[ik - outStep];
            }

            // Generate odd samples (inverse high pass-filter)

            //Initialize counters
            ik = outOff;

            if (outLen > 1)
            {
                // symmetric extension.
                outSig[ik] -= TWO_ALPHA * outSig[ik + outStep];
            }
            ik += iStep;

            //Apply first lifting step to each "inner" sample
            for (i = 2; i < outLen - 1; i += 2)
            {
                outSig[ik] -= ALPHA * (outSig[ik - outStep] + outSig[ik + outStep]);
                ik += iStep;
            }

            //Handle head boundary effect if input signal has even length
            if ((outLen % 2 == 1) && (outLen > 1))
            {
                //Use symmetric extension 
                outSig[ik] -= TWO_ALPHA * outSig[ik - outStep];
            }
        }

        /// <summary> Returns true if the wavelet filter computes or uses the
        /// same "inner" subband coefficient as the full frame wavelet transform,
        /// and false otherwise. In particular, for block based transforms with 
        /// reduced overlap, this method should return false. The term "inner"
        /// indicates that this applies only with respect to the coefficient that 
        /// are not affected by image boundaries processings such as symmetric
        /// extension, since there is not reference method for this.
        /// 
        /// The result depends on the length of the allowed overlap when
        /// compared to the overlap required by the wavelet filter. It also
        /// depends on how overlap processing is implemented in the wavelet
        /// filter.
        /// 
        /// </summary>
        /// <param name="tailOvrlp">This is the number of samples in the input
        /// signal before the first sample to filter that can be used for
        /// overlap.
        /// 
        /// </param>
        /// <param name="headOvrlp">This is the number of samples in the input
        /// signal after the last sample to filter that can be used for
        /// overlap.
        /// 
        /// </param>
        /// <param name="inLen">This is the lenght of the input signal to filter.The
        /// required number of samples in the input signal after the last sample
        /// depends on the length of the input signal.
        /// 
        /// </param>
        /// <returns> true if both overlaps are greater than 2, and correct 
        /// processing is applied in the analyze() method.
        /// 
        /// 
        /// 
        /// </returns>
        public override bool isSameAsFullWT(int tailOvrlp, int headOvrlp, int inLen)
        {

            //If the input signal has even length.
            if (inLen % 2 == 0)
            {
                return tailOvrlp >= 2 && headOvrlp >= 1;
            }
            //Else if the input signal has odd length.
            else
            {
                return tailOvrlp >= 2 && headOvrlp >= 2;
            }
        }

        /// <summary> Returns a string of information about the synthesis wavelet filter
        /// 
        /// </summary>
        /// <returns> wavelet filter type.
        /// 
        /// 
        /// </returns>
        public override string ToString()
        {
            return "w9x7 (lifting)";
        }
    }
}