# Changelog

This file starts with 2.4.0. Earlier releases are described on the
[GitHub releases page](https://github.com/cinderblocks/CoreJ2K/releases).

## Unreleased

### Added

- **A hard limit on the complete output**: `J2KEncoderConfiguration.WithMaxBytes(n)` (builder `WithMaxBytes`, parameter `max_bytes`). The JP2
  boxes, including metadata, count towards `n`; the encoder keeps as much of the image as fits and never writes more, and fails if the headers
  alone do not fit. A bitrate limits the codestream only; this limits the whole file. It sets up a single quality layer
  and cannot be combined with lossless coding, PLT markers, tile-parts or packed packet headers.

- **Regions of interest held in memory**: `ROIMask` (`FromConvexHull`, `FromPolygon`, `FromEllipse`, `FromBytes`, `FromPredicate`) and
  `ROIConfiguration.AddMask`, for arbitrary shapes without a PGM file on disk. A mask is one bit per pixel; `FromConvexHull` builds
  the region from a set of landmarks directly. Masks travel in the new `ParameterList.RoiMasks`, and `Rroi` refers to them as `M <index>`.

- **Distortion weights**: `DistortionWeights` and `J2KEncoderConfiguration.WithDistortionWeights` (option `Dweights`) weight the distortion of
  code-blocks by component, resolution level and subband, which steers where the rate allocator spends a limited budget, for example 1.25 on
  luma. They combine with ROI and with `WithMaxBytes`, and change nothing in the codestream's structure.

- **A portrait preset**: `CompleteEncoderConfigurationBuilder.ForPortrait(maxBytes, face)` and `CompleteConfigurationPresets.Portrait` set up a JP2 for
  a face image under a hard size limit: one tile and one layer, ICT with 9/7, five levels, 64x64 code-blocks, a luma weight of 1.25, and an optional
  Maxshift face region at start level 4.

- **Encode telemetry**: `J2KEncoderConfiguration.WithTelemetry(callback)` (builder `WithTelemetry`) reports, once the output is final, every
  code-block's passes, coded bytes, per-pass byte offsets, skipped bit-planes and distortion per quality layer, its place in its subband, its
  region-of-interest coefficients and passes, and the bytes of the headers, packet headers, packet bodies, EOC marker and JP2 boxes, each counted where
  it was written. It is for accounting outside the codec, such as how much of a size-limited file went to a face. It does not change the output,
  costs nothing when not set, and cannot be combined with tile-parts or packed packet headers.

### Fixed

- **A lossless encode could change samples of deep images.** The last coding passes of a code-block are kept whatever their estimated distortion, and
  that estimate can fall below an earlier pass's, but the final layer compared the resulting negative slope with its threshold of 0 and dropped them.
  Samples of 12-bit and deeper images came back up to 10 away from the source (a 50x20 tile with five decomposition levels is enough), and other
  decoders read the same wrong values. A threshold of 0 now takes every valid pass.
- **Encoding a 16-bit ImageSharp image (`L16`, `Rgb48`, `Rgba64`) produced an almost flat grey image.** The reader declared 16 bits per component but
  supplied the top 8 bits around a 16-bit mid-point. It now supplies the full samples; a 16-bit lossless encode is exact.
- **Skia bitmaps of the types `Rgba16161616`, `Rgba1010102`, `Rgb565`, `Rgb101010x` and similar were encoded as noise.** The reader takes one byte per
  component, which only the 8-bit types have. The others are now converted to 8-bit `Rgba8888` (`Alpha8` or `Rg88` for the one- and two-channel types) first.
- **The helpers that take a `CompleteEncoderConfigurationBuilder` dropped its metadata and Part 2 transforms** (`EncodeToJ2K(builder)`, `EncodeToJ2KHighQuality(copyright)`,
  `EncodeToJ2KWeb(copyright)` and the `SaveAs` versions, in the ImageSharp, Skia, Avalonia, Windows and Pfim packages). They built the `J2KEncoderConfiguration` and
  encoded with that, so the copyright and comments were never written. They now call `builder.Encode`.
- **A JP2 file with two, or five or more, components could not be decoded.** The palette stage, which passes the components through when the file has no palette, rejected
  any count but 1, 3 and 4.
- **`CompleteEncoderConfigurationBuilder.Build()` overwrote settings of the encoder configuration that the caller had not set.** `WithWavelet(w => w.WithDecompositionLevels(3))`
  also set the filter to 5/3, which a lossy encode rejects ("Filter ... does not allow non-reversible quantization"), and `WithQuantization(q => q.WithBaseStepSize(...))`
  also set the type and guard bits. `Build()` now passes on only the values that were set.
- **`J2KDecoderConfiguration`: `QuitConditions.MaxCodeBlocks` always failed** because parsing mode, which is on by default, cannot be combined with it. Parsing is
  now turned off when a code-block limit is set and parsing was not asked for; asking for both is reported by `Validate`.
- **`WithComponentFilter` and `WithTileOrder` did nothing.** The wavelet builder wrote an `Ffilters_comp` parameter and the progression builder a `Porder` and
  `Porder_tile`, which nothing reads, and `Build()` dropped both settings. They now write `Ffilters` and `Aptype` with the per-component and per-tile syntax; the
  complete builder gives each component the quantization type its filter needs, and a mixed set of filters on the first three components switches the colour transform
  off. `WaveletConfiguration` gains `WithComponentFilter` and `ProgressionConfiguration` gains `WithTileOrder`, so `J2KEncoderConfiguration` takes them too, and `Validate`
  reports a 9-7 component filter in lossless mode.
- **A JP2 or codestream with some components reversible (5/3) and others not (9/7) could not be decoded** (`NullReferenceException`). The decode loop read the block it had passed in,
  but the converter hands back its own when the components differ in data type, and the converter cast the rows of all components into one shared buffer, so a 9/7
  component was overwritten by the next one. Every component now has its own.
- **`FromJ2KFile`, `FromJ2KBytes` and `FromJ2KStream` in CoreJ2K.ImageSharp threw `InvalidCastException` for RGB and greyscale streams.** They return
  `Image<Rgba32>` but the decoder produced `Rgb24` and `L8` images for 3- and 1-component streams. They now convert, adding an opaque alpha channel.
- **`WithROI` now takes effect.** `J2KEncoderConfiguration.WithROI` stored the configuration but never passed it to the encoder, so the
  output was identical to an encode without ROI. It now writes `Rroi`, `Rstart_level`, `Ralign` and `Rno_rect`. The builder gains `WithROI`.
  Component `-1` means all components and `0` means the first component only; `ROI_ENCODING_GUIDE.md` said `0` meant all, and is rewritten.
- **ROI encoding no longer writes corrupt streams when the quantized magnitudes are too wide.** Maxshift needs twice the magnitude bit count to
  fit in 31 bits, so a component with more than 15 bits (a very small `Qstep`, or deep samples) overflowed and decoded to garbage with no
  error, in CoreJ2K and in OpenJPEG alike. The encoder now throws `InvalidOperationException` naming the component and the count.
  Block-aligned ROI (`Ralign`) does not scale coefficients and is not limited.
- The distortion weight for ROI code-blocks overflowed `int` at 16 or more magnitude bits (`1 << (bits << 1)`); it is now computed in floating point.
- **Tiled encodes with an image offset failed.** The tile count ignored the origin of the tile grid, so `ref` combined with `tiles` counted tiles beyond the image and
  the encoder threw (`ArgumentOutOfRangeException` or `OverflowException`).
- **Lossy decodes that use the colour transform came out about half a level too bright on the dark half of the image.** The inverse transform rounded values centred on
  zero by adding one half and truncating, which rounds negative values up by a whole level; on one test file the PSNR against the source rose from 48.7 to 59.6 dB
  (OpenJPEG decodes it at 59.6). With no wavelet decomposition (`Wlev 0`) a lossy component was also rounded to integers before the transform.
- **A file with packed packet headers in the main header (`pph_main`, PPM) could not be decoded**: the marker was read in the wrong byte order.
- **Random access to tiles through TLM failed in a JP2 file** (`NullReferenceException`): the offsets were measured from the start of the codestream but used as file
  positions. When the TLM data was wrong, the decoder said it would parse the tiles in sequence but did not.
- **Tile-parts.** A tile with more packets than the first tile was split into several tile-parts although none were asked for. More than 255 tile-parts for one tile
  (`tile_parts` of a few packets) wrote a count the SOT marker cannot hold; the packets per tile-part are now raised to stay within 255. When packed packet headers or
  tile-parts made the codestream shorter, the end of the old codestream stayed after the new end marker.
- **Selective arithmetic-coding bypass could make the encoder throw `IndexOutOfRangeException`** when the estimated end of a raw pass lay past the end of the code-block's data.
- **Damaged files could hang the decoder or end in an implementation exception.** A packet header of 1 bits read a tag tree and a length indicator without end, and a JP2
  box of length 0 was never passed. Both now fail; a file that makes the decoder run into a missing structure raises `InvalidOperationException` (with the original as
  the inner exception) instead of `NullReferenceException` or `IndexOutOfRangeException`, and an arbitrary-filter file raises `NotSupportedException`.
- **Decoding time grew with the square of the number of tiles** (16,384 tiles of 4x4 took 17 seconds, now 0.3) because each code-block looked at the decomposition level of
  every tile-component.
- **`CodestreamValidator` rejected valid codestreams**: it never recognised the main header markers it compared (reporting COD as missing) and miscounted the precinct sizes.
- **Decoding a tiled image, or one with an image origin, at a reduced resolution (`WithResolutionLevel`) put every tile but the first off the image.** The tile
  sizes and origins the wavelet stage reported were those of the full-size tile, so the rows of a tile were read at the wrong width and written where the next tile
  belongs. They are now those of the level being reconstructed. Checked against the official `p0_03` stream and OpenJPEG over a few thousand random files.
- **`QuitConditions.WithMaxLayers(n)` decoded n - 1 layers**, so one layer gave a blank image and the full count could not be asked for. It now decodes n.
- **A JP2 file with a channel definition box that names an opacity channel could not be decoded**, nor could one that writes its own colour channels in another order
  with the palette in play. That is every RGBA and grey-plus-alpha JP2, including those CoreJ2K wrote (`IndexOutOfRangeException`). Channels are now put out as the colours in
  the order of their association, then the opacity channels, then any the box does not mention; the definition applies after the palette, as the standard says.
- **Components that are subsampled could not be decoded properly.** In a raw codestream they gave `IndexOutOfRangeException`, or an image mostly of empty rows; in a JP2,
  factors other than 2 were refused, and an image whose components are all subsampled alike came out at the size of the reference grid. The components are now put on
  the grid of the finest one, which keeps its samples while the others repeat theirs, so an image subsampled alike is as large as its components, as in the
  standard's reference images. That makes eleven streams of the conformance suite decode that did not (`p0_02`, `p0_05`, `p0_06`, `p0_10`, `p1_01`, `p1_03`, `p1_07`, `a4_colr`,
  `a6_mono_colr`, `b2_mono` and `e2_colr`).
