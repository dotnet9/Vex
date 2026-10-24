@echo off
rem publish_native.bat
rem 作用：为本机 Native AOT 单文件发布定位 PowerShell 7，再调用同目录 publish_native.ps1。
rem 区别：不调用 publish.ps1，不制作 EXE/DEB/DMG，也不生成发布说明。
rem       publish_all.bat 负责安装包；prepare_release_notes.bat 只整理说明文本。
setlocal
set "PWSH="
for /f "delims=" %%P in ('where pwsh 2^>nul') do (
    if not defined PWSH set "PWSH=%%P"
)
if not defined PWSH if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" set "PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
if not defined PWSH if exist "%LocalAppData%\Microsoft\WindowsApps\pwsh.exe" set "PWSH=%LocalAppData%\Microsoft\WindowsApps\pwsh.exe"
if not defined PWSH (
    echo Native publish requires PowerShell 7 ^(pwsh^). Install it, then run this command again.
    exit /b 1
)
"%PWSH%" -NoLogo -NoProfile -File "%~dp0publish_native.ps1" %*
exit /b %ERRORLEVEL%
