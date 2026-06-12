// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.IO;
using Xunit;
using CoreJ2K;
using CoreJ2K.Util;
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for Reader Requirements (rreq) box writing and auto-detection.
    /// ISO/IEC 15444-2 requires a conformant JPX file to contain an rreq box.
    /// </summary>
    public class RreqBoxTests
    {
        // ------------------------------------------------------------------
        // ReaderRequirementsBox unit tests
        // ------------------------------------------------------------------

        [Fact]
        public void GetContentBytes_Empty_WritesValidMinimalBox()
        {
            var box = new ReaderRequirementsBox();
            var bytes = box.GetContentBytes();

            // ML(1) + FUAM(2) + DCM(2) + NSF(2) + NVF(2) = 9 bytes
            Assert.Equal(9, bytes.Length);
            Assert.Equal(2, bytes[0]);           // ML = 2
            Assert.Equal(0, bytes[1]);           // FUAM hi
            Assert.Equal(0, bytes[2]);           // FUAM lo — no features
            Assert.Equal(0, bytes[3]);           // DCM  hi
            Assert.Equal(0, bytes[4]);           // DCM  lo
            Assert.Equal(0, bytes[5]);           // NSF  hi
            Assert.Equal(0, bytes[6]);           // NSF  lo
            Assert.Equal(0, bytes[7]);           // NVF  hi
            Assert.Equal(0, bytes[8]);           // NVF  lo
        }

        [Fact]
        public void GetContentBytes_SingleFeature_SetsMaskBit()
        {
            var box = new ReaderRequirementsBox();
            box.StandardFeatures.Add(ReaderRequirementsBox.FEATURE_JPX_DCO); // SF=3 → bit 2
            var bytes = box.GetContentBytes();

            // ML(1)+FUAM(2)+DCM(2)+NSF(2)+SF(2)+SM(2)+NVF(2) = 13 bytes
            Assert.Equal(13, bytes.Length);
            Assert.Equal(0, bytes[1]);   Assert.Equal(4, bytes[2]);  // FUAM = 0x0004
            Assert.Equal(0, bytes[3]);   Assert.Equal(4, bytes[4]);  // DCM  = 0x0004
            Assert.Equal(0, bytes[5]);   Assert.Equal(1, bytes[6]);  // NSF  = 1
            // Feature entry: SF=3, SM=0x0004
            Assert.Equal(0, bytes[7]);   Assert.Equal(3, bytes[8]);  // SF
            Assert.Equal(0, bytes[9]);   Assert.Equal(4, bytes[10]); // SM
            Assert.Equal(0, bytes[11]);  Assert.Equal(0, bytes[12]); // NVF
        }

        [Fact]
        public void GetContentBytes_ThreeFeatures_MaskIsUnion()
        {
            // MCT(1→bit0=0x0001) + NLT(2→bit1=0x0002) + DCO(3→bit2=0x0004) → 0x0007
            var box = ReaderRequirementsBox.BuildForJpx(hasMct: true, hasNlt: true, hasDco: true);
            var bytes = box.GetContentBytes();

            // 9 + 3*4 = 21 bytes
            Assert.Equal(21, bytes.Length);
            Assert.Equal(0, bytes[1]);  Assert.Equal(7, bytes[2]);  // FUAM = 0x0007
            Assert.Equal(0, bytes[3]);  Assert.Equal(7, bytes[4]);  // DCM  = 0x0007
            Assert.Equal(0, bytes[5]);  Assert.Equal(3, bytes[6]);  // NSF  = 3
        }

        [Fact]
        public void BuildForJpx_OnlyActiveFeatures_Listed()
        {
            var box = ReaderRequirementsBox.BuildForJpx(hasMct: true, hasNlt: false, hasDco: false);
            Assert.Single(box.StandardFeatures);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_MCT, box.StandardFeatures);

            var box2 = ReaderRequirementsBox.BuildForJpx(hasMct: false, hasNlt: false, hasDco: true);
            Assert.Single(box2.StandardFeatures);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_DCO, box2.StandardFeatures);
        }

        // ------------------------------------------------------------------
        // Round-trip integration tests
        // ------------------------------------------------------------------

        private static (InterleavedImageSource src, int[][] orig) MakeImage(int w, int h)
        {
            var comps = new int[1][];
            var orig = new int[1][];
            comps[0] = new int[w * h];
            orig[0] = new int[w * h];
            for (var i = 0; i < w * h; i++)
            {
                orig[0][i] = (i * 7) & 0xFF;
                comps[0][i] = orig[0][i] - 128;
            }
            return (new InterleavedImageSource(w, h, 1, 8, new[] { false }, comps), orig);
        }

        private static FileFormatReader ReadBoxes(byte[] data)
        {
            var reader = new FileFormatReader(new ISRandomAccessIO(new MemoryStream(data)));
            reader.readFileFormat();
            return reader;
        }

        [Fact]
        public void PlainJp2_NoRreq()
        {
            // A plain JP2 file (no Part 2 features) must NOT contain an rreq box.
            var (src, _) = MakeImage(32, 32);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";
            var bytes = J2kImage.ToBytes(src, null, pl)!;

            var reader = ReadBoxes(bytes);
            Assert.Null(reader.Metadata.ReaderRequirements);
        }

        [Fact]
        public void JpxWithDco_WritesRreq_ContainsDcoFeature()
        {
            var (src, orig) = MakeImage(32, 32);
            var dco = new DCOMarkerSegment { Offsets = new[] { 5 } };
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            var bytes = J2kImage.ToBytes(src, null, pl, null, null, dco)!;
            var reader = ReadBoxes(bytes);

            Assert.NotNull(reader.Metadata.ReaderRequirements);
            var rreq = reader.Metadata.ReaderRequirements!;
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_DCO, rreq.StandardFeatures);
        }

        [Fact]
        public void JpxWithNlt_WritesRreq_ContainsNltFeature()
        {
            var (src, _) = MakeImage(32, 32);
            var nlt = new NLTMarkerSegment { Type = NLTType.None };
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            var bytes = J2kImage.ToBytes(src, null, pl, new[] { nlt })!;
            var reader = ReadBoxes(bytes);

            Assert.NotNull(reader.Metadata.ReaderRequirements);
            Assert.Contains(ReaderRequirementsBox.FEATURE_JPX_NLT,
                reader.Metadata.ReaderRequirements!.StandardFeatures);
        }

        [Fact]
        public void JpxWithDco_RoundTrips_ImagePixelsExact()
        {
            var (src, orig) = MakeImage(48, 48);
            var dco = new DCOMarkerSegment { Offsets = new[] { 12 } };
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            var bytes = J2kImage.ToBytes(src, null, pl, null, null, dco)!;
            var decoded = J2kImage.FromBytes(bytes);

            Assert.NotNull(decoded);
            var comp = decoded!.GetComponent(0);
            for (var i = 0; i < orig[0].Length; i++)
                Assert.Equal(orig[0][i], comp[i]);
        }

        [Fact]
        public void JpxWithJpxBoxes_WritesRreq_NoFeatures()
        {
            // A file with JPX boxes but no Part 2 codestream markers should still
            // get an rreq box (with zero features listed — minimal but conformant).
            var meta = new J2KMetadata();
            meta.UseJpxBrand = true;

            var (src, _) = MakeImage(16, 16);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            var bytes = J2kImage.ToBytes(src, meta, pl)!;
            var reader = ReadBoxes(bytes);

            Assert.NotNull(reader.Metadata.ReaderRequirements);
            Assert.Empty(reader.Metadata.ReaderRequirements!.StandardFeatures);
        }

        [Fact]
        public void UserPresetRreq_IsRespected_NotOverwritten()
        {
            // If the caller pre-sets ReaderRequirements, J2kImage.ToBytes must leave it alone.
            var meta = new J2KMetadata();
            var customRreq = new ReaderRequirementsBox();
            customRreq.StandardFeatures.Add(99); // sentinel value
            meta.ReaderRequirements = customRreq;

            var dco = new DCOMarkerSegment { Offsets = new[] { 1 } };
            var (src, _) = MakeImage(16, 16);
            var pl = J2kImage.GetDefaultEncoderParameterList();
            pl["file_format"] = "on";
            pl["lossless"] = "on";

            var bytes = J2kImage.ToBytes(src, meta, pl, null, null, dco)!;
            var reader = ReadBoxes(bytes);

            Assert.NotNull(reader.Metadata.ReaderRequirements);
            Assert.Contains((ushort)99, reader.Metadata.ReaderRequirements!.StandardFeatures);
        }
    }
}
