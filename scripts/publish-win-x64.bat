@echo off
rem Publish win-x64 NativeAOT to artifacts\publish\win-x64\Vex\ (no installer).
rem Double-click to run; extra args are forwarded, e.g.:
rem   publish-win-x64.bat -Version 2.0.0.0
setlocal
set "SCRIPT_DIR=%~dp0"

echo [Vex] Publishing win-x64 NativeAOT ...
where pwsh >nul 2>nul && (set "PS=pwsh") || (set "PS=powershell")
%PS% -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%publish.ps1" -RuntimeIdentifier win-x64 %*
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
    echo.
    echo [FAILED] publish exit code %EXITCODE%
) else (
    echo.
    echo [OK] publish output: repo root\artifacts\publish\win-x64\Vex\
)
pause
exit /b %EXITCODE%
