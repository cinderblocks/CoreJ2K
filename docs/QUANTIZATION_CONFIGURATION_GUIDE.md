# Quantization Configuration API Guide

## Overview

CoreJ2K provides a **comprehensive, fluent API** for configuring JPEG 2000 quantization parameters. This modern interface offers fine-grained control over quantization with type-safe configuration, built-in presets, and validation.

## Table of Contents

- [Quick Start](#quick-start)
- [Understanding Quantization](#understanding-quantization)
- [Basic Usage](#basic-usage)
- [Quantization Types](#quantization-types)
- [Configuration Options](#configuration-options)
- [Presets](#presets)
- [Advanced Features](#advanced-features)
- [Complete Examples](#complete-examples)
- [API Reference](#api-reference)

## Quick Start

```csharp
using CoreJ2K.Configuration;

// Use a preset for common scenarios
var config = QuantizationPresets.HighQuality;

// Or build custom configuration
var custom = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.01f)
    .WithGuardBits(2);

// Apply to encoder configuration
var encoderConfig = new J2KEncoderConfiguration()
    .WithQuantization(q => q.UseExpounded().WithBaseStepSize(0.01f));
```

## Understanding Quantization

### What is Quantization?

Quantization controls the **trade-off between image quality and file size**:
- **Smaller step sizes** = Higher quality, larger files
- **Larger step sizes** = Lower quality, smaller files

### Quantization Types

| Type | Description | Use Case |
|------|-------------|----------|
| **Reversible** | Perfect reconstruction (lossless) | Medical, archival, no loss allowed |
| **Derived** | All subbands use same relative scaling | Simple lossy compression |
| **Expounded** | Independent control per subband | Advanced lossy, fine-tuning |

### Key Parameters

- **Base Step Size**: Primary control for quality/size trade-off
- **Guard Bits**: Protection against quantization overflow
- **Subband Steps**: Fine-tune individual frequency subbands (expounded only)

## Basic Usage

### Using Presets

```csharp
// Lossless (reversible)
var lossless = QuantizationPresets.Lossless;

// High quality lossy
var highQuality = QuantizationPresets.HighQuality;

// Balanced quality/size
var balanced = QuantizationPresets.Balanced;

// High compression
var compressed = QuantizationPresets.HighCompression;
```

### Custom Configuration

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.01f)    // Quality control
    .WithGuardBits(2);           // Overflow protection
```

### With Encoder

```csharp
var encoderConfig = new J2KEncoderConfiguration()
    .WithQuality(0.8)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.008f)
        .WithGuardBits(2));

byte[] jp2Data = J2kImage.ToBytes(image, encoderConfig);
```

## Quantization Types

### 1. Reversible (Lossless)

Perfect reconstruction - no information loss.

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseReversible();

// Or use preset
var lossless = QuantizationPresets.Lossless;
```

**Characteristics:**
- ? Exact reconstruction
- ? No quality loss
- ? Larger file sizes
- **Use for:** Medical imaging, archival, legal documents

### 2. Derived (Scalar Derived)

Simple lossy with uniform scaling.

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseDerived()
    .WithBaseStepSize(0.01f);
```

**Characteristics:**
- ? Simple configuration
- ? Consistent across subbands
- ? Less flexible than expounded
- **Use for:** General-purpose lossy compression

### 3. Expounded (Scalar Expounded)

Advanced lossy with per-subband control.

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.01f)
    .WithSubbandStep(0, "LL", 0.008f)  // Fine-tune low frequencies
    .WithSubbandStep(0, "HH", 0.015f); // Coarser for high frequencies
```

**Characteristics:**
- ? Maximum flexibility
- ? Per-subband optimization
- ? Best quality/size trade-offs
- **Use for:** Professional imaging, custom optimization

## Configuration Options

### Base Step Size

Controls overall quantization strength.

```csharp
// Very high quality (near-lossless)
.WithBaseStepSize(0.001f)

// High quality
.WithBaseStepSize(0.002f)

// Balanced
.WithBaseStepSize(0.0078125f)

// High compression
.WithBaseStepSize(0.02f)

// Maximum compression
.WithBaseStepSize(0.05f)
```

**Guidelines:**
- `0.001 - 0.002`: Near-lossless, archival quality
- `0.002 - 0.01`: High quality, professional use
- `0.01 - 0.03`: Balanced, general purpose
- `0.03 - 0.1`: High compression, acceptable quality

### Guard Bits

Protects against quantization overflow.

```csharp
// Minimal protection (smaller files)
.WithGuardBits(0)

// Standard protection (recommended)
.WithGuardBits(1)

// Enhanced protection (high bit-depth)
.WithGuardBits(2)

// Maximum protection
.WithGuardBits(3)
```

**Recommendations:**
- **0-1 bits**: 8-bit images, standard use
- **2 bits**: 12-16 bit images, high dynamic range
- **3+ bits**: Very high bit-depth, special cases

### Subband Steps (Expounded Only)

Fine-tune individual frequency subbands.

```csharp
// Single subband
.WithSubbandStep(0, "LL", 0.008f)  // Resolution 0, Low-Low

// All subbands at a resolution level
.WithResolutionSteps(
    0,              // Resolution level
    0.008f,         // LL (low frequencies)
    0.012f,         // HL (horizontal edges)
    0.012f,         // LH (vertical edges)
    0.015f)         // HH (diagonal, high frequencies)
```

**Subband Types:**
- **LL** (Low-Low): Coarse approximation, most important
- **HL** (High-Low): Horizontal detail
- **LH** (Low-High): Vertical detail
- **HH** (High-High): Diagonal detail, can use larger steps

## Presets

### Lossless

Perfect reconstruction, no quality loss.

```csharp
var config = QuantizationPresets.Lossless;
// Type: Reversible
// Use: Medical, archival, legal
```

### Near-Lossless

Visually indistinguishable, very high quality.

```csharp
var config = QuantizationPresets.NearLossless;
// BaseStep: 0.001
// GuardBits: 2
// Use: High-end photography, cinema
```

### High Quality

Excellent visual quality, good compression.

```csharp
var config = QuantizationPresets.HighQuality;
// BaseStep: 0.002
// GuardBits: 2
// Use: Professional photography, print
```

### Balanced

Good quality with moderate compression.

```csharp
var config = QuantizationPresets.Balanced;
// BaseStep: 0.0078125
// GuardBits: 1
// Use: General purpose, web, storage
```

### High Compression

Acceptable quality, small files.

```csharp
var config = QuantizationPresets.HighCompression;
// BaseStep: 0.02
// GuardBits: 1
// Use: Bandwidth-limited, previews
```

### Maximum Compression

Low quality, very small files.

```csharp
var config = QuantizationPresets.MaximumCompression;
// BaseStep: 0.05
// GuardBits: 1
// Use: Thumbnails, extreme compression
```

### Domain-Specific Presets

```csharp
// Medical imaging (lossless)
var medical = QuantizationPresets.Medical;

// Archival storage (very high quality)
var archival = QuantizationPresets.Archival;

// Web delivery (balanced)
var web = QuantizationPresets.Web;

// Thumbnail generation (higher compression)
var thumbnail = QuantizationPresets.Thumbnail;
```

## Advanced Features

### Custom Subband Weighting

Optimize for specific image characteristics.

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.01f)
    // Preserve low frequencies (important detail)
    .WithResolutionSteps(0, 0.008f, 0.012f, 0.012f, 0.015f)
    // Allow more loss in high frequencies
    .WithResolutionSteps(1, 0.010f, 0.015f, 0.015f, 0.020f);
```

### Multi-Resolution Optimization

Different quantization for different resolution levels.

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.01f);

// Fine detail at level 0 (highest resolution)
config.WithResolutionSteps(0, 0.008f, 0.010f, 0.010f, 0.012f);

// Moderate at level 1
config.WithResolutionSteps(1, 0.012f, 0.015f, 0.015f, 0.018f);

// Coarser at level 2
config.WithResolutionSteps(2, 0.015f, 0.020f, 0.020f, 0.025f);
```

### Validation

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseReversible()
    .WithSubbandStep(0, "LL", 0.01f);  // Error: can't combine!

if (!config.IsValid)
{
    var errors = config.Validate();
    foreach (var error in errors)
        Console.WriteLine($"Error: {error}");
}
// Output: "Custom subband steps are not applicable for reversible quantization"
```

### Cloning

```csharp
var base Config = QuantizationPresets.HighQuality;
var customized = baseConfig.Clone();

customized.WithBaseStepSize(0.003f);
// baseConfig unchanged, customized modified
```

## Complete Examples

### Example 1: Medical Imaging (Lossless)

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseReversible();

var encoderConfig = new J2KEncoderConfiguration()
    .WithLossless()
    .WithQuantization(q => q.UseReversible())
    .WithTiles(t => t.SetSize(512, 512));

byte[] dicomData = J2kImage.ToBytes(medicalImage, encoderConfig);
```

### Example 2: Professional Photography (High Quality)

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.002f)
    .WithGuardBits(2);

var encoderConfig = new J2KEncoderConfiguration()
    .WithQuality(0.95)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.002f)
        .WithGuardBits(2))
    .WithFileFormat(true);

byte[] photoData = J2kImage.ToBytes(photo, encoderConfig);
```

### Example 3: Web Delivery (Balanced)

```csharp
var config = QuantizationPresets.Web;

var encoderConfig = new J2KEncoderConfiguration()
    .WithQuality(0.75)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.01f)
        .WithGuardBits(1))
    .WithTiles(t => t.SetSize(512, 512));

