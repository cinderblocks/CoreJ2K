# Changelog

This file starts with 2.4.0. Earlier releases are described on the
[GitHub releases page](https://github.com/cinderblocks/CoreJ2K/releases).

## Unreleased

### Added

- **A hard limit on the complete output**: `J2KEncoderConfiguration.WithMaxBytes(n)` (builder `WithMaxBytes`, parameter `max_bytes`). The JP2
  boxes, including metadata, count towards `n`; the encoder keeps as much of the image as fits and never writes more, and fails if the headers
  alone do not fit. A bitrate limits the codestream only; this limits the whole file. It sets up a single quality layer
  and cannot be combined with lossless coding, PLT markers, tile-parts or packed packet headers.

### Fixed

- **`WithROI` now takes effect.** `J2KEncoderConfiguration.WithROI` stored the configuration but never passed it to the encoder, so the
  output was identical to an encode without ROI. It now writes `Rroi`, `Rstart_level`, `Ralign` and `Rno_rect`. The builder gains `WithROI`.
  Component `-1` means all components and `0` means the first component only; `ROI_ENCODING_GUIDE.md` said `0` meant all, and is rewritten.
- **ROI encoding no longer writes corrupt streams when the quantized magnitudes are too wide.** Maxshift needs twice the magnitude bit count to
  fit in 31 bits, so a component with more than 15 bits (a very small `Qstep`, or deep samples) overflowed and decoded to garbage with no
  error, in CoreJ2K and in OpenJPEG alike. The encoder now throws `InvalidOperationException` naming the component and the count.
  Block-aligned ROI (`Ralign`) does not scale coefficients and is not limited.
- The distortion weight for ROI code-blocks overflowed `int` at 16 or more magnitude bits (`1 << (bits << 1)`); it is now computed in floating point.

### Changed

- **The ImageSharp package is `CoreJ2K.ImageSharp` again.** It was published as `CoreJ2K.ImageSharp-Official` while the `CoreJ2K.ImageSharp`
  id on NuGet belonged to someone else; that id has been transferred to this project. `CoreJ2K.ImageSharp-Official` is deprecated and
  will not get further releases. To migrate, replace the package reference; the assembly and namespace (`CoreJ2K.ImageSharp`) are unchanged.

## 2.4.0

### Read before upgrading

Three things behave differently from 2.3.x. Each is one line to revert.

- **Decode limits are on by default.** A decode whose estimated memory exceeds 2 GiB, or whose image exceeds 1 Gpixel, at the requested
  resolution throws `DecoderLimitException` before anything is allocated. Opt out with `DecoderLimits.Default = DecoderLimits.None;`, use
  `DecoderLimits.Strict` for untrusted input, or set limits per call with `J2KDecoderConfiguration.WithLimits`.
- **Decoding and encoding are parallel by default**, using up to `Environment.ProcessorCount` threads, with output identical to a
  single-threaded run. If your application already decodes or encodes many images at once, set
  `J2kImage.DefaultMaxDegreeOfParallelism = 1;` once at start-up, or pass `WithMaxDegreeOfParallelism(1)` to a single call.
- **Streams written with PLT (`Hplt`) or TLM (`Htlm`) markers are different.** `Htlm` now writes a TLM marker; before, it wrote nothing.
  PLT streams have correct tile-part lengths, complete PLT markers and PLT entries in the order the packets are written (see Fixed).
  Streams written without those options are byte-identical to what the 2.3.x encoder wrote.

### Added

- **Decode limits** (`DecoderLimits`, `DecoderLimitException`): `MaxPixels`, `MaxMemoryBytes` and `MaxTileComponents`, checked from the
  header alone, measured on the image as it will be produced, so previews of huge images keep working. The ISO/IEC 15444-1 maxima
  (65535 tiles, 16384 components) always apply.
- **Cancellation.** Decodes and encodes observe a `CancellationToken` and stop with `OperationCanceledException` within a few
  milliseconds. Tokens can be passed to `FromStream`/`FromBytes`/`FromFile`, `DecodeToImage<T>`, `ToBytes` and `WriteTo`, or set with
  `J2KDecoderConfiguration.WithCancellationToken` and `J2KEncoderConfiguration.WithCancellationToken`. The `*Async` methods end as
  `Canceled`, not `Faulted`. Cancelling leaves nothing behind that affects the next call.
- **Parallel decoding:** code-blocks and the inverse wavelet transform. On 8 cores a 4096x4096 RGB lossless decode went from 9.2 s to
  2.8 s (single tile).
- **Parallel encoding:** code-block coding, the forward wavelet transform, rate allocation and packet writing, with a producer thread
  that reads the source ahead of the coders. On 8 cores a 4096x4096 RGB encode is about 4.4x faster than on one thread. Both directions
  also process wavelet columns in cache-friendly blocks, which is faster on one thread too.
- **`threads` parameter, `MaxDegreeOfParallelism` on the configurations and `WithMaxDegreeOfParallelism` on the builder** set the thread
  count for one call; `J2kImage.DefaultMaxDegreeOfParallelism` sets it process-wide.
- **TLM markers can be written**, with `pl["Htlm"] = "on"`. `Htlm` is ignored, with a warning, together with `tile_parts` or packed
  packet headers, because those rewrite the tile-parts afterwards.

### Fixed

- **Tiled decoding** gave wrong samples for every tile after the first when tile origins were not multiples of 2^levels, and threw
  `IndexOutOfRangeException` for custom precinct sizes on multi-tile images.
- **Stale samples in the reconstruction buffer:** areas no code-block wrote (truncated or skipped blocks) leaked samples from earlier
  decodes, so identical decodes could differ.
- **Truncated streams:** a cut-off multi-layer stream, or a packet whose head was cut short, threw `IndexOutOfRangeException`.
- **Tile-part length (`Psot`) with PLT markers** left out the PLT marker, so readers that follow the lengths, such as OpenJPEG, failed to
  decode multi-tile PLT streams.
- **PLT markers dropped packet lengths** beyond the first 65532 bytes of a tile-part. They now use as many segments as needed. The
  decoder kept only the last PLT segment of a tile-part header and now keeps all of them.
- **PLT entries were not in written order** for progressions other than layer-first, for progression changes, and for several
  components. They now are.
- **SOP markers:** a tile with more than 65536 packets could not be decoded, because the decoder did not wrap the 16-bit sequence number.
- **TLM:** the `Stlm` size fields were in the wrong bits, in the encoder and in the decoder. TLM markers from other encoders are now read
  correctly, including those with several segments, which the decoder used to reject, and those without tile indices. Finding a tile
  from TLM took time that grew with the cube of the number of tiles (an 11000-tile stream could not be decoded in reasonable time) and
  now takes one pass.

### Known issues

- `Hppm` and `Hppt` are accepted but have no effect. Use `pph_main` and `pph_tile` for packed packet headers.
- PLM markers are read but never written.
- The rate target does not count PLT markers, so a rate-limited stream written with `Hplt` comes out a little over its target. TLM
  markers are counted.
