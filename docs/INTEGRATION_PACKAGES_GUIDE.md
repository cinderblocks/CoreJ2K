# Integration Packages - Modern API Usage Guide

## Overview

All CoreJ2K integration packages (Skia, ImageSharp, Windows, Pfim) now have **extension methods** that make using the modern configuration API even easier. These extensions provide convenient, type-safe methods for encoding and decoding JPEG 2000 images directly from platform-specific image types.

---

## CoreJ2K.Skia (SkiaSharp)

### Installation
```bash
dotnet add package CoreJ2K.Skia
```

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.Skia;
using CoreJ2K.Configuration;
using SkiaSharp;

// Load image
var bitmap = SKBitmap.Decode("photo.jpg");

// Lossless encoding
byte[] lossless = bitmap.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = bitmap.EncodeToJ2KHighQuality("© 2025 Photographer");

// Web optimized
byte[] webImage = bitmap.EncodeToJ2KWeb("© 2025 Company");

// Save directly
bitmap.SaveAsJ2KLossless("output.jp2");
bitmap.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = bitmap.EncodeToJ2K(CompleteConfigurationPresets.Web);

// Or with custom metadata
byte[] data = bitmap.EncodeToJ2K(
    CompleteConfigurationPresets.Photography
        .WithComment("Sunset over mountains")
        .WithCopyright("© 2025 Photographer"));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithTiles(t => t.SetSize(512, 512))
    .WithMetadata(m => m
        .WithComment("Product photo")
        .WithCopyright("© 2025 Company")
        .WithXml(xmpData));

byte[] data = bitmap.EncodeToJ2K(builder);

// Or save directly
bitmap.SaveAsJ2K("output.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.Skia;
using CoreJ2K.Configuration;

// Simple decoding
var bitmap = SKBitmapJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithResolution(2);  // Half resolution

var thumbnail = SKBitmapJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var bitmap2 = SKBitmapJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var bitmap3 = SKBitmapJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example

```csharp
using SkiaSharp;
using CoreJ2K.Skia;
using CoreJ2K.Configuration;

// Load and process image
var bitmap = SKBitmap.Decode("input.jpg");

// Apply processing
using var surface = new SKCanvas(bitmap);
// ... apply filters, transforms, etc.

// Encode with high quality for archival
bitmap.SaveAsJ2K("archive.jp2", 
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Processed: 2025-01-15")
            .WithComment("Source: input.jpg")
            .WithCopyright("© 2025 Organization")
            .WithXml("<processing>" +
                     "<filters>sharpen,denoise</filters>" +
                     "<timestamp>2025-01-15T10:30:00Z</timestamp>" +
                     "</processing>")));

// Also save web version
bitmap.SaveAsJ2KWeb("web.jp2", "© 2025 Organization");
```

---

## CoreJ2K.ImageSharp (SixLabors.ImageSharp)

### Installation
```bash
dotnet add package CoreJ2K.ImageSharp
```

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load image
var image = Image.Load<Rgba32>("photo.jpg");

// Lossless encoding
byte[] lossless = image.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = image.EncodeToJ2KHighQuality("© 2025 Photographer");

// Web optimized
byte[] webImage = image.EncodeToJ2KWeb("© 2025 Company");

// Save directly
image.SaveAsJ2KLossless("output.jp2");
image.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = image.EncodeToJ2K(CompleteConfigurationPresets.Photography);

// Or with custom metadata
byte[] data = image.EncodeToJ2K(
    CompleteConfigurationPresets.Web
        .WithComment("E-commerce product")
        .WithCopyright("© 2025 Shop Inc."));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.01f))
    .WithWavelet(w => w
        .UseIrreversible_9_7()
        .WithDecompositionLevels(5))
    .WithMetadata(m => m
        .WithComment("Product SKU: ABC-123")
        .WithCopyright("© 2025 Company"));

byte[] data = image.EncodeToJ2K(builder);