byte[] webData = J2kImage.ToBytes(webImage, encoderConfig);
```

### Example 4: Archival Storage (Near-Lossless)

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.0015f)
    .WithGuardBits(2);

var encoderConfig = new J2KEncoderConfiguration()
    .WithQuality(0.99)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.0015f)
        .WithGuardBits(2))
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithErrorResilience(er => er.EnableAll());

byte[] archiveData = J2kImage.ToBytes(archiveImage, encoderConfig);
```

### Example 5: Custom Subband Optimization

```csharp
var config = new QuantizationConfigurationBuilder()
    .UseExpounded()
    .WithBaseStepSize(0.01f)
    // Prioritize low frequencies (important visual content)
    .WithSubbandStep(0, "LL", 0.007f)
    .WithSubbandStep(0, "HL", 0.011f)
    .WithSubbandStep(0, "LH", 0.011f)
    .WithSubbandStep(0, "HH", 0.015f)
    // Allow more loss in higher resolution levels
    .WithResolutionSteps(1, 0.012f, 0.016f, 0.016f, 0.020f);

var encoderConfig = new J2KEncoderConfiguration()
    .WithBitrate(2.0f)
    .WithQuantization(q => config);

byte[] optimizedData = J2kImage.ToBytes(image, encoderConfig);
```

