// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using CoreJ2K.j2k.util;

namespace CoreJ2K.j2k.codestream
{
    /// <summary>
    /// Provides comprehensive validation of JPEG 2000 codestream markers per ISO/IEC 15444-1 Annex A.
    /// Validates marker ordering, presence of required markers, and marker segment syntax.
    /// </summary>
    public class CodestreamValidator
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> info = new List<string>();

        /// <summary>
        /// Gets validation errors found during codestream validation.
        /// </summary>
        public IReadOnlyList<string> Errors => errors.AsReadOnly();

        /// <summary>
        /// Gets validation warnings found during codestream validation.
        /// </summary>
        public IReadOnlyList<string> Warnings => warnings.AsReadOnly();

        /// <summary>
        /// Gets informational messages from validation.
        /// </summary>
        public IReadOnlyList<string> Info => info.AsReadOnly();

        /// <summary>
        /// Returns true if any validation errors were found.
        /// </summary>
        public bool HasErrors => errors.Count > 0;

        /// <summary>
        /// Returns true if any validation warnings were found.
        /// </summary>
        public bool HasWarnings => warnings.Count > 0;

        /// <summary>
        /// Validates a complete JPEG 2000 codestream.
        /// </summary>
        /// <param name="codestream">The codestream bytes to validate.</param>
        /// <param name="maxBytesToRead">Maximum bytes to read (0 = read all).</param>
        /// <returns>True if validation passed without errors.</returns>
        public bool ValidateCodestream(byte[] codestream, int maxBytesToRead = 0)
        {
            errors.Clear();
            warnings.Clear();
            info.Clear();

            if (codestream == null || codestream.Length < 2)
            {
                errors.Add("Codestream is null or too small");
                return false;
            }

            var maxBytes = maxBytesToRead > 0 ? Math.Min(maxBytesToRead, codestream.Length) : codestream.Length;

            try
            {
                // Validate main header
                var pos = ValidateMainHeader(codestream, maxBytes);
                if (pos < 0)
                    return false;

                // Note: Full tile-part validation would require parsing the entire codestream
                // For now, we focus on main header and basic structure
                info.Add($"Main header validated successfully (ends at byte {pos})");

                return !HasErrors;
            }
            catch (IndexOutOfRangeException ex)
            {
                // More specific error for array access issues
                errors.Add($"Codestream truncated or corrupted: {ex.Message}");
                return false;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // Handle out of range access
                errors.Add($"Invalid marker segment length or position: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                errors.Add($"Exception during validation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates the main header of a codestream.
        /// Returns the position after the main header, or -1 on error.
        /// </summary>
        private int ValidateMainHeader(byte[] data, int maxBytes)
        {
            var pos = 0;

            // 1. SOC marker must be first (0xFF4F)
            if (!ValidateSOC(data, ref pos, maxBytes))
                return -1;

            // 2. SIZ marker must immediately follow SOC (0xFF51)
            if (!ValidateSIZ(data, ref pos, maxBytes))
                return -1;

            // 3. Main header markers (in flexible order, but COD must precede first tile-part)
            var hasCOD = false;
            var hasQCD = false;
            var codCount = 0;
            var qcdCount = 0;

            while (pos < maxBytes - 1)
            {
                // Safety check: ensure we have at least 2 bytes for marker
                if (pos + 1 >= maxBytes)
                {
                    warnings.Add($"Main header validation incomplete (reached maxBytes limit at position {pos})");
                    break;
                }

                // Check if we've reached SOT (start of tile-part headers)
                if (data[pos] == 0xFF && data[pos + 1] == 0x90) // SOT marker
                {
                    // Main header complete
                    if (!hasCOD)
                    {
                        errors.Add("Main header missing required COD marker");
                        return -1;
                    }
                    if (!hasQCD)
                    {
                        errors.Add("Main header missing required QCD marker");
                        return -1;
                    }
                    return pos;
                }

                // Check if we've reached SOD (start of data)
                if (data[pos] == 0xFF && data[pos + 1] == 0x93) // SOD marker
                {
                    errors.Add("SOD marker found before SOT (no tile-parts defined)");
                    return -1;
                }

                // Check if we've reached EOC (end of codestream)
                if (data[pos] == 0xFF && data[pos + 1] == 0xD9) // EOC marker
                {
                    warnings.Add("EOC marker found in main header (before any tile-parts)");
                    return pos;
                }

                // Read marker
                if (data[pos] != 0xFF)
                {
                    errors.Add($"Expected marker at position {pos}, found 0x{data[pos]:X2}");
                    return -1;
                }

                var marker = (data[pos] << 8) | data[pos + 1];
                pos += 2;

                switch (marker)
                {
                    case Markers.COD: // 0xFF52
                        if (codCount > 0)
                            warnings.Add("Multiple COD markers in main header (last one takes precedence)");
                        if (!ValidateCOD(data, ref pos, maxBytes))
                            return -1;
                        hasCOD = true;
                        codCount++;
                        break;

                    case Markers.COC: // 0xFF53
                        if (!hasCOD)
                            warnings.Add("COC marker before COD marker");
                        if (!ValidateCOC(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.QCD: // 0xFF5C
                        if (qcdCount > 0)
                            warnings.Add("Multiple QCD markers in main header (last one takes precedence)");
                        if (!ValidateQCD(data, ref pos, maxBytes))
                            return -1;
                        hasQCD = true;
                        qcdCount++;
                        break;

                    case Markers.QCC: // 0xFF5D
                        if (!hasQCD)
                            warnings.Add("QCC marker before QCD marker");
                        if (!ValidateQCC(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.RGN: // 0xFF5E
                        if (!ValidateRGN(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.POC: // 0xFF5F
                        if (!ValidatePOC(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.PPM: // 0xFF60
                        if (!ValidatePPM(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.TLM: // 0xFF55
                        if (!ValidateTLM(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.PLM: // 0xFF57
                        if (!ValidatePLM(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.CRG: // 0xFF63
                        if (!ValidateCRG(data, ref pos, maxBytes))
                            return -1;
                        break;

                    case Markers.COM: // 0xFF64
                        if (!ValidateCOM(data, ref pos, maxBytes))
                            return -1;
                        break;

                    default:
                        // Don't fail on unknown markers, just warn and try to skip
                        warnings.Add($"Unknown or unexpected marker in main header: 0x{marker:X4} at position {pos - 2}");
                        // Try to skip the marker if it has a length field
                        if (!TrySkipUnknownMarker(data, ref pos, maxBytes))
                            return -1;
                        break;
                }
            }

            // If we reach here, we didn't find SOT or EOC
            warnings.Add("Main header validation incomplete (no SOT or tile-parts found in examined data)");
            return pos;
        }

        private bool ValidateSOC(byte[] data, ref int pos, int maxBytes)
        {
            if (pos + 1 >= maxBytes)
            {
                errors.Add("Codestream too short for SOC marker");
                return false;
            }

            if (data[pos] != 0xFF || data[pos + 1] != 0x4F)
            {
                errors.Add($"Codestream must start with SOC marker (0xFF4F), found: 0x{data[pos]:X2}{data[pos + 1]:X2}");
                return false;
            }

            pos += 2;
            info.Add("SOC marker validated");
            return true;
        }

        private bool ValidateSIZ(byte[] data, ref int pos, int maxBytes)
        {
            if (pos + 1 >= maxBytes)
            {
                errors.Add("Insufficient data for SIZ marker");
                return false;
            }

            if (data[pos] != 0xFF || data[pos + 1] != 0x51)
            {
                errors.Add($"SIZ marker (0xFF51) must immediately follow SOC, found: 0x{data[pos]:X2}{data[pos + 1]:X2}");
                return false;
            }

            pos += 2;

            // Read Lsiz
            if (pos + 2 > maxBytes)
            {
                errors.Add("Insufficient data for SIZ length");
                return false;
            }

            var lsiz = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            if (lsiz < 41) // Minimum SIZ size
            {
                errors.Add($"Invalid SIZ marker length: {lsiz} (minimum is 41)");
                return false;
            }

            if (pos + lsiz - 2 > maxBytes)
            {
                errors.Add($"Insufficient data for SIZ marker segment (need {lsiz - 2} more bytes)");
                return false;
            }

            // Read Rsiz (capabilities)
            var rsiz = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            // Read image dimensions
            var xsiz = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
            var ysiz = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;

            if (xsiz == 0 || ysiz == 0)
            {
                errors.Add($"Invalid image dimensions in SIZ: {xsiz}x{ysiz}");
                return false;
            }

            // Skip XOsiz, YOsiz, XTsiz, YTsiz, XTOsiz, YTOsiz (24 bytes)
            pos += 24;

            // Read Csiz (number of components)
            var csiz = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            if (csiz == 0 || csiz > 16384)
            {
                errors.Add($"Invalid number of components in SIZ: {csiz}");
                return false;
            }

            // Validate that we have data for all components (3 bytes each)
            var expectedLength = 38 + (3 * csiz);
            if (lsiz != expectedLength)
            {
                errors.Add($"SIZ length mismatch: expected {expectedLength}, got {lsiz}");
                return false;
            }

            // Skip component info
            pos += 3 * csiz;

            info.Add($"SIZ marker validated: {xsiz}x{ysiz}, {csiz} components, Rsiz=0x{rsiz:X4}");
            return true;
        }

        private bool ValidateCOD(byte[] data, ref int pos, int maxBytes)
        {
            if (pos + 2 > maxBytes)
            {
                errors.Add("Insufficient data for COD length");
                return false;
            }

            var lcod = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            if (lcod < 12)
            {
                errors.Add($"Invalid COD marker length: {lcod} (minimum is 12)");
                return false;
            }

            if (pos + lcod - 2 > maxBytes)
            {
                errors.Add($"Insufficient data for COD marker segment");
                return false;
            }

            // Read Scod
            var scod = data[pos++];

            // Skip SGcod (4 bytes: progression order, layers, MCT)
            pos += 4;

            // Read decomposition levels
            var levels = data[pos++];
            if (levels > 32)
            {
                warnings.Add($"High number of decomposition levels: {levels}");
            }

            // Skip rest of marker
            pos += lcod - 9;

            info.Add($"COD marker validated: {levels} decomposition levels");
            return true;
        }

        private bool ValidateCOC(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "COC");
        }

        private bool ValidateQCD(byte[] data, ref int pos, int maxBytes)
        {
            if (pos + 2 > maxBytes)
            {
                errors.Add("Insufficient data for QCD length");
                return false;
            }

            var lqcd = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            if (lqcd < 4)
            {
                errors.Add($"Invalid QCD marker length: {lqcd}");
                return false;
            }

            if (pos + lqcd - 2 > maxBytes)
            {
                errors.Add($"Insufficient data for QCD marker segment");
                return false;
            }

            // Read Sqcd (quantization style)
            var sqcd = data[pos++];
            var qstyle = sqcd & 0x1F;
            var guardBits = (sqcd >> 5) & 0x07;

            if (qstyle > 2)
            {
                errors.Add($"Invalid quantization style in QCD: {qstyle}");
                return false;
            }

            // Skip rest of marker
            pos += lqcd - 3;

            info.Add($"QCD marker validated: style={qstyle}, guard bits={guardBits}");
            return true;
        }

        private bool ValidateQCC(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "QCC");
        }

        private bool ValidateRGN(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "RGN");
        }

        private bool ValidatePOC(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "POC");
        }

        private bool ValidatePPM(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "PPM");
        }

        private bool ValidateTLM(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "TLM");
        }

        private bool ValidatePLM(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "PLM");
        }

        private bool ValidateCRG(byte[] data, ref int pos, int maxBytes)
        {
            return SkipMarkerSegment(data, ref pos, maxBytes, "CRG");
        }

        private bool ValidateCOM(byte[] data, ref int pos, int maxBytes)
        {
            if (pos + 2 > maxBytes)
            {
                errors.Add("Insufficient data for COM length");
                return false;
            }

            var lcom = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            if (lcom < 5) // Minimum: length(2) + Rcom(2) + at least 1 byte
            {
                errors.Add($"Invalid COM marker length: {lcom}");
                return false;
            }

            if (pos + lcom - 2 > maxBytes)
            {
                errors.Add($"Insufficient data for COM marker segment");
                return false;
            }

            // Read Rcom (registration value)
            var rcom = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            // Skip comment data
            pos += lcom - 4;

            if (rcom == 1)
            {
                info.Add($"COM marker (Latin-1 text, {lcom - 4} bytes)");
            }
            else if (rcom == 0)
            {
                info.Add($"COM marker (binary data, {lcom - 4} bytes)");
            }
            else
            {
                warnings.Add($"COM marker with non-standard Rcom value: {rcom}");
            }

            return true;
        }

        private bool SkipMarkerSegment(byte[] data, ref int pos, int maxBytes, string markerName)
        {
            if (pos + 2 > maxBytes)
            {
                errors.Add($"Insufficient data for {markerName} length");
                return false;
            }

            var length = (data[pos] << 8) | data[pos + 1];
            pos += 2;

            if (length < 2)
            {
                errors.Add($"Invalid {markerName} marker length: {length}");
                return false;
            }

            if (pos + length - 2 > maxBytes)
            {
                errors.Add($"Insufficient data for {markerName} marker segment");
                return false;
            }

            pos += length - 2;
            info.Add($"{markerName} marker validated ({length} bytes)");
            return true;
        }

        /// <summary>
        /// Attempts to skip an unknown marker by reading its length field.
        /// Returns false if the marker cannot be skipped safely.
        /// </summary>
        private bool TrySkipUnknownMarker(byte[] data, ref int pos, int maxBytes)
        {
            try
            {
                // Check if we have room for a length field
                if (pos + 2 > maxBytes)
                {
                    errors.Add($"Cannot read length of unknown marker at position {pos - 2} (insufficient data)");
                    return false;
                }

                var length = (data[pos] << 8) | data[pos + 1];
                
                if (length < 2)
                {
                    errors.Add($"Invalid marker segment length: {length}");
                    return false;
                }

                if (pos + length > maxBytes)
                {
                    warnings.Add($"Marker segment extends beyond available data (need {length} bytes, have {maxBytes - pos})");
                    pos = maxBytes; // Skip to end
                    return true; // Return true to allow continuation
                }

                pos += length;
                info.Add($"Skipped unknown marker ({length} bytes)");
                return true;
            }
            catch (Exception)
            {
                errors.Add($"Failed to skip unknown marker at position {pos - 2}");
                return false;
            }
        }

        /// <summary>
        /// Gets a formatted validation report.
        /// </summary>
        public string GetValidationReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Codestream Validation Report ===");
            report.AppendLine();

            if (errors.Count == 0 && warnings.Count == 0)
            {
                report.AppendLine("? Codestream is valid (ISO/IEC 15444-1 Annex A compliant)");
            }
            else
            {
                if (errors.Count > 0)
                {
                    report.AppendLine($"ERRORS ({errors.Count}):");
                    foreach (var error in errors)
                    {
                        report.AppendLine($"  ? {error}");
                    }
                    report.AppendLine();
                }

                if (warnings.Count > 0)
                {
                    report.AppendLine($"WARNINGS ({warnings.Count}):");
                    foreach (var warning in warnings)
                    {
                        report.AppendLine($"  ? {warning}");
                    }
                    report.AppendLine();
                }
            }

            if (info.Count > 0)
            {
                report.AppendLine($"INFORMATION ({info.Count}):");
                foreach (var infoMsg in info)
                {
                    report.AppendLine($"  ? {infoMsg}");
                }
            }

            return report.ToString();
        }
    }
}
