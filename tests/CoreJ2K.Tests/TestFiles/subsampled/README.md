# Subsampled-component test streams

Lossless codestreams of 36 x 24 images whose components are subsampled, written with OpenJPEG 2.5 (`opj_compress -F 36,24,3,8,u@... -mct 0`).
Component `c` holds, at its own sample `(x, y)`, the value `(x * 7 + y * 13 + c * 50 + x * y) & 255`. Used by `SubsampledComponentTests`. (The size is a multiple of every factor used because OpenJPEG's raw reader sizes a component as
`width * height / (dx * dy)`, which is not what it writes in the codestream for sizes that are not.)

| File | Subsampling of the three components |
|---|---|
| `mixed-2x2-chroma.j2k` | 1x1, 2x2, 2x2 |
| `mixed-2x1-and-1x2.j2k` | 1x1, 2x1, 1x2 |
| `mixed-3x3-and-4x4.j2k` | 1x1, 3x3, 4x4 |
| `all-2x2.j2k` | 2x2, 2x2, 2x2 |
