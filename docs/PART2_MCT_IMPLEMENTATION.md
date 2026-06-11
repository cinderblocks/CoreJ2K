# JPEG 2000 Part 2 — Multiple Component Transform (MCT/MCC/MCO/CBD)

The flagship Part 2 (ISO/IEC 15444-2) codestream extension: a transform applied across a
collection of components, wired end-to-end through encode and decode. Built on the Rsiz/CAP
capability foundation introduced with NLT.

All three array-based MCC transform kinds are implemented:
- **Matrix** decorrelation (`out = M·in`), the analysis matrix's inverse stored for synthesis.
- **Dependency** lifting/prediction (strictly-lower-triangular matrix P), exactly reversible
  even with fractional coefficients (the rounded prediction uses the originals in both directions).
- **Wavelet** — a reversible 5/3 (LeGall) lifting applied across the components.

The transform kind is carried in the MCC marker (`TransformType`) and applied by a single
`ComponentTransform` stage that branches on kind and direction (analysis on encode, synthesis
on decode); stages compose by chaining.

## Marker family

| Marker | Code | Role | Model class |
|--------|------|------|-------------|
| MCT | 0xFF74 | Transform array definition (decorrelation matrix / offset vector) | `MctArrayMarkerSegment` |
| MCC | 0xFF75 | Component collection + which array/transform applies | `MccMarkerSegment` |
| MCO | 0xFF77 | Order in which collection transforms are applied | `McoMarkerSegment` |
| CBD | 0xFF78 | (Intermediate) component bit depths | `CbdMarkerSegment` |

Each is read in both `HeaderDecoder` passes and exposed (`MctArrays`, `MccSegments`,
`McoSegment`, `CbdSegment`); each is written by `HeaderEncoder` when an MCT is requested,
along with `Rsiz = RSIZ_EXTENSIONS` and a CAP marker.

## Runtime transform

- `MctTransform` bridges markers and maths: `AssembleDecodeStages` reconstructs the ordered
  synthesis matrix stages from the markers; `BuildMarkers` builds the marker set from encode
  specs (storing the **synthesis** matrix = inverse of the analysis matrix, so a decoder
  reconstructs originals directly); `Invert` is a Gauss-Jordan matrix inverse.
- `MatrixComponentTransform` (`CoreJ2K.j2k.image.mct`) applies one stage:
  `out = Matrix · in + Offset` across the collection's components at each sample position.
  The same class serves both directions (encoder uses the analysis matrix, decoder the
  synthesis matrix); multiple stages compose by chaining instances. Components outside the
  collection pass through; integer blocks are transformed with rounding, float blocks in
  floating point. Results are written into the caller-supplied `DataBlk` in place.

## Pipeline placement

MCT sits at the component-transform position — after the tiler/NLT on encode (with the
Part 1 component transform disabled), and after the inverse Part 1 component transform on
decode, before NLT:

```
encode:  tiler → [ForwNLT] → ForwMCT → ForwCompTransf(none) → wavelet …
decode:  … wavelet⁻¹ → InvCompTransf(none) → InvMCT → [InvNLT] → output
```

Wired into both decode methods and the 8-bit fast path.

## Encoder entry point

```csharp
var spec = new MctEncodeSpec {
    Components = new[] { 0, 1, 2 },
    ForwardMatrix = analysisMatrix,   // coded = ForwardMatrix · original
    Irreversible = true,
    ElementType = MctElementType.Float64
};
var pl = J2kImage.GetDefaultEncoderParameterList();
pl["lossless"] = "on";
pl["Mct"] = "off";                    // use MCT instead of the Part 1 RCT/ICT
byte[] codestream = J2kImage.ToBytes(source, metadata: null, parameters: pl,
                                     nltSegments: null, mctSpecs: new[] { spec });
```
Decoding is automatic: when MCT markers are present the decoder assembles the synthesis
stages and applies them with no caller configuration.

## Notes on losslessness and range

A general matrix transform can push intermediate (e.g. difference) channels outside the
nominal component range. Reversible (5/3, lossless) coding preserves those exact integer
values regardless of magnitude, and the inverse matrix restores the originals — so a
**unimodular integer matrix** (integer inverse) round-trips losslessly, as does any
permutation. Irreversible/float matrices are near-lossless (rounding) and need a larger
intermediate bit depth (CBD) in practice. Both an exact permutation and an exact reversible
decorrelation are covered by the round-trip tests.

## Scope / honesty

- **All three array-based MCC kinds** (matrix, dependency, wavelet) are implemented. The
  wavelet kind is a fixed single-level reversible 5/3 across the components (it does not yet
  reference an arbitrary kernel via ATK, nor multi-level decomposition). One transform stage
  per collection is modelled (multiple stages compose by chaining).
- The marker **field semantics** follow ISO/IEC 15444-2, but the exact byte layouts of the
  MCC/MCO/CBD segments are CoreJ2K's documented representation: they round-trip faithfully
  through CoreJ2K and have **not** been validated against third-party MCT producers. (The MCT
  array `Imct` array-type/element-type packing follows the standard.)
- Per the established pattern, callers feed `InterleavedImageSource` signed-centred samples;
  the encoder's Part 1 component transform must be disabled (`Mct=off`) so MCT is the sole
  cross-component transform.

## Tests

`tests/CoreJ2K.Tests/Part2MCTTests.cs` — 9 tests: marker round-trips (MCT/MCC/MCO/CBD incl.
transform type), matrix inverse correctness, and full encode→decode pipeline round-trips for a
component permutation (exact), a reversible integer matrix decorrelation (exact, with
out-of-range difference channels), a dependency lifting with a fractional prediction matrix
(exact), and a 5/3 wavelet across the components (exact). The full suite (904 tests) passes with
no regressions.
