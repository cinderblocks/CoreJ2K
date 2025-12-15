# CoreJ2K Quick Reference Guide

## Installation

```bash
# Core library
dotnet add package CoreJ2K

# With image support
dotnet add package CoreJ2K.Skia        # Recommended (cross-platform)
dotnet add package CoreJ2K.ImageSharp  # Modern .NET
dotnet add package CoreJ2K.Windows     # Windows-only
```

## Decoding

### Basic Decoding
```csharp
using CoreJ2K;

// From file
var image = J2kImage.FromFile("image.jp2");
var bitmap = image.As<SKBitmap>();

// From stream
var image = J2kImage.FromStream(fileStream);

// From bytes
var image = J2kImage.FromBytes(byteArray);
```

### With Configuration
```csharp
using CoreJ2K.Configuration;

var config = new J2KDecoderConfiguration()
    .WithResolution(2)           // Half resolution
    .WithMaxBytes(1024 * 1024);  // Limit to 1MB

var image = J2kImage.FromFile("image.jp2", config);
```

## Encoding - Modern API (Recommended)

### One-Liners with Presets
```csharp
using CoreJ2K.Configuration;

// Web delivery
byte[] data = CompleteConfigurationPresets.Web
    .WithCopyright("© 2025")
    .Encode(imageSource);

// Medical imaging
byte[] data = CompleteConfigurationPresets.Medical
    .WithComment("Patient: Anonymous")
    .Encode(imageSource);

// High-quality photography
byte[] data = CompleteConfigurationPresets.Photography
    .WithCopyright("© 2025 Photographer")
    .Encode(imageSource);
```

### Quality Levels
```csharp
// Lossless (perfect reconstruction)
new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .Build();

// Near-lossless (99% quality)
new CompleteEncoderConfigurationBuilder()
    .ForNearLossless()
    .Build();

// High quality (90%)
new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .Build();

// Balanced (75%)
new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .Build();

// High compression
new CompleteEncoderConfigurationBuilder()
    .ForHighCompression()
    .Build();
```

### Use Cases
```csharp
.ForMedical()      // Lossless + medical settings
.ForArchival()     // Very high quality + error resilience
.ForWeb()          // Progressive + balanced
.ForThumbnail()    // Fast + small
.ForGeospatial()   // Spatial browsing + tiled
.ForStreaming()    // Quality progressive
```

### Custom Configuration
```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .WithQuality(0.85)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.01f))
    .WithWavelet(w => w
        .UseIrreversible_9_7()
        .WithDecompositionLevels(6))
    .WithTiles(t => t.SetSize(512, 512))
    .WithMetadata(m => m
        .WithComment("Description")
        .WithCopyright("© 2025"))
    .Build();

byte[] data = J2kImage.ToBytes(image, config);
```

## Encoding - Traditional API

### Lossless
```csharp
var params = new ParameterList();
params["lossless"] = "on";
params["file_format"] = "on";
byte[] data = J2kImage.ToBytes(image, params);
```

### Lossy with Quality
```csharp
var params = new ParameterList();
params["rate"] = "0.5";        // 0.5 bits/pixel
params["file_format"] = "on";  // JP2 wrapper
byte[] data = J2kImage.ToBytes(image, params);
```

## Common Parameters

### Encoder Configuration
| Property | Modern API | Traditional | Example |
|----------|-----------|-------------|---------|
| Quality | `.WithQuality(0.8)` | `params["quality"] = "0.8"` | 0.0-1.0 |
| Bitrate | `.WithBitrate(2.0f)` | `params["rate"] = "2.0"` | bits/pixel |
| Lossless | `.ForLossless()` | `params["lossless"] = "on"` | on/off |
| Tiles | `.WithTiles(t => t.SetSize(512,512))` | `params["tiles"] = "512 512"` | width height |
| Levels | `.WithWavelet(w => w.WithDecompositionLevels(5))` | `params["Wlev"] = "5"` | 1-32 |
| Filter | `.WithWavelet(w => w.UseIrreversible_9_7())` | `params["Ffilters"] = "w9x7"` | w5x3/w9x7 |
| Progression | `.WithProgression(p => p.UseLRCP())` | `params["Aptype"] = "layer"` | layer/res/pos-comp |

### Decoder Configuration
| Property | Modern API | Traditional | Example |
|----------|-----------|-------------|---------|
| Resolution | `.WithResolution(2)` | `params["res"] = "2"` | 0-N |
| Max Layers | `.WithMaxLayers(5)` | `params["rate"] = "0 5"` | 0-N |
| Max Bytes | `.WithMaxBytes(1024)` | `params["nbytes"] = "1024"` | bytes |

## Quantization Types

```csharp
// Reversible (lossless only)
.WithQuantization(q => q.UseReversible())

// Derived (simple lossy)
.WithQuantization(q => q.UseDerived())

// Expounded (advanced lossy)
.WithQuantization(q => q.UseExpounded()
    .WithBaseStepSize(0.01f))
```

