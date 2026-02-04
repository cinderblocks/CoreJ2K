# CoreJ2K Fuzzing

Comprehensive fuzzing infrastructure for CoreJ2K JPEG 2000 codec.

## Quick Start

```powershell
# Install SharpFuzz
dotnet tool install --global SharpFuzz.CommandLine

# Build
dotnet build -c Release

# Run fuzzing
pwsh Run-Fuzzing.ps1 -Quick
```

## Targets

- **decoder** - Fuzz JPEG 2000 decoding (default)
- **encoder** - Fuzz JPEG 2000 encoding
- **headers** - Fuzz header parsing
- **markers** - Fuzz marker segments

## Documentation

See [FUZZING_GUIDE.md](FUZZING_GUIDE.md) for comprehensive documentation.

## Results

Fuzzing findings are saved to `Findings/`:
- `crash_*.bin` - Inputs that caused crashes
- `hang_*.bin` - Inputs that caused hangs
- `*.stderr.txt` - Error output

## CI Integration

Fuzzing runs automatically:
- **Weekly** - Every Sunday at 2 AM UTC
- **Manual** - GitHub Actions workflow dispatch

Check the Actions tab for results.

## Contributing

Found a bug via fuzzing? Please:
1. Open a GitHub issue
2. Attach the crash testcase
3. Include the stderr output
4. Specify the fuzzing target

## Security

For security vulnerabilities found through fuzzing, please report privately to the maintainers.
