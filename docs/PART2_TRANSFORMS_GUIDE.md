# JPEG 2000 Part 2 Transforms Guide

## Overview

CoreJ2K implements four JPEG 2000 Part 2 (ISO/IEC 15444-2) codestream coding
extensions — three that operate as pre-/post-processing stages around the wavelet
engine, and one that replaces the wavelet filter itself:

| Transform | Marker | What it does |
|-----------|--------|-------------|
| DCO | `0xFF70` | Per-component signed integer DC offset |
| NLT | `0xFF76` | Non-linear point transform (gamma, log, LUT) |
| MCT | `0xFF74`/`0xFF75`/`0xFF76` | Multi-component matrix/dependency/wavelet transform |
| ATK | `0xFF79` | Arbitrary lifting-based wavelet kernel replacing the 5/3 or 9/7 |

All four produce **JPX** output (ISO/IEC 15444-2 file format). The `rreq` (Reader
Requirements) box is written automatically, advertising which extensions are in use.

The transforms are additive: DCO, NLT, MCT, and ATK can all be active
simultaneously (ATK disables only the Part 1 RCT/ICT component transform, not
the Part 2 MCT).

## Encode Pipeline Order

On encode the transforms are applied in this order before the wavelet:

```
image samples → DCO (subtract offset) → NLT (forward) → MCT (decorrelate) → wavelet
```

Decode reverses the order:

```
wavelet → InvMCT → InvNLT → InvDCO (add back offset) → output samples
```

## Using the Builder API

All three transforms are accessible through `CompleteEncoderConfigurationBuilder`.

### DCO — Variable DC Offset

DCO shifts each component's sample values by a signed integer. This is useful when
the codec expects signed-centred data but the source image is unsigned, or when
applying a known bias correction.

```csharp
// Single component (or all components share the same offset)
var bytes = new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .WithEncoder(e => e.WithFileFormat(true))
    .WithDco(128)          // subtract 128 from component 0
    .Encode(imageSource);
```

```csharp
// Per-component offsets (one value per component, in component order)
builder.WithDco(128, 0, 0);  // subtract 128 from component 0, leave 1 and 2 unchanged
```

```csharp
// Pre-built segment
var seg = new DCOMarkerSegment { Offsets = new[] { 128, 64, 64 } };
builder.WithDco(seg);
```

`WithDco` replaces any previously set DCO segment on the builder. There is at most
one DCO marker per codestream (covering all components).

### NLT — Non-Linear Point Transform

NLT applies a per-sample non-linear function before compression and its inverse
after decompression. Supported types are defined by `NLTType`:

| `NLTType` | Description |
|-----------|-------------|
| `None` | No-op (identity transform; marks the component as NLT-coded) |
| `SignedLinear` | Signed linear (clamp + shift) |
| `Gamma` | Power-law gamma correction |

```csharp
// Identity (marks codestream with NLT; no actual transformation)
builder.AddNlt(new NLTMarkerSegment { Type = NLTType.None });
```

```csharp
// Global gamma correction applied to all components
builder.AddNlt(new NLTMarkerSegment
{
    Type = NLTType.Gamma,
    BitDepth = 8,
    GammaExponent = 2.2,
    ComponentIndex = NLTMarkerSegment.AllComponents
});
```

```csharp
// Per-component: different settings for luma vs chroma
builder
    .AddNlt(new NLTMarkerSegment { Type = NLTType.Gamma, GammaExponent = 2.2, ComponentIndex = 0 })
    .AddNlt(new NLTMarkerSegment { Type = NLTType.None, ComponentIndex = 1 })
    .AddNlt(new NLTMarkerSegment { Type = NLTType.None, ComponentIndex = 2 });
```

```csharp
// Inline action form
builder.AddNlt(s =>
{
    s.Type = NLTType.Gamma;
    s.BitDepth = 8;
    s.GammaExponent = 2.2;
});
```

`AddNlt` accumulates segments. Call it once per component (or once globally with
`ComponentIndex = NLTMarkerSegment.AllComponents`).

### MCT — Multiple Component Transform

MCT applies a matrix, dependency (lifting), or wavelet transform across components.
It is more complex to configure; the `MctEncodeSpec` describes one transform stage.

```csharp
// Identity 3×3 matrix (no actual decorrelation, but marks codestream with MCT)
var spec = new MctEncodeSpec
{
    TransformType = MctTransformType.Matrix,
    Components = new[] { 0, 1, 2 },
    ForwardMatrix = new double[,]
    {
        { 1, 0, 0 },
        { 0, 1, 0 },
        { 0, 0, 1 }
    },
    Irreversible = false
};
builder.AddMct(spec);
```