## Wavelet Filters

```csharp
// Reversible 5/3 (lossless)
.WithWavelet(w => w.UseReversible_5_3())

// Irreversible 9/7 (better lossy compression)
.WithWavelet(w => w.UseIrreversible_9_7())
```

## Progression Orders

```csharp
.WithProgression(p => p.UseLRCP())  // Quality progressive (default)
.WithProgression(p => p.UseRLCP())  // Resolution progressive
.WithProgression(p => p.UseRPCL())  // Spatial browsing
.WithProgression(p => p.UsePCRL())  // Tile access
.WithProgression(p => p.UseCPRL())  // Component access
```

## Metadata

```csharp
.WithMetadata(m => m
    .WithComment("Description")
    .WithCopyright("© 2025 Company")
    .WithXml("<metadata>...</metadata>")
    .WithUuid(guid, data))
```

## Error Resilience

```csharp
.WithEncoder(e => e.WithErrorResilience(er => er
    .EnableSOP()    // Start of packet markers
    .EnableEPH()    // End of packet headers
    .EnableAll()))  // All resilience features
```

## ROI (Region of Interest)

```csharp
.WithEncoder(e => e.WithROI(roi => roi
    .SetMaskSource(maskImage)
    .UseMaxShift()
    .SetShiftValue(10)))
```

## Complete Presets Reference

| Preset | Quality | Use Case | Settings |
|--------|---------|----------|----------|
| `Medical` | Lossless | Medical imaging | Reversible, 512×512 tiles |
| `Archival` | 98% | Long-term storage | Very high quality, error resilience |
| `Web` | 75% | Web delivery | Progressive, 512×512 tiles |
| `Thumbnail` | ~50% | Previews | Fast, 256×256 tiles, 3 levels |
| `Geospatial` | 75% | GIS/mapping | Spatial progressive, 256×256 tiles |
| `Streaming` | 75% | Progressive download | Quality progressive |
| `Photography` | 90% | High-quality photos | High quality + metadata |
| `GeneralPurpose` | 75% | General use | Balanced settings |

## File Formats

```csharp
// JP2 format (recommended - includes metadata)
params["file_format"] = "on"

// Raw codestream (smaller, no metadata)
params["file_format"] = "off"
```

## Quick Comparison

| Scenario | Code |
|----------|------|
| **Fastest encode** | `.ForThumbnail()` or 3 decomposition levels |
| **Smallest file** | `.ForHighCompression()` or low bitrate |
| **Best quality** | `.ForLossless()` or `.ForNearLossless()` |
| **Best for web** | `.ForWeb()` with progressive |
| **Medical imaging** | `.ForMedical()` (lossless) |
| **Large images** | Use tiles: `.WithTiles(t => t.SetSize(256,256))` |

## Performance Tips

1. **Use tiles for large images** (>4K resolution)
2. **Fewer decomposition levels = faster** (but less compression)
3. **Irreversible 9/7 = better compression** (but lossy)
4. **LRCP progression = fastest** encoding
5. **Disable error resilience** if not needed (smaller files)

## Common Use Cases

### Web Gallery
```csharp
CompleteConfigurationPresets.Web
    .WithProgression(p => p.UseRLCP())  // Resolution progressive
    .WithTiles(t => t.SetSize(512, 512))
    .WithCopyright("© 2025")
    .Encode(image);
```

### Medical DICOM
```csharp
CompleteConfigurationPresets.Medical
    .WithMetadata(m => m
        .WithComment("Patient: Anonymous")
        .WithXml(dicomMetadata))
    .Encode(medicalImage);
```

### Archival Scanning
```csharp
CompleteConfigurationPresets.Archival
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithEncoder(e => e.WithErrorResilience(er => er.EnableAll()))
    .WithMetadata(m => m
        .WithComment("Document ID: ABC-123")
        .WithCopyright("© 2025 Archive"))
    .Encode(scannedDocument);
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Out of memory | Use tiles: `.WithTiles(t => t.SetSize(512, 512))` |
| File too large | Reduce quality or bitrate |
| Encoding too slow | Use fewer decomposition levels or `.ForFast()` |
| Poor quality | Increase quality, bitrate, or use lossless |
| Can't decode | Check JPEG 2000 Part 1 compliance |

## Links

- **Documentation**: [Complete Builder Guide](docs/COMPLETE_BUILDER_GUIDE.md)
- **GitHub**: https://github.com/cinderblocks/CoreJ2K
- **NuGet**: https://www.nuget.org/packages/CoreJ2K/
- **Issues**: https://github.com/cinderblocks/CoreJ2K/issues

---

**Need more help?** Check the [full documentation](docs/COMPLETE_BUILDER_GUIDE.md) or [GitHub Discussions](https://github.com/cinderblocks/CoreJ2K/discussions).