// Or save directly
image.SaveAsJ2K("output.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;
using SixLabors.ImageSharp.PixelFormats;

// Simple decoding
var image = ImageSharpJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithResolution(1);  // Quarter resolution

var thumbnail = ImageSharpJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var image2 = ImageSharpJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var image3 = ImageSharpJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;

// Load and process image
var image = Image.Load<Rgba32>("input.jpg");

// Apply ImageSharp processing
image.Mutate(x => x
    .Resize(1920, 1080)
    .GaussianSharpen()
    .AutoOrient());

// Encode with custom settings
image.SaveAsJ2K("output.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForWeb()
        .WithProgression(p => p.UseRLCP())  // Resolution progressive
        .WithTiles(t => t.SetSize(512, 512))
        .WithMetadata(m => m
            .WithComment("Processed web image")
            .WithComment($"Dimensions: {image.Width}×{image.Height}")
            .WithCopyright("© 2025 Company")));
```

---

## CoreJ2K.Windows (System.Drawing)

### Installation
```bash
dotnet add package CoreJ2K.Windows
```

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.Windows;
using CoreJ2K.Configuration;
using System.Drawing;

// Load image
var bitmap = new Bitmap("photo.jpg");

// Lossless encoding
byte[] lossless = bitmap.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = bitmap.EncodeToJ2KHighQuality("© 2025 Photographer");

// Web optimized
byte[] webImage = bitmap.EncodeToJ2KWeb("© 2025 Company");

// Save directly
bitmap.SaveAsJ2KLossless("output.jp2");
bitmap.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = bitmap.EncodeToJ2K(CompleteConfigurationPresets.Photography);

// Or with custom metadata
byte[] data = bitmap.EncodeToJ2K(
    CompleteConfigurationPresets.Archival
        .WithComment("Scanned document")
        .WithCopyright("© 2025 Archives"));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForArchival()
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithEncoder(e => e.WithErrorResilience(er => er.EnableAll()))
    .WithMetadata(m => m
        .WithComment("Historical document")
        .WithComment("Date: 1865")
        .WithCopyright("© 2025 Museum"));

byte[] data = bitmap.EncodeToJ2K(builder);

// Or save directly
bitmap.SaveAsJ2K("archive.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.Windows;
using CoreJ2K.Configuration;
using System.Drawing;

// Simple decoding
var bitmap = BitmapJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithMaxBytes(1024 * 1024);  // Limit to 1MB

var preview = BitmapJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var bitmap2 = BitmapJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var bitmap3 = BitmapJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using CoreJ2K.Windows;
using CoreJ2K.Configuration;

// Load scanned document
var bitmap = new Bitmap("scan.tif");

// Process if needed
using (var g = Graphics.FromImage(bitmap))
{
    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
    // ... additional processing
}

// Archive with maximum quality
bitmap.SaveAsJ2K("document_archive.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Document ID: DOC-2025-0001")
            .WithComment("Scan Date: 2025-01-15")
            .WithComment("Scanner: HP ScanJet Pro")
            .WithComment("DPI: 600")
            .WithCopyright("© 2025 Organization")
            .WithXml("<document>" +
                     "<type>letter</type>" +
                     "<year>1865</year>" +
                     "<condition>good</condition>" +
                     "</document>")));
```

---

## CoreJ2K.Pfim (Pfim - DDS/TGA)

### Installation
```bash
dotnet add package CoreJ2K.Pfim
```

### About Pfim
Pfim is a lightweight library for reading DDS (DirectDraw Surface) and TGA (Targa) image formats, commonly used in game development and 3D graphics. CoreJ2K.Pfim provides integration between Pfim and JPEG 2000.

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;
using Pfim;

// Load DDS or TGA image
var image = Dds.Create("texture.dds");
// or
var image = Targa.Create("texture.tga");

// Lossless encoding
byte[] lossless = image.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = image.EncodeToJ2KHighQuality("© 2025 Game Studio");

// Web optimized
byte[] webImage = image.EncodeToJ2KWeb("© 2025 Company");

// Save directly
image.SaveAsJ2KLossless("output.jp2");
image.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = image.EncodeToJ2K(CompleteConfigurationPresets.Web);

// Or with custom metadata
byte[] data = image.EncodeToJ2K(
    CompleteConfigurationPresets.Photography
        .WithComment("Game texture")
        .WithCopyright("© 2025 Game Studio"));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithMetadata(m => m
        .WithComment("Game Asset: texture_wall_01")
        .WithComment("Resolution: 2048×2048")
        .WithCopyright("© 2025 Game Studio"));

byte[] data = image.EncodeToJ2K(builder);

// Or save directly
image.SaveAsJ2K("texture.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;
using Pfim;

// Simple decoding
var image = PfimJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithResolution(2);  // Half resolution

var thumbnail = PfimJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var image2 = PfimJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var image3 = PfimJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example: Game Asset Pipeline

```csharp
using Pfim;
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;

// Load game textures (DDS format)
var diffuseMap = Dds.Create("diffuse.dds");
var normalMap = Dds.Create("normal.dds");
var specularMap = Dds.Create("specular.dds");

```csharp
// Integration Packages - Modern API Usage Guide

## Overview

All CoreJ2K integration packages (Skia, ImageSharp, Windows, Pfim) now have **extension methods** that make using the modern configuration API even easier. These extensions provide convenient, type-safe methods for encoding and decoding JPEG 2000 images directly from platform-specific image types.

---

## CoreJ2K.Skia (SkiaSharp)

### Installation
```bash
dotnet add package CoreJ2K.Skia
```

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.Skia;
using CoreJ2K.Configuration;
using SkiaSharp;

// Load image
var bitmap = SKBitmap.Decode("photo.jpg");

// Lossless encoding
byte[] lossless = bitmap.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = bitmap.EncodeToJ2KHighQuality("© 2025 Photographer");

// Web optimized
byte[] webImage = bitmap.EncodeToJ2KWeb("© 2025 Company");

// Save directly
bitmap.SaveAsJ2KLossless("output.jp2");
bitmap.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = bitmap.EncodeToJ2K(CompleteConfigurationPresets.Web);

// Or with custom metadata
byte[] data = bitmap.EncodeToJ2K(
    CompleteConfigurationPresets.Photography
        .WithComment("Sunset over mountains")
        .WithCopyright("© 2025 Photographer"));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithTiles(t => t.SetSize(512, 512))
    .WithMetadata(m => m
        .WithComment("Product photo")
        .WithCopyright("© 2025 Company")
        .WithXml(xmpData));

byte[] data = bitmap.EncodeToJ2K(builder);

// Or save directly
bitmap.SaveAsJ2K("output.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.Skia;
using CoreJ2K.Configuration;

// Simple decoding
var bitmap = SKBitmapJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithResolution(2);  // Half resolution

var thumbnail = SKBitmapJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var bitmap2 = SKBitmapJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var bitmap3 = SKBitmapJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example

```csharp
using SkiaSharp;
using CoreJ2K.Skia;
using CoreJ2K.Configuration;

// Load and process image
var bitmap = SKBitmap.Decode("input.jpg");

// Apply processing
using var surface = new SKCanvas(bitmap);
// ... apply filters, transforms, etc.

// Encode with high quality for archival
bitmap.SaveAsJ2K("archive.jp2", 
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Processed: 2025-01-15")
            .WithComment("Source: input.jpg")
            .WithCopyright("© 2025 Organization")
            .WithXml("<processing>" +
                     "<filters>sharpen,denoise</filters>" +
                     "<timestamp>2025-01-15T10:30:00Z</timestamp>" +
                     "</processing>")));

// Also save web version
bitmap.SaveAsJ2KWeb("web.jp2", "© 2025 Organization");
```

---

## CoreJ2K.ImageSharp (SixLabors.ImageSharp)

### Installation
```bash
dotnet add package CoreJ2K.ImageSharp
```

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load image
var image = Image.Load<Rgba32>("photo.jpg");

// Lossless encoding
byte[] lossless = image.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = image.EncodeToJ2KHighQuality("© 2025 Photographer");

// Web optimized
byte[] webImage = image.EncodeToJ2KWeb("© 2025 Company");

// Save directly
image.SaveAsJ2KLossless("output.jp2");
image.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = image.EncodeToJ2K(CompleteConfigurationPresets.Photography);

// Or with custom metadata
byte[] data = image.EncodeToJ2K(
    CompleteConfigurationPresets.Web
        .WithComment("E-commerce product")
        .WithCopyright("© 2025 Shop Inc."));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.01f))
    .WithWavelet(w => w
        .UseIrreversible_9_7()
        .WithDecompositionLevels(5))
    .WithMetadata(m => m
        .WithComment("Product SKU: ABC-123")
        .WithCopyright("© 2025 Company"));

byte[] data = image.EncodeToJ2K(builder);

// Or save directly
image.SaveAsJ2K("output.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;
using SixLabors.ImageSharp.PixelFormats;

// Simple decoding
var image = ImageSharpJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithResolution(1);  // Quarter resolution

var thumbnail = ImageSharpJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var image2 = ImageSharpJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var image3 = ImageSharpJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;

// Load and process image
var image = Image.Load<Rgba32>("input.jpg");

// Apply ImageSharp processing
image.Mutate(x => x
    .Resize(1920, 1080)
    .GaussianSharpen()
    .AutoOrient());

// Encode with custom settings
image.SaveAsJ2K("output.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForWeb()
        .WithProgression(p => p.UseRLCP())  // Resolution progressive
        .WithTiles(t => t.SetSize(512, 512))
        .WithMetadata(m => m
            .WithComment("Processed web image")
            .WithComment($"Dimensions: {image.Width}×{image.Height}")
            .WithCopyright("© 2025 Company")));
```

---

## CoreJ2K.Windows (System.Drawing)

### Installation
```bash
dotnet add package CoreJ2K.Windows
```

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.Windows;
using CoreJ2K.Configuration;
using System.Drawing;

// Load image
var bitmap = new Bitmap("photo.jpg");

// Lossless encoding
byte[] lossless = bitmap.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = bitmap.EncodeToJ2KHighQuality("© 2025 Photographer");

// Web optimized
byte[] webImage = bitmap.EncodeToJ2KWeb("© 2025 Company");

// Save directly
bitmap.SaveAsJ2KLossless("output.jp2");
bitmap.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = bitmap.EncodeToJ2K(CompleteConfigurationPresets.Photography);

// Or with custom metadata
byte[] data = bitmap.EncodeToJ2K(
    CompleteConfigurationPresets.Archival
        .WithComment("Scanned document")
        .WithCopyright("© 2025 Archives"));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForArchival()
    .WithTiles(t => t.SetSize(1024, 1024))
    .WithEncoder(e => e.WithErrorResilience(er => er.EnableAll()))
    .WithMetadata(m => m
        .WithComment("Historical document")
        .WithComment("Date: 1865")
        .WithCopyright("© 2025 Museum"));

byte[] data = bitmap.EncodeToJ2K(builder);

// Or save directly
bitmap.SaveAsJ2K("archive.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.Windows;
using CoreJ2K.Configuration;
using System.Drawing;

// Simple decoding
var bitmap = BitmapJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithMaxBytes(1024 * 1024);  // Limit to 1MB

var preview = BitmapJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var bitmap2 = BitmapJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var bitmap3 = BitmapJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using CoreJ2K.Windows;
using CoreJ2K.Configuration;

// Load scanned document
var bitmap = new Bitmap("scan.tif");

// Process if needed
using (var g = Graphics.FromImage(bitmap))
{
    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
    // ... additional processing
}

// Archive with maximum quality
bitmap.SaveAsJ2K("document_archive.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Document ID: DOC-2025-0001")
            .WithComment("Scan Date: 2025-01-15")
            .WithComment("Scanner: HP ScanJet Pro")
            .WithComment("DPI: 600")
            .WithCopyright("© 2025 Organization")
            .WithXml("<document>" +
                     "<type>letter</type>" +
                     "<year>1865</year>" +
                     "<condition>good</condition>" +
                     "</document>")));
```

---

## CoreJ2K.Pfim (Pfim - DDS/TGA)

### Installation
```bash
dotnet add package CoreJ2K.Pfim
```

### About Pfim
Pfim is a lightweight library for reading DDS (DirectDraw Surface) and TGA (Targa) image formats, commonly used in game development and 3D graphics. CoreJ2K.Pfim provides integration between Pfim and JPEG 2000.

### Encoding Examples

#### Quick One-Liners
```csharp
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;
using Pfim;

// Load DDS or TGA image
var image = Dds.Create("texture.dds");
// or
var image = Targa.Create("texture.tga");

// Lossless encoding
byte[] lossless = image.EncodeToJ2KLossless();

// High quality with copyright
byte[] highQuality = image.EncodeToJ2KHighQuality("© 2025 Game Studio");

// Web optimized
byte[] webImage = image.EncodeToJ2KWeb("© 2025 Company");

// Save directly
image.SaveAsJ2KLossless("output.jp2");
image.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

#### Using Presets
```csharp
// With complete configuration presets
byte[] data = image.EncodeToJ2K(CompleteConfigurationPresets.Web);

// Or with custom metadata
byte[] data = image.EncodeToJ2K(
    CompleteConfigurationPresets.Photography
        .WithComment("Game texture")
        .WithCopyright("© 2025 Game Studio"));
```

#### Using Fluent Configuration
```csharp
// Build custom configuration
var builder = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithMetadata(m => m
        .WithComment("Game Asset: texture_wall_01")
        .WithComment("Resolution: 2048×2048")
        .WithCopyright("© 2025 Game Studio"));

byte[] data = image.EncodeToJ2K(builder);

// Or save directly
image.SaveAsJ2K("texture.jp2", builder);
```

### Decoding Examples

```csharp
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;
using Pfim;

// Simple decoding
var image = PfimJ2kExtensions.FromJ2KFile("image.jp2");

// With configuration
var config = new J2KDecoderConfiguration()
    .WithResolution(2);  // Half resolution

var thumbnail = PfimJ2kExtensions.FromJ2KFile("image.jp2", config);

// From bytes
byte[] data = File.ReadAllBytes("image.jp2");
var image2 = PfimJ2kExtensions.FromJ2KBytes(data);

// From stream
using var stream = File.OpenRead("image.jp2");
var image3 = PfimJ2kExtensions.FromJ2KStream(stream);
```

### Real-World Example: Game Asset Pipeline

```csharp
using Pfim;
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;

// Load game textures (DDS format)
var diffuseMap = Dds.Create("diffuse.dds");
var normalMap = Dds.Create("normal.dds");
var specularMap = Dds.Create("specular.dds");

// Archive textures as JPEG 2000 for storage/distribution
diffuseMap.SaveAsJ2K("archive/diffuse.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Asset: Wall Texture Set")
            .WithComment("Type: Diffuse Map")
            .WithCopyright("© 2025 Game Studio")
            .WithXml("<gameAsset>" +
                     "<category>textures</category>" +
                     "<material>concrete</material>" +
                     "<lod>0</lod>" +
                     "</gameAsset>")));

