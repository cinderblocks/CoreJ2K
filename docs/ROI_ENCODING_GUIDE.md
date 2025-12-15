# ROI (Region of Interest) Encoding Guide

## Overview

Region of Interest (ROI) encoding allows you to specify parts of an image that should be encoded with higher quality than the background. CoreJ2K implements the JPEG 2000 Part 1 Maxshift ROI method, which ensures that ROI regions can be decoded first and with better quality at any given bitrate.

## Table of Contents

- [Quick Start](#quick-start)
- [API Reference](#api-reference)
- [ROI Shapes](#roi-shapes)
- [Configuration Options](#configuration-options)
- [Performance Optimization](#performance-optimization)
- [Examples](#examples)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Quick Start

### Basic ROI Encoding

```csharp
using CoreJ2K;
using CoreJ2K.j2k.roi;
using SkiaSharp;

// Load image
var bitmap = SKBitmap.Decode("input.png");

// Configure ROI
var roiConfig = new ROIConfiguration()
    .AddRectangle(component: 0, x: 100, y: 100, width: 400, height: 300)
    .SetBlockAlignment(false);

// Encode with ROI
var encoder = new J2KEncoder()
    .WithROI(roiConfig);
    
byte[] jp2Data = encoder.Encode(bitmap);
File.WriteAllBytes("output.jp2", jp2Data);
```

## API Reference

### ROIConfiguration Class

The `ROIConfiguration` class provides a fluent API for configuring ROI encoding.

#### Key Methods

| Method | Description |
|--------|-------------|
| `AddRectangle(component, x, y, width, height)` | Adds a rectangular ROI |
| `AddCircle(component, centerX, centerY, radius)` | Adds a circular ROI |
| `AddArbitraryShape(component, maskFilePath)` | Adds ROI from PGM mask file |
| `SetBlockAlignment(enabled)` | Enables/disables block-aligned ROI |
| `SetStartLevel(level)` | Sets minimum resolution level for ROI |
| `SetScalingMode(mode)` | Sets the ROI scaling method |
| `ForceGenericMask(force)` | Forces generic mask generation |
| `Validate()` | Validates configuration and returns errors |

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BlockAligned` | bool | false | Use block-aligned ROI encoding |
| `StartLevel` | int | -1 | Force lowest N resolution levels to ROI |
| `ForceGenericMaskGeneration` | bool | false | Force generic mask (even for rectangles) |
| `ScalingMode` | ROIScalingMode | MaxShift | ROI scaling method |
| `Regions` | IReadOnlyList | - | List of configured ROI regions |

## ROI Shapes

### Rectangular ROI

Best performance for regular rectangular regions.

```csharp
var config = new ROIConfiguration()
    .AddRectangle(
        component: 0,    // Component index (0 = all components)
        x: 100,          // Upper-left X coordinate
        y: 100,          // Upper-left Y coordinate
        width: 400,      // Width in pixels
        height: 300      // Height in pixels
    );
```

Alternative using Rectangle struct:
```csharp
var rect = new Rectangle(100, 100, 400, 300);
var config = new ROIConfiguration()
    .AddRectangle(component: 0, rect);
```

### Circular ROI

Useful for spotlight effects or radial regions.

```csharp
var config = new ROIConfiguration()
    .AddCircle(
        component: 0,    // Component index
        centerX: 500,    // Center X coordinate
        centerY: 400,    // Center Y coordinate
        radius: 200      // Radius in pixels
    );
```

Alternative using Point struct:
```csharp
var center = new Point(500, 400);
var config = new ROIConfiguration()
    .AddCircle(component: 0, center, radius: 200);
```

### Arbitrary Shape ROI

For complex, irregular regions using a PGM mask file.

```csharp
var config = new ROIConfiguration()
    .AddArbitraryShape(component: 0, "roi_mask.pgm");
```

**PGM Mask Format:**
- Must be same size as input image
- Non-zero pixels = ROI
- Zero pixels = background
- Grayscale values are treated as binary (0 vs non-zero)

Example PGM file creation:
```csharp
// Create a simple PGM mask
using (var writer = new StreamWriter("mask.pgm"))
{
    writer.WriteLine("P2");  // ASCII PGM
    writer.WriteLine($"{width} {height}");
    writer.WriteLine("255"); // Max value
    
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            // Set ROI pixels to non-zero
            writer.Write(IsROI(x, y) ? "255 " : "0 ");
        }
        writer.WriteLine();
    }
}
```

## Configuration Options

### Block Alignment

Block-aligned ROI treats entire code-blocks as ROI if any coefficient belongs to ROI.

```csharp
// Recommended for better performance
var config = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 400, 300)
    .SetBlockAlignment(true);
```

**Benefits:**
- ? Faster encoding (no coefficient scaling)
- ? Simpler implementation
- ? Less precise ROI boundaries

**When to use:**
- Performance is critical
- ROI boundaries don't need pixel-perfect accuracy
- ROI regions are significantly larger than code-blocks

### Start Level

Forces the lowest N resolution levels to belong entirely to the ROI.

```csharp
var config = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 400, 300)
    .SetStartLevel(2); // Levels 0 and 1 are fully ROI
```

**Use cases:**
- Progressive transmission where low-res preview needs ROI quality
- Thumbnail generation from ROI regions
- Multi-scale image analysis

### Scaling Mode

Currently only MaxShift method is supported (JPEG 2000 Part 1 standard).

```csharp
var config = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 400, 300)
    .SetScalingMode(ROIScalingMode.MaxShift);
```

## Performance Optimization

### 1. ROI Mask Caching

CoreJ2K automatically caches computed ROI masks to improve performance when encoding multiple tiles or images with similar ROI configurations.

```csharp
// Cache is automatically managed
// Access statistics:
var cache = encoder.GetROIMaskCache();
Console.WriteLine($"Hit ratio: {cache.Statistics.HitRatio:P2}");
Console.WriteLine($"Memory usage: {cache.GetMemoryUsage() / 1024} KB");
```

### 2. Memory-Efficient Masks

For large images, use optimized mask representations:

**Bit-Packed Masks** (32x less memory for binary masks):
```csharp
// Automatically used internally for rectangular/circular ROIs
// Manual usage:
var bitPacked = new BitPackedROIMask(width, height);
bitPacked[x, y] = true; // Set ROI pixel
```

**Sparse Masks** (efficient for small ROIs):
```csharp
// Automatically chosen when ROI coverage < ~30%
var sparse = new SparseROIMask(maskData, width, height);
Console.WriteLine($"Memory savings: {sparse.GetMemorySavingsRatio():F1}x");
```

### 3. Performance Tips

| Scenario | Recommendation | Performance Impact |
|----------|----------------|-------------------|
| Multiple small ROIs | Use rectangular ROIs | ??? Excellent |
| Large irregular ROI | Use block alignment | ?? Good |
| Precise boundaries needed | Disable block alignment | ? Fair |
| Multiple images, same ROI | Reuse ROIConfiguration | ?? Good |
| Very large images | Enable mask caching | ??? Excellent |

## Examples

### Example 1: Face ROI in Portrait

```csharp
// Encode portrait with face region at higher quality
var faceRect = DetectFace(image); // Your face detection

var config = new ROIConfiguration()
    .AddRectangle(0, faceRect) // All components
    .SetBlockAlignment(false);  // Precise boundaries

var encoder = new J2KEncoder()
    .WithROI(config)
    .WithQuality(0.5); // 0.5 bpp overall, but face gets more

byte[] data = encoder.Encode(portrait);
```

### Example 2: Medical Image with Multiple ROIs

```csharp
// Highlight multiple anatomical regions
var config = new ROIConfiguration()
    .AddCircle(0, tumor1X, tumor1Y, tumorRadius)
    .AddCircle(0, tumor2X, tumor2Y, tumorRadius)
    .AddRectangle(0, organX, organY, organWidth, organHeight)
    .SetStartLevel(0); // Even thumbnail shows ROIs clearly

var encoder = new J2KEncoder()
    .WithROI(config)
    .WithLossless(true); // Lossless for medical
    
byte[] dicomData = encoder.Encode(medicalImage);
```

### Example 3: Satellite Image with Ground Truth

```csharp
// Encode satellite image with high-quality region of interest
var targetArea = new Rectangle(1000, 1000, 500, 500);

var config = new ROIConfiguration()
    .AddRectangle(0, targetArea)
    .SetBlockAlignment(true) // Performance for large image
    .SetStartLevel(-1);

var encoder = new J2KEncoder()
    .WithROI(config)
    .WithTileSize(512, 512) // Tiling for large image
    .WithQuality(0.8);

byte[] geoData = encoder.Encode(satelliteImage);
```

### Example 4: Text Document Enhancement

```csharp
// Prioritize text regions in scanned document
var textMask = DetectTextRegions(document); // Returns PGM mask
SavePGMMask(textMask, "text_mask.pgm");

var config = new ROIConfiguration()
    .AddArbitraryShape(0, "text_mask.pgm")
    .SetBlockAlignment(false);

var encoder = new J2KEncoder()
    .WithROI(config)
    .WithQuality(0.4); // Background at low quality, text sharp

byte[] archiveData = encoder.Encode(document);
```

### Example 5: Multi-Component ROI

```csharp
// Different ROI for each color component
var config = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 200, 200) // Red channel
    .AddRectangle(1, 150, 150, 200, 200) // Green channel
    .AddRectangle(2, 200, 200, 200, 200) // Blue channel
    .SetBlockAlignment(true);

