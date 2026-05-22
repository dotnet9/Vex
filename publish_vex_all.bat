@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\Vex\Vex.csproj"

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
dotnet publish "%PROJECT%" -c Release -f %~2 -r %~3 /p:PublishProfile=%~1
exit /b %ERRORLEVEL%
