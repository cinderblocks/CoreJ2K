# Complete Configuration Builder Guide

## Overview

The **CompleteEncoderConfigurationBuilder** is CoreJ2K's unified, fluent API that integrates all aspects of JPEG 2000 encoding into a single, easy-to-use interface. It combines quantization, wavelet, progression, metadata, and encoder settings into one cohesive configuration system.

## Table of Contents

- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Quality Presets](#quality-presets)
- [Use Case Presets](#use-case-presets)
- [Complete Presets](#complete-presets)
- [Configuration Methods](#configuration-methods)
- [Real-World Examples](#real-world-examples)
- [Advanced Usage](#advanced-usage)
- [API Reference](#api-reference)

## Quick Start

### Simplest Usage: One-Liner

```csharp
using CoreJ2K.Configuration;

// Encode with a preset
byte[] data = CompleteConfigurationPresets.Web
    .WithCopyright("© 2025 My Company")
    .Encode(imageSource);
```

### Basic Usage: Build and Encode

```csharp
// Create configuration
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithCopyright("© 2025")
    .Build();

// Encode image
byte[] jp2Data = J2kImage.ToBytes(imageSource, config);
```

### With Metadata

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForWeb()
    .WithComment("Product photo")
    .WithCopyright("© 2025 Company")
    .WithMetadata(m => m.WithXml(xmpData))
    .Build();

byte[] data = J2kImage.ToBytes(image, config);
```

## Architecture

The Complete Builder integrates seven configuration components:

```
CompleteEncoderConfigurationBuilder
??? EncoderConfiguration     (quality, bitrate, tiles, etc.)
??? QuantizationConfiguration (quality/size trade-offs)
??? WaveletConfiguration     (transform settings)
??? ProgressionConfiguration (data organization)
??? MetadataConfiguration    (comments, copyright, XML, UUIDs)
```

Each component can be configured independently or through unified presets.

## Quality Presets

### ForLossless()

Perfect reconstruction, no quality loss.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForLossless();
```

**Characteristics:**
- Reversible quantization
- Reversible 5/3 wavelet
- No information loss
- Larger file sizes
- **Use for:** Medical imaging, legal documents, archival

### ForNearLossless()

Visually indistinguishable from original, very high quality.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForNearLossless();
```

**Characteristics:**
- Quality: 99%
- Base step size: 0.001
- 6 decomposition levels
- **Use for:** High-end photography, cinema, archival

### ForHighQuality()

Excellent visual quality with good compression.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality();
```

**Characteristics:**
- Quality: 90%
- Base step size: 0.002
- Quality progressive transmission
- **Use for:** Professional photography, print media

### ForBalanced()

Good quality with moderate compression.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForBalanced();
```

**Characteristics:**
- Quality: 75%
- Base step size: 0.0078125
- Balanced file size
- **Use for:** General purpose, web, storage

### ForHighCompression()

Acceptable quality with small file size.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighCompression();
```

**Characteristics:**
- Bitrate: 1.0 bpp
- Higher compression
- Visible artifacts possible
- **Use for:** Bandwidth-limited, previews

## Use Case Presets

### ForMedical()

Optimized for medical imaging with lossless compression.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForMedical()
    .WithMetadata(m => m
        .WithComment("Patient: Anonymous")
        .WithComment("Study: CT Scan")
        .WithXml(dicomMetadata));
```

**Settings:**
- Lossless compression
- Reversible 5/3 wavelet
- Resolution progressive
- 512×512 tiles
- **Use for:** DICOM, diagnostic imaging, patient records

### ForArchival()

Very high quality for long-term preservation.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForArchival()
    .WithCopyright("© 2025 Institution");
```

**Settings:**
- Quality: 98%
- Base step size: 0.0015
- 6 decomposition levels
- Error resilience enabled
- 1024×1024 tiles
- **Use for:** Museums, libraries, historical archives

### ForWeb()

Progressive transmission for web delivery.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForWeb()
    .WithTiles(t => t.SetSize(512, 512));
```

**Settings:**
- Quality: 75%
- Quality progressive
- 512×512 tiles
- Web-optimized
- **Use for:** Websites, galleries, e-commerce

### ForThumbnail()

Fast encoding for preview images.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForThumbnail();
```

**Settings:**
- Bitrate: 0.5 bpp
- 3 decomposition levels
- 256×256 tiles
- Fast encoding
- **Use for:** Thumbnails, previews, icons

### ForGeospatial()

Spatial browsing for GIS applications.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForGeospatial()
    .WithComment("Satellite imagery - North America");
```

**Settings:**
- Spatial progressive (RPCL)
- 256×256 tiles
- Optimized for panning/zooming
- **Use for:** Maps, satellite imagery, GIS

### ForStreaming()

Quality progressive for bandwidth-limited delivery.

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForStreaming();
```

**Settings:**
- Quality progressive (LRCP)
- 512×512 tiles
- Optimized for progressive display
- **Use for:** Streaming, progressive download

## Complete Presets

Ready-to-use static presets that combine all settings:

```csharp
// Medical imaging
CompleteConfigurationPresets.Medical

// Archival storage
CompleteConfigurationPresets.Archival

// Web delivery
CompleteConfigurationPresets.Web

// Thumbnail generation
CompleteConfigurationPresets.Thumbnail

// Geospatial/GIS
CompleteConfigurationPresets.Geospatial

// Streaming delivery
CompleteConfigurationPresets.Streaming

// High-quality photography
CompleteConfigurationPresets.Photography

// General purpose
CompleteConfigurationPresets.GeneralPurpose
```

**Example:**

```csharp
byte[] data = CompleteConfigurationPresets.Web
    .WithCopyright("© 2025")
    .Encode(imageSource);
```

## Configuration Methods

### Quantization

```csharp
.WithQuantization(q => q
    .UseExpounded()
    .WithBaseStepSize(0.01f)
    .WithGuardBits(2))
```

### Wavelet

```csharp
.WithWavelet(w => w
    .UseIrreversible_9_7()
    .WithDecompositionLevels(6))
```

### Progression

```csharp
.WithProgression(p => p
    .UseLRCP()
    .WithTileOrder(0, ProgressionOrder.RLCP))
```

### Metadata

```csharp
.WithMetadata(m => m
    .WithComment("Description")
    .WithCopyright("© 2025")
    .WithXml(xmpData)
    .WithUuid(guid, data))
```

### Encoder

```csharp
.WithEncoder(e => e
    .WithQuality(0.85)
    .WithTiles(t => t.SetSize(512, 512))
    .WithErrorResilience(er => er.EnableAll()))
```

### Shortcuts

```csharp
// Direct methods
.WithQuality(0.85)
.WithBitrate(2.0f)
.WithTiles(t => t.SetSize(512, 512))
.WithComment("Comment text")
.WithCopyright("© 2025")
```

## Real-World Examples

### Example 1: Medical DICOM Image

```csharp
using CoreJ2K.Configuration;

var config = new CompleteEncoderConfigurationBuilder()
    .ForMedical()
    .WithMetadata(m => m
        .WithComment("Patient: Anonymous")
        .WithComment("Study: MRI Brain Scan")
        .WithComment("Date: 2025-01-15")
        .WithComment("Institution: General Hospital")
        .WithXml("<dicom>" +
                 "<studyId>12345</studyId>" +
                 "<modality>MR</modality>" +
                 "</dicom>")
        .WithUuid(institutionGuid, institutionData))
    .Build();

byte[] dicomData = J2kImage.ToBytes(medicalImage, config);
```

### Example 2: Professional Photography

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithCopyright("© 2025 John Doe Photography")
    .WithComment("Sunset over Grand Canyon")
    .WithComment("Canon EOS R5 + RF 24-105mm f/4")
    .WithMetadata(m => m
        .WithXml("<exif>" +
                 "<camera>Canon EOS R5</camera>" +
                 "<lens>RF 24-105mm f/4 L IS USM</lens>" +
                 "<iso>100</iso>" +
                 "<shutter>1/250</shutter>" +
                 "<aperture>f/8</aperture>" +
                 "</exif>"))
    .Build();

byte[] photoData = J2kImage.ToBytes(photo, config);
```

### Example 3: E-Commerce Product Image

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForWeb()
    .WithProgression(p => p.UseRLCP()) // Resolution progressive
    .WithTiles(t => t.SetSize(512, 512))
    .WithCopyright("© 2025 E-Shop Inc.")
    .WithComment("Product SKU: ABC-123")
    .WithComment("Category: Electronics")
    .Build();

byte[] productImage = J2kImage.ToBytes(image, config);
```

### Example 4: Satellite/Geospatial Imagery

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForGeospatial()
    .WithMetadata(m => m
        .WithComment("Satellite: Landsat 8")
        .WithComment("Region: North America")
        .WithComment("Date: 2025-01-15")
        .WithXml("<gml:metadata>" +
                 "<bounds>-180,90,180,-90</bounds>" +
                 "<resolution>30m</resolution>" +
                 "<bands>7</bands>" +
                 "</gml:metadata>")
        .WithCopyright("© 2025 Satellite Provider"))
    .WithTiles(t => t.SetSize(256, 256))
    .Build();

byte[] satelliteData = J2kImage.ToBytes(satelliteImage, config);
```

### Example 5: Archival Document Scanning

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForArchival()
    .WithMetadata(m => m
        .WithComment("Historical Document Archive")
        .WithComment("Document ID: DOC-2025-0001")
        .WithComment("Scanned: 2025-01-15")
        .WithComment("Scanner: Epson Expression 12000XL")
        .WithComment("DPI: 600")
        .WithCopyright("© 2025 National Archives")
        .WithXml("<archive>" +
                 "<collection>Historical Letters</collection>" +
                 "<year>1865</year>" +
                 "<condition>Good</condition>" +
                 "</archive>"))
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithEncoder(e => e.WithErrorResilience(er => er.EnableAll()))
    .Build();

byte[] archiveData = J2kImage.ToBytes(scannedDocument, config);
```

### Example 6: Thumbnail Generation Pipeline

```csharp
// Generate multiple sizes efficiently
var sizes = new[] { (256, 256), (512, 512), (1024, 1024) };

foreach (var (width, height) in sizes)
{
    var thumbnailConfig = new CompleteEncoderConfigurationBuilder()
        .ForThumbnail()
        .WithTiles(t => t.SetSize(width, height))
        .WithComment($"Thumbnail {width}×{height}")
        .Build();
    
    var resizedImage = ResizeImage(originalImage, width, height);
    byte[] thumbData = J2kImage.ToBytes(resizedImage, thumbnailConfig);
    
    SaveToFile($"thumbnail_{width}x{height}.jp2", thumbData);
}
```

### Example 7: Progressive Web Gallery

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForWeb()
    .WithProgression(p => p.UseRLCP()) // Start with low-res, progressively improve
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.01f))
    .WithWavelet(w => w
        .UseIrreversible_9_7()
        .WithDecompositionLevels(5))
    .WithTiles(t => t.SetSize(512, 512))
    .WithMetadata(m => m
        .WithComment("Art Gallery Collection")
        .WithComment("Available for purchase")
        .WithCopyright("© 2025 Artist Name")
        .WithXml(iptcMetadata))
    .Build();

byte[] galleryImage = J2kImage.ToBytes(artwork, config);
```

### Example 8: Custom Fine-Tuned Configuration

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .WithQuality(0.85)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.008f)
        .WithGuardBits(2)
        // Fine-tune subband quantization
        .WithSubbandStep(0, "LL", 0.007f)  // Preserve low frequencies
        .WithSubbandStep(0, "HH", 0.015f)) // Allow more loss in high frequencies
    .WithWavelet(w => w
        .UseIrreversible_9_7()
        .WithDecompositionLevels(6))
    .WithProgression(p => p
        .UseLRCP()
        .WithTileOrder(0, ProgressionOrder.RLCP)) // Different order for first tile
    .WithTiles(t => t
        .SetSize(1024, 1024)
        .SetOffset(0, 0))
    .WithEncoder(e => e
        .WithErrorResilience(er => er
            .EnableSOP()
            .EnableEPH()))
    .WithMetadata(m => m
        .WithComment("Custom optimized configuration")
        .WithComment("Tuned for specific use case")
        .WithCopyright("© 2025 Company")
        .WithXml(customMetadata)
        .WithUuid(applicationGuid, customData))
    .Build();

byte[] data = J2kImage.ToBytes(image, config);
```

## Advanced Usage

### Cloning and Customization

```csharp
// Start with a preset
var baseConfig = CompleteConfigurationPresets.HighQuality.Clone();

// Customize for specific needs
baseConfig
    .WithQuantization(q => q.WithBaseStepSize(0.003f))
    .WithMetadata(m => m.WithComment("Custom variant"));

byte[] data = baseConfig.Encode(imageSource);
```

### Conditional Configuration

```csharp
var config = new CompleteEncoderConfigurationBuilder();

// Apply different settings based on conditions
if (isHighPriority)
{
    config.ForHighQuality();
}
else
{
    config.ForBalanced();
}

if (needsMetadata)
{
    config.WithCopyright(copyrightText);
    config.WithComment(description);
}

if (isTiled)
{
    config.WithTiles(t => t.SetSize(512, 512));
}

byte[] data = config.Build().Encode(imageSource);
```

### Validation Before Encoding

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithMetadata(m => m.WithComment("Test"));

// Validate configuration
if (!config.IsValid)
{
    var errors = config.Validate();
    foreach (var error in errors)
    {
        Console.WriteLine($"Configuration error: {error}");
    }
    return;
}

// Encode
byte[] data = config.Encode(imageSource);
```

### Building Configuration Only

```csharp
// Build configuration without encoding immediately
var config = new CompleteEncoderConfigurationBuilder()
    .ForWeb()
    .WithCopyright("© 2025")
    .Build();

// Get metadata separately
var metadata = new CompleteEncoderConfigurationBuilder()
    .WithMetadata(m => m
        .WithComment("Test")
        .WithCopyright("© 2025"))
    .GetMetadata();

// Use later
byte[] data = J2kImage.ToBytes(imageSource, metadata, config);
```

### Preset Comparison

```csharp
var presets = new[]
{
    ("Lossless", CompleteConfigurationPresets.Medical),
    ("High Quality", CompleteConfigurationPresets.Photography),
    ("Balanced", CompleteConfigurationPresets.Web),
    ("High Compression", CompleteConfigurationPresets.Thumbnail)
};

foreach (var (name, preset) in presets)
{
    var data = preset.Encode(imageSource);
    Console.WriteLine($"{name}: {data.Length} bytes");
}
```

## API Reference

### CompleteEncoderConfigurationBuilder

**Quality Presets:**
- `ForLossless()` - Lossless compression
- `ForNearLossless()` - 99% quality
- `ForHighQuality()` - 90% quality
- `ForBalanced()` - 75% quality
- `ForHighCompression()` - High compression

**Use Case Presets:**
- `ForMedical()` - Medical imaging
- `ForArchival()` - Archival storage
- `ForWeb()` - Web delivery
- `ForThumbnail()` - Thumbnail generation
- `ForGeospatial()` - GIS/mapping
- `ForStreaming()` - Streaming delivery

**Configuration Methods:**
- `WithQuantization(Action<QuantizationConfigurationBuilder>)` - Configure quantization
- `WithWavelet(Action<WaveletConfigurationBuilder>)` - Configure wavelet
- `WithProgression(Action<ProgressionConfigurationBuilder>)` - Configure progression
- `WithMetadata(Action<MetadataConfigurationBuilder>)` - Configure metadata
- `WithEncoder(Action<J2KEncoderConfiguration>)` - Configure encoder
- `WithQuality(double)` - Set quality (0.0-1.0)
- `WithBitrate(float)` - Set bitrate (bpp)
- `WithTiles(Action<TileConfiguration>)` - Configure tiles
- `WithComment(string)` - Add comment
- `WithCopyright(string)` - Add copyright

**Build Methods:**
- `Build()` - Build encoder configuration
- `GetMetadata()` - Get metadata object
- `Encode(BlkImgDataSrc)` - Encode image directly

**Validation:**
- `Validate()` - Get validation errors
- `IsValid` - Check if valid

**Properties:**
- `EncoderConfiguration` - Get encoder config
- `Quantization` - Get quantization config
- `Wavelet` - Get wavelet config
- `Progression` - Get progression config
- `Metadata` - Get metadata config

### CompleteConfigurationPresets

Static preset instances:
- `Medical` - Medical imaging
- `Archival` - Archival storage
- `Web` - Web delivery
- `Thumbnail` - Thumbnail generation
- `Geospatial` - GIS/mapping
- `Streaming` - Streaming delivery
- `Photography` - High-quality photography
- `GeneralPurpose` - General purpose

## Best Practices

### 1. Start with Presets

```csharp
// Good: Start with a preset and customize
var config = CompleteConfigurationPresets.Web
    .WithCopyright("© 2025");

// Also good: Use preset methods
var config = new CompleteEncoderConfigurationBuilder()
    .ForWeb()
    .WithCopyright("© 2025");
```

### 2. Match Configuration to Use Case

| Use Case | Recommended Preset |
|----------|-------------------|
| Medical/Legal | `Medical` or `ForLossless()` |
| Photography | `Photography` or `ForHighQuality()` |
| Archival | `Archival` |
| Web Display | `Web` or `ForWeb()` |
| Mobile/Bandwidth | `ForHighCompression()` |
| Thumbnails | `Thumbnail` or `ForThumbnail()` |
| GIS/Maps | `Geospatial` or `ForGeospatial()` |
| Streaming | `Streaming` or `ForStreaming()` |

### 3. Add Metadata for Context

```csharp
// Always include copyright and relevant context
.WithCopyright("© 2025 Your Organization")
.WithComment("Purpose/Description")
.WithComment("Source/Origin")
```

### 4. Validate Before Production

```csharp
if (!config.IsValid)
{
    throw new InvalidOperationException(
        string.Join("\n", config.Validate()));
}
```

### 5. Use Appropriate Tile Sizes

```csharp
// Small images: no tiling or larger tiles
.WithTiles(t => t.SetSize(1024, 1024))

// Large images: smaller tiles for better streaming
.WithTiles(t => t.SetSize(256, 256))

// Very large images: very small tiles
.WithTiles(t => t.SetSize(128, 128))
```

## Performance Considerations

### Encoding Speed vs Quality

| Configuration | Speed | File Size | Quality | Memory |
|---------------|-------|-----------|---------|--------|
| Lossless | Slower | Largest | Perfect | High |
| Near-Lossless | Slower | Very Large | 99.9% | High |
| High Quality | Medium | Large | 95% | Medium |
| Balanced | Medium | Medium | 85% | Medium |
| High Compression | Faster | Small | 70% | Low |
| Maximum Compression | Fastest | Smallest | 50% | Low |

### Optimization Tips

1. **Use fewer decomposition levels for faster encoding:**
   ```csharp
   .WithWavelet(w => w.WithDecompositionLevels(3))
   ```

2. **Use larger tiles for less overhead:**
   ```csharp
   .WithTiles(t => t.SetSize(1024, 1024))
   ```

3. **Use derived quantization for simpler processing:**
   ```csharp
   .WithQuantization(q => q.UseDerived())
   ```

4. **Use LRCP progression for fastest encoding:**
   ```csharp
   .WithProgression(p => p.UseLRCP())
   ```

## See Also

- [Encoder Configuration Guide](ENCODER_CONFIGURATION_GUIDE.md)
- [Quantization Configuration Guide](QUANTIZATION_CONFIGURATION_GUIDE.md)
- [Wavelet Configuration Guide](WAVELET_CONFIGURATION_GUIDE.md)
- [Progression Configuration Guide](PROGRESSION_CONFIGURATION_GUIDE.md)
- [Metadata Configuration Guide](METADATA_CONFIGURATION_GUIDE.md)

---

**The Complete Builder provides the easiest and most powerful way to configure JPEG 2000 encoding in CoreJ2K!**
