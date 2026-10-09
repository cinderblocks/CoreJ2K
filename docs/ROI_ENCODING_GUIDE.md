# ROI (Region of Interest) Encoding Guide

## Overview

Region of Interest (ROI) encoding spends more of the bit budget on parts of the image you care about. CoreJ2K implements the
JPEG 2000 Part 1 **Maxshift** method (ISO/IEC 15444-1, Annex H): the coefficients of the ROI are shifted up so that every ROI
bit-plane is coded before any background bit-plane. Any conforming decoder understands the result; the shift is recorded in an
RGN marker, and no mask is transmitted.

Because the ROI is coded first, a rate-limited encode keeps the ROI sharp and lets the background lose detail.

## Quick start

```csharp
using CoreJ2K;
using CoreJ2K.Configuration;
using CoreJ2K.j2k.roi;

var roi = new ROIConfiguration()
    .AddRectangle(component: -1, x: 100, y: 100, width: 400, height: 300)
    .SetStartLevel(4);

var config = new J2KEncoderConfiguration()
    .WithBitrate(0.5f)
    .WithROI(roi);

byte[] jp2 = J2kImage.ToBytes(image, config);
```

With ImageSharp, pass the same configuration to `EncodeToJ2K`:

```csharp
using CoreJ2K.ImageSharp;

byte[] jp2 = image.EncodeToJ2K(config);
```

The builder has the same method:

```csharp
var config = new CompleteEncoderConfigurationBuilder()
    .WithBitrate(0.5f)
    .WithROI(r => r.AddRectangle(-1, 100, 100, 400, 300).SetStartLevel(4))
    .Build();
```

## Components

Every `Add...` method takes a **component index**:

| Value | Meaning |
|-------|---------|
| `-1`  | All components. Use this for a region of the picture, such as a face. |
| `0`, `1`, `2`, ... | That component only (0-based). `0` is the first component: red in RGB, luma (Y) after an ICT/RCT. |

`AddRectangle(0, ...)` therefore does **not** mean "all components". A face prioritised on component 0 alone leaves the
other components coded without priority.

## Shapes

```csharp
new ROIConfiguration()
    .AddRectangle(-1, x: 100, y: 100, width: 400, height: 300)   // upper-left corner, size
    .AddCircle(-1, centerX: 500, centerY: 400, radius: 200)
    .AddArbitraryShape(-1, "mask.pgm");                           // see below
```

Coordinates are in pixels from the image origin.

**Arbitrary shapes** come from a PGM file the same size as the image, where non-zero pixels belong to the ROI. The path
cannot contain whitespace, and the file must exist when the configuration is validated. Rectangles use a fast mask generator;
circles and masks use the generic one.

### In-memory masks

A region of any shape can also be held in memory as a `ROIMask`, with no file. A mask is as large as the image it is used with
(one bit per pixel), and a pixel is inside when its centre is.

```csharp
// From the convex hull of facial landmarks (pixel coordinates from the top left):
var face = ROIMask.FromConvexHull(width, height, landmarks);   // IReadOnlyList<PointF> or (double X, double Y)

var config = new J2KEncoderConfiguration()
    .WithMaxBytes(12000)
    .WithROI(new ROIConfiguration().AddMask(-1, face).SetStartLevel(4));
```

| Factory | Makes |
|---------|-------|
| `FromConvexHull(w, h, points)` | The hull of a point set. Points inside it change nothing; they must not all lie on one line. |
| `FromPolygon(w, h, vertices)` | A polygon (even-odd rule where it crosses itself). Parts outside the image are cut off. |
| `FromEllipse(w, h, cx, cy, rx, ry)` | An ellipse with axes along the image axes. |
| `FromBytes(w, h, pixels)` | One byte per pixel, row by row; non-zero is inside. |
| `FromPredicate(w, h, (x, y) => ...)` | Any test applied to every pixel. |

The point lists take `System.Drawing.PointF` or plain `(double X, double Y)` tuples; use the tuples when your own point type is
ImageSharp's `PointF`, which is a different type.

