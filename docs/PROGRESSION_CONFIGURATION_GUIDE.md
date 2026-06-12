# Progression Configuration API Guide

## Overview

`ProgressionConfigurationBuilder` controls the order in which compressed data is
organized in the codestream. The progression order determines which data appears
first, affecting how the image can be accessed during streaming or partial decoding.

## Quick Start

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithProgression(p => p.UseLRCP())
    .Build();
```

## Progression Orders

JPEG 2000 defines five progression orders, each optimising for a different access
pattern. The four axes are: **L** (quality Layer), **R** (Resolution), **C** (Component),
**P** (spatial Position).

| Order | Method | First axis varies | Best for |
|-------|--------|------------------|----------|
| LRCP | `UseLRCP()` | Quality layer | Progressive quality, streaming |
| RLCP | `UseRLCP()` | Resolution | Thumbnail → full resolution |
| RPCL | `UseRPCL()` | Resolution + Position | Spatial browsing, pan/zoom |
| PCRL | `UsePCRL()` | Position | Tile-based random access |
| CPRL | `UseCPRL()` | Component | Multi-spectral / hyperspectral |

### LRCP — Quality Progressive

Data is ordered by quality layer first. A decoder receiving a partial stream sees
a low-quality version of the full image that improves as more data arrives.

```csharp
var p = new ProgressionConfigurationBuilder().UseLRCP();
```

Use when: streaming over limited bandwidth, progressive quality refinement.

### RLCP — Resolution Progressive

Data is ordered by resolution first. A decoder sees a thumbnail that progressively
sharpens to full resolution.

```csharp
var p = new ProgressionConfigurationBuilder().UseRLCP();
```

Use when: image browsers, web viewers that display thumbnails before loading full
resolution.

### RPCL — Spatial Browsing

Data is ordered by resolution then spatial position. Supports efficient pan and zoom
without decoding the whole image.

```csharp
var p = new ProgressionConfigurationBuilder().UseRPCL();
```

Use when: large images accessed via a viewer, GIS/mapping, ROI applications.

### PCRL — Tile Access

Data is ordered by spatial position. Individual tiles can be extracted and decoded
independently.

```csharp
var p = new ProgressionConfigurationBuilder().UsePCRL();
```

Use when: tile servers, distributed rendering, independent tile decoding.

### CPRL — Component Access

Data is ordered by component first. Individual spectral bands can be extracted
without decoding other components.

```csharp
var p = new ProgressionConfigurationBuilder().UseCPRL();
```

Use when: multi-spectral imaging, hyperspectral data, selective band access.

## Convenience Methods

```csharp
p.ForQualityProgressive();   // LRCP
p.ForResolutionProgressive(); // RLCP
p.ForSpatialBrowsing();      // RPCL
p.ForTileAccess();           // PCRL
p.ForComponentAccess();      // CPRL
```

## Per-Tile Progression Orders

Different tiles can use different progression orders:

```csharp
var p = new ProgressionConfigurationBuilder()
    .UseLRCP()                              // default for all tiles
    .WithTileOrder(0, ProgressionOrder.RPCL)  // tile 0 uses spatial order
    .WithTileOrder(1, ProgressionOrder.RLCP); // tile 1 uses resolution order
```

Call `UseDefaultTileOrders()` to remove per-tile overrides.

## Presets

```csharp
var p = ProgressionPresets.QualityProgressive;   // LRCP
var p = ProgressionPresets.ResolutionProgressive; // RLCP
var p = ProgressionPresets.SpatialBrowsing;      // RPCL
var p = ProgressionPresets.TileAccess;           // PCRL
var p = ProgressionPresets.ComponentAccess;      // CPRL
var p = ProgressionPresets.WebStreaming;         // LRCP
var p = ProgressionPresets.Medical;             // RLCP
var p = ProgressionPresets.Geospatial;          // RPCL
```

## Using with CompleteEncoderConfigurationBuilder

```csharp
var bytes = new CompleteEncoderConfigurationBuilder()
    .ForHighQuality()
    .WithProgression(p => p.UseRPCL()) // override preset with spatial browsing
    .Encode(imageSource);
```

## Decision Guide

```
Need progressive quality over slow connections?  → LRCP
Need thumbnail before full-res load?             → RLCP
Need pan/zoom without full decode?               → RPCL
Need independent tile extraction?                → PCRL
Need individual spectral bands?                  → CPRL
Not sure?                                        → LRCP (safe default)
```
