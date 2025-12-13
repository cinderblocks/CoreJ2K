// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;

namespace CoreJ2K.j2k.fileformat.reader
{
    /// <summary>
    /// Provides comprehensive validation for JPEG 2000 JP2 file format per ISO/IEC 15444-1.
    /// Validates box ordering, required boxes, and proper structure.
    /// </summary>
    public class JP2Validator
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        /// <summary>
        /// Gets validation errors found during JP2 file format validation.
        /// </summary>
        public IReadOnlyList<string> Errors => errors.AsReadOnly();

        /// <summary>
        /// Gets validation warnings found during JP2 file format validation.
        /// </summary>
        public IReadOnlyList<string> Warnings => warnings.AsReadOnly();

        /// <summary>
        /// Returns true if any validation errors were found.
        /// </summary>
        public bool HasErrors => errors.Count > 0;

        /// <summary>
        /// Returns true if any validation warnings were found.
        /// </summary>
        public bool HasWarnings => warnings.Count > 0;

        /// <summary>
        /// Validates the JP2 file format structure.
        /// </summary>
        public void ValidateFileFormat(JP2Structure structure)
        {
            errors.Clear();
            warnings.Clear();

            // Validate signature box
            ValidateSignatureBox(structure);

            // Validate file type box
            ValidateFileTypeBox(structure);

            // Validate JP2 header box
            ValidateJP2HeaderBox(structure);

            // Validate codestream box
            ValidateCodestreamBox(structure);

            // Validate box ordering
            ValidateBoxOrdering(structure);
        }

        /// <summary>
        /// Validates the JP2 Signature Box per ISO/IEC 15444-1 Section I.5.1.
        /// </summary>
        private void ValidateSignatureBox(JP2Structure structure)
        {
            if (!structure.HasSignatureBox)
            {
                errors.Add("JP2 Signature Box is missing (required per ISO/IEC 15444-1 Section I.5.1)");
                return;
            }

            if (structure.SignatureBoxPosition != 0)
            {
                errors.Add($"JP2 Signature Box must be first box in file (found at position {structure.SignatureBoxPosition})");
            }

            if (structure.SignatureBoxLength != 12)
            {
                errors.Add($"JP2 Signature Box must be exactly 12 bytes (found {structure.SignatureBoxLength} bytes)");
            }
        }

        /// <summary>
        /// Validates the File Type Box per ISO/IEC 15444-1 Section I.5.2.
        /// </summary>
        private void ValidateFileTypeBox(JP2Structure structure)
        {
            if (!structure.HasFileTypeBox)
            {
                errors.Add("File Type Box is missing (required per ISO/IEC 15444-1 Section I.5.2)");
                return;
            }

            // File Type box should immediately follow signature box (at position 12)
            if (structure.HasSignatureBox && structure.FileTypeBoxPosition != 12)
            {
                warnings.Add($"File Type Box should immediately follow JP2 Signature Box (found at position {structure.FileTypeBoxPosition}, expected at 12)");
            }

            if (!structure.HasValidBrand)
            {
                errors.Add("File Type Box must have 'jp2 ' (0x6a703220) as brand for JP2 compliance");
            }

            if (!structure.HasJP2Compatibility)
            {
                errors.Add("File Type Box compatibility list must include 'jp2 ' (0x6a703220)");
            }

            if (structure.FileTypeBoxLength < 20)
            {
                errors.Add($"File Type Box is too short: {structure.FileTypeBoxLength} bytes (minimum 20 bytes)");
            }
        }

        /// <summary>
        /// Validates the JP2 Header Box per ISO/IEC 15444-1 Section I.5.3.
        /// </summary>
        private void ValidateJP2HeaderBox(JP2Structure structure)
        {
            if (!structure.HasJP2HeaderBox)
            {
                errors.Add("JP2 Header Box is missing (required per ISO/IEC 15444-1 Section I.5.3)");
                return;
            }

            // Validate required sub-boxes
            if (!structure.HasImageHeaderBox)
            {
                errors.Add("Image Header Box is missing from JP2 Header (required per ISO/IEC 15444-1 Section I.5.3.1)");
            }
            else if (structure.ImageHeaderBoxOrder != 0)
            {
                errors.Add($"Image Header Box must be first box in JP2 Header (found at position {structure.ImageHeaderBoxOrder})");
            }

            if (!structure.HasColourSpecificationBox)
            {
                errors.Add("Colour Specification Box is missing from JP2 Header (required per ISO/IEC 15444-1 Section I.5.3.3)");
            }

            // Validate optional boxes ordering
            if (structure.HasPaletteBox && structure.HasComponentMappingBox)
            {
                if (structure.PaletteBoxOrder >= structure.ComponentMappingBoxOrder)
                {
                    errors.Add("Palette Box must appear before Component Mapping Box in JP2 Header");
                }
            }

            if (structure.HasComponentMappingBox && !structure.HasPaletteBox)
            {
                warnings.Add("Component Mapping Box present but no Palette Box found (unusual configuration)");
            }

            // Validate bits per component box when needed
            if (structure.ImageHeaderBPCValue == 0xFF && !structure.HasBitsPerComponentBox)
            {
                errors.Add("Bits Per Component Box is required when Image Header BPC field is 0xFF");
            }

            if (!structure.ImageHeaderBPCValue.HasValue || structure.ImageHeaderBPCValue == 0xFF)
            {
                if (!structure.HasBitsPerComponentBox)
                {
                    warnings.Add("Could not determine bit depth information (no Image Header or Bits Per Component Box)");
                }
            }
        }