```csharp
// Custom decorrelation matrix
var spec = new MctEncodeSpec
{
    TransformType = MctTransformType.Matrix,
    Components = new[] { 0, 1, 2 },
    ForwardMatrix = new double[,]
    {
        {  0.299,  0.587,  0.114 },   // Y
        { -0.169, -0.331,  0.500 },   // Cb
        {  0.500, -0.419, -0.081 }    // Cr
    },
    Irreversible = true,
    ElementType = MctElementType.Float64
};
builder.AddMct(spec);
```

`AddMct` accumulates stages. Multi-stage transforms are applied in the order added.

### ATK — Arbitrary Transformation Kernel

ATK replaces the Part 1 wavelet filter with a custom lifting kernel for all
tile-components. Reversible (integer lifting, lossless) and irreversible
(real-valued lifting with subband gains) kernels are both supported.

```csharp
// A custom reversible kernel: weaker predict/update than the 5/3
var kernel = new AtkMarkerSegment
{
    Index = 5,               // referenced by the COD transformation byte
    Reversible = true,
    Steps = new List<AtkLiftingStep>
    {
        new AtkLiftingStep { Coefficients = new double[] { -3, -3 }, Epsilon = 3, Beta = 4 },
        new AtkLiftingStep { Coefficients = new double[] { 1, 1 },  Epsilon = 3, Beta = 4 }
    }
};

var bytes = new CompleteEncoderConfigurationBuilder()
    .ForLossless()           // reversible kernels require reversible quantization
    .WithEncoder(e => e.WithFileFormat(true))
    .WithAtk(kernel)
    .Encode(imageSource);
```

Presets `AtkMarkerSegment.CreateW5x3Equivalent(i)` / `CreateW9x7Equivalent(i)`
express the Part 1 filters in ATK form. See
[PART2_ATK_IMPLEMENTATION.md](PART2_ATK_IMPLEMENTATION.md) for the lifting model
and constraints.

## Combining Transforms

All three can be active simultaneously:

```csharp
var bytes = new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .WithEncoder(e => e.WithFileFormat(true))
    .WithDco(128, 128, 128)
    .AddNlt(new NLTMarkerSegment { Type = NLTType.None })
    .AddMct(identitySpec)
    .Encode(imageSource);
```

## Reader Requirements Box

When any Part 2 transform is active, CoreJ2K automatically writes a conformant
`rreq` (Reader Requirements) box advertising the features in use. No manual
configuration is needed.

To inspect the rreq box in a decoded file:

```csharp
J2kImage.FromStream(stream, out var metadata);
var rreq = metadata.ReaderRequirements;
if (rreq != null)
    Console.WriteLine($"Features: {string.Join(", ", rreq.StandardFeatures)}");
```

Feature IDs: `1` = MCT, `2` = NLT, `3` = DCO, `4` = ATK, `5` = DFS.

## Accessing the Segments Directly

For low-level control, use the raw `J2kImage.ToBytes` overload:

```csharp
byte[] data = J2kImage.ToBytes(
    imageSource,
    metadata,              // J2KMetadata? — JP2/JPX file metadata
    parameterList,         // ParameterList
    nltSegments,           // IList<NLTMarkerSegment>?
    mctSpecs,              // IList<MctEncodeSpec>?
    dcoSegment             // DCOMarkerSegment?
);
```

## What Is Not Yet Implemented

| Feature | Marker | Status |
|---------|--------|--------|
| Downsampling Factor Structures | `0xFF72` | Not implemented |
| Arbitrary Decomposition Structures | `0xFF73` | Not implemented |
| Trellis Coded Quantization | `0xFF52` | Not implemented |

DFS/ADS change the shape of the wavelet decomposition tree (the packet and
code-block machinery currently assumes the Part 1 dyadic Mallat structure), and
TCQ replaces the scalar quantizer; both are substantial engine changes rather
than additive extensions.

See also: [PART2_NLT_IMPLEMENTATION.md](PART2_NLT_IMPLEMENTATION.md),
[PART2_MCT_IMPLEMENTATION.md](PART2_MCT_IMPLEMENTATION.md),
[PART2_ATK_IMPLEMENTATION.md](PART2_ATK_IMPLEMENTATION.md).
