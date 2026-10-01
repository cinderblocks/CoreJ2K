# OpenJPEG-encoded tiled fixtures

Lossless (5-3 reversible) codestreams produced with OpenJPEG 2.5.4 `opj_compress` from synthetic
images, used by `OpenJpegTiledInteropTests`. They are machine-generated test data with no third-party
content. The source image is defined by the `Pixel(x, y, c)` formula in the test:

    ((x*3 + y*5 + ((x*y)>>4) + c*40 + ((x*7 + y*11 + c) % 13)) & 255)

| File | Image | opj_compress options |
|------|-------|----------------------|
| gray200_tile100_6res.j2k | 200x200 gray | `-t 100,100` |
| gray250x170_tile100x70_rpcl_prec64.j2k | 250x170 gray | `-t 100,70 -p RPCL -c [64,64]x6` |
| rgb160x140_tile60_cprl_prec32.j2k | 160x140 RGB | `-t 60,60 -p CPRL -c [32,32]x6` |
| gray250x170_tile96_7res_origin.j2k | 250x170 gray | `-t 96,96 -n 7 -d 5,3 -T 3,2` |

Avoid tile sizes that leave a last row/column narrower than 13 pixels when generating more at five or
more decomposition levels (the point where a resolution level becomes empty for that tile). OpenJPEG 2.5.4
does not round-trip those losslessly: for last-tile widths 1-12, OpenJPEG decoding its own stream, ffmpeg's
native decoder and CoreJ2K all differ from the source image (CoreJ2K and ffmpeg identically), while all three
decode CoreJ2K-encoded streams of the same images exactly. From 13 pixels up all three are exact.
