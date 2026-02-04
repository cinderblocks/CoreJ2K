# CoreJ2K Fuzzing Script for Windows
# Requires: SharpFuzz, AFL.NET, or libFuzzer

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("decoder", "encoder", "headers", "markers")]
    [string]$Target = "decoder",
    
    [Parameter(Mandatory=$false)]
    [string]$TestcasesDir = ".\Testcases",
    
    [Parameter(Mandatory=$false)]
    [string]$FindingsDir = ".\Findings",
    
    [Parameter(Mandatory=$false)]
    [int]$Timeout = 5000, # 5 seconds
    
    [Parameter(Mandatory=$false)]
    [int]$MemoryLimit = 2048, # 2GB in MB
    
    [Parameter(Mandatory=$false)]
    [switch]$Quick # Quick mode: fewer iterations
)

Write-Host "CoreJ2K Fuzzing Script" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check if SharpFuzz is installed
$sharpFuzzInstalled = Get-Command "sharpfuzz" -ErrorAction SilentlyContinue
if (-not $sharpFuzzInstalled) {
    Write-Host "Installing SharpFuzz..." -ForegroundColor Yellow
    dotnet tool install --global SharpFuzz.CommandLine
}

# Build the project
Write-Host "Building CoreJ2K.Fuzz..." -ForegroundColor Yellow
Push-Location $PSScriptRoot
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
} finally {
    Pop-Location
}

# Instrument CoreJ2K.dll for fuzzing
Write-Host "Instrumenting CoreJ2K.dll with SharpFuzz..." -ForegroundColor Yellow
$dllPath = Join-Path $PSScriptRoot "bin\Release\net8.0\CoreJ2K.dll"
sharpfuzz $dllPath

# Create directories
New-Item -ItemType Directory -Force -Path $TestcasesDir | Out-Null
New-Item -ItemType Directory -Force -Path $FindingsDir | Out-Null

# Check for test cases
$testcaseCount = (Get-ChildItem -Path $TestcasesDir -File -ErrorAction SilentlyContinue).Count
if ($testcaseCount -eq 0) {
    Write-Host "No testcases found. Creating minimal testcase..." -ForegroundColor Yellow
    
    # Create a minimal valid JPEG 2000 file as seed
    $minimalJ2K = @(
        0xFF, 0x4F,           # SOC marker
        0xFF, 0x51,           # SIZ marker
        0x00, 0x29,           # Lsiz (length)
        0x00, 0x00,           # Rsiz (capabilities)
        0x00, 0x00, 0x00, 0x10,  # Xsiz (width = 16)
        0x00, 0x00, 0x00, 0x10,  # Ysiz (height = 16)
        0x00, 0x00, 0x00, 0x00,  # XOsiz
        0x00, 0x00, 0x00, 0x00,  # YOsiz
        0x00, 0x00, 0x00, 0x10,  # XTsiz
        0x00, 0x00, 0x00, 0x10,  # YTsiz
        0x00, 0x00, 0x00, 0x00,  # XTOsiz
        0x00, 0x00, 0x00, 0x00,  # YTOsiz
        0x00, 0x01,              # Csiz (components = 1)
        0x08, 0x00, 0x00,        # Component 0
        0xFF, 0xD9               # EOC marker
    )
    
    $seedPath = Join-Path $TestcasesDir "seed.j2k"
    [System.IO.File]::WriteAllBytes($seedPath, [byte[]]$minimalJ2K)
    Write-Host "Created minimal seed testcase: $seedPath" -ForegroundColor Green
}

# Determine iterations
$iterations = if ($Quick) { 100 } else { 10000 }

Write-Host ""
Write-Host "Fuzzing Configuration:" -ForegroundColor Cyan
Write-Host "  Target: $Target" -ForegroundColor White
Write-Host "  Testcases: $TestcasesDir" -ForegroundColor White
Write-Host "  Findings: $FindingsDir" -ForegroundColor White
Write-Host "  Timeout: ${Timeout}ms" -ForegroundColor White
Write-Host "  Memory Limit: ${MemoryLimit}MB" -ForegroundColor White
Write-Host "  Iterations: $iterations" -ForegroundColor White
Write-Host ""