### Example 6: Progressive Quality Tiers

```csharp
// Base quality for all users
var baseConfig = new QuantizationConfigurationBuilder()
    .ForBalanced();

// Premium quality for subscribers
var premiumConfig = new QuantizationConfigurationBuilder()
    .ForHighQuality();

// Use based on user tier
var config = isPremiumUser ? premiumConfig : baseConfig;

var encoderConfig = new J2KEncoderConfiguration()
    .WithQuantization(q => config);

byte[] data = J2kImage.ToBytes(image, encoderConfig);
```

### Example 7: Thumbnail Generation

```csharp
var config = QuantizationPresets.Thumbnail;

var encoderConfig = new J2KEncoderConfiguration()
    .WithBitrate(0.5f)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.03f))
    .WithTiles(t => t.SetSize(256, 256))
    .WithWavelet(w => w.WithDecompositionLevels(3));

byte[] thumbData = J2kImage.ToBytes(thumbnail, encoderConfig);
```

## API Reference

### QuantizationConfigurationBuilder

Main quantization configuration class.

**Methods:**
- `UseReversible()` - Enable lossless quantization
- `UseDerived()` - Enable scalar derived quantization
- `UseExpounded()` - Enable scalar expounded quantization
- `WithBaseStepSize(float)` - Set base quantization step
- `WithGuardBits(int)` - Set guard bits (0-7)
- `WithSubbandStep(int, string, float)` - Set step for specific subband
- `WithResolutionSteps(int, float, float, float, float)` - Set all subbands at level
- `UseDefaultSubbandSteps()` - Clear custom subband steps
- `ForHighQuality()` - Configure for high quality
- `ForBalanced()` - Configure for balanced quality/size
- `ForHighCompression()` - Configure for high compression
- `Clone()` - Create independent copy
- `Validate()` - Get validation errors
- `ToString()` - Get string representation

