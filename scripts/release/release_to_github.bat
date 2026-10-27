@echo off
setlocal
set "PWSH="
for /f "delims=" %%P in ('where pwsh 2^>nul') do (
    if not defined PWSH set "PWSH=%%P"
)
if not defined PWSH if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" set "PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
if not defined PWSH if exist "%LocalAppData%\Microsoft\WindowsApps\pwsh.exe" set "PWSH=%LocalAppData%\Microsoft\WindowsApps\pwsh.exe"
if not defined PWSH (
    echo GitHub release requires PowerShell 7 ^(pwsh^). Install it, then run this command again.
    exit /b 1
)
"%PWSH%" -NoLogo -NoProfile -File "%~dp0release_to_github.ps1" %*
exit /b %ERRORLEVEL%
