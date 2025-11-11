# CoreJ2K Encoder Parameters — Cheat Sheet

This cheat sheet lists encoder parameter names (exact strings used by `ParameterList`), short synopsis, and defaults where available. The encoder recognizes the same option names used historically in JJ2000/CSJ2K; use them exactly as keys in a `ParameterList`.

Usage example (C#):

```csharp
var p = new ParameterList();
p["rate"] = "0.5";           // set target bitrate (bpp)
byte[] j2k = J2kImage.ToBytes(bitmap, p);
```

---

## General encoder options (encoder_pinfo)
- `debug` — on|off — Print debugging messages. Default: `off`.
- `disable_jp2_extension` — on|off — Disable automatic `.jp2` extension when `file_format` is used. Default: `off`.
- `file_format` — on|off — Wrap codestream in JP2 file format. Default: `on`.
- `pph_tile` — on|off — Pack packet headers into tile headers. Default: `off`.
- `pph_main` — on|off — Pack packet headers into main header. Default: `off`.
- `pfile` — <filename> — Load arguments from file. Default: `null`.
- `tile_parts` — <packets per tile-part> — Packets per tile-part. Default: `0`.
- `tiles` — <nominal tile width> <nominal tile height> — Nominal tile dimensions. Default: `0 0` (no tiling).
- `ref` — <x> <y> — Image origin in canvas system. Default: `0 0`.
- `tref` — <x> <y> — Tile partition origin. Default: `0 0`.
- `rate` — <bpp> — Output bitrate in bits-per-pixel; `-1` means lossless/no discard. Default: `-1`.
- `lossless` — on|off — Turn on lossless compression (reversible quantization + w5x3). Default: `off`.
- `i` — <image file(s)> — Input image(s) (PGM/PPM/PGX). Mandatory for low-level flow.
- `o` — <file name> — Output filename (mandatory for CLI encoding). For library use, `ToBytes` returns bytes.
- `verbose` — on|off — Print encoding info. Default: `on`.
- `v` — on|off — Print version/copyright. Default: `off`.
- `u` — on|off — Print usage. Default: `off`.

---

## Tiling and packet options
- `tiles` — `"W H"` — Nominal tile width and height, e.g. `"1024 1024"`.
- `tile_parts` — integer — Max packets per tile-part (0 = all in first tile-part).
- `pph_tile`, `pph_main` — on|off — Packed packet headers (tile/main).
- `Psop`, `Peph` — on|off — Use Psop/Peph markers (packet header / end-of-packet header).

---

## Wavelet transform options (prefix `W`)
Source: `ForwardWT.ParameterInfo`
- `Wlev` — <number of decomposition levels> — Number of wavelet decomposition levels. Default: `5`.
- `Wwt` — `[full]` — Wavelet transform mode. Default: `full`.
- `Wcboff` — `<x y>` — Code-block partition origin (0 or 1). Default: must be provided (default in usage: `0 0`).

Note: `Wcboff` is required by the forward transform instance creation in the encoder code; typical value: `"0 0"`.

---

## Filters (prefix `F`)
Source: `AnWTFilter.ParameterInfo`
- `Ffilters` — `[<tile-component idx>] <id>` — Specifies analysis wavelet filters (e.g. `"w5x3"` or `"w9x7"`).
  - Default: depends on `lossless` / quantization type; `w5x3` for reversible, `w9x7` for non-reversible.

---

## Component transform (prefix `M`)
Source: `ForwCompTransf.ParameterInfo`
- `Mct` — `[<tile index>] [on|off] ...` — Multiple component transform (RCT/ICT). Values: `on`/`off`, `rct`/`ict` in per-tile contexts.
  - Default: not specified (encoder picks `rct` or `ict` depending on filters and `lossless`).

---

## Quantization (prefix `Q`)
Source: `Quantizer.ParameterInfo`
- `Qtype` — `[<tile-component idx>] <id>` — Quantization type: `reversible`, `derived`, `expounded`.
  - Default: depends on `lossless` flag and other defaults.
- `Qstep` — `[<tile-component idx>] <bnss>` — Base normalized quantization step size (ignored in reversible). Default: `0.0078125`.
- `Qguard_bits` — `[<tile-component idx>] <gb>` — Number of guard bits. Default: `2`.

---

## Rate allocation / layering (prefix `A`)
Source: `PostCompRateAllocator.ParameterInfo`
- `Alayers` — `[<rate> [+<layers>] ... | sl]` — Explicit layers specification. Default example in source: `"0.015 +20 2.0 +10"`.
- `Aptype` — progression order: `res`, `layer`, `res-pos`, `pos-comp`, `comp-pos` (or complex POC syntax). Default: depends on `Rroi` / other options; typical default is `layer`.

Notes: `Alayers` uses internal syntax (rates and optional `+<layers>`). See code comments in `PostCompRateAllocator` for parsing rules.

---

## Other encoder modules and options
- Progression/poc options are handled via `Aptype` and related POC syntax.
- Entropy/packet options: `Clen_calc`, `Cterm_type`, `Cseg_symbol` etc. — configured via encoder specs; see `EncoderSpecs` and the specific `ModuleSpec` classes in code.

---

## Where to find authoritative lists (in this repository)
- `CoreJ2K/J2kImage.cs` — `encoder_pinfo` (general encoder options)
- `CoreJ2K/j2k/wavelet/analysis/ForwardWT.cs` — `W` options (`Wlev`, `Wwt`, `Wcboff`)
- `CoreJ2K/j2k/wavelet/analysis/AnWTFilter.cs` — `Ffilters`
- `CoreJ2K/j2k/image/forwcomptransf/ForwCompTransf.cs` — `Mct`
- `CoreJ2K/j2k/quantization/quantizer/Quantizer.cs` — `Qtype`, `Qstep`, `Qguard_bits`
- `CoreJ2K/j2k/entropy/encoder/PostCompRateAllocator.cs` — `Alayers`, `Aptype`

Use those files as the canonical source of parameter names and default values.

---

## Quick recipes
- Lossy, JP2 wrapped, tiled 1024×1024, 5-level WT at 0.5 bpp

```csharp
var p = new ParameterList();
p["rate"] = "0.5";
p["file_format"] = "on";
p["tiles"] = "1024 1024";
p["Wlev"] = "5";
var bytes = J2kImage.ToBytes(bitmap, p);
```

- Lossless JP2

```csharp
var p = new ParameterList();
p["lossless"] = "on";
p["file_format"] = "on";
var bytes = J2kImage.ToBytes(bitmap, p);
```
