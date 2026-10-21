@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\Vex\Vex.csproj"

rem ILC 在多核机器（>=32 逻辑核）上线程池会触发 fail-fast 崩溃，限核规避（2026-10-06 验证）
set "DOTNET_PROCESSOR_COUNT=4"
rem NuGetAudit 默认联网审计在代理环境下会卡死 restore，本地发布关闭
set "NUGET_AUDIT=false"

if not "%~1"=="" (
    echo Usage: publish_all.bat
    echo Release installers are built by GitHub Actions. See docs/README.md.
    exit /b 2
)

echo Publishing Vex profiles...

call :publish FolderProfile__win-x64 net10.0-windows win-x64 || exit /b 1
call :publish FolderProfile__linux-x64 net10.0 linux-x64 || exit /b 1
call :publish FolderProfile__linux-arm64 net10.0 linux-arm64 || exit /b 1
call :publish FolderProfile__osx-x64 net10.0 osx-x64 || exit /b 1
call :publish FolderProfile__osx-arm64 net10.0 osx-arm64 || exit /b 1

echo All Vex publish profiles completed.

exit /b 0

:publish
echo.
echo === %~1 ===
pwsh -NoLogo -NoProfile -File "%ROOT%scripts\publish.ps1" -RuntimeIdentifier %~3
exit /b %ERRORLEVEL%