**Properties:**
- `Type` - Quantization type
- `BaseStepSize` - Base step size
- `GuardBits` - Number of guard bits
- `UseDefaultSteps` - Whether using default subband steps
- `IsValid` - Whether configuration is valid

### QuantizationPresets

Static presets for common scenarios.

**Available Presets:**
- `Lossless` - Reversible (perfect reconstruction)
- `NearLossless` - Visually indistinguishable (0.001 step)
- `HighQuality` - Excellent quality (0.002 step)
- `Balanced` - Good quality/size (0.0078125 step)
- `HighCompression` - Acceptable quality (0.02 step)
- `MaximumCompression` - Low quality (0.05 step)
- `Medical` - Medical imaging (lossless)
- `Archival` - Archival storage (0.0015 step)
- `Web` - Web delivery (0.01 step)
- `Thumbnail` - Thumbnail generation (0.03 step)

## Best Practices

### 1. Start with Presets

```csharp
// Start with a preset
var config = QuantizationPresets.HighQuality;

// Customize if needed
config.WithBaseStepSize(0.0025f);
```

### 2. Match to Use Case

| Use Case | Recommended Preset/Config |
|----------|---------------------------|
| Medical/Legal | `Lossless` |
| Photography | `HighQuality` or `NearLossless` |
| Archival | `Archival` |
| Web Display | `Web` or `Balanced` |
| Mobile/Low Bandwidth | `HighCompression` |
| Thumbnails | `Thumbnail` or `MaximumCompression` |

### 3. Validate Configuration

```csharp
if (!config.IsValid)
{
    throw new InvalidOperationException(
        string.Join("\n", config.Validate()));
}
```

### 4. Consider Bit Depth

```csharp
// 8-bit images
.WithGuardBits(1)

// 12-16 bit images
.WithGuardBits(2)

// Higher bit depths
.WithGuardBits(3)
```

### 5. Test and Iterate

```csharp
var configs = new[]
{
    (0.005f, "Very High"),
    (0.01f, "High"),
    (0.02f, "Medium"),
    (0.03f, "Low")
};

foreach (var (stepSize, label) in configs)
{
    var config = new QuantizationConfigurationBuilder()
        .WithBaseStepSize(stepSize);
    
    var data = J2kImage.ToBytes(image, config);
    Console.WriteLine($"{label}: {data.Length} bytes");
}
```

## See Also

- [Encoder Configuration Guide](ENCODER_CONFIGURATION_GUIDE.md)
- [Decoder Configuration Guide](DECODER_CONFIGURATION_GUIDE.md)
- [ROI Encoding Guide](ROI_ENCODING_GUIDE.md)

---

**Note:** Quantization configuration can be used standalone or integrated with the encoder configuration API for streamlined workflow.
