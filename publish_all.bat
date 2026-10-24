@echo off
setlocal
where pwsh >nul 2>nul
if errorlevel 1 (
    powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" %*
) else (
    pwsh -NoLogo -NoProfile -File "%~dp0publish.ps1" %*
)
exit /b %ERRORLEVEL%
