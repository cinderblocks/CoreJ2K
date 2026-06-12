# CoreJ2K Documentation

## Guides

User-facing how-tos for encoding, decoding, and metadata.

| Guide | Description |
|-------|-------------|
| [Quick Reference](QUICK_REFERENCE.md) | Installation, basic encode/decode, common patterns |
| [Complete Builder Guide](COMPLETE_BUILDER_GUIDE.md) | Unified fluent API with presets — start here |
| [Encoder Configuration Guide](ENCODER_CONFIGURATION_GUIDE.md) | `J2KEncoderConfiguration` — bitrate, tiles, quality |
| [Decoder Configuration Guide](DECODER_CONFIGURATION_GUIDE.md) | `J2KDecoderConfiguration` — resolution, layers, color |
| [Wavelet Configuration Guide](WAVELET_CONFIGURATION_GUIDE.md) | Filter selection (5/3 vs 9/7) and decomposition levels |
| [Progression Configuration Guide](PROGRESSION_CONFIGURATION_GUIDE.md) | LRCP/RLCP/RPCL/PCRL/CPRL — access patterns |
| [Quantization Configuration Guide](QUANTIZATION_CONFIGURATION_GUIDE.md) | Scalar quantization, guard bits, expounded vs derived |
| [Metadata Configuration Guide](METADATA_CONFIGURATION_GUIDE.md) | Comments, copyright, XML boxes, UUID boxes |
| [ROI Encoding Guide](ROI_ENCODING_GUIDE.md) | Region of interest priority encoding |
| [ICC Profile Support](ICC_PROFILE_SUPPORT.md) | Embedded ICC color profile reading and writing |
| [Integration Packages Guide](INTEGRATION_PACKAGES_GUIDE.md) | `CoreJ2K.SkiaSharp`, `CoreJ2K.ImageSharp`, etc. |
| [ArrayPool Security Guide](ARRAYPOOL_SECURITY_GUIDE.md) | Buffer pool security and isolation options |
| **Part 2 (JPX)** | |
| [Part 2 Transforms Guide](PART2_TRANSFORMS_GUIDE.md) | DCO, NLT, MCT — usage via the builder API |

## Reference

Internals, marker/box format notes, and implementation summaries.

### Codestream Markers

| Document | Description |
|----------|-------------|
| [Codestream Marker Reference](CODESTREAM_MARKER_REFERENCE.md) | All supported Part 1 markers and their status |
| [Marker Quick Reference](MARKER_QUICK_REFERENCE.md) | Condensed marker table |
| [SOP/EPH Marker Support](SOP_EPH_MARKER_SUPPORT.md) | Start-of-packet and end-of-packet header markers |
| [TLM Marker Support](TLM_MARKER_SUPPORT.md) | Tile-part length marker |
| [CRG Marker Implementation](CRG_MARKER_IMPLEMENTATION.md) | Component registration marker |
| [Codestream Validation](CODESTREAM_VALIDATION_IMPLEMENTATION.md) | Marker sequence validation |

### File Format Boxes

| Document | Description |
|----------|-------------|
| [Extended File Type Validation](EXTENDED_FILE_TYPE_VALIDATION.md) | `ftyp` box brand/compatibility handling |
| [Extended Length Box Support](EXTENDED_LENGTH_BOX_SUPPORT.md) | XLBox (64-bit length) support |
| [Resolution Metadata](RESOLUTION_SUPPORT.md) | Capture/display resolution boxes |
| [Bits Per Component](BITS_PER_COMPONENT_IMPLEMENTATION.md) | `bpcc` box |
| [Channel Definition](CHANNEL_DEFINITION_SUPPORT.md) | `cdef` box |
| [Palette](PALETTE_IMPLEMENTATION.md) | `pclr` + `cmap` boxes |
| [UUID Info / Reader Requirements](UUIDINFO_READERREQ_IMPLEMENTATION.md) | `uinf` and `rreq` boxes |
| [Metadata Overview](METADATA.md) | Comments, XML, UUID, ICC in JP2 files |

### Part 2 (JPX) Implementation Notes

| Document | Description |
|----------|-------------|
| [Part 2 Box Support](PART2_BOXES_IMPLEMENTATION.md) | Early JPX container box support |
| [Part 2 JPX Boxes](PART2_JPX_BOXES_IMPLEMENTATION.md) | Full JPX file-format box layer |
| [Part 2 NLT](PART2_NLT_IMPLEMENTATION.md) | Non-linearity point transform internals |
| [Part 2 MCT](PART2_MCT_IMPLEMENTATION.md) | Multiple component transform internals |

### Other

| Document | Description |
|----------|-------------|
| [Part 14 JPXML](PART14_JPXML_IMPLEMENTATION.md) | ISO/IEC 15444-14 XML metadata representation |
