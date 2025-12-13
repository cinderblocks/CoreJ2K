# Palette and Component Mapping Support Implementation

## Summary

Successfully implemented **Palette (pclr)** and **Component Mapping (cmap)** box support for JPEG 2000 files, which is a **critical requirement for ISO/IEC 15444-1 Part 1 compliance**. These features enable support for palettized/indexed color images in JP2 files.

## Changes Made

### 1. Metadata Classes (`CoreJ2K/j2k/fileformat/metadata/J2KMetadata.cs`)

Added comprehensive metadata support for palette and component mapping:

#### PaletteData Class
- Stores palette entries for indexed color images
- Supports up to 1024 entries with multiple color columns
- Handles both signed and unsigned bit depths (1-16 bits per component)
- Format: `[NumEntries][NumColumns][BitDepths][Entries]`
- Methods: `IsSigned()`, `GetBitDepth()`, `GetEntry()`

#### ComponentMappingData Class
- Maps codestream components to output image channels
- Supports both direct mapping (type 0) and palette mapping (type 1)
- Stores channel index, mapping type, and palette column index
- Property: `UsesPalette` - indicates if any channel uses palette mapping

#### ComponentMapping Class
- Individual mapping entry: ComponentIndex, MappingType, PaletteColumn
- MappingType: 0 = direct use, 1 = palette mapping

### 2. FileFormatReader (`CoreJ2K/j2k/fileformat/reader/FileFormatReader.cs`)

Added reading support for palette and component mapping boxes:

#### readPaletteBox()
- Parses Palette Box (0x70636c72 / 'pclr')
- Reads: NE (entries), NPC (columns), B[] (bit depths), C[][] (entry values)
- Handles 8-bit and 16-bit palette entries
- Supports signed/unsigned values with proper sign extension
- Stores data in `Metadata.Palette`

#### readComponentMappingBox()
- Parses Component Mapping Box (0x636d6170 / 'cmap')
- Reads array of 4-byte entries: CMP (2), MTYP (1), PCOL (1)
- Validates box length (must be multiple of 4)
- Stores data in `Metadata.ComponentMapping`

#### readJP2HeaderBox() - Updated
- Now processes PALETTE_BOX and COMPONENT_MAPPING_BOX
- Maintains proper box ordering per ISO spec

### 3. FileFormatWriter (`CoreJ2K/j2k/fileformat/writer/FileFormatWriter.cs`)

Added writing support for palette and component mapping boxes:

#### writePaletteBox()
- Writes Palette Box with correct format
- Calculates box length dynamically based on bit depths
- Writes 8-bit or 16-bit entries as appropriate
- Handles signed values correctly

#### writeComponentMappingBox()
- Writes Component Mapping Box
- Writes CMP, MTYP, PCOL for each channel
- Ensures proper 4-byte alignment per channel

#### writeJP2HeaderBox() - Updated
- Calculates box lengths for palette and component mapping
- Writes boxes in correct order per ISO/IEC 15444-1:
  1. Image Header (required, first)
  2. Colour Specification (required)
  3. Bits Per Component (optional)
  4. **Palette (optional, must come before cmap)**
  5. **Component Mapping (optional, must come after palette)**
  6. Channel Definition (optional)
  7. Resolution (optional)

### 4. Tests (`tests/CoreJ2K.Tests/PaletteTests.cs`)

Comprehensive test suite with 5 tests:

1. **TestPaletteBoxWriteAndRead** - Tests basic palette writing and reading with 4-color RGB palette
2. **TestPaletteBoxWithSignedValues** - Tests signed palette values (-128 to 127)
3. **TestComponentMappingDirectMode** - Tests direct component mapping (no palette)
4. **TestPaletteBox16Bit** - Tests 16-bit palette entries (up to 65535)
5. **TestPaletteBoxOrdering** - Verifies palette box comes before component mapping box

All tests pass successfully! ?

## Compliance Status

### ISO/IEC 15444-1 Part 1 Requirements Met:
? Palette Box (pclr) - Section I.5.3.4
? Component Mapping Box (cmap) - Section I.5.3.5
? Proper box ordering in JP2 Header
? Support for indexed color images
? Support for 8-bit and 16-bit palette entries
? Support for signed and unsigned values
? Validation of box structures

