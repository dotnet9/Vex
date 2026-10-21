@echo off
setlocal
echo Local publishing builds the win-x64 NativeAOT application and Windows installer.
echo All five platforms are built on their native GitHub Actions runners when a release tag is pushed.
call "%~dp0package_win_x64.bat" %*
exit /b %ERRORLEVEL%