normalMap.SaveAsJ2K("archive/normal.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Asset: Wall Texture Set")
            .WithComment("Type: Normal Map")
            .WithCopyright("© 2025 Game Studio")));

specularMap.SaveAsJ2K("archive/specular.jp2",
    new CompleteEncoderConfigurationBuilder()
        .ForArchival()
        .WithMetadata(m => m
            .WithComment("Asset: Wall Texture Set")
            .WithComment("Type: Specular Map")
            .WithCopyright("© 2025 Game Studio")));

Console.WriteLine("Game textures archived as JPEG 2000");
```

### Use Case: Texture Conversion Pipeline

```csharp
using Pfim;
using CoreJ2K.Pfim;
using CoreJ2K.Configuration;
using System.IO;

public class TextureConverter
{
    public void ConvertDdsToJp2(string inputPath, string outputPath)
    {
        // Load DDS texture
        var dds = Dds.Create(inputPath);
        
        // Determine appropriate compression based on texture size
        var builder = dds.Width > 2048
            ? new CompleteEncoderConfigurationBuilder().ForArchival()
            : new CompleteEncoderConfigurationBuilder().ForBalanced();
        
        // Add metadata
        builder.WithMetadata(m => m
            .WithComment($"Source: {Path.GetFileName(inputPath)}")
            .WithComment($"Format: {dds.Format}")
            .WithComment($"Size: {dds.Width}×{dds.Height}")
            .WithCopyright("© 2025 Game Studio"));
        
        // Save as JPEG 2000
        dds.SaveAsJ2K(outputPath, builder);
        
        Console.WriteLine($"Converted {inputPath} → {outputPath}");
        Console.WriteLine($"Size: {new FileInfo(inputPath).Length / 1024}KB → " +
                         $"{new FileInfo(outputPath).Length / 1024}KB");
    }
    
    public void BatchConvert(string sourceDir, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        
        foreach (var ddsFile in Directory.GetFiles(sourceDir, "*.dds"))
        {
            var outputFile = Path.Combine(
                outputDir,
                Path.GetFileNameWithoutExtension(ddsFile) + ".jp2");
            
            ConvertDdsToJp2(ddsFile, outputFile);
        }
    }
}
```

---

## Common Patterns Across All Libraries

### Pattern 1: Simple Encoding with Preset
```csharp
// SkiaSharp
bitmap.EncodeToJ2KWeb();

// ImageSharp
image.EncodeToJ2KWeb();

// Windows
bitmap.EncodeToJ2KWeb();

// Pfim
pfimImage.EncodeToJ2KWeb();
```

### Pattern 2: Custom Configuration
```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithMetadata(m => m
        .WithComment("My image")
        .WithCopyright("© 2025"));

// All libraries support the same pattern
byte[] data = image.EncodeToJ2K(config);
```

### Pattern 3: Save Directly to File
```csharp
// All libraries
image.SaveAsJ2KHighQuality("output.jp2", "© 2025");
```

### Pattern 4: Decode with Configuration
```csharp
var config = new J2KDecoderConfiguration()
    .WithResolution(2);

// SkiaSharp
var bitmap = SKBitmapJ2kExtensions.FromJ2KFile("image.jp2", config);

