# Testing against other implementations, and with random input

Besides the unit tests, `tests/CoreJ2K.Tests/Conformance/` holds tests that draw their input at random and, where they can, ask another JPEG 2000
implementation what the right answer is. They run with the rest of the suite on a small number of cases and can be pointed at as many as you like.

## What there is

| Test class | What it checks |
|---|---|
| `ScenarioSweepTests` | Encodes and decodes random *scenarios* (see below). The codestream passes `CodestreamValidator`, the decoded image has the right shape, a lossless coding returns every sample. |
| `OpenJpegDifferentialTests` | CoreJ2K encodes and OpenJPEG decodes; OpenJPEG encodes and CoreJ2K decodes. Lossless files must agree exactly, lossy ones to within a level or so. Skipped when OpenJPEG is not installed. |
| `MalformedStreamTests` | Decodes valid files that were cut short, had bytes changed, or a stretch overwritten. The decoder must not hang, and the exception must not be one of the decoder's own slips (null reference, index out of range, ...). |
| `CodecRegressionTests` | One small test for each defect those sweeps found. |
| `ScenarioTriage` | Not a test: explains one scenario (see "When something fails"). |

A *scenario* (`CodingScenario`) is a random choice of image size, depth and signedness, components, image offset, tiling, wavelet, quantization, code-block and
precinct sizes, progression, layers, entropy-coder modes, markers (SOP, EPH, PLT, TLM, packed packet headers, tile-parts) and region of interest. It is a pure
function of its seed, so a failure is reproduced by the seed alone. `SampleImage` makes the picture (smooth, edges, noise, constant, extremes, sparse) for a seed.

## OpenJPEG

The tools `opj_compress` and `opj_decompress` are looked for in `OPJ_BIN` (a directory), on the `PATH`, and in the usual install directories (MacPorts, Homebrew,
`/usr/local/bin`). Without them the differential tests are skipped, everything else still runs.

Some combinations are left out of the comparison because OpenJPEG itself fails on them (each is explained next to the code in `OpenJpegDifferentialTests`): a
region of interest together with selective bypass, a tile whose packets are all empty when the packet headers are packed, tiles narrower than the wavelet is deep
when OpenJPEG encodes, the numbering of PPT marker segments across the tile-parts of a tile, and PPM with several tiles each in several tile-parts.

## Running more

All of these are environment variables:

| Variable | Meaning |
|---|---|
| `COREJ2K_SWEEP_COUNT` | How many scenarios (or, for the damaged-file test, files) to draw. The defaults are 60 to 120. |
| `COREJ2K_SWEEP_START` | The first seed. Use a new range for fresh cases. |
| `COREJ2K_SWEEP_REPORT` | A file to write the full report to (failures are grouped by cause, with every seed). |
| `COREJ2K_SWEEP_DUMP` | A directory to keep the encoded files in. |
| `COREJ2K_FUZZ_MUTATIONS` | Damaged copies made of each file (12). |
| `COREJ2K_FUZZ_DEADLINE` | Seconds a damaged file may take to decode (20). |
| `COREJ2K_FUZZ_DUMP` | A directory to keep the damaged files that fail in. |

```
COREJ2K_SWEEP_COUNT=3000 COREJ2K_SWEEP_START=40000 COREJ2K_SWEEP_REPORT=/tmp/sweep \
    dotnet test tests/CoreJ2K.Tests -f net10.0 --filter "FullyQualifiedName~Conformance"
```

## When something fails

The report names the seed. Set `COREJ2K_TRIAGE_SEED` to it (and `COREJ2K_TRIAGE_DIR` to keep the files) and run `ScenarioTriage` with a detailed logger:

```
COREJ2K_TRIAGE_SEED=1234 dotnet test tests/CoreJ2K.Tests -f net10.0 --filter "FullyQualifiedName~ScenarioTriage" --logger "console;verbosity=detailed"
```

It encodes the scenario with CoreJ2K and with OpenJPEG, decodes each file with both decoders, and prints how every pairing compares with the source (and with
each other, with the mean error to show a bias). Two decoders that agree with each other and not with the source point at the encoder that made the file;
one that disagrees with the other on the same file is the decoder at fault. For a third opinion on 8-bit images, `ffmpeg -i file.j2k -f rawvideo -pix_fmt gray out.raw`
uses an independent decoder.

Once the cause is known, use `CodingScenario.With(...)` to take a scenario down to the smallest one that still fails, and add it to `CodecRegressionTests`.
