# JPEG 2000 Part 2 (JPX) Extended File-Format Boxes

Implements the container/metadata layer of ISO/IEC 15444-2 (JPX), extending the
existing JPR (`jpr `) and Label (`lbl `) support documented in
`PART2_BOXES_IMPLEMENTATION.md`.

## Boxes added

| Box  | Type code | Model class | Decode | Round-trip |
|------|-----------|-------------|--------|------------|
| Association (superbox) | `asoc` (0x61736f63) | `AssociationBox` | Number List decoded; other children preserved raw | ✅ |
| Number List | `nlst` (0x6e6c7374) | `NumberListBox` | Full | ✅ |
| Data Reference | `dtbl` (0x6474626c) | `DataReferenceBox` / `DataEntryUrl` | Full (NDR + `url ` entries) | ✅ |
| Fragment Table (superbox) | `ftbl` (0x6674626c) | `FragmentTableBox` | Wraps one Fragment List | ✅ |
| Fragment List | `flst` (0x666c7374) | `FragmentListBox` / `Fragment` | Full (NF + OFF/LEN/DR) | ✅ |
| Cross Reference | `cref` (0x63726566) | `CrossReferenceBox` | Carries a Fragment List | ✅ |
| Codestream Header (superbox) | `jpch` (0x6a706368) | `CodestreamHeaderBox` | Children preserved raw | ✅ |
| Compositing Layer Header (superbox) | `jplh` (0x6a706c68) | `CompositingLayerHeaderBox` | Children preserved raw | ✅ |

Plus the Part 2 File Type brand **`jpx `** (0x6a707820).

## Design notes

- **Box (de)serialization lives in the model classes.** Each class exposes a
  static `Parse(byte[] content)` and an instance `GetContentBytes()` operating on
  the box payload (DBox, excluding the 8-byte header). `FileFormatReader` /
  `FileFormatWriter` stay thin: they read/write the framing and delegate. Shared
  big-endian and box-framing helpers are in the internal `Jp2BoxIO`.
- **Superboxes preserve unknown children verbatim.** `jpch`, `jplh`, and the
  non-`nlst` children of `asoc` are kept as `Jp2BoxData` (type + raw payload),
  guaranteeing byte-exact round-trips regardless of the exact child semantics.
- **Number List encoding.** Each AN value's high byte selects the entity kind
  (0 = rendered result, 1 = codestream, 2 = compositing layer); the low 24 bits
  carry the zero-based index. Helpers: `AddRenderedResult`, `AddCodestream`,
  `AddCompositingLayer`, `IsCodestream`, `IsCompositingLayer`, `IndexOf`.
- **Automatic branding.** The File Type box emits `jpx ` (with a `jp2 ` + `jpx `
  compatibility list) whenever any JPX box is present
  (`J2KMetadata.HasJpxBoxes`) or `J2KMetadata.UseJpxBrand` is set; otherwise the
  baseline `jp2 ` brand is written unchanged. `writeFileTypeBox()` now returns the
  bytes written so the file-length bookkeeping stays correct.

## Usage

```csharp
var metadata = new J2KMetadata();

// Attach a label to codestream 0 via an Association box
metadata.AddAssociation(new NumberListBox().AddCodestream(0), "Region of interest");

// Reference data stored in another file
metadata.DataReference = new DataReferenceBox()
    .AddUrl("https://example.com/external.j2c");

// Describe a fragmented codestream
metadata.FragmentTables.Add(new FragmentTableBox
{
    FragmentList = new FragmentListBox().AddFragment(offset: 2048, length: 1024, dataReference: 1)
});

var bytes = J2kImage.ToBytes(imageSource, metadata); // File is branded 'jpx '
```

Reading is symmetric: `J2kImage.FromBytes(bytes, out var md)` populates
`md.Associations`, `md.DataReference`, `md.FragmentTables`, `md.CrossReferences`,
`md.CodestreamHeaders`, and `md.CompositingLayerHeaders`.

## Scope / limitations

This is the **file-format (container) layer** of Part 2. The codestream coding
extensions (arbitrary transformation kernels `ATK`, multiple-component transforms
`MCT`/`MCC`/`MCO`, nonlinear transform `NLT`, variable DC offset, trellis-coded
quantization, etc.) are **not** part of this work.

- `cref` is modelled as carrying a Fragment List box; an exotic file embedding a
  different internal structure would round-trip only the recognized Fragment List.
- Extended Length (XLBox, 16-byte header) is not modelled for these metadata
  boxes — consistent with the rest of the file-format reader.
- Fragment references are described and preserved, but external/fragmented data is
  not automatically resolved and assembled during decode.

## Tests

`tests/CoreJ2K.Tests/Part2JpxBoxTests.cs` — 14 tests: model-level round-trips for
every box, full write→read file round-trips, and brand selection. All pass on
net8.0 / net9.0 / net10.0; the existing 133 file-format/conformance tests are
unaffected.
