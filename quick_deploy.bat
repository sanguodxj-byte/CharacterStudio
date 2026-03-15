@echo off
chcp 65001 >nul
setlocal

set "MOD_NAME=CharacterStudio"
set "RIMWORLD_MODS=D:\steam\steamapps\common\RimWorld\Mods"
set "SOURCE_DIR=%~dp0"
set "TARGET_DIR=%RIMWORLD_MODS%\%MOD_NAME%"
set "DLL_SOURCE=%SOURCE_DIR%1.6\Assemblies\CharacterStudio.dll"
set "DLL_TARGET=%TARGET_DIR%\1.6\Assemblies\CharacterStudio.dll"

echo.
echo ========================================
echo   CharacterStudio 快速部署工具
echo ========================================
echo.

:: Step 1: 编译
echo [1/3] 编译 Release...
pushd "%SOURCE_DIR%Source\CharacterStudio"
dotnet build -c Release --nologo
if errorlevel 1 (
    echo [错误] 编译失败
    popd
    pause
    exit /b 1
)
popd
echo 编译成功！
echo.

:: Step 2: 检查游戏是否在运行
echo [2/3] 检查 RimWorld 是否运行中...
tasklist /FI "IMAGENAME eq RimWorldWin64.exe" 2>nul | find /I "RimWorldWin64.exe" >nul
if not errorlevel 1 (
    echo.
    echo [警告] 检测到 RimWorld 正在运行！
    echo 请关闭 RimWorld 后按任意键继续部署...
    echo 或按 Ctrl+C 取消。
    echo.
    pause
)

:: 再次检查
tasklist /FI "IMAGENAME eq RimWorldWin64.exe" 2>nul | find /I "RimWorldWin64.exe" >nul
if not errorlevel 1 (
    echo [错误] RimWorld 仍在运行，无法覆盖 DLL，部署取消。
    pause
    exit /b 1
)

:: Step 3: 复制文件
echo [3/3] 复制模组文件...

if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

xcopy "%SOURCE_DIR%About" "%TARGET_DIR%\About\" /e /i /y /q >nul
if exist "%SOURCE_DIR%1.6" xcopy "%SOURCE_DIR%1.6" "%TARGET_DIR%\1.6\" /e /i /y /q >nul
if exist "%SOURCE_DIR%Languages" xcopy "%SOURCE_DIR%Languages" "%TARGET_DIR%\Languages\" /e /i /y /q >nul
if exist "%SOURCE_DIR%Defs" xcopy "%SOURCE_DIR%Defs" "%TARGET_DIR%\Defs\" /e /i /y /q >nul
if exist "%SOURCE_DIR%Textures" xcopy "%SOURCE_DIR%Textures" "%TARGET_DIR%\Textures\" /e /i /y /q >nul

echo.
echo ========================================
echo   部署成功！
echo ========================================
echo.
echo 模组已部署到: %TARGET_DIR%
echo 重启 RimWorld 加载新版本。
echo.
pause
