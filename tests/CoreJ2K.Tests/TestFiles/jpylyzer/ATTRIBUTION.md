# jpylyzer test-file subset — attribution

These JPEG 2000 (JP2) files are a small, clearly-licensed subset vendored from the
**jpylyzer-test-files** project for third-party file-format parsing and robustness tests.

- Upstream: https://github.com/openpreserve/jpylyzer-test-files
- Only files released under **Creative Commons Attribution (CC-BY)** are included here.
  Files from that project whose licence is unknown or proprietary (the `oj-*`, `tika-*`,
  `erdas-*`, `sentinel.*`, etc.) are deliberately **not** included.

| File | Creator / source | Licence | Notes |
|------|------------------|---------|-------|
| `reference.jp2` | Wikimedia Commons (1783 Montgolfier balloon illustration) | CC-BY | Valid JP2 — positive parsing test. |
| `bitwiser-icc-corrupted-tagcount-1911.jp2` | Andy Jackson, British Library | CC-BY | Valid JP2 structure with a single bit flipped in the ICC profile tagCount. |
| `bitwiser-resolutionbox-corrupted-boxlength-8127.jp2` | Andy Jackson, British Library | CC-BY | Invalid — corrupted Resolution Box length (negative test). |
| `bitwiser-headerbox-corrupted-boxlength-22181.jp2` | Andy Jackson, British Library | CC-BY | Invalid — corrupted JP2 Header Box length (negative test). |
| `bitwiser-codestreamheader-corrupted-xsiz-10918.jp2` | Andy Jackson, British Library | CC-BY | Invalid — corrupted SIZ Xsiz field (negative test). |

The `bitwiser-*` files were produced by the British Library "bitwiser" tool, which flips a
single bit of a base JP2 to exercise validators; the number in each name is the flipped bit
offset. These files are used here **unmodified**.

## Scope of these tests

This subset validates CoreJ2K's **JP2 file-format box reader and robustness** against real,
third-party-produced files. It is **not** an ISO/IEC
15444-4 conformance suite (these files carry no reference decoded outputs).
