# Wavelet Configuration API Guide

## Overview

`WaveletConfigurationBuilder` configures the discrete wavelet transform used during
encoding: which filter pair to use and how many decomposition levels to apply.
It is used standalone or as part of `CompleteEncoderConfigurationBuilder`.

## Quick Start

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .WithWavelet(w => w.UseReversible_5_3().WithDecompositionLevels(5))
    .WithEncoder(e => e.WithFileFormat(true))
    .Build();
```

## Filter Selection

JPEG 2000 supports two filter pairs.

| Filter | Method | Use case |
|--------|--------|----------|
| 5/3 (Le Gall) | `UseReversible_5_3()` / `UseReversible()` | Lossless — only reversible filter supports perfect reconstruction |
| 9/7 (Daubechies) | `UseIrreversible_9_7()` / `UseIrreversible()` | Lossy — better compression and visual quality |

```csharp
// Lossless — must use 5/3
var lossless = new WaveletConfigurationBuilder().UseReversible_5_3();

// Lossy — 9/7 preferred
var lossy = new WaveletConfigurationBuilder().UseIrreversible_9_7();
```

## Decomposition Levels

More levels decompose the image into finer frequency bands, improving compression
at the cost of more computation. The default is 5; valid range is 1–32.

```csharp
var w = new WaveletConfigurationBuilder()
    .UseIrreversible_9_7()
    .WithDecompositionLevels(6); // one extra level for better quality
```

Practical guidance:
- **Thumbnails / fast encode**: 2–3 levels
- **General purpose**: 5 levels (default)
- **Archival / high quality**: 6 levels
- **Very large images**: 6–8 levels

## Per-Component Filters

Different components can use different filters (e.g., luma reversible, chroma
irreversible):

```csharp
var w = new WaveletConfigurationBuilder()
    .UseReversible_5_3()              // default for all components
    .WithComponentFilter(1, WaveletFilter.Irreversible97)  // override component 1
    .WithComponentFilter(2, WaveletFilter.Irreversible97); // override component 2
```

Call `UseDefaultComponentFilters()` to clear per-component overrides.

## Presets

`WaveletPresets` provides ready-made configurations:

```csharp
var w = WaveletPresets.Lossless;      // 5/3, 5 levels
var w = WaveletPresets.HighQuality;   // 9/7, 6 levels
var w = WaveletPresets.Balanced;      // 9/7, 5 levels
var w = WaveletPresets.Fast;          // 9/7, 3 levels
var w = WaveletPresets.Medical;       // 5/3, 6 levels
var w = WaveletPresets.Archival;      // 5/3, 6 levels
var w = WaveletPresets.Web;           // 9/7, 5 levels
var w = WaveletPresets.Thumbnail;     // 9/7, 3 levels
```

## Using with CompleteEncoderConfigurationBuilder

```csharp
var bytes = new CompleteEncoderConfigurationBuilder()
    .ForBalanced()
    .WithWavelet(w => w.WithDecompositionLevels(6))  // override preset's level
    .Encode(imageSource);
```

## Standalone Usage

```csharp
var wavelet = new WaveletConfigurationBuilder()
    .UseIrreversible_9_7()
    .WithDecompositionLevels(5);

var pl = J2kImage.GetDefaultEncoderParameterList();
wavelet.ApplyTo(pl);
byte[] data = J2kImage.ToBytes(imageSource, pl);
```

## Validation

```csharp
var w = new WaveletConfigurationBuilder().WithDecompositionLevels(5);
if (!w.IsValid)
    Console.WriteLine(string.Join(", ", w.Validate()));
```
