```
▄█████  ▄▄▄  ▄▄▄▄  ▄▄▄▄▄    ██ ████▄ ██ ▄█▀ 
██     ██▀██ ██▄█▄ ██▄▄     ██  ▄██▀ ████   
▀█████ ▀███▀ ██ ██ ██▄▄▄ ████▀ ███▄▄ ██ ▀█▄ 
A Managed and Portable JPEG2000 Codec for .NET Platforms
```

****

[![NuGet](https://img.shields.io/nuget/v/CoreJ2K.svg?label=CoreJ2K&logo=nuget)](https://www.nuget.org/packages/CoreJ2K/)
[![Downloads](https://img.shields.io/nuget/dt/CoreJ2K?label=Downloads&logo=nuget)](https://www.nuget.org/packages/CoreJ2K/)
[![License](https://img.shields.io/badge/License-BSD%203--Clause-blue.svg)](http://www.opensource.org/licenses/bsd-license.php)
[![Socket Badge](https://badge.socket.dev/nuget/package/corej2k)](https://badge.socket.dev/nuget/package/corej2k)  
[![Release Build](https://github.com/cinderblocks/CoreJ2K/actions/workflows/ci-and-release.yml/badge.svg)](https://github.com/cinderblocks/CoreJ2K/actions/workflows/ci-and-release.yml)
[![CodeQL Security Analysis](https://github.com/cinderblocks/CoreJ2K/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/cinderblocks/CoreJ2K/actions/workflows/codeql-analysis.yml)
[![Fuzzing](https://github.com/cinderblocks/CoreJ2K/actions/workflows/fuzzing.yml/badge.svg)](https://github.com/cinderblocks/CoreJ2K/actions/workflows/fuzzing.yml)

---

## 🚀 Quick Start

```bash
dotnet add package CoreJ2K
dotnet add package CoreJ2K.Skia
```

### Simple API

```csharp
using CoreJ2K;
using SkiaSharp;

// Decode – legacy path (returns InterleavedImage, useful for sample-level access)
using var image = J2kImage.FromStream(File.OpenRead("image.jp2"));
var bitmap = image.As<SKBitmap>();

// Decode – fast path (8-bit images, skips the intermediate int[] buffer: ~4× less memory)
var bitmap2 = J2kImage.DecodeToImage<SKBitmap>(File.OpenRead("image.jp2"));

// Encode with modern API (recommended)
byte[] data = CompleteConfigurationPresets.Web
    .WithCopyright("© 2025")
    .Encode(imageSource);

// Or use traditional API
byte[] j2kData = J2kImage.ToBytes(bitmap);
```

**[📖 Full Documentation](#documentation) • [💻 More Examples](#quick-examples) • [🎯 Modern API Guide](#modern-configuration-api) • [📦 All Packages](#installation)**

---

## 📑 Table of Contents

- **[About](#about)**
  - [What is CoreJ2K?](#what-is-corej2k)
  - [Key Features](#key-features)
  - [Why Choose CoreJ2K?](#why-choose-corej2k)
- **[Getting Started](#getting-started)**
  - [Installation](#installation)
  - [Quick Examples](#quick-examples)
  - [Platform Support](#platform-support)
- **[Documentation](#documentation)**
  - [Encoding Guide](#encoding-guide)
  - [Advanced Parameters](#advanced-parameters)
  - [Usage Notes](#usage-notes)
- **[Standards & Compliance](#standards--compliance)**
  - [JPEG 2000 Part 1 (100%)](#part-1-core-coding-system)
  - [JPEG 2000 Part 2 (~50%)](#part-2-extensions)
  - [Library Comparison](#library-comparison)
  - [Full Specification Details](#jpeg-2000-specification-compliance)
- **[Support](#support)**
- **[Contributing](#contributing)**
- **[License](#license)

---

## About

### What is CoreJ2K?

CoreJ2K is a **pure C# implementation** of the JPEG 2000 image compression standard for .NET. Modern fork of CSJ2K (C# port of jj2000), designed for .NET Standard and modern .NET platforms. Provides both **encoding and decoding** with **100% ISO/IEC 15444-1 Part 1 compliance**.

### Key Features

| Feature | Description |
|---------|-------------|
| **🏆 Standards Compliant** | 100% JPEG 2000 Part 1 (ISO/IEC 15444-1) • ~50% Part 2 extensions • 27 codestream markers • 22 JP2 boxes • Part 14 JPXML |
| **⚡ Modern .NET** | .NET Standard 2.0/2.1 • .NET 8/9/10 • .NET Framework 4.8.1 (via netstandard2.0) • All platforms |
| **🎯 Production Ready** | Lossless/Lossy • ROI • Files >4GB • Error resilience • 1,000+ tests |
| **📦 Easy Integration** | NuGet packages • Simple API • SkiaSharp/ImageSharp/System.Drawing support |
| **🆓 Open Source** | BSD-3-Clause • No fees • Active development • Community driven |

### Why Choose CoreJ2K?

| ✅ For .NET Developers | ✅ For Production Use |
|------------------------|----------------------|
| Native C# (no P/Invoke) | Battle-tested & stable |
| Memory safe (managed code) | Complete Part 1 compliance |
| Safe for concurrent independent calls | Medical imaging (DICOM) ready |
| Familiar NuGet install | GIS/geospatial compatible |
| Works with all image libraries | Interoperable with all decoders |

**Comparison:** CoreJ2K is the **only open-source .NET library** with full JPEG 2000 Part 1 compliance and complete pointer marker read/write support.

[↑ Back to top](#corej2k)

---

## Getting Started

### Installation

#### Core Library
```bash
dotnet add package CoreJ2K
```

#### With Image Integration
```bash
# SkiaSharp (recommended - cross-platform)
dotnet add package CoreJ2K.Skia

# ImageSharp (modern .NET)
dotnet add package CoreJ2K.ImageSharp

# System.Drawing (Windows)
dotnet add package CoreJ2K.Windows

# Pfim (DDS/TGA)
dotnet add package CoreJ2K.Pfim
```

Integration packages are discovered and registered automatically at runtime on JIT
runtimes (.NET Framework and .NET 8+). NativeAOT or aggressively trimmed apps should
register creators explicitly, e.g. `ImageFactory.Register(new SKBitmapImageCreator())` —
see [Plugin Registration](docs/INTEGRATION_PACKAGES_GUIDE.md#plugin-registration).

### Quick Examples

#### Basic Decoding
```csharp
using CoreJ2K;
using SkiaSharp;

// From file
var image = J2kImage.FromStream(File.OpenRead("image.jp2"));
var bitmap = image.As<SKBitmap>();

// From bytes
byte[] data = File.ReadAllBytes("image.j2k");
var image2 = J2kImage.FromBytes(data);

// Save as PNG
using var output = File.OpenWrite("output.png");
bitmap.Encode(output, SKEncodedImageFormat.Png, 90);
```

#### Fast Decode (8-bit, ~4× less peak memory)

For the common 8-bit decode-and-display path, `DecodeToImage<T>` writes pixels **directly into the backend image**, skipping the intermediate `int[]` buffer that `InterleavedImage` would normally allocate. Use this whenever you don't need sample-level access after decoding.

```csharp
using CoreJ2K;
using SkiaSharp;

// From stream – fast path (8-bit components: ~1 B/sample peak vs ~5 B/sample legacy)
SKBitmap bitmap = J2kImage.DecodeToImage<SKBitmap>(File.OpenRead("image.jp2"));

// From byte array
SKBitmap bitmap2 = J2kImage.DecodeToImage<SKBitmap>(File.ReadAllBytes("image.j2k"));

// From file path
SKBitmap bitmap3 = J2kImage.DecodeFileToImage<SKBitmap>("image.jp2");
```

> **When to use the legacy `FromStream` path instead:**  
> - You need to inspect or modify individual samples (`GetSample`, `SetComponent`, `Crop`, …).  
> - Any component has bit depth > 8 (the fast path falls back automatically, but you may also call `FromStream` directly).

| API | Peak memory (8-bit RGB) | Best for |
|-----|------------------------|----------|
| `DecodeToImage<T>` | ~1 B/sample | Display / encode pipeline |
| `FromStream().As<T>()` | ~5 B/sample | Sample inspection / editing |

#### Tile Access with TLM Markers

If a codestream has a TLM (tile-part lengths) marker, the decoder uses it to locate each tile without reading the packets of the tiles
before it. Nothing needs to be enabled for decoding; it applies to streams from CoreJ2K and from other encoders. To write one, see
[Encoding with TLM and PLT markers](#encoding-with-tlm-and-plt-markers).

### Basic Encoding
```csharp
// Default (high quality)
byte[] j2k = J2kImage.ToBytes(bitmap);

// Lossless
var lossless = new ParameterList { ["lossless"] = "on", ["file_format"] = "on" };
byte[] data = J2kImage.ToBytes(bitmap, lossless);

// Lossy with target bitrate
var lossy = new ParameterList { ["rate"] = "0.5", ["file_format"] = "on" };
byte[] compressed = J2kImage.ToBytes(bitmap, lossy);
```

### Platform Support

| Platform | Framework | Package |
|----------|-----------|---------|
| **Windows** | .NET 8/9, Framework 4.8.1, Standard 2.x | CoreJ2K, CoreJ2K.Windows |
| **Linux** | .NET 8/9, Standard 2.x | CoreJ2K, CoreJ2K.Skia |
| **macOS** | .NET 8/9, Standard 2.x | CoreJ2K, CoreJ2K.Skia |
| **Mobile** | .NET 8/9 (MAUI), Xamarin | CoreJ2K, CoreJ2K.Skia |
| **Web** | .NET 8/9, ASP.NET Core | CoreJ2K, CoreJ2K.ImageSharp |

[↑ Back to top](#corej2k)

---

## Documentation

### Modern Configuration API

**CoreJ2K 1.3.0+ includes a comprehensive, fluent configuration API** that makes JPEG 2000 encoding easier and more intuitive than ever.

#### Quick Start with Presets

```csharp
using CoreJ2K.Configuration;

// One-liner encoding with preset
byte[] webImage = CompleteConfigurationPresets.Web
    .WithCopyright("© 2025")
    .Encode(imageSource);

// Medical imaging (lossless)
byte[] medical = CompleteConfigurationPresets.Medical
    .WithMetadata(m => m
        .WithComment("Patient: Anonymous")
        .WithComment("Study: CT Scan"))
    .Encode(dicomImage);

// High-quality photography
byte[] photo = CompleteConfigurationPresets.Photography
    .WithCopyright("© 2025 Photographer")
    .WithComment("Sunset over mountains")
    .Encode(photograph);
```

#### Quality Presets

```csharp
// Lossless (perfect reconstruction)
var config = new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .Build();

// High quality (90% quality)
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .Build();

// Balanced (75% quality)
var config = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .Build();

// High compression
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighCompression()
    .Build();
```

#### Use Case Presets

```csharp
// Medical imaging - lossless with appropriate settings
.ForMedical()

// Archival storage - very high quality + error resilience
.ForArchival()

// Web delivery - progressive + balanced quality
.ForWeb()

// Thumbnail generation - fast + small
.ForThumbnail()

// Geospatial/GIS - spatial browsing + tiled
.ForGeospatial()

// Streaming - quality progressive
.ForStreaming()
```

#### Fine-Grained Control

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .WithQuality(0.85)
    .WithQuantization(q => q
        .UseExpounded()
        .WithBaseStepSize(0.008f)
        .WithGuardBits(2))
    .WithWavelet(w => w
        .UseIrreversible_9_7()
        .WithDecompositionLevels(6))
    .WithProgression(p => p.UseLRCP())
    .WithTiles(t => t.SetSize(512, 512))
    .WithMetadata(m => m
        .WithComment("Custom configuration")
        .WithCopyright("© 2025")
        .WithXml(customMetadata))
    .Build();

byte[] data = J2kImage.ToBytes(image, config);
```

**📚 Complete Guides:**
- [Complete Builder Guide](docs/COMPLETE_BUILDER_GUIDE.md) - Unified API with presets
- [Encoder Configuration Guide](docs/ENCODER_CONFIGURATION_GUIDE.md) - Encoding parameters
- [Quantization Configuration Guide](docs/QUANTIZATION_CONFIGURATION_GUIDE.md) - Quality/size trade-offs
- [Wavelet Configuration Guide](docs/WAVELET_CONFIGURATION_GUIDE.md) - Transform settings
- [Progression Configuration Guide](docs/PROGRESSION_CONFIGURATION_GUIDE.md) - Data organization
- [Metadata Configuration Guide](docs/METADATA_CONFIGURATION_GUIDE.md) - Comments, copyright, XML
- [Part 2 Transforms Guide](docs/PART2_TRANSFORMS_GUIDE.md) - DCO, NLT, MCT (JPX codestream extensions)
- [Part 14 JPXML Implementation](docs/PART14_JPXML_IMPLEMENTATION.md) - XML metadata representation (ISO/IEC 15444-14)

### Traditional API (Encoding Guide)

#### Lossless
```csharp
var p = new ParameterList();
p["lossless"] = "on";
p["file_format"] = "on";
byte[] data = J2kImage.ToBytes(bitmap, p);
```

#### Lossy with Quality
```csharp
var p = new ParameterList();
p["rate"] = "0.5";         // 0.5 bits/pixel
p["file_format"] = "on";   // JP2 wrapper
p["tiles"] = "1024 1024";  // Tile size
p["Wlev"] = "5";           // Decomposition levels
byte[] data = J2kImage.ToBytes(bitmap, p);
```

#### Progressive Quality Layers
```csharp
var p = new ParameterList();
p["rate"] = "0.2";
p["Alayers"] = "0.015 +20 2.0 +10";  // Multiple layers
p["Aptype"] = "res";                  // Resolution progression
byte[] data = J2kImage.ToBytes(bitmap, p);
```

### Advanced Parameters

Common encoder parameters (case-sensitive):

| Parameter | Example Value | Description |
|-----------|--------------|-------------|
| `lossless` | `"on"` / `"off"` | Enable lossless compression |
| `rate` | `"0.5"` | Target bitrate (bits per pixel) |
| `file_format` | `"on"` / `"off"` | Use JP2 container format |
| `tiles` | `"1024 1024"` | Tile dimensions (width height) |
| `Wlev` | `"5"` | Wavelet decomposition levels |
| `Wcboff` | `"0 0"` | Code-block partition origin |
| `Ffilters` | `"w5x3"` / `"w9x7"` | Wavelet filter (reversible/irreversible) |
| `Qtype` | `"reversible"` / `"expounded"` | Quantization type |
| `Aptype` | `"res"` / `"layer"` / `"pos-comp"` | Progression order |
| `Alayers` | `"0.015 +20 2.0"` | Layer rate specification |
| `Mct` | `"on"` / `"off"` | Multi-component transform |
| `Psop` | `"on"` / `"off"` | SOP markers (error resilience) |
| `Peph` | `"on"` / `"off"` | EPH markers (error resilience) |

**Note:** Parameter names match internal encoder conventions (no leading dashes).

### Usage Notes

- **InterleavedImage**: Cross-platform image wrapper. Use `As<T>()` to convert to platform types (e.g., `SKBitmap`). Carries a full `int[]` sample buffer — use `DecodeToImage<T>` instead when you only need the final bitmap.
- **DecodeToImage\<T\>**: Memory-efficient decode that skips `InterleavedImage` entirely for 8-bit images (~4× less peak memory). Falls back to `FromStream + As<T>` automatically for >8-bit components.
- **ParameterList**: Optional encoding parameters. Use indexer to set: `params["key"] = "value"`
- **Image Sources**: Accepts SKBitmap, Bitmap, Image, or codec-specific formats (PGM/PPM/PGX streams)
- **Thread Safety**: Independent decode and encode calls can run concurrently on separate threads.
- **Parallel Decoding**: Code-block decoding and the inverse wavelet transform use up to all cores by default (about 4x faster on 8 cores for lossless, 3x for lossy), with bit-identical output. Tune with `WithMaxDegreeOfParallelism(n)` or `J2kImage.DefaultMaxDegreeOfParallelism`. Encoding is parallel too (code-block coding, the forward wavelet transform, rate allocation and packet writing): about 4x faster on 8 cores, again with identical output.
- **Cancellation**: Decodes and encodes observe a `CancellationToken` and stop within milliseconds with `OperationCanceledException`, including the `*Async` methods. Pass a token to `FromBytes`/`DecodeBytes`/`DecodeToImage<T>`/`ToBytes`/`WriteTo`, or set it with `J2KDecoderConfiguration.WithCancellationToken` / `J2KEncoderConfiguration.WithCancellationToken`.
- **Decode Limits**: A decode is rejected with `DecoderLimitException` before any image-sized allocation if it would exceed the default 1 Gpixel / 2 GiB limits (at the requested resolution). Use `DecoderLimits.Strict` for untrusted input or `DecoderLimits.None` to opt out. See the [decoder guide](docs/DECODER_CONFIGURATION_GUIDE.md#8-resource-limits).

### Upgrading to 2.4.0

Two defaults changed. Each is one line to revert.

- **Decode limits are on by default.** A decode whose estimated memory exceeds 2 GiB (or 1 Gpixel) at the requested resolution now throws
  `DecoderLimitException` before allocating anything. A 16384x16384 RGB image (estimated about 3.2 GB) used to be attempted and is now
  rejected. If you decode very large trusted images, such as GIS or whole-slide data, opt out once at start-up with
  `DecoderLimits.Default = DecoderLimits.None;`, or raise the limit for specific calls with `.WithLimits(...)`. Reduced-resolution decodes
  are measured at the reduced size, so previews of huge images are unaffected.
- **Decoding and encoding are parallel by default.** Each decode uses up to all processors for code-block decoding and the inverse wavelet
  transform, and each encode uses up to all processors for code-block coding and the forward wavelet transform. Output is bit-identical
  to a single-threaded run. If your application already decodes or encodes many images concurrently (a server, for example), set
  `J2kImage.DefaultMaxDegreeOfParallelism = 1;` once at start-up to opt out of both, or pass `WithMaxDegreeOfParallelism(1)` to a
  single call.

Streams written with PLT (`Hplt`) or TLM (`Htlm`) markers are different from 2.3.x: tile-part lengths now include the PLT marker, PLT
markers list every packet in the order it is written, and `Htlm` now writes a TLM marker. Streams written without those options are
byte-identical to 2.3.x. See the [changelog](CHANGELOG.md) for everything in this release.

### Encoding with TLM and PLT Markers

TLM (tile-part lengths, in the main header) and PLT (packet lengths, in each tile-part header) markers let a reader find a tile, or a
packet within it, without parsing everything before it. They are off by default. Turn them on with encoder parameters:

```csharp
var pl = new ParameterList
{
    ["tiles"] = "512 512",
    ["Htlm"] = "on",   // TLM marker in the main header
    ["Hplt"] = "on",   // PLT markers in the tile-part headers
};
byte[] data = J2kImage.ToBytes(image, pl);
```

- The decoder reads both and uses TLM to find tiles. Other decoders can use them too; OpenJPEG reads CoreJ2K's streams with both.
- TLM lists one tile-part per tile, with 32-bit lengths. The marker counts against a rate target. PLT markers do not, so a rate-limited
  stream written with `Hplt` comes out a little over its target.
- `Htlm` is ignored, with a warning, together with `tile_parts` or packed packet headers (`pph_main`, `pph_tile`), which rewrite the
  tile-parts after they are written.
- `Hppm` and `Hppt` are accepted but have no effect; use `pph_main` and `pph_tile`.

[↑ Back to top](#corej2k)

---

## Standards & Compliance

### Part 1: Core Coding System

**CoreJ2K achieves 100% JPEG 2000 Part 1 (ISO/IEC 15444-1) compliance:**

| Component | Support | Details |
|-----------|---------|---------|
| **Codestream Markers** | ✅ 27/27 (100%) | All main header, tile-part, and packet markers |
| **JP2 File Format** | ✅ 22/22 boxes (100%) | All required and optional boxes |
| **Wavelet Transforms** | ✅ Complete | 5-3 (reversible), 9-7 (irreversible) |
| **Quantization** | ✅ Complete | Reversible, scalar derived, scalar expounded |
| **Entropy Coding** | ✅ Complete | Full MQ coder (arithmetic coding) |
| **ROI Encoding** | ✅ Complete | Max-shift method, arbitrary shapes |
| **Progression Orders** | ✅ All 5 | LRCP, RLCP, RPCL, PCRL, CPRL |
| **Error Resilience** | ✅ Complete | SOP/EPH markers, segmentation symbols |
| **Pointer Markers** | ✅ | PPM and PPT (`pph_main`, `pph_tile`), PLT (`Hplt`) and TLM (`Htlm`) read and written; PLM read only |
| **TLM Tile Access** | ✅ | Tiles located from TLM tile-part lengths |
| **Extended Length** | ✅ Complete | XLBox support for files >4GB |
| **ICC Profiles** | ✅ Complete | Full color management support |
| **Metadata** | ✅ Complete | XML, UUID, resolution, channels, Part 14 JPXML |

**Compliance Scores:**
- Baseline Profile: ✅ 100%
- Profile 0: ✅ 100%
- Profile 1: ✅ 100%
- Extended Features: ✅ 100%
- Part 14 JPXML: ✅ 100%

### Part 2: Extensions

CoreJ2K implements the most commonly used JPEG 2000 Part 2 (ISO/IEC 15444-2) features end-to-end (encode and decode):

| Feature | Markers | Read | Write | Notes |
|---------|---------|------|-------|-------|
| **JPX File Format** | ASOC, NLST, DTBL, FTBL, FLST, CREF, JPCH, JPLH | ✅ | ✅ | Full JPX box set + `jpx ` brand |
| **Extended Capabilities** | CAP (0xFF50) | ✅ | ✅ | Auto-emitted for any Part 2 codestream |
| **Variable DC Offset** | DCO (0xFF70) | ✅ | ✅ | Per-component integer DC shifts |
| **Non-linearity Transform** | NLT (0xFF76) | ✅ | ✅ | Gamma (power-law) and LUT types |
| **Multi-Component Transform** | MCT/MCC/MCO (0xFF74–77) | ✅ | ✅ | Matrix decorrelation, dependency lifting, 5/3 wavelet |
| **Component Bit Depth** | CBD (0xFF78) | ✅ | ✅ | Per-component depth signaling |
| **Arbitrary Decomposition** | DFS/ADS (0xFF72–73) | ❌ | ❌ | Non-standard wavelet tree shapes |
| **Arbitrary Transform Kernels** | ATK (0xFF79) | ✅ | ✅ | Custom reversible/irreversible lifting kernels — see [ATK guide](docs/PART2_ATK_IMPLEMENTATION.md) |
| **Trellis Coded Quantization** | — | ❌ | ❌ | Alternative entropy path |
| **Single Sample Overlap** | — | ❌ | ❌ | Tie-in with DFS/ADS |

**Coverage: ~50%** — all production-relevant per-sample and multi-component transforms, plus custom wavelet kernels (ATK), are implemented. Remaining gaps are the wavelet-shape features (DFS/ADS), trellis-coded quantization and single-sample overlap.

> **API note:** Pass Part 2 parameters to `J2kImage.ToBytes`:
> ```csharp
> // DCO: per-component DC offset
> var dco = new DCOMarkerSegment { Offsets = new[] { 10, -3, 20 } };
>
> // NLT: gamma non-linearity
> var nlt = new NLTMarkerSegment { Type = NLTType.Gamma, GammaExponent = 2.2 };
>
> // Encode with Part 2 extensions (Rsiz + CAP written automatically)
> byte[] data = J2kImage.ToBytes(src, metadata, pl, nltSegments: new[] { nlt }, dcoSegment: dco);
> ```

### Library Comparison

Quick comparison with major JPEG 2000 libraries:

| Feature | CoreJ2K | Kakadu | OpenJPEG | JJ2000 | LEADTOOLS |
|---------|---------|---------|----------|---------|-----------|
| **Part 1 Compliance** | ✅ 100% | ✅ 100% | ⚠️ ~95% | ⚠️ ~90% | ✅ 100% |
| **Part 2 Coverage** | ⚠️ ~50% | ✅ ~95% | ⚠️ ~20% | ❌ 0% | ✅ ~80% |
| **Language** | C# | C++ | C | Java | C/C++ |
| **License** | BSD (Free) | Commercial | BSD (Free) | JJ2000 | Commercial |
| **Cost** | ✅ Free | ❌ $$$$ | ✅ Free | ✅ Free | ❌ $$$$ |
| **.NET Native** | ✅ | ❌ | ⚠️ P/Invoke | ❌ | ✅ |
| **Memory Safety** | ✅ Managed | ⚠️ Manual | ⚠️ Manual | ✅ Managed | ⚠️ Manual |
| **Pointer Markers** | ✅ Full R/W | ✅ Full R/W | ⚠️ Read only | ⚠️ Read only | ✅ Full R/W |
| **Files >4GB** | ✅ Yes | ✅ Yes | ⚠️ Limited | ❌ No | ✅ Yes |
| **Part 14 JPXML** | ✅ Full R/W | ⚠️ Partial | ❌ No | ❌ No | ❌ No |
| **Active Dev** | ✅ 2026 | ✅ 2025 | ✅ 2025 | ❌ 2010 | ✅ 2025 |

**CoreJ2K Unique Advantages:**
- Only open-source .NET library with full Part 1 compliance
- Only open-source library with complete pointer marker read/write
- Memory-safe managed code (no buffer overflows)
- Zero licensing costs

**[📊 View detailed comparison tables →](#appendix-detailed-comparison-tables)**

### JPEG 2000 Specification Compliance

CoreJ2K supports multiple parts of ISO/IEC 15444:

| Part | Name | Read | Write | Status |
|------|------|------|-------|--------|
| **Part 1** | Core Coding System | ✅ Full | ✅ Full | **100%** Complete |
| **Part 2** | Extensions | ⚠️ Partial | ⚠️ Partial | **~50%** — JPX boxes, DCO, NLT, MCT family, ATK; DFS/ADS pending |
| **Part 4** | Conformance Testing | N/A | N/A | **100%** Complete |
| **Part 14** | XML Representation (JPXML) | ✅ Full | ✅ Full | **100%** Complete |

**Part 1 covers 99% of real-world JPEG 2000 usage** including:
- ✅ Medical imaging (DICOM)
- ✅ Digital cinema (DCP)
- ✅ Geospatial/satellite imagery
- ✅ Digital archives and libraries
- ✅ High-quality image storage

**[📖 View complete specification details →](#jpeg-2000-specification-compliance)**

[↑ Back to top](#corej2k)

---

## Support

### Resources

- 📚 **Documentation**: This README and inline code docs
- 💬 **Discussions**: [GitHub Discussions](https://github.com/cinderblocks/CoreJ2K/discussions)
- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/cinderblocks/CoreJ2K/issues)
- 📦 **Packages**: [NuGet Gallery](https://www.nuget.org/packages/CoreJ2K/)

### External Links

- [JPEG 2000 Implementation Guide](http://www.jpeg.org/jpeg2000guide/guide/contents.html)
- [ISO/IEC 15444-1 Standard](https://www.iso.org/standard/37674.html)
- [SkiaSharp GitHub](https://github.com/mono/SkiaSharp)
- [ImageSharp GitHub](https://github.com/SixLabors/ImageSharp)
- [OpenJPEG GitHub](https://github.com/uclouvain/openjpeg)

### Support the Project

If CoreJ2K helps your project:
- ⭐ **Star the repository** on GitHub
- 📢 **Share** with others
- 💰 **Donate** via cryptocurrency:
  - [![ZEC](https://img.shields.io/keybase/zec/cinder?label=Zcash)](https://keybase.io/cinder)
  - [![BTC](https://img.shields.io/keybase/btc/cinder?label=Bitcoin)](https://keybase.io/cinder)

[↑ Back to top](#corej2k)

---

## Contributing

Contributions welcome! Ways to help:

- 🐛 **Report bugs** via [GitHub Issues](https://github.com/cinderblocks/CoreJ2K/issues)
- 💡 **Suggest features** in [Discussions](https://github.com/cinderblocks/CoreJ2K/discussions)
- 📝 **Improve docs** with pull requests
- 🧪 **Add tests** for better coverage
- 🔧 **Fix issues** and submit PRs

[↑ Back to top](#corej2k)

---

## License

**BSD 3-Clause License**

```
Copyright (c) 1999-2000 JJ2000 Partners
Copyright (c) 2007-2012 Jason S. Clary
Copyright (c) 2013-2016 Anders Gustafsson, Cureos AB
Copyright (c) 2024-2026 Sjofn LLC
```

Free to use in commercial and open-source projects. No licensing fees.

**[→ Full license text](http://www.opensource.org/licenses/bsd-license.php)**

---

<div align="center">

**Made with ❤️ for the .NET community**

[![GitHub stars](https://img.shields.io/github/stars/cinderblocks/CoreJ2K?style=social)](https://github.com/cinderblocks/CoreJ2K)
[![GitHub issues](https://img.shields.io/github/issues/cinderblocks/CoreJ2K?logo=github)](https://github.com/cinderblocks/CoreJ2K/issues)
[![Commit Activity](https://img.shields.io/github/commit-activity/m/cinderblocks/CoreJ2K?logo=github)](https://github.com/cinderblocks/CoreJ2K)

[↑ Back to top](#corej2k)

</div>

---

## Appendix: Detailed Comparison Tables

<details>
<summary><b>Click to expand full library comparison</b></summary>

### Standards Compliance and Features

| Feature | CoreJ2K | Kakadu | OpenJPEG | JJ2000 | JasPer | Pillow | LEADTOOLS |
|---------|---------|---------|----------|---------|---------|---------|-----------|
| **Part 1 Compliance** | ✅ 100% | ✅ 100% | ⚠️ ~95% | ⚠️ ~90% | ⚠️ ~85% | ⚠️ ~70% | ✅ 100% |
| **Part 2 Coverage** | ⚠️ ~50% | ✅ ~95% | ⚠️ ~20% | ❌ 0% | ❌ 0% | ❌ 0% | ✅ ~80% |
| **Decode** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Basic | ✅ Full |
| **Encode** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ❌ No | ✅ Full |
| **JP2 File Format** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ⚠️ Partial | ⚠️ Basic | ✅ Full |
| **Lossless** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **Lossy** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **Error Resilience (SOP/EPH)** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ⚠️ Partial | ❌ No | ✅ Full |
| **Pointer Markers (PPM/PPT/PLM/PLT/TLM)** | ✅ Full R/W | ✅ Full R/W | ⚠️ Read only | ⚠️ Read only | ❌ No | ❌ No | ✅ Full R/W |
| **ROI (Region of Interest)** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ⚠️ Limited | ❌ No | ✅ Yes |
| **Extended Length (XLBox)** | ✅ Full | ✅ Full | ⚠️ Limited | ❌ No | ❌ No | ❌ No | ✅ Full |
| **Multi-component Transform** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ⚠️ Limited | ⚠️ Limited | ✅ Yes |
| **Tile-based Processing** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ⚠️ Limited | ✅ Yes |
| **Progression Orders** | ✅ All 5 | ✅ All 5 | ✅ All 5 | ✅ All 5 | ⚠️ Limited | ⚠️ Limited | ✅ All 5 |
| **Quality Layers** | ✅ Unlimited | ✅ Unlimited | ✅ Unlimited | ✅ Unlimited | ⚠️ Limited | ⚠️ Limited | ✅ Unlimited |
| **ICC Profile Support** | ✅ Full | ✅ Full | ✅ Full | ⚠️ Basic | ⚠️ Basic | ✅ Full | ✅ Full |
| **Metadata (XML/UUID)** | ✅ Full | ✅ Full | ⚠️ Partial | ⚠️ Partial | ❌ No | ⚠️ Basic | ✅ Full |
| **Part 14 JPXML (XML metadata representation)** | ✅ Full R/W | ⚠️ Partial | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No |
| **Palette/Component Mapping** | ✅ Full | ✅ Full | ✅ Full | ⚠️ Partial | ⚠️ Partial | ❌ No | ✅ Full |

### Technical Characteristics

| Aspect | CoreJ2K | Kakadu | OpenJPEG | JJ2000 | JasPer | Pillow | LEADTOOLS |
|--------|---------|---------|----------|---------|---------|---------|-----------|
| **Language** | C# | C++ | C | Java | C | Python/C | C/C++ |
| **License** | BSD (Open) | Commercial | BSD (Open) | JJ2000 (Open) | MIT (Open) | PIL (Open) | Commercial |
| **Cost** | ✅ Free | ❌ $$$$ | ✅ Free | ✅ Free | ✅ Free | ✅ Free | ❌ $$$$ |
| **Active Development** | ✅ Active | ✅ Active | ✅ Active | ❌ Abandoned | ⚠️ Minimal | ✅ Active | ✅ Active |
| **Last Update** | 2026 | 2025 | 2025 | 2010 | 2022 | 2025 | 2025 |
| **Platform Support** | .NET all | All | All | JVM | All | All | Windows mainly |
| **Cross-platform** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ⚠️ Limited |
| **Memory Safety** | ✅ Managed | ⚠️ Manual | ⚠️ Manual | ✅ Managed | ⚠️ Manual | ✅ Managed | ⚠️ Manual |
| **Multi-threading** | ✅ Parallel decode and encode | ✅ Yes | ✅ Yes | ⚠️ Limited | ⚠️ Limited | ✅ Yes | ✅ Yes |
| **SIMD Optimization** | ✅ Yes (AVX/auto-vec) | ✅ Full | ✅ Full | ❌ No | ❌ No | ⚠️ Limited | ✅ Full |

### Performance and Quality

| Metric | CoreJ2K | Kakadu | OpenJPEG | JJ2000 | JasPer | Pillow | LEADTOOLS |
|--------|---------|---------|----------|---------|---------|---------|-----------|
| **Encoding Speed** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Decoding Speed** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Compression Ratio** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Image Quality** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Memory Efficiency** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Large File Support (>4GB)** | ✅ Yes | ✅ Yes | ⚠️ Limited | ❌ No | ❌ No | ❌ No | ✅ Yes |

### Developer Experience

| Aspect | CoreJ2K | Kakadu | OpenJPEG | JJ2000 | JasPer | Pillow | LEADTOOLS |
|--------|---------|---------|----------|---------|---------|---------|-----------|
| **Documentation** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **API Simplicity** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Code Examples** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **NuGet/Package Manager** | ✅ Yes | ❌ No | ✅ Yes | ⚠️ Maven | ❌ Manual | ✅ PyPI | ✅ Yes |
| **Community Support** | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Issue Tracking** | ✅ GitHub | ⚠️ Private | ✅ GitHub | ❌ Closed | ✅ GitHub | ✅ GitHub | ⚠️ Private |
| **Integration Ease** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |

### Use Case Suitability

| Use Case | CoreJ2K | Kakadu | OpenJPEG | JJ2000 | JasPer | Pillow | LEADTOOLS |
|----------|---------|---------|----------|---------|---------|---------|-----------|
| **.NET Applications** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐ | ⭐⭐ | ⭐ | ⭐⭐⭐⭐⭐ |
| **Cross-platform Apps** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Web Services** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Mobile Apps** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |
| **Medical Imaging** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Geospatial/GIS** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Archive/Digital Libraries** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐
