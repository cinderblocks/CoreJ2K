# CoreJ2K Fuzzing Guide

**Status**: ?? **Active Fuzzing Infrastructure**  
**Coverage**: Decoder, Encoder, Headers, Markers  
**Framework**: SharpFuzz + AFL/libFuzzer

---

## ?? **Overview**

Fuzzing is an automated testing technique that feeds random/mutated inputs to find crashes, hangs, and unexpected behavior. CoreJ2K includes comprehensive fuzzing infrastructure to discover:

- Buffer overflows
- Integer overflows
- Null dereferences
- Infinite loops
- Memory corruption
- Assertion failures
- Unexpected exceptions

---

## ?? **Quick Start**

### **1. Install Prerequisites**

```powershell
# Install SharpFuzz
dotnet tool install --global SharpFuzz.CommandLine

# Optional: Install AFL.NET for advanced fuzzing
# https://github.com/Metalnem/afl.net
```

### **2. Build Fuzzer**

```powershell
cd CoreJ2K.Fuzz
dotnet build -c Release
```

### **3. Run Fuzzing**

```powershell
# Quick fuzzing (100 iterations)
pwsh Run-Fuzzing.ps1 -Quick

# Full fuzzing (10,000 iterations)
pwsh Run-Fuzzing.ps1

# Specific target
pwsh Run-Fuzzing.ps1 -Target decoder
```

---

## ?? **Fuzzing Targets**

### **1. Decoder (Default)**
Tests JPEG 2000 decoding with random/mutated inputs.

**What it tests**:
- Header parsing (SOC, SIZ, COD, QCD, etc.)
- Tile decoding
- Codestream parsing
- Image reconstruction
- Error handling

**Command**:
```powershell
pwsh Run-Fuzzing.ps1 -Target decoder
```

### **2. Encoder**
Tests JPEG 2000 encoding with random parameters.

**What it tests**:
- Image validation
- Quantization configuration
- Wavelet transform
- Entropy encoding
- Output generation

**Command**:
```powershell
pwsh Run-Fuzzing.ps1 -Target encoder
```

### **3. Headers**
Focuses on header parsing without full decoding.

**What it tests**:
- Main header segments
- Tile-part headers
- Marker segment parsing
- Parameter validation

**Command**:
```powershell
pwsh Run-Fuzzing.ps1 -Target headers
```

### **4. Markers**
Tests individual marker segment parsers.

**What it tests**:
- SIZ (Image and tile size)
- COD (Coding style default)
- QCD (Quantization default)
- SOT (Start of tile-part)
- And all other markers

**Command**:
```powershell
pwsh Run-Fuzzing.ps1 -Target markers
```

---

## ?? **Directory Structure**

```
CoreJ2K.Fuzz/
??? Program.cs              # Main fuzzing harness
??? Run-Fuzzing.ps1         # PowerShell fuzzing script
??? Testcases/              # Seed corpus (valid JPEG 2000 files)
?   ??? seed.j2k
?   ??? test1.j2k
?   ??? ...
??? Findings/               # Crashes and hangs discovered
    ??? crash_001.bin
    ??? crash_001.stderr.txt
    ??? hang_042.bin
```

---

## ?? **Advanced Usage**

### **With AFL (Recommended)**

AFL (American Fuzzy Lop) provides coverage-guided fuzzing for maximum effectiveness.

```powershell
# Instrument CoreJ2K
sharpfuzz CoreJ2K.Fuzz\bin\Release\net8.0\CoreJ2K.dll

# Run AFL
afl-fuzz -i Testcases -o Findings -t 5000 -m 2048 -- CoreJ2K.Fuzz.exe decoder
```

### **With libFuzzer**

```powershell
# Build with libFuzzer support
dotnet build -c Release /p:EnableLibFuzzer=true

# Run
dotnet run -c Release -- -fuzz=decoder -runs=100000
```

### **Continuous Fuzzing**

For long-running fuzzing campaigns:

```powershell
# Run in background (PowerShell)
Start-Job -ScriptBlock {
    cd C:\path\to\CoreJ2K.Fuzz
    pwsh Run-Fuzzing.ps1 -Iterations 1000000
}

# Check status
Get-Job

# Get results
Receive-Job -Id 1
```

---

## ?? **Analyzing Results**

### **Crashes**

When a crash is found:

```powershell
# Reproduce the crash
$crashFile = "Findings\crash_042.bin"
Get-Content $crashFile -AsByteStream | .\bin\Release\net8.0\CoreJ2K.Fuzz.exe decoder

# View stderr
Get-Content "Findings\crash_042.stderr.txt"
```

**Expected Output**:
```
UNEXPECTED EXCEPTION: NullReferenceException
Message: Object reference not set to an instance of an object
Stack: at CoreJ2K.J2kImage.Decode(...)
```

### **Minimization**

Minimize crash testcases to simplest form:

```powershell
# Manual minimization
$bytes = [System.IO.File]::ReadAllBytes("crash_042.bin")

# Try removing bytes from end
while ($bytes.Length -gt 10) {
    $bytes = $bytes[0..($bytes.Length - 2)]
    [System.IO.File]::WriteAllBytes("minimal.bin", $bytes)
    
    # Test if still crashes
    $result = & .\bin\Release\net8.0\CoreJ2K.Fuzz.exe decoder < minimal.bin
    if ($LASTEXITCODE -eq 0) {
        # Stopped crashing, restore last byte
        break
    }
}
```

### **Reporting**

Create GitHub issue with:
1. Crash testcase (attach file)
2. Stack trace from stderr
3. Fuzzing target
4. Environment details

---

## ?? **Seed Corpus**

Good seed corpus improves fuzzing effectiveness.

