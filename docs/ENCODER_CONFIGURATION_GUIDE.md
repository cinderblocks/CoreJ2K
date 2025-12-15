# Modern Encoder Configuration API Guide

## Overview

CoreJ2K now provides a modern, fluent API for configuring JPEG 2000 encoding parameters. This replaces the old string-based `ParameterList` approach with a type-safe, IntelliSense-friendly interface.

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
using SkiaSharp;

// Load image
var bitmap = SKBitmap.Decode("input.png");

// Configure encoder (fluent API)
var config = new J2KEncoderConfiguration()
    .WithQuality(0.8)                    // 80% quality
    .WithTiles(t => t.SetSize(512, 512)) // 512x512 tiles
    .WithFileFormat(true);                // JP2 format

// Encode
byte[] jp2Data = J2kImage.ToBytes(bitmap, config);
File.WriteAllBytes("output.jp2", jp2Data);
```

## Benefits

### Old Way (ParameterList)
```csharp
var pl = new ParameterList();
pl["rate"] = "2.0";
pl["tiles"] = "512 512";
pl["Wlev"] = "5";
pl["Ffilters"] = "w9x7";
pl["Qtype"] = "expounded";
```

**Problems:**
- ? No IntelliSense
- ? String parsing errors at runtime
- ? No type safety
- ? Cryptic parameter names
- ? Easy to make mistakes

### New Way (Modern API)
```csharp
var config = new J2KEncoderConfiguration()
    .WithBitrate(2.0f)
    .WithTiles(t => t.SetSize(512, 512))
    .WithWavelet(w => w
        .UseIrreversible97()
        .WithDecompositionLevels(5))
    .WithQuantization(q => q.UseExpounded());
```

**Benefits:**
- ? Full IntelliSense support
- ? Compile-time error detection
- ? Type-safe configuration
- ? Self-documenting code
- ? Built-in validation

## Basic Usage

### Lossless Compression

```csharp
var config = new J2KEncoderConfiguration()
    .WithLossless()
    .WithTiles(t => t.SetSize(1024, 1024));

byte[] data = J2kImage.ToBytes(image, config);
```

### Lossy Compression with Quality Level

```csharp
var config = new J2KEncoderConfiguration()
    .WithQuality(0.8)  // 0.0 (lowest) to 1.0 (highest)
    .WithTiles(t => t.SetSize(512, 512));

byte[] data = J2kImage.ToBytes(image, config);
```

### Lossy Compression with Target Bitrate

```csharp
var config = new J2KEncoderConfiguration()
    .WithBitrate(2.5f)  // 2.5 bits per pixel
    .WithTiles(t => t.SetSize(512, 512));

byte[] data = J2kImage.ToBytes(image, config);
```

## Configuration Options

### 1. Quality/Rate Control

```csharp
// Quality-based (0.0 to 1.0)
.WithQuality(0.8)

// Bitrate-based (bits per pixel)
.WithBitrate(2.5f)

// Lossless (automatic configuration)
.WithLossless()

// Raw codestream vs JP2 format
.WithFileFormat(true)  // true = JP2, false = raw codestream
```

### 2. Tile Configuration

```csharp
.WithTiles(tiles => tiles
    .SetSize(1024, 1024)           // Tile dimensions
    .WithImageReference(0, 0)      // Image origin
    .WithTilingReference(0, 0)     // Tiling origin
    .WithPacketsPerTilePart(100))  // Packets per tile-part
```

### 3. Wavelet Transform

```csharp
.WithWavelet(wavelet => wavelet
    .UseIrreversible97()            // or .UseReversible53()
    .WithDecompositionLevels(5)     // 0-32 levels
    .WithCodeBlockOrigin(0, 0))     // 0 or 1
```

**Wavelet Filters:**
- `UseReversible53()` - 5-3 reversible (lossless)
- `UseIrreversible97()` - 9-7 irreversible (lossy, better compression)

### 4. Quantization

```csharp
.WithQuantization(quant => quant
    .UseExpounded()                 // or .UseReversible() / .UseDerived()
    .WithBaseStepSize(0.01f)        // Quantization step
    .WithGuardBits(2))              // 0-7 guard bits
```

**Quantization Types:**
- `UseReversible()` - For lossless compression
- `UseDerived()` - Scalar derived quantization
- `UseExpounded()` - Scalar expounded quantization (default)

### 5. Progression Order

```csharp
.WithProgression(prog => prog
    .WithOrder(ProgressionOrder.LRCP)         // Progression order
    .WithQualityLayers(0.1f, 0.5f, 1.0f))     // Multiple quality layers
```

**Progression Orders:**
- `LRCP` - Layer-Resolution-Component-Position (default)
- `RLCP` - Resolution-Layer-Component-Position
- `RPCL` - Resolution-Position-Component-Layer
- `PCRL` - Position-Component-Resolution-Layer
- `CPRL` - Component-Position-Resolution-Layer

### 6. Code Blocks

```csharp
.WithCodeBlocks(cb => cb
    .SetSize(64, 64))  // Must be power of 2, 4-1024
