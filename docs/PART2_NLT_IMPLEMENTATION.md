# JPEG 2000 Part 2 — Non-linearity Point Transformation (NLT)

First end-to-end JPEG 2000 Part 2 (ISO/IEC 15444-2) **codestream coding extension** in
CoreJ2K: a per-component point transform applied before compression and inverted on
decode, plus the capability-signalling foundation needed for any Part 2 codestream.

## What's included

### Capability foundation (shared by all future Part 2 codestream work)
- **Rsiz acceptance** — `HeaderDecoder.readSIZ` no longer rejects `Rsiz > 2`; it records
  `UsesExtensions` and continues. Part 2 codestreams can now be read at all.
- **CAP marker (0xFF50)** — read (`HeaderDecoder.readCAP`) and written
  (`HeaderEncoder.writeCAP`). The encoder sets `Rsiz = RSIZ_EXTENSIONS` and emits CAP
  whenever NLT segments are present.
- New `Markers` constants: `CAP` (0xFF50), `NLT` (0xFF76), `RSIZ_EXTENSIONS` (0x8000).

### NLT marker (0xFF76)
`CoreJ2K.j2k.codestream.NLTMarkerSegment` models the marker and its point transform:
- Fields: component selector `Cnlt` (or `AllComponents`), `BitDepth`/`Signed` (`BDnlt`),
  and `Type` (`None` / `Gamma` / `LookupTable`).
- **Gamma** — parametric power law `y = x^(1/E)` with exact inverse `x = y^E`.
- **Lookup table** — explicit mapping of transformed → original sample values; exact and
  fully invertible when the table permutes its own domain, and **identity outside the
  table domain** so it is robust to where it sits relative to the DC level shift.
- Self-contained binary (de)serialization (`Read`/`Write`/`ToBytes`/`FromBytes`).
- Decoder parses NLT in both header passes and exposes `HeaderDecoder.NLTSegments`;
  encoder writes segments via `HeaderEncoder.NLTSegments`.

### Pipeline stages
`CoreJ2K.j2k.image.nlt.ForwNLT` / `InvNLT` (`ImgDataAdapter` + `BlkImgDataSrc`) apply the
forward transform between the tiler and the component transform on encode, and the inverse
transform after the inverse component transform on decode. Integer blocks are transformed
exactly; float (irreversible) blocks via nearest-integer rounding. Both stages populate the
caller-supplied `DataBlk` in place (the decoder's sample-copy loop reads the passed block).

### Encoder entry point
```csharp
var nlt = new NLTMarkerSegment {
    ComponentIndex = NLTMarkerSegment.AllComponents,
    BitDepth = 8, Signed = true, Type = NLTType.LookupTable, Lut = myLut
};
var pl = J2kImage.GetDefaultEncoderParameterList();
pl["lossless"] = "on";
byte[] codestream = J2kImage.ToBytes(source, metadata: null, parameters: pl,
                                     nltSegments: new[] { nlt });
```
Decoding is automatic: when the codestream carries NLT segments the decoder applies the
inverse transform with no caller configuration.

## Domain note

The coding pipeline operates on DC level-shifted (signed-centred) samples — the decoder
re-adds the centre (`2^(B-1)`) when emitting the image. NLT segments must therefore be
configured for that domain (e.g. `Signed = true`, a LUT over `[-128, 127]` for an 8-bit
component). `InterleavedImageSource` passes samples through unchanged, so callers feed it
signed-centred data; this is exercised by the baseline round-trip test.

## Scope / honesty

- The NLT marker's **field semantics** (Cnlt / BDnlt / Tnlt) follow ISO/IEC 15444-2; the
  gamma-exponent encoding (16.16 fixed point) and LUT payload layout are this library's
  documented representation and round-trip faithfully through CoreJ2K, but have not been
  validated against third-party NLT producers.
- The CAP `Pcap` Part-2 bit uses the bit-`(32 - part)` convention; CAP is informational
  here (the NLT marker drives decode behaviour).
- Other Part 2 codestream extensions (ATK, MCT/MCC/MCO, variable DC offset, TCQ, single
  sample overlap) remain unimplemented; the Rsiz/CAP foundation above is the shared
  groundwork for them.

## Tests

`tests/CoreJ2K.Tests/Part2NLTTests.cs` — 11 tests: marker round-trips (gamma + signed/
unsigned LUT), transform invertibility (LUT exact, identity-outside-domain, gamma within
tolerance), stage isolation, marker presence in the encoded codestream, and full
encode→decode pipeline round-trips (identity and non-trivial LUT). The full suite (895
tests) passes with no regressions.
