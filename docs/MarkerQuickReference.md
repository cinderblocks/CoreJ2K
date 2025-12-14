# JPEG 2000 Marker Quick Reference

## Marker Summary Table

| Marker | Code | Length | Main Hdr | Tile Hdr | Purpose |
|--------|------|--------|----------|----------|---------|
| **SOC** | 0xFF4F | 2 | ? Required (1st) | ? | Start of codestream |
| **SIZ** | 0xFF51 | Variable | ? Required (2nd) | ? | Image/tile dimensions |
| **COD** | 0xFF52 | Variable | ? Required | ? Optional | Coding style default |
| **COC** | 0xFF53 | Variable | ? Optional | ? Optional | Coding style component |
| **QCD** | 0xFF5C | Variable | ? Required | ? Optional | Quantization default |
| **QCC** | 0xFF5D | Variable | ? Optional | ? Optional | Quantization component |
| **RGN** | 0xFF5E | Variable | ? Optional | ? Optional | Region of interest |
| **POC** | 0xFF5F | Variable | ? Optional | ? Optional | Progression order change |
| **PPM** | 0xFF60 | Variable | ? Optional | ? | Packed packet hdrs (main) |
| **TLM** | 0xFF55 | Variable | ? Optional | ? | Tile-part lengths |
| **PLM** | 0xFF57 | Variable | ? Optional | ? | Packet lengths (main) |
| **CRG** | 0xFF63 | Variable | ? Optional | ? | Component registration |
| **COM** | 0xFF64 | Variable | ? Optional | ? Optional | Comment |
| **SOT** | 0xFF90 | 12 | ? | ? Required | Start of tile-part |
| **PPT** | 0xFF61 | Variable | ? | ? Optional | Packed packet hdrs (tile) |
| **PLT** | 0xFF58 | Variable | ? | ? Optional | Packet lengths (tile) |
| **SOD** | 0xFF93 | 2 | ? | ? Required | Start of data |
| **SOP** | 0xFF91 | 6 | ? | ? In packets | Start of packet |
| **EPH** | 0xFF92 | 2 | ? | ? In packets | End of packet header |
| **EOC** | 0xFFD9 | 2 | ? Required (last) | ? | End of codestream |

---

## Codestream Structure

```
???????????????????????????????????????????
? SOC (0xFF4F)                            ? ? Always first
???????????????????????????????????????????
? MAIN HEADER                             ?
?  ?? SIZ (0xFF51) [Required]             ? ? Image dimensions
?  ?? COD (0xFF52) [Required]             ? ? Coding style
?  ?? QCD (0xFF5C) [Required]             ? ? Quantization
?  ?? COC (0xFF53) [Optional, per comp]   ?
?  ?? QCC (0xFF5D) [Optional, per comp]   ?
?  ?? RGN (0xFF5E) [Optional, per comp]   ?
?  ?? POC (0xFF5F) [Optional]             ?
?  ?? PPM (0xFF60) [Optional]             ?
?  ?? TLM (0xFF55) [Optional]             ?
?  ?? PLM (0xFF57) [Optional]             ?
?  ?? CRG (0xFF63) [Optional]             ?
?  ?? COM (0xFF64) [Optional, multiple]   ?
???????????????????????????????????????????
? TILE-PART 0                             ?
?  ?? SOT (0xFF90) [Required]             ? ? Tile index & length
?  ?? Tile-Part Header [Optional]         ?
?  ?   ?? COD, COC, QCD, QCC, RGN, POC   ?
?  ?   ?? PPT, PLT                        ?
?  ?   ?? COM                             ?
?  ?? SOD (0xFF93) [Required]             ? ? Start of data
?  ?? Tile-Part Bitstream                 ?
?      ?? Packet 0                        ?
?      ?   ?? [SOP (0xFF91)] [Optional]   ?
?      ?   ?? Packet header               ?
?      ?   ?? [EPH (0xFF92)] [Optional]   ?
?      ?   ?? Packet data                 ?
?      ?? Packet 1                        ?
?      ?? ...                             ?
???????????????????????????????????????????
? TILE-PART 1                             ?
?  ?? (same structure)                    ?
???????????????????????????????????????????
? ...                                     ?
???????????????????????????????????????????
? EOC (0xFFD9)                            ? ? Always last
???????????????????????????????????????????
```

---

## Common Field Values

### Rsiz (Capabilities) - SIZ Marker

| Value | Description |
|-------|-------------|
| 0x0000 | Baseline (no special features) |
| 0x0001 | + Error resilience |
| 0x0002 | + ROI |
| 0x0003 | + Error resilience + ROI |

### Scod (Coding Style) - COD Marker

| Bit | Flag | Description |
|-----|------|-------------|
| 0 | 0x01 | Precinct partition used |
| 1 | 0x02 | SOP markers used |
| 2 | 0x04 | EPH markers used |
| 3 | 0x08 | Horizontal code-block partition at x=1 |
| 4 | 0x10 | Vertical code-block partition at y=1 |

### Progression Orders

| Value | Order | Description |
|-------|-------|-------------|
| 0 | LRCP | Layer ? Resolution ? Component ? Position |
| 1 | RLCP | Resolution ? Layer ? Component ? Position |
| 2 | RPCL | Resolution ? Position ? Component ? Layer |
| 3 | PCRL | Position ? Component ? Resolution ? Layer |
| 4 | CPRL | Component ? Position ? Resolution ? Layer |

