# Comprehensive Codestream Marker Support

## Overview

CoreJ2K provides **complete support for all JPEG 2000 codestream markers** per ISO/IEC 15444-1 Annex A. This includes reading, writing, validation, and inspection of all markers defined in the standard.

## Marker Reference

### Main Header Markers (Required)

| Marker | Code | Name | Required | Description |
|--------|------|------|----------|-------------|
| SOC | 0xFF4F | Start of Codestream | Yes | Must be first marker |
| SIZ | 0xFF51 | Image and Tile Size | Yes | Must immediately follow SOC |
| COD | 0xFF52 | Coding Style Default | Yes | Default coding parameters |
| QCD | 0xFF5C | Quantization Default | Yes | Default quantization |

### Main Header Markers (Optional)

| Marker | Code | Name | Required | Description |
|--------|------|------|----------|-------------|
| COC | 0xFF53 | Coding Style Component | No | Component-specific coding |
| QCC | 0xFF5D | Quantization Component | No | Component-specific quantization |
| RGN | 0xFF5E | Region of Interest | No | ROI parameters |
| POC | 0xFF5F | Progression Order Change | No | Progression order changes |
| PPM | 0xFF60 | Packed Packet Headers (Main) | No | Packet headers in main header |
| TLM | 0xFF55 | Tile-Part Lengths | No | Tile-part length information |
| PLM | 0xFF57 | Packet Length (Main) | No | Packet lengths |
| CRG | 0xFF63 | Component Registration | No | Component spatial registration |
| COM | 0xFF64 | Comment | No | Text or binary comments |

### Tile-Part Header Markers

| Marker | Code | Name | Required | Description |
|--------|------|------|----------|-------------|
| SOT | 0xFF90 | Start of Tile-Part | Yes | Tile-part header start |
| COD | 0xFF52 | Coding Style Default | No | Override main COD |
| COC | 0xFF53 | Coding Style Component | No | Override main COC |
| QCD | 0xFF5C | Quantization Default | No | Override main QCD |
| QCC | 0xFF5D | Quantization Component | No | Override main QCC |
| RGN | 0xFF5E | Region of Interest | No | Tile-specific ROI |
| POC | 0xFF5F | Progression Order Change | No | Tile-specific progression |
| PPT | 0xFF61 | Packed Packet Headers (Tile) | No | Packet headers in tile |
| PLT | 0xFF58 | Packet Length (Tile) | No | Tile packet lengths |
| COM | 0xFF64 | Comment | No | Tile-specific comments |
| SOD | 0xFF93 | Start of Data | Yes | Tile-part bitstream start |

### Packet-Level Markers

| Marker | Code | Name | Description |
|--------|------|------|-------------|
| SOP | 0xFF91 | Start of Packet | Optional packet synchronization |
| EPH | 0xFF92 | End of Packet Header | Optional packet header end |

### End Marker

| Marker | Code | Name | Required | Description |
|--------|------|------|----------|-------------|
| EOC | 0xFFD9 | End of Codestream | Yes | Must be last marker |

---

## Marker Details

### SOC - Start of Codestream (0xFF4F)

**Format:** Fixed length, 2 bytes

**Purpose:** Indicates the beginning of a JPEG 2000 codestream.

**Validation:**
- Must be the first marker in the codestream
- No parameters

**Example:**
```csharp
// Writing SOC
writer.Write(Markers.SOC);

// Validating SOC
if (data[0] != 0xFF || data[1] != 0x4F)
{
    errors.Add("Codestream must start with SOC marker");
}
```

---

### SIZ - Image and Tile Size (0xFF51)

**Format:** Variable length

**Fields:**
- Lsiz (2 bytes): Marker segment length
- Rsiz (2 bytes): Capabilities (0x0000 = baseline)
- Xsiz, Ysiz (4 bytes each): Reference grid size
- XOsiz, YOsiz (4 bytes each): Image offset
- XTsiz, YTsiz (4 bytes each): Tile size
- XTOsiz, YTOsiz (4 bytes each): Tile offset
- Csiz (2 bytes): Number of components
- For each component:
  - Ssiz (1 byte): Bit depth and signedness
  - XRsiz, YRsiz (1 byte each): Subsampling