        /// <summary>
        /// Validates the Contiguous Codestream Box per ISO/IEC 15444-1 Section I.5.4.
        /// </summary>
        private void ValidateCodestreamBox(JP2Structure structure)
        {
            if (!structure.HasContiguousCodestreamBox)
            {
                errors.Add("Contiguous Codestream Box is missing (required per ISO/IEC 15444-1 Section I.5.4)");
                return;
            }

            if (structure.ContiguousCodestreamBoxPosition < structure.JP2HeaderBoxPosition + structure.JP2HeaderBoxLength)
            {
                errors.Add("Contiguous Codestream Box must appear after JP2 Header Box");
            }
        }

        /// <summary>
        /// Validates the overall box ordering per ISO/IEC 15444-1.
        /// </summary>
        private void ValidateBoxOrdering(JP2Structure structure)
        {
            // Validate top-level box order: Signature -> FileType -> JP2Header -> Codestream
            var expectedOrder = new List<string> { "Signature", "FileType", "JP2Header", "Codestream" };
            var actualOrder = structure.GetTopLevelBoxOrder();

            for (int i = 0; i < Math.Min(expectedOrder.Count, actualOrder.Count); i++)
            {
                if (expectedOrder[i] != actualOrder[i])
                {
                    warnings.Add($"Unexpected box ordering: expected {expectedOrder[i]} at position {i}, found {actualOrder[i]}");
                }
            }

            // Check for multiple JP2 Header boxes
            if (structure.JP2HeaderBoxCount > 1)
            {
                errors.Add($"Multiple JP2 Header Boxes found ({structure.JP2HeaderBoxCount}). Only one is allowed.");
            }

            // Check for boxes appearing before JP2 Header that shouldn't
            if (structure.HasMetadataBeforeHeader)
            {
                warnings.Add("Metadata boxes (XML, UUID) found before JP2 Header Box (non-standard location)");
            }
        }

        /// <summary>
        /// Gets a formatted validation report.
        /// </summary>
        public string GetValidationReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== JP2 File Format Validation Report ===");
            report.AppendLine();

            if (errors.Count == 0 && warnings.Count == 0)
            {
                report.AppendLine("? File is valid JP2 format (ISO/IEC 15444-1 compliant)");
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
                }
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// Represents the structure of a JP2 file for validation purposes.
    /// </summary>
    public class JP2Structure
    {
        // Signature Box
        public bool HasSignatureBox { get; set; }
        public int SignatureBoxPosition { get; set; }
        public int SignatureBoxLength { get; set; }

        // File Type Box
        public bool HasFileTypeBox { get; set; }
        public int FileTypeBoxPosition { get; set; }
        public int FileTypeBoxLength { get; set; }
        public bool HasValidBrand { get; set; }
        public bool HasJP2Compatibility { get; set; }
        public int MinorVersion { get; set; }

        // JP2 Header Box
        public bool HasJP2HeaderBox { get; set; }
        public int JP2HeaderBoxPosition { get; set; }
        public int JP2HeaderBoxLength { get; set; }
        public int JP2HeaderBoxCount { get; set; }

        // JP2 Header Sub-boxes
        public bool HasImageHeaderBox { get; set; }
        public int ImageHeaderBoxOrder { get; set; }
        public byte? ImageHeaderBPCValue { get; set; }

        public bool HasColourSpecificationBox { get; set; }
        public int ColourSpecificationBoxOrder { get; set; }

        public bool HasBitsPerComponentBox { get; set; }
        public int BitsPerComponentBoxOrder { get; set; }

        public bool HasPaletteBox { get; set; }
        public int PaletteBoxOrder { get; set; }

        public bool HasComponentMappingBox { get; set; }
        public int ComponentMappingBoxOrder { get; set; }

        public bool HasChannelDefinitionBox { get; set; }
        public int ChannelDefinitionBoxOrder { get; set; }

        public bool HasResolutionBox { get; set; }
        public int ResolutionBoxOrder { get; set; }

        // Contiguous Codestream Box
        public bool HasContiguousCodestreamBox { get; set; }
        public int ContiguousCodestreamBoxPosition { get; set; }

        // Other boxes
        public bool HasMetadataBeforeHeader { get; set; }

        /// <summary>
        /// Gets the top-level box order as a list of box names.
        /// </summary>
        public List<string> GetTopLevelBoxOrder()
        {
            var order = new List<(int position, string name)>();

            if (HasSignatureBox)
                order.Add((SignatureBoxPosition, "Signature"));
            if (HasFileTypeBox)
                order.Add((FileTypeBoxPosition, "FileType"));
            if (HasJP2HeaderBox)
                order.Add((JP2HeaderBoxPosition, "JP2Header"));
            if (HasContiguousCodestreamBox)
                order.Add((ContiguousCodestreamBoxPosition, "Codestream"));

            order.Sort((a, b) => a.position.CompareTo(b.position));

            return order.ConvertAll(item => item.name);
        }
    }
}
