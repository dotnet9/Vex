@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\Vex\Vex.csproj"
set "PACKAGE_AFTER=false"

if not "%~1"=="" (
    if /I "%~1"=="--package" (
        set "PACKAGE_AFTER=true"
    ) else (
        echo Usage: publish_all.bat [--package]
        exit /b 2
    )
)

echo Publishing Vex profiles...

call :publish FolderProfile__win-x64 net10.0-windows win-x64 || exit /b 1
call :publish FolderProfile__linux-x64 net10.0 linux-x64 || exit /b 1
call :publish FolderProfile__linux-arm64 net10.0 linux-arm64 || exit /b 1
call :publish FolderProfile__osx-x64 net10.0 osx-x64 || exit /b 1
call :publish FolderProfile__osx-arm64 net10.0 osx-arm64 || exit /b 1

echo All Vex publish profiles completed.

if /I "%PACKAGE_AFTER%"=="true" (
    echo.
    echo Packaging Vex release artifacts...
    powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\package_vex_artifacts.ps1" || exit /b 1
)

exit /b 0

:publish
echo.
echo === %~1 ===
dotnet publish "%PROJECT%" -c Release -f %~2 -r %~3 /p:PublishProfile=%~1
exit /b %ERRORLEVEL%
