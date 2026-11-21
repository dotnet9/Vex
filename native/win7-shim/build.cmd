@echo off
rem 重新编译 Win7 垫片 DLL（仅在修改 synch_shim.c 后需要执行）。
rem 使用 vswhere 动态定位 MSVC——不写死 VS 安装路径。
rem 产物 api-ms-win-core-synch-l1-2-0.dll 输出到本目录。
setlocal
set "SCRIPT_DIR=%~dp0"

rem vswhere 由 VS Installer 自带，路径固定
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"

if not defined VSPATH (
    echo [ERROR] 未找到 Visual Studio（需要 C++ 工具集）
    exit /b 1
)

echo [INFO] VS: %VSPATH%
call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1
cl /LD /O2 /MT "%SCRIPT_DIR%synch_shim.c" "%SCRIPT_DIR%shim.def" /Fe:"%SCRIPT_DIR%api-ms-win-core-synch-l1-2-0.dll"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] 编译失败
    exit /b 1
)
echo [OK] %SCRIPT_DIR%api-ms-win-core-synch-l1-2-0.dll