byte[] data = new J2KEncoder()
    .WithROI(config)
    .Encode(colorImage);
```

### Example 6: Progressive ROI Transmission

```csharp
// Configure for optimal progressive decoding
var config = new ROIConfiguration()
    .AddRectangle(0, centerX, centerY, roiWidth, roiHeight)
    .SetStartLevel(2); // First 2 levels entirely ROI

var encoder = new J2KEncoder()
    .WithROI(config)
    .WithProgressionOrder(ProgressionOrder.RPCL) // Resolution-Position
    .WithQualityLayers(new[] { 0.1, 0.3, 0.6, 1.0 });

byte[] progressiveData = encoder.Encode(image);
```

## Best Practices

### 1. ROI Size Selection

```csharp
// ? Good: ROI covers 10-30% of image
var config = new ROIConfiguration()
    .AddRectangle(0, x, y, width, height);

// ?? Avoid: ROI covers > 50% (defeats purpose)
// ?? Avoid: ROI too small (< 64x64 pixels)
```

### 2. Component Selection

```csharp
// ? Good: Apply to all components
.AddRectangle(-1, x, y, w, h) // or component: 0 for first

// ? Good: Selective component ROI (advanced)
.AddRectangle(0, x1, y1, w1, h1) // Red
.AddRectangle(1, x2, y2, w2, h2) // Green

