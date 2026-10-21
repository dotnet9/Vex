@echo off
setlocal
pwsh -NoLogo -NoProfile -File "%~dp0scripts\package_windows.ps1" %*
exit /b %ERRORLEVEL%