// ImageSharp
var image = ImageSharpJ2kExtensions.FromJ2KFile("image.jp2", config);

// Windows
var bitmap = BitmapJ2kExtensions.FromJ2KFile("image.jp2", config);

// Pfim
var pfimImage = PfimJ2kExtensions.FromJ2KFile("image.jp2", config);
```
---

## ASP.NET Core Integration

### Example: Image Upload and Processing Service

```csharp
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CoreJ2K.ImageSharp;
using CoreJ2K.Configuration;

[ApiController]
[Route("api/images")]
public class ImageController : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        using var stream = file.OpenReadStream();
        var image = await Image.LoadAsync<Rgba32>(stream);

        // Process image
        image.Mutate(x => x.Resize(1920, 1080));

        // Encode as JPEG 2000 with metadata
        var j2kData = image.EncodeToJ2K(
            new CompleteEncoderConfigurationBuilder()
                .ForWeb()
                .WithMetadata(m => m
                    .WithComment($"Uploaded: {DateTime.UtcNow}")
                    .WithComment($"Filename: {file.FileName}")
                    .WithCopyright("© 2025 Your Company")));

        // Save to storage
        var filename = $"{Guid.NewGuid()}.jp2";
        await System.IO.File.WriteAllBytesAsync($"uploads/{filename}", j2kData);

        return Ok(new { filename, size = j2kData.Length });
    }

    [HttpGet("{filename}")]
    public async Task<IActionResult> GetImage(string filename, [FromQuery] int? resolution)
    {
        var path = $"uploads/{filename}";
        if (!System.IO.File.Exists(path))
            return NotFound();

        // Decode with optional resolution reduction
        var config = resolution.HasValue
            ? new J2KDecoderConfiguration().WithResolution(resolution.Value)
            : null;

        var image = ImageSharpJ2kExtensions.FromJ2KFile(path, config);

        // Convert to PNG for browser
        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        ms.Position = 0;

        return File(ms.ToArray(), "image/png");
    }
}
```

---

## MAUI/Xamarin Integration (SkiaSharp)

### Example: Photo Gallery App

```csharp
using SkiaSharp;
using CoreJ2K.Skia;
using CoreJ2K.Configuration;