// ?? Usually unnecessary: Different ROI per component
```

### 3. Validation

```csharp
var config = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 400, 300);

// Always validate before encoding
if (!config.IsValid)
{
    var errors = config.Validate();
    foreach (var error in errors)
        Console.WriteLine($"Error: {error}");
    return;
}
```

### 4. Error Handling

```csharp
try
{
    var config = new ROIConfiguration()
        .AddArbitraryShape(0, maskPath);
    
    if (!config.IsValid)
        throw new InvalidOperationException(
            string.Join("\n", config.Validate()));
    
    byte[] data = new J2KEncoder()
        .WithROI(config)
        .Encode(image);
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"ROI mask file not found: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Invalid ROI configuration: {ex.Message}");
}
```

## Troubleshooting

### Issue: ROI not visible in decoded image

**Possible causes:**
1. ROI coordinates outside image bounds
2. Bitrate too low to see difference
3. ROI too small relative to image size

**Solutions:**
```csharp
// Ensure ROI is within bounds
var config = new ROIConfiguration()
    .AddRectangle(0, 
        x: Math.Max(0, roiX),
        y: Math.Max(0, roiY),
        width: Math.Min(roiWidth, imageWidth - roiX),
        height: Math.Min(roiHeight, imageHeight - roiY));

// Use higher bitrate to see ROI effect
.WithQuality(0.5) // At least 0.5 bpp recommended

// Validate configuration
if (!config.IsValid)
    LogErrors(config.Validate());
```

### Issue: Poor performance with large ROIs

**Solutions:**
```csharp
// Enable block alignment
.SetBlockAlignment(true)

// Use rectangular ROIs when possible (faster than arbitrary)
.AddRectangle(0, x, y, w, h) // Instead of PGM mask

// Enable caching (automatic but can tune size)
encoder.SetROICacheSize(200); // Increase cache size
```

### Issue: File size larger than expected

**Causes:**
- ROI covers too much of image
- Using lossless with ROI (redundant)

**Solutions:**
```csharp
// Check ROI coverage
double roiCoverage = (roiWidth * roiHeight) / (imageWidth * imageHeight);
if (roiCoverage > 0.3)
    Console.WriteLine("Warning: ROI covers > 30% of image");

// Use lossy compression
.WithQuality(0.8) // Instead of .WithLossless(true)
```

### Issue: Validation errors

```csharp
var config = new ROIConfiguration()
    .AddRectangle(0, 100, 100, 400, 300);

var errors = config.Validate();
if (errors.Any())
{
    Console.WriteLine("Validation errors:");
    foreach (var error in errors)
        Console.WriteLine($"  - {error}");
}

// Common validation errors:
// - "width must be positive" ? Check ROI dimensions
// - "mask file not found" ? Verify file path
// - "at least one ROI must be defined" ? Add ROI region
// - "StartLevel must be -1 or greater" ? Fix start level value
```

## See Also

- [JPEG 2000 Encoding Guide](ENCODING_GUIDE.md)
- [Performance Tuning](PERFORMANCE.md)
- [API Documentation](API.md)
- [ISO/IEC 15444-1 Standard](https://www.iso.org/standard/78321.html)