### Quantization Styles - QCD/QCC Markers

| Value | Style | Description |
|-------|-------|-------------|
| 0x00 | No quantization | Reversible (5-3 wavelet) |
| 0x01 | Scalar derived | One value, others derived |
| 0x02 | Scalar expounded | All values explicitly given |

### Wavelet Transformations

| Value | Filter | Description |
|-------|--------|-------------|
| 0 | 5-3 | Reversible (lossless capable) |
| 1 | 9-7 | Irreversible (lossy) |

---

## Typical Marker Sequence

### Lossless Encoding

```
SOC
SIZ  (image dimensions, 1 component, 8-bit)
COD  (5 levels, 1 layer, 5-3 reversible)
QCD  (no quantization)
TLM  (tile-part lengths)
COM  ("Lossless encoding")
SOT  (tile 0)
SOD
  [tile 0 data]
...
EOC
```

### Lossy Progressive Encoding

```
SOC
SIZ  (image dimensions, 3 components, 8-bit)
COD  (5 levels, 10 layers, 9-7 irreversible, RLCP)
QCD  (scalar expounded)
POC  (progression changes)
TLM  (tile-part lengths)
COM  ("Progressive encoding")
SOT  (tile 0)
SOD
  SOP (packet 0)
  [packet header]
  EPH
  [packet data]
  ...
...
EOC
```

---

## Code-Block Style Flags

| Bit | Flag | Name | Description |
|-----|------|------|-------------|
| 0 | 0x01 | BYPASS | Selective arithmetic bypass |
| 1 | 0x02 | RESET | Reset MQ contexts |
| 2 | 0x04 | TERMINATE | Terminate each pass |
| 3 | 0x08 | CAUSAL | Vertically causal context |
| 4 | 0x10 | PREDICT | Predictable termination |
| 5 | 0x20 | SEGSYM | Segmentation symbols |

---

## Marker Length Formulas

### SIZ
```
Lsiz = 38 + 3 × Csiz
```
Where Csiz = number of components

### COD (without precincts)
```
Lcod = 12
```

### COD (with precincts)
```
Lcod = 12 + (decomposition_levels + 1)
```

### QCD
```
Reversible:    Lqcd = 4 + (3 × levels + 1)
Derived:       Lqcd = 5
Expounded:     Lqcd = 4 + 2 × (3 × levels + 1)
```

### SOT
```
Lsot = 10 (always)
```

### COM
```
Lcom = 4 + length(comment)
```

---

## Common Validation Errors

| Error | Cause | Fix |
|-------|-------|-----|
| "Codestream must start with SOC" | Missing or corrupt SOC marker | Ensure 0xFF4F at start |
| "SIZ must immediately follow SOC" | Wrong marker order | Place SIZ after SOC |
| "COD marker missing" | No coding style specified | Add COD in main header |
| "Invalid code-block size" | cw + ch > 12 | Reduce code-block dimensions |
| "Component index out of range" | COC/QCC index ? Csiz | Check component count |
| "Missing SOT before tile-part" | No tile-part header | Add SOT marker |
| "Missing SOD marker" | No data delimiter | Add SOD after tile-part header |
| "EOC marker not found" | Truncated codestream | Add 0xFFD9 at end |

---

## Quick CodeJ2K Examples

### Validate Codestream
```csharp
var validator = new CodestreamValidator();
validator.ValidateCodestream(bytes);
Console.WriteLine(validator.GetValidationReport());
```

### Read Marker Info
```csharp
var siz = headerInfo.sizValue;
Console.WriteLine($"Image: {siz.xsiz}x{siz.ysiz}");
Console.WriteLine($"Components: {siz.csiz}");
Console.WriteLine($"Tiles: {siz.NumTiles}");
```

### Write Markers
```csharp
// SOC
writer.Write(Markers.SOC);

// SIZ
var sizWriter = new SIZMarkerWriter(...);
sizWriter.Write(writer);

// COD
var codWriter = new CODMarkerWriter(...);
codWriter.Write(writer, true, -1, nc);
```

---

## Marker Size Reference

| Marker | Min Bytes | Typical Bytes | Max Bytes |
|--------|-----------|---------------|-----------|
| SOC | 2 | 2 | 2 |
| SIZ | 41 | 50-60 | 49,155 |
| COD | 12 | 12-18 | 45 |
| QCD | 4 | 8-25 | 197 |
| SOT | 12 | 12 | 12 |
| SOD | 2 | 2 | 2 |
| COM | 6 | 50-200 | 65,535 |
| EOC | 2 | 2 | 2 |

---

## Memory for Quick Reference

**Required Main Header Order:**
```
SOC ? SIZ ? COD ? QCD
```

**Required Tile-Part Order:**
```
SOT ? [headers] ? SOD ? [data]
```

**Always Present:**
```
SOC (first) ... EOC (last)
```

**Code-Block Max:**
```
2^(cw+2) × 2^(ch+2) ? 4096
cw + ch ? 12
```

**Component Fields:**
```
Csiz < 257 ? 1 byte
Csiz ? 257 ? 2 bytes
```

---

## See Also

- [Full Marker Reference](CodestreamMarkerReference.md)
- [CodestreamValidator API](CodestreamValidator.md)
- [ISO/IEC 15444-1 Annex A](https://www.iso.org/standard/78321.html)