public class PhotoService
{
    public async Task<byte[]> SavePhotoAsync(Stream photoStream, string copyright)
    {
        // Load from stream
        var bitmap = SKBitmap.Decode(photoStream);

        // Resize for mobile
        var resized = bitmap.Resize(new SKImageInfo(1080, 1920), SKFilterQuality.High);

        // Encode with mobile-optimized settings
        return resized.EncodeToJ2K(
            new CompleteEncoderConfigurationBuilder()
                .ForBalanced()  // Good quality, reasonable size
                .WithTiles(t => t.SetSize(256, 256))  // Small tiles for mobile
                .WithMetadata(m => m
                    .WithComment($"Captured: {DateTime.Now}")
                    .WithCopyright(copyright)));
    }

    public async Task<SKBitmap> LoadThumbnailAsync(string path)
    {
        // Load at reduced resolution for thumbnail
        var config = new J2KDecoderConfiguration()
            .WithResolution(2);  // 1/4 size

        return SKBitmapJ2kExtensions.FromJ2KFile(path, config);
    }

    public async Task<SKBitmap> LoadFullImageAsync(string path)
    {
        // Load at full resolution
        return SKBitmapJ2kExtensions.FromJ2KFile(path);
    }
}
```

---

## Benefits of Integration Extensions

### 1. Type Safety
```csharp
// Direct type conversion - no casting needed
SKBitmap bitmap = SKBitmapJ2kExtensions.FromJ2KFile("image.jp2");
```

### 2. IntelliSense Support
```csharp
// All extension methods appear in IntelliSense
bitmap.EncodeToJ2K...  // Shows all available methods
```

### 3. Consistent API
```csharp
// Same pattern across all libraries
image.EncodeToJ2KWeb("© 2025");  // Works with SkiaSharp, ImageSharp, Windows
```

### 4. Less Code
```csharp
// Before (without extensions)
var config = new CompleteEncoderConfigurationBuilder().ForWeb().Build();
var data = J2kImage.ToBytes(bitmap, config);

