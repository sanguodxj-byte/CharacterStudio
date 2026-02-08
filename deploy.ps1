# ============================================
# CharacterStudio Deploy Script (PowerShell)
# Deploy mod to RimWorld Mods directory
# ============================================

param(
    [string]$RimWorldMods = "D:\steam\steamapps\common\RimWorld\Mods",
    [switch]$NoBuild,
    [switch]$Clean,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ModName = "CharacterStudio"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$TargetDir = Join-Path $RimWorldMods $ModName

function Write-Step {
    param([string]$Message)
    Write-Host "`n[*] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "[X] $Message" -ForegroundColor Red
}

# ============================================
# Start Deploy
# ============================================

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "   CharacterStudio Deploy Tool" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

# Check RimWorld Mods directory
Write-Step "Checking RimWorld Mods directory..."
if (-not (Test-Path $RimWorldMods)) {
    Write-Err "RimWorld Mods directory not found: $RimWorldMods"
    Write-Host "Please use -RimWorldMods parameter to specify the correct path"
    exit 1
}
Write-Success "Found Mods directory: $RimWorldMods"

# Check source directory
$AboutXml = Join-Path $ScriptDir "About\About.xml"
if (-not (Test-Path $AboutXml)) {
    Write-Err "Cannot find About.xml, please run this script from mod root directory"
    exit 1
}

# Build project
if (-not $NoBuild) {
    Write-Step "Building project..."
    $CsprojPath = Join-Path $ScriptDir "Source\CharacterStudio\CharacterStudio.csproj"
    
    if (Test-Path $CsprojPath) {
        try {
            Push-Location (Split-Path $CsprojPath -Parent)
            $buildOutput = & dotnet build -c Release 2>&1
            
            if ($LASTEXITCODE -ne 0) {
                Write-Err "Build failed"
                if ($Verbose) {
                    Write-Host $buildOutput
                }
                Pop-Location
                exit 1
            }
            Write-Success "Build successful"
            Pop-Location
        }
        catch {
            Write-Warn "Build error: $_"
            Write-Warn "Please ensure .NET SDK is installed"
            Pop-Location
        }
    }
    else {
        Write-Warn "Project file not found, skipping build"
    }
}

# Prepare target directory
Write-Step "Preparing target directory..."
if (Test-Path $TargetDir) {
    if ($Clean) {
        Write-Host "  Removing old version..."
        Remove-Item -Path $TargetDir -Recurse -Force
    }
    else {
        Write-Host "  Updating existing installation..."
    }
}
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}
Write-Success "Target directory: $TargetDir"

# Define directories to copy
$Directories = @(
    @{ Name = "About"; Required = $true },
    @{ Name = "1.6"; Required = $false },
    @{ Name = "1.5"; Required = $false },
    @{ Name = "Languages"; Required = $false },
    @{ Name = "Defs"; Required = $false },
    @{ Name = "Textures"; Required = $false },
    @{ Name = "Patches"; Required = $false }
)

# Copy files
Write-Step "Copying mod files..."

# Ensure 1.5 directory exists by copying 1.6 if needed
$Source16 = Join-Path $ScriptDir "1.6"
$Source15 = Join-Path $ScriptDir "1.5"
if ((Test-Path $Source16) -and (-not (Test-Path $Source15))) {
    Write-Host "  Creating 1.5 compatibility folder..."
    Copy-Item -Path $Source16 -Destination $Source15 -Recurse -Force
}

foreach ($dir in $Directories) {
    $sourcePath = Join-Path $ScriptDir $dir.Name
    
    if (Test-Path $sourcePath) {
        Write-Host "  Copying $($dir.Name)..."
        Copy-Item -Path $sourcePath -Destination $TargetDir -Recurse -Force
    }
    elseif ($dir.Required) {
        Write-Err "Required directory not found: $($dir.Name)"
        exit 1
    }
}

# Verify deployment
Write-Step "Verifying deployment..."
$errors = @()
$warnings = @()

# Check required files
$requiredFiles = @(
    "About\About.xml"
)

foreach ($file in $requiredFiles) {
    $filePath = Join-Path $TargetDir $file
    if (-not (Test-Path $filePath)) {
        $errors += "Missing file: $file"
    }
}

# Check DLL
$dllPath = Join-Path $TargetDir "1.6\Assemblies\CharacterStudio.dll"
if (-not (Test-Path $dllPath)) {
    $warnings += "DLL not found, mod may not work correctly"
}
else {
    $dllInfo = Get-Item $dllPath
    Write-Host "  DLL Size: $([math]::Round($dllInfo.Length / 1KB, 2)) KB"
    Write-Host "  Modified: $($dllInfo.LastWriteTime)"
}

# Output results
if ($errors.Count -gt 0) {
    Write-Host ""
    foreach ($err in $errors) {
        Write-Err $err
    }
    Write-Host "`nDeployment verification failed" -ForegroundColor Red
    exit 1
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    foreach ($warn in $warnings) {
        Write-Warn $warn
    }
}

# Success
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "   Deploy Successful!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nMod deployed to: $TargetDir"
Write-Host "Please restart RimWorld to load the new version`n"

# Statistics
$totalFiles = (Get-ChildItem -Path $TargetDir -Recurse -File).Count
$totalSize = [math]::Round((Get-ChildItem -Path $TargetDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1KB, 2)
Write-Host "Total files: $totalFiles"
Write-Host "Total size: $totalSize KB"
Write-Host ""