# Check if AFL is available (optional, advanced)
$aflPath = Get-Command "afl-fuzz" -ErrorAction SilentlyContinue
if ($aflPath) {
    Write-Host "AFL detected. Running AFL fuzzing..." -ForegroundColor Green
    
    $exePath = Join-Path $PSScriptRoot "bin\Release\net8.0\CoreJ2K.Fuzz.exe"
    
    # AFL command
    afl-fuzz -i $TestcasesDir -o $FindingsDir -t $Timeout -m $MemoryLimit -- $exePath $Target
} else {
    # Fallback: Simple random fuzzing
    Write-Host "AFL not found. Running simple mutation fuzzing..." -ForegroundColor Yellow
    Write-Host "For better results, install AFL.NET: https://github.com/Metalnem/afl.net" -ForegroundColor Yellow
    Write-Host ""
    
    # Simple mutation-based fuzzing
    $exePath = Join-Path $PSScriptRoot "bin\Release\net8.0\CoreJ2K.Fuzz.exe"
    
    Write-Host "Running $iterations fuzzing iterations..." -ForegroundColor Cyan
    
    $crashes = 0
    $hangs = 0
    $exceptions = 0
    
    for ($i = 0; $i -lt $iterations; $i++) {
        # Select random testcase
        $testcases = Get-ChildItem -Path $TestcasesDir -File
        $seedFile = Get-Random -InputObject $testcases
        
        # Create mutated version
        $mutatedPath = Join-Path $env:TEMP "fuzz_input_${i}.bin"
        
        # Simple mutation: flip random bytes
        $bytes = [System.IO.File]::ReadAllBytes($seedFile.FullName)
        $mutations = [Math]::Min(10, $bytes.Length / 10)
        
        for ($m = 0; $m -lt $mutations; $m++) {
            $pos = Get-Random -Maximum $bytes.Length
            $bytes[$pos] = Get-Random -Maximum 256
        }
        
        [System.IO.File]::WriteAllBytes($mutatedPath, $bytes)
        
        # Run fuzzer
        try {
            $process = Start-Process -FilePath $exePath `
                -ArgumentList $Target `
                -RedirectStandardInput $mutatedPath `
                -RedirectStandardError (Join-Path $env:TEMP "fuzz_stderr.txt") `
                -NoNewWindow `
                -PassThru
            
            $completed = $process.WaitForExit($Timeout)
            
            if (-not $completed) {
                # Hang detected
                $hangs++
                $process.Kill()
                Write-Host "HANG detected at iteration ${i}" -ForegroundColor Red
                
                $hangPath = Join-Path $FindingsDir "hang_${i}.bin"
                Copy-Item $mutatedPath $hangPath
            }
            elseif ($process.ExitCode -ne 0) {
                # Crash or exception
                $crashes++
                Write-Host "CRASH detected at iteration ${i} (exit code: $($process.ExitCode))" -ForegroundColor Red
                
                $crashPath = Join-Path $FindingsDir "crash_${i}.bin"
                Copy-Item $mutatedPath $crashPath
                
                # Copy stderr
                $stderrPath = Join-Path $FindingsDir "crash_${i}.stderr.txt"
                Copy-Item (Join-Path $env:TEMP "fuzz_stderr.txt") $stderrPath -ErrorAction SilentlyContinue
            }
        }
        catch {
            $exceptions++
            Write-Host "EXCEPTION at iteration ${i}: $($_.Exception.Message)" -ForegroundColor Red
        }
        finally {
            Remove-Item $mutatedPath -ErrorAction SilentlyContinue
        }
        
        # Progress
        if (($i + 1) % 100 -eq 0) {
            $percent = [Math]::Round((($i + 1) / $iterations) * 100, 1)
            Write-Host "Progress: $($i + 1)/$iterations ($percent%) - Crashes: $crashes, Hangs: $hangs" -ForegroundColor Cyan
        }
    }
    
    Write-Host ""
    Write-Host "Fuzzing Complete!" -ForegroundColor Green
    Write-Host "  Total Iterations: $iterations" -ForegroundColor White
    Write-Host "  Crashes Found: $crashes" -ForegroundColor $(if ($crashes -gt 0) { "Red" } else { "Green" })
    Write-Host "  Hangs Found: $hangs" -ForegroundColor $(if ($hangs -gt 0) { "Red" } else { "Green" })
    Write-Host "  Exceptions: $exceptions" -ForegroundColor $(if ($exceptions -gt 0) { "Yellow" } else { "Green" })
    Write-Host ""
    
    if ($crashes -gt 0 -or $hangs -gt 0) {
        Write-Host "Check findings in: $FindingsDir" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Fuzzing session complete." -ForegroundColor Cyan