// After (with extensions)
var data = bitmap.EncodeToJ2KWeb();
```

### 5. Clear Intent
```csharp
bitmap.SaveAsJ2KLossless("medical.jp2");  // Crystal clear what this does
```

---

## Migration from Traditional API

### Before (Traditional)
```csharp
// Old way
var params = new ParameterList();
params["lossless"] = "on";
params["file_format"] = "on";
byte[] data = J2kImage.ToBytes(bitmap, params);
```

### After (Modern with Extensions)
```csharp
// New way - much simpler!
byte[] data = bitmap.EncodeToJ2KLossless();
```

---

## Summary

All CoreJ2K integration packages now support the modern configuration API through convenient extension methods:

✅ **CoreJ2K.Skia** - SkiaSharp integration (cross-platform)  
✅ **CoreJ2K.ImageSharp** - ImageSharp integration (modern .NET)  
✅ **CoreJ2K.Windows** - System.Drawing integration (Windows)  
✅ **CoreJ2K.Pfim** - Pfim integration (DDS/TGA game textures)  

**Features across all packages:**

✅ **Type-safe** - No casting, full IntelliSense  
✅ **Consistent** - Same pattern across all libraries  
✅ **Concise** - One-liner encoding/decoding  
✅ **Powerful** - Full access to modern configuration  
✅ **Flexible** - Use presets or custom configurations  

**The easiest way to work with JPEG 2000 in .NET!** 🚀

---

## See Also

- [Complete Builder Guide](COMPLETE_BUILDER_GUIDE.md) - Full modern API reference
- [Quick Reference](QUICK_REFERENCE.md) - Fast lookup guide
- [README](../README.md) - Main documentation
