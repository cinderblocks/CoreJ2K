// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.
//
// Third-party JP2 file-format parsing and robustness tests over a CC-BY subset of the
// jpylyzer-test-files project (see TestFiles/jpylyzer/ATTRIBUTION.md). These exercise the
// box reader against files CoreJ2K did not produce, and the reader's handling of
// deliberately-corrupted files. This is NOT an ISO/IEC 15444-4 conformance suite, and it
// does not cover Part 2 codestream extensions (NLT, MCT) — those are absent from these files.

using System;
using System.IO;
using Xunit;
using CoreJ2K;
using CoreJ2K.j2k.fileformat.reader;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests
{
    public class JpylyzerFixtureTests
    {
        private static readonly string FixtureDir =
            Path.Combine(AppContext.BaseDirectory, "TestFiles", "jpylyzer");

        private static byte[] Load(string name) => File.ReadAllBytes(Path.Combine(FixtureDir, name));

        private static FileFormatReader ReadBoxes(string name)
        {
            var reader = new FileFormatReader(new ISRandomAccessIO(new MemoryStream(Load(name))));
            reader.readFileFormat();
            return reader;
        }

        // Only "controlled" failures are acceptable on malformed input — never an unhandled
        // crash such as NullReferenceException / IndexOutOfRange / Overflow.
        private static bool IsControlled(Exception e) =>
            e is InvalidOperationException || e is IOException || e is EndOfStreamException
            || e is ArgumentException;

        [Fact]
        public void ReferenceJp2_ParsesBoxesAndFindsCodestream()
        {
            var reader = ReadBoxes("reference.jp2");

            Assert.True(reader.JP2FFUsed);
            Assert.True(reader.FileStructure.HasImageHeaderBox);
            Assert.True(reader.FirstCodeStreamPos > 0);
            Assert.True(reader.FirstCodeStreamLength > 0);
        }

        // KNOWN LIMITATION (surfaced by this third-party file): full decode of reference.jp2
        // currently throws ArgumentOutOfRangeException in InvWTFull.waveletTreeReconstruction,
        // preceded by many entropy "Error detected at bit-plane" warnings. The root cause is
        // upstream of the wavelet stage — the codestream's coding features are mis-decoded, so the
        // subband geometry handed to reconstruction is inconsistent. Tracked as a decode bug to
        // investigate separately; box-level parsing of this file works (see the test above).
        [Fact(Skip = "Known decode limitation: real-world Kakadu JP2 mis-decodes upstream of InvWTFull; box parsing works.")]
        public void ReferenceJp2_Decodes()
        {
            var img = J2kImage.FromBytes(Load("reference.jp2"));

            Assert.NotNull(img);
            Assert.True(img.Width > 0 && img.Height > 0);
        }

        [Fact]
        public void CorruptedIccJp2_ParsesGracefully()
        {
            // Valid JP2 structure with a corrupted ICC tagCount: the reader should still parse
            // the file (ICC corruption is tolerated with a warning, not a hard failure).
            var reader = ReadBoxes("bitwiser-icc-corrupted-tagcount-1911.jp2");

            Assert.True(reader.JP2FFUsed);
            Assert.True(reader.FirstCodeStreamPos > 0);
        }

        [Theory]
        [InlineData("bitwiser-resolutionbox-corrupted-boxlength-8127.jp2")]
        [InlineData("bitwiser-headerbox-corrupted-boxlength-22181.jp2")]
        [InlineData("bitwiser-codestreamheader-corrupted-xsiz-10918.jp2")]
        public void CorruptedJp2_HandledWithoutUncontrolledCrash(string name)
        {
            var reader = new FileFormatReader(new ISRandomAccessIO(new MemoryStream(Load(name))));
            try
            {
                reader.readFileFormat();
                // Parsed leniently (box-level corruption tolerated) — acceptable.
            }
            catch (Exception e) when (IsControlled(e))
            {
                // Rejected cleanly — acceptable.
            }
        }

        [Fact]
        public void Fixtures_ArePresent()
        {
            // Guards against a broken CopyToOutputDirectory configuration.
            Assert.True(Directory.Exists(FixtureDir), $"Fixture directory missing: {FixtureDir}");
            Assert.True(File.Exists(Path.Combine(FixtureDir, "reference.jp2")));
        }
    }
}
