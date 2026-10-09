# ISO/IEC 15444-4 conformance streams — attribution

A few of the codestreams of the JPEG 2000 conformance test suite (ITU-T T.803 | ISO/IEC 15444-4), with the reference
images that the suite gives for them (`*.pgx`, one file per component). They are used by `Iso15444PartFourStreamTests`.

- Upstream: the `input/conformance` and `baseline/conformance` folders of https://github.com/uclouvain/openjpeg-data
- `pN_NN.j2k` are the codestreams; `c1pN_NN_C.pgx` are the full-resolution reference images (component C) and `c0p0_03r1.pgx` is
  the reference for `p0_03.j2k` decoded one resolution level down.

| Stream | What it exercises |
|---|---|
| `p0_02.j2k` | one component subsampled by 2 horizontally (the image is 127 x 126 on the reference grid, the component 64 x 126) |
| `p0_03.j2k` | tiles, decoded at a lower resolution |
| `p0_10.j2k` | components subsampled by 4, tiles, tile-parts that do not say how many there are, and an empty tile-part |
| `p1_01.j2k` | a subsampled component with an image and a tile origin that are not multiples of the factor |
| `p1_07.j2k` | a full-resolution and a 4 x 1 subsampled component, an image origin, 1 x 1 precincts, SOP and EPH, RPCL progression |

## Notice that comes with the files

These files were originally developed by Algo Vision Technology GmbH,
Aware Inc., Kodak Inc., and Ricoh Innovations Inc., in the course of
development of ITU-T 803 | ISO/IEC 15444-4.  The files help test parts
of implementations of a part of ITU-T 800 | ISO/IEC 15444-1. Copyright
holders agree not to assert against ITU, ISO/IEC and users of the JPEG
2000 Standards (Users) any of their rights under the copyright, not
including other intellectual property rights, for these files with
respect to the usage by ITU, ISO/IEC and Users of these files or
modifications thereof for use in hardware or software products
claiming conformance to or testing conformance to the JPEG 2000
Standard.  The original developers of these files, ITU and ISO/IEC
assume no liability for use of these files or modifications
thereof. No right to these files is granted for non JPEG 2000 Standard
uses. Copyright holders have full right to use these files for his/her
own purpose, assign or donate these files to any third party and to
inhibit third parties from using this software module for non JPEG 2000
Standard conforming products. This copyright notice must be
included in all copies or derivative works of these files.

Copyright (c) 2002.
