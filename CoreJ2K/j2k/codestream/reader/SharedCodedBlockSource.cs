// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using CoreJ2K.j2k.entropy.decoder;
using CoreJ2K.j2k.wavelet.synthesis;

namespace CoreJ2K.j2k.codestream.reader
{
    /// <summary>
    /// Lets several decoding chains pull compressed code-blocks from one bitstream reader.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bitstream reader seeks and reads one shared input stream and keeps per-tile state, so it cannot be used from
    /// several threads. This wrapper serialises <see cref="GetCodeBlock"/> (a seek and a memory copy, so contention is
    /// negligible next to entropy decoding) and passes everything else through.
    /// </para>
    /// <para>
    /// Exactly one chain, the primary one, drives tile changes through this object. Chains created with
    /// <see cref="CreateFollower"/> never advance the reader: their <c>SetTile</c> only refreshes their own view of the
    /// tile the primary chain has already moved to.
    /// </para>
    /// </remarks>
    internal sealed class SharedCodedBlockSource : MultiResImgDataAdapter, CodedCBlkDataSrcDec
    {
        private readonly CodedCBlkDataSrcDec _reader;
        private readonly object _readerLock = new object();

        public SharedCodedBlockSource(CodedCBlkDataSrcDec reader) : base(reader)
        {
            _reader = reader;
        }

        public int CbULX => _reader.CbULX;

        public int CbULY => _reader.CbULY;

        public override SubbandSyn GetSynSubbandTree(int t, int c) => _reader.GetSynSubbandTree(t, c);

        public DecLyrdCBlk GetCodeBlock(int c, int m, int n, SubbandSyn sb, int fl, int nl, DecLyrdCBlk ccb)
        {
            lock (_readerLock)
            {
                return _reader.GetCodeBlock(c, m, n, sb, fl, nl, ccb);
            }
        }

        /// <summary>Creates a source for an additional chain that follows the primary chain's current tile.</summary>
        public CodedCBlkDataSrcDec CreateFollower() => new Follower(this);

        private sealed class Follower : MultiResImgDataAdapter, CodedCBlkDataSrcDec
        {
            private readonly SharedCodedBlockSource _shared;

            public Follower(SharedCodedBlockSource shared) : base(shared)
            {
                _shared = shared;
            }

            public int CbULX => _shared.CbULX;

            public int CbULY => _shared.CbULY;

            public override SubbandSyn GetSynSubbandTree(int t, int c) => _shared.GetSynSubbandTree(t, c);

            public DecLyrdCBlk GetCodeBlock(int c, int m, int n, SubbandSyn sb, int fl, int nl, DecLyrdCBlk ccb)
                => _shared.GetCodeBlock(c, m, n, sb, fl, nl, ccb);

            // The primary chain has already moved the reader to this tile; only refresh the cached tile index.
            public override void SetTile(int x, int y) => tIdx = TileIdx;

            public override void NextTile() => tIdx = TileIdx;
        }
    }
}
