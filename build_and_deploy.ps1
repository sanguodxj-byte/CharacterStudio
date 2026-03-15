# CharacterStudio 快速编译部署脚本
# 每次代码修改后运行此脚本

$ErrorActionPreference = "Stop"
$ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path
$SRC  = Join-Path $ROOT "Source\CharacterStudio"
$TARGET_ASSEMBLIES = Join-Path $ROOT "1.6\Assemblies"
$RIMWORLD_MOD = "D:\steam\steamapps\common\RimWorld\Mods\CharacterStudio"

Write-Host "" 
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CharacterStudio 编译 + 部署" -ForegroundColor Cyan
Write-Host "========================================"
Write-Host ""

# 1. 编译
Write-Host "[1/3] 编译 Release..." -ForegroundColor Yellow
Push-Location $SRC
try {
    & dotnet build -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}
Write-Host "      编译成功" -ForegroundColor Green

# 2. 复制 DLL 到 1.6/Assemblies
Write-Host "[2/3] 复制 DLL..." -ForegroundColor Yellow
$releaseDir = Join-Path $SRC "obj\Release"
$dll = Get-ChildItem $releaseDir -Filter "CharacterStudio.dll" -Recurse | Select-Object -First 1
if ($null -eq $dll) {
    # 也尝试 bin/Release
    $binDir = Join-Path $SRC "bin\Release"
    $dll = Get-ChildItem $binDir -Filter "CharacterStudio.dll" -Recurse | Select-Object -First 1
}
if ($null -eq $dll) { throw "找不到编译产物 CharacterStudio.dll" }

if (-not (Test-Path $TARGET_ASSEMBLIES)) { New-Item -ItemType Directory -Path $TARGET_ASSEMBLIES | Out-Null }
Copy-Item $dll.FullName -Destination (Join-Path $TARGET_ASSEMBLIES "CharacterStudio.dll") -Force
Write-Host "      已复制: $($dll.FullName)" -ForegroundColor Green
Write-Host "         -> $TARGET_ASSEMBLIES\CharacterStudio.dll"

# 3. 部署到游戏目录
Write-Host "[3/3] 部署到 RimWorld..." -ForegroundColor Yellow
if (-not (Test-Path "D:\steam\steamapps\common\RimWorld\Mods")) {
    Write-Host "      [跳过] RimWorld Mods 目录不存在" -ForegroundColor DarkGray
} else {
    # 确保目标目录存在
    $dirs = @("About", "1.6\Assemblies", "Languages", "Defs", "Textures")
    foreach ($d in $dirs) {
        $src = Join-Path $ROOT $d
        $dst = Join-Path $RIMWORLD_MOD $d
        if (Test-Path $src) {
            if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }
            Copy-Item "$src\*" -Destination $dst -Recurse -Force
            Write-Host "      $d" -ForegroundColor DarkGray
        }
    }
    Write-Host "      部署完成: $RIMWORLD_MOD" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  完成！重启 RimWorld 加载新版本" -ForegroundColor Cyan  
Write-Host "========================================"
Write-Host ""