**Example:**
```csharp
// Writing SIZ
var writer = new SIZMarkerWriter(imageSrc, isOrigSig, tiler, numComponents);
writer.Write(binaryWriter);

// Reading SIZ
var validator = new CodestreamValidator();
validator.ValidateCodestream(codestreamBytes);
```

**Validation Rules:**
- Must immediately follow SOC
- Minimum length: 41 bytes
- XRsiz, YRsiz must be ? 1
- Component bit depth: 1-38 bits

---

### COD - Coding Style Default (0xFF52)

**Format:** Variable length

**Fields:**
- Lcod (2 bytes): Marker segment length
- Scod (1 byte): Coding style flags
  - Bit 0: Precinct partition used
  - Bit 1: SOP markers used
  - Bit 2: EPH markers used
  - Bits 3-4: Code-block partition origin
- SGcod:
  - Progression order (1 byte): 0-4
  - Number of layers (2 bytes)
  - Multiple component transform (1 byte): 0 or 1
- SPcod:
  - Decomposition levels (1 byte)
  - Code-block width exponent (1 byte): 2-8
  - Code-block height exponent (1 byte): 2-8
  - Code-block style (1 byte)
  - Transformation (1 byte): 0=5-3 reversible, 1=9-7 irreversible
  - Precinct sizes (variable, if used)

**Example:**
```csharp
// Writing COD
var writer = new CODMarkerWriter(encSpec, dwt, ralloc);
writer.Write(binaryWriter, isMainHeader: true, tileIdx: -1, numComponents);

// Reading COD parameters
var cod = headerInfo.codValue["main"];
Console.WriteLine($"Decomposition levels: {cod.spcod_ndl}");
Console.WriteLine($"Number of layers: {cod.sgcod_nl}");
Console.WriteLine($"Progression order: {cod.sgcod_po}");
```

**Validation Rules:**
- Required in main header
- Code-block dimensions: 2^(cw+2) × 2^(ch+2), max 4096 samples
- cw + ch ? 12
- Decomposition levels: typically ? 32

---

### QCD - Quantization Default (0xFF5C)

**Format:** Variable length

**Fields:**
- Lqcd (2 bytes): Marker segment length
- Sqcd (1 byte): Quantization style and guard bits
  - Bits 0-4: Quantization style
    - 0: No quantization (reversible)
    - 1: Scalar derived
    - 2: Scalar expounded
  - Bits 5-7: Guard bits (0-7)
- SPqcd: Quantization parameters (variable)
  - For reversible: 1 byte per subband (exponent only)
  - For irreversible: 2 bytes per subband (exponent + mantissa)

**Example:**
```csharp
// Accessing QCD parameters
var qcd = headerInfo.qcdValue["main"];
var quantType = qcd.QuantType;
var guardBits = qcd.NumGuardBits;

Console.WriteLine($"Quantization type: {quantType}");
Console.WriteLine($"Guard bits: {guardBits}");
```

**Validation Rules:**
- Required in main header
- Quantization style: 0-2
- Guard bits: 0-7
- Number of subbands: 3 * decomposition_levels + 1

---

### SOT - Start of Tile-Part (0xFF90)

**Format:** Fixed length, 12 bytes

**Fields:**
- Lsot (2 bytes): Length = 10
- Isot (2 bytes): Tile index
- Psot (4 bytes): Tile-part length
- TPsot (1 byte): Tile-part index (0-based)
- TNsot (1 byte): Number of tile-parts

**Example:**
```csharp
// Writing SOT
var writer = new SOTMarkerWriter();
writer.Write(binaryWriter, tileIndex, tileLength);

// Reading SOT
var sot = headerInfo.sotValue[$"t{tileIdx}_tp{tilePartIdx}"];
Console.WriteLine($"Tile {sot.isot}, part {sot.tpsot} of {sot.tnsot}");
Console.WriteLine($"Length: {sot.psot} bytes");
```

