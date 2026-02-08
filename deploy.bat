@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ============================================
:: TheSecondSeat.Studio 部署脚本
:: 将模组部署到 RimWorld Mods 目录
:: ============================================

:: 配置
set "MOD_NAME=CharacterStudio"
set "RIMWORLD_MODS=D:\steam\steamapps\common\RimWorld\Mods"
set "SOURCE_DIR=%~dp0"
set "TARGET_DIR=%RIMWORLD_MODS%\%MOD_NAME%"

echo.
echo ========================================
echo   CharacterStudio 部署工具
echo ========================================
echo.

:: 检查 RimWorld Mods 目录是否存在
if not exist "%RIMWORLD_MODS%" (
    echo [错误] RimWorld Mods 目录不存在: %RIMWORLD_MODS%
    echo 请检查 RimWorld 安装路径是否正确
    pause
    exit /b 1
)

:: 检查源目录
if not exist "%SOURCE_DIR%About\About.xml" (
    echo [错误] 无法找到模组文件，请确保在模组根目录运行此脚本
    pause
    exit /b 1
)

:: 编译项目（如果存在 csproj）
echo [1/4] 检查是否需要编译...
if exist "%SOURCE_DIR%Source\CharacterStudio\CharacterStudio.csproj" (
    echo 正在编译项目...
    
    :: 检查 dotnet 是否可用
    where dotnet >nul 2>&1
    if errorlevel 1 (
        echo [警告] 未找到 dotnet CLI，跳过编译步骤
        echo 请确保 1.6\Assemblies\CharacterStudio.dll 已是最新版本
    ) else (
        pushd "%SOURCE_DIR%Source\CharacterStudio"
        dotnet build -c Release
        if errorlevel 1 (
            echo [错误] 编译失败
            popd
            pause
            exit /b 1
        )
        popd
        echo 编译成功！
    )
) else (
    echo 未找到项目文件，跳过编译
)

:: 清理目标目录
echo.
echo [2/4] 准备目标目录...
if exist "%TARGET_DIR%" (
    echo 删除旧版本: %TARGET_DIR%
    rmdir /s /q "%TARGET_DIR%"
)
mkdir "%TARGET_DIR%"

:: 复制文件
echo.
echo [3/4] 复制模组文件...

:: 复制 About 目录
echo   复制 About...
xcopy "%SOURCE_DIR%About" "%TARGET_DIR%\About\" /e /i /q >nul

:: 复制 1.6 目录 (包含编译后的 DLL)
if exist "%SOURCE_DIR%1.6" (
    echo   复制 1.6...
    xcopy "%SOURCE_DIR%1.6" "%TARGET_DIR%\1.6\" /e /i /q >nul
)

:: 复制 Languages 目录
if exist "%SOURCE_DIR%Languages" (
    echo   复制 Languages...
    xcopy "%SOURCE_DIR%Languages" "%TARGET_DIR%\Languages\" /e /i /q >nul
)

:: 复制 Defs 目录（如果存在）
if exist "%SOURCE_DIR%Defs" (
    echo   复制 Defs...
    xcopy "%SOURCE_DIR%Defs" "%TARGET_DIR%\Defs\" /e /i /q >nul
)

:: 复制 Textures 目录（如果存在）
if exist "%SOURCE_DIR%Textures" (
    echo   复制 Textures...
    xcopy "%SOURCE_DIR%Textures" "%TARGET_DIR%\Textures\" /e /i /q >nul
)

:: 复制 Patches 目录（如果存在）
if exist "%SOURCE_DIR%Patches" (
    echo   复制 Patches...
    xcopy "%SOURCE_DIR%Patches" "%TARGET_DIR%\Patches\" /e /i /q >nul
)

:: 验证部署
echo.
echo [4/4] 验证部署...
set "DEPLOY_OK=1"

if not exist "%TARGET_DIR%\About\About.xml" (
    echo [错误] About.xml 未找到
    set "DEPLOY_OK=0"
)

if not exist "%TARGET_DIR%\1.6\Assemblies\CharacterStudio.dll" (
    echo [警告] DLL 未找到，模组可能无法正常工作
)

if "%DEPLOY_OK%"=="1" (
    echo.
    echo ========================================
    echo   部署成功！
    echo ========================================
    echo.
    echo 模组已部署到: %TARGET_DIR%
    echo.
    echo 请重启 RimWorld 以加载新版本
    echo.
) else (
    echo.
    echo [错误] 部署验证失败
)

pause