# CharacterStudio quick build and deploy script
# Run this after code changes

$ErrorActionPreference = 'Stop'
$ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path
$SRC = Join-Path $ROOT 'Source\CharacterStudio'
$TARGET_ASSEMBLIES = Join-Path $ROOT '1.6\Assemblies'
$RIMWORLD_MODS_ROOT = 'D:\steam\steamapps\common\RimWorld\Mods'
$RIMWORLD_MOD = Join-Path $RIMWORLD_MODS_ROOT 'CharacterStudio'

Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host '  CharacterStudio Build + Deploy' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''

# 1. Build
Write-Host '[1/3] Building Release...' -ForegroundColor Yellow
Push-Location $SRC
try {
    & dotnet build CharacterStudio.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit $LASTEXITCODE)"
    }
}
finally {
    Pop-Location
}
Write-Host '      Build succeeded' -ForegroundColor Green

# 2. Copy DLL to 1.6/Assemblies
Write-Host '[2/3] Copying DLL...' -ForegroundColor Yellow
$releaseDir = Join-Path $SRC 'obj\Release'
$dll = Get-ChildItem -Path $releaseDir -Filter 'CharacterStudio.dll' -Recurse -File | Select-Object -First 1
if ($null -eq $dll) {
    $binDir = Join-Path $SRC 'bin\Release'
    $dll = Get-ChildItem -Path $binDir -Filter 'CharacterStudio.dll' -Recurse -File | Select-Object -First 1
}
if ($null -eq $dll) {
    throw 'Could not find build output CharacterStudio.dll'
}

if (-not (Test-Path $TARGET_ASSEMBLIES)) {
    New-Item -ItemType Directory -Path $TARGET_ASSEMBLIES -Force | Out-Null
}
$localDllTarget = Join-Path $TARGET_ASSEMBLIES 'CharacterStudio.dll'
Copy-Item -Path $dll.FullName -Destination $localDllTarget -Force
Write-Host "      Copied: $($dll.FullName)" -ForegroundColor Green
Write-Host "         -> $localDllTarget"

# 3. Deploy to RimWorld Mods
Write-Host '[3/3] Deploying to RimWorld...' -ForegroundColor Yellow
if (-not (Test-Path $RIMWORLD_MODS_ROOT)) {
    Write-Host '      [skip] RimWorld Mods directory does not exist' -ForegroundColor DarkGray
}
else {
    $dirs = @('About', '1.6\Assemblies', 'Languages', 'Defs', 'Textures')
    foreach ($d in $dirs) {
        $src = Join-Path $ROOT $d
        $dst = Join-Path $RIMWORLD_MOD $d
        if (Test-Path $src) {
            if (-not (Test-Path $dst)) {
                New-Item -ItemType Directory -Path $dst -Force | Out-Null
            }

            $srcWildcard = Join-Path $src '*'
            Copy-Item -Path $srcWildcard -Destination $dst -Recurse -Force
            Write-Host "      $d" -ForegroundColor DarkGray
        }
    }

    Write-Host "      Deploy complete: $RIMWORLD_MOD" -ForegroundColor Green
}

Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host '  Done! Restart RimWorld to load update' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''