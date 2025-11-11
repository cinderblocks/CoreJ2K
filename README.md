# CoreJ2K - A Managed and Portable JPEG2000 Codec

Copyright (c) 1999-2000 JJ2000 Partners;  
Copyright (c) 2007-2012 Jason S. Clary; 
Copyright (c) 2013-2016 Anders Gustafsson, Cureos AB;  
Copyright (c) 2024-2025 Sjofn LLC.   

Licensed and distributable under the terms of the [BSD license](http://www.opensource.org/licenses/bsd-license.php)

## Summary

`CoreJ2K` is a managed, portable implementation of a JPEG 2000 codec for .NET platforms. It is a modern fork of `CSJ2K` (itself a C# port of `jj2000`) adapted for .NET Standard and newer .NET targets.

This project provides decoding and encoding of JPEG 2000 images and small helpers to bridge platform image types to the codec. The `CoreJ2K.Skia` package supplies `SkiaSharp` integrations (e.g. `SKBitmap`, `SKPixmap`).

## Installation

Install the core library and the Skia integration from NuGet:

```
dotnet add package CoreJ2K
dotnet add package CoreJ2K.Skia
dotnet add package SkiaSharp
```

(Use the package manager appropriate for your project type.)

## Quick examples (using CoreJ2K.Skia)

These examples assume you reference `CoreJ2K`, `CoreJ2K.Skia` and `SkiaSharp`.

Decoding a JPEG 2000 file to an `SKBitmap` and saving as PNG:

```csharp
using System.IO;
using SkiaSharp;
using CoreJ2K; // J2kImage

// Decode from file stream
using var fs = File.OpenRead("image.j2k");
var portable = J2kImage.FromStream(fs);
var bitmap = portable.As<SKBitmap>();

// Save as PNG
using var outFs = File.OpenWrite("out.png");
bitmap.Encode(outFs, SKEncodedImageFormat.Png, 90);
```

Decoding from a byte array:

```csharp
byte[] j2kData = File.ReadAllBytes("image.j2k");
var portable = J2kImage.FromBytes(j2kData);
var bitmap = portable.As<SKBitmap>();
// Use `bitmap` in your app (draw, convert, save...)
```

Encoding an `SKBitmap` to JPEG2000 bytes (default options):

```csharp
using SkiaSharp;
using CoreJ2K;

// `bitmap` is an existing SKBitmap
byte[] j2kBytes = J2kImage.ToBytes(bitmap);
File.WriteAllBytes("encoded.j2k", j2kBytes);
```

Encoding from low-level image source (PGM/PPM/PGX) or platform images

```csharp
// Use J2kImage.CreateEncodableSource(Stream) when you have PGM/PPM/PGX data as streams
// or pass a platform-specific image (e.g. SKBitmap) to J2kImage.ToBytes(object)
```

## Advanced encoding parameters (ParameterList)

`J2kImage.ToBytes` accepts an optional `ParameterList` to control encoding options (compression rate, wavelet levels, tiling, component transform, etc.). The library expects parameter names without leading dashes (the same names used internally). Common encoder keys (exact names accepted) include:

- `rate` — target output bitrate in bits-per-pixel (bpp) (string/float)
- `lossless` — `on`/`off`
- `file_format` — `on`/`off` (wrap codestream in JP2)
- `tiles` — nominal tile width and height, e.g. `"1024 1024"`
- `tile_parts` — packets per tile-part (integer)
- `Wlev` — number of wavelet decomposition levels (integer)
- `Wcboff` — code-block partition origin: two ints `"0 0"` or `"1 1"`
- `Ffilters` — wavelet filters (e.g. `"w5x3"` or `"w9x7"`)
- `Mct` — component transform (`on`/`off` or `rct`/`ict` per tile)
- `Qtype`, `Qstep`, `Qguard_bits` — quantization controls
- `Alayers` — explicit layers specification (rate and optional +layers)
- `Aptype` — progression order specification (e.g. `"res"` or `"layer"`)
- `pph_tile`, `pph_main`, `Psop`, `Peph` — packet/header options

Below are practical examples that use the exact parameter names the encoder recognizes.

Lossy encoding with a target rate and common options:

```csharp
using CoreJ2K;
using SkiaSharp;

var parameters = new ParameterList();
parameters["rate"] = "0.5";          // target 0.5 bpp
parameters["file_format"] = "on";    // produce a .jp2 wrapper
parameters["tiles"] = "1024 1024";  // tile size
parameters["Wlev"] = "5";            // 5 decomposition levels
parameters["Wcboff"] = "0 0";       // code-block origin

byte[] j2kBytes = J2kImage.ToBytes(bitmap, parameters);
File.WriteAllBytes("encoded_lossy.j2k", j2kBytes);
```

Lossless encoding (uses reversible quantization / w5x3 by default):

```csharp
var parameters = new ParameterList();
parameters["lossless"] = "on";
parameters["file_format"] = "on";
byte[] j2kBytes = J2kImage.ToBytes(bitmap, parameters);
```

Advanced layer and progression control (explicit layers + progression type):

```csharp
// 'Alayers' uses the same syntax as the encoder internals, e.g. "0.015 +20 2.0 +10"
var pl = new ParameterList();
pl["rate"] = "0.2";
pl["Alayers"] = "0.015 +20 2.0 +10"; // example: multiple target rates with extra layers
pl["Aptype"] = "res"; // progression mode: "res", "layer", "pos-comp", "comp-pos", or "res-pos"

byte[] j2kBytes = J2kImage.ToBytes(bitmap, pl);
```

Set wavelet filters and quantization explicitly:

```csharp
var p = new ParameterList();
p["Ffilters"] = "w9x7"; // use 9x7 (irreversible) filters
p["Qtype"] = "expounded"; // non-reversible quantization type
p["Wcboff"] = "0 0";

byte[] bytes = J2kImage.ToBytes(bitmap, p);
```

Note: parameter names and value formats are codec-specific and must match the strings above. Use the `ParameterList` indexer (or `Add`) to set keys. Some options accept per-tile or per-component specifications using the library's option syntax (see `Alayers`, `Ffilters`, `Qtype`, `Mct` handling in the source).

## Platform-specific package targets and runtime notes

This repository contains multiple packages and TFMs to support a wide range of .NET runtimes. Pick the package and target framework appropriate for your application:

- Core libraries:
  - `CoreJ2K` targets `netstandard2.0` and `netstandard2.1` for broad compatibility.
  - `CoreJ2K.Skia` targets `netstandard2.0` / `netstandard2.1` and also has `net8.0` / `net9.0` builds in the workspace for modern runtimes.
  - `CoreJ2K.Pfim` targets `netstandard2.0` / `netstandard2.1` and also has `net8.0` / `net9.0` builds in the workspace for modern runtimes.
  - `CoreJ2K.ImageSharp` targets `net8.0` and `net9.0` for optimized modern integrations.
  - `CoreJ2K.Windows` produces multi-TFM packages: `netstandard2.0`, `netstandard2.1`, `net8.0-windows`, `net9.0-windows`.

- .NET Framework 4.8.1 compatibility: projects that target `netstandard2.0` can be consumed from .NET Framework 4.8.1 applications. Prefer the `netstandard2.0` package when targeting legacy frameworks.

- SkiaSharp native assets: when using `CoreJ2K.Skia` include the appropriate `SkiaSharp.NativeAssets.*` package for your OS and app type (for example `SkiaSharp.NativeAssets.Linux`, `SkiaSharp.NativeAssets.Windows`, `SkiaSharp.NativeAssets.Desktop`, or platform-specific runtime packs). Without native assets `SKBitmap` will not function at runtime.

- Check NuGet package pages for runtime support tables and any native dependency guidance (native runtimes, RIDs, and platform-specific notes).

## Usage notes

- `PortableImage` is the library's cross-platform image wrapper. Use `As<T>()` to cast to platform-specific types, for example `As<SKBitmap>()` when using the Skia integration.
- `J2kImage.ToBytes(object, ParameterList?)` accepts platform-specific image objects or codec-specific sources. If encoding parameters are required you can supply a `ParameterList` instance with keys shown above.
- The library exposes many encoder options using the same names as the original JJ2000/CSJ2K code; check the source for the full list of supported keys (e.g. `encoder_pinfo` in `J2kImage`).

## Links

* [Guide to the practical implementation of JPEG2000](http://www.jpeg.org/jpeg2000guide/guide/contents.html)

Badges and packages

[![CoreJ2K NuGet-Release](https://img.shields.io/nuget/v/CoreJ2K.svg?label=CoreJ2K)](https://www.nuget.org/packages/CoreJ2K/) 
[![CoreJ2K.Skia NuGet-Release](https://img.shields.io/nuget/v/CoreJ2K.Skia.svg?label=CoreJ2K.Skia)](https://www.nuget.org/packages/CoreJ2K.Skia/) 
[![CoreJ2K.Windows NuGet-Release](https://img.shields.io/nuget/v/CoreJ2K.Windows.svg?label=CoreJ2K.Windows)](https://www.nuget.org/packages/CoreJ2K.Windows/)  
[![NuGet Downloads](https://img.shields.io/nuget/dt/CoreJ2K?label=NuGet%20downloads)](https://www.nuget.org/packages/CoreJ2K/)  
[![Commits per month](https://img.shields.io/github/commit-activity/m/cinderblocks/CoreJ2K/master)](https://www.github.com/cinderblocks/CoreJ2K/)  
[![Build status](https://ci.appveyor.com/api/projects/status/9fr2467p5wxt6qxx?svg=true)](https://ci.appveyor.com/project/cinderblocks57647/corej2k)  
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/5704c7b134b249b3ac8ba3ca9a76dbbb)](https://app.codacy.com/gh/cinderblocks/CoreJ2K/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![ZEC](https://img.shields.io/keybase/zec/cinder)](https://keybase.io/cinder) [![BTC](https://img.shields.io/keybase/btc/cinder)](https://keybase.io/cinder)  
