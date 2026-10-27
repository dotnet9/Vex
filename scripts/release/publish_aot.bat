@echo off
rem publish_aot.bat
rem Show AOT platforms or create Native AOT payloads and versioned ZIP archives.
setlocal
set "PWSH="
for /f "delims=" %%P in ('where pwsh 2^>nul') do (
    if not defined PWSH set "PWSH=%%P"
)
if not defined PWSH if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" set "PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
if not defined PWSH if exist "%LocalAppData%\Microsoft\WindowsApps\pwsh.exe" set "PWSH=%LocalAppData%\Microsoft\WindowsApps\pwsh.exe"
if not defined PWSH (
    echo AOT publishing requires PowerShell 7 ^(pwsh^). Install it, then run this command again.
    exit /b 1
)
"%PWSH%" -NoLogo -NoProfile -File "%~dp0publish_aot.ps1" %*
exit /b %ERRORLEVEL%