### **Sources**:

1. **OpenJPEG Test Suite** (Public domain)
   ```powershell
   # Download
   Invoke-WebRequest -Uri "https://github.com/uclouvain/openjpeg-data/archive/master.zip" -OutFile "openjpeg-data.zip"
   Expand-Archive openjpeg-data.zip
   
   # Copy to testcases
   Copy-Item openjpeg-data\input\conformance\*.j2k Testcases\
   ```

2. **JPEG 2000 Conformance Test Suite**
   - ISO/IEC 15444-4 test images
   - Available from ISO or JPEG.org

3. **Real-world Images**
   - Medical images (DICOM with JPEG 2000)
   - Satellite imagery
   - Digital cinema (DCP)

### **Creating Seeds**:

```csharp
// Create minimal seed programmatically
var image = new InterleavedImage(16, 16, 3, new[] { 8, 8, 8 });
// Fill with test pattern
for (int i = 0; i < image.Data.Length; i++)
    image.Data[i] = (i * 37) % 256;

var j2k = J2kImage.FromImage(image);
File.WriteAllBytes("Testcases/minimal.j2k", j2k.ToBytes());
```

---

## ?? **CI Integration**

Fuzzing runs automatically on:
- **Weekly**: Every Sunday at 2 AM UTC
- **Manual**: Via GitHub Actions workflow dispatch

**View Results**:
1. Go to Actions ? Fuzzing
2. Download artifacts
3. Check for crash_*.bin files

---

## ?? **Coverage Analysis**

Track code coverage during fuzzing:

```powershell
# Build with coverage
dotnet build -c Release /p:CollectCoverage=true

# Run fuzzing
pwsh Run-Fuzzing.ps1 -Quick

# Generate coverage report
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report
```

---

## ?? **Expected Exceptions**

Some exceptions are expected and handled:

| Exception | Reason | Status |
|-----------|--------|--------|
| `ArgumentException` | Invalid parameters | ? Expected |
| `InvalidOperationException` | Malformed data | ? Expected |
| `OutOfMemoryException` | Huge allocation attempt | ? Expected |
| `NotSupportedException` | Unsupported feature | ? Expected |
| `IOException` | Truncated file | ? Expected |
| `EndOfStreamException` | Unexpected EOF | ? Expected |

### **Unexpected Exceptions** (Should report):

| Exception | Severity | Action |
|-----------|----------|--------|
| `NullReferenceException` | ?? CRITICAL | Report immediately |
| `IndexOutOfRangeException` | ?? CRITICAL | Report immediately |
| `DivideByZeroException` | ?? CRITICAL | Report immediately |
| `StackOverflowException` | ?? CRITICAL | Report immediately |
| `AccessViolationException` | ?? CRITICAL | Report immediately |

---

## ?? **Tips for Effective Fuzzing**

### **1. Start with Valid Seeds**
Mutating valid JPEG 2000 files finds bugs faster than random bytes.

### **2. Use Multiple Targets**
Different targets exercise different code paths.

### **3. Monitor Memory**
Set reasonable memory limits to catch allocation bombs:
```powershell
pwsh Run-Fuzzing.ps1 -MemoryLimit 2048  # 2GB
```

### **4. Adjust Timeout**
Balance between thoroughness and speed:
```powershell
pwsh Run-Fuzzing.ps1 -Timeout 10000  # 10 seconds
```

### **5. Review All Crashes**
Even expected exceptions might reveal edge cases worth fixing.

---

## ?? **Security Fuzzing**

Focus areas for security:

### **1. Integer Overflows**
- Image dimensions (width × height)
- Tile calculations
- Buffer allocations

**Test with**: Large dimension values in headers

### **2. Buffer Overruns**
- Marker segment lengths
- Code-block data
- Array indexing

**Test with**: Invalid lengths and indices

### **3. Infinite Loops**
- Recursive structures
- Tile iteration
- Subband traversal

**Test with**: Circular references in data

### **4. Memory Exhaustion**
- Huge image dimensions
- Deep wavelet decomposition
- Many tiles

**Test with**: Extreme parameter values

---

## ?? **Resources**

### **Fuzzing Frameworks**:
- [SharpFuzz](https://github.com/Metalnem/sharpfuzz) - .NET fuzzing
- [AFL](https://github.com/google/AFL) - Coverage-guided fuzzer
- [libFuzzer](https://llvm.org/docs/LibFuzzer.html) - LLVM fuzzer

### **JPEG 2000 Specifications**:
- ISO/IEC 15444-1 (Core coding system)
- ISO/IEC 15444-4 (Conformance testing)

### **Related**:
- [OSS-Fuzz](https://github.com/google/oss-fuzz) - Continuous fuzzing
- [ClusterFuzz](https://google.github.io/clusterfuzz/) - Scalable fuzzing

---

## ?? **Success Metrics**

**Good fuzzing session**:
- ? No unexpected crashes
- ? All exceptions are expected types
- ? No hangs (infinite loops)
- ? Code coverage >80% of fuzzed paths

**Findings to fix**:
- ? Any `NullReferenceException`
- ? Any `IndexOutOfRangeException`
- ? Any `StackOverflowException`
- ? Any hangs >5 seconds

---

**Status**: Ready for production fuzzing  
**Confidence**: High (after fixing all P0-P1 bugs)  
**Next**: Integrate with OSS-Fuzz for continuous fuzzing

---

**Related Documents**:
- [Bug Fixes Summary](../BUG_FIXES_SUMMARY.md)
- [Integer Truncation Fixes](../INTEGER_TRUNCATION_FIXES.md)
- [Null Dereference Fixes](../NULL_DEREFERENCE_FIXES.md)
