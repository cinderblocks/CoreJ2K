# CoreJ2K Codec Test

This is a demonstration program that showcases the **CoreJ2K modern configuration API**. It provides comprehensive examples of both traditional and modern encoding/decoding approaches.

## What This Demo Shows

### 1. Traditional API
- Classic encoding with ParameterList (still works!)
- Backward compatibility

### 2. Modern API - Presets
- One-liner encoding with presets
- Lossless, high quality, and web-optimized encoding
- Complete configuration presets (Medical, Photography, Archival)

### 3. Modern API - Custom Configuration
- Fluent configuration builder
- Fine-grained control over all parameters
- Configuration validation
- Metadata management

### 4. Integration Package Extensions
- **SkiaSharp** - Direct file saving methods
- **ImageSharp** - Modern .NET image processing (NET8.0+)
- **Pfim** - DDS/TGA game texture support
- **Windows** - System.Drawing integration

### 5. Decoding
- Standard decoding
- Configured decoding (resolution control)
- Extension method usage

### 6. Performance
- Comparison between traditional and modern APIs
- Zero overhead demonstration

## Running the Demo

```bash
# Build
dotnet build codectest.csproj

# Run with .NET 8.0 (includes ImageSharp)
dotnet run --framework net8.0

# Run with .NET Framework 4.8.1
dotnet run --framework net481
```

## Sample Output

```
CoreJ2K Modern API Demo
=======================

?? Traditional API Encoding
---------------------------
? Encoded mono image (traditional API): 15,234 bytes
? Encoded racoon (traditional API): 89,456 bytes

?? Modern API - Presets
-----------------------
? Lossless preset: 125,678 bytes
? High quality preset: 67,890 bytes
? Web preset: 45,123 bytes
? Medical preset: 128,901 bytes
? Photography preset: 72,345 bytes
? Archival preset: 98,765 bytes

??  Modern API - Custom Configuration
-------------------------------------
? Custom configuration: 56,789 bytes
  ? Configuration is valid
  Configuration: Complete Configuration: Quantization: Expounded...

?? SkiaSharp Integration
------------------------
? Saved lossless (SkiaSharp)
? Saved high quality with copyright (SkiaSharp)
? Saved with custom config (SkiaSharp)

?? Pfim Integration (DDS/TGA)
-----------------------------
  ? Pfim is for DDS/TGA game textures
  Usage:
    var dds = Dds.Create("texture.dds");
    dds.SaveAsJ2KWeb("texture.jp2", "© 2025 Game Studio");
  ? Pfim extension methods available

???  ImageSharp Integration
-------------------------
? Saved lossless (ImageSharp)
? Saved high quality with copyright (ImageSharp)
? Saved with custom config (ImageSharp)

?? Decoding Demo
----------------
? Decoded: 1920×1080 pixels
? Decoded at half resolution: 960×540 pixels
? Decoded with extension method: 1920×1080 pixels

? Performance Comparison
------------------------
Traditional API: 156ms ? 89,456 bytes
Modern API (preset): 158ms ? 45,123 bytes
Modern API (custom): 159ms ? 56,789 bytes

? All methods have similar performance
? Modern API adds zero overhead!

Decoding: 45ms for 1920×1080 image

? All demonstrations complete!
Check the 'output' directory for generated files.
```

## Output Files

The demo generates various JPEG 2000 files in the `output` directory:

- `traditional_*.jp2` - Traditional API examples
- `preset_*.jp2` - Preset configuration examples
- `custom_configured.jp2` - Custom configuration example
- `skia_*.jp2` - SkiaSharp integration examples
- `imagesharp_*.jp2` - ImageSharp integration examples (NET8.0+)

## Code Examples

### One-Liner Encoding
```csharp
// Lossless
bitmap.SaveAsJ2KLossless("output.jp2");

// High quality with copyright
bitmap.SaveAsJ2KHighQuality("output.jp2", "© 2025");

// Web optimized
bitmap.SaveAsJ2KWeb("output.jp2");
```

### Custom Configuration
```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithMetadata(m => m
        .WithComment("My image")
        .WithCopyright("© 2025"));

bitmap.SaveAsJ2K("output.jp2", config);
```

### Preset Usage
```csharp
var data = bitmap.EncodeToJ2K(CompleteConfigurationPresets.Photography);
```

## Requirements

- .NET 8.0 or .NET Framework 4.8.1
- Sample images in `samples/` directory
- CoreJ2K and integration packages

## See Also

- [Complete Builder Guide](../docs/COMPLETE_BUILDER_GUIDE.md)
- [Integration Packages Guide](../docs/INTEGRATION_PACKAGES_GUIDE.md)
- [Quick Reference](../docs/QUICK_REFERENCE.md)
- [README](../README.md)