**Validation Rules:**
- Must appear before each tile-part
- TPsot < TNsot
- Isot < number of tiles

---

### COM - Comment (0xFF64)

**Format:** Variable length

**Fields:**
- Lcom (2 bytes): Marker segment length
- Rcom (2 bytes): Registration value
  - 0: Binary data
  - 1: Latin-1 text (ISO 8859-15)
  - Other: Reserved
- Ccom (variable): Comment data

**Example:**
```csharp
// Writing COM
var writer = new COMMarkerWriter(enableJJ2K: true, otherComments: "Copyright 2025");
writer.Write(binaryWriter);

// Reading COM
foreach (var com in headerInfo.comValue.Values)
{
    if (com.rcom == 1)
    {
        var text = System.Text.Encoding.UTF8.GetString(com.ccom);
        Console.WriteLine($"Comment: {text}");
    }
}
```

---

### POC - Progression Order Change (0xFF5F)

**Format:** Variable length

**Purpose:** Defines progression order changes for multi-resolution, multi-quality encoding.

**Fields:** For each progression change:
- RSpoc (1 byte): Resolution start
- CSpoc (1 or 2 bytes): Component start
- LYEpoc (2 bytes): Layer end
- REpoc (1 byte): Resolution end
- CEpoc (1 or 2 bytes): Component end
- Ppoc (1 byte): Progression order

**Example:**
```csharp
// Writing POC
var writer = new POCMarkerWriter(encSpec, numComponents);
writer.Write(binaryWriter, isMainHeader: true, tileIdx: -1);

// Reading POC
var poc = headerInfo.pocValue["main"];
Console.WriteLine($"Progression changes: {poc.rspoc.Length}");
```

---

## Progression Orders

| Value | Order | Description |
|-------|-------|-------------|
| 0 | LRCP | Layer-Resolution-Component-Position |
| 1 | RLCP | Resolution-Layer-Component-Position |
| 2 | RPCL | Resolution-Position-Component-Layer |
| 3 | PCRL | Position-Component-Resolution-Layer |
| 4 | CPRL | Component-Position-Resolution-Layer |

---

## Marker Validation

CoreJ2K provides comprehensive marker validation through `CodestreamValidator`:

### Basic Validation

```csharp
using CoreJ2K.j2k.codestream;

var validator = new CodestreamValidator();
var isValid = validator.ValidateCodestream(codestreamBytes);

if (validator.HasErrors)
{
    Console.WriteLine("Validation errors:");
    foreach (var error in validator.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}

if (validator.HasWarnings)
{
    Console.WriteLine("Validation warnings:");
    foreach (var warning in validator.Warnings)
    {
        Console.WriteLine($"  - {warning}");
    }
}

// Get full report
Console.WriteLine(validator.GetValidationReport());
```

### Partial Validation (for large files)

```csharp
// Validate only first 64KB (fast)
var validator = new CodestreamValidator();
var isValid = validator.ValidateCodestream(codestreamBytes, maxBytesToRead: 65536);
```

### Validation Features

? **Main Header Validation:**
- SOC marker presence and position
- SIZ marker validation (dimensions, bit depths, subsampling)
- COD marker validation (code-block sizes, decomposition levels)
- QCD marker validation (quantization style, guard bits)
- Marker ordering

? **Tile-Part Validation:**
- SOT marker validation
- SOD marker presence
- Tile-part header markers
- Tile dimensions and indices

? **Packet-Level Validation:**
- SOP marker validation (if enabled)
- EPH marker validation (if enabled)

? **Comprehensive Checks:**
- Marker segment lengths
- Parameter value ranges
- Marker ordering requirements
- Required vs optional markers

---

## Marker Inspection Utilities

### Inspecting Marker Segments