```

### 7. Entropy Coding

```csharp
.WithEntropyCoding(entropy => 
{
    entropy.LengthCalculation = LengthCalculation.NearOptimal;
    entropy.Termination = TerminationType.NearOptimal;
    entropy.SegmentationSymbol = false;
    entropy.CausalMode = false;
    entropy.ResetMQ = false;
    entropy.BypassMode = false;
    entropy.RegularTermination = false;
})
```

### 8. Error Resilience

```csharp
.WithErrorResilience(resilience => resilience
    .EnableSOPMarkers()    // Start of Packet markers
    .EnableEPHMarkers())   // End of Packet Header markers
// or
.WithErrorResilience(resilience => resilience.EnableAll())
```

### 9. Region of Interest (ROI)

```csharp
var roiConfig = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 400, 300);

var config = new J2KEncoderConfiguration()
    .WithQuality(0.5)
    .WithROI(roiConfig);
```

## Complete Examples

### Example 1: High-Quality Lossy

```csharp
var config = new J2KEncoderConfiguration()
    .WithQuality(0.9)
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithWavelet(w => w
        .UseIrreversible97()
        .WithDecompositionLevels(6))
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.005f))
    .WithFileFormat(true);

byte[] data = J2kImage.ToBytes(image, config);
```

### Example 2: Lossless with Tiling

```csharp
var config = new J2KEncoderConfiguration()
    .WithLossless()
    .WithTiles(t => t
        .SetSize(512, 512)
        .WithPacketsPerTilePart(50))
    .WithFileFormat(true);

byte[] data = J2kImage.ToBytes(image, config);
```

### Example 3: Progressive Quality Layers

```csharp
var config = new J2KEncoderConfiguration()
    .WithBitrate(2.0f)
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithProgression(prog => prog
        .WithOrder(ProgressionOrder.RLCP)
        .WithQualityLayers(0.2f, 0.5f, 1.0f, 2.0f));

byte[] data = J2kImage.ToBytes(image, config);
```

### Example 4: Medical Imaging (Lossless + Error Resilience)

```csharp
var config = new J2KEncoderConfiguration()
    .WithLossless()
    .WithTiles(t => t.SetSize(512, 512))
    .WithErrorResilience(er => er.EnableAll())
    .WithFileFormat(true);

byte[] dicomData = J2kImage.ToBytes(medicalImage, config);
```

### Example 5: Geospatial/Satellite Imagery

```csharp
var config = new J2KEncoderConfiguration()
    .WithQuality(0.85)
    .WithTiles(t => t
        .SetSize(2048, 2048)
        .WithPacketsPerTilePart(200))
    .WithWavelet(w => w
        .UseIrreversible97()
        .WithDecompositionLevels(7))
    .WithProgression(prog => prog
        .WithOrder(ProgressionOrder.RPCL))
    .WithFileFormat(true);

byte[] satelliteData = J2kImage.ToBytes(satelliteImage, config);
```

### Example 6: Web Delivery (Small File Size)

```csharp
var config = new J2KEncoderConfiguration()
    .WithBitrate(0.5f)  // 0.5 bpp for small files
    .WithTiles(t => t.SetSize(256, 256))
    .WithWavelet(w => w
        .UseIrreversible97()
        .WithDecompositionLevels(4))
    .WithProgression(prog => prog
        .WithOrder(ProgressionOrder.LRCP)
        .WithQualityLayers(0.1f, 0.3f, 0.5f))
    .WithFileFormat(true);

byte[] webData = J2kImage.ToBytes(webImage, config);
```

### Example 7: Archive Quality (High Quality + ROI)

```csharp
var roiConfig = new ROIConfiguration()
    .AddRectangle(0, 500, 500, 1000, 1000);  // Important region

var config = new J2KEncoderConfiguration()
    .WithQuality(0.95)
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithWavelet(w => w
        .UseIrreversible97()
        .WithDecompositionLevels(6))
    .WithROI(roiConfig)
    .WithFileFormat(true);

byte[] archiveData = J2kImage.ToBytes(archiveImage, config);
```

## Migration Guide

### Converting from ParameterList

**Old Code:**
```csharp
var pl = new ParameterList();
pl["rate"] = "2.0";
pl["lossless"] = "off";
pl["file_format"] = "on";
pl["tiles"] = "512 512";
pl["Wlev"] = "5";
pl["Ffilters"] = "w9x7";
pl["Qtype"] = "expounded";
pl["Qstep"] = "0.01";
pl["Cblksiz"] = "64 64";
pl["Psop"] = "on";
pl["Peph"] = "on";

byte[] data = J2kImage.ToBytes(image, pl);
```

**New Code:**
```csharp
var config = new J2KEncoderConfiguration()
    .WithBitrate(2.0f)
    .WithFileFormat(true)
    .WithTiles(t => t.SetSize(512, 512))
    .WithWavelet(w => w
        .UseIrreversible97()
        .WithDecompositionLevels(5))
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.01f))
    .WithCodeBlocks(cb => cb.SetSize(64, 64))
    .WithErrorResilience(er => er
        .EnableSOPMarkers()
        .EnableEPHMarkers());