Several masks and shapes in one configuration are combined into one region. A mask of the wrong size makes the encode fail with
"Input image and ROI mask must have the same size".

The masks travel in the encoder's parameter list: `ToParameterList()` fills `ParameterList.RoiMasks` and writes `M <index>` in
`Rroi`. If you build a `ParameterList` by hand, add the masks to `RoiMasks` yourself; an `M` that points at a missing mask is an
error, not silently ignored. Copying only the string options to another list drops the masks, and the encode fails.

## Options

| Method | `Rroi`-style option | Default | Description |
|--------|---------------------|---------|-------------|
| `SetStartLevel(level)` | `Rstart_level` | `-1` (off) | The lowest `level + 1` resolution levels are coded entirely as ROI, so low-resolution reconstructions are not left without detail in the ROI. `0` is the lowest resolution level only. |
| `SetBlockAlignment(true)` | `Ralign` | off | Any code-block that touches the ROI counts as ROI. Coefficients are not shifted; the allocator weights distortion instead. Less precise boundaries, and no limit on magnitude bits (below). |
| `ForceGenericMask()` | `Rno_rect` | off | Use the generic mask generator even when every region is a rectangle. |
| `SetScalingMode(...)` | | `MaxShift` | Maxshift is the only method. |

The configuration is written to the encoder's parameter list when you call `ToParameterList()` (the encoder methods do that
for you). If you work with `ParameterList` directly, set `Rroi`, `Rstart_level`, `Ralign` and `Rno_rect` yourself; for example
`pl["Rroi"] = "R 100 100 400 300"`. In `Rroi`, `R x y w h`, `C cx cy r` and `A file` add regions, and a `c<n>` word (for
example `c0` or `c0-2`) restricts every region after it to those components until the next `c<n>`.

## Limit on magnitude bits

Maxshift shifts the background down by as many bits as the largest quantized magnitude has, and the code-block coder holds
31 bit-planes, so a component can have **at most 15 quantized magnitude bits** when it has an ROI. The count depends on the
quantization step and the source precision, not on the picture. Past the limit the encoder throws an
`InvalidOperationException` ("ROI coding with Maxshift supports at most 15 quantized magnitude bits ...") instead of writing a
stream that decodes to garbage. To fix it, use a larger quantization step, a lower-precision source, or `SetBlockAlignment(true)`.

Typical 8-bit lossy settings are well inside the limit. Sources of 12 bits or more with a very small step, and lossless
coding of deep samples, can exceed it.

## Rate control

ROI changes what the encoder keeps, not how many bytes it writes: the limit still applies, and the ROI takes its share first.
`WithBitrate` limits the codestream only, not the JP2 boxes around it, and the default of many quality layers adds
packet-header overhead. For a firm byte budget use `WithMaxBytes(n)`, which limits the complete output, JP2 boxes included, and
uses a single quality layer (see the encoder configuration guide).

## Validation

`ROIConfiguration.Validate()` returns the problems it finds (no regions, a non-positive size or radius, a negative
coordinate, a component below `-1`, a missing or whitespace-containing mask path). `J2KEncoderConfiguration.Validate()`
includes them.

## Troubleshooting

| Symptom | Cause |
|---------|-------|
| The output has no RGN marker and looks like a plain encode | No regions were added, or the configuration was built with an older CoreJ2K that ignored `WithROI` (fixed in 2.4.1). |
| The ROI is sharp on one colour channel only | The region was added for component `0` instead of `-1`. |
| `Could not instantiate ROI scaler: ... magnitude bits` | See "Limit on magnitude bits". |
| `Arbitrary ROI mask file path cannot contain whitespace` | Move or rename the PGM file. |
| `'-Rroi M n' refers to an in-memory ROI mask that is not in ... RoiMasks` | The parameter list lost its masks; encode with the configuration, or add them to `RoiMasks`. |
| `Input image and ROI mask must have the same size` | The PGM file or `ROIMask` must match the image dimensions. |