```csharp
using CoreJ2K.j2k.codestream;

// Create header info from file
var headerInfo = new HeaderInfo();

// Access SIZ information
var siz = headerInfo.sizValue;
Console.WriteLine($"Image size: {siz.xsiz - siz.x0siz}x{siz.ysiz - siz.y0siz}");
Console.WriteLine($"Number of components: {siz.csiz}");
Console.WriteLine($"Number of tiles: {siz.NumTiles}");

for (int c = 0; c < siz.csiz; c++)
{
    Console.WriteLine($"Component {c}:");
    Console.WriteLine($"  Bit depth: {siz.getOrigBitDepth(c)}");
    Console.WriteLine($"  Signed: {siz.isOrigSigned(c)}");
    Console.WriteLine($"  Dimensions: {siz.getCompImgWidth(c)}x{siz.getCompImgHeight(c)}");
    Console.WriteLine($"  Subsampling: {siz.xrsiz[c]}x{siz.yrsiz[c]}");
}

// Access COD information
var cod = headerInfo.codValue["main"];
Console.WriteLine($"\nCoding style:");
Console.WriteLine($"  Decomposition levels: {cod.spcod_ndl}");
Console.WriteLine($"  Layers: {cod.sgcod_nl}");
Console.WriteLine($"  Code-block size: {1 << (cod.spcod_cw + 2)}x{1 << (cod.spcod_ch + 2)}");
Console.WriteLine($"  Transformation: {(cod.spcod_t[0] == 0 ? "5-3 reversible" : "9-7 irreversible")}");
Console.WriteLine($"  MCT: {(cod.sgcod_mct == 1 ? "Yes" : "No")}");

// Access QCD information  
var qcd = headerInfo.qcdValue["main"];
Console.WriteLine($"\nQuantization:");
Console.WriteLine($"  Type: {qcd.QuantType}");
Console.WriteLine($"  Guard bits: {qcd.NumGuardBits}");
```

### Marker Debugging

```csharp
// Print all marker information
Console.WriteLine(headerInfo.toStringMainHeader());

// Print tile-specific markers
for (int t = 0; t < numTiles; t++)
{
    Console.WriteLine(headerInfo.toStringTileHeader(t, numTileParts));
}
```

---

## Writing Markers

CoreJ2K provides marker writers for encoding:

### Main Header Writing

```csharp
using CoreJ2K.j2k.codestream.writer.markers;

// SIZ marker
var sizWriter = new SIZMarkerWriter(imageSrc, isOrigSig, tiler, numComponents);
sizWriter.Write(binaryWriter);

// COD marker
var codWriter = new CODMarkerWriter(encSpec, dwt, ralloc);
codWriter.Write(binaryWriter, isMainHeader: true, tileIdx: -1, numComponents);

// COM marker
var comWriter = new COMMarkerWriter(enableJJ2K: true, otherComments: "My comment");
comWriter.Write(binaryWriter);
```

### Tile-Part Header Writing

```csharp
// SOT marker
var sotWriter = new SOTMarkerWriter();
sotWriter.Write(binaryWriter, tileIndex, tileLength);

// Tile-specific COD (optional)
var codWriter = new CODMarkerWriter(encSpec, dwt, ralloc);
codWriter.Write(binaryWriter, isMainHeader: false, tileIdx: tileIndex, numComponents);
```

---

## Advanced Features

### Code-Block Styles

The code-block style field in COD/COC markers supports these flags:

```csharp
// From StdEntropyCoderOptions
OPT_BYPASS          = 0x01  // Selective arithmetic coding bypass
OPT_RESET_MQ        = 0x02  // Reset MQ coder contexts
OPT_TERM_PASS       = 0x04  // Terminate each coding pass
OPT_VERT_STR_CAUSAL = 0x08  // Vertically causal context
OPT_PRED_TERM       = 0x10  // Predictable termination
OPT_SEG_SYMBOLS     = 0x20  // Use segmentation symbols
```

### Precinct Sizes

When precincts are used (Scod bit 0 = 1):
- Specified as PPx and PPy exponents
- Format: (2^PPy << 4) | 2^PPx
- Separate value for each decomposition level

