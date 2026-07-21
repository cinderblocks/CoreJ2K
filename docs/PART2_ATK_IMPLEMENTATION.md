# JPEG 2000 Part 2 — Arbitrary Transformation Kernels (ATK)

## Overview

CoreJ2K supports the JPEG 2000 Part 2 (ISO/IEC 15444-2) **Arbitrary Transformation
Kernel** extension: custom lifting-based wavelet kernels that replace the Part 1
5/3 and 9/7 filters. A kernel is declared in an **ATK marker segment** (`0xFF79`)
in the main header, and the COD/COC `SPcod`/`SPcoc` transformation byte references
it by index (values `0` and `1` remain the Part 1 9/7 and 5/3; values `2..127`
select the ATK segment with the matching index).

Both reversible (integer lifting, lossless-capable) and irreversible (real-valued
lifting with subband gains) kernels are supported, on both the encode and decode
sides, through the full wavelet engine — this is a real transform capability, not
just marker plumbing.

## Model

`AtkMarkerSegment` (namespace `CoreJ2K.j2k.codestream`) describes a kernel:

| Property | Meaning |
|----------|---------|
| `Index` | Kernel id (2..127) referenced by the COD/COC transformation byte |
| `Reversible` | Integer lifting (lossless) vs real-valued lifting |
| `FirstStepUpdatesOdd` | Whether the first lifting step updates odd (high-pass) samples; the Part 1 kernels do |
| `Steps` | The lifting ladder, applied in order on analysis, reversed on synthesis |
| `LowGain` / `HighGain` | Irreversible only: subband scale factors applied after analysis |

Each `AtkLiftingStep` holds `Coefficients` (integers for reversible kernels),
tap-placement `Offset`, and — for reversible kernels — the downshift `Epsilon`
and rounding offset `Beta`. A reversible step computes

```
x[p] += floor((sum_k A[k] * x[p + 2*(k - Offset) - 1] + Beta) / 2^Epsilon)
```

and an irreversible step the same without the offset/shift. Boundaries use
whole-sample symmetric extension. Because every lifting ladder is invertible by
construction, any reversible kernel round-trips losslessly.

Presets `AtkMarkerSegment.CreateW5x3Equivalent(index)` and
`CreateW9x7Equivalent(index)` express the Part 1 filters in ATK form; the 5/3
equivalent is verified **bit-exact** against the built-in filter, which validates
the generic engine's arithmetic and boundary handling.

## Usage

### Builder API

```csharp
var kernel = new AtkMarkerSegment
{
    Index = 5,
    Reversible = true,
    Steps = new List<AtkLiftingStep>
    {
        new AtkLiftingStep { Coefficients = new double[] { -3, -3 }, Epsilon = 3, Beta = 4 },
        new AtkLiftingStep { Coefficients = new double[] { 1, 1 },  Epsilon = 3, Beta = 4 }
    }
};

var bytes = new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .WithEncoder(e => e.WithFileFormat(true))
    .WithAtk(kernel)
    .Encode(imageSource);
```

### Raw API

```csharp
byte[] data = J2kImage.ToBytes(imgsrc, metadata, parameterList,
    nltSegments, mctSpecs, dcoSegment, atkKernel);
```

Decoding needs nothing special: `J2kImage.FromBytes(data)` resolves the kernel
from the ATK segment automatically.

### Constraints

- A reversible kernel requires reversible quantization (`lossless on` or
  `Qtype reversible`); an irreversible kernel requires lossy quantization.
- The kernel replaces the wavelet filter for **all tile-components**; it cannot
  be combined with an explicit `Ffilters` choice.
- The Part 1 inter-component transform (RCT/ICT, `Mct`) is disabled — Part 1
  ties it to the 5/3/9/7 filters. Use the Part 2 MCT extension for
  cross-component decorrelation alongside an ATK kernel.
- ATK output is branded Part 2 (`Rsiz`, CAP marker) and, with file format on,
  produces a JPX file whose `rreq` box advertises standard feature 4.

## Implementation notes

- `CoreJ2K/j2k/wavelet/AtkLifting.cs` — the generic lifting engine (int + float,
  analysis + synthesis) shared by the four filter classes
  `AnWTFilterIntArbitrary`, `AnWTFilterFloatArbitrary`,
  `SynWTFilterIntArbitrary`, `SynWTFilterFloatArbitrary`.
- Rate-allocation energy weights come from synthesis impulse responses derived
  numerically from the lifting ladder (reversible steps linearized by dropping
  the floor and scaling by `2^-Epsilon`). For the 5/3-equivalent kernel this
  reproduces the Part 1 waveforms `[0.5, 1, 0.5]` and
  `[-0.125, -0.25, 0.75, -0.25, -0.125]` exactly.
- Degenerate length-1 signals follow the Part 1 conventions (low-pass
  passthrough, high-pass Nyquist gain 2).
- The decoder reads ATK segments before COD/COC so `readFilter` can resolve
  custom kernel ids; a transformation byte referencing a missing kernel raises
  `CorruptedCodestreamException`.

## Conformance caveat

The ATK byte layout documented in `AtkMarkerSegment` follows the field semantics
of the ISO/IEC 15444-2 ATK segment (index, reversibility flags, lifting steps
with coefficients, shifts and rounding offsets) but is **CoreJ2K's documented
representation**: it round-trips faithfully through CoreJ2K and has not been
validated against third-party ATK producers (no third-party ATK fixtures exist
in this repository). The substantive deliverable is the working transform: the
generic engine is verified bit-exact against the Part 1 5/3 filter, numerically
against the 9/7, and lossless end-to-end for custom kernels.

## Tests

`tests/CoreJ2K.Tests/Part2ATKTests.cs` (22 tests):

- Marker serialization round-trips (reversible + irreversible) and validation.
- Generic engine vs built-in 5/3: analysis and synthesis **bit-exact** for
  lengths 1..17, both phase variants.
- Generic engine vs built-in 9/7: numerically equivalent.
- Synthesis waveform extraction matches the Part 1 5/3 waveforms.
- Perfect reconstruction of custom reversible and irreversible kernels.
- Full pipeline lossless round-trips: 5/3-equivalent, custom kernels, odd
  dimensions, multi-component, tiled.
- ATK 9/7-equivalent decode matches built-in 9/7 decode.
- JPX `rreq` advertises feature 4; ATK/CAP markers present in the codestream.
- Error handling: filter/kernel conflicts, quantization mismatches, decoding a
  stream whose ATK segment is missing.