- **Position-based progressions (RPCL, PCRL, CPRL) misplaced the first precinct of a subsampled component when the image origin was not a multiple of the precinct size**,
  so the packets were read in the wrong order. The test for it included the subsampling factor, which the standard's does not (official stream `p1_07`).
- **A tile-part with no packets, and tile-parts that do not announce their number (TNsot 0), made the packet reader read the next tile-part's marker as data** (official `p0_10`).
  Tile-parts beyond the announced number are now read as well (OpenJPEG's "TPsot==TNsot"; some encoders write the last index, not the count).
- **A POC marker segment in a tile-part header threw `KeyNotFoundException`** (official `p0_07` and `e1_colr`), and so did printing the header of a tile that had none of an optional segment.
- **A JP2 file with several colour specification boxes used the last one**, and refused one whose method it did not know. The first one that can be used is taken (an ICC
  profile, or an enumerated colour space that is converted), as the standard says; this is why `file5.jp2` of the conformance suite came out unconverted.
  When the colour conversion cannot be set up, for example an RGB profile on an image that also has an alpha channel, the channels are returned unconverted with a warning
  instead of failing; a colour space that is not converted no longer drops the palette and the channel definitions with it.
- **Files that other decoders read and CoreJ2K refused:** a QCD or QCC segment with fewer step sizes than there are subbands (the missing ones get a zero exponent); a codestream
  box longer than the file, bytes after the last box, and boxes with a 64-bit length that fits in 2 GiB; a tile size of 2^31 or more (one tile over the image); and a colour
  transform asked for by an image with fewer than three components (it is not applied).
- **The inverse irreversible colour transform failed when a component beyond the third was asked for first** (`NullReferenceException`; it came up once the channels were put in order).

### Changed

- **Subsampled components and the size of the image.** An image whose components are all subsampled alike is now decoded at the size of the components, not of the reference
  grid (`subsampling_2.jp2` of the conformance suite is 640 x 512, not 1280 x 1024); components of different sampling are repeated up to the finest. Nothing changes for images
  that are not subsampled. The encoder still writes components of full resolution only.
- **The ImageSharp package is `CoreJ2K.ImageSharp` again.** It was published as `CoreJ2K.ImageSharp-Official` while the `CoreJ2K.ImageSharp`
  id on NuGet belonged to someone else; that id has been transferred to this project. `CoreJ2K.ImageSharp-Official` is deprecated and
  will not get further releases. To migrate, replace the package reference; the assembly and namespace (`CoreJ2K.ImageSharp`) are unchanged.
- **Dependencies updated:** Avalonia 12.1.3, System.Drawing.Common 10.0.12 and JetBrains.Annotations 2026.2.0. SixLabors.ImageSharp stays at 3.1.12 and SkiaSharp at
  3.119.4. Five advisories now apply to ImageSharp 3.1.12 (ICC profile parsing, TIFF and BigTIFF decoding and encoding, histogram equalization); the versions that fix them
  (4.1.2) need a Six Labors licence key to build in Release, so the advisories are suppressed in `Directory.Build.props` for now. CoreJ2K does not call the code they concern,
  but an application that decodes untrusted images with ImageSharp should know. SkiaSharp 4 is not used because Avalonia.Skia 12.1.3 is built against SkiaSharp 3.
- **`WithSubbandStep` and `WithResolutionSteps` are obsolete and no longer ignored.** The encoder has no per-subband step sizes (it derives every subband's step from the
  base step), so the values were written to a `Qstep_subband` parameter that nothing reads and dropped by `Build()`. `QuantizationConfigurationBuilder.ApplyTo` and
  `CompleteEncoderConfigurationBuilder.Build()` now throw `NotSupportedException` when one is set, and `Validate` reports it. To favour a subband or resolution level
  under a rate limit, use `WithDistortionWeights`.

### Known issues

- A JP2 component mapping box that takes some channels from the palette and others straight from a component (`mtyp` 0 next to 1) is refused.
- Encoding with many decomposition levels (the standard allows 32) needs memory that doubles with every level, and ends the process at 31: the norms of the subband
  synthesis filters are found from waveforms of that length. Up to 28 levels work.
- The enumerated colour spaces e-sYCC (24), CMYK, CIELab and ROMM-RGB are not converted (the samples are returned as they are), and an ICC profile is applied to
  images of 1 or 3 components only.
- `issue135.j2k`/`kodak_2layers_lrcp.j2c` of the OpenJPEG test data (12 bit, two layers, 9/7 with the colour transform) decodes differently from OpenJPEG in the second layer's
  refinements of the code-blocks that have data in both layers (up to 274 of 4095 at the worst sample). The packet headers of the whole file and the entropy decoding of one
  block were checked against separate implementations of the standard and agree with CoreJ2K; no third decoder could read the file, so which one is right is not settled.

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