```csharp
// Example: 64x64 precincts at all levels
var ppx = 6; // 2^6 = 64
var ppy = 6;
var precinctSize = (ppy << 4) | ppx; // 0x66
```

### Component-Specific Markers

Override default behavior for individual components:

```csharp
// COC - component-specific coding
var coc = headerInfo.NewCOC;
coc.ccoc = componentIndex;
coc.scoc = codingStyle;
// ... set other parameters

// QCC - component-specific quantization
var qcc = headerInfo.NewQCC;
qcc.cqcc = componentIndex;
qcc.sqcc = quantStyle;
// ... set quantization parameters
```

---

## Marker Constants Reference

All marker constants are defined in `Markers` struct:

```csharp
// Delimiters
Markers.SOC  // 0xFF4F - Start of Codestream
Markers.SOT  // 0xFF90 - Start of Tile-part
Markers.SOD  // 0xFF93 - Start of Data
Markers.EOC  // 0xFFD9 - End of Codestream

// Marker segments
Markers.SIZ  // 0xFF51 - Image and tile size
Markers.COD  // 0xFF52 - Coding style default
Markers.COC  // 0xFF53 - Coding style component
Markers.RGN  // 0xFF5E - Region of interest
Markers.QCD  // 0xFF5C - Quantization default
Markers.QCC  // 0xFF5D - Quantization component
Markers.POC  // 0xFF5F - Progression order change
Markers.TLM  // 0xFF55 - Tile-part lengths
Markers.PLM  // 0xFF57 - Packet length (main)
Markers.PLT  // 0xFF58 - Packet length (tile)
Markers.PPM  // 0xFF60 - Packed packet headers (main)
Markers.PPT  // 0xFF61 - Packed packet headers (tile)
Markers.SOP  // 0xFF91 - Start of packet
Markers.EPH  // 0xFF92 - End of packet header
Markers.CRG  // 0xFF63 - Component registration
Markers.COM  // 0xFF64 - Comment

// Field constants
Markers.RSIZ_BASELINE        // 0x00
Markers.RSIZ_ER_FLAG         // 0x01
Markers.RSIZ_ROI             // 0x02
Markers.SCOX_PRECINCT_PARTITION  // 0x01
Markers.SCOX_USE_SOP         // 0x02
Markers.SCOX_USE_EPH         // 0x04
Markers.SQCX_NO_QUANTIZATION     // 0x00
Markers.SQCX_SCALAR_DERIVED      // 0x01
Markers.SQCX_SCALAR_EXPOUNDED    // 0x02
```

---

## Best Practices

### 1. Always Validate Codestreams

```csharp
var validator = new CodestreamValidator();
if (!validator.ValidateCodestream(codestream))
{
    // Handle errors
    Console.WriteLine(validator.GetValidationReport());
}
```

### 2. Use Appropriate Marker Parameters

- Code-blocks: 32×32 or 64×64 typical
- Decomposition levels: 5-6 typical for most images
- Layers: 1 for lossless, multiple for progressive

### 3. Include COM Markers for Metadata

```csharp
var comWriter = new COMMarkerWriter(
    enableJJ2K: true,
    otherComments: "Created by MyApp#Version 1.0#Copyright 2025");
```

### 4. Consider SOP/EPH for Error Resilience

```csharp
// Enable in encoder specs
encSpec.sops.setDefault("on");
encSpec.ephs.setDefault("on");
```

---

## See Also

- [CodestreamValidator API](CodestreamValidator.md)
- [HeaderInfo API](HeaderInfo.md)
- [Marker Writers](MarkerWriters.md)
- [ISO/IEC 15444-1 Annex A](https://www.iso.org/standard/78321.html)

---

## Summary

CoreJ2K provides **complete, production-ready marker support** with:

? All 20+ markers from ISO/IEC 15444-1 Annex A
? Comprehensive validation
? Reading and writing support
? Detailed marker inspection
? Helper utilities and constants
? Error resilience features
? Full documentation

The implementation is **standards-compliant, well-tested, and ready for use**! ??
