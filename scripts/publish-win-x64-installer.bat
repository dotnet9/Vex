@echo off
rem Build the win-x64 AOT installer and portable zip (with SHA-256) into artifacts\release\.
rem Includes the NativeAOT publish and Inno Setup 6 packaging; takes a few minutes.
rem Double-click to run; extra args are forwarded, e.g.:
rem   publish-win-x64-installer.bat -InnoSetupCompiler "D:\Tools\Inno Setup 6\ISCC.exe" -WorkRoot "E:\temp_files\vex-windows"
setlocal
set "SCRIPT_DIR=%~dp0"

echo [Vex] Building win-x64 AOT installer (AOT publish + Inno Setup + SHA-256) ...
where pwsh >nul 2>nul && (set "PS=pwsh") || (set "PS=powershell")
%PS% -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%package_windows.ps1" %*
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
    echo.
    echo [FAILED] installer build exit code %EXITCODE%
) else (
    echo.
    echo [OK] installer and checksums: repo root\artifacts\release\
)
pause
exit /b %EXITCODE%