### Remaining for Full Compliance:
- Bits Per Component Box (bpcc) - reading/writing (constant defined, not integrated)
- File Type Box - complete validation (brand, MinV, compatibility list)
- JP2 Header Box - comprehensive validation (ordering, required boxes)
- Colour Specification Box - full ICC profile validation, METH 3/4 support
- Component Registration (creg) Box - not implemented
- Complete error handling and validation

## Usage Example

```csharp
// Create a palette for an indexed color image
var metadata = new J2KMetadata();

// Define a 256-color palette with RGB values
var palette = new PaletteData
{
    NumEntries = 256,
    NumColumns = 3, // RGB
    BitDepths = new short[] { 7, 7, 7 }, // 8-bit unsigned
    Entries = /* ... 256x3 color values ... */
};
metadata.Palette = palette;

// Map single indexed component to 3 RGB channels via palette
metadata.AddComponentMapping(0, 1, 0); // R from palette column 0
metadata.AddComponentMapping(0, 1, 1); // G from palette column 1
metadata.AddComponentMapping(0, 1, 2); // B from palette column 2

// Write JP2 file
var writer = new FileFormatWriter(stream, height, width, 1, new[] { 8 }, codestreamLength);
writer.Metadata = metadata;
writer.writeFileFormat();
```

## Technical Details

### Palette Box Format (per ISO/IEC 15444-1 Section I.5.3.4):
```
LBox (4 bytes) - Box length
TBox (4 bytes) - 0x70636c72 ('pclr')
NE   (2 bytes) - Number of palette entries (1-1024)
NPC  (1 byte)  - Number of palette columns/components
B[]  (NPC bytes) - Bit depth for each column (bit 7=sign, bits 0-6=depth-1)
C[]  (variable) - Palette entries [NE][NPC], each 1 or 2 bytes
```

### Component Mapping Box Format (per ISO/IEC 15444-1 Section I.5.3.5):
```
LBox (4 bytes) - Box length
TBox (4 bytes) - 0x636d6170 ('cmap')
Array of:
  CMP  (2 bytes) - Codestream component index
  MTYP (1 byte)  - Mapping type (0=direct, 1=palette)
  PCOL (1 byte)  - Palette column (used when MTYP=1)
```

## Benefits

1. **Standards Compliance**: Enables ISO/IEC 15444-1 Part 1 compliance for palettized images
2. **Space Efficiency**: Indexed color images use less storage than full RGB
3. **Format Support**: Enables reading/writing palettized JP2 files from other encoders
4. **Flexibility**: Supports both direct and palette-mapped color modes
5. **Precision**: Handles 1-16 bit palette entries, signed or unsigned

## Next Steps for Full Compliance

See the comprehensive analysis in the previous conversation for remaining compliance items, prioritized as:

**Critical:**
1. ? **Palette/Component Mapping** (COMPLETED)
2. Bits Per Component box integration
3. File Type box complete validation
4. JP2 Header ordering validation
5. Colour Specification complete implementation

**Important:**
6. Component Registration box
7. Resolution box complete writing
8. Comprehensive error handling

**Nice to Have:**
9. Reader Requirements writing
10. UUID Info writing
11. Extended Part 2 features

## Files Modified

- `CoreJ2K/j2k/fileformat/metadata/J2KMetadata.cs` - Added palette and component mapping metadata classes
- `CoreJ2K/j2k/fileformat/reader/FileFormatReader.cs` - Added palette and component mapping reading
- `CoreJ2K/j2k/fileformat/writer/FileFormatWriter.cs` - Added palette and component mapping writing
- `tests/CoreJ2K.Tests/PaletteTests.cs` - Comprehensive test suite (5 tests, all passing)

## Test Results

```
Test summary: total: 5, failed: 0, succeeded: 5, skipped: 0, duration: 0.8s
Build succeeded in 2.0s
```

All tests pass successfully, demonstrating correct implementation of palette and component mapping functionality.
