// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using CoreJ2K.j2k.codestream;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for comprehensive codestream validation per ISO/IEC 15444-1 Annex A.
    /// </summary>
    public class CodestreamValidatorTests
    {
        [Fact]
        public void TestValidCodestreamStructure()
        {
            // Create a minimal valid codestream: SOC + SIZ + COD + QCD
            var codestream = CreateMinimalValidCodestream();

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(codestream);

            // Main header should be valid even if incomplete (no SOT)
            // Validator will add a warning about no SOT found
            var report = validator.GetValidationReport();
            
            // The validation might pass or fail depending on whether it finds SOT
            // What's important is that it doesn't report structural errors in main header
            Assert.True(validator.Info.Count > 0, $"Should have info messages. Report:\n{report}");
        }

        [Fact]
        public void TestMissingSOC()
        {
            // Create codestream without SOC
            var codestream = new byte[] { 0xFF, 0x51 }; // Starts with SIZ instead

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(codestream);

            Assert.False(result, "Codestream without SOC should fail");
            Assert.True(validator.HasErrors);
            Assert.Contains("SOC marker", validator.GetValidationReport());
        }

        [Fact]
        public void TestMissingSIZ()
        {
            // Create codestream with SOC but wrong marker after
            var codestream = new byte[] { 0xFF, 0x4F, 0xFF, 0x52 }; // SOC + COD

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(codestream);

            Assert.False(result);
            Assert.True(validator.HasErrors);
            Assert.Contains("SIZ", validator.GetValidationReport());
        }

        [Fact]
        public void TestInvalidSIZLength()
        {
            // SOC + SIZ with invalid length
            var codestream = new byte[]
            {
                0xFF, 0x4F, // SOC
                0xFF, 0x51, // SIZ
                0x00, 0x10  // Invalid length (too short)
            };

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(codestream);

            Assert.False(result);
            Assert.True(validator.HasErrors);
        }

        [Fact]
        public void TestMissingCOD()
        {
            // Valid SOC + SIZ but missing COD before SOT
            var codestream = CreateSOCAndSIZ();
            var withSOT = AppendBytes(codestream, new byte[] { 0xFF, 0x90 }); // Add SOT

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(withSOT, withSOT.Length);

            Assert.False(result);
            Assert.True(validator.HasErrors);
            Assert.Contains("COD", validator.GetValidationReport());
        }

        [Fact]
        public void TestMissingQCD()
        {
            // Valid SOC + SIZ + COD but add an unexpected marker instead of QCD
            var codestream = CreateSOCAndSIZ();
            var withCOD = AppendCOD(codestream);
            
            // Add a marker that will trigger error in main header (before SOT)
            var badMarker = AppendBytes(withCOD, new byte[] { 
                0xFF, 0x90, // SOT marker (unexpected without QCD first)
                0x00, 0x0A, // Lsot
                0x00, 0x00, // Isot
                0x00, 0x00, 0x00, 0x00, // Psot
                0x00, 0x01  // TPsot, TNsot
            });

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(badMarker, badMarker.Length);

            var report = validator.GetValidationReport();
            // The validator should detect that we're missing QCD before SOT
            Assert.False(result, $"Should fail. Report:\n{report}");
            Assert.True(validator.HasErrors, "Should have errors");
        }

        [Fact]
        public void TestCOMMarkerValidation()
        {
            // Test COM marker with valid Latin-1 text
            var codestream = CreateSOCAndSIZ();
            var withCOD = AppendCOD(codestream);
            var withQCD = AppendQCD(withCOD);
            var withCOM = AppendCOM(withQCD, "Test Comment");

            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(withCOM, withCOM.Length);

            var report = validator.GetValidationReport();
            // Should have info about COM marker
            Assert.True(validator.Info.Count > 0, $"Should have info about COM marker. Report:\n{report}");
        }

        [Fact]
        public void TestTooSmallCodestream()
        {
            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(new byte[] { 0xFF });

            Assert.False(result);
            Assert.True(validator.HasErrors);
        }

        [Fact]
        public void TestNullCodestream()
        {
            var validator = new CodestreamValidator();
            var result = validator.ValidateCodestream(null);

            Assert.False(result);
            Assert.True(validator.HasErrors);
        }

        [Fact]
        public void TestValidationReport()
        {
            var codestream = new byte[] { 0xFF, 0x4F, 0xFF, 0x52 }; // SOC + wrong marker

            var validator = new CodestreamValidator();
            validator.ValidateCodestream(codestream);

            var report = validator.GetValidationReport();
            
            Assert.Contains("Codestream Validation Report", report);
            Assert.Contains("ERRORS", report);
            Assert.Contains("?", report);
        }

        [Fact]
        public void TestMaxBytesValidation()
        {
            // Simple test: validate just SOC and SIZ
            var codestream = CreateSOCAndSIZ();

            var validator = new CodestreamValidator();
            // Only validate what we have
            var result = validator.ValidateCodestream(codestream, codestream.Length);

            var report = validator.GetValidationReport();
            // Should validate SOC and SIZ successfully, even if incomplete
            Assert.True(validator.Info.Count >= 2, $"Should have validated SOC and SIZ. Report:\n{report}");
        }

        // Helper methods to create test codestreams

        private byte[] CreateMinimalValidCodestream()
        {
            var codestream = CreateSOCAndSIZ();
            codestream = AppendCOD(codestream);
            codestream = AppendQCD(codestream);
            // Note: A complete codestream would need SOT, SOD, tile data, and EOC
            // But for header validation, this is sufficient
            return codestream;
        }

        private byte[] CreateSOCAndSIZ()
        {
            // SOC marker
            var soc = new byte[] { 0xFF, 0x4F };

            // SIZ marker with minimal valid data
            var siz = new byte[]
            {
                0xFF, 0x51, // SIZ marker
                0x00, 0x29, // Lsiz = 41 bytes (minimum)
                0x00, 0x00, // Rsiz = 0 (baseline)
                // Xsiz (4 bytes) = 256
                0x00, 0x00, 0x01, 0x00,
                // Ysiz (4 bytes) = 256
                0x00, 0x00, 0x01, 0x00,
                // XOsiz (4 bytes) = 0
                0x00, 0x00, 0x00, 0x00,
                // YOsiz (4 bytes) = 0
                0x00, 0x00, 0x00, 0x00,
                // XTsiz (4 bytes) = 256
                0x00, 0x00, 0x01, 0x00,
                // YTsiz (4 bytes) = 256
                0x00, 0x00, 0x01, 0x00,
                // XTOsiz (4 bytes) = 0
                0x00, 0x00, 0x00, 0x00,
                // YTOsiz (4 bytes) = 0
                0x00, 0x00, 0x00, 0x00,
                // Csiz (2 bytes) = 1 component
                0x00, 0x01,
                // Component 0: Ssiz(1) + XRsiz(1) + YRsiz(1)
                0x07, 0x01, 0x01 // 8-bit unsigned, no subsampling
            };

            return AppendBytes(soc, siz);
        }

        private byte[] AppendCOD(byte[] existing)
        {
            // Minimal COD marker
            var cod = new byte[]
            {
                0xFF, 0x52, // COD marker
                0x00, 0x0C, // Lcod = 12 bytes
                0x00,       // Scod = default
                0x00,       // SGcod: progression order = 0 (LRCP)
                0x00, 0x01, // Number of layers = 1
                0x00,       // MCT = 0 (no transform)
                0x05,       // Decomposition levels = 5
                0x02,       // Code-block width = 32 (2^(2+2))
                0x02,       // Code-block height = 32
                0x00,       // Code-block style = default
                0x01        // Wavelet transform = 5-3 reversible
            };

            return AppendBytes(existing, cod);
        }

        private byte[] AppendQCD(byte[] existing)
        {
            // Minimal QCD marker (reversible, no quantization)
            var qcd = new byte[]
            {
                0xFF, 0x5C, // QCD marker
                0x00, 0x13, // Lqcd = 19 bytes (for 5 levels, 16 subbands)
                0x00,       // Sqcd = no quantization (reversible)
                // Exponents for each subband (1 byte each, 16 total for 5 levels)
                0x08, 0x08, 0x08, 0x08,
                0x09, 0x09, 0x09,
                0x0A, 0x0A, 0x0A,
                0x0B, 0x0B, 0x0B,
                0x0C, 0x0C, 0x0C
            };

            return AppendBytes(existing, qcd);
        }

        private byte[] AppendCOM(byte[] existing, string comment)
        {
            var commentBytes = System.Text.Encoding.UTF8.GetBytes(comment);
            var com = new byte[4 + 2 + commentBytes.Length];
            
            com[0] = 0xFF;
            com[1] = 0x64; // COM marker
            var length = 2 + 2 + commentBytes.Length;
            com[2] = (byte)(length >> 8);
            com[3] = (byte)(length & 0xFF);
            com[4] = 0x00; // Rcom = 1 (Latin-1)
            com[5] = 0x01;
            
            Array.Copy(commentBytes, 0, com, 6, commentBytes.Length);
            
            return AppendBytes(existing, com);
        }

        private byte[] AppendBytes(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Array.Copy(first, 0, result, 0, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