byte[] data = J2kImage.ToBytes(image, config);
```

### Parameter Mapping Table

| Old Parameter | New API Method |
|---------------|----------------|
| `rate` | `.WithBitrate(float)` or `.WithQuality(double)` |
| `lossless` | `.WithLossless()` |
| `file_format` | `.WithFileFormat(bool)` |
| `tiles` | `.WithTiles(t => t.SetSize(w, h))` |
| `Wlev` | `.WithWavelet(w => w.WithDecompositionLevels(n))` |
| `Ffilters` | `.WithWavelet(w => w.UseReversible53())` or `.UseIrreversible97()` |
| `Qtype` | `.WithQuantization(q => q.UseReversible())` etc. |
| `Qstep` | `.WithQuantization(q => q.WithBaseStepSize(float))` |
| `Qguard_bits` | `.WithQuantization(q => q.WithGuardBits(int))` |
| `Cblksiz` | `.WithCodeBlocks(cb => cb.SetSize(w, h))` |
| `Psop` | `.WithErrorResilience(er => er.EnableSOPMarkers())` |
| `Peph` | `.WithErrorResilience(er => er.EnableEPHMarkers())` |
| `Aptype` | `.WithProgression(p => p.WithOrder(ProgressionOrder))` |

## API Reference

### J2KEncoderConfiguration

Main configuration class with fluent API.

**Methods:**
- `WithBitrate(float bitrate)` - Sets target bitrate in bpp
- `WithQuality(double quality)` - Sets quality level (0.0-1.0)
- `WithLossless()` - Enables lossless compression
- `WithFileFormat(bool useFileFormat)` - Use JP2 format wrapper
- `WithTiles(Action<TileConfiguration>)` - Configure tiling
- `WithWavelet(Action<WaveletConfiguration>)` - Configure wavelet transform
- `WithQuantization(Action<QuantizationConfiguration>)` - Configure quantization
- `WithProgression(Action<ProgressionConfiguration>)` - Configure progression
- `WithCodeBlocks(Action<CodeBlockConfiguration>)` - Configure code blocks
- `WithEntropyCoding(Action<EntropyCodingConfiguration>)` - Configure entropy coding
- `WithErrorResilience(Action<ErrorResilienceConfiguration>)` - Configure error resilience
- `WithROI(ROIConfiguration)` - Configure region of interest
- `Validate()` - Returns list of validation errors
- `ToParameterList()` - Converts to legacy ParameterList (for internal use)

**Properties:**
- `TargetBitrate` - Get/set target bitrate
- `Lossless` - Get/set lossless mode
- `UseFileFormat` - Get/set file format usage
- `Tiles` - Access tile configuration
- `Wavelet` - Access wavelet configuration
- `Quantization` - Access quantization configuration
- `Progression` - Access progression configuration
- `CodeBlocks` - Access code-block configuration
- `EntropyCoding` - Access entropy coding configuration
- `ErrorResilience` - Access error resilience configuration
- `ROI` - Access ROI configuration
- `IsValid` - Check if configuration is valid

### Component Configuration Classes

- `TileConfiguration` - Tile partitioning settings
- `WaveletConfiguration` - Wavelet transform settings
- `QuantizationConfiguration` - Quantization settings
- `ProgressionConfiguration` - Progression order and layers
- `CodeBlockConfiguration` - Code-block size settings
- `EntropyCodingConfiguration` - Entropy coding options
- `ErrorResilienceConfiguration` - Error resilience markers

## Validation

The configuration automatically validates settings:

```csharp
var config = new J2KEncoderConfiguration()
    .WithLossless()
    .WithBitrate(2.0f);  // Conflicting settings

if (!config.IsValid)
{
    var errors = config.Validate();
    foreach (var error in errors)
        Console.WriteLine($"Error: {error}");
}
// Output: Error: Cannot specify both lossless mode and a target bitrate
```

## Best Practices

1. **Always validate** before encoding:
   ```csharp
   if (!config.IsValid)
       throw new InvalidOperationException(string.Join("\n", config.Validate()));
   ```

2. **Use appropriate tile sizes** for your use case:
   - Small images (< 1024x1024): No tiling (`SetSize(0, 0)`)
   - Medium images: 512x512 or 1024x1024
   - Large images (> 4096x4096): 2048x2048

3. **Choose correct wavelet filter**:
   - Lossless: Always use `UseReversible53()`
   - Lossy: Use `UseIrreversible97()` for better compression

4. **Match quantization to wavelet**:
   - Reversible53 ? `UseReversible()`
   - Irreversible97 ? `UseExpounded()` or `UseDerived()`

5. **Enable error resilience** for unreliable transmission:
   ```csharp
   .WithErrorResilience(er => er.EnableAll())
   ```

## See Also

- [ROI Encoding Guide](ROI_ENCODING_GUIDE.md)
- [JPEG 2000 Standard Compliance](../README.md#standards--compliance)
- [Performance Tuning](PERFORMANCE.md)

---

**Note:** The old `ParameterList` API remains fully supported for backward compatibility. The new API is recommended for all new code.
