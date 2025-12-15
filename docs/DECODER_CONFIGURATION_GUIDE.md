# Modern Decoder Configuration API Guide

## Overview

CoreJ2K now provides a **modern, fluent API** for configuring JPEG 2000 decoding parameters. This complements the encoder configuration API with a type-safe, IntelliSense-friendly interface for decoding.

## Table of Contents

- [Quick Start](#quick-start)
- [Benefits](#benefits)
- [Basic Usage](#basic-usage)
- [Configuration Options](#configuration-options)
- [Complete Examples](#complete-examples)
- [Migration Guide](#migration-guide)
- [API Reference](#api-reference)

## Quick Start

```csharp
using CoreJ2K;
using CoreJ2K.Configuration;

// Load JPEG 2000 data
byte[] jp2Data = File.ReadAllBytes("image.jp2");

// Configure decoder (fluent API)
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(2)          // Decode at lower resolution
    .WithColorSpace(true)             // Apply color transforms
    .WithProgressiveDecoding();       // Enable progressive mode

// Decode
var image = J2kImage.FromBytes(jp2Data, config);
var bitmap = image.As<SKBitmap>();
```

## Benefits

### Old Way (ParameterList)
```csharp
var pl = new ParameterList();
pl["res"] = "2";
pl["nocolorspace"] = "off";
pl["parsing"] = "on";
```

**Problems:**
- ? No IntelliSense
- ? String parsing errors at runtime
- ? Inverted boolean logic ("nocolorspace")
- ? No type safety
- ? Unclear parameter meanings

### New Way (Modern API)
```csharp
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(2)
    .WithColorSpace(true)
    .WithProgressiveDecoding();
```

**Benefits:**
- ? Full IntelliSense support
- ? Compile-time error detection
- ? Intuitive boolean logic
- ? Type-safe configuration
- ? Self-documenting code

## Basic Usage

### Full Resolution Decode

```csharp
var config = new J2KDecoderConfiguration()
    .WithHighestResolution()
    .WithColorSpace(true);

var image = J2kImage.FromBytes(data, config);
```

### Low Resolution Preview

```csharp
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(0)  // Lowest resolution
    .WithColorSpace(true)
    .WithVerbose(false);

var thumbnail = J2kImage.FromBytes(data, config);
```

### Progressive Decoding with Rate Limit

```csharp
var config = new J2KDecoderConfiguration()
    .WithDecodingRate(0.5f)  // 0.5 bits per pixel
    .WithProgressiveDecoding()
    .WithColorSpace(true);

var image = J2kImage.FromStream(stream, config);
```

### Fast Decode with Early Termination

```csharp
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(1)
    .WithQuitConditions(q => q
        .WithMaxLayers(3)
        .DecodeOnlyFirstTilePart())
    .WithVerbose(false);

var quickPreview = J2kImage.FromFile("large.jp2", config);
```

## Configuration Options

### 1. Resolution Level

```csharp
// Specific resolution level (0 = lowest)
.WithResolutionLevel(2)

// Highest available resolution
.WithHighestResolution()
```

**Resolution Levels:**
- `0` - Lowest resolution (fastest decode, smallest memory)
- `1, 2, 3...` - Intermediate resolutions
- `-1` or `WithHighestResolution()` - Full resolution

### 2. Rate/Bandwidth Control

```csharp
// Limit by bits per pixel
.WithDecodingRate(1.0f)  // 1.0 bpp

// Limit by total bytes
.WithDecodingBytes(100000)  // 100KB

// Decode all data (default)
.WithDecodingRate(-1)
```

### 3. Color Space Transformation

```csharp
// Apply color space transforms (default)
.WithColorSpace(true)

// Skip color space processing (faster but wrong colors)
.WithColorSpace(false)
```

### 4. Parsing Mode

```csharp
// Progressive parsing mode (default, quality-aware)
.WithParsingMode(true)
// or
.WithProgressiveDecoding()

// Truncate mode (simpler, less quality-aware)
.WithParsingMode(false)
```

**Parsing Mode Comparison:**
- **Parsing Mode (true):** Creates virtual progressive layers, quality-aware truncation
- **Truncate Mode (false):** Simple byte truncation, faster but lower quality

### 5. Quit Conditions (Early Termination)

```csharp
.WithQuitConditions(quit => quit
    .WithMaxCodeBlocks(100)         // Decode max 100 code blocks
    .WithMaxLayers(5)                // Decode max 5 quality layers
    .WithMaxBitPlanes(8)             // Decode max 8 bit planes
    .QuitAfterFirstProgression()     // Stop after first progression
    .DecodeOnlyFirstTilePart())      // Only decode first tile part
```

**Use Cases:**
- **Fast Preview:** Limit code blocks and layers
- **Thumbnail:** Decode only first tile part
- **Progressive Display:** Use max layers control

### 6. Component Transformation

```csharp
// Use component transform from codestream (default)
.WithComponentTransform(ct => ct.Enable())

// Disable component transform
.WithComponentTransform(ct => ct.Disable())
```

### 7. Verbose Output

```csharp
// Enable verbose logging (default)
.WithVerbose(true)

// Disable verbose logging (faster)
.WithVerbose(false)
```

## Complete Examples

### Example 1: Thumbnail Generation

```csharp
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(0)           // Lowest resolution
    .WithQuitConditions(q => q
        .WithMaxLayers(2)             // Only first 2 layers
        .DecodeOnlyFirstTilePart())   // Fast decode
    .WithColorSpace(true)
    .WithVerbose(false);

var thumbnail = J2kImage.FromFile("large-image.jp2", config);
```

### Example 2: Progressive Web Delivery

```csharp
var config = new J2KDecoderConfiguration()
    .WithDecodingRate(0.25f)          // Low bitrate for mobile
    .WithProgressiveDecoding()        // Quality-aware
    .WithResolutionLevel(1)           // Medium resolution
    .WithColorSpace(true)
    .WithVerbose(false);

var webImage = J2kImage.FromStream(networkStream, config);
```

### Example 3: Fast Decode for Analysis

```csharp
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(2)
    .WithColorSpace(false)            // Skip color transform
    .WithQuitConditions(q => q
        .WithMaxCodeBlocks(50))       // Minimal decode
    .WithVerbose(false);

var analysisImage = J2kImage.FromBytes(data, config);
```

### Example 4: High-Quality Medical Imaging

```csharp
var config = new J2KDecoderConfiguration()
    .WithHighestResolution()
    .WithColorSpace(true)
    .WithComponentTransform(ct => ct.Enable())
    .WithParsingMode(true)
    .WithVerbose(true);              // Log for quality assurance

var medicalImage = J2kImage.FromFile("xray.jp2", config);
```

### Example 5: Bandwidth-Limited Streaming

```csharp
var config = new J2KDecoderConfiguration()
    .WithDecodingBytes(50000)         // 50KB limit
    .WithProgressiveDecoding()
    .WithResolutionLevel(1)
    .WithQuitConditions(q => q
        .WithMaxLayers(4))
    .WithColorSpace(true);

var streamedImage = J2kImage.FromStream(stream, config);
```

### Example 6: Multi-Resolution Tile Map

```csharp
// Level 0 - Full resolution
var fullConfig = new J2KDecoderConfiguration()
    .WithHighestResolution()
    .WithColorSpace(true);

// Level 1 - Medium resolution
var mediumConfig = new J2KDecoderConfiguration()
    .WithResolutionLevel(2)
    .WithColorSpace(true);

// Level 2 - Thumbnail
var thumbConfig = new J2KDecoderConfiguration()
    .WithResolutionLevel(0)
    .WithQuitConditions(q => q.DecodeOnlyFirstTilePart())
    .WithVerbose(false);

var fullImage = J2kImage.FromFile("map.jp2", fullConfig);
var mediumImage = J2kImage.FromFile("map.jp2", mediumConfig);
var thumbnail = J2kImage.FromFile("map.jp2", thumbConfig);
```

### Example 7: Decode with Metadata Extraction

```csharp
var config = new J2KDecoderConfiguration()
    .WithHighestResolution()
    .WithColorSpace(true);

var image = J2kImage.FromStream(
    stream, 
    out var metadata, 
    config);

// Access metadata
foreach (var comment in metadata.Comments)
{
    Console.WriteLine($"Comment: {comment}");
}
```

## Migration Guide

### Converting from ParameterList

**Old Code:**
```csharp
var pl = new ParameterList();
pl["res"] = "2";
pl["rate"] = "1.0";
pl["nocolorspace"] = "off";
pl["parsing"] = "on";
pl["verbose"] = "off";
pl["l_quit"] = "5";
pl["one_tp"] = "on";

var image = J2kImage.FromBytes(data, pl);
```

**New Code:**
```csharp
var config = new J2KDecoderConfiguration()
    .WithResolutionLevel(2)
    .WithDecodingRate(1.0f)
    .WithColorSpace(true)           // Note: not "nocolorspace"
    .WithProgressiveDecoding()
    .WithVerbose(false)
    .WithQuitConditions(q => q
        .WithMaxLayers(5)
        .DecodeOnlyFirstTilePart());

var image = J2kImage.FromBytes(data, config);
```

### Parameter Mapping Table

| Old Parameter | New API Method | Notes |
|---------------|----------------|-------|
| `res` | `.WithResolutionLevel(int)` | Direct mapping |
| `rate` | `.WithDecodingRate(float)` | Direct mapping |
| `nbytes` | `.WithDecodingBytes(int)` | Direct mapping |
| `nocolorspace` | `.WithColorSpace(bool)` | ?? **Inverted logic!** |
| `parsing` | `.WithParsingMode(bool)` or `.WithProgressiveDecoding()` | Direct mapping |
| `verbose` | `.WithVerbose(bool)` | Direct mapping |
| `ncb_quit` | `.WithQuitConditions(q => q.WithMaxCodeBlocks(int))` | Nested config |
| `l_quit` | `.WithQuitConditions(q => q.WithMaxLayers(int))` | Nested config |
| `m_quit` | `.WithQuitConditions(q => q.WithMaxBitPlanes(int))` | Nested config |
| `poc_quit` | `.WithQuitConditions(q => q.QuitAfterFirstProgression())` | Method call |
| `one_tp` | `.WithQuitConditions(q => q.DecodeOnlyFirstTilePart())` | Method call |
| `comp_transf` | `.WithComponentTransform(ct => ct.Enable()/Disable())` | Nested config |

### Important: Boolean Logic Changes

The modern API uses **positive logic** instead of negative:

| Old Parameter | Old Value | New API | New Value |
|---------------|-----------|---------|-----------|
| `nocolorspace` | `"off"` (apply colorspace) | `.WithColorSpace()` | `true` |
| `nocolorspace` | `"on"` (skip colorspace) | `.WithColorSpace()` | `false` |

## API Reference

### J2KDecoderConfiguration

Main configuration class with fluent API for decoding.

**Methods:**
- `WithResolutionLevel(int level)` - Sets resolution level (0=lowest)
- `WithHighestResolution()` - Decode at full resolution
- `WithDecodingRate(float rate)` - Limits decoding rate (bpp)
- `WithDecodingBytes(int bytes)` - Limits decoding bytes
- `WithColorSpace(bool useColorSpace)` - Apply color transforms
- `WithParsingMode(bool parsingMode)` - Use parsing vs truncate mode
- `WithProgressiveDecoding()` - Enable progressive parsing mode
- `WithVerbose(bool verbose)` - Control verbose output
- `WithQuitConditions(Action<QuitConditions>)` - Configure early termination
- `WithComponentTransform(Action<ComponentTransformSettings>)` - Configure component transform
- `Validate()` - Returns list of validation errors
- `ToParameterList()` - Converts to legacy ParameterList (internal use)

**Properties:**
- `ResolutionLevel` - Get/set resolution level
- `DecodingRate` - Get/set decoding rate
- `DecodingBytes` - Get/set decoding bytes
- `UseColorSpace` - Get/set color space usage
- `ParsingMode` - Get/set parsing mode
- `Verbose` - Get/set verbose output
- `QuitConditions` - Access quit conditions config
- `ComponentTransform` - Access component transform settings
- `IsValid` - Check if configuration is valid

### QuitConditions

Configuration for early termination during decoding.

**Properties:**
- `MaxCodeBlocks` - Maximum code blocks to decode (-1 = no limit)
- `MaxLayers` - Maximum quality layers to decode (-1 = no limit)
- `MaxBitPlanes` - Maximum bit planes to decode (-1 = no limit)
- `QuitAfterFirstProgressionOrder` - Stop after first progression
- `OnlyFirstTilePart` - Decode only first tile part per tile

**Methods:**
- `WithMaxCodeBlocks(int)` - Sets max code blocks
- `WithMaxLayers(int)` - Sets max layers
- `WithMaxBitPlanes(int)` - Sets max bit planes
- `QuitAfterFirstProgression()` - Enable first-progression quit
- `DecodeOnlyFirstTilePart()` - Enable first-tile-part only

### ComponentTransformSettings

Configuration for component transformation during decoding.

**Properties:**
- `UseComponentTransform` - Whether to apply component transform

**Methods:**
- `Enable()` - Enable component transform (default)
- `Disable()` - Disable component transform

## Validation

The configuration automatically validates settings:

```csharp
var config = new J2KDecoderConfiguration()
    .WithDecodingRate(1.0f)
    .WithDecodingBytes(10000);  // Conflict!

if (!config.IsValid)
{
    var errors = config.Validate();
    foreach (var error in errors)
        Console.WriteLine($"Error: {error}");
}
// Output: Error: Cannot specify both decoding rate and decoding bytes
```

## Best Practices

### 1. Resolution Selection

```csharp
// Thumbnails: Use lowest resolution
.WithResolutionLevel(0)

// Previews: Use medium resolution
.WithResolutionLevel(2)

// Full quality: Use highest resolution
.WithHighestResolution()
```

### 2. Performance Optimization

```csharp
// Fastest decoding
var fastConfig = new J2KDecoderConfiguration()
    .WithResolutionLevel(0)          // Low resolution
    .WithColorSpace(false)            // Skip color transform
    .WithQuitConditions(q => q
        .DecodeOnlyFirstTilePart())   // Minimal data
    .WithVerbose(false);              // No logging
```

### 3. Quality vs Speed Trade-offs

```csharp
// Maximum quality (slow)
.WithHighestResolution()
.WithColorSpace(true)
.WithParsingMode(true)

// Balanced (medium speed)
.WithResolutionLevel(2)
.WithColorSpace(true)
.WithQuitConditions(q => q.WithMaxLayers(5))

// Fast preview (fastest)
.WithResolutionLevel(0)
.WithColorSpace(false)
.WithQuitConditions(q => q.DecodeOnlyFirstTilePart())
```

### 4. Memory Management

```csharp
// Low memory footprint
var lowMemConfig = new J2KDecoderConfiguration()
    .WithResolutionLevel(0)           // Smaller image
    .WithQuitConditions(q => q
        .WithMaxCodeBlocks(50)        // Less data
        .DecodeOnlyFirstTilePart())   // Minimal tiles
    .WithVerbose(false);              // Less overhead
```

### 5. Network/Streaming

```csharp
// Progressive network decode
var streamConfig = new J2KDecoderConfiguration()
    .WithDecodingBytes(50000)         // Bandwidth limit
    .WithProgressiveDecoding()        // Quality-aware
    .WithResolutionLevel(1)           // Not full res
    .WithQuitConditions(q => q
        .WithMaxLayers(4));           // Limit layers
```

## Use Case Matrix

| Use Case | Resolution | Rate Limit | Parsing | Quit Conditions | Color Space |
|----------|------------|------------|---------|-----------------|-------------|
| **Thumbnail** | 0 (lowest) | - | Off | First tile part | Optional |
| **Preview** | 1-2 | Low | On | Max layers 3-5 | Yes |
| **Web Display** | 1-2 | Medium | On | Max layers 5-8 | Yes |
| **Full Quality** | -1 (highest) | - | On | None | Yes |
| **Fast Decode** | 0-1 | - | Off | First tile + low layers | No |
| **Progressive** | Variable | Yes | On | Max layers | Yes |
| **Analysis** | 0-2 | - | Off | Max code blocks | No |
| **Archival** | -1 (highest) | - | On | None | Yes |

## See Also

- [Encoder Configuration Guide](ENCODER_CONFIGURATION_GUIDE.md)
- [ROI Encoding Guide](ROI_ENCODING_GUIDE.md)
- [Performance Tuning](../README.md#platform-support)

---

**Note:** The old `ParameterList` API remains fully supported for backward compatibility. The new API is recommended for all new code.